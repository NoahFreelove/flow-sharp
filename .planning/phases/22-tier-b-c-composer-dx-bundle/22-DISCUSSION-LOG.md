# Phase 22: Tier B/C Composer DX Bundle - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-01
**Phase:** 22-tier-b-c-composer-dx-bundle
**Areas discussed:** legato overlap semantics, quantize swing semantic, voicing on incomplete chords
**Areas deferred to planner:** plan decomposition

---

## Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Plan decomposition | 6 per-feature plans vs 3 grouped vs 1 monolithic | |
| legato overlap semantics | Boundary behavior when legato extension hits next-note onset | ✓ |
| quantize swing semantic | What -1..1 means musically (magnitude + sign) | ✓ |
| voicing on incomplete chords | drop2/drop3/spread/open behavior when chord lacks enough notes | ✓ |

**User's choice:** legato overlap semantics, quantize swing semantic, voicing on incomplete chords
**Notes:** Plan decomposition deferred to the planner — Claude's discretion captured in CONTEXT.md.

---

## Legato overlap semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Overlap into next onset | Note duration grows by 0.2 × its own length, even past next-note onset. Renderer mixes overlapping voices (true legato phrasing). MIDI export emits overlapping note-on/off pairs. | ✓ |
| Clamp to next onset | Note duration grows but is capped at next-onset − epsilon. Notes never overlap. Safe for monophonic synths and MIDI. | |
| Fill gap only | Note extends only into existing rest-gap before next note. If notes back-to-back, no change. | |
| Param picks behavior | Add a third arg `mode` to let user pick per-call. | |

**User's choice:** Overlap into next onset
**Notes:** True polyphonic legato is the goal — leverages the existing audio renderer's poly-mix pipeline; MIDI is fine emitting overlapping note events. Duration math: `extended = duration × (1 + overlap)`.

---

## Quantize swing semantic — magnitude

| Option | Description | Selected |
|--------|-------------|----------|
| Linear: swing × (sub/2) | swing=0 → no shift; swing=1 → offbeat shifts by half a subdivision (dotted-eighth feel). Linear interpolation. Matches DAW "swing %" sliders. | ✓ |
| Triplet ratio swing | swing=0 → straight 50/50; swing=1 → jazz triplet 66.7/33.3 (2:1). Magnitude interpolated as a ratio. | |
| Percent of next subdivision | swing=N means offbeat shifts by N×100% toward next downbeat. Stronger swing per unit; smaller usable range. | |

**User's choice:** Linear: swing × (sub/2)

## Quantize swing semantic — sign

| Option | Description | Selected |
|--------|-------------|----------|
| Negative = offbeat early | swing=-0.5 shifts offbeats EARLIER by same magnitude as +0.5 shifts later. Access to push/reverse-swing feels. | ✓ |
| Negative = same as positive | Treat swing as `|swing|` internally; sign ignored. | |
| Negative on every other pair | Negative swing alternates direction per beat — asymmetric shuffle. | |

**User's choice:** Negative = offbeat early
**Combined:** `offset = swing × (subdivision_length / 2)` with signed swing ∈ [-1, 1]; positive = drag, negative = push.

---

## Voicing on incomplete chords

| Option | Description | Selected |
|--------|-------------|----------|
| Charitable: return chord unchanged | Per project memory (charitable interpretation). Voicing returns input as-is, documented in CONTEXT and code. No error, no log spam. | ✓ |
| Auto-double-then-voice | If chord too small, octave-double the root/top to bring it to minimum, then voice. Always produces a real voicing. | |
| Error with did-you-mean | Throw clear error: "voicing 'drop2' requires ≥4 notes; did you mean 'open' or 'close'?" | |
| Inline mode flag | Add `mode` param: `voicing(chord, name, "strict" | "charitable")`, default charitable. | |

**User's choice:** Charitable: return chord unchanged
**Notes:** Direct application of `feedback_charitable_interpretation.md` — silent-and-documented assumptions over errors when musical intent is clear. Symmetrical across all named voicings; documented in code via the function's doc comment with a CONTEXT D-07 breadcrumb.

---

## Claude's Discretion

- **Plan decomposition** — User declined to discuss. Recommended baseline in CONTEXT.md: 6 per-feature plans optimized for parallelism, with grouping allowed if a feature is too thin to justify its own plan.
- **`loadWav` overload disambiguation (DX-15)** — Existing `OverloadResolver` Int/Float dispatch handles `loadWav(path, 12)` (semitones=Int) vs `loadWav(path, 1.5)` (ratio=Float). No new mechanism needed.
- **Resampler choice (DX-15)** — Linear default; sinc deferred to v1.4 if quality complaints surface.
- **Portamento CC5 mapping (DX-14)** — Linear ms→CC5 default with documented reference points (0ms→0, 100ms→64, 200ms→127 clamped).

## Deferred Ideas

- Phase-vocoder time-preserving pitch shift for `loadWav` — explicit anti-feature for v1.3 (REQUIREMENTS.md line 104). v1.4 candidate.
- Auto-derived chord-tone / scale-tone arpeggio sequencing beyond basic enum (REQUIREMENTS.md line 105).
- Sinc resampler quality option for `loadWav` — clean future overload.
- Configurable portamento mapping curve — exponential or per-synth tables.
- Strict mode for `voicing` — `voicing(chord, name, "strict")` future extension.
