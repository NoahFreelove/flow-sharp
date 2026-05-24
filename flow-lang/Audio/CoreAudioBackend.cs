using System.Runtime.InteropServices;

namespace FlowLang.Audio;

/// <summary>
/// Audio backend using Apple's AudioToolbox AudioQueue API via P/Invoke.
/// Targets macOS only — the DllImport path resolves to the system AudioToolbox.framework.
/// </summary>
/// <remarks>
/// Push-mode strategy: allocate a small pool of native AudioQueue buffers, fill and
/// enqueue them, and rely on the AudioQueue callback to recycle buffers back into a
/// free queue. <see cref="Play"/> blocks until the queue drains; <see cref="WriteChunk"/>
/// is non-draining for streaming callers.
///
/// The DllImport declarations below resolve lazily on first call — declaring this class
/// on Linux is safe as long as no code path invokes the P/Invokes. <see cref="IsAvailable"/>
/// is the only entry point that probes, and it catches <see cref="DllNotFoundException"/>.
/// </remarks>
public sealed class CoreAudioBackend : IAudioBackend
{
    private const int BufferCount = 3;
    private const int FramesPerBuffer = 4096;

    private IntPtr _audioQueue;
    private int _sampleRate;
    private int _channels;
    private bool _disposed;
    private bool _started;
    private readonly object _lock = new();

    // Free-buffer pool — buffers the AudioQueue callback has returned to us for refill.
    private readonly Queue<IntPtr> _freeBuffers = new();
    private readonly ManualResetEventSlim _bufferAvailable = new(false);

    // All allocated buffer pointers (for Dispose cleanup).
    private IntPtr[]? _allocatedBuffers;

    // Keep the managed delegate alive for the queue's lifetime so the unmanaged
    // callback function pointer remains valid.
    private AudioQueueOutputCallback? _callback;

    public string Name => "CoreAudio";
    public bool IsInitialized => _audioQueue != IntPtr.Zero;

    /// <summary>
    /// Checks whether the AudioToolbox.framework is available on this system.
    /// Returns false on Linux (DllNotFoundException) and true on macOS where the
    /// system framework resolves.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            // Harmless probe — passing IntPtr.Zero returns a non-zero OSStatus,
            // but what we care about is whether the native library resolves.
            AudioQueueDispose(IntPtr.Zero, true);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            // Any other exception means the framework loaded but something else
            // went wrong with the probe — still counts as "available".
            return true;
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
            CloseQueue();

            _sampleRate = sampleRate;
            _channels = channels;

            uint bytesPerFrame = (uint)(channels * sizeof(float));
            var format = new AudioStreamBasicDescription
            {
                mSampleRate = sampleRate,
                mFormatID = kAudioFormatLinearPCM,
                mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked,
                mBytesPerPacket = bytesPerFrame,
                mFramesPerPacket = 1,
                mBytesPerFrame = bytesPerFrame,
                mChannelsPerFrame = (uint)channels,
                mBitsPerChannel = 32,
                mReserved = 0,
            };

            // Pin the managed callback delegate by storing it on an instance field —
            // GC will keep it alive as long as this instance lives.
            _callback = OnAudioQueueBuffer;
            IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(_callback);

            int status = AudioQueueNewOutput(
                ref format,
                callbackPtr,
                IntPtr.Zero,        // userData
                IntPtr.Zero,        // callback runloop — null = AudioQueue's internal thread
                IntPtr.Zero,        // callback runloop mode
                0,                  // flags (reserved, must be 0)
                out _audioQueue);

            if (status != 0 || _audioQueue == IntPtr.Zero)
            {
                Console.Error.WriteLine($"CoreAudio: AudioQueueNewOutput failed (OSStatus={status})");
                _audioQueue = IntPtr.Zero;
                return false;
            }

