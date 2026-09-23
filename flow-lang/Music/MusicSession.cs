using FlowLang.Diagnostics;
using FlowLang.Runtime;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Music;

/// <summary>
/// Music state for one execution context: the section registry, style packs, SFZ
/// registries and the memoized musical-context resolution. Attached through
/// <see cref="ExecutionContext.GetExtension{T}"/>; composer code reaches it through
/// the members in <see cref="MusicContextExtensions"/>.
/// </summary>
public sealed class MusicSession : ISessionExtension
{
    /// <summary>
    /// Section registry keyed by section name. Phase 36 (D-36-18): a list per name so
    /// overloads with different pattern signatures coexist, in declaration order (the
    /// overload resolver's tiebreaker); a plain section is a list of one.
    /// </summary>
    public Dictionary<string, List<SectionData>> SectionRegistry { get; } = new();

    /// <summary>
    /// Phase 36 (D-36-12) style-pack registry keyed by interned Symbol values. Filled at
    /// engine init from the shipped and user packs (last write wins); <c>registerStyle</c>
    /// mutates it at runtime.
    /// </summary>
    public Dictionary<Value, DictData> StyleRegistry { get; } = new();

    /// <summary>Symbol names whose "user pack overrides shipped" advisory already fired.</summary>
    public HashSet<string> StyleOverrideAdvisoriesEmitted { get; } = new();

    /// <summary>
    /// True while the shipped packs load, so re-registering a shipped pack is not
    /// reported as a user override.
    /// </summary>
    public bool SuppressStyleOverrideAdvisory { get; set; }

    /// <summary>Set by <c>use "@sfz"</c>; gates <c>loadSfz</c> and <c>sampler:NAME</c>.</summary>
    public bool SfzEnabled { get; set; }

    /// <summary>GM Symbol → relative .sfz path map from <c>sfz.flow</c>.</summary>
    public Dictionary<Value, string> SfzInstruments { get; } = new();

#if !FLOW_WEB
    /// <summary>
    /// Variable name → bound SFZ patch, written when an <c>Sfz</c> variable is declared
    /// (last binding wins) and read by the <c>sampler:NAME</c> instrument. Stripped on Web
    /// with the SFZ surface (D-47-08).
    /// </summary>
    public Dictionary<string, StandardLibrary.Audio.Sfz.SfzData> SfzPatchRegistry { get; } = new();
#endif

    /// <summary>One-shot SFZ advisory dedup keys, per context.</summary>
    public HashSet<string> SfzDiagnostics { get; } = new();

    /// <summary>
    /// <c>sfz_root</c> read once per context at the first <c>loadSfz</c>, so config edits
    /// during a run cannot change an in-flight render. Null until read.
    /// </summary>
    public string? ResolvedSfzRoot { get; set; }

    private MusicalContext? _resolved;
    private long _resolvedVersion = -1;

    /// <summary>
    /// Flat view of <see cref="SectionRegistry"/> for consumers that do not know about
    /// overloads: the last-registered entry per name.
    /// </summary>
    public Dictionary<string, SectionData> SectionRegistryFlat()
    {
        var flat = new Dictionary<string, SectionData>();
        foreach (var (key, list) in SectionRegistry)
        {
            if (list.Count > 0)
                flat[key] = list[list.Count - 1];
        }
        return flat;
    }

    /// <summary>
    /// Resolves the musical context by walking the active frames innermost first; the
    /// first non-null value of each property wins. The tuning stack adopts the first
    /// non-empty stack. Unresolved tempo and time signature fall back to the config
    /// defaults, then 120 BPM and 4/4; swing falls back to 0.5.
    /// The result is memoized until scope state changes and must not be mutated.
    /// </summary>
    public MusicalContext Resolve(ExecutionContext context)
    {
        if (_resolved is not null && _resolvedVersion == context.ScopeVersion)
            return _resolved;

        var resolved = new MusicalContext();
        bool tuningResolved = false;
        foreach (var frame in context.ActiveFrames)
        {
            var scope = frame.GetScope<MusicalContext>();
            if (scope != null)
            {
                resolved.TimeSignature ??= scope.TimeSignature;
                resolved.Tempo ??= scope.Tempo;
                resolved.Swing ??= scope.Swing;
                resolved.Key ??= scope.Key;
                resolved.Velocity ??= scope.Velocity;
                resolved.Pan ??= scope.Pan;
                resolved.Gain ??= scope.Gain;
                resolved.ReverbTime ??= scope.ReverbTime;
                // Phase 32 D-12: file-scope pragmas sit on the global frame and block
                // forms push above them, so the innermost non-empty stack wins.
                if (!tuningResolved && scope.TuningStack.Count > 0)
                {
                    foreach (var rt in new Stack<RenderTuning>(scope.TuningStack))
                        resolved.TuningStack.Push(rt);
                    tuningResolved = true;
                }
                // null means "no override": the renderer applies the voice-pool default
                // of 32, note streams default to octave 4, and the pedal is off.
                resolved.VoicePoolSize ??= scope.VoicePoolSize;
                resolved.DefaultOctave ??= scope.DefaultOctave;
                resolved.SustainPedal ??= scope.SustainPedal;
            }
            if (resolved.TimeSignature != null && resolved.Tempo != null
                && resolved.Swing != null && resolved.Key != null
                && resolved.Velocity != null && resolved.Pan != null
                && resolved.Gain != null && resolved.ReverbTime != null
                && tuningResolved && resolved.VoicePoolSize != null
                && resolved.DefaultOctave != null)
                break;
        }
        // REQ-4: block value, then config.toml, then the baked default.
        resolved.Tempo ??= context.Session.Config.DefaultTempo.HasValue
            ? (double)context.Session.Config.DefaultTempo.Value
            : 120.0;
        resolved.TimeSignature ??= ParseTimesigOrDefault(context.Session.Config.DefaultTimesig);
        resolved.Swing ??= 0.5;

        _resolved = resolved;
        _resolvedVersion = context.ScopeVersion;
        return resolved;
    }

