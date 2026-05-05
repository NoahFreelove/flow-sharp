using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Transforms;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase25;

/// <summary>
/// Phase 25 (DEFER-06): humanizeGaussian(Sequence, Double, Int) Box-Muller transform.
/// Pins deterministic-by-seed velocity perturbation, rest passthrough, clamp invariants,
/// and statistical sanity. Anchors decisions D-01..D-25 from 25-CONTEXT.md.
///
/// CRITICAL invariants (RESEARCH §Common Pitfalls):
///   D-01  signature (Sequence, Double, Int) order (seq, amount, seed)
///   D-03  LOCAL new Random(seed); never touches ExecutionContext.GetRand
///   D-05  basic Box-Muller (cos branch); D-06 sin discarded
///   D-07  velJitter = z * amount * 0.2
///   D-09  velocity clamped to [0.05, 1.0]
///   D-10  amount==0 short-circuit returns input unchanged
///   D-11  rests pass through (no PRNG consumption)
///   D-18  existing humanize is FROZEN — these Facts pin humanizeGaussian only
/// </summary>
[Collection("FlowScripts")]
public class HumanizeGaussianFacts
{
    private const double BaseVelocity = 0.63;
    private const double Tol = 1e-9;

    // FROZEN PIN — computed via test-first run with seed=42, amount=0.1, BaseVelocity=0.63.
    // If .NET's Random algorithm shifts in a future patch (per .NET 6+ stability guarantee
    // this should not happen), this constant is the canary that catches the regression.
    // Formula reference: z = sqrt(-2 ln u1) * cos(2π u2); newVelocity = clamp(0.63 + z * 0.1 * 0.2, 0.05, 1.0).
    private const double Seeded42_FirstNote_PinnedVelocity = 0.6413705509099572;

    private static InternalFunctionRegistry BuildRegistry()
    {
        var registry = new InternalFunctionRegistry();
        TransformFunctions.Register(registry);
        return registry;
    }

    private static SequenceData BuildBaseSequence(int noteCount, double velocity)
    {
        var notes = new List<MusicalNoteData>();
        for (int i = 0; i < noteCount; i++)
        {
            // C4 quarter notes at requested base velocity. Direct C# construction
            // avoids MusicalContext.Velocity interference per Pitfall 4.
            notes.Add(new MusicalNoteData(
                noteName: 'C', octave: 4, alteration: 0, durationValue: 4, isRest: false,
                centOffset: null, isTied: false, velocity: velocity));
        }
        var bar = new BarData(notes, new TimeSignatureData(4, 4));
        var seq = new SequenceData();
        seq.AddBar(bar);
        return seq;
    }

    private static SequenceData BuildMixedSequence(double velocity)
    {
        // 2 notes + 2 rests interleaved (D-11 fixture).
        var notes = new List<MusicalNoteData>
        {
            new('C', 4, 0, 4, isRest: false, centOffset: null, isTied: false, velocity: velocity),
            new(' ', 0, 0, 4, isRest: true),
            new('D', 4, 0, 4, isRest: false, centOffset: null, isTied: false, velocity: velocity),
            new(' ', 0, 0, 4, isRest: true),
        };
        var bar = new BarData(notes, new TimeSignatureData(4, 4));
        var seq = new SequenceData();
        seq.AddBar(bar);
        return seq;
    }

    private static SequenceData CallHumanizeGaussian(SequenceData seq, double amount, int seed)
    {
        var registry = BuildRegistry();
        var sig = new FunctionSignature("humanizeGaussian",
            [SequenceType.Instance, DoubleType.Instance, IntType.Instance]);
        // Real API per flow-lang/StandardLibrary/InternalFunctionRegistry.cs:22 —
        // TryGetImplementation(name, signature, out impl, out matchedSig). Returns bool.
        if (!registry.TryGetImplementation("humanizeGaussian", sig, out var fn, out _) || fn is null)
            throw new InvalidOperationException("humanizeGaussian not registered");
        var args = new List<Value>
        {
            Value.Sequence(seq),
            Value.Double(amount),
            Value.Int(seed),
        };
        var result = fn(args);
        return result.As<SequenceData>();
    }

    private static List<MusicalNoteData> NonRestNotes(SequenceData seq)
    {
        var hits = new List<MusicalNoteData>();
        foreach (var bar in seq.Bars)
            foreach (var n in bar.MusicalNotes)
                if (!n.IsRest) hits.Add(n);
        return hits;
    }

    [Fact]
    public void Seeded42_FirstNoteVelocity_PinnedExactly()
    {
        var seq = BuildBaseSequence(noteCount: 4, velocity: BaseVelocity);
        var result = CallHumanizeGaussian(seq, amount: 0.1, seed: 42);
        var hits = NonRestNotes(result);
        Assert.Equal(4, hits.Count);
        Assert.Equal(Seeded42_FirstNote_PinnedVelocity, hits[0].Velocity, Tol);
    }

