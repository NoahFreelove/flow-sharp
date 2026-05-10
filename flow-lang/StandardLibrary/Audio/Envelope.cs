namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Envelope kind (shape) enumeration.
/// </summary>
public enum EnvelopeKind
{
    /// <summary>
    /// Attack-Release envelope (2 parameters: attack, release in seconds).
    /// </summary>
    AR,

    /// <summary>
    /// Attack-Decay-Sustain-Release envelope (4 parameters: attack, decay, sustain level, release in seconds).
    /// </summary>
    ADSR
}

/// <summary>
/// Runtime representation of an amplitude envelope.
/// </summary>
public class Envelope
{
    /// <summary>
    /// The kind of envelope (AR or ADSR).
    /// </summary>
    public EnvelopeKind Kind { get; }

    /// <summary>
    /// Envelope parameters (interpretation depends on Kind).
    /// AR: [attack, release]
    /// ADSR: [attack, decay, sustain, release]
    /// All time values in seconds, sustain is amplitude level (0-1).
    /// </summary>
    public double[] Parameters { get; }

    /// <summary>
    /// Sample rate for time-to-samples conversion.
    /// </summary>
    public int SampleRate { get; }

    public Envelope(EnvelopeKind kind, double[] parameters, int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));

        Kind = kind;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SampleRate = sampleRate;

        ValidateParameters();
    }

    private void ValidateParameters()
    {
        switch (Kind)
        {
            case EnvelopeKind.AR:
                if (Parameters.Length != 2)
                    throw new ArgumentException("AR envelope requires 2 parameters (attack, release)");
                if (Parameters[0] < 0 || Parameters[1] < 0)
                    throw new ArgumentException("AR envelope times must be non-negative");
                break;

            case EnvelopeKind.ADSR:
                if (Parameters.Length != 4)
                    throw new ArgumentException("ADSR envelope requires 4 parameters (attack, decay, sustain, release)");
                if (Parameters[0] < 0 || Parameters[1] < 0 || Parameters[3] < 0)
                    throw new ArgumentException("ADSR envelope times must be non-negative");
                if (Parameters[2] < 0 || Parameters[2] > 1)
                    throw new ArgumentException("ADSR sustain level must be between 0 and 1");
                break;
        }
    }

    public override string ToString()
    {
        var paramStr = string.Join(", ", Parameters.Select(p => p.ToString("F3")));
        return $"Envelope[{Kind}: {paramStr}]";
    }
}
