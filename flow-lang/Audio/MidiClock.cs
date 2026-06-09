#if !FLOW_WEB
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Midi;

namespace FlowLang.Audio;

/// <summary>
/// Phase 40 CLOCK-01/02 — the MIDI clock service. Two halves:
///
/// <para><b>Master (CLOCK-01) — the ONLY genuinely-new mechanism in Phase 40
/// (40-PATTERNS §No Analog Found).</b> A dedicated background thread runs a
/// <see cref="Stopwatch"/>-deadline loop emitting 24 pulses-per-quarter-note
/// (0xF8) at the active <see cref="MusicalContext.Tempo"/>, plus 0xFA (start) on
/// enable and 0xFC (stop) on dispose. Pulse scheduling uses absolute Stopwatch
/// deadlines with a short final spin-wait for sub-ms accuracy — NOT
/// <see cref="Thread.Sleep"/>, whose ~1-15 ms Linux granularity smears the tempo
/// audibly (40-RESEARCH Pitfall 4). Tempo changes apply only at the NEXT bar
/// boundary: the tempo is re-read at each downbeat, so a mid-bar
/// <c>MusicalContext.Tempo</c> change does not alter the pulse rate until the
/// next bar.</para>
///
/// <para><b>Slave (CLOCK-02).</b> Reuses the OSC <c>StartListener</c> lifecycle
/// verbatim (a background <see cref="Task"/> + <see cref="CancellationTokenSource"/>
/// with <c>Cts.Token.Register(dispose)</c> to break a blocked receive — Pitfall 5).
/// Counts incoming 0xF8 pulses, derives BPM from inter-pulse Stopwatch deltas with
/// an 8-pulse settle (averages the last 8 deltas before writing Tempo so a single
/// jittery pulse never lurches the tempo), and drives
/// <see cref="MusicalContext.Tempo"/> (validated via
/// <see cref="MusicalContext.IsValidTempo"/>).</para>
///
/// <para><b>Charitable everywhere:</b> NEVER throws — every failure path
/// WarnOnce's ("[clock] ...") + continues; <see cref="Stop"/> is bounded by a 1s
/// join so dispose never hangs (T-40-04).</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03).</para>
/// </summary>
public sealed class MidiClock
{
    /// <summary>MIDI System Real-Time status bytes (single-byte messages).
    /// Named with a <c>Byte</c> suffix so <see cref="StopByte"/> does not collide
    /// with the <see cref="Stop"/> lifecycle method.</summary>
    public const byte ClockPulse = 0xF8;
    public const byte StartByte = 0xFA;
    public const byte ContinueByte = 0xFB;
    public const byte StopByte = 0xFC;

    /// <summary>Pulses per quarter note — the MIDI clock standard (CLOCK-01).</summary>
    public const int PulsesPerQuarter = 24;

    /// <summary>Slave settle window: BPM is averaged over the last 8 inter-pulse
    /// deltas before writing Tempo (CLOCK-02).</summary>
    public const int SettlePulses = 8;

    private readonly MusicalContext _context;
    private readonly CancellationTokenSource _cts = new();

    // ===== Master state =====
    private readonly IMidiOutputHandle? _output;
    private Thread? _masterThread;

    // ===== Slave state =====
    private Task? _slaveTask;
    private IDisposable? _slaveSubscription;     // unsubscribes the raw-byte event on stop
    private readonly object _slaveLock = new();
    private readonly Queue<double> _interPulseMs = new(); // last N inter-pulse deltas (ms)
    private long _lastPulseTicks = -1;
    private long _pulseCount;
    private readonly Stopwatch _slaveWatch = Stopwatch.StartNew();

    /// <summary>Number of 0xF8 pulses per bar = 24 × beats-per-bar. Read from the
    /// active time signature numerator at construction (defaults to 4 when no
    /// timesig is in scope — 4/4). Master re-reads tempo at each multiple of this.</summary>
    private readonly int _pulsesPerBar;

    /// <summary>The clock session mode (master ⊕ slave). A switch is honored only
    /// at a bar boundary (CLOCK-02) — see <see cref="RequestModeSwitch"/>.</summary>
    public ClockMode Mode { get; private set; }

    /// <summary>Test seam: when non-null, the slave subscribes to THIS byte-stream
    /// source instead of a real RtMidi input device — ClockSlaveTests inject a
    /// synthetic 0xF8 stream via <see cref="SlaveByteSource.Emit"/> with no ALSA.
    /// Always reset to null in test teardown.</summary>
    public static SlaveByteSource? SlaveSourceOverride { get; set; }

