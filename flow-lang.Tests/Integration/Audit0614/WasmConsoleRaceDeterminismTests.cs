using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0614;

/// <summary>
/// sweep-0614 regression-wasm-determinism — guards the fix for the D-48-16
/// two-run cmp-clean violation re-introduced by 1f31a5e.
///
/// <para><b>The regression:</b> <see cref="WasmEntry.RunFromJs"/> redirects the
/// PROCESS-GLOBAL <c>Console.Out</c>/<c>Console.Error</c> to capture Flow
/// <c>print</c> + advisory output (D-48-15). 1f31a5e moved the FULL per-run
/// engine bootstrap (@std + style packs) INSIDE that redirect window, widening
/// it dramatically. Under a parallel test runner — where ~60 other test files
/// (and the shared FlowEngineRunner fixture) also swap Console.Out/Error — those
/// swaps interleaved with the wide WASM redirect window, so the run's
/// <c>print</c>/advisory output landed in the WRONG writer. The captured
/// stdout/stderr came back EMPTY and the two-run RunResult JSON diverged
/// (<c>SameSource_TwoRuns_IdenticalRunResultJson</c> + the WASM stdout Facts
/// flipped red only in full-suite order).</para>
///
/// <para><b>The fix has two layers:</b>
/// <list type="number">
///   <item>The engine is now built OUTSIDE the Console-redirect window
///         (<see cref="WasmEntry.RunFromJs"/>), shrinking the window to just
///         <c>engine.Execute</c> — defense-in-depth narrowing.</item>
///   <item><c>flow-lang.Tests/xunit.runner.json</c> sets
///         <c>parallelizeTestCollections=false</c> — the HARD guarantee on a
///         shared-process runner. The browser is single-threaded, so this
///         changes nothing about runtime behavior.</item>
/// </list></para>
///
/// <para>Shares the Phase 48 Console-redirection serial collection because
/// RunFromJs redirects process-wide Console streams.</para>
/// </summary>
[Collection(FlowLang.Tests.Integration.Phase48.WasmEntryConsoleCollection.Name)]
public sealed class WasmConsoleRaceDeterminismTests
{
    private const string AdvisorySource =
        "use \"@patterns\"\n" +
        "Sequence s = | C4q D4q E4q F4q |\n" +
        "(fast s 0.0)\n";

    private static string StripDurationMs(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("durationMs");
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Structural pin: the test project's <c>xunit.runner.json</c> MUST disable
    /// cross-collection parallelism. A future contributor re-enabling it (or
    /// deleting the file) silently re-opens the process-global Console race that
    /// flips RunResult stdout/stderr to empty. This Fact fails loudly so the
    /// guarantee can't regress unnoticed.
    /// </summary>
    [Fact]
    public void XunitConfig_DisablesCollectionParallelism()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "xunit.runner.json");
        Assert.True(File.Exists(path),
            "xunit.runner.json must be copied to the test output dir — it pins " +
            "parallelizeTestCollections=false (sweep-0614 regression-wasm-determinism).");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(
            doc.RootElement.TryGetProperty("parallelizeTestCollections", out var flag),
            "xunit.runner.json must declare parallelizeTestCollections.");
        Assert.False(flag.GetBoolean(),
            "parallelizeTestCollections MUST be false — RunFromJs redirects the " +
            "process-global Console; parallel collections would clobber the " +
            "capture and break D-48-16 two-run cmp-clean.");
    }

    /// <summary>
    /// End-to-end behavioral pin (serialized, as the whole suite now runs):
    /// many sequential pairs of the same advisory-emitting source must each
    /// produce byte-identical RunResult JSON (minus durationMs) AND actually
    /// carry the <c>[fast]</c> advisory in stderr. This is the contract that
    /// flipped red in full-suite order before the fix — the captured
    /// stdout/stderr came back EMPTY when a parallel collection clobbered the
    /// process-global Console during RunFromJs's (then-wide) redirect window.
    ///
    /// <para>Running MANY pairs (not just one) makes the byte-identity contract
    /// a strong signal: every iteration re-runs the full lex→parse→interpret
    /// pipeline through a fresh engine and the per-run WarnOnce reset, so a
    /// re-introduced process-static leak (advisory dropped on a later run) or a
    /// capture regression would surface as a non-identical pair or a missing
    /// <c>[fast]</c> line.</para>
    ///
    /// <para>NOTE: this Fact relies on the serialization guarantee pinned by
    /// <see cref="XunitConfig_DisablesCollectionParallelism"/> — on a shared
    /// process, a parallel collection swapping the global Console cannot be
    /// defended against in product code without an injectable output writer
    /// (a v1.6 surface change). Serialization is the correct guarantee.</para>
    /// </summary>
    [Fact]
    public void RunFromJs_AdvisorySource_ManyPairs_ByteIdentical()
    {
#pragma warning disable CA1416 // browser-only export invoked from Desktop for regression
        WasmEntry.DisposeFromJs();

        for (int i = 0; i < 16; i++)
        {
            var json1 = WasmEntry.RunFromJs(AdvisorySource);
            var json2 = WasmEntry.RunFromJs(AdvisorySource);

            var stderr1 = (string?)JsonNode.Parse(json1)!.AsObject()["stderr"] ?? string.Empty;
            // Defense against vacuous pass: the advisory MUST be captured.
            Assert.Contains("[fast]", stderr1);

            var bytes1 = Encoding.UTF8.GetBytes(StripDurationMs(json1));
            var bytes2 = Encoding.UTF8.GetBytes(StripDurationMs(json2));
            Assert.Equal(bytes1, bytes2);
        }
#pragma warning restore CA1416
    }
}
