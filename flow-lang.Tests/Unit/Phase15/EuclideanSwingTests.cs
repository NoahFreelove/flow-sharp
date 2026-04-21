using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase15;

/// <summary>
/// Phase 15 Plan 04 (DX-09): swing accent semantics on the 4-arg
/// <c>euclidean(Int, Int, Note, Double)</c> overload.
///
/// CONTEXT decisions exercised here:
///   D-05  swing clamped to [-1.0, 1.0]
///   D-06  on-beat = hit index divisible by <c>gridStep = max(1, steps / hits)</c>
///   D-07  accent magnitude is a raw velocity delta (no multiplier)
///   D-08  asymmetric accent: unaccented set stays at base; positive swing accents
///         on-beats, negative swing accents off-beats
///   ROADMAP #1 (F-21): swing changes ONLY velocity, never note duration
///
/// Bjorklund(3, 8) = [x . . x . . x .]  — hits at step indices 0, 3, 6
///   gridStep = max(1, 8/3) = 2 → on-beats at indices {0, 2, 4, 6},
///   so hits-on-beat = {0, 6}, hits-off-beat = {3}.
/// Bjorklund(5, 8) = [x . x x . x x .]  — hits at 0, 2, 3, 5, 6
///   gridStep = max(1, 8/5) = 1 → every index is on-beat, so all hits are accented.
///
/// Observable pin: non-rest <see cref="MusicalNoteData.Velocity"/>, compared by absolute
/// tolerance 1e-9 where the math is exact, or inequality where only the direction matters.
/// </summary>
[Collection("FlowScripts")]
public class EuclideanSwingTests
{
    private const double BaseVelocity = 0.63;
    private const double Tol = 1e-9;

    private static List<MusicalNoteData> HitNotes(SequenceData seq)
    {
        var bar = seq.Bars[0];
        var hits = new List<MusicalNoteData>();
        foreach (var n in bar.MusicalNotes)
            if (!n.IsRest) hits.Add(n);
        return hits;
    }

    private static SequenceData RunEuclidean(FlowEngineRunner runner, string callExpr, string varName = "s")
    {
        var (success, _, stderr, errorCount) = runner.RunSource(
            "use \"@std\"\n" +
            $"Sequence {varName} = {callExpr}\n");
        Assert.True(success, $"euclidean call failed. stderr={stderr}");
        Assert.Equal(0, errorCount);
        var v = runner.GetVariable(varName);
        return v.As<SequenceData>();
    }

