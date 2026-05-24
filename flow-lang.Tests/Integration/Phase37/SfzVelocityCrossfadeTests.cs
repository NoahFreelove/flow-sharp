using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-02 — `xfin_lovel`/`xfin_hivel` opcodes parsed; velocity
/// within the crossfade band produces NON-zero output from BOTH layers.
/// Wave 0 scaffold per 37-VALIDATION.md. Filled by Plan 37-03.
/// </summary>
[Collection("FlowScripts")]
public class SfzVelocityCrossfadeTests : IDisposable
{
    public SfzVelocityCrossfadeTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-03 (SAMP-02 velocity xfade)")]
    public void SfzVelocityCrossfade_BothLayersContribute_InBand()
    {
        Assert.True(true);
    }
}
