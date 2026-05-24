using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-01 — `seq_position`/`seq_length` opcodes parsed; multiple
/// triggers on the same key produce DIFFERENT samples (round-robin). Wave 0
/// scaffold per 37-VALIDATION.md. Filled by Plan 37-03.
/// </summary>
[Collection("FlowScripts")]
public class SfzRoundRobinTests : IDisposable
{
    public SfzRoundRobinTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-03 (SAMP-01 round-robin)")]
    public void SfzRoundRobin_AdvancesAcrossTriggers()
    {
        Assert.True(true);
    }
}
