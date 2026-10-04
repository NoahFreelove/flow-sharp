namespace Flow.Audio;

/// <summary>Owned immutable decoded stereo PCM. Assets are shared across clip windows;
/// construction copies input and rejects nonfinite samples. Decode/file IO is host work.</summary>
public sealed class PcmAsset
{
    private readonly float[] _samples;
    public int SampleRate { get; }
    public int Frames => _samples.Length / 2;
    public long Bytes => (long)_samples.Length * sizeof(float);
    public PcmAsset(ReadOnlySpan<float> samples, int sampleRate, long maxBytes = 256 * 1024 * 1024)
    {
        if (sampleRate is < 1 or > 384000 || samples.Length % 2 != 0 || (long)samples.Length * sizeof(float) > maxBytes)
            throw new ArgumentException("Invalid asset format or decoded memory budget");
        foreach (float sample in samples) if (!float.IsFinite(sample)) throw new ArgumentException("PCM must be finite");
        _samples = samples.ToArray(); SampleRate = sampleRate;
    }
    public void CopyTo(Span<float> destination) => _samples.CopyTo(destination);
    /// <summary>Copy a stereo window, padding frames beyond the asset with silence.</summary>
    public void CopyFramesTo(long sourceOffsetFrames, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffsetFrames);
        if (destination.Length % 2 != 0) throw new ArgumentException("Stereo destination requires whole frames", nameof(destination));
        destination.Clear();
        if (sourceOffsetFrames >= Frames) return;
        int first = checked((int)sourceOffsetFrames * 2);
        _samples.AsSpan(first, Math.Min(destination.Length, _samples.Length - first)).CopyTo(destination);
    }
    internal float Sample(long frame, int channel) => frame < 0 || frame >= Frames ? 0 : _samples[(int)frame * 2 + channel];
}

public sealed record ScheduledAudioClip(Guid ClipId, PcmAsset Asset, long StartFrame, long EndFrame,
    long SourceOffsetFrames, long SourceLengthFrames, Flow.Music.Model.AudioClipEnvelope? Envelope = null);
