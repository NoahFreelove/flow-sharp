using System;
using System.Threading;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-01 — Wave 0 debounce coalescing tests.
///
/// Asserts that <see cref="LiveReloadManager.DebounceMs"/> is 200ms (D-38-05 /
/// Pitfall #21) and that rapid file-change events within 200ms coalesce into a
/// single render trigger.
///
/// The tests subclass <see cref="LiveReloadManager"/> via a testable seam to
/// count <c>OnRenderTriggered()</c> invocations without booting <c>FlowEngine</c>.
/// </summary>
[Collection("FlowScripts")]
public class WatchDebounceTests : IDisposable
{
    public WatchDebounceTests()
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
    /// D-38-05 LOCK: the debounce constant on <see cref="LiveReloadManager"/>
    /// MUST be 200ms (down from the Phase 28 500ms baseline).
    /// </summary>
    [Fact]
    public void DebounceMs_Is200_NotLegacy500()
    {
        Assert.Equal(200, LiveReloadManager.DebounceMs);
    }

    /// <summary>
    /// Two synthetic Changed events 50ms apart MUST coalesce — the debounce
    /// gate fires the render trigger exactly once.
    /// </summary>
    [Fact]
    public void RapidSaves_Within200ms_CoalesceIntoOneRender()
    {
        using var harness = new CountingLiveReloadHarness();

        harness.SimulateChange();
        Thread.Sleep(50);
        harness.SimulateChange();

        // Give the debounce gate a moment to settle (gate operates on wall-clock).
        Thread.Sleep(50);

        Assert.Equal(1, harness.RenderTriggerCount);
    }

    /// <summary>
    /// Two Changed events 220ms apart cross the 200ms threshold and MUST fire
    /// the render trigger twice.
    /// </summary>
    [Fact]
    public void RapidSaves_220msApart_TriggerTwoRenders()
    {
        using var harness = new CountingLiveReloadHarness();

        harness.SimulateChange();
        Thread.Sleep(220);
        harness.SimulateChange();

        Thread.Sleep(50);

        Assert.Equal(2, harness.RenderTriggerCount);
    }

    /// <summary>
    /// Testable seam: subclass <see cref="LiveReloadManager"/> and override
    /// <c>OnRenderTriggered()</c> so we can count without booting FlowEngine.
    /// </summary>
    private sealed class CountingLiveReloadHarness : LiveReloadManager
    {
        public int RenderTriggerCount;

        public CountingLiveReloadHarness()
            : base(filePath: System.IO.Path.GetTempFileName(), deviceName: null)
        {
        }

        protected override void OnRenderTriggered()
        {
            Interlocked.Increment(ref RenderTriggerCount);
        }

        /// <summary>
        /// Simulates a FileSystemWatcher Changed event by invoking the
        /// internal debounce gate the same way <c>_watcher.Changed</c> does.
        /// </summary>
        public void SimulateChange()
        {
            InvokeTriggerForTesting();
        }
    }
}
