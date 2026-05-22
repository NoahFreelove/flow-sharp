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
    /// Sustain pedal — when true, notes within this context render with their
    /// audio buffer extended by <see cref="SustainTailSeconds"/> so they ring
    /// through subsequent notes, mimicking a piano's sustain pedal. The flag
    /// itself is a stack via the musical-context push/pop, so nested
    /// <c>sustainPedal { ... }</c> blocks compose naturally with other context.
    /// </summary>
    public bool? SustainPedal { get; set; }

    /// <summary>
    /// Locked default sustain tail when SustainPedal is active. 2 seconds matches
    /// a real piano's perceptual decay envelope without creating mud — long
    /// enough that held notes ring through the next 1-2 beats at typical 100-130
    /// BPM tempos, short enough that overlapping sustained notes don't pile up
    /// into volume swells that mask attacks (perceived as tempo drift).
    /// </summary>
    public const double SustainTailSeconds = 2.0;

    /// <summary>
    /// Phase 32 D-12 transitional shim: the Phase 23 scalar field. SUPERSEDED by
    /// <see cref="TuningStack"/> + <see cref="ActiveTuning"/>. Marked
    /// <see cref="ObsoleteAttribute"/> so any unmigrated read site surfaces as a
    /// compile warning; Phase 23 readers (FlowEngine, SongRenderer, MidiExport,
    /// HarmonyFunctions) are migrated to <see cref="ActiveTuning"/> in Plan 32-05
    /// Task 2. This field is no longer read by any production code path — kept
    /// transitionally because direct deletion broke the Phase 23 readers' compile
    /// step in Task 1 (the migrations live in Task 2 per the plan).
    /// </summary>
    [Obsolete("Phase 32 D-12: use TuningStack + ActiveTuning. Scheduled for removal after Plan 32-06 lands.")]
    public TuningSystem? Tuning { get; set; }

    /// <summary>
    /// Phase 32 CONTEXT D-12 (supersedes Phase 23 D-05): render-time tuning context as
    /// a push/pop stack. Phase 23's scalar <c>TuningSystem? Tuning</c> field is replaced
    /// by this stack of <see cref="RenderTuning"/> values to support the new
    /// <c>tuning t { ... }</c> musical-context block (Plan 32-06) layered on top of the
    /// existing Phase 23 file-scope pragma (<c>enable justIntonation;</c> etc.).
    ///
    /// Stack semantics:
    /// <list type="bullet">
    ///   <item>File-scope pragmas push EXACTLY ONCE at engine startup via
    ///   <see cref="ExecutionContext.SetFileScopeTuning"/> — the bottom frame. Never
    ///   popped at REPL boundary (D-08 sticky pragma carried over from Phase 23).</item>
    ///   <item>Block forms (Plan 32-06's <c>tuning t { ... }</c>) push above via
    ///   <see cref="ExecutionContext.PushTuning"/> and pop via
    ///   <see cref="ExecutionContext.PopTuning"/>. REPL eval boundary force-pops via
    ///   <see cref="ExecutionContext.ResetBlockTuningStack"/> back to the file-scope
    ///   frame (D-14 ephemeral blocks). Pitfall 2 coexistence.</item>
    ///   <item><see cref="ActiveTuning"/> returns the top-of-stack
    ///   <see cref="RenderTuning"/>, falling back to <see cref="RenderTuning.Default"/>
    ///   (12-TET) when the stack is empty.</item>
    /// </list>
    /// All Phase 23 readers consume <see cref="ActiveTuning"/> per RESEARCH Pitfall 1
    /// (single resolution accessor; the stack itself is mutation-only via the
    /// ExecutionContext entry points).
    /// </summary>
    public Stack<RenderTuning> TuningStack { get; } = new Stack<RenderTuning>();

    /// <summary>
    /// Phase 32 D-12 single resolution accessor: returns the top-of-stack
    /// <see cref="RenderTuning"/>, or <see cref="RenderTuning.Default"/> (12-TET) if
    /// the stack is empty. This is the SINGLE read path all Phase 23 reader sites
    /// (SongRenderer.ResolveRenderTuning, MidiExport D-13, HarmonyFunctions enharmonic
    /// guard, VocalizationFunctions sing) now consume — see RESEARCH §"Readers of
    /// MusicalContext.Tuning". Inheritance across the call stack is resolved by
    /// <see cref="ExecutionContext.GetMusicalContext"/> walking frames top-to-bottom.
    /// </summary>
    public RenderTuning ActiveTuning => TuningStack.Count > 0 ? TuningStack.Peek() : RenderTuning.Default;

    /// <summary>
    /// Creates a new context with all values inherited (null).
    /// </summary>
    public MusicalContext() { }

    /// <summary>
    /// Creates a copy of this context. The <see cref="TuningStack"/> is deep-cloned
    /// (two-reversal trick on <see cref="Stack{T}"/> to preserve order); each frame's
    /// <see cref="RenderTuning"/> is a struct so reference issues do not apply.
    /// </summary>
    public MusicalContext Clone()
    {
        var clone = new MusicalContext
        {
            TimeSignature = TimeSignature,
            Tempo = Tempo,
            Swing = Swing,
            Key = Key,
            Velocity = Velocity,
            Pan = Pan,
            Gain = Gain,
            ReverbTime = ReverbTime,
            VoicePoolSize = VoicePoolSize,
            SustainPedal = SustainPedal
        };
        // Stack<T> enumeration order is top-to-bottom; the single-arg ctor preserves
        // that order, so naive `new Stack<T>(original)` would REVERSE the stack.
        // Two-reversal trick: copy to a temp Stack (now reversed), then construct
        // the clone from that temp (reversed again → original order).
        if (TuningStack.Count > 0)
        {
            var reversed = new Stack<RenderTuning>(TuningStack);
            foreach (var rt in reversed)
                clone.TuningStack.Push(rt);
        }
        return clone;
    }

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
        if (TuningStack.Count > 0) parts.Add($"tuning={ActiveTuning} (stack depth {TuningStack.Count})");
        return $"MusicalContext({string.Join(", ", parts)})";
    }
}
