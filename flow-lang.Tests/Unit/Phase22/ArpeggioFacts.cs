using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-10 acceptance Facts pinning the 4-arg arpeggio overload (Chord, NoteValue, direction, pattern).
///
/// Decisions referenced:
///   D-07 (charitable interpretation, project memory): random direction falls back to "up"
///   in v1.3 per RESEARCH Pitfall 7 (seeded random arpeggio deferred to v1.4 to preserve
///   byte-identical determinism).
///   RESEARCH Assumption A8: chord-tone / scale-tone pattern strings route to "linear" in
///   v1.3 (deferred per RESEARCH §Future Requirements).
///
/// Facts drive the built-in via <see cref="FlowEngineRunner"/> because arpeggio() returns a
/// SequenceData whose internal MusicalNotes structure is the assertion target. We extract the
/// resulting Sequence variable from the global frame and inspect bars/notes directly — stdout
/// substring checks would be too coarse since SequenceData.ToString() reports only bar/beat counts.
///
/// Phase 22 plan 22-01 — RED state: 7 of 8 Facts must FAIL before Task 2 lands the 4-arg overload.
/// `Existing_TwoArgOverload_Unchanged` may pass pre-Task-2 since the 2-arg signature already
/// exists (regression gate proving Task 2 doesn't break the existing path).
/// </summary>
[Collection("FlowScripts")]
public class ArpeggioFacts
{
    /// <summary>
    /// Helper: extracts the MusicalNoteData list from the first bar of a Sequence variable.
    /// Cmaj7 = C, E, G, B (4 notes); the registered arpeggio builds a single-bar Sequence,
    /// so MusicalNotes of bar 0 is the canonical assertion target.
    /// </summary>
    private static IReadOnlyList<MusicalNoteData> GetNotes(FlowEngineRunner runner, string seqVar)
    {
        var seq = runner.GetVariable(seqVar).As<SequenceData>();
        Assert.NotEmpty(seq.Bars);
        return seq.Bars[0].MusicalNotes;
    }

    [Fact]
    public void UpLinear_FourNoteAscent()
    {
        // (arpeggio Cmaj7 QUARTER "up" "linear") → C, E, G, B at quarter rate.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence asc = (arpeggio Cmaj7 QUARTER ""up"" ""linear"")
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", System.StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");

        var notes = GetNotes(runner, "asc");
        Assert.Equal(4, notes.Count);

        // Cmaj7 letter sequence: C, E, G, B (NoteName property is the upper-case letter)
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('E', notes[1].NoteName);
        Assert.Equal('G', notes[2].NoteName);
        Assert.Equal('B', notes[3].NoteName);

