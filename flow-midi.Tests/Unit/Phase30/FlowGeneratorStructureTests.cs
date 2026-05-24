// Plan 30-06 Task 3 — RED-on-HEAD facts pinning Bug B emit-structure defects
// in FlowGenerator.cs.
//
// References:
// - .planning/debug/midi-import-quarter-quantize.md (composer had to sed
//   `(play output)` away to make the imported .flow output useful)
// - .planning/phases/30-flow-cli-formal-install/30-RESEARCH.md Bug B Scope
//   Assessment Layer 3 — FlowGenerator emit adjustments.
// - SPEC-5: "one `Sequence trackN = | ... |` per MIDI track inside a single
//   `section roundtrip { ... }`"
//
// Three pins:
//   1. `(play output)` trailer must NOT appear in midi2flow output.
//      FlowGenerator.cs:123 currently emits it unconditionally.
//   2. One Sequence per source track / channel — no _rh / _lh suffixes
//      (caused by the AddSplitTracks heuristic in Quantizer; Plan 30-08
//      removes it).
//   3. Auto-fit duration suffix-elision (CanAutoFit at FlowGenerator.cs:239)
//      must NOT trigger when round-trip fidelity is requested — every note
//      token must carry an explicit duration suffix. Plan 30-08 adds an
//      --explicit-durations flag, default ON for midi2flow.
//
// Do NOT [Skip] any fact.

using FlowMidi.Conversion;
using FlowMidi.Tests.Fixtures;
using Xunit;

namespace FlowMidi.Tests.Unit.Phase30;

public class FlowGeneratorStructureTests
{
    const int Tpqn = 480;

    // Pins Bug B emit-structure defect: `(play output)` trailer in midi2flow output.
    //
    // RED-on-HEAD: FlowGenerator.cs:123 unconditionally emits
    // `(play output)`. For midi2flow the generated file should be a
    // round-trip artifact, not an auto-playing script; the composer adds
    // `(play output)` themselves when ready (or replaces with writeWav).
    //
    // Plan 30-08 either always drops the trailer (RESEARCH recommendation)
    // or gates it behind a `--with-play-trailer` flag (default off).
    [Fact]
    public void Generated_Output_Has_No_Play_Output_Trailer_When_Round_Trip_Mode()
    {
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddFourQuarterNotes(channel: 0, pitch: 60)
            .Build();

        var qr = Quantizer.Quantize(midi);
        // Plan 30-08: round-trip mode drops the `(play output)` trailer.
        // Non-roundTrip mode preserves it for backward-compat with the existing
        // `flow-midi` CLI (see flow-midi/Program.cs line 79 — default-arg call site).
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid", roundTrip: true);

        Assert.DoesNotContain("(play output)", source);
    }

    // Pins SPEC-5 emit shape: one Sequence per source track / channel,
    // no _rh / _lh sub-track suffixes.
    //
    // Fixture: Format-0 single track with two channels, each channel >2 octaves
    // wide. On HEAD: AddSplitTracks splits each channel into _rh + _lh →
    // 4 sequences. Target: 2 sequences (one per channel).
    [Fact]
    public void One_Sequence_Per_Track_Channel_No_RH_LH_Suffix()
    {
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            // Channel 1: C2..C5 (>2 octaves) — triggers split on HEAD.
            .AddNote(channel: 0, pitch: 36, startTick: 0,    endTick: 480)
            .AddNote(channel: 0, pitch: 60, startTick: 480,  endTick: 960)
            .AddNote(channel: 0, pitch: 72, startTick: 960,  endTick: 1440)
            .AddNote(channel: 0, pitch: 48, startTick: 1440, endTick: 1920)
            // Channel 2: C3..C6 (>2 octaves) — triggers split on HEAD.
            .AddNote(channel: 1, pitch: 48, startTick: 0,    endTick: 480)
            .AddNote(channel: 1, pitch: 72, startTick: 480,  endTick: 960)
            .AddNote(channel: 1, pitch: 84, startTick: 960,  endTick: 1440)
            .AddNote(channel: 1, pitch: 60, startTick: 1440, endTick: 1920)
            .Build();

        var qr = Quantizer.Quantize(midi);
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid");

        // Count "Sequence " declarations.
        int sequenceCount = System.Text.RegularExpressions.Regex.Matches(source, @"\bSequence\s+\w+\s*=").Count;
        Assert.Equal(2, sequenceCount);

        // No _rh or _lh in the generated source.
        Assert.DoesNotContain("_rh", source);
        Assert.DoesNotContain("_lh", source);
    }

    // Pins SPEC-5 explicit-durations contract for midi2flow round-trip.
    //
    // RED-on-HEAD: when every note shares the same duration, CanAutoFit at
    // FlowGenerator.cs:239 returns true and FormatBar elides the duration
    // suffix from every token. The result depends on bar size to reconstruct
    // durations, which loses round-trip determinism.
    //
    // Plan 30-08 must add explicit-durations mode (default ON for midi2flow)
    // so every note token carries its `q` / `e` / etc. suffix.
    [Fact]
    public void No_Auto_Fit_Elision_When_All_Quarters_For_Round_Trip()
    {
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddFourQuarterNotes(channel: 0, pitch: 60)
            .Build();

        var qr = Quantizer.Quantize(midi);
        // Plan 30-08: round-trip mode disables CanAutoFit elision so every note
        // token carries an explicit duration suffix — auto-fit's implicit
        // bar-derived duration reconstruction loses round-trip determinism.
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid", roundTrip: true);

        // Find the note-stream line(s) — every note token must carry a
        // duration suffix. The four pitches we authored are C4, D4, E4, F4.
        // On HEAD with auto-fit, the line reads `| C4 D4 E4 F4 |` (no `q`s).
        // Target: `| C4q D4q E4q F4q |`.
        Assert.Contains("C4q", source);
        Assert.Contains("D4q", source);
        Assert.Contains("E4q", source);
        Assert.Contains("F4q", source);
    }

    // Companion fact: mixed-duration tracks already preserve explicit durations
    // on HEAD (CanAutoFit returns false). GREEN-on-HEAD baseline — proves the
    // "explicit duration" mode is the existing-but-conditional default.
    [Fact]
    public void Mixed_Q_E_Track_Has_Explicit_Durations_On_HEAD_Baseline()
    {
        // 1 quarter, 2 eighths in bar 1 — mixed durations defeat CanAutoFit.
        var midi = new MidiFixtureBuilder()
            .WithTpqn(Tpqn)
            .WithFormat(0)
            .AddTempoEvent(120.0)
            .AddTimeSignatureEvent(4, 4)
            .AddNote(channel: 0, pitch: 60, startTick: 0,   endTick: 480) // C4 quarter
            .AddNote(channel: 0, pitch: 62, startTick: 480, endTick: 720) // D4 eighth
            .AddNote(channel: 0, pitch: 64, startTick: 720, endTick: 960) // E4 eighth
            .Build();

        var qr = Quantizer.Quantize(midi);
        var source = FlowGenerator.Generate(midi, qr, "fixture.mid");

        Assert.Contains("C4q", source);
        Assert.Contains("D4e", source);
        Assert.Contains("E4e", source);
    }
}
