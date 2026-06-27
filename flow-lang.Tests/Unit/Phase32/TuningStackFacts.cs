using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLangTests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 32-05 Task 3 — pins the stack semantics of
/// <see cref="MusicalContext.TuningStack"/> + <see cref="MusicalContext.ActiveTuning"/>
/// and the four <see cref="ExecutionContext"/> entry points (SetFileScopeTuning,
/// PushTuning, PopTuning, ResetBlockTuningStack).
///
/// Constructs ExecutionContext directly via the same shape Phase 23
/// PitchConversionTuningFacts uses — no FlowEngineRunner needed because this
/// suite operates at the Runtime layer. Each Fact constructs a fresh
/// <see cref="ExecutionContext"/> + <see cref="MusicalContext"/>, exercises the
/// relevant method sequence, and asserts the resulting <c>ActiveTuning</c> OR
/// <c>TuningStack.Count</c> state.
///
/// Pitfall 2 explicit Fact (D-08 sticky pragma + D-14 ephemeral blocks):
/// <see cref="ResetBlockTuningStack_PreservesPragmaFrame_PopsBlocks"/>.
/// </summary>
public class TuningStackFacts
{
    // ---- helpers ----

    private static ExecutionContext NewExecutionContext()
    {
        var reporter = new ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new ExecutionContext(reporter, registry);
    }

