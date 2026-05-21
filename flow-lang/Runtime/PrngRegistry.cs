using System;
using System.Collections.Generic;
using System.Text;
using FlowLang.Core;

namespace FlowLang.Runtime;

/// <summary>
/// Phase 36 Plan 36-01 — singleton-per-<see cref="ExecutionContext"/> registry
/// keyed by <c>(SourceLocation, generator-name)</c> returning a deterministic
/// <see cref="Random"/>. All PRNG-driven primitives shipped from Phase 36
/// onward (<c>markov</c> / <c>lsystem</c> / <c>cellular</c> / <c>lorenz</c> /
/// <c>logistic</c> / <c>degrade</c> / <c>sparseSeq</c> / <c>sometimes</c> /
/// <c>jam</c>) MUST route their unseeded paths through this registry per
/// D-v1.5-06 + D-36-09.
///
/// <para>
/// Reseeded at every <c>renderSong</c> / <c>writeWav</c> / <c>exportWav</c>
/// boundary via <see cref="ResetAtRenderBoundary"/> so two consecutive renders
/// of the same source produce byte-identical WAV+MIDI output (Phase 18/25/27/28/29/33
/// inheritance — the two-run cmp-clean determinism contract documented in
/// CLAUDE.md § Conventions).
/// </para>
///
/// <para>
/// Seed derivation uses an FNV-1a 32-bit stable hash over UTF-8 bytes of the
/// site's file path, line, column, generator name, and a render-boundary salt
/// (currently zero; reserved for future <c>live</c>-block opt-out per D-v1.5-07
/// and RESEARCH §Pattern 6 Open Question 3). The hash is process-stable and
/// platform-independent — C# <see cref="object.GetHashCode"/> is randomized
/// per process and MUST NOT be used here (Pitfall 4 in this phase is
/// Lorenz-specific cross-platform FP divergence; the PRNG side is FNV-1a).
/// </para>
///
/// <para>
/// Singleton-per-<see cref="ExecutionContext"/>, NOT a static singleton:
/// each <c>FlowEngine</c> instance owns one <see cref="PrngRegistry"/> via
/// <see cref="ExecutionContext.PrngRegistry"/>. This makes the test framework's
/// hermetic snapshot/restore round-trip work — see
/// <see cref="ExecutionContext.SnapshotState"/> /
/// <see cref="ExecutionContext.RestoreState"/>.
/// </para>
/// </summary>
public class PrngRegistry
{
    // Live Random instances per call site. Mutates as Phase 36 stochastic
    // primitives advance their PRNG state across a render pass.
    private readonly Dictionary<(SourceLocation Site, string Name), Random> _registry = new();

    // Per-key DRAW COUNT — the number of times .Next() (or any consumer)
    // advanced the Random for that key during the current render pass.
    // Snapshot/restore uses this to deterministically rewind: at restore time
    // we re-create each Random from its deterministic seed and replay the
    // captured draw count to bring it to the snapshot's state. This avoids
    // depending on Random's non-serializable internal state.
    private readonly Dictionary<(SourceLocation Site, string Name), long> _drawCounts = new();

    private int _renderBoundarySalt = 0;

    /// <summary>
    /// Returns a deterministic <see cref="Random"/> for the given call site
    /// + generator name. Same <c>(site, name)</c> returns the SAME
    /// <see cref="Random"/> reference across the same render pass — calling
    /// <see cref="Random.Next()"/> advances shared state. Reseeded at the
    /// next <see cref="ResetAtRenderBoundary"/> boundary so subsequent renders
    /// don't accumulate PRNG state.
    ///
    /// <para>
    /// NOTE: callers MUST advance the Random exclusively through
    /// <see cref="NextInt(SourceLocation, string)"/> /
    /// <see cref="NextDouble(SourceLocation, string)"/> overloads to keep
    /// snapshot/restore round-tripping. Direct <c>rng.Next()</c> calls on the
    /// returned Random bypass the draw-count bookkeeping and the post-restore
    /// state will drift. The wrapper methods are provided as the public
    /// stable advance API; Plan 36-05+ stochastic primitives use them.
    /// </para>
    /// </summary>
    public Random GetRandom(SourceLocation site, string name)
    {
        var key = (site, name);
        if (!_registry.TryGetValue(key, out var rng))
        {
            int seed = ComputeDeterministicSeed(site, name, _renderBoundarySalt);
            rng = new Random(seed);
            _registry[key] = rng;
            _drawCounts[key] = 0;
        }
        return rng;
    }

    /// <summary>
    /// Draw an int from the (site, name)-keyed Random while bookkeeping the
    /// draw count for snapshot/restore. Phase 36 stochastic primitives should
    /// route through this rather than calling <c>GetRandom(...).Next()</c>
    /// directly.
    /// </summary>
    public int NextInt(SourceLocation site, string name)
    {
        var rng = GetRandom(site, name);
        _drawCounts[(site, name)] = _drawCounts[(site, name)] + 1;
        return rng.Next();
    }

