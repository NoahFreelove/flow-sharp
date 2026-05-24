using System.Reflection;
using System.Text;
using System.Text.Json;
using FlowLang.Core;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;

namespace FlowLang.StdlibAuditor;

/// <summary>
/// Phase 42 Plan 42-01 Task 1 — read-only stdlib audit harness.
///
/// Reflects over <see cref="FlowType"/> + every signature registered via
/// <see cref="BuiltInFunctions.RegisterSignaturesOnly"/> (NOT
/// <see cref="BuiltInFunctions.RegisterAllImplementations"/>, per RESEARCH
/// Pitfall — RegisterAllImplementations does NOT wire Sfz / NotationIO /
/// OSC; only RegisterSignaturesOnly proxies through the full Register*
/// chain including manager-bound + context-bound paths).
///
/// Emits a machine-readable JSON graph consumed by Plan 03 (AUDIT.md
/// authoring) and an empty markdown skeleton with the 7 prioritization
/// sections. Zero production code touched — invariant for Phase 42.
///
/// Usage:
///   dotnet run --project scripts/StdlibAuditor -- --emit-json PATH
///   dotnet run --project scripts/StdlibAuditor -- --emit-markdown-skeleton PATH
///   dotnet run --project scripts/StdlibAuditor -- --emit-json PATH --emit-markdown-skeleton PATH
///
/// At least one flag is required. Default paths land under
/// .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/ when only
/// the flag with no value is supplied.
/// </summary>
internal static class Program
{
    private const string DefaultJsonPath =
        ".planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/type-signature-graph.json";
    private const string DefaultMarkdownPath =
        ".planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/AUDIT-skeleton.md";

    // Reference-identity types per RESEARCH Pitfall 2 — strict equality only.
    // NOT considered "orphans" even when their consumer count is low.
    private static readonly HashSet<string> ReferenceIdentityTypeNames = new()
    {
        "TuningType",
        "SfzType",
        "MarkovModelType",
        "LsystemModelType",
        "OscHandleType",
    };

    // Music-type companions that overload-gap analysis (REQ-AUDIT-06)
    // expects beside any Double/Float-accepting builtin.
    private static readonly string[] MusicTypeCompanions = new[]
    {
        "Decibel",
        "Cent",
        "Hertz",
        "Millisecond",
        "Second",
        "Semitone",
    };

    static int Main(string[] args)
    {
        string? jsonPath = null;
        string? markdownPath = null;
        bool jsonRequested = false;
        bool markdownRequested = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--emit-json":
                    jsonRequested = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        jsonPath = args[++i];
                    }
                    break;
                case "--emit-markdown-skeleton":
                    markdownRequested = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        markdownPath = args[++i];
                    }
                    break;
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"warning: unknown argument '{args[i]}'");
                    break;
            }
        }

        if (!jsonRequested && !markdownRequested)
        {
            PrintUsage();
            return 1;
        }

        jsonPath ??= DefaultJsonPath;
        markdownPath ??= DefaultMarkdownPath;

        // Build the graph once; both emitters consume the same model.
        var graph = AuditExtractor.Build();

        if (jsonRequested)
        {
            WriteAtomic(jsonPath, AuditExtractor.RenderJson(graph));
        }

        if (markdownRequested)
        {
            WriteAtomic(markdownPath, AuditExtractor.RenderMarkdownSkeleton(graph));
        }

        int coercible = graph.Types.Count(t => t.Kind == "coercible");
        int refIdentity = graph.Types.Count(t => t.Kind == "reference-identity");
        int signatureCount = graph.Signatures.Count;
        Console.WriteLine(
            $"Done. {graph.Types.Count} types ({coercible} coercible, {refIdentity} ref-identity), " +
            $"{signatureCount} signatures, {graph.Orphans.Count} orphans, " +
            $"{graph.Asymmetries.Count} asymmetric pairs, " +
            $"{graph.OverloadGapCandidates.Count} overload-gap candidates.");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: dotnet run --project scripts/StdlibAuditor -- [--emit-json PATH] [--emit-markdown-skeleton PATH]");
        Console.Error.WriteLine(
            "Reflects over FlowType + registered FunctionSignature and emits a JSON audit graph.");
        Console.Error.WriteLine(
            "At least one of --emit-json / --emit-markdown-skeleton is required.");
        Console.Error.WriteLine($"Default JSON path: {DefaultJsonPath}");
        Console.Error.WriteLine($"Default markdown skeleton path: {DefaultMarkdownPath}");
    }

    // Atomic-write pattern per RESEARCH Security §V12: write to temp, then File.Move
    // with overwrite=true. Never leaves an output file in a torn / partial state.
    private static void WriteAtomic(string finalPath, string contents)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(finalPath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tempPath = finalPath + ".tmp";
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, finalPath, overwrite: true);
    }
}