    /// <summary>Whether the timing thread / listener is still running.</summary>
    public bool IsRunning => !_cts.IsCancellationRequested;

    private MidiClock(ClockMode mode, MusicalContext context, IMidiOutputHandle? output)
    {
        Mode = mode;
        _context = context;
        _output = output;
        int beatsPerBar = context.TimeSignature?.Numerator ?? 4;
        if (beatsPerBar <= 0) beatsPerBar = 4;
        _pulsesPerBar = PulsesPerQuarter * beatsPerBar;
    }

    // =========================================================================
    // MASTER (CLOCK-01)
    // =========================================================================

    /// <summary>
    /// Start a clock master: emit 0xFA, then 24 PPQN 0xF8 pulses at the active
    /// tempo on a dedicated <see cref="Stopwatch"/>-timed thread until
    /// <see cref="Stop"/>. Tempo is re-read at each bar boundary (no mid-bar jumps).
    /// </summary>
    public static MidiClock StartMaster(MusicalContext context, IMidiOutputHandle? output)
    {
        var clock = new MidiClock(ClockMode.Master, context, output);
        clock._masterThread = new Thread(clock.RunMasterLoop)
        {
            IsBackground = true,
            Name = "flow-midi-clock-master",
        };
        // IN-03: set the elevated priority AFTER construction inside a try/catch so a
        // ThreadStateException / security denial actually falls through charitably
        // (the object-initializer form would let it propagate out of StartMaster,
        // contradicting the "charitable fall-through if denied" intent). A slightly
        // elevated priority helps the Stopwatch deadline loop hold sub-ms accuracy on
        // a busy box; on Linux priority changes are typically no-ops anyway.
        try { clock._masterThread.Priority = ThreadPriority.AboveNormal; } catch { }
        clock._masterThread.Start();
        return clock;
    }

