---
phase: 36-sequence-algebra-generative
plan: 03
subsystem: language-foundation
tags: [named-arguments, parameter-names, backfill, builtin-registry, D-36-11, PAT-01]
dependency_graph:
  requires:
    - 36-02   # named-arg parser + resolver dispatch + FunctionSignature.ParameterNames field
  provides:
    - "All 208 non-varargs registry.Register sites in BuiltInFunctions.cs carry ParameterNames"
    - "ParameterNamesCoverageTest single-source-of-truth grep gate (28-row Theory)"
    - "5/28 coverage rows GREEN at 36-03 closure (BuiltInFunctions + StdLib + Collections + Bars + DictFunctions)"
    - "15/28 coverage rows RED (intentional drive-by-test gate for Plan 36-04)"
    - "8/28 coverage rows trivially GREEN (zero-register files + not-yet-existent files)"
  affects:
    - 36-04 # Plan 36-04 source-file backfill flips its 15 RED coverage rows to GREEN
    - 36-05+ # Generative / improv / section plans inherit the universal named-arg surface
tech-stack:
  added: []
  patterns:
    - "Backfill convention: ParameterNames as 4th positional arg of FunctionSignature constructor (mirrors Phase 35 LANG-03)"
    - "Naming convention by domain: a/b for arithmetic, value for unary builtins, buf/seq/bar for music inputs, fn/pred for callbacks, d/k/v for dict ops, path-first for file I/O"
    - "Varargs short-circuit: IsVarArgs sites carry no ParameterNames per RESEARCH Open Question 2; the resolver's 5 validation gates reject named-arg calls to varargs with a clear diagnostic"
    - "Single-author coverage test: Plan 36-03 ships COMPLETE [InlineData] roster covering Plan 36-04 file scope; Plan 36-04 touches ONLY source files (eliminates parallel-write conflict)"
    - "File-not-found tolerance in coverage test: missing files (e.g., DSP/*Functions.cs that Plan 36-04 may create) are treated as zero-on-zero and auto-flip GREEN when the file appears"
key-files:
  created:
    - flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs
  modified:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
key-decisions:
  - "D-36-03-01 — backfill BuiltInFunctions.cs as the single-touched source file; StdLib.cs/Collections.cs/Bars.cs/DictFunctions.cs in scope have ZERO FunctionSignature constructions (the registry.Register calls all live in BuiltInFunctions.cs), so backfill is a no-op for them; their coverage rows pass with the 0-on-0 trivial case"
  - "D-36-03-02 — coverage test is line-grep based with single-line `//` comment skip (matches Phase 29 LicenseAuditTests shape); inline mid-line comments are NOT stripped (none of the in-scope sources put `registry.Register(` inside a comment fragment)"
  - "D-36-03-03 — non-existent files (DSP/CompressorFunctions/ReverbFunctions/FilterFunctions/DelayFunctions.cs) are treated as zero-on-zero in coverage test rather than skipped, so Plan 36-04 source-file creation auto-flips the row GREEN with no test edits required"
  - "D-36-03-04 — DictFunctions.cs row uses the same 0-on-0 trivial pass (the file is the data-layer implementing dict ops; the registrations live in BuiltInFunctions.cs RegisterDict)"
patterns-established:
  - "Backfilling FunctionSignature: replace `new FunctionSignature(name, types);` → `new FunctionSignature(name, types,\\n    ParameterNames: [\"n0\", \"n1\", ...]);` preserving exact existing indentation"
  - "Grep-gate test row: xUnit Theory + [InlineData(relativePath)] + line-filtered grep + Assert registerCount == paramNamesCount + varArgsCount"
  - "Composer-facing name picking: derive from the C# lambda's `args[N].As<...>()` local variable name; for multi-overload functions (transpose Semitone vs Cent, str over 18 types), use identical names across overloads when the semantic role is identical"
