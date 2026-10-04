using System.Diagnostics;
using System.Runtime.InteropServices;
using Flow.Audio;

namespace Flow.Platform.Linux;

public readonly record struct NativeCallbackTiming(double CurrentSeconds, double OutputDacSeconds);

public readonly record struct CallbackSample(long StartTicks, long ElapsedTicks, int Frames,
    bool OutputUnderflow, long AllocatedBytes, NativeCallbackTiming? NativeTiming = null, ulong StatusFlags = 0, bool InjectedStall = false);

/// <summary>
/// Preallocated instrumentation for one audio consumer. Process is callback-owned;
/// Capture may only run after callbacks have stopped/joined. Samples include startup.
/// Muting happens after rendering so silent device runs exercise the actual DSP path.
/// This measures the managed body, not native dispatch or GC before callback entry.
/// </summary>
public sealed class CallbackRenderProbe
{
    private readonly QueuedSinePlayback _playback;
    private readonly CallbackSample[] _samples;
    private readonly bool _mute;
    private int _count;
    private long _dropped;
    private int _fault;
    private readonly int _stallAfterCallbacks;
    private readonly int _stallMilliseconds;
    private readonly bool _captureThreadIdentity;
    private int _nativeThreadId;
    public int NativeThreadId => Volatile.Read(ref _nativeThreadId);
    [DllImport("libc", EntryPoint = "gettid")]
    private static extern int GetThreadId();
    private int _callbacks;
    private int _injectedStalls;
    public int InjectedStalls => Volatile.Read(ref _injectedStalls);
    public bool Faulted => Volatile.Read(ref _fault) != 0;

    public int SampleRate => _playback.SampleRate;
    public int BlockFrames => _playback.MaxBlockFrames;

    public CallbackRenderProbe(QueuedSinePlayback playback, int sampleCapacity = 400_000, bool mute = true,
        int stallAfterCallbacks = -1, int stallMilliseconds = 0, bool captureThreadIdentity = false)
    {
        ArgumentNullException.ThrowIfNull(playback);
        if (sampleCapacity < 1 || sampleCapacity > 2_000_000) throw new ArgumentOutOfRangeException(nameof(sampleCapacity));
        if (stallAfterCallbacks < -1 || stallMilliseconds < 0 || stallMilliseconds > 1000 ||
            (stallAfterCallbacks == -1) != (stallMilliseconds == 0) || (stallMilliseconds > 0 && !mute))
            throw new ArgumentException("Diagnostic starvation requires muted output, a callback index and a 1–1000 ms stall");
        if (captureThreadIdentity && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Native diagnostic thread identity requires Linux");
        _captureThreadIdentity = captureThreadIdentity;
        _stallAfterCallbacks = stallAfterCallbacks;
        _stallMilliseconds = stallMilliseconds;
        _playback = playback;
        _samples = new CallbackSample[sampleCapacity];
        _mute = mute;
    }

    public void Process(Span<float> output, bool outputUnderflow = false)
    {
        ProcessCore(output, Stopwatch.GetTimestamp(), outputUnderflow ? 4UL : 0, null);
    }

    private void ProcessCore(Span<float> output, long start, ulong flags, NativeCallbackTiming? timing)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        _playback.Read(output);
        if (_mute) output.Clear();
        // Fault injection belongs only to this diagnostic probe. Never used by
        // normal measurements or the playback engine; never fabricates flags.
        bool injected = _callbacks++ == _stallAfterCallbacks;
        if (injected)
        {
            Interlocked.Increment(ref _injectedStalls);
            Thread.Sleep(_stallMilliseconds);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long elapsed = Stopwatch.GetTimestamp() - start;
        if (_count < _samples.Length)
            _samples[_count++] = new(start, elapsed, output.Length / 2, (flags & 4) != 0, allocated, timing, flags, injected);
        else _dropped++;
    }

    // Native entry boundary shared with the hardware adapter and buffer tests.
    internal unsafe int ProcessNative(nint output, nuint frames, nuint flags, nint timeInfo = 0)
    {
        long start = Stopwatch.GetTimestamp();
        if (output == 0 || frames > int.MaxValue / 2)
        {
            Volatile.Write(ref _fault, 1);
            return 2; // paAbort; no valid addressable output to clear.
        }
        var samples = new Span<float>((void*)output, (int)frames * 2);
        try
        {
            // Opt-in diagnostic identity, one syscall on the first native callback only.
            if (_captureThreadIdentity && _nativeThreadId == 0)
                Volatile.Write(ref _nativeThreadId, GetThreadId());
            NativeCallbackTiming? timing = null;
            if (timeInfo != 0)
            {
                var native = *(PortAudioNative.CallbackTimeInfo*)timeInfo;
                if (double.IsFinite(native.CurrentTime) && double.IsFinite(native.OutputDacTime))
                    timing = new(native.CurrentTime, native.OutputDacTime);
            }
            ProcessCore(samples, start, (ulong)flags, timing);
            return 0; // paContinue, including stopped/EOF silence.
        }
        catch
        {
            samples.Clear();
            Volatile.Write(ref _fault, 1);
            return 2; // No exception may cross into native code.
        }
    }

    public (CallbackSample[] Samples, long Dropped) Capture() => (_samples.AsSpan(0, _count).ToArray(), _dropped);
}
