using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Shared;
using Xunit;

namespace FlowLang.Tests.Integration.Phase15;

/// <summary>
/// DX-09 / ROADMAP criterion #2: byte-identical determinism across the
/// MIDI + WAV serialization boundary. Two passes through writeMidi /
/// writeWav with the same seed must produce byte-identical files. This is
/// the cross-process correlate of <see cref="EuclideanHumanizeTests.SameSeed_ProducesIdenticalVelocities"/>
/// — that Fact pins in-process determinism on the <see cref="MusicalNoteData.Velocity"/>
/// values; these Facts pin the same property at the file-byte level once the
/// values have travelled through DryWetMidi / WAV encoding.
///
/// Authored under the two-pass strict empirical-capture protocol
/// (Phase 14 CONTEXT D-13). The MIDI Fact pins an EMPIRICAL velocity byte
/// sequence so silent <c>System.Random</c> algorithm drift across .NET patch
/// versions (RESEARCH Pitfall 7) surfaces as a RED Fact rather than landing
/// unobserved.
/// </summary>
[Collection("FlowScripts")]
public class EuclideanByteIdenticalTests
{
    [Fact]
    public void SameSeed_ByteIdenticalMidi()
    {
        using var runner = new FlowEngineRunner();
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string outputDir = Path.Combine(repoRoot, "tests", "output");
        string path1 = Path.Combine(outputDir, "phase15_seed42_run1.mid");
        string path2 = Path.Combine(outputDir, "phase15_seed42_run2.mid");

        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            Directory.CreateDirectory(outputDir);

            // Two isolated engine runs with identical seed. Using two RunSource
            // calls (rather than one script that writes twice) guarantees no
            // residual per-engine state crosses the boundary — the determinism
            // contract under D-17 is "local new Random(seed) per call", and
            // these two calls happen in two separate FlowEngine instances.
            string sourceRun1 = """
                use "@std"
                use "@audio"
                tempo 120 {
                    timesig 4/4 {
                        Sequence g = (euclidean 3 8 C4 0.3 0.1 42)
                        section s { g }
                        Song song = [s]
                        (writeMidi "tests/output/phase15_seed42_run1.mid" song)
                        (print "run1: ok")
                    }
                }
                """;
            string sourceRun2 = sourceRun1.Replace("run1", "run2");

            var (success1, _, stderr1, errorCount1) = runner.RunSource(sourceRun1);
            Assert.True(success1, $"run1 failed: stderr={stderr1}");
            Assert.Equal(0, errorCount1);

            using var runner2 = new FlowEngineRunner();
            var (success2, _, stderr2, errorCount2) = runner2.RunSource(sourceRun2);
            Assert.True(success2, $"run2 failed: stderr={stderr2}");
            Assert.Equal(0, errorCount2);

            Assert.True(File.Exists(path1), $"MIDI not written: {path1}");
            Assert.True(File.Exists(path2), $"MIDI not written: {path2}");

            // Primary gate: cross-file byte-identity.
            // If this fails, euclidean's 6-arg overload is non-deterministic
            // and Plan 04 has a regression — escalate before pinning bytes.
            byte[] bytes1 = File.ReadAllBytes(path1);
            byte[] bytes2 = File.ReadAllBytes(path2);
            Assert.True(bytes1.SequenceEqual(bytes2),
                $"MIDI bytes differ: run1 len={bytes1.Length}, run2 len={bytes2.Length}");

            // Secondary gate: empirically-pinned velocity byte sequence
            // (Phase 14 D-13 two-pass strict empirical capture).
            //
            // Sequence of three hits comes from euclidean(3, 8, C4, swing=0.3,
            // humanize=0.1, seed=42):
            //   * Bjorklund(3, 8) places hits at step indices [0, 3, 6].
            //   * Step 0 is on-beat (multiple of floor(8/3)=2 alignment) → swing
            //     accent of +0.3 over the base 0.63 → ~0.93 → MIDI byte ~118
            //     before humanize jitter. Steps 3, 6 fall off the on-beat grid
            //     under D-06 → unaccented base 0.63 → ~80 before jitter.
            //   * humanize 0.1 with new Random(42) jitters each velocity by
            //     ±0.1 deterministically.
            //
            // Captured on net10.0.107 (runtime Microsoft.NETCore.App 10.0.7,
            // 2026-04-25). If a future .NET patch update changes
            // System.Random(42).NextDouble() these bytes shift and the Fact
            // goes RED — which is the silent-drift gate (RESEARCH Pitfall 7).
            byte[] velocities = MidiReadHelpers.GetVelocityBytes(path1);
            Assert.Equal(
                expected: new byte[] { 122, 70, 108 },
                actual: velocities);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    /// <summary>
    /// ROADMAP criterion #2 second half: byte-identical WAV across two
    /// renders with identical seed. Stronger contract than the MIDI gate
    /// because it exercises the full audio pipeline (renderSong → voice
    /// synthesis → mix → WAV encode). Any nondeterminism in synth math,
    /// voice ordering, or buffer zeroing surfaces here.
    ///
    /// No empirical byte-array pin: WAV files run multi-megabyte and are
    /// impractical to pin in source. SequenceEqual of the full files plus a
    /// length-smoke check is the in-process pin; cross-version audio-layer
    /// drift would surface as a Red Fact on CI without source-level pinning.
    /// </summary>
    [Fact]
    public void SameSeed_ByteIdenticalWav()
    {
        using var runner = new FlowEngineRunner();
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string outputDir = Path.Combine(repoRoot, "tests", "output");
        string path1 = Path.Combine(outputDir, "phase15_seed42_run1.wav");
        string path2 = Path.Combine(outputDir, "phase15_seed42_run2.wav");

        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            Directory.CreateDirectory(outputDir);

            string sourceRun1 = """
                use "@std"
                use "@audio"
                tempo 120 {
                    timesig 4/4 {
                        Sequence g = (euclidean 3 8 C4 0.3 0.1 42)
                        section s { g }
                        Song song = [s]
                        Buffer rendered = (renderSong song "piano")
                        (writeWav "tests/output/phase15_seed42_run1.wav" rendered)
                        (print "run1: ok")
                    }
                }
                """;
            string sourceRun2 = sourceRun1.Replace("run1", "run2");

            var (success1, _, stderr1, errorCount1) = runner.RunSource(sourceRun1);
            Assert.True(success1, $"run1 failed: stderr={stderr1}");
            Assert.Equal(0, errorCount1);

            using var runner2 = new FlowEngineRunner();
            var (success2, _, stderr2, errorCount2) = runner2.RunSource(sourceRun2);
            Assert.True(success2, $"run2 failed: stderr={stderr2}");
            Assert.Equal(0, errorCount2);

            Assert.True(File.Exists(path1), $"WAV not written: {path1}");
            Assert.True(File.Exists(path2), $"WAV not written: {path2}");

            byte[] bytes1 = File.ReadAllBytes(path1);
            byte[] bytes2 = File.ReadAllBytes(path2);

            // Smoke sanity — rendered audio must produce a non-trivial file.
            Assert.True(bytes1.Length > 1000, $"WAV suspiciously small: {bytes1.Length} bytes");

            // Primary gate: ROADMAP #2 byte-identical WAV contract.
            Assert.True(bytes1.SequenceEqual(bytes2),
                $"WAV bytes differ: run1 len={bytes1.Length}, run2 len={bytes2.Length}");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
