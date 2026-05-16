---
phase: 33
plan: 01
subsystem: sfz-test-infrastructure
tags: [phase-33, wave-0, fixture, audit, repo-size-gate]
requires:
  - 33-SPEC.md (SPEC-2 GM-orchestral 19-symbol set; SPEC-7 < 100 KB fixture cap)
  - 33-CONTEXT.md (D-09 19-symbol list; D-20 smoke fixture shape)
  - 33-RESEARCH.md (A1 VSCO path audit; A7 + Q3 control header; Example 4 smoke.sfz body)
  - 33-PATTERNS.md (smoke-fixture analog table row; Phase 29 RepoSizeTests pattern)
provides:
  - test-fixtures/sfz-smoke (2-region SFZ + 2 sine-burst WAVs + LICENSE; ~19 KB total)
  - regenerator helper Phase33FixtureGenerator (deterministic; satisfies two-run cmp-clean contract)
  - CI gate Phase33.RepoSizeTests (asserts < 100 KB)
  - Plan 33-04 input — VSCO-CONTROL-DECISION.md FOUND result mandates 14-opcode whitelist + <control> parsing
  - Plan 33-05 input — VSCO-PATH-AUDIT.md 15 verified + 4 TBD GM-symbol paths for the sfz.flow dict
  - Plan 33-06 input — smoke.sfz region 1 with loop_continuous + loop_start/end exercises crossfade test
  - Plan 33-04 input — smoke.sfz exercises the no-<control> codepath alongside VSCO patches' <control> codepath
affects:
  - .gitignore (added Phase 33 fixture un-ignore block; mirrors Phase 29/32 precedents)
tech-stack:
  added:
    - none — all code is hand-rolled C# under existing test project
  patterns:
    - Phase 29 RepoSizeTests const-named cap shape
    - Phase 32 ScalaParserFacts FindRepoRoot walk-up
    - Phase 18/25/27 two-run cmp-clean byte-identical determinism
key-files:
  created:
    - .planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md
    - .planning/phases/33-sfz-orchestral-sampler/33-VSCO-CONTROL-DECISION.md
    - flow-lang.Tests/Tools/Phase33FixtureGenerator.cs
    - flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz
    - flow-lang.Tests/fixtures/sfz-smoke/C4_sine.wav
    - flow-lang.Tests/fixtures/sfz-smoke/G5_sine.wav
    - flow-lang.Tests/fixtures/sfz-smoke/LICENSE.md
    - flow-lang.Tests/Integration/Phase33/RepoSizeTests.cs
  modified:
    - .gitignore (un-ignore block for sfz-smoke fixtures)
decisions:
  - "VSCO-CE Q3 resolved FOUND: 15/15 probed VSCO patches use <control> default_path= as the first non-comment header — Plan 33-04 MUST extend the opcode whitelist to 14 entries (adds default_path) and parse <control> as a fourth header type"
  - "VSCO-CE A1 resolved: all .sfz files live at the SFZ branch root (NOT under nested Strings/Violin/), the .wav samples live under nested instrument-category dirs, and <control> default_path= bridges the two — Plan 33-05's dict stores top-level filenames only"
  - "4 of 19 GM symbols (#choir, #guitar, #harpsichord, #celeste) have NO VSCO-CE 1.1.0 patch — they ship as TBD with a clear error pointing the composer at the absolute-path overload"
  - "Sustain articulation is canonical for the dict; multi-articulation patches (Stac/Spic/Pizz/Trem/KS) reachable only via the absolute-path loadSfz overload"
  - "Solo > ensemble preference for #violin; ensemble-canonical for #viola + #cello (no solo VSCO patch exists for those)"
  - "Path normalisation (Windows backslash → OS separator) is Plan 33-04 parser concern, NOT Plan 33-05 dict concern"
metrics:
  duration: ~10 min
  completed: 2026-05-16
  tasks: 3
  commits: 3
  files-touched: 9
  fixture-bytes: 19461 (19% of SPEC-7 cap)
  audit-rows-verified: 15
  audit-rows-tbd: 4
---

# Phase 33 Plan 01: Wave 0 — SFZ Test Infrastructure Summary

Wave 0 ships the < 100 KB synthetic SFZ smoke fixture (SPEC-7), the deterministic regenerator helper proving two-run cmp-clean byte-identical output (Phase 18/25/27 contract), the 100 KB repo-size CI gate, and the VSCO-CE 1.1.0 path audit + `<control>` decision that unblock Plans 33-04 (parser whitelist sizing) and 33-05 (sfz.flow dict population).

