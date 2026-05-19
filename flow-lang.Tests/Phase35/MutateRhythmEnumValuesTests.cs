using System;
using System.Collections.Generic;
using FlowLang.StandardLibrary.Composition;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 HK-03 regression facts: <see cref="VariationFunctions.MutateRhythm"/>
/// must map each <see cref="NoteValueType.Value"/> enum integer to the
/// next-shorter division (i.e. WHOLE -> two HALFs, HALF -> two QUARTERs,
/// QUARTER -> two EIGHTHs, EIGHTH -> two SIXTEENTHs).
///
/// Source-of-truth enum (NoteValueType.cs:22-29): WHOLE=0, HALF=1, QUARTER=2,
/// EIGHTH=3, SIXTEENTH=4. The pre-Phase-35 04-VERIFICATION.md gap claimed the
/// switch used beat-fraction integers (1=&gt;2, 2=&gt;4, 4=&gt;8, 8=&gt;16) — that bug
/// was silently corrected at an earlier checkpoint (see 35-02-SUMMARY.md
/// §HK-03 for the audit), and these facts PIN the current correct shape so
/// it cannot regress.
/// </summary>
public class MutateRhythmEnumValuesTests
{
    /// <summary>
    /// Constructs a quarter-note (or whichever DurationValue is requested)
    /// MusicalNoteData fixture, invokes <see cref="VariationFunctions.MutateRhythm"/>,
    /// and returns the resulting split notes. Uses a fixed seed so the test
    /// is deterministic — MutateRhythm does not currently consume the Random
    /// (the split is unconditional), but pass one anyway for forward
    /// compatibility.
    /// </summary>
    private static List<MusicalNoteData> SplitOnce(int durationValue)
    {
        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: durationValue, isRest: false);
        var output = new List<MusicalNoteData>();
        VariationFunctions.MutateRhythm(note, new Random(0), output);
        return output;
    }

    [Fact]
    public void WholeMutatesToTwoHalves()
    {
        // case 0 (WHOLE) => 1 (HALF) — emit two HALF notes.
        var split = SplitOnce((int)NoteValueType.Value.WHOLE);
        Assert.Equal(2, split.Count);
        Assert.Equal((int)NoteValueType.Value.HALF, split[0].DurationValue);
        Assert.Equal((int)NoteValueType.Value.HALF, split[1].DurationValue);
    }

    [Fact]
    public void HalfMutatesToTwoQuarters()
    {
        // case 1 (HALF) => 2 (QUARTER) — emit two QUARTER notes.
        var split = SplitOnce((int)NoteValueType.Value.HALF);
        Assert.Equal(2, split.Count);
        Assert.Equal((int)NoteValueType.Value.QUARTER, split[0].DurationValue);
        Assert.Equal((int)NoteValueType.Value.QUARTER, split[1].DurationValue);
    }

    [Fact]
    public void QuarterMutatesToTwoEighths()
    {
        // case 2 (QUARTER) => 3 (EIGHTH) — this is the headline 04-VERIFICATION.md
        // assertion: a quarter note must NOT split into two sixteenths.
        var split = SplitOnce((int)NoteValueType.Value.QUARTER);
        Assert.Equal(2, split.Count);
        Assert.Equal((int)NoteValueType.Value.EIGHTH, split[0].DurationValue);
        Assert.Equal((int)NoteValueType.Value.EIGHTH, split[1].DurationValue);
        // Anti-regression: must NOT be SIXTEENTH (the pre-fix shape produced
        // SIXTEENTHs because the switch read 2 => 4 instead of 2 => 3).
        Assert.NotEqual((int)NoteValueType.Value.SIXTEENTH, split[0].DurationValue);
    }

    [Fact]
    public void EighthMutatesToTwoSixteenths()
    {
        // case 3 (EIGHTH) => 4 (SIXTEENTH) — terminal split.
        var split = SplitOnce((int)NoteValueType.Value.EIGHTH);
        Assert.Equal(2, split.Count);
        Assert.Equal((int)NoteValueType.Value.SIXTEENTH, split[0].DurationValue);
        Assert.Equal((int)NoteValueType.Value.SIXTEENTH, split[1].DurationValue);
    }

    [Fact]
    public void SixteenthCannotSplitFurther()
    {
        // case 4 (SIXTEENTH) => null fallthrough — return original note unchanged.
        var split = SplitOnce((int)NoteValueType.Value.SIXTEENTH);
        Assert.Single(split);
        Assert.Equal((int)NoteValueType.Value.SIXTEENTH, split[0].DurationValue);
    }
}
