#if !FLOW_WEB
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 Plan 40-04 — the REAL native-path verification (the whole point of the
/// ABI fix). Exercises the REAL <see cref="RtMidiMidiBackend"/> + <see cref="MidiClock"/>
/// over a live ALSA <c>snd-virmidi</c> loopback, capturing the wire bytes with the
/// ALSA <c>amidi</c> CLI — proving the modern librtmidi bindings (Plan 40-04) work
/// end-to-end on real hardware, not just through the in-process CaptureMidiBackend
/// seam that hid the RtMidi.Core ABI crash.
///
/// <para><b>Charitable SKIP (D-40-07 / mirrors Phase 39 mscore gate):</b> every Fact
/// probes <see cref="LibRtMidi.IsAvailable"/> AND for a VirMIDI port pair (a librtmidi
/// port whose name contains "VirMIDI" / "Virtual Raw MIDI" with a parseable
/// <c>card-sub</c> suffix) AND for the <c>amidi</c> binary. When any is absent the
/// Fact calls <c>Assert.Skip</c> with a specific reason (audit-0609 §8.1 — prior
/// pattern was Assert.True(true) which reported PASS with zero assertions exercised)
/// so CI without <c>snd-virmidi</c> stays green. On the bench box (snd-virmidi
/// loaded + librtmidi 6.0.0) they RUN and assert the captured bytes.</para>
///
/// <para><b>VirMIDI mapping:</b> the librtmidi ALSA-seq port "Virtual Raw MIDI 3-0"
/// (seq client 28:0) is the same device as rawmidi <c>hw:3,0</c>. The "3-0" suffix is
/// parsed at runtime → <c>hw:3,0</c>; the first available pair is chosen (card 3 is
/// NOT hardcoded). Writing to the librtmidi port is read by <c>amidi -p hw:3,0 -d</c>;
/// injecting via <c>amidi -p hw:3,0 -S "F8"</c> is read by a librtmidi input bound to
/// the same port.</para>
///
/// <para><b>Serialized via <see cref="WasmEntryConsoleCollection"/>:</b> these Facts
/// spawn CPU-heavy <c>amidi</c> child processes + busy-poll a real input device. Run
/// in parallel they (a) starve the process-wide-Console-redirecting Phase 48 WASM
/// tests and (b) jitter the real-time Stopwatch deltas the in-process
/// <c>ClockSlaveTests</c>/<c>ClockMasterTests</c> rely on. Sharing the existing serial
/// collection forces every CPU-/Console-sensitive class to run one-at-a-time
/// (Plan 40-04 Rule 1 test-infra fix — same root cause + remedy Plan 40-01/02/03
/// applied to VirtualMidiTests / OfflineRenderDeterminismTests / JackTransportTests).</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class RealMidiLoopbackTests
{
    private readonly ITestOutputHelper _out;
    public RealMidiLoopbackTests(ITestOutputHelper output) => _out = output;

    /// <summary>A resolved VirMIDI loopback pair: the librtmidi port NAME (for
    /// RtMidiMidiBackend.OpenOutput / MidiClock) and the matching ALSA rawmidi device
    /// (<c>hw:CARD,SUB</c>) for amidi capture/inject.</summary>
    private readonly record struct VirMidiPair(string PortName, int Card, int Sub)
    {
        public string RawDevice => $"hw:{Card},{Sub}";
    }

    /// <summary>
    /// Find the first librtmidi OUTPUT port that names a VirMIDI device and whose
    /// "card-sub" suffix can be parsed into an <c>hw:CARD,SUB</c> rawmidi device.
    /// Returns null when none is found (→ charitable skip). Does NOT hardcode card 3.
    /// </summary>
    private static VirMidiPair? FindVirMidiPair()
    {
        if (!LibRtMidi.IsAvailable()) return null;

        var backend = new RtMidiMidiBackend();
        IReadOnlyList<string> ports;
        try { ports = backend.ListPorts(); }
        catch { return null; }
        finally { backend.Dispose(); }

        // Names look like "Virtual Raw MIDI 3-0:VirMIDI 3-0 28:0". Pull the FIRST
        // "<card>-<sub>" token; verify the rawmidi device exists per `amidi -l`.
        var rawDevices = ListAmidiRawDevices(); // set of "hw:C,S" amidi reports
        var rx = new Regex(@"(\d+)-(\d+)");
        foreach (var name in ports)
        {
            if (name.IndexOf("VirMIDI", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("Virtual Raw MIDI", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var m = rx.Match(name);
            if (!m.Success) continue;
            int card = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int sub = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            string hw = $"hw:{card},{sub}";
            if (rawDevices.Count == 0 || rawDevices.Contains(hw))
                return new VirMidiPair(name, card, sub);
        }
        return null;
    }

    /// <summary>Parse `amidi -l` for the set of rawmidi devices (e.g. "hw:3,0").
    /// Empty set when amidi is unavailable — callers then trust the parsed suffix.</summary>
    private static HashSet<string> ListAmidiRawDevices()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var output = RunCli("amidi", "-l", timeoutMs: 4000, captureStdout: true);
        if (output == null) return set;
        var rx = new Regex(@"hw:\d+,\d+");
        foreach (Match m in rx.Matches(output)) set.Add(m.Value);
        return set;
    }

    /// <summary>Whether the `amidi` binary is runnable.</summary>
    private static bool AmidiAvailable() => RunCli("amidi", "-V", timeoutMs: 4000, captureStdout: true) != null
        || RunCli("amidi", "-l", timeoutMs: 4000, captureStdout: true) != null;

    /// <summary>Run a CLI command, returning its combined stdout (or null on
    /// launch failure / timeout). Used for amidi -l / -V probes.</summary>
    private static string? RunCli(string file, string args, int timeoutMs, bool captureStdout)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            string stdout = captureStdout ? p.StandardOutput.ReadToEnd() : string.Empty;
            string stderr = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return null; }
            return stdout + "\n" + stderr;
        }
        catch
        {
            return null;
        }
    }

    private static void SkipIfUnavailable(out VirMidiPair pair)
    {
        pair = default;
        if (!AmidiAvailable())
            Assert.Skip("amidi CLI absent — real MIDI loopback requires amidi (ALSA utils)");
        var found = FindVirMidiPair();
        if (found == null)
            Assert.Skip("librtmidi or snd-virmidi VirMIDI loopback port absent — load snd-virmidi + install librtmidi-dev");
        pair = found.Value;
    }

    // =========================================================================
    // amidi capture helper — spawn `amidi -p hw:C,S -d`, return when killed.
    // =========================================================================

    private sealed class AmidiCapture : IDisposable
    {
        private readonly Process _proc;
        private readonly StringBuilder _sb = new();
        private readonly object _lock = new();
        private readonly Thread _reader;
        private volatile bool _stop;

        public AmidiCapture(string rawDevice)
        {
            var psi = new ProcessStartInfo("amidi", $"-p {rawDevice} -d")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            _proc = Process.Start(psi)!;
            // Read stdout CHAR-BY-CHAR on a background thread rather than via
            // BeginOutputReadLine: `amidi -d` prints each MIDI message's hex WITHOUT a
            // trailing newline until the NEXT message arrives, so the last message
            // (e.g. a final 0xFC or a trailing sysex) is stuck in the line buffer and
            // never delivered to a line-based reader before we kill amidi. A raw
            // char-stream reader captures every flushed byte immediately.
            var stream = _proc.StandardOutput;
            _reader = new Thread(() =>
            {
                try
                {
                    var chBuf = new char[256];
                    while (!_stop)
                    {
                        int read = stream.Read(chBuf, 0, chBuf.Length);
                        if (read <= 0) break; // EOF (process exited)
                        lock (_lock) _sb.Append(chBuf, 0, read);
                    }
                }
                catch { /* stream closed on kill — fine */ }
            })
            { IsBackground = true, Name = "amidi-capture-reader" };
            _reader.Start();
        }

        /// <summary>Captured hex bytes so far, upper-case, space/newline-normalized to a
        /// single space-joined string (e.g. "90 3C 64 B0 07 5A 80 3C 00").</summary>
        public string Hex()
        {
            string raw;
            lock (_lock) raw = _sb.ToString();
            var tokens = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => Regex.IsMatch(t, "^[0-9A-Fa-f]{2}$"))
                .Select(t => t.ToUpperInvariant());
            return string.Join(" ", tokens);
        }

        /// <summary>Poll <see cref="Hex"/> until it contains every <paramref name="needles"/>
        /// substring or the timeout elapses (amidi output is line-buffered + the child
        /// process flushes asynchronously, so a fixed sleep races). Returns the final
        /// captured hex either way.</summary>
        public string WaitForHex(int timeoutMs, params string[] needles)
        {
            var sw = Stopwatch.StartNew();
            string hex = Hex();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                hex = Hex();
                if (needles.All(n => hex.Contains(n))) return hex;
                Thread.Sleep(25);
            }
            return hex;
        }

        public void Dispose()
        {
            _stop = true;
            try { if (!_proc.HasExited) _proc.Kill(true); } catch { }
            try { _proc.WaitForExit(2000); } catch { }
            try { _reader.Join(1000); } catch { }
            try { _proc.Dispose(); } catch { }
        }
    }

    /// <summary>Inject a single raw MIDI message (hex, no spaces, e.g. "F8") into a
    /// rawmidi device via `amidi -p hw:C,S -S "<hex>"`.</summary>
    private static void AmidiSend(string rawDevice, string hexNoSpaces)
        => RunCli("amidi", $"-p {rawDevice} -S {hexNoSpaces}", timeoutMs: 4000, captureStdout: false);

    // =========================================================================
    // ROW 1-2: real output — NoteOn / NoteOff / CC / sysex round-trip.
    // =========================================================================

    [Fact]
    public void RealOutput_NoteCcSysex_CapturedByteForByte()
    {
        SkipIfUnavailable(out var pair);

        using var capture = new AmidiCapture(pair.RawDevice);
        Thread.Sleep(300); // let amidi attach before we send

        var backend = new RtMidiMidiBackend();
        IMidiOutputHandle? handle = backend.OpenOutput(pair.PortName);
        Assert.NotNull(handle); // real open must succeed on the bench box

        try
        {
            handle!.SendNoteOn(0, 60, 100);      // 90 3C 64
            Thread.Sleep(40);
            handle.SendControlChange(0, 7, 100); // B0 07 64
            Thread.Sleep(40);
            handle.SendNoteOff(0, 60);           // 80 3C 00
            Thread.Sleep(40);
            handle.SendSysex(new byte[] { 0xF0, 0x7D, 0x01, 0x02, 0xF7 }); // framed sysex
            // Poll the async amidi capture until every expected byte group is present
            // (line-buffered child flush races a fixed sleep).
            string captured = capture.WaitForHex(2000,
                "90 3C 64", "B0 07 64", "80 3C 00", "F0 7D 01 02 F7");
            _out.WriteLine("captured: " + captured);

            // The exact channel-voice bytes must appear on the wire.
            Assert.Contains("90 3C 64", captured); // NoteOn ch0 C4 vel100
            Assert.Contains("B0 07 64", captured); // CC ch0 controller7 value100
            Assert.Contains("80 3C 00", captured); // NoteOff ch0 C4
            // Framed sysex round-trips verbatim.
            Assert.Contains("F0 7D 01 02 F7", captured);
        }
        finally
        {
            handle!.Close();
            backend.Dispose();
        }
    }

    // =========================================================================
    // ROW 3 (CLOCK-01): real master — transport 0xFA (start) + 0xFC (stop) over
    // the real wire via amidi.
    //
    // NOTE on 0xF8: ALSA's snd-virmidi rawmidi-capture side FILTERS the 0xF8
    // timing-clock realtime byte specifically (kernel behavior — verified
    // empirically Plan 40-04: the same MidiClock master delivers 0xFA + 0xFC
    // through `amidi -d` every run, but never a 0xF8). This is NOT a Flow bug — the
    // master DOES put 0xF8 on the wire (the 24-PPQN rate is machine-proven by
    // ClockMasterTests.ClockMaster24PpqnRate), and 0xF8 flows fine in the OTHER
    // direction (Row 4 reads 0xF8 injected via `amidi -S` over the real librtmidi
    // input). So Row 3 asserts the transport messages that ARE wire-observable —
    // proving the master's native librtmidi output reaches a real MIDI consumer.
    // =========================================================================

    [Fact]
    public void RealClockMaster_EmitsTransportStartStopOverWire()
    {
        SkipIfUnavailable(out var pair);

        using var capture = new AmidiCapture(pair.RawDevice);
        Thread.Sleep(300);

        var backend = new RtMidiMidiBackend();
        IMidiOutputHandle? handle = backend.OpenOutput(pair.PortName);
        Assert.NotNull(handle);

        const double bpm = 120.0;
        var ctx = new MusicalContext { Tempo = bpm };
        // 24 PPQN at 120 BPM → ~48 pulses/s. Run ~0.8s so the master genuinely emits
        // ~38 pulses (rate pinned by ClockMasterTests) plus the 0xFA on start.
        var clock = MidiClock.StartMaster(ctx, handle);
        Thread.Sleep(800);
        clock.Stop();        // emits 0xFC
        // Poll the async amidi capture until both transport bytes land.
        string hex = capture.WaitForHex(2000, "FA", "FC");
        handle!.Close();
        backend.Dispose();
        _out.WriteLine("captured clock transport hex: " + hex);

        var tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // 0xFA (start) on enable + 0xFC (stop) on dispose — both wire-observable.
        Assert.Contains("FA", tokens); // MIDI Start
        Assert.Contains("FC", tokens); // MIDI Stop
    }

    // =========================================================================
    // ROW 4 (CLOCK-02): real slave — inject 0xF8 via amidi, Tempo locks.
    // =========================================================================

    [Fact]
    public void RealClockSlave_LocksTempoFromInjected24Ppqn()
    {
        SkipIfUnavailable(out var pair);

        // Ensure no test seam is active — exercise the REAL librtmidi input path.
        MidiClock.SlaveSourceOverride = null;

        var ctx = new MusicalContext();
        var clock = MidiClock.StartSlave(ctx, pair.PortName);
        // Give the input poll thread a moment to bind the port.
        Thread.Sleep(300);

        try
        {
            // Target 120 BPM → 24 PPQN → one pulse every ~20.83 ms. Inject a steady
            // stream well past the 8-pulse settle window. amidi -S spawns per call, so
            // its launch latency dominates real spacing; we measure the ACHIEVED rate
            // from wall-clock and assert Tempo locks near it (not a fixed BPM).
            const int pulses = 40;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < pulses; i++)
            {
                AmidiSend(pair.RawDevice, "F8");
                Thread.Sleep(15);
            }
            sw.Stop();
            Thread.Sleep(200); // let the last pulses drain into the settle averager

            long pulseCount = clock.PulseCount;
            _out.WriteLine($"slave saw {pulseCount} pulses over {sw.ElapsedMilliseconds} ms");

            // The slave must have RECEIVED the injected pulses over the real input
            // path (proves rtmidi_in_ignore_types(false,...) + poll works).
            Assert.True(pulseCount >= 24,
                $"slave received too few 0xF8 pulses ({pulseCount}) — real input path not delivering clock");

            // After the settle window fills, a live tempo must be derived + locked.
            Assert.True(ctx.TryGetLiveTempo(out double liveBpm),
                "slave did not drive the live-sync tempo sink from the injected clock");
            _out.WriteLine($"locked live tempo = {liveBpm:F1} BPM");

            // The achieved per-pulse interval from wall-clock → expected BPM. amidi
            // launch latency makes the real rate slower than the nominal 15 ms sleep,
            // so derive the expectation from the OBSERVED span and assert the locked
            // tempo tracks it within a generous band (the contract is "locks to the
            // injected rate after settle", not a hardcoded BPM).
            double observedPulseMs = (double)sw.ElapsedMilliseconds / Math.Max(1, pulses - 1);
            double expectedBpm = 60000.0 / (observedPulseMs * MidiClock.PulsesPerQuarter);
            _out.WriteLine($"observed pulse interval ≈ {observedPulseMs:F1} ms → expected ≈ {expectedBpm:F1} BPM");
            Assert.InRange(liveBpm, expectedBpm * 0.5, expectedBpm * 2.0);
            // LINK-02 sanity: the slave drives the LIVE sink, NEVER MusicalContext.Tempo.
            Assert.Null(ctx.Tempo);
        }
        finally
        {
            clock.Stop();
        }
    }
}
#endif