    /// <summary>
    /// Parses the <c>default_timesig</c> config value ("N/M"). Missing → 4/4; malformed
    /// (non-positive or non-power-of-2 denominator) → 4/4 with a one-shot warning.
    /// </summary>
    private static TimeSignatureData ParseTimesigOrDefault(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return new TimeSignatureData(4, 4);
        var parts = config.Split('/');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var num) && num > 0
            && int.TryParse(parts[1], out var den) && den > 0
            && (den & (den - 1)) == 0)
        {
            return new TimeSignatureData(num, den);
        }
        RenderingDiagnostics.WarnOnce(
            $"config-default-timesig:{config}",
            $"Warning: malformed default_timesig in config.toml: \"{config}\" — falling back to 4/4.");
        return new TimeSignatureData(4, 4);
    }

    private sealed record State(
        Dictionary<string, List<SectionData>> Sections,
        MusicalContext? GlobalContext,
        bool SfzEnabled,
        Dictionary<Value, string> SfzInstruments,
#if !FLOW_WEB
        Dictionary<string, StandardLibrary.Audio.Sfz.SfzData> SfzPatches,
#endif
        HashSet<string> SfzDiagnostics,
        string? SfzRoot,
        Dictionary<Value, DictData> Styles,
        HashSet<string> StyleAdvisories);

    public object? Snapshot(ExecutionContext context) => new State(
        SectionRegistry.ToDictionary(kv => kv.Key, kv => new List<SectionData>(kv.Value)),
        context.GlobalFrame.GetScope<MusicalContext>()?.Clone(),
        SfzEnabled,
        new Dictionary<Value, string>(SfzInstruments),
#if !FLOW_WEB
        new Dictionary<string, StandardLibrary.Audio.Sfz.SfzData>(SfzPatchRegistry),
#endif
        new HashSet<string>(SfzDiagnostics),
        ResolvedSfzRoot,
        new Dictionary<Value, DictData>(StyleRegistry),
        new HashSet<string>(StyleOverrideAdvisoriesEmitted));

    public void Restore(ExecutionContext context, object? snapshot)
    {
        var state = (State)snapshot!;
        Refill(SectionRegistry, state.Sections.Select(kv => KeyValuePair.Create(kv.Key, new List<SectionData>(kv.Value))));
        context.GlobalFrame.SetScope(state.GlobalContext);
        SfzEnabled = state.SfzEnabled;
        Refill(SfzInstruments, state.SfzInstruments);
#if !FLOW_WEB
        Refill(SfzPatchRegistry, state.SfzPatches);
#endif
        SfzDiagnostics.Clear();
        SfzDiagnostics.UnionWith(state.SfzDiagnostics);
        ResolvedSfzRoot = state.SfzRoot;
        Refill(StyleRegistry, state.Styles);
        StyleOverrideAdvisoriesEmitted.Clear();
        StyleOverrideAdvisoriesEmitted.UnionWith(state.StyleAdvisories);
        _resolved = null;

        // Hermetic tests: the synthesizers' noise generator restarts too.
        StandardLibrary.Audio.Synthesizers.SynthUtils.ResetNoiseRng();
    }

    private static void Refill<TKey, TValue>(Dictionary<TKey, TValue> target, IEnumerable<KeyValuePair<TKey, TValue>> source)
        where TKey : notnull
    {
        target.Clear();
        foreach (var (key, value) in source)
            target[key] = value;
    }
}
