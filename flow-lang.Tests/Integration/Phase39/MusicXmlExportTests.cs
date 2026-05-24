using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Notation;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase39;

/// <summary>
/// Phase 39 Plan 39-01 — XML-01 acceptance facts for <c>writeMusicXML</c>.
/// Builds <see cref="SongData"/> programmatically and verifies the resulting
/// MusicXML 3.1 partwise output is well-formed, deterministic, and faithfully
/// represents the music model.
/// </summary>
[Collection("FlowScripts")]
public class MusicXmlExportTests : IDisposable
{
    private readonly string _tmpDir;

    public MusicXmlExportTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tmpDir = Path.Combine(Path.GetTempPath(), $"p39_01_xml_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { Directory.Delete(_tmpDir, recursive: true); } catch { /* best-effort */ }
    }

    private static int Quarter => (int)NoteValueType.Value.QUARTER;

    private static MusicalNoteData QuarterNote(char name, int octave, int alteration = 0,
        Articulation articulation = Articulation.Normal, double velocity = 0.63, double? cents = null)
    {
        // (char noteName, int octave, int alteration, int? durationValue, bool isRest,
        //  double? centOffset, bool isTied, double velocity, Articulation articulation, ...)
        return new MusicalNoteData(
            name, octave, alteration,
            durationValue: Quarter,
            isRest: false,
            centOffset: cents,
            isTied: false,
            velocity: velocity,
            articulation: articulation);
    }

