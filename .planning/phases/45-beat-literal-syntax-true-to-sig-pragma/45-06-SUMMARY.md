---
phase: 45-beat-literal-syntax-true-to-sig-pragma
plan: 06
subsystem: cross-file boundary + composer tutorials + tracking-file sweep (phase closer)
tags: [phase-45, closer, cross-file, tutorials, audio-baselines, verification, wave-4]
requires: [45-04, 45-05]
provides:
  - ProcDeclaration.IsBeatTrueToSig per-proc pragma capture + Interpreter push/pop (Rule 1 fix — cross-file boundary)
  - tests/test_beat_cross_file.flow + tests/test_beat_cross_file_helper.flow (pragma-on entry + pragma-off helper)
  - 4 (str Beat) round-trip Facts (D-14 lock) + CrossFileSmokeFact
  - examples/beat/intro.flow (6/8 jig) + examples/beat/cut-time.flow (2/2 cut time)
  - flow-lang.Tests/baselines/Phase45/{intro,cut-time}.wav committed two-run cmp-clean baselines
  - 4 tutorial Facts (two-run cmp-clean x2 + baseline-match x2)
  - CLAUDE.md D-13 table row + Pragmas family bullet
  - REQUIREMENTS.md Phase 45 section (26 REQ-BEAT-NN) + traceability rows
  - ROADMAP.md Phase 45 closure + STATE.md v1.5 10/15 + 45-VERIFICATION.md closer deliverable
affects:
  - flow-lang/Ast/Statements/ProcDeclaration.cs (+IsBeatTrueToSig field + xmldoc)
  - flow-lang/Parsing/Parser.cs (capture IsBeatTrueToSig at parse time)
  - flow-lang/Interpreter/ExpressionEvaluator.cs (lambda lexical capture)
  - flow-lang/Interpreter/Interpreter.cs (per-proc push/pop)
  - flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs (+9 Facts + RunInterpreter/Sha256 helpers)
  - CLAUDE.md / .planning/{REQUIREMENTS,ROADMAP,STATE}.md / 45-VERIFICATION.md
tech-stack:
  added: []
  patterns:
    - "Per-proc pragma capture mirroring Phase 44 ProcDeclaration.IsStrict (parse-time capture + Interpreter push/pop in same try/finally as PushFrame/PopFrame)"
    - "Process.Start cross-file smoke + two-run SHA-256 cmp-clean (Phase 44 StrictFlowScriptSuiteTests precedent)"
    - "Committed WAV baselines for deterministic synthesis (Phase 28/37 precedent — no PRNG sites)"
key-files:
  created:
    - tests/test_beat_cross_file.flow
    - tests/test_beat_cross_file_helper.flow
    - examples/beat/intro.flow
    - examples/beat/cut-time.flow
    - flow-lang.Tests/baselines/Phase45/intro.wav
    - flow-lang.Tests/baselines/Phase45/cut-time.wav
    - .planning/phases/45-beat-literal-syntax-true-to-sig-pragma/45-VERIFICATION.md
  modified:
    - flow-lang/Ast/Statements/ProcDeclaration.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs
    - CLAUDE.md
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md
    - .planning/STATE.md
decisions:
  - "D-12 honored: two tutorials (examples/beat/intro.flow 6/8 jig + cut-time.flow 2/2)"
  - "D-13 honored: CLAUDE.md Music Types row REPLACED (1.5 Beat-tagged -> 0.5b Beat literal) + Pragmas family bullet"
  - "D-14 honored: (str Beat) plain double pinned by 4 round-trip Facts"
  - "D-15/D-16/D-17 honored as deferrals/documentary carry-forward (no Phase 45 implementation)"
  - "Rule 1 fix: ProcDeclaration.IsBeatTrueToSig per-proc capture — the cross-file boundary (REQ-BEAT-TEST-04) was broken because the RegisterContextDependent constructor read the caller's live bit, not the declaring file's"
metrics:
  duration_minutes: 55
  tasks_completed: 3
  files_created: 7
  files_modified: 9
  tests_added: 9
  tests_pass_phase45: 66
  tests_pass_phase44_strict: 275
  completed_date: "2026-05-29"
requirements:
  - REQ-BEAT-TEST-04
  - REQ-BEAT-TEST-05
  - REQ-BEAT-TEST-06
  - REQ-BEAT-TEST-07
  - REQ-BEAT-DOC-01
  - REQ-BEAT-DOC-02
  - REQ-BEAT-DOC-03
  - REQ-BEAT-DOC-04