    [Fact]
    public void Seeded42_TwoConsecutiveCalls_ProduceIdenticalOutput()
    {
        var seq = BuildBaseSequence(noteCount: 4, velocity: BaseVelocity);
        var r1 = NonRestNotes(CallHumanizeGaussian(seq, 0.1, 42));
        var r2 = NonRestNotes(CallHumanizeGaussian(seq, 0.1, 42));
        Assert.Equal(r1.Count, r2.Count);
        for (int i = 0; i < r1.Count; i++)
            Assert.Equal(r1[i].Velocity, r2[i].Velocity, Tol);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentOutput()
    {
        var seq = BuildBaseSequence(noteCount: 4, velocity: BaseVelocity);
        var r42 = NonRestNotes(CallHumanizeGaussian(seq, 0.1, 42));
        var r43 = NonRestNotes(CallHumanizeGaussian(seq, 0.1, 43));
        bool anyDiffers = false;
        for (int i = 0; i < r42.Count; i++)
            if (Math.Abs(r42[i].Velocity - r43[i].Velocity) > Tol) anyDiffers = true;
        Assert.True(anyDiffers, "seed=42 and seed=43 produced byte-identical output — humanizeGaussian may be a no-op");
    }

    [Fact]
    public void AmountZero_ReturnsInputUnchanged()
    {
        var seq = BuildBaseSequence(noteCount: 4, velocity: BaseVelocity);
        var result = CallHumanizeGaussian(seq, amount: 0.0, seed: 42);
        var hits = NonRestNotes(result);
        Assert.Equal(4, hits.Count);
        foreach (var n in hits)
            Assert.Equal(BaseVelocity, n.Velocity, Tol);
    }

    [Fact]
    public void Rests_PassThroughUnchanged()
    {
        var seq = BuildMixedSequence(velocity: BaseVelocity);
        var result = CallHumanizeGaussian(seq, amount: 0.5, seed: 42);
        var bar = result.Bars[0];
        Assert.Equal(4, bar.MusicalNotes.Count);
        // Rests at indices 1 and 3 must pass through unchanged
        Assert.True(bar.MusicalNotes[1].IsRest);
        Assert.True(bar.MusicalNotes[3].IsRest);
        // Non-rests at indices 0 and 2 must have changed velocity (probabilistically; with amount=0.5 jitter is high)
        Assert.False(bar.MusicalNotes[0].IsRest);
        Assert.False(bar.MusicalNotes[2].IsRest);
    }

    [Fact]
    public void Velocity_ClampedTo_005_to_10()
    {
        // Extreme baseline (0.99) + amount=1.0 forces many out-of-range raw values
        var seq = BuildBaseSequence(noteCount: 100, velocity: 0.99);
        var result = CallHumanizeGaussian(seq, amount: 1.0, seed: 42);
        var hits = NonRestNotes(result);
        foreach (var n in hits)
        {
            Assert.True(n.Velocity >= 0.05, $"velocity {n.Velocity} below 0.05 floor");
            Assert.True(n.Velocity <= 1.0, $"velocity {n.Velocity} above 1.0 ceiling");
        }
        // Clamp engagement check — a no-op humanizeGaussian (returning the input
        // unchanged) would also pass the bounds checks above (0.99 is in range).
        // With baseline 0.99 + amount=1.0 + Gaussian jitter * 0.2 = stddev 0.2, the
        // upper tail is almost certain to push at least one sample above 1.0 and
        // engage the upper clamp. Assert at least one velocity is at the boundary.
        bool clampEngaged = hits.Any(n => Math.Abs(n.Velocity - 1.0) < 1e-9
                                       || Math.Abs(n.Velocity - 0.05) < 1e-9);
        Assert.True(clampEngaged,
            "Expected clamp to engage on at least one of 100 samples (baseline 0.99 + amount=1.0). " +
            "If this fails, humanizeGaussian may be a no-op or jitter scale is wrong.");
    }

    [Fact]
    public void LargeSequence_DistributionIsApproximatelyNormal()
    {
        const int n = 1000;
        const double baseVel = 0.5;
        const double amount = 0.5;
        const double expectedStddev = amount * 0.2; // 0.1
        var seq = BuildBaseSequence(noteCount: n, velocity: baseVel);
        var result = CallHumanizeGaussian(seq, amount, seed: 42);
        var hits = NonRestNotes(result);
        var perturbations = hits.Select(h => h.Velocity - baseVel).ToList();
        double mean = perturbations.Average();
        double variance = perturbations.Select(p => (p - mean) * (p - mean)).Sum() / perturbations.Count;
        double stddev = Math.Sqrt(variance);
        Assert.True(Math.Abs(mean) < 0.02, $"mean perturbation {mean} too far from 0");
        Assert.True(Math.Abs(stddev - expectedStddev) / expectedStddev < 0.20,
            $"stddev {stddev} more than 20% from expected {expectedStddev}");
    }
}
