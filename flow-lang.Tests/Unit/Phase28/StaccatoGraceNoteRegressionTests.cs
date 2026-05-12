using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Regression for the Phase 28 UAT BLOCKER originally reported as
/// "every staccato note in ragtime_polyphony.wav bar 2 has an audible
/// pre-attack grace note." Root cause was unrelated to the SPEC-5
/// articulation envelope — it was a NOTE-STREAM PARSER bug in
/// <see cref="Parser"/>.ParseNoteStream where the multi-line bar list
///
///     | ... |
///     | ... |
///
/// produces the token sequence <c>PIPE [bar1] PIPE PIPE [bar2] PIPE</c>.
/// Pre-fix, every adjacent-PIPE pair silently inserted a whole-bar rest
/// between the content bars, doubling the rendered bar count (4 source
/// bars → 7 rendered bars in the ragtime fixture). The composer heard
/// each rendered staccato bar onset arriving after a 2-second silent
/// gap, and the C2w bass voice attack that started the bar perceptually
/// grafted onto the C5 staccato as a grace-note-like thump.
///
/// The fix collapses adjacent PIPEs into a single bar boundary (the
/// closing | of bar N AND the opening | of bar N+1 are the same token
/// in a multi-line list). Explicit empty/rest bars must be written
/// <c>| _ |</c> just like in single-line layouts.
///
/// This regression test pins the bar-count contract by asserting that:
///   1. Multi-line 4-bar source → exactly 4 compiled bars.
///   2. Multi-line 8-bar source → exactly 8 compiled bars.
///   3. Single-line equivalents produce the SAME bar count as multi-line.
///   4. Explicit <c>| _ |</c> rest bars are preserved (NOT collapsed).
/// </summary>
public class StaccatoGraceNoteRegressionTests
{
    private const int SampleRate = 44100;
    private const double Bpm = 120.0;

    /// <summary>
    /// The Phase 28 ragtime fixture has 4 source bars across 4 lines.
    /// Pre-fix this compiled to 7 bars. Post-fix it must compile to 4.
    /// </summary>
    [Fact]
    public void RagtimeFixture_MultiLineFourBars_CompilesToFourBars()
    {
        const string source = @"
            | {voice C2w} {voice C5q E5q G5q E5q} |
            | {voice C2w} {voice C5q stacc D5q stacc E5q stacc F5q stacc} |
            | {voice C2w} {voice C5q leg D5q leg E5q leg F5q leg} |
            | C4q stacc D4q ten E4q > F4q marc |
        ";
        var seq = CompileNoteStream(source, new TimeSignatureData(4, 4));
        Assert.Equal(4, seq.Bars.Count);
        Assert.Equal(16.0, seq.TotalBeats);
    }

    /// <summary>
    /// Maple-Leaf fixture has 8 source bars across 8 lines. Pre-fix this
    /// compiled to 15 bars. Post-fix must compile to 8.
    /// </summary>
    [Fact]
    public void MapleLeafFixture_MultiLineEightBars_CompilesToEightBars()
    {
        const string source = @"
            | {voice Ab1q [Eb3 Ab3 C4]q} {voice Eb5e G5e Ab5e Bb5e} |
            | {voice Ab1q [Eb3 Ab3 C4]q} {voice Db6e Bb5e Ab5e G5e} |
            | {voice Bb1q [F3 Bb3 D4]q} {voice F5e Ab5e Bb5e C6e} |
            | {voice Bb1q [F3 Bb3 D4]q} {voice Eb6e C6e Bb5e Ab5e} |
            | {voice Eb1q [Bb2 Eb3 G3]q} {voice G5e Bb5e Eb6e Db6e} |
            | {voice Eb1q [Bb2 Eb3 G3]q} {voice Bb5e Ab5e G5e F5e} |
            | {voice Ab1q [Eb3 Ab3 C4]q} {voice Eb5q Ab5q} |
            | {voice Ab1q [Eb3 Ab3 C4]q} {voice C5q Eb5q} |
        ";
        var seq = CompileNoteStream(source, new TimeSignatureData(2, 4));
        Assert.Equal(8, seq.Bars.Count);
        Assert.Equal(16.0, seq.TotalBeats);
    }

    /// <summary>
    /// Single-line equivalent of the synthetic ragtime fixture must
    /// produce the SAME 4-bar / 16-beat compile output as the multi-
    /// line version. Pre-fix, the two layouts differed (single-line: 4,
    /// multi-line: 7); the fix normalizes them.
    /// </summary>
    [Fact]
    public void RagtimeFixture_SingleLineFourBars_MatchesMultiLineBarCount()
    {
        const string source =
            "| {voice C2w} {voice C5q E5q G5q E5q} | {voice C2w} {voice C5q stacc D5q stacc E5q stacc F5q stacc} | {voice C2w} {voice C5q leg D5q leg E5q leg F5q leg} | C4q stacc D4q ten E4q > F4q marc |";
        var seq = CompileNoteStream(source, new TimeSignatureData(4, 4));
        Assert.Equal(4, seq.Bars.Count);
        Assert.Equal(16.0, seq.TotalBeats);
    }

