# Phase 39: Notation Citizenship - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 39 makes Flow a first-class citizen of the music-notation ecosystem. Composer surface:

1. **MusicXML export** — `(writeMusicXML song "piece.musicxml")` emits MusicXML 3.1 partwise. MuseScore is the reference consumer (D-v1.5-08). Multi-track `Song` → multi-part `<score-partwise>`. Articulation decision table locked by D-v1.5-08 (Accent → `<accent/>`, Marcato → `<strong-accent/>`, Staccato → `<staccato/>`, Tenuto → `<tenuto/>`, Sforzando → `<dynamics><sfz/></dynamics>`, Legato → slur spans NOT per-note). Microtonal pitches (from Phase 32 Scala tunings) emit as decimal `<alter>` with cent precision. Phase 28 voice blocks → `<voice>N</voice>` within parts.
2. **MusicXML round-trip CI gate** — `mscore --convert-to mxl` validates structural preservation (note count + durations + pitches + articulations). One-way Flow → XML; XML import is an explicit anti-feature (deferred to v1.6 per FEATURES.md lock).
3. **LilyPond export** — `(writeLilyPond song "piece.ly")` emits LilyPond text. Multi-voice notation via `\new Voice` inside `<< { ... } \\ { ... } >>`. Tuplet brackets via `\tuplet N/M {...}`; nested tuplets flattened by ratio-multiplication (engraver compatibility). Microtonal pitches as cent-offset comments alongside nearest 12-TET notation. Output compiles via `lilypond -dno-print-pages` without engraver errors.
4. **ABC notation import** — `(abc "X:1\nT:Reel\nM:4/4\nK:Dmaj\n...")` returns `Section` (single-tune) or `Array[Section]` (multi-tune `X:` blocks). ABC 2.1 subset + abc2midi extensions (modal keys `Edor`/`Dmix`/`Aphr`/etc., `Q:` tempo). Unknown ornaments/headers dropped with `[abc]` stderr advisory (charitable interpretation).
5. **MML notation import** — `(mml "T120 L4 O4 cdefga>c")` returns `Sequence`. PC-98-era common core (notes, accidentals `+`/`#`/`-`, octave `O<n>`/`>`/`<`, length `L<n>`, tempo `T<n>`, loops `[...]<n>`). Dialect-specific FM operator routing / drum maps ignored with `[mml]` stderr advisory.

**In scope:** XML-01, XML-02, LILY-01, ABC-01, ABC-02, MML-01 from REQUIREMENTS.md (6 REQs).

**Out of scope:** MusicXML import (anti-feature lock, v1.6), LilyPond import, ABC export, MML export, MEI export, MIDI Polyphonic Expression (MPE), GuitarPro / PowerTab formats, custom notation DSLs, score-rendering / printing (LilyPond's job, not Flow's), notation-driven playback differences from existing render path (notation is a one-way emit, not a render mode).

</domain>

<decisions>
## Implementation Decisions

### Stdlib Module Layout

- **D-39-01:** Single `@notation` stdlib module — `use "@notation"` activates `writeMusicXML`, `writeLilyPond`, `abc`, `mml` builtins. Mirrors Phase 33's single `@sfz` surface (one composer-facing module per phase domain). Alternative considered: 4 separate modules (`@musicxml`/`@lilypond`/`@abc`/`@mml`) — rejected because the 4 surfaces are commutatively the "notation interchange" concern, and 4 imports for one mental model is friction. Implementation files split per format under `flow-lang/StandardLibrary/Notation/` (parallel to `Audio/Sfz/`, `Audio/Tuning/`).
- **D-39-02:** Sub-phase order per roadmap — MusicXML → LilyPond → ABC → MML. Round-trip CI gate (XML-02) ships in the same plan as MusicXML export (XML-01), not as a separate plan. If late scope cuts surface, MML defers to v1.6 first (per roadmap fallback hedge); ABC defers second.

### Vendoring (no new NuGets per research/STACK.md)

