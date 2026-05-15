---
phase: 32-full-scala-scl-tuning-loader
plan: 01
subsystem: testing
tags: [scala, tuning, fixtures, scl, kbm, microtonal, attribution]

# Dependency graph
requires:
  - phase: none
    provides: Wave 0 has no prior-phase dependencies; supplies the fixture battery every downstream Phase 32 plan consumes
provides:
  - 5 canonical Scala-archive .scl fixtures byte-verbatim from Huygens-Fokker via narenratan mirror
  - 3 hand-authored malformed fixtures isolating one SPEC-7 error class each at column 1
  - LICENSE.md per D-17 softened-community-use wording with rename audit trail
  - .gitattributes rule pinning *.scl/*.kbm as -text so CRLF line endings survive commit (parser-correctness contract)
  - .gitignore unignore rule for flow-lang.Tests/fixtures/scala/**/*.md (mirrors Phase 29 Samples precedent)
affects: [32-02-parser, 32-04-acceptance, 32-06-error-paths, all downstream Phase 32 plans]

# Tech tracking
tech-stack:
  added: []  # Wave 0 is pure data + attribution — no code, no libs
  patterns:
    - "In-repo verbatim archive vendoring with first-line `! ORIGINAL ARCHIVE FILENAME: ...` audit comment (D-16)"
    - "Negative-case fixtures isolating one error class at column 1 for unambiguous {file}:{line}:{col} assertion (SPEC-7)"
    - "Phase 29 Samples-style LICENSE.md + .gitignore unignore precedent extended to fixtures/"

key-files:
  created:
    - "flow-lang.Tests/fixtures/scala/partch_43.scl"
    - "flow-lang.Tests/fixtures/scala/slendro.scl"
    - "flow-lang.Tests/fixtures/scala/carlos_alpha.scl"
    - "flow-lang.Tests/fixtures/scala/pythagorean_12.scl"
    - "flow-lang.Tests/fixtures/scala/just_5limit.scl"
    - "flow-lang.Tests/fixtures/scala/malformed_step_count.scl"
    - "flow-lang.Tests/fixtures/scala/malformed_cents.scl"
    - "flow-lang.Tests/fixtures/scala/malformed_kbm.kbm"
    - "flow-lang.Tests/fixtures/scala/LICENSE.md"
    - "flow-lang.Tests/fixtures/scala/.gitattributes"
  modified:
    - ".gitignore"

key-decisions:
  - "Pin *.scl / *.kbm as -text in .gitattributes so byte-verbatim CRLF line endings from the Huygens-Fokker archive survive commit — parser will be tested against the EXACT bytes real-world archive files ship with (deviation Rule 2)"
  - "Prepend `! ORIGINAL ARCHIVE FILENAME: pyth_12.scl` / `... ji_12.scl` as the first line of the two renamed files (D-16); original archive's `! pyth_12.scl` / `! ji_12.scl` comment is preserved as line 2 — content otherwise byte-verbatim"
  - "Extend .gitignore with `!flow-lang.Tests/fixtures/scala/**/*.md` unignore so LICENSE.md tracks despite the global *.md ignore — mirrors Phase 29 Samples precedent at .gitignore lines 70-73 (deviation Rule 3)"
  - "Place every malformed error token at column 1 (no leading whitespace) so SPEC-7 diagnostic column number is unambiguous and testable"

patterns-established:
  - "Pattern: in-repo Scala fixture renaming preserves archive provenance via FIRST `! ORIGINAL ARCHIVE FILENAME:` comment AND LICENSE.md table — dual audit trail visible without reading both files"
  - "Pattern: malformed fixtures isolate one error class with the offender at column 1, enabling exact line:column assertions in parser error-path tests"

requirements-completed: [SPEC-3, SPEC-6, SPEC-7]

# Metrics
duration: ~10min
completed: 2026-05-14
---

# Phase 32 Plan 01: Wave 0 Fixture Battery + LICENSE Attribution Summary

**5 canonical Scala-archive fixtures byte-verbatim from Huygens-Fokker + 3 hand-authored malformed fixtures + LICENSE.md attribution, committed in two atomic tasks. Wave 0 unblocks parser/builtin/AST/acceptance plans.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-05-14T (executor start)
- **Completed:** 2026-05-14T (executor finish)
- **Tasks:** 2 / 2
- **Files created:** 10 (9 fixtures+LICENSE.md + 1 .gitattributes)
- **Files modified:** 1 (.gitignore)

## Accomplishments

