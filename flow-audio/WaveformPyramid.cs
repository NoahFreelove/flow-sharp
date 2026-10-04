namespace Flow.Audio;

public readonly record struct WaveformPeak(float LeftMin, float LeftMax, float RightMin, float RightMax);
public sealed class WaveformLevel
{
    public long FramesPerPeak { get; }
    public IReadOnlyList<WaveformPeak> Peaks { get; }
    internal WaveformLevel(long framesPerPeak, WaveformPeak[] peaks)
    { FramesPerPeak = framesPerPeak; Peaks = Array.AsReadOnly(peaks); }
}
/// <summary>Immutable worker-built min/max pyramid for UI waveform drawing.
/// No callback work; no PCM retained. Levels contain actual extrema, including the last partial bucket.</summary>
public sealed class WaveformPyramid
{
    public IReadOnlyList<WaveformLevel> Levels { get; }
    public int Frames { get; }
    private WaveformPyramid(int frames, List<WaveformLevel> levels) { Frames = frames; Levels = levels.AsReadOnly(); }
    public static WaveformPyramid Build(PcmAsset asset, int baseFramesPerPeak = 256,
        long maxBytes = 32 * 1024 * 1024, CancellationToken cancellation = default)
    {
        if (baseFramesPerPeak < 1 || maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(baseFramesPerPeak));
        long count = ((long)asset.Frames + baseFramesPerPeak - 1) / baseFramesPerPeak, total = 0;
        for (long n = count; n > 0; n = n == 1 ? 0 : (n + 1) / 2) total = checked(total + n);
        if (total * 16 > maxBytes) throw new ArgumentException("Waveform exceeds peak memory budget");
        var levels = new List<WaveformLevel>();
        var peaks = new WaveformPeak[(int)count];
        for (int bucket = 0; bucket < peaks.Length; bucket++)
        {
            cancellation.ThrowIfCancellationRequested();
            long begin = (long)bucket * baseFramesPerPeak, end = Math.Min(asset.Frames, begin + baseFramesPerPeak);
            float lmin = float.PositiveInfinity, lmax = float.NegativeInfinity, rmin = lmin, rmax = lmax;
            for (long frame = begin; frame < end; frame++)
            {
                if ((frame & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                float l = asset.Sample(frame, 0), r = asset.Sample(frame, 1);
                lmin = Math.Min(lmin, l); lmax = Math.Max(lmax, l); rmin = Math.Min(rmin, r); rmax = Math.Max(rmax, r);
            }
            peaks[bucket] = new(lmin, lmax, rmin, rmax);
        }
        long width = baseFramesPerPeak;
        while (peaks.Length > 0)
        {
            cancellation.ThrowIfCancellationRequested(); levels.Add(new(width, peaks));
            if (peaks.Length == 1) break;
            var next = new WaveformPeak[(peaks.Length + 1) / 2];
            for (int i = 0; i < next.Length; i++)
            {
                if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                var a = peaks[i * 2]; var b = peaks[Math.Min(i * 2 + 1, peaks.Length - 1)];
                next[i] = new(Math.Min(a.LeftMin, b.LeftMin), Math.Max(a.LeftMax, b.LeftMax), Math.Min(a.RightMin, b.RightMin), Math.Max(a.RightMax, b.RightMax));
            }
            peaks = next; width *= 2;
        }
        cancellation.ThrowIfCancellationRequested();
        return new(asset.Frames, levels);
    }
}
