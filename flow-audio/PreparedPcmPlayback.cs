namespace Flow.Audio;

/// <summary>Fixed-duration audio windows. Same-rate clips copy exact samples;
/// differing rates use explicit linear interpolation, not musical time stretching.
/// A bounded active set and interval tree avoid full-project scans in Read/Seek.</summary>
public sealed class PreparedPcmPlayback : IPreparedAudioPlayback
{
    private readonly ScheduledAudioClip[] _clips;
    private readonly int[] _active;
    private readonly long[] _maxEnd;
    private readonly int _leaves;
    private int _count, _next;
    public int SampleRate { get; }
    public int MaxBlockFrames { get; }
    public long TotalFrames { get; }
    public long PositionFrames { get; private set; }

    public PreparedPcmPlayback(IEnumerable<ScheduledAudioClip> clips, int sampleRate, int blockFrames,
        long minimumFrames = 0, int maxOverlap = 64, int maxClips = 100_000)
    {
        if (sampleRate is < 1 or > 384000 || blockFrames is < 1 or > 65536 || minimumFrames < 0 || maxOverlap is < 1 or > 256 || maxClips < 1)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _clips = clips.Take(checked(maxClips + 1)).OrderBy(c => c.StartFrame).ToArray();
        if (_clips.Length > maxClips) throw new ArgumentException("Audio clip budget exceeded");
        foreach (var clip in _clips)
        {
            if (clip.Asset is null || clip.StartFrame >= clip.EndFrame || clip.EndFrame <= 0 || clip.SourceOffsetFrames < 0 || clip.SourceLengthFrames < 1)
                throw new ArgumentException("Invalid audio clip window");
            _ = checked(clip.SourceOffsetFrames + clip.SourceLengthFrames);
        }
        int concurrent = 0;
        // End before start at the same frame; half-open intervals.
        foreach (var boundary in _clips.SelectMany(c => new[] { (Time: c.StartFrame, Delta: 1), (Time: c.EndFrame, Delta: -1) })
                     .OrderBy(p => p.Time).ThenBy(p => p.Delta))
            if ((concurrent += boundary.Delta) > maxOverlap) throw new ArgumentException("Audio overlap budget exceeded");
        SampleRate = sampleRate; MaxBlockFrames = blockFrames;
        TotalFrames = Math.Max(minimumFrames, _clips.Select(c => c.EndFrame).DefaultIfEmpty(0).Max());
        _active = new int[maxOverlap];
        _leaves = 1; while (_leaves < _clips.Length) _leaves *= 2;
        _maxEnd = new long[_leaves * 2];
        for (int i = 0; i < _clips.Length; i++) _maxEnd[_leaves + i] = _clips[i].EndFrame;
        for (int i = _leaves - 1; i > 0; i--) _maxEnd[i] = Math.Max(_maxEnd[i * 2], _maxEnd[i * 2 + 1]);
        Reset();
    }
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames) throw new ArgumentException("Invalid stereo block");
        output.Clear();
        int frames = (int)Math.Min(output.Length / 2, TotalFrames - PositionFrames);
        for (int frame = 0; frame < frames; frame++)
        {
            long time = PositionFrames + frame;
            for (int i = _count - 1; i >= 0; i--)
                if (_clips[_active[i]].EndFrame <= time)
                { Array.Copy(_active, i + 1, _active, i, _count - i - 1); _count--; }
            while (_next < _clips.Length && _clips[_next].StartFrame <= time) _active[_count++] = _next++;
            for (int i = 0; i < _count; i++)
            {
                var clip = _clips[_active[i]];
                double local = (time - (double)clip.StartFrame) * clip.Asset.SampleRate / SampleRate;
                if (local < 0 || local >= clip.SourceLengthFrames) continue;
                long first = (long)Math.Floor(local);
                double fraction = local - first;
                double gain = clip.Envelope?.GainAt(clip.SourceOffsetFrames + first, fraction) ?? 1;
                for (int channel = 0; channel < 2; channel++)
                {
                    float a = clip.Asset.Sample(clip.SourceOffsetFrames + first, channel);
                    float b = first + 1 < clip.SourceLengthFrames ? clip.Asset.Sample(clip.SourceOffsetFrames + first + 1, channel) : 0;
                    float sample = fraction == 0 ? a : (float)(a + (b - (double)a) * fraction);
                    output[frame * 2 + channel] += gain == 1 ? sample : (float)(sample * gain);
                }
            }
        }
        PositionFrames += frames;
        return frames;
    }
    public void Seek(long frame)
    {
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        PositionFrames = frame; _count = 0;
        int low = 0, high = _clips.Length;
        while (low < high) { int mid = low + (high - low) / 2; if (_clips[mid].StartFrame < frame) low = mid + 1; else high = mid; }
        _next = low;
        if (frame < TotalFrames) Restore(1, 0, _leaves, low, frame);
    }
    private void Restore(int node, int from, int to, int before, long frame)
    {
        if (from >= before || _maxEnd[node] <= frame) return;
        if (to - from == 1) { _active[_count++] = from; return; }
        int middle = from + (to - from) / 2;
        Restore(node * 2, from, middle, before, frame);
        Restore(node * 2 + 1, middle, to, before, frame);
    }
    public void Reset() => Seek(0);
}
