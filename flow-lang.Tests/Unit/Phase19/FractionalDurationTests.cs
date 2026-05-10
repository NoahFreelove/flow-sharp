using System.Collections.Generic;
using System.Linq;
using FlowLang.Ast.Expressions;
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
/// TUP-04 (C4/N) + TUP-08 (C4/X:Y[suffix]) — per-note fractional-duration acceptance Facts.
/// All DurationFraction values are pinned in quarter-note units (music21 convention per
/// Phase 18 18-02 SUMMARY). Includes the random-choice colon-collision regression Fact
/// per RESEARCH §Pitfall Phase-19-specific.
/// </summary>
public class FractionalDurationTests
{
    private static SequenceData CompileNoteStream(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");

        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        var noteStream = (NoteStreamExpression)stmt.Expression;

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

    private static NoteStreamExpression ParseToAst(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");
        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        return (NoteStreamExpression)stmt.Expression;
    }

    // ===== TUP-04: C4/N arbitrary-denominator =====

    [Fact]
    public void SlashTwelve_ProducesThreeOneTwelfthNotes()
    {
        // TUP-04: | C4/12 D4/12 E4/12 | → 3 notes each DurationFraction = 1/3 quarter (= 1/12 whole)
        var seq = CompileNoteStream("| C4/12 D4/12 E4/12 |");
        var bar = seq.Bars[0];
        Assert.Equal(3, bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            Assert.NotNull(note.DurationFraction);
            // Fraction(4, 12) normalises to Fraction(1, 3)
            Assert.Equal(new Fraction(1, 3), note.DurationFraction);
        }
        double sum = bar.MusicalNotes.Sum(n => n.GetBeats(4));
        Assert.Equal(1.0, sum, 10);
    }

    [Fact]
    public void SlashOne_ProducesWholeNote()
    {
        // TUP-04: | C4/1 | → DurationFraction = 4/1 quarter = 1 whole
        var seq = CompileNoteStream("| C4/1 |");
        var bar = seq.Bars[0];
        Assert.Single(bar.MusicalNotes);
        Assert.Equal(new Fraction(4, 1), bar.MusicalNotes[0].DurationFraction);
        // GetBeats: 4/1 quarter × 4 / 4 = 4 beats (= one whole in 4/4)
        Assert.Equal(4.0, bar.MusicalNotes[0].GetBeats(4), 10);
    }

    [Fact]
    public void SlashZero_RaisesParseError()
    {
        // TUP-04: | C4/0 | → parse error
        var (hasErrors, formatted) = TryCompileNoteStream("| C4/0 |");
        Assert.True(hasErrors);
        Assert.Contains("Duration denominator must be ≥ 1", formatted);
    }

    // ===== TUP-08: C4/X:Y[suffix] per-note shorthand =====

    [Fact]
    public void PerNoteThreeAgainstTwo_EquivalentToBracket()
    {
        // TUP-08: | C4/3:2 D4/3:2 E4/3:2 | ≡ | {3:2 C4 D4 E4}q |
        var seqPerNote = CompileNoteStream("| C4/3:2 D4/3:2 E4/3:2 |");
        var seqBracket = CompileNoteStream("| {3:2 C4 D4 E4}q |");
        Assert.Equal(seqBracket.Bars[0].MusicalNotes.Count, seqPerNote.Bars[0].MusicalNotes.Count);
        for (int i = 0; i < seqPerNote.Bars[0].MusicalNotes.Count; i++)
        {
            Assert.Equal(seqBracket.Bars[0].MusicalNotes[i].DurationFraction,
                         seqPerNote.Bars[0].MusicalNotes[i].DurationFraction);
            Assert.Equal(new Fraction(1, 3), seqPerNote.Bars[0].MusicalNotes[i].DurationFraction);
        }
    }

    [Fact]
    public void PerNoteWithHalfSuffix_OneTenthWhole()
    {
        // TUP-08: | C4/5:4h | → DurationFraction = h × 1/5 = 2/1 × 1/5 = 2/5 quarter (= 1/10 whole per SPEC)
        var seq = CompileNoteStream("| C4/5:4h |");
        var bar = seq.Bars[0];
        Assert.Single(bar.MusicalNotes);
        Assert.Equal(new Fraction(2, 5), bar.MusicalNotes[0].DurationFraction);
    }

    [Fact]
    public void MixedRatios_AdjacentNotesLegal()
    {
        // TUP-08 / CONTEXT D-02: per-note instances are independent — mixed ratios legal in adjacent notes
        var seq = CompileNoteStream("| C4/3:2q D4/5:4q E4/3:2q |");
        var bar = seq.Bars[0];
        Assert.Equal(3, bar.MusicalNotes.Count);
        Assert.Equal(new Fraction(1, 3), bar.MusicalNotes[0].DurationFraction);
        Assert.Equal(new Fraction(1, 5), bar.MusicalNotes[1].DurationFraction);
        Assert.Equal(new Fraction(1, 3), bar.MusicalNotes[2].DurationFraction);
    }

    [Fact]
    public void PerNoteZeroNumerator_RaisesParseError()
    {
        // TUP-08: | C4/0:2 | → parse error citing X must be ≥ 1
        var (hasErrors, formatted) = TryCompileNoteStream("| C4/0:2 |");
        Assert.True(hasErrors);
        Assert.Contains("Tuplet ratio numerator X must be ≥ 1", formatted);
    }

    // ===== Regression — random-choice weight syntax does not collide with /X:Y =====

    [Fact]
    public void RandomChoiceWeights_AndPerNoteTuplet_DoNotCollide()
    {
        // RESEARCH §Pitfall Phase-19-specific: (? C4:50 E4:50) random weights and /3:2 per-note
        // tuplet ratios coexist via structural disambiguation (random choice gated by `(?` outer paren;
        // /X:Y happens AFTER Slash). The AST shapes don't overlap.
        var ast = ParseToAst("| (? C4:50 E4:50) D4/3:2 |");
        var bar = ast.Bars[0];
        Assert.Equal(2, bar.Elements.Count);

        // First element: RandomChoiceElement with 2 weighted choices
        var rc = Assert.IsType<RandomChoiceElement>(bar.Elements[0]);
        Assert.Equal(2, rc.Choices.Count);
        Assert.Equal(("C4", (int?)50), rc.Choices[0]);
        Assert.Equal(("E4", (int?)50), rc.Choices[1]);

        // Second element: NoteElement D4 with TupletRatio = (3, 2)
        var ne = Assert.IsType<NoteElement>(bar.Elements[1]);
        Assert.Equal("D4", ne.NoteName);
        Assert.Equal((3, 2), ne.TupletRatio);
    }

    // ===== Phase 18 dormancy regression (non-fractional path) =====

    [Fact]
    public void NoteStreamCompiler_NoSlashSyntax_TupletRatioStaysNull()
    {
        // | C4q D4q E4q | — no /N, no /X:Y → TupletRatio AND DurationFraction stay null on every note.
        // If THIS fails, the new parser arm is leaking syntactically — would break Phase 18 ByteIdentical*.
        var ast = ParseToAst("| C4q D4q E4q |");
        var bar = ast.Bars[0];
        foreach (var elem in bar.Elements)
        {
            var ne = Assert.IsType<NoteElement>(elem);
            Assert.Null(ne.TupletRatio);
        }

        var seq = CompileNoteStream("| C4q D4q E4q |");
        foreach (var note in seq.Bars[0].MusicalNotes)
        {
            Assert.Null(note.DurationFraction);
        }
    }
}