// ============================================================================
// Audit extractor — shared between Program.cs and the xUnit self-check fixture
// in flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs.
// ============================================================================

/// <summary>
/// Reflective extractor that builds the FlowType <-> FunctionSignature
/// adjacency graph from a freshly-constructed <see cref="InternalFunctionRegistry"/>.
///
/// Public so the xUnit fixture can call <see cref="Build"/> in-process
/// instead of shelling out to <c>dotnet run</c>.
/// </summary>
public static class AuditExtractor
{
    public sealed record TypeEntry(
        string Name,            // Type.Name e.g. "BeatType" — stable C# class name
        string FlowName,        // FlowType.Name e.g. "Beat" — surface name used in signatures
        string Kind,            // "coercible" | "reference-identity" | "strict-equality"
        int ConsumerCount,
        int Specificity,
        bool IsHashable);

    public sealed record SignatureEntry(
        string Name,
        IReadOnlyList<string> InputTypes,
        IReadOnlyList<string>? ParameterNames,
        bool IsVarArgs);

    public sealed record OrphanEntry(
        string Name,
        string FlowName,
        string Kind,
        string Reason);

    public sealed record AsymmetryEntry(
        string A,
        string B,
        bool AIsCompatibleWithB,
        bool BIsCompatibleWithA,
        bool ACanConvertToB,
        bool BCanConvertToA);

    public sealed record OverloadGapEntry(
        string Function,
        bool AcceptsDouble,
        bool AcceptsFloat,
        IReadOnlyList<string> MissingMusicTypes);

    public sealed record Graph(
        IReadOnlyList<TypeEntry> Types,
        IReadOnlyList<SignatureEntry> Signatures,
        IReadOnlyList<OrphanEntry> Orphans,
        IReadOnlyList<AsymmetryEntry> Asymmetries,
        IReadOnlyList<OverloadGapEntry> OverloadGapCandidates);

    private static readonly HashSet<string> ReferenceIdentityTypeNames = new()
    {
        "TuningType",
        "SfzType",
        "MarkovModelType",
        "LsystemModelType",
        "OscHandleType",
    };

    private static readonly string[] MusicTypeCompanions = new[]
    {
        "Decibel",
        "Cent",
        "Hertz",
        "Millisecond",
        "Second",
        "Semitone",
    };

