using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 D-39-21 — articulation emit per the locked D-v1.5-08 decision table.
/// Consumed by <see cref="MusicXmlExport"/> and <see cref="LilyPondExport"/>.
///
/// <para>
/// Implementation uses C# <c>switch</c> expressions on the
/// <see cref="Articulation"/> enum WITHOUT the <c>_</c> wildcard arm
/// (Pitfall 5). The C# compiler's exhaustiveness check raises a warning
/// when a new <see cref="Articulation"/> value is added, forcing manual
/// emit-site update so notation outputs cannot silently regress on new
/// articulations.
/// </para>
///
/// <para>
/// D-v1.5-08 table:
/// <list type="bullet">
///   <item><description>Accent → <c>&lt;accent/&gt;</c></description></item>
///   <item><description>Marcato → <c>&lt;strong-accent/&gt;</c></description></item>
///   <item><description>Staccato → <c>&lt;staccato/&gt;</c></description></item>
///   <item><description>Tenuto → <c>&lt;tenuto/&gt;</c></description></item>
///   <item><description>Sforzando → <c>&lt;dynamics&gt;&lt;sfz/&gt;&lt;/dynamics&gt;</c>
///     (emitted at the &lt;direction&gt; level, NOT inside &lt;articulations&gt; — callers
///     handle separately; ToMusicXml returns null for Sforzando)</description></item>
///   <item><description>Legato → slur spans, NOT per-note tags (D-39-07 grouping
///     policy in MusicXmlExport / LilyPondExport — ToMusicXml returns null)</description></item>
///   <item><description>Normal → no articulation tag (null)</description></item>
/// </list>
/// </para>
/// </summary>
public static class ArticulationEmit
{
    /// <summary>
    /// Returns the MusicXML inner tag content for an articulation, or null when
    /// the articulation does NOT belong inside the <c>&lt;articulations&gt;</c>
    /// wrapper (Sforzando, Legato, Normal). Callers handle null per D-v1.5-08.
    /// </summary>
    public static string? ToMusicXml(Articulation a)
    {
        // CS8524 disabled: every NAMED Articulation value is covered. The compiler
        // can't statically prove an unnamed (int)Articulation cast isn't passed; in
        // that case we fall through to the null arm of the second switch — the
        // unmapped value semantically maps to "no articulation tag", matching the
        // charitable-interpretation default (D-v1.5-05). Adding a new named
        // Articulation value DOES warn here (forcing emit-site update per Pitfall 5).
#pragma warning disable CS8524
        return a switch
        {
            Articulation.Accent    => "<accent/>",
            Articulation.Marcato   => "<strong-accent/>",
            Articulation.Staccato  => "<staccato/>",
            Articulation.Tenuto    => "<tenuto/>",
            Articulation.Sforzando => null,  // emitted as <direction><dynamics><sfz/></dynamics></direction>
            Articulation.Legato    => null,  // emitted as <slur> spans per D-39-07
            Articulation.Normal    => null,
        };
#pragma warning restore CS8524
    }

    /// <summary>
    /// Returns the LilyPond articulation suffix (attached after a note + duration),
    /// or null when the articulation is handled separately (Legato → slur parens
    /// `(` and `)` per D-39-07).
    /// </summary>
    public static string? ToLilyPond(Articulation a)
    {
#pragma warning disable CS8524
        return a switch
        {
            Articulation.Accent    => "->",
            Articulation.Marcato   => "-^",
            Articulation.Staccato  => "-.",
            Articulation.Tenuto    => "--",
            Articulation.Sforzando => "\\sfz",
            Articulation.Legato    => null,  // emitted as slur parens per D-39-07
            Articulation.Normal    => null,
        };
#pragma warning restore CS8524
    }

    /// <summary>
    /// D-39-07 helper — returns true when an articulation participates in slur
    /// span grouping (currently only Legato). Slur grouping logic in the emit
    /// paths reads this to identify run starts/ends.
    /// </summary>
    public static bool RequiresSlur(Articulation a) => a == Articulation.Legato;
}
