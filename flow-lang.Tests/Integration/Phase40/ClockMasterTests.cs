using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Midi;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 Plan 40-02 CLOCK-01 — MIDI clock MASTER rate + transport + bar-boundary
/// tempo application. Drives <see cref="MidiClock"/> against an in-process
/// timestamping output handle (no real ALSA / <c>librtmidi.so</c>): asserts the
/// master emits exactly 24 0xF8 pulses per quarter at the active tempo within
/// tolerance, 0xFA on start + 0xFC on stop, and that a mid-bar
/// <see cref="MusicalContext.Tempo"/> change is deferred to the next bar boundary.
///
/// <para>The byte-capture portion needs NO native lib (the Plan-01
/// <see cref="CaptureMidiBackend"/> seam idea, extended with Stopwatch timestamps
/// for the rate assertion). The real end-to-end RtMidi path is HUMAN-UAT
/// (D-40-07) and charitable-skips when the lib is absent — exercised nowhere here.</para>
///
/// <para><b>Serialized via <see cref="WasmEntryConsoleCollection"/> (Plan 40-04
/// Rule 1):</b> the rate assertions measure real-time Stopwatch inter-pulse spacing,
/// so CPU contention from the parallel real-hardware <c>RealMidiLoopbackTests</c>
/// jittered the deltas under full-suite load. Sharing the serial collection isolates
/// the timing-sensitive clock tests from the CPU-heavy loopback class.</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class ClockMasterTests
{
    /// <summary>An <see cref="IMidiOutputHandle"/> that records each sent raw byte
    /// together with a Stopwatch timestamp, so the test can assert both the byte
    /// stream and the inter-pulse timing. Note/CC/program are unused by the clock.</summary>
    private sealed class TimestampingHandle : IMidiOutputHandle
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        public readonly List<(byte status, double atMs)> Raw = new();
        private readonly object _lock = new();

        public void SendRaw(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            lock (_lock) { Raw.Add((bytes[0], _watch.Elapsed.TotalMilliseconds)); }
        }

        public void SendNoteOn(int channel, int pitch, int velocity) { }
        public void SendNoteOff(int channel, int pitch) { }
        public void SendControlChange(int channel, int controller, int value) { }
        public void SendProgramChange(int channel, int program) { }
        public void SendSysex(byte[] data) { }
        public void Close() { }
        public void Dispose() { }

        public List<(byte status, double atMs)> Snapshot()
        {
            lock (_lock) { return new List<(byte, double)>(Raw); }
        }
    }

    [Fact]
    public void ClockMaster24PpqnRate()
    {
        const double bpm = 120.0;
        var handle = new TimestampingHandle();
        var ctx = new MusicalContext { Tempo = bpm };

        var clock = MidiClock.StartMaster(ctx, handle);

        // At 120 BPM, 24 PPQN → a pulse every ~20.833 ms. Run ~4 quarters ≈ 2s,
        // expecting ~96 pulses. Give it a generous wall-clock window.
        double pulseIntervalMs = 60000.0 / (bpm * MidiClock.PulsesPerQuarter);
        int targetQuarters = 4;
        double runMs = pulseIntervalMs * MidiClock.PulsesPerQuarter * targetQuarters;
        Thread.Sleep((int)runMs + 300);

        clock.Stop();

        var sent = handle.Snapshot();

        // ----- Transport: 0xFA first, 0xFC last -----
        Assert.NotEmpty(sent);
        Assert.Equal(MidiClock.StartByte, sent.First().status);
        Assert.Equal(MidiClock.StopByte, sent.Last().status);

        // ----- Pulse count == 24 pulses per quarter for the elapsed window -----
        // The clock runs until Stop(); the exact number of pulses depends on the
        // wall-clock window, not a fixed target. Verify the count matches
        // 24-PPQN at the active tempo for the OBSERVED run span (first→last pulse):
        // count ≈ span / pulseInterval + 1, within a small scheduler margin. This
        // is the true "24 pulses per quarter at the active tempo" assertion (CLOCK-01).
        var pulseTimes = sent.Where(e => e.status == MidiClock.ClockPulse).Select(e => e.atMs).ToList();
        Assert.True(pulseTimes.Count >= 24, $"expected ≥24 pulses, got {pulseTimes.Count}");
        double spanMs = pulseTimes.Last() - pulseTimes.First();
        double expectedCount = spanMs / pulseIntervalMs + 1; // inclusive of both endpoints
        Assert.InRange(pulseTimes.Count, expectedCount - 4, expectedCount + 4);
        // Sanity: the count corresponds to ~24 pulses per quarter (≥ 3 quarters ran).
        Assert.True(pulseTimes.Count >= 24 * 3, $"expected ≥3 quarters of pulses, got {pulseTimes.Count}");

        // ----- Inter-pulse mean ≈ 60/BPM/24 within tolerance -----
        var deltas = new List<double>();
        for (int i = 1; i < pulseTimes.Count; i++) deltas.Add(pulseTimes[i] - pulseTimes[i - 1]);
        double meanMs = deltas.Average();
        // Tolerance: ±3 ms around the ideal 20.833 ms (scheduler jitter on a CI box).
        Assert.InRange(meanMs, pulseIntervalMs - 3.0, pulseIntervalMs + 3.0);
    }

    [Fact]
    public void MidTempoChange_DeferredToBarBoundary()
    {
        // 4/4 at 60 BPM → a quarter is 1000 ms, a bar (4 quarters) is 4000 ms,
        // and a pulse is ~41.67 ms. We change the tempo mid-bar and assert the
        // pulse rate does NOT change until the next bar boundary.
        const double slowBpm = 60.0;
        const double fastBpm = 240.0;
        var handle = new TimestampingHandle();
        var ctx = new MusicalContext
        {
            Tempo = slowBpm,
            TimeSignature = new FlowLang.TypeSystem.SpecialTypes.TimeSignatureData(4, 4),
        };

        var clock = MidiClock.StartMaster(ctx, handle);

        // Let the first bar run partway (≈1.5 quarters at 60 BPM = 1500 ms), then
        // change tempo mid-bar. The master re-reads tempo only at the next bar
        // boundary (4000 ms), so pulses through ~4000 ms stay at the slow rate.
        Thread.Sleep(1500);
        ctx.Tempo = fastBpm; // mid-bar change

        // Run to ~3500 ms (still inside bar 1) and snapshot.
        Thread.Sleep(2000);
        clock.Stop();

        var sent = handle.Snapshot();
        var pulseTimes = sent.Where(e => e.status == MidiClock.ClockPulse).Select(e => e.atMs).ToList();

        // Inspect inter-pulse deltas AFTER the mid-bar change (atMs > 1500) but
        // BEFORE the next bar boundary (atMs < 4000). They must still reflect the
        // SLOW tempo (~41.67 ms), proving the change was deferred.
        double slowPulseMs = 60000.0 / (slowBpm * MidiClock.PulsesPerQuarter);   // ~41.67
        var inWindow = new List<double>();
        for (int i = 1; i < pulseTimes.Count; i++)
        {
            double at = pulseTimes[i];
            if (at > 1600 && at < 3900) inWindow.Add(pulseTimes[i] - pulseTimes[i - 1]);
        }

        Assert.True(inWindow.Count >= 3, $"expected ≥3 post-change in-bar deltas, got {inWindow.Count}");
        double meanInWindow = inWindow.Average();
        // Must be near the SLOW rate, NOT the fast rate (~10.4 ms). Tolerance ±5 ms.
        Assert.InRange(meanInWindow, slowPulseMs - 5.0, slowPulseMs + 5.0);
    }

    /// <summary>
    /// WR-04: a MASTER clock advances the SHARED pulse counter, so
    /// <see cref="MidiClock.PulseCount"/> and <see cref="MidiClock.AtBarBoundary"/>
    /// reflect its real progress. Before the fix the master counted with a discarded
    /// local, leaving PulseCount stuck at 0 → AtBarBoundary always true → the
    /// CLOCK-02 master→slave bar-boundary gate never deferred. Here, after ~2 quarters
    /// at 120 BPM (≈48 pulses) the master's PulseCount must be well above 0, and a
    /// non-bar-multiple count must report NOT at a bar boundary (4/4 → 96/bar).
    /// </summary>
    [Fact]
    public void ClockMaster_AdvancesSharedPulseCount()
    {
        const double bpm = 120.0;
        var handle = new TimestampingHandle();
        var ctx = new MusicalContext
        {
            Tempo = bpm,
            TimeSignature = new FlowLang.TypeSystem.SpecialTypes.TimeSignatureData(4, 4),
        };

        var clock = MidiClock.StartMaster(ctx, handle);
        // ~2 quarters at 120 BPM = ~1 s → ~48 pulses (well under the 96 pulses/bar).
        Thread.Sleep(1000);

        long count = clock.PulseCount;
        clock.Stop();

        // Master genuinely advanced the shared counter (was permanently 0 before).
        Assert.True(count > 24, $"master PulseCount did not advance: {count}");
        Assert.True(count < 96, $"unexpectedly ran a full bar: {count}");

        // A non-bar-multiple count must NOT be reported as a bar boundary, proving
        // AtBarBoundary reads the same advancing counter (CLOCK-02 gate works).
        if (count % (24 * 4) != 0)
            Assert.False(clock.AtBarBoundary());
    }

    [Fact]
    public void ClockHandle_RefIdentity_AndCleanDispose()
    {
        var h1 = new ClockHandleData
        {
            Mode = ClockMode.Master,
            Clock = MidiClock.StartMaster(new MusicalContext { Tempo = 120 }, new TimestampingHandle()),
        };
        var h2 = new ClockHandleData
        {
            Mode = ClockMode.Master,
            Clock = MidiClock.StartMaster(new MusicalContext { Tempo = 120 }, new TimestampingHandle()),
        };

        var v1 = Value.ClockHandle(h1);
        var v2 = Value.ClockHandle(h2);

        // Reference identity: distinct handles → distinct underlying data.
        Assert.Same(ClockHandleType.Instance, v1.Type);
        Assert.NotSame(v1.As<ClockHandleData>(), v2.As<ClockHandleData>());

        // Clean dispose within ~1s (no hang).
        var sw = Stopwatch.StartNew();
        h1.Clock.Stop();
        h2.Clock.Stop();
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2500, $"dispose took {sw.ElapsedMilliseconds} ms (should be ≪ 1s each)");
    }
}