- **D-39-03:** **MusicXML scaffolding** — vendor `sightreader/musicxml-schemas` XSD-generated POCOs under `flow-lang/Vendor/MusicXmlSchemas/` per research. Treat as code-only dep (no NuGet). Used as types; serialize via `System.Xml.Serialization` (the POCOs already carry `[XmlElement]` attributes from the schema generator). License verification mandatory at plan-start; fallback to hand-rolled `StringBuilder` emit if license blocks.
- **D-39-04:** **ABC parser** — vendor `matthewcpp/ABCSharp` source under `flow-lang/Vendor/ABCSharp/`. Per research/STACK.md: only actively-updated C# ABC implementation (last commit 2024), MIT-advertised. License verification mandatory at plan-start; fallback to hand-rolled parser (~800 lines feasible — ABC grammar is small) if license blocks. Vendored source carries upstream `LICENSE` + a `VENDORED-FROM.md` pointer to the source commit hash for diff-tracking against future upstream changes.
- **D-39-05:** **LilyPond + MML** — fully hand-rolled per research/STACK.md. LilyPond emit is pure `StringBuilder` text composition; MML import is a hand-rolled tokenizer (PC-98 common core is ~10 commands). No vendored sources for either.

### MusicXML Decisions (XML-01, XML-02)

