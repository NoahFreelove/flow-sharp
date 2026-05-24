using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DRUM-01 — drum note pitch-shift uses `#auto` PSOLA path for
/// transient kits (kick=36, snare=38). Wave 0 scaffold per
/// 37-VALIDATION.md. Filled by Plan 37-06 (D-37-14 DSP-02/03 dependency).
/// </summary>
[Collection("FlowScripts")]
public class DrumPitchShiftAutoTests : IDisposable
{
    public DrumPitchShiftAutoTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-06 (DRUM-01 #auto pitch shift)")]
    public void DrumPitchShift_TransientKit_RoutesThroughPsola()
    {
        Assert.True(true);
    }
}
