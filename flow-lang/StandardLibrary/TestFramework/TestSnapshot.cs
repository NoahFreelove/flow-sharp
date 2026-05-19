using System.Collections.Generic;
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
    public required IReadOnlyDictionary<string, FlowLang.TypeSystem.SpecialTypes.SectionData> SectionRegistry { get; init; }

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
}
