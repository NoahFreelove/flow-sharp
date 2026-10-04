namespace Flow.Music.Model;

/// <summary>Non-destructive gain and linear fades anchored to source frames.
/// Splits can share the same envelope without introducing a new fade at the cut.
/// Overlapping fades multiply. Fade-out reaches zero on the final source frame.</summary>
public sealed record AudioClipEnvelope
{
    public long StartFrame { get; }
    public long LengthFrames { get; }
    public double Gain { get; }
    public long FadeInFrames { get; }
    public long FadeOutFrames { get; }

    public AudioClipEnvelope(long startFrame, long lengthFrames, double gain = 1,
        long fadeInFrames = 0, long fadeOutFrames = 0)
    {
        if (startFrame < 0 || lengthFrames < 1 || !double.IsFinite(gain) || gain < 0 ||
            fadeInFrames < 0 || fadeOutFrames < 0 || fadeInFrames > lengthFrames || fadeOutFrames > lengthFrames)
            throw new ArgumentException("Invalid clip gain or fade window");
        _ = checked(startFrame + lengthFrames);
        StartFrame = startFrame; LengthFrames = lengthFrames; Gain = gain;
        FadeInFrames = fadeInFrames; FadeOutFrames = fadeOutFrames;
    }

    /// <summary>The whole source frame remains integral before subtracting the
    /// anchor, preserving precision for windows positioned beyond 2^53 frames.</summary>
    public double GainAt(long sourceFrame, double fraction = 0)
    {
        double local = (sourceFrame - StartFrame) + fraction;
        double incoming = FadeInFrames == 0 ? 1 : Math.Clamp(local / FadeInFrames, 0, 1);
        double remaining = ((StartFrame + LengthFrames - 1) - sourceFrame) - fraction;
        double outgoing = FadeOutFrames == 0 ? 1 : Math.Clamp(remaining / FadeOutFrames, 0, 1);
        return Gain * incoming * outgoing;
    }
}
