#if !FLOW_WEB
using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Diagnostics;

namespace FlowLang.Audio;

/// <summary>
/// Phase 40 MIDI-RT-02 — real-time MIDI output over librtmidi (ALSA-seq on Linux).
/// The Desktop <see cref="IMidiBackend"/>. Whole-file <c>#if !FLOW_WEB</c> guarded +
/// Compile-Removed on the Web target so the native MIDI dep never reaches the WASM
/// closure (T-40-03).
///
/// <para><b>Plan 40-04 — direct librtmidi P/Invoke (ABI fix).</b> This backend used
/// to wrap RtMidi.Core 1.0.53 (2018), whose pinned binding calls the OLD
/// <c>const char* rtmidi_get_port_name(device, port)</c> signature. Modern librtmidi
/// (≥ 4.0; 6.0.0 / <c>librtmidi.so.7</c> on this box) changed that to
/// <c>int rtmidi_get_port_name(device, port, char* bufOut, int* bufLen)</c> — so
/// RtMidi.Core reads the length-out pointer as a string and frees garbage,
/// <c>free(): invalid pointer</c>-aborting the WHOLE process during
/// <c>(midiPorts)</c> enumeration. The in-process <c>CaptureMidiBackend</c> test seam
/// hid this; it crashed on real hardware. Replaced wholesale with direct
/// <see cref="LibRtMidi"/> bindings (modern signatures), mirroring the
/// <c>[DllImport("jack")]</c> approach in
/// <see cref="FlowLang.StandardLibrary.Midi.JackFunctions"/>. The Open-Q1 reflection
/// bridge is GONE — raw byte send is the public <c>rtmidi_out_send_message</c>.</para>
///
/// <para><b>Charitable everywhere (40-RESEARCH §317):</b> a missing
/// <c>librtmidi.so</c>, an absent port, or a send failure NEVER throws — every path
/// WarnOnce's and continues. <see cref="IsAvailable"/> delegates to the cached
/// <see cref="LibRtMidi.IsAvailable"/> probe (NativeLibrary.TryLoad + a real
/// create/free), so <c>MidiPlaybackManager</c> falls back to
/// <see cref="NullMidiBackend"/> when librtmidi is unusable.</para>
///
/// <para><b>Channel + key are 0-based (Pitfall 3 GONE):</b> the modern raw-byte path
/// builds the wire status byte directly (<c>0x90 | channel</c>, <c>0x80 | channel</c>,
/// etc.) so there is no 1-based-vs-0-based enum to translate. <see cref="ToRtChannel"/>
/// / <see cref="ToRtKey"/> are retained as defensive 0..15 / 0..127 clamps (the same
/// values the wire bytes need) so the existing channel-mapping guard test still pins
/// drum→ch9.</para>
/// </summary>
public sealed class RtMidiMidiBackend : IMidiBackend
{
    /// <inheritdoc/>
    public string Name => "RtMidi";

    /// <inheritdoc/>
    public bool IsInitialized => true;

    /// <inheritdoc/>
    public event Action<IReadOnlyList<string>>? PortChanged;

