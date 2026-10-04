namespace Flow.Audio;

/// <summary>Two independent prepared sources on one track bus. Owns both cursors.</summary>
public sealed class PreparedMixedPlayback : IPreparedAudioPlayback
{
    private readonly IPreparedAudioPlayback _first, _second;
    private readonly float[] _scratch;
    public int SampleRate => _first.SampleRate;
    public int MaxBlockFrames => _first.MaxBlockFrames;
    public long TotalFrames { get; }
    public long PositionFrames { get; private set; }
    public PreparedMixedPlayback(IPreparedAudioPlayback first, IPreparedAudioPlayback second)
    {
        if (ReferenceEquals(first, second) || first.SampleRate != second.SampleRate || first.MaxBlockFrames != second.MaxBlockFrames)
            throw new ArgumentException("Mix sources must be independent with matching format");
        _first = first; _second = second;
        TotalFrames = Math.Max(first.TotalFrames, second.TotalFrames);
        _scratch = new float[MaxBlockFrames * 2]; Reset();
    }
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames) throw new ArgumentException("Invalid stereo block");
        int frames = (int)Math.Min(output.Length / 2, TotalFrames - PositionFrames);
        _first.Read(output); _second.Read(_scratch.AsSpan(0, output.Length));
        for (int i = 0; i < output.Length; i++) output[i] += _scratch[i];
        PositionFrames += frames;
        return frames;
    }
    public void Seek(long frame)
    {
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        _first.Seek(Math.Min(frame, _first.TotalFrames)); _second.Seek(Math.Min(frame, _second.TotalFrames)); PositionFrames = frame;
    }
    public void Reset() => Seek(0);
}