    /// <summary>
    /// Explicit <c>| _ |</c> rest bars must remain bars in the output.
    /// The fix only collapses ADJACENT PIPEs (which can only occur from
    /// the multi-line layout). A bar with a single rest element under-
    /// score is content, not an empty-PIPE pair.
    /// </summary>
    [Fact]
    public void ExplicitRestBar_PreservedInMultiLineLayout()
    {
        const string source = @"
            | C4q D4q E4q F4q |
            | _ |
            | G4q A4q B4q C5q |
        ";
        var seq = CompileNoteStream(source, new TimeSignatureData(4, 4));
        Assert.Equal(3, seq.Bars.Count);
        Assert.Equal(12.0, seq.TotalBeats);
    }

    /// <summary>
    /// Pickup notation through multi-line must still produce a pickup
    /// bar + the regular bars (the fix's empty-bar collapse must not
    /// swallow the pickup's content).
    /// </summary>
    [Fact]
    public void PickupNotation_PreservedThroughMultiLineParse()
    {
        const string source = @"
            pickup | G4q |
            | C5q D5q E5q F5q |
        ";
        var seq = CompileNoteStream(source, new TimeSignatureData(4, 4));
        Assert.Equal(2, seq.Bars.Count);
        Assert.Equal(5.0, seq.TotalBeats);
    }

    /// <summary>
    /// Sanity probe at the synth layer: a single C5 quarter staccato
    /// through the piano synth must produce ONE peak in its envelope
    /// multiplier curve (a sustain=0 envelope has attack ramp up, then
    /// decay ramp down, then stays at zero). This was the originally-
    /// hypothesized root cause; it remained PASSING throughout the
    /// investigation, which is why the bug had to be elsewhere — the
    /// parser, not the SPEC-5 envelope.
    /// </summary>
    [Fact]
    public void PianoStaccato_EnvelopeMultiplierHasExactlyOnePeak()
    {
        SynthUtils.ResetNoiseRng();
        var note = new MusicalNoteData(
            'C', 5, 0,
            (int)NoteValueType.Value.QUARTER,
            isRest: false,
            articulation: Articulation.Staccato);
        var bar = new BarData(new[] { note }, new TimeSignatureData(4, 4));
        var voices = BarRenderer.RenderBarToVoices(bar, new PianoSynthesizer(), SampleRate, Bpm);
        Assert.Single(voices);
        int numSamples = voices[0].Buffer.Frames;
        var envelope = SynthUtils.GenerateArticulationADSR(
            Articulation.Staccato,
            baseAttack: 0.003, baseDecay: 0.6, baseSustain: 0.12, baseRelease: 0.3,
            frames: numSamples, sampleRate: SampleRate);
        Assert.Equal(1, CountLocalMaxima(envelope, minProminence: 0.05f));
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    /// <summary>
    /// Lexes + parses a note-stream source (must be a single top-level
    /// expression of the form <c>| ... |</c> or <c>pickup | ... |</c>),
    /// then compiles it through <see cref="NoteStreamCompiler"/>. Mirrors
    /// the helper used by Phase 19 TupletBracketTests so the bar-count
    /// contract is exercised against the SAME pipeline the engine uses.
    /// </summary>
    private static SequenceData CompileNoteStream(string source, TimeSignatureData timeSig)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");
        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        var noteStream = (FlowLang.Ast.Expressions.NoteStreamExpression)stmt.Expression;
        var compiler = new NoteStreamCompiler(reporter);
        var ctx = new MusicalContext { TimeSignature = timeSig };
        var seq = compiler.Compile(noteStream, ctx);
        Assert.False(reporter.HasErrors, $"Compile errors: {reporter.FormatErrors()}");
        return seq;
    }

    private static int CountLocalMaxima(float[] curve, float minProminence)
    {
        if (curve.Length < 3) return 0;
        var peaks = new List<int>();
        for (int i = 1; i < curve.Length - 1; i++)
        {
            if ((curve[i] > curve[i - 1] && curve[i] >= curve[i + 1]) ||
                (curve[i] >= curve[i - 1] && curve[i] > curve[i + 1]))
                peaks.Add(i);
        }
        int prominent = 0;
        for (int p = 0; p < peaks.Count; p++)
        {
            int peakIdx = peaks[p];
            float peakVal = curve[peakIdx];
            int leftBound = p > 0 ? peaks[p - 1] : 0;
            float leftMin = curve[peakIdx];
            for (int j = leftBound; j <= peakIdx; j++) if (curve[j] < leftMin) leftMin = curve[j];
            int rightBound = p < peaks.Count - 1 ? peaks[p + 1] : curve.Length - 1;
            float rightMin = curve[peakIdx];
            for (int j = peakIdx; j <= rightBound; j++) if (curve[j] < rightMin) rightMin = curve[j];
            float prominence = peakVal - Math.Max(leftMin, rightMin);
            if (prominence >= minProminence) prominent++;
        }
        return prominent;
    }
}
