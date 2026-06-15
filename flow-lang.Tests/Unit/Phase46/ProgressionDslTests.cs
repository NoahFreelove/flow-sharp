using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase46;

/// <summary>
/// Phase 46 Plan 46-05 (CLEAN-12 / D-12): unit coverage for the Progression DSL
/// (<c>progression | I IV V |</c>). The DSL is a distinct ergonomic chord-progression
/// surface that is KEPT + INVESTED per D-01 — prior to this plan it had ZERO unit tests.
///
/// Idiom mirrors <see cref="FlowLang.Tests.Unit.Phase15.EuclideanSwingTests"/>:
/// drive the language end-to-end via <see cref="FlowEngineRunner.RunSource"/> inside a
/// <c>key Cmajor { ... }</c> block, then read the structured
/// <see cref="SequenceData"/> back via <see cref="FlowEngineRunner.GetVariable"/>.
///
/// Behaviours covered:
///   (1) `progression | I IV V I |` in key Cmajor yields 4 bars.
///   (2) the `:N` bar-count suffix (`| I:2 V |`) yields the right bar count.
///   (3) the `voices N` modifier path parses + compiles (voiceCount honored).
///   (4) the no-key error case reports ErrorCount &gt; 0 and the documented stderr substring.
///   (5) voice-leading determinism — same progression compiled twice → identical pitches
///       (ProgressionCompiler.FindNearestPitchClass is deterministic).
/// </summary>
[Collection("FlowScripts")]
public class ProgressionDslTests
{
    /// <summary>
    /// Compiles a progression <paramref name="body"/> inside a <c>key Cmajor { }</c> block
    /// and returns the resulting <see cref="SequenceData"/>. Asserts the run succeeded with
    /// zero reported errors (the happy path used by assertions 1-3 and 5).
    /// </summary>
    private static SequenceData RunProg(FlowEngineRunner runner, string body, string varName = "s")
    {
        // Declare the binding at top level then assign inside the key block, so the
        // compiled Sequence lands in the global frame where GetVariable reads it.
        // (Variables DECLARED inside a `key { }` block are scoped to that block.)
        var (success, _, stderr, errorCount) = runner.RunSource(
            "use \"@std\"\n" +
            $"Sequence {varName} = | C4q |\n" +
            "key Cmajor {\n" +
            $"  {varName} = {body}\n" +
            "}\n");
        Assert.True(success, $"progression compile failed. stderr={stderr}");
        Assert.Equal(0, errorCount);
        return runner.GetVariable(varName).As<SequenceData>();
    }

    /// <summary>
    /// Flattens every non-rest note across all bars into a stable MIDI-pitch list —
    /// the observable used for the voice-leading determinism check. Mirrors the
    /// MIDI convention used by <c>NoteType.ToMidiNote</c> (C4 = 60).
    /// </summary>
    private static List<int> AllPitches(SequenceData seq)
    {
        var pitches = new List<int>();
        foreach (var bar in seq.Bars)
            foreach (var n in bar.MusicalNotes)
                if (!n.IsRest)
                    pitches.Add(NoteType.ToMidiNote(n.NoteName, n.Octave, n.Alteration));
        return pitches;
    }

