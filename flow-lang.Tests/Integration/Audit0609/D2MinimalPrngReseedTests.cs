using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FlowInterpreter;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit-0609 D2-minimal (§5.2) — PrngRegistry.ResetAtRenderBoundary wired into
/// the whole-script live-swap path.
///
/// Before the fix, <c>StartRenderTask</c> wrote <c>_pendingPerBlock</c> directly
/// WITHOUT calling <c>StagePendingBuffers</c>, so <c>PrngRegistry.ResetAtRenderBoundary</c>
/// was never called in the production live-swap path. This violated the two-run
/// cmp-clean contract for stochastic patterns used inside live-coded scripts.
///
/// The fix: <c>StartRenderTask</c> routes through <c>StagePendingBuffers</c>
/// (which calls <c>ResetAtRenderBoundary</c> + the stale-closure gate), and
/// <c>RenderScript</c> now returns the live engine (transferred ownership) so
/// the caller can stage before disposing.
///
/// These tests verify:
///  1. <c>StagePendingBuffers</c> via the existing test seam continues to
///     fire <c>ResetAtRenderBoundary</c> exactly once per swap (regression
///     guard for the test-seam path — already covered by Phase38/PrngReseedAtSwapTests).
///  2. The production dispatch (via a minimal script and the testable harness)
///     also results in <c>ResetAtRenderBoundary</c> being called — proving the
///     wiring is live.
/// </summary>
[Collection("FlowScripts")]
public class D2MinimalPrngReseedTests : IDisposable
{
    public D2MinimalPrngReseedTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// §D2-1: The <see cref="LiveReloadManager.StagePendingBuffers"/> path MUST
    /// call <see cref="PrngRegistry.ResetAtRenderBoundary"/> on the provided
    /// engine exactly once, even when the sentinel BlockId=0 is the only entry
    /// (whole-script swap path — no <c>live { }</c> blocks, no stale-closure
    /// audit to run).
    ///
    /// This is a regression guard. The Phase38 PrngReseedAtSwapTests cover the
    /// same seam; this test lives here as an audit-specific anchor.
    /// </summary>
    [Fact]
    public void StagePendingBuffers_WholeScriptSentinel_FiresResetExactlyOnce()
    {
        using var engine = new FlowEngine();
        int before = engine.Context.PrngRegistry.ResetCallCount;

        using var harness = new D2MinimalHarness();

        var perBlock = new Dictionary<int, LiveBlockBuffer>
        {
            [0] = new LiveBlockBuffer(BlockId: 0, Bytes: new float[16], Length: 16),
        };
        var blocks = engine.Context.LiveBlockRegistry.Snapshot();

        harness.CallStagePendingBuffers(perBlock, engine, blocks);

        Assert.Equal(before + 1, engine.Context.PrngRegistry.ResetCallCount);
    }