requirements-completed: [PAT-01]
metrics:
  duration: ~30 min
  tasks_completed: 2
  files_changed: 2
  parameter_names_added: 208
  coverage_test_rows: 28
  coverage_36_03_green: 5
  coverage_36_04_red: 15
  coverage_36_04_trivially_green: 8
  build_warnings: 5  # all pre-existing
  build_errors: 0
  tests_phase36_named_args: "12/12 GREEN"
  tests_phase36_prng_registry: "11/11 GREEN"
  tests_phase35_regression: "80/80 GREEN"
  completed_date: 2026-05-21
---

# Phase 36 Plan 03: ParameterNames backfill across BuiltInFunctions.cs + ParameterNamesCoverageTest scaffold

**208 of 211 `registry.Register(...)` sites in BuiltInFunctions.cs carry composer-facing `ParameterNames` (3 varargs exempt); 28-row coverage test gates both Plan 36-03 (5 rows GREEN) and Plan 36-04 (15 RED + 8 zero-count GREEN) file scope.**

## Performance

- **Duration:** ~30 min
- **Completed:** 2026-05-21
- **Tasks:** 2 (both committed atomically)
- **Files modified:** 1 (BuiltInFunctions.cs) + 1 created (ParameterNamesCoverageTest.cs)

## Accomplishments