---

# Phase 45 Plan 06: Cross-File Boundary + Tutorials + Phase Closer Summary

Wave 4 closer — finished Phase 45 with the cross-file pragma-boundary test pair, the two composer-facing tutorials in `examples/beat/`, committed two-run cmp-clean WAV baselines, the `(str)` round-trip lock Facts, the CLAUDE.md documentation update, the REQUIREMENTS.md / ROADMAP.md / STATE.md tracking-file sweep, and the `45-VERIFICATION.md` closure deliverable. All 26 REQ-BEAT-NN requirements close here; v1.5 progresses to 10/15 phases. **The cross-file boundary required a Rule 1 fix** — the must-have truth was broken at plan-spawn because the `(beat N)` `RegisterContextDependent` constructor read the caller's live pragma bit, not the declaring file's; closed by adding `ProcDeclaration.IsBeatTrueToSig` per-proc capture mirroring Phase 44's `IsStrict`.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Cross-file pair + (str) round-trip Facts + cross-file smoke + ProcDeclaration.IsBeatTrueToSig Rule 1 fix | `4a0a041` |
| 2 | Composer tutorials + audio baselines + two-run cmp-clean Facts | `308c37a` |
| 3 | CLAUDE.md + REQUIREMENTS.md + ROADMAP.md + STATE.md + 45-VERIFICATION.md sweep | `3769717` |

## Cross-File Boundary Behavior Verified

`tests/test_beat_cross_file.flow` (pragma-ON entry) `use`-s `tests/test_beat_cross_file_helper.flow` (pragma-OFF helper declaring `proc bumpBeat (Beat: b) ... return (beat 1)`). Inside the entry's `timesig 6/8 { }`:

- `Beat localLit = 1b` → `(str localLit)` = **0.5** (local literal sees the 6/8 multiplier 4/8 = 0.5).
- `Beat fromHelper = (bumpBeat (beat 0))` → `(str fromHelper)` = **1** (the helper proc's `(beat 1)` reads its DECLARING file's pragma bit, which is OFF — raw quarters, no multiplier).

This is the Pitfall 3 / D-04 file-scope contract: the declaring file's pragma bit governs construction, consumers never re-interpret.

## Tutorial File Scope

| Tutorial | Chapters | Render |
|----------|----------|--------|
| `examples/beat/intro.flow` | (1) 4/4 identity `1b = 1` / `0.5b = 0.5`; (2) 6/8 pragma `1b = 0.5` (eighth) / `2b = 1` / `0.5b = 0.25`; (3) rendered 6/8 jig melody + bass | `renderSong ... "flute"` → `/tmp/beat_intro.{wav,mid}` |
| `examples/beat/cut-time.flow` | 2/2 cut time `1b = 2` (half) / `0.5b = 1` (quarter); rendered march | `renderSong ... "brass"` → `/tmp/beat_cut_time.{wav,mid}` |

## Baseline WAV SHA-256

| Baseline | SHA-256 |
|----------|---------|
| `flow-lang.Tests/baselines/Phase45/intro.wav` | `d401374c2f84bd142a8af85ace98e1ad2e580316118a25b1d78d9f6455fb3394` |
| `flow-lang.Tests/baselines/Phase45/cut-time.wav` | `d3e0e832c5c17d1943986036bcbe0093a2e5c30c7c2ca9306e063886d054362d` |

Both verified byte-identical across two consecutive runs before committing (no PRNG sites → exact determinism).

## CLAUDE.md Edit Positions

- **Music Types Quick Reference table** (~line 189): `| `1.5` (Beat-tagged) | ... |` REPLACED with the D-13 verbatim `| `0.5b` (Beat literal) | `Beat` | `Double`, `Float` | beat-position arithmetic; `enable beat-true-to-sig;` opt-in retunes... |` row.
- **Music-Specific section** (~line 201): new **Pragmas** bullet listing all 8 pragmas (`hAsB` / `justIntonation` / `pythagorean` / `equalTemperament` / `scaleLint` / `matchExhaustive` / `strict` / `beat-true-to-sig`) with the multiplier semantics, tutorial cross-refs, and the per-proc declaring-file capture note.

## Tracking-File Final Shapes

