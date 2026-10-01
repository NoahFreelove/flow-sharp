using Flow.Music.Model;

namespace Flow.Audio;

/// <summary>
/// A stateless dry sine renderer. No interpreter, samples, devices or ambient session.
/// Preserves legacy sine duration, voice stealing, pan/gain and section clipping.
/// Nonzero reverb is rejected; instrument/effect routing is not implemented here.
/// </summary>
public static class SineCompositionRenderer
{
    internal sealed class Voice(NoteEvent note, long frames, long startFrame, double onsetSeconds)
    {
        public NoteEvent Note { get; } = note;
        public long Frames { get; } = frames;
        public long StartFrame { get; } = startFrame;
        public double OnsetSeconds { get; } = onsetSeconds;
        public long CutFrames { get; set; } = frames;
        public int Order { get; set; }
    }

    /// <summary>Validates the supported score/settings before any output is delivered.</summary>
    public static long GetFrameCount(CompositionSnapshot composition, RenderOptions? options = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(composition);
        options ??= new();
        options.Validate();
        long total = 0;
        foreach (var placement in composition.Placements)
        {
            cancellation.ThrowIfCancellationRequested();
            Validate(placement.Section, options, cancellation);
            total = checked(total + checked(SectionFrames(placement.Section, options.SampleRate) * placement.RepeatCount));
        }
        return total;
    }

