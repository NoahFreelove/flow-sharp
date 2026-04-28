using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase18;

/// <summary>
/// FRAC-02 ctor wiring + GetBeats branch shape. Per D-USER-04, Phase 18 is the
/// inverse of every other phase: nothing existing should change. These Facts
/// pin BOTH the new field exists AND the existing path is unchanged when null.
/// </summary>
public class MusicalNoteDataTests
{
    [Fact]
    public void DurationFraction_DefaultsToNull()
    {
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false);
        Assert.Null(n.DurationFraction);
    }

    [Fact]
    public void DurationFraction_OptionalCtorParam_AcceptedAtEndOfSignature()
    {
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            durationFraction: new Fraction(1, 3));
        Assert.Equal(new Fraction(1, 3), n.DurationFraction);
    }

    [Fact]
    public void GetBeats_DurationFractionNull_UsesEnumPath()
    {
        // Pre-Phase-18 behavior: quarter note in 4/4 = 1 beat (0.25 × 4 = 1.0).
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false);
        Assert.Equal(1.0, n.GetBeats(4));
    }

    [Fact]
    public void GetBeats_DurationFractionNull_DottedQuarter_UsesEnumPath()
    {
        // Pre-Phase-18 behavior: dotted quarter in 4/4 = 1.5 beats (0.25 × 1.5 × 4).
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false, isDotted: true);
        Assert.Equal(1.5, n.GetBeats(4));
    }

    [Fact]
    public void GetBeats_DurationFractionSet_OverridesEnum()
    {
        // Fraction(1, 3) quarter-note units in 4/4 (denom=4):
        //   beats = (1 × 4) / (3 × 4) = 4/12 = 1/3
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            durationFraction: new Fraction(1, 3));
        Assert.Equal(1.0 / 3.0, n.GetBeats(4), 10); // 10 decimal places of precision
    }

    [Fact]
    public void ToString_UnchangedFromPreFraction()
    {
        // Pitfall 5 mitigation: DurationFraction MUST NOT surface in ToString in Phase 18.
        // 54 tests/test_*.flow scripts depend on this output remaining stable.
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            durationFraction: new Fraction(1, 3));
        Assert.Equal("quarter(C4)", n.ToString());
    }
}
