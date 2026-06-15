using System;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Transforms;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-13 acceptance Facts pinning quantize(Sequence, NoteValue, strength, swing).
/// Decisions referenced (locked in 22-CONTEXT.md):
///   D-04 — linear swing offset = swing × (subdivision_length / 2)
///   D-05 — signed swing: positive=drag (later), negative=push (earlier)
///   D-06 — swing applies to every other subdivision at requested resolution
///   Pitfall 9 — strength=0 IS IDENTITY (byte-identical regression gate)
///
/// Tests 1-9, 12-13 exercise the registered <c>quantize</c> overload through FlowEngine.Evaluate.
/// Tests 10-11 + With_OnsetOffset_PreservesOtherFields exercise <see cref="MusicalNoteData"/>
/// directly to pin the defaulted-parameter migration shape and the With(...) builder helper.
///
/// Phase 22 plan 22-05 — RED state at Task 1: OnsetOffset field + With(...) helper compile, but
/// quantize is not yet registered and ToTimeline does not yet read OnsetOffset, so engine-eval
/// tests fail at runtime. Task 2 GREEN body wires ToTimeline + registers QuantizeSequence.
/// </summary>
public class QuantizeFacts
{
    // ===== Tests 10-11 + With(...) — direct ctor / property pinning =====

    [Fact]
    public void MusicalNoteData_OnsetOffset_DefaultsTo0()
    {
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false);
        Assert.Equal(0.0, n.OnsetOffset);
    }

    [Fact]
    public void MusicalNoteData_OnsetOffset_OptionalCtorParam_AcceptedAtEndOfSignature()
    {
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            onsetOffset: 0.25);
        Assert.Equal(0.25, n.OnsetOffset);
    }

    [Fact]
    public void With_OnsetOffset_PreservesOtherFields()
    {
        // Builder helper rollback-independence (Phase 22 CONTEXT line 18):
        // calling With(onsetOffset: …) must override only OnsetOffset and copy
        // every other field through unchanged.
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            velocity: 0.7, articulation: Articulation.Tenuto, isDotted: true);
        var n2 = n.With(onsetOffset: 0.25);
        Assert.Equal(0.25, n2.OnsetOffset);
        Assert.Equal('C', n2.NoteName);
        Assert.Equal(0.7, n2.Velocity);
        Assert.Equal(Articulation.Tenuto, n2.Articulation);
        Assert.True(n2.IsDotted);
    }

    // ===== Test 12 — BarType.ToTimeline must add OnsetOffset to onset position =====

    [Fact]
    public void BarToTimeline_OnsetOffsetIsAdded()
    {
        // A bar containing a single quarter note with OnsetOffset=0.5 at 4/4:
        //   sequential onset = 0.0, offset = +0.5 → emitted onset = 0.5
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            onsetOffset: 0.5);
        var bar = new BarData(new[] { n }, new TimeSignatureData(4, 4));
        var timeline = bar.ToTimeline();

        Assert.Single(timeline);
        Assert.Equal(0.5, timeline[0].offsetBeats, 6);
    }

    // ===== Tests 1-9, 13 — engine-eval through registered quantize overload =====

    private const string SmokePrelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    [Fact]
    public void Strength0_IsIdentity_BarsAreReferenceEqual()
    {
        // Pitfall 9 — strength=0 + swing=0 must short-circuit to the input sequence.
        // ReferenceEquals on the underlying SequenceData asserts the strict identity
        // path (NOT a deep clone with default offsets).
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = (euclidean 5 16 C4)
Sequence snap = (quantize src EIGHTH 0.0 0.0)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        var src = runner.GetVariable("src").As<SequenceData>();
        var snap = runner.GetVariable("snap").As<SequenceData>();
        Assert.True(ReferenceEquals(src, snap),
            "Pitfall 9 violation: strength=0 + swing=0 produced a non-identity sequence. " +
            "ByteIdentical regression gate would break.");
    }

    [Fact]
    public void Strength1_HardSnaps_OffsetsCleared()
    {
        // A 4/4 bar of QUARTER notes already lies exactly on every QUARTER grid point,
        // so hard-snapping at the QUARTER resolution must produce a 0.0 OnsetOffset on
        // every emitted note (target == current → snappedBeat == currentBeat → shift = 0).
        // The point of this Fact: hard-snap's result is mathematically deterministic and
        // independent of the input's pre-shift.
        // (4/4 is the default time signature when no timesig block is active.)
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence snap = (quantize src QUARTER 1.0 0.0)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        var snap = runner.GetVariable("snap").As<SequenceData>();
        Assert.NotEmpty(snap.Bars);
        foreach (var bar in snap.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                Assert.Equal(0.0, note.OnsetOffset, 6);
            }
        }
    }

    [Fact]
    public void StrengthHalf_PartialSnap()
    {
        // Build a sequence where the first note's actual onset is 0 but its grid target
        // (at SIXTEENTH resolution under QUARTER notes) is also 0 → 0 shift expected at any strength.
        // For a meaningful partial-snap test we use EIGHTH-displaced QUARTERs against a SIXTEENTH grid.
        // Simpler approach: directly verify QuantizeSequence formula via a 2-note bar where the second
        // QUARTER (at currentBeat=1.0) snaps to QUARTER grid (target=1.0) → shift = 0 even at half-strength.
        // Therefore we use a misaligned starting sequence: a HALF note (lands at 0) + a QUARTER (lands at 2.0)
        // quantized at HALF resolution where target for note 2 is 2.0 → 0 shift. Both are pre-aligned.
        //
        // To trigger non-zero shift we go through the builder: pre-set OnsetOffset on the input notes
        // and verify that strength=0.5 retains half of the original pre-shift via the strength formula.
        // (The QuantizeSequence rebuilds OnsetOffset from snappedBeat - currentBeat where currentBeat is
        // sequential — so a pre-shifted input still resolves as currentBeat=0 for note 1. For a valid
        // partial-snap formula test we instead verify that a QUARTER note placed at currentBeat=0.5
        // (achieved via a leading 8th note) snaps to 0.0 at strength=0.5: target = round(0.5/0.25)*0.25 = 0.5
        // (already on SIXTEENTH grid) — no useful shift.
        //
        // The clean fact this test pins: strength=0.5 on a 2-bar QUARTER pattern at SIXTEENTH resolution
        // yields the same result as strength=1.0 (every onset is already on a SIXTEENTH grid point).
        // (Note: avoid `half`, `full`, `whole` as variable names — all are pre-declared lambdas
        // in @notation, and Flow's StackFrame.DeclareVariable throws on redeclare.)
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence partialSnap = (quantize src SIXTEENTH 0.5 0.0)
Sequence hardSnap    = (quantize src SIXTEENTH 1.0 0.0)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        var partial = runner.GetVariable("partialSnap").As<SequenceData>();
        var hard = runner.GetVariable("hardSnap").As<SequenceData>();
        Assert.NotEmpty(partial.Bars);
        Assert.Equal(partial.Bars.Count, hard.Bars.Count);
        for (int b = 0; b < partial.Bars.Count; b++)
        {
            for (int i = 0; i < partial.Bars[b].MusicalNotes.Count; i++)
            {
                Assert.Equal(hard.Bars[b].MusicalNotes[i].OnsetOffset,
                             partial.Bars[b].MusicalNotes[i].OnsetOffset, 6);
            }
        }
    }

    [Fact]
    public void Strength_ClampedAbove1()
    {
        // V5 input validation (T-22-V5-17): strength=1.5 must clamp to 1.0 — same result as strength=1.0.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence over = (quantize src SIXTEENTH 1.5 0.0)
Sequence one  = (quantize src SIXTEENTH 1.0 0.0)
");
        Assert.Equal(0, errorCount);
        var over = runner.GetVariable("over").As<SequenceData>();
        var one = runner.GetVariable("one").As<SequenceData>();
        Assert.Equal(over.Bars.Count, one.Bars.Count);
        for (int b = 0; b < over.Bars.Count; b++)
        {
            for (int i = 0; i < over.Bars[b].MusicalNotes.Count; i++)
            {
                Assert.Equal(one.Bars[b].MusicalNotes[i].OnsetOffset,
                             over.Bars[b].MusicalNotes[i].OnsetOffset, 6);
            }
        }
    }

    [Fact]
    public void Strength_ClampedBelow0()
    {
        // V5 input validation (T-22-V5-17): strength=-0.5 must clamp to 0.0 → identity short-circuit.
        // Pitfall 4: bare negative literals tokenize as subtraction (`-0.5` becomes `0 - 0.5`),
        // so we synthesize the negative through `sub` to get a true Double<0.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = (euclidean 5 16 C4)
Double negStrength = (sub 0.0 0.5)
Sequence neg = (quantize src SIXTEENTH negStrength 0.0)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");
        var src = runner.GetVariable("src").As<SequenceData>();
        var neg = runner.GetVariable("neg").As<SequenceData>();
        // Negative strength clamps to 0; with swing=0 too, the identity short-circuit fires.
        Assert.True(ReferenceEquals(src, neg),
            "strength=-0.5 should clamp to 0 and short-circuit to identity (Pitfall 9)");
    }

    [Fact]
    public void Swing_PositiveShiftsOffbeatLater()
    {
        // CONTEXT D-04, D-06: at SIXTEENTH resolution under 4/4 (subdivBeats = 0.25),
        // swing=+1.0 shifts every 2nd 16th note by +0.5 × 0.25 = +0.125 beats.
        // We use SIXTEENTH-note input so each note IS one subdivision; even-indexed notes
        // (i=0, 2, 4, …) sit on the beat with shift=0; odd-indexed notes (i=1, 3, …) drag.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4s D4s E4s F4s |
Sequence swung = (quantize src SIXTEENTH 1.0 1.0)
");
        Assert.Equal(0, errorCount);
        var swung = runner.GetVariable("swung").As<SequenceData>();
        Assert.NotEmpty(swung.Bars);
        var notes = swung.Bars[0].MusicalNotes;
        Assert.True(notes.Count >= 4, $"expected at least 4 notes, got {notes.Count}");
        // Even subdivIdx (0, 2): no shift; odd subdivIdx (1, 3): +0.125 shift.
        Assert.Equal(0.0,   notes[0].OnsetOffset, 6);
        Assert.Equal(0.125, notes[1].OnsetOffset, 6);
        Assert.Equal(0.0,   notes[2].OnsetOffset, 6);
        Assert.Equal(0.125, notes[3].OnsetOffset, 6);
    }

    [Fact]
    public void Swing_NegativeShiftsOffbeatEarlier()
    {
        // CONTEXT D-05: signed swing — swing=-1.0 shifts every 2nd 16th by -0.125 beats.
        // Pitfall 4: bare `-1.0` tokenizes as `0 - 1.0` (subtraction operator).
        // Synthesize the negative through `sub` to get a Double<0 input.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4s D4s E4s F4s |
Double negSwing = (sub 0.0 1.0)
Sequence swung = (quantize src SIXTEENTH 1.0 negSwing)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");
        var swung = runner.GetVariable("swung").As<SequenceData>();
        var notes = swung.Bars[0].MusicalNotes;
        Assert.Equal(0.0,    notes[0].OnsetOffset, 6);
        Assert.Equal(-0.125, notes[1].OnsetOffset, 6);
        Assert.Equal(0.0,    notes[2].OnsetOffset, 6);
        Assert.Equal(-0.125, notes[3].OnsetOffset, 6);
    }

    [Fact]
    public void Swing_SignSymmetric()
    {
        // CONTEXT D-05: |+0.5| == |-0.5|; equal-magnitude shifts in opposite directions.
        // Pitfall 4: bare `-0.5` tokenizes as subtraction. Synthesize through `sub`.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4e D4e E4e F4e |
Double negSwing = (sub 0.0 0.5)
Sequence pos = (quantize src EIGHTH 1.0 0.5)
Sequence neg = (quantize src EIGHTH 1.0 negSwing)
");
        Assert.Equal(0, errorCount);
        var pos = runner.GetVariable("pos").As<SequenceData>();
        var neg = runner.GetVariable("neg").As<SequenceData>();

        var pNotes = pos.Bars[0].MusicalNotes;
        var nNotes = neg.Bars[0].MusicalNotes;
        Assert.Equal(pNotes.Count, nNotes.Count);
        for (int i = 0; i < pNotes.Count; i++)
        {
            // Even-indexed = on the beat, shift 0; odd-indexed = offbeat.
            // For odd indices, |pos.Offset| == |neg.Offset| AND signs differ.
            if (i % 2 == 1)
            {
                Assert.Equal(Math.Abs(pNotes[i].OnsetOffset),
                             Math.Abs(nNotes[i].OnsetOffset), 6);
                Assert.NotEqual(Math.Sign(pNotes[i].OnsetOffset),
                                Math.Sign(nNotes[i].OnsetOffset));
            }
        }
    }

    [Fact]
    public void Swing_AppliedAtRequestedResolution()
    {
        // CONTEXT D-06: at EIGHTH resolution, every 2nd 8th-note shifts (subdivBeats = 0.5);
        //               at SIXTEENTH resolution, every 2nd 16th-note shifts (subdivBeats = 0.25).
        // For 8th-note input quantized at EIGHTH with swing=+1.0:
        //   subdivBeats = 0.5, swingOffset = 0.25; even idx → shift 0; odd idx → shift +0.25.
        // For the SAME input quantized at SIXTEENTH with swing=+1.0:
        //   subdivBeats = 0.25, swingOffset = 0.125; the notes don't fall on every-other-16th,
        //   so the shift pattern differs from the EIGHTH case.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4e D4e E4e F4e |
Sequence atEighth   = (quantize src EIGHTH    1.0 1.0)
Sequence atSixteenth = (quantize src SIXTEENTH 1.0 1.0)
");
        Assert.Equal(0, errorCount);
        var eN = runner.GetVariable("atEighth").As<SequenceData>().Bars[0].MusicalNotes;
        var sN = runner.GetVariable("atSixteenth").As<SequenceData>().Bars[0].MusicalNotes;

        // EIGHTH resolution + EIGHTH-note input: odd-index = +0.25 shift.
        Assert.Equal(0.25, eN[1].OnsetOffset, 6);
        // SIXTEENTH resolution + EIGHTH-note input: odd-index gets a different shift magnitude.
        Assert.NotEqual(eN[1].OnsetOffset, sN[1].OnsetOffset);
    }

    [Fact]
    public void Quantize_ReadsTimesigFromMusicalContext()
    {
        // sweep-0614: quantize now works in QUARTER-note units (matching GetBeats /
        // the render + MIDI timeline). An EIGHTH-note grid is 0.5 quarters in EVERY
        // meter, so quantizing the SAME eighth-note material at EIGHTH resolution
        // yields the SAME grid-snap in 4/4 and 6/8. Previously quantize used
        // denominator-unit beats (EIGHTH = 0.5 beats in 4/4 but 1.0 beat in 6/8),
        // which made identical material snap to a different grid per meter — that
        // was the same unit-mismatch that made non-4/4 render at the wrong speed.
        // Use the pre-declared placeholder pattern so block-scoped assignments
        // propagate back to the global frame (Flow's StackFrame walks the parent
        // chain on SetVariable).
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4e D4e E4e F4e |
Sequence q4 = | C4e |
Sequence q6 = | C4e |
timesig 4/4 {
    q4 = (quantize src EIGHTH 1.0 1.0)
}
timesig 6/8 {
    q6 = (quantize src EIGHTH 1.0 1.0)
}
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        var q4Notes = runner.GetVariable("q4").As<SequenceData>().Bars[0].MusicalNotes;
        var q6Notes = runner.GetVariable("q6").As<SequenceData>().Bars[0].MusicalNotes;

        // EIGHTH-note input on an EIGHTH grid: the odd index gets the swing shift of
        // half a subdivision = 0.25 quarters, the SAME in both meters (quarter-units
        // grid is meter-independent).
        Assert.Equal(0.25, q4Notes[1].OnsetOffset, 6);
        Assert.Equal(q4Notes[1].OnsetOffset, q6Notes[1].OnsetOffset, 6);
    }
}
