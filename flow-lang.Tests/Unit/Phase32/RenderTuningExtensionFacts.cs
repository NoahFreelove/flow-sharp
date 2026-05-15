using System;
using System.Collections.Generic;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 03 Task 2 — Facts pinning the render-time entry point of the
/// custom-tuning path. The new <see cref="PitchConversion.NoteToFrequency"/>
/// branch fires when <c>tuning.Custom != null</c>; the Phase 23 12-TET short-circuit
/// fires when <c>tuning.Custom is null</c> AND <c>tuning.System == EqualTemperament</c>
/// (Pitfall 3 mutual-exclusion guard).
///
/// Pitfall 3 — the test `PitchConversion_CustomOverridesSystem_PitfallGuard`
/// constructs <c>new RenderTuning(EqualTemperament, Major, 'C', 0, customNonNull)</c>
/// and asserts the function reads <c>custom.MidiToHz</c>, NOT the EQ short-circuit
/// formula. This catches the bug pre-execute.
/// </summary>
public class RenderTuningExtensionFacts
{
    private static MusicalNoteData MakeNote(char letter, int octave, int alteration, double? centOffset = null) =>
        new MusicalNoteData(letter, octave, alteration, durationValue: null, isRest: false, centOffset: centOffset);

    /// <summary>
    /// Synthetic ParsedScala for partch_43 (43 ratio steps; final = 2/1 = 1200¢).
    /// Same constants as TuningTypeFacts.MakePartch43; duplicated locally because
    /// the helper there is private. Keeping facts files self-contained avoids a
    /// cross-file test-helper dependency.
    /// </summary>
    private static ParsedScala MakePartch43()
    {
        var ratioList = new (int Num, int Den)[]
        {
            (81, 80), (33, 32), (21, 20), (16, 15), (12, 11), (11, 10), (10, 9),
            (9, 8), (8, 7), (7, 6), (32, 27), (6, 5), (11, 9), (5, 4), (14, 11),
            (9, 7), (21, 16), (4, 3), (27, 20), (11, 8), (7, 5), (10, 7), (16, 11),
            (40, 27), (3, 2), (32, 21), (14, 9), (11, 7), (8, 5), (18, 11), (5, 3),
            (27, 16), (12, 7), (7, 4), (16, 9), (9, 5), (20, 11), (11, 6), (15, 8),
            (40, 21), (64, 33), (160, 81), (2, 1)
        };
        int n = ratioList.Length;
        var cents = new double[n];
        var ratioDict = new Dictionary<int, (int Num, int Den)>();
        for (int i = 0; i < n; i++)
        {
            cents[i] = 1200.0 * Math.Log2((double)ratioList[i].Num / ratioList[i].Den);
            ratioDict[i] = ratioList[i];
        }
        return new ParsedScala(
            Description: "Harry Partch's 43-tone pure scale",
            StepCents: cents[..^1],
            PeriodCents: cents[^1],
            Ratios: ratioDict,
            FilePath: "synthetic:partch_43.scl");
    }

    private static ScalaKbm DefaultKbm(ParsedScala scl) =>
        new ScalaKbm(
            size: 0, firstMidi: 0, lastMidi: 127, middleNote: 60, referenceNote: 69,
            referenceHz: 440.0, formalOctave: 0, mapping: Array.Empty<int?>(),
            period: scl.PeriodCents);

    // ---- RenderTuning default-Custom contract ----

    [Fact]
    public void RenderTuning_Default_HasNullCustom()
    {
        Assert.Null(RenderTuning.Default.Custom);
    }

    [Fact]
    public void RenderTuning_Default_Equals_RenderTuning4Arg()
    {
        // Existing 4-arg call sites (SongRenderer, Phase 23 tests, RenderTuning.Default
        // factory) must compile unchanged with the new 5th positional parameter at the
        // end of the parameter list. The default value `Custom = null` makes the
        // 4-arg form equivalent to the 5-arg form with Custom omitted.
        var four = new RenderTuning(TuningSystem.EqualTemperament, Mode.Major, 'C', 0);
        var five = new RenderTuning(TuningSystem.EqualTemperament, Mode.Major, 'C', 0, null);
        Assert.Equal(four, five);
    }

    // ---- 12-TET short-circuit preserved when Custom == null ----

