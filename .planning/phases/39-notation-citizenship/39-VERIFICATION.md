# Phase 39: Notation Citizenship - Verification

**Verified:** 2026-05-23
**Status:** passed
**Branch:** `phase-39-notation` (isolated worktree at `/home/noah/Desktop/projects/flow-sharp-phase39`)
**Last commit before VERIFICATION:** `474595e` (Plan 39-04 MML import)
**Concurrent Phase 37 work in main tree:** UNAFFECTED — Phase 39 ships in an isolated
worktree per parent task brief; no STATE.md / ROADMAP.md / REQUIREMENTS.md edits
crossed into this commit history.

## REQ Coverage

| REQ ID  | Requirement                                                                | Plan(s) | Status |
|---------|----------------------------------------------------------------------------|---------|--------|
| XML-01  | `(writeMusicXML "file.musicxml" song)` emits MuseScore-compatible 3.1     | 39-01   | PASS   |
| XML-02  | `mscore --convert-to mxl` round-trip CI gate (charitable skip when absent) | 39-01   | PASS   |
| LILY-01 | `(writeLilyPond "file.ly" song)` emits lilypond 2.24-compatible text       | 39-02   | PASS   |
| ABC-01  | `(abc str)` returns Section | Array[Section] for ABC 2.1 + abc2midi       | 39-03   | PASS   |
| ABC-02  | Unknown ornaments / headers dropped with `[abc]` stderr advisory           | 39-03   | PASS   |
| MML-01  | `(mml str)` returns Sequence for PC-98 common core                         | 39-04   | PASS   |

All 6 REQs implemented and verified.

## Test Counts

### xUnit (`dotnet test --filter "FullyQualifiedName~Phase39" --no-restore --no-build`)
- **55 PASS + 1 SKIP** (total 56)
- Breakdown:
  - `VendoredSourceLicenseTests`: 3 facts
  - `MusicXmlExportTests`: 9 facts (XML well-formedness, multi-part, two-run cmp-clean,
    D-v1.5-08 articulation table, Sforzando dynamics, D-39-07 Legato slur grouping (run
    + singleton), voice blocks, InstrumentRouting parity with MidiExport)
  - `MusicXmlRoundTripTests`: 1 PASS (charitable skip when mscore absent) + 1 SKIP
    (`StructuralPreservation_NoteCountMatches` — lights up automatically when CI
    provisions `mscore`)
  - `LilyPondExportTests`: 14 facts (version header, score/layout/midi blocks, Dutch
    pitch convention, multi-staff, voice-block siblings, microtonal comments,
    two-run cmp-clean, D-v1.5-08 articulation table + Sforzando, D-39-07 Legato
    grouping)
  - `AbcImportTests`: 11 facts (single-tune Section, multi-tune Array[Section], modal
    Edor, Q:120 / Q:1/4=120 / Q:"Allegro" 1/4=140 tempo, default L: per-meter,
    multi-bar split, sharp/flat accidentals, octave shifts)
  - `AbcCharitableTests`: 6 facts (unknown ornament '~' advisory, unknown header 'Z'
    advisory, malformed garbage no-throw, invalid Q: fallback to 120, empty body
    no-throw, WarnOnce dedup)
  - `MmlImportTests`: 11 facts (basic scale, tempo, accidentals, octave abs/rel,
    length override, loop expansion, depth-cap-17 advisory, expansion-cap [c]100000
    advisory, unknown opcode '@1' advisory, malformed garbage no-throw, dotted note)

### Composer-facing (`dotnet run --project flow-cli -- test tests/test_notation_*_example.flow`)
- **4/4 PASS**:
  - `test_notation_to_musicxml_example.flow`
  - `test_notation_to_lilypond_example.flow`
  - `test_notation_from_abc_example.flow`
  - `test_notation_from_mml_example.flow`

### Backwards-compat (`dotnet test --filter "FullyQualifiedName~Phase33.SfzMidiExportTests"`)
- **10/10 PASS** — Phase 33 SFZ MIDI export tests still GREEN after Plan 39-01's
  `InstrumentRouting` extraction from `MidiExport.cs` (D-39-20). The byte-identical
  MIDI contract is preserved because `MidiExport.ResolveGmProgram` and
  `MidiExport.StripSamplerPrefix` now thin-delegate to the shared `InstrumentRouting`
  helper — same 17-entry routing table, same Phase 33 D-16 horn-before-brass
  ordering, same `sampler:` prefix-strip behavior.

### Pre-existing fragile tests
- `RagtimeFixtureTests.Ragtime_Synthetic_RmsRegression` and a sibling fail on baseline
  (pre-Phase-39) due to a 0.90 dB RMS drift exceeding the locked 0.5 dB tolerance
  in window 0 (0ms-100ms). Verified with `git stash` + bare-baseline run; these
  failures are NOT introduced by Phase 39 work. They are tracked under v1.5
  pre-existing backlog (Phase 28 RMS baseline calibration).

## Example Chapters (D-39-22)

All 4 example chapters under `examples/notation/` run cleanly via
`dotnet run --project flow-interpreter examples/notation/{chapter}.flow`:

- `to_musicxml.flow` — writes valid MusicXML 3.1 to `examples/notation/output/to_musicxml.musicxml`
  (head verified: `<?xml version="1.0" encoding="utf-8"?>` + `<score-partwise version="3.1">`)