    /// <summary>
    /// Builds the audit graph by (1) reflecting over <see cref="FlowType"/>
    /// subclasses in its assembly, (2) wiring every signature via a
    /// short-lived <see cref="FlowEngine"/> (which performs the COMPLETE
    /// wiring including the context-bound paths SfzBuiltins / NotationIoBuiltins
    /// / OscFunctions / Markov / Lsystem / Cellular / Chaos / Stretch /
    /// PitchShift / Granular / Pattern / Jam that <see cref="BuiltInFunctions.RegisterSignaturesOnly"/>
    /// alone does NOT cover — the plan's PITFALL hint was inverted; both
    /// RegisterAllImplementations AND RegisterSignaturesOnly miss the
    /// FlowEngine-only paths), (3) computing orphan / asymmetry /
    /// overload-gap derived tables.
    ///
    /// FlowEngine construction is safe to invoke from a console / xUnit
    /// harness: it only initializes per-engine state in memory (AudioPlaybackManager
    /// + SampleCache + SfzSampleCache + ExecutionContext). No PulseAudio
    /// backend opens until the first <c>play</c> / <c>preview</c> builtin
    /// invocation, which the audit never makes. Style packs load from
    /// <c>flow-lang/improv/styles/*.flow</c> + <c>~/.config/flow/styles/*.flow</c>
    /// — charitable (a bad pack fires a one-shot stderr advisory and continues),
    /// per Phase 36 D-36-12.
    /// </summary>
    public static Graph Build()
    {
        // Construct a FlowEngine to wire the FULL registry — RegisterSignaturesOnly
        // would miss SfzBuiltins / NotationIoBuiltins / OscFunctions /
        // MarkovFunctions / LsystemFunctions / CellularFunctions / ChaosFunctions /
        // StretchFunctions / PitchShiftFunctions / GranularFunctions /
        // PatternFunctions / JamFunctions / StyleRegistry, all of which are wired
        // ONLY by FlowEngine.cs:140-207.
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;

        // (1) Type inventory — concrete, non-abstract, non-generic FlowType subclasses
        // that expose a public static Instance property of the right type.
        var typeAsm = typeof(FlowType).Assembly;
        var discovered = typeAsm.GetTypes()
            .Where(t => typeof(FlowType).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType)
            .Select(t =>
            {
                var instanceProp = t.GetProperty(
                    "Instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProp?.GetValue(null) as FlowType;
                return (Type: t, Instance: instance);
            })
            .Where(x => x.Instance is not null)
            .OrderBy(x => x.Type.Name, StringComparer.Ordinal)
            .ToList();

        // (2) Consumer map — keyed by FlowType.Name (the surface name returned by
        // FlowType.Name and embedded in FunctionSignature.InputTypes). This is
        // distinct from Type.Name (the C# class name with the "Type" suffix);
        // we keep both in the TypeEntry rows so the orphan list can cite either.
        var consumersByFlowName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, inst) in discovered)
        {
            consumersByFlowName[inst!.Name] = 0;
        }

        var signatures = new List<SignatureEntry>();
        foreach (var kvp in registry.EnumerateSignatures().OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string name = kvp.Key;
            foreach (var sig in kvp.Value)
            {
                var inputs = sig.InputTypes.Select(t => t.Name).ToList();
                foreach (var paramTypeName in inputs)
                {
                    if (consumersByFlowName.ContainsKey(paramTypeName))
                    {
                        consumersByFlowName[paramTypeName]++;
                    }
                }
                signatures.Add(new SignatureEntry(
                    Name: name,
                    InputTypes: inputs,
                    ParameterNames: sig.ParameterNames?.ToList(),
                    IsVarArgs: sig.IsVarArgs));
            }
        }

        // (3) Classify each type as coercible / reference-identity / strict-equality.
        // Coercible = overrides IsCompatibleWith with a non-trivial body (delegates
        // through the helper below). Reference-identity = the well-known set from
        // RESEARCH Pitfall 2. Everything else is strict-equality (base implementation).
        var typeEntries = new List<TypeEntry>();
        foreach (var (clrType, inst) in discovered)
        {
            string kind;
            if (ReferenceIdentityTypeNames.Contains(clrType.Name))
            {
                kind = "reference-identity";
            }
            else if (OverridesIsCompatible(clrType))
            {
                kind = "coercible";
            }
            else
            {
                kind = "strict-equality";
            }

            int consumerCount = consumersByFlowName.TryGetValue(inst!.Name, out var c) ? c : 0;
            typeEntries.Add(new TypeEntry(
                Name: clrType.Name,
                FlowName: inst.Name,
                Kind: kind,
                ConsumerCount: consumerCount,
                Specificity: inst.GetSpecificity(),
                IsHashable: inst.IsHashable()));
        }

        // (4) Orphan list — coercible types with zero consumers. Reference-identity
        // types are deliberately excluded per RESEARCH Pitfall 2 (their low consumer
        // counts are by design — they participate via reference-identity dispatch
        // sites that the audit can't see via signature enumeration alone).
        var orphans = typeEntries
            .Where(t => t.Kind == "coercible" && t.ConsumerCount == 0)
            .Select(t => new OrphanEntry(
                Name: t.Name,
                FlowName: t.FlowName,
                Kind: t.Kind,
                Reason: "coercible type with zero consumer signatures — composer cannot pass values of this type to any builtin"))
            .ToList();

