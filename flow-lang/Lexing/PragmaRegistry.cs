using FlowLang.Diagnostics;

namespace FlowLang.Lexing;

/// <summary>
/// Closed-set registry of recognized file-scope pragma names. Phase 21 ships
/// <c>hAsB</c> as the only entry per D-17. Future phases (23 microtonal, 24
/// scaleLint, 35 matchExhaustive) add their own entries when they ship — the
/// closed-set design guarantees unknown names error via D-12 rather than
/// silently passing.
///
/// <para>
/// Phase 35 LANG-04 Wave 2a: <see cref="SuggestNearest"/> now delegates to
/// the lifted <see cref="LevenshteinHelper.SuggestNearest"/>. The pragma
/// registry's did-you-mean path and the diagnostic renderer's did-you-mean
/// path converge on a single source of truth, matching Pitfall 5's
/// "show ONE suggestion within max(2, len/3)" recommendation.
/// </para>
/// </summary>
public static class PragmaRegistry
{
    /// <summary>
    /// Map from pragma name to a one-line human-readable description. The
    /// description is currently informational (not surfaced in errors); it lives
    /// here so the registry doubles as the canonical pragma reference.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
            ["justIntonation"] = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
            ["pythagorean"] = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
            ["equalTemperament"] = "12-tone equal temperament (default). Explicit form for tooling-visible intent.",
            ["scaleLint"] = "Phase 31 D-03: scale-lint is now default-on; this pragma is accepted as a no-op for v1.3 backward compat.",
            ["matchExhaustive"] = "Phase 35 D-v1.5-05: promote non-exhaustive match warnings to errors. File-scope only; does NOT propagate via use imports (Pitfall 4).",
            ["strict"] = "Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports."
        };

    /// <summary>True iff <paramref name="name"/> is a recognized pragma.</summary>
    public static bool IsKnown(string name) => KnownPragmas.ContainsKey(name);

    /// <summary>
    /// Returns the alphabetized csv of known pragma names for D-12 errors.
    /// Ordinal sort ensures deterministic output for tests and snapshots.
    /// </summary>
    public static string AlphabetizedKnownNames() =>
        string.Join(", ", KnownPragmas.Keys.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// Returns the closest known pragma name within distance
    /// <c>max(2, typed.Length / 3)</c>, or <c>null</c> if no candidate is
    /// close enough.
    ///
    /// <para>
    /// Phase 35 LANG-04 Wave 2a: delegates to
    /// <see cref="LevenshteinHelper.SuggestNearest"/> — both the pragma
    /// registry and the diagnostic renderer converge on the lifted helper.
    /// The threshold remains <c>max(2, typed.Length / 3)</c> per the
    /// Phase 21 choice (Pitfall 5).
    /// </para>
    /// </summary>
    public static string? SuggestNearest(string typed) =>
        LevenshteinHelper.SuggestNearest(typed, KnownPragmas.Keys);
}
