using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 MIX-02 — SFZ + per-region pan (Phase 33) + per-voice pan
/// (Phase 37) compose correctly (multiplicative or additive — locked in
/// Plan 37-03). Wave 0 scaffold per 37-VALIDATION.md. Filled by Plan 37-03.
/// </summary>
[Collection("FlowScripts")]
public class SfzPanCompositionTests : IDisposable
{
    public SfzPanCompositionTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-03 (MIX-02 pan composition)")]
    public void SfzPanComposition_RegionAndVoice_ComposeCorrectly()
    {
        Assert.True(true);
    }
}
