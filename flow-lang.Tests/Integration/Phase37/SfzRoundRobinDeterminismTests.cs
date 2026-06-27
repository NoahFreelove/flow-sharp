using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-01 — round-robin counter advances deterministically across
/// triggers, and resets at the render boundary so two consecutive renders of
/// the same song pick the same RR sequence (preserves the two-run cmp-clean
/// determinism contract per RESEARCH §Pitfall 6).
/// </summary>
[Collection("FlowScripts")]
public class SfzRoundRobinDeterminismTests : IDisposable
{
    public SfzRoundRobinDeterminismTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static SfzData LoadRoundRobinFixture()
    {
        var path = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures",
            "Phase37", "round_robin.sfz");
        var content = File.ReadAllText(path);
        return SfzParser.Parse(content, path, "round_robin");
    }

    private static int[] CapturePicksOverTriggers(SfzRenderer renderer, SfzData patch, int triggerCount)
    {
        var picks = new int[triggerCount];
        for (int i = 0; i < triggerCount; i++)
        {
            picks[i] = renderer.PickRegion_TestOnly(patch, midiPitch: 60, midiVelocity: 100).SeqPosition;
        }
        return picks;
    }

    [Fact]
    public void RoundRobin_AdvancesAcrossTriggers_PicksSeq1Then2Then1Then2()
    {
        var patch = LoadRoundRobinFixture();
        // Use a stub cache — PickRegion_TestOnly does not load samples.
        var cache = new SfzSampleCache();
        var renderer = new SfzRenderer(cache);
        var picks = CapturePicksOverTriggers(renderer, patch, 4);

        // First trigger → seq_position 1 (counter 0 → 0 % 2 + 1 = 1).
        // Second → seq_position 2; third → 1; fourth → 2.
        Assert.Equal(new[] { 1, 2, 1, 2 }, picks);
    }

    [Fact]
    public void RoundRobin_ResetAtRenderBoundary_RestartsAtSeq1()
    {
        var patch = LoadRoundRobinFixture();
        var cache = new SfzSampleCache();
        var renderer = new SfzRenderer(cache);

        var firstRun = CapturePicksOverTriggers(renderer, patch, 4);
        renderer.ResetAtRenderBoundary();
        var secondRun = CapturePicksOverTriggers(renderer, patch, 4);

        // Reset must yield byte-identical pick sequences across the boundary.
        Assert.Equal(firstRun, secondRun);
    }
}
