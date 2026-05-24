using System.Collections.Concurrent;
using System.Collections.Generic;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Core;

namespace FlowLang.Runtime;

/// <summary>
/// Phase 38 Plan 38-02 LIVE-01 — singleton-per-<see cref="ExecutionContext"/>
/// registry keyed by <see cref="LiveBlockStatement.BlockId"/> tracking the
/// composer's active <c>live &lt;quantize&gt; { ... }</c> blocks. Mirrors the
/// shape of <see cref="PrngRegistry"/> (Phase 36 Plan 36-01) so Plan 38-03's
/// swap consumer (LiveReloadManager) can replace per-block pending buffers
/// at the next quantize-unit boundary (D-38-02 multi-block independent swap).
///
/// <para>
/// <b>BlockId stability:</b> <see cref="LiveBlockStatement.ComputeBlockId"/>
/// is FNV-1a of the block's <see cref="SourceLocation"/> — deterministic
/// across re-renders. Adding/removing live blocks shifts subsequent lines'
/// BlockIds; the unchanged BlockIds remain stable, and the diff is what Plan
/// 38-03's swap consumer reads to decide which voices to truncate vs. inherit.
/// </para>
///
/// <para>
/// <b>Concurrency:</b> the registry is backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> because Plan 38-01's
/// <c>LiveReloadManager</c> orchestrates re-renders on a background
/// <c>Task.Run</c> while the streaming playback loop reads on the audio
/// thread. The two-actor pattern matches PrngRegistry's
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>-backed cache.
/// </para>
/// </summary>
public sealed class LiveBlockRegistry
{
    private readonly ConcurrentDictionary<int, LiveBlockRegistration> _registry = new();

    /// <summary>
    /// Registers (or replaces) the registration for a given
    /// <see cref="LiveBlockRegistration.BlockId"/>. Replacement semantics
    /// mirror Plan 38-01's per-block pending-buffer staging — on each
    /// re-render the interpreter calls Register again with the newly
    /// captured Body + QuantizeBeats; the previous entry is supplanted
    /// (last-write-wins) so the registry always reflects the most-recent
    /// composer source.
    /// </summary>
    public void Register(LiveBlockRegistration registration)
    {
        _registry[registration.BlockId] = registration;
    }

    /// <summary>
    /// Returns an immutable snapshot of the registry — keys are
    /// <see cref="LiveBlockRegistration.BlockId"/>s, values are the
    /// most-recently-registered <see cref="LiveBlockRegistration"/> per
    /// block. Consumed by Plan 38-03's <c>LiveReloadManager.StagePendingBuffers</c>
    /// at swap time to diff old vs. new and stage per-block pending buffers.
    /// </summary>
    public IReadOnlyDictionary<int, LiveBlockRegistration> Snapshot()
    {
        // Copy into a plain Dictionary so callers can iterate without seeing
        // mid-iteration concurrent mutations.
        return new Dictionary<int, LiveBlockRegistration>(_registry);
    }

    /// <summary>
    /// Clears all registered live blocks. Called by Plan 38-01's
    /// LiveReloadManager at re-render entry so a removed live block's
    /// previous entry doesn't leak into the next swap diff. Matches the
    /// <see cref="PrngRegistry.ResetAtRenderBoundary"/> precedent at
    /// Phase 36 Plan 36-01 line 122 — boundary-scoped clearance is the
    /// engine-wide pattern.
    /// </summary>
    public void Clear()
    {
        _registry.Clear();
    }
}

/// <summary>
/// Phase 38 Plan 38-02 — per-block registration record stored in
/// <see cref="LiveBlockRegistry"/>. The shape comes from RESEARCH §A
/// lines 638-648; Plan 38-03 will extend this with a CapturedBuffer +
/// SnapshotContext field once the swap consumer lands.
/// </summary>
/// <param name="BlockId">FNV-1a hash of <paramref name="Location"/>.</param>
/// <param name="Location">Source location of the <c>live</c> keyword.</param>
/// <param name="Body">Statements inside the <c>{ ... }</c>; re-evaluated by
/// the swap callback at each re-render boundary.</param>
/// <param name="QuantizeBeats">Resolved quantize duration in beats —
/// NoteValue tokens (<c>q</c>/<c>h</c>/<c>w</c>/<c>e</c>/<c>s</c>) and
/// Int+<c>bar</c>/<c>bars</c> forms collapse onto a single Double at
/// registration time so the swap path doesn't re-resolve.</param>
public sealed record LiveBlockRegistration(
    int BlockId,
    SourceLocation Location,
    IReadOnlyList<Statement> Body,
    double QuantizeBeats
);
