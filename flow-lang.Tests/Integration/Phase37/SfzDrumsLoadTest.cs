using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DRUM-01 — `(loadSfz #drums)` resolves to `GM-StylePerc.sfz`
/// and parses without error. Wave 0 scaffold per 37-VALIDATION.md.
/// Filled by Plan 37-06 (D-37-13 VSCO-CE SFZ drums).
/// </summary>
[Collection("FlowScripts")]
public class SfzDrumsLoadTest : IDisposable
{
    public SfzDrumsLoadTest()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact(Skip = "Wave 0 scaffold — implemented by Plan 37-06 (DRUM-01 SFZ drums load)")]
    public void SfzDrumsLoad_DrumsSymbol_ResolvesAndParses()
    {
        Assert.True(true);
    }
}
