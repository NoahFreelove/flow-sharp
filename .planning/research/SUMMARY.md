# Project Research Summary — v1.3 Composer DX Tier B/C

**Project:** Flow Language
**Milestone:** v1.3 Composer DX Tier B/C — Tuplets, DEFER closures, Tier B/C bundle
**Synthesis date:** 2026-04-26
**Confidence:** HIGH

---

## Executive Summary

v1.3 is a subsequent-milestone, additive feature drop on the v1.2 base. Lead capability is tuplet brackets `(N:M ...)` and arbitrary fractional note durations (`C4/12`); milestone also closes six DEFER items from v1.2 and ships Tier B/C composer DX (arpeggio params, chord inversions, delay sync, microtonal tuning, scale linting, legato/portamento, snap-to-grid quantize, WAV pitch-shift varispeed).

All four research files converge on five strategic conclusions:

1. **Zero new external dependencies** — every feature is hand-roll territory given Flow's minimal-dep philosophy
2. **Hand-rolled `Fraction` struct** as foundational primitive (rational duration arithmetic, ~50 LOC)
3. **Two-stage pragma layering** with strict file-scope semantics (Haskell `LANGUAGE` precedent)
4. **Microtonal at the `PitchConversion.NoteToFrequency` seam** — NOT in transforms or synthesizers
5. **Gaussian humanize lands LAST** so byte-identical determinism is locked first

Principal risk is not algorithmic but **ordering** — five binding pre-ordering constraints from PITFALLS map directly into the phase sequence. The architecture/pitfalls phase-count disagreement (6 vs 18) reflects different granularity, not substantive disagreement; synthesized recommendation is **9 phases** that respect all binding orderings.

---

## Key Findings

### Stack — zero new deps confirmed

- **Existing stack covers everything**: .NET 10 + C# 13 + DryWetMidi 8.0.3 + PulseAudio + OmniSharp.Extensions.LanguageServer 0.19.9 — all unchanged
- **Hand-rolled additions**:
  - `Fraction(int, int)` struct with GCD normalization (~50 LOC)
  - OLA + linear/sinc resample for varispeed pitch-shift (~200 LOC)
  - Box-Muller transform for Gaussian humanize (~6 LOC)
  - `.scl` parser if Scala loader chosen (~50 LOC)
- **Optional housekeeping**: drop unused Pidgin 3.5.1 reference during v1.3
- **Explicitly NOT recommended**: Fractions/Rationals/BigRational (overkill for low-denominator tuplet math), SoundTouch.Net (LGPL incompatible), MathNet.Numerics (5MB+ for one Gaussian draw), NAudio/CSCore/NWaves (already excluded by PROJECT.md)

### Features — table stakes vs differentiators

**Must-have (P1):** tuplet brackets, fractional `C4/N`, DEFER-01 range, DEFER-04 enharmonic edges, DEFER-05 slice negative, DEFER-02/03 pragma + H-alias

**Should-have (P2):** DEFER-06 Gaussian, arpeggio params, chord voicings, delay sync to note values, snap-to-grid quantize, legato/portamento articulations, varispeed loadWav, scale linting, microtonal (shape varies)