            // Allocate the buffer pool. Each buffer holds FramesPerBuffer frames.
            uint bufferByteSize = (uint)(FramesPerBuffer * bytesPerFrame);
            _allocatedBuffers = new IntPtr[BufferCount];
            _freeBuffers.Clear();
            for (int i = 0; i < BufferCount; i++)
            {
                int allocStatus = AudioQueueAllocateBuffer(_audioQueue, bufferByteSize, out IntPtr buf);
                if (allocStatus != 0 || buf == IntPtr.Zero)
                {
                    Console.Error.WriteLine($"CoreAudio: AudioQueueAllocateBuffer failed (OSStatus={allocStatus})");
                    CloseQueue();
                    return false;
                }
                _allocatedBuffers[i] = buf;
                _freeBuffers.Enqueue(buf);
            }
            _bufferAvailable.Set();
            _started = false;
            return true;
        }
    }

    public void Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default)
    {
        if (samples.Length == 0)
            return;

        EnsureInitialized(sampleRate, channels);

        var clamped = AudioUtils.ClampSamples(samples);
        int samplesPerBuffer = FramesPerBuffer * channels;
        int srcOffset = 0;

        while (srcOffset < clamped.Length)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Stop();
                return;
            }

            // Wait for a free buffer.
            IntPtr buf = WaitForFreeBuffer(cancellationToken);
            if (buf == IntPtr.Zero)
            {
                // Cancelled mid-wait.
                Stop();
                return;
            }

            int remaining = clamped.Length - srcOffset;
            int chunkSamples = Math.Min(samplesPerBuffer, remaining);
            int chunkBytes = chunkSamples * sizeof(float);

            if (!CopySamplesIntoBuffer(buf, clamped, srcOffset, chunkSamples, chunkBytes))
            {
                // Bail out — error already logged in helper.
                Stop();
                return;
            }

            int enqStatus;
            lock (_lock)
            {
                if (!IsInitialized)
                    return;
                enqStatus = AudioQueueEnqueueBuffer(_audioQueue, buf, 0, IntPtr.Zero);
            }
            if (enqStatus != 0)
            {
                Console.Error.WriteLine($"CoreAudio: AudioQueueEnqueueBuffer failed (OSStatus={enqStatus})");
                Stop();
                return;
            }

            // Start the queue once we have audio queued.
            EnsureStarted();

            srcOffset += chunkSamples;
        }

        // Drain — AudioQueueStop with immediate=false blocks until the queue runs dry.
        lock (_lock)
        {
            if (IsInitialized && _started)
            {
                AudioQueueStop(_audioQueue, false);
                _started = false;
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
            throw new InvalidOperationException("No audio output available. CoreAudio init failed.");
    }

    public void WriteChunk(float[] samples, int offset, int count, int sampleRate, int channels)
    {
        if (count <= 0)
            return;

        EnsureInitialized(sampleRate, channels);

        // Build a clamped sub-buffer to avoid copying the entire source array.
        var chunk = new float[count];
        for (int i = 0; i < count; i++)
        {
            int srcIdx = offset + i;
            if (srcIdx >= samples.Length) break;
            float s = samples[srcIdx];
            if (float.IsNaN(s) || float.IsInfinity(s))
                chunk[i] = 0f;
            else
                chunk[i] = Math.Clamp(s, -1.0f, 1.0f);
        }

        IntPtr buf = WaitForFreeBuffer(CancellationToken.None);
        if (buf == IntPtr.Zero)
            return;

        int chunkBytes = count * sizeof(float);
        if (!CopySamplesIntoBuffer(buf, chunk, 0, count, chunkBytes))
            return;

        int enqStatus;
        lock (_lock)
        {
            if (!IsInitialized)
                return;
            enqStatus = AudioQueueEnqueueBuffer(_audioQueue, buf, 0, IntPtr.Zero);
        }
        if (enqStatus != 0)
        {
            Console.Error.WriteLine($"CoreAudio: AudioQueueEnqueueBuffer failed (OSStatus={enqStatus})");
            return;
        }

        EnsureStarted();
        // No drain — streaming caller controls the loop.
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (IsInitialized)
            {
                AudioQueueStop(_audioQueue, true);
                _started = false;
            }
        }
    }

    public IReadOnlyList<string> GetDevices()
    {
        // AudioQueue API doesn't expose per-device enumeration directly — that's the
        // domain of AudioHardware / CoreAudio HAL. Return empty list to match
        // PulseAudioSimpleBackend semantics.
        return [];
    }

    public bool SetDevice(string deviceName)
    {
        // Same rationale as GetDevices. Composer uses macOS System Settings → Sound
        // to change the default output device.
        Console.Error.WriteLine(
            "CoreAudio backend does not support runtime device switching. " +
            "Use System Settings → Sound to change the output device.");
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        lock (_lock)
        {
            CloseQueue();
        }
        _bufferAvailable.Dispose();
    }

    // --- Helpers ---

    private void EnsureStarted()
    {
        lock (_lock)
        {
            if (!IsInitialized || _started)
                return;
            int startStatus = AudioQueueStart(_audioQueue, IntPtr.Zero);
            if (startStatus != 0)
            {
                Console.Error.WriteLine($"CoreAudio: AudioQueueStart failed (OSStatus={startStatus})");
                return;
            }
            _started = true;
        }
    }

    /// <summary>
    /// Wait for a free AudioQueueBuffer from the recycle pool. Returns IntPtr.Zero
    /// if the wait is cancelled.
    /// </summary>
    private IntPtr WaitForFreeBuffer(CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_lock)
            {
                if (_freeBuffers.Count > 0)
                {
                    var buf = _freeBuffers.Dequeue();
                    if (_freeBuffers.Count == 0)
                        _bufferAvailable.Reset();
                    return buf;
                }
            }

            try
            {
                // Poll with a short timeout so cancellation is responsive.
                _bufferAvailable.Wait(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return IntPtr.Zero;
            }

            if (cancellationToken.IsCancellationRequested)
                return IntPtr.Zero;

            if (_disposed)
                return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Copy <paramref name="sampleCount"/> samples from <paramref name="source"/> starting at
    /// <paramref name="srcOffset"/> into the native data region of an AudioQueueBuffer and
    /// patch its mAudioDataByteSize field.
    /// </summary>
    private static bool CopySamplesIntoBuffer(IntPtr bufferPtr, float[] source, int srcOffset, int sampleCount, int byteSize)
    {
        AudioQueueBuffer header;
        try
        {
            header = Marshal.PtrToStructure<AudioQueueBuffer>(bufferPtr);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CoreAudio: failed to read AudioQueueBuffer header: {ex.Message}");
            return false;
        }

        if (header.mAudioData == IntPtr.Zero || byteSize > header.mAudioDataBytesCapacity)
        {
            Console.Error.WriteLine($"CoreAudio: AudioQueueBuffer too small ({header.mAudioDataBytesCapacity} bytes) for {byteSize}-byte chunk.");
            return false;
        }

        Marshal.Copy(source, srcOffset, header.mAudioData, sampleCount);

        // mAudioDataByteSize field offset:
        //   AudioQueueBuffer layout = [mAudioDataBytesCapacity:uint][mAudioData:IntPtr][mAudioDataByteSize:uint]
        // i.e. sizeof(uint) + IntPtr.Size from the struct base.
        int byteSizeOffset = sizeof(uint) + IntPtr.Size;
        Marshal.WriteInt32(bufferPtr, byteSizeOffset, byteSize);
        return true;
    }

    private void OnAudioQueueBuffer(IntPtr userData, IntPtr aq, IntPtr buffer)
    {
        // Runs on the AudioQueue's internal thread.
        lock (_lock)
        {
            _freeBuffers.Enqueue(buffer);
        }
        _bufferAvailable.Set();
    }

    private void CloseQueue()
    {
        // Caller MUST hold _lock.
        if (_audioQueue != IntPtr.Zero)
        {
            try { AudioQueueStop(_audioQueue, true); } catch { /* best effort */ }

            if (_allocatedBuffers != null)
            {
                for (int i = 0; i < _allocatedBuffers.Length; i++)
                {
                    if (_allocatedBuffers[i] != IntPtr.Zero)
                    {
                        try { AudioQueueFreeBuffer(_audioQueue, _allocatedBuffers[i]); } catch { /* best effort */ }
                        _allocatedBuffers[i] = IntPtr.Zero;
                    }
                }
                _allocatedBuffers = null;
            }

            try { AudioQueueDispose(_audioQueue, true); } catch { /* best effort */ }
            _audioQueue = IntPtr.Zero;
        }

        _freeBuffers.Clear();
        _bufferAvailable.Reset();
        _started = false;
        _callback = null;
    }

    // --- AudioToolbox P/Invoke + structs ---

    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

    private const uint kAudioFormatLinearPCM = 0x6C70636D; // 'lpcm'
    private const uint kAudioFormatFlagIsFloat = 1;
    private const uint kAudioFormatFlagIsPacked = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double mSampleRate;
        public uint mFormatID;
        public uint mFormatFlags;
        public uint mBytesPerPacket;
        public uint mFramesPerPacket;
        public uint mBytesPerFrame;
        public uint mChannelsPerFrame;
        public uint mBitsPerChannel;
        public uint mReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioQueueBuffer
    {
        public uint mAudioDataBytesCapacity;
        public IntPtr mAudioData;
        public uint mAudioDataByteSize;
        // Additional fields (mUserData, mPacketDescriptionCapacity, mPacketDescriptions,
        // mPacketDescriptionCount) are not needed for PCM playback.
    }

    private delegate void AudioQueueOutputCallback(IntPtr userData, IntPtr aq, IntPtr buffer);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueNewOutput(
        ref AudioStreamBasicDescription inFormat,
        IntPtr inCallbackProc,
        IntPtr inUserData,
        IntPtr inCallbackRunLoop,
        IntPtr inCallbackRunLoopMode,
        uint inFlags,
        out IntPtr outAQ);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueAllocateBuffer(IntPtr inAQ, uint inBufferByteSize, out IntPtr outBuffer);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueEnqueueBuffer(IntPtr inAQ, IntPtr inBuffer, uint inNumPacketDescs, IntPtr inPacketDescs);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueStart(IntPtr inAQ, IntPtr inStartTime);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueStop(IntPtr inAQ, [MarshalAs(UnmanagedType.U1)] bool inImmediate);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueDispose(IntPtr inAQ, [MarshalAs(UnmanagedType.U1)] bool inImmediate);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueFreeBuffer(IntPtr inAQ, IntPtr inBuffer);
}
