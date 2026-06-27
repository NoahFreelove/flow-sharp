using System;
using System.Threading;
using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Midi;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 Plan 40-02 CLOCK-02 — MIDI clock SLAVE: derive BPM from an incoming
/// 0xF8 stream, apply an 8-pulse settle, and drive
/// <see cref="MusicalContext.Tempo"/>. Plus the master ⊕ slave bar-boundary
/// switch contract and charitable bind-failure handling.
///
/// <para>No real ALSA needed: the slave subscribes to an injected
/// <see cref="SlaveByteSource"/> (the <c>MidiClock.SlaveSourceOverride</c> test
/// seam, modeling OSC's <c>DispatchPacketForTesting</c>). Each Fact resets the
/// override to null in a finally block so the seam never leaks across tests.</para>
///
/// <para><b>Serialized via <see cref="WasmEntryConsoleCollection"/> (Plan 40-04
/// Rule 1):</b> these Facts derive BPM from real-time Stopwatch inter-pulse deltas,
/// so CPU contention from the parallel real-hardware <c>RealMidiLoopbackTests</c>
/// (which spawns <c>amidi</c> + busy-polls) jittered the deltas under full-suite
/// load. Sharing the serial collection keeps the timing-sensitive clock tests from
/// running alongside the CPU-heavy loopback class.</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class ClockSlaveTests
{
    /// <summary>Emit <paramref name="count"/> 0xF8 pulses spaced
    /// <paramref name="intervalMs"/> apart via real Stopwatch-spaced sleeps, so the
    /// slave's inter-pulse deltas reflect the intended rate.</summary>
    private static void EmitSteadyPulses(SlaveByteSource src, int count, int intervalMs)
    {
        for (int i = 0; i < count; i++)
        {
            src.EmitClockPulse();
            if (i < count - 1) Thread.Sleep(intervalMs);
        }
    }

    [Fact]
    public void ClockSlaveDrivesTempo()
    {
        var src = new SlaveByteSource();
        MidiClock.SlaveSourceOverride = src;
        try
        {
            var ctx = new MusicalContext();
            var clock = MidiClock.StartSlave(ctx, "virtual");

            // Target 125 BPM → pulse interval = 60000 / (125 * 24) = 20 ms.
            // Emit 12 pulses at 20 ms so the 8-delta settle window fills (needs ≥9).
            const int intervalMs = 20;
            EmitSteadyPulses(src, 12, intervalMs);

            clock.Stop();

            double expectedBpm = 60000.0 / (intervalMs * MidiClock.PulsesPerQuarter); // 125
            // WR-01 (LINK-02): the slave drives the LIVE-SYNC tempo sink, NOT
            // ctx.Tempo (which would leak into offline render). Read it back via
            // TryGetLiveTempo. ctx.Tempo MUST stay untouched (null) so the render
            // path is unaffected.
            Assert.Null(ctx.Tempo);
            Assert.True(ctx.TryGetLiveTempo(out double liveBpm),
                "slave should have driven the live-sync tempo sink");
            // Sleep granularity on a CI box jitters the real deltas; tolerate ±20%.
            Assert.InRange(liveBpm, expectedBpm * 0.80, expectedBpm * 1.20);
        }
        finally
        {
            MidiClock.SlaveSourceOverride = null;
        }
    }

    [Fact]
    public void EightPulseSettle_SmoothsSingleJitteryPulse()
    {
        var src = new SlaveByteSource();
        MidiClock.SlaveSourceOverride = src;
        try
        {
            var ctx = new MusicalContext();
            var clock = MidiClock.StartSlave(ctx, "virtual");

            // Fill the settle window with a steady 20 ms rate (125 BPM)...
            // WR-01: read the live-sync sink, not ctx.Tempo.
            const int steadyMs = 20;
            EmitSteadyPulses(src, 10, steadyMs);
            ctx.TryGetLiveTempo(out double settledBpm);
            Assert.True(settledBpm > 0, "tempo should be set after the settle window fills");

            // ...then inject ONE jittery pulse (a long 80 ms gap = a momentary
            // tempo dip to ~31 BPM if taken raw). The 8-pulse average must absorb
            // it: the written tempo must NOT lurch to the jittery value.
            Thread.Sleep(80);
            src.EmitClockPulse();
            ctx.TryGetLiveTempo(out double afterJitterBpm);

            clock.Stop();

            // A single 80 ms delta among 7 × 20 ms deltas averages to ≈ 28.6 ms →
            // ≈ 87 BPM, NOT the raw 31 BPM the jittery pulse alone implies. The key
            // CLOCK-02 contract: the tempo does not collapse to the outlier's value.
            double rawJitterBpm = 60000.0 / (80.0 * MidiClock.PulsesPerQuarter); // ~31
            Assert.True(afterJitterBpm > rawJitterBpm * 1.5,
                $"8-pulse settle failed: tempo {afterJitterBpm:F1} lurched toward the jittery {rawJitterBpm:F1} BPM");
        }
        finally
        {
            MidiClock.SlaveSourceOverride = null;
        }
    }

    [Fact]
    public void ModeSwitch_HonoredOnlyAtBarBoundary()
    {
        var src = new SlaveByteSource();
        MidiClock.SlaveSourceOverride = src;
        try
        {
            // 4/4 → pulsesPerBar = 24 × 4 = 96. A switch lands only when the pulse
            // count is an exact multiple of 96.
            var ctx = new MusicalContext
            {
                TimeSignature = new TimeSignatureData(4, 4),
            };
            var clock = MidiClock.StartSlave(ctx, "virtual");

            // At pulse 0 (no pulses yet) we ARE at a bar boundary → switch applies.
            Assert.True(clock.AtBarBoundary());
            Assert.True(clock.RequestModeSwitch(ClockMode.Master));
            Assert.Equal(ClockMode.Master, clock.Mode);

            // Advance a few pulses into the bar (not a multiple of 96) — a switch
            // request must be DEFERRED (returns false; mode unchanged).
            for (int i = 0; i < 5; i++) src.EmitClockPulse();
            Assert.False(clock.AtBarBoundary());
            Assert.False(clock.RequestModeSwitch(ClockMode.Slave));
            Assert.Equal(ClockMode.Master, clock.Mode); // deferred — still Master

            // Advance to the next bar boundary (total 96 pulses) — switch applies.
            for (int i = 5; i < 96; i++) src.EmitClockPulse();
            Assert.True(clock.AtBarBoundary());
            Assert.True(clock.RequestModeSwitch(ClockMode.Slave));
            Assert.Equal(ClockMode.Slave, clock.Mode);

            clock.Stop();
        }
        finally
        {
            MidiClock.SlaveSourceOverride = null;
        }
    }

    [Fact]
    public void SlaveBindFailure_DeadHandle_NoThrow()
    {
        // No override set → the slave takes the real-hardware path. On this
        // lib-absent dev box the RtMidi input bind fails charitably: StartSlave
        // returns a live (dead) handle, never throws, and Stop is a clean no-op.
        MidiClock.SlaveSourceOverride = null;
        var ctx = new MusicalContext();

        var ex = Record.Exception(() =>
        {
            var clock = MidiClock.StartSlave(ctx, "no-such-port-xyzzy");
            // Give the bind task a moment to run + fail charitably.
            Thread.Sleep(100);
            clock.Stop();
        });

        Assert.Null(ex); // charitable: absent port → dead handle, no throw (T-40-04)
        // Tempo was never driven (no pulses arrived) — stays null/unchanged.
        Assert.Null(ctx.Tempo);
    }
}
