---
phase: 27
slug: tutorial-showcase-refresh
type: discuss
gathered: 2026-05-10
status: ready-for-planning
---

# Phase 27: Tutorial + Showcase Refresh — Context

**Gathered:** 2026-05-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Refresh `examples/tutorial.flow` (currently 684 lines, ~19 chapters, last touched Phase 16 for v1.2) and `examples/showcase.flow` (currently 44 lines, "v1.2 Ambient Piece") so a new user running them once experiences EVERY v1.3 feature end-to-end. Byte-identical determinism contract holds across two consecutive runs (`cmp`-clean) for both files. Existing v1.1 + v1.2 chapter coverage preserved.

**ROADMAP success criterion #1 has a known gap** — it omits Phase 26.2's additions (volume, Hertz literals, Ms-FX overloads, Second-decay reverb, createXxxTone-Hertz, gain-vs-volume split). Phase 27 closure rewrites QOL-04 to include them (D-101) and tutorial.flow demonstrates them.

**v1.3 final phase.** When Phase 27 closes, v1.3 milestone (12/12 phases) is shippable.

**In scope (every v1.3 feature must be demonstrated):**

Language additions:
- Prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(idiv)`/`(neg)`/`(concat)` (Phase 26)
- Symbol primitive `#foo` (Phase 26.1 SYM-01)
- Tuple `<<a, b, c>>` + `tup@N` indexing + `<<a, b>> = expr` destructuring + `~>` flow op + `(unpack)` runtime (Phase 26.1 TUP-09/10/11)
- Generic `Dict<K, V>` with 14-op surface (Phase 26.1 DICT-01/02/03)

Music features:
- Tuplets `{3:2 ...}q` + per-note `C4/12` fractional + nested tuplets (Phase 19 TUP-01..08)
- `range(Int, Int)` / `range(Int, Int, Int)` builtin (Phase 20 DEFER-01)
- Multi-letter enharmonics E↔Fb / F↔E# / B↔Cb / C↔B# (Phase 20 DEFER-04)
- Negative slice `arr@-1` / `slice(arr, -3, _)` Python-style (Phase 20 DEFER-05)
- `enable hAsB;` H-as-B alias pragma (Phase 21 DEFER-02/03 + PRAG-01/02)
- DX-10..15 bundle: arpeggio with rate+direction+pattern; chord inversions+voicings; NoteValue-rate delay; quantize; legato/portamento; varispeed-loadWav (Phase 22)
- Microtonal pragmas `enable justIntonation;` / `pythagorean;` / `equalTemperament;` (Phase 23 MICR-01..03)
- `enable scaleLint;` pragma (Phase 24 LINT-01..03 — flow-lsp surfaces diagnostics; tutorial documents print-only)
- `humanizeGaussian(seq, amount, seed)` (Phase 25 DEFER-06)

Phase 26.2 surface (NEW — not in QOL-04 yet, added per D-101):
- `volume(Buffer, Double)` linear-multiplier function (Phase 26.2 ERG-03)
- `gain` dB-only / `volume` linear split (Phase 26.2 ERG-03 D-04..D-07)
- Hertz literals `440Hz` / `1.5kHz` (Phase 26.2 ERG-04 D-11..D-13)
- `Millisecond.IsCompatibleWith(Double|Float)` + `Second.IsCompatibleWith(Double|Float)` + new `HertzType` (Phase 26.2 ERG-01)
- Ms-FX overloads on `delay` / `compress` / `sidechain` (Phase 26.2 ERG-02)
- Second-decay reverb `(reverb buf mix 1.5s)` (Phase 26.2 ERG-02)
- Hertz overloads on `lowpass` / `highpass` / `bandpass` filters (Phase 26.2 ERG-04)
- Hertz overloads on `createSineTone` / `createSawTone` / `createSquareTone` / `createTriangleTone` (Phase 26.2 ERG-04)
- `(gain buf -12dB)` literal closure (Phase 26.2 ERG-05)

