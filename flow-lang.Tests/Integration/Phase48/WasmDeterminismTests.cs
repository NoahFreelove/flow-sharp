using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-05 — Two-run cmp-clean determinism pin for the WASM
/// runtime path per D-48-16. The same Flow source executed twice through
/// <see cref="WasmEntry.RunFromJs"/> must produce byte-identical structured
/// output. CLAUDE.md "## Conventions §Two-run cmp-clean determinism" is
/// load-bearing for Phase 28+; D-48-16 carries the contract forward to the
/// Web target.
///
/// <para><b>Measurement strategy:</b> The two-run cmp-clean contract is
/// strongest at the rendered-byte level (Float32Array PCM equality). On
/// Desktop without a browser host the rendered audio path is captured
/// only as side effect via <c>WebAudioBackend.Play</c>, but the structured
/// <see cref="RunResult"/> JSON serializes deterministic stdout/stderr
/// capture plus the structured-error array. Byte-equal JSON across two
/// runs proves the FlowEngine.Execute pipeline (lex → parse → interpret →
/// print) is deterministic; the audio-rendering path inherits the same
/// FlowEngine root, so the determinism contract holds end-to-end (Phase 28
/// dither-RNG seeding precedent — deterministic seeds = byte-identical
/// renders).</para>
///
/// <para><b>Cross-platform caveat (D-36-09):</b> chaos primitives (Lorenz,
/// logistic) are forward-Euler chaotic integrators; chained FP arithmetic
/// diverges exponentially across platforms after ~50 iterations. Same-
/// platform two-run cmp-clean is preserved (and what this test checks);
/// cross-platform is NOT guaranteed for chaos primitives. THIS test uses
/// only <c>print</c> + <c>add</c> — pure integer arithmetic that holds
/// cross-platform as well.</para>
///
/// <para><b>Platform suppression:</b> <see cref="WasmEntry.RunFromJs"/> carries
/// <c>[SupportedOSPlatform("browser")]</c>; calling from Desktop fires CA1416.
/// This is correct on the call site — the [JSExport] boundary is browser-only
/// for marshalling, but the underlying <c>FlowEngine.Execute</c> path is
/// platform-agnostic. Verifying determinism on Desktop is a valid proxy for
/// Web behavior (the Web build runs the same flow-lang.dll through the same
/// Execute entry, just with [JSImport]/[JSExport] marshalling shims; the
/// determinism comes from FlowEngine.Execute itself, NOT from the marshalling
/// layer).</para>
/// </summary>
public class WasmDeterminismTests
{
    /// <summary>
    /// Deterministic source — pure arithmetic + print. No chaos primitives,
    /// no music timing, no <c>random</c> / <c>randomInt</c>. Cross-platform
    /// byte-identical (D-36-09 chaos-caveat does NOT apply).
    /// </summary>
    private const string DeterministicSource =
        "(print \"hello flow\")\n(print 42)\n(print (add 1 2))";

    /// <summary>
    /// Strips the <c>durationMs</c> field from a RunResult JSON string.
    /// <c>durationMs</c> measures wall-clock <see cref="System.Diagnostics.Stopwatch"/>
    /// time and legitimately varies across runs (a few-ms jitter from CLR / kernel
    /// scheduling). Stripping it leaves the deterministic-payload fields
    /// (<c>wav</c> / <c>midi</c> / <c>stdout</c> / <c>stderr</c> / <c>errors</c>)
    /// for byte-equal comparison.
    /// </summary>
    private static string StripDurationMs(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("durationMs");
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    [Fact]
    public void SameSource_TwoRuns_IdenticalStdout()
    {
        // [SupportedOSPlatform("browser")] on WasmEntry.RunFromJs fires CA1416
        // when called from Desktop. The Execute path is platform-agnostic; the
        // browser-only marker is a marshalling-boundary concern, not a runtime
        // gate. Suppress for the determinism test.
#pragma warning disable CA1416 // calling browser-only API from Desktop for determinism check
        var json1 = WasmEntry.RunFromJs(DeterministicSource);
        var json2 = WasmEntry.RunFromJs(DeterministicSource);
#pragma warning restore CA1416

        var stdout1 = JsonDocument.Parse(json1).RootElement.GetProperty("stdout").GetString();
        var stdout2 = JsonDocument.Parse(json2).RootElement.GetProperty("stdout").GetString();

        Assert.Equal(stdout1, stdout2);
        // Smoke-check the actual content (defense against vacuous equality —
        // both empty strings would also pass the previous Assert.Equal).
        Assert.Contains("hello flow", stdout1!);
        Assert.Contains("42", stdout1);
        Assert.Contains("3", stdout1); // (add 1 2)
    }

    [Fact]
    public void SameSource_TwoRuns_IdenticalRunResultJson()
    {
#pragma warning disable CA1416 // calling browser-only API from Desktop for determinism check
        var json1 = WasmEntry.RunFromJs(DeterministicSource);
        var json2 = WasmEntry.RunFromJs(DeterministicSource);
#pragma warning restore CA1416

        // Strip durationMs (legitimate wall-clock jitter) before byte-equal cmp.
        var stripped1 = StripDurationMs(json1);
        var stripped2 = StripDurationMs(json2);

        // Compare as UTF-8 bytes — strongest possible determinism signal.
        // System.Text.Json preserves insertion order; reserialization through
        // JsonNode.ToJsonString preserves the original field ordering so the
        // strip-and-cmp is sound.
        var bytes1 = Encoding.UTF8.GetBytes(stripped1);
        var bytes2 = Encoding.UTF8.GetBytes(stripped2);
        Assert.Equal(bytes1, bytes2);
    }
}
