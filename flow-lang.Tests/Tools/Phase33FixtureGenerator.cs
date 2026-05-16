using System;
using System.IO;
using Xunit;

namespace FlowLang.Tests.Tools;

/// <summary>
/// Phase 33 Plan 01 Task 2 — one-shot regenerator for the SFZ smoke fixture
/// committed at <c>flow-lang.Tests/fixtures/sfz-smoke/</c>.
///
/// Synthesises two 100 ms / 44.1 kHz / 16-bit mono PCM sine bursts:
///   <list type="bullet">
///     <item>C4_sine.wav — 261.6256 Hz (equal-tempered MIDI 60, A=440)</item>
///     <item>G5_sine.wav — 783.9909 Hz (equal-tempered MIDI 79, A=440)</item>
///   </list>
/// and writes the smoke.sfz patch verbatim from 33-RESEARCH.md §"Code Examples"
/// Example 4 — a 2-region fixture exercising the loop_continuous crossfade path
/// (region 1: MIDI 48..71 / pitch_keycenter=60 / loop_start=2205 /
/// loop_end=4410) and the nearest-pitch fallback (region 2: MIDI 72..127 /
/// pitch_keycenter=79 / loop_mode=no_loop).
///
/// Determinism contract: two consecutive invocations produce byte-identical
/// output. The sine-burst formula is fully deterministic — no RNG, no
/// floating-point seed, no time-dependent sample. This satisfies the Phase
/// 18/25/27 two-run-cmp-clean contract for the Phase 33 fixture lineage.
///
/// Total fixture-directory size: ~18 KB (well under SPEC-7's 100 KB cap
/// enforced by <c>Phase33.RepoSizeTests</c>).
///
/// Invoke explicitly:
///   <c>dotnet test --filter "Phase33FixtureGenerator_Smoke_GeneratesFixtures"</c>
///
/// Sample formula (per 33-RESEARCH.md Example 4 + 33-01-PLAN.md interfaces):
///   <c>short s = (short)(Math.Sin(2.0 * Math.PI * freq * i / 44100.0) * 0.5 * short.MaxValue)</c>
/// — amplitude 0.5 leaves Phase 28 envelope headroom; no DC offset; no
/// high-frequency ringing.
/// </summary>
public static class Phase33FixtureGenerator
{
    /// <summary>Sample rate in Hz (matches Flow's audio pipeline default).</summary>
    public const int SampleRate = 44100;

    /// <summary>Burst duration: 100 ms = 4410 frames.</summary>
    public const int FrameCount = 4410;

    /// <summary>C4 frequency (equal-tempered, A=440).</summary>
    public const double C4Hz = 261.6256;

    /// <summary>G5 frequency (equal-tempered, A=440).</summary>
    public const double G5Hz = 783.9909;

    /// <summary>Generates the C4 sine burst WAV at the given output path. Idempotent.</summary>
    public static void GenerateC4Sine(string outPath) => WriteSineBurstWav(outPath, C4Hz);

    /// <summary>Generates the G5 sine burst WAV at the given output path. Idempotent.</summary>
    public static void GenerateG5Sine(string outPath) => WriteSineBurstWav(outPath, G5Hz);

    /// <summary>
    /// Writes a 4410-frame 16-bit mono 44.1 kHz sine burst at <paramref name="freq"/> Hz
    /// to <paramref name="outPath"/>. Uses a manually-laid-out 44-byte RIFF/WAVE header
    /// followed by 8820 bytes of PCM payload. Layout matches FileIO.cs's WAV-write
    /// path (RIFF + WAVE + fmt subchunk + data subchunk).
    /// </summary>
    private static void WriteSineBurstWav(string outPath, double freq)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        const int bitsPerSample = 16;
        const int channels = 1;
        const int bytesPerSample = bitsPerSample / 8;
        const int byteRate = SampleRate * channels * bytesPerSample;
        const int blockAlign = channels * bytesPerSample;
        const int dataSize = FrameCount * channels * bytesPerSample; // 8820 bytes
        const int fileSize = 36 + dataSize; // 8 (RIFF) + 4 (WAVE) + 24 (fmt) + 8 (data hdr) + payload

        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // RIFF header (12 bytes)
        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(fileSize);
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

