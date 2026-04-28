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
/// TUP-05 — bar-fit validator with charitable overflow + Info diagnostic.
/// Per CONTEXT D-03 (locked algorithm) + charitable-interpretation memory:
/// overflow is silent-truncate + Info, NOT hard error. Preserves byte-identical
/// determinism (same input always yields same truncation).
///
/// Refinement of D-03: when remaining capacity is exactly zero (boundary lands
/// on the last fitting note), the validator DROPS the overflowing element instead
/// of emitting a zero-duration note. The Info diagnostic still fires.
/// </summary>
public class BarFitOverflowTests
{
    /// <summary>
    /// Compiles a note-stream source string with a fresh ErrorReporter so we can
    /// inspect Info-severity diagnostics emitted by ValidateBarFit. Uses the
    /// 2-arg NoteStreamCompiler ctor (added in Plan 19-03 Task 1) to inject the
    /// reporter — the parameterless ctor used in earlier Phase19 Facts skips
    /// the diagnostic.
    /// </summary>
    private static (SequenceData seq, ErrorReporter reporter) CompileWithReporter(
        string source, TimeSignatureData? ts = null)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();

        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        var noteStream = (NoteStreamExpression)stmt.Expression;

        var compiler = new NoteStreamCompiler(reporter);
        var ctx = new MusicalContext { TimeSignature = ts ?? new TimeSignatureData(4, 4) };
        var seq = compiler.Compile(noteStream, ctx);
        return (seq, reporter);
    }

    private static (bool hasErrors, string formatted) TryParse(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        parser.Parse();
        return (reporter.HasErrors, reporter.FormatErrors());
    }

    [Fact]
    public void ExactFitFourFourBar_FourTupletsPlusTwoQuarters_NoOverflow()
    {
        // SPEC TUP-05 acceptance: | {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q | in 4/4 → exact fit
        // 1/4 + 1/4 + 1/4 + 1/4 = 4/4. No Info diagnostic.
        var (seq, reporter) = CompileWithReporter("| {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q |");
        var bar = seq.Bars[0];

        // 3 + 3 + 1 + 1 = 8 emitted notes
        Assert.Equal(8, bar.MusicalNotes.Count);

        // Zero Info diagnostics
        int infoCount = reporter.Errors.Count(e => e.Level == DiagnosticLevel.Info);
        Assert.Equal(0, infoCount);
    }

    [Fact]
    public void OverflowFiveFourths_TruncatesAtBoundary_EmitsInfo()
    {
        // SPEC TUP-05 acceptance: | {3:2 C4 D4 E4}q B4q C5q D5q E5q | in 4/4 → 5/4 > 4/4
        // After triplet (1/4) + B4q (1/4) + C5q (1/4) + D5q (1/4) = 4/4 (exact).
        // E5q would push to 5/4. Boundary lands on E5q with remaining=0 → DROP it.
        var (seq, reporter) = CompileWithReporter("| {3:2 C4 D4 E4}q B4q C5q D5q E5q |");
        var bar = seq.Bars[0];

        // 3 (triplet) + B4q + C5q + D5q = 6 emitted notes (E5q dropped per zero-remaining refinement).
        Assert.Equal(6, bar.MusicalNotes.Count);

        // Last preserved note is D5q with full quarter
        Assert.Equal('D', bar.MusicalNotes[5].NoteName);
        Assert.Equal(5, bar.MusicalNotes[5].Octave);

        // Exactly 1 Info-severity diagnostic naming the overflow ratio
        var infos = reporter.Errors.Where(e => e.Level == DiagnosticLevel.Info).ToList();
        Assert.Single(infos);
        Assert.Contains("Bar overflow", infos[0].Message);
        Assert.Contains("5/1", infos[0].Message);  // overflow sum (5/1 quarter = 5/4 of a 4/4 bar)
        Assert.Contains("4/1", infos[0].Message);  // bar capacity (4/4 normalises to 4/1 in Fraction)
    }

    [Fact]
    public void OverflowMidElement_NonZeroRemaining_TruncatesBoundary()
    {
        // 4/4 bar: three triplets (3 × 1/1 quarter = 3/1 quarter) + E5h (= 2/1 quarter).
        // sum after 3 triplets = 3/1 quarter. nextSum = 3/1 + 2/1 = 5/1 > 4/1.
        // remaining = 4/1 - 3/1 = 1/1 quarter. Truncate E5h's DurationFraction to 1/1.
        var (seq, reporter) = CompileWithReporter(
            "| {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q {3:2 B4 C5 D5}q E5h |");
        var bar = seq.Bars[0];

        // 3 + 3 + 3 + 1 (truncated E5h) = 10 notes preserved
        Assert.Equal(10, bar.MusicalNotes.Count);

        // E5h is the boundary element — its DurationFraction should be Fraction(1, 1)
        var boundary = bar.MusicalNotes[9];
        Assert.Equal('E', boundary.NoteName);
        Assert.Equal(5, boundary.Octave);
        Assert.NotNull(boundary.DurationFraction);
        Assert.Equal(new Fraction(1, 1), boundary.DurationFraction);

        // Exactly 1 Info diagnostic
        Assert.Single(reporter.Errors.Where(e => e.Level == DiagnosticLevel.Info));
    }

    [Fact]
    public void NonTupletBar_DoesNotInvokeValidator()
    {
        // Pitfall 2 mitigation: bar with no DurationFraction-bearing notes is uninvolved.
        // | C4q D4q E4q F4q | in 4/4 → all 4 notes have DurationFraction=null → validator skipped.
        // Phase 18 byte-identical contract preserved structurally.
        var (seq, reporter) = CompileWithReporter("| C4q D4q E4q F4q |");
        var bar = seq.Bars[0];

        Assert.Equal(4, bar.MusicalNotes.Count);
        foreach (var n in bar.MusicalNotes)
        {
            Assert.Null(n.DurationFraction);
        }

        // Zero Info diagnostics — validator was bypassed entirely
        int infoCount = reporter.Errors.Count(e => e.Level == DiagnosticLevel.Info);
        Assert.Equal(0, infoCount);
    }

    [Fact]
    public void TupletBracketWithoutSuffix_RaisesParseError_ValidatorNeverReached()
    {
        // SPEC TUP-05 D-USER-C: tuplet bracket without explicit duration suffix is parse error.
        // This is enforced at the parser level (Plan 19-01) — validator never invoked.
        var (hasErrors, formatted) = TryParse("| {3:2 C4 D4 E4} |");
        Assert.True(hasErrors);
        Assert.Contains("Tuplet bracket requires explicit duration suffix", formatted);
    }

    [Fact]
    public void SixEightBarWithOneTriplet_UnderfillAccepted()
    {
        // 6/8 → bar capacity = 24/8 = 3 quarters. Two triplets (1q each) + one eighth (1/2 q) = 5/2 q.
        // 5/2 < 3 → underflow accepted (rest implicit per CONTEXT D-03 "Sums that underflow are accepted").
        // No Info diagnostic.
        var (seq, reporter) = CompileWithReporter(
            "| {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4e |",
            new TimeSignatureData(6, 8));
        var bar = seq.Bars[0];

        Assert.Equal(7, bar.MusicalNotes.Count);  // 3 + 3 + 1
        int infoCount = reporter.Errors.Count(e => e.Level == DiagnosticLevel.Info);
        Assert.Equal(0, infoCount);
    }
}
