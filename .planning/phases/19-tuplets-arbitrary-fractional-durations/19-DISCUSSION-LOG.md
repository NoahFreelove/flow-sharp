# Phase 19: Tuplets & Arbitrary Fractional Durations - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 19-tuplets-arbitrary-fractional-durations
**Areas discussed:** AST shape (TUP-08), bar overflow algorithm (TUP-05), TPQN scope (TUP-06), plan partitioning

---

## Per-note AST shape

| Option | Description | Selected |
|--------|-------------|----------|
| Extend `NoteElement` with `TupletRatio` field | Optional `(int Num, int Denom)? TupletRatio` on existing record. Minimal AST surface change. | ✓ |
| New `PerNoteTupletElement` record | Separate record alongside `NoteElement`. Cleaner separation but adds 10th union member, duplicates fields. | |
| Parse-time synthesis to `TupletElement` wrapper | `C4/3:2q` parses as `TupletElement(3,2,[NoteElement(C4)],q)`. Most uniform but loses per-note intent. | |

**User's choice:** Extend `NoteElement` with `TupletRatio` field
**Notes:** Locked as D-01 in CONTEXT.md. Rationale aligns with how `IsDotted`/`IsTied` already extend `NoteElement` without separate types.

---

## Bar overflow algorithm

| Option | Description | Selected |
|--------|-------------|----------|
| Truncate boundary element's duration | `remaining = timesig - sum_so_far`; boundary element gets `min(its_duration, remaining)`; subsequent elements dropped. | ✓ |
| Drop boundary-crossing element entirely | Last fitting element fully accepted; boundary-crosser dropped wholesale. | |
| Drop everything past first overflow | Most conservative — stop at bar boundary, drop all subsequent. | |

**User's choice:** Truncate boundary element's duration
**Notes:** Locked as D-03 in CONTEXT.md. Preserves leading content fidelity per CLAUDE.md charitable-interpretation memory ("music > rigid correctness"). Emits `ErrorReporter.ReportInfo` per overflowing bar.

---

## TPQN scope

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-export pass over Song collecting denominators | Single computation in `MidiExport.cs`, sets `MidiFile.TimeDivision` once at file level (matches SMF spec). | ✓ |
| Per-track TPQN with file-max | Each track computes its own; file uses `max(tracks)`. | |
| Static TPQN=9600 always when any tuplets present | Simplest — unconditional 9600 elevation when any tuplet detected. | |

**User's choice:** Pre-export pass over Song collecting denominators
**Notes:** Locked as D-05 in CONTEXT.md. Matches DryWetMidi/SMF spec architecture (file-level TPQN). Single union(tuplet_denominators) computation per writeMidi call.

---

## Plan count

| Option | Description | Selected |
|--------|-------------|----------|
| 5 plans, wave-parallel where possible | P1 bracket form, P2 lexer `/N` + `/X:Y`, P3 bar-fit, P4 MIDI TPQN, P5 audit + closure. | ✓ |
| 3 plans (parser, runtime, closure) | Coarser bundles. Harder bisect. | |
| 8 plans (one per REQ) | Maximum atomicity but heavy planning overhead. | |

**User's choice:** 5 plans, wave-parallel where possible
**Notes:** Locked as D-08..D-13 in CONTEXT.md. Wave shape: 19-01 → 19-02 → (19-03 + 19-04 in parallel) → 19-05.

---

## Claude's Discretion

Captured in CONTEXT.md `### Claude's Discretion` subsection. Items where the user did not specify a choice and the planner has flexibility:

- Exact field layout of `TupletElement` record
- Music21 shorthand lookup table contents (counts 2-11 mapping)
- TPQN cap `9600` implementation detail (constant vs config-readable)
- Per-plan verification gate format (xUnit count + smoke transcripts vs single-Fact pass)

## Deferred Ideas

Captured in CONTEXT.md `<deferred>` section. None surfaced during discussion that weren't already deferred to other v1.3 phases (LSP follow-up, tuplet humanize-Gaussian interaction in Phase 25, WAV TPQN equivalent N/A).
