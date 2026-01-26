namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Runtime representation of an oscillator state.
/// Maintains phase continuity for seamless waveform generation across buffers.
/// </summary>
public class OscillatorState
{
    /// <summary>
    /// Current phase position (0.0 to 1.0).
    /// Wraps around at 1.0 to maintain continuity.
    /// </summary>
    public double Phase { get; set; }

    /// <summary>
    /// Oscillator frequency in Hz.
    /// </summary>
    public double Frequency { get; set; }

    /// <summary>
    /// Sample rate in Hz (e.g., 44100, 48000).
    /// </summary>
    public int SampleRate { get; set; }

    public OscillatorState(double frequency, int sampleRate)
    {
        if (frequency < 0)
            throw new ArgumentException("Frequency cannot be negative", nameof(frequency));
        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));

        Phase = 0.0;
        Frequency = frequency;
        SampleRate = sampleRate;
    }

    /// <summary>
    /// Resets the phase to zero.
    /// </summary>
    public void ResetPhase()
    {
        Phase = 0.0;
    }

    /// <summary>
    /// Advances the phase by one sample period.
    /// </summary>
    public void AdvancePhase()
    {
        Phase += Frequency / SampleRate;
        Phase -= Math.Floor(Phase);
    }

    public override string ToString()
    {
        return $"OscillatorState[{Frequency:F2} Hz, Phase={Phase:F4}, SR={SampleRate}]";
    }
}
