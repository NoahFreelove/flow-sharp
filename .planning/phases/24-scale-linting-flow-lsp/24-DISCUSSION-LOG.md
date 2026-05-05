# Phase 24: Scale Linting (flow-lsp) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-04
**Phase:** 24-scale-linting-flow-lsp
**Areas discussed:** Diatonic comparison shape, Mode coverage, Scan scope inside note streams, Diagnostic message + range

---

## Area-Selection Question

| Option | Description | Selected |
|--------|-------------|----------|
| Diatonic comparison shape | Pitch-class match vs spelling-aware vs hybrid pitch-class-with-spelling-annotation | ✓ |
| Mode coverage | Major/minor only vs all 7 church modes vs major/minor + 5 modes + named non-7-note scales | ✓ |
| Scan scope inside note streams | Which NoteStreamElement subtypes get diagnostics: NoteElement, ChordElement, NamedChordElement, RomanNumeralElement, cent-offset, RandomChoiceElement, VariableReferenceElement | ✓ |
| Diagnostic message + range | Terse vs helpful, single-character vs token-wide range, source identifier choice | ✓ |

**User's choice:** All 4 areas
**Notes:** None

---

## Diatonic Comparison Shape (DA-1)

| Option | Description | Selected |
|--------|-------------|----------|
| Spelling-aware | Compare letter+accidental against the key's diatonic spellings. F#4 flagged AND Gb4 flagged (different messages). E#4 flagged in Cmajor even though pitch-class is F natural (= diatonic). Per-key diatonic-spelling set required. Aligns with Phase 23's spelling-aware JI tables. | ✓ |
| Pitch-class only | Compare semitone (0–11) against the key's diatonic semitone set. F#4 and Gb4 both flagged identically. E#4 in Cmajor NOT flagged (pitch class 5 = F natural = diatonic). Simpler. | |
| Hybrid: pitch-class with spelling annotation | Diatonicity uses pitch-class; flagged messages include spelling hint when user picked awkward spelling. Most code; needs both pitch-class set AND preferred-spelling set per key. | |

**User's choice:** Spelling-aware (Recommended)
**Notes:** None — captured as D-01 in CONTEXT.md.

---

## Mode Coverage (DA-2)

| Option | Description | Selected |
|--------|-------------|----------|
| All 7 church modes | major + natural minor + dorian + phrygian + lydian + mixolydian + locrian. Phase 23 D-04 already extended TryParseKeyWithMode for the 5 new modes. Phase 24 needs a 5-mode interval-set helper. | ✓ |
| Major + minor only | Reuse existing GetScaleNotes; key Edorian {} silently inactive for lint. Smallest blast radius; disappoints composers who use modes. | |
| Major/minor + 5 modes + named non-7-note scales | Above + pentatonic, blues, harmonic-minor, melodic-minor, whole-tone. Goes beyond REQ scope; explicit scope expansion. | |

**User's choice:** All 7 church modes (Recommended)
**Notes:** Captured as D-02 in CONTEXT.md.

### Mode Coverage Follow-up — Helper Location (DA-3)

| Option | Description | Selected |
|--------|-------------|----------|
| flow-lsp private | New flow-lsp/Diagnostics/DiatonicSpellings.cs holds the 7-mode interval arrays + per-key spelling derivation. flow-lang stays at one-line touch (PragmaRegistry only). Honors the "zero flow-lang touch" goal verbatim. | ✓ |
| Extend ScaleDatabase in flow-lang | Add ChurchModeIntervals[] arrays and extend GetScaleNotes() to call TryParseKeyWithMode internally. Two-line touch but shared with future theory tooling. | |
| flow-lang StandardLibrary (no stdlib registration) | Add ScaleDatabase.GetDiatonicSpellings(rootNote, mode) but DON'T register it as a Flow built-in — keep it C#-internal. | |

**User's choice:** flow-lsp private (Recommended)
**Notes:** Captured as D-04 / D-05 in CONTEXT.md.

---

## Scan Scope Inside Note Streams (DA-4)

