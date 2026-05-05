# Phase 23: Microtonal Tuning (Wedge) — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-03
**Phase:** 23-microtonal-tuning-wedge
**Areas discussed:** JI tonic resolution, Pragma → renderer plumbing, Cent + spelling under non-12-TET, MIDI export tuning awareness

---

## JI Tonic Resolution

### Q1: Tonic source

| Option | Description | Selected |
|--------|-------------|----------|
| Active `key` block (Recommended) | Read tonic from `MusicalContext.Current.Key`. Innermost-key-wins, matches Phase 24 LINT-03 nested-key semantics. Requires fallback for no-key code. | ✓ |
| Default to C if no key | Tonic = active `key`, falls back to C4 if no `key { }` is in scope. | |
| Error if no key | Hard-fail at render time if `enable justIntonation;` is active but no `key` is in scope. | |
| Pragma argument: `enable justIntonation(C4)` | Tonic baked into the pragma itself. Phase 21 pragmas are arg-less today — would be a syntax extension. | |

**User's choice:** Active `key` block.
**Notes:** Implies a fallback question for no-key code, asked next.

### Q2: No-key fallback

| Option | Description | Selected |
|--------|-------------|----------|
| Default to C major silently (Recommended) | Renderer assumes C major root, no warning. Aligns with `feedback_charitable_interpretation` memory and Phase 22 D-07 voicing pattern. | ✓ |
| Default to C, log a one-time warning to stderr | Plays under C-rooted JI but prints `[justIntonation] no key declared, rooting at C` once. | |
| Use first sounding pitch as tonic | If no key, the first non-rest note becomes the 1/1. Order-sensitive; surprising for transforms. | |
| Render as 12-TET (no-op the pragma) | Composer's pragma silently does nothing without a key. Defeats the purpose. | |

**User's choice:** Default to C major silently.
**Notes:** Documented in pragma reference + `PitchConversion.NoteToFrequency` doc comment.

### Q3: Mode handling

| Option | Description | Selected |
|--------|-------------|----------|
| Tonic only — mode ignored (Recommended) | Single 12-tone chromatic JI table rooted at the tonic. `Aminor` and `Amajor` produce identical ratios. | |
| Mode shifts the ratio table | Different modes use different reference ratio sets (natural minor → 6/5 third; dorian → 9/8+6/5+9/8 etc). | ✓ |
| Use 5-limit major table only, ignore mode entirely | `Aminor` uses major-mode JI table rooted at A. Effectively identical to option 1. | |

**User's choice:** Mode shifts the ratio table.
**Notes:** Choosing the more musicologically faithful path; expanded scope to 7 mode-specific tables.

### Q4: Mode tables to ship

| Option | Description | Selected |
|--------|-------------|----------|
| Major + natural minor only (Recommended) | Match what `ScaleDatabase` parses today. Smallest blast radius. | |
| Major + minor + standard church modes | Major + natural minor + dorian + phrygian + lydian + mixolydian + locrian. Requires extending `ScaleDatabase.ParseKeyName`. | ✓ |
| Major only — reverse the previous answer | Defer mode-aware tables to v1.4. | |

**User's choice:** Major + minor + standard church modes.
**Notes:** Phase 23 owns the `ScaleDatabase.ParseKeyName` extension for the 5 church mode suffixes — this also unblocks Phase 24 `scaleLint`.

---

## Pragma → Renderer Plumbing

### Q1: Tuning carrier

| Option | Description | Selected |
|--------|-------------|----------|
| `MusicalContext.Tuning` (file-set, not stacked) (Recommended) | Top-level (non-Push/Pop) property. FlowEngine sets once at entry. Synthesizers read symmetrically with `Key`/`Tempo`. | ✓ |
| `MusicalContext.Tuning` (stacked, Push/Pop like tempo) | Identical to tempo/key/timesig stack. Future-proofs for hypothetical block-scoped `tuning { }` (deferred per D-02). | |
| `RenderingContext` static accessor (separate from MusicalContext) | New global. Cleaner separation but new surface. | |
| Pass `ITuning` explicitly to `NoteToFrequency` | Most explicit; no global state. Costs: every Synthesizer.RenderNote signature changes. | |

**User's choice:** `MusicalContext.Tuning` (file-set, not stacked).
**Notes:** Aligns with Pitfall 5 #4 + Phase 21 plumbing patterns.

### Q2: Bridge point

| Option | Description | Selected |
|--------|-------------|----------|
| FlowEngine sets entry-point tuning; modules inherit caller (Recommended) | `FlowEngine.Run()` reads entry-point pragmas; `ModuleLoader` doesn't touch tuning. Imports run in caller's tuning. | ✓ |
| Interpreter sets tuning at every Program entry (modules override) | Module pragmas would temporarily override; contradicts Phase 21 D-06. | |
| Inject synthetic `SetTuning` statement at parse time | Visible in AST dumps; debugger-friendly. Costs: new statement type. | |

**User's choice:** FlowEngine sets entry-point tuning; modules inherit caller.
**Notes:** Matches Phase 21 D-06 + CLAUDE.md "imports execute in caller's context".

### Q3: REPL behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Tuning persists across REPL lines until reset (Recommended) | Pragma extraction stays per-line (D-07 unchanged); resolved tuning STICKS until replaced or session ends. | ✓ |
| Tuning resets per line (strict D-07) | Most consistent semantically; worst UX. | |
| REPL meta-command `:tuning ji` to set session-level tuning | Two surfaces; users will conflate them. | |
| Defer REPL tuning UX to a later phase | Phase 23 ships file-execution semantics only. | |

**User's choice:** Tuning persists across REPL lines until reset.
**Notes:** Documented departure from strict pragma scope; appears in pragma reference + REPL `--help`.

