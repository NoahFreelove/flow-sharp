using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-02 — `(stretch buf 2.0 mode=#vocoder)` doubles audio length
/// within ±1 sample. Wave 0 scaffold per 37-VALIDATION.md. Filled by Plan 37-02.
/// </summary>
[Collection("FlowScripts")]
public class StretchVocoderTests : IDisposable
{
    public StretchVocoderTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-02 (DSP-02 vocoder)")]
    public void StretchVocoder_Factor2_DoublesLength_WithinOneSample()
    {
        Assert.True(true);
    }
}