- **D-39-06:** **Microtonal emission** — always decimal `<alter>` (cents → semitone-fraction at `cents / 100.0` precision). MuseScore renders decimal alter values natively. No text-annotation fallback ladder — simplest path, matches "ergonomics first" and avoids per-tuning UX noise for Carlos Alpha / Bohlen-Pierce (which have ≥10¢ deviations everywhere). REQUIREMENTS.md says "as `<alter>` with cent precision when supported, else as text annotations" — we read "when supported" as "MuseScore 3.6+ which always supports decimal alter"; text annotations only emit if a downstream consumer review surfaces breakage.
- **D-39-07:** **Articulation slur grouping (Legato)** — runs of ≥2 consecutive Legato-articulated notes within the same `<voice>` become one slur span (`<slur type="start" number="N"/>` … `<slur type="stop" number="N"/>`). No cross-voice slurs (engraver convention). Single Legato notes get no slur (a slur of one note is meaningless). Slur number is per-voice incrementing, scoped to the part.
- **D-39-08:** **Round-trip gate fallback** — XML-02 CI gate runs `mscore --convert-to mxl` when `mscore` is in PATH. When absent, emit `[xml] mscore not found — round-trip gate skipped` stderr advisory and PASS (do not fail CI). Matches Flow's charitable interpretation default (D-v1.5-05). Pinning a Docker MuseScore in CI rejected: adds CI infrastructure burden Flow doesn't need at v1.5 traction level.
- **D-39-09:** **Multi-track to multi-part** — each `Sequence` in a `Song` becomes one `<part id="PN">` with `<score-part id="PN"><part-name>{seq.Name}</part-name></score-part>` in `<part-list>`. Reuse the instrument-routing logic from `MidiExport.cs` (sequence-name → GM program prefix-match) for `<part-name>` and `<score-instrument>` annotations. Voice blocks within a sequence become `<voice>N</voice>` children of `<note>` (MusicXML's per-note voice tagging).
- **D-39-10:** **Output file extension default** — `.musicxml` (MuseScore default; opens directly without dialog). Composer can override via the path arg. Compressed `.mxl` format NOT emitted by Flow (composer can re-compress via `mscore` if they need it; we ship the canonical readable form).

### LilyPond Decisions (LILY-01)

- **D-39-11:** **Nested tuplet flattening** — compute the effective ratio by multiplying outer × inner (e.g., `{3:2 {5:4 ...}}` → effective `15:8`, emit flat `\tuplet 15/8 {...}`). Mathematically correct; engraver-compatible (LilyPond's `\tuplet` doesn't nest cleanly without `\override TupletBracket.bracket-visibility` games). Alternative considered: emit nested `\tuplet` blocks and let `lilypond` raise — rejected because Flow's LilyPond emit is "produce something that compiles", not "let the engraver fail".
- **D-39-12:** **Microtonal as comment** — per REQUIREMENTS.md: emit nearest 12-TET pitch with `% +50c` comment alongside. LilyPond has native quarter-tone notation (`ces`/`is`/`isih`/etc.) but it's quarter-precision only — cent precision needs Scheme-based custom accidentals which raise complexity beyond the v1.5 target. Comment-form lets engravers manually convert if needed.
- **D-39-13:** **Multi-voice notation** — sequences within a song become `\new Staff` per sequence; voice blocks (Phase 28 `{voice ...}`) within a sequence become `\new Voice` siblings inside `<< { ... } \\ { ... } >>`. Reuse the same voice-name → voice-index mapping as MusicXML for consistency. Output file extension default `.ly`.
- **D-39-14:** **LilyPond `\version` header** — emit `\version "2.24.0"` (current LTS at time of writing). Composer can post-edit if their LilyPond install is older. Version mismatch is a LilyPond warning, not an error.

### ABC Decisions (ABC-01, ABC-02)

- **D-39-15:** **Dialect coverage** — ABC 2.1 core (notes, accidentals, durations `2`/`/2`, octave `'`/`,`, bar lines `|`/`||`/`|]`, key `K:`, meter `M:`, length `L:`, title `T:`, X-index `X:`, tune body) + abc2midi `Q:` tempo extension + modal keys (`Edor`, `Dmix`, `Aphr`, `Cmix`, `Glyd`, `Bphr`, `Floc`). Unknown ornaments (`~`/`T`/`H`/`S`/`O`/`M`/`P`/etc.) dropped with one-shot `[abc] dropped ornament '{token}' at line {N}` stderr advisory (dedup per `(token, line)` per process). Unknown headers (any `X:` letter not in the supported set) dropped with `[abc] ignored header '{letter}'` advisory.
- **D-39-16:** **Multi-tune files** — files containing multiple `X:N` blocks return `Array[Section]` (one Section per tune). Single-tune files return a single `Section` (NOT a 1-element array — avoids composer-facing array unwrap noise for the common case). Type-dispatch via `(abc str)` overload: single `X:` block → `Section`, multiple → `Array[Section]`.
- **D-39-17:** **Charitable interpretation throughout** — malformed ABC (mismatched bars, invalid pitches, unknown key signatures) does NOT throw. Tokenizer skips with `[abc]` advisory and continues; the resulting Section may be incomplete but is always usable. Strict mode is NOT a v1.5 deliverable (deferred to v1.6 if composer demand surfaces). Matches D-v1.5-05.

### MML Decisions (MML-01)

- **D-39-18:** **Dialect coverage** — PC-98 common core only: notes (`a`-`g`), accidentals (`+`/`#`/`-`), octave (`O<n>` absolute, `>`/`<` relative shift), length (`L<n>`), tempo (`T<n>`), loops (`[...]<n>`). Dialect-specific opcodes (FM operator routing, drum-bank selection, custom envelope shapes from MUCOM/PMD/MOL) dropped with one-shot `[mml] dropped opcode '{token}' at offset {N}` stderr advisory. Multi-dialect support deferred to v1.6.
- **D-39-19:** **Charitable interpretation** — same posture as ABC. Malformed MML never throws; tokenizer skips with `[mml]` advisory. Loop nesting depth capped at 16 (mirror Phase 36 T-36-17 DoS guard) — deeper nests collapse to one iteration with `[mml] loop nesting depth exceeded` advisory.

### Cross-Cutting Decisions

- **D-39-20:** **Reuse the MidiExport multi-track pipeline insights** — Phase 28's `MidiExport.cs` (652 lines) already solved sequence-name → GM-program routing, voice block per-note MIDI tick math, articulation → MIDI event mapping. MusicXML + LilyPond emit consume the same `BarData` / `MusicalNoteData` / `Sequence` structures; reuse the routing helpers (extract into `flow-lang/StandardLibrary/Notation/InstrumentRouting.cs` if more than one format needs them, per Phase 36 Plan 36-01 utility-extraction precedent).
- **D-39-21:** **Phase 35 pattern matching usage** — articulation emit per D-v1.5-08 is the natural site for `match`: `(match articulation | Articulation.Accent => "<accent/>" | Articulation.Marcato => "<strong-accent/>" | ...)`. Same in LilyPond emit. This is the Phase 35 dependency root contract per D-v1.5-10 — Phase 39 articulation emit is one of the named consumers.
- **D-39-22:** **Examples to ship** — `examples/notation/` directory with one chapter per format: `to_musicxml.flow` (export → MuseScore round-trip), `to_lilypond.flow` (export → `lilypond` PDF render), `from_abc.flow` (import a Reel from thesession.org corpus → render to WAV), `from_mml.flow` (import a PC-98 chiptune snippet → render to WAV). All four chapters pass two-run cmp-clean determinism. Each chapter doubles as a regression test under `tests/test_notation_*_example.flow`.

### Claude's Discretion (deferred to researcher / planner)

- Exact `Vendor/` directory naming convention (`Vendor/` vs `ThirdParty/` vs `External/`) — researcher picks; codebase has no prior precedent (Phase 33 SFZ was hand-rolled, Phase 32 Scala was hand-rolled).
- Whether MusicXML emit uses `System.Xml.Serialization` against the vendored POCOs or `XmlWriter` directly (the POCO-attribute path is cleaner; the `XmlWriter` path is faster and avoids reflection — planner picks based on a small benchmark of a 100-bar score).
- LilyPond `\midi { }` block emission default — keep (matches the research example) or strip (Flow already exports MIDI via Phase 28 `writeMidi`). Recommended: keep, since LilyPond users expect it.
- ABC `Q:` tempo numerator/denominator parsing edge cases (`Q:1/4=120` vs `Q:120` vs `Q:"Allegro" 1/4=120`) — researcher checks ABCSharp's coverage; hand-fill gaps if any.
- MML loop semantics edge case — `[abc[de]2f]3` (nested loops): does the inner `[de]2` expand inside each outer iteration, or expand once? PC-98 dialect lore says inner-each-time; researcher confirms against a reference implementation.
- Whether to ship a tiny `flow notation convert` CLI subcommand (analog to Phase 30's `flow midi convert`) — leans no for v1.5 (composer can compose `flow run` + `(writeMusicXML)`); revisit in Phase 41 if `flow doc` work suggests batch conversion is wanted.
- Exact plan breakdown — researcher / plan-checker decide how to slice 4-6 plans. Suggested shape: Plan 39-01 Wave 0 + MusicXML export + round-trip gate, 39-02 LilyPond export, 39-03 ABC import + vendor ABCSharp, 39-04 MML import, 39-05 Closer (examples + VERIFICATION + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep). MusicXML + LilyPond commutative; ABC blocks on ABCSharp license verification.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### v1.5 milestone-level
- `.planning/PROJECT.md` — Core value, v1.5 milestone goal, key constraints
- `.planning/REQUIREMENTS.md` lines 9-20 — Locked decisions D-v1.5-01 through D-v1.5-11 (esp. D-v1.5-05 charitable interpretation default, D-v1.5-08 MusicXML articulation decision table, D-v1.5-10 Phase 35 dependency)
- `.planning/REQUIREMENTS.md` "Notation Export" + "Notation Import" sections — XML-01, XML-02, LILY-01, ABC-01, ABC-02, MML-01 requirement wording (treat as floor; D-39-* decisions in THIS file refine and extend)
- `.planning/ROADMAP.md` Phase 39 section — Goal + success criteria + sub-order hint (MusicXML → LilyPond → ABC → MML)

### Phase 35 dependency-root (must understand surface before consuming)
- `.planning/phases/35-language-foundation/35-VERIFICATION.md` — What Phase 35 actually shipped (LANG-01..04, TEST-01..02, HK-01..04 verified)
- `.planning/phases/35-language-foundation/35-05-SUMMARY.md` + `35-06-SUMMARY.md` — Pattern AST family (`Ast/Patterns/`), MatchExpression, music-aware extractors. Phase 39 D-39-21 articulation emit consumes this.
- `.planning/phases/35-language-foundation/35-03-SUMMARY.md` — Rust-style DiagnosticRenderer (used if ABC/MML import errors need source-quoted span pointers; charitable advisory path takes precedence per D-39-17/D-39-19)

### v1.5 research (composer's source-of-truth picks)
- `.planning/research/FEATURES.md` Phase 39 section — Phase 39 framing (notation ecosystem citizenship), one-way emit constraint, MML as deferred fallback hedge
- `.planning/research/STACK.md` Phase 39 row + dedicated sections — vendoring recommendation for ABCSharp + musicxml-schemas, hand-roll LilyPond + MML, anti-recommendation for `MusicXml.NET` (parser-only, wrong direction)
- `.planning/research/SUMMARY.md` — Phase 39 dependency-tree position (downstream of Phase 35; orthogonal to Phase 37 and Phase 38)
- `.planning/research/PITFALLS.md` — Any notation-format-specific pitfalls if present (researcher to confirm)

### Existing code (researcher must scout)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` (652 lines) — Multi-track export pattern (sequence-name → GM-program routing, articulation → MIDI event mapping, conductor track). D-39-20 extracts shared routing helpers if needed.
- `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (623 lines) — Structured text parser pattern (header / `<region>` / opcode parsing with charitable advisory on unknown opcodes). ABC and MML parsers follow this style.
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` (302 lines) — Smaller text parser pattern (single-format, no `<region>` blocks). Closest size analog for MML (~10 commands).
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` (127 lines) — Builtin registration pattern (single-arg + multi-arg overload + reference-identity value type for `Tuning`). MusicXML / LilyPond / ABC / MML builtins follow this style.
- `flow-lang/Runtime/MusicalContext.cs` — Key, tempo, time signature, voice pool, tuning state. ABC `K:`/`M:`/`Q:`/`L:` headers populate this; MML `T<n>`/`O<n>` populate it. MusicXML / LilyPond emit READ this for `<attributes>` / `\key`/`\time`/`\tempo` emit.
- `flow-lang/Ast/Statements/SectionDeclaration.cs` — Section AST. ABC multi-tune files (`X:1`, `X:2`, ...) produce one Section per tune per D-39-16.
- `flow-lang/Runtime/Value.cs` — Value factory methods. ABC import constructs `Sequence` / `Section` / `Array[Section]` per Phase 26.1 type system.
- `flow-lang/flow-lang.csproj` — Existing `<PackageReference>` (only DryWetMidi + Pidgin). Phase 39 adds ZERO new packages per D-39-03/04/05 (vendored sources only).

### Articulation decision table (verbatim from D-v1.5-08, for emit-site reference)
- Accent → `<accent/>`
- Marcato → `<strong-accent/>`
- Staccato → `<staccato/>`
- Tenuto → `<tenuto/>`
- Sforzando → `<dynamics><sfz/></dynamics>`
- Legato → slur spans NOT per-note (D-39-07 grouping policy)

### Examples to ship (D-39-22)
- `examples/notation/to_musicxml.flow` (new) — Export a 4-bar piece → MuseScore round-trip
- `examples/notation/to_lilypond.flow` (new) — Export a 4-bar piece → `lilypond` PDF render
- `examples/notation/from_abc.flow` (new) — Import a Reel from thesession.org → render to WAV
- `examples/notation/from_mml.flow` (new) — Import a PC-98 chiptune snippet → render to WAV

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **MidiExport.cs** (`flow-lang/StandardLibrary/Audio/MidiExport.cs`, 652 lines) — Sequence-name → GM program prefix-match routing (piano* → 0, brass*/horn* → 56, sax* → 65, etc.), conductor track for tempo/timesig changes, articulation → MIDI event mapping, multi-track structure. MusicXML + LilyPond emit reuse the routing logic; D-39-20 extracts to a shared helper if both surfaces touch it.
- **SfzParser.cs** + **ScalaParser.cs** + **ScalaKbmParser.cs** (`flow-lang/StandardLibrary/Audio/{Sfz,Tuning}/`) — Three text-format parsers ranging 184-623 lines. Established pattern: tokenizer + recursive-descent or line-by-line state machine + `ParseException` carrying line number + charitable-advisory fallback for unknown opcodes. ABC + MML parsers follow this style.
- **Vendored sample bundle precedent** (`flow-lang/Samples/CREDITS.md` + per-instrument `LICENSE.md`) — Phase 29 vendored CC-BY 4.0 audio assets with attribution discipline (CREDITS.md + per-asset LICENSE.md + automated `LicenseAuditTests`). D-39-03/04 vendor source code with the same discipline: `flow-lang/Vendor/{ABCSharp,MusicXmlSchemas}/LICENSE` + `VENDORED-FROM.md` (source commit hash) + ideally a `VendoredSourceLicenseTests` audit gate (researcher decides).
- **PrngRegistry** (`flow-lang/Runtime/PrngRegistry.cs` from Phase 36) — Not directly consumed by Phase 39 (notation emit is deterministic by construction; ABC/MML import has no PRNG), but the keyed-by-SourceLocation pattern is the precedent for any Phase 39 helper that wants per-call-site state.
- **Pattern AST family** (`flow-lang/Ast/Patterns/` from Phase 35) — D-39-21 articulation emit uses `(match articulation | Articulation.Accent => ... | _ => ...)`. Music-aware extractors (chord literal, articulation symbol) match directly.
- **MusicalContext** (`flow-lang/Runtime/MusicalContext.cs`) — Tempo, key, time signature, voice pool, tuning. MusicXML `<attributes>` + LilyPond `\key`/`\time`/`\tempo` READ from this; ABC `K:`/`M:`/`Q:` and MML `T<n>` POPULATE it (push a frame for the parsed Section, pop on return).

### Established Patterns
- **Charitable interpretation** (D-v1.5-05) — Phase 39 import surfaces (ABC, MML) follow this. Malformed input never throws; tokenizer skips with `[abc]` / `[mml]` stderr advisory and continues. The resulting `Section` / `Sequence` may be incomplete but is always usable. Strict mode deferred to v1.6.
- **One-shot stderr advisories** — `RenderingDiagnostics.WarnOnce` keyed on a sentinel. Phase 39 advisories use sentinels like `f"abc-ornament:{token}:{line}"` / `f"mml-opcode:{token}:{offset}"` so identical failure points dedup per process.
- **Stdlib module activation via `use "@name"`** — Phase 33 `@sfz` precedent. Phase 39's `@notation` follows the same shape: thin `flow-lang/notation.flow` file (forward declarations + doc comments) that resolves to C# builtins registered conditionally on the `use` statement.
  - **Caveat:** existing `flow-lang/notation.flow` is the MUSICAL notation module (note durations, rests, bar/sequence building) and is auto-loaded via `std.flow`. Researcher decides: rename to `@notation-io` / `@score` / `@notation-export` to avoid collision, or expand existing `@notation` with the new builtins. Recommended naming: `@notation-io` (the IO concern is distinct from the in-language notation primitives).
- **Two-run cmp-clean determinism** — Phase 18/25/27/28/29/33/36 inheritance. Phase 39 export surfaces emit deterministically by construction (no PRNG, sorted-key XML attributes via `XmlWriterSettings`). The 4 example chapters under `examples/notation/` pass two-run cmp-clean via the test framework (TEST-01).
- **Defaulted-parameter AST extension** — Phase 35 + Phase 36 precedent. Phase 39 introduces NO new AST nodes (notation is a stdlib concern, not a language concern); if any are needed, defaulted params keep the migration single-commit-friendly per D-v1.5-01.

### Integration Points
- **SongRenderer / SequenceRenderer / BarRenderer** — Already produce `BarData` / `MusicalNoteData` / `Sequence` structures that MusicXML + LilyPond emit consume directly. No render-path changes — emit reads the post-resolution model.
- **FlowEngine.ExecuteScriptAndGetResult** — Composer-facing API; `(writeMusicXML song "piece.musicxml")` and `(abc "...")` route through the standard builtin dispatch. ABC import returns a Value whose Type is `Section` or `Array[Section]` per D-39-16.
- **flow CLI `flow test`** — Phase 39 ships tests under `tests/test_notation_*.flow` (export round-trip + import smoke tests + 4 `*_example.flow` regression tests). All composer-facing tests gate Phase 39 verification per the test framework (TEST-01 / TEST-02 from Phase 35).
- **CI gate `mscore --convert-to mxl`** — XML-02 round-trip gate. New `.github/workflows/notation-roundtrip.yml` or extension to existing CI; D-39-08 skip-when-absent fallback keeps it non-blocking for local dev.
- **`@notation-io` module init** — FlowEngine init registers the 4 builtins (`writeMusicXML`, `writeLilyPond`, `abc`, `mml`) conditionally on `use "@notation-io"`. Vendored ABCSharp loads lazily on first `(abc ...)` call to keep `flow-lang.dll` cold-load time unchanged for composers who don't use notation IO.

</code_context>

<specifics>
## Specific Ideas

- **MuseScore as reference consumer** (D-v1.5-08 locked at milestone start, not re-discussed here): the MusicXML emit must open correctly in MuseScore 4.x without any "Errors found" dialog. Composer can route through Sibelius / Dorico / Finale post-hoc; Flow doesn't optimize for them.
- **Vendored source bundle discipline** (Phase 29 precedent): per-vendored-source `LICENSE` + `VENDORED-FROM.md` (source URL + commit hash) + ideally an automated `VendoredSourceLicenseTests` audit. Prevents drift between upstream license terms and Flow's distribution posture.
- **Sub-phase order from roadmap**: MusicXML first (highest-value surface, drives D-39-08 CI gate work), LilyPond second (commutative with MusicXML — composer's eye for D-39-11 nested-tuplet flattening tested in LilyPond first since LilyPond's `\tuplet` is stricter than MusicXML's `<tuplet>`), ABC third (gated on license verification at plan-start), MML fourth (deferrable to v1.6 if late scope cuts surface).
- **`examples/notation/` directory** (new): one chapter per format, all four pass two-run cmp-clean. Mirrors Phase 36's `examples/generative/` chapter pattern.

</specifics>

<deferred>
## Deferred Ideas

- **MusicXML import** — explicit anti-feature lock per FEATURES.md. Defer to v1.6 if composer demand surfaces (one-way export covers ~90% of composer use cases at v1.5 traction level).
- **LilyPond import** — out of scope for v1.5. LilyPond input is engraver-DSL-shaped, not music-data-shaped; useful for round-trip but not for composers writing in Flow.
- **ABC export** — out of scope for v1.5. ABC is a one-way import format for Flow (composer writes Flow, occasionally pulls in an ABC tune from thesession.org); exporting Flow as ABC has no clear use case.
- **MML export** — out of scope. PC-98 chiptune target audience is essentially non-existent at v1.5 traction level.
- **MEI export / GuitarPro / PowerTab** — niche formats with small consumer pools; defer until composer demand surfaces.
- **Custom notation DSLs** (composer writes their own notation grammar via Flow patterns) — interesting but belongs in a future "extensibility" phase, not in notation citizenship.
- **`flow notation convert` CLI subcommand** — composable from `flow run` + `(writeMusicXML)` + script. Revisit in Phase 41 if `flow doc` work suggests batch conversion is wanted.
- **MML multi-dialect support** (MUCOM, PMD, MOL) — PC-98 common core covers the historically-important corpus; multi-dialect deferred to v1.6 if composer demand surfaces.
- **ABC strict mode** — charitable interpretation is the v1.5 default per D-v1.5-05 / D-39-17. Strict mode (error-on-unknown) is a `enable abcStrict;` pragma candidate for v1.6.
- **MusicXML compressed `.mxl` output** — composer can re-compress via `mscore` post-hoc; Flow ships canonical readable XML only.

</deferred>

---

*Phase: 39-notation-citizenship*
*Context gathered: 2026-05-23*