        // (5) Asymmetric conversions — iterate (A, B) pairs.
        var asymmetries = new List<AsymmetryEntry>();
        for (int i = 0; i < discovered.Count; i++)
        {
            for (int j = 0; j < discovered.Count; j++)
            {
                if (i == j) continue;
                var a = discovered[i].Instance!;
                var b = discovered[j].Instance!;
                bool aToB = a.IsCompatibleWith(b);
                bool bToA = b.IsCompatibleWith(a);
                bool aConvB = a.CanConvertTo(b);
                bool bConvA = b.CanConvertTo(a);
                if (aToB != bToA || aConvB != bConvA)
                {
                    asymmetries.Add(new AsymmetryEntry(
                        A: discovered[i].Type.Name,
                        B: discovered[j].Type.Name,
                        AIsCompatibleWithB: aToB,
                        BIsCompatibleWithA: bToA,
                        ACanConvertToB: aConvB,
                        BCanConvertToA: bConvA));
                }
            }
        }

        // (6) Overload-gap candidates — for each function name that has at least
        // one signature accepting Double or Float as a parameter, check whether
        // any sibling signature (same function name) accepts each music-type
        // companion in MusicTypeCompanions. Report companions that are missing.
        var byFunction = signatures
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        var overloadGaps = new List<OverloadGapEntry>();
        foreach (var grp in byFunction)
        {
            var allInputTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sig in grp)
            {
                foreach (var t in sig.InputTypes) allInputTypes.Add(t);
            }
            bool acceptsDouble = allInputTypes.Contains("Double");
            bool acceptsFloat = allInputTypes.Contains("Float");
            if (!acceptsDouble && !acceptsFloat) continue;

