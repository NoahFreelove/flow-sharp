using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// MICR-01 acceptance: canonical ratio Facts for the 14 mode tables shipped in
/// plan 23-01. RED-then-GREEN per Phase 18-22 precedent — these Facts pin Wikipedia
/// + Mudcat reference ratios BEFORE Wave 2 wires them through PitchConversion.
/// </summary>
public class TuningRatioFacts
{
    [Fact]
    public void JustMajor_CtoE_Is5to4()
        => Assert.Equal(5.0/4.0,
            TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'E', 0),
            precision: 10);

    [Fact]
    public void JustMajor_CtoG_Is3to2()
        => Assert.Equal(3.0/2.0,
            TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'G', 0),
            precision: 10);

    [Fact]
    public void JustMajor_CtoF_Is4to3()
        => Assert.Equal(4.0/3.0,
            TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'F', 0),
            precision: 10);

    [Fact]
    public void JustMajor_CtoD_Is9to8()
        => Assert.Equal(9.0/8.0,
            TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'D', 0),
            precision: 10);

    [Fact]
    public void JustMajor_CtoA_Is5to3()
        => Assert.Equal(5.0/3.0,
            TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'A', 0),
            precision: 10);

    [Fact]
    public void JustMajor_CtoB_Is15to8()
        => Assert.Equal(15.0/8.0,
            TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'B', 0),
            precision: 10);

    [Fact]
    public void PythagoreanMajor_CtoE_Is81to64()
        => Assert.Equal(81.0/64.0,
            TuningTables.LookupRatio(TuningSystem.Pythagorean, Mode.Major, 'E', 0),
            precision: 10);

    [Fact]
    public void PythagoreanMajor_CtoG_Is3to2()
        => Assert.Equal(3.0/2.0,
            TuningTables.LookupRatio(TuningSystem.Pythagorean, Mode.Major, 'G', 0),
            precision: 10);

    [Fact]
    public void PythagoreanMajor_CtoB_Is243to128()
        => Assert.Equal(243.0/128.0,
            TuningTables.LookupRatio(TuningSystem.Pythagorean, Mode.Major, 'B', 0),
            precision: 10);

    [Fact]
    public void PythagoreanMajor_CSharp_Is2187to2048()
        => Assert.Equal(2187.0/2048.0,
            TuningTables.LookupRatio(TuningSystem.Pythagorean, Mode.Major, 'C', +1),
            precision: 10);

    [Fact]
    public void Tables_HasExactly14Entries()
        => Assert.Equal(14, TuningTables.Tables.Count);

    [Fact]
    public void Tables_NoEqualTemperamentEntries()
    {
        // EqualTemperament short-circuits before LookupRatio per Pitfall 6.
        foreach (var (sys, _) in TuningTables.Tables.Keys)
            Assert.NotEqual(TuningSystem.EqualTemperament, sys);
    }

    [Fact]
    public void RatioMath_CentOffsetMultiplier_Zero_ReturnsOne()
        => Assert.Equal(1.0, RatioMath.CentOffsetMultiplier(0.0), precision: 10);

    [Fact]
    public void RatioMath_CentOffsetMultiplier_1200Cents_ReturnsTwo()
        => Assert.Equal(2.0, RatioMath.CentOffsetMultiplier(1200.0), precision: 10);
}
