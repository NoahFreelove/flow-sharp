using System.IO;
using System.Text;
using System.Xml;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 XML-01 — exports a Flow <see cref="SongData"/> to a MusicXML 3.1
/// partwise file consumable by MuseScore (reference consumer per D-v1.5-08),
/// Sibelius, Dorico, Finale, LilyPond, and any other MusicXML-aware engraver.
///
/// <para>
/// One-way emit: MusicXML import is an explicit anti-feature for v1.5 per
/// FEATURES.md anti-feature lock. The XML-02 round-trip CI gate uses
/// <c>mscore --convert-to mxl</c> structural diff (charitable-skip when
/// mscore is absent per D-39-08); see <c>MusicXmlRoundTripTests</c>.
/// </para>
///
/// <para>
/// Determinism contract (Pitfall 6): emit uses
/// <see cref="System.Xml.XmlWriter"/> directly with fixed
/// <c>NewLineChars = "\n"</c> + <c>UTF8Encoding(emitBOM: false)</c> so the
/// same input produces byte-identical output across runs and across .NET
/// patch versions. <see cref="System.Xml.Serialization.XmlSerializer"/>
/// reflection ordering is documented as implementation-defined and would
/// break two-run cmp-clean.
/// </para>
///
/// <para>
/// Architecture: walks the same <c>SongData → Sections → Sequences → Bars
/// → Notes</c> tree as <see cref="MidiExport"/>, reusing
/// <see cref="MidiExport.ComputeRequiredTpqn"/> for divisions and
/// <see cref="MidiExport.KeySignatureMap"/> for fifths. Sequence-name → GM
/// program routing goes through the shared
/// <see cref="InstrumentRouting"/> helper per D-39-20.
/// </para>
/// </summary>
public static class MusicXmlExport
{
    /// <summary>
    /// Write a <see cref="SongData"/> to a MusicXML 3.1 partwise file at the
    /// given path. Throws <see cref="System.ArgumentException"/> on null/empty
    /// path; defers any IO error to the underlying <see cref="XmlWriter"/>.
    /// </summary>
    public static void WriteMusicXml(string filepath, SongData song)
    {
        if (string.IsNullOrWhiteSpace(filepath))
            throw new System.ArgumentException("MusicXML filepath cannot be null or empty");

        int divisions = MidiExport.ComputeRequiredTpqn(song);

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

        // Collect unique sequence names across all sections, preserving first-
        // occurrence order. Mirrors MidiExport SPEC-6 multi-track ordering so
        // both formats name parts identically.
        var uniqueSequenceNames = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                continue;
            foreach (var seqName in sectionData.Sequences.Keys)
            {
                if (seen.Add(seqName))
                    uniqueSequenceNames.Add(seqName);
            }
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        using var writer = XmlWriter.Create(filepath, settings);
        writer.WriteStartDocument();
        writer.WriteDocType(
            "score-partwise",
            "-//Recordare//DTD MusicXML 3.1 Partwise//EN",
            "http://www.musicxml.org/dtds/partwise.dtd",
            null);

        writer.WriteStartElement("score-partwise");
        writer.WriteAttributeString("version", "3.1");

        // <part-list> — one <score-part id="P{N}"> per unique sequence
        writer.WriteStartElement("part-list");
        for (int i = 0; i < uniqueSequenceNames.Count; i++)
        {
            string seqName = uniqueSequenceNames[i];
            string partId = $"P{i + 1}";
            writer.WriteStartElement("score-part");
            writer.WriteAttributeString("id", partId);
            writer.WriteElementString("part-name", InstrumentRouting.StripSamplerPrefix(seqName));
            writer.WriteEndElement();  // score-part
        }
        writer.WriteEndElement();  // part-list

        // Emit one <part> per unique sequence. Within each part, walk all
        // sections (in song.Sections order, honoring SongSectionRef.RepeatCount)
        // and emit measures from that sequence in chronological order.
        for (int i = 0; i < uniqueSequenceNames.Count; i++)
        {
            string seqName = uniqueSequenceNames[i];
            string partId = $"P{i + 1}";
            writer.WriteStartElement("part");
            writer.WriteAttributeString("id", partId);

            int measureNumber = 1;
            bool firstMeasureInPart = true;

            foreach (var sectionRef in song.Sections)
            {
                if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                    continue;
                if (!sectionData.Sequences.TryGetValue(seqName, out var sequence))
                    continue;

                int sectionTimeSigNum = sectionData.Context?.TimeSignature?.Numerator ?? timeSigNumerator;
                int sectionTimeSigDenom = sectionData.Context?.TimeSignature?.Denominator ?? timeSigDenominator;
                double sectionTempo = sectionData.Context?.Tempo ?? bpm;
                string? sectionKey = sectionData.Context?.Key ?? key;

                for (int repeat = 0; repeat < sectionRef.RepeatCount; repeat++)
                {
                    foreach (var bar in sequence.Bars)
                    {
                        WriteMeasure(
                            writer,
                            bar,
                            measureNumber,
                            firstMeasureInPart,
                            divisions,
                            sectionTimeSigNum,
                            sectionTimeSigDenom,
                            sectionTempo,
                            sectionKey);
                        firstMeasureInPart = false;
                        measureNumber++;
                    }
                }
            }

            writer.WriteEndElement();  // part
        }

        writer.WriteEndElement();  // score-partwise
        writer.WriteEndDocument();
    }

