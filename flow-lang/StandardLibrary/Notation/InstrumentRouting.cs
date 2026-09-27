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
    public static string StripSamplerPrefix(string name) => Flow.Music.IO.InstrumentRouting.StripSamplerPrefix(name);

    /// <summary>
    /// Maps a Sequence's name to a (GM program, MIDI channel) pair. The table lives in
    /// <see cref="Flow.Music.IO.InstrumentRouting"/> so snapshot export shares it.
    /// </summary>
    public static (int gmProgram, int channel) ResolveGmProgram(string seqName) =>
        Flow.Music.IO.InstrumentRouting.ResolveGmProgram(seqName);
}