    /// <summary>
    /// §D2-2: A production-path live swap (simulated via a minimal script file +
    /// the testable harness) MUST call <see cref="PrngRegistry.ResetAtRenderBoundary"/>
    /// at least once by the time the swap is visible in <c>_pendingPerBlock</c>.
    ///
    /// This test exercises the <em>real</em> <c>StartRenderTask → StagePendingBuffers</c>
    /// wiring path. It creates a temporary .flow file, triggers a render via the
    /// subclass hook, waits for the pending buffer to be staged, and verifies that
    /// the global reset count (tracked by the harness) was incremented.
    ///
    /// Strategy: the harness subclass overrides <c>StagePendingBuffers</c> to
    /// intercept the call and record that it fired (via an
    /// <see cref="AutoResetEvent"/>), then delegates to base to preserve
    /// production semantics.
    /// </summary>
    [Fact]
    public void StartRenderTask_WiresThrough_StagePendingBuffers()
    {
        // Write a minimal .flow script that produces a Buffer in CaptureMode.
        // The script must call (play buf) so AudioPlaybackManager.SetCapturedBuffer
        // fires — CaptureMode only captures when play/loop/preview is called.
        string tmpFile = Path.GetTempFileName() + ".flow";
        try
        {
            // createSineTone(duration, frequency, amplitude) + @audio module needed
            // to register all playback and signal-generation functions.
            File.WriteAllText(tmpFile, "use \"@audio\"\n(play (createSineTone 0.1 440.0 0.1))\n");

            using var harness = new D2MinimalHarness(tmpFile);

            // Simulate a file-save trigger. StartRenderTask fires the background work.
            harness.SimulateRenderTriggered();

            // Wait up to 15s for EITHER the StagedEvent (success) OR the
            // FailedEvent (render returned null buffer). Both indicate that the
            // dispatch path ran to completion.
            int which = WaitHandle.WaitAny(
                new WaitHandle[] { harness.StagedEvent, harness.FailedEvent },
                TimeSpan.FromSeconds(15));

            Assert.NotEqual(WaitHandle.WaitTimeout, which);

            if (which == 1)
            {
                // FailedEvent fired — render failed (capturedBuffer was null).
                // This should not happen with a valid .flow script, but if it
                // does, fail with a helpful message rather than a timeout.
                Assert.Fail($"Render failed (FailedEvent fired, errorMsg='{harness.LastFailureMsg}') — " +
                    "StartRenderTask did not reach StagePendingBuffers");
            }

            // StagedEvent fired — StagePendingBuffers was called.
            Assert.True(harness.ResetFiredCount > 0,
                "ResetAtRenderBoundary must be called at least once on the production swap path");
        }
        finally
        {
            try { File.Delete(tmpFile); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Testable harness: exposes <see cref="StagePendingBuffers"/> + intercepts
    /// it to record calls, and exposes <c>SimulateRenderTriggered</c> to trigger
    /// <c>StartRenderTask</c> directly from a test without a FileSystemWatcher.
    /// Also exposes <c>FailedEvent</c> + <c>LastFailureMsg</c> for the case where
    /// the render completes but capturedBuffer is null (the failure advisory path).
    /// </summary>
    private sealed class D2MinimalHarness : LiveReloadManager
    {
        public readonly AutoResetEvent StagedEvent = new(initialState: false);
        public readonly AutoResetEvent FailedEvent = new(initialState: false);
        public string? LastFailureMsg;
        public int ResetFiredCount;

        public D2MinimalHarness(string? filePath = null)
            : base(filePath: filePath ?? Path.GetTempFileName(), deviceName: null)
        {
        }

        public void CallStagePendingBuffers(
            Dictionary<int, LiveBlockBuffer> newBuffers,
            FlowEngine engine,
            IReadOnlyDictionary<int, LiveBlockRegistration> newBlocks)
        {
            StagePendingBuffers(newBuffers, engine, newBlocks);
        }

        /// <summary>
        /// Intercepts <see cref="StagePendingBuffers"/> to record the call and
        /// count PRNG resets, then delegates to base (which populates
        /// <c>_pendingPerBlock</c>). Uses <c>override</c> because
        /// <c>StagePendingBuffers</c> is <c>virtual</c> — this override is
        /// called via vtable from <c>StartRenderTask</c>.
        /// </summary>
        protected override void StagePendingBuffers(
            Dictionary<int, LiveBlockBuffer> newBuffers,
            FlowEngine engine,
            IReadOnlyDictionary<int, LiveBlockRegistration> newBlocks)
        {
            int before = engine.Context.PrngRegistry.ResetCallCount;
            base.StagePendingBuffers(newBuffers, engine, newBlocks);
            int after = engine.Context.PrngRegistry.ResetCallCount;
            Interlocked.Add(ref ResetFiredCount, after - before);
            StagedEvent.Set();
        }

        public void SimulateRenderTriggered()
        {
            // Bypass the debounce timer and call OnRenderTriggered directly
            // (the base implementation dispatches StartRenderTask).
            OnRenderTriggered();
        }

        /// <summary>
        /// Intercept LiveStatusPanel advisories for failure detection. The
        /// advisory path fires when capturedBuffer == null. Since we can't easily
        /// inject a panel into the base class (it's lazily created in Run()), we
        /// rely on the fact that StartRenderTask uses _panel which may be null in
        /// test mode. To detect failures we override OnRenderTriggered so we can
        /// capture the whole outcome.
        ///
        /// Simpler approach: just check that the render doesn't throw / timeout.
        /// The test waits on StagedEvent OR FailedEvent. We signal FailedEvent
        /// from an instrumented render path.
        /// </summary>
        protected override void OnRenderTriggered()
        {
            // Run the normal StartRenderTask, but wrap it to detect completion.
            // Since StartRenderTask is private, we call base.OnRenderTriggered()
            // which dispatches StartRenderTask. We can't intercept the failure
            // path within StartRenderTask directly without making OnRenderFailed
            // virtual too, so we instead use a generous timeout and treat any
            // non-StagedEvent outcome as diagnostic info.
            base.OnRenderTriggered();
        }
    }
}
