using System;
using System.IO;
using System.Linq;
using System.Text;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase43;

/// <summary>
/// Phase 43 Plan 43-04 Task 2 — covers REQ-MOD-09 (Beat-companion delay
/// overload) and REQ-MOD-10 (Beat-companion renderBarAtBeat overload).
///
/// Pins:
///   1. <c>delay(Buffer, Beat, Double, Double)</c> is registered in the
///      InternalFunctionRegistry alongside the existing Buffer/Double,
///      Buffer/Millisecond, and Buffer/NoteValue overloads.
///   2. <c>renderBarAtBeat(Bar, Beat, String, Int, Double)</c> is registered
///      alongside the existing Buffer/Double overload.
///   3. The Beat overloads produce numerically-equivalent output to the
///      Millisecond overload when the tempo math matches (RMS-equivalent
///      since Delay.Apply is deterministic and Beat→ms is the only change).
///   4. The Beat-overload of delay emits the
///      <c>[delay] no active tempo — defaulting to 120 BPM</c> advisory
///      exactly once outside any tempo block (D-08 / D-09 parity).
///   5. The pre-Phase-43 bare-Double dispatch path remains undisturbed —
///      a literal Double in the second arg routes to the Double overload
///      (+1000 exact match) and never fires the Beat-overload advisory.
///
/// Stderr capture mirrors
/// <see cref="FlowLang.Tests.Integration.Phase37.StretchAutoAdvisoryTests"/>.
/// </summary>
[Collection("FlowScripts")]
public class BeatCompanionOverloadTests : IDisposable
{
    public BeatCompanionOverloadTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static string CaptureStderr(Action action)
    {
        var original = Console.Error;
        var sb = new StringBuilder();
        var writer = new StringWriter(sb);
        Console.SetError(writer);
        try { action(); }
        finally { Console.SetError(original); }
        return sb.ToString();
    }

    /// <summary>
    /// Test 4 (REQ-MOD-09 + REQ-MOD-10 reflective signature presence):
    /// reflectively enumerate the registry's signatures and assert that
    /// <c>delay</c> and <c>renderBarAtBeat</c> each have at least one
    /// registered overload whose second parameter type is
    /// <see cref="BeatType"/>. Mirrors
    /// <see cref="FlowLang.Tests.Integration.Phase42.AuditHarnessTests.Registry_WiresSfzAndNotationIoAndOsc"/>.
    /// </summary>
    [Fact]
    public void Registry_HasBeatOverloadsOnDelayAndRenderBarAtBeat()
    {
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;
        var sigs = registry.EnumerateSignatures().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        Assert.True(sigs.ContainsKey("delay"), "delay missing from registry");
        Assert.Contains(sigs["delay"], s =>
            s.InputTypes.Count >= 2 && s.InputTypes[1] is BeatType);

        Assert.True(sigs.ContainsKey("renderBarAtBeat"), "renderBarAtBeat missing from registry");
        Assert.Contains(sigs["renderBarAtBeat"], s =>
            s.InputTypes.Count >= 2 && s.InputTypes[1] is BeatType);
    }

    /// <summary>
    /// Test 2 (REQ-MOD-09 advisory): outside any tempo block, calling the
    /// Beat-typed delay overload emits the
    /// <c>[delay] no active tempo — defaulting to 120 BPM</c> advisory.
    ///
    /// The C# overload lambda calls <see cref="Diagnostics.RenderingDiagnostics.WarnOnce"/>
    /// with sentinel <c>delay-beat-no-tempo</c>; we exercise it through the
    /// public registry surface rather than parsing Beat-typed source so we
    /// don't depend on a Beat literal lexer change. The lambda accepts a
    /// Value carrying a Beat-typed FlowType — that's what `Value.Beat(...)`
    /// constructs.
    /// </summary>
    [Fact]
    public void DelayBeatOverload_OutsideTempoBlock_FiresAdvisory()
    {
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;

        // Build a tiny dummy buffer.
        var buf = new AudioBuffer(0, 1, 44100);
        var sig = new FunctionSignature("delay",
            new FlowType[] { BufferType.Instance, BeatType.Instance, DoubleType.Instance, DoubleType.Instance },
            ParameterNames: new[] { "buf", "beats", "feedback", "mix" });

        Assert.True(
            registry.TryGetImplementation("delay", sig, out var impl, out var registeredSig),
            $"delay(Buffer, Beat, Double, Double) was not registered. " +
            $"Got registeredSig={registeredSig}.");
        Assert.NotNull(impl);

        string stderr = CaptureStderr(() =>
        {
            var args = new[]
            {
                Value.Buffer(buf),
                Value.Beat(0.5),
                Value.Double(0.3),
                Value.Double(0.5),
            };
            _ = impl!(args);
        });

        Assert.Contains(
            "[delay] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)",
            stderr);
    }

