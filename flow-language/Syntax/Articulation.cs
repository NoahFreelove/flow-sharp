namespace FlowLang.Syntax;

/// <summary>
/// Articulation marks written after a note in a stream (<c>stacc</c>, <c>leg</c>, <c>&gt;</c>, ...).
/// Part of the grammar; renderers decide how each shapes a note's envelope.
/// Phase 28 (SPEC-3): Legato is a first-class articulation value here, separate from the
/// Phase 22 legato() transform which adjusts DurationOverlap. The Articulation.Legato value
/// is what `leg` after a note in a `|...|` stream produces; renderers extend its sounding
/// duration ~110% with a soft crossfade (BarRenderer applies the duration multiplier; per-synth
/// envelopes apply the soft release).
/// </summary>
public enum Articulation
{
    Normal,     // Default envelope
    Staccato,   // Short, detached (~50% duration)
    Tenuto,     // Full sustain, held to full value
    Marcato,    // Accented + slightly shortened
    Accent,     // Velocity bump, normal duration
    Sforzando,  // Sudden loud spike, then return to previous dynamic
    Legato      // Phase 28: extended duration (~110%) with soft crossfade into next note
}
