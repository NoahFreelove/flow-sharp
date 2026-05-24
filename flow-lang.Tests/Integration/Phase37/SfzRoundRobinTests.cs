using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Sfz;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-01 (parser side) — `seq_position`/`seq_length` opcodes parsed
/// from a Phase 37 SFZ fixture. Renderer-side round-robin selection lives in
/// <see cref="SfzRoundRobinDeterminismTests"/>. Excessive `seq_length` clamps
/// to spec max 100 per RESEARCH §Pitfall 1 with a one-shot WarnOnce advisory.
/// </summary>
[Collection("FlowScripts")]
public class SfzRoundRobinTests : IDisposable
{
    public SfzRoundRobinTests()
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
    public void Parser_ReadsSeqPositionSeqLength_FromFixture()
    {
        var path = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures",
            "Phase37", "round_robin.sfz");
        Assert.True(File.Exists(path), $"Phase37 round-robin fixture missing: {path}");
        var content = File.ReadAllText(path);
        var data = SfzParser.Parse(content, path, "round_robin");

        Assert.Equal(2, data.Regions.Count);
        Assert.Equal(2, data.Regions[0].SeqLength);
        Assert.Equal(2, data.Regions[1].SeqLength);
        // One region claims position 1, the other position 2.
        Assert.Contains(data.Regions, r => r.SeqPosition == 1);
        Assert.Contains(data.Regions, r => r.SeqPosition == 2);
    }

    [Fact]
    public void Parser_ClampsExcessiveSeqLength_WithWarnOnce()
    {
        // Capture stderr to observe the WarnOnce advisory.
        var origErr = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            string sfz = @"<region>
sample=stub.wav
lokey=60 hikey=60 lovel=1 hivel=127
seq_position=1 seq_length=999999
";
            var data = SfzParser.Parse(sfz, "/tmp/inline.sfz", "inline_excessive_seqlen");
            Assert.Single(data.Regions);
            // Spec max 100 per Pitfall 1 — clamped.
            Assert.Equal(100, data.Regions[0].SeqLength);
        }
        finally
        {
            Console.SetError(origErr);
        }

        var stderr = capture.ToString();
        Assert.Contains("seq_length=999999 exceeds spec max 100", stderr);
    }
}
