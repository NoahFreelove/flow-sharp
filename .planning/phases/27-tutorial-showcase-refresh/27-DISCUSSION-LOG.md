# Phase 27: Tutorial + Showcase Refresh — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-10
**Phase:** 27-tutorial-showcase-refresh
**Areas discussed:** QOL-04 scope + Phase 26.2 features, showcase.flow refresh strategy, chapter integration strategy, pragma demonstration

---

## QOL-04 Scope + Phase 26.2 Coverage

### Q1 — REQUIREMENTS.md QOL-04 vs Phase 26.2 features?

| Option | Selected |
|--------|----------|
| Update QOL-04 + demo Phase 26.2 features | ✓ |
| Leave QOL-04 as-is, demo Phase 26.2 anyway | |
| QOL-04 authoritative — skip Phase 26.2 in tutorial | |

**Notes:** Mirrors Phase 26.1 closure pattern that rewrote DICT-01/02/03.

### Q2 — Phase 26.2 chapter placements?

| Option | Selected |
|--------|----------|
| gain/volume split — own chapter | ✓ |
| Hertz literals — inline into chapter 9 'Effects' | ✓ |
| Ms-FX overloads (delay/compress/sidechain) — inline into chapter 9 'Effects' | ✓ |
| Second-decay reverb — inline into chapter 16 'Reverb Time' | ✓ |

**Notes:** Full Phase 26.2 surface integration.

### Q3 — Audible-in-graduation-song features from Phase 26.2?

| Option | Selected |
|--------|----------|
| volume() for section dynamics | ✓ |
| Hertz literal in a filter sweep | ✓ |
| Ms-typed delay on a pad or lead | ✓ |
| Second-decay reverb wrapping a section | ✓ |

**Notes:** All four make the graduation song.

### Q4 — Music Types Quick Reference in CLAUDE.md?

| Option | Selected |
|--------|----------|
| Yes — small reference table | ✓ |
| No — tutorial.flow is enough | |
| Defer to a follow-up docs phase | |

**Notes:** Single source of truth for the music-type surface.

---

## Showcase.flow Refresh Strategy

### Q1 — Refresh strategy for current 44-line v1.2 ambient piece?

| Option | Selected |
|--------|----------|
| Replace with new v1.3 piece | ✓ |
| Keep v1.2 piece + add showcase_v1_3.flow | |
| Refresh in-place — add v1.3 features without changing arrangement | |

**Notes:** Pre-public, single canonical showcase preserved.

### Q2 — Genre / mood?

| Option | Selected |
|--------|----------|
| Polyrhythmic minimal (tuplet-forward) | ✓ |
| Microtonal ambient (tuning-forward) | |
| Modern jazz / fusion | |
| Dance / EDM-leaning | |

**Notes:** 120 BPM, tuplet groove + euclidean drum + ambient pad bed + soft melody. Microtonal pragma activated for JI flavor on pad. Dict-driven drum keyed by Symbol.

### Q3 — Length cap?

| Option | Selected |
|--------|----------|
| 60-80 lines | |
| Up to 120 lines | |
| No cap — whatever the piece needs | ✓ |

**Notes:** Phase 16 D-02 precedent applies — concision preferred not enforced.

### Q4 — Byte-identical determinism contract?

| Option | Selected |
|--------|----------|
| Update existing fact files in Phase 27 closure | ✓ |
| Add new Phase 27 fact files alongside | |
| Skip showcase byte-pin in Phase 27 | |

**Notes:** Single canonical contract — Phase18 + Phase25 fact files refresh their pinned bytes.

---

## Chapter Integration Strategy

### Q1 — How do v1.3 features land?

| Option | Selected |
|--------|----------|
| Phase 16 D-01 precedent: weave by domain | |
| Big v1.3 capabilities mega-chapter at end | |
| Numbered v1.3 sub-chapters (20.1, 20.2, ...) | |
| Hybrid: weave language features, batch music features | ✓ |

