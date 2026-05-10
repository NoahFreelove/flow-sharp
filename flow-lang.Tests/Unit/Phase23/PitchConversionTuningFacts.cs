using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// MICR-01 end-to-end frequency-ratio Facts at the
/// <see cref="PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)"/> render
/// boundary. The leaf-level <c>TuningTables.LookupRatio</c> Facts (Wave 1) pin the
/// ratio dictionary; these Facts pin the FULL render-time pipeline that composes
/// ratio × tonic Hz × cent multiplier per CONTEXT D-10.
///
/// BLOCKER-2 acceptance: the canonical 5/4 JI major third + 3/2 Pythagorean perfect
/// fifth are pinned at the PitchConversion render boundary, not just at the leaf
/// table lookup.
/// </summary>
public class PitchConversionTuningFacts
{
    private static MusicalNoteData MakeNote(char letter, int octave, int alteration) =>
        new MusicalNoteData(letter, octave, alteration, durationValue: null, isRest: false);

    [Fact]
    public void PitchConversionEndToEnd_JI_CtoE_FrequencyRatio_Is5to4()
    {
        // Canonical 5-limit JI major third: E above C tonic = 5/4.
        var jiTuning = new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0);
        double cFreq = PitchConversion.NoteToFrequency(MakeNote('C', 4, 0), jiTuning);
        double eFreq = PitchConversion.NoteToFrequency(MakeNote('E', 4, 0), jiTuning);
        Assert.Equal(5.0 / 4.0, eFreq / cFreq, precision: 10);
    }

    [Fact]
    public void PitchConversionEndToEnd_Pythagorean_CtoG_FrequencyRatio_Is3to2()
    {
        // Canonical 3-limit Pythagorean perfect fifth: G above C tonic = 3/2.
        var pythTuning = new RenderTuning(TuningSystem.Pythagorean, Mode.Major, 'C', 0);
        double cFreq = PitchConversion.NoteToFrequency(MakeNote('C', 4, 0), pythTuning);
        double gFreq = PitchConversion.NoteToFrequency(MakeNote('G', 4, 0), pythTuning);
        Assert.Equal(3.0 / 2.0, gFreq / cFreq, precision: 10);
    }

    [Fact]
    public void JI_FrequenciesDiffer_FromEqualTemperament()
    {
        // Sanity-check the wedge actually fires: JI E4 must differ measurably from 12-TET E4.
        // 5/4 = 1.25 vs 2^(4/12) ≈ 1.2599. ~14 cent gap on E4 = ~2.7 Hz at A4=440 reference.
        var jiTuning = new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0);
        var eqTuning = RenderTuning.Default;
        double jiE  = PitchConversion.NoteToFrequency(MakeNote('E', 4, 0), jiTuning);
        double eqE  = PitchConversion.NoteToFrequency(MakeNote('E', 4, 0), eqTuning);
        Assert.NotEqual(jiE, eqE);
        Assert.True(System.Math.Abs(jiE - eqE) > 0.5,
            $"expected JI E4 != 12-TET E4 by >0.5 Hz; got {jiE} vs {eqE}");
    }

    [Fact]
    public void Pythagorean_FrequenciesDiffer_FromEqualTemperament()
    {
        // Pythagorean E4 = 81/64 = 1.265625 vs 12-TET E4 ≈ 1.2599 — the Pythagorean third
        // is sharper than 12-TET by a syntonic-comma-shifted amount.
        var pythTuning = new RenderTuning(TuningSystem.Pythagorean, Mode.Major, 'C', 0);
        var eqTuning   = RenderTuning.Default;
        double pythE = PitchConversion.NoteToFrequency(MakeNote('E', 4, 0), pythTuning);
        double eqE   = PitchConversion.NoteToFrequency(MakeNote('E', 4, 0), eqTuning);
        Assert.NotEqual(pythE, eqE);
    }

    [Fact]
    public void EqualTemperamentShortCircuit_BitIdentical_To1ArgOverload()
    {
        // Pitfall 6 contract pin: when tuning.System == EqualTemperament AND no centOffset,
        // the new overload must produce a frequency BIT-IDENTICAL to the existing 1-arg
        // overload. This is the load-bearing mechanism that keeps tutorial.flow / showcase
        // .flow byte-identical.
        var note = MakeNote('A', 4, 0);  // 440 Hz canonical
        double via1Arg = PitchConversion.NoteToFrequency(note);
        double viaTuning = PitchConversion.NoteToFrequency(note, RenderTuning.Default);
        // Use Equal (no precision tolerance) — bit-identical contract.
        Assert.Equal(via1Arg, viaTuning);
    }
}
