using Flow.Music.Model;

namespace Flow.Audio;

/// <summary>The legacy sounding-duration policy, shared by snapshot and Flow renderers.</summary>
public static class NoteDuration
{
    public const double SustainTailSeconds = 2.0;

    public static double RenderQuarters(double authoredQuarters, NoteArticulation articulation,
        bool tied, double followingRestQuarters, double overlap, bool sustainPedal, double bpm)
    {
        double duration = authoredQuarters;
        switch (articulation)
        {
            case NoteArticulation.Staccato:
            case NoteArticulation.Marcato: duration *= 0.25; break;
            case NoteArticulation.Legato: duration *= 1.10; break;
        }
        if (tied)
        {
            duration += followingRestQuarters;
            if (!sustainPedal) duration += (0.1 / 60.0) * bpm;
        }
        if (overlap > 0) duration *= 1.0 + overlap;
        if (sustainPedal) duration += (SustainTailSeconds * bpm) / 60.0;
        return duration;
    }
    /// <summary>Project-map rendering separates musical duration from fixed-time
    /// tie/pedal extensions so tempo changes never convert milliseconds into beats.</summary>
    public static (double Quarters, double FixedSeconds) ProjectDuration(double authoredQuarters,
        NoteArticulation articulation, bool tied, double followingRestQuarters, double overlap, bool sustainPedal)
    {
        double duration = articulation switch
        {
            NoteArticulation.Staccato or NoteArticulation.Marcato => authoredQuarters * 0.25,
            NoteArticulation.Legato => authoredQuarters * 1.10,
            _ => authoredQuarters,
        };
        if (tied) duration += followingRestQuarters;
        double seconds = tied && !sustainPedal ? 0.1 : 0;
        if (overlap > 0) { duration *= 1 + overlap; seconds *= 1 + overlap; }
        if (sustainPedal) seconds += SustainTailSeconds;
        return (duration, seconds);
    }
}
