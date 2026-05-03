using System;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-14 acceptance Facts pinning legato(Sequence, Double) — extends each note's rendered
/// duration by an overlap factor without moving onsets (CONTEXT D-01..D-03).
///
/// Decisions referenced (locked in 22-CONTEXT.md):
///   D-01 — legato(seq, 0.5) extends each note's rendered duration to 1.5× (overlap factor)
///   D-02 — legato preserves note onsets (next-note onset unchanged); polyphonic mix in
///          SongRenderer handles overlap additively
///   Pitfall 3 — extending duration MUST NOT move onsets; achieved via per-note
///          DurationOverlap field read AFTER ToTimeline produces onsets
///
/// Tests 1-2 + With(...) pin the defaulted-parameter migration shape.
/// Tests 3-4 verify ToTimeline/onset invariance + the BarRenderer integration semantics.
/// Tests 5-6 verify edge cases (empty sequence, identity at overlap=0).
/// </summary>
public class LegatoFacts
{
    // ===== Test 1-2 + With() — direct ctor / property pinning =====

    [Fact]
    public void DurationOverlap_DefaultsTo0()
    {
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false);
        Assert.Equal(0.0, n.DurationOverlap);
    }

    [Fact]
    public void DurationOverlap_OptionalCtorParam_AcceptedAtEndOfSignature()
    {
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            durationOverlap: 0.5);
        Assert.Equal(0.5, n.DurationOverlap);
    }

    [Fact]
    public void With_DurationOverlap_PreservesOtherFields()
    {
        // Builder helper rollback-independence (Phase 22 CONTEXT line 18):
        // calling With(durationOverlap: …) overrides only DurationOverlap and copies
        // every other field through unchanged — including 22-05's OnsetOffset and
        // sibling 22-06 PortamentoMs.
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            velocity: 0.7, onsetOffset: 0.1, portamentoMs: 50.0);
        var n2 = n.With(durationOverlap: 0.5);
        Assert.Equal(0.5, n2.DurationOverlap);
        Assert.Equal(0.1, n2.OnsetOffset);   // preserved by With()
        Assert.Equal(50.0, n2.PortamentoMs); // preserved by With()
        Assert.Equal(0.7, n2.Velocity);
    }

    // ===== Test 3-4 — engine-eval through registered legato overload =====

    private const string SmokePrelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    [Fact]
    public void OverlapHalf_PropagatesDurationOverlapField()
    {
        // CONTEXT D-01 + D-02: legato(seq, 0.5) sets DurationOverlap=0.5 on every note,
        // BarRenderer reads this AFTER ToTimeline produces onsets (Pitfall 3 honored
        // because the field is per-note, not a duration mutation that would cascade).
        // Verify: every note's DurationOverlap == 0.5 in the resulting sequence.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence smooth = (legato src 0.5)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        var smooth = runner.GetVariable("smooth").As<SequenceData>();
        Assert.NotEmpty(smooth.Bars);
        foreach (var bar in smooth.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                Assert.Equal(0.5, note.DurationOverlap, 6);
            }
        }
    }

    [Fact]
    public void OnsetsUnchanged()
    {
        // Pitfall 3: legato extends duration but does NOT move onsets. Compare
        // ToTimeline output before vs after legato — onset positions identical
        // because legato sets DurationOverlap (consumed at render time) NOT
        // DurationValue/DurationFraction (which would cascade through ToTimeline).
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence smooth = (legato src 1.0)
");
        Assert.Equal(0, errorCount);
        var src = runner.GetVariable("src").As<SequenceData>();
        var smooth = runner.GetVariable("smooth").As<SequenceData>();
        Assert.Equal(src.Bars.Count, smooth.Bars.Count);
        for (int b = 0; b < src.Bars.Count; b++)
        {
            var srcLine = src.Bars[b].ToTimeline();
            var smoothLine = smooth.Bars[b].ToTimeline();
            Assert.Equal(srcLine.Count, smoothLine.Count);
            for (int i = 0; i < srcLine.Count; i++)
            {
                Assert.Equal(srcLine[i].offsetBeats, smoothLine[i].offsetBeats, 6);
            }
        }
    }

    // ===== Test 5-6 — edge cases =====

    [Fact]
    public void Legato_OnSingleNoteSequence_PropagatesField()
    {
        // Charitable smoke: minimal valid input (one note) goes through cleanly with no
        // exception and DurationOverlap stamped on the lone note. Pitfall 4 / 12-05 noted
        // that bare `| |` is not valid Flow syntax — Flow requires at least one note in a
        // note stream — so this Fact uses a one-note input instead.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q |
Sequence smooth = (legato src 0.5)
");
        Assert.Equal(0, errorCount);
        var smooth = runner.GetVariable("smooth").As<SequenceData>();
        int totalNotes = 0;
        foreach (var bar in smooth.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                Assert.Equal(0.5, note.DurationOverlap, 6);
                totalNotes++;
            }
        }
        Assert.True(totalNotes >= 1, $"expected at least 1 note, got {totalNotes}");
    }

    [Fact]
    public void Legato_OverlapZero_IsIdentityOfDurationOverlapField()
    {
        // overlap=0 → DurationOverlap=0 on every note (effectively no-op at render time;
        // BarRenderer's `if (DurationOverlap > 0.0)` guard short-circuits).
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence smooth = (legato src 0.0)
");
        Assert.Equal(0, errorCount);
        var smooth = runner.GetVariable("smooth").As<SequenceData>();
        foreach (var bar in smooth.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                Assert.Equal(0.0, note.DurationOverlap, 6);
            }
        }
    }

    // ===== Test 7 — composition with other 22-06 sibling slot (portamento) =====

    [Fact]
    public void Legato_AndPortamento_Compose()
    {
        // RESEARCH Open Question 4: chaining (legato (portamento seq X) Y) preserves both flags.
        // Each transform calls With(...) naming only its own slot, so the other slot survives.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence glide = (portamento src 100ms)
Sequence both = (legato glide 0.5)
");
        Assert.Equal(0, errorCount);
        var both = runner.GetVariable("both").As<SequenceData>();
        Assert.NotEmpty(both.Bars);
        foreach (var bar in both.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                Assert.Equal(0.5,   note.DurationOverlap, 6);
                Assert.Equal(100.0, note.PortamentoMs, 6);
            }
        }
    }
}
