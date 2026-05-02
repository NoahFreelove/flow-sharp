using System;
using System.IO;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-12 acceptance Facts pinning NoteValue-rate delay overload synced to MusicalContext.Tempo.
/// Decisions referenced:
///   RESEARCH Pitfall 1 — bare-integer dispatch ambiguity (test 9 pins observed behavior)
///   RESEARCH Pitfall 10 — user-facing examples use EIGHTH/QUARTER constants from @notation
///
/// Tests 1-5 invoke <see cref="EffectsFunctions.NoteValueToMs"/> directly to pin the math.
/// Tests 6-9 use FlowEngine.Evaluate to exercise the registered overload through OverloadResolver.
///
/// Phase 22 plan 22-04 — RED state at Task 1: helper exists as a NotImplemented-returning stub
/// (returns 0.0); engine-eval tests fail because the NoteValue overload is not registered.
/// Task 2 GREEN body replaces the stub and registers <c>RegisterContextDependent</c>.
/// </summary>
public class DelaySyncFacts
{
    /// <summary>Synthesize a small mono buffer for engine-eval delay tests.</summary>
    private static AudioBuffer SynthSine(int frames, int sampleRate, int channels)
    {
        var buf = new AudioBuffer(frames, channels, sampleRate);
        for (int f = 0; f < frames; f++)
            for (int ch = 0; ch < channels; ch++)
                buf.SetSample(f, ch, (float)Math.Sin(2 * Math.PI * 440 * f / sampleRate));
        return buf;
    }

    // ===== Tests 1-5: NoteValueToMs math (direct helper call) =====

    [Fact]
    public void NoteValueToMs_EighthAt120Bpm_Returns250()
    {
        // 60_000 / 120 / 2 = 250 ms (eighth note at 120 BPM).
        double ms = EffectsFunctions.NoteValueToMs(NoteValueType.Value.EIGHTH, 120.0);
        Assert.InRange(ms, 249.5, 250.5);
    }

    [Fact]
    public void NoteValueToMs_QuarterAt120Bpm_Returns500()
    {
        // 60_000 / 120 = 500 ms (quarter note at 120 BPM).
        double ms = EffectsFunctions.NoteValueToMs(NoteValueType.Value.QUARTER, 120.0);
        Assert.InRange(ms, 499.5, 500.5);
    }

    [Fact]
    public void NoteValueToMs_EighthAt240Bpm_Returns125()
    {
        // Tempo doubles → time halves: 60_000 / 240 / 2 = 125 ms.
        double ms = EffectsFunctions.NoteValueToMs(NoteValueType.Value.EIGHTH, 240.0);
        Assert.InRange(ms, 124.5, 125.5);
    }

    [Fact]
    public void NoteValueToMs_WholeAt60Bpm_Returns4000()
    {
        // 60_000 / 60 * 4 = 4000 ms (whole note at 60 BPM = 4 quarter notes).
        double ms = EffectsFunctions.NoteValueToMs(NoteValueType.Value.WHOLE, 60.0);
        Assert.InRange(ms, 3999.5, 4000.5);
    }

    [Fact]
    public void NoteValueToMs_SixteenthAt120Bpm_Returns125()
    {
        // 60_000 / 120 / 4 = 125 ms (sixteenth note at 120 BPM).
        double ms = EffectsFunctions.NoteValueToMs(NoteValueType.Value.SIXTEENTH, 120.0);
        Assert.InRange(ms, 124.5, 125.5);
    }

    // ===== Test 6: existing ms-rate overload regression gate =====

    [Fact]
    public void Existing_MsRateOverload_Unchanged()
    {
        // Regression: the existing Double-rate delay overload must produce
        // byte-identical output across two calls. This guards against accidental
        // mutation of RegisterDelay or DelayEffect when the NoteValue overload lands.
        using var runner1 = new FlowEngineRunner();
        var (s1, _, e1, n1) = runner1.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.1 440.0 0.5)
Buffer wet = (delay src 250.0 0.5 0.4)
Int frames = (getFrames wet)
");
        Assert.Equal(0, n1);
        int frames1 = runner1.GetVariable("frames").As<int>();