**Anti-features (won't ship):** phase-vocoder time-preserving pitch shift, default-on scale linting, global H lexer alias, ABC `(p:q:r` counter form

### Architecture — `Fraction` is the root dependency

- `MusicalNoteData.DurationFraction` optional field overrides existing `DurationValue` enum when set; existing power-of-2 path unchanged when null (zero-disruption migration; all 70+ existing tests stay byte-identical)
- `TupletElement` recursive AST node mirrors music21 model; children heterogeneous via existing `NoteStreamElement` discriminated union
- Pragma two-stage layering: lexer pre-scan (Haskell-precedent regex, ~20 LOC) + interpreter `Program.Pragmas` snapshot
- Microtonal seam: `ITuningSystem` interface at `PitchConversion.NoteToFrequency`; 11 synthesizers stay pitch-agnostic (take Hz)
- flow-lsp scale-lint: zero new infrastructure (full diagnostic plumbing exists since v1.2 Phase 17, severity levels supported, `MusicalNoteData` carries `SourceLocation`/`SourceLength`)

**Blast radii by feature:**
- HBR (high blast radius, 6+ files): tuplets (9 files), microtonal (18+ files), pragma (6-7 files)
- MBR (2-4 files): arbitrary durations, chord inversions, scale lint, legato
- LBR (1-2 files): all DEFER closures + arpeggio + delay sync + quantize + varispeed

### Pitfalls — five binding pre-ordering constraints

1. **Floating-point drift** (Pitfall 1) → Rational arithmetic MUST precede tuplet syntax
2. **Pragma `use` leakage** (Pitfall 4) → Pragma MUST precede H-alias AND scale-lint; file-scope only, NEVER propagated
3. **DEFER hidden dependencies** (Pitfall 10) → DEFER spike MUST precede DEFER-04; DEFER-04 MUST precede DEFER-02/03
4. **Microtonal vs transform interaction** (Pitfall 5) → Microtonal MUST be its own phase (18+ file blast radius)
5. **PRNG determinism** (Pitfall 6) → Gaussian humanize MUST be LAST PRNG-touching phase

**Plus:**
- AUDIT-VERIFIED C5 risk (Pitfall 9) — tuplet augment/diminish silently invalidates marker without re-test
- TPQN insufficient for 7/11/13-tuplets (Pitfall 3) — auto-elevate, cap at 9600

---

## Open Decisions (User Input Required)

1. **Tuplet bracket syntax: `()` vs `{}`?**
   - Recommended: `(N:M ...)` parens (consistent with existing `(? ...)` random choice; first-token-is-Int rule disambiguates from random choice)
   - Alternative: `{N:M ...}` braces — cleaner (no existing use in note-stream context) but breaks visual symmetry with random-choice/grace/ghost prefix-paren forms

2. **Pragma scope: file vs block?**
   - Recommended: file-scope only, top-of-file only, NOT propagated via `use` (Haskell `LANGUAGE`/Rust `#![feature]` precedent)
   - Alternative: block-scope (`enable hAsB { ... }`) — finer control but additional complexity

3. **Microtonal: full Scala loader vs named-tunings wedge vs defer to v1.4?**
   - (A) Named-tunings wedge — `enable justIntonation;` / `enable pythagorean;` — ships differentiator at low cost, ~3 days
   - (B) Full Scala loader — `tuning loadScala("path.scl") { ... }` — highest payoff, dedicated phase, ~1-2 weeks
   - (C) Defer to v1.4 — removes 18-file blast radius from v1.3, lets tuplets/DX bundle bed in first

4. **DEFER-06 Gaussian: separate function vs string-discriminated overload?**
   - Recommended: separate `humanizeGaussian()` function (preserves v1.2 byte-identical determinism for existing uniform calls without ANY parser/dispatch changes)
   - Alternative: 7-arg overload `humanize(seq, amount, "gaussian")` — collapses signatures but adds string-discriminator anti-pattern

5. **TPQN cap value?**
   - Recommended: 9600 (matches existing safety patterns; SMF spec hard limit is 32767 but no DAW imports correctly above 9600)

---

## Implications for Roadmap

**Suggested phases: 9** (with binding orderings encoded)

| # | Phase | Goal | Blast Radius | Pre-orderings |
|---|-------|------|--------------|---------------|
| 0 | DEFER spike + dependency mapping | Per-DEFER design notes; no production code | None (investigation) | Binds Phase 3 + Phase 4 |
| 1 | Foundation: Rational duration arithmetic | `Fraction` struct + `MusicalNoteData.DurationFraction` | LBR | Foundation; binds Phase 2 |
| 2 | Tuplet syntax + bar-fit validator + auto-elevated TPQN | Lead capability; collision grep transcript; AUDIT-VERIFIED C5 re-validated | HBR (9 files) | After Phase 1 |
| 3 | Pragma infrastructure + DEFER-04 enharmonic edges + DEFER-02/03 H-alias | Three deliverables in correct dependency order; strict file-scope test | HBR (6-7 files) | After Phase 0 |
| 4 | Cheap DEFER closures: range + slice negative-from-end | DEFER-01 + DEFER-05; clears most of DEFER list | LBR | After Phase 0 |
| 5 | Tier B/C composer DX bundle (non-microtonal) | arpeggio + chord voicings + delay sync + snap-to-grid + legato/portamento + varispeed loadWav | MBR (per feature) | After Phase 1 (delay sync uses Fraction) |
| 6 | Microtonal tuning (own phase) | `ITuningSystem` + 12-TET no-op first, then chosen Open Decision 3 path; AUDIT-VERIFIED C1 re-validated | HBR (18+ files) | Independent; can run last |
| 7 | Scale linting (flow-lsp only) | Zero flow-lang touch; opt-in via Phase 3 pragma | flow-lsp only (MBR) | After Phase 3; can parallel Phase 6 |
| 8 | DEFER-06 Gaussian humanize (LAST PRNG phase) | Box-Muller in `MathUtils`; new `humanizeGaussian()` function; showcase byte-pin re-pinned in same commit | LBR | LAST among PRNG-touching phases |
| 9 | Tutorial + showcase refresh + closure | Every v1.3 feature in tutorial; cmp-clean determinism re-pinned | LBR | Last |

### Research Flags

**Needs research at plan-phase time:**
- Phase 0 (DEFER spike — required, this IS the research)
- Phase 1 (Rational API surface — recommended)
- Phase 2 (BarFitResult semantics + TPQN cap + collision grep — required)
- Phase 3 (lexer pre-scan regex + PragmaRegistry closed-set — required)
- Phase 6 (microtonal — required, depends on Open Decision 3)
- Phase 7 (LSP completion design + tuning interaction — recommended)
- Phase 8 (Gaussian Box-Muller pinning + CI determinism — required)

**Standard patterns (no research needed):**
- Phase 4 (cheap DEFERs — Phase 0 spike covers ambiguity)
- Phase 5 (Tier B/C — each feature has clear precedent)
- Phase 9 (tutorial — Phase 16 v1.2 precedent)

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Direct csproj inspection; zero new deps confirmed across all 4 research files |
| Features | HIGH (mental models) / MEDIUM-HIGH (exact behaviors) | Convergence verified across 6+ notation systems and DAWs |
| Architecture | HIGH | All file:line references verified against HEAD; blast-radius mapped |
| Pitfalls | HIGH | Ten pitfalls mapped to specific mechanisms; five binding pre-orderings surface as direct roadmap constraints |

**Overall confidence: HIGH.**

---

## Gaps to Address

All gaps are decisions, not research gaps — five Open Decisions listed above are user-input items. None block roadmap generation. Phase 6 (microtonal) shape varies based on Decision 3; if deferred to v1.4, Phase 6 drops out and 18-file blast radius disappears from v1.3.

---

## Sources

### Stack
- [Fractions 8.3.2 on NuGet](https://www.nuget.org/packages/fractions/)
- [Rationals 2.3.0 on NuGet](https://www.nuget.org/packages/Rationals/)
- [SoundTouch.Net 2.3.2 on NuGet](https://www.nuget.org/packages/SoundTouch.Net)
- [Scala .scl format spec — Huygens-Fokker](https://www.huygens-fokker.org/scala/scl_format.html)
- [DryWetMidi on NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi)

### Features
- [music21 Advanced Durations & Tuplets](https://music21.org/music21docs/usersGuide/usersGuide_19_duration2.html)
- [music21j Tuplet class docs](http://tarmo.uuu.ee/varia/failid/komp/music21j/doc/music21.duration.Tuplet.html)
- [Lilypond Notation Reference — Tuplets](https://lilypond.org/doc/v2.24/Documentation/notation/writing-rhythms#tuplets)
- ABC Notation 2.2 spec — Tuplets
- [Sweetwater — MIDI portamento (CC65/CC5)](https://www.sweetwater.com/insync/midi-controller-numbers-cc-msg/)

### Architecture
- [music21 duration.py source](https://github.com/cuthbertLab/music21/blob/master/music21/duration.py)
- [Formalizing Time Units to Handle Symbolic Music Durations](https://arxiv.org/pdf/2310.14952)
- Existing v1.2 `flow-lsp/NoteStream/NoteStreamContext.cs` (template for scale-lint context resolution)

### Pitfalls
- v1.2 RETROSPECTIVE.md (Top Lessons #1, #3 — audit-spike-as-its-own-phase, determinism contracts)
- v1.2 PROJECT.md (charitable interpretation memory; "music > rigid correctness")
- AUDIT-VERIFIED markers in `flow-lang/Interpreter/Interpreter.cs:292`, `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:239,261`
