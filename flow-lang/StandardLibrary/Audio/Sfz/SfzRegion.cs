namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 — one fully-flattened SFZ region. Carries every opcode value the
/// renderer needs at lookup / render time. The <c>&lt;global&gt;</c> /
/// <c>&lt;group&gt;</c> / <c>&lt;region&gt;</c> header inheritance defined by
/// the SFZ spec is applied AT PARSE TIME by the <c>SfzParser</c> (Plan 33-04)
/// — runtime never traverses headers, every region is self-contained per
/// CONTEXT Claude's Discretion.
///
/// Field semantics (all 13 ordered, per CONTEXT § "SfzRegion field set"):
///
/// <list type="bullet">
///   <item><description><see cref="SamplePath"/> — relative to <see cref="SfzData.BasePath"/>;
///   resolved against the .sfz file's directory at sample-load time
///   (Plan 33-05 <c>SfzSampleCache.EagerLoad</c>).</description></item>
///
///   <item><description><see cref="PitchKeycenter"/> — MIDI pitch the recorded sample is at
///   (defaults to 60 / middle C in SFZ if absent).</description></item>
///
///   <item><description><see cref="LoKey"/> / <see cref="HiKey"/> — inclusive MIDI pitch range
///   the region covers (defaults <c>0..127</c> in SFZ if absent).</description></item>
///
///   <item><description><see cref="LoVel"/> / <see cref="HiVel"/> — inclusive MIDI velocity range
///   the region covers (defaults <c>1..127</c> in SFZ if absent — see Pitfall 9
///   in 33-RESEARCH for the <c>lovel=0</c> vs <c>lovel=1</c> trap; the renderer
///   clamps note velocities to <c>[1, 127]</c> at lookup-index time).</description></item>
///
///   <item><description><see cref="LoopMode"/> — see <see cref="SfzLoopMode"/>.</description></item>
///
///   <item><description><see cref="LoopStart"/> / <see cref="LoopEnd"/> — SOURCE-FRAME indices into
///   the loaded WAV (NOT seconds). Render-time clamp per Pitfall 3:
///   <c>effectiveLoopEnd = Math.Min(LoopEnd, sourceBuffer.Length - 1)</c>.</description></item>
///
///   <item><description><see cref="AmpegAttack"/> / <see cref="AmpegRelease"/> — in SECONDS
///   (SFZ-native units). Per SPEC REQ-8: when &gt; 0, these override the Phase 28
///   baseline attack/release before articulation rules layer on top. <c>0</c>
///   means "no override; use baseline." Phase 28 articulation envelope still
///   applies on top of the looped output.</description></item>
///
///   <item><description><see cref="Volume"/> — LINEAR amplitude. The parser converts the
///   SFZ <c>volume</c> dB value via <c>Math.Pow(10.0, db / 20.0)</c> at parse
///   time (Pitfall 8 in 33-RESEARCH). Runtime never sees dB. SFZ default of
///   <c>volume=0</c> dB → linear <c>1.0</c>; SFZ <c>-6</c> dB → linear ≈
///   <c>0.501</c>.</description></item>
///
///   <item><description><see cref="Pan"/> — Flow's <c>[-1.0, +1.0]</c> range. The parser
///   divides the SFZ <c>pan</c> <c>[-100, +100]</c> value by <c>100.0</c> at
///   parse time (Pitfall 7 in 33-RESEARCH). Runtime never sees the SFZ range.
///   SFZ <c>pan=0</c> → Flow pan <c>0.0</c> (centered);
///   SFZ <c>pan=+100</c> → Flow pan <c>+1.0</c> (hard right).</description></item>
/// </list>
///
/// Sealed record: immutable + structural equality + the region grid stores
/// references not copies.
/// </summary>
public sealed record SfzRegion(
    string SamplePath,
    int PitchKeycenter,
    int LoKey,
    int HiKey,
    int LoVel,
    int HiVel,
    SfzLoopMode LoopMode,
    int LoopStart,
    int LoopEnd,
    double AmpegAttack,
    double AmpegRelease,
    double Volume,
    double Pan);
