using System.Runtime.InteropServices;

namespace FlowLang.Audio;

/// <summary>
/// Audio backend using PulseAudio Simple API via P/Invoke.
/// Works with both native PulseAudio and PipeWire's PulseAudio compatibility layer.
/// </summary>
public sealed class PulseAudioSimpleBackend : IAudioBackend
{
    private IntPtr _connection;
    private int _sampleRate;
    private int _channels;
    private bool _disposed;
    private readonly object _lock = new();

    public string Name => "PulseAudio";
    public bool IsInitialized => _connection != IntPtr.Zero;

    /// <summary>
    /// Checks whether libpulse-simple is available on this system.
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

    public bool Initialize(int sampleRate, int channels)
    {
        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (channels < 1 || channels > 8)
            throw new ArgumentException("Channel count must be between 1 and 8.", nameof(channels));

        lock (_lock)
        {
            CloseConnection();

            _sampleRate = sampleRate;
            _channels = channels;

            var sampleSpec = new pa_sample_spec
            {
                format = PA_SAMPLE_FLOAT32LE,
                rate = (uint)sampleRate,
                channels = (byte)channels
            };

            int error;
            _connection = pa_simple_new(
                IntPtr.Zero,       // Use default server
                "flow-lang",       // Application name
                PA_STREAM_PLAYBACK,
                IntPtr.Zero,       // Use default device
                "playback",        // Stream description
                ref sampleSpec,
                IntPtr.Zero,       // Use default channel map
                IntPtr.Zero,       // Use default buffering attributes
                out error);

            if (_connection == IntPtr.Zero)
            {
                var errorMsg = Marshal.PtrToStringAnsi(pa_strerror(error));
                Console.Error.WriteLine($"PulseAudio: Failed to connect: {errorMsg}");
                return false;
            }

            return true;
        }
    }

    public void Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default)
    {
        if (samples.Length == 0)
            return;

        lock (_lock)
        {
            // Re-initialize if sample rate or channels changed
            if (!IsInitialized || sampleRate != _sampleRate || channels != _channels)
            {
                if (!Initialize(sampleRate, channels))
                    throw new InvalidOperationException(
                        "No audio output available. Install PipeWire or PulseAudio.");
            }
        }

        // Clamp samples to [-1.0, 1.0] to prevent distortion
        var clamped = ClampSamples(samples);

        // Pin the float array and write in chunks to support cancellation
        var handle = GCHandle.Alloc(clamped, GCHandleType.Pinned);
        try
        {
            int totalBytes = clamped.Length * sizeof(float);
            const int chunkSamples = 4096;
            int bytesPerChunk = chunkSamples * sizeof(float);
            int byteOffset = 0;

            while (byteOffset < totalBytes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Stop();
                    return;
                }

                int remaining = totalBytes - byteOffset;
                int writeSize = Math.Min(bytesPerChunk, remaining);

                int error;
                int result;

                lock (_lock)
                {
                    if (!IsInitialized)
                        return;

                    var ptr = handle.AddrOfPinnedObject() + byteOffset;
                    result = pa_simple_write(_connection, ptr, (nuint)writeSize, out error);
                }

                if (result < 0)
                {
                    var errorMsg = Marshal.PtrToStringAnsi(pa_strerror(error));
                    throw new InvalidOperationException($"PulseAudio write error: {errorMsg}");
                }

                byteOffset += writeSize;
            }
        }
        finally
        {
            handle.Free();
        }

        // Drain: wait for playback to finish
        if (!cancellationToken.IsCancellationRequested)
        {
            lock (_lock)
            {
                if (IsInitialized)
                {
                    pa_simple_drain(_connection, out _);
                }
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (IsInitialized)
            {
                pa_simple_flush(_connection, out _);
            }
        }
    }

    public IReadOnlyList<string> GetDevices()
    {
        // PulseAudio Simple API does not support device enumeration.
        // Return empty list — device selection requires the async API.
        return [];
    }

    public bool SetDevice(string deviceName)
    {
        // PulseAudio Simple API doesn't support runtime device switching.
        // Would need to reconnect with the device name passed to pa_simple_new.
        // For now, report that this is not supported.
        Console.Error.WriteLine(
            "PulseAudio Simple API does not support runtime device switching. " +
            "Use system audio settings to change the output device.");
        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            CloseConnection();
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

    /// <summary>
    /// Clamp all samples to the valid range [-1.0, 1.0] and handle NaN/Infinity.
    /// Returns a new array if clamping was needed, otherwise returns the original.
    /// </summary>
    private static float[] ClampSamples(float[] samples)
    {
        bool needsClamp = false;
        for (int i = 0; i < samples.Length; i++)
        {
            if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) ||
                samples[i] > 1.0f || samples[i] < -1.0f)
            {
                needsClamp = true;
                break;
            }
        }

        if (!needsClamp)
            return samples;

        var clamped = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            if (float.IsNaN(s) || float.IsInfinity(s))
                clamped[i] = 0f;
            else
                clamped[i] = Math.Clamp(s, -1.0f, 1.0f);
        }
        return clamped;
    }

    // --- PulseAudio Simple API P/Invoke bindings ---

    private const int PA_STREAM_PLAYBACK = 1;
    private const int PA_SAMPLE_FLOAT32LE = 5; // PA_SAMPLE_FLOAT32LE

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

    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern int pa_simple_write(IntPtr s, IntPtr data, nuint bytes, out int error);

    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern int pa_simple_drain(IntPtr s, out int error);

    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern int pa_simple_flush(IntPtr s, out int error);

    [DllImport("libpulse.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_strerror(int error);
}
