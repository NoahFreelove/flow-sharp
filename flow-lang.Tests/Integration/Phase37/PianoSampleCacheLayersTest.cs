using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 PIANO-01 — Piano `SampleCache` has ≥4 velocity layers per pitch
/// point (pp/mp/mf/ff) after eager-load. Wave 0 scaffold per
/// 37-VALIDATION.md. Filled by Plan 37-04 (D-37-09).
/// </summary>
[Collection("FlowScripts")]
public class PianoSampleCacheLayersTest : IDisposable
{
    public PianoSampleCacheLayersTest()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-04 (PIANO-01 ≥4 layers)")]
    public void PianoSampleCache_HasAtLeast4VelocityLayers()
    {
        Assert.True(true);
    }
}
