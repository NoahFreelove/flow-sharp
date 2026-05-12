// Plan 30-06 Task 3 — RED-on-HEAD facts pinning Bug B Defect 2 + Defect 3.
//
// References:
// - .planning/debug/midi-import-quarter-quantize.md
// - .planning/phases/30-flow-cli-formal-install/30-RESEARCH.md Bug B Scope Assessment
//   Defect 2: leading-empty-bar emission (Quantizer.cs:355-359 emits bars from
//     idx 0 regardless of first-note onset); AddRests (Quantizer.cs:604-637)
//     over-emits "_" rest tokens because the "evenly divides gap" tolerance
//     accepts large counts.
//   Defect 3: AddSplitTracks (Quantizer.cs:201-247) splits any track whose pitch
//     range exceeds 24 semitones into _rh / _lh sub-tracks; this violates the
//     SPEC-5 "one Sequence per source track" contract.
//
// Plan 30-07 turns these GREEN by adding a leading-empty-bar trim, fixing
// AddRests over-emission, and removing the AddSplitTracks heuristic.
//
// Do NOT [Skip] any fact.

using FlowMidi.Conversion;
using FlowMidi.Tests.Fixtures;
using Xunit;

namespace FlowMidi.Tests.Unit.Phase30;

public class QuantizerRoundingTests
{
    const int Tpqn = 480;

    // Pins Bug B Defect 2 — leading-empty-bar emission (Quantizer.cs:355-359
    // computes totalBars from maxTick globally and emits a bar for every index
    // from 0; the trailing trim at line 475 only handles the END).
    //
    // RED-on-HEAD: a fixture whose first note begins at the start of bar 2
    // (tick 1920) currently emits two bars — bar 0 (BarNumber=0) full of rests
    // and bar 1 (BarNumber=1) with the actual note. After Plan 30-07's leading
    // trim, bar 0 disappears and the first emitted bar has BarNumber=1.
    [Fact]
    public void Empty_Leading_Bars_Are_Trimmed()
    {
        // First note starts at tick 1920 = start of bar 2 (0-indexed bar 1).
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 1920, endTick: 2400) // quarter at start of bar 2
            .AddNote(channel: 0, pitch: 62, startTick: 2400, endTick: 2880)
            .AddNote(channel: 0, pitch: 64, startTick: 2880, endTick: 3360)
            .AddNote(channel: 0, pitch: 65, startTick: 3360, endTick: 3840)
            .Build();

        var result = Quantizer.Quantize(midi);

        Assert.Single(result.Tracks);
        var bars = result.Tracks[0].Bars;
        Assert.NotEmpty(bars);

        // The first emitted bar must have BarNumber == 1, not 0. On HEAD this
        // fails because QuantizeSpans emits bar 0 with rest-only contents.
        Assert.Equal(1, bars[0].BarNumber);

        // And bar 0 should not be present at all (no leading-rest bar).
        Assert.DoesNotContain(bars, b => b.BarNumber == 0);
    }

    // Pins Bug B Defect 2 — AddRests over-emission.
    //
    // RED-on-HEAD: a gap of ~1440 ticks (3 quarters = 3/4 of a bar at 4/4
    // TPQN=480) following a quarter note currently emits multiple "_" rest
    // tokens because AddRests' inner loop accepts the largest grid value that
    // "evenly divides" the gap under a tpqn*0.1 tolerance — and then emits
    // `count` rests of that smaller unit. For a 1440-tick gap, the algorithm
    // ends up emitting 3 (quarter) rests rather than ONE rest covering the
    // gap (a half + quarter, or a dotted-half, or one auto-fit "_").
    //
    // The exact emission shape depends on AddRests' "find a uniform unit"
    // logic. The assertion is intentionally LOOSE — at most 3 RestElement
    // entries for the gap region (matching the Phase 30-07 target of one
    // rest-per-beat at worst). On HEAD: 3+ are emitted; in pathological cases
    // (sub-grid gaps near 32nd-note boundaries) up to 8.
    [Fact]
    public void Rest_Of_Three_Quarters_Is_Few_Rests_Not_Many()
    {
        // Quarter at 0..480, then silence to end of bar at 1920 (1440 ticks of gap).
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0, endTick: 480)
            // Force the bar to actually be emitted by anchoring a note at the
            // start of bar 2; without this, bar 1 may be entirely rest-only
            // and the trailing-rest trim would drop it.
            .AddNote(channel: 0, pitch: 60, startTick: 1920, endTick: 2400)
            .Build();

        var result = Quantizer.Quantize(midi);

        Assert.Single(result.Tracks);
        var bar1 = result.Tracks[0].Bars.First(b => b.BarNumber == 0);
        var restCount = bar1.Elements.OfType<RestElement>().Count();

        // After the single quarter note, the bar should have at most 3 rests
        // (one per beat 2/3/4). On HEAD: AddRests emits 3 quarter-rests in
        // a tight case but can degenerate to many more for sub-grid gaps.
        //
        // The defect's stronger manifestation is the bar with `D4s. _ _ _ _ _`
        // in ragtime_imported.flow — 5+ rests for sub-grid gaps. Plan 30-07
        // collapses adjacent same-suffix rests into one.
        Assert.True(restCount <= 1,
            $"Bug B Defect 2 (AddRests over-emission): a 3-quarter-rest gap after a quarter note must compress to a single auto-fit '_' rest (Plan 30-07 target). On HEAD the AddRests inner-loop emits {restCount} RestElement entries because the 'evenly divides gap' tolerance accepts the largest count that fits.");
    }

    // Pins Bug B Defect 3 — RH/LH pitch-split heuristic (AddSplitTracks,
    // Quantizer.cs:201-247). SPEC-5 contract: "one Sequence per source track".
    //
    // RED-on-HEAD: a track whose pitch range exceeds 24 semitones (here C2..C5,
    // 36 semitones) is split into baseName + "_rh" and baseName + "_lh"
    // sub-tracks. Result: 2 tracks for 1 input channel.
    //
    // Plan 30-08 (or 30-07 — both depend on the AddSplitTracks removal) deletes
    // this heuristic. Composer-authored channel assignment is the source of
    // truth for RH/LH split, not heuristic pitch-range inference.
    [Fact]
    public void Two_Octave_Range_Does_Not_Split_RH_LH()
    {
        // Bass C2 (MIDI 36) to treble C5 (MIDI 72) — 36 semitones, all channel 0.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 36, startTick: 0,    endTick: 480)  // C2 (bass)
            .AddNote(channel: 0, pitch: 48, startTick: 480,  endTick: 960)  // C3
            .AddNote(channel: 0, pitch: 60, startTick: 960,  endTick: 1440) // C4 (middle)
            .AddNote(channel: 0, pitch: 72, startTick: 1440, endTick: 1920) // C5 (treble)
            .Build();

        var result = Quantizer.Quantize(midi);

        // On HEAD: result.Tracks.Count == 2 (track_ch1_rh + track_ch1_lh).
        // Target (after Plan 30-08 removes AddSplitTracks): exactly 1 track.
        Assert.Single(result.Tracks);
        Assert.DoesNotContain("_rh", result.Tracks[0].Name);
        Assert.DoesNotContain("_lh", result.Tracks[0].Name);
    }
}
