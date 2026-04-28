---
phase: 16
slug: tutorial-refresh
status: complete
completed: 2026-04-25
subsystem: docs + examples
tags: [phase-closure, qol-03, tutorial, showcase, v1.2-milestone-final]

requires:
  - phase: 14-composer-dx-part-1
    provides: "DX-05 slice + DX-06 enharmonic + DX-08 dynamics MIDI velocity (3 of 14 tutorial features)"
  - phase: 15-composer-dx-part-2
    provides: "DX-07 reverbTime + DX-09 euclidean swing/humanize (2 of 14 tutorial features); byte-identical contract for fixed-seed reproduction"
provides:
  - "examples/tutorial.flow refreshed — 14 v1.1+v1.2 features demonstrated end-to-end with audible WAV + MIDI export"
  - "examples/showcase.flow rewritten — v1.2 ambient mood piece (reverbTime + euclidean humanize + dynamics)"
  - "examples/output/ directory + .gitignore — discoverable output location alongside the example sources"
  - "ROADMAP criterion #4 moot-note documented — fourth criterion-moot/reframe in v1.2 (Phase 12 TEST-03, Phase 14 DX-06, Phase 15 #3, Phase 16 #4)"
affects: [v1.2-milestone-close (this is the final phase; /gsd-complete-milestone v1.2 next)]

tech-stack:
  added: []
  patterns:
    - "Documentation-only closure plan with worktree: false (orchestrator-owned files force-restored after worktree merge per Phase 15 Plan 07 precedent)"
    - "Criterion-moot audit-trail pattern (4th instance in v1.2): when CONTEXT resolves a ROADMAP criterion to moot, closure plan documents moot-note in VERIFICATION.md so the unchecked checkbox reads as preserved audit trail not as forgotten"
    - "examples/output/.gitignore mirroring tests/output/ pattern (Phase 15 Plan 01 precedent — per-directory gitignore with header comment + *.wav + *.mid)"
    - "Single-Song-multiple-format export pattern: writeWav consumes Buffer (rendered + post-effects); writeMidi consumes Song (notation only); both fire from the same Song variable to satisfy CONTEXT D-04 dual-export requirement"
    - "Tutorial-as-determinism-smoke pattern: tutorial uses fixed euclidean seed (42) so two consecutive runs produce byte-identical examples/output/* — free regression check inheriting from Phase 15 byte-identical contract"
    - "Documented WAV/MIDI asymmetry pattern (Plan 16-03): tempoRamp tail mixed into WAV via mix(finalMix, ritBuf) but MIDI export remains pure-notation; documented in tutorial Note: comment so the reader understands the divergence is intentional"

