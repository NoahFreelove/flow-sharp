# Phase 39: Notation Citizenship - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-23
**Phase:** 39-notation-citizenship
**Mode:** `--auto` (autonomous single-pass discuss, all gray areas auto-selected, recommended options chosen)
**Areas discussed:** Stdlib module layout, Vendoring, MusicXML (microtonal + slur grouping + CI gate fallback + multi-track), LilyPond (nested-tuplet flattening + microtonal + multi-voice + version header), ABC (dialect coverage + multi-tune + charitable interpretation), MML (dialect coverage + charitable interpretation), Cross-cutting (MidiExport reuse + Phase 35 match usage + examples)

---

## Stdlib Module Layout

| Option | Description | Selected |
|--------|-------------|----------|
| Single `@notation` module | One `use "@notation"` exposes all 4 surfaces; mirrors Phase 33 `@sfz` precedent | ✓ |
| Two modules (`@notation-export` + `@notation-import`) | Separates output vs input concerns | |
| Four modules (`@musicxml`/`@lilypond`/`@abc`/`@mml`) | Maximal separation; 4 imports for one mental model | |

**Auto choice:** Single module (recommended — matches Phase 33 / Phase 36 stdlib precedent).
**Notes:** Existing `flow-lang/notation.flow` (musical-notation primitives, not IO) creates a naming collision; researcher decides between `@notation-io`, `@score`, `@notation-export` — recommended `@notation-io` to disambiguate from the existing in-language notation module.

---

## Vendoring Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Vendor per research/STACK.md | `matthewcpp/ABCSharp` source + `sightreader/musicxml-schemas` POCOs under `flow-lang/Vendor/`; zero new NuGets | ✓ |
| Hand-roll everything | Skip vendoring, write ABC + MusicXML schemas from scratch | |
| Add new NuGet packages | Pull `MusicXml.NET` / similar from NuGet | |

**Auto choice:** Vendor per research (recommended).
**Notes:** License verification mandatory at plan-start; hand-rolled fallback if license blocks. Vendored source follows Phase 29 sample-bundle discipline (per-bundle `LICENSE` + `VENDORED-FROM.md` with source commit hash + automated audit gate). `MusicXml.NET` rejected: parser-only (wrong direction; Flow needs writer).

---

## MusicXML — Microtonal Emission

| Option | Description | Selected |
|--------|-------------|----------|
| Always decimal `<alter>` | `cents / 100.0` precision; MuseScore renders natively | ✓ |
| Decimal `<alter>` ≤50¢, text annotation >50¢ | Hybrid threshold ladder | |
| Cent annotation as text on every microtonal note | Maximum readability for engravers | |

**Auto choice:** Always decimal `<alter>` (recommended — simplest, matches "ergonomics first", avoids UX noise on Carlos Alpha / Bohlen-Pierce).
**Notes:** REQUIREMENTS.md "when supported" interpreted as "MuseScore 3.6+ which always supports decimal alter". Text annotation ladder reserved for downstream-consumer breakage if it surfaces.

---

## MusicXML — Articulation Slur Grouping (Legato)

| Option | Description | Selected |
|--------|-------------|----------|
| Any run of ≥2 consecutive Legato notes → one slur | Cross-voice slurs permitted | |
| Same-voice consecutive Legato runs → one slur (no cross-voice) | Engraver convention | ✓ |
| Per-note `<slur>` emission, let MuseScore merge | Simpler emit, noisier output | |

**Auto choice:** Same-voice grouping (recommended — engraver convention; matches D-v1.5-08 "slur spans NOT per-note").
**Notes:** Single Legato notes get no slur (1-note slur is meaningless). Slur number is per-voice incrementing, scoped to the part.

---

## MusicXML — Round-Trip Gate Fallback (XML-02)

| Option | Description | Selected |
|--------|-------------|----------|
| Skip when `mscore` absent + advisory | Charitable interpretation; non-blocking for local dev | ✓ |
| Hard-fail CI when `mscore` not available | Strict gating | |
| Pin Docker MuseScore in CI | Maximum reproducibility, infrastructure burden | |

**Auto choice:** Skip when absent + `[xml] mscore not found — round-trip gate skipped` advisory (recommended — matches D-v1.5-05).
**Notes:** Docker pinning rejected as CI infrastructure burden Flow doesn't need at v1.5 traction level.

---

## LilyPond — Nested Tuplet Flattening

| Option | Description | Selected |
|--------|-------------|----------|
| Compute effective ratio, emit flat `\tuplet N/M` | Mathematically correct; engraver-compatible | ✓ |
| Emit nested `\tuplet` blocks anyway | Lets `lilypond` raise on incompatibility | |
| Flatten via duration-rounding to nearest non-nested | Approximate; loses precision | |

