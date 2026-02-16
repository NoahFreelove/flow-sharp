using System.Text;
using FlowMidi.Midi;

namespace FlowMidi.Conversion;

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
        { (7, false), "Csharpmajor" },{ (7, true), "Gsharpminor" },
        { (-1, false), "Fmajor" },    { (-1, true), "Dminor" },
        { (-2, false), "Bbmajor" },   { (-2, true), "Gminor" },
        { (-3, false), "Ebmajor" },   { (-3, true), "Cminor" },
        { (-4, false), "Abmajor" },   { (-4, true), "Fminor" },
        { (-5, false), "Dbmajor" },   { (-5, true), "Bbminor" },
        { (-6, false), "Gbmajor" },   { (-6, true), "Ebminor" },
        { (-7, false), "Cbmajor" },   { (-7, true), "Abminor" },
    };

    public static string Generate(MidiFile midi, QuantizeResult quantizeResult, string sourceFileName)
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
        sb.AppendLine();

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

        if (drumTracks.Count > 0)
        {
            sb.AppendLine($"{indent}Note: Drum track(s) skipped (Flow uses different drum notation)");
            sb.AppendLine();
        }

        // Use sections + Song for all cases (renderSong is the standard API)
        var trackNames = new List<string>();

        foreach (var track in playableTracks)
        {
            string sectionName = SanitizeVarName(track.Name);
            // Deduplicate section names
            string uniqueName = sectionName;
            int suffix = 2;
            while (trackNames.Contains(uniqueName))
                uniqueName = $"{sectionName}_{suffix++}";
            trackNames.Add(uniqueName);

            sb.AppendLine($"{indent}section {uniqueName} {{");
            string sectionIndent = indent + "    ";
            string seqVar = uniqueName + "_seq";
            WriteSequence(sb, sectionIndent, seqVar, track);
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }

        // Song expression
        string songSections = string.Join(" ", trackNames);
        sb.AppendLine($"{indent}Song song = [{songSections}]");
        sb.AppendLine($"{indent}Buffer output = (renderSong song \"piano\")");
        sb.AppendLine($"{indent}(play output)");

        sb.AppendLine();

        // Close context blocks
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

    static void WriteSequence(StringBuilder sb, string indent, string varName, QuantizedTrack track)
    {
        if (track.Bars.Count == 0) return;

        // Check if all notes in the track share the same duration (enables auto-fit)
        bool useAutoFit = CanAutoFit(track);

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
        var parts = new List<string>();
        bool barHasNotes = bar.Elements.Any(e => e is NoteElement or ChordElement);

        foreach (var elem in bar.Elements)
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
                    parts.Add(s);
                    break;
                }

                case RestElement:
                {
                    // Flow rests are just "_" — no duration suffix allowed.
                    // The rest auto-fits to fill available space in the bar.
                    parts.Add("_");
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
