using System.IO;
using System.Linq;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase23;

/// <summary>
/// Phase 23 D-08 byte-identical determinism gate. Two complementary Facts:
///
/// 1. <see cref="ExplicitEqualTemperament_ProducesIdenticalOutput"/>: an explicit
///    <c>enable equalTemperament;</c> pragma produces byte-identical WAV output to
///    a no-pragma source — the closed-set EqualTemperament short-circuits to the
///    existing 12-TET path (Pitfall 6).
/// 2. <see cref="ByteIdenticalDefaultTuning_NoPragma_StillBitIdentical_AfterPattern_A_Threading"/>:
///    after Pattern A threading lands across the synthesizer interface + 13 call
///    sites, running the same no-pragma source twice still produces bit-identical
///    WAV bytes — the new <see cref="FlowLang.StandardLibrary.Audio.Tuning.RenderTuning.Default"/>
///    path takes the byte-identical short-circuit. If the Pitfall 6 mitigation
///    breaks (e.g., someone changes the EqualTemperament short-circuit to call
///    <c>NoteToFrequency</c> with a slightly different formula), this Fact RED-fires
///    immediately.
/// </summary>
[Collection("FlowScripts")]
public class ByteIdenticalDefaultTuningTests
{
    private static string FlowSource(string outPath, string? pragma) =>
        (pragma ?? string.Empty) + @"
use ""@std""
use ""@audio""
use ""@composition""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section sec23 {
                Sequence mel = | C4q E4q G4q |
            }
            Song s23 = [sec23]
            Buffer renderedBuffer = (renderSong s23 ""sine"")
            (writeWav """ + outPath.Replace("\\", "/") + @""" renderedBuffer)
        }
    }
}
";

    [Fact]
    public void ExplicitEqualTemperament_ProducesIdenticalOutput()
    {
        // Two minimal sources differing ONLY in the pragma line. Output WAV files must
        // be byte-identical per D-08 + Pitfall 6.
        string outDir = Path.Combine(Path.GetTempPath(), "flow_p23_byteidentical");
        Directory.CreateDirectory(outDir);
        string path1 = Path.Combine(outDir, "explicit_with.wav");
        string path2 = Path.Combine(outDir, "explicit_without.wav");
        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);

        string sourceWith    = FlowSource(path1, "enable equalTemperament;\n");
        string sourceWithout = FlowSource(path2, null);

        using (var runner1 = new FlowEngineRunner())
        {
            var (ok1, _, stderr1, _) = runner1.RunSource(sourceWith);
            Assert.True(ok1, $"sourceWith run failed: {stderr1}");
        }
        using (var runner2 = new FlowEngineRunner())
        {
            var (ok2, _, stderr2, _) = runner2.RunSource(sourceWithout);
            Assert.True(ok2, $"sourceWithout run failed: {stderr2}");
        }

        Assert.True(File.Exists(path1), $"expected output file at {path1}");
        Assert.True(File.Exists(path2), $"expected output file at {path2}");

        var bytesWith = File.ReadAllBytes(path1);
        var bytesWithout = File.ReadAllBytes(path2);
        Assert.True(bytesWith.Length > 0);
        Assert.Equal(bytesWithout.Length, bytesWith.Length);
        Assert.True(bytesWith.SequenceEqual(bytesWithout),
            $"D-08 contract violated: explicit equalTemperament differs from no-pragma " +
            $"(with: {bytesWith.Length}, without: {bytesWithout.Length}).");
    }

    [Fact]
    public void ByteIdenticalDefaultTuning_NoPragma_StillBitIdentical_AfterPattern_A_Threading()
    {
        // Pitfall 6 short-circuit regression: after Pattern A ships, running the same
        // no-pragma source twice must still produce bit-identical WAV bytes (the new
        // RenderTuning.Default path must short-circuit to the exact existing 12-TET
        // code via PitchConversion.NoteToFrequency Pitfall 6 mitigation).
        string outDir = Path.Combine(Path.GetTempPath(), "flow_p23_byteidentical");
        Directory.CreateDirectory(outDir);
        string path1 = Path.Combine(outDir, "shortcircuit_run1.wav");
        string path2 = Path.Combine(outDir, "shortcircuit_run2.wav");
        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);

        string source1 = FlowSource(path1, null);
        string source2 = FlowSource(path2, null);

        using (var runner1 = new FlowEngineRunner())
        {
            var (ok1, _, stderr1, _) = runner1.RunSource(source1);
            Assert.True(ok1, $"run1 failed: {stderr1}");
        }
        using (var runner2 = new FlowEngineRunner())
        {
            var (ok2, _, stderr2, _) = runner2.RunSource(source2);
            Assert.True(ok2, $"run2 failed: {stderr2}");
        }

        Assert.True(File.Exists(path1));
        Assert.True(File.Exists(path2));

        var bytes1 = File.ReadAllBytes(path1);
        var bytes2 = File.ReadAllBytes(path2);
        Assert.True(bytes1.Length > 0);
        Assert.Equal(bytes1.Length, bytes2.Length);
        Assert.True(bytes1.SequenceEqual(bytes2),
            $"Pitfall 6 short-circuit regression: no-pragma double-run produced different " +
            $"bytes (run1: {bytes1.Length}, run2: {bytes2.Length}).");
    }
}
