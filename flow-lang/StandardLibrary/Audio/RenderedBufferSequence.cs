namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Collects owned, completed section buffers and assembles one output in linear
/// time. Inputs must not be mutated until Build returns. Repeats retain one source
/// buffer, not one copy per repetition. This is not a streaming renderer yet.
/// </summary>
internal sealed class RenderedBufferSequence(int sampleRate, int channels)
{
    private readonly List<(AudioBuffer Buffer, int Repetitions)> _parts = new();
    private int _frames;

    public void Add(AudioBuffer buffer, int repetitions)
    {
        // Preserve legacy zero/negative-repeat behavior: render the section, but
        // contribute no audio. Empty sections likewise require no storage or work.
        if (repetitions <= 0 || buffer.Frames == 0) return;
        if (buffer.SampleRate != sampleRate || buffer.Channels != channels)
            throw new ArgumentException("Section buffer format does not match the output", nameof(buffer));
        var frames = checked((long)_frames + (long)buffer.Frames * repetitions);
        if (frames * channels > Array.MaxLength)
            throw new InvalidOperationException("Rendered song exceeds the contiguous audio buffer limit");
        _frames = checked((int)frames);
        _parts.Add((buffer, repetitions));
    }

    public AudioBuffer Build(Action? checkpoint = null)
    {
        checkpoint?.Invoke();
        var output = new AudioBuffer(_frames, channels, sampleRate);
        int destination = 0;
        foreach (var (buffer, repetitions) in _parts)
            for (int repeat = 0; repeat < repetitions; repeat++)
                for (int source = 0; source < buffer.Data.Length;)
                {
                    checkpoint?.Invoke();
                    int count = Math.Min(65_536, buffer.Data.Length - source);
                    Array.Copy(buffer.Data, source, output.Data, destination, count);
                    source += count;
                    destination += count;
                }
        return output;
    }
}
