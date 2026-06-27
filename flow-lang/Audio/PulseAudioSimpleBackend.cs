using System.Runtime.InteropServices;

namespace FlowLang.Audio;

/// <summary>
/// Audio backend using PulseAudio Simple API via P/Invoke.
/// Works with both native PulseAudio and PipeWire's PulseAudio compatibility layer.
/// </summary>
/// <remarks>
/// P/Invoke declarations are in <see cref="LibPulse"/> (audit §8.7 — extracted from
/// the private copy that previously lived here to share with
/// <see cref="PulseAudioCaptureBackend"/>).
/// </remarks>
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
            LibPulse.GetErrorString(0);
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

            var sampleSpec = new LibPulse.PaSampleSpec
            {
                format = LibPulse.PA_SAMPLE_FLOAT32LE,
                rate = (uint)sampleRate,
                channels = (byte)channels
            };

            int error;
            _connection = LibPulse.pa_simple_new(
                IntPtr.Zero,       // Use default server
                "flow-lang",       // Application name
                LibPulse.PA_STREAM_PLAYBACK,
                IntPtr.Zero,       // Use default device
                "playback",        // Stream description
                ref sampleSpec,
                IntPtr.Zero,       // Use default channel map
                IntPtr.Zero,       // Use default buffering attributes
                out error);

            if (_connection == IntPtr.Zero)
            {
                var errorMsg = LibPulse.GetErrorString(error);
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
        var clamped = AudioUtils.ClampSamples(samples);

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
                    result = LibPulse.pa_simple_write(_connection, ptr, (nuint)writeSize, out error);
                }

                if (result < 0)
                {
                    var errorMsg = LibPulse.GetErrorString(error);
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
                    LibPulse.pa_simple_drain(_connection, out _);
                }
            }
        }
    }

    public void EnsureInitialized(int sampleRate, int channels)
    {
        lock (_lock)
        {
            if (IsInitialized && sampleRate == _sampleRate && channels == _channels)
                return;
        }

        if (!Initialize(sampleRate, channels))
            throw new InvalidOperationException(
                "No audio output available. Install PipeWire or PulseAudio.");
    }

    // Reusable scratch for WriteChunk — called continuously from the single
    // streaming thread during live playback; a per-call allocation is steady-state
    // Gen0 garbage on the audio-feed path (audit 2026-06-09 §8.6).
    private float[] _chunkScratch = Array.Empty<float>();

    public void WriteChunk(float[] samples, int offset, int count, int sampleRate, int channels)
    {
        if (count <= 0)
            return;

        EnsureInitialized(sampleRate, channels);

        // Clamp samples in-place check; write from a clamped sub-buffer
        // to avoid allocating a full copy of the source array.
        if (_chunkScratch.Length < count)
            _chunkScratch = new float[count];
        var chunk = _chunkScratch;
        for (int i = 0; i < count; i++)
        {
            int srcIdx = offset + i;
            float s = srcIdx < samples.Length ? samples[srcIdx] : 0f;
            if (float.IsNaN(s) || float.IsInfinity(s))
                chunk[i] = 0f;
            else
                chunk[i] = Math.Clamp(s, -1.0f, 1.0f);
        }

        var handle = GCHandle.Alloc(chunk, GCHandleType.Pinned);
        try
        {
            int writeBytes = count * sizeof(float);

            lock (_lock)
            {
                if (!IsInitialized)
                    return;

                int error;
                var ptr = handle.AddrOfPinnedObject();
                int result = LibPulse.pa_simple_write(_connection, ptr, (nuint)writeBytes, out error);

                if (result < 0)
                {
                    var errorMsg = LibPulse.GetErrorString(error);
                    throw new InvalidOperationException($"PulseAudio write error: {errorMsg}");
                }
            }
            // Note: No pa_simple_drain -- streaming loop feeds continuously.
        }
        finally
        {
            handle.Free();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (IsInitialized)
            {
                LibPulse.pa_simple_flush(_connection, out _);
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
            LibPulse.pa_simple_free(_connection);
            _connection = IntPtr.Zero;
        }
    }
}