    // (1) Four roman numerals → four bars.
    [Fact]
    public void Progression_FourNumerals_YieldsFourBars()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunProg(runner, "progression | I IV V I |");
        Assert.Equal(4, seq.Bars.Count);
        // Each bar must sound at least one (non-rest) voice.
        foreach (var bar in seq.Bars)
            Assert.Contains(bar.MusicalNotes, n => !n.IsRest);
    }

    // (2) The `:N` bar-count suffix expands a single numeral into N bars.
    // `| I:2 V |` → I (2 bars) + V (1 bar) = 3 bars.
    [Fact]
    public void Progression_BarCountSuffix_ExpandsBars()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunProg(runner, "progression | I:2 V |");
        Assert.Equal(3, seq.Bars.Count);
    }

    // (3) The `voices N` modifier sets the voice count: every bar carries exactly N
    // simultaneous (non-rest) notes.
    [Fact]
    public void Progression_VoicesModifier_SetsVoiceCount()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunProg(runner, "progression voices 4 | I IV V I |");
        Assert.Equal(4, seq.Bars.Count);
        foreach (var bar in seq.Bars)
        {
            int voiced = bar.MusicalNotes.Count(n => !n.IsRest);
            Assert.Equal(4, voiced);
        }
    }

    // (4) No active key context → the DSL reports an error (does not throw) with the
    // documented message, and the run surfaces ErrorCount > 0.
    [Fact]
    public void Progression_NoKeyContext_ReportsError()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(
            "use \"@std\"\n" +
            "Sequence s = progression | I IV V |\n");
        Assert.True(errorCount > 0, "expected an error when no key context is active");
        Assert.Contains("progression requires an active key context", stderr);
    }

    // (5) Voice leading is deterministic: the same progression compiled in two fresh
    // engines yields an identical pitch sequence (FindNearestPitchClass is deterministic).
    [Fact]
    public void Progression_VoiceLeading_IsDeterministic()
    {
        using var runnerA = new FlowEngineRunner();
        var seqA = RunProg(runnerA, "progression | I IV V I |", "a");

        using var runnerB = new FlowEngineRunner();
        var seqB = RunProg(runnerB, "progression | I IV V I |", "b");

        var pitchesA = AllPitches(seqA);
        var pitchesB = AllPitches(seqB);
        Assert.NotEmpty(pitchesA);
        Assert.Equal(pitchesA, pitchesB);
    }

    // (6) FIX 0615: `progression | I IV V |` in Cmajor renders STACKED block chords,
    // not a sequential arpeggio. Each of the 3 bars must place >=3 voices at the SAME
    // onset (offsetBeats == 0 via BarData.ToTimeline), and the whole chord must sit in a
    // sensible register — no over-bassy C2 (every voice >= MIDI 48 / C3).
    [Fact]
    public void Progression_RendersStackedChords_InSensibleRegister()
    {
        using var runner = new FlowEngineRunner();
        var seq = RunProg(runner, "progression | I IV V |");

        Assert.Equal(3, seq.Bars.Count);

        foreach (var bar in seq.Bars)
        {
            // ToTimeline gives the authoritative onset-per-note view. A block chord =
            // every voice emitted at the same beat offset.
            var timeline = bar.ToTimeline().Where(t => !t.note.IsRest).ToList();
            Assert.True(timeline.Count >= 3,
                $"expected >=3 voices in the bar, got {timeline.Count}");

            // All voices share ONE onset (simultaneous strike, not an arpeggio).
            double firstOnset = timeline[0].offsetBeats;
            Assert.All(timeline, t => Assert.Equal(firstOnset, t.offsetBeats, 6));

            // Exactly one lead voice (IsChordTone=false); the rest stack on it.
            int leads = timeline.Count(t => !t.note.IsChordTone);
            Assert.Equal(1, leads);

            // Sensible register: nothing below C3 (MIDI 48) — the old bass sat at C2 (~36).
            foreach (var (note, _) in timeline)
            {
                int midi = NoteType.ToMidiNote(note.NoteName, note.Octave, note.Alteration);
                Assert.True(midi >= 48,
                    $"voice at MIDI {midi} is below C3 (over-bassy); expected >= 48");
            }
        }
    }

    // (7) FIX 0615: a progression chord is timing-identical to the equivalent note-stream
    // chord bracket [..] — both place every voice at onset 0 of the bar (one shared onset).
    // This pins the progression onto the SAME simultaneity mechanism brackets use.
    [Fact]
    public void Progression_ChordTiming_MatchesBracketChord()
    {
        using var runner = new FlowEngineRunner();

        // First bar of `progression | I |` in Cmajor.
        var prog = RunProg(runner, "progression | I |", "p");
        var progOnsets = prog.Bars[0].ToTimeline()
            .Where(t => !t.note.IsRest)
            .Select(t => Math.Round(t.offsetBeats, 6))
            .Distinct()
            .ToList();

        // A bracket chord [C4 E4 G4]w sounds all three at the bar's single onset.
        var bracket = RunProg(runner, "| [C4 E4 G4]w |", "b");
        var bracketOnsets = bracket.Bars[0].ToTimeline()
            .Where(t => !t.note.IsRest)
            .Select(t => Math.Round(t.offsetBeats, 6))
            .Distinct()
            .ToList();

        // Both collapse to a single shared onset — no arpeggiation in either path.
        Assert.Single(progOnsets);
        Assert.Single(bracketOnsets);
        Assert.Equal(bracketOnsets, progOnsets);
    }
}
