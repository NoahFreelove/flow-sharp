// Plan 30-06 Task 3 — RED-on-HEAD facts pinning Bug B Defect 1
// (Quantizer.cs:568-602 SnapDurationCapped + Quantizer.cs:103+ Quantize
// + Quantizer.cs:346-479 QuantizeSpans).
//
// References:
// - .planning/debug/midi-import-quarter-quantize.md (Bug B trigger + symptoms)
// - .planning/phases/30-flow-cli-formal-install/30-RESEARCH.md Bug B Scope Assessment
//   Defect 1: Quarter notes rendered as sixteenth-dotted + 5 rests.
//
// The defect: when `availableTicks` (Quantizer.cs:424) is even 1 tick less than 480,
// `SnapDurationCapped` strictly rejects the `q` grid (480 ticks > 479 cap) and
// falls back to dotted-eighth ("e", true) at 360 ticks — leaving the bar to
// fill with several `AddRests` "_" thirty-second emissions. This pattern
// reproduces the composer-observed Bug B output `D4s. _ _ _ _ _`.
//
// Plan 30-07 must add a small TPQN-relative tolerance to SnapDurationCapped
// so quarter-aligned ticks (or near-quarter ticks within ~tpqn/32) still snap
// to ("q", false). When that lands, these facts turn GREEN.
//
// Do NOT [Skip] any fact. RED is the goal of Plan 30-06 Task 3.

using FlowMidi.Conversion;
using FlowMidi.Tests.Fixtures;
using Xunit;

namespace FlowMidi.Tests.Unit.Phase30;

public class QuantizerSnapDurationTests
{
    const int Tpqn = 480;

    // Pins Bug B Defect 1 — see .planning/debug/midi-import-quarter-quantize.md
    // GREEN-on-HEAD baseline: tick-clean 4-quarter input must produce 4 `q` tokens.
    // This is the canonical regression test for the Bug B symptom; if any future
    // change breaks tick-clean snapping, this fact will catch it.
    [Fact]
    public void FourQuarterNotes_In_4_4_Produce_Four_Q_Tokens()
    {
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddFourQuarterNotes(channel: 0, pitch: 60)
            .Build();

        var result = Quantizer.Quantize(midi);

        Assert.Single(result.Tracks);
        var bars = result.Tracks[0].Bars;
        Assert.Single(bars);
        var notes = bars[0].Elements.OfType<NoteElement>().ToList();
        Assert.Equal(4, notes.Count);
        foreach (var n in notes)
        {
            Assert.Equal("q", n.DurationSuffix);
            Assert.False(n.IsDotted,
                $"Bug B Defect 1: a tick-clean quarter at TPQN=480 must snap to ('q', false), got ('{n.DurationSuffix}', dotted={n.IsDotted}).");
        }
    }

    // Pins Bug B Defect 1 — slight tick jitter case.
    // RED-on-HEAD: a quarter whose duration is 479 ticks instead of 480 (off
    // by 1 tick — well within human/DAW capture tolerance) is REJECTED by the
    // strict-cap `SnapDurationCapped` and snaps to ("e", true) at 360 ticks.
    // Plan 30-07's tolerance fix turns this GREEN.
    [Fact]
    public void Quarter_Note_With_One_Tick_Gap_Still_Snaps_To_Q()
    {
        // Pattern: quarter at 0..479, quarter at 479..959, quarter at 959..1439, quarter at 1439..1919.
        // Each note is one tick short of a true quarter. End-of-bar at 1920.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,    endTick: 479)
            .AddNote(channel: 0, pitch: 62, startTick: 479,  endTick: 959)
            .AddNote(channel: 0, pitch: 64, startTick: 959,  endTick: 1439)
            .AddNote(channel: 0, pitch: 65, startTick: 1439, endTick: 1919)
            .Build();

        var result = Quantizer.Quantize(midi);

        Assert.Single(result.Tracks);
        var notes = result.Tracks[0].Bars
            .SelectMany(b => b.Elements)
            .OfType<NoteElement>()
            .ToList();
        Assert.NotEmpty(notes);

        // The CRITICAL assertion: a 1-tick-shy quarter must still snap to q,false.
        // On HEAD this fails — strict-cap rejects q (480 > 479) and produces e. (360).
        Assert.Equal("q", notes[0].DurationSuffix);
        Assert.False(notes[0].IsDotted,
            $"Bug B Defect 1: a quarter note whose duration is 479 ticks (1 tick shy of TPQN=480) must still snap to ('q', false). On HEAD it falls back to ('e', dotted) because SnapDurationCapped strictly rejects gridTicks > capTicks with zero tolerance.");
    }

