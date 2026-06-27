// Plan 30-06 Task 2 — GREEN-on-HEAD smoke facts. These prove:
//   1. flow-midi.Tests can reach Quantizer (internal static class) via
//      InternalsVisibleTo so subsequent RED facts can call Quantize.
//   2. MidiFixtureBuilder produces a MidiFile with the expected shape so
//      subsequent RED facts can synthesize Bug B scenarios.
//
// If either fact regresses to RED, the rest of the Phase 30 / Bug B fact
// suite (QuantizerSnapDurationTests, QuantizerRoundingTests,
// FlowGeneratorStructureTests in Plans 30-07/08) becomes uninterpretable —
// failures could be caused by harness breakage rather than the production
// code under test. Keep these GREEN at all times.

using FlowMidi.Conversion;
using FlowMidi.Midi;
using FlowMidi.Tests.Fixtures;
using Xunit;

namespace FlowMidi.Tests.Unit.Phase30;

public class HarnessSmokeFacts
{
    [Fact]
    public void SuffixToTicks_Quarter_At_Tpqn_480_Returns_480()
    {
        // Sanity: the Quantizer's tick math for a plain quarter at TPQN=480
        // must round-trip to exactly 480 ticks. This is the constant the
        // entire Bug B Defect 1 test family depends on.
        long ticks = Quantizer.SuffixToTicks("q", false, 480);
        Assert.Equal(480L, ticks);
    }

    [Fact]
    public void Builder_Constructs_MidiFile_With_Expected_Events()
    {
        // One tempo + one time-sig + one quarter note → 1 track, 4 events
        // (TempoEvent, TimeSignatureEvent, NoteOnEvent, NoteOffEvent).
        // Verifies the builder's tick sorting + record construction path.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(480)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0, endTick: 480)
            .Build();

        Assert.Equal(480, midi.TicksPerQuarterNote);
        Assert.Single(midi.Tracks);
        Assert.Equal(4, midi.Tracks[0].Events.Count);
        Assert.Contains(midi.Tracks[0].Events, e => e is TempoEvent);
        Assert.Contains(midi.Tracks[0].Events, e => e is TimeSignatureEvent);
        Assert.Contains(midi.Tracks[0].Events, e => e is NoteOnEvent);
        Assert.Contains(midi.Tracks[0].Events, e => e is NoteOffEvent);
    }
}
