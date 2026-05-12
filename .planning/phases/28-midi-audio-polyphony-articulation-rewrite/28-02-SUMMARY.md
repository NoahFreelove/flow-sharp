---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 02
status: complete
requirements: [SPEC-4]
self_check: PASSED
test_count_before: 887
test_count_after: 901
new_facts: 17
commits:
  - 130416a feat(28-02): apply SPEC-4 locked articulation rules
  - 31ddfb1 test(28-02): pin SPEC-4 locked articulation rules — 17 facts
key_files:
  created:
    - flow-lang.Tests/Unit/Phase28/ArticulationRulesTests.cs
    - flow-lang.Tests/Unit/Phase28/ArticulationVelocityTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/BarRenderer.cs (locked duration multipliers)
    - flow-lang/Runtime/NoteStreamCompiler.cs (locked velocity rules)
---

## Plan 02 — Locked Articulation Rules at Compiler/BarRenderer

### What shipped

The cross-instrument generic layer now applies the SPEC-4 LOCKED articulation
rules. Per-synth envelope shaping (release tail, Sforzando spike) lands in
Plan 03.

1. **BarRenderer duration multipliers** (`BarRenderer.cs:67-87`):
   - Staccato `× 0.25`
   - Marcato `× 0.25` (Staccato-shortened envelope)
   - Legato `× 1.10` (Phase 28 articulation enum, NOT the Phase 22 transform)
   - Tenuto / Accent / Sforzando / Normal — duration unchanged
   - Tied-note +100 ms overlap and `note.DurationOverlap` (Phase 22 transform)
     paths are preserved; both Legato sources COMPOSE — a note with
     `Articulation.Legato` AND `DurationOverlap=0.5` ends up rendered at
     `1.0 × 1.10 × 1.5 = 1.65` of authored duration.

2. **NoteStreamCompiler velocity rules** (`NoteStreamCompiler.cs:777-794`):
   - Accent and Marcato both `Math.Min(velocity + 0.30, 1.0)`
   - Sforzando — NO scalar boost (replaces prior `velocity = 0.95` override
     that clobbered the composer's intended dynamic). Time-varying spike
     lands in Plan 03's `GenerateArticulationADSR`.
   - Legato / Tenuto / Staccato / Normal — velocity unchanged.

### Key links — verified

- `BarRenderer.cs:67` — `// Phase 28 locked articulation duration multipliers (SPEC-4):`
- `BarRenderer.cs:75-87` — switch with `Staccato 0.25`, `Marcato 0.25`,
  `Legato 1.10`, fall-through for the rest.
- `NoteStreamCompiler.cs:777` — `// Phase 28 locked velocity adjustments (SPEC-4):`
- `NoteStreamCompiler.cs:786-794` — switch with combined `Accent | Marcato`
  +0.30 case; explicit comment that Sforzando passes through.

### Truths verified by xUnit

**Duration (ArticulationRulesTests, 8 facts, ±5% tolerance, sine synth):**

| Articulation | Multiplier | Audible @ BPM 120 (C4q) |
|--------------|-----------|-------------------------|
| Normal       | 100%      | 0.50 sec                |
| Staccato     | 25%       | 0.125 sec               |
| Marcato      | 25%       | 0.125 sec               |
| Tenuto       | 100%      | 0.50 sec                |
| Legato       | 110%      | 0.55 sec                |
| Accent       | 100%      | 0.50 sec                |
| Sforzando    | 100%      | 0.50 sec                |

Plus `ArticulationRules_AllSix` cross-cut.

**Velocity (ArticulationVelocityTests, 9 facts, ±0.02 tolerance, base 0.5):**

| Articulation | Compiler velocity |
|--------------|-------------------|
| Accent       | 0.80              |
| Marcato      | 0.80              |
| Sforzando    | 0.50 (NO boost)   |
| Staccato     | 0.50              |
| Tenuto       | 0.50              |
| Legato       | 0.50              |
| Normal       | 0.50              |
| Accent base 0.9 | 1.00 (clamped) |

Plus `Marcato_StaccEnvelope_AccentVelocity` cross-cut that asserts BOTH
0.80 velocity AND 0.125 sec audible duration on a single Marcato note.

### Test counts

- Phase 28 unit facts (xUnit): **21/21 GREEN** (4 from Plan 01 + 17 new)
- Phase 22 LegatoFacts: **8/8 GREEN** (DurationOverlap path unchanged —
  `legato(seq, X)` transform stays compatible)
- Full unit suite: **901/901 GREEN** (was 887 — +14 net new facts,
  no regressions across all 60+ phases)

### Self-Check: PASSED

Build clean (no new warnings), all targeted tests pass, full suite green, no
architectural deviations from PLAN.md. Sforzando's velocity-pass-through is the
only behavioral change vs pre-Phase-28; explicitly documented in the
NoteStreamCompiler comment block so a future reader knows why the static
`0.95` was removed.

### Deviations

None. Sforzando has no parser articulation token (unlike `stacc`/`ten`/
`marc`/`leg`/`>`) — its velocity Fact constructs the `NoteElement` AST
directly (helper `CompileSingleC4q`) so the test still pins the locked
rule independent of future parser additions. This is a test-implementation
detail, not a SPEC deviation.

### Hand-off to dependent plans

- **Plan 28-03** owns the per-synth envelope shaping for the same
  articulations (release tail for Staccato/Marcato/Legato, time-varying
  spike for Sforzando). The BarRenderer duration multiplier and the
  compiler velocity boost it sees are the locked constants verified here;
  the synth-side ADSR adjustments compose with these multipliers.
- **Plan 28-04 (multi-track MIDI)** consumes Marcato's locked +0.30
  velocity boost when emitting MIDI velocity values.
- **Plan 28-06 (test infra)** RMS regression baselines must be regenerated
  for any pre-existing fixture that exercises Sforzando notes — the
  velocity change from 0.95 to base passes through to the rendered
  buffer. ByteIdentical pragma tests already audited and green during
  this plan's full-suite run.
