using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Platform.Linux;

/// <summary>Modern RtMidi C ABI (4+), consistent with the existing language MIDI backend.</summary>
internal sealed class RtMidiInputPort : IMidiInputPort
{
    private IntPtr _handle;
    private RtMidiInputPort()
    {
        _handle = Native.Create();
        try { Check(); }
        catch { Dispose(); throw; }
    }

    internal static IReadOnlyList<string> ListPorts()
    {
        using var port = new RtMidiInputPort();
        return Array.AsReadOnly(port.Names());
    }

    internal static RtMidiInputPort Open(string name)
    {
        var port = new RtMidiInputPort();
        try
        {
            var names = port.Names();
            var matches = names.Select((value, index) => (value, index))
                .Where(item => string.Equals(item.value, name, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
                throw new IOException($"Expected one MIDI input named '{name}', found {matches.Length}.");
            Native.Ignore(port._handle, true, true, true);
            port.Check();
            Native.Open(port._handle, (uint)matches[0].index, "Flow DAW input");
            port.Check();
            return port;
        }
        catch { port.Dispose(); throw; }
    }

    private string[] Names()
    {
        uint count = Native.Count(_handle);
        Check();
        if (count > 4096) throw new IOException("MIDI port enumeration exceeded its budget.");
        var names = new string[count];
        var buffer = new byte[4096];
        for (uint i = 0; i < count; i++)
        {
            Array.Clear(buffer);
            int length = buffer.Length;
            int result = Native.Name(_handle, i, buffer, ref length);
            Check();
            int end = Array.IndexOf(buffer, (byte)0);
            if (result < 0 || length > buffer.Length || end < 0)
                throw new IOException("Could not read the complete MIDI port name.");
            names[i] = Encoding.UTF8.GetString(buffer, 0, end);
        }
        return names;
    }

    public int Read(byte[] buffer)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        UIntPtr length = (UIntPtr)buffer.Length;
        double delta = Native.Read(_handle, buffer, ref length);
        Check();
        if (!double.IsFinite(delta) || delta < 0 || length.ToUInt64() > (ulong)buffer.Length)
            throw new IOException("Native MIDI input failed or exceeded its packet buffer.");
        return (int)length.ToUInt64();
    }

    private void Check()
    {
        if (_handle == IntPtr.Zero) throw new IOException("Could not create MIDI input.");
        var wrapper = Marshal.PtrToStructure<Native.Wrapper>(_handle);
        if (!wrapper.Ok) throw new IOException(Marshal.PtrToStringUTF8(wrapper.Message) ?? "Native MIDI input failed.");
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        // RtMidi input destruction closes the port and releases its native reader.
        Native.Free(_handle);
        _handle = IntPtr.Zero;
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Wrapper
        {
            internal IntPtr Pointer, Data;
            [MarshalAs(UnmanagedType.I1)] internal bool Ok;
            internal IntPtr Message;
        }
        private const string Lib = "rtmidi";
        [DllImport(Lib, EntryPoint = "rtmidi_in_create_default", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Create();
        [DllImport(Lib, EntryPoint = "rtmidi_in_free", CallingConvention = CallingConvention.Cdecl)] internal static extern void Free(IntPtr device);
        [DllImport(Lib, EntryPoint = "rtmidi_get_port_count", CallingConvention = CallingConvention.Cdecl)] internal static extern uint Count(IntPtr device);
        [DllImport(Lib, EntryPoint = "rtmidi_get_port_name", CallingConvention = CallingConvention.Cdecl)] internal static extern int Name(IntPtr device, uint index, byte[] buffer, ref int length);
        [DllImport(Lib, EntryPoint = "rtmidi_open_port", CallingConvention = CallingConvention.Cdecl)] internal static extern void Open(IntPtr device, uint index, [MarshalAs(UnmanagedType.LPStr)] string name);
        [DllImport(Lib, EntryPoint = "rtmidi_in_ignore_types", CallingConvention = CallingConvention.Cdecl)] internal static extern void Ignore(IntPtr device, [MarshalAs(UnmanagedType.I1)] bool sysex, [MarshalAs(UnmanagedType.I1)] bool timing, [MarshalAs(UnmanagedType.I1)] bool sensing);
        [DllImport(Lib, EntryPoint = "rtmidi_in_get_message", CallingConvention = CallingConvention.Cdecl)] internal static extern double Read(IntPtr device, byte[] buffer, ref UIntPtr length);
    }
}
