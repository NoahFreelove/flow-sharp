using System.Text;
using FlowMidi.Midi;

namespace FlowMidi.Conversion;

// Generate(..., roundTrip: false) — default; preserves existing flow-midi CLI output
//                                   (with `(play output)` trailer + auto-fit elision +
//                                   `song_part` section + track.Name-derived sequence names).
// Generate(..., roundTrip: true)  — Plan 30-08; emits SPEC-5 round-trip-friendly source:
//                                   no `(play output)`, explicit durations on every note,
//                                   `trackN_seq` index-derived naming, section `roundtrip`,
//                                   `Song s = [roundtrip]` marker, no renderSong/play emission.

/// <summary>
/// Generates idiomatic .flow source code from quantized MIDI data.
/// </summary>
static class FlowGenerator
{
    /// <summary>
    /// Maps MIDI key signature (sharps/flats count + major/minor) to Flow key names.
    /// </summary>
    static readonly Dictionary<(int SharpsFlats, bool IsMinor), string> KeySignatureMap = new()
    {
        { (0, false), "Cmajor" },     { (0, true), "Aminor" },
        { (1, false), "Gmajor" },     { (1, true), "Eminor" },
        { (2, false), "Dmajor" },     { (2, true), "Bminor" },
        { (3, false), "Amajor" },     { (3, true), "Fsharpminor" },
        { (4, false), "Emajor" },     { (4, true), "Csharpminor" },
        { (5, false), "Bmajor" },     { (5, true), "Gsharpminor" },
        { (6, false), "Fsharpmajor" },{ (6, true), "Dsharpminor" },
        { (7, false), "Csharpmajor" },{ (7, true), "Asharpminor" },
        { (-1, false), "Fmajor" },    { (-1, true), "Dminor" },
        { (-2, false), "Bbmajor" },   { (-2, true), "Gminor" },
        { (-3, false), "Ebmajor" },   { (-3, true), "Cminor" },
        { (-4, false), "Abmajor" },   { (-4, true), "Fminor" },
        { (-5, false), "Dbmajor" },   { (-5, true), "Bbminor" },
        { (-6, false), "Gbmajor" },   { (-6, true), "Ebminor" },
        { (-7, false), "Cbmajor" },   { (-7, true), "Abminor" },
    };