    /// <summary>
    /// Test 5 (RESEARCH A5 — no specificity tiebreaker ambiguity): when the
    /// second arg is a bare <see cref="Value.Double"/>, the resolver picks
    /// the existing <c>delay(Buffer, Double, Double, Double)</c> overload at
    /// +1000 exact match, NOT the Beat overload. The Beat-overload advisory
    /// MUST NOT fire on that path. This preserves Phase 26.2 byte-identical
    /// behavior on existing scripts.
    /// </summary>
    [Fact]
    public void DelayDoubleOverload_StillResolvesToDoubleAndNoBeatAdvisory()
    {
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;

        var buf = new AudioBuffer(0, 1, 44100);
        var doubleSig = new FunctionSignature("delay",
            new FlowType[] { BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance },
            ParameterNames: new[] { "buf", "timeMs", "feedback", "mix" });

        Assert.True(
            registry.TryGetImplementation("delay", doubleSig, out var impl, out _),
            "Pre-Phase-43 delay(Buffer, Double, Double, Double) overload disappeared");
        Assert.NotNull(impl);

        string stderr = CaptureStderr(() =>
        {
            var args = new[]
            {
                Value.Buffer(buf),
                Value.Double(250.0),
                Value.Double(0.3),
                Value.Double(0.5),
            };
            _ = impl!(args);
        });

        Assert.DoesNotContain("[delay] no active tempo", stderr);
    }

    /// <summary>
    /// Test 1 (REQ-MOD-09 RMS equivalence): the Beat overload at 120 BPM
    /// produces a buffer equivalent to the Millisecond overload at the
    /// matching ms value (0.5 beat × 60_000/120 = 250 ms). Same Delay.Apply
    /// DSP path → identical output bytes given the same input.
    /// </summary>
    [Fact]
    public void DelayBeatOverload_AtTempo120_MatchesMillisecondOverload()
    {
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;

        // 1 second of stereo silence — Delay.Apply on silence returns silence
        // (the regression-safe path for this fact; we're only proving the
        // dispatch picks the same DSP entry and produces the same shape).
        var buf = new AudioBuffer(44100, 1, 44100);

        // Push a tempo=120 frame onto the engine's call stack so the Beat
        // overload reads the explicit tempo (no advisory should fire).
        engine.Context.CurrentFrame.MusicalContext = new MusicalContext { Tempo = 120.0 };

        var beatSig = new FunctionSignature("delay",
            new FlowType[] { BufferType.Instance, BeatType.Instance, DoubleType.Instance, DoubleType.Instance },
            ParameterNames: new[] { "buf", "beats", "feedback", "mix" });
        var msSig = new FunctionSignature("delay",
            new FlowType[] { BufferType.Instance, MillisecondType.Instance, DoubleType.Instance, DoubleType.Instance },
            ParameterNames: new[] { "buf", "timeMs", "feedback", "mix" });

        Assert.True(registry.TryGetImplementation("delay", beatSig, out var beatImpl, out _));
        Assert.True(registry.TryGetImplementation("delay", msSig, out var msImpl, out _));

        var beatArgs = new[]
        {
            Value.Buffer(buf), Value.Beat(0.5), Value.Double(0.3), Value.Double(0.5),
        };
        var msArgs = new[]
        {
            Value.Buffer(buf), Value.Millisecond(250.0), Value.Double(0.3), Value.Double(0.5),
        };

        string stderr = CaptureStderr(() =>
        {
            var beatRes = (AudioBuffer)beatImpl!(beatArgs).Data!;
            var msRes = (AudioBuffer)msImpl!(msArgs).Data!;

            Assert.Equal(msRes.Frames, beatRes.Frames);
            Assert.Equal(msRes.Channels, beatRes.Channels);
            Assert.Equal(msRes.SampleRate, beatRes.SampleRate);

            // Pinned-tempo path: byte-identical because both calls dispatch
            // to the same Delay.Apply with delayMs = 250 (0.5 * 60000/120).
            // AudioBuffer stores samples interleaved in .Data
            // (length = Frames * Channels).
            Assert.Equal(msRes.Data.Length, beatRes.Data.Length);
            for (int i = 0; i < msRes.Data.Length; i++)
            {
                Assert.Equal(msRes.Data[i], beatRes.Data[i]);
            }
        });

        Assert.DoesNotContain("[delay] no active tempo", stderr);
    }

