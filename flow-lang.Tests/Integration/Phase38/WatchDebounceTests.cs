using System;
using System.Threading;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 / Audit-0609 D3 — debounce coalescing tests.
///
/// Audit-0609 D3 replaced the D-38-05 leading-edge gate with a trailing-edge
/// restartable timer (owner approval 2026-06-09). Each file-change event resets
/// a <see cref="LiveReloadManager.DebounceMs"/>-ms timer; the render fires once
/// after the burst quiesces. This ensures the FINAL write of a format-on-save
/// or atomic-rename editor is never dropped.
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
    /// The debounce constant MUST remain 200ms (D-38-05 value — Audit-0609 D3
    /// changed the debounce SHAPE from leading-edge to trailing-edge but kept
    /// the 200ms window).
    /// </summary>
    [Fact]
    public void DebounceMs_Is200_NotLegacy500()
    {
        Assert.Equal(200, LiveReloadManager.DebounceMs);
    }

    /// <summary>
    /// A burst of three events all within 200ms MUST yield exactly ONE render
    /// reading the FINAL content (trailing-edge contract). We wait >200ms after
    /// the last event to let the timer fire, then assert count == 1.
    /// </summary>
    [Fact]
    public void BurstOfThreeEventsWithin200ms_YieldsExactlyOneRender()
    {
        using var harness = new CountingLiveReloadHarness();

        // Simulate a format-on-save burst: 3 events within ~60ms.
        harness.SimulateChange();
        Thread.Sleep(20);
        harness.SimulateChange();
        Thread.Sleep(20);
        harness.SimulateChange(); // ← this is the "FINAL write"

        // Wait for the trailing-edge timer to fire (200ms from last event).
        Thread.Sleep(350);

        Assert.Equal(1, harness.RenderTriggerCount);
    }

    /// <summary>
    /// Two events 50ms apart MUST coalesce — the debounce timer resets on the
    /// second event so only one render fires.
    /// </summary>
    [Fact]
    public void RapidSaves_Within200ms_CoalesceIntoOneRender()
    {
        using var harness = new CountingLiveReloadHarness();

        harness.SimulateChange();
        Thread.Sleep(50);
        harness.SimulateChange();

        // Wait for the trailing-edge timer to fire.
        Thread.Sleep(350);

        Assert.Equal(1, harness.RenderTriggerCount);
    }

    /// <summary>
    /// Two events 220ms apart produce TWO renders: the first timer fires at
    /// T+200ms (before the second event at T+220ms), the second event starts a
    /// new timer that fires at T+420ms.
    /// </summary>
    [Fact]
    public void TwoSaves_220msApart_TriggerTwoRenders()
    {
        using var harness = new CountingLiveReloadHarness();

        harness.SimulateChange();
        Thread.Sleep(220); // first timer fires at T+200ms; second event at T+220ms

        Assert.Equal(1, harness.RenderTriggerCount); // first render already fired

        harness.SimulateChange();
        Thread.Sleep(350); // wait for second timer

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
