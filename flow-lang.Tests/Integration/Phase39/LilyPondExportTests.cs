using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Notation;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase39;

/// <summary>
/// Phase 39 Plan 39-02 LILY-01 — acceptance facts for <c>writeLilyPond</c>.
/// Builds SongData programmatically and verifies the LilyPond emit's
/// version header, structural shape, Dutch pitch convention, articulation
/// table per D-v1.5-08, microtonal comments per D-39-12, slur grouping
/// per D-39-07, and two-run cmp-clean determinism.
/// </summary>
[Collection("FlowScripts")]
public class LilyPondExportTests : IDisposable
{
    private readonly string _tmpDir;

    public LilyPondExportTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tmpDir = Path.Combine(Path.GetTempPath(), $"p39_02_ly_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { Directory.Delete(_tmpDir, recursive: true); } catch { /* best-effort */ }
    }

    private static int Quarter => (int)NoteValueType.Value.QUARTER;

    private static MusicalNoteData QN(char name, int octave, int alteration = 0,
        Articulation art = Articulation.Normal, double? cents = null)
    {
        return new MusicalNoteData(name, octave, alteration, Quarter, false,
            centOffset: cents, isTied: false, velocity: 0.63, articulation: art);
    }

    private static SongData BuildSong(IEnumerable<MusicalNoteData>? notes = null,
        string sequenceName = "piano", string? key = "Cmajor", int num = 4, int denom = 4)
    {
        var ts = new TimeSignatureData(num, denom);
        var noteList = (notes ?? new[] { QN('C', 4), QN('D', 4), QN('E', 4), QN('F', 4) }).ToList();
        var bar = new BarData(noteList, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = key };
        var sequences = new Dictionary<string, SequenceData> { [sequenceName] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        return new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });
    }

    [Fact]
    public void EmitsVersionHeader()
    {
        var song = BuildSong();
        string path = Path.Combine(_tmpDir, "version.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.StartsWith("\\version \"2.24.0\"", content);
    }

    [Fact]
    public void EmitsScoreLayoutMidiBlocks()
    {
        var song = BuildSong();
        string path = Path.Combine(_tmpDir, "blocks.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("\\score {", content);
        Assert.Contains("\\layout { }", content);
        Assert.Contains("\\midi { }", content);
    }

    [Fact]
    public void PitchEmitDutchConvention_CSharp4Cis()
    {
        var song = BuildSong(new[]
        {
            QN('C', 4, alteration: 1), QN('D', 4), QN('E', 4), QN('F', 4)
        });
        string path = Path.Combine(_tmpDir, "cis.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("cis'", content);
    }

    [Fact]
    public void PitchEmitDutchConvention_BFlat3Bes()
    {
        var song = BuildSong(new[]
        {
            QN('B', 3, alteration: -1), QN('C', 4), QN('D', 4), QN('E', 4)
        });
        string path = Path.Combine(_tmpDir, "bes.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("bes", content);
    }

    [Fact]
    public void PitchEmitOctaveMarker_D5DoubleQuote()
    {
        var song = BuildSong(new[]
        {
            QN('D', 5), QN('C', 4), QN('C', 4), QN('C', 4)
        });
        string path = Path.Combine(_tmpDir, "octave.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("d''", content);
    }

    [Fact]
    public void MultiSequenceEmitsMultipleStaffs()
    {
        var ts = new TimeSignatureData(4, 4);
        var pianoSeq = new SequenceData();
        pianoSeq.AddBar(new BarData(new List<MusicalNoteData>
            { QN('C', 4), QN('D', 4), QN('E', 4), QN('F', 4) }, ts));
        var violinSeq = new SequenceData();
        violinSeq.AddBar(new BarData(new List<MusicalNoteData>
            { QN('G', 4), QN('A', 4), QN('B', 4), QN('C', 5) }, ts));
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData>
            { ["piano"] = pianoSeq, ["violin"] = violinSeq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "multi.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("\\new Staff = \"piano\"", content);
        Assert.Contains("\\new Staff = \"violin\"", content);
    }

    [Fact]
    public void VoiceBlocksEmitNewVoiceSiblings()
    {
        var ts = new TimeSignatureData(4, 4);
        var v1 = new BarData(new List<MusicalNoteData>
            { QN('C', 4), QN('D', 4), QN('E', 4), QN('F', 4) }, ts);
        var v2 = new BarData(new List<MusicalNoteData>
            { QN('C', 5), QN('D', 5), QN('E', 5), QN('F', 5) }, ts);
        var parent = new BarData(new List<MusicalNoteData>(), ts);
        parent.ParallelVoices = new List<BarData> { v1, v2 };

        var seq = new SequenceData();
        seq.AddBar(parent);
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["piano"] = seq };
        var section = new SectionData("main", sequences, ctx, null);
        var song = new SongData(
            new List<SongSectionRef> { new SongSectionRef("main", 1) },
            new Dictionary<string, SectionData> { ["main"] = section });

        string path = Path.Combine(_tmpDir, "voices.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        int countNewVoice = System.Text.RegularExpressions.Regex.Matches(content, "\\\\new Voice").Count;
        Assert.True(countNewVoice >= 2, $"Expected ≥2 \\new Voice declarations; got {countNewVoice}");
        Assert.Contains("\\\\", content);  // the voice separator
    }

    [Fact]
    public void MicrotonalEmitsCentComment_Plus50()
    {
        var song = BuildSong(new[]
        {
            QN('C', 4, cents: 50.0), QN('D', 4), QN('E', 4), QN('F', 4)
        });
        string path = Path.Combine(_tmpDir, "cents_plus.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("% +50c", content);
    }

    [Fact]
    public void MicrotonalEmitsCentComment_Minus25()
    {
        var song = BuildSong(new[]
        {
            QN('C', 4, cents: -25.0), QN('D', 4), QN('E', 4), QN('F', 4)
        });
        string path = Path.Combine(_tmpDir, "cents_minus.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("% -25c", content);
    }

    [Fact]
    public void TwoRunCmpClean_ByteIdentical()
    {
        var song = BuildSong();
        string a = Path.Combine(_tmpDir, "a.ly");
        string b = Path.Combine(_tmpDir, "b.ly");
        LilyPondExport.WriteLilyPond(a, song);
        LilyPondExport.WriteLilyPond(b, song);
        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
    }

    [Fact]
    public void ArticulationsEmitPerD_v1_5_08()
    {
        var notes = new List<MusicalNoteData>
        {
            QN('C', 4, art: Articulation.Accent),
            QN('D', 4, art: Articulation.Marcato),
            QN('E', 4, art: Articulation.Staccato),
            QN('F', 4, art: Articulation.Tenuto),
        };
        var song = BuildSong(notes);
        string path = Path.Combine(_tmpDir, "art.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("->", content);   // Accent
        Assert.Contains("-^", content);   // Marcato
        Assert.Contains("-.", content);   // Staccato
        Assert.Contains("--", content);   // Tenuto
    }

    [Fact]
    public void SforzandoEmitsSfzMacro()
    {
        var notes = new List<MusicalNoteData>
        {
            QN('C', 4, art: Articulation.Sforzando),
            QN('D', 4), QN('E', 4), QN('F', 4)
        };
        var song = BuildSong(notes);
        string path = Path.Combine(_tmpDir, "sfz.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("\\sfz", content);
    }

    [Fact]
    public void LegatoEmitsSlurParens_ThreeRun()
    {
        var notes = new List<MusicalNoteData>
        {
            QN('C', 4, art: Articulation.Legato),
            QN('D', 4, art: Articulation.Legato),
            QN('E', 4, art: Articulation.Legato),
            QN('F', 4, art: Articulation.Normal),
        };
        var song = BuildSong(notes);
        string path = Path.Combine(_tmpDir, "leg_run.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        Assert.Contains("(", content);
        Assert.Contains(")", content);
    }

    [Fact]
    public void SingleLegatoNoteGetsNoSlurParens()
    {
        var notes = new List<MusicalNoteData>
        {
            QN('C', 4, art: Articulation.Legato),
            QN('D', 4, art: Articulation.Normal),
            QN('E', 4, art: Articulation.Normal),
            QN('F', 4, art: Articulation.Normal),
        };
        var song = BuildSong(notes);
        string path = Path.Combine(_tmpDir, "leg_single.ly");
        LilyPondExport.WriteLilyPond(path, song);
        string content = File.ReadAllText(path);
        // No slur parens — single Legato unmarked per D-39-07.
        Assert.DoesNotContain("(", content);
        Assert.DoesNotContain(")", content);
    }
}