**Out of scope (capture for a later phase):**
- Phase 16 D-09 comment-style refresh (only refresh comments where v1.3 requires explanation)
- `mHz` (millihertz) literals — wait for LFOs
- Full Scala loader (`.scl`) — deferred to v1.4
- `frequencyToNote(Hertz) → Note` helper — Phase 26.2 RESEARCH Open Q1 resolved as not needed
- Phase-vocoder time-preserving pitch shift — explicit anti-feature for v1.3
- `enable scaleLint;` actual diagnostic rendering — flow-lsp owns; tutorial documents but doesn't run

</domain>

<decisions>
## Implementation Decisions

### 1. QOL-04 Scope + Phase 26.2 Coverage

- **D-101:** Phase 27 closure rewrites REQUIREMENTS.md QOL-04 to include the Phase 26.2 surface (`volume(buf, linear)`, Hertz literals, Ms-FX overloads, Second-decay reverb, createXxxTone-Hertz, gain-vs-volume split). Mirrors the pattern Phase 26.1 closure used to rewrite DICT-01/02/03 entries against landed scope. Tutorial.flow demonstrates every item in the rewritten QOL-04.
- **D-102:** Phase 26.2 features land in tutorial.flow as follows:
  - `gain`/`volume` split — **own chapter** (the most footgun-prone v1.3 concept; deserves dedicated explanation).
  - Hertz literals (`440Hz` / `1.5kHz`) — **inline into existing chapter 9 'Effects and the Audio Pipeline'**.
  - Ms-FX overloads (delay/compress/sidechain) — **inline into chapter 9 'Effects'**, replacing existing examples that pass bare `Double` for time params.
  - Second-decay reverb — **inline at end of chapter 16 'Reverb Time'**.
- **D-103:** Audible-in-graduation-song features from Phase 26.2 (per Phase 16 D-07 pattern):
  - `volume(buf, linear)` for section dynamics — replaces or sits alongside the existing per-section `gain` graduation use.
  - Hertz literal in a filter sweep — `(lowpass renderedSection 1.2kHz)` somewhere in the song.
  - Ms-typed delay on a pad or lead — `(delay melodyBuf 250ms 0.5 0.4)`.
  - Second-decay reverb wrapping a section — either `(reverb sectionBuf 0.5 1.8s)` on a tail OR a `reverbTime { ... }` context block (planner picks whichever reads cleanest).
- **D-104:** CLAUDE.md gains a "Music Types Quick Reference" table appended to "Language Features → Music-Specific" — single table with columns: literal | type | IsCompatibleWith | accepted at. ~20 lines. Helps composers AND future agents scope work. Single source of truth for the music-type surface.

### 2. Showcase.flow Refresh Strategy

- **D-201:** **Replace** the v1.2 ambient piece with a new v1.3 showcase. Pre-public, no legacy users — single canonical `examples/showcase.flow` reflecting the current language surface. The "wow listen to this" compact-demo role is preserved.
- **D-202:** Genre / mood = **polyrhythmic minimal (tuplet-forward)**. 120 BPM Cmajor (or planner-picked key — JI tuning constrains key choice somewhat). Foreground a `{3:2 ...}q` tuplet groove + `euclidean` drum + ambient pad bed + a soft melody. Microtonal pragma activated for JI flavor on the pad. **Dict-driven drum pattern** keyed by `Symbol` (`#kick #snare #hihat`) using the new 14-op Dict surface. Phase 26.2 FX woven in: filter sweep at section boundary (Hertz), volume automation on the pad, Ms-typed delay on the lead, Second-decay reverb tail.
- **D-203:** **No length cap** (Phase 16 D-02 precedent applies — concision preferred but not enforced). Showcase may grow past 80 lines if the piece needs it. Composer feel wins.
- **D-204:** **Update existing fact files** in Phase 27 closure to refresh the byte-pin assertions in `Phase18ByteIdenticalShowcaseTests` + `Phase25ByteIdenticalShowcaseGaussianTests`. Both classes' test purpose stays valid (lock byte-identical determinism); only the pinned bytes update. Phase 27 closure runs the showcase twice, captures the bytes, encodes them as the new pin. Single canonical contract — no parallel "legacy v1.2 fact" surface.

### 3. Chapter Integration Strategy

