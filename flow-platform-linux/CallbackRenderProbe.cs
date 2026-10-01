using System.Diagnostics;
using Flow.Audio;

namespace Flow.Platform.Linux;

public readonly record struct CallbackSample(long StartTicks, long ElapsedTicks, int Frames,
    bool OutputUnderflow, long AllocatedBytes);

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
    public bool Faulted => Volatile.Read(ref _fault) != 0;

    public int SampleRate => _playback.SampleRate;
    public int BlockFrames => _playback.MaxBlockFrames;

    public CallbackRenderProbe(QueuedSinePlayback playback, int sampleCapacity = 400_000, bool mute = true)
    {
        ArgumentNullException.ThrowIfNull(playback);
        if (sampleCapacity < 1 || sampleCapacity > 2_000_000) throw new ArgumentOutOfRangeException(nameof(sampleCapacity));
        _playback = playback;
        _samples = new CallbackSample[sampleCapacity];
        _mute = mute;
    }

    public void Process(Span<float> output, bool outputUnderflow = false)
    {
        long start = Stopwatch.GetTimestamp();
        long before = GC.GetAllocatedBytesForCurrentThread();
        _playback.Read(output);
        if (_mute) output.Clear();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long elapsed = Stopwatch.GetTimestamp() - start;
        if (_count < _samples.Length)
            _samples[_count++] = new(start, elapsed, output.Length / 2, outputUnderflow, allocated);
        else _dropped++;
    }

    // Native entry boundary shared with the hardware adapter and buffer tests.
    internal unsafe int ProcessNative(nint output, nuint frames, nuint flags)
    {
        if (output == 0 || frames > int.MaxValue / 2)
        {
            Volatile.Write(ref _fault, 1);
            return 2; // paAbort; no valid addressable output to clear.
        }
        var samples = new Span<float>((void*)output, (int)frames * 2);
        try
        {
            Process(samples, (flags & 4) != 0); // paOutputUnderflow
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