            var missing = MusicTypeCompanions
                .Where(mt => !allInputTypes.Contains(mt))
                .ToList();
            if (missing.Count == 0) continue;
            // Only emit a candidate row if at least one music-type companion is missing.
            overloadGaps.Add(new OverloadGapEntry(
                Function: grp.Key,
                AcceptsDouble: acceptsDouble,
                AcceptsFloat: acceptsFloat,
                MissingMusicTypes: missing));
        }
        overloadGaps.Sort((x, y) => string.Compare(x.Function, y.Function, StringComparison.Ordinal));

        return new Graph(
            Types: typeEntries,
            Signatures: signatures,
            Orphans: orphans,
            Asymmetries: asymmetries,
            OverloadGapCandidates: overloadGaps);
    }

    /// <summary>
    /// Returns true when the given concrete type overrides
    /// <see cref="FlowType.IsCompatibleWith"/>. Used to identify the
    /// coercible-type subset that participates in numeric / music-type
    /// widening (Beat, Cent, Decibel, Millisecond, Second, Semitone,
    /// Hertz, etc.). Types that inherit the base implementation use
    /// strict equality only and are NOT considered coercible.
    /// </summary>
    public static bool OverridesIsCompatible(Type t)
    {
        var method = t.GetMethod(
            nameof(FlowType.IsCompatibleWith),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(FlowType) },
            modifiers: null);
        if (method is null) return false;
        // DeclaringType is the type that actually owns the override. If it's
        // the concrete type t, then t has its own override; if it's FlowType
        // itself, the type uses the base implementation.
        return method.DeclaringType == t;
    }

    // ========================================================================
    // JSON + Markdown renderers
    // ========================================================================

    public static string RenderJson(Graph g)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        // Hand-build the top-level object so the JSON keys exactly match the
        // schema documented in 42-01-PLAN.md and the acceptance criteria.
        var payload = new
        {
            types = g.Types.Select(t => new
            {
                name = t.Name,
                flow_name = t.FlowName,
                kind = t.Kind,
                consumer_count = t.ConsumerCount,
                specificity = t.Specificity,
                is_hashable = t.IsHashable,
            }).ToList(),
            signatures = g.Signatures.Select(s => new
            {
                name = s.Name,
                input_types = s.InputTypes,
                parameter_names = s.ParameterNames,
                is_var_args = s.IsVarArgs,
            }).ToList(),
            orphans = g.Orphans.Select(o => new
            {
                name = o.Name,
                flow_name = o.FlowName,
                kind = o.Kind,
                reason = o.Reason,
            }).ToList(),
            asymmetries = g.Asymmetries.Select(a => new
            {
                a = a.A,
                b = a.B,
                is_compat = new[] { a.AIsCompatibleWithB, a.BIsCompatibleWithA },
                can_convert = new[] { a.ACanConvertToB, a.BCanConvertToA },
            }).ToList(),
            overload_gap_candidates = g.OverloadGapCandidates.Select(c => new
            {
                function = c.Function,
                accepts_double = c.AcceptsDouble,
                accepts_float = c.AcceptsFloat,
                missing_music_types = c.MissingMusicTypes,
            }).ToList(),
        };

        // Newline normalization — single \n per line per RESEARCH §Pitfall 6
        // (two-run cmp-clean). JsonSerializer's default writer uses \n on Linux
        // already; using Encoder defaults keeps things deterministic.
        return JsonSerializer.Serialize(payload, options) + "\n";
    }

    public static string RenderMarkdownSkeleton(Graph g)
    {
        var sb = new StringBuilder();
        sb.Append("# Phase 42 — Type System & Stdlib Audit (Skeleton)\n\n");
        sb.Append("**Generated:** ");
        sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd"));
        sb.Append("\n");
        sb.Append("**Source:** `scripts/StdlibAuditor` reflective harness over ");
        sb.Append(g.Types.Count);
        sb.Append(" FlowType subclasses and ");
        sb.Append(g.Signatures.Count);
        sb.Append(" registered signatures.\n");
        sb.Append("**Status:** SKELETON — Plan 03 authors the prioritized body using ");
        sb.Append("`type-signature-graph.json` (this file's machine-readable sibling).\n\n");
        sb.Append("---\n\n");

        sb.Append("## Orphaned Types\n\n");
        sb.Append("_Coercible types with zero consumer signatures. ");
        sb.Append("Reference-identity types (Tuning/Sfz/MarkovModel/LsystemModel/OscHandle) excluded per RESEARCH Pitfall 2._\n\n");
        sb.Append("Count: ");
        sb.Append(g.Orphans.Count);
        sb.Append("\n\n");

        sb.Append("## Missing Conversions\n\n");
        sb.Append("_To be authored from `overload_gap_candidates` + manual cross-check._\n\n");

        sb.Append("## Asymmetric Pairs\n\n");
        sb.Append("_Pairs where `A.IsCompatibleWith(B) != B.IsCompatibleWith(A)` (or the CanConvertTo equivalent). ");
        sb.Append("False positives expected for music-type → numeric widening (by design, Pitfall 5)._\n\n");
        sb.Append("Count: ");
        sb.Append(g.Asymmetries.Count);
        sb.Append("\n\n");

        sb.Append("## Dead-End Builtins\n\n");
        sb.Append("_Builtins whose return value flows nowhere — requires manual cross-check against `.flow` stdlib (REQ-AUDIT-05)._\n\n");

        sb.Append("## Overload Gaps\n\n");
        sb.Append("_Functions accepting Double/Float but missing music-type companions (REQ-AUDIT-06)._\n\n");
        sb.Append("Count: ");
        sb.Append(g.OverloadGapCandidates.Count);
        sb.Append("\n\n");

        sb.Append("## Clamp & Advisory Inventory\n\n");
        sb.Append("_To be authored from Plan 02's `grep -rn 'Math.Clamp'` + `'RenderingDiagnostics.WarnOnce'` sweep._\n\n");

        sb.Append("## Prioritization & Phase Routing\n\n");
        sb.Append("_To be authored: each finding routed to Phase 43, Phase 44, or v1.6 backlog with composer-impact rationale._\n\n");

        sb.Append("## Limitations\n\n");
        sb.Append("- `FunctionSignature` has no `ReturnType` field (Open Question 1) — producer half of the type graph is inferred manually, NOT enumerated.\n");
        sb.Append("- Reference-identity types (TuningType, SfzType, MarkovModelType, LsystemModelType, OscHandleType) are excluded from the orphan list by design.\n");
        sb.Append("- Asymmetric-pair detection produces a candidate list; many entries are correct-by-design widening edges (e.g. Beat → Double).\n");
        return sb.ToString();
    }
}