    private static SongData BuildTrivialSong(string sequenceName = "piano",
        IEnumerable<MusicalNoteData>? notes = null, string? key = "Cmajor", int num = 4, int denom = 4)
    {
        var ts = new TimeSignatureData(num, denom);
        var noteList = (notes ?? new[]
        {
            QuarterNote('C', 4),
            QuarterNote('D', 4),
            QuarterNote('E', 4),
            QuarterNote('F', 4),
        }).ToList();
        var bar = new BarData(noteList, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = key };
        var sequences = new Dictionary<string, SequenceData> { [sequenceName] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var registry = new Dictionary<string, SectionData> { ["main"] = section };
        return new SongData(new List<SongSectionRef> { new SongSectionRef("main", 1) }, registry);
    }

    [Fact]
    public void EmitsWellFormedXml_FromTrivialSong()
    {
        var song = BuildTrivialSong();
        string path = Path.Combine(_tmpDir, "trivial.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);

        Assert.True(File.Exists(path));
        // Parse - any malformed XML throws
        var doc = XDocument.Load(path);
        Assert.NotNull(doc.Root);
        Assert.Equal("score-partwise", doc.Root!.Name.LocalName);
        Assert.Equal("3.1", doc.Root.Attribute("version")?.Value);
    }

    [Fact]
    public void EmitsMultipartScore_FromMultiSequenceSong()
    {
        // Build a SongData with 2 sequences: piano + violin
        var ts = new TimeSignatureData(4, 4);
        var pianoSeq = new SequenceData();
        pianoSeq.AddBar(new BarData(new List<MusicalNoteData> { QuarterNote('C', 4), QuarterNote('D', 4) }, ts));
        var violinSeq = new SequenceData();
        violinSeq.AddBar(new BarData(new List<MusicalNoteData> { QuarterNote('G', 4), QuarterNote('A', 4) }, ts));
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = pianoSeq, ["violin"] = violinSeq };
        var section = new SectionData("main", sequences, ctx, null);
        var registry = new Dictionary<string, SectionData> { ["main"] = section };
        var song = new SongData(new List<SongSectionRef> { new SongSectionRef("main", 1) }, registry);

        string path = Path.Combine(_tmpDir, "multi.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);

        var doc = XDocument.Load(path);
        var scoreParts = doc.Root!.Elements("part-list").Single().Elements("score-part").ToList();
        Assert.Equal(2, scoreParts.Count);
        var parts = doc.Root.Elements("part").ToList();
        Assert.Equal(2, parts.Count);
    }

    [Fact]
    public void TwoRunCmpClean_ByteIdentical()
    {
        var song = BuildTrivialSong();
        string pathA = Path.Combine(_tmpDir, "a.musicxml");
        string pathB = Path.Combine(_tmpDir, "b.musicxml");
        MusicXmlExport.WriteMusicXml(pathA, song);
        MusicXmlExport.WriteMusicXml(pathB, song);
        var bytesA = File.ReadAllBytes(pathA);
        var bytesB = File.ReadAllBytes(pathB);
        Assert.Equal(bytesA, bytesB);
    }

    [Fact]
    public void ArticulationsEmitPerD_v1_5_08_Table()
    {
        // One note per articulation that emits inside <articulations>.
        // Sforzando is verified separately below; Legato/Normal don't emit
        // articulation tags so they aren't in this set.
        var notes = new List<MusicalNoteData>
        {
            QuarterNote('C', 4, articulation: Articulation.Accent),
            QuarterNote('D', 4, articulation: Articulation.Marcato),
            QuarterNote('E', 4, articulation: Articulation.Staccato),
            QuarterNote('F', 4, articulation: Articulation.Tenuto),
        };
        // Single bar with 4 notes (fits 4/4)
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(notes, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "articulations.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);

        string content = File.ReadAllText(path);
        Assert.Contains("<accent />", content.Replace("<accent/>", "<accent />"));  // XmlWriter may add space
        Assert.Contains("<strong-accent", content);
        Assert.Contains("<staccato", content);
        Assert.Contains("<tenuto", content);
    }

    [Fact]
    public void SforzandoEmitsDynamicsDirection()
    {
        var notes = new List<MusicalNoteData>
        {
            QuarterNote('C', 4, articulation: Articulation.Sforzando),
            QuarterNote('D', 4),
            QuarterNote('E', 4),
            QuarterNote('F', 4),
        };
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(notes, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "sforzando.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("<sfz", content);
        Assert.Contains("<dynamics>", content);
    }

    [Fact]
    public void LegatoSlurGrouping_PerD_39_07()
    {
        // 3 consecutive Legato + 1 non-Legato → expect ONE slur start + ONE slur stop
        var notes = new List<MusicalNoteData>
        {
            QuarterNote('C', 4, articulation: Articulation.Legato),
            QuarterNote('D', 4, articulation: Articulation.Legato),
            QuarterNote('E', 4, articulation: Articulation.Legato),
            QuarterNote('F', 4, articulation: Articulation.Normal),
        };
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(notes, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "legato_run.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);
        string content = File.ReadAllText(path);
        // EXACTLY one slur start and one slur stop for the 3-note run
        int startCount = System.Text.RegularExpressions.Regex.Matches(content, "type=\"start\"").Count;
        int stopCount = System.Text.RegularExpressions.Regex.Matches(content, "type=\"stop\"").Count;
        Assert.Equal(1, startCount);
        Assert.Equal(1, stopCount);
    }

    [Fact]
    public void SingleLegatoNoteGetsNoSlur()
    {
        // ONE Legato note → NO slur (single notes don't get slurs per D-39-07)
        var notes = new List<MusicalNoteData>
        {
            QuarterNote('C', 4, articulation: Articulation.Legato),
            QuarterNote('D', 4, articulation: Articulation.Normal),
            QuarterNote('E', 4, articulation: Articulation.Normal),
            QuarterNote('F', 4, articulation: Articulation.Normal),
        };
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(notes, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "legato_single.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);
        string content = File.ReadAllText(path);
        Assert.DoesNotContain("<slur", content);
    }

    [Fact]
    public void VoiceBlocks_EmitVoiceTags()
    {
        // Bar with 2 parallel voices
        var ts = new TimeSignatureData(4, 4);
        var voice1Notes = new List<MusicalNoteData>
        {
            QuarterNote('C', 4), QuarterNote('D', 4), QuarterNote('E', 4), QuarterNote('F', 4)
        };
        var voice2Notes = new List<MusicalNoteData>
        {
            QuarterNote('C', 5), QuarterNote('D', 5), QuarterNote('E', 5), QuarterNote('F', 5)
        };
        var voiceBar1 = new BarData(voice1Notes, ts);
        var voiceBar2 = new BarData(voice2Notes, ts);
        var parentBar = new BarData(new List<MusicalNoteData>(), ts);
        parentBar.ParallelVoices = new List<BarData> { voiceBar1, voiceBar2 };

        var seq = new SequenceData();
        seq.AddBar(parentBar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "voices.musicxml");
        MusicXmlExport.WriteMusicXml(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("<voice>1</voice>", content);
        Assert.Contains("<voice>2</voice>", content);
    }

    [Fact]
    public void InstrumentRoutingDelegatesToShared()
    {
        // Both paths produce the same (gmProgram, channel) — D-39-20.
        Assert.Equal(InstrumentRouting.ResolveGmProgram("violin"),
                     MidiExport.ResolveGmProgram("violin"));
        Assert.Equal((40, 0), InstrumentRouting.ResolveGmProgram("violin"));
        Assert.Equal((0, 9), InstrumentRouting.ResolveGmProgram("drum"));
        Assert.Equal((60, 0), InstrumentRouting.ResolveGmProgram("horn"));  // D-16: horn before brass
    }
}