    [Fact]
    public void PitchConversion_NullCustom_Matches12TetBaseline()
    {
        // Default RenderTuning has Custom=null AND System=EqualTemperament, so the
        // Phase 23 byte-identical 12-TET fast path fires. MIDI 60 ≈ 261.6256 Hz
        // (= 440 / 2^(9/12)) — this IS the 12-TET answer and is correct here.
        var note60 = MakeNote('C', 4, 0);
        double hz = PitchConversion.NoteToFrequency(note60, RenderTuning.Default);
        Assert.Equal(261.6255653005986, hz, precision: 6);
    }

    // ---- NonNull Custom routes through MidiToHz ----

    [Fact]
    public void PitchConversion_NonNullCustom_ReadsMidiToHz()
    {
        // The function must read exactly resolved.MidiToHz[60] — NOT the 12-TET
        // 261.6256 number — when Custom is non-null. Assert equality against the
        // array entry directly, NOT against a hardcoded number, because the
        // scale-step-walked Partch value is whatever the algorithm produces.
        var scl = MakePartch43();
        var resolved = new ResolvedTuning(scl, DefaultKbm(scl));
        var rt = new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0, resolved);
        var note60 = MakeNote('C', 4, 0);
        double hz = PitchConversion.NoteToFrequency(note60, rt);
        Assert.Equal(resolved.MidiToHz[60], hz, precision: 9);
    }

    [Fact]
    public void PitchConversion_NonNullCustom_AppliesCentOffset()
    {
        // Cent offset preserved through the new branch: hz = MidiToHz[midi] ×
        // CentOffsetMultiplier(cents). 5¢ on top of the Partch MIDI-60 entry.
        var scl = MakePartch43();
        var resolved = new ResolvedTuning(scl, DefaultKbm(scl));
        var rt = new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0, resolved);
        var note60 = MakeNote('C', 4, 0, centOffset: 5.0);
        double hz = PitchConversion.NoteToFrequency(note60, rt);
        double expected = resolved.MidiToHz[60] * RatioMath.CentOffsetMultiplier(5.0);
        Assert.Equal(expected, hz, precision: 9);
    }

    [Fact]
    public void PitchConversion_NonNullCustom_OutOfRangeMidi_ReturnsZero()
    {
        // MIDI 200 is out of 0..127 range. Bounds-clamp returns 0.0 inside the
        // Custom branch (mirrors D-08 unmapped-key handling for the MIDI-out-of-range
        // edge case the bounds check guards against).
        var scl = MakePartch43();
        var resolved = new ResolvedTuning(scl, DefaultKbm(scl));
        var rt = new RenderTuning(TuningSystem.JustIntonation, Mode.Major, 'C', 0, resolved);
        // GetMidiNote('C', 15, 0) == (15+1)*12 + 0 + 0 == 192 — still in 0..127? No, 192 > 127.
        // (Phase23 tests don't exercise octaves > 8 so this synthetic out-of-range note
        // is a defensive guard.)
        var note = MakeNote('C', 15, 0);
        double hz = PitchConversion.NoteToFrequency(note, rt);
        Assert.Equal(0.0, hz);
    }

    // ---- Pitfall 3 mutual-exclusion guard ----

    [Fact]
    public void PitchConversion_CustomOverridesSystem_PitfallGuard()
    {
        // A hand-constructed RenderTuning with System=EqualTemperament AND
        // Custom != null MUST take the Custom branch (NOT the 12-TET short-circuit).
        // This is the Pitfall 3 mutual-exclusion guard the plan explicitly calls out:
        // without the early `Custom is not null` return at the top of NoteToFrequency
        // PLUS the `Custom is null` requirement on the 12-TET short-circuit, the EQ
        // path would silently swallow the Custom override.
        var scl = MakePartch43();
        var resolved = new ResolvedTuning(scl, DefaultKbm(scl));
        var rt = new RenderTuning(TuningSystem.EqualTemperament, Mode.Major, 'C', 0, resolved);
        var note60 = MakeNote('C', 4, 0);
        double hz = PitchConversion.NoteToFrequency(note60, rt);
        // The Custom branch produces resolved.MidiToHz[60] which for Partch is ~395 Hz,
        // NOT the 12-TET 261.6256 Hz.
        Assert.Equal(resolved.MidiToHz[60], hz, precision: 9);
        Assert.NotEqual(261.6256, hz, precision: 3);
    }
}