key-files:
  created:
    - examples/output/.gitignore
    - .planning/phases/16-tutorial-refresh/16-VERIFICATION.md
    - .planning/phases/16-tutorial-refresh/16-SUMMARY.md
    - .planning/phases/16-tutorial-refresh/16-01-SUMMARY.md
    - .planning/phases/16-tutorial-refresh/16-02-SUMMARY.md
    - .planning/phases/16-tutorial-refresh/16-03-SUMMARY.md
    - .planning/phases/16-tutorial-refresh/16-04-SUMMARY.md
    - .planning/phases/16-tutorial-refresh/16-05-SUMMARY.md
  modified:
    - examples/tutorial.flow (348 lines → 635 lines after expansion + graduation refactor)
    - examples/showcase.flow (84 lines → 44 lines, fully rewritten)
    - .planning/ROADMAP.md (Phase 16 row + Plan list + Progress table + criterion #4 moot-note + milestone header)
    - .planning/REQUIREMENTS.md (QOL-03 Shipped marker + Traceability row + footer date)
    - .planning/STATE.md (frontmatter + Current Position + Resume Instructions + Accumulated Context)

decisions:
  - "ROADMAP criterion #4 (C5 migration notes) marked moot per CONTEXT D-14 — fourth criterion-moot/reframe in v1.2 (Phase 12 TEST-03 + Phase 14 DX-06 + Phase 15 #3 precedents). Audit trail preserved in 16-VERIFICATION.md."
  - "Showcase scope addition (CONTEXT D-03) shipped as separate plan 16-04 to avoid bundling tutorial expansion + showcase rewrite into a single plan and consuming context budget."
  - "Tutorial graduation piece exports BOTH WAV and MIDI from the same Song value (CONTEXT D-04) — writeMidi consumes Song; writeWav consumes the post-effects Buffer; both fire at the end of the graduation chapter."
  - "Tempo ramp tail mixed into the WAV (Plan 16-03) but NOT reflected in MIDI export — documented in tutorial Note: comment so the reader understands the asymmetry (MIDI captures notation, WAV captures realized audio)."
  - "Fixed euclidean seed (42 in tutorial, 7 in showcase) used in chapter 18 + graduation groove + showcase pulse — inherits Phase 15 byte-identical contract; tutorial doubles as a determinism smoke test."
  - "Showcase tone preserved (CONTEXT §specifics 'wow listen to this'): no Note: chapter dividers, no per-feature commentary, three short atmospheric // annotations only."
  - "examples/output/ uses per-directory .gitignore (CONTEXT §Claude's Discretion 'either is fine') matching Phase 15 Plan 01 tests/output/ precedent rather than top-level .gitignore entry."
  - "Inline `mp` velocity marker chosen in showcase (Plan 16-04) over wrapping `dynamics mp { ... }` block — block form scopes the inner Sequence to the dynamics block, breaking section/reverbTime references downstream. Inline form satisfies the dynamics requirement without the scope barrier; documented as Rule 1 deviation in 16-04-SUMMARY."

requirements-completed: [QOL-03]

duration: ~57min execution (3min Plan 01 + 50min Plan 02 + 2min Plan 03 + 2min Plan 04) + ~closure-plan execution time
completed: 2026-04-25
---

# Phase 16 — Tutorial Refresh — SUMMARY

**A new user running `examples/tutorial.flow` against v1.2 now experiences every v1.1 + v1.2 composer-visible feature end-to-end — 14 features across 20 chapters, producing audible WAV + MIDI from a single graduation Song value (CONTEXT D-04 dual-export), with the showcase shipped in parallel as a 44-line "wow listen" v1.2 ambient mood piece. The phase ships QOL-03 and closes the v1.2 milestone (7 of 7 phases complete).**

---

## Goal vs Delivered

**Goal (ROADMAP):** A new user running `examples/tutorial.flow` against
v1.2 can experience every v1.1 + v1.2 composer-visible feature
end-to-end, producing audible WAV and MIDI output, so features added
since v1.0 stop atrophying unused.

**Delivered:**

- **examples/tutorial.flow** expanded 348 → 635 lines across 20
  numbered chapters. All 14 required v1.1+v1.2 features demonstrated
  with at least one runnable snippet per feature, each named in at
  least one prose comment (ROADMAP criterion #3 satisfied).
- **examples/tutorial.flow runs to completion** with exit 0 producing
  non-empty `examples/output/flow_tutorial.wav` (5,503,724 bytes) +
  `flow_tutorial.mid` (814 bytes) — ROADMAP criteria #1 and #2.
- **examples/showcase.flow** rewritten 84 → 44 lines as a v1.2 ambient
  mood piece (CONTEXT D-03 parallel scope) — Aminor/72 BPM,
  reverbTime 3.2 + euclidean 5/16 humanize seed=7 + dynamics mp +
  crescendo, "strings" preset; produces non-empty
  `examples/output/flow_showcase.{wav,mid}` (2,352,044 + 200 bytes).
- **examples/output/** directory established with co-located
  `.gitignore` (mirrors Phase 15 Plan 01 `tests/output/.gitignore`
  pattern); `git ls-files examples/output/` confirms only
  `.gitignore` tracked, all generated artifacts properly ignored.
- **ROADMAP criterion #4** (C5 migration notes) marked moot per
  CONTEXT D-14 — fourth criterion-moot/reframe in v1.2 (after Phase 12
  TEST-03 + Phase 14 DX-06 + Phase 15 #3); audit trail preserved in
  16-VERIFICATION.md §Criterion #4 Moot.
- **Byte-identical determinism contract** (inherited from Phase 15
  Plan 05) holds end-to-end through the tutorial + showcase render
  paths: two consecutive runs produce `cmp`-clean WAV + MIDI for both
  files, leveraging fixed seeds (42 in tutorial, 7 in showcase).
- **Full suite GREEN** at phase close: `dotnet test flow-sharp.sln
  --nologo --no-build` reports 287/287 — same baseline as Phase 15
  close (no new test surface added by Phase 16; tutorial and showcase
  are runtime content, not test infrastructure).

---

## Plans Shipped

5 plans across 4 waves; full commit hash manifest in
[16-VERIFICATION.md §Commit Hash Manifest](./16-VERIFICATION.md).

**Wave 1 (sequential)** — output dir + v1.1 chapters:

- **16-01** — `examples/output/.gitignore` + 5 new tutorial chapters
  (Comments, Mixing, Synth Presets, Tempo Ramps, Voice Synthesis);
  existing chapters renumbered 5-15 without touching their bodies.
  Commits: `47269f4` (gitignore) + `94d20fb` (tutorial expansion).

**Wave 2 (sequential)** — v1.2 chapters + dual export:

- **16-02** — 5 new tutorial chapters (14-18: Slicing, Enharmonic & Flat
  Spellings, Reverb Time, MIDI Velocity with Dynamics, Euclidean
  Rhythms) + writeWav/writeMidi pair from same Song value (CONTEXT
  D-04). Existing graduation chapter 15 renumbered to 20. Commit:
  `5bf93c9`.

**Wave 3 (parallel)** — graduation refactor + showcase rewrite:

- **16-03** — graduation piece (chapter 20) refactored to integrate
  all 4 CONTEXT D-07 audible features: reverbTime 2.5 outro hall +
  euclidean 5/16 seed=42 groove + per-section gain 0.6/1.0 dynamic
  arc + tempoRamp 100→60 BPM ritardando tail mixed into the WAV.
  Commit: `be18d5c`.
- **16-04** — `examples/showcase.flow` rewritten 84 → 44 lines as v1.2
  ambient mood piece (Aminor 72 BPM, reverbTime 3.2 + euclidean 5/16
  humanize seed=7 + crescendo + inline mp). Commit: `1c3b723`.

**Wave 4 (sequential)** — closure:

- **16-05** (this plan) — REQUIREMENTS QOL-03 Shipped marker + ROADMAP
  Phase 16 complete + criterion #4 moot-note + 16-VERIFICATION.md +
  16-SUMMARY.md (this file) + STATE.md advance. Single atomic docs
  commit; v1.2 milestone ready for /gsd-complete-milestone.

---

## Feature Distribution

Which plan delivered which of the 14 v1.1+v1.2 features:

| Feature | Plan | Chapter |
|---------|------|---------|
| `//` line comments | 16-01 | Ch. 5 (Comments) + sprinkled |
| `mix` | 16-01 | Ch. 10 (Mixing) |
| Per-section `gain` | 16-01 (Ch. 11) + 16-03 (Ch. 20 audible arc) | Ch. 11, Ch. 20 |
| `strings` synth preset | 16-01 | Ch. 11 (Synth Presets) |
| `organ` synth preset | 16-01 | Ch. 11 (Synth Presets) |
| `bell` synth preset | 16-01 | Ch. 11 (Synth Presets) |
| `tempoRamp` | 16-01 (Ch. 12 ritardando+accelerando) + 16-03 (Ch. 20 audible WAV tail) | Ch. 12, Ch. 20 |
| `sing` | 16-01 | Ch. 19 (Voice Synthesis) |
| `tts` | 16-01 (prose mention only — espeak-ng not assumed per CONTEXT D-16) | Ch. 19 |
| `slice` | 16-02 | Ch. 14 (Slicing) |
| Enharmonic + flat literals | 16-02 | Ch. 15 (Enharmonic) |
| `reverbTime` | 16-02 (Ch. 16) + 16-03 (Ch. 20 outro hall) | Ch. 16, Ch. 20 |
| `dynamics`/`crescendo` MIDI velocity | 16-02 | Ch. 17 (MIDI Velocity) |
| `euclidean` swing/humanize | 16-02 (Ch. 18) + 16-03 (Ch. 20 groove) | Ch. 18, Ch. 20 |
| `writeWav` | 16-02 (graduation export reroute from /tmp to examples/output/) | Ch. 20 |
| `writeMidi` (D-04 dual export) | 16-02 (paired with writeWav) | Ch. 20 |

Showcase (Plan 16-04) anchors on `reverbTime` + `euclidean` humanize +
`dynamics`/`crescendo` (3 of 14) but is decoration, not education —
tutorial alone satisfies ROADMAP criterion #1 coverage.

---

## Divergences

Aggregate from per-plan SUMMARYs (full detail in each
`16-NN-SUMMARY.md` §Deviations):

- **Plan 01:** Zero deviations. Plan executed verbatim. Note: plan's
  expected test count (287) and the **worktree-base** baseline (284)
  differed by 3 — documented as planning-time count drift carried
  through subsequent plan SUMMARYs. At the closure-plan HEAD on
  mainline, the actual count is 287/287 (the 3-test gap was a
  worktree-base artifact, not a real regression — the missing tests
  exist in mainline ancestry but were not in the worktree branchpoint
  used by Plans 16-01..16-04 executor agents).
- **Plan 02:** One Rule-3 inline-fix. Plan's `<interfaces>` EXAMPLE
  used `String wavPath = "..."; (writeWav wavPath finalMix)` form,
  but the plan's `<verify><automated>` regex
  `writeWav.*examples/output/flow_tutorial\.wav` requires the path on
  the same line as the call. Resolved by inlining path strings into
  writeWav/writeMidi calls — both demonstrative AND verifiable. Same
  worktree-base 284/284 vs mainline 287/287 count drift documented.
  Documented `dotnet test flow-sharp.sln --nologo` CLR fatal error
  ("Internal CLR error 0x80131506") — unrelated to plan scope (test
  orchestration runner concurrency); resolved by running per-project
  `dotnet test flow-lang.Tests/flow-lang.Tests.csproj` which reports
  GREEN. Issue tracked but not blocking.
- **Plan 03:** Zero deviations. Plan A (mix-in tempoRamp tail)
  executed verbatim per the plan's `<interfaces>` skeleton; mix on
  first try, no fallback to separate-WAV needed. Two-run determinism
  smoke confirmed WAV + MIDI byte-identical (`cmp` rc=0 for both).
  Same worktree-base 284 count drift carried through.
- **Plan 04:** One Rule-1 auto-fix. First `dotnet run` on the
  showcase produced `examples/showcase.flow:29:44: error: Variable
  'melody' not found` — `dynamics mp { Sequence melody = | ... | }`
  block scoped `melody` to the dynamics block; the section block
  downstream couldn't reference it. Fixed by replacing with inline
  `mp` velocity marker form `Sequence melody = | mp _ _ E5q ... |` —
  same per-note velocity floor without the scope barrier. The plan's
  `<interfaces>` SUGGESTED SKELETON itself used the block form
  (warned about scoping in prose but used it in the example);
  inline-marker substitution is functionally equivalent and
  structurally cleaner. Both required dynamics-related primitives
  still demonstrated (inline mp marker AND crescendo curve).
- **Plan 05 (this plan):** Zero functional deviations. Documented the
  worktree-base vs mainline test-count drift in this SUMMARY (the
  per-plan SUMMARYs reported 284/284 from the worktree base; the
  closure plan runs on mainline at HEAD and observes 287/287, which
  matches the plan's expected count). The discrepancy is provenance,
  not regression — Phase 15 closed at 287, Phase 16 added no test
  surface, mainline at this commit is still 287.

---

## ROADMAP Evolution

- **2026-04-25:** Phase 16 completed. v1.2 milestone progress advances
  from 6/7 phases to **7/7** — all v1.2 phases complete. Milestone is
  ready for closure via `/gsd-complete-milestone v1.2`.
- **2026-04-25:** ROADMAP Phase 16 row marked Complete; Plans 16-01
  through 16-05 all checked; Progress table row updated `0/?` → `5/5`
  with completion date 2026-04-25.
- **2026-04-25:** ROADMAP Phase 16 criterion #4 marked **moot** per
  CONTEXT D-14 (audit-trail-preserving moot-note appended inside the
  Phase 16 detail block, with full audit trail in 16-VERIFICATION.md
  §Criterion #4 Moot). **Fourth criterion-moot/reframe in v1.2** —
  pattern established across Phase 12 TEST-03 reframe + Phase 14
  DX-06 reframe + Phase 15 criterion #3 reframe + Phase 16 criterion
  #4 moot.
- **2026-04-25:** ROADMAP milestone header advanced from "started
  2026-04-18" to "started 2026-04-18; final phase completed
  2026-04-25; ready for /gsd-complete-milestone".
- **2026-04-25:** REQUIREMENTS QOL-03 row flipped to Shipped with
  4-commit manifest (`94d20fb + 5bf93c9 + be18d5c + 1c3b723`);
  Traceability table updated; footer advanced.

---

## Deferred Items

No new deferred items introduced by Phase 16. Phase 14/15 deferred
items unchanged:

- **DEFER-02** (H = B note-stream alias) — STILL OPEN (depends on
  DEFER-03 pragma system shipping first)
- **DEFER-03** (pragma `enable` system) — STILL OPEN
- **DEFER-04** (multi-letter enharmonic-edge respelling: E↔Fb / B↔Cb /
  C↔B# / F↔E#) — STILL OPEN. Phase 16 chapter 15 honored CONTEXT D-16
  charitable interpretation by deliberately avoiding these edges
  (used Db4/Eb4/F#3/G#5/Db major key examples).
- **DEFER-06** (`slice` negative-from-end Pythonic indexing) — STILL
  OPEN. Phase 16 chapter 14 used positive indices only
  (`(slice xs 1 4)`, `(slice xs 3 100)`, `(slice xs 3 2)`).

---

## Threat Surface

Per-plan threat models (T-16-01..T-16-09 across all 5 plans) all
marked `accept` (low-risk content modifications). T-16-08 (tampering
of commit hash manifest accuracy) mitigated by collecting hashes via
`git log --oneline` AT COMMIT TIME in this closure plan. T-16-09
(repudiation of criterion #4 moot-note audit trail) mitigated by
citing three independent corroborating sources (Phase 11 dismissal +
Phase 12 SUMMARY non-trigger + CONTEXT D-14). No new code surface
introduced. `examples/output/.gitignore` prevents inadvertent commit
of generated artifacts (`git ls-files examples/output/` returns only
`.gitignore`).

---

## Next Phase

**v1.2 milestone closure** via `/gsd-complete-milestone v1.2`. After
the milestone closes, the project enters v1.3 planning.

Phase 17 HUMAN-UAT items (3 pending tests in
`.planning/phases/17-flow-language-server/17-HUMAN-UAT.md`, plus 2
deferred-to-first-tag rows for non-dev OS + Marketplace/OpenVSX
publish verification) remain orthogonal to milestone closure — they
resolve at first release tag, not at /gsd-complete-milestone.

---

## Self-Check: PASSED

Verified the closure-plan deliverables are all in place at the
closure commit:

- `.planning/REQUIREMENTS.md` QOL-03 row flipped to Shipped (`grep -c
  "QOL-03.*Shipped"` → 2 incl. Traceability table; no
  `<PLAN-NN-HASH>` placeholders remaining)
- `.planning/ROADMAP.md` Phase 16 row marked complete (`grep -c "^- \[x\]
  \*\*Phase 16:"` → 1); Plan list 5/5 checked; Progress table row
  updated to `5/5 | Complete | 2026-04-25`; criterion #4 moot-note
  appended (`grep -c "moot per CONTEXT D-14"` → 1); milestone header
  advanced (`grep -c "ready for /gsd-complete-milestone"` → 1)
- `16-VERIFICATION.md` (NEW) — exists with all 8 required sections;
  feature grep map pinned with actual counts; smoke transcript
  pinned; commit hash manifest pinned; criterion #4 moot-note with
  full audit trail; sign-off checklist
- `16-SUMMARY.md` (NEW) — this file
- `.planning/STATE.md` updated (Phase 16 closed, completed_phases 6
  → 7, total_plans + completed_plans both 41, milestone progress
  recomputed, Resume Instructions advanced to /gsd-complete-milestone
  v1.2, accumulated-context bullets added for Plans 16-01..16-05)
- `dotnet test flow-sharp.sln --nologo --no-build` → 287/287 GREEN at
  HEAD
- `dotnet run --project flow-interpreter examples/tutorial.flow` →
  exit 0 + "Congratulations" printed + non-empty WAV (5,503,724 B) +
  non-empty MIDI (814 B) at `examples/output/`
- `dotnet run --project flow-interpreter examples/showcase.flow` →
  exit 0 + non-empty WAV (2,352,044 B) + non-empty MIDI (200 B) at
  `examples/output/`
- `git ls-files examples/output/` returns only `examples/output/.gitignore`
  (artifacts properly ignored)
- Two-run determinism smoke clean (`cmp` rc=0 for tutorial+showcase
  WAV+MIDI)

---

*Phase: 16-tutorial-refresh*
*Closed: 2026-04-25*