- **D-301:** **Hybrid integration** — language features WEAVE into existing chapters by domain; music features BATCH into a new "v1.3 Music Capabilities" mega-chapter at end. Phase 16 D-01 ("weave by domain") applied selectively where the analogue exists; the music side gets its own consolidated chapter to avoid sprawling the existing 19 chapters past coherent length.
- **D-302:** Language-feature weaves:
  - **Prefix arithmetic** → update chapter 2 "Arithmetic and String Interpolation". Show `(add 10 25)`, `(sub)`, `(mul)`, `(div)`, `(idiv)`, `(concat)`. STD-01..03 already migrated all in-repo `.flow` files; only the chapter prose needs an explicit "no infix" rule + `(neg)` unary form. Existing print strings displaying "+" / "*" are fine (display-only).
  - **Symbols `#foo`** → new chapter immediately after chapter 1 "Variables and Basic Types" (e.g. chapter 1.5 "Symbols" or chapter 2 reordering). Demonstrate `(eq #foo #foo)` interning + `(eq #foo "foo") = false` strict separation from String.
  - **Tuples + `~>` unpack** → new chapter immediately after current chapter 4 "Collections and Loops". Cover `<<a, b, c>>` literal, `<<>>` empty + `<<x>>` singleton, `tup@N` indexing, `<<a, b>> = expr` destructuring assignment, `~>` parse-time multi-arg unpack, `(unpack)` runtime equivalent. Note: this chapter introduces `~>` BEFORE chapter 5 introduces Dict's `(each)` callback (which depends on `~>` semantics).
  - **`Dict<K, V>`** → new chapter immediately after the Tuples chapter. Demonstrate `(dict #kick 90 #snare 70)` flat + `(dictTuple <<#kick, 90>> ...)` tuple-pair, both constructor forms; the 14-op surface (`get`/`getOr`/`set`/`remove`/`has`/`keys`/`values`/`size`/`merge`/`each`/`map`/`filter`); insertion-order preservation; NaN-key special case (mention only — too fiddly for the main flow).
