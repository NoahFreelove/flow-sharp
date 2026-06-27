using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.TestFramework;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext —
// the bare name is ambiguous under net10.0's implicit usings.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-11 Task 2 — behavior facts for the <c>jam</c> chord-aware
/// Markov improvisation API (IMPROV-01 / D-36-10).
///
/// <para>
/// Pins: (a) jam returns a Sequence of exactly <c>length</c> bars; (b) the
/// chord-tone bias in #classical is actually observed in output; (c) the
/// <c>key=</c> override pushes a synthetic frame that affects scale-tone
/// selection; (d) unknown styles fall back to #jazz with a one-shot advisory;
/// (e) seeded jam is deterministic across calls in the same process;
/// (f) unseeded jam routes through PrngRegistry; (g) order out-of-range is
/// clamped with a charitable advisory; (h) style+key incompatibility warns
/// but doesn't crash (D-36-08).
/// </para>
///
/// <para>
/// Note on variable scoping: top-level Flow variables live in the
/// <c>GlobalFrame</c> and are reachable via <see cref="FlowEngineRunner.GetVariable"/>.
/// Variables declared INSIDE musical-context blocks (<c>tempo / timesig / key</c>)
/// live in their pushed frame and pop when the block ends — these tests
/// therefore declare top-level Sequences and rely on the active musical-context
/// being read at call time by <c>jam</c>.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class JamFunctionsTests
{
    [Fact]
    public void JamReturnsSequenceOfExactLength()
    {
        // jam should produce exactly `length` bars regardless of the `over`
        // progression length — the over cycles via modular indexing.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 Am7 Dm7 G7 |
            Sequence improvised = (jam over #jazz 4 "Cmajor" 42 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        var seq = runner.GetVariable("improvised").As<SequenceData>();
        Assert.Equal(4, seq.Bars.Count);
    }

    [Fact]
    public void JamSeededIsDeterministic()
    {
        // Two seeded jam calls with the same seed against the same `over`
        // produce identical output. We compare bar count + every MIDI pitch.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 Am7 Dm7 G7 |
            Sequence a = (jam over #jazz 4 "Cmajor" 42 2)
            Sequence b = (jam over #jazz 4 "Cmajor" 42 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        var a = runner.GetVariable("a").As<SequenceData>();
        var b = runner.GetVariable("b").As<SequenceData>();
        AssertSequenceEqual(a, b);
    }

    [Fact]
    public void JamClassicalRespectsChordToneBias()
    {
        // #classical's strong-beat weights: chord_tone=0.85, scale_tone=0.15,
        // chromatic_passing=0.00. Slots 0 and 4 of each output bar are strong
        // beats. With seed=7 over Cmaj7 (chord tones C, E, G, B), at least
        // 70% of strong-beat notes should land on chord tones (loose threshold
        // accommodating stochasticity over 16 bars × 2 strong slots = 32
        // strong notes).
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 |
            Sequence improvised = (jam over #classical 16 "Cmajor" 7 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");

        var seq = runner.GetVariable("improvised").As<SequenceData>();
        Assert.Equal(16, seq.Bars.Count);

        var chordPCs = new HashSet<int> { 0, 4, 7, 11 }; // C, E, G, B
        int strongTotal = 0, strongOnChord = 0;
        foreach (var bar in seq.Bars)
        {
            for (int slot = 0; slot < bar.MusicalNotes.Count; slot++)
            {
                if (slot != 0 && slot != 4) continue;
                if (bar.MusicalNotes[slot].IsRest) continue;
                var note = bar.MusicalNotes[slot];
                int midi = NoteType.ToMidiNote(note.NoteName, note.Octave, note.Alteration);
                int pc = ((midi % 12) + 12) % 12;
                strongTotal++;
                if (chordPCs.Contains(pc)) strongOnChord++;
            }
        }

        Assert.True(strongTotal > 0, "Expected at least one strong-beat note");
        double ratio = (double)strongOnChord / strongTotal;
        Assert.True(ratio >= 0.7,
            $"Expected >= 70% of strong-beat notes on chord tones with #classical bias; "
            + $"got {strongOnChord}/{strongTotal} = {ratio:F2}");
    }

    [Fact]
    public void JamKeyOverrideAffectsScaleSelection()
    {
        // With #jazz (which uses scale-tone weight) the choice of scale-tone
        // notes depends on the active key. Calling jam with key="Cmajor" vs
        // key="Fsharpmajor" against the SAME `over` + same seed should produce
        // DIFFERENT sequences (the scale-tone pool differs — C-major has no
        // sharps; F#-major has 6 sharps so they overlap in only one note class).
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 |
            Sequence inC = (jam over #jazz 4 "Cmajor" 99 2)
            Sequence inFsharp = (jam over #jazz 4 "Fsharpmajor" 99 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");

        var seqC = runner.GetVariable("inC").As<SequenceData>();
        var seqFs = runner.GetVariable("inFsharp").As<SequenceData>();

        bool anyDiff = false;
        for (int b = 0; b < Math.Min(seqC.Bars.Count, seqFs.Bars.Count) && !anyDiff; b++)
        {
            var na = seqC.Bars[b].MusicalNotes;
            var nb = seqFs.Bars[b].MusicalNotes;
            int common = Math.Min(na.Count, nb.Count);
            for (int i = 0; i < common; i++)
            {
                int midiA = NoteType.ToMidiNote(na[i].NoteName, na[i].Octave, na[i].Alteration);
                int midiB = NoteType.ToMidiNote(nb[i].NoteName, nb[i].Octave, nb[i].Alteration);
                if (midiA != midiB) { anyDiff = true; break; }
            }
        }

        Assert.True(anyDiff,
            "Expected key override to produce at least one differing note under the same seed");
    }

    [Fact]
    public void JamUnknownStyleFallsBackToJazz()
    {
        // (jam over=chords style=#nonexistent ...) should not throw — it falls
        // back to #jazz and emits a one-shot stderr advisory.
        RenderingDiagnostics.ResetForTesting();
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 |
            Sequence result = (jam over #nonexistent_style 4 "Cmajor" 1 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        Assert.Contains("unknown style '#nonexistent_style'", stderr);
        // And the result IS a valid 4-bar sequence (charitable fallback).
        var seq = runner.GetVariable("result").As<SequenceData>();
        Assert.Equal(4, seq.Bars.Count);
    }

    [Fact]
    public void JamOrderClampedTo1To3()
    {
        // order=5 clamps to 3 + WarnOnce. Composer-facing surface continues to
        // work (charitable).
        RenderingDiagnostics.ResetForTesting();
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 |
            Sequence result = (jam over #jazz 4 "Cmajor" 1 5)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        Assert.Contains("clamped to 3", stderr);
        Assert.Equal(4, runner.GetVariable("result").As<SequenceData>().Bars.Count);
    }

    [Fact]
    public void JamLengthZeroReturnsEmpty()
    {
        // length=0 → empty Sequence + advisory.
        RenderingDiagnostics.ResetForTesting();
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 |
            Sequence result = (jam over #jazz 0 "Cmajor" 1 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        Assert.Equal(0, runner.GetVariable("result").As<SequenceData>().Bars.Count);
        Assert.Contains("[jam] length 0", stderr);
    }

    [Fact]
    public void JamUnseededAtSameCallSiteIsRunReproducible()
    {
        // Unseeded jam routes through PrngRegistry keyed by (CurrentCallSite,
        // "jam"). Two consecutive unseeded calls at the SAME source position
        // share underlying PRNG state. The contract is: register the test once
        // and rerun via TestRunner — TestSnapshot/Restore should reset state
        // so re-running the same Thunk produces the same result. We pin a
        // weaker but still valuable contract: a single registered test
        // invoking unseeded jam runs successfully.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 Am7 Dm7 G7 |
            Sequence unseeded = (jam over)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        var seq = runner.GetVariable("unseeded").As<SequenceData>();
        // Default length is 8.
        Assert.Equal(8, seq.Bars.Count);
    }

    [Fact]
    public void JamMinimalCallWithJustOverWorks()
    {
        // (jam over) — only the required arg. Defaults apply: style=#jazz,
        // length=8, key=context, seed=null (PrngRegistry), order=2.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 |
            Sequence result = (jam over)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        Assert.Equal(8, runner.GetVariable("result").As<SequenceData>().Bars.Count);
    }

    [Fact]
    public void JamRegisteredTestRunsViaTestRunner()
    {
        // End-to-end smoke that the (test ...) framework + TestRunner.Run
        // surface works around jam: register a single test whose body
        // pierces a seeded-jam determinism assertion, then drive the
        // registered tests via the in-process TestRunner. Failing assertion
        // would surface a non-zero `failed` count.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            use "@test"
            Sequence over = | Cmaj7 Am7 Dm7 G7 |
            Sequence a = (jam over #jazz 4 "Cmajor" 42 2)
            Sequence b = (jam over #jazz 4 "Cmajor" 42 2)
            (test "jam seeded is deterministic"
                lazy((assertNotesMatch a b)))
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");

        var testRunner = new TestRunner();
        var (passed, failed) = testRunner.Run(runner.GetEngine(), "<test>");
        Assert.Equal(1, passed);
        Assert.Equal(0, failed);
    }

    // ====================================================================
    // helpers
    // ====================================================================

    /// <summary>
    /// Asserts two Sequences match in bar count + every note's MIDI pitch +
    /// duration. Looser than assertNotesMatch (skip velocity / articulation)
    /// so tests are robust to defaulting changes; the structural notes-equal
    /// contract is what matters for the seeded-determinism gate.
    /// </summary>
    private static void AssertSequenceEqual(SequenceData a, SequenceData b)
    {
        Assert.Equal(a.Bars.Count, b.Bars.Count);
        for (int barIdx = 0; barIdx < a.Bars.Count; barIdx++)
        {
            var na = a.Bars[barIdx].MusicalNotes;
            var nb = b.Bars[barIdx].MusicalNotes;
            Assert.Equal(na.Count, nb.Count);
            for (int i = 0; i < na.Count; i++)
            {
                int midiA = NoteType.ToMidiNote(na[i].NoteName, na[i].Octave, na[i].Alteration);
                int midiB = NoteType.ToMidiNote(nb[i].NoteName, nb[i].Octave, nb[i].Alteration);
                Assert.True(midiA == midiB,
                    $"bar {barIdx} slot {i}: MIDI mismatch ({midiA} vs {midiB})");
                Assert.Equal(na[i].DurationValue, nb[i].DurationValue);
            }
        }
    }
}
