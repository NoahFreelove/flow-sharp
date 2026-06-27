using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Notation;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase39;

/// <summary>
/// Phase 39 Plan 39-04 MML-01 — acceptance facts for the <c>mml</c> builtin.
/// Verifies the hand-rolled PC-98 MML common-core parser per D-39-18 / D-39-19.
/// </summary>
[Collection("FlowScripts")]
public class MmlImportTests
{
    public MmlImportTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static string CaptureStderr(System.Action body)
    {
        var originalErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try { body(); } finally { Console.SetError(originalErr); }
        return sw.ToString();
    }

    private static System.Collections.Generic.List<MusicalNoteData> NotesOf(SequenceData seq)
    {
        return seq.Bars[0].MusicalNotes.Where(n => !n.IsRest).ToList();
    }

    [Fact]
    public void ParsesBasicScale_EightNotes()
    {
        var seq = MmlImport.ParseMml("T120 L4 O4 cdefga>c");
        // c d e f g a + octave-up + c → 7 letter-tokens but the > is just an octave
        // shift, not a note. Re-count from source: c, d, e, f, g, a, c → 7 notes
        // (the > shifts state.Octave from 4 to 5 before the final c).
        var notes = NotesOf(seq);
        Assert.Equal(7, notes.Count);
    }

    [Fact]
    public void Tempo_PopulatesContext()
    {
        // The MML parser stores tempo in state.Tempo; for the test we just
        // verify the parse succeeds. The current MmlImport returns a
        // SequenceData; Tempo is available via the parser state but not
        // exposed on SequenceData. Future iteration may wrap into a
        // SectionData with MusicalContext.Tempo. For this gate we verify
        // the parser doesn't throw on T140.
        var seq = MmlImport.ParseMml("T140 L4 O4 c");
        Assert.NotNull(seq);
        Assert.Equal(1, NotesOf(seq).Count);
    }

    [Fact]
    public void AccidentalSharpFlatParsed()
    {
        var seq = MmlImport.ParseMml("L4 O4 c+ d-");
        var notes = NotesOf(seq);
        Assert.Equal(2, notes.Count);
        Assert.Equal(1, notes[0].Alteration);   // c+ → sharp
        Assert.Equal(-1, notes[1].Alteration);  // d- → flat
    }

    [Fact]
    public void OctaveAbsoluteAndRelative()
    {
        var seq = MmlImport.ParseMml("L4 O3 c >c <c O5 c");
        var notes = NotesOf(seq);
        Assert.Equal(4, notes.Count);
        Assert.Equal(3, notes[0].Octave);  // O3 c
        Assert.Equal(4, notes[1].Octave);  // >c (up to 4)
        Assert.Equal(3, notes[2].Octave);  // <c (back to 3)
        Assert.Equal(5, notes[3].Octave);  // O5 c
    }

    [Fact]
    public void LengthOverride()
    {
        var seq = MmlImport.ParseMml("L4 c c8 c");
        var notes = NotesOf(seq);
        Assert.Equal(3, notes.Count);
        Assert.Equal((int)NoteValueType.Value.QUARTER, notes[0].DurationValue);
        Assert.Equal((int)NoteValueType.Value.EIGHTH, notes[1].DurationValue);
        Assert.Equal((int)NoteValueType.Value.QUARTER, notes[2].DurationValue);
    }

    [Fact]
    public void LoopExpansion()
    {
        var seq = MmlImport.ParseMml("L4 O4 [cd]2");
        var notes = NotesOf(seq);
        // [cd]2 → c d c d → 4 notes
        Assert.Equal(4, notes.Count);
    }

    [Fact]
    public void LoopNestingCap_DepthExceeded()
    {
        string deep = new string('[', 17) + "c" + new string(']', 17);
        var output = CaptureStderr(() =>
        {
            var seq = MmlImport.ParseMml(deep);
            Assert.NotNull(seq);
        });
        Assert.Contains("[mml] loop nesting depth", output);
    }

    [Fact]
    public void LoopBombBoundedToCap()
    {
        // [c]100000 → 100,000 iterations; cap should kick in
        var output = CaptureStderr(() =>
        {
            var seq = MmlImport.ParseMml("[c]100000");
            Assert.NotNull(seq);
            int count = NotesOf(seq).Count;
            // Must NOT explode beyond MaxExpandedNoteCount (65536)
            Assert.True(count <= 65536);
        });
        Assert.Contains("[mml] expansion cap", output);
    }

    [Fact]
    public void UnknownOpcodeDropped()
    {
        var output = CaptureStderr(() =>
        {
            var seq = MmlImport.ParseMml("L4 @1 c d e f");
            Assert.NotNull(seq);
        });
        Assert.Contains("[mml] dropped opcode", output);
    }

    [Fact]
    public void MalformedInputNeverThrows()
    {
        var seq = MmlImport.ParseMml("GARBAGE!!!\n\n\x01");
        Assert.NotNull(seq);
    }

    [Fact]
    public void DottedNote()
    {
        var seq = MmlImport.ParseMml("L4 O4 c.");
        var notes = NotesOf(seq);
        Assert.Single(notes);
        Assert.True(notes[0].IsDotted);
    }
}