    /// <summary>
    /// Cheap feature-detection probe. Delegates to <see cref="LibRtMidi.IsAvailable"/>
    /// (NativeLibrary.TryLoad of the "rtmidi" SONAME + a real
    /// <c>rtmidi_out_create_default</c>/free). A missing <c>librtmidi.so</c> →
    /// returns false → <see cref="NullMidiBackend"/> fallback (Pitfall 2).
    /// </summary>
    public static bool IsAvailable()
    {
        try { return LibRtMidi.IsAvailable(); }
        catch { return false; }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListPorts()
    {
        IntPtr dev = IntPtr.Zero;
        try
        {
            dev = LibRtMidi.rtmidi_out_create_default();
            if (dev == IntPtr.Zero || !LibRtMidi.IsOk(dev))
            {
                RenderingDiagnostics.WarnOnce(
                    "midi-listports-create",
                    "[midi] could not create a MIDI output device for enumeration — returning empty list");
                return Array.Empty<string>();
            }

            uint count = LibRtMidi.rtmidi_get_port_count(dev);
            var names = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
                names.Add(LibRtMidi.GetPortName(dev, i));
            return names;
        }
        catch (Exception ex)
        {
            RenderingDiagnostics.WarnOnce(
                "midi-listports-failed",
                $"[midi] could not enumerate MIDI output ports: {ex.Message} — returning empty list");
            return Array.Empty<string>();
        }
        finally
        {
            if (dev != IntPtr.Zero)
            {
                try { LibRtMidi.rtmidi_out_free(dev); } catch { /* best-effort */ }
            }
        }
    }

    /// <summary>
    /// WR-07 port-name matcher (pure, unit-testable): an empty/whitespace
    /// <paramref name="port"/> matches NOTHING (returns <c>-1</c>) so an empty
    /// string can never silently bind an arbitrary first device
    /// (<c>string.Contains("")</c> is true for every device). Otherwise prefers an
    /// exact case-insensitive name match, then falls back to a case-insensitive
    /// substring match. Returns the index of the matched name, or <c>-1</c>.
    /// </summary>
    internal static int MatchPortIndex(IReadOnlyList<string> names, string port)
    {
        if (string.IsNullOrWhiteSpace(port) || names == null) return -1;
        for (int i = 0; i < names.Count; i++)
            if (string.Equals(names[i], port, StringComparison.OrdinalIgnoreCase)) return i;
        for (int i = 0; i < names.Count; i++)
            if (names[i] != null && names[i].Contains(port, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <inheritdoc/>
    public IMidiOutputHandle? OpenOutput(string port)
    {
        // WR-07: an empty/whitespace port name must NOT silently bind to whatever
        // device happens to be first (string.Contains("") is true for every device).
        // Treat it as a charitable dead handle + WarnOnce, consistent with the
        // absent-port path.
        if (string.IsNullOrWhiteSpace(port))
        {
            RenderingDiagnostics.WarnOnce(
                "midi-open-empty-port",
                "[midi] openMidiOutput('') — empty/whitespace port name matches no device; returning a dead handle");
            return null;
        }

        IntPtr dev = IntPtr.Zero;
        try
        {
            dev = LibRtMidi.rtmidi_out_create_default();
            if (dev == IntPtr.Zero || !LibRtMidi.IsOk(dev))
            {
                RenderingDiagnostics.WarnOnce(
                    $"midi-open-create:{port}",
                    $"[midi] could not create a MIDI output device while opening '{port}' — returning dead handle");
                if (dev != IntPtr.Zero) { try { LibRtMidi.rtmidi_out_free(dev); } catch { } }
                return null;
            }

            // Enumerate + match by the same WR-07 rule used in MatchPortIndex.
            uint count = LibRtMidi.rtmidi_get_port_count(dev);
            var names = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
                names.Add(LibRtMidi.GetPortName(dev, i));

            int idx = MatchPortIndex(names, port);
            if (idx < 0)
            {
                RenderingDiagnostics.WarnOnce(
                    $"midi-open-absent:{port}",
                    $"[midi] no MIDI output port matching '{port}' — openMidiOutput returns a dead handle");
                try { LibRtMidi.rtmidi_out_free(dev); } catch { }
                return null;
            }

            LibRtMidi.rtmidi_open_port(dev, (uint)idx, "flow-midi-out");
            if (!LibRtMidi.IsOk(dev))
            {
                RenderingDiagnostics.WarnOnce(
                    $"midi-open-failed:{port}",
                    $"[midi] failed to open MIDI output port '{port}' (index {idx}) — returning dead handle");
                try { LibRtMidi.rtmidi_out_free(dev); } catch { }
                return null;
            }

            // Ownership of `dev` transfers to the handle (freed on Close/Dispose).
            var handle = new RtMidiOutputHandle(dev);
            dev = IntPtr.Zero; // prevent the finally from double-freeing
            return handle;
        }
        catch (Exception ex)
        {
            RenderingDiagnostics.WarnOnce(
                $"midi-open-error:{port}",
                $"[midi] error opening MIDI output port '{port}': {ex.Message} — returning dead handle");
            if (dev != IntPtr.Zero) { try { LibRtMidi.rtmidi_out_free(dev); } catch { } }
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Output devices are owned by their handles (freed on handle Dispose). The
        // backend holds no per-instance native state. PortChanged subscribers are
        // dropped with the instance.
        PortChanged = null;
    }

    /// <summary>
    /// Clamp a 0-based Flow/GM channel to 0..15 — the value the wire status byte's
    /// low nibble needs. The Pitfall-3 1-based RtMidi.Core <c>Channel</c> enum is
    /// GONE (the raw-byte path is 0-based), but the clamp + the drum→ch9 guard test
    /// remain meaningful. Returns the clamped 0-based channel.
    /// </summary>
    internal static int ToRtChannel(int zeroBased)
        => zeroBased < 0 ? 0 : (zeroBased > 15 ? 15 : zeroBased);

    /// <summary>Clamp a 0..127 pitch (the wire data byte range). Defensive.</summary>
    internal static int ToRtKey(int pitch)
        => pitch < 0 ? 0 : (pitch > 127 ? 127 : pitch);

    /// <summary>
    /// Concrete output handle wrapping a librtmidi <c>RtMidiOutPtr</c>. Every send
    /// builds the canonical MIDI wire bytes and hands them to
    /// <c>rtmidi_out_send_message</c> — note/CC/program AND raw clock bytes go
    /// through the SAME public entry point (no reflection bridge).
    /// </summary>
    private sealed class RtMidiOutputHandle : IMidiOutputHandle
    {
        private IntPtr _device;
        private bool _closed;
        private readonly object _lock = new();

        public RtMidiOutputHandle(IntPtr device) => _device = device;

        private void Send(byte[] bytes, string warnKey, string what)
        {
            lock (_lock)
            {
                if (_closed || _device == IntPtr.Zero) return;
                try
                {
                    LibRtMidi.rtmidi_out_send_message(_device, bytes, bytes.Length);
                }
                catch (Exception ex)
                {
                    RenderingDiagnostics.WarnOnce(warnKey, $"[midi] {what} send failed: {ex.Message}");
                }
            }
        }

        public void SendNoteOn(int channel, int pitch, int velocity)
        {
            int ch = ToRtChannel(channel), p = ToRtKey(pitch);
            int v = velocity < 0 ? 0 : (velocity > 127 ? 127 : velocity);
            Send(new byte[] { (byte)(0x90 | ch), (byte)p, (byte)v }, "midi-send-noteon", "note-on");
        }

        public void SendNoteOff(int channel, int pitch)
        {
            int ch = ToRtChannel(channel), p = ToRtKey(pitch);
            Send(new byte[] { (byte)(0x80 | ch), (byte)p, 0 }, "midi-send-noteoff", "note-off");
        }

        public void SendControlChange(int channel, int controller, int value)
        {
            int ch = ToRtChannel(channel);
            int c = controller < 0 ? 0 : (controller > 127 ? 127 : controller);
            int v = value < 0 ? 0 : (value > 127 ? 127 : value);
            Send(new byte[] { (byte)(0xB0 | ch), (byte)c, (byte)v }, "midi-send-cc", "control-change");
        }

        public void SendProgramChange(int channel, int program)
        {
            int ch = ToRtChannel(channel);
            int prog = program < 0 ? 0 : (program > 127 ? 127 : program);
            Send(new byte[] { (byte)(0xC0 | ch), (byte)prog }, "midi-send-program", "program-change");
        }

        public void SendSysex(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            // The data is already framed (0xF0 ... 0xF7) at the builtin boundary
            // (MidiFunctions.BufferToSysexBytes). Send verbatim.
            Send(data, "midi-send-sysex", "sysex");
        }

        public void SendRaw(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            Send(bytes, "midi-send-raw", "raw");
        }

        public void Close()
        {
            lock (_lock)
            {
                if (_closed) return;
                _closed = true;
                if (_device != IntPtr.Zero)
                {
                    try { LibRtMidi.rtmidi_close_port(_device); } catch { /* best-effort */ }
                    try { LibRtMidi.rtmidi_out_free(_device); } catch { /* best-effort */ }
                    _device = IntPtr.Zero;
                }
            }
        }

        public void Dispose() => Close();
    }
}
#endif