## Tasks Completed

| # | Name | Commit | Files |
|---|------|--------|-------|
| 1 | VSCO-CE 1.1.0 path audit + control decision | `13cbe1c` | 33-VSCO-PATH-AUDIT.md, 33-VSCO-CONTROL-DECISION.md |
| 2 | Synthetic smoke fixture + regenerator helper | `9b13681` | Phase33FixtureGenerator.cs, smoke.sfz, C4_sine.wav, G5_sine.wav, LICENSE.md, .gitignore |
| 3 | RepoSizeTests gate | `49dbc34` | Integration/Phase33/RepoSizeTests.cs |

## Fixture Provenance

```
SHA-256 (committed bytes — pinned in this summary so future regenerator drift surfaces)
  smoke.sfz   = 67862eb70bae4a9d5549e3405ce8fa441003b1f87b40d6162d487b6a43739f14   (541 bytes)
  C4_sine.wav = e3b2cfb6f85378bf80aff9a23070368249d69a776d163267959008ea276f6d4b   (8864 bytes)
  G5_sine.wav = de7f7c0088de58426e10271a0aa5b01d2454c2b823a61e3bfe20df5e0d249c55   (8864 bytes)
  LICENSE.md  = (regenerable; no provenance pin needed)                             (1192 bytes)
                                                                            -------
  fixture dir total                                                           19461 bytes (~19 KB)
```

