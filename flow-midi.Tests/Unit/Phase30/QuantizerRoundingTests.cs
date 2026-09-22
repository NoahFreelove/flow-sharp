// MIDI quantization contract: trim globally empty leading bars, represent rests,
// and preserve note timing when melodic channels are divided into hand/voice tracks.
// See docs/decisions/2026-09-20-baseline-compatibility.md.

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
        // in an imported file — 5+ rests for sub-grid gaps. Plan 30-07
        // collapses adjacent same-suffix rests into one.
        Assert.True(restCount <= 1,
            $"Bug B Defect 2 (AddRests over-emission): a 3-quarter-rest gap after a quarter note must compress to a single auto-fit '_' rest (Plan 30-07 target). On HEAD the AddRests inner-loop emits {restCount} RestElement entries because the 'evenly divides gap' tolerance accepts the largest count that fits.");
    }

    // Current contract: preserve hand separation and note timing across voices.
    // See docs/decisions/2026-09-20-baseline-compatibility.md.
    [Fact]
    public void Melodic_Hands_Preserve_Note_Onsets_And_Durations()
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

        Assert.Equal(new[] { "track_ch1_rh", "track_ch1_lh" }, result.Tracks.Select(t => t.Name));
        var notes = new List<(string Name, long Start, long Duration)>();
        foreach (var track in result.Tracks)
        {
            Assert.Equal(0, track.Channel);
            Assert.False(track.IsDrumTrack);
            foreach (var bar in track.Bars)
            {
                long cursor = bar.BarNumber * 4L * Tpqn;
                foreach (var element in bar.Elements)
                {
                    long duration = element.DurationTicks(Tpqn);
                    if (element is NoteElement note)
                        notes.Add((note.NoteName, cursor, duration));
                    cursor += duration;
                }
            }
        }
        Assert.Equal(new[] { ("C2", 0L, 480L), ("C3", 480L, 480L),
            ("C4", 960L, 480L), ("C5", 1440L, 480L) }, notes.OrderBy(n => n.Start));
    }
}
