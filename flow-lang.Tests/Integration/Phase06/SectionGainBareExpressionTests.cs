using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase06;

/// <summary>
/// FIX-02 x AUDIO-06 regression gate: bare note streams inside
/// section { gain N { ... } } render > 0 frames. Pre-fix (before commit
/// 2156690) this silently rendered 0 frames; the existing .flow script
/// tests/test_section_gain_bare_expr.flow printed "PASSED" regardless of
/// frame count, so the pre-fix regression was invisible to the Theory
/// harness. This Fact asserts on the numeric frame count (via stdout), not
/// a stdout sentinel, so any future re-introduction of the bare-expression-
/// inside-gain bug breaks the suite.
/// </summary>
[Collection("FlowScripts")]
public class SectionGainBareExpressionTests
{
    [Fact]
    public void GainNestedInSection_RendersNonZeroFrames()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
section s { gain 0.5 { | C4 D4 E4 F4 | } }
Song sg = [s]
Buffer b = (renderSong sg ""sine"")
Int frames = (getFrames b)
(print $""frames: {(str frames)}"")
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        // Pre-fix: "frames: 0" — silent regression. Post-fix: frames > 0.
        Assert.DoesNotContain("frames: 0\n", stdout);
        Assert.Contains("frames:", stdout);
    }
}
