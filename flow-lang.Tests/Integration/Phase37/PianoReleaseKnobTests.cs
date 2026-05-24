using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 PIANO-01 — `release=` named arg overrides default; release=2.0
/// produces audible tail at t=1.5s past authored end. Wave 0 scaffold per
/// 37-VALIDATION.md. Filled by Plan 37-04 (D-37-11).
/// </summary>
[Collection("FlowScripts")]
public class PianoReleaseKnobTests : IDisposable
{
    public PianoReleaseKnobTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-04 (PIANO-01 release= knob)")]
    public void PianoReleaseKnob_Release2s_ProducesAudibleTail()
    {
        Assert.True(true);
    }
}