19461 bytes ÷ 102400 byte cap = 19 % budget consumed. Plans 33-04 / 33-05 / 33-06 have headroom to add small auxiliary fixtures (e.g. Plan 33-04's malformed-SFZ negative-test corpus) without inflating past the cap.

## Audit Outcome

| Symbol | VSCO-CE Path | Confidence |
|--------|--------------|------------|
| `#violin` | `SViolinVib.sfz` | verified |
| `#viola` | `ViolaEnsSusVib.sfz` | verified (ensemble-canonical) |
| `#cello` | `CelloEnsSusVib.sfz` | verified (ensemble-canonical) |
| `#contrabass` | `ContrabassSusVB.sfz` | verified |
| `#flute` | `FluteSusVib.sfz` | verified |
| `#oboe` | `OboeSusVib.sfz` | verified |
| `#clarinet` | `ClarinetSus.sfz` | verified |
| `#bassoon` | `BassoonSus.sfz` | verified |
| `#trumpet` | `TrumpetSus.sfz` | verified |
| `#horn` | `FHornSus.sfz` | verified |
| `#trombone` | `TromboneSus.sfz` | verified |
| `#tuba` | `TubaSus.sfz` | verified |
| `#piano` | `UprightPiano.sfz` | verified |
| `#harp` | `Harp.sfz` | verified |
| `#timpani` | `Timpani.sfz` | verified |
| `#choir` | (not in VSCO-CE) | TBD |
| `#guitar` | (not in VSCO-CE) | TBD |
| `#harpsichord` | (not in VSCO-CE) | TBD |
| `#celeste` | (not in VSCO-CE) | TBD |

**15 verified / 4 TBD.** TBD rows ship in Plan 33-05 with an empty-path entry that produces a clear `UnknownInstrumentSymbolError` pointing the composer at the absolute-path overload — preferred to fabricating a path the composer's install can't satisfy.

## Control Decision

**FOUND.** 15/15 probed VSCO-CE patches across all six instrument categories declare `<control> default_path=...` as the first non-comment header. Without `<control>` parsing every VSCO `sample=` resolution would fail with `FileNotFoundError`, invalidating SPEC-2's "with sfz_root configured, `(loadSfz #violin)` returns a non-null Sfz value" acceptance criterion.

**Mandate for Plan 33-04:**
1. Whitelist becomes **14 opcodes** (adds `default_path` to the 13).
2. **`<control>` parses as a fourth header type** alongside `<global>` / `<group>` / `<region>`.
3. **`default_path=` cascades into every region's `sample=` path resolution at parse time.**
4. **Backslash → OS separator normalisation** before the path join (Linux primary).
5. **Smoke fixture exercises the no-`<control>` codepath**; VSCO patches exercise the `<control>` codepath. Plan 33-04 ships unit tests for both.

SPEC-3's "13 listed opcodes" wording is conditionally relaxed to 14 to include `default_path`.

## Verification

- `dotnet test --filter "FullyQualifiedName~Phase33.RepoSizeTests"` exits 0 (16 ms; well under the 1-second done-criterion ceiling).
- `du -sh flow-lang.Tests/fixtures/sfz-smoke/` reports 36 KB filesystem-block-rounded; `du -sb` reports 19461 bytes raw.
- Re-running `Phase33FixtureGenerator_Smoke_GeneratesFixtures` produces SHA-256-identical `.wav` output across consecutive runs (Phase 18/25/27 two-run determinism contract preserved).
- `dotnet build flow-lang.Tests/flow-lang.Tests.csproj` reports zero new warnings, zero errors.
- Audit row count: 19 GM symbols (verified by `grep -c '^| `#'` in `33-VSCO-PATH-AUDIT.md`).
- Control decision file resolves Q3 with `FOUND` keyword present per the planned contract.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added `.gitignore` un-ignore block for new fixture directory**
- **Found during:** Task 2 commit
- **Issue:** The repo-wide `*.wav` and `*.md` ignore globs (`.gitignore` lines 11 + 15) prevented `git add` from staging `C4_sine.wav`, `G5_sine.wav`, and `LICENSE.md`. The plan specified the artifact paths but did not include the `.gitignore` un-ignore precedent that Phase 29 / Phase 32 fixtures established.
- **Fix:** Added a `!flow-lang.Tests/fixtures/sfz-smoke/**` block to `.gitignore` mirroring the Phase 29 / Phase 32 fixture precedents at lines 87-89 and 94-96 (per CLAUDE.md sample-bundle pattern).
- **Files modified:** `.gitignore` (added 6 lines after the Phase 32 Scala block).
- **Commit:** `9b13681` (folded into Task 2 commit since the gitignore patch is what made the fixture commits possible).

**2. [Rule 1 - Bug] xUnit1013 warning from public helper methods on a class with [Fact]**
- **Found during:** Task 2 first build
- **Issue:** xUnit's analyzers flag any public method on a class containing a `[Fact]` as a candidate test method. The original Phase33FixtureGenerator had `GenerateC4Sine` / `GenerateG5Sine` as public statics alongside the `[Fact]`.
- **Fix:** Split the `[Fact]` into a sibling class `Phase33FixtureGeneratorFacts` so the helper class itself has no test members. Cleaner design + sidesteps the rule entirely without per-method suppressions.
- **Files modified:** `flow-lang.Tests/Tools/Phase33FixtureGenerator.cs`
- **Commit:** `9b13681` (folded into Task 2 commit since the warning was a Task 2 artefact)

No other deviations. The plan's three tasks executed in declaration order with no architectural surprises and no checkpoints required.

## Threat Model Compliance

| Threat ID | Disposition | Mitigation Status |
|-----------|-------------|-------------------|
| T-33-PARSE-01 (DoS via fixture size) | accept | N/A — 2-region hand-authored fixture |
| T-33-FIXTURE-01 (binary tampering) | mitigate | Regenerator helper deterministic; SHA-256 pinned in this SUMMARY for drift detection |
| T-33-SIZE-01 (DoS via committed-fixture growth) | mitigate | RepoSizeTests fact in Task 3 fails CI past 100 KB |

All three threats addressed.

## Self-Check: PASSED

Files-on-disk verification:

```
FOUND: .planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md
FOUND: .planning/phases/33-sfz-orchestral-sampler/33-VSCO-CONTROL-DECISION.md
FOUND: flow-lang.Tests/Tools/Phase33FixtureGenerator.cs
FOUND: flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz
FOUND: flow-lang.Tests/fixtures/sfz-smoke/C4_sine.wav
FOUND: flow-lang.Tests/fixtures/sfz-smoke/G5_sine.wav
FOUND: flow-lang.Tests/fixtures/sfz-smoke/LICENSE.md
FOUND: flow-lang.Tests/Integration/Phase33/RepoSizeTests.cs
```

Commit verification:

```
FOUND: 13cbe1c   docs(33-01): VSCO-CE 1.1.0 path audit + control decision (Task 1)
FOUND: 9b13681   feat(33-01): synthetic SFZ smoke fixture + regenerator helper (Task 2)
FOUND: 49dbc34   test(33-01): RepoSizeTests gate for sfz-smoke fixture (Task 3)
```

All claimed artefacts exist; all claimed commits exist on the worktree branch.
