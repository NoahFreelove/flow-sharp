using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Holds the current musical context state for a scope.
/// Each scope can override specific properties; null means "inherit from parent".
/// </summary>
public class MusicalContext
{
    /// <summary>
    /// Set of all recognized key strings (12 major + 12 minor).
    /// </summary>
    public static readonly HashSet<string> ValidKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cmajor", "Cminor",
        "Csharpmajor", "Csharpminor",
        "Dbmajor", "Dbminor",
        "Dmajor", "Dminor",
        "Dsharpmajor", "Dsharpminor",
        "Ebmajor", "Ebminor",
        "Emajor", "Eminor",
        "Fmajor", "Fminor",
        "Fsharpmajor", "Fsharpminor",
        "Gbmajor", "Gbminor",
        "Gmajor", "Gminor",
        "Gsharpmajor", "Gsharpminor",
        "Abmajor", "Abminor",
        "Amajor", "Aminor",
        "Asharpmajor", "Asharpminor",
        "Bbmajor", "Bbminor",
        "Bmajor", "Bminor"
    };

    public TimeSignatureData? TimeSignature { get; set; }
    public double? Tempo { get; set; }
    public double? Swing { get; set; }  // 0.0 to 1.0 (0.5 = straight, 0.67 = triplet swing)
    public string? Key { get; set; }    // e.g., "Cmajor", "Aminor"
    public double? Velocity { get; set; }  // 0.0 to 1.0 (null = inherit, default mf = 0.63)
    public double? Pan { get; set; }  // -1.0 (left) to 1.0 (right), null = inherit
    public double? Gain { get; set; }  // 0.0 to 2.0 (null = inherit, default 1.0 at usage site)
    public double? ReverbTime { get; set; }  // 0.0 (dry) to 30.0 (clamped ceiling), null = inherit; seconds

    /// <summary>
    /// Creates a new context with all values inherited (null).
    /// </summary>
    public MusicalContext() { }

    /// <summary>
    /// Creates a copy of this context.
    /// </summary>
    public MusicalContext Clone() => new()
    {
        TimeSignature = TimeSignature,
        Tempo = Tempo,
        Swing = Swing,
        Key = Key,
        Velocity = Velocity,
        Pan = Pan,
        Gain = Gain,
        ReverbTime = ReverbTime
    };

    /// <summary>
    /// Validates that the key is a recognized key string.
    /// Returns true if valid, false otherwise.
    /// </summary>
    public static bool IsValidKey(string key)
    {
        return ValidKeys.Contains(key);
    }

    /// <summary>
    /// Validates that a tempo value is positive.
    /// </summary>
    public static bool IsValidTempo(double tempo)
    {
        return tempo > 0;
    }

    /// <summary>
    /// Validates that a swing value is in [0.0, 1.0].
    /// </summary>
    public static bool IsValidSwing(double swing)
    {
        return swing >= 0.0 && swing <= 1.0;
    }

    /// <summary>
    /// Validates that a gain value is in [0.0, 2.0].
    /// </summary>
    public static bool IsValidGain(double gain)
    {
        return gain >= 0.0 && gain <= 2.0;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (TimeSignature != null) parts.Add($"timesig={TimeSignature}");
        if (Tempo != null) parts.Add($"tempo={Tempo}");
        if (Swing != null) parts.Add($"swing={Swing}");
        if (Key != null) parts.Add($"key={Key}");
        if (Velocity != null) parts.Add($"velocity={Velocity}");
        if (Pan != null) parts.Add($"pan={Pan}");
        if (Gain != null) parts.Add($"gain={Gain}");
        if (ReverbTime != null) parts.Add($"reverbTime={ReverbTime}");
        return $"MusicalContext({string.Join(", ", parts)})";
    }
}
