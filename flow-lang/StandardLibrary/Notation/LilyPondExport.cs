using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 Plan 39-02 LILY-01 — exports a Flow <see cref="SongData"/> to a
/// LilyPond <c>.ly</c> source file compatible with <c>lilypond 2.24+</c>.
///
/// <para>
/// Pure <see cref="StringBuilder"/> composition (Pitfall 6 — deterministic
/// output). Walks the same SongData tree as <see cref="MusicXmlExport"/>;
/// reuses <see cref="InstrumentRouting"/> (for staff naming) and
/// <see cref="ArticulationEmit.ToLilyPond"/> (per D-v1.5-08 articulation
/// table).
/// </para>
///
/// <para>
/// Structure:
/// <code>
/// \version "2.24.0"
/// \score {
///   &lt;&lt;
///     \new Staff = "piano" {
///       \tempo 4 = 120
///       \time 4/4
///       \key c \major
///       { c'4 d'4 e'4 f'4 | }
///     }
///   &gt;&gt;
///   \layout { }
///   \midi { }
/// }
/// </code>
/// </para>
///
/// <para>
/// Voice blocks (Phase 28 <c>bar.ParallelVoices</c>) emit as <c>\new Voice</c>
/// siblings inside a per-bar <c>&lt;&lt; { v1 } \\ { v2 } &gt;&gt;</c> block
/// per D-39-13.
/// </para>
///
/// <para>
/// Microtonal pitches (D-39-12): nearest 12-TET note with <c>% +Nc</c>
/// comment alongside (Pitfall 2 — LilyPond's native quarter-tone notation
/// is too coarse; cent-precision needs Scheme custom accidentals which
/// raise complexity beyond v1.5).
/// </para>
///
/// <para>
/// LilyPond <c>\midi { }</c> block kept per Claude's Discretion (D-39-13
/// section) — matches the LilyPond user-base expectation. Composer can
/// post-edit to strip.
/// </para>
/// </summary>
public static class LilyPondExport
{
    /// <summary>
    /// Write a SongData to a LilyPond .ly file.
    /// </summary>
    public static void WriteLilyPond(string filepath, SongData song)
    {
        if (string.IsNullOrWhiteSpace(filepath))
            throw new System.ArgumentException("LilyPond filepath cannot be null or empty");

        var sb = new StringBuilder();

        // D-39-14 — fixed version header
        sb.Append("\\version \"2.24.0\"\n");

        // Determine global context from the first section
        double bpm = 120.0;
        int timeSigNumerator = 4;
        int timeSigDenominator = 4;
        string? key = null;
        if (song.Sections.Count > 0)
        {
            var firstSectionRef = song.Sections[0];
            if (song.SectionRegistry.TryGetValue(firstSectionRef.Name, out var firstSection))
            {
                var ctx = firstSection.Context;
                if (ctx != null)
                {
                    bpm = ctx.Tempo ?? bpm;
                    if (ctx.TimeSignature != null)
                    {
                        timeSigNumerator = ctx.TimeSignature.Numerator;
                        timeSigDenominator = ctx.TimeSignature.Denominator;
                    }
                    key = ctx.Key;
                }
            }
        }

        // Collect unique sequence names in first-occurrence order across sections
        var uniqueSequenceNames = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                continue;
            foreach (var seqName in sectionData.Sequences.Keys)
                if (seen.Add(seqName)) uniqueSequenceNames.Add(seqName);
        }

        sb.Append("\\score {\n");
        sb.Append("  <<\n");

        // D-39-13 — one \new Staff per unique Sequence
        foreach (var seqName in uniqueSequenceNames)
        {
            string staffName = InstrumentRouting.StripSamplerPrefix(seqName);
            sb.Append($"    \\new Staff = \"{EscapeIdentifier(staffName)}\" {{\n");
            sb.Append($"      \\tempo 4 = {(int)System.Math.Round(bpm)}\n");
            sb.Append($"      \\time {timeSigNumerator}/{timeSigDenominator}\n");
            string keyLine = KeyToLilyPond(key);
            if (!string.IsNullOrEmpty(keyLine))
                sb.Append($"      {keyLine}\n");

            // Walk all sections and emit this sequence's bars in order
            foreach (var sectionRef in song.Sections)
            {
                if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                    continue;
                if (!sectionData.Sequences.TryGetValue(seqName, out var sequence))
                    continue;
                int sectionTimeSigDenom = sectionData.Context?.TimeSignature?.Denominator ?? timeSigDenominator;
                for (int rep = 0; rep < sectionRef.RepeatCount; rep++)
                {
                    foreach (var bar in sequence.Bars)
                    {
                        EmitBar(sb, bar, sectionTimeSigDenom);
                    }
                }
            }

            sb.Append("    }\n");
        }

        sb.Append("  >>\n");
        sb.Append("  \\layout { }\n");
        sb.Append("  \\midi { }\n");
        sb.Append("}\n");

