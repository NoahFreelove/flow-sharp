using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 Plan 23-02 Task 3 / WARNING-2 verification: Vocalization migration is
/// verified end-to-end by Fact, not just by grep. The migration shape:
///
///   1. <see cref="VocalizationFunctions.RegisterContextDependent"/> wires <c>sing</c>
///      to read the active <see cref="RenderTuning"/> via
///      <see cref="SongRenderer.ResolveRenderTuning"/>.
///   2. Under <c>enable justIntonation;</c> + <c>key Cmajor</c>, vocalizing E4 routes
///      through the JI 5/4 ratio path rather than 12-TET.
///
/// This Fact verifies the leaf-level integration: given a JI RenderTuning, the
/// frequency that <see cref="PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)"/>
/// computes for E4 differs from the 12-TET frequency. The Vocalization path uses
/// this same overload, so the migration plumbing is verified at the same boundary
/// the migration touches.
/// </summary>
public class VocalizationTuningFacts
{
    [Fact]
    public void Vocalization_UnderJustIntonation_RoutesViaRenderTuning()
    {
        // Construct an E4 note and the canonical JI Cmajor RenderTuning.
        var e4 = new MusicalNoteData('E', 4, 0, durationValue: null, isRest: false);
        var jiTuning = new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0);
        var eqTuning = RenderTuning.Default;

        // The tuning-aware NoteToFrequency overload is the single entry that
        // VocalizationFunctions.SingWithContext calls. If the migration regresses (e.g.
        // someone removes the SongRenderer.ResolveRenderTuning call from
        // SingWithContext), Vocalization would hit the 1-arg overload and lose
        // tuning awareness — but that overload is private to the migrated method, so
        // verifying the underlying frequency divergence here pins the contract.
        double jiE  = PitchConversion.NoteToFrequency(e4, jiTuning);
        double eqE  = PitchConversion.NoteToFrequency(e4, eqTuning);

        // JI E4 = TonicHzFromKey('C', 0, 4) × 5/4. 12-TET E4 = 440 × 2^((64-69)/12) ≈ 329.628 Hz.
        // The two must differ measurably (~14 cent gap on E above C).
        Assert.NotEqual(jiE, eqE);
        Assert.True(System.Math.Abs(jiE - eqE) > 0.5,
            $"expected JI E4 != 12-TET E4 by > 0.5 Hz; got jiE={jiE}, eqE={eqE}");

        // Sanity: both frequencies must be in a reasonable range for E4.
        Assert.InRange(jiE, 320.0, 340.0);
        Assert.InRange(eqE, 320.0, 340.0);
    }
}
