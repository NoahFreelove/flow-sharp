using System.Text.Json.Nodes;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-06 (fix, cycle 3) — regression net for the
/// single-threaded-WASM DEADLOCK defect class. See
/// <c>.planning/debug/wasm-boot-no-app-bundle.md</c> (cycle 3).
///
/// <para><b>Why this test exists:</b> <see cref="WasmEntry.RunFromJs"/> originally
/// wrapped <c>FlowEngine.Execute</c> in <c>Task.Run + workerTask.Wait(30s)</c>
/// (the D-48-10 hard wall-clock cap, Pattern C carried over from Phase 38 LIVE-02).
/// Mono-WASM is SINGLE-THREADED by default — <c>Task.Run</c> queues the work to the
/// one main thread and <c>Wait</c> then blocks that same thread, so
/// <c>Execute</c> never ran. Every browser call DEADLOCKED and returned
/// <c>kind="cancel"</c> at exactly 30s. The fix runs <c>Execute</c> SYNCHRONOUSLY
/// on the calling thread; the 30s cap becomes best-effort / non-preemptive
/// in-browser.</para>
///
/// <para><b>Coverage honesty (partial proxy):</b> the Desktop xUnit runner is
/// MULTI-THREADED, so the old <c>Task.Run</c> shape would have PASSED here while
/// the browser deadlocked — exactly why this defect class kept hiding behind
/// Desktop in-process tests. These Facts assert the post-fix contract that a
/// simple script runs to COMPLETION through <see cref="WasmEntry.RunFromJs"/> and
/// returns populated stdout / empty errors. They cannot reproduce a true
/// single-threaded deadlock on Desktop; the REAL confirmation is the human browser
/// re-smoke (audible 440 Hz tone, no <c>kind="cancel"</c>). This is the strongest
/// available browser-free proxy: if RunFromJs ever stops running the script to
/// completion (e.g. a future re-introduction of a blocking worker wrapper that also
/// deadlocked on Desktop), these Facts catch it.</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class WasmSynchronousExecutionTests
{
    /// <summary>
    /// A simple <c>print</c> script runs to COMPLETION and returns populated
    /// stdout with no errors — proving synchronous execution actually ran the
    /// script (a deadlock would have produced empty stdout + a cancel error).
    /// </summary>
    [Fact]
    public void RunFromJs_SimpleScript_RunsToCompletion_PopulatedStdout_NoErrors()
    {
#pragma warning disable CA1416 // browser-only export; the Execute path is platform-agnostic on Desktop
        var json = WasmEntry.RunFromJs("(print \"hi\")");
#pragma warning restore CA1416

        var node = JsonNode.Parse(json)!.AsObject();

        // stdout proves the script body actually executed (not a deadlock-timeout).
        Assert.Contains("hi", (string?)node["stdout"] ?? string.Empty);

        // No errors at all — and in particular NO kind="cancel" (the deadlock
        // signature). The cap is non-preemptive in single-threaded WASM, so a
        // fast script can never be cancelled.
        var errors = node["errors"]!.AsArray();
        Assert.Empty(errors);
    }

    /// <summary>
    /// A short tone-render script (the Plan 48-06 smoke shape, minus playback)
    /// runs to COMPLETION through the export with no cancel/runtime errors. The
    /// browser smoke plays the tone; here we only confirm the Execute pipeline
    /// returns cleanly rather than deadlocking on the 30s cap.
    /// </summary>
    [Fact]
    public void RunFromJs_ToneRender_RunsToCompletion_NoCancel()
    {
#pragma warning disable CA1416 // browser-only export; the Execute path is platform-agnostic on Desktop
        // createSineTone renders a 0.5s buffer; the trailing print makes stdout
        // non-empty, proving the render statement ran to completion (a deadlock
        // would leave stdout empty + a cancel error). `use "@audio"` resolves on
        // the Desktop runner's real filesystem; in-browser stdlib VFS mounting is
        // a separate follow-up (see debug file) and does NOT affect this proxy.
        var json = WasmEntry.RunFromJs(
            "use \"@audio\"\nBuffer tone = (createSineTone 1.0 440.0 0.5)\n(print \"rendered\")");
#pragma warning restore CA1416

        var node = JsonNode.Parse(json)!.AsObject();

        Assert.Contains("rendered", (string?)node["stdout"] ?? string.Empty);

        // No cancel/runtime error — a deadlock would have surfaced kind="cancel".
        foreach (var e in node["errors"]!.AsArray())
        {
            var kind = (string?)e!.AsObject()["kind"];
            Assert.NotEqual("cancel", kind);
        }
    }
}
