using FlowLang.Core;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase25;

/// <summary>
/// Phase 25 Plan 25-01 (DEFER-06 precondition): MusicalNoteData.With(...)
/// helper extension with a `double? velocity = null` slot.
///
/// Anchors decisions:
///   D-17  Helper extracted/extended for testability (RESEARCH §Claude's Discretion #1)
///   D-18  Existing humanize is FROZEN — this extension lets humanizeGaussian (Plan 25-02)
///         AVOID repeating the bug at TransformFunctions.cs:896-898 where the 12-arg ctor
///         silently drops 5 Phase-18/22 fields (DurationFraction, OnsetOffset, DurationOverlap,
///         PortamentoMs, IsChordTone).
///
/// RESEARCH §Critical Pre-Existing Bug: humanizeGaussian MUST use note.With(velocity: x)
/// to preserve all 17 fields. These Facts pin the With() extension semantics so Plan 25-02
/// can rely on field-preservation as a contract.
/// </summary>
[Collection("FlowScripts")]
public class NoteTypeWithVelocityFacts
{
    private const double Tol = 1e-9;

    private static MusicalNoteData BuildRichNote()
    {
        // All 17 fields populated with non-default values to verify field preservation.
        return new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 1, durationValue: 4, isRest: false,
            centOffset: 25.0, isTied: true, velocity: 0.7,
            articulation: Articulation.Staccato, isDotted: true,
            sourceLocation: new SourceLocation(10, 5, "test.flow"), sourceLength: 12,
            durationFraction: new Fraction(3, 8),
            onsetOffset: 0.05,
            durationOverlap: 0.1,
            portamentoMs: 15.0,
            isChordTone: true);
    }

    [Fact]
    public void With_VelocityNull_PreservesOriginal()
    {
        var note = BuildRichNote();
        var copyDefaultArg = note.With();
        var copyExplicitNull = note.With(velocity: null);

        Assert.Equal(0.7, copyDefaultArg.Velocity, Tol);
        Assert.Equal(0.7, copyExplicitNull.Velocity, Tol);
    }

    [Fact]
    public void With_VelocitySet_ReturnsNewVelocity()
    {
        var note = BuildRichNote();
        var copy = note.With(velocity: 0.42);

        Assert.Equal(0.42, copy.Velocity, Tol);
        Assert.Equal(0.7, note.Velocity, Tol);  // original unchanged (immutability)
    }

    [Fact]
    public void With_VelocityAndOnsetOffset_BothApply()
    {
        var note = BuildRichNote();
        var copy = note.With(velocity: 0.42, onsetOffset: 0.2);

        Assert.Equal(0.42, copy.Velocity, Tol);
        Assert.Equal(0.2, copy.OnsetOffset, Tol);
        // Non-overridden fields preserved
        Assert.Equal('C', copy.NoteName);
        Assert.Equal(0.1, copy.DurationOverlap, Tol);
        Assert.Equal(15.0, copy.PortamentoMs, Tol);
    }

    [Fact]
    public void With_VelocitySet_PreservesAll16OtherFields()
    {
        var note = BuildRichNote();
        var copy = note.With(velocity: 0.5);

        // Pin every non-velocity field — the bug-prevention regression for the
        // pre-existing TransformFunctions.cs:896-898 12-arg ctor field-drop pattern.
        Assert.Equal(note.NoteName, copy.NoteName);
        Assert.Equal(note.Octave, copy.Octave);
        Assert.Equal(note.Alteration, copy.Alteration);
        Assert.Equal(note.DurationValue, copy.DurationValue);
        Assert.Equal(note.IsRest, copy.IsRest);
        Assert.Equal(note.CentOffset, copy.CentOffset);
        Assert.Equal(note.IsTied, copy.IsTied);
        Assert.Equal(note.Articulation, copy.Articulation);
        Assert.Equal(note.IsDotted, copy.IsDotted);
        Assert.Equal(note.SourceLocation, copy.SourceLocation);
        Assert.Equal(note.SourceLength, copy.SourceLength);
        Assert.Equal(note.DurationFraction, copy.DurationFraction);
        Assert.Equal(note.OnsetOffset, copy.OnsetOffset, Tol);
        Assert.Equal(note.DurationOverlap, copy.DurationOverlap, Tol);
        Assert.Equal(note.PortamentoMs, copy.PortamentoMs, Tol);
        Assert.Equal(note.IsChordTone, copy.IsChordTone);

        // Velocity changed
        Assert.Equal(0.5, copy.Velocity, Tol);
    }
}