- **REQUIREMENTS.md**: new `### Beat Literal Syntax & True-to-Sig Pragma (Phase 45)` section with section-intro paragraph + all 26 REQ-BEAT-NN entries marked `[x]` + 26 new v1.5 Traceability rows; coverage count 87 → 113.
- **ROADMAP.md**: top-level Phase 45 checklist entry added; Phase 45 detail block `**Plans:** 6/6 — SHIPPED 2026-05-29` + 45-06 `[x]`; progress table row `45. ... | 6/6 | Complete | 2026-05-29`.
- **STATE.md**: frontmatter `completed_phases` 8 → 9, `completed_plans` 70 → 71, `percent` 53 → 60, `stopped_at: Phase 45 complete`; v1.5 Phase Map gains a Phase 45 row + progress 9 → 10/15; Phase 45 highlights block prepended.
- **45-VERIFICATION.md** (NEW, 192 lines): §1 26-REQ closure table, §2 66-Fact breakdown, §3 .flow smoke inventory, §4 two-run cmp-clean SHA evidence, §5 known caveats (incl. Rule 1 cross-file fix), §6 metrics.

## Total Phase 45 Fact Count

**66 GREEN** = 21 (`BeatLiteralParserTests` — 7 lex + 5 AST + 9 supporting) + 4 (`PragmaScannerHyphenTests`) + 27→ (`BeatTrueToSigPragmaTests` decls: 6 registry/context + 4 cross-file restore + 13 multiplier matrix + 4 str + 1 cross-file smoke + 4 tutorial; Theory rows expand) + 9 (`BeatConstructorTests` Theory-expanded). Phase 44 strict 275/275 GREEN (zero regression from shared per-proc push/pop). 128 happy-path `tests/test_*.flow` scripts pass; 4 expected non-zero error scripts unchanged.

## All 26 REQ-BEAT-NN Closure Mapping

LEX-01..04 + PRAGMA-HYPHEN-01 → 45-01 (`d6d0731`/`fffd82f`); AST-01..03 → 45-02 (`121eb30`); PRAGMA-01..04 → 45-03 (`7372ce3`/`84df903`); AST-04 + TEST-01..03 + TEST-05 → 45-04 (`8ec7145`/`d62c64d`); CONSTRUCTOR-01..02 + TEST-06(partial) → 45-05 (`5fe8566`); TEST-04 + TEST-06(str) + TEST-07 + DOC-01..04 → 45-06 (`4a0a041`/`308c37a`/`3769717`).

## Phase 45 Closing Chain of Commits (all 6 plans)

- 45-01: `d6d0731`, `fffd82f`
- 45-02: `121eb30`
- 45-03: `7372ce3`, `84df903`
- 45-04: `8ec7145`, `d62c64d`
- 45-05: `5fe8566`
- 45-06: `4a0a041` (Task 1), `308c37a` (Task 2), `3769717` (Task 3 sweep) + final metadata commit (this SUMMARY)

## Deviations from Plan

### Auto-Fixed Issues