        // Quarter rate enum = 2 per NoteValueType.Value.QUARTER
        foreach (var n in notes)
        {
            Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue);
        }
    }

    [Fact]
    public void DirectionDownReversesNotes()
    {
        // (arpeggio Cmaj7 QUARTER "down" "linear") → B, G, E, C
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence desc = (arpeggio Cmaj7 QUARTER ""down"" ""linear"")
");
        Assert.Equal(0, errorCount);

        var notes = GetNotes(runner, "desc");
        Assert.Equal(4, notes.Count);
        Assert.Equal('B', notes[0].NoteName);
        Assert.Equal('G', notes[1].NoteName);
        Assert.Equal('E', notes[2].NoteName);
        Assert.Equal('C', notes[3].NoteName);
    }

    [Fact]
    public void DirectionUpdown_DoesNotRepeatApex()
    {
        // (arpeggio Cmaj7 EIGHTH "updown" "linear") → 4 up + 3 down (apex B not repeated) = 7 notes.
        // Order: C E G B  G E C
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence ud = (arpeggio Cmaj7 EIGHTH ""updown"" ""linear"")
");
        Assert.Equal(0, errorCount);

        var notes = GetNotes(runner, "ud");
        Assert.Equal(7, notes.Count);
        var letters = notes.Select(n => n.NoteName).ToArray();
        Assert.Equal(new[] { 'C', 'E', 'G', 'B', 'G', 'E', 'C' }, letters);
    }

    [Fact]
    public void DirectionDownup_DoesNotRepeatNadir()
    {
        // (arpeggio Cmaj7 EIGHTH "downup" "linear") → 4 down + 3 up (nadir C not repeated) = 7 notes.
        // Order: B G E C  E G B
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence du = (arpeggio Cmaj7 EIGHTH ""downup"" ""linear"")
");
        Assert.Equal(0, errorCount);

        var notes = GetNotes(runner, "du");
        Assert.Equal(7, notes.Count);
        var letters = notes.Select(n => n.NoteName).ToArray();
        Assert.Equal(new[] { 'B', 'G', 'E', 'C', 'E', 'G', 'B' }, letters);
    }

    [Fact]
    public void Pattern_ChordTone_RoutesToLinear()
    {
        // RESEARCH Assumption A8: pattern "chord-tone" produces same notes as "linear" in v1.3.
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence linear = (arpeggio Cmaj7 QUARTER ""up"" ""linear"")
Sequence chordTone = (arpeggio Cmaj7 QUARTER ""up"" ""chord-tone"")
");
        Assert.Equal(0, errorCount);

        var linear = GetNotes(runner, "linear");
        var chordTone = GetNotes(runner, "chordTone");
        Assert.Equal(linear.Count, chordTone.Count);
        for (int i = 0; i < linear.Count; i++)
        {
            Assert.Equal(linear[i].NoteName, chordTone[i].NoteName);
            Assert.Equal(linear[i].Octave, chordTone[i].Octave);
            Assert.Equal(linear[i].Alteration, chordTone[i].Alteration);
        }
    }

    [Fact]
    public void Pattern_ScaleTone_RoutesToLinear()
    {
        // RESEARCH Assumption A8: pattern "scale-tone" produces same notes as "linear" in v1.3.
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence linear = (arpeggio Cmaj7 QUARTER ""up"" ""linear"")
Sequence scaleTone = (arpeggio Cmaj7 QUARTER ""up"" ""scale-tone"")
");
        Assert.Equal(0, errorCount);

        var linear = GetNotes(runner, "linear");
        var scaleTone = GetNotes(runner, "scaleTone");
        Assert.Equal(linear.Count, scaleTone.Count);
        for (int i = 0; i < linear.Count; i++)
        {
            Assert.Equal(linear[i].NoteName, scaleTone[i].NoteName);
            Assert.Equal(linear[i].Octave, scaleTone[i].Octave);
            Assert.Equal(linear[i].Alteration, scaleTone[i].Alteration);
        }
    }

    [Fact]
    public void Direction_Random_FallsBackToUp()
    {
        // RESEARCH Pitfall 7 / D-07: "random" falls back to "up" order in v1.3 (deferred to v1.4).
        // Charitable interpretation: do not error, do not introduce non-determinism.
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
use ""@notation""
Sequence up = (arpeggio Cmaj7 QUARTER ""up"" ""linear"")
Sequence rnd = (arpeggio Cmaj7 QUARTER ""random"" ""linear"")
");
        Assert.Equal(0, errorCount);

        var up = GetNotes(runner, "up");
        var rnd = GetNotes(runner, "rnd");
        Assert.Equal(up.Count, rnd.Count);
        for (int i = 0; i < up.Count; i++)
        {
            Assert.Equal(up[i].NoteName, rnd[i].NoteName);
            Assert.Equal(up[i].Octave, rnd[i].Octave);
            Assert.Equal(up[i].Alteration, rnd[i].Alteration);
        }
    }

    [Fact]
    public void Existing_TwoArgOverload_Unchanged()
    {
        // Regression gate: the existing 2-arg arpeggio(Chord, String) signature must remain
        // resolvable through the OverloadResolver after the 4-arg overload is added.
        // The existing 2-arg overload uses EIGHTH duration regardless of caller — verify shape.
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(@"
use ""@std""
Sequence two = (arpeggio Cmaj ""up"")
");
        Assert.Equal(0, errorCount);

        var notes = GetNotes(runner, "two");
        // Cmaj has 3 notes (C, E, G) — the existing 2-arg uses EIGHTH durations.
        Assert.Equal(3, notes.Count);
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('E', notes[1].NoteName);
        Assert.Equal('G', notes[2].NoteName);
        foreach (var n in notes)
        {
            Assert.Equal((int)NoteValueType.Value.EIGHTH, n.DurationValue);
        }
    }
}
