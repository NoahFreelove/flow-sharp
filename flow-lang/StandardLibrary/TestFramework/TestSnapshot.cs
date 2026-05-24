using System.Collections.Generic;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Sfz;

namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-02 — immutable capture of the 11+ mutable
/// state surfaces enumerated in RESEARCH §Pitfall 3. Produced by
/// <see cref="ExecutionContext.SnapshotState"/> before each test body
/// runs; consumed by <see cref="ExecutionContext.RestoreState"/> after
/// the body returns (or throws) so the next test sees a pristine slate.
///
/// <para>
/// Per RESEARCH §Pitfall 3 Notable Departures: NO reflection — every
/// captured field has an explicit set/restore site. Adding a new
/// mutable surface to the engine requires adding a field here AND
/// touching <see cref="ExecutionContext.SnapshotState"/> /
/// <see cref="ExecutionContext.RestoreState"/>. This is intentional
/// — the explicit list makes leak audits possible.
/// </para>
///
/// <para>
/// Per RESEARCH Assumption A8: <c>AudioPlaybackManager</c> is NOT
/// captured here — tests must not trigger live playback. A follow-up
/// CLAUDE.md edit will document this constraint to composers.
/// </para>
/// </summary>
public sealed record TestSnapshot
{
    // 1-3. Global frame variables, registered TestRegistry size (not state —
    //      a marker so we can confirm we restore to the same registry
    //      cardinality), and SectionRegistry contents.
    public required IReadOnlyDictionary<string, Value> GlobalVariables { get; init; }
    public required int TestRegistryCount { get; init; }
    // Phase 36 Plan 36-10 (D-36-18) — value type is now List<SectionData> so
    // overload-bearing same-name registrations are captured + restored
    // verbatim. Pre-Phase-36 snapshot consumers only need the single
    // last-registered entry per name (see ExecutionContext.SectionRegistryFlat)
    // but the snapshot preserves the full overload list for fidelity.
    public required IReadOnlyDictionary<string, List<FlowLang.TypeSystem.SpecialTypes.SectionData>> SectionRegistry { get; init; }

    // 4. Phase 26.1 — Symbol intern table (per-context).
    public required IReadOnlyDictionary<string, Value> SymbolInternTable { get; init; }

    // 5. PRNG state — FixedRandSeed + FixedGen + Gen.
    public required int FixedRandSeed { get; init; }
    public required System.Random? FixedGen { get; init; }
    public required System.Random? Gen { get; init; }

    // 6. Musical-context stack — snapshot the global frame's MusicalContext
    //    instance (cloned via MusicalContext.Clone if non-null).
    public required MusicalContext? GlobalFrameMusicalContext { get; init; }

    // 7-10. Phase 33 SFZ statics — SfzEnabled + SfzInstruments +
    //       SfzPatchRegistry + SfzDiagnostics + ResolvedSfzRoot.
    public required bool SfzEnabled { get; init; }
    public required IReadOnlyDictionary<Value, string> SfzInstruments { get; init; }
    public required IReadOnlyDictionary<string, SfzData> SfzPatchRegistry { get; init; }
    public required IReadOnlySet<string> SfzDiagnostics { get; init; }
    public required string? ResolvedSfzRoot { get; init; }

    // 11. FlowConfig.Active singleton reference. Last-write-wins reset.
    public required FlowConfigPoco FlowConfigActive { get; init; }

    // 12. Phase 36 Plan 36-01 — PrngRegistry draw-count snapshot. Defaulted-null
    //     so pre-Phase-36 TestSnapshot constructions remain backward-compatible
    //     (RestoreState null-guards this field per T-36-03). The map carries
    //     the per-key draw count at snapshot time; restore re-creates each
    //     Random from its deterministic seed and replays the captured draw
    //     count to bring the PRNG state to the snapshot's exact position.
    //     Storing draw counts rather than Random instances guarantees the
    //     PRNG state is RECONSTRUCTABLE from the snapshot — System.Random
    //     has no public serialization/clone API.
    public IReadOnlyDictionary<(SourceLocation Site, string Name), long>? PrngRegistryState
    {
        get; init;
    }

    // 13. Phase 36 Plan 36-11 — StyleRegistry snapshot. Defaulted-null so
    //     pre-Plan-36-11 TestSnapshot constructions stay backward-compatible.
    //     The dict is shallow-copied (Value keys are interned, DictData values
    //     are immutable per Phase 26.1 DICT-02). The override-advisory dedup
    //     set is captured alongside so the WarnOnce sentinels reset cleanly.
    public IReadOnlyDictionary<Value, DictData>? StyleRegistryState { get; init; }
    public IReadOnlySet<string>? StyleOverrideAdvisoriesEmitted { get; init; }
}