    // Pins Bug B Defect 1 — composer-observed Q-E-E rhythm.
    // Tick-clean input. May be GREEN-on-HEAD for this clean fixture (the bug
    // primarily manifests on jittered ticks), but pins the rhythm shape contract:
    // after Plan 30-07, the composer's canonical <Q, E, E> rhythm must round-trip
    // cleanly. If a regression ever flips Q to anything else, this fact catches it.
    [Fact]
    public void Quarter_Eighth_Eighth_Pattern_Produces_Q_E_E_In_Order()
    {
        // 1 quarter at 0..480, then 2 eighths at 480..720 and 720..960.
        // bar 1 (0..1920): Q + E + E + rest(960 ticks).
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,   endTick: 480)
            .AddNote(channel: 0, pitch: 62, startTick: 480, endTick: 720)
            .AddNote(channel: 0, pitch: 64, startTick: 720, endTick: 960)
            .Build();

        var result = Quantizer.Quantize(midi);

        Assert.Single(result.Tracks);
        var notes = result.Tracks[0].Bars
            .SelectMany(b => b.Elements)
            .OfType<NoteElement>()
            .ToList();

        Assert.Equal(3, notes.Count);
        Assert.Equal(("q", false), (notes[0].DurationSuffix, notes[0].IsDotted));
        Assert.Equal(("e", false), (notes[1].DurationSuffix, notes[1].IsDotted));
        Assert.Equal(("e", false), (notes[2].DurationSuffix, notes[2].IsDotted));
    }

    // Pins Bug B Defect 1 — half-note duration must snap to ("h", false), not
    // some compound rhythm. RED-on-HEAD when adjacent-note gap is jittered:
    // the algorithm uses `availableTicks = nextEventTick - cursor` as its cap
    // (Quantizer.cs:424), so a half note whose follower starts 1 tick early
    // makes h=960 > 959 cap → strictly rejected → falls to q. (720 ticks) +
    // trailing rest fill.
    //
    // Plan 30-07's TPQN-relative tolerance band fixes this for all grid sizes.
    [Fact]
    public void Half_Note_When_Next_Note_Is_One_Tick_Early_Still_Snaps_To_H()
    {
        // Half at 0..959. Follower starts at 959 (1 tick early). End of bar at 1920.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,   endTick: 959)
            .AddNote(channel: 0, pitch: 62, startTick: 959, endTick: 1920)
            .Build();

        var result = Quantizer.Quantize(midi);

        Assert.Single(result.Tracks);
        var notes = result.Tracks[0].Bars
            .SelectMany(b => b.Elements)
            .OfType<NoteElement>()
            .ToList();
        Assert.NotEmpty(notes);

        // First note: jittered half → must still snap to ("h", false). On HEAD
        // this fails because availableTicks = 959 - 0 = 959, and
        // SnapDurationCapped strictly rejects h-grid (960 > 959).
        Assert.Equal("h", notes[0].DurationSuffix);
        Assert.False(notes[0].IsDotted,
            $"Bug B Defect 1: a half note whose follower starts 1 tick early (gap=959 < h-grid=960) must still snap to ('h', false). On HEAD it falls back to ('q', dotted) because SnapDurationCapped strictly rejects gridTicks > capTicks with zero tolerance.");
    }
}
