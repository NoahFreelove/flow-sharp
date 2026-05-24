using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-04 — UI-SPEC §"Glyph Composition Rules" lines 208-214:
///   - bar line `|` wins over sustain `#` at bar-boundary columns (line 214)
///   - onset glyph wins over sustain `#` at the start cell (line 213)
/// Both are guaranteed by the rendering logic: bar lines are stamped AFTER notes
/// in the bottom output pass, and onsets fill the same cell as sustain start.
/// </summary>
[Collection("FlowScripts")]
public class GlyphCollisionTests : IDisposable
{
    public GlyphCollisionTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
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
    /// A whole-note that spans the full bar MUST not erase the bar line that follows it.
    /// The bar-line `|` glyph wins over a sustain `#` at the bar-boundary column
    /// per UI-SPEC line 214.
    /// </summary>
    [Fact]
    public void BarLineWinsOverSustainHash()
    {
        var ts = new TimeSignatureData(4, 4);
        int whole = (int)NoteValueType.Value.WHOLE;
        // Bar 1: a single whole-note that fills the whole bar.
        var bar1 = new BarData(new[]
        {
            new MusicalNoteData('C', 4, 0, durationValue: whole, isRest: false, articulation: Articulation.Normal),
        }, ts);
        // Bar 2: another whole-note so there's a bar-boundary BETWEEN them.
        var bar2 = new BarData(new[]
        {
            new MusicalNoteData('C', 4, 0, durationValue: whole, isRest: false, articulation: Articulation.Normal),
        }, ts);
        var seq = new SequenceData();
        seq.AddBar(bar1);
        seq.AddBar(bar2);

        var rendered = Render(seq);

        // The grid rows MUST contain at least one '|' character that is NOT the outer
        // border (preserved bar-boundary marker between bar1 and bar2).
        var pitchRowLines = rendered.Split('\n')
            .Where(l => l.Contains('#'))
            .ToList();
        Assert.NotEmpty(pitchRowLines);

        var anyInteriorBarLine = pitchRowLines.Any(l =>
        {
            // Walk the interior of the row (skip the leading "label |" prefix and the
            // trailing "|"); look for a '|' that has neighbours on both sides inside the grid.
            int firstPipe = l.IndexOf('|');
            int lastPipe = l.LastIndexOf('|');
            if (firstPipe < 0 || lastPipe <= firstPipe + 1) return false;
            var interior = l.Substring(firstPipe + 1, lastPipe - firstPipe - 1);
            return interior.Contains('|');
        });
        Assert.True(anyInteriorBarLine,
            "Bar-line `|` must win over sustain `#` at the bar boundary between bar1 and bar2");
    }

    /// <summary>
    /// An accented note's onset cell MUST be the onset glyph `&gt;`, not the sustain `#`.
    /// Onset wins over sustain because they share the same cell (the onset IS the start
    /// of the sustain region) per UI-SPEC line 213.
    /// </summary>
    [Fact]
    public void OnsetGlyphWinsOverSustainHash()
    {
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(new[]
        {
            new MusicalNoteData('C', 4, 0,
                durationValue: (int)NoteValueType.Value.HALF, isRest: false,
                articulation: Articulation.Accent),
        }, ts);
        var seq = new SequenceData();
        seq.AddBar(bar);

        var rendered = Render(seq);

        Assert.Contains(">", rendered);
        // The onset glyph composes with sustain: `>` is followed by `#` cells in the grid.
        Assert.Contains(">#", rendered);
    }
}
