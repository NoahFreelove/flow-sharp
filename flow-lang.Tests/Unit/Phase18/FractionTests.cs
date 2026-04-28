using System;
using FlowLang.TypeSystem;
using Xunit;

namespace FlowLang.Tests.Unit.Phase18;

/// <summary>
/// FRAC-01 acceptance: Fraction rational-arithmetic primitive.
/// Pins canonical examples from REQUIREMENTS.md FRAC-01 + edge cases from
/// 18-RESEARCH.md §6 Pitfall 3 (zero denom) + Pattern 3 (sign normalization).
/// Per D-USER-03 ToString always emits "Num/Denom" (no special-casing 1/1).
/// </summary>
public class FractionTests
{
    [Fact]
    public void TripletThirds_SumToOne()
    {
        var third = new Fraction(1, 3);
        Assert.Equal(new Fraction(1, 1), third + third + third);
    }

    [Fact]
    public void TwoFourths_NormalizeToOneHalf()
    {
        Assert.Equal(new Fraction(1, 2), new Fraction(2, 4));
    }

    [Fact]
    public void ThreeTwelfths_NormalizeToOneFourth()
    {
        Assert.Equal(new Fraction(1, 4), new Fraction(3, 12));
    }

    [Fact]
    public void MultiplicationProducesProduct()
    {
        Assert.Equal(new Fraction(1, 12), new Fraction(1, 3) * new Fraction(1, 4));
    }

    [Fact]
    public void LessThanIsRational()
    {
        Assert.True(new Fraction(1, 3) < new Fraction(1, 2));
        Assert.False(new Fraction(1, 2) < new Fraction(1, 3));
    }

    [Fact]
    public void ZeroDenominator_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => new Fraction(1, 0));
    }

    [Fact]
    public void NegativeDenom_SignOnNumerator()
    {
        var f = new Fraction(1, -2);
        Assert.Equal(-1, f.Num);
        Assert.Equal(2, f.Denom);
    }

    [Fact]
    public void ToString_FormatNumSlashDenom()
    {
        Assert.Equal("3/4", new Fraction(3, 4).ToString());
        // D-USER-03: always emit Num/Denom — no special-casing 1/1.
        Assert.Equal("1/1", new Fraction(1, 1).ToString());
    }

    [Fact]
    public void GetHashCode_EqualFractionsHashEqual()
    {
        // Constructor normalizes; record struct generates value-equal hash on normalized fields.
        Assert.Equal(new Fraction(1, 2).GetHashCode(), new Fraction(2, 4).GetHashCode());
    }
}
