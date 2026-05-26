using System.Runtime.InteropServices;

namespace FlowLang.Audio;

/// <summary>
/// Audio CAPTURE backend using PulseAudio Simple API via P/Invoke.
/// Sibling-class mirror of <see cref="PulseAudioSimpleBackend"/> with the direction
/// flag flipped from <c>PA_STREAM_PLAYBACK = 1</c> to <c>PA_STREAM_RECORD = 2</c>
/// and the data primitive flipped from <c>pa_simple_write</c> to <c>pa_simple_read</c>.
/// Works with both native PulseAudio and PipeWire's PulseAudio compatibility layer.
///
/// <para>
/// Phase 38 Plan 38-05 AUDIO-IN-01. Recommended sibling-class form per
/// 38-RESEARCH.md §I lines 957-1031. Locking idiom + P/Invoke surface preserve
/// the playback sibling's discipline so a future async-API upgrade can promote
/// both classes uniformly.
/// </para>
///
/// <para>
/// Charitable failure model (D-v1.5-05 + Pitfall #12 "live session never dies
/// mid-set"): <see cref="Initialize"/> returns <c>false</c> on libpulse-simple
/// load failure or device-open failure rather than throwing. The caller
/// (<c>InputFunctions.MicBuffer</c>) emits a one-shot stderr advisory and
/// returns a silent buffer of the requested duration. Hosts without PulseAudio
/// (macOS/Windows/CI containers) never crash the engine — they just receive
/// silence + an advisory.
/// </para>
/// </summary>
public sealed class PulseAudioCaptureBackend : IDisposable
{
    private IntPtr _connection;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly string _streamName;
    private bool _disposed;
    private readonly object _lock = new();

    public string Name => "PulseAudio-Capture";
    public bool IsInitialized => _connection != IntPtr.Zero;
    public int SampleRate => _sampleRate;
    public int Channels => _channels;

    /// <summary>
    /// Constructs a capture backend for the given sample rate + channel count.
    /// The <paramref name="streamName"/> appears in PulseAudio's pavucontrol UI under
    /// the capture-streams tab; "capture" is the default to mirror the playback
    /// sibling's "playback" stream description.
    /// </summary>
    public PulseAudioCaptureBackend(int sampleRate, int channels, string streamName = "capture")
    {
        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (channels < 1 || channels > 8)
            throw new ArgumentException("Channel count must be between 1 and 8.", nameof(channels));
        _sampleRate = sampleRate;
        _channels = channels;
        _streamName = streamName;
    }