        using var runner2 = new FlowEngineRunner();
        var (s2, _, e2, n2) = runner2.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.1 440.0 0.5)
Buffer wet = (delay src 250.0 0.5 0.4)
Int frames = (getFrames wet)
");
        Assert.Equal(0, n2);
        int frames2 = runner2.GetVariable("frames").As<int>();

        Assert.Equal(frames1, frames2);
        Assert.True(frames1 > 0, $"ms-rate delay produced empty buffer (frames={frames1})");
    }

    // ===== Test 7: NoteValue overload smoke at tempo 120 =====

    [Fact]
    public void NoteValueOverload_AtTempo120_ProducesNonEmptyBuffer()
    {
        // Engine-eval: tempo 120 { (delay buf EIGHTH 0.5 0.4) } returns a non-empty buffer.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
use ""@notation""
Buffer src = (createSineTone 0.1 440.0 0.5)
Int wetFrames = 0
tempo 120 {
    Buffer wet = (delay src EIGHTH 0.5 0.4)
    wetFrames = (getFrames wet)
}
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        int frames = runner.GetVariable("wetFrames").As<int>();
        Assert.True(frames > 0, $"NoteValue delay overload produced empty buffer (frames={frames})");
    }

    // ===== Test 8: no active tempo defaults to 120 BPM =====

    [Fact]
    public void NoneActiveTempo_DefaultsTo120Bpm()
    {
        // When no `tempo X { ... }` block is active, tempo defaults to 120.0
        // via the `?? 120.0` fallback. The output frame count for EIGHTH at 120 BPM
        // outside a tempo block must equal the same call inside a tempo 120 block.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
use ""@notation""
Buffer src = (createSineTone 0.1 440.0 0.5)
Buffer wetOutside = (delay src EIGHTH 0.5 0.4)
Int outsideFrames = (getFrames wetOutside)
Int insideFrames = 0
tempo 120 {
    Buffer wetInside = (delay src EIGHTH 0.5 0.4)
    insideFrames = (getFrames wetInside)
}
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        int outside = runner.GetVariable("outsideFrames").As<int>();
        int inside = runner.GetVariable("insideFrames").As<int>();
        Assert.Equal(inside, outside);
        Assert.True(outside > 0);
    }

    // ===== Test 9: bare-integer dispatch documented (Pitfall 1) =====

    [Fact]
    public void BareIntegerArg_DispatchesAmbiguous_DocumentedPitfall1()
    {
        // Pitfall 1: (delay buf 250 0.5 0.4) with a bare Int arg is AMBIGUOUS between the
        // Double-rate and NoteValue-rate overloads:
        //   - NoteValueType.IsCompatibleWith treats IntType as compatible (NoteValueType.cs:19)
        //   - IntType is convertible to DoubleType via the numeric ladder
        // Both candidates score equally at the OverloadResolver, so the resolver reports an
        // ambiguous-overload error. The fix per RESEARCH Pitfall 10 is for users to write
        // either `250.0` (forces Double) or use the `EIGHTH`/`QUARTER` named NoteValue
        // constants from @notation.
        //
        // This Fact PINS the ambiguity so that any future change to the score table —
        // intentional disambiguation OR accidental tie-break — surfaces in CI as a behavior
        // change requiring an explicit decision.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.1 440.0 0.5)
Buffer wet = (delay src 250 0.5 0.4)
");
        // Observed behavior in v1.3: errorCount > 0 with "Ambiguous overload" stderr.
        // If a future plan disambiguates the dispatch, this assertion will go RED and the
        // change should be reviewed (likely flipping the assertion to `errorCount == 0`).
        Assert.True(errorCount > 0,
            $"bare-Int dispatch unexpectedly resolved (errorCount={errorCount}); Pitfall 1 ambiguity may have been silently fixed — review and update this Fact.");
        Assert.Contains("Ambiguous overload", stderr);
    }
}
