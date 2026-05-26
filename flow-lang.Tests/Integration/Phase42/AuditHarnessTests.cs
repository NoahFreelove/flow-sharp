using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FlowLang.Core;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using Xunit;

namespace FlowLang.Tests.Integration.Phase42;

/// <summary>
/// Phase 42 Plan 42-01 Task 2 — read-only audit harness self-check.
///
/// Mirrors the source-grep + offender-accumulation pattern of
/// <see cref="FlowLang.Tests.Phase36.PrngRegistryNewRandomGateTests"/>
/// and the sibling reflective-audit pattern of
/// <see cref="FlowLang.Tests.Integration.Phase29.LicenseAuditTests"/>.
///
/// Pins the behavior of <c>scripts/StdlibAuditor</c> in-process by
/// reproducing its extraction logic against a short-lived
/// <see cref="FlowEngine"/>. The harness MUST:
///   1. Enumerate every <see cref="FlowType"/> subclass and every
///      registered <see cref="FunctionSignature"/> without throwing.
///   2. Surface <c>BeatType</c> as a coercible orphan (the RESEARCH
///      §Summary anchor regression — Beat is compatible with Double/Float
///      but no signature accepts a Beat parameter).
///   3. Recognize the 5 reference-identity types
///      (TuningType / SfzType / MarkovModelType / LsystemModelType /
///      OscHandleType) as <c>kind: reference-identity</c> so they are
///      NOT flagged as coercible orphans (per RESEARCH Pitfall 2).
///   4. Produce a non-empty asymmetric-conversion list (many entries
///      expected — Beat↔Double is canonical, plus Decibel/Cent/Hertz/
///      Millisecond/Second/Semitone all have A→Double but not Double→A
///      by design).
///   5. Wire the FlowEngine-only context-bound paths (Sfz / NotationIO /
///      OSC), proving the registry-construction path matches the production
///      surface. The plan's PITFALL hint said use <c>RegisterSignaturesOnly</c>
///      — but BOTH RegisterAllImplementations AND RegisterSignaturesOnly
///      miss these paths; only <see cref="FlowEngine"/> construction
///      covers them. This fact guards against future plans accidentally
///      reverting to the partial wiring.
/// </summary>
public class AuditHarnessTests
{
    // Reference-identity types per RESEARCH Pitfall 2 — strict equality only.
    // Mirrors the same constant in scripts/StdlibAuditor/Program.cs
    // (intentional duplication: the test is self-contained for diagnosability).
    private static readonly HashSet<string> ReferenceIdentityTypeNames = new(StringComparer.Ordinal)
    {
        "TuningType",
        "SfzType",
        "MarkovModelType",
        "LsystemModelType",
        "OscHandleType",
    };

    /// <summary>
    /// Lazy cache so each [Fact] reuses the same enumeration — FlowEngine
    /// construction takes ~50ms and we'd otherwise pay it 5+ times per test
    /// class run. The lazy is `Lazy<>`, NOT `static readonly`, so test
    /// failures isolate per-fact instead of cascading.
    /// </summary>
    private static readonly Lazy<HarnessSnapshot> _snapshot =
        new(BuildSnapshot, isThreadSafe: true);

    private sealed record TypeRow(
        string ClrName,        // e.g. "BeatType"
        string FlowName,       // e.g. "Beat"
        bool IsCoercible,
        bool IsReferenceIdentity,
        int ConsumerCount);

    private sealed record HarnessSnapshot(
        IReadOnlyList<TypeRow> Types,
        IReadOnlyDictionary<string, IReadOnlyList<FunctionSignature>> Signatures,
        int AsymmetricPairCount,
        IReadOnlyList<string> CoercibleOrphans);

