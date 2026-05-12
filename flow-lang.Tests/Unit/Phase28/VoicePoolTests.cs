using System;
using System.IO;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Phase 28 (SPEC-7) Plan 05 unit-level acceptance facts pinning the
/// voicePool block end-to-end:
///   • Range check 1..256 with the locked error message
///   • Default 32 applied when no voicePool block is in scope
///   • Boundary values (1, 256) accepted
///
/// Verification path: render a Song through SongRenderer (via the engine's
/// `renderSong` builtin), then read VoiceAllocator.LastPoolSizeUsedForTests
/// (test-only static recorded in AllocateWithPool). The static is reset
/// before each test for isolation.
///
/// VoiceAllocator.LastPoolSizeUsedForTests is backed by AsyncLocal so xUnit's
/// parallel test execution doesn't race the value across classes — each test's
/// logical flow sees only the pool size it triggered.
/// </summary>
public class VoicePoolTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    private static string TempWavPath(string name) =>
        Path.Combine(Path.GetTempPath(), $"flow_phase28_voicepool_{name}_{Guid.NewGuid():N}.wav");

    private static (bool Success, string Stdout, string Stderr, int ErrorCount) RunFlow(string flowSource)
    {
        VoiceAllocator.LastPoolSizeUsedForTests = null; // isolate test
        using var runner = new FlowEngineRunner();
        return runner.RunSource(flowSource);
    }

    [Fact]
    public void VoicePool_ParsesAndApplies()
    {
        // voicePool 16 { ... renderSong } — VoiceAllocator.AllocateWithPool
        // must record poolSize == 16 in LastPoolSizeUsedForTests.
        string outPath = TempWavPath(nameof(VoicePool_ParsesAndApplies));
        string source = Prelude + $@"
voicePool 16 {{
    tempo 120 {{
        timesig 4/4 {{
            Sequence s = | C4q D4q E4q F4q |
            section sec {{ Sequence v = s }}
            Song song = [sec]
            Buffer mix = (renderSong song ""piano"")
            (writeWav ""{outPath.Replace("\\", "/")}"" mix)
        }}
    }}
}}
";
        var (success, _, stderr, errorCount) = RunFlow(source);
        Assert.True(success && errorCount == 0, $"errors={errorCount}\nstderr={stderr}");
        Assert.Equal(16, VoiceAllocator.LastPoolSizeUsedForTests);
        if (File.Exists(outPath)) File.Delete(outPath);
    }

    [Fact]
    public void VoicePool_DefaultsTo32()
    {
        // No voicePool block in scope → AllocateWithPool sees the SPEC-7 locked
        // default of 32 from RenderSequenceToVoicesWithPool.
        string outPath = TempWavPath(nameof(VoicePool_DefaultsTo32));
        string source = Prelude + $@"
tempo 120 {{
    timesig 4/4 {{
        Sequence s = | C4q D4q E4q F4q |
        section sec {{ Sequence v = s }}
        Song song = [sec]
        Buffer mix = (renderSong song ""piano"")
        (writeWav ""{outPath.Replace("\\", "/")}"" mix)
    }}
}}
";
        var (success, _, stderr, errorCount) = RunFlow(source);
        Assert.True(success && errorCount == 0, $"errors={errorCount}\nstderr={stderr}");
        Assert.Equal(32, VoiceAllocator.LastPoolSizeUsedForTests);
        if (File.Exists(outPath)) File.Delete(outPath);
    }

    [Fact]
    public void VoicePool_RejectsOutOfRange_Zero()
    {
        // voicePool 0 → interpreter error with the locked message.
        string source = Prelude + @"
voicePool 0 {
    Int x = 1
}
";
        var (_, _, stderr, errorCount) = RunFlow(source);
        Assert.True(errorCount > 0, "expected at least one error, got none");
        Assert.Contains("Voice pool size must be between 1 and 256", stderr);
    }

    [Fact]
    public void VoicePool_RejectsOutOfRange_TooBig()
    {
        // voicePool 300 → interpreter error with the locked message.
        string source = Prelude + @"
voicePool 300 {
    Int x = 1
}
";
        var (_, _, stderr, errorCount) = RunFlow(source);
        Assert.True(errorCount > 0, "expected at least one error, got none");
        Assert.Contains("Voice pool size must be between 1 and 256", stderr);
    }

    [Fact]
    public void VoicePool_AcceptsBoundary_One()
    {
        string outPath = TempWavPath(nameof(VoicePool_AcceptsBoundary_One));
        string source = Prelude + $@"
voicePool 1 {{
    tempo 120 {{
        timesig 4/4 {{
            Sequence s = | C4q |
            section sec {{ Sequence v = s }}
            Song song = [sec]
            Buffer mix = (renderSong song ""piano"")
            (writeWav ""{outPath.Replace("\\", "/")}"" mix)
        }}
    }}
}}
";
        var (success, _, stderr, errorCount) = RunFlow(source);
        Assert.True(success && errorCount == 0, $"errors={errorCount}\nstderr={stderr}");
        Assert.Equal(1, VoiceAllocator.LastPoolSizeUsedForTests);
        if (File.Exists(outPath)) File.Delete(outPath);
    }

    [Fact]
    public void VoicePool_AcceptsBoundary_TwoFiftySix()
    {
        string outPath = TempWavPath(nameof(VoicePool_AcceptsBoundary_TwoFiftySix));
        string source = Prelude + $@"
voicePool 256 {{
    tempo 120 {{
        timesig 4/4 {{
            Sequence s = | C4q D4q E4q F4q |
            section sec {{ Sequence v = s }}
            Song song = [sec]
            Buffer mix = (renderSong song ""piano"")
            (writeWav ""{outPath.Replace("\\", "/")}"" mix)
        }}
    }}
}}
";
        var (success, _, stderr, errorCount) = RunFlow(source);
        Assert.True(success && errorCount == 0, $"errors={errorCount}\nstderr={stderr}");
        Assert.Equal(256, VoiceAllocator.LastPoolSizeUsedForTests);
        if (File.Exists(outPath)) File.Delete(outPath);
    }
}
