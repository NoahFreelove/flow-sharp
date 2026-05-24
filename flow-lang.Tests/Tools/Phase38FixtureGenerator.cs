using System;
using System.IO;
using Xunit;

namespace FlowLang.Tests.Tools;

/// <summary>
/// Phase 38 Plan 38-05 Task 1 — one-shot regenerator for the audio-input smoke
/// fixture committed at <c>flow-lang.Tests/Integration/Phase38/TestFixtures/mic_fixture.wav</c>.
///
/// Synthesises a 1-second 440 Hz sine wave at 48 000 Hz / 16-bit / mono PCM —
/// the canonical "tuning fork" tone with a deliberately NON-44.1 kHz native
/// rate so <c>MicBufferResampleTests</c> can exercise the linear-interp
/// resample path end-to-end without a real microphone (38-VALIDATION.md
/// line 80 "synthetic capture-path fixture").
///
/// <para>
/// Determinism contract: two consecutive invocations produce byte-identical
/// output. The sine-burst formula is fully deterministic — no RNG, no
/// timestamp, no platform-dependent <c>Math.Sin</c> chain. Same-platform
/// two-run cmp-clean preserved per CLAUDE.md "Phase 36 chaos primitives —
/// same-platform determinism only" convention (this fixture stays
/// cross-platform deterministic because it's a single <c>Math.Sin</c> per
/// sample, not chained).
/// </para>
///
/// Invoke explicitly:
///   <c>dotnet test --filter "Phase38FixtureGenerator_MicFixture_GeneratesFixture"</c>
///
/// Sample formula (mirrors <c>Phase33FixtureGenerator</c>):
///   <c>short s = (short)(Math.Sin(2.0 * Math.PI * 440 * i / 48000.0) * 0.5 * short.MaxValue)</c>
/// — amplitude 0.5 leaves headroom for the downstream -20 dB attenuation path
/// in <c>InputFunctions.MicBuffer</c>; no DC offset; no high-frequency ringing.
///
/// File size: 1 s × 48 000 Hz × 2 bytes/sample = 96 KB data + 44 B RIFF header =
/// ~96 044 B (above the 50 KB threshold required by Task 1 done-criterion).
/// </summary>
public static class Phase38FixtureGenerator
{
    public const int SampleRate = 48_000;
    public const int Channels = 1;
    public const int BitsPerSample = 16;
    public const double Frequency = 440.0;
    public const double DurationSec = 1.0;
    public const double Amplitude = 0.5;

    private static string FixturePath
    {
        get
        {
            // The test binary runs from a `bin/<Configuration>/<tfm>/` subtree of
            // flow-lang.Tests/; walk up to the project root and reach the fixture
            // location. We commit the fixture into the project tree so other
            // tests can <None Update> it for CopyToOutputDirectory.
            var dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Integration", "Phase38", "TestFixtures")))
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            if (dir == null)
                throw new DirectoryNotFoundException(
                    "Could not locate flow-lang.Tests project root containing Integration/Phase38/TestFixtures/");
            return Path.Combine(dir, "Integration", "Phase38", "TestFixtures", "mic_fixture.wav");
        }
    }

    /// <summary>
    /// Synthesises and writes mic_fixture.wav. Idempotent — re-running produces
    /// byte-identical output. Marked as a regular <see cref="Fact"/> so it runs
    /// as part of the normal test suite and self-heals if the fixture is ever
    /// deleted / corrupted.
    /// </summary>
    [Fact]
    public static void Phase38FixtureGenerator_MicFixture_GeneratesFixture()
    {
        var path = FixturePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        int frameCount = (int)(DurationSec * SampleRate);
        var pcmSamples = new short[frameCount * Channels];
        for (int i = 0; i < frameCount; i++)
        {
            double sample = Math.Sin(2.0 * Math.PI * Frequency * i / SampleRate) * Amplitude;
            pcmSamples[i] = (short)(sample * short.MaxValue);
        }

        WriteWavPcm16(path, pcmSamples, SampleRate, Channels);

        // Self-check — readable + plausibly sized
        var info = new FileInfo(path);
        Assert.True(info.Exists, $"Fixture not written to {path}");
        Assert.True(info.Length > 50_000, $"Fixture suspiciously small: {info.Length} bytes");
    }

    /// <summary>
    /// Minimal RIFF/WAVE/fmt /data writer for 16-bit PCM. Mirrors the shape of
    /// <c>flow-lang/StandardLibrary/Audio/FileIO.cs:WriteWav</c> but stripped to
    /// the bare essentials needed for a deterministic fixture (no dither, no
    /// per-channel deinterleave, no bit-depth dispatch).
    /// </summary>
    private static void WriteWavPcm16(string path, short[] samples, int sampleRate, int channels)
    {
        int dataSize = samples.Length * sizeof(short);
        int byteRate = sampleRate * channels * sizeof(short);
        short blockAlign = (short)(channels * sizeof(short));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(36 + dataSize); // file size - 8
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

        // fmt chunk
        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16);                    // chunk size
        writer.Write((short)1);              // format code = PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)16);             // bits per sample

        // data chunk
        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(dataSize);
        foreach (var s in samples)
            writer.Write(s);
    }
}
