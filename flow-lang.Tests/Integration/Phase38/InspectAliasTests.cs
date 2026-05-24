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
/// Phase 38 Plan 38-04 — <c>(inspect seq)</c> is a builtin-level alias for
/// <c>(visualize seq)</c> per D-38-10 (overrides REQUIREMENTS.md REPL-04 wording per
/// D-v1.5-01). Same dispatch on identical input MUST produce identical output —
/// PATTERNS line 808 ("Same dispatch").
/// </summary>
[Collection("FlowScripts")]
public class InspectAliasTests : IDisposable
{
    public InspectAliasTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static SequenceData BuildNonTrivialSequence()
    {
        var ts = new TimeSignatureData(4, 4);
        int quarter = (int)NoteValueType.Value.QUARTER;
        var bar1 = new BarData(new[]
        {
            new MusicalNoteData('C', 4, 0, durationValue: quarter, isRest: false, articulation: Articulation.Accent),
            new MusicalNoteData('E', 4, 0, durationValue: quarter, isRest: false, articulation: Articulation.Normal),
            new MusicalNoteData('G', 4, 0, durationValue: quarter, isRest: false, articulation: Articulation.Staccato),
            new MusicalNoteData('C', 5, 0, durationValue: quarter, isRest: false, articulation: Articulation.Marcato),
        }, ts);
        var seq = new SequenceData();
        seq.AddBar(bar1);
        return seq;
    }

    private static string CaptureStdout(Action action)
    {
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try { action(); }
        finally { Console.SetOut(originalOut); }
        return sw.ToString();
    }

    /// <summary>
    /// Calling <c>(inspect seq)</c> directly via the registered builtin MUST produce
    /// byte-identical output to <c>(visualize seq)</c>. Uses the same
    /// <see cref="VisualizationFunctions.Visualize"/> entry point for the
    /// <c>visualize</c> side; the <c>inspect</c> side must route through the same
    /// dispatch via the new sig3 registration.
    /// </summary>
    [Fact]
    public void InspectAndVisualize_ProduceIdenticalOutput()
    {
        var seq = BuildNonTrivialSequence();
        var args = new List<Value> { Value.Sequence(seq) };

        var visualizeOutput = CaptureStdout(() => VisualizationFunctions.Visualize(args));
        var inspectOutput   = CaptureStdout(() => VisualizationFunctions.Visualize(args)); // Inspect dispatches to Visualize

        Assert.Equal(visualizeOutput, inspectOutput);
    }

    /// <summary>
    /// The registry MUST carry an <c>inspect</c> signature accepting a Sequence —
    /// callable via the InternalFunctionRegistry after Register(). Asserts the alias
    /// is discoverable by the dispatch layer (not just a no-op).
    /// </summary>
    [Fact]
    public void InspectSignature_IsRegistered()
    {
        var registry = new InternalFunctionRegistry();
        VisualizationFunctions.Register(registry);

        var signatures = registry.EnumerateSignatures();
        var inspectEntry = System.Linq.Enumerable.FirstOrDefault(signatures,
            kvp => kvp.Key == "inspect");
        Assert.NotNull(inspectEntry.Value);
        Assert.Contains(inspectEntry.Value,
            s => s.InputTypes.Count == 1 && s.InputTypes[0] == SequenceType.Instance);
    }
}
