using System.Collections.Generic;
using System.Linq;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase19;

/// <summary>
/// TUP-07 — AUDIT-VERIFIED C5 re-validated against tuplet sequences.
///
/// Per CONTEXT D-15 two-pass strict authorship: this file is Pass 1 — drafted
/// from REQUIREMENTS.md TUP-07 wording + 19-SPEC.md acceptance criteria alone.
/// SPEC's "[1/12, 1/12, 1/12] whole" translates to Fraction(1, 3) quarter per
/// Phase 18 18-02 SUMMARY's quarter-unit pin (DurationFraction stores quarter-units).
///
///   augment(tupletSeq) doubles → input [1/3 q × 3] → output [2/3 q × 3] (= [1/6 whole])
///   diminish(tupletSeq) halves → input [1/3 q × 3] → output [1/6 q × 3] (= [1/24 whole])
///
/// Existing power-of-2 enum path stays unchanged when DurationFraction is null —
/// Phase 11/12 C5 audit remains valid for non-tuplet sequences (Tests 3 + 4).
/// </summary>
public class TupletAugmentDiminishTests
{
    /// <summary>
    /// Build a single-bar musical sequence containing the given notes.
    /// </summary>
    private static SequenceData BuildSequence(IEnumerable<MusicalNoteData> notes)
    {
        var seq = new SequenceData();
        var bar = new BarData(notes, new TimeSignatureData(4, 4));
        seq.AddBar(bar);
        return seq;
    }

    private static MusicalNoteData TupletNote(char name, int octave, Fraction df)
    {
        return new MusicalNoteData(
            name, octave, alteration: 0,
            durationValue: (int)NoteValueType.Value.QUARTER, // best-effort enum mirror; rational override applies
            isRest: false,
            durationFraction: df);
    }

    [Fact]
    public void Augment_RationalDouble()
    {
        // SPEC TUP-07: augment([1/12 whole, ×3]) → [1/6 whole, ×3]
        // Quarter-units (Phase 18 pin): augment([1/3 q, ×3]) → [2/3 q, ×3]
        var input = BuildSequence(new[]
        {
            TupletNote('C', 4, new Fraction(1, 3)),
            TupletNote('D', 4, new Fraction(1, 3)),
            TupletNote('E', 4, new Fraction(1, 3)),
        });

        var result = FlowLang.StandardLibrary.Transforms.TransformFunctions.AugmentForTesting(input);
        var notes = result.Bars[0].MusicalNotes;
        Assert.Equal(3, notes.Count);
        foreach (var n in notes)
        {
            Assert.NotNull(n.DurationFraction);
            Assert.Equal(new Fraction(2, 3), n.DurationFraction);
        }
    }

    [Fact]
    public void Diminish_RationalHalve()
    {
        // SPEC TUP-07: diminish([1/12 whole, ×3]) → [1/24 whole, ×3]
        // Quarter-units (Phase 18 pin): diminish([1/3 q, ×3]) → [1/6 q, ×3]
        var input = BuildSequence(new[]
        {
            TupletNote('C', 4, new Fraction(1, 3)),
            TupletNote('D', 4, new Fraction(1, 3)),
            TupletNote('E', 4, new Fraction(1, 3)),
        });

        var result = FlowLang.StandardLibrary.Transforms.TransformFunctions.DiminishForTesting(input);
        var notes = result.Bars[0].MusicalNotes;
        Assert.Equal(3, notes.Count);
        foreach (var n in notes)
        {
            Assert.NotNull(n.DurationFraction);
            Assert.Equal(new Fraction(1, 6), n.DurationFraction);
        }
    }

    [Fact]
    public void Augment_NonTupletPath_StaysOnEnumPath()
    {
        // Phase 18 byte-identical contract preserved: when DurationFraction is null,
        // augment runs the existing enum path — QUARTER (2) → HALF (1), DurationFraction stays null.
        var note = new MusicalNoteData('C', 4, 0,
            durationValue: (int)NoteValueType.Value.QUARTER, isRest: false);
        var input = BuildSequence(new[] { note });

        var result = FlowLang.StandardLibrary.Transforms.TransformFunctions.AugmentForTesting(input);
        var resultNote = result.Bars[0].MusicalNotes.Single();
        Assert.Equal((int)NoteValueType.Value.HALF, resultNote.DurationValue);
        Assert.Null(resultNote.DurationFraction);
    }

    [Fact]
    public void Diminish_NonTupletPath_StaysOnEnumPath()
    {
        // Phase 18 byte-identical contract preserved: QUARTER (2) → EIGHTH (3), DurationFraction stays null.
        var note = new MusicalNoteData('C', 4, 0,
            durationValue: (int)NoteValueType.Value.QUARTER, isRest: false);
        var input = BuildSequence(new[] { note });

        var result = FlowLang.StandardLibrary.Transforms.TransformFunctions.DiminishForTesting(input);
        var resultNote = result.Bars[0].MusicalNotes.Single();
        Assert.Equal((int)NoteValueType.Value.EIGHTH, resultNote.DurationValue);
        Assert.Null(resultNote.DurationFraction);
    }

    [Fact]
    public void AuditVerifiedComment_Phase19TUP07_PresentAtBothSites()
    {
        // SPEC TUP-07 acceptance: AUDIT-VERIFIED comment at TransformFunctions.cs:239 + 261
        // includes "re-validated against tuplet sequences (Phase 19 TUP-07)".
        // Grep regression: must find ≥ 2 markers (one each at Augment + Diminish).
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(testsRoot, ".."));
        string sourcePath = System.IO.Path.Combine(
            repoRoot, "flow-lang", "StandardLibrary", "Transforms", "TransformFunctions.cs");
        string content = System.IO.File.ReadAllText(sourcePath);
        int matchCount = System.Text.RegularExpressions.Regex.Matches(
            content, "Phase 19 TUP-07").Count;
        Assert.True(matchCount >= 2,
            $"Expected ≥ 2 'Phase 19 TUP-07' AUDIT-VERIFIED markers " +
            $"(one each at Augment + Diminish); found {matchCount}");
    }
}