**Auto choice:** Effective-ratio flattening (recommended).
**Notes:** Example: `{3:2 {5:4 ...}}` → effective `15:8` → emit `\tuplet 15/8 {...}`. LilyPond's `\tuplet` doesn't nest cleanly without `\override TupletBracket.bracket-visibility` games.

---

## LilyPond — Microtonal Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Cent-offset comment alongside nearest 12-TET | Per REQUIREMENTS.md spec | ✓ |
| Native quarter-tone notation (`ces`/`is`/`isih`) | Quarter-precision only; rich-but-limited | |
| Scheme-based custom accidentals (cent-precision) | Maximum fidelity, complexity beyond v1.5 target | |

**Auto choice:** Cent-offset comment (recommended — per spec; engravers can manually convert).
**Notes:** Scheme-based path reserved for v1.6+ if composer demand surfaces.

---

## ABC — Dialect Coverage

| Option | Description | Selected |
|--------|-------------|----------|
| ABC 2.1 core + abc2midi `Q:` tempo + modal keys | Smallest viable surface; charitable advisory on unknowns | ✓ |
| Core + ornaments mapped to Flow articulations | Wider coverage, brittle on ornament mapping | |
| Full ABC 2.1 strict mode (error on unknowns) | Maximum correctness, breaks composer flow | |

**Auto choice:** Core + `Q:` + modal keys (recommended — matches D-v1.5-05 charitable interpretation default).
**Notes:** Unknown ornaments (`~`/`T`/`H`/`S`/`O`/etc.) dropped with one-shot `[abc] dropped ornament '{token}' at line {N}` advisory.

---

## MML — Dialect Coverage

| Option | Description | Selected |
|--------|-------------|----------|
| PC-98 common core only | Notes, accidentals, octave, length, tempo, loops | ✓ |
| Core + basic drum maps (`n<midi>`) | Wider coverage, drum-map dialect drift | |
| Multi-dialect (PC-98 + MOL + MUCOM) | Maximum compatibility, scope explosion | |

**Auto choice:** PC-98 common core (recommended — per REQUIREMENTS.md; v1.6 can add dialects).
**Notes:** Dialect-specific opcodes (FM operator routing, drum-bank selection) dropped with one-shot `[mml] dropped opcode '{token}' at offset {N}` advisory. Loop nesting depth capped at 16 (mirror Phase 36 T-36-17 DoS guard).

---

## Cross-Cutting — Pattern Matching Site

| Option | Description | Selected |
|--------|-------------|----------|
| Articulation emit via Phase 35 `(match ...)` | Natural use of pattern matching; D-v1.5-10 contract | ✓ |
| If/else ladder over articulation enum | Avoids Phase 35 dependency exposure | |

**Auto choice:** Phase 35 `(match ...)` (recommended — fulfills D-v1.5-10 dependency-root contract).
**Notes:** Articulation emit per D-v1.5-08 is one of the named consumers of Phase 35 pattern matching.

---

## Claude's Discretion

- Exact `Vendor/` directory naming (`Vendor/` vs `ThirdParty/` vs `External/`) — researcher picks; no prior precedent in codebase.
- MusicXML emit serialization: `System.Xml.Serialization` against vendored POCOs vs `XmlWriter` directly — planner benchmarks on a 100-bar score.
- LilyPond `\midi { }` block emission default — recommend keep (LilyPond users expect it).
- ABC `Q:` tempo numerator/denominator parsing edge cases — researcher checks ABCSharp's coverage, hand-fills gaps.
- MML nested loop semantics edge case (`[abc[de]2f]3`) — researcher confirms against PC-98 reference implementation.
- Tiny `flow notation convert` CLI subcommand — leans no for v1.5; revisit in Phase 41.
- Exact plan breakdown — researcher / plan-checker decide how to slice 4-6 plans.

## Deferred Ideas

- MusicXML import (anti-feature lock per FEATURES.md; defer v1.6 if demand)
- LilyPond import (engraver-DSL-shaped, not music-data-shaped)
- ABC export (no clear composer use case)
- MML export (audience essentially non-existent at v1.5 traction)
- MEI / GuitarPro / PowerTab (niche, deferred until demand)
- Custom notation DSLs (extensibility phase, not citizenship phase)
- `flow notation convert` CLI subcommand (revisit Phase 41)
- MML multi-dialect support (MUCOM/PMD/MOL) — PC-98 common core covers historical corpus
- ABC strict mode — `enable abcStrict;` pragma candidate for v1.6
- MusicXML compressed `.mxl` output — composer can re-compress via `mscore` post-hoc