**1. [Rule 1 — Bug] Cross-file pragma boundary was broken — `(beat N)` in a pragma-off helper proc read the caller's live bit, not the declaring file's.**
- **Found during:** Task 1, running `tests/test_beat_cross_file.flow` for the first time — `(bumpBeat (beat 0))` returned 0.5, not the required 1.0.
- **Issue:** Plan 45-03 wired `BeatTrueToSig` through `ModuleLoader`'s file-LOAD save-set-restore but NOT through proc-INVOCATION. `(beat N)` is a `RegisterContextDependent` builtin reading the LIVE `ctx.BeatTrueToSig`; a helper proc declared in a pragma-off file but invoked from a pragma-on file read the caller's (wrong) bit. This broke the plan's load-bearing must-have truth (REQ-BEAT-TEST-04).
- **Fix:** Added `ProcDeclaration.IsBeatTrueToSig` (parse-time capture from the declaring file's `PragmaSet`, mirroring Phase 44 `ProcDeclaration.IsStrict`) + per-proc push/pop in `Interpreter.ExecuteUserFunctionWithCaptures` (same try/finally as the strict-bit push/pop) + lexical capture on synthetic lambda ProcDeclarations in `ExpressionEvaluator.EvaluateLambda`. This is the EXACT pattern Phase 44 established — not architectural.
- **Files modified:** `flow-lang/Ast/Statements/ProcDeclaration.cs`, `flow-lang/Parsing/Parser.cs`, `flow-lang/Interpreter/ExpressionEvaluator.cs`, `flow-lang/Interpreter/Interpreter.cs` (commit `4a0a041`).
- **Verification:** `tests/test_beat_cross_file.flow` now prints `helper (beat 1) ... = 1`; Phase 44 strict 275/275 GREEN confirms zero strict regression from the shared proc-path change; 128 happy-path scripts pass.

### Plan-body adjustments (no code defect)

**2. Inline `Note:` comments don't work after code on the same line** — `Note:` is a statement form, not an inline comment. Tutorials use full-line `Note:` comments only (matching `examples/scala/intro.flow`). The Song/render block sits INSIDE the `tempo`/`timesig` blocks (matching `examples/showcase.flow`) rather than after them.

**3. Rest-with-duration `_h` is not valid note-stream syntax** — rests are bare `_` (default duration). The cut-time march's final bars use pitched notes instead of `_h` rests.

**4. `tests/` + `examples/beat/` are gitignored** — force-added per the long-standing convention (127+ tracked `tests/test_*.flow` files). Baseline WAVs under `flow-lang.Tests/baselines/` are NOT gitignored (Phase 37 `.wav` baselines precedent).

**5. STATE.md "8/12 phases" plan wording was stale** — actual STATE uses a 15-phase total; updated to 10/15 (matching the v1.5 Phase Map text) rather than the plan's 8/12.

### Out-of-Scope Discoveries

- **Pre-existing stray `</content></invoke>` artifact in REQUIREMENTS.md** (lines ~228 between the REQ-STRICT `---` and the Phase 42 block) — malformed markdown from a prior edit, NOT touched by this plan (the Phase 45 section was inserted cleanly above it). Logged here; not in scope to fix.
- **2 transient whole-suite xUnit failures** unrelated to Phase 45: `FlowLang.Tests.Tools.VerifyRichnessGain.Print_Current_RichnessRatios` (synth-richness diagnostic tool) + `FlowLang.Tests.Integration.Phase48.WasmDeterminismTests.SameSource_TwoRuns_IdenticalRunResultJson` (Phase 48 WIP — Phase 48 is "In Progress"). Neither touches Beat/pragma/proc-execution. My change only adds a default-false `BeatTrueToSig` push/pop, which cannot alter WASM JSON determinism or synth richness ratios. The `--no-build` whole-suite run also surfaced incidental Rug.Osc build errors from FlowTarget conditioning drift (the default Desktop `dotnet build flow-lang` is clean) — a Phase 47/48-track artifact, not a Phase 45 regression.

## Stub Tracking

None. Every deliverable is fully wired: cross-file boundary is exercised end-to-end at both the `.flow` and xUnit layers; tutorials render real WAV+MIDI; baselines are committed; all 26 REQ-BEAT-NN are evidenced in 45-VERIFICATION.md.

## Threat Flags

None. Documentation + composer tutorials + tracking-file sweep + a test-infrastructure-adjacent interpreter fix. T-45-14 (stale CLAUDE.md row) mitigated via the D-13 REPLACE (old row gone, `grep 0.5b` pins the new one). T-45-15 (baseline regression) accepted per the Phase 28 precedent. No new network/auth/file-access surface.

## Self-Check: PASSED

- File existence:
  - `tests/test_beat_cross_file.flow` / `_helper.flow` — FOUND (force-added)
  - `examples/beat/intro.flow` / `cut-time.flow` — FOUND (force-added)
  - `flow-lang.Tests/baselines/Phase45/intro.wav` / `cut-time.wav` — FOUND (committed)
  - `.planning/phases/45-beat-literal-syntax-true-to-sig-pragma/45-VERIFICATION.md` — FOUND (192 lines)
  - `flow-lang/Ast/Statements/ProcDeclaration.cs` — FOUND (IsBeatTrueToSig present)
- Commit existence: `4a0a041` / `308c37a` / `3769717` — all FOUND in git log.
- Deliverables: cross-file boundary verified (helper = 1, local = 0.5); 2 tutorials render; 2 baselines two-run cmp-clean; 66 Phase 45 Facts GREEN; Phase 44 strict 275/275; CLAUDE.md + REQUIREMENTS.md (26 [x]) + ROADMAP.md (6/6) + STATE.md (10/15) + 45-VERIFICATION.md all reconciled.
