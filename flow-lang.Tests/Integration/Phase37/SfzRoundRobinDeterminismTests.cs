using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-01 — round-robin sequence deterministic across two
/// consecutive renders (voice ordinal seed). Wave 0 scaffold per
/// 37-VALIDATION.md. Filled by Plan 37-03.
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

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-03 (SAMP-01 RR determinism)")]
    public void SfzRoundRobin_TwoRuns_ByteIdentical()
    {
        Assert.True(true);
    }
}
