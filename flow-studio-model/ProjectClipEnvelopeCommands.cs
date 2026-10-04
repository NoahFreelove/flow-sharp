using Flow.Music.Model;

namespace Flow.Studio.Model;

public static class ProjectClipEnvelopeCommands
{
    public static AudioClip WithEnvelope(AudioClip clip, AudioClipEnvelope? envelope) =>
        new(clip.Id, clip.TrackId, clip.SourceId, clip.AnchorQuarters, clip.SourceOffsetFrames,
            clip.LengthFrames, clip.SampleRate, clip.Nudge, envelope);

    public static void Set(ProjectDocument document, Guid clipId, double gain, long fadeInFrames, long fadeOutFrames) =>
        document.Edit("Change clip gain and fades", p =>
        {
            var clip = p.Arrangement.AudioClips.SingleOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown audio clip");
            var envelope = new AudioClipEnvelope(clip.SourceOffsetFrames, clip.LengthFrames, gain, fadeInFrames, fadeOutFrames);
            var changed = WithEnvelope(clip, gain == 1 && fadeInFrames == 0 && fadeOutFrames == 0 ? null : envelope);
            return changed == clip ? p : new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips, p.Arrangement.AudioClips.Select(c => c.Id == clipId ? changed : c)),
                p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings);
        });
}
