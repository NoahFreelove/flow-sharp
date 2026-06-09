using System.Runtime.InteropServices;

namespace FlowLang.Audio;

/// <summary>
/// Shared PulseAudio Simple API P/Invoke surface consumed by both
/// <see cref="PulseAudioSimpleBackend"/> (playback) and
/// <see cref="PulseAudioCaptureBackend"/> (capture).
///
/// <para>
/// Before this file existed each backend privately re-declared the
/// same <c>pa_sample_spec</c> struct and the same <c>pa_simple_new</c> /
/// <c>pa_simple_free</c> / <c>pa_strerror</c> DllImports verbatim —
/// every marshaling fix had to land twice. Audit §8.7 (2026-06-09)
/// extracted them here so there is exactly one copy of each ABI binding.
/// </para>
///
/// <para>
/// <b>ABI contract</b>: all signatures are identical to the two private
/// copies they replace. The only deliberate behavioral change is the
/// <see cref="PaStrerrorUtf8"/> helper (§8.7 pa_strerror fix): PulseAudio
/// returns UTF-8 encoded error strings; the old
/// <c>Marshal.PtrToStringAnsi</c> call silently mangles non-ASCII bytes
/// (e.g., locale-translated messages on non-English Linux), replaced here
/// with <c>Marshal.PtrToStringUTF8</c>.
/// </para>
///
/// <para>
/// <b>Web-strip</b>: this file is <c>Compile Remove</c>'d on
/// <c>FlowTarget=Web</c> (flow-lang.csproj Web ItemGroup) exactly like
/// the two backend files it serves. The AssemblyReferenceScanTests
/// forbidden-P/Invoke gate ("libpulse") continues to pass on the Web build.
/// </para>
/// </summary>
internal static class LibPulse
{
    // -----------------------------------------------------------------------
    // Stream direction constants (pa_stream_direction_t, pulse/def.h)
    // -----------------------------------------------------------------------

    /// <summary>PA_STREAM_PLAYBACK — used by <see cref="PulseAudioSimpleBackend"/>.</summary>
    internal const int PA_STREAM_PLAYBACK = 1;

    /// <summary>PA_STREAM_RECORD — used by <see cref="PulseAudioCaptureBackend"/>.</summary>
    internal const int PA_STREAM_RECORD = 2;

    // -----------------------------------------------------------------------
    // Sample format constant (pa_sample_format_t, pulse/sample.h)
    // -----------------------------------------------------------------------

    /// <summary>PA_SAMPLE_FLOAT32LE — native-endian IEEE-754 32-bit float.</summary>
    internal const int PA_SAMPLE_FLOAT32LE = 5;

    // -----------------------------------------------------------------------
    // pa_sample_spec struct (pulse/sample.h)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mirrors <c>pa_sample_spec</c> from <c>pulse/sample.h</c>.
    /// LayoutKind.Sequential + no explicit Pack attribute: the C struct
    /// has no special alignment requirement beyond the standard C ABI.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PaSampleSpec
    {
        public int format;
        public uint rate;
        public byte channels;
    }

    // -----------------------------------------------------------------------
    // Shared externs: pa_simple_new / pa_simple_free / pa_strerror
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opens a PulseAudio simple connection (<c>pa_simple_new</c>).
    /// <paramref name="dir"/> selects playback (<see cref="PA_STREAM_PLAYBACK"/>)
    /// or capture (<see cref="PA_STREAM_RECORD"/>).
    /// </summary>
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr pa_simple_new(
        IntPtr server,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int dir,
        IntPtr dev,
        [MarshalAs(UnmanagedType.LPStr)] string streamName,
        ref PaSampleSpec ss,
        IntPtr channelMap,
        IntPtr attr,
        out int error);

    /// <summary>
    /// Closes and frees a PulseAudio simple connection (<c>pa_simple_free</c>).
    /// </summary>
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void pa_simple_free(IntPtr s);

    // -----------------------------------------------------------------------
    // Direction-specific data primitives
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes PCM data to a playback stream (<c>pa_simple_write</c>).
    /// Used exclusively by <see cref="PulseAudioSimpleBackend"/>.
    /// </summary>
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_simple_write(IntPtr s, IntPtr data, nuint bytes, out int error);

    /// <summary>
    /// Reads PCM data from a capture stream (<c>pa_simple_read</c>).
    /// Used exclusively by <see cref="PulseAudioCaptureBackend"/>.
    /// </summary>
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_simple_read(IntPtr s, IntPtr data, nuint bytes, out int error);

    /// <summary>
    /// Drains a playback stream (<c>pa_simple_drain</c>).
    /// Used exclusively by <see cref="PulseAudioSimpleBackend"/>.
    /// </summary>
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_simple_drain(IntPtr s, out int error);

    /// <summary>
    /// Flushes a playback stream (<c>pa_simple_flush</c>).
    /// Used exclusively by <see cref="PulseAudioSimpleBackend"/>.
    /// </summary>
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int pa_simple_flush(IntPtr s, out int error);

    // -----------------------------------------------------------------------
    // pa_strerror (libpulse.so.0 — the main library, not -simple)
    // -----------------------------------------------------------------------

    [DllImport("libpulse.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_strerror(int error);

    /// <summary>
    /// Returns the human-readable error string for a PulseAudio error code.
    ///
    /// <para>
    /// PulseAudio's <c>pa_strerror</c> returns a UTF-8 encoded string.
    /// The old per-backend callers used <c>Marshal.PtrToStringAnsi</c>
    /// which treats the bytes as the current system ANSI code page and
    /// mangles non-ASCII characters (e.g. locale-translated error messages
    /// on non-English Linux). This helper uses
    /// <c>Marshal.PtrToStringUTF8</c> — the fix mandated by audit §8.7.
    /// </para>
    ///
    /// <para>
    /// Falls back to an English-language placeholder when the native call
    /// cannot be resolved (library absent) or returns a null pointer —
    /// both defensive against the charitable-failure contract used by the
    /// backends on non-Linux hosts.
    /// </para>
    /// </summary>
    internal static string GetErrorString(int error)
    {
        try
        {
            var ptr = pa_strerror(error);
            return Marshal.PtrToStringUTF8(ptr) ?? $"PulseAudio error {error}";
        }
        catch (DllNotFoundException)
        {
            return $"PulseAudio error {error} (libpulse not available)";
        }
    }
}
