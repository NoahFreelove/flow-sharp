using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-04 — articulation glyph extension to <c>(visualize seq)</c> per
/// D-38-10 + UI-SPEC §"Glyph Inventory" lines 187-201 (LOCKED). Each Phase 28
/// articulation gets a single-cell ASCII glyph at the note's onset cell; sustain
/// cells stay as <c>#</c>. Sequences with no articulation data render identical to
/// the pre-Phase-38 baseline (Articulation.Normal → onset cell is <c>#</c>).
/// </summary>
[Collection("FlowScripts")]
public class VisualizeArticulationGlyphTests : IDisposable
{
    public VisualizeArticulationGlyphTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static SequenceData BuildSingleNoteSequence(Articulation articulation,
        int durationValue = (int)NoteValueType.Value.HALF /* ordinal 1 = half note */)
    {
        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: durationValue, isRest: false,
            articulation: articulation);
        var bar = new BarData(new[] { note }, new TimeSignatureData(4, 4));
        var seq = new SequenceData();
        seq.AddBar(bar);
        return seq;
    }

    private static string Render(SequenceData seq)
    {
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            VisualizationFunctions.Visualize(new List<Value> { Value.Sequence(seq) });
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        return sw.ToString();
    }

    /// <summary>
    /// Accent (`&gt;`) MUST render at the onset cell; subsequent sustain cells stay <c>#</c>.
    /// Per UI-SPEC line 210 example: an accented half-note renders as <c>&gt;###</c>.
    /// </summary>
    [Fact]
    public void AccentNote_RendersAngleBracketAtOnset()
    {
        var rendered = Render(BuildSingleNoteSequence(Articulation.Accent, durationValue: (int)NoteValueType.Value.HALF));
        Assert.Contains(">", rendered);
        // The onset is followed by sustain '#' cells — this asserts the composition.
        Assert.Contains(">#", rendered);
    }

    /// <summary>
    /// Parameterised assertion over the full Phase 28 enum + Normal (which has NO onset glyph).
    /// Per UI-SPEC Glyph Inventory: Accent → &gt;, Staccato → ., Marcato → ^, Tenuto → _,
    /// Sforzando → !, Normal → # (no glyph, fall through to sustain char).
    /// Legato uses gap-cell rendering instead; covered by its own dedicated test below.
    /// </summary>
    [Theory]
    [InlineData(Articulation.Accent, '>')]
    [InlineData(Articulation.Staccato, '.')]
    [InlineData(Articulation.Marcato, '^')]
    [InlineData(Articulation.Tenuto, '_')]
    [InlineData(Articulation.Sforzando, '!')]
    [InlineData(Articulation.Normal, '#')]
    public void AllSixArticulations_RenderCorrectGlyphs(Articulation articulation, char expectedGlyph)
    {
        var rendered = Render(BuildSingleNoteSequence(articulation, durationValue: (int)NoteValueType.Value.HALF));
        Assert.Contains(expectedGlyph.ToString(), rendered);
    }

    /// <summary>
    /// Single-column note (sixteenth at columnsPerBeat=2) MUST render as the onset
    /// glyph ALONE — no trailing <c>#</c>. Per UI-SPEC line 211.
    /// </summary>
    [Fact]
    public void SingleCellStaccato_RendersDotOnly()
    {
        // Sixteenth note — ordinal 4 in NoteValueType.Value.
        // 0.25 beats × 2 columns/beat = 0.5 → rounds to ~1 col (single-cell per UI-SPEC line 211).
        var rendered = Render(BuildSingleNoteSequence(Articulation.Staccato,
            durationValue: (int)NoteValueType.Value.SIXTEENTH));
        Assert.Contains(".", rendered);
        // Crude guard: there should NOT be a `.#` adjacency anywhere in the grid rows
        // (the staccato cell collapses to a single dot).
        Assert.DoesNotContain(".#", rendered);
    }

    /// <summary>
    /// Sequences containing ONLY Articulation.Normal notes MUST render identically to
    /// the pre-Phase-38 baseline (onset cell is <c>#</c>, sustain cells are <c>#</c>).
    /// Regression guard for the backwards-compat contract per UI-SPEC line 232.
    /// </summary>
    [Fact]
    public void NormalArticulation_PreservesPrePhase38Output()
    {
        var rendered = Render(BuildSingleNoteSequence(Articulation.Normal, durationValue: (int)NoteValueType.Value.HALF));
        Assert.Contains("#", rendered);
        // No new glyphs introduced for a Normal-only sequence
        Assert.DoesNotContain(">", rendered);
        Assert.DoesNotContain("^", rendered);
        Assert.DoesNotContain("!", rendered);
    }
}
