using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// D-03 mode-table-shift verification: each (TuningSystem, Mode) pair pins its
/// canonical 7-degree diatonic scale per Mudcat (JI) / Wikipedia (Pythagorean).
/// </summary>
public class TuningModeShiftFacts
{
    // (system, mode, letter, alteration, expectedRatio)
    [Theory]
    // JI Major (Ionian)
    [InlineData(TuningSystem.JustIntonation, Mode.Major, 'E', 0, 5.0/4.0)]
    // JI Aeolian (natural minor) — flat 3rd
    [InlineData(TuningSystem.JustIntonation, Mode.Minor, 'E', 0, 6.0/5.0)]
    // JI Dorian — flat 3rd, natural 6th, flat 7th
    [InlineData(TuningSystem.JustIntonation, Mode.Dorian, 'E', 0, 6.0/5.0)]
    [InlineData(TuningSystem.JustIntonation, Mode.Dorian, 'A', 0, 5.0/3.0)]
    // JI Phrygian — flat 2, flat 3, flat 6, flat 7
    [InlineData(TuningSystem.JustIntonation, Mode.Phrygian, 'D', 0, 27.0/25.0)]
    // JI Lydian — raised 4
    [InlineData(TuningSystem.JustIntonation, Mode.Lydian, 'F', 0, 25.0/18.0)]
    // JI Mixolydian — flat 7
    [InlineData(TuningSystem.JustIntonation, Mode.Mixolydian, 'B', 0, 9.0/5.0)]
    // JI Locrian — diminished 5
    [InlineData(TuningSystem.JustIntonation, Mode.Locrian, 'G', 0, 36.0/25.0)]
    // Pythagorean Major
    [InlineData(TuningSystem.Pythagorean, Mode.Major, 'E', 0, 81.0/64.0)]
    // Pythagorean Aeolian — flat 3rd
    [InlineData(TuningSystem.Pythagorean, Mode.Minor, 'E', 0, 32.0/27.0)]
    // Pythagorean Dorian
    [InlineData(TuningSystem.Pythagorean, Mode.Dorian, 'E', 0, 32.0/27.0)]
    [InlineData(TuningSystem.Pythagorean, Mode.Dorian, 'A', 0, 27.0/16.0)]
    // Pythagorean Lydian — raised 4
    [InlineData(TuningSystem.Pythagorean, Mode.Lydian, 'F', 0, 729.0/512.0)]
    // Pythagorean Mixolydian — flat 7
    [InlineData(TuningSystem.Pythagorean, Mode.Mixolydian, 'B', 0, 16.0/9.0)]
    public void ModeTable_CanonicalScaleDegree(
        TuningSystem system, Mode mode, char letter, int alteration, double expected)
        => Assert.Equal(expected,
            TuningTables.LookupRatio(system, mode, letter, alteration),
            precision: 10);
}