    /// <summary>
    /// Checks whether libpulse-simple is available on this system. Mirrors the
    /// playback sibling's <c>IsAvailable</c> static probe (lines 23-34).
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            pa_strerror(0);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Opens the capture stream against the default input device. Mirrors
    /// <c>PulseAudioSimpleBackend.Initialize</c> at lines 36-78 with:
    ///   <list type="bullet">
    ///     <item><c>dir = PA_STREAM_RECORD</c> (was <c>PA_STREAM_PLAYBACK</c>)</item>
    ///     <item><c>streamName = "capture"</c> (was "playback")</item>
    ///   </list>
    /// Returns <c>true</c> on success, <c>false</c> on libpulse load / device-open
    /// failure (writes the libpulse error message to stderr, then surrenders to
    /// the caller's charitable-fallback path per D-v1.5-05).
    /// </summary>
    public bool Initialize(out string? error)
    {
        error = null;
        lock (_lock)
        {
            CloseConnection();

            var sampleSpec = new pa_sample_spec
            {
                format = PA_SAMPLE_FLOAT32LE,
                rate = (uint)_sampleRate,
                channels = (byte)_channels
            };

            int errorCode;
            try
            {
                _connection = pa_simple_new(
                    IntPtr.Zero,        // Use default server
                    "flow-lang",        // Application name (matches playback sibling)
                    PA_STREAM_RECORD,   // <-- direction-swapped vs playback
                    IntPtr.Zero,        // Use default device (composer can override via PulseAudio mixer)
                    _streamName,        // <-- "capture" by default
                    ref sampleSpec,
                    IntPtr.Zero,        // Use default channel map
                    IntPtr.Zero,        // Use default buffering attributes
                    out errorCode);
            }
            catch (DllNotFoundException)
            {
                error = "libpulse-simple.so.0 not found";
                _connection = IntPtr.Zero;
                return false;
            }

            if (_connection == IntPtr.Zero)
            {
                var errMsg = Marshal.PtrToStringAnsi(pa_strerror(errorCode));
                error = $"PulseAudio capture failed to connect: {errMsg}";
                Console.Error.WriteLine(error);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Reads <paramref name="totalFrames"/> frames (each <see cref="Channels"/>
    /// floats) from the capture stream. Blocks until the requested amount has been
    /// delivered by PulseAudio. Returns the captured samples as a single
    /// interleaved <c>float[]</c> of length <c>totalFrames * Channels</c>.
    ///
    /// <para>
    /// Implementation mirrors the playback sibling's
    /// <c>PulseAudioSimpleBackend.Play</c> at lines 80-156 — pin the managed
    /// buffer with <see cref="GCHandle"/>, chunk the read in 4 KB units to stay
    /// friendly with PulseAudio's internal buffer, take the lock around every
    /// touch of <c>_connection</c>. The implementation path mirrors
    /// 38-RESEARCH.md §I lines 990-1021.
    /// </para>
    ///
    /// <para>
    /// On error, returns <c>null</c> with <paramref name="error"/> populated from
    /// <c>pa_strerror</c>. Caller handles the charitable-fallback path (silent
    /// buffer + stderr advisory).
    /// </para>
    /// </summary>
    public float[]? CaptureSamples(int totalFrames, out string? error)
    {
        error = null;
        if (totalFrames <= 0)
            return Array.Empty<float>();

        if (!IsInitialized)
        {
            error = "PulseAudio capture stream not initialized";
            return null;
        }

        int totalSamples = totalFrames * _channels;
        int totalBytes = totalSamples * sizeof(float);
        var samples = new float[totalSamples];
        var handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
        try
        {
            int byteOffset = 0;
            const int chunkBytes = 4096 * sizeof(float);
            while (byteOffset < totalBytes)
            {
                int readSize = Math.Min(chunkBytes, totalBytes - byteOffset);
                int errorCode;
                int result;

                lock (_lock)
                {
                    if (!IsInitialized)
                    {
                        error = "PulseAudio capture stream was closed during read";
                        return null;
                    }

                    var ptr = handle.AddrOfPinnedObject() + byteOffset;
                    result = pa_simple_read(_connection, ptr, (nuint)readSize, out errorCode);
                }

                if (result < 0)
                {
                    var errMsg = Marshal.PtrToStringAnsi(pa_strerror(errorCode));
                    error = $"PulseAudio read error: {errMsg}";
                    return null;
                }

                byteOffset += readSize;
            }
        }
        finally
        {
            handle.Free();
        }
        return samples;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            lock (_lock)
            {
                CloseConnection();
            }
        }
    }

    private void CloseConnection()
    {
        if (_connection != IntPtr.Zero)
        {
            pa_simple_free(_connection);
            _connection = IntPtr.Zero;
        }
    }

    // --- PulseAudio Simple API P/Invoke bindings ---
    //
    // Mirrors flow-lang/Audio/PulseAudioSimpleBackend.cs:273-311 with:
    //   PA_STREAM_PLAYBACK = 1  →  PA_STREAM_RECORD = 2  (per pulseaudio/src/pulse/def.h)
    //   pa_simple_write        →  pa_simple_read
    // All other bindings carry identical Cdecl + LPStr marshalling so the
    // PulseAudio runtime cannot tell sibling instances apart at the ABI layer.

    private const int PA_STREAM_RECORD = 2;       // <-- direction-swapped vs playback (was PA_STREAM_PLAYBACK = 1)
    private const int PA_SAMPLE_FLOAT32LE = 5;    // matches playback sibling

    [StructLayout(LayoutKind.Sequential)]
    private struct pa_sample_spec
    {
        public int format;
        public uint rate;
        public byte channels;
    }

    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_simple_new(
        IntPtr server,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int dir,
        IntPtr dev,
        [MarshalAs(UnmanagedType.LPStr)] string streamName,
        ref pa_sample_spec ss,
        IntPtr channelMap,
        IntPtr attr,
        out int error);

    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void pa_simple_free(IntPtr s);

    // <-- NEW: capture-direction primitive (mirror of pa_simple_write at PulseAudioSimpleBackend.cs:301-302)
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern int pa_simple_read(IntPtr s, IntPtr data, nuint bytes, out int error);

    [DllImport("libpulse.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_strerror(int error);
}