    /// <summary>
    /// Emit a single <c>&lt;measure&gt;</c>. The FIRST measure of each part
    /// carries <c>&lt;attributes&gt;</c> (divisions, key, time, clef);
    /// subsequent measures omit the attributes block per MusicXML convention.
    /// </summary>
    private static void WriteMeasure(
        XmlWriter writer,
        BarData bar,
        int measureNumber,
        bool isFirstMeasureInPart,
        int divisions,
        int timeSigNumerator,
        int timeSigDenominator,
        double tempo,
        string? key)
    {
        writer.WriteStartElement("measure");
        writer.WriteAttributeString("number", measureNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (isFirstMeasureInPart)
        {
            writer.WriteStartElement("attributes");
            writer.WriteElementString("divisions",
                divisions.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (key != null && MidiExport.KeySignatureMap.TryGetValue(key, out var keySig))
            {
                writer.WriteStartElement("key");
                writer.WriteElementString("fifths",
                    keySig.sharpsFlats.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteElementString("mode", keySig.minor == 1 ? "minor" : "major");
                writer.WriteEndElement();  // key
            }
            writer.WriteStartElement("time");
            writer.WriteElementString("beats",
                timeSigNumerator.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteElementString("beat-type",
                timeSigDenominator.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();  // time
            writer.WriteStartElement("clef");
            writer.WriteElementString("sign", "G");
            writer.WriteElementString("line", "2");
            writer.WriteEndElement();  // clef
            writer.WriteEndElement();  // attributes

            // Tempo as <direction>
            writer.WriteStartElement("direction");
            writer.WriteAttributeString("placement", "above");
            writer.WriteStartElement("direction-type");
            writer.WriteStartElement("metronome");
            writer.WriteElementString("beat-unit", "quarter");
            writer.WriteElementString("per-minute",
                ((int)System.Math.Round(tempo)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();  // metronome
            writer.WriteEndElement();  // direction-type
            writer.WriteStartElement("sound");
            writer.WriteAttributeString("tempo",
                ((int)System.Math.Round(tempo)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();  // sound
            writer.WriteEndElement();  // direction
        }

        int barTimeSigDenom = bar.TimeSignature?.Denominator ?? timeSigDenominator;

        // Phase 28 voice-block dispatch — when ParallelVoices is set, emit each
        // voice's notes under its own <voice>N</voice> tag in turn. Per
        // MusicXML convention, a <backup duration="N"/> element resets the
        // cursor between voices within the same measure.
        if (bar.ParallelVoices != null && bar.ParallelVoices.Count > 0)
        {
            for (int v = 0; v < bar.ParallelVoices.Count; v++)
            {
                int totalDurationThisVoice = 0;
                var voiceBar = bar.ParallelVoices[v];
                int voiceTimeSigDenom = voiceBar.TimeSignature?.Denominator ?? barTimeSigDenom;
                EmitVoiceNotes(writer, voiceBar.MusicalNotes, voiceNumber: v + 1, divisions,
                    voiceTimeSigDenom, ref totalDurationThisVoice);
                // Backup to start of measure before the next voice (except after the last)
                if (v < bar.ParallelVoices.Count - 1 && totalDurationThisVoice > 0)
                {
                    writer.WriteStartElement("backup");
                    writer.WriteElementString("duration",
                        totalDurationThisVoice.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
            }
        }
        else
        {
            int totalDuration = 0;
            EmitVoiceNotes(writer, bar.MusicalNotes, voiceNumber: 1, divisions, barTimeSigDenom, ref totalDuration);
        }

        writer.WriteEndElement();  // measure
    }

    /// <summary>
    /// Emit a sequential list of notes for one voice. Implements D-39-07 Legato
    /// slur grouping (runs of ≥2 consecutive Legato notes become one
    /// <c>&lt;slur number="N"&gt;</c> span; single Legato notes get no slur)
    /// plus the D-v1.5-08 articulation decision table via
    /// <see cref="ArticulationEmit.ToMusicXml"/>.
    /// </summary>
    private static void EmitVoiceNotes(
        XmlWriter writer,
        IReadOnlyList<MusicalNoteData> notes,
        int voiceNumber,
        int divisions,
        int timeSigDenom,
        ref int totalDuration)
    {
        // First pass: identify Legato runs (≥2 consecutive notes with Articulation.Legato)
        // and assign per-voice slur numbers. We scan and produce per-index slur events:
        //   slurStartAt[i] = N when note i starts a slur with number N
        //   slurStopAt[i]  = N when note i ends a slur with number N
        var slurStartAt = new Dictionary<int, int>();
        var slurStopAt = new Dictionary<int, int>();
        int slurCounter = 0;
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
                // close any open run
                if (runStart >= 0)
                {
                    int runEnd = i - 1;
                    if (runEnd > runStart)
                    {
                        slurCounter++;
                        slurStartAt[runStart] = slurCounter;
                        slurStopAt[runEnd] = slurCounter;
                    }
                    runStart = -1;
                }
            }
        }
        // Tail-end run
        if (runStart >= 0)
        {
            int runEnd = notes.Count - 1;
            if (runEnd > runStart)
            {
                slurCounter++;
                slurStartAt[runStart] = slurCounter;
                slurStopAt[runEnd] = slurCounter;
            }
        }

        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];

            // Sforzando emits a <direction><dynamics><sfz/></dynamics></direction>
            // BEFORE the <note> per D-v1.5-08. Rest notes skip dynamics.
            if (!note.IsRest && note.Articulation == Articulation.Sforzando)
            {
                writer.WriteStartElement("direction");
                writer.WriteAttributeString("placement", "above");
                writer.WriteStartElement("direction-type");
                writer.WriteStartElement("dynamics");
                writer.WriteElementString("sfz", "");
                writer.WriteEndElement();  // dynamics
                writer.WriteEndElement();  // direction-type
                writer.WriteEndElement();  // direction
            }

            writer.WriteStartElement("note");

            if (note.IsChordTone)
            {
                writer.WriteElementString("chord", "");
            }

            if (note.IsRest)
            {
                writer.WriteStartElement("rest");
                writer.WriteEndElement();  // rest
            }
            else
            {
                writer.WriteStartElement("pitch");
                writer.WriteElementString("step",
                    char.ToUpperInvariant(note.NoteName).ToString());
                // <alter> includes Alteration (integer semitones) + CentOffset / 100.0
                // (D-39-06: always decimal cent precision, never text-annotation fallback).
                double alterTotal = note.Alteration + (note.CentOffset ?? 0.0) / 100.0;
                if (alterTotal != 0.0)
                {
                    writer.WriteElementString("alter",
                        alterTotal.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
                }
                writer.WriteElementString("octave",
                    note.Octave.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteEndElement();  // pitch
            }

            double beats = note.GetBeats(timeSigDenom);
            int duration = (int)System.Math.Round(beats * divisions);
            if (duration < 1) duration = 1;  // MusicXML <duration> must be positive
            writer.WriteElementString("duration",
                duration.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!note.IsChordTone)
                totalDuration += duration;

            writer.WriteElementString("voice",
                voiceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // <type> — power-of-2 note value name
            string? typeName = NoteValueToTypeName(note.DurationValue);
            if (typeName != null)
                writer.WriteElementString("type", typeName);

            if (note.IsDotted)
            {
                writer.WriteStartElement("dot");
                writer.WriteEndElement();
            }

            // <notations> block: slurs + articulations.
            string? articulationTag = note.IsRest ? null : ArticulationEmit.ToMusicXml(note.Articulation);
            bool hasSlurStart = slurStartAt.TryGetValue(i, out var startN);
            bool hasSlurStop = slurStopAt.TryGetValue(i, out var stopN);
            if (articulationTag != null || hasSlurStart || hasSlurStop)
            {
                writer.WriteStartElement("notations");
                if (hasSlurStart)
                {
                    writer.WriteStartElement("slur");
                    writer.WriteAttributeString("number",
                        startN.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("type", "start");
                    writer.WriteEndElement();
                }
                if (hasSlurStop)
                {
                    writer.WriteStartElement("slur");
                    writer.WriteAttributeString("number",
                        stopN.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("type", "stop");
                    writer.WriteEndElement();
                }
                if (articulationTag != null)
                {
                    writer.WriteStartElement("articulations");
                    writer.WriteRaw(articulationTag);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();  // notations
            }

            writer.WriteEndElement();  // note
        }
    }

    /// <summary>
    /// Map Flow's <see cref="NoteValueType.Value"/> int to the MusicXML
    /// <c>&lt;type&gt;</c> string. Null when no duration is specified
    /// (charitable per D-v1.5-05 — emit just <c>&lt;duration&gt;</c>).
    /// </summary>
    private static string? NoteValueToTypeName(int? durationValue)
    {
        if (!durationValue.HasValue) return null;
        return durationValue.Value switch
        {
            0 => "whole",
            1 => "half",
            2 => "quarter",
            3 => "eighth",
            4 => "16th",
            5 => "32nd",
            6 => "64th",
            7 => "128th",
            _ => null,
        };
    }
}
