// sweep-0614 — three confirmed midi2flow fidelity defects:
//   1. Note velocity was silently discarded (every imported note flattened to the
//      0.63 default), losing the entire dynamic shape — FlowGenerator now emits a
//      bucketed dynamic token (ppp..fff) before notes/chords.
//   2. A Format-1 track with ANY ch9 note was flagged all-drum and dropped wholesale,
//      losing melody — Quantizer now splits Format-1/2 tracks by channel like Format 0.
//   3. midi2flow reported exit 0 with a comment-only file when all tracks were
//      dropped — FlowGenerator.GenerateWithStats exposes PlayableTrackCount so the
//      CLIs can warn + return a non-zero exit.

using System.Linq;
using FlowMidi.Conversion;
using FlowMidi.Tests.Fixtures;
using Xunit;

namespace FlowMidi.Tests.Unit.Sweep0614;

public class MidiImportFidelityTests
{
    const int Tpqn = 480;

    // ===== Bug 1: velocity → dynamic markings =====

    [Fact]
    public void Velocity_EmitsDynamicMarkings_NotFlat()
    {
        // C4 vel20 (pp) / D4 vel120 (fff) / E4 vel20 (pp) / F4 vel120 (fff).
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,         endTick: Tpqn,     velocity: 20)
            .AddNote(channel: 0, pitch: 62, startTick: Tpqn,      endTick: 2 * Tpqn, velocity: 120)
            .AddNote(channel: 0, pitch: 64, startTick: 2 * Tpqn,  endTick: 3 * Tpqn, velocity: 20)
            .AddNote(channel: 0, pitch: 65, startTick: 3 * Tpqn,  endTick: 4 * Tpqn, velocity: 120)
            .Build();

        var qr = Quantizer.Quantize(midi);
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid", roundTrip: true);

