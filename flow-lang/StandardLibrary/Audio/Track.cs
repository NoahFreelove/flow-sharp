namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Runtime representation of a track - a collection of voices with timeline positioning.
///
/// Legacy DAW-style multitrack layer (pre-Phase-25). Superseded by the Song/Section
/// render path (<see cref="FlowLang.StandardLibrary.Audio.SongRenderer"/>) as the
/// canonical arrangement primitive — kept as a usable lower-level manual-mixing
/// surface (shares the <see cref="Voice"/> type).
/// </summary>
public class Track
{
    /// <summary>
    /// Collection of voices on this track.
    /// </summary>
    public List<Voice> Voices { get; }

    /// <summary>
    /// Track offset on timeline in beats.
    /// </summary>
    public double OffsetBeats { get; set; }

    /// <summary>
    /// Track-level gain multiplier (1.0 = unity gain).
    /// </summary>
    public double Gain { get; set; }

    /// <summary>
    /// Track-level pan position (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public double Pan { get; set; }

    /// <summary>
    /// Sample rate for rendering.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Number of channels for rendering.
    /// </summary>
    public int Channels { get; }

    public Track(int sampleRate, int channels)
    {
        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));
        if (channels < 1)
            throw new ArgumentException("Channel count must be at least 1", nameof(channels));

        Voices = new List<Voice>();
        OffsetBeats = 0.0;
        Gain = 1.0;
        Pan = 0.0;
        SampleRate = sampleRate;
        Channels = channels;
    }

    public override string ToString()
    {
        return $"Track[{Voices.Count} voices, Offset={OffsetBeats:F2} beats, Gain={Gain:F2}, {SampleRate}Hz, {Channels}ch]";
    }
}
