using System.Collections.Generic;
using FlowLang.Core;
using FlowLang.Runtime;

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
    // 1-2. Global frame variables, and the registered TestRegistry size (not state —
    //      a marker so we can confirm we restore to the same registry cardinality).
    public required IReadOnlyDictionary<string, Value> GlobalVariables { get; init; }
    public required int TestRegistryCount { get; init; }

    // 4. Phase 26.1 — Symbol intern table (per-context).
    public required IReadOnlyDictionary<string, Value> SymbolInternTable { get; init; }

    // 5. PRNG state — FixedRandSeed + FixedGen + Gen.
    public required int FixedRandSeed { get; init; }
    public required System.Random? FixedGen { get; init; }
    public required System.Random? Gen { get; init; }

    // 6. Domain session extensions (ExecutionContext.GetExtension), keyed by
    //    extension type, each holding the state its Snapshot returned.
    public required IReadOnlyDictionary<System.Type, object?> ExtensionStates { get; init; }

    // 10b. Phase 39 — notation-io module activation gate. Defaulted-false so
    //      pre-Phase-39 TestSnapshot constructions remain backward-compatible
    //      (no new `required` keyword to avoid breaking existing callers).
    public bool NotationIoEnabled { get; init; } = false;

    // 10c. Phase 38 Plan 38-06 OSC-01 — OSC module activation gate. Defaulted-
    //      false so pre-Phase-38 TestSnapshot constructions remain backward-
    //      compatible (no new `required` keyword to avoid breaking existing
    //      callers).
    public bool OscEnabled { get; init; } = false;

    // 10d. Phase 40 Plan 40-01 MIDI-RT-01 — @midi module activation gate.
    //      Defaulted-false so pre-Phase-40 TestSnapshot constructions remain
    //      backward-compatible (no new `required` keyword).
    public bool MidiEnabled { get; init; } = false;

    // 10e. Phase 40 Plan 40-03 JACK-01 — @jack module activation gate.
    //      Defaulted-false so pre-Phase-40 TestSnapshot constructions remain
    //      backward-compatible (no new `required` keyword).
    public bool JackEnabled { get; init; } = false;

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
}