- 208 `ParameterNames: [...]` annotations added to BuiltInFunctions.cs spanning RegisterStdLib (63), RegisterMath (20), RegisterCollections (16), RegisterAudio (46), RegisterContextDependentFunctions (11), RegisterDict (12), RegisterBars (8), RegisterMusicalNotationFunctions (19), RegisterEuclideanOverloads (2), RegisterIterationGuard (1).
- 3 varargs sites (`list`, `dict`, `dictTuple`) intentionally left without `ParameterNames` per RESEARCH Open Question 2 (the resolver's validation gate #1 rejects named-arg calls to varargs with a clear "cannot be used with variadic function" diagnostic — see Plan 36-02 Test 10).
- ParameterNamesCoverageTest xUnit Theory shipped with COMPLETE 28-row [InlineData] roster — single-source-of-truth ownership so Plan 36-04 needs no test-file edits, eliminating the parallel-write conflict per CONTEXT D-36-11 + checker review.
- Coverage gate state at closure: 5/28 GREEN (Plan 36-03 scope) + 8/28 trivially GREEN (zero-Register files + not-yet-existent files) + 15/28 RED (intentional drive-by-test gate for Plan 36-04).
- Phase 35 + Phase 36 NamedArgs + Phase 36 PrngRegistry regression suites all preserved (80 + 12 + 11 = 103 GREEN baseline unchanged).

## Task Commits

Each task was committed atomically:

1. **Task 1: Backfill BuiltInFunctions.cs stdlib + collections + bars + audio + dict + notation** — `3e5de25` (feat)
2. **Task 2: Ship ParameterNamesCoverageTest scaffold with complete [InlineData] roster** — `3df3a68` (test)

## Files Created/Modified

- **Modified:** `flow-lang/StandardLibrary/BuiltInFunctions.cs` — 416 insertions / 208 deletions (one-line signatures expanded to two-line `, ParameterNames: [...]` form; 3 varargs sites annotated with comment markers; sandwiched comments added at top of each RegisterX method explaining the D-36-11 convention).
- **Created:** `flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs` (130 lines) — xUnit Theory with 28 [InlineData] rows covering both Plan 36-03 (5 rows) and Plan 36-04 (23 rows) file scope. File mirrors `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` for shape.

## Naming Convention Picks (D-36-11 compliance)

The backfill assigns composer-facing parameter names per the convention in 36-03-PLAN.md `<interfaces>`. Conventions across the 208 sites:

| Domain        | Names used                                          | Example builtins                                         |
|---------------|-----------------------------------------------------|----------------------------------------------------------|
| Arithmetic    | `a`, `b`                                            | add / sub / mul / div / idiv / equals / lt / and / or    |
| Unary/value   | `value` / `x`                                       | str (18 overloads), neg, sin/cos/tan, sqrt, abs          |
| Strings       | `s`                                                 | len, print, stringToInt, stringToDouble                  |
| Collections   | `arr`, `n`, `element`, `start`, `end`, `step`       | take, drop, range, slice, append, prepend, contains      |
| Lambdas       | `fn`, `pred`, `initial`                             | each, map, filter, reduce, oscillator                    |
| Music inputs  | `seq`, `bar`, `note`, `notes`, `pitch`, `chord`     | createBar*, addNoteToBar, addBarToSequence               |
| Buffers       | `buf`, `frame`, `channel`, `frames`, `sampleRate`   | createBuffer, getSample, setSample, fillBuffer, mix      |
| Files         | `path` (first), `buf`, `bitDepth`                   | writeWav, exportWav, loadWav (path always first arg)     |
| Synthesizers  | `state`, `amplitude`, `frequency`, `duration`       | createOscillatorState, createSineTone, generateSine      |
| Envelopes     | `attack`, `decay`, `sustain`, `release`             | createAR, createADSR, applyEnvelope                      |
| Timeline      | `bpm`, `voice`, `track`, `offset`, `gain`, `pan`    | setBPM, createVoice, setVoiceGain, setTrackPan           |
| Dict          | `d`, `k`, `v`, `default`, `d1`/`d2`                 | get, getOr, set, remove, has, keys, values, merge        |
| Conditionals  | `cond`, `then`, `else`                              | if (Lazy + strict overloads)                             |
| Iteration     | `seed`, `max`                                       | ??set, setMaxIterations, setMaxVoices                    |
| Euclidean     | `hits`, `steps`, `note`, `swing`, `humanize`, `seed`| euclidean (3/4/6-arity overloads)                        |
| Math          | `base`, `exp`, `x`                                  | pow, log, floor, ceil, round                             |
| Tuples        | `tup`, `fn`                                         | unpack                                                   |
| Phase 26.1    | `value` (Beat constructor)                          | beat                                                     |

Multi-overload functions (e.g., `transpose` from Plan 36-02; `str` across 18 type overloads; `add`/`sub`/`mul`/`div` Int/Long/Float/Double/Number) use IDENTICAL parameter names across overloads where the semantic role is identical — composer mental model treats them as one logical function.

## Decisions Made

See `key-decisions` in frontmatter. Summary:

- **D-36-03-01** — BuiltInFunctions.cs is the single source file actually touched in Task 1. The plan's listed in-scope files (StdLib.cs / Collections.cs / Bars.cs / DictFunctions.cs) are companion data-layer files that contain `static Value Foo(IReadOnlyList<Value> args) { ... }` implementations referenced by the `registry.Register(...)` call sites in BuiltInFunctions.cs. They have ZERO FunctionSignature constructions, so their coverage rows pass with the trivial 0-on-0 case. This matches the planner's intent — the verify command for Task 2 (`grep -c "ParameterNames:" Collections/DictFunctions.cs` matches non-varargs Register count) is satisfied with 0 == 0.
- **D-36-03-02** — Coverage test uses line-filtered grep matching Phase 29 LicenseAuditTests precedent. Single-line `//` comments are stripped before counting; inline mid-line comments are NOT (none of the in-scope sources put `registry.Register(` inside a comment fragment).
- **D-36-03-03** — Coverage test handles non-existent files as zero-on-zero, not Assert.Skip. This keeps the roster complete and the Plan 36-04 row for any DSP-split file flips GREEN automatically when Plan 36-04 creates the file with proper backfill (no test edit required).
- **D-36-03-04** — Coverage gate uses `Assert.True(registerCount == paramNamesCount + varArgsCount, ...)` with a clear diagnostic message ("Backfill incomplete in flow-lang/{path}: {X} registry.Register call(s) but {Y} ParameterNames + {Z} IsVarArgs"). The RED state at Plan 36-04 scope rows surfaces the exact gap remaining.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Edit tool sandbox write barrier on BuiltInFunctions.cs**
- **Found during:** Task 1 (multi-section backfill)
- **Issue:** The Edit tool reported successful writes for ~15 separate edits across `RegisterIterationGuard` / `RegisterStdLib` / `RegisterMath` / `RegisterCollections` / `RegisterAudio`, and the in-tool Read showed the modified content, but the on-disk file (verified via Bash `grep`, `cat`, `md5sum`, `stat`) remained byte-identical to the pre-edit baseline. The Edit tool appears to operate against a snapshot/overlay layer that doesn't always persist back to the underlying ext4 inode for this particular file. Repeated Read+Edit cycles produced "file unchanged since your last Read" stale-cache errors.
- **Fix:** Switched to file-level rewrites via Python scripts (`/tmp/backfill.py`, `/tmp/backfill2.py`, `/tmp/backfill3.py`) executed through Bash. Each script reads the on-disk file, performs idempotent textual replacements with halt-on-not-found error reporting, and writes the result back via standard Python `open(path, "w")`. This bypasses the Edit-tool sandbox issue entirely. Three sequential scripts handled all 208 backfill sites with zero "NOT FOUND" errors after the third pass.
- **Files modified:** flow-lang/StandardLibrary/BuiltInFunctions.cs (single source touched)
- **Verification:** `grep -c "ParameterNames" flow-lang/StandardLibrary/BuiltInFunctions.cs` returns 208 + 3 IsVarArgs = 211 (matches `grep -c "registry.Register("`). `dotnet build` clean. `Phase36.NamedArgs` 12/12 GREEN. `Phase35` 80/80 GREEN.
- **Committed in:** 3e5de25 (Task 1 commit)
- **Tool note:** The Edit tool's "(file state is current in your context — no need to Read it back)" message and subsequent "Wasted call — file unchanged since your last Read" reminder both made the diagnostic non-obvious. After confirming via `cat` and `md5sum` that on-disk content remained unchanged despite ~15 reported-successful Edits, the workaround was switching authoring to standard file-write paths.

---

**Total deviations:** 1 auto-fixed (Rule 3 — blocking tool/sandbox issue).
**Impact on plan:** Zero. The Python-script workaround produces a byte-identical result to what the Edit tool would have produced if it had persisted. All naming convention picks, varargs exemptions, and coverage assertions ship exactly as the plan specified.

## Coverage Test [InlineData] Roster

All 28 rows shipped in Task 2 commit `3df3a68`. State at Plan 36-03 closure:

### Plan 36-03 scope — GREEN (5 rows)
| File | Register | ParameterNames | IsVarArgs | Status |
|------|---------:|---------------:|----------:|--------|
| BuiltInFunctions.cs                       | 211 | 208 | 3 | GREEN |
| StdLib.cs                                 |   0 |   0 | 0 | GREEN (trivial) |
| Collections.cs                            |   0 |   0 | 0 | GREEN (trivial) |
| Bars.cs                                   |   0 |   0 | 0 | GREEN (trivial) |
| Collections/DictFunctions.cs              |   0 |   0 | 0 | GREEN (trivial) |

### Plan 36-04 scope — trivially GREEN (8 rows, no Register calls)
| File | Register | Status |
|------|---------:|--------|
| Audio/SignalGeneration.cs                 |   0 | GREEN (trivial) |
| Audio/FileIO.cs                           |   0 | GREEN (trivial) |
| Audio/AudioCore.cs                        |   0 | GREEN (trivial) |
| Audio/ClassicalComposition.cs             |   0 | GREEN (trivial) |
| Audio/DSP/CompressorFunctions.cs          | n/a | GREEN (file not found — Plan 36-04 may create) |
| Audio/DSP/ReverbFunctions.cs              | n/a | GREEN (file not found — Plan 36-04 may create) |
| Audio/DSP/FilterFunctions.cs              | n/a | GREEN (file not found — Plan 36-04 may create) |
| Audio/DSP/DelayFunctions.cs               | n/a | GREEN (file not found — Plan 36-04 may create) |

### Plan 36-04 scope — RED (15 rows, intentional drive-by-test gate)
| File | Register | Status |
|------|---------:|--------|
| Audio/EffectsFunctions.cs                 |  21 | RED |
| Audio/PanningFunctions.cs                 |   1 | RED |
| Audio/PlaybackFunctions.cs                |  11 | RED |
| Audio/MidiExport.cs                       |   1 | RED |
| Audio/Tuning/ScalaBuiltins.cs             |   3 | RED |
| Audio/Sfz/SfzBuiltins.cs                  |   3 | RED |
| Audio/Vocalization/VocalizationFunctions.cs |  3 | RED |
| Transforms/TransformFunctions.cs          |  24 | RED (2 ParameterNames seeded by Plan 36-02 transpose) |
| Composition/VariationFunctions.cs         |   6 | RED |
| Composition/PolyrhythmFunctions.cs        |   2 | RED |
| Composition/SongFunctions.cs              |   4 | RED |
| Harmony/HarmonyFunctions.cs               |  15 | RED |
| Harmony/Voicings.cs                       |   2 | RED |
| TestFramework/TestFunctions.cs            |   6 | RED |
| VisualizationFunctions.cs                 |   2 | RED |

Each RED row's `Assert.True` message includes the exact gap: e.g., `"Backfill incomplete in flow-lang/StandardLibrary/Transforms/TransformFunctions.cs: 24 registry.Register call(s) but 2 ParameterNames + 0 IsVarArgs = 2."` — this is the actionable diagnostic Plan 36-04 will resolve.

## Issues Encountered

- **Worktree branch HEAD mismatch at executor entry.** The worktree's HEAD was at `687281c` (pre-36-02 merge), but the spawn-time `<worktree_branch_check>` block expects merge-base with `6e6f4b3` (post-36-02). Resolved by running `git reset --hard 6e6f4b39929c5398bd5418175f3f24a3d9aa8d0a` (which the worktree_branch_check block itself includes as a conditional self-recovery). After reset, all Phase 35 + Phase 36 baseline tests pass.
- **Pre-existing Phase 35 FlowTestCli failures.** Two tests (`FailingTestExitsNonZero`, `FlowTestRunsAllRegisteredTests`) fail with `flow.dll missing` until `dotnet build flow-cli/flow-cli.csproj` is run. Rebuilt flow-cli; both pass. This is environment setup, not a code regression.

## User Setup Required

None. No external service configuration required.

## Next Phase Readiness

- **Plan 36-04 ready to start in parallel.** The 15 RED coverage rows in `ParameterNamesCoverageTest` provide the exact list of source files Plan 36-04 must touch. The test file itself is owned by Plan 36-03 — Plan 36-04 will NOT edit it, eliminating the parallel-write conflict.
- **The named-arg surface (D-36-11) is now usable for the entire BuiltInFunctions.cs catalog.** Composers can immediately call any of the 208 backfilled builtins with named args: `(transpose seq amount=2)`, `(map arr fn=doubler)`, `(filter arr pred=isPositive)`, `(get d k=#foo)`, `(set d k=#foo v=42)`, `(writeWav path="out.wav" buf=mix)`, `(createBuffer frames=44100 channels=2 sampleRate=44100)`, etc.
- **Plan 36-12 (later in the phase)** will run the coverage test as the 100%-complete gate after Plan 36-04 finishes.

## Self-Check: PASSED

Verified before commit:

- All 2 task commits exist in git log (`3e5de25`, `3df3a68`)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` modified — 208 ParameterNames added, 3 IsVarArgs preserved, 211 total Register sites
- `flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs` created with 28-row [InlineData] Theory
- `dotnet build flow-lang/flow-lang.csproj` exits 0 (5 pre-existing warnings unchanged)
- `dotnet test --filter "Phase36.NamedArgs|Phase36.NamedArgBackcompat"` → 12/12 GREEN
- `dotnet test --filter "Phase36.ParameterNamesCoverageTest"` → 13/28 PASSED + 15/28 FAILED (expected RED state for 36-04 scope drive-by-test)
- `dotnet test --filter "Phase35"` → 80/80 GREEN (regression intact)
- `grep -v '^[[:space:]]*//' flow-lang/StandardLibrary/BuiltInFunctions.cs | grep -c "ParameterNames:"` → 208 (well above the verification floor of 150)
- `grep -v '^[[:space:]]*//' flow-lang/StandardLibrary/Collections/DictFunctions.cs | grep -c "ParameterNames:"` → 0 (matches the file's 0 non-varargs Register sites; the dict-op registrations live in BuiltInFunctions.cs RegisterDict and ARE backfilled there)

---
*Phase: 36-sequence-algebra-generative*
*Completed: 2026-05-21*
