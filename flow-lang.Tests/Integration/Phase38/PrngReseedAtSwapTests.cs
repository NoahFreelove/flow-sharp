using System;
using System.Collections.Generic;
using System.Linq;
using FlowInterpreter;
using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-03 LIVE-03 — Wave 0 PRNG-reseed-at-swap tests.
///
/// Asserts that <see cref="LiveReloadManager"/>'s StagePendingBuffers consumer
/// calls <see cref="PrngRegistry.ResetAtRenderBoundary"/> exactly once per
/// live-swap, per RESEARCH §D line 770. Mirrors the Phase 36 Plan 36-01 API
/// contract — every render boundary clears the (site, name)-keyed Random cache
/// so the next pass reseeds deterministically.
///
/// Uses the new <see cref="PrngRegistry.ResetCallCount"/> instrumentation
/// (analog to <see cref="VoiceAllocator.LastPoolSizeUsedForTests"/> AsyncLocal
/// precedent at VoiceAllocator.cs:23-28) to count invocations without booting
/// FlowEngine.
///
/// Tests are RED until Task 3 wires StagePendingBuffers + the ResetCallCount
/// instrumentation.
/// </summary>
[Collection("FlowScripts")]
public class PrngReseedAtSwapTests : IDisposable
{
    public PrngReseedAtSwapTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Direct unit test: invoking <see cref="LiveReloadManager.StagePendingBuffersForTesting"/>
    /// (the public test seam wrapping the private StagePendingBuffers method)
    /// with a non-empty buffer dict + a synthetic engine MUST call
    /// PrngRegistry.ResetAtRenderBoundary exactly once.
    /// </summary>
    [Fact]
    public void StagePendingBuffers_CallsResetAtRenderBoundaryExactlyOnce()
    {
        // Fresh engine — its PrngRegistry starts at ResetCallCount == 0.
        using var engine = new FlowEngine();
        int before = engine.Context.PrngRegistry.ResetCallCount;

        using var harness = new TestableLiveReloadManager();

        // Synthetic per-block buffer dict — sentinel BlockId=0 (Plan 38-01
        // whole-script mode); the staging path doesn't introspect the bytes.
        var perBlock = new Dictionary<int, LiveBlockBuffer>
        {
            [0] = new LiveBlockBuffer(BlockId: 0, Bytes: new float[16], Length: 16),
        };

        // The engine's LiveBlockRegistry is empty (no live{} blocks parsed) —
        // the auditor-walk loop is a no-op, but the ResetAtRenderBoundary
        // call MUST still fire because the swap path always resets PRNG state
        // at the boundary (RESEARCH §D line 770).
        var blocks = engine.Context.LiveBlockRegistry.Snapshot();

        harness.StagePendingBuffersForTesting(perBlock, engine, blocks);

        int after = engine.Context.PrngRegistry.ResetCallCount;
        Assert.Equal(before + 1, after);
    }

    /// <summary>
    /// Repeated staging passes accumulate ResetCallCount linearly — no
    /// suppression / batching.
    /// </summary>
    [Fact]
    public void StagePendingBuffers_AccumulatesResetCountAcrossSwaps()
    {
        using var engine = new FlowEngine();
        int before = engine.Context.PrngRegistry.ResetCallCount;

        using var harness = new TestableLiveReloadManager();

        var perBlock = new Dictionary<int, LiveBlockBuffer>
        {
            [0] = new LiveBlockBuffer(BlockId: 0, Bytes: new float[16], Length: 16),
        };
        var blocks = engine.Context.LiveBlockRegistry.Snapshot();

        harness.StagePendingBuffersForTesting(perBlock, engine, blocks);
        harness.StagePendingBuffersForTesting(perBlock, engine, blocks);
        harness.StagePendingBuffersForTesting(perBlock, engine, blocks);

        Assert.Equal(before + 3, engine.Context.PrngRegistry.ResetCallCount);
    }

    /// <summary>
    /// Test-only subclass exposing the private StagePendingBuffers method via
    /// a public seam — mirrors the WatchDebounceTests CountingLiveReloadHarness
    /// pattern.
    /// </summary>
    private sealed class TestableLiveReloadManager : LiveReloadManager
    {
        public TestableLiveReloadManager()
            : base(filePath: System.IO.Path.GetTempFileName(), deviceName: null)
        {
        }

        public void StagePendingBuffersForTesting(
            Dictionary<int, LiveBlockBuffer> newBuffers,
            FlowEngine engine,
            IReadOnlyDictionary<int, LiveBlockRegistration> newBlocks)
        {
            StagePendingBuffers(newBuffers, engine, newBlocks);
        }
    }
}
