namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Runtime representation of a voice - a positioned audio clip on a timeline.
/// </summary>
public class Voice
{
    /// <summary>
    /// The audio buffer containing the clip data.
    /// </summary>
    public AudioBuffer Buffer { get; }

    /// <summary>
    /// Position on timeline in beats.
    /// </summary>
    public double OffsetBeats { get; set; }

    /// <summary>
    /// Gain multiplier (1.0 = unity gain).
    /// </summary>
    public double Gain { get; set; }

    /// <summary>
    /// Pan position (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public double Pan { get; set; }

    public Voice(AudioBuffer buffer, double offsetBeats)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        OffsetBeats = offsetBeats;
        Gain = 1.0;
        Pan = 0.0;
    }

    public override string ToString()
    {
        return $"Voice[Offset={OffsetBeats:F2} beats, Gain={Gain:F2}, Pan={Pan:F2}, Duration={Buffer.Frames} frames]";
    }
}