---

## Cent + Spelling Under Non-12-TET

### Q1: Spelling sensitivity

| Option | Description | Selected |
|--------|-------------|----------|
| Spelling-aware (Recommended) | `Eb4` → 6/5; `D#4` → 75/64. Ratio table keys on (note name, alteration). | ✓ |
| Semitone-based (spelling ignored) | `Eb4` and `D#4` map to the same ratio. Defeats much of JI's point. | |
| Spelling-aware in JI; semitone-based in Pythagorean | Mixed contract; harder to teach. | |

**User's choice:** Spelling-aware.
**Notes:** Honors Pitfall 5 #3.

### Q2: Cent offset composition

| Option | Description | Selected |
|--------|-------------|----------|
| Additive in cent-space (Recommended) | `freq = tonic_hz × ratio × 2^(cents/1200)`. Cents stack on top of tuning ratio. | ✓ |
| Cents ignored under non-12-TET | Composer's `+50c` becomes 0c. Surprising silent loss. | |
| Cents replace the ratio | `C4+50c` becomes a pure 50c offset above tonic, ignoring chromatic table. | |

**User's choice:** Additive in cent-space.
**Notes:** Cents always do the same thing they did in 12-TET; charitable.

### Q3: `enharmonic()` behavior under non-12-TET

| Option | Description | Selected |
|--------|-------------|----------|
| Warn once per session (Recommended) | One-time stderr warning per session: `[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)`. | ✓ |
| Silent destructive conversion | Composer beware; documented in function doc comment. | |
| No-op when tuning != 12-TET | Return input unchanged inside JI/Pythagorean. Surprising. | |
| Defer enharmonic warning to a follow-up phase | v1.4 audits enharmonic call sites. Leaves silent regression open. | |

**User's choice:** Warn once per session.
**Notes:** Matches Pitfall 5 #3 + AUDIT-VERIFIED marker; documented exception to charitable-interpretation memory.

### Q4: `transpose` MIDI round-trip under spelling-aware JI

| Option | Description | Selected |
|--------|-------------|----------|
| Honor MICR-02 verbatim, document the silent respelling (Recommended) | Transforms stay MIDI-based; renderer uses `HarmonyFunctions.GetInKeyEnharmonic` for diatonic spelling when key is active. Document the ~21c shift caveat. | ✓ |
| Make transforms spelling-preserving (slot-based) | Major scope expansion; contradicts MICR-02. | |
| Warn when transpose round-trip changes spelling under non-12-TET | One-time warning symmetric with `enharmonic()`. Per-call overhead. | |
| Defer entirely — ship JI with warning that transforms may not be spelling-safe | v1.4 addresses spelling-preserving transforms. | |

**User's choice:** Honor MICR-02 verbatim, document the silent respelling.
**Notes:** Use existing key-aware spelling via `HarmonyFunctions.GetInKeyEnharmonic` so spellings stay diatonic when a key is active. `transposePreserveSpelling` strict-mode noted as v1.4 candidate.

---

## MIDI Export Tuning Awareness

### Q1: MIDI export scope

| Option | Description | Selected |
|--------|-------------|----------|
| Audio-only; MIDI export documented as 12-TET (Recommended) | Phase 23 = synthesizer + audio only. `writeMidi` under non-12-TET emits one-time stderr warning. | ✓ |
| Tuning-aware MIDI export with pitch-bend events | Implements Pitfall 5 #2 in this phase. Scope expansion: per-voice channel allocation, external player verification. | |
| Tuning-aware MIDI export but only for the cent-offset path | Smaller scope than full tuning-aware; still expands MIDI export. | |
| Silent 12-TET MIDI export, no warning | Composer hears JI in audio, gets 12-TET MIDI silently. Pitfall 5 silent regression case. | |

**User's choice:** Audio-only; MIDI export documented as 12-TET.
**Notes:** One-time stderr warning when `writeMidi` is called under non-12-TET. Faithful microtonal MIDI export deferred to v1.4.

---

## Claude's Discretion

- Type shape of `MusicalContext.Tuning` (closed enum vs `ITuning` interface vs sealed-record) — planner decides; recommendation captured in CONTEXT.md.
- File layout under `flow-lang/StandardLibrary/Audio/Tuning/` — planner decides.
- Exact ratio values in chromatic tables — planner picks one canonical 5-limit JI table (Helmholtz/Ellis recommended) + one canonical chain-of-fifths Pythagorean table; pin with citations.
- Warning channel for D-11 / D-13 — `Console.Error.WriteLine` recommended (matches existing `transpose` warning style).
- Test placement: `tests/test_tuning_*.flow` smoke + `TuningFacts` xUnit — planner decides on split vs combined.
- Determinism gate for tutorial.flow / showcase.flow — recommendation: keep tutorial/showcase 12-TET to preserve v1.2 byte-identical pin; add separate `tests/test_tuning_determinism.flow`.

## Deferred Ideas

- Full Scala (`.scl`) loader — v1.4.
- Faithful microtonal MIDI export with per-channel pitch-bend events — v1.4.
- Spelling-preserving transforms (`transposePreserveSpelling` etc.) — v1.4 candidate.
- Block-scope `tuning { ... }` syntax — Phase 21 D-02 defers.
- Configurable A4 reference frequency (432 Hz, etc.) — v1.4+.
- Mode-aware tuning tables for harmonic minor / melodic minor / blues / etc. — future work alongside Scala loader.
- LSP pre-call warning for `enharmonic()` under non-12-TET — flow-lsp post-v1.3.
- REPL meta-command `:tuning ji` — rejected in favor of persisted-tuning behavior; revisit if confusing.
