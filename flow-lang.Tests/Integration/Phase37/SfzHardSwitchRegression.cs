using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-02 — when neither xfin nor xfout opcodes are declared, the
/// SfzRenderer falls back to Phase 33's hard-switch region pick. This guards
/// the Phase 33 byte-identical baseline: any region whose XfinLoVel ==
/// XfoutLoVel == -1 emits the same audio bytes Plan 37-03 inherited from
/// Phase 33 (no layerGain multiplication on the fitted buffer).
/// </summary>
[Collection("FlowScripts")]
public class SfzHardSwitchRegression : IDisposable
{
    public SfzHardSwitchRegression()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact]
    public void HardSwitch_RegionWithoutXfade_AppliesNoLayerGain()
    {
        // Inline SFZ patch with no xfin/xfout opcodes — the sentinel defaults
        // (-1, -1, -1, -1) signal "hard-switch fallback active".
        string sfz = @"<region>
sample=stub.wav
lokey=60 hikey=60 lovel=1 hivel=127
pan=0
";
        var data = SfzParser.Parse(sfz, "/tmp/inline_hard_switch.sfz", "hard_switch");
        Assert.Single(data.Regions);
        var region = data.Regions[0];

        // SAMP-02 sentinel check — no xfin/xfout means hard-switch path.
        Assert.Equal(-1, region.XfinLoVel);
        Assert.Equal(-1, region.XfinHiVel);
        Assert.Equal(-1, region.XfoutLoVel);
        Assert.Equal(-1, region.XfoutHiVel);

        // ComputeXfadeGain — the helper SfzRenderer uses to compute layerGain
        // returns 1.0 for hard-switch regions (no attenuation = byte-identical
        // to Phase 33's fitted buffer prior to articulation envelope).
        double gain = SfzRenderer.ComputeXfadeGain_TestOnly(region, midiVelocity: 80);
        Assert.Equal(1.0, gain, precision: 9);
    }
}
