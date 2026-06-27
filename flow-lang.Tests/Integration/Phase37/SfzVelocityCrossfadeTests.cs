using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Sfz;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-02 (parser side) — `xfin_lovel`/`xfin_hivel`/`xfout_lovel`/
/// `xfout_hivel` opcodes parsed from a Phase 37 SFZ fixture. Renderer-side
/// equal-power velocity crossfade lives downstream (Task 3 acceptance via
/// the audible renders that exercise SfzRenderer.Render).
/// </summary>
[Collection("FlowScripts")]
public class SfzVelocityCrossfadeTests : IDisposable
{
    public SfzVelocityCrossfadeTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Parser_ReadsXfinXfoutOpcodes_FromFixture()
    {
        var path = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures",
            "Phase37", "velocity_xfade.sfz");
        Assert.True(File.Exists(path), $"Phase37 velocity_xfade fixture missing: {path}");
        var content = File.ReadAllText(path);
        var data = SfzParser.Parse(content, path, "velocity_xfade");

        Assert.Equal(2, data.Regions.Count);

        // Layer 1 (the lower-velocity layer) declares xfout only.
        var layer1 = data.Regions.First(r => r.HiVel == 80);
        Assert.Equal(60, layer1.XfoutLoVel);
        Assert.Equal(80, layer1.XfoutHiVel);
        Assert.Equal(-1, layer1.XfinLoVel);
        Assert.Equal(-1, layer1.XfinHiVel);

        // Layer 2 (the upper-velocity layer) declares xfin only.
        var layer2 = data.Regions.First(r => r.LoVel == 60);
        Assert.Equal(60, layer2.XfinLoVel);
        Assert.Equal(80, layer2.XfinHiVel);
        Assert.Equal(-1, layer2.XfoutLoVel);
        Assert.Equal(-1, layer2.XfoutHiVel);
    }
}
