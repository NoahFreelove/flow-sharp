namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 — the four loop-mode values recognized by the SFZ <c>loop_mode</c>
/// opcode. The set + spelling is verified against the SFZ format reference at
/// <c>https://sfzformat.com/opcodes/loop_mode</c>:
///
/// <list type="bullet">
///   <item><description><c>NoLoop</c>: SFZ <c>no_loop</c> — sample plays once, no looping.</description></item>
///   <item><description><c>OneShot</c>: SFZ <c>one_shot</c> — sample plays to its end regardless of note-off
///   (typical for percussion / non-sustaining hits).</description></item>
///   <item><description><c>LoopContinuous</c>: SFZ <c>loop_continuous</c> — loop region indefinitely
///   between <c>loop_start</c> and <c>loop_end</c> for the full note duration; release tail
///   plays after note-off.</description></item>
///   <item><description><c>LoopSustain</c>: SFZ <c>loop_sustain</c> — loop only while the note is
///   sustained; release plays the post-loop tail once note-off arrives.</description></item>
/// </list>
///
/// Pitfall: an unknown / misspelled SFZ <c>loop_mode</c> string falls back to
/// <see cref="NoLoop"/> with a one-shot stderr advisory emitted by the parser
/// (the actual fallback logic lives in Plan 33-04 <c>SfzParser</c>; this enum
/// is the value-set the parser writes into <see cref="SfzRegion.LoopMode"/>).
///
/// Also note: SFZ spec convention is that a region with <c>loop_start</c> /
/// <c>loop_end</c> declared but NO <c>loop_mode</c> opcode defaults to
/// <see cref="LoopContinuous"/>, not <see cref="NoLoop"/> — that defaulting
/// also lives in the parser (Plan 33-04), not here.
/// </summary>
public enum SfzLoopMode
{
    NoLoop,
    OneShot,
    LoopContinuous,
    LoopSustain,
}