    private void RunMasterLoop()
    {
        // 0xFA start on enable.
        SafeSendRaw(StartByte);

        var watch = Stopwatch.StartNew();
        double nextDeadlineMs = 0.0;

        // Re-read tempo at each bar boundary (downbeat). Seed with the current
        // tempo so the very first bar is at the active tempo.
        double bpm = ReadTempoOrDefault();
        double pulseIntervalMs = PulseIntervalMs(bpm);

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // WR-04: read the SHARED pulse counter (the single source of truth)
                // so AtBarBoundary()/PulseCount/RequestModeSwitch see the master's
                // progress too. The bar-boundary tempo re-read uses the same counter.
                long pulseIndex = Interlocked.Read(ref _pulseCount);

                // Bar boundary: re-read tempo so a mid-bar Tempo change only takes
                // effect here (CLOCK-01 — no mid-bar jumps).
                if (pulseIndex % _pulsesPerBar == 0)
                {
                    bpm = ReadTempoOrDefault();
                    pulseIntervalMs = PulseIntervalMs(bpm);
                }

                // Wait until the absolute deadline for this pulse: coarse sleep
                // for the bulk, then a short spin-wait for the final sub-ms
                // (Pitfall 4 — NOT a plain Thread.Sleep loop).
                SpinUntil(watch, nextDeadlineMs);
                if (_cts.IsCancellationRequested) break;

                SafeSendRaw(ClockPulse);

                // WR-04: advance the SHARED counter (was a discarded local before),
                // so the master's bar position is observable by the CLOCK-02 gate.
                Interlocked.Increment(ref _pulseCount);
                nextDeadlineMs += pulseIntervalMs;
            }
        }
        catch (Exception ex)
        {
            RenderingDiagnostics.WarnOnce("clock-master-loop", $"[clock] master loop error: {ex.Message}");
        }
        finally
        {
            // 0xFC stop on dispose.
            SafeSendRaw(StopByte);
        }
    }

    /// <summary>
    /// Wait until <paramref name="watch"/> reaches <paramref name="deadlineMs"/>.
    /// Coarse-sleeps (cancellation-aware) until ~2 ms out, then spin-waits the
    /// remainder for sub-ms accuracy (Pitfall 4). Returns early if cancelled.
    /// </summary>
    private void SpinUntil(Stopwatch watch, double deadlineMs)
    {
        const double SpinThresholdMs = 2.0;
        while (!_cts.IsCancellationRequested)
        {
            double remaining = deadlineMs - watch.Elapsed.TotalMilliseconds;
            if (remaining <= 0) return;

            if (remaining > SpinThresholdMs)
            {
                // Coarse, cancellation-aware wait for the bulk of the interval.
                // WaitHandle.WaitOne returns immediately on cancel → responsive Stop.
                int sleepMs = (int)(remaining - SpinThresholdMs);
                if (sleepMs > 0) _cts.Token.WaitHandle.WaitOne(sleepMs);
            }
            else
            {
                // Final sub-ms: busy spin-wait for accuracy.
                Thread.SpinWait(64);
            }
        }
    }

    private static double PulseIntervalMs(double bpm)
    {
        // 24 PPQN → one pulse every (60 / BPM / 24) seconds = 60000 / (BPM*24) ms.
        if (bpm <= 0) bpm = 120.0;
        return 60000.0 / (bpm * PulsesPerQuarter);
    }

    private double ReadTempoOrDefault()
    {
        // WR-01: prefer the live-sync tempo sink (a slave/JACK may be driving it)
        // before the static MusicalContext.Tempo. Both are realtime-only reads on
        // the master timing thread; neither path touches offline render.
        if (_context.TryGetLiveTempo(out double live) && MusicalContext.IsValidTempo(live))
            return live;
        double? t = _context.Tempo;
        if (t.HasValue && MusicalContext.IsValidTempo(t.Value)) return t.Value;
        return 120.0; // charitable default when no tempo is in scope
    }

    private void SafeSendRaw(byte status)
    {
        try { _output?.SendRaw(new[] { status }); }
        catch (Exception ex)
        {
            RenderingDiagnostics.WarnOnce("clock-send-raw", $"[clock] raw send failed: {ex.Message}");
        }
    }

    // =========================================================================
    // SLAVE (CLOCK-02)
    // =========================================================================

    /// <summary>
    /// Start a clock slave: subscribe to the incoming raw-byte stream (test seam
    /// when <see cref="SlaveSourceOverride"/> is set; a real RtMidi input device
    /// otherwise — bind via the Plan-01 input access path), count 0xF8 pulses,
    /// derive BPM with an 8-pulse settle, and drive
    /// <see cref="MusicalContext.Tempo"/>. Reuses the OSC StartListener lifecycle
    /// (Cts.Token.Register dispose to break a blocked receive — Pitfall 5).
    /// </summary>
    public static MidiClock StartSlave(MusicalContext context, string port)
    {
        var clock = new MidiClock(ClockMode.Slave, context, output: null);

        var source = SlaveSourceOverride;
        if (source != null)
        {
            // Test seam: subscribe to the injected byte source synchronously.
            EventHandler<byte[]> handler = (_, bytes) => clock.OnIncomingBytes(bytes);
            source.MessageReceived += handler;
            clock._slaveSubscription = new Unsubscriber(() => source.MessageReceived -= handler);
            clock._slaveTask = Task.CompletedTask;
            return clock;
        }

        // Real-hardware path: bind an RtMidi input device by name + subscribe to
        // its internal raw-byte Message event (Plan-01 Open-Q1 input seam). The
        // device + reflection live in RtMidiMidiBackend territory; here we only
        // need the EventHandler<byte[]> stream, which the input bridge exposes.
        // Charitable: if binding fails, return a dead handle + WarnOnce.
        clock._slaveTask = Task.Run(() =>
        {
            IDisposable? unsub = null;
            try
            {
                if (!RtMidiInputBridge.TrySubscribe(port, clock.OnIncomingBytes, clock._cts.Token, out unsub))
                {
                    RenderingDiagnostics.WarnOnce(
                        $"clock-slave-bind:{port}",
                        $"[clock] clockSlave('{port}') — no such input port (or librtmidi.so absent); returning a dead handle");
                    return;
                }

                // WR-03: own the subscription on THIS task thread. Publish it to the
                // field under the lock, then immediately re-check cancellation: if
                // Stop() already fired BEFORE we subscribed (the Stop-before-assign
                // race), the field write happened-after Stop()'s dispose, so this task
                // must tear it down itself. The finally below is the unconditional
                // owner-thread teardown — Stop() disposing the field is now only a
                // fast-path wake, never the sole teardown path.
                lock (clock._slaveLock)
                {
                    clock._slaveSubscription = unsub;
                }
                if (clock._cts.IsCancellationRequested)
                    return; // finally disposes unsub

                // Block until cancellation — the byte stream arrives via the event.
                clock._cts.Token.WaitHandle.WaitOne();
            }
            catch (Exception ex)
            {
                RenderingDiagnostics.WarnOnce(
                    $"clock-slave-error:{port}",
                    $"[clock] clockSlave('{port}') error: {ex.Message} — returning a dead handle");
            }
            finally
            {
                // WR-03: the owning task ALWAYS tears down its own subscription, so
                // the RtMidi input device can never be leaked on a Stop/Start race or
                // a plain cancellation. Idempotent: Unsubscriber.Dispose null-guards,
                // so a double-dispose (here + Stop()) is harmless. Clear the field so
                // Stop()'s later read doesn't touch a disposed handle.
                try { unsub?.Dispose(); } catch { }
                lock (clock._slaveLock)
                {
                    if (ReferenceEquals(clock._slaveSubscription, unsub))
                        clock._slaveSubscription = null;
                }
            }
        }, clock._cts.Token);

        return clock;
    }

    /// <summary>
    /// Process one incoming raw-byte chunk. Each 0xF8 advances the pulse counter;
    /// inter-pulse Stopwatch deltas feed the 8-pulse settle averager; once the
    /// window is full, the derived BPM is written to
    /// <see cref="MusicalContext.Tempo"/> (validated). Charitable on any error.
    /// </summary>
    internal void OnIncomingBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return;
        foreach (var b in bytes)
        {
            if (b != ClockPulse) continue; // ignore start/continue/stop + channel data
            OnClockPulse();
        }
    }

    private void OnClockPulse()
    {
        // IN-04 / WR-04: the pulse counter is uniformly lock-free (Interlocked
        // write here, Interlocked.Read in AtBarBoundary/PulseCount + the master
        // loop). The lock below still guards the inter-pulse queue + _lastPulseTicks,
        // which are slave-only state.
        Interlocked.Increment(ref _pulseCount);
        lock (_slaveLock)
        {
            long now = _slaveWatch.ElapsedTicks;

            if (_lastPulseTicks >= 0)
            {
                double deltaMs = (now - _lastPulseTicks) * 1000.0 / Stopwatch.Frequency;
                if (deltaMs > 0)
                {
                    _interPulseMs.Enqueue(deltaMs);
                    while (_interPulseMs.Count > SettlePulses) _interPulseMs.Dequeue();

                    // 8-pulse settle: only write Tempo once we have a full window,
                    // averaging the last 8 deltas so a single jittery pulse cannot
                    // lurch the tempo (CLOCK-02).
                    if (_interPulseMs.Count >= SettlePulses)
                    {
                        double sum = 0;
                        foreach (var d in _interPulseMs) sum += d;
                        double meanMs = sum / _interPulseMs.Count;
                        double bpm = BpmFromPulseIntervalMs(meanMs);
                        // WR-01/WR-02 (LINK-02): drive the LIVE-SYNC tempo sink, NOT
                        // MusicalContext.Tempo. Writing .Tempo on the cached resolved
                        // context (which sections capture) would leak the live clock
                        // tempo into the deterministic offline render path. SetLiveTempo
                        // is Interlocked (thread-safe) and consumed only by the realtime
                        // clock master — never by SongRenderer/writeWav/writeMidi.
                        if (MusicalContext.IsValidTempo(bpm))
                            _context.SetLiveTempo(bpm);
                    }
                }
            }
            _lastPulseTicks = now;
        }
    }

    private static double BpmFromPulseIntervalMs(double pulseMs)
    {
        // Inverse of PulseIntervalMs: BPM = 60000 / (pulseMs * 24).
        if (pulseMs <= 0) return 0;
        return 60000.0 / (pulseMs * PulsesPerQuarter);
    }

    // =========================================================================
    // MODE SWITCH (CLOCK-02 — master ⊕ slave, bar-boundary only)
    // =========================================================================

    /// <summary>
    /// Request a master ⊕ slave mode switch. The switch is HONORED only at a bar
    /// boundary (CLOCK-02): a mid-bar request is deferred until the current bar's
    /// final pulse. Returns <c>true</c> if applied immediately (a bar boundary was
    /// reached), <c>false</c> if deferred.
    ///
    /// <para>The clock tracks bar position via its pulse counter; a request lands
    /// at a bar boundary when the running pulse count is an exact multiple of the
    /// pulses-per-bar. v1.5 ships the deferral CONTRACT (the bar-boundary gate);
    /// the actual master↔slave thread re-spin is left to the composer stopping the
    /// old handle and starting the new one — the gate is what CLOCK-02 verifies.</para>
    /// </summary>
    public bool RequestModeSwitch(ClockMode target)
    {
        if (target == Mode) return true; // no-op switch is trivially "at boundary"
        if (AtBarBoundary())
        {
            Mode = target;
            return true;
        }
        return false; // deferred — caller re-requests at the next boundary
    }

    /// <summary>Whether the clock is currently at a bar boundary (pulse count is a
    /// multiple of pulses-per-bar). The master thread + slave both advance a pulse
    /// counter; either side's count gates a switch.</summary>
    public bool AtBarBoundary()
    {
        long count = Interlocked.Read(ref _pulseCount);
        return count % _pulsesPerBar == 0;
    }

    /// <summary>Test/inspection helper: number of 0xF8 pulses seen (slave) so far.</summary>
    public long PulseCount => Interlocked.Read(ref _pulseCount);

    // =========================================================================
    // STOP (models OscFunctions.StopListener — Cancel → join/dispose → Wait(1s))
    // =========================================================================

    /// <summary>
    /// Stop the clock cleanly within ~1s, never hanging (T-40-04). Cancels the
    /// CTS (master loop exits + emits 0xFC; slave receive breaks), unsubscribes
    /// the slave byte source, and joins the master thread / waits the slave task
    /// with a 1s cap. Idempotent.
    /// </summary>
    public void Stop()
    {
        try { _cts.Cancel(); } catch { }
        // WR-03: a fast-path wake — dispose the subscription if it's already
        // published, under the lock for consistency with the slave task's
        // publish/teardown. The slave task's finally is the AUTHORITATIVE owner-thread
        // teardown; this is best-effort to break a blocked receive promptly. Both are
        // idempotent (Unsubscriber.Dispose null-guards), so disposing twice is safe.
        IDisposable? sub;
        lock (_slaveLock) { sub = _slaveSubscription; }
        try { sub?.Dispose(); } catch { }

        if (_masterThread != null)
        {
            try { _masterThread.Join(TimeSpan.FromSeconds(1)); } catch { }
        }
        if (_slaveTask != null)
        {
            try { _slaveTask.Wait(TimeSpan.FromSeconds(1)); }
            catch (AggregateException) { }
            catch (Exception) { }
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private Action? _action;
        public Unsubscriber(Action action) => _action = action;
        public void Dispose()
        {
            var a = _action;
            _action = null;
            try { a?.Invoke(); } catch { }
        }
    }
}

