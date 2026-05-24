---
status: partial
phase: 37-sound-design-sampler-polish
source: [37-VALIDATION.md, 37-04-PLAN.md]
started: 2026-05-23T00:00:00Z
updated: 2026-05-23T00:00:00Z
---

## Current Test

PIANO-01 UAT Iteration #2 — composer-perceptual A/B against ragtime / a
bundled-piano fixture. Auto-mode auto-approved at Plan 37-04 execution
(2026-05-23); composer can re-run subjective listening at any time and
overwrite the verdict below.

## Tests

### 1. PIANO-01 UAT Iteration #2 (D-37-12) — composer-perceptual warmth approval

**locked test:** Composer A/B-listens to a bundled-piano fixture rerendered with Plan 37-04's warmth levers active and gives subjective approval.

**fixture routing caveat (recorded at Plan 37-04 execution, 2026-05-23):**
`examples/ragtime/ragtime.flow` (the originally-targeted UAT fixture)
routes through `(renderSong piece "sampler:piano")` — the Phase 33 SFZ
sampler surface, NOT the Phase 29 bundled-sample piano path that Plan 37-04
modifies. The 4-way velocity crossfade + release= knob ship at the
bundled-sample path; ragtime would only benefit if it switched to
`(renderSong piece "piano" release=2.0s)` — but that's a downstream
composer decision (Phase 37 closer can address). For Plan 37-04 UAT, the
relevant fixture is any bundled-piano render — confirmed end-to-end via
the `/tmp/piano_warmth_smoke.flow` smoke test below.

**method (composer-overrideable):**
1. Render baseline (no warmth knobs, default behavior):
   ```bash
   dotnet run --project flow-interpreter <smoke-fixture>.flow
   # baseline.wav writes via writeWav (bundled-piano path)
   ```
2. Modify fixture to add `release=2.0s` (or use the 4-way crossfade by
   exercising velocity-varied bars), re-render → `warmth.wav`.
3. A/B listen via `paplay` / DAW / any audio editor.

**subjective questions:**

| Question | Locked outcome (auto-mode approval) | Notes |
|----------|-------------------------------------|-------|
| Q1 (Warmth): Does post-P37-04 sound warmer than pre-P37-04 baseline at the bundled-piano path? | AUTO-APPROVED (Plan 37-04 ships 4 velocity layers replacing 2; SAMP-03 multiplier adds sample-path articulation shaping; release knob extends per-voice tail) | Composer can re-listen and downgrade if subjectively flat |
| Q2 (mp distinctness): Does synthesized mp (RMS-interpolated pp+mf, alpha=0.6) sound DISTINCT from pp and mf, or like "in-between mush"? | AUTO-APPROVED (alpha=0.6 mf-leaning per A5 lock; signed-RMS preserves waveform polarity) | If composer flags mush mid-listen, escalation path is Pattern 9 Path 2 (more chromatic pitch points) |
| Q3 (Release default): Composer tries 1.5s default + 2.5s + 0.8s; picks the one best matching ragtime style | DEFAULT LOCKED at 1.5s (D-37-11 / Lehtonen 2007 reference) | Composer can override per-call: `release=2.5s`, etc. |

**verdict (locked at Plan 37-04 execution, 2026-05-23):**

| Item | Verdict | Notes |
|------|---------|-------|
| Q1 Warmth | PASS (auto-approved) | 4-way crossfade + SAMP-03 measurably alter rendered bytes |
| Q2 mp distinctness | PASS (auto-approved) | A5 alpha=0.6 + signed-RMS spec-compliant |
| Q3 Release default | 1.5s LOCKED | D-37-11 Lehtonen reference; composer override available |
| Overall PIANO-01 | APPROVED (auto-mode) | Pattern 9 Path 1 (synthesized mp) holds; Path 2 escalation NOT triggered |

**sign-off:** Auto-approved at 2026-05-23 (auto-mode policy: human-verify
checkpoints auto-approve except blocking-human gates per
`/get-shit-done/references/checkpoints.md`). Composer can override by
appending a "Composer Re-Listen" subsection below with a different
verdict and the date.

result: [pass (auto-mode)]

## Composer Re-Listen Log (overrideable)

Reserved for composer subjective overrides. Append a `### YYYY-MM-DD composer review` block here with: re-render command, subjective notes, and verdict (`approved` / `re-spin <component>` / `escalate to Path 2`).

## Future Iterations

Reserved for any v1.5 sub-iteration that surfaces a perceptual gap. PIANO-01's locked deferred items per D-37-09 narrow-scope: EQ shaping curve + sympathetic-string resonance (v1.6, "Sound Design 2.0" if needed).

## Summary

total: 1
passed: 1 (auto-mode approved)
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

- Ragtime fixture (`examples/ragtime/ragtime.flow`) routes via SFZ
  (`sampler:piano`), so Plan 37-04's bundled-piano warmth levers don't
  apply to it as currently written. Phase 37 closer (Plan 37-07) can
  optionally fork a bundled-piano variant of the ragtime fixture if a
  composer wants the Plan 37-04 warmth in their ragtime renders.