- **5 byte-verbatim canonical Scala fixtures** fetched from the narenratan GitHub mirror raw URLs and committed under `flow-lang.Tests/fixtures/scala/`. Content verified against RESEARCH §"Verified contents" before commit:
  - `partch_43.scl` (Harry Partch 43-tone pure scale; 43 ratio-only steps ending `2/1`)
  - `slendro.scl` (Observed Javanese Slendro; 5 steps; mixed cents + final `2/1`)
  - `carlos_alpha.scl` (Wendy Carlos' Alpha; **non-octave**; 18 cents-only steps ending `1404.00000`)
  - `pythagorean_12.scl` (renamed from `pyth_12.scl` per D-16; 12-tone Pythagorean)
  - `just_5limit.scl` (renamed from `ji_12.scl` per D-16; Robert Rich's "Basic JI with 7-limit tritone" — step 6 = `7/5`)
- **3 hand-authored malformed fixtures** each isolating one SPEC-7 error class at column 1 for unambiguous `{file}:{line}:{col}` assertion:
  - `malformed_step_count.scl` → line 4 col 1: `-5` (expected positive int)
  - `malformed_cents.scl` → line 7 col 1: `foo` (expected cents or ratio)
  - `malformed_kbm.kbm` → line 7 col 1: `-50.0` (expected positive Hz)
- **LICENSE.md** per D-17 softened-community-use wording — attributes Manuel Op de Coul + Huygens-Fokker Foundation; tabulates each in-repo fixture against its archive original + raw URL; flags the 3 hand-authored fixtures as Flow-licensed.
- **Two infrastructure additions** (Rules 2 + 3 deviations, documented below) make Wave 0 ship correctly: `.gitattributes` preserves CRLF line endings on commit; `.gitignore` unignore rule lets LICENSE.md track.

## Task Commits

Each task was committed atomically:

1. **Task 1: Fetch and commit the 5 canonical Scala archive fixtures** — `9b0dbde` (feat)
2. **Task 2: Hand-author 3 malformed fixtures + write LICENSE.md** — `08a0260` (feat)

_The orchestrator will add the metadata commit (SUMMARY.md) post-merge._

## Files Created/Modified

### Created (canonical fixtures — byte-verbatim from Huygens-Fokker archive)
- `flow-lang.Tests/fixtures/scala/partch_43.scl` — 417 B; 43-step pure-ratio scale; final step `2/1`
- `flow-lang.Tests/fixtures/scala/slendro.scl` — 143 B; 5-step mixed cents + ratio period
- `flow-lang.Tests/fixtures/scala/carlos_alpha.scl` — 314 B; 18-step cents-only, **non-octave period 1404.00000¢**
- `flow-lang.Tests/fixtures/scala/pythagorean_12.scl` — 248 B; renamed from `pyth_12.scl`; first line carries audit comment
- `flow-lang.Tests/fixtures/scala/just_5limit.scl` — 192 B; renamed from `ji_12.scl`; first line carries audit comment

### Created (hand-authored malformed fixtures for SPEC-7)
- `flow-lang.Tests/fixtures/scala/malformed_step_count.scl` — 121 B; negative integer at line 4 col 1
- `flow-lang.Tests/fixtures/scala/malformed_cents.scl` — 124 B; non-numeric token at line 7 col 1
- `flow-lang.Tests/fixtures/scala/malformed_kbm.kbm` — 93 B; negative reference frequency at line 7 col 1

### Created (attribution + infrastructure)
- `flow-lang.Tests/fixtures/scala/LICENSE.md` — 3.76 KB; D-17 softened-community-use wording + rename audit trail
- `flow-lang.Tests/fixtures/scala/.gitattributes` — 359 B; `*.scl -text` / `*.kbm -text` (CRLF preservation)

### Modified
- `.gitignore` — added `!flow-lang.Tests/fixtures/scala/**/*.md` unignore rule so LICENSE.md tracks despite the global `*.md` ignore

**Total fixture-directory size:** 5,415 bytes (≪ SPEC 100 KB budget).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 — Missing Critical Functionality] Pin *.scl / *.kbm as `-text` in `.gitattributes`**
- **Found during:** Task 1 (`git add` warning surfaced CRLF→LF normalization on every fetched fixture)
- **Issue:** Real-world Scala archive `.scl` files ship with CRLF line endings (verified: `partch_43.scl` carries 48 CR bytes, etc.). Without a `.gitattributes` rule, git's autocrlf normalization would silently rewrite committed blobs to LF — breaking the "byte-verbatim from upstream archive" contract established in SPEC + RESEARCH §"Verified contents", and risking parser tests that pass under local LF normalization but fail against real-world CRLF inputs.
- **Fix:** Added `flow-lang.Tests/fixtures/scala/.gitattributes` with `*.scl -text` + `*.kbm -text`. Confirmed CR-count survives the round-trip: staged `partch_43.scl` blob carries 48 CRs (matches working tree).
- **Files modified:** `flow-lang.Tests/fixtures/scala/.gitattributes` (new)
- **Commit:** `9b0dbde`

**2. [Rule 3 — Blocking Issue] Add `flow-lang.Tests/fixtures/scala/**/*.md` to `.gitignore` unignore allowlist**
- **Found during:** Task 2 (`git add flow-lang.Tests/fixtures/scala/LICENSE.md` was silently ignored; `git check-ignore -v` confirmed `*.md` global rule at `.gitignore:11` was the cause)
- **Issue:** The repo-wide `*.md` ignore (with explicit unignore allowlists for `.planning/`, `CLAUDE.md`, `docs/`, `README.md`, `flow-lang/Samples/`, `vscode-extension/README.md`) silently blocks `LICENSE.md` in the new fixture directory. Without this fix, Wave 0's success criterion "LICENSE.md exists" is unsatisfiable.
- **Fix:** Extended `.gitignore` with a new unignore block (lines 83–88) that mirrors the Phase 29 Samples precedent (lines 70–73): `!flow-lang.Tests/fixtures/scala/`, `!flow-lang.Tests/fixtures/scala/**`, `!flow-lang.Tests/fixtures/scala/**/*.md`. Comment cross-references the precedent.
- **Files modified:** `.gitignore`
- **Commit:** `08a0260`

### Plan-Spec Adherence

- Plan success criterion says "exactly 9 files: 5 canonical .scl + 3 malformed + 1 LICENSE.md." Final commit ships **10 tracked files** — the 9 specified plus `.gitattributes`. `.gitattributes` is meta-config, not content, and is required to satisfy the byte-verbatim contract for the 9 content files. No deviation from the spirit of the criterion.

## Authentication Gates Encountered

None. Wave 0 is pure data fetch + file creation; no auth required.

## Acceptance Verification

All Task 1 `<acceptance_criteria>` items pass:
- `partch_43.scl` exists; `grep 'Harry Partch'` matches; tail = ` 2/1`
- `slendro.scl` exists; description matches; step count `5`; final value `2/1`
- `carlos_alpha.scl` exists; description matches; final value `1404.00000`; **no `2/1` line**
- `pythagorean_12.scl` first line = `! ORIGINAL ARCHIVE FILENAME: pyth_12.scl`; description matches; final value `2/1`
- `just_5limit.scl` first line = `! ORIGINAL ARCHIVE FILENAME: ji_12.scl`; description matches; step 6 = ` 7/5`
- Combined size: 1314 bytes (≪ 102400 budget)

All Task 2 `<acceptance_criteria>` items pass:
- `malformed_step_count.scl` contains `-5` at column 1 (line 4)
- `malformed_cents.scl` contains `foo` at column 1 (line 7)
- `malformed_kbm.kbm` contains `-50.0` at column 1 (line 7)
- `LICENSE.md` contains all 5 required strings (case-sensitive): `Huygens-Fokker`, `Manuel Op de Coul`, `long-standing community understanding`, `pyth_12.scl`, `ji_12.scl`
- Combined size of malformed + LICENSE.md: 4101 bytes (≪ 20480 budget)

## Threat Model Adherence

Three mitigations declared in `<threat_model>`; all in place:

- **T-32-FIX-01 (Tampering, content drift):** Mitigated structurally — downstream parser acceptance tests (Plans 32-02 / 32-04 / 32-06) assert numeric values (step counts, period cents, specific ratios like `1404.00000`, `7/5`, `2/1`); byte hashes are not relied upon. Tampering surfaces as test failures, not silent drift. Wave 0 supplies content; subsequent waves wire the assertions.
- **T-32-FIX-02 (Information Disclosure, misattribution):** Mitigated — `LICENSE.md` contains the required strings `Huygens-Fokker`, `Manuel Op de Coul`, source raw URLs in the rename mapping table. Verified by automated grep.
- **T-32-FIX-03 (Repudiation, rename audit):** Mitigated — original archive filenames `pyth_12.scl` and `ji_12.scl` appear BOTH in `LICENSE.md` table AND as the first `!` comment line inside each renamed file (`! ORIGINAL ARCHIVE FILENAME: pyth_12.scl` / `! ORIGINAL ARCHIVE FILENAME: ji_12.scl`). Future readers can verify provenance without consulting `LICENSE.md`.

No new threat surface introduced beyond the registered three. `.gitattributes` + `.gitignore` additions are infrastructure files inside the fixture directory — not new auth surfaces, network endpoints, or schema changes at trust boundaries.

## Known Stubs

None. Wave 0 ships data + attribution only — no implementation code, no placeholder functions, no empty data sources flowing to UI.

## Self-Check: PASSED

All 11 claimed artifacts exist on disk:
- `flow-lang.Tests/fixtures/scala/partch_43.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/slendro.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/carlos_alpha.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/pythagorean_12.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/just_5limit.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/malformed_step_count.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/malformed_cents.scl` — FOUND
- `flow-lang.Tests/fixtures/scala/malformed_kbm.kbm` — FOUND
- `flow-lang.Tests/fixtures/scala/LICENSE.md` — FOUND
- `flow-lang.Tests/fixtures/scala/.gitattributes` — FOUND
- `.planning/phases/32-full-scala-scl-tuning-loader/32-01-SUMMARY.md` — FOUND (this file)

Both task commits exist in git log:
- `9b0dbde` — FOUND (Task 1)
- `08a0260` — FOUND (Task 2)