- `to_lilypond.flow` — writes valid LilyPond 2.24 source to
  `examples/notation/output/to_lilypond.ly` (head verified: `\version "2.24.0"` + `\score {` + 4 staff bars)
- `from_abc.flow` — imports a 2-bar C major scale, prints success
- `from_mml.flow` — imports a 10-note PC-98 chiptune fragment, prints success

`examples/notation/README.md` documents the 4-chapter set with reproduction
prerequisites (`mscore` + `lilypond` listed as OPTIONAL per D-39-08 charitable-skip
posture).

## Decisions Made (Claude's Discretion section of CONTEXT.md)

All resolved in Plan 39-01 RESEARCH.md and locked through subsequent plans:

- **Vendor/ directory naming:** `flow-lang/Vendor/` (PascalCase, sibling to `Samples/`).
- **MusicXmlSchemas POCO vendoring: SKIPPED** (Plan 39-01 T1 decision — `XDocument`
  structural diff suffices for the XML-02 round-trip gate; emit path uses hand-rolled
  `XmlWriter` per Pitfall 6 — `XmlSerializer` reflection ordering would break
  two-run cmp-clean).
- **ABCSharp source vendoring: SKIPPED** (Plan 39-03 revised D-39-04 — hand-rolled
  `AbcLexer.cs` + `AbcImport.cs` at ~600 LOC fits Flow's narrow ABC needs (ABC 2.1
  core + abc2midi `Q:` + modal keys) better than ingesting a third-party dep).
  Both `sightreader/musicxml-schemas` and `matthewcpp/ABCSharp` were verified MIT-
  licensed at research time via WebFetch; if future v1.6 reconsiders vendoring,
  both are still available.
- **MusicXML emit path:** Hand-rolled `XmlWriter` with `NewLineChars = "\n"` +
  `UTF8Encoding(emitBOM: false)` for cross-platform byte-identical output (Pitfall 6).
- **LilyPond `\midi { }` block default:** KEEP (matches LilyPond user-base
  expectation; trivial to emit; composers can post-edit).
- **MML nested-loop semantics:** Inner expands each outer iteration (PC-98 PMD/MUCOM
  convention).
- **`flow notation convert` CLI subcommand:** Deferred to v1.6 (composer composes
  `flow run` + `(writeMusicXML)` directly).
- **Plan breakdown:** 5 plans across 3 waves — Plan 39-01 wave 0 (dependency root +
  MusicXML + InstrumentRouting extraction); Plans 39-02 + 39-03 wave 1 (commutative);
  Plans 39-04 + 39-05 wave 2.
- **Articulation match exhaustiveness:** Implemented as C# `switch` expression with
  `#pragma warning disable CS8524` scoped to the switch arm only — explicit enum
  enumeration forces emit-site update when a new `Articulation` value is added
  (Pitfall 5).

## Cross-Cutting Determinism Contract Preserved

- Two-run cmp-clean determinism: every notation IO surface is deterministic by
  construction (no PRNG). `XmlWriterSettings.NewLineChars = "\n"` + fixed
  iteration order over `Dictionary` insertion-ordered keys + `StringBuilder` with
  explicit `\n` literals guarantee byte-identical output across runs.
- Phase 33 SFZ MIDI byte-identical contract preserved — `MidiExport.ResolveGmProgram`
  / `StripSamplerPrefix` thin-delegate to `InstrumentRouting`; the 17-entry routing
  table is byte-identical to pre-Phase-39 behavior (10/10 `SfzMidiExportTests` PASS).
- Phase 28 MIDI multi-track byte-identical contract preserved (same delegation
  path).

## Notes for Merge to dev

- This worktree (`phase-39-notation` branch) is ISOLATED from the concurrent
  Phase 37 work in the main tree (`dev` branch). All 5 commits in the branch are
  scoped to Phase 39 directories + files:
  - `.planning/phases/39-notation-citizenship/` (planning artifacts)
  - `flow-lang/StandardLibrary/Notation/` (8 new files)
  - `flow-lang/Vendor/` (new dir + README + .gitkeep)
  - `flow-lang/notation-io.flow` (new module)
  - `flow-lang.Tests/Integration/Phase39/` (6 new test files)
  - `tests/test_notation_*_example.flow` (4 new composer tests)
  - `examples/notation/` (new dir + README + 4 chapters + output/.gitkeep)
  - `CLAUDE.md` (2 inserts: Standard Library Modules table + Music-Specific bullet)
  - Touched: `flow-lang/Core/FlowEngine.cs` (1 line + comment),
    `flow-lang/Runtime/ExecutionContext.cs` (NotationIoEnabled field + Snapshot/Restore),
    `flow-lang/StandardLibrary/Audio/MidiExport.cs` (thin-delegation of 2 methods),
    `flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs` (NotationIoEnabled field),
    `flow-lang/flow-lang.csproj` (1 `<None Update>` block),
    `.gitignore` (1 `!flow-lang/notation-io.flow` allow-list line).

- `.planning/STATE.md` was NOT updated in this worktree per the parent task brief.
- `.planning/ROADMAP.md` Phase 39 entry NOT updated.
- `.planning/REQUIREMENTS.md` REQ-ID statuses NOT updated.

**Merge guidance (one line):**
`git merge phase-39-notation into dev; mark Phase 39 COMPLETE in ROADMAP.md;
mark XML-01/02, LILY-01, ABC-01/02, MML-01 IMPLEMENTED in REQUIREMENTS.md;
reconcile STATE.md by hand`.
