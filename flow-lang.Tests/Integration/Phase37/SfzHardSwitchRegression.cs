using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-02 — hard-switch fallback when xfin/xfout absent matches
/// Phase 33 byte-identical baseline. Wave 0 scaffold per 37-VALIDATION.md.
/// Filled by Plan 37-03.
/// </summary>
[Collection("FlowScripts")]
public class SfzHardSwitchRegression : IDisposable
{
    public SfzHardSwitchRegression()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-03 (SAMP-02 hard-switch regression)")]
    public void SfzHardSwitch_NoXfade_MatchesPhase33Baseline()
    {
        Assert.True(true);
    }
}
