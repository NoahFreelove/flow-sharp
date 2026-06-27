#if !FLOW_WEB
using System;
using System.Runtime.InteropServices;

namespace FlowLang.Audio;

/// <summary>
/// Phase 40 Plan 40-04 — direct <c>[DllImport("rtmidi")]</c> bindings against the
/// MODERN librtmidi C API (RtMidi ≥ 4.0; tested on 6.0.0 / <c>librtmidi.so.7</c>).
/// Replaces the RtMidi.Core 1.0.53 (2018) managed wrapper, whose pinned ABI calls
/// the OLD <c>const char* rtmidi_get_port_name(device, port)</c> signature; modern
/// librtmidi changed that to <c>int rtmidi_get_port_name(device, port, char* bufOut,
/// int* bufLen)</c>. RtMidi.Core's stale binding reads the length-out pointer as a
/// <c>const char*</c> and frees garbage → <c>free(): invalid pointer</c> aborts the
/// whole process during port enumeration. The fix is to bind the modern signatures
/// ourselves — exactly the libjack approach in
/// <see cref="FlowLang.StandardLibrary.Midi.JackFunctions"/>.
///
/// <para><b>Charitable probe (Pitfall 2 / MIDI-RT-04):</b> the native library is
/// optional — it ships in <c>librtmidi-dev</c> / the distro <c>librtmidi</c> package,
/// NOT in any NuGet. <see cref="IsAvailable"/> attempts a <c>NativeLibrary.TryLoad</c>
/// of the "rtmidi" SONAME and falls back to a real <c>rtmidi_out_create_default</c>
/// probe wrapped in try/catch — a missing <c>librtmidi.so</c> surfaces as
/// <see cref="DllNotFoundException"/> and degrades to "no MIDI" so
/// <c>MidiPlaybackManager</c> picks <see cref="NullMidiBackend"/>. The probe result
/// is cached (the SONAME presence does not change within a process).</para>
///
/// <para><b>Memory safety (get_port_name):</b> the modern form REQUIRES the caller to
/// pass a pre-allocated <c>char*</c> buffer plus a <c>int* bufLen</c> initialized to
/// the buffer size — see <see cref="GetPortName"/>. We pass a 512-byte managed buffer
/// (the proven-working probe in <c>/tmp/rtmidi_probe.c</c> used the same), never NULL,
/// so the native side fills it without allocating. The <see cref="RtMidiWrapper"/>
/// struct's <c>ok</c> bit is checked after create/open so a failed native call
/// degrades charitably rather than dereferencing a bad pointer.</para>
///
/// <para><c>#if !FLOW_WEB</c> + Compile-Removed on the Web target (the csproj Web
/// ItemGroup strips this file) so the native MIDI dep never reaches the WASM closure
/// (T-40-03). All P/Invoke is naturally Web-stripped by that Compile Remove.</para>
/// </summary>
internal static class LibRtMidi
{
    /// <summary>The native SONAME. ".NET resolves "rtmidi" to <c>librtmidi.so</c> (the
    /// dev symlink → <c>librtmidi.so.7</c> on this box) via the standard native-lib
    /// search — the same posture as <c>[DllImport("jack")]</c> in JackFunctions.</summary>
    private const string Lib = "rtmidi";

    // =========================================================================
    // RtMidiWrapper — the struct returned by the create functions. RtMidiPtr,
    // RtMidiInPtr, RtMidiOutPtr are all `struct RtMidiWrapper*`. We marshal the
    // struct by VALUE (via Marshal.PtrToStructure on the returned IntPtr) ONLY to
    // read the `ok` bit + `msg` for error checks; every API call takes the raw
    // IntPtr device handle.
    //
    //   struct RtMidiWrapper { void* ptr; void* data; bool ok; const char* msg; };
    // =========================================================================
    [StructLayout(LayoutKind.Sequential)]
    internal struct RtMidiWrapper
    {
        public IntPtr ptr;
        public IntPtr data;
        [MarshalAs(UnmanagedType.I1)]
        public bool ok;
        public IntPtr msg;
    }

    // ----- create / free -----

