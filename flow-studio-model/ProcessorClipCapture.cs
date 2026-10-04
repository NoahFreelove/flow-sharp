using System.Security.Cryptography;
using System.Text;
using Flow.Audio;
using Flow.Music.Model;

namespace Flow.Studio.Model;

/// <summary>Control-thread capture of authored clip content, before track effects.
/// Placement and nudge belong to the replacement clip, not its processor input.</summary>
public static class ProcessorClipCapture
{
    internal sealed record NoteOrigin(int Placement, int Repeat, int Sequence, int Note, NoteEvent Original, NoteEvent Captured);
    /// <summary>Flatten only the selected source window. This is an authored-note
    /// transform, not a render: section gain, pan, pedal and other render controls
    /// are not baked into notes. Voices are isolated per section occurrence and
    /// sequence. Trimmed notes lose their now-inaccurate exact-duration annotation.</summary>
    public static PluginNoteInput Notes(ScoreClip clip, CompositionSnapshot composition,
        CancellationToken cancellation = default) => Notes(clip, composition, null, cancellation);

    internal static PluginNoteInput Notes(ScoreClip clip, CompositionSnapshot composition,
        Dictionary<Guid, NoteOrigin>? origins, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(composition);
        var notes = new List<NoteEvent>();
        double offset = 0, windowEnd = clip.SourceOffsetQuarters + clip.LengthQuarters;
        long candidates = 0;
        int occurrences = 0;
        for (int p = 0; p < composition.Placements.Count; p++)
        {
            cancellation.ThrowIfCancellationRequested();
            var placement = composition.Placements[p];
            double length = placement.Section.DurationQuarters;
            double end = offset + length * placement.RepeatCount;
            if (!double.IsFinite(end)) throw new ArgumentException("Source length is not finite");
            if (length > 0 && end > clip.SourceOffsetQuarters && offset < windowEnd)
            {
                int first = (int)Math.Clamp(Math.Floor((clip.SourceOffsetQuarters - offset) / length), 0, placement.RepeatCount);
                int last = (int)Math.Clamp(Math.Ceiling((windowEnd - offset) / length), 0, placement.RepeatCount);
                for (int repeat = first; repeat < last; repeat++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (++occurrences > 100000) throw new ArgumentException("Processor occurrence budget exceeded");
                    double start = offset + repeat * length;
                    for (int s = 0; s < placement.Section.Sequences.Count; s++)
                    {
                        var sequence = placement.Section.Sequences[s];
                        for (int n = 0; n < sequence.Notes.Count; n++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (++candidates > 1600000) throw new ArgumentException("Processor note intersection budget exceeded");
                            var note = sequence.Notes[n];
                            double on = start + note.OffsetQuarters;
                            double left = Math.Max(Math.Max(on, start), clip.SourceOffsetQuarters);
                            double right = Math.Min(Math.Min(on + note.DurationQuarters, start + length), windowEnd);
                            if (left >= windowEnd || left >= start + length || right < left ||
                                (right == left && note.DurationQuarters != 0)) continue;
                            if (notes.Count == 100000) throw new ArgumentException("Processor note budget exceeded");
                            string occurrence = FormattableString.Invariant($"{clip.Id:N}/{p}/{repeat}/{s}");
                            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{occurrence}/{n}/{note.Id:N}"))).AsSpan(0, 16));
                            bool trimmed = left != on || right != on + note.DurationQuarters;
                            var captured = note with { Id = id, VoiceId = occurrence + "/" + note.VoiceId,
                                OffsetQuarters = left - clip.SourceOffsetQuarters, DurationQuarters = right - left,
                                ExactDuration = trimmed ? null : note.ExactDuration };
                            notes.Add(captured);
                            origins?.Add(id, new(p, repeat, s, n, note, captured));
                        }
                    }
                }
            }
            offset = end;
        }
        return new(clip.LengthQuarters, notes);
    }

    /// <summary>Capture exact stereo frames, padding a shortened source with
    /// silence. Asset resolution/decoding is the caller's responsibility.</summary>
    public static PluginAudioInput Audio(AudioClip clip, PcmAsset asset)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(asset);
        if (clip.SampleRate != asset.SampleRate) throw new ArgumentException("Clip and source sample rates differ");
        if (clip.LengthFrames > 16 * 1024 * 1024 / 8) throw new ArgumentException("Processor audio window exceeds 16 MiB");
        var samples = new float[checked((int)clip.LengthFrames * 2)];
        asset.CopyFramesTo(clip.SourceOffsetFrames, samples);
        // Processing commits the audible window, so the replacement has no
        // additional envelope (avoids applying the same fade/gain twice).
        if (clip.Envelope is { } envelope)
            for (int frame = 0; frame < samples.Length / 2; frame++)
            {
                double gain = envelope.GainAt(clip.SourceOffsetFrames + frame);
                samples[frame * 2] = (float)(samples[frame * 2] * gain);
                samples[frame * 2 + 1] = (float)(samples[frame * 2 + 1] * gain);
            }
        return new([new(samples, clip.SampleRate)]);
    }
}