        // fmt subchunk (24 bytes total: 8 header + 16 body)
        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16);                  // subchunk size for PCM
        writer.Write((short)1);            // PCM format code
        writer.Write((short)channels);     // mono
        writer.Write(SampleRate);          // 44100
        writer.Write(byteRate);            // 88200
        writer.Write((short)blockAlign);   // 2
        writer.Write((short)bitsPerSample); // 16

        // data subchunk header (8 bytes)
        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(dataSize);

        // data subchunk payload — 4410 short samples
        // Amplitude 0.5 leaves Phase 28 envelope headroom + avoids clipping.
        for (int i = 0; i < FrameCount; i++)
        {
            double phase = 2.0 * Math.PI * freq * i / SampleRate;
            short sample = (short)(Math.Sin(phase) * 0.5 * short.MaxValue);
            writer.Write(sample);
        }
    }

    /// <summary>
    /// Verbatim smoke.sfz body from 33-RESEARCH.md §"Code Examples" Example 4.
    /// 2-region SFZ exercising the <global> / <group> / <region> header inheritance,
    /// all 13 known opcodes (across the union of both regions plus inherited
    /// globals), and the loop_continuous + no_loop branches that Plan 33-06's
    /// crossfade test consumes.
    ///
    /// Newline policy: \n only (Unix). The smoke fixture lives in the test
    /// project's fixtures directory which is untouched by .gitattributes
    /// CRLF rewriting in this repo.
    /// </summary>
    public const string SmokeSfzContent =
        "// flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz — synthetic test fixture\n" +
        "// SPEC-7: self-contained, < 100 KB, no external dependencies.\n" +
        "// Regenerate via: dotnet test --filter Phase33FixtureGenerator_Smoke_GeneratesFixtures\n" +
        "\n" +
        "<global>\n" +
        "ampeg_attack=0.005\n" +
        "ampeg_release=0.05\n" +
        "\n" +
        "<group>\n" +
        "volume=0\n" +
        "pan=0\n" +
        "\n" +
        "<region>\n" +
        "sample=C4_sine.wav\n" +
        "pitch_keycenter=60\n" +
        "lokey=48\n" +
        "hikey=71\n" +
        "lovel=1\n" +
        "hivel=127\n" +
        "loop_mode=loop_continuous\n" +
        "loop_start=2205\n" +
        "loop_end=4410\n" +
        "\n" +
        "<region>\n" +
        "sample=G5_sine.wav\n" +
        "pitch_keycenter=79\n" +
        "lokey=72\n" +
        "hikey=127\n" +
        "lovel=1\n" +
        "hivel=127\n" +
        "loop_mode=no_loop\n";

    /// <summary>
    /// Locates the repository root by walking up from the test binary directory
    /// looking for the <c>flow-lang.Tests/fixtures</c> directory. Mirrors the
    /// pattern from <c>flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs</c>.
    /// </summary>
    public static string FindRepoRoot()
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
}

/// <summary>
/// xUnit fact wrapper for <see cref="Phase33FixtureGenerator"/>. Lives in a
/// sibling class so the helper class itself has no test members — keeps
/// xUnit1013 quiet without per-method suppressions.
/// </summary>
public class Phase33FixtureGeneratorFacts
{
    /// <summary>
    /// One-shot fact: regenerates the SFZ smoke fixture in
    /// <c>flow-lang.Tests/fixtures/sfz-smoke/</c>. Idempotent — safe to re-run.
    /// Asserts each WAV file is between 8 KB and 20 KB; asserts the .sfz body
    /// contains the loop_continuous opcode and both sample= references.
    /// </summary>
    [Fact]
    public void Phase33FixtureGenerator_Smoke_GeneratesFixtures()
    {
        var repoRoot = Phase33FixtureGenerator.FindRepoRoot();
        var fixtureDir = Path.Combine(repoRoot, "flow-lang.Tests", "fixtures", "sfz-smoke");
        Directory.CreateDirectory(fixtureDir);

        var c4Path = Path.Combine(fixtureDir, "C4_sine.wav");
        var g5Path = Path.Combine(fixtureDir, "G5_sine.wav");
        var sfzPath = Path.Combine(fixtureDir, "smoke.sfz");

        Phase33FixtureGenerator.GenerateC4Sine(c4Path);
        Phase33FixtureGenerator.GenerateG5Sine(g5Path);
        File.WriteAllText(sfzPath, Phase33FixtureGenerator.SmokeSfzContent);

        var c4Size = new FileInfo(c4Path).Length;
        var g5Size = new FileInfo(g5Path).Length;

        Assert.InRange(c4Size, 8 * 1024, 20 * 1024);
        Assert.InRange(g5Size, 8 * 1024, 20 * 1024);

        var sfzBody = File.ReadAllText(sfzPath);
        Assert.Contains("loop_continuous", sfzBody);
        Assert.Contains("sample=C4_sine.wav", sfzBody);
        Assert.Contains("sample=G5_sine.wav", sfzBody);
    }
}