**Notes:** Compromise — language features by domain, music features consolidated.

### Q2 — Language-feature weave plan?

| Option | Selected |
|--------|----------|
| Prefix arithmetic — update chapter 2 | ✓ |
| Symbols — new chapter after 'Variables and Basic Types' | ✓ |
| Tuples + ~> unpack — new chapter after Collections | ✓ |
| Dict<K, V> — new chapter after Tuples | ✓ |

**Notes:** Symbols → Tuples → Dict ordering matters because Dict's `(each)` callback depends on `~>` semantics.

### Q3 — Music features mega-chapter sub-sections?

| Option | Selected |
|--------|----------|
| Tuplets + fractional durations | ✓ |
| Microtonal tuning + scale-lint pragmas | ✓ |
| Composer DX bundle (DX-10..15) | ✓ |
| Range, multi-letter enharmonics, negative slice, hAsB pragma, humanizeGaussian (misc small wins) | ✓ |

**Notes:** All four sub-sections.

### Q4 — Graduation song?

| Option | Selected |
|--------|----------|
| New v1.3 graduation song — replace Phase 16's | ✓ |
| Keep Phase 16 song + add v1.3 sequel song | |
| Keep Phase 16 song verbatim, add v1.3 features in-place | |

**Notes:** Single canonical graduation. Mirrors showcase replace.

---

## Pragma Demonstration

### Q1 — Strategy for 3 file-scope pragmas?

| Option | Selected |
|--------|----------|
| Multi-file under examples/pragmas/ | ✓ |
| Inline mini-demo — print-only output + paste-ready snippets | |
| Pick one canonical pragma to actually run — microtonal | |
| Split tutorial into 3 .flow files | |

**Notes:** Each pragma gets a dedicated companion file demo.

### Q2 — Companion files to ship?

| Option | Selected |
|--------|----------|
| examples/pragmas/h_alias.flow | ✓ |
| examples/pragmas/microtonal_ji.flow | ✓ |
| examples/pragmas/microtonal_pythagorean.flow | |
| examples/pragmas/scale_lint.flow | |

**Notes:** Pythagorean overlaps too much with JI; scale-lint is flow-lsp-only.

### Q3 — Byte-identical determinism for companion files?

| Option | Selected |
|--------|----------|
| Yes — Phase27ByteIdenticalPragmaTests for both files | ✓ |
| Only the JI companion gets byte-pinned | |
| No byte-pin — manual cmp via run twice | |

**Notes:** New fact class under flow-lang.Tests/Unit/Phase27/.

### Q4 — Output directory for companion files?

| Option | Selected |
|--------|----------|
| Same examples/output/ directory | ✓ |
| Separate examples/pragmas/output/ | |

**Notes:** Single .gitignore rule covers all artifacts.

---

## Claude's Discretion

- Chapter ordering may reshuffle minor numberings if it improves readability.
- Graduation song key + tempo within the polyrhythmic-minimal genre frame (D-202 sets showcase genre, tutorial graduation is independent).
- Tutorial graduation pragma activation — default no pragma, can activate JI if benefits audibly.
- Companion file synth choices (sine for JI ratio, piano for h_alias likely).
- Section structure of the v1.3 graduation song.
- Tutorial chapter rewrites for prefix-arithmetic context if examples read awkwardly.

## Deferred Ideas

- Pythagorean microtonal companion file — wait for composer demand.
- Scale-lint companion file — flow-lsp owns; defer to docs phase.
- mHz (millihertz) literal demo — wait for LFOs.
- frequencyToNote(Hertz) → Note helper demonstration — Phase 26.2 RESEARCH Open Q1 resolved as not-needed.
- Full Scala (.scl) loader companion file — v1.4.
- Tutorial split into multiple files — rejected for breaking single-tutorial mental model.
- Comment-style refresh — Phase 16 D-09 split carries over verbatim.
- Tutorial --watch mode dedicated chapter — composer DX, not language feature.