    private static HarnessSnapshot BuildSnapshot()
    {
        // Construct a FlowEngine to wire the FULL registry — BuiltInFunctions
        // alone (either RegisterAllImplementations or RegisterSignaturesOnly)
        // misses SfzBuiltins / NotationIoBuiltins / OscFunctions / Markov /
        // Lsystem / Cellular / Chaos / Stretch / PitchShift / Granular /
        // Pattern / Jam / StyleRegistry, all of which are wired ONLY by
        // FlowEngine.cs:140-207. Safe for audit use: FlowEngine doesn't open
        // PulseAudio until `play`/`preview` is called, which this fixture
        // never does. Style packs load charitably (D-36-12) — a bad pack
        // fires a stderr advisory and continues.
        using var engine = new FlowEngine();
        var registry = engine.Context.InternalRegistry;

        // Reflect over the FlowType subclass set.
        var typeAsm = typeof(FlowType).Assembly;
        var discovered = typeAsm.GetTypes()
            .Where(t => typeof(FlowType).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && !t.IsGenericType)
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

        // Consumer count keyed by FlowType.Name (the surface name embedded in
        // FunctionSignature.InputTypes — distinct from Type.Name).
        var consumersByFlowName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, inst) in discovered)
        {
            consumersByFlowName[inst!.Name] = 0;
        }

        var sigDict = new Dictionary<string, IReadOnlyList<FunctionSignature>>(StringComparer.Ordinal);
        foreach (var kvp in registry.EnumerateSignatures())
        {
            sigDict[kvp.Key] = kvp.Value;
            foreach (var sig in kvp.Value)
            {
                foreach (var paramType in sig.InputTypes)
                {
                    if (consumersByFlowName.ContainsKey(paramType.Name))
                    {
                        consumersByFlowName[paramType.Name]++;
                    }
                }
            }
        }

        var typeRows = new List<TypeRow>();
        foreach (var (clrType, inst) in discovered)
        {
            bool isRefId = ReferenceIdentityTypeNames.Contains(clrType.Name);
            bool isCoercible = !isRefId && OverridesIsCompatible(clrType);
            int count = consumersByFlowName.TryGetValue(inst!.Name, out var c) ? c : 0;
            typeRows.Add(new TypeRow(
                ClrName: clrType.Name,
                FlowName: inst.Name,
                IsCoercible: isCoercible,
                IsReferenceIdentity: isRefId,
                ConsumerCount: count));
        }

        // Asymmetric-pair count — same iteration as scripts/StdlibAuditor/Program.cs.
        int asymCount = 0;
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
                if (aToB != bToA || aConvB != bConvA) asymCount++;
            }
        }

        var orphans = typeRows
            .Where(r => r.IsCoercible && r.ConsumerCount == 0)
            .Select(r => r.ClrName)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        return new HarnessSnapshot(
            Types: typeRows,
            Signatures: sigDict,
            AsymmetricPairCount: asymCount,
            CoercibleOrphans: orphans);
    }

    /// <summary>
    /// Returns true when the given concrete type overrides
    /// <see cref="FlowType.IsCompatibleWith"/>. Coercible-type detection
    /// helper — types using the base implementation are strict-equality
    /// only and are NOT considered coercible.
    /// </summary>
    private static bool OverridesIsCompatible(Type t)
    {
        var method = t.GetMethod(
            nameof(FlowType.IsCompatibleWith),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(FlowType) },
            modifiers: null);
        return method is not null && method.DeclaringType == t;
    }

    /// <summary>
    /// Walks up from the test assembly location until a directory containing
    /// <c>flow-sharp.sln</c> is found. Mirrors the helper from
    /// <see cref="FlowLang.Tests.Phase36.PrngRegistryNewRandomGateTests"/>.
    /// Provided for future Phase 42 follow-up tests that may want to grep
    /// the source tree — not directly used by the in-process facts below.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate flow-sharp.sln walking up from " + AppContext.BaseDirectory);
        return dir.FullName;
    }

    // =====================================================================
    // Facts
    // =====================================================================

    [Fact]
    public void Harness_EnumeratesWithoutThrowing()
    {
        var snap = _snapshot.Value;
        Assert.True(snap.Types.Count >= 20,
            $"Expected >= 20 FlowType subclasses; got {snap.Types.Count}.");
        int totalSignatures = snap.Signatures.Sum(kvp => kvp.Value.Count);
        Assert.True(totalSignatures >= 200,
            $"Expected >= 200 registered signatures; got {totalSignatures}. " +
            "If this regressed, BuiltInFunctions or FlowEngine wiring is incomplete.");
    }

    [Fact]
    public void OrphanList_ContainsBeatType()
    {
        var snap = _snapshot.Value;
        Assert.Contains("BeatType", snap.CoercibleOrphans);
        // Failure message context for future maintainers:
        // BeatType must appear in the orphan list — see RESEARCH.md §Summary.
        // If a producer/consumer for Beat shipped (e.g. a new signature
        // accepting a Beat parameter), this test needs to be updated to drop
        // BeatType from the expected-orphan set — and the new finding should
        // be reflected in AUDIT.md's "Resolved Orphans" section.
    }

    [Theory]
    [InlineData("TuningType")]
    [InlineData("SfzType")]
    [InlineData("MarkovModelType")]
    [InlineData("LsystemModelType")]
    [InlineData("OscHandleType")]
    public void RefIdentityTypes_NotFlaggedAsCoercibleOrphans(string typeName)
    {
        var snap = _snapshot.Value;
        // The harness MUST classify these as reference-identity, not coercible.
        // A reference-identity type with zero consumers is BY DESIGN — they
        // participate via reference-identity dispatch (Tuning passes through
        // `tuning t { }` blocks, Sfz through `renderSong song "sampler:NAME"`,
        // etc.) which signature enumeration cannot see.
        var row = snap.Types.FirstOrDefault(r => r.ClrName == typeName);
        Assert.NotNull(row);
        Assert.False(row!.IsCoercible,
            $"{typeName} is classified as coercible — it must be reference-identity per RESEARCH Pitfall 2. " +
            $"Check the ReferenceIdentityTypeNames set in scripts/StdlibAuditor/Program.cs and this test.");
        Assert.True(row.IsReferenceIdentity,
            $"{typeName} must be tagged kind=reference-identity so it is suppressed from the orphan list.");
        Assert.DoesNotContain(typeName, snap.CoercibleOrphans);
    }

    [Fact]
    public void AsymmetricConversions_NonEmpty()
    {
        var snap = _snapshot.Value;
        Assert.True(snap.AsymmetricPairCount >= 1,
            $"Expected at least 1 asymmetric conversion pair; got {snap.AsymmetricPairCount}. " +
            "Music-types like Beat/Decibel/Cent override IsCompatibleWith to widen to Double/Float, " +
            "but Double/Float don't reciprocate — the count should be substantially > 0.");
    }

    [Fact]
    public void Registry_WiresSfzAndNotationIoAndOsc()
    {
        // Guards the PITFALL from RESEARCH §Pattern 2: SfzBuiltins /
        // NotationIoBuiltins / OscFunctions are wired ONLY via FlowEngine.cs,
        // NOT via BuiltInFunctions.RegisterAllImplementations and NOT via
        // BuiltInFunctions.RegisterSignaturesOnly. The audit harness MUST
        // construct a FlowEngine (or equivalent) to capture them — if a
        // future refactor drops these from the registry construction path,
        // this fact catches it immediately.
        var snap = _snapshot.Value;
        Assert.True(snap.Signatures.ContainsKey("loadSfz"),
            "loadSfz signature missing — Phase 33 SfzBuiltins wiring lost. " +
            "FlowEngine.cs:173 (SfzBuiltins.Register) must run during registry construction.");
        Assert.True(snap.Signatures.ContainsKey("writeMusicXML"),
            "writeMusicXML signature missing — Phase 39 NotationIoBuiltins wiring lost. " +
            "FlowEngine.cs:180 (NotationIoBuiltins.Register) must run during registry construction.");
        Assert.True(snap.Signatures.ContainsKey("oscSend"),
            "oscSend signature missing — Phase 38 OscFunctions wiring lost. " +
            "FlowEngine.cs:190 (OscFunctions.Register) must run during registry construction.");
    }
}