    // F-09 — Swing clamped to 1.0 on the upper edge.
    // swing=1.5 silently clamps to 1.0; accented hits reach base+1.0 which then
    // clamps at the MusicalNoteData constructor to 1.0 exactly.
    [Fact]
    public void Swing_AboveMax_ClampsTo1()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunEuclidean(runner, "(euclidean 3 8 C4 1.5)");
        var hits = HitNotes(seq);
        Assert.Equal(3, hits.Count);
        double max = hits.Max(n => n.Velocity);
        Assert.True(max <= 1.0 + Tol, $"max velocity {max} > 1.0");
        Assert.True(max > 0.99, $"expected clamp-to-1.0 saturation; max={max}");
    }

    // F-10 — Negative swing accents off-beats.
    // For (3,8) with hits at {0, 3, 6}: gridStep=2 → on-beats = {0, 6}, off-beats = {3}.
    // With swing=-0.3, accentAmount=0.3 applied to off-beats only, so hit at step 3
    // has velocity 0.93, hits at steps 0 and 6 stay at base (0.63).
    [Fact]
    public void NegativeSwing_AccentsOffBeats()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunEuclidean(runner, "(euclidean 3 8 C4 (sub 0.0 0.3))");
        var hits = HitNotes(seq);
        Assert.Equal(3, hits.Count);
        // hits[0] at step 0 (on-beat, unaccented), hits[1] at step 3 (off-beat, accented),
        // hits[2] at step 6 (on-beat, unaccented).
        Assert.Equal(BaseVelocity, hits[0].Velocity, Tol);
        Assert.Equal(BaseVelocity + 0.3, hits[1].Velocity, Tol);
        Assert.Equal(BaseVelocity, hits[2].Velocity, Tol);
        Assert.True(hits[1].Velocity > hits[0].Velocity);
        Assert.True(hits[1].Velocity > hits[2].Velocity);
    }

    // F-11 — On-beat detection matches step-grid in two scenarios.
    // Scenario A: (3,8) → gridStep=2, hits at {0,3,6}: indices 0,6 on-beat; 3 off-beat.
    // Scenario B: (5,8) → gridStep=1, every hit on-beat; all hits accented.
    [Fact]
    public void OnBeat_DetectionMatchesGrid()
    {
        using var runner = new FlowEngineRunner();

        var seqA = RunEuclidean(runner, "(euclidean 3 8 C4 0.3)", "sA");
        var hitsA = HitNotes(seqA);
        Assert.Equal(3, hitsA.Count);
        Assert.Equal(BaseVelocity + 0.3, hitsA[0].Velocity, Tol); // step 0 accented
        Assert.Equal(BaseVelocity,        hitsA[1].Velocity, Tol); // step 3 unaccented
        Assert.Equal(BaseVelocity + 0.3, hitsA[2].Velocity, Tol); // step 6 accented

        using var runner2 = new FlowEngineRunner();
        var seqB = RunEuclidean(runner2, "(euclidean 5 8 C4 0.3)", "sB");
        var hitsB = HitNotes(seqB);
        Assert.Equal(5, hitsB.Count);
        foreach (var n in hitsB)
            Assert.Equal(BaseVelocity + 0.3, n.Velocity, Tol);
    }

    // F-12 — Accent amount is a raw delta: swing=0.25 adds 0.25 to accented velocity.
    [Fact]
    public void AccentAmount_IsRawDelta()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunEuclidean(runner, "(euclidean 3 8 C4 0.25)");
        var hits = HitNotes(seq);
        Assert.Equal(3, hits.Count);
        // hits[0] at step 0 is on-beat → accented; expected 0.63 + 0.25 = 0.88.
        Assert.Equal(0.88, hits[0].Velocity, Tol);
    }

    // F-13 — Asymmetric accent: unaccented set stays at base (NOT base - swing).
    [Fact]
    public void Asymmetric_UnaccentedStaysAtBase()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunEuclidean(runner, "(euclidean 3 8 C4 0.3)");
        var hits = HitNotes(seq);
        Assert.Equal(3, hits.Count);
        // hits[1] at step 3 is off-beat (unaccented with positive swing) — must stay at base.
        Assert.Equal(BaseVelocity, hits[1].Velocity, Tol);
        // Sanity: not the de-accented value 0.63 - 0.3 = 0.33.
        Assert.NotEqual(0.33, hits[1].Velocity);
    }

    // F-21 — Swing changes velocity only, never note duration (ROADMAP #1).
    [Fact]
    public void Swing_ChangesVelocity_NotTiming()
    {
        using var runner = new FlowEngineRunner();
        var seq0 = RunEuclidean(runner, "(euclidean 3 8 C4 0.0)", "s0");

        using var runner2 = new FlowEngineRunner();
        var seq1 = RunEuclidean(runner2, "(euclidean 3 8 C4 0.5)", "s1");

        var notes0 = seq0.Bars[0].MusicalNotes;
        var notes1 = seq1.Bars[0].MusicalNotes;
        Assert.Equal(notes0.Count, notes1.Count);

        bool anyVelocityDiff = false;
        for (int i = 0; i < notes0.Count; i++)
        {
            // Timing must be identical at every index (both rest/hit and duration).
            Assert.Equal(notes0[i].IsRest, notes1[i].IsRest);
            Assert.Equal(notes0[i].DurationValue, notes1[i].DurationValue);
            Assert.Equal(notes0[i].IsDotted, notes1[i].IsDotted);
            if (!notes0[i].IsRest && notes1[i].Velocity != notes0[i].Velocity)
                anyVelocityDiff = true;
        }
        Assert.True(anyVelocityDiff,
            "swing=0.5 must move at least one accented hit's velocity vs swing=0.0");
    }
}
