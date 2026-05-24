using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-02 — `(stretch buf 2.0 mode=#auto)` emits stderr
/// `[stretch] mode=#auto picked: X% vocoder / Y% psola across N frames`
/// exactly once per call. Wave 0 scaffold per 37-VALIDATION.md.
/// Filled by Plan 37-02 (D-37-06 one-shot stderr advisory).
/// </summary>
[Collection("FlowScripts")]
public class StretchAutoAdvisoryTests : IDisposable
{
    public StretchAutoAdvisoryTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-02 (DSP-02 #auto advisory)")]
    public void StretchAuto_EmitsOneShotAdvisory_PerCall()
    {
        Assert.True(true);
    }
}
