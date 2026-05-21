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
    private readonly Dictionary<(SourceLocation Site, string Name), Random> _registry = new();
    private int _renderBoundarySalt = 0;

    /// <summary>
    /// Returns a deterministic <see cref="Random"/> for the given call site
    /// + generator name. Same <c>(site, name)</c> returns the SAME
    /// <see cref="Random"/> reference across the same render pass — calling
    /// <see cref="Random.Next()"/> advances shared state. Reseeded at the
    /// next <see cref="ResetAtRenderBoundary"/> boundary so subsequent renders
    /// don't accumulate PRNG state.
    /// </summary>
    public Random GetRandom(SourceLocation site, string name)
    {
        var key = (site, name);
        if (!_registry.TryGetValue(key, out var rng))
        {
            int seed = ComputeDeterministicSeed(site, name, _renderBoundarySalt);
            rng = new Random(seed);
            _registry[key] = rng;
        }
        return rng;
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
    }

    /// <summary>
    /// Test-only access: returns a snapshot copy of the cache so
    /// <see cref="ExecutionContext.SnapshotState"/> can persist it into
    /// <see cref="FlowLang.StandardLibrary.TestFramework.TestSnapshot.PrngRegistryState"/>.
    /// </summary>
    public IReadOnlyDictionary<(SourceLocation Site, string Name), Random> SnapshotForTesting()
    {
        // Copy the dictionary so subsequent mutations don't bleed into the
        // captured snapshot. The Random REFERENCES are shared — the test
        // restore path below repopulates from this same dict, so reference
        // sharing is the intended behavior (mirrors how Phase 35 captures
        // FixedGen by reference and rebuilds from FixedRandSeed on restore).
        return new Dictionary<(SourceLocation, string), Random>(_registry);
    }

    /// <summary>
    /// Test-only restore: clears the current cache and re-populates from the
    /// snapshot. Called by <see cref="ExecutionContext.RestoreState"/>.
    /// </summary>
    public void RestoreFromSnapshot(IReadOnlyDictionary<(SourceLocation Site, string Name), Random> state)
    {
        _registry.Clear();
        foreach (var (key, rng) in state)
        {
            _registry[key] = rng;
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