        // The dynamic shape must survive — vel20 → pp, vel120 → fff.
        Assert.Contains("pp", source);
        Assert.Contains("fff", source);
        // The note-stream line must carry the dynamic tokens before the notes.
        var streamLine = source.Split('\n').First(l => l.Contains("C4"));
        Assert.Contains("pp C4", streamLine.Replace("  ", " "));
    }

    [Fact]
    public void UniformVelocity_EmitsSingleDynamicTokenPerBar()
    {
        // All four notes at vel100 (96..111 → ff). The per-bar sticky should emit
        // exactly ONE "ff" token, with the remaining three notes inheriting it.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddFourQuarterNotes(channel: 0, pitch: 60)   // default vel 100 → ff
            .Build();

        var qr = Quantizer.Quantize(midi);
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid", roundTrip: true);

        var streamLine = source.Split('\n').First(l => l.Contains("C4"));
        // Count standalone "ff" dynamic tokens (space-delimited) — exactly one for the bar.
        int ffTokens = streamLine.Split(' ').Count(tok => tok.Trim() == "ff");
        Assert.Equal(1, ffTokens);
    }

    [Fact]
    public void DynamicBuckets_RoundTripThroughFlowParser()
    {
        // The emitted dynamic tokens must be re-parseable by the Flow note-stream
        // parser. A vel that buckets to "pp" must appear and the note still parse.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,     endTick: Tpqn,     velocity: 10)  // ppp
            .AddNote(channel: 0, pitch: 62, startTick: Tpqn,  endTick: 2 * Tpqn, velocity: 127) // fff
            .Build();

        var qr = Quantizer.Quantize(midi);
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid", roundTrip: true);
        Assert.Contains("ppp", source);
        Assert.Contains("fff", source);
    }

    // ===== Bug 2: Format-1 channel-9 must not nuke the melodic part =====

    [Fact]
    public void Format1_StrayDrumHit_DoesNotDropMelody()
    {
        // One MTrk: C4/D4/E4 on ch0 (melody) + one snare (note 38) on ch9.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(1)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,        endTick: Tpqn)       // C4
            .AddNote(channel: 0, pitch: 62, startTick: Tpqn,     endTick: 2 * Tpqn)   // D4
            .AddNote(channel: 9, pitch: 38, startTick: 2 * Tpqn, endTick: 2 * Tpqn + 120) // stray snare
            .AddNote(channel: 0, pitch: 64, startTick: 2 * Tpqn, endTick: 3 * Tpqn)   // E4
            .Build();

        var qr = Quantizer.Quantize(midi);

        // The melodic ch0 notes must survive as a non-drum track (before the fix the
        // entire MTrk was flagged IsDrumTrack and dropped → "no playable tracks").
        var melodicTracks = qr.Tracks.Where(t => !t.IsDrumTrack).ToList();
        Assert.NotEmpty(melodicTracks);
        var melodicPitches = melodicTracks
            .SelectMany(t => t.Bars)
            .SelectMany(b => b.Elements)
            .OfType<NoteElement>()
            .Select(n => n.NoteName)
            .ToList();
        Assert.Contains("C4", melodicPitches);
        Assert.Contains("D4", melodicPitches);
        Assert.Contains("E4", melodicPitches);

        // The ch9 snare is still routed to a separate drums track.
        Assert.Contains(qr.Tracks, t => t.IsDrumTrack);

        // End-to-end: the generated .flow has playable tracks (not the comment-only file).
        var result = FlowGenerator.GenerateWithStats(midi, qr, "mixed.mid", roundTrip: true);
        Assert.True(result.PlayableTrackCount >= 1);
        Assert.Contains("C4", result.Source);
    }

    [Fact]
    public void Format1_PureDrumTrack_StillClassifiedDrum()
    {
        // A pure ch9 track must still be a single drums track (unchanged behavior).
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(1)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 9, pitch: 36, startTick: 0,    endTick: 120)
            .AddNote(channel: 9, pitch: 38, startTick: Tpqn, endTick: Tpqn + 120)
            .Build();

        var qr = Quantizer.Quantize(midi);
        Assert.All(qr.Tracks, t => Assert.True(t.IsDrumTrack));
        Assert.DoesNotContain(qr.Tracks, t => !t.IsDrumTrack);
    }

    [Fact]
    public void Format1_PureMelodicTrack_Unchanged()
    {
        // A pure ch0 track must produce one melodic (non-drum) track, no drums track.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(1)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddFourQuarterNotes(channel: 0, pitch: 60)
            .Build();

        var qr = Quantizer.Quantize(midi);
        Assert.DoesNotContain(qr.Tracks, t => t.IsDrumTrack);
        Assert.Contains(qr.Tracks, t => !t.IsDrumTrack);
    }

    // ===== Bug 3: honest no-playable-tracks signal =====

    [Fact]
    public void DrumOnlyMidi_ReportsZeroPlayableTracks()
    {
        // A drum-only Format-1 file → comment-only artifact. GenerateWithStats must
        // report PlayableTrackCount == 0 so the CLI can warn + exit non-zero.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(1)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 9, pitch: 36, startTick: 0,    endTick: 120)
            .AddNote(channel: 9, pitch: 38, startTick: Tpqn, endTick: Tpqn + 120)
            .Build();

        var qr = Quantizer.Quantize(midi);
        var result = FlowGenerator.GenerateWithStats(midi, qr, "drums.mid", roundTrip: true);

        Assert.Equal(0, result.PlayableTrackCount);
        Assert.True(result.DroppedDrumTrackCount >= 1);
        Assert.Contains("no playable tracks found", result.Source);
    }

    [Fact]
    public void PlayableMidi_ReportsNonZeroPlayableTracks()
    {
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddFourQuarterNotes(channel: 0, pitch: 60)
            .Build();

        var qr = Quantizer.Quantize(midi);
        var result = FlowGenerator.GenerateWithStats(midi, qr, "ok.mid", roundTrip: true);
        Assert.True(result.PlayableTrackCount >= 1);
        Assert.DoesNotContain("no playable tracks found", result.Source);
    }
}