    public static string Generate(MidiFile midi, QuantizeResult quantizeResult, string sourceFileName, bool roundTrip = false, bool sustainPedal = true, bool useSfz = false)
    {
        var sb = new StringBuilder();
        var tracks = quantizeResult.Tracks;

        // Filter out drum tracks, empty tracks, and tracks that are almost entirely rests
        var playableTracks = tracks
            .Where(t => !t.IsDrumTrack && t.Bars.Count > 0)
            .Where(t => t.Bars.Any(b => b.Elements.Any(e => e is NoteElement or ChordElement)))
            .ToList();
        var drumTracks = tracks.Where(t => t.IsDrumTrack).ToList();

        if (playableTracks.Count == 0)
        {
            sb.AppendLine($"Note: Converted from {sourceFileName} — no playable tracks found");
            return sb.ToString();
        }

        // Gather metadata from MIDI and quantizer
        var allEvents = midi.Tracks.SelectMany(t => t.Events).ToList();
        var tempoEvent = allEvents.OfType<TempoEvent>().FirstOrDefault();
        var keySigEvent = allEvents.OfType<KeySignatureEvent>().FirstOrDefault();

        int bpm = tempoEvent != null ? (int)Math.Round(tempoEvent.Bpm) : 120;
        int timeSigNum = quantizeResult.TimeSigNumerator;
        int timeSigDen = quantizeResult.TimeSigDenominator;
        string? flowKey = null;
        if (keySigEvent != null)
            KeySignatureMap.TryGetValue((keySigEvent.SharpsFlats, keySigEvent.IsMinor), out flowKey);

        // Header comment
        sb.AppendLine($"Note: Converted from {sourceFileName}");
        sb.AppendLine();

        // Imports
        sb.AppendLine("use \"@std\"");
        sb.AppendLine("use \"@audio\"");
        if (useSfz && !roundTrip)
        {
            sb.AppendLine("use \"@sfz\"");
        }
        sb.AppendLine();

        // SFZ piano binding — resolves #piano against VSCO-CE's UprightPiano.sfz via
        // sfz_root in ~/.config/flow/config.toml. Must render with flow-cli (not
        // flow-interpreter) — only flow-cli loads the XDG config.
        if (useSfz && !roundTrip)
        {
            sb.AppendLine("Sfz piano = (loadSfz #piano)");
            sb.AppendLine();
        }

        // Open context blocks
        string indent = "";

        sb.AppendLine($"tempo {bpm} {{");
        indent = "    ";

        sb.AppendLine($"{indent}timesig {timeSigNum}/{timeSigDen} {{");
        indent = "        ";

        bool hasKey = flowKey != null;
        if (hasKey)
        {
            sb.AppendLine($"{indent}key {flowKey} {{");
            indent = "            ";
        }

        sb.AppendLine();

        // Sustain pedal wrap — when the source is piano-style (default), wrap the
        // section in `sustainPedal { ... }` so the renderer extends every note's
        // buffer by ~4 seconds. This emulates a pianist holding the sustain pedal
        // throughout (typical for Romantic-era piano). Disable with --no-sustain
        // for staccato or non-piano sources.
        if (sustainPedal && !roundTrip)
        {
            sb.AppendLine($"{indent}sustainPedal {{");
            indent += "    ";
        }

        if (drumTracks.Count > 0)
        {
            sb.AppendLine($"{indent}Note: Drum track(s) skipped (Flow uses different drum notation)");
            sb.AppendLine();
        }

        // Emit ONE section containing one Sequence per MIDI track. SongRenderer
        // mixes sequences within a section in parallel (additive), but
        // concatenates sections sequentially. To preserve the original
        // multi-track layering, all tracks must live inside a single section.
        string sectionName;
        if (roundTrip)
        {
            // SPEC-5: round-trip artifact section name (literal "roundtrip").
            sectionName = "roundtrip";
        }
        else
        {
            sectionName = "song_part";
        }
        sb.AppendLine($"{indent}section {sectionName} {{");
        string sectionIndent = indent + "    ";

        var seqNames = new List<string>();
        int trackIdx = 0;
        foreach (var track in playableTracks)
        {
            trackIdx++;
            string seqVar;
            if (roundTrip)
            {
                // SPEC-5: flat track-index naming. Plan 30-09 wires `flow midi2flow`
                // to this branch — the generated source is a round-trip artifact, so
                // sequence names must be stable and source-track-order-derived
                // (not dependent on MIDI track-name strings that may be missing or
                // sanitize-collide).
                seqVar = $"track{trackIdx}_seq";
            }
            else
            {
                // Default path — preserve existing flow-midi CLI behavior:
                // SanitizeVarName + dedup-via-suffix.
                string baseName = SanitizeVarName(track.Name);
                string uniqueName = baseName;
                int suffix = 2;
                while (seqNames.Contains(uniqueName))
                    uniqueName = $"{baseName}_{suffix++}";
                seqNames.Add(uniqueName);
                seqVar = uniqueName + "_seq";
            }

            WriteSequence(sb, sectionIndent, seqVar, track, forceExplicitDurations: roundTrip);
        }

        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        if (roundTrip)
        {
            // SPEC-5: emit the literal `Song s = [roundtrip]` marker only. Plan 30-09's
            // `flow midi2flow` CLI splices `(writeMidi ...)` after this marker so the
            // round-trip artifact stays a pure structural translation — no automatic
            // renderSong / play / writeWav emission here.
            sb.AppendLine($"{indent}Song s = [{sectionName}]");
        }
        else
        {
            // Single section holds all parallel parts.
            sb.AppendLine($"{indent}Song song = [{sectionName}]");
            string instrumentTag = useSfz ? "sampler:piano" : "piano";
            sb.AppendLine($"{indent}Buffer output = (renderSong song \"{instrumentTag}\")");
            sb.AppendLine($"{indent}(play output)");
        }

        sb.AppendLine();

        // Close context blocks
        if (sustainPedal && !roundTrip)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.AppendLine($"{indent}}}");
        }
        if (hasKey)
        {
            indent = "        ";
            sb.AppendLine($"{indent}}}");
        }
        indent = "    ";
        sb.AppendLine($"{indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    static void WriteSequence(StringBuilder sb, string indent, string varName, QuantizedTrack track, bool forceExplicitDurations = false)
    {
        if (track.Bars.Count == 0) return;

        // Check if all notes in the track share the same duration (enables auto-fit).
        // When forceExplicitDurations is true (Plan 30-08 round-trip mode), bypass
        // auto-fit so every note carries its duration suffix verbatim — auto-fit's
        // implicit bar-derived duration reconstruction loses round-trip determinism.
        bool useAutoFit = forceExplicitDurations ? false : CanAutoFit(track);

        sb.Append($"{indent}Sequence {varName} = ");

        // Build bar strings, skipping bars that are all rests at the end
        var barStrings = new List<string>();
        foreach (var bar in track.Bars)
        {
            barStrings.Add(FormatBar(bar, useAutoFit));
        }

        // Build the note stream as one continuous expression.
        // Use line wrapping but keep the stream continuous (no | | empty bars).
        var streamBuilder = new StringBuilder("| ");
        int col = indent.Length + $"Sequence {varName} = ".Length + 2;
        string contIndent = indent + new string(' ', $"Sequence {varName} = ".Length);
        int wrapCol = 100;

        for (int i = 0; i < barStrings.Count; i++)
        {
            string barStr = barStrings[i];
            bool isLast = i == barStrings.Count - 1;
            string suffix = isLast ? " |" : " | ";

            // Check if adding this bar would exceed wrap width
            if (col + barStr.Length + suffix.Length > wrapCol && col > contIndent.Length + 5)
            {
                // Wrap: end current line (no trailing |), continue on next
                sb.AppendLine(streamBuilder.ToString().TrimEnd());
                streamBuilder.Clear();
                streamBuilder.Append(contIndent);
                col = contIndent.Length;
            }

            streamBuilder.Append(barStr);
            streamBuilder.Append(suffix);
            col += barStr.Length + suffix.Length;
        }

        sb.AppendLine(streamBuilder.ToString());
    }

    static string FormatBar(QuantizedBar bar, bool useAutoFit)
    {
        // Single flat note stream per bar — true polyphony is expressed at the
        // Sequence level (one Sequence per voice in a section), not at the bar
        // level via {voice} blocks. The per-bar voice-block path was abandoned
        // because per-bar voice allocation discarded musical voice identity
        // across bars (a melody line could end up in voice 1 of bar 1 and voice 2
        // of bar 2, causing re-attacks at every bar boundary). The track-wide
        // voice allocator in Quantizer.cs now produces one stable Sequence per
        // voice and the FlowGenerator emits them as parallel sequences in one
        // section — Flow's SongRenderer mixes them additively.
        return FormatElements(bar.Elements, useAutoFit);
    }

    static string FormatElements(List<IBarElement> elements, bool useAutoFit)
    {
        var parts = new List<string>();

        foreach (var elem in elements)
        {
            switch (elem)
            {
                case NoteElement note:
                {
                    string s = note.NoteName;
                    if (!useAutoFit)
                    {
                        s += note.DurationSuffix;
                        if (note.IsDotted) s += ".";
                    }
                    if (note.IsTied) s += "~";
                    parts.Add(s);
                    break;
                }

                case ChordElement chord:
                {
                    string notes = string.Join(" ", chord.NoteNames);
                    string s = $"[{notes}]";
                    if (!useAutoFit)
                    {
                        s += chord.DurationSuffix;
                        if (chord.IsDotted) s += ".";
                    }
                    if (chord.IsTied) s += "~";
                    parts.Add(s);
                    break;
                }

                case RestElement rest:
                {
                    // Flow supports both auto-fit rests (`_`) and duration-suffixed
                    // rests (`_ q`, `_ h`, `_ e .` ...). The space before the suffix
                    // is required because `_q` lexes as a single underscore-prefixed
                    // identifier (Flow allows leading underscores in identifiers).
                    if (!useAutoFit && rest.DurationSuffix != null)
                    {
                        string r = "_ " + rest.DurationSuffix;
                        if (rest.IsDotted) r += " .";
                        parts.Add(r);
                    }
                    else
                    {
                        parts.Add("_");
                    }
                    break;
                }
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Checks if all elements in the track have the same duration — if so, we can omit
    /// duration suffixes (auto-fit mode in Flow note streams).
    /// </summary>
    static bool CanAutoFit(QuantizedTrack track)
    {
        string? commonSuffix = null;
        bool? commonDotted = null;

        foreach (var bar in track.Bars)
        {
            foreach (var elem in bar.Elements)
            {
                string suffix;
                bool isDotted;

                switch (elem)
                {
                    case NoteElement n:
                        suffix = n.DurationSuffix;
                        isDotted = n.IsDotted;
                        break;
                    case ChordElement c:
                        suffix = c.DurationSuffix;
                        isDotted = c.IsDotted;
                        break;
                    case RestElement:
                        // Rests are always plain "_" in output, skip for auto-fit check
                        continue;
                    default:
                        continue;
                }

                if (commonSuffix == null)
                {
                    commonSuffix = suffix;
                    commonDotted = isDotted;
                }
                else if (suffix != commonSuffix || isDotted != commonDotted)
                {
                    return false;
                }
            }
        }

        return true;
    }

    static string SanitizeVarName(string name)
    {
        var sb = new StringBuilder();
        bool lastWasUnderscore = false;

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore)
            {
                sb.Append('_');
                lastWasUnderscore = true;
            }
        }

        string result = sb.ToString().Trim('_');

        if (result.Length == 0 || char.IsDigit(result[0]))
            result = "track_" + result;

        return result.ToLowerInvariant();
    }
}