    /// <summary>Synthetic <see cref="RenderTuning"/> for stack-semantics tests; tonic + mode
    /// are irrelevant to the stack contract under test (we only assert which entry the stack
    /// holds, not what frequencies it produces).</summary>
    private static RenderTuning RtJustIntonation()
        => new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0);

    private static RenderTuning RtPythagorean()
        => new RenderTuning(TuningSystem.Pythagorean, Mode.Major, 'D', 0);

    /// <summary>A second RenderTuning shape distinguishable from <see cref="RtJustIntonation"/>
    /// via the Mode discriminator, so equality comparisons in the stack-content assertions
    /// don't accidentally hit identical records.</summary>
    private static RenderTuning RtJustIntonationDorian()
        => new RenderTuning(TuningSystem.JustIntonation, Mode.Dorian, 'C', 0);

    // ---- Facts ----

    [Fact]
    public void MusicalContext_Default_HasEmptyStack_ActiveTuningIsDefault()
    {
        var mc = new MusicalContext();
        Assert.Empty(mc.TuningStack);
        Assert.Equal(RenderTuning.Default, mc.ActiveTuning);
        // Default RenderTuning encodes the byte-identical 12-TET short-circuit trigger.
        Assert.Null(mc.ActiveTuning.Custom);
        Assert.Equal(TuningSystem.EqualTemperament, mc.ActiveTuning.System);
    }

    [Fact]
    public void PushTuning_Once_ActiveTuningReturnsPushedValue()
    {
        var ctx = NewExecutionContext();
        var rt = RtJustIntonation();
        ctx.PushTuning(rt);

        // PushTuning targets CurrentFrame. From the global scope the current frame
        // IS the global frame, so the resolved context sees the push.
        var resolved = ctx.GetMusicalContext();
        Assert.Equal(rt, resolved.ActiveTuning);
    }

    [Fact]
    public void PushTuning_Twice_ActiveTuningReturnsTopValue()
    {
        var ctx = NewExecutionContext();
        var first = RtJustIntonation();
        var second = RtPythagorean();
        ctx.PushTuning(first);
        ctx.PushTuning(second);

        var resolved = ctx.GetMusicalContext();
        Assert.Equal(second, resolved.ActiveTuning);
        Assert.Equal(2, resolved.TuningStack.Count);
    }

    [Fact]
    public void PopTuning_AfterTwoPushes_RevealsLowerValue()
    {
        var ctx = NewExecutionContext();
        var first = RtJustIntonation();
        var second = RtPythagorean();
        ctx.PushTuning(first);
        ctx.PushTuning(second);
        ctx.PopTuning();

        var resolved = ctx.GetMusicalContext();
        Assert.Equal(first, resolved.ActiveTuning);
        Assert.Single(resolved.TuningStack);
    }

    [Fact]
    public void SetFileScopeTuning_TwiceReplaces_DoesNotAccumulate()
    {
        var ctx = NewExecutionContext();
        var first = RtJustIntonation();
        var second = RtPythagorean();
        ctx.SetFileScopeTuning(first);
        ctx.SetFileScopeTuning(second);

        // Phase 23 D-08 / Phase 32 D-12 + Pitfall 2: the file-scope frame is REPLACED,
        // not stacked. After two consecutive SetFileScopeTuning calls the global frame's
        // stack contains exactly ONE entry (the second value).
        Assert.NotNull(ctx.GlobalFrame.MusicalContext);
        Assert.Single(ctx.GlobalFrame.MusicalContext!.TuningStack);
        Assert.Equal(second, ctx.GlobalFrame.MusicalContext.TuningStack.Peek());

        var resolved = ctx.GetMusicalContext();
        Assert.Equal(second, resolved.ActiveTuning);
    }

    [Fact]
    public void ResetBlockTuningStack_PreservesPragmaFrame_PopsBlocks()
    {
        // Pitfall 2 explicit coexistence Fact:
        //   D-08 (Phase 23 carried forward): file-scope pragmas survive REPL eval boundaries
        //   D-14 (Phase 32):                  block-form pushes (tuning t { ... }) force-close
        // The bottom-of-stack file-scope frame is sticky; everything above it is ephemeral.
        var ctx = NewExecutionContext();
        var pragma = RtJustIntonation();
        var blockA = RtPythagorean();
        var blockB = RtJustIntonationDorian();

        ctx.SetFileScopeTuning(pragma);   // bottom frame
        ctx.PushTuning(blockA);           // simulates one tuning { ... } level
        ctx.PushTuning(blockB);           // simulates a nested tuning { ... } level
        Assert.Equal(3, ctx.GlobalFrame.MusicalContext!.TuningStack.Count);

        ctx.ResetBlockTuningStack();      // REPL eval boundary

        // After reset: stack has exactly the pragma frame. Block frames are gone.
        Assert.Single(ctx.GlobalFrame.MusicalContext.TuningStack);
        Assert.Equal(pragma, ctx.GlobalFrame.MusicalContext.TuningStack.Peek());
        Assert.Equal(pragma, ctx.GetMusicalContext().ActiveTuning);

        // Additional ResetBlockTuningStack calls are idempotent — the pragma frame
        // stays. This is the D-08 REPL-stickiness invariant.
        ctx.ResetBlockTuningStack();
        Assert.Single(ctx.GlobalFrame.MusicalContext.TuningStack);
        Assert.Equal(pragma, ctx.GlobalFrame.MusicalContext.TuningStack.Peek());
    }

    [Fact]
    public void PopTuning_OnEmptyStack_Throws()
    {
        var ctx = NewExecutionContext();
        // CurrentFrame == GlobalFrame here; its MusicalContext is either null or has
        // an empty TuningStack. PopTuning's defensive guard throws either way.
        Assert.Throws<InvalidOperationException>(() => ctx.PopTuning());
    }

    [Fact]
    public void Clone_DeepCopiesTuningStack_PreservesOrder()
    {
        // Stack<T> Clone correctness: the two-reversal trick in MusicalContext.Clone
        // preserves push order (top stays top). This Fact pins that contract — a
        // subtle bug source if a future refactor switches to a single-arg Stack<T>
        // copy ctor (which silently reverses).
        var mc = new MusicalContext();
        var first = RtJustIntonation();
        var second = RtPythagorean();
        var third = RtJustIntonationDorian();
        mc.TuningStack.Push(first);
        mc.TuningStack.Push(second);
        mc.TuningStack.Push(third);

        var clone = mc.Clone();

        Assert.Equal(3, clone.TuningStack.Count);
        Assert.Equal(third, clone.TuningStack.Peek());
        // Drain the clone and verify order matches the original push sequence.
        Assert.Equal(third, clone.TuningStack.Pop());
        Assert.Equal(second, clone.TuningStack.Pop());
        Assert.Equal(first, clone.TuningStack.Pop());
        // Cloning must not mutate the source.
        Assert.Equal(3, mc.TuningStack.Count);
        Assert.Equal(third, mc.TuningStack.Peek());
    }

    [Fact]
    public void TuningStack_CarriesCustomResolvedTuning_ActiveTuningPreservesIt()
    {
        // Phase 32 D-03 sanity: the stack handles RenderTuning entries that carry a
        // Custom ResolvedTuning reference. The Pitfall 3 mutual-exclusion contract
        // (Custom != null implies the wedge System is ignored) lives in
        // PitchConversion / SongRenderer.ResolveRenderTuning — at the stack layer
        // we just verify ActiveTuning preserves the Custom reference verbatim.
        var ctx = NewExecutionContext();
        var resolved = MakeSyntheticResolvedTuning();
        var customTuning = new RenderTuning(TuningSystem.EqualTemperament, Mode.Major, 'C', 0, resolved);
        ctx.PushTuning(customTuning);

        var activeTuning = ctx.GetMusicalContext().ActiveTuning;
        Assert.NotNull(activeTuning.Custom);
        Assert.Same(resolved, activeTuning.Custom);
        Assert.Equal(customTuning, activeTuning);
    }

    // Minimal ResolvedTuning fixture: 2-step cents-only scale (100¢ / 1200¢ period).
    // Sufficient to exercise the stack contract; the full ResolvedTuning math is
    // exercised by ResolvedTuningFacts / TuningTypeFacts.
    private static ResolvedTuning MakeSyntheticResolvedTuning()
    {
        var scl = new ParsedScala(
            Description: "stack-fact synthetic 2-step",
            StepCents: new[] { 100.0 },
            PeriodCents: 1200.0,
            Ratios: new Dictionary<int, (int Num, int Den)>(),
            FilePath: "synthetic:stack-fact.scl");
        var kbm = ScalaKbmParser.Default(scl);
        return new ResolvedTuning(scl, kbm);
    }
}
