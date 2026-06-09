using System;
using System.Collections.Generic;
using FlowLang.Audio;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 Wave-0 — an in-process loopback <see cref="IMidiBackend"/> /
/// <see cref="IMidiOutputHandle"/> test seam that records every sent byte
/// array into a public list. Models the OSC <c>HandlerInvokeOverride</c> +
/// <c>PulseAudioCaptureBackend.CaptureOverride</c> seams: byte/rate assertions
/// need NO real ALSA / <c>librtmidi.so</c>.
///
/// <para>This is the load-bearing seam for VirtualMidiTests (MIDI-RT-02 byte
/// assertions) and Plan 02's ClockMaster/Slave rate tests. Each send is
/// recorded as the canonical MIDI status+data byte tuple so tests can assert
/// the exact wire bytes (status nibble + channel, pitch, velocity, etc.).</para>
/// </summary>
public sealed class CaptureMidiBackend : IMidiBackend
{
    private readonly List<string> _ports;
    private readonly CaptureMidiHandle _handle = new();

    /// <summary>The single shared handle every <see cref="OpenOutput"/> returns
    /// (so tests can open then inspect the same recorded list).</summary>
    public CaptureMidiHandle Handle => _handle;

    /// <summary>Every byte array sent through the opened handle, in order.</summary>
    public IReadOnlyList<byte[]> Sent => _handle.Sent;

    public CaptureMidiBackend(params string[] ports)
    {
        _ports = new List<string>(ports);
    }

    public string Name => "Capture";
    public bool IsInitialized => true;
    public IReadOnlyList<string> ListPorts() => _ports;

    public IMidiOutputHandle? OpenOutput(string port)
    {
        // Charitable: an absent port returns null (dead handle), matching the
        // RtMidi/Null contract so MidiHotPlugNeverThrows can exercise both seams.
        if (!_ports.Contains(port)) return null;
        return _handle;
    }

    public event Action<IReadOnlyList<string>>? PortChanged
    {
        add { }
        remove { }
    }

    public void Dispose() { }

    /// <summary>
    /// Records every send as raw MIDI bytes. Note/CC/program are translated to
    /// their canonical status+data byte sequences (status nibble | 0-based channel)
    /// exactly as a real backend would put them on the wire — so tests assert the
    /// true bytes, not the C# call shape.
    /// </summary>
    public sealed class CaptureMidiHandle : IMidiOutputHandle
    {
        private readonly List<byte[]> _sent = new();
        public IReadOnlyList<byte[]> Sent => _sent;
        public bool Closed { get; private set; }

        private static byte Clamp7(int v) => (byte)(v < 0 ? 0 : (v > 127 ? 127 : v));
        private static byte ClampCh(int c) => (byte)(c < 0 ? 0 : (c > 15 ? 15 : c));

        public void SendNoteOn(int channel, int pitch, int velocity)
            => _sent.Add(new byte[] { (byte)(0x90 | ClampCh(channel)), Clamp7(pitch), Clamp7(velocity) });

        public void SendNoteOff(int channel, int pitch)
            => _sent.Add(new byte[] { (byte)(0x80 | ClampCh(channel)), Clamp7(pitch), 0 });

        public void SendControlChange(int channel, int controller, int value)
            => _sent.Add(new byte[] { (byte)(0xB0 | ClampCh(channel)), Clamp7(controller), Clamp7(value) });

        public void SendProgramChange(int channel, int program)
            => _sent.Add(new byte[] { (byte)(0xC0 | ClampCh(channel)), Clamp7(program) });

        public void SendSysex(byte[] data)
        {
            // Record verbatim — tests assert the length-cap applied at the builtin.
            var copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            _sent.Add(copy);
        }

        public void SendRaw(byte[] bytes)
        {
            var copy = new byte[bytes.Length];
            Array.Copy(bytes, copy, bytes.Length);
            _sent.Add(copy);
        }

        public void Close() => Closed = true;
        public void Dispose() => Close();

        /// <summary>Test helper: clear the recorded list between assertions.</summary>
        public void Clear() => _sent.Clear();
    }
}
