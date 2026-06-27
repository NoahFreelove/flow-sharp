namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 D-39-20 — shared GM-program + channel routing for instrument names.
/// Extracted from <see cref="FlowLang.StandardLibrary.Audio.MidiExport"/> so that
/// the Phase 39 MusicXML + LilyPond emit paths consume the same table as MIDI export.
/// This is the single source of truth for sequence-name → GM-program mapping across
/// every notation surface (MIDI, MusicXML, LilyPond).
///
/// <para>
/// Ordering significance: the more-specific Phase 33 entries (violin, viola, cello,
/// contrabass, oboe, clarinet, bassoon, horn, trombone, tuba, timpani, choir, harp,
/// guitar, harpsichord, celeste) MUST be checked BEFORE the Phase 28 generic entries
/// (piano, brass, bass, sax, flute, string, organ, bell, drum). In particular,
/// <c>horn</c> MUST precede <c>brass</c> because the Phase 28 <c>brass</c> entry
/// historically also matched <c>horn*</c>; Phase 33 D-16 reassigns <c>horn → 60</c>
/// (French horn). Likewise <c>bassoon</c> (GM 70) MUST precede the sweep-0614
/// generic <c>bass</c> entry (GM 32) since both share the <c>bass</c> prefix.
/// </para>
///
/// <para>
/// The <c>sampler:</c> prefix is stripped BEFORE any StartsWith check so
/// <c>sampler:NAME</c> routes to the same GM program as <c>NAME</c> alone.
/// </para>
/// </summary>
public static class InstrumentRouting
{
    /// <summary>
    /// Strip the <c>sampler:</c> prefix if present so the GM lookup and any
    /// downstream track-name meta-event both see the canonical instrument name.
    /// </summary>
    public static string StripSamplerPrefix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.StartsWith("sampler:", System.StringComparison.OrdinalIgnoreCase)
            ? name.Substring("sampler:".Length)
            : name;
    }

    /// <summary>
    /// Maps a Sequence's name to a (GM program, MIDI channel) pair using
    /// case-insensitive prefix matching. Drum sequences route to channel 9
    /// (GM percussion). All other instrument prefixes default to channel 0.
    /// Unrecognized names default to GM 0 (acoustic grand piano), channel 0.
    /// </summary>
    public static (int gmProgram, int channel) ResolveGmProgram(string seqName)
    {
        if (string.IsNullOrEmpty(seqName)) return (0, 0);

        string stripped = StripSamplerPrefix(seqName);
        string lower = stripped.ToLowerInvariant();

        // Phase 33 D-16 — more-specific names first. `horn` MUST come before
        // `brass` because the Phase 28 brass entry historically swallowed
        // horn* sequences; D-16 reassigns horn → 60 (French horn).
        if (lower.StartsWith("violin"))      return (40, 0);
        if (lower.StartsWith("viola"))       return (41, 0);
        if (lower.StartsWith("cello"))       return (42, 0);
        if (lower.StartsWith("contrabass"))  return (43, 0);
        if (lower.StartsWith("oboe"))        return (68, 0);
        if (lower.StartsWith("clarinet"))    return (71, 0);
        if (lower.StartsWith("bassoon"))     return (70, 0);
        if (lower.StartsWith("horn"))        return (60, 0);
        if (lower.StartsWith("trombone"))    return (57, 0);
        if (lower.StartsWith("tuba"))        return (58, 0);
        if (lower.StartsWith("timpani"))     return (47, 9);  // channel 9 = percussion
        if (lower.StartsWith("choir"))       return (52, 0);
        if (lower.StartsWith("harp"))        return (46, 0);
        if (lower.StartsWith("guitar"))      return (24, 0);
        if (lower.StartsWith("harpsichord")) return (6, 0);
        if (lower.StartsWith("celeste"))     return (8, 0);

        // Phase 28 entries (ordering must come AFTER Phase 33 entries so
        // horn/violin/etc. don't fall through to brass etc.)
        if (lower.StartsWith("piano"))  return (0, 0);
        if (lower.StartsWith("brass"))  return (56, 0);
        // sweep-0614 (gap-routing-tuning-format): a `bass*`-named sequence
        // previously fell through to GM 0 (acoustic grand piano). Route it to
        // GM 32 (Acoustic Bass). MUST come after the Phase 33 `bassoon` entry
        // (program 70) above — `bassoon` shares the `bass` prefix, and the
        // more-specific check is ordered first so it isn't swallowed here.
        if (lower.StartsWith("bass"))   return (32, 0);
        if (lower.StartsWith("sax"))    return (65, 0);
        if (lower.StartsWith("flute"))  return (73, 0);
        if (lower.StartsWith("string")) return (48, 0);
        if (lower.StartsWith("organ"))  return (19, 0);
        if (lower.StartsWith("bell"))   return (14, 0);
        if (lower.StartsWith("drum"))   return (0, 9);
        return (0, 0);
    }
}
