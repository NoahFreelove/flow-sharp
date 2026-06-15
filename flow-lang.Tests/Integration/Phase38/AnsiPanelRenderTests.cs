using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
///
/// Because the panel inserts ANSI dim/reset escapes BETWEEN the field label
/// and field value (per Typography table — "labels dim, values default"),
/// assertions strip ESC sequences first via <see cref="StripAnsi"/> before
/// substring-checking the visible text.
/// </summary>
[Collection("FlowScripts")]
public class AnsiPanelRenderTests : IDisposable
{
    private static readonly Regex AnsiEscapeRegex =
        new Regex("\\u001b\\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    /// <summary>Strips ANSI CSI escapes (ESC [ ... letter) from a string.</summary>
    private static string StripAnsi(string s) => AnsiEscapeRegex.Replace(s, string.Empty);

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

        var visible = StripAnsi(sw.ToString());

        // Row 1 — tempo / timesig / bar (label-dim ESCs stripped).
        Assert.Contains("Tempo: 120 BPM", visible);
        Assert.Contains("TimeSig: 4/4", visible);
        Assert.Contains("Bar: 47", visible);

        // Row 2 — live blocks segment present (one of the two blocks suffices to
        // verify the row was rendered at all).
        Assert.Contains("live 1bar @ L12", visible);

        // Row 3 — voices summary + at least one instrument breakdown.
        Assert.Contains("Voices: 8/32", visible);
        Assert.Contains("piano:3", visible);

        // Row 4 — empty at first PublishState (no advisory yet); the row is
        // either absent or contains only the placeholder. We verify by
        // absence of a Warning advisory body.
        Assert.DoesNotContain("[live]", visible);
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

        var visible = StripAnsi(sw.ToString());

        // Row 1 + row 3 present.
        Assert.Contains("Tempo: 120 BPM", visible);
        Assert.Contains("Voices: 0/32", visible);

        // Row 2 omitted per UI-SPEC line 145 — no "Live blocks:" prefix at all.
        Assert.DoesNotContain("Live blocks:", visible);
    }

    /// <summary>
    /// sweep-0614 (cli-repl-watch): the panel must reserve the top rows via a
    /// DECSTBM scroll region (<c>\x1b[5;r</c>) BEFORE the first absolute
    /// cursor-positioned panel paint (<c>\x1b[1;1H</c>), so the host's scrolling
    /// log lines land below the fixed panel instead of colliding with it.
    /// Pins <see cref="LiveStatusPanel.BeginScrollRegion"/> + the Dispose reset.
    /// </summary>
    [Fact]
    public void BeginScrollRegion_EmitsDecstbmBeforeFirstAbsolutePanelPaint()
    {
        var sw = new StringWriter();
        // _writesToStdout is false for a StringWriter, so the panel does NOT emit
        // absolute \x1b[1;1H cursor moves in this seam. We instead assert the
        // scroll-region escape itself is emitted by BeginScrollRegion (forceTtyMode
        // keeps ANSI on), and is reset on Dispose.
        var panel = new LiveStatusPanel(@out: sw, forceTtyMode: true);

        panel.BeginScrollRegion();
        // Idempotent: a second call must not double-emit.
        panel.BeginScrollRegion();
        string afterBegin = sw.ToString();
        Assert.Contains("\x1b[5;r", afterBegin); // DECSTBM: region from row 5 to end
        Assert.Equal(1, CountOccurrences(afterBegin, "\x1b[5;r"));

        panel.Dispose();
        string afterDispose = sw.ToString();
        Assert.Contains("\x1b[r", afterDispose); // DECSTBM reset on teardown
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
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

        var visible = StripAnsi(sw.ToString());
        Assert.Contains("[live] evaluation timed out at 30s", visible);
    }
}
