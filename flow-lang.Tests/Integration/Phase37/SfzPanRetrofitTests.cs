using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 MIX-02 — SFZ-rendered voice with `voice.Pan = 0.7` produces
/// stereo with right-louder-than-left. Wave 0 scaffold per 37-VALIDATION.md.
/// Filled by Plan 37-03 (D-37-16 SFZ pan retrofit).
/// </summary>
[Collection("FlowScripts")]
public class SfzPanRetrofitTests : IDisposable
{
    public SfzPanRetrofitTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-03 (MIX-02 SFZ pan retrofit)")]
    public void SfzPanRetrofit_VoicePan07_RightLouderThanLeft()
    {
        Assert.True(true);
    }
}