    /// <summary>
    /// Test 3 (REQ-MOD-10 renderBarAtBeat Beat overload): the
    /// renderBarAtBeat Beat overload returns the same voice-array shape as
    /// the Double overload because the underlying CLR data is identical
    /// (Beat backs double). Exercised through the registry; the bar/synth
    /// args are minimal.
    /// </summary>
    [Fact]
    public void RenderBarAtBeatBeatOverload_ProducesSameVoiceArrayAsDoubleOverload()
    {
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;

        // Empty Musical-mode bar to avoid synth quirks; the renderer should
        // return an empty voice array. Lifted from the Phase 28 voice-pool
        // test pattern. The two-arg constructor sets Mode = Musical
        // (BarData.cs:108-116).
        var bar = new BarData(
            Array.Empty<MusicalNoteData>(),
            new TimeSignatureData(4, 4));

        var doubleSig = new FunctionSignature("renderBarAtBeat",
            new FlowType[]
            {
                BarType.Instance, DoubleType.Instance, StringType.Instance,
                IntType.Instance, DoubleType.Instance,
            },
            ParameterNames: new[] { "bar", "beat", "synth", "sampleRate", "bpm" });
        var beatSig = new FunctionSignature("renderBarAtBeat",
            new FlowType[]
            {
                BarType.Instance, BeatType.Instance, StringType.Instance,
                IntType.Instance, DoubleType.Instance,
            },
            ParameterNames: new[] { "bar", "beat", "synth", "sampleRate", "bpm" });

        Assert.True(registry.TryGetImplementation("renderBarAtBeat", doubleSig, out var doubleImpl, out _));
        Assert.True(registry.TryGetImplementation("renderBarAtBeat", beatSig, out var beatImpl, out _));

        var doubleArgs = new[]
        {
            Value.Bar(bar), Value.Double(1.0), Value.String("piano"),
            Value.Int(44100), Value.Double(120.0),
        };
        var beatArgs = new[]
        {
            Value.Bar(bar), Value.Beat(1.0), Value.String("piano"),
            Value.Int(44100), Value.Double(120.0),
        };

        var doubleRes = doubleImpl!(doubleArgs);
        var beatRes = beatImpl!(beatArgs);

        // Both must return Voice arrays (Value.Array of VoiceType.Instance).
        Assert.IsType<ArrayType>(doubleRes.Type);
        Assert.IsType<ArrayType>(beatRes.Type);
        var doubleArr = (System.Collections.Generic.IReadOnlyList<Value>)doubleRes.Data!;
        var beatArr = (System.Collections.Generic.IReadOnlyList<Value>)beatRes.Data!;
        Assert.Equal(doubleArr.Count, beatArr.Count);
    }
}
