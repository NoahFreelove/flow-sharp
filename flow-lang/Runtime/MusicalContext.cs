using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Holds the current musical context state for a scope.
/// Each scope can override specific properties; null means "inherit from parent".
/// </summary>
public class MusicalContext
{
    /// <summary>
    /// Set of all recognized key strings: 17 roots × 7 modes = 119 entries.
    /// Phase 23 Plan 23-03 Task 1 (D-04) extends this from the original 34 entries
    /// (17 roots × {major, minor}) to the full 119 covering the 5 church modes
    /// (dorian, phrygian, lydian, mixolydian, locrian). Without this extension,
    /// <c>key Cdorian { ... }</c> fails the existing <see cref="IsValidKey"/> check
    /// before tuning math sees it.
    /// </summary>
    public static readonly HashSet<string> ValidKeys = BuildValidKeys();

    private static HashSet<string> BuildValidKeys()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] roots =
        {
            "C", "Csharp", "Db",
            "D", "Dsharp", "Eb",
            "E",
            "F", "Fsharp", "Gb",
            "G", "Gsharp", "Ab",
            "A", "Asharp", "Bb",
            "B",
        };
        string[] modes = { "major", "minor", "dorian", "phrygian", "lydian", "mixolydian", "locrian" };
        foreach (var root in roots)
            foreach (var mode in modes)
                set.Add(root + mode);
        return set;
    }

    public TimeSignatureData? TimeSignature { get; set; }
    public double? Tempo { get; set; }
    public double? Swing { get; set; }  // 0.0 to 1.0 (0.5 = straight, 0.67 = triplet swing)
    public string? Key { get; set; }    // e.g., "Cmajor", "Aminor"
    public double? Velocity { get; set; }  // 0.0 to 1.0 (null = inherit, default mf = 0.63)
    public double? Pan { get; set; }  // -1.0 (left) to 1.0 (right), null = inherit
    public double? Gain { get; set; }  // 0.0 to 2.0 (null = inherit, default 1.0 at usage site)
    public double? ReverbTime { get; set; }  // 0.0 (dry) to 30.0 (clamped ceiling), null = inherit; seconds

    /// <summary>
    /// Phase 28 SPEC-7: voice pool size for the current scope. null = inherit
    /// from parent; when no <c>voicePool</c> block is in scope, the
    /// SequenceRenderer applies the locked default of 32 voices.
    /// Range: 1..256. Out-of-range values rejected at interpreter time
    /// (<see cref="FlowLang.Interpreter.Interpreter"/>) with the message
    /// "Voice pool size must be between 1 and 256, got N".
    /// </summary>
    public int? VoicePoolSize { get; set; }

    /// <summary>
    /// Phase 23 D-05/D-08: render-time tuning system (top-level non-stacked field). When null,
    /// rendering uses the byte-identical 12-TET path per Pitfall 6 short-circuit. The
    /// FlowEngine bridge resolves the active <c>enable justIntonation;</c> /
    /// <c>enable pythagorean;</c> / <c>enable equalTemperament;</c> pragma into this field
    /// ONCE before <c>_interpreter.Execute(program)</c>. D-07 REPL persistence: pragma absence
    /// does NOT reset previous tuning across REPL evaluations.
    /// </summary>
    public TuningSystem? Tuning { get; set; }

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
        ReverbTime = ReverbTime,
        Tuning = Tuning,
        VoicePoolSize = VoicePoolSize
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
        if (Tuning != null) parts.Add($"tuning={Tuning}");
        return $"MusicalContext({string.Join(", ", parts)})";
    }
}