| Option | Description | Selected |
|--------|-------------|----------|
| Atomic notes (NoteElement) — always | C4, F#4, Eb4. Locked by REQ LINT-01 acceptance. Always in scope. | ✓ |
| Bracket chords ([C4 E4 G4]q) — per-pitch | ChordElement contains explicit per-pitch notes; check each note independently. | ✓ |
| Cent-offset notes (E4+50c) — check base, ignore cents | NoteElement.CentOffset present. Diatonicity decided by base note; cents never trigger lint. | ✓ |
| Random-choice elements ((? C4 F#4)) — check each option | RandomChoiceElement holds list of NoteStreamElements; recurse and lint each. | ✓ |

**User's choice:** All 4 (Atomic notes, Bracket chords, Cent-offset, Random-choice)
**Notes:** All four kinds explicitly in scope. Captured as D-06 through D-09 in CONTEXT.md.

### Scan Scope Follow-up — Trickier Cases (DA-5)

| Option | Description | Selected |
|--------|-------------|----------|
| Charitable: skip undecidables, recurse where obvious | RomanNumeralElement → SKIP (in-key by construction). NamedChordElement → SKIP (intentional notation). VariableReferenceElement → SKIP (statically undecidable). TupletElement → RECURSE. RestElement → SKIP. | ✓ |
| Strict: lint everything pitched | Resolve NamedChordElement via ChordParser, lint each chord tone (F#m in Cmajor flags F#, A, C# — three diagnostics). Risks composer fatigue. | |
| Charitable + named-chord root only | Same as Charitable, except NamedChordElement flags JUST the chord ROOT if non-diatonic. Compromise. | |

**User's choice:** Charitable: skip undecidables, recurse where obvious (Recommended)
**Notes:** Captured as D-10 through D-15 in CONTEXT.md. Aligns with charitable-interpretation memory.

---

## Diagnostic Message + Range (DA-6 + DA-7)

### Message Style (DA-6)

| Option | Description | Selected |
|--------|-------------|----------|
| Helpful: identify + suggest | "F#4 not diatonic in Cmajor (try F4 or G4)" — names note, key, and 1-2 in-scale alternatives by adjacent semitone. Spelling-aware nuance: "E#4 not diatonic in Cmajor; pitch-class matches F (try F4)". | ✓ |
| Terse: identify only | "F#4 not diatonic in Cmajor." Period. No suggestion. Matches existing flow-lang ErrorReporter terseness. | |
| Hover-rich + terse squiggle | Squiggle terse; LSP hover on squiggled token shows helpful expansion. More implementation surface; uncommon LSP pattern. | |

**User's choice:** Helpful: identify + suggest (Recommended)
**Notes:** Captured as D-16 in CONTEXT.md.

### Range Width + Source Identifier (DA-7)

| Option | Description | Selected |
|--------|-------------|----------|
| Token-wide range, source 'flow.scaleLint' | Squiggle spans full note token (F#4 = 3 cols). Analyzer walks ParseResult.Tokens for Token.Text.Length. Diagnostic.Source = 'flow.scaleLint' enables independent filtering. | ✓ |
| Single-char range, source 'flow' | Reuse LspMappings.ToRange exactly. Source = 'flow' (same as parse errors). Smallest implementation. | |
| Token-wide range, source 'flow' | Token-wide squiggle but stays under shared 'flow' source. Loses per-feature filterability. | |

**User's choice:** Token-wide range, source 'flow.scaleLint' (Recommended)
**Notes:** Captured as D-17 / D-18 in CONTEXT.md.

---

## Wrap-up (Done Question)

| Option | Description | Selected |
|--------|-------------|----------|
| I'm ready for context | Pipeline integration is implementation detail — planner/researcher decides. Proceed to write CONTEXT.md. | ✓ |
| Discuss pipeline integration | One more area: should scale-lint diagnostics merge with parse errors in a single publish call, or publish independently? | |
| Explore more gray areas | Identify additional gray areas — REPL handling, scaleLint-on-without-key-block behavior, performance/debounce, test placement. | |

**User's choice:** I'm ready for context (Recommended)
**Notes:** Pipeline integration deferred to Claude's discretion (planner decides extend-publisher vs sibling-publisher vs widen-FlowError).

---

## Claude's Discretion

Surfaced in CONTEXT.md `<decisions>` "Claude's Discretion" subsection:

- Pipeline integration shape (extend `DiagnosticsPublisher.Publish` to accept a second LSP-native list, OR widen `FlowError` with a `Source` field, OR build sibling publisher).
- Whether to produce diagnostics during a partial-parse (with parse errors present) — recommended yes.
- Diatonic-spelling derivation strategy in `DiatonicSpellings.cs` — 30-key hardcoded map (recommended) vs. circle-of-fifths algorithm.
- Test layout: per-mode `.flow` smokes (7 files) vs. single `tests/test_scale_lint.flow` covering all 7 modes.
- Test placement under `flow-lang.Tests/Unit/Phase24/` (recommended) vs. new `flow-lsp.Tests/` project.
- Alternative-pitch suggestion ordering convention when the non-diatonic note is equidistant from two diatonic neighbors.
- Whether the analyzer caches per-key diatonic-spelling sets across `didChange` calls (default: don't cache; recompute per parse).

## Deferred Ideas

Captured in CONTEXT.md `<deferred>` section:

- Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone / octatonic scale support — REQ scope is church modes only.
- Quick-fix code actions ("respell as F4", "wrap in key Gmajor { }") — Phase 17 deferred code actions to a future phase.
- Borrowed-chord / modal-mixture analysis (bVII tolerance in major, neapolitan/augmented-sixth, secondary-dominant pre-allowance) — separate "harmonic-aware lint" phase if requested.
- Roman numeral mismatch warnings — different problem from scale linting.
- Hover-rich diagnostic detail — DA-6 chose helpful inline message instead.
- Configurable diagnostic severity (Warning vs Information toggle) — not in REQ; future config flag.
- "Did you mean a different mode?" suggestion when notes fit a non-declared mode — interesting future enhancement.
- Analysis of standalone notes outside `| ... |` — no surrounding key context.
- CLI lint mode (`flow-lsp -- --lint file.flow`) — promote `DiatonicSpellings.cs` to flow-lang first if requested.
- Default-on scale linting — explicitly anti-feature per REQUIREMENTS.md line 113.
