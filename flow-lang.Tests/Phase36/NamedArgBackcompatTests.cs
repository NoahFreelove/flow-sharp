using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-02 Task 3 — backward-compat regression gate for the
/// universal named-argument rollout (D-36-11).
///
/// The big risk in adding ParameterNames + NamedArgs as defaulted fields
/// across FunctionSignature and FunctionCallExpression is that legacy
/// positional calls — the entire existing surface — start behaving
/// differently. Plans 36-03 + 36-04 will backfill ~350 builtin signatures
/// with parameter names in parallel; until then (and forever after), the
/// rule is: positional calls resolve IDENTICALLY pre- and post-Phase 36.
///
/// This fact samples 5 distinct existing builtins (transpose, slice, gain,
/// lowpass, scaleNotes) and exercises each with a positional-only call
/// through the full Lexer → Parser → Interpreter pipeline, asserting that
/// each returns a non-null Value of the expected Flow type. If any of these
/// snap, the backfill plans need to stop and re-evaluate. Plan 36-12 will
/// later ship a stricter ParameterNamesCoverageTest grep gate for the
/// 100% backfill milestone.
/// </summary>
public class NamedArgBackcompatTests
{
    [Fact]
    public void PositionalCallsStillResolveUntouched()
    {
        using var engine = new FlowEngine(verbose: false);

        // 5 distinct positional-only calls, one per line:
        //   - transpose(Sequence, Semitone) — Transforms
        //   - retrograde(Sequence)          — Transforms (no-arg variant)
        //   - gain(Buffer, Decibel)         — Audio DSP
        //   - lowpass(Buffer, Hertz)        — Audio DSP (filter)
        //   - scaleNotes(String)            — Harmony (returns Strings/Array<Note>)
        var source = """
            use "@std"
            use "@audio"
            Sequence seq = | C4q D4q |
            Sequence tposed = (transpose seq 2)
            Sequence retro = (retrograde seq)
            Buffer dry = (createSineTone 0.5 440.0 0.5)
            Buffer boosted = (gain dry -6dB)
            Buffer filtered = (lowpass dry 800Hz)
            Strings notes = (scaleNotes "Cmajor")
            """;

        var ok = engine.Execute(source, "<backcompat>");
        Assert.True(ok, $"Execute failed: {engine.ErrorReporter.FormatErrors()}");
        Assert.False(engine.ErrorReporter.HasErrors,
            engine.ErrorReporter.FormatErrors());

        // Spot-check each variable exists and is non-null. The point of this
        // fact is "no regressions in resolution" — exact Value contents are
        // covered by the per-feature integration tests.
        Assert.NotNull(engine.Context.GetVariable("tposed"));
        Assert.NotNull(engine.Context.GetVariable("retro"));
        Assert.NotNull(engine.Context.GetVariable("dry"));
        Assert.NotNull(engine.Context.GetVariable("boosted"));
        Assert.NotNull(engine.Context.GetVariable("filtered"));
        Assert.NotNull(engine.Context.GetVariable("notes"));
    }
}