- **D-303:** Music-feature batch chapter ("v1.3 Music Capabilities") sub-sections:
  - **Tuplets + fractional durations** — `{3:2 ...}q` bracket + per-note `C4/12` + nested tuplets + tied-note interaction. The marquee v1.3 music feature — leads the chapter.
  - **Microtonal tuning + scale-lint pragmas** — `enable justIntonation;` / `pythagorean;` / `equalTemperament;` activation. `enable scaleLint;` only meaningful in flow-lsp — tutorial documents print-only ("flow-lsp surfaces diagnostics; flow-interpreter does not").
  - **Composer DX bundle (DX-10..15)** — arpeggio with rate+direction+pattern; chord inversions/voicings; NoteValue-rate delay; quantize; legato/portamento; varispeed-loadWav. ~6 sub-sub-sections; heaviest content area.
  - **Misc small wins** — `range(Int, Int)`/`range(Int, Int, Int)`, multi-letter enharmonics (E↔Fb / F↔E# / B↔Cb / C↔B#), negative slice `arr@-1` + `slice(_, -3, _)`, `enable hAsB;` reference (companion file demonstrates), `humanizeGaussian` seed-determinism.
- **D-304:** **New v1.3 graduation song** at the end — replace Phase 16's v1.2 graduation song. Single canonical graduation. Mirrors showcase "replace" decision (D-201). The v1.3 song integrates audible features per D-103 + tuplet flourish + microtonal pragma activation (file-scoped — tutorial.flow may activate ONE microtonal pragma if the graduation song benefits; alternative: graduation song lives in 12-TET and microtonal demo is left to the companion file per D-401). **Planner decides** whether tutorial.flow activates `enable justIntonation;` based on whether the graduation song benefits audibly — if not, no pragma in tutorial.flow; companion file (D-402) carries the demo.

### 4. Pragma Demonstration

- **D-401:** **Multi-file approach.** Tutorial.flow's pragma sub-section (inside the music-batch chapter, D-303 "Microtonal tuning + scale-lint pragmas") contains print-only explanation + paste-ready snippets + a pointer: "run `examples/pragmas/X.flow` to hear/see this in action." Each companion file is small (~30-40 lines), demonstrates ONE pragma, runs standalone with WAV/MID output. Cleanly separates concerns; each pragma gets a dedicated demo without cross-contamination.
- **D-402:** Companion files that ship under `examples/pragmas/`:
  - `examples/pragmas/h_alias.flow` — `enable hAsB;` activated. Demonstrates `| H4q B4q |` produces two identical notes (German B notation). Confirms `H` outside note streams remains a usable identifier (e.g. `Int H = 5;`). ~30 lines, runs to exit 0 with WAV output.
  - `examples/pragmas/microtonal_ji.flow` — `enable justIntonation;` activated. Demonstrates Cmaj triad in JI vs 12-TET frequency-ratio comparison via print of the active tuning frequencies (5:4 vs ~1.2599 for the major third); also renders short audible WAV. ~40 lines.
  - **Pythagorean / scale-lint companions NOT shipped in 27** — Pythagorean overlaps too much with JI for marginal value; scale-lint is flow-lsp-only and impactful demo requires flow-lsp screenshots, not a runnable .flow file. Both can be added in a later docs-only follow-up if requested.
- **D-403:** **Phase27ByteIdenticalPragmaTests** — new fact class under `flow-lang.Tests/Unit/Phase27/` pinning WAV/MID bytes for both companion files (4 facts: `cmp`-clean + non-empty for h_alias.flow + microtonal_ji.flow). Mirrors Phase18+25 byte-identical regression contract pattern. Companion files are part of the v1.3 deliverable; should not regress silently.
- **D-404:** **Companion file output co-locates with tutorial/showcase output** in `examples/output/` directory. Filenames disambiguate: `flow_tutorial.{wav,mid}`, `flow_showcase.{wav,mid}`, `h_alias.{wav,mid}`, `microtonal_ji.{wav,mid}`. Single `.gitignore` rule (already present from Phase 16 D-05) covers all artifacts. Composer scrolling `examples/output/` sees the full v1.3 surface output side-by-side.

### 5. Out of Phase 27 (deferred / acknowledged)

- **D-501:** Phase 27 does NOT implement `enable scaleLint;` diagnostic rendering in flow-interpreter. flow-lsp owns surface; tutorial.flow documents print-only.
- **D-502:** Phase 27 does NOT add Pythagorean or scale-lint companion files. JI is the canonical microtonal demo. If composer demand surfaces post-release, ship as v1.4 docs-only follow-up.
- **D-503:** Phase 27 does NOT touch `mHz` literal lexing (deferred per Phase 26.2 D-12), `frequencyToNote(Hertz)` helper (Phase 26.2 RESEARCH Open Q1 resolved as not-needed), or full Scala loader (deferred to v1.4 per v1.3 D-03).

### Claude's Discretion

- **Chapter ordering:** D-302 specifies the targets (after chapter 1 / after chapter 4 / etc.), but the planner may reshuffle minor numberings if it improves readability (e.g., if Symbols feel more natural between Variables and Arithmetic vs. immediately after Variables). Don't force the order against composer flow.
- **Graduation song key + tempo:** D-304 leaves the song's key + tempo to planner discretion within the polyrhythmic-minimal genre frame (D-202 sets showcase genre, but tutorial graduation song is independent).
- **Tutorial graduation pragma activation:** D-304 closure note — planner picks whether tutorial.flow activates one microtonal pragma based on song fit. Default: NO pragma in tutorial.flow (companion files carry pragma demos); if planner finds the graduation song benefits audibly from JI, can activate.
- **Companion file synth choices:** `h_alias.flow` and `microtonal_ji.flow` use whatever default synth makes the demonstration land cleanest (sine for pure-tone JI ratio comparison probably; piano for h_alias to make notes audibly distinct).
- **Section structure of the v1.3 graduation song:** intro/verse/chorus/bridge/outro vs. simpler intro/main/outro vs. through-composed — planner picks based on what showcases the audible features (D-103) most clearly without sprawl.
- **Tutorial chapter rewrites for prefix-arithmetic context:** if any existing chapter's example reads awkwardly with prefix forms (e.g. very long arithmetic chains), planner may shorten or restructure. STD-03 already migrated, so this should be rare.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & Requirements
- `.planning/ROADMAP.md` § "Phase 27: Tutorial + Showcase Refresh" — goal, success criteria 1-4 (note: criterion #1 will be expanded per D-101 to include Phase 26.2 features).
- `.planning/REQUIREMENTS.md` § Quality of Life — QOL-04 entry; D-101 rewrites at closure.
- `.planning/STATE.md` — confirms v1.3 milestone progress 11/12; Phase 27 closes the milestone.

### Phase 16 Precedent (the previous tutorial-refresh phase)
- `.planning/phases/16-tutorial-refresh/16-CONTEXT.md` — D-01..D-09 patterns (weave-by-domain, no-length-cap, showcase-also-refreshed-in-parallel, single-graduation-piece, examples/output/ directory). Most decisions in this CONTEXT inherit from Phase 16 unless explicitly overridden.
- `.planning/phases/16-tutorial-refresh/16-SUMMARY.md` — what shipped in Phase 16.
- `.planning/phases/16-tutorial-refresh/16-VERIFICATION.md` — verification template shape.
- `.planning/phases/16-tutorial-refresh/16-REVIEW-FIX.md` — surfaces issues caught in review (review the patterns to avoid in Phase 27).

### Phase 26.1 Closure Precedent (REQUIREMENTS.md author-at-closure pattern)
- `.planning/phases/26.1-symbols-tuples-dicts/26.1-06-SUMMARY.md` — closure pattern that rewrote DICT-01/02/03 entries against landed scope; D-101 follows.
- `.planning/phases/26.1-symbols-tuples-dicts/26.1-VERIFICATION.md` — VERIFICATION.md shape for closure phases.

### Phase 26.2 Closure Precedent (recent ERG-01..05 author-at-closure)
- `.planning/phases/26.2-music-type-ergonomics-fx-overloads-inserted/26.2-06-SUMMARY.md` — most-recent closure pattern; D-101 + D-104 + D-204 follow.
- `.planning/phases/26.2-music-type-ergonomics-fx-overloads-inserted/26.2-CONTEXT.md` — Phase 26.2 D-04..D-07 (gain/volume split), D-11..D-13 (Hertz), D-08..D-10 (Ms/Decibel FX overloads). The features the tutorial demonstrates.

### Source files (read for tutorial chapter content)
- `examples/tutorial.flow` — current 684-line tutorial (Phase 16 v1.2 state). Existing 19 chapters listed at lines 25, 44, 64, 90, 126, 140, 168, 196, 236, 264, 283, 322, 343, 368, 392, 435, 460, 497, 530, 555.
- `examples/showcase.flow` — current 44-line "v1.2 Ambient Piece" (Aminor pad, 72 BPM, padBase + pulse + melody). D-201 replaces.
- `flow-lang/audio.flow` — `internal proc` declarations for every audio builtin (gain, volume, delay, compress, sidechain, reverb, lowpass, highpass, bandpass, createXxxTone, etc.). Tutorial's chapter 9 examples must use these correctly per Phase 26.2 audio.flow forward decls.
- `flow-lang/composition.flow`, `flow-lang/notation.flow` — composition + notation conveniences used throughout tutorial.

### Test infrastructure (regression contract)
- `flow-lang.Tests/Unit/Phase18/ByteIdenticalShowcaseTests.cs` — D-204 updates byte-pin assertions to match new showcase output.
- `flow-lang.Tests/Unit/Phase25/ByteIdenticalShowcaseGaussianTests.cs` — D-204 updates byte-pin assertions to match new showcase Gaussian-humanize output.
- `flow-lang.Tests/Unit/Phase27/Phase27ByteIdenticalPragmaTests.cs` — NEW (D-403) — pins WAV/MID bytes for the 2 companion files.

### CLAUDE.md
- `CLAUDE.md` § "Language Features → Core" — already has gain=dB / volume=linear bullet (Phase 26.2 closure); D-104 appends "Music Types Quick Reference" table after this section.
- `CLAUDE.md` § "Language Features → Music-Specific" — note-stream syntax + chord literals + roman numerals + transforms etc. Tutorial chapter content should align with how this section describes the surface.

### Memory / Project (project-level decisions)
- `.planning/PROJECT.md` — pre-public, no legacy burden — D-201 (replace v1.2 showcase) + D-304 (replace Phase 16 graduation song) follow this lean.
- Memory `feedback_ergonomics_priority` — D-101 (update QOL-04 + tutorial.flow demos Phase 26.2) follows.
- Memory `feedback_charitable_interpretation` — D-102 (gain=dB / volume=linear gets own chapter explaining the footgun-fix) reflects silent-and-documented lean: function name documents the unit; chapter explains why.
- Memory `project_genre_agnostic` — D-202 (polyrhythmic minimal as showcase genre) doesn't privilege; tutorial graduation song uses different genre by planner discretion (D-304 closure note).
- Memory `project_pre_public_no_legacy_burden` — D-201 + D-204 + D-304 all rely on this.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Phase 16 chapter pattern** — `(print "")` before/after section + `(print "--- N. Section Name ---")` divider. Tutorial chapters follow this format consistently. New v1.3 chapters keep the same numbering scheme; insertions get `1.5 / 4.5` half-numbering or full renumbering at planner discretion (D-302 weaves vs. D-303 batch chapter).
- **Phase 16 graduation-song pattern** — final chapter renders one Song to WAV+MID via `writeWav` + `writeMidi` to `examples/output/`. D-304 follows: new v1.3 song uses the same artifact pattern; filenames stay `flow_tutorial.{wav,mid}`.
- **Phase 16 examples/output/ directory + .gitignore** — already in place from Phase 16 D-05; D-404 reuses for companion files.
- **`Note:` vs `//` comment style** — Phase 16 D-09 split. Tutorial uses `Note:` for chapter-header-level multi-line dividers (visually distinct, top of file), `//` for inline single-line comments. Phase 27 keeps the same style — only refresh comments where v1.3 requires explanation.
- **Showcase mood-piece pattern** — current showcase.flow shows the compact-demo pattern: `tempo X { timesig 4/4 { key Yminor { ... reverbTime { section atmosphere { ... } } ... } } }` with named sequences feeding a one-section `Song` rendered to WAV+MID. D-201 replaces the piece content; the structural pattern carries over.
- **euclidean rhythms with seed for byte-identical determinism** — Phase 16 + 25 precedent: `(euclidean steps hits note swing humanize seed)` 6-arg call with fixed seed. D-202 showcase uses this for the drum.
- **`humanizeGaussian(seq, amount, seed)` with fixed seed** — Phase 25 precedent for byte-identical Gaussian humanize. D-202 showcase uses this for the melody.

### Established Patterns
- **STD-01..03 prefix-arithmetic migration is complete** (Phase 26 closure) — all in-repo `.flow` files already use `(add)`/`(sub)`/`(mul)`/`(div)`/`(idiv)`/`(neg)`/`(concat)` builtins. D-302 chapter 2 update is prose-only (rule: no infix); existing print strings displaying "+" / "*" are display-only and stay.
- **gain=dB / volume=linear shipped via Phase 26.2** — `examples/tutorial.flow` may currently use `gain` in dB context (existing behavior preserved). D-102 + D-103 add `volume` usage; D-302 chapter 9 update keeps the existing `gain` usage in dB.
- **audio.flow `internal proc` forward decl pattern** — every C# builtin overload has a matching `internal proc` decl in audio.flow. Tutorial chapter examples use the user-facing names (e.g. `(volume buf 0.5)`); the forward decls already make this work.
- **byte-identical determinism contract** — Phase 18 + 25 ByteIdentical tests pin specific bytes; D-204 updates the pinned bytes when showcase content changes. Pattern: capture bytes via `dotnet run` × 2 + `cmp`, encode as test asserts.

### Integration Points
- **`flow-interpreter`'s `--watch` mode** — composers iterate on `.flow` files; tutorial chapters can mention `dotnet run --project flow-interpreter -- --watch examples/tutorial.flow` once.
- **Companion files under `examples/pragmas/`** — new directory (D-401). Each file is a standalone runnable `.flow` script. flow-interpreter resolves them via `dotnet run --project flow-interpreter examples/pragmas/X.flow`.
- **`examples/output/` is in .gitignore** (Phase 16 D-05). All artifacts (tutorial, showcase, companion files) write here. No commit pollution.

</code_context>

<specifics>
## Specific Ideas

- **Tuplet groove example for showcase D-202:** something like `Sequence drumTriplets = | {3:2 _ kick _ }q kick {3:2 _ kick snare}q kick |` — tuplets on beats 1 and 3 of a 4/4 bar against a straight kick on 1 and 3. Polyrhythmic feel without losing the downbeat.
- **Dict-driven drum dispatch for showcase:** `Dict<Symbol, Note> drums = (dict #kick C2 #snare D2 #hihat F#3); (each drumPattern (fn Symbol s, Beat at => (renderHit (get drums s) at)))` — exercises both `each` 2-arg lambda + `~>` unpack semantics + Symbol-keyed Dict + Note-typed Dict values.
- **Phase 26.2 audible-in-graduation snippet:** intro section uses `(volume introBuf 0.4)` for quiet, chorus uses `(volume chorusBuf 0.9)` for loud, both rendered through `(reverb sectionBuf 0.5 1.8s)` for the reverb tail, `(lowpass introBuf 1.2kHz)` filter sweep at the verse→chorus transition.
- **JI companion file structure:** `enable justIntonation; ... Cmaj triad ... print frequency of C4 + E4 + G4 ... compare to 12-TET print ... render short WAV with C4 E4 G4 chord ... exit 0` — under 40 lines.
- **h_alias companion file structure:** `enable hAsB; ... | H4q B4q C5q | rendered (showing H4 == B4 audibly) ... outside note stream: Int H = 5; (print H) ... exit 0` — under 30 lines.
- **Music Types Quick Reference table for CLAUDE.md (D-104):** rough sketch of the columns:
  | Literal | Type | IsCompatibleWith | Accepted at |
  | `-12dB` | `Decibel` | `Double`, `Float` | `gain`, `compress` threshold, `sidechain` threshold, anywhere `Double` |
  | `100ms` | `Millisecond` | `Double`, `Float` | `delay`, `compress` attack/release, `sidechain` attack/release, `CanConvertTo Second` |
  | `2.5s` | `Second` | `Double`, `Float` | `reverb` decay, `CanConvertTo Millisecond` |
  | `+50c` | `Cent` | `Double`, `Float` | `transpose` cent-precision |
  | `+2st` | `Semitone` | `Int` | `transpose` semitone-precision |
  | `1.5` (Beat-tagged) | `Beat` | `Double`, `Float` | beat-position arithmetic |
  | `440Hz` / `1.5kHz` | `Hertz` | `Double`, `Float` | `lowpass`/`highpass`/`bandpass`, `createSineTone`/etc. |
  | `#foo` | `Symbol` | strict (no Double/Float) | `Dict<Symbol, V>` keys, identity-equality usage |

</specifics>

<deferred>
## Deferred Ideas

- **Pythagorean microtonal companion file** — overlaps too much with JI for marginal value. Ship in v1.4 docs-only follow-up if composer demand surfaces.
- **Scale-lint companion file** — flow-lsp owns surface; runnable `.flow` file demonstrating it would need flow-lsp running, not just flow-interpreter. Defer to v1.4 docs (potentially screenshot-based) or skip entirely.
- **`mHz` (millihertz) literal demo** — no FX site needs sub-Hz frequencies until LFOs land. Defer.
- **`frequencyToNote(Hertz) → Note` helper demonstration** — Phase 26.2 RESEARCH Open Q1 resolved this as not-needed (no Hz-taking PitchConversion API exists); revisit only if added in a future phase.
- **Full Scala (`.scl`) loader companion file** — deferred to v1.4 per v1.3 D-03 (heavy: 18+ file blast radius).
- **Tutorial split into multiple files** — considered as Pragma demo Option (d); rejected for breaking the single-tutorial mental model. Companion files under `examples/pragmas/` are the splitting compromise (D-401).
- **Comment-style refresh** — Phase 16 D-09 split (`Note:` vs `//`) carries over verbatim; only refresh comments where v1.3 requires explanation. No global comment-style overhaul in 27.
- **Tutorial `--watch` mode demonstration as its own chapter** — mention once in passing; no dedicated chapter (composer DX, not language feature).

</deferred>

---

*Phase: 27-tutorial-showcase-refresh*
*Context gathered: 2026-05-10*
