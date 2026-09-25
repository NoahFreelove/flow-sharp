namespace Flow.Audio;

/// <summary>One render's immutable configuration. Output is interleaved stereo float PCM.</summary>
public sealed record RenderOptions(int SampleRate = 44100, int BlockFrames = 4096,
    int MaxNotesPerSection = 1_000_000)
{
    internal void Validate()
    {
        if (SampleRate < 1 || SampleRate > 384000) throw new ArgumentOutOfRangeException(nameof(SampleRate));
        if (BlockFrames < 1 || BlockFrames > 65536) throw new ArgumentOutOfRangeException(nameof(BlockFrames));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNotesPerSection);
    }
}

/// <summary>Delivered output frames, including repeats; not score seconds or preparation progress.</summary>
public readonly record struct RenderProgress(long CompletedFrames, long TotalFrames);