        File.WriteAllText(filepath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Emit a single bar's notes, handling voice-block dispatch + bar-line trailing.
    /// </summary>
    private static void EmitBar(StringBuilder sb, BarData bar, int timeSigDenom)
    {
        int barTimeSigDenom = bar.TimeSignature?.Denominator ?? timeSigDenom;

        if (bar.ParallelVoices != null && bar.ParallelVoices.Count > 0)
        {
            // D-39-13 — voice blocks become \new Voice siblings inside << ... \\ ... >>
            sb.Append("      <<\n");
            for (int v = 0; v < bar.ParallelVoices.Count; v++)
            {
                var voiceBar = bar.ParallelVoices[v];
                int voiceTimeSigDenom = voiceBar.TimeSignature?.Denominator ?? barTimeSigDenom;
                sb.Append("        \\new Voice { ");
                EmitNoteRun(sb, voiceBar.MusicalNotes, voiceTimeSigDenom);
                sb.Append("}\n");
                if (v < bar.ParallelVoices.Count - 1)
                    sb.Append("        \\\\\n");
            }
            sb.Append("      >>\n");
        }
        else
        {
            sb.Append("      { ");
            EmitNoteRun(sb, bar.MusicalNotes, barTimeSigDenom);
            sb.Append("| }\n");
        }
    }

    /// <summary>
    /// Emit a sequential note run. Implements D-39-07 Legato slur grouping
    /// via per-voice scan-ahead: runs of ≥2 consecutive
    /// <see cref="Articulation.Legato"/> notes become <c>(...)</c> slur
    /// pairs; singletons unmarked.
    /// </summary>
    private static void EmitNoteRun(StringBuilder sb, IReadOnlyList<MusicalNoteData> notes, int timeSigDenom)
    {
        // First pass — identify Legato runs
        var slurStartAt = new HashSet<int>();
        var slurStopAt = new HashSet<int>();
        int runStart = -1;
        for (int i = 0; i < notes.Count; i++)
        {
            bool isLegato = !notes[i].IsRest && ArticulationEmit.RequiresSlur(notes[i].Articulation);
            if (isLegato)
            {
                if (runStart < 0) runStart = i;
            }
            else
            {
                if (runStart >= 0)
                {
                    int runEnd = i - 1;
                    if (runEnd > runStart)
                    {
                        slurStartAt.Add(runStart);
                        slurStopAt.Add(runEnd);
                    }
                    runStart = -1;
                }
            }
        }
        if (runStart >= 0)
        {
            int runEnd = notes.Count - 1;
            if (runEnd > runStart)
            {
                slurStartAt.Add(runStart);
                slurStopAt.Add(runEnd);
            }
        }

        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            if (note.IsRest)
            {
                string restDur = LilyPondPitch.ToLilyPondDuration(note.DurationValue, note.IsDotted);
                sb.Append($"r{restDur} ");
                continue;
            }

            string pitch = LilyPondPitch.ToLilyPondPitch(note.NoteName, note.Alteration, note.Octave);
            string dur = LilyPondPitch.ToLilyPondDuration(note.DurationValue, note.IsDotted);
            sb.Append(pitch);
            sb.Append(dur);

            // Articulation suffix (per D-v1.5-08)
            string? art = ArticulationEmit.ToLilyPond(note.Articulation);
            if (art != null)
                sb.Append(art);

            // Slur grouping (D-39-07)
            if (slurStartAt.Contains(i))
                sb.Append('(');
            if (slurStopAt.Contains(i))
                sb.Append(')');

            // Microtonal comment (D-39-12)
            if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0)
            {
                int cents = (int)System.Math.Round(note.CentOffset.Value);
                string sign = cents >= 0 ? "+" : "";
                sb.Append($" % {sign}{cents}c");
            }

            sb.Append(' ');
        }
    }

    /// <summary>
    /// Map Flow's key string ("Cmajor" / "Aminor" / "Fsharpmajor" / etc.)
    /// to LilyPond's <c>\key {root} \{major|minor}</c> declaration. Returns
    /// empty string for unrecognized keys (charitable per D-v1.5-05 — LilyPond
    /// then assumes C major).
    /// </summary>
    private static string KeyToLilyPond(string? key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (!MidiExport.KeySignatureMap.TryGetValue(key, out var keySig))
            return string.Empty;

        string lower = key.ToLowerInvariant();
        char letter = lower.Length > 0 ? lower[0] : 'c';
        string accidental = "";
        if (lower.Length >= 6 && lower.Substring(1, 5) == "sharp")
            accidental = "is";
        else if (lower.Length >= 2 && lower[1] == 'b')
            accidental = "es";

        string mode = keySig.minor == 1 ? "minor" : "major";
        return $"\\key {letter}{accidental} \\{mode}";
    }

    /// <summary>
    /// Escape LilyPond identifier characters. Sequence names like
    /// <c>"piano LH"</c> with spaces would break the engraver — sanitize
    /// to alphanumerics + underscore.
    /// </summary>
    private static string EscapeIdentifier(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.ToString();
    }
}
