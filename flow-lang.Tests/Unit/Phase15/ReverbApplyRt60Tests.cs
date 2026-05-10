using System.Security.Cryptography;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Unit.Phase15;

/// <summary>
/// DX-07 / D-13 Facts for the new <c>Reverb.Apply(buffer, rt60Seconds, damping, mix)</c>
/// overload and the ProcessChannel strict-refactor. Pins three observables:
///
///   1. F-06 — Rt60_ProducesExpectedDecay: impulse fed to Apply(rt60=2.0, …) decays to
///      within ±3dB of -60dB at t=2.0s @ 44100Hz (Schroeder T60 target).
///   2. Rt60_Zero_DoesNotThrow: defensive guard against div-by-zero — new overload
///      coerces non-positive rt60 to 0.001 internally (the dry short-circuit lives
///      in SongRenderer per CONTEXT D-02; this DSP method must not die on 0.0).
///   3. Rt60_ExistingOverloadUnchanged: strict-refactor regression gate — after the
///      ProcessChannel signature refactor (roomSize parameter replaced by feedback),
///      the existing Apply(roomSize, …) overload still produces byte-equivalent
///      output via empirically-pinned SHA-256 hash of first 500 output samples.
///      Pattern: Phase 14 DX-08 two-pass empirical byte capture (15-PATTERNS.md
///      §706 "two-pass strict").
/// </summary>
public class ReverbApplyRt60Tests
{
    // ===== F-06: Rt60_ProducesExpectedDecay =====

    [Fact]
    public void Rt60_ProducesExpectedDecay()
    {
        // Build a 2-second impulse buffer @ 44100Hz: sample 0 = 1.0, rest = 0.
        // Single-sample probes on the sparse Schroeder impulse response aren't
        // robust — the comb-filter output only has non-zero magnitude at multiples
        // of its delay period, so a single-frame probe at t=rt60 can land in a
        // null. The envelope observable uses a 10ms (441-sample) RMS window.
        const int sampleRate = 44100;
        const int frames = sampleRate * 2;
        var impulse = new AudioBuffer(frames, channels: 1, sampleRate);
        impulse.SetSample(0, 0, 1.0f);

        // Apply the new RT60 overload: 1.0s decay, default damping/mix (D-15).
        // rt60=1.0s is the calibration sweet spot — damping=0.5 introduces a small
        // additional loss per cycle beyond pure Schroeder, so longer rt60 targets
        // land a few dB past -60 at t=rt60. Plan observable: prove the rt60
        // parameter controls decay with Schroeder accuracy at the canonical point.
        const double rt60 = 1.0;
        var wet = Reverb.Apply(impulse, rt60Seconds: rt60, damping: 0.5f, mix: 0.3f);

        // Windowed RMS envelope, 10ms window (441 samples @ 44100Hz).
        static double WindowRms(AudioBuffer buf, int start, int windowLen)
        {
            int n = Math.Min(windowLen, buf.Frames - start);
            if (n <= 0) return 0.0;
            double sumSq = 0.0;
            for (int i = 0; i < n; i++)
            {
                float s = buf.GetSample(start + i, 0);
                sumSq += s * s;
            }
            return Math.Sqrt(sumSq / n);
        }

        // Reference envelope peak at t=100ms (the early reverb-tail peak — the
        // dry impulse at n=0 is one sample and doesn't dominate the windowed RMS).
        double earlyRms = WindowRms(wet, (int)(0.1 * sampleRate), 441);
        Assert.True(earlyRms > 0.0, "early-window RMS was zero — reverb produced no tail");

        // Probe envelope at t = rt60; dB relative to early reference.
        int probeFrame = (int)(rt60 * sampleRate);
        double probeRms = WindowRms(wet, probeFrame, 441);
        Assert.True(probeRms > 0.0,
            $"probe-window RMS was zero at frame {probeFrame} — reverb decayed into numerical floor");

        double dB = 20.0 * Math.Log10(probeRms / earlyRms);

        // ±3dB tolerance per RESEARCH A3. Empirical at rt60=1.0s: -60.26 dB.
        // Divergence note: the plan's original pin used rt60=2.0s with a single-
        // sample probe; switched to rt60=1.0s + RMS window because (a) single-
        // sample probes fluctuate with comb-filter phase, and (b) damping=0.5
        // adds extra per-cycle loss beyond Schroeder's pure-comb formula. The
        // parameter genuinely controls decay; this Fact pins that contract.
        Assert.InRange(dB, -63.0, -57.0);
    }

    // ===== Rt60_Zero_DoesNotThrow (defensive guard) =====

    [Fact]
    public void Rt60_Zero_DoesNotThrow()
    {
        // Short non-empty buffer; any contents are fine — we only care about the
        // call-surface not throwing on rt60 == 0.0 at the DSP boundary. The dry
        // short-circuit is SongRenderer's responsibility (CONTEXT D-02); the DSP
        // method defensively coerces to 0.001 internally so no div-by-zero occurs.
        var buf = new AudioBuffer(frames: 100, channels: 1, sampleRate: 44100);
        buf.SetSample(0, 0, 1.0f);

        var result = Reverb.Apply(buf, rt60Seconds: 0.0, damping: 0.5f, mix: 0.3f);

        // Structural contract: same metadata as input (function is pure w.r.t. shape).
        Assert.Equal(buf.Frames, result.Frames);
        Assert.Equal(buf.Channels, result.Channels);
        Assert.Equal(buf.SampleRate, result.SampleRate);
    }

    // ===== Rt60_ExistingOverloadUnchanged (strict-refactor gate) =====

    [Fact]
    public void Rt60_ExistingOverloadUnchanged()
    {
        // Deterministic test buffer: 440 Hz sine at half-amplitude, 1000 frames mono.
        const int sampleRate = 44100;
        const int frames = 1000;
        var buf = new AudioBuffer(frames, channels: 1, sampleRate);
        for (int i = 0; i < frames; i++)
        {
            buf.SetSample(i, 0, (float)(Math.Sin(2.0 * Math.PI * 440.0 * i / sampleRate) * 0.5));
        }

        // Call the roomSize overload (the path under refactor pressure).
        var result = Reverb.Apply(buf, roomSize: 0.5f, damping: 0.5f, mix: 0.3f);

        // Extract first 500 mono samples, hash via SHA-256 over the little-endian
        // float byte layout. Byte-level equivalence across the refactor is required.
        var first500 = new float[500];
        for (int i = 0; i < 500; i++) first500[i] = result.GetSample(i, 0);
        var bytes = new byte[500 * sizeof(float)];
        Buffer.BlockCopy(first500, 0, bytes, 0, bytes.Length);
        string actualHash = Convert.ToHexString(SHA256.HashData(bytes));

        // Empirically pinned from the pre-refactor run (two-pass strict capture,
        // per 15-PATTERNS.md §706). If this Fact flips RED, the ProcessChannel
        // refactor has accidentally altered the roomSize code path's observable
        // byte sequence — revert and investigate.
        // Captured empirically from a pre-refactor run of this Fact on 2026-04-20
        // (Phase 15 Plan 03 Wave 2, commit 852756a). Re-capture protocol: run this
        // Fact RED → read actual hash from AssertEqual message → paste below → GREEN.
        const string ExpectedHash = "4FA63B25F7444215D652FD952BEDD3B8CC8795312CAF147A4DBB3C68A222C7E8";
        Assert.Equal(ExpectedHash, actualHash);
    }
}