    /// <summary>
    /// Draw a double in [0, 1) from the (site, name)-keyed Random with
    /// draw-count bookkeeping.
    /// </summary>
    public double NextDouble(SourceLocation site, string name)
    {
        var rng = GetRandom(site, name);
        _drawCounts[(site, name)] = _drawCounts[(site, name)] + 1;
        return rng.NextDouble();
    }

    /// <summary>
    /// Called at <c>renderSong</c> / <c>writeWav</c> / <c>exportWav</c> entry.
    /// Clears the per-site cache so the next pass starts from fresh reseeded
    /// <see cref="Random"/>s. The render-boundary salt stays constant in v1.5;
    /// Phase 38's <c>live</c> opt-out (per RESEARCH Open Question 3) may turn
    /// it into a non-deterministic input.
    /// </summary>
    public void ResetAtRenderBoundary()
    {
        _registry.Clear();
        _drawCounts.Clear();
    }

    /// <summary>
    /// Test-only access: returns a snapshot of per-key draw counts so
    /// <see cref="ExecutionContext.SnapshotState"/> can persist them into
    /// <see cref="FlowLang.StandardLibrary.TestFramework.TestSnapshot.PrngRegistryState"/>.
    /// At restore time each Random is re-created from its deterministic seed
    /// and the captured draw count is replayed so the post-restore state matches
    /// the snapshot state EXACTLY.
    /// </summary>
    public IReadOnlyDictionary<(SourceLocation Site, string Name), long> SnapshotForTesting()
    {
        return new Dictionary<(SourceLocation Site, string Name), long>(_drawCounts);
    }

    /// <summary>
    /// Test-only restore: clears the current cache and re-creates each Random
    /// from its deterministic seed, replaying the captured draw count to bring
    /// the PRNG state to the snapshot's exact position.
    /// </summary>
    public void RestoreFromSnapshot(IReadOnlyDictionary<(SourceLocation Site, string Name), long> state)
    {
        _registry.Clear();
        _drawCounts.Clear();
        foreach (var (key, drawCount) in state)
        {
            int seed = ComputeDeterministicSeed(key.Site, key.Name, _renderBoundarySalt);
            var rng = new Random(seed);
            // Replay the captured draw count to advance the Random's state.
            // Phase 36 primitives use .Next() / .NextDouble() — both advance
            // the internal state by one draw, so replaying via .Next() restores
            // the snapshot's exact PRNG position (the SAME .Next() call's
            // implementation under the hood for both Next/NextDouble).
            for (long i = 0; i < drawCount; i++)
                rng.Next();
            _registry[key] = rng;
            _drawCounts[key] = drawCount;
        }
    }

    /// <summary>
    /// FNV-1a 32-bit stable hash combining file path + line + column + name + salt.
    /// Process-stable and platform-independent — C# <see cref="string.GetHashCode"/>
    /// is randomized per process and MUST NOT be used here. See RESEARCH §Pattern 6
    /// lines 671-687 for the canonical formula.
    /// </summary>
    private static int ComputeDeterministicSeed(SourceLocation site, string name, int salt)
    {
        unchecked
        {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;

            uint hash = fnvOffsetBasis;

            // 1. File path (UTF-8 bytes; null → empty).
            string filePath = site.FileName ?? string.Empty;
            byte[] filePathBytes = Encoding.UTF8.GetBytes(filePath);
            for (int i = 0; i < filePathBytes.Length; i++)
            {
                hash ^= filePathBytes[i];
                hash *= fnvPrime;
            }

            // 2. Line (4 bytes, little-endian-style byte-by-byte mix).
            uint lineU = unchecked((uint)site.Line);
            hash ^= (lineU & 0xFF);          hash *= fnvPrime;
            hash ^= ((lineU >> 8) & 0xFF);   hash *= fnvPrime;
            hash ^= ((lineU >> 16) & 0xFF);  hash *= fnvPrime;
            hash ^= ((lineU >> 24) & 0xFF);  hash *= fnvPrime;

            // 3. Column (4 bytes).
            uint colU = unchecked((uint)site.Column);
            hash ^= (colU & 0xFF);           hash *= fnvPrime;
            hash ^= ((colU >> 8) & 0xFF);    hash *= fnvPrime;
            hash ^= ((colU >> 16) & 0xFF);   hash *= fnvPrime;
            hash ^= ((colU >> 24) & 0xFF);   hash *= fnvPrime;

            // 4. Generator name (UTF-8 bytes).
            byte[] nameBytes = Encoding.UTF8.GetBytes(name ?? string.Empty);
            for (int i = 0; i < nameBytes.Length; i++)
            {
                hash ^= nameBytes[i];
                hash *= fnvPrime;
            }

            // 5. Render-boundary salt (4 bytes).
            uint saltU = unchecked((uint)salt);
            hash ^= (saltU & 0xFF);          hash *= fnvPrime;
            hash ^= ((saltU >> 8) & 0xFF);   hash *= fnvPrime;
            hash ^= ((saltU >> 16) & 0xFF);  hash *= fnvPrime;
            hash ^= ((saltU >> 24) & 0xFF);  hash *= fnvPrime;

            return unchecked((int)hash);
        }
    }
}
