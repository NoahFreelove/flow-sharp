using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-02 — Pitfall 11 identity fast-paths.
/// <c>(stretch buf 1.0)</c> and <c>(pitchShift buf 0c)</c> return the input
/// verbatim (byte-for-byte). Preserves two-run cmp-clean determinism.
/// Filled by Plan 37-02 Task 2.
/// </summary>
[Collection("FlowScripts")]
public class StretchIdentityTests : IDisposable
{
    public StretchIdentityTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        Phase37Fixtures.EnsureFixturesExist();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact]
    public void Stretch_FactorOne_ReturnsInputByteIdentical()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        foreach (var mode in new[] { StretchMode.Vocoder, StretchMode.Psola, StretchMode.Auto })
        {
            var result = StretchEngine.Process(input, factor: 1.0, mode: mode);
            Assert.Same(input, result);
        }
    }

    [Fact]
    public void PitchShift_ZeroCents_ReturnsInputByteIdentical()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        foreach (var mode in new[] { StretchMode.Vocoder, StretchMode.Psola, StretchMode.Auto })
        {
            var result = PitchShiftEngine.Process(input, cents: 0.0, mode: mode);
            Assert.Same(input, result);
        }
    }
}
