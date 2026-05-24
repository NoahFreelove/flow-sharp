using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-03 — `(pitchShift buf +5st)` shifts pitch by 5 semitones,
/// preserves duration within ±1 sample. Wave 0 scaffold per 37-VALIDATION.md.
/// Filled by Plan 37-02.
/// </summary>
[Collection("FlowScripts")]
public class PitchShiftTests : IDisposable
{
    public PitchShiftTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-02 (DSP-03 pitchShift)")]
    public void PitchShift_FiveSemitones_PreservesDuration()
    {
        Assert.True(true);
    }
}