    [DllImport(Lib, EntryPoint = "rtmidi_out_create_default", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr rtmidi_out_create_default();

    [DllImport(Lib, EntryPoint = "rtmidi_in_create_default", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr rtmidi_in_create_default();

    [DllImport(Lib, EntryPoint = "rtmidi_out_free", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void rtmidi_out_free(IntPtr device);

    [DllImport(Lib, EntryPoint = "rtmidi_in_free", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void rtmidi_in_free(IntPtr device);

    // ----- port enumeration -----

    [DllImport(Lib, EntryPoint = "rtmidi_get_port_count", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint rtmidi_get_port_count(IntPtr device);

    // MODERN buffer-out form. bufOut is a caller-allocated byte buffer; bufLen is
    // in/out (set to the buffer size on call, the written length on return). Pass a
    // generous fixed buffer so the native side never has to allocate.
    [DllImport(Lib, EntryPoint = "rtmidi_get_port_name", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int rtmidi_get_port_name(IntPtr device, uint portNumber, byte[]? bufOut, ref int bufLen);

    // ----- open / close -----

    [DllImport(Lib, EntryPoint = "rtmidi_open_port", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void rtmidi_open_port(IntPtr device, uint portNumber,
        [MarshalAs(UnmanagedType.LPStr)] string portName);

    [DllImport(Lib, EntryPoint = "rtmidi_open_virtual_port", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void rtmidi_open_virtual_port(IntPtr device,
        [MarshalAs(UnmanagedType.LPStr)] string portName);

    [DllImport(Lib, EntryPoint = "rtmidi_close_port", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void rtmidi_close_port(IntPtr device);

    // ----- output -----

    // Raw byte send — clock 0xF8/0xFA/0xFB/0xFC + notes/CC/sysex all go through this.
    [DllImport(Lib, EntryPoint = "rtmidi_out_send_message", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int rtmidi_out_send_message(IntPtr device, byte[] message, int length);

    // ----- input -----

    // CRITICAL for clock slave: by DEFAULT RtMidi ignores sysex + TIMING + active-
    // sense, so 0xF8 clock pulses would never arrive. Call with (false,false,false)
    // to stop ignoring timing so the slave listener sees the clock stream.
    [DllImport(Lib, EntryPoint = "rtmidi_in_ignore_types", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void rtmidi_in_ignore_types(IntPtr device,
        [MarshalAs(UnmanagedType.I1)] bool midiSysex,
        [MarshalAs(UnmanagedType.I1)] bool midiTime,
        [MarshalAs(UnmanagedType.I1)] bool midiSense);

    // Poll the next queued message (non-blocking). Returns the event delta-time in
    // seconds; fills message[0..*size) and writes the byte count to *size. *size must
    // be the buffer capacity on call. A size of 0 on return means no message was
    // queued.
    [DllImport(Lib, EntryPoint = "rtmidi_in_get_message", CallingConvention = CallingConvention.Cdecl)]
    internal static extern double rtmidi_in_get_message(IntPtr device, byte[] message, ref UIntPtr size);

    // =========================================================================
    // Managed helpers
    // =========================================================================

    /// <summary>Buffer size for <see cref="GetPortName"/> + the slave poll buffer.
    /// 512 bytes is more than enough for any ALSA-seq port name and matches the
    /// proven C probe.</summary>
    internal const int NameBufferSize = 512;

    /// <summary>
    /// Read whether the device wrapper's <c>ok</c> bit is set (last native call
    /// succeeded). A null handle reports false. Used after create/open so a failed
    /// native call degrades charitably instead of being silently used.
    /// </summary>
    internal static bool IsOk(IntPtr device)
    {
        if (device == IntPtr.Zero) return false;
        try
        {
            var w = Marshal.PtrToStructure<RtMidiWrapper>(device);
            return w.ok;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Read the name of the <paramref name="portNumber"/>-th port via the MODERN
    /// buffer-out signature. Returns the empty string on any failure (charitable).
    /// </summary>
    internal static string GetPortName(IntPtr device, uint portNumber)
    {
        if (device == IntPtr.Zero) return string.Empty;
        try
        {
            var buf = new byte[NameBufferSize];
            int len = NameBufferSize;
            int rc = rtmidi_get_port_name(device, portNumber, buf, ref len);
            if (rc < 0) return string.Empty;
            // `len` is the written length INCLUDING the NUL terminator on modern
            // librtmidi; trim at the first NUL defensively rather than trusting len.
            int end = Array.IndexOf(buf, (byte)0);
            if (end < 0) end = Math.Min(len, buf.Length);
            if (end <= 0) return string.Empty;
            return System.Text.Encoding.UTF8.GetString(buf, 0, end);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool? _availableCache;
    private static readonly object _probeLock = new();

    /// <summary>
    /// Cached charitable feature-detection probe. True only when the native
    /// <c>librtmidi.so</c> can be loaded AND a default output device can be created.
    /// A missing lib (<see cref="DllNotFoundException"/>), a missing entry point, or
    /// any native surprise → false → <see cref="NullMidiBackend"/> fallback. The
    /// SONAME presence is process-stable, so the result is cached after the first
    /// probe.
    /// </summary>
    internal static bool IsAvailable()
    {
        if (_availableCache.HasValue) return _availableCache.Value;
        lock (_probeLock)
        {
            if (_availableCache.HasValue) return _availableCache.Value;
            _availableCache = Probe();
            return _availableCache.Value;
        }
    }

    private static bool Probe()
    {
        // Confirm the modern API actually answers by creating + freeing a default
        // output device. This goes DIRECTLY through the DllImport resolver — NOT
        // NativeLibrary.TryLoad("rtmidi") — because the two resolvers disagree: the
        // DllImport marshaller decorates the bare "rtmidi" name into librtmidi.so
        // candidates and resolves it, whereas NativeLibrary.TryLoad of the
        // undecorated SONAME returns false on this box even though the .so is present
        // (verified empirically Plan 40-04). Gating on TryLoad therefore wrongly
        // reported "no MIDI" and forced the NullMidiBackend fallback. A missing
        // librtmidi.so surfaces here as DllNotFoundException, which we catch →
        // charitable "no MIDI" (Pitfall 2).
        try
        {
            IntPtr dev = rtmidi_out_create_default();
            if (dev == IntPtr.Zero) return false;
            bool ok = IsOk(dev);
            rtmidi_out_free(dev);
            return ok;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch { return false; }
    }

    /// <summary>Test-only: reset the cached probe (used by no production path).</summary>
    internal static void ResetAvailabilityCacheForTesting() => _availableCache = null;
}
#endif
