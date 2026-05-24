using System;
using System.Collections.Generic;
using System.IO;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-01 — Wave 0 ANSI panel rendering tests.
///
/// Asserts the 4-row ANSI live status panel layout per UI-SPEC §"Panel Layout"
/// (rows 127-133): tempo+timesig+bar, live blocks (omitted if zero), voices
/// N/M + per-instrument breakdown, sticky advisory.
///
/// Captures panel output to a <see cref="StringWriter"/> via the
/// <see cref="LiveStatusPanel"/> ctor's <c>out</c> seam; passes
/// <c>forceTtyMode: true</c> so the ANSI path is exercised even when the test
/// runner has redirected stdout.
/// </summary>
[Collection("FlowScripts")]
public class AnsiPanelRenderTests : IDisposable
{
    public AnsiPanelRenderTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact]
    public void Render_With2LiveBlocks_Emits4RowsWithBarTempoVoicesAdvisory()
    {
        var sw = new StringWriter();
        using var panel = new LiveStatusPanel(@out: sw, forceTtyMode: true);

        var blocks = new List<LiveBlockDisplay>
        {
            new(Quantize: "1bar", Line: 12, LastSwapBar: 47, SecondsSinceSwap: 0),
            new(Quantize: "2bar", Line: 34, LastSwapBar: 46, SecondsSinceSwap: 64),
        };
        var instruments = new Dictionary<string, int>
        {
            ["piano"] = 3,
            ["brass"] = 2,
            ["strings"] = 3,
        };

        panel.PublishState(
            tempo: 120,
            timesig: (4, 4),
            bar: 47,
            blocks: blocks,
            activeVoices: 8,
            poolSize: 32,
            perInstrumentCount: instruments);

        var output = sw.ToString();

        // Row 1 — tempo / timesig / bar.
        Assert.Contains("Tempo: 120 BPM", output);
        Assert.Contains("TimeSig: 4/4", output);
        Assert.Contains("Bar: 47", output);

        // Row 2 — live blocks segment present (one of the two blocks suffices to
        // verify the row was rendered at all).
        Assert.Contains("live 1bar @ L12", output);

        // Row 3 — voices summary + at least one instrument breakdown.
        Assert.Contains("Voices: 8/32", output);
        Assert.Contains("piano:3", output);

        // Row 4 — empty at first PublishState (no advisory yet); the row is
        // either absent or contains only the placeholder. We verify by
        // absence of a Warning advisory body.
        Assert.DoesNotContain("[live]", output);
    }

    [Fact]
    public void Render_With0LiveBlocks_OmitsRow2_Renders3Rows()
    {
        var sw = new StringWriter();
        using var panel = new LiveStatusPanel(@out: sw, forceTtyMode: true);

        panel.PublishState(
            tempo: 120,
            timesig: (4, 4),
            bar: 1,
            blocks: Array.Empty<LiveBlockDisplay>(),
            activeVoices: 0,
            poolSize: 32,
            perInstrumentCount: new Dictionary<string, int>());

        var output = sw.ToString();

        // Row 1 + row 3 present.
        Assert.Contains("Tempo: 120 BPM", output);
        Assert.Contains("Voices: 0/32", output);

        // Row 2 omitted per UI-SPEC line 145 — no "Live blocks:" prefix at all.
        Assert.DoesNotContain("Live blocks:", output);
    }

    [Fact]
    public void PublishAdvisory_PopulatesRow4_WithSurfacePrefix()
    {
        var sw = new StringWriter();
        using var panel = new LiveStatusPanel(@out: sw, forceTtyMode: true);

        panel.PublishState(
            tempo: 120,
            timesig: (4, 4),
            bar: 5,
            blocks: Array.Empty<LiveBlockDisplay>(),
            activeVoices: 0,
            poolSize: 32,
            perInstrumentCount: new Dictionary<string, int>());

        panel.PublishAdvisory(
            body: "[live] evaluation timed out at 30s — keeping previous version",
            level: AdvisoryLevel.Warning,
            dedupKey: "live-timeout:test.flow");

        var output = sw.ToString();
        Assert.Contains("[live] evaluation timed out at 30s", output);
    }
}
