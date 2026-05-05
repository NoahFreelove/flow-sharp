using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// D-10 cent offsets compose additively in cent-space:
///   freq = tonic_hz * ratio * 2^(cents/1200).
/// Per the charitable-interpretation memory, cents never silently disappear.
/// </summary>
public class CentOffsetAdditivityFacts
{
    [Fact]
    public void ZeroCents_ReturnsOne()
        => Assert.Equal(1.0, RatioMath.CentOffsetMultiplier(0.0), precision: 10);

    [Fact]
    public void OneOctave_ReturnsTwo()
        => Assert.Equal(2.0, RatioMath.CentOffsetMultiplier(1200.0), precision: 10);

    [Fact]
    public void NegativeOctave_ReturnsHalf()
        => Assert.Equal(0.5, RatioMath.CentOffsetMultiplier(-1200.0), precision: 10);

    [Fact]
    public void JI_FifthPlus5Cents_AppliesToRatio()
    {
        // E4+5c under JI Major: ratio 5/4 multiplied by 2^(5/1200).
        double ratio = TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'E', 0);
        double withCents = ratio * RatioMath.CentOffsetMultiplier(5.0);
        double expected = (5.0/4.0) * Math.Pow(2.0, 5.0/1200.0);
        Assert.Equal(expected, withCents, precision: 10);
    }
}
