using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// D-09 spelling-aware ratio tables: under JI / Pythagorean, Eb and D# render at
/// different ratios. The chromatic ratio table keys on (Letter, Alteration), not
/// on semitone offset — RESEARCH §Pitfall 5 #3 + Pattern 2.
/// </summary>
public class SpellingAwareTuningFacts
{
    [Fact]
    public void JI_Eb_DistinctFrom_DSharp()
    {
        double eFlat = TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'E', -1);
        double dSharp = TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'D', +1);
        Assert.Equal(6.0/5.0, eFlat, precision: 10);
        Assert.Equal(75.0/64.0, dSharp, precision: 10);
        Assert.NotEqual(eFlat, dSharp);
    }

    [Fact]
    public void Pythagorean_Eb_DistinctFrom_DSharp()
    {
        double eFlat = TuningTables.LookupRatio(TuningSystem.Pythagorean, Mode.Major, 'E', -1);
        double dSharp = TuningTables.LookupRatio(TuningSystem.Pythagorean, Mode.Major, 'D', +1);
        Assert.Equal(32.0/27.0, eFlat, precision: 10);
        Assert.Equal(19683.0/16384.0, dSharp, precision: 10);
        Assert.NotEqual(eFlat, dSharp);
    }

    [Fact]
    public void JI_Tritone_AsymmetricPair()
    {
        // Wikipedia asymmetric 5-limit chromatic table: F# = 25/18 (planned per
        // RESEARCH; alternative 45/32 noted as Pitfall 2 variant), Gb = 64/45.
        double fSharp = TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'F', +1);
        double gFlat = TuningTables.LookupRatio(TuningSystem.JustIntonation, Mode.Major, 'G', -1);
        Assert.NotEqual(fSharp, gFlat);
    }

    [Fact]
    public void EqualTemperament_Bypasses_TableLookup()
    {
        // Sanity: no (EqualTemperament, *) entry exists. Callers MUST short-circuit
        // before LookupRatio per Pitfall 6.
        Assert.Throws<KeyNotFoundException>(() =>
            TuningTables.LookupRatio(TuningSystem.EqualTemperament, Mode.Major, 'E', 0));
    }
}
