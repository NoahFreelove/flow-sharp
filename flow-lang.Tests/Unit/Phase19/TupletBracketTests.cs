using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Unit.Phase19;

/// <summary>
/// TUP-01 / TUP-02 / TUP-03 — bracket-form tuplet acceptance Facts.
/// These pin DurationFraction values produced by NoteStreamCompiler in
/// quarter-note units (music21 convention per Phase 18 18-02 SUMMARY).
///
/// SPEC's "1/12 whole" prose translates to 1/3 quarter (1/12 × 4 = 1/3).
/// All Facts use the quarter-note-unit form because that is what
/// MusicalNoteData.DurationFraction stores per Phase 18.
/// </summary>
public class TupletBracketTests
{
    private static SequenceData CompileNoteStream(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");

        // Top-level statement is an ExpressionStatement wrapping a NoteStreamExpression.
        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        var noteStream = (FlowLang.Ast.Expressions.NoteStreamExpression)stmt.Expression;

        var compiler = new NoteStreamCompiler();
        var ctx = new MusicalContext { TimeSignature = new TimeSignatureData(4, 4) };
        return compiler.Compile(noteStream, ctx);
    }

    private static (bool hasErrors, string formatted) TryCompileNoteStream(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        parser.Parse();
        return (reporter.HasErrors, reporter.FormatErrors());
    }

    [Fact]
    public void TripletQuarterGroup_ProducesThreeOneTwelfthNotes()
    {
        // TUP-01: | {3:2 C4 D4 E4}q | → 3 notes each with DurationFraction = 1/3 quarter (= 1/12 whole)
        var seq = CompileNoteStream("| {3:2 C4 D4 E4}q |");
        var bar = seq.Bars[0];
        Assert.Equal(3, bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            Assert.NotNull(note.DurationFraction);
            Assert.Equal(new Fraction(1, 3), note.DurationFraction);
        }
        // GetBeats sum = 3 × (1/3 × 4 / 4) = 1.0 quarter beat
        double sum = bar.MusicalNotes.Sum(n => n.GetBeats(4));
        Assert.Equal(1.0, sum, 10);
    }

    [Fact]
    public void ShorthandThree_EquivalentToThreeTwo()
    {
        // TUP-02: {3 ...}q ≡ {3:2 ...}q
        var seqShort = CompileNoteStream("| {3 C4 D4 E4}q |");
        var seqExplicit = CompileNoteStream("| {3:2 C4 D4 E4}q |");
        Assert.Equal(seqExplicit.Bars[0].MusicalNotes.Count, seqShort.Bars[0].MusicalNotes.Count);
        for (int i = 0; i < seqShort.Bars[0].MusicalNotes.Count; i++)
        {
            Assert.Equal(seqExplicit.Bars[0].MusicalNotes[i].DurationFraction,
                         seqShort.Bars[0].MusicalNotes[i].DurationFraction);
        }
    }

    [Fact]
    public void ShorthandFive_LookupTableLocked()
    {
        // TUP-02 LOCKED entry: {5 ...}q → 5:4 → each note 1/5 quarter
        var seq = CompileNoteStream("| {5 C4 D4 E4 F4 G4}q |");
        var bar = seq.Bars[0];
        Assert.Equal(5, bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            Assert.Equal(new Fraction(1, 5), note.DurationFraction);
        }
    }

    [Fact]
    public void ShorthandSeven_LookupTableLocked()
    {
        // TUP-02 LOCKED entry: {7 ...}q → 7:4 → each note 1/7 quarter
        var seq = CompileNoteStream("| {7 C4 D4 E4 F4 G4 A4 B4}q |");
        var bar = seq.Bars[0];
        Assert.Equal(7, bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            Assert.Equal(new Fraction(1, 7), note.DurationFraction);
        }
    }

    [Fact]
    public void ShorthandTwelve_RaisesParseError()
    {
        // TUP-02: counts ≥ 12 are out of music21 lookup-table bounds
        var (hasErrors, formatted) = TryCompileNoteStream("| {12 C4 D4 E4 F4 G4 A4 B4 C5 D5 E5 F5 G5}q |");
        Assert.True(hasErrors);
        Assert.Contains("counts 2-11", formatted);
    }

    [Fact]
    public void TupletWithoutDurationSuffix_RaisesParseError()
    {
        // CONTEXT D-04 / SPEC TUP-05 D-06 lock: tuplet bracket REQUIRES explicit duration suffix
        var (hasErrors, formatted) = TryCompileNoteStream("| {3:2 C4 D4 E4} |");
        Assert.True(hasErrors);
        Assert.Contains("Tuplet bracket requires explicit duration suffix", formatted);
    }

    [Fact]
    public void NestedTriplet_OuterAndInnerComposeViaScaleAccumulation()
    {
        // TUP-03: | {3:2 C4 {3:2 D4 E4 F4}q G4}h |
        // Outer: half-bracket, 3 slots → each outer slot = (h / 3) = 2/3 quarter = 1/6 whole
        // Inner triplet on the middle outer slot: each inner = (2/3 quarter) / 3 = 2/9 quarter = 1/18 whole
        // Output order: C4 (outer slot 1), D4/E4/F4 (inner slots), G4 (outer slot 3)
        var seq = CompileNoteStream("| {3:2 C4 {3:2 D4 E4 F4}q G4}h |");
        var bar = seq.Bars[0];
        Assert.Equal(5, bar.MusicalNotes.Count);

        // C4 — outer slot
        Assert.Equal(new Fraction(2, 3), bar.MusicalNotes[0].DurationFraction);
        Assert.Equal('C', bar.MusicalNotes[0].NoteName);

        // D4, E4, F4 — inner slots
        Assert.Equal(new Fraction(2, 9), bar.MusicalNotes[1].DurationFraction);
        Assert.Equal(new Fraction(2, 9), bar.MusicalNotes[2].DurationFraction);
        Assert.Equal(new Fraction(2, 9), bar.MusicalNotes[3].DurationFraction);
        Assert.Equal('D', bar.MusicalNotes[1].NoteName);
        Assert.Equal('E', bar.MusicalNotes[2].NoteName);
        Assert.Equal('F', bar.MusicalNotes[3].NoteName);

        // G4 — outer slot
        Assert.Equal(new Fraction(2, 3), bar.MusicalNotes[4].DurationFraction);
        Assert.Equal('G', bar.MusicalNotes[4].NoteName);

        // Sum check: 2/3 + 2/9 + 2/9 + 2/9 + 2/3 = 4/3 + 6/9 = 4/3 + 2/3 = 6/3 = 2 quarters = one half ✓
        double sum = bar.MusicalNotes.Sum(n => n.GetBeats(4));
        Assert.Equal(2.0, sum, 10);
    }

    [Fact]
    public void NoteStreamCompiler_NonTupletPath_DurationFractionStaysNull()
    {
        // Phase 18 dormancy regression gate: existing | C4q D4q | path produces null DurationFraction.
        // If THIS Fact fails, the new compiler dispatch is leaking DurationFraction into non-tuplet output —
        // would break Phase 18 ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests.cs.
        var seq = CompileNoteStream("| C4q D4q |");
        var bar = seq.Bars[0];
        Assert.Equal(2, bar.MusicalNotes.Count);
        Assert.Null(bar.MusicalNotes[0].DurationFraction);
        Assert.Null(bar.MusicalNotes[1].DurationFraction);
    }
}