    /// <summary>
    /// Synchronously delivers borrowed blocks: the sink must copy samples it retains.
    /// One block is reused; metadata for at most one section is prepared at a time.
    /// Cancellation or sink failure propagates; already delivered output is not rolled back.
    /// Progress runs inline after the sink accepts each block. Callbacks must return
    /// cooperatively; use host process isolation if hard termination is required.
    /// </summary>
    public static void Render(CompositionSnapshot composition, Action<ReadOnlyMemory<float>> sink,
        RenderOptions? options = null, CancellationToken cancellation = default,
        Action<RenderProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        options ??= new();
        long total = GetFrameCount(composition, options, cancellation);
        var block = new float[options.BlockFrames * 2];
        long completed = 0;
        foreach (var placement in composition.Placements)
        {
            cancellation.ThrowIfCancellationRequested();
            if (placement.RepeatCount == 0) continue;
            var section = placement.Section;
            long frames = SectionFrames(section, options.SampleRate);
            if (frames == 0) continue;
            var voices = Prepare(section, options.SampleRate, cancellation);
            var byOnset = voices.OrderBy(v => v.StartFrame).ToArray();
            var active = new List<Voice>();
            float angle = (float)((section.Settings.Pan + 1.0) * 0.25 * Math.PI);
            float left = MathF.Cos(angle) * (float)section.Settings.Gain;
            float right = MathF.Sin(angle) * (float)section.Settings.Gain;
            for (int repeat = 0; repeat < placement.RepeatCount; repeat++)
            {
                int next = 0;
                active.Clear();
                for (long start = 0; start < frames;)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(options.BlockFrames, frames - start);
                    Array.Clear(block, 0, count * 2);
                    // Sweep note intervals so each block visits only overlapping voices.
                    // Mixing stays in source order to preserve floating-point addition bits.
                    for (int i = active.Count - 1; i >= 0; i--)
                        if (active[i].StartFrame + active[i].CutFrames <= start) active.RemoveAt(i);
                    bool added = false;
                    while (next < byOnset.Length && byOnset[next].StartFrame < start + count)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var candidate = byOnset[next++];
                        if (candidate.StartFrame + candidate.CutFrames <= start) continue;
                        active.Add(candidate);
                        added = true;
                    }
                    if (added) active.Sort(static (a, b) => a.Order.CompareTo(b.Order));
                    foreach (var voice in active)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        MixVoice(voice, block.AsSpan(0, count * 2), start, options.SampleRate, left, right);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    sink(block.AsMemory(0, count * 2));
                    completed += count;
                    progress?.Invoke(new(completed, total));
                    cancellation.ThrowIfCancellationRequested();
                    start += count;
                }
            }
        }
        if (total == 0) progress?.Invoke(new(0, 0));
        cancellation.ThrowIfCancellationRequested();
    }

    // Shared sample kernel: keep operation order identical to legacy sine output.
    internal static void MixVoice(Voice voice, Span<float> output, long start,
        int sampleRate, float left, float right)
    {
        long from = Math.Max(start, voice.StartFrame);
        long to = Math.Min(start + output.Length / 2, checked(voice.StartFrame + voice.CutFrames));
        int fade = voice.CutFrames < voice.Frames
            ? (int)Math.Min((int)(0.005 * sampleRate), voice.CutFrames) : 0;
        for (long frame = from; frame < to; frame++)
        {
            // Absolute-time formula and float operation order preserve the sine baseline.
            long local = frame - voice.StartFrame;
            double time = local / (double)sampleRate;
            float sample = (float)(0.3 * voice.Note.Velocity *
                Math.Sin(2.0 * Math.PI * voice.Note.Pitch!.FrequencyHz * time));
            if (fade > 0 && local >= voice.CutFrames - fade)
                sample *= 1.0f - ((float)(local - (voice.CutFrames - fade)) / fade);
            int dest = (int)(frame - start) * 2;
            output[dest] += sample * left;
            output[dest + 1] += sample * right;
        }
    }

    internal static List<Voice> Prepare(SectionSnapshot section, int sampleRate, CancellationToken cancellation)
    {
        var all = new List<Voice>();
        double bpm = section.Settings.Bpm;
        foreach (var sequence in section.Sequences)
        {
            // VoiceId includes bar identity for compiled Flow scores: ties stop at a bar boundary.
            var restRuns = new double[sequence.Notes.Count];
            // Keep legacy left-to-right summation for exact floating-point duration parity.
            var byVoice = sequence.Notes.Select((note, index) => (note, index)).GroupBy(x => x.note.VoiceId);
            foreach (var group in byVoice)
            {
                cancellation.ThrowIfCancellationRequested();
                var notes = group.ToArray();
                for (int i = 0; i < notes.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!notes[i].note.IsTied || notes[i].note.Pitch is null) continue;
                    double rests = 0;
                    for (int j = i + 1; j < notes.Length && notes[j].note.Pitch is null; j++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        rests += notes[j].note.DurationQuarters;
                    }
                    restRuns[notes[i].index] = rests;
                }
            }
            var voices = new List<Voice>();
            for (int i = 0; i < sequence.Notes.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var note = sequence.Notes[i];
                if (note.Pitch is null) continue;
                double duration = NoteDuration.RenderQuarters(note.DurationQuarters, note.Articulation,
                    note.IsTied, restRuns[i], note.DurationOverlap, section.Settings.SustainPedal, bpm);
                // Existing synthesis divides then multiplies; positioning multiplies by seconds/quarter.
                long length = Frames((duration / bpm) * 60.0 * sampleRate);
                long onset = Frames(note.OffsetQuarters * (60.0 / bpm) * sampleRate);
                _ = checked(onset + length);
                voices.Add(new(note, length, onset, note.OffsetQuarters * (60.0 / bpm)));
            }
            ApplyPool(voices, section.Settings.VoicePoolSize ?? 32, sampleRate, cancellation);
            all.AddRange(voices);
        }
        for (int i = 0; i < all.Count; i++) all[i].Order = i;
        return all;
    }

    private static void ApplyPool(List<Voice> voices, int size, int rate, CancellationToken cancellation)
    {
        if (voices.Count <= size) return;
        var active = new List<Voice>();
        // OrderBy is stable: input order breaks ties, as in the legacy allocator.
        foreach (var voice in voices.OrderBy(v => v.OnsetSeconds))
        {
            cancellation.ThrowIfCancellationRequested();
            active.RemoveAll(v => v.OnsetSeconds + (double)v.Frames / rate <= voice.OnsetSeconds);
            if (active.Count >= size)
            {
                var oldest = active[0];
                oldest.CutFrames = Math.Min(oldest.Frames, Math.Max(0, Frames((voice.OnsetSeconds - oldest.OnsetSeconds) * rate)));
                active.RemoveAt(0);
            }
            active.Add(voice);
        }
    }

    internal static long SectionFrames(SectionSnapshot section, int rate) =>
        Frames(section.DurationQuarters * (60.0 / section.Settings.Bpm) * rate);

    private static long Frames(double value)
    {
        if (!double.IsFinite(value) || value >= long.MaxValue || value <= long.MinValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Frame position exceeds the render range");
        return (long)value;
    }

    private static void Validate(SectionSnapshot section, RenderOptions options, CancellationToken cancellation)
    {
        var settings = section.Settings;
        if (settings.ReverbSeconds is { } reverb && reverb != 0)
            throw new NotSupportedException("The sine renderer supports dry sections only; reverb requires an effect renderer.");
        if (!double.IsFinite(settings.Gain) || !double.IsFinite(settings.Pan) || settings.Pan is < -1 or > 1)
            throw new ArgumentException("Gain must be finite and pan must be in [-1, 1]");
        if ((settings.VoicePoolSize ?? 32) is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(settings.VoicePoolSize));
        long count = 0;
        foreach (var sequence in section.Sequences)
        {
            count += sequence.Notes.Count;
            if (count > options.MaxNotesPerSection) throw new InvalidOperationException("Section exceeds the render note budget");
            foreach (var note in sequence.Notes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (note.VoiceId is null || !double.IsFinite(note.Velocity) || !double.IsFinite(note.DurationOverlap) ||
                    (note.Pitch is { } pitch && (!double.IsFinite(pitch.FrequencyHz) || pitch.FrequencyHz <= 0)))
                    throw new ArgumentException("Notes need a voice identity, finite velocity/overlap and positive finite frequency");
            }
        }
    }
}
