using System.Collections.Generic;

namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 — top-level immutable model produced by the <c>SfzParser</c>
/// (Plan 33-04) and consumed by every other Phase 33 surface (renderer,
/// sample cache, sampler:NAME dispatch). Wrapped by
/// <c>FlowLang.Runtime.Value.Sfz(SfzData)</c> for first-class language access.
///
/// Field semantics:
///
/// <list type="bullet">
///   <item><description><see cref="Description"/> — the .sfz filename, or the first non-comment
///   line of the file (parser decides). Used for diagnostic dedup keys
///   (e.g. <c>sfz:opcode:&lt;Description&gt;:&lt;name&gt;</c>) and for the
///   advisory message format.</description></item>
///
///   <item><description><see cref="BasePath"/> — directory containing the .sfz file. Joined with
///   each region's <see cref="SfzRegion.SamplePath"/> at sample-load time to
///   resolve the absolute WAV path.</description></item>
///
///   <item><description><see cref="Regions"/> — preserves SFZ declaration order. Order matters
///   twice: (a) D-02 last-declared-wins is enforced by overwriting
///   <see cref="Grid"/> cells in declaration order during the build, and
///   (b) deterministic eager-load iteration during the renderSong-walk in
///   Plan 33-05 (per Pitfall 5 — the eager-load wraps this list in
///   <c>OrderBy(SamplePath, Ordinal).ThenBy(PitchKeycenter)</c> for cross-run
///   stability anyway).</description></item>
///
///   <item><description><see cref="Grid"/> — a <c>SfzRegion?[128, 128]</c> lookup index keyed
///   by <c>(midiPitch, midiVelocity)</c> per CONTEXT D-01. The cell value is the
///   winning region under SFZ last-declared-wins (D-02 — write order encodes the
///   spec rule). A <c>null</c> cell means "no region covers that
///   <c>(pitch, velocity)</c> pair" → the renderer falls back via
///   <see cref="SortedByPitch"/>.</description></item>
///
///   <item><description><see cref="SortedByPitch"/> — deduplicated, ascending list of MIDI
///   pitches with ANY region coverage. Used by SPEC REQ-4 nearest-pitch
///   fallback per CONTEXT D-03: when <c>Grid[pitch, vel]</c> is null, find the
///   closest pitch in this index and varispeed-shift that region's sample by
///   the pitch delta. ~512 bytes per patch.</description></item>
///
///   <item><description><see cref="IsPercussion"/> — Phase 37 DRUM-01 W7 LOCK
///   (revision pass 2/3). True when this Sfz value was produced by
///   <c>loadSfz(#drums)</c> or any future percussion-class dict-symbol.
///   False for all 19 non-drum GM-dict resolutions and for the
///   <c>loadSfz(String)</c> path (the filename arg is opaque to the
///   load-time flag — composer using the string path opts out of
///   percussion routing). Drives <see cref="SfzRenderer"/>'s
///   <c>#auto</c> pitch-shift gate per CONTEXT D-37-14 / W7 LOCK: the
///   dict-symbol is the source of truth, NOT the filename. Filename
///   inspection in <see cref="SfzRenderer"/> would be fragile against
///   composer renames, VSCO-CE forks, or custom percussion-patch
///   extensions to the GM dict. Default <c>false</c> preserves existing
///   construction sites (Phase 33 parser tests, the
///   <c>loadSfz(String)</c> bypass path, fixture loaders) unchanged —
///   positional-record-with-default append is back-compat-safe.</description></item>
/// </list>
/// </summary>
public sealed record SfzData(
    string Description,
    string BasePath,
    IReadOnlyList<SfzRegion> Regions,
    SfzRegion?[,] Grid,
    int[] SortedByPitch,
    bool IsPercussion = false);