/// <summary>
/// Phase 40 CLOCK-02 test seam — an injectable raw-byte source the slave can
/// subscribe to in place of a real RtMidi input device. ClockSlaveTests
/// construct one, set <see cref="MidiClock.SlaveSourceOverride"/>, start the
/// slave, then <see cref="Emit"/> a synthetic 0xF8 stream. Mirrors the OSC
/// <c>DispatchPacketForTesting</c> / <c>HandlerInvokeOverride</c> seam idea.
/// </summary>
public sealed class SlaveByteSource
{
    /// <summary>Fired for every emitted byte chunk; the slave subscribes here.</summary>
    public event EventHandler<byte[]>? MessageReceived;

    /// <summary>Emit a raw-byte chunk to all subscribers (synchronous).</summary>
    public void Emit(byte[] bytes) => MessageReceived?.Invoke(this, bytes);

    /// <summary>Convenience: emit a single 0xF8 clock pulse.</summary>
    public void EmitClockPulse() => Emit(new[] { MidiClock.ClockPulse });
}

/// <summary>
/// Phase 40 CLOCK-02 real-hardware input bridge — Plan 40-04 direct librtmidi input.
/// Opens an <c>RtMidiInPtr</c> via <see cref="LibRtMidi"/>, calls
/// <c>rtmidi_in_ignore_types(in, false, false, false)</c> (CRITICAL — by default
/// RtMidi IGNORES timing/realtime so 0xF8 clock would never arrive), opens the
/// matched port, then polls <c>rtmidi_in_get_message</c> on a dedicated background
/// thread, feeding each chunk into the slave's settle logic. Replaces the old
/// RtMidi.Core reflection seam (the internal <c>IRtMidiInputDevice.Message</c> event)
/// — that whole managed wrapper is gone (its 2018 ABI crashes on modern librtmidi).
///
/// <para>Charitable (T-40-04): returns <c>false</c> when the port is absent or the
/// native lib is unavailable (slave then runs as a dead handle), NEVER throwing. The
/// byte-capture-driven tests never touch this path (they use
/// <see cref="SlaveByteSource"/>); this is exercised by the real ALSA-loopback test
/// (<c>RealMidiLoopbackTests</c>) on a box with <c>librtmidi.so</c> + a VirMIDI port.</para>
/// </summary>
internal static class RtMidiInputBridge
{
    public static bool TrySubscribe(string port, Action<byte[]> onBytes, CancellationToken ct, out IDisposable? unsubscriber)
    {
        unsubscriber = null;
        // WR-07: an empty/whitespace port matches every device via Contains("") —
        // refuse to bind to an arbitrary first device. Charitable no-match.
        if (string.IsNullOrWhiteSpace(port)) return false;

        IntPtr dev = IntPtr.Zero;
        try
        {
            if (!LibRtMidi.IsAvailable()) return false;

            dev = LibRtMidi.rtmidi_in_create_default();
            if (dev == IntPtr.Zero || !LibRtMidi.IsOk(dev))
            {
                if (dev != IntPtr.Zero) { try { LibRtMidi.rtmidi_in_free(dev); } catch { } }
                return false;
            }

            // Resolve a matching input port by name — exact (case-insensitive) first,
            // then substring (WR-07 rule, same as the output backend).
            uint count = LibRtMidi.rtmidi_get_port_count(dev);
            var names = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
                names.Add(LibRtMidi.GetPortName(dev, i));
            int idx = RtMidiMidiBackend.MatchPortIndex(names, port);
            if (idx < 0) { try { LibRtMidi.rtmidi_in_free(dev); } catch { } return false; }

            // CRITICAL: stop ignoring timing so 0xF8 clock pulses are queued.
            try { LibRtMidi.rtmidi_in_ignore_types(dev, false, false, false); } catch { }

            LibRtMidi.rtmidi_open_port(dev, (uint)idx, "flow-clock-in");
            if (!LibRtMidi.IsOk(dev)) { try { LibRtMidi.rtmidi_in_free(dev); } catch { } return false; }

            // Poll on a dedicated background thread until cancellation. rtmidi_in_get_message
            // is non-blocking (returns size 0 when empty), so a short sleep between polls
            // keeps CPU low while staying responsive to 24-PPQN clock (≈ every 20 ms at
            // 120 BPM). 1 ms poll is comfortably finer than the pulse interval.
            var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            IntPtr devCaptured = dev;
            var pollThread = new Thread(() =>
            {
                var buf = new byte[LibRtMidi.NameBufferSize];
                try
                {
                    while (!pollCts.IsCancellationRequested)
                    {
                        UIntPtr size = (UIntPtr)buf.Length;
                        double delta;
                        try { delta = LibRtMidi.rtmidi_in_get_message(devCaptured, buf, ref size); }
                        catch { break; }
                        _ = delta;
                        int n = (int)size;
                        if (n > 0)
                        {
                            var chunk = new byte[n];
                            Array.Copy(buf, chunk, n);
                            try { onBytes(chunk); } catch { /* charitable per-chunk */ }
                        }
                        else
                        {
                            // Nothing queued — brief cancellation-aware wait.
                            pollCts.Token.WaitHandle.WaitOne(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    RenderingDiagnostics.WarnOnce(
                        $"clock-input-poll:{port}",
                        $"[clock] input poll loop error for '{port}': {ex.Message}");
                }
            })
            { IsBackground = true, Name = "flow-midi-clock-slave-in" };
            pollThread.Start();

            IntPtr devToFree = dev;
            dev = IntPtr.Zero; // ownership transfers to the unsubscriber
            unsubscriber = new ActionDisposable(() =>
            {
                try { pollCts.Cancel(); } catch { }
                try { pollThread.Join(TimeSpan.FromSeconds(1)); } catch { }
                try { LibRtMidi.rtmidi_close_port(devToFree); } catch { }
                try { LibRtMidi.rtmidi_in_free(devToFree); } catch { }
                try { pollCts.Dispose(); } catch { }
            });
            return true;
        }
        catch (Exception ex)
        {
            RenderingDiagnostics.WarnOnce(
                $"clock-input-bridge:{port}",
                $"[clock] input bridge unavailable for '{port}': {ex.Message}");
            if (dev != IntPtr.Zero) { try { LibRtMidi.rtmidi_in_free(dev); } catch { } }
            return false;
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var a = _action;
            _action = null;
            try { a?.Invoke(); } catch { }
        }
    }
}
#endif
