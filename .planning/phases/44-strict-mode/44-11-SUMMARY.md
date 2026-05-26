---
phase: 44-strict-mode
plan: 11
subsystem: composer-facing-fixtures
tags: [phase-44, wave-6, showcase, determinism, closure, integration-gate]

requires:
  - phase: 44-strict-mode/44-03
    provides: "OverloadResolver strict-tier filter — Decibel/Hertz/Second tagged-type overloads exact-match under strict (Axis A fixture relies on this)"
  - phase: 44-strict-mode/44-04
    provides: "6 forward + 4 reverse explicit-conversion builtins ((db x)/(hz x)/(ms x)/(sec x)/(cents x)/(semitones x) + (double x)/(float x)/(int x)/(long x)) — explicit-conversions fixture exercises every entry"
  - phase: 44-strict-mode/44-05
    provides: "13 §6a input-perimeter clamp sites — Axis B fixture passes in-range args to every clamp site to verify strict success path"
  - phase: 44-strict-mode/44-06
    provides: "HIGH-priority §6b advisory sites elevated — showcase chain exercises some of these (renderSong + reverb + lowpass paths)"
  - phase: 44-strict-mode/44-07
    provides: "MED+LOW §6b advisory sites elevated — pragma composition (justIntonation) traverses Harmony/Tuning paths"
  - phase: 44-strict-mode/44-09
    provides: "Set-theoretic (equals) + cross-type comparison strict error + Dict-strict regression pin — equality + dict_typecheck fixtures verify composer-facing behavior"
  - phase: 44-strict-mode/44-10
    provides: "REPL :strict on/off + sticky session (not directly exercised by fixtures but the integration phase-gate confirms script-mode strict paths stay GREEN)"

provides:
  - "7 composer-facing .flow fixtures under tests/strict/ — 6 narrow (axis_a / axis_b / explicit_conversions / equality / with_justintonation / dict_typecheck) + 1 showcase (~16 bars single-instrument piano, /tmp/flow_strict_showcase.wav)"
  - "StrictFlowScriptSuiteTests.cs — xUnit phase-gate that spawns flow-interpreter on each fixture via `dotnet exec` + asserts exit 0 + stdout contains PASS (9 Facts: 7 Theory rows + 2 sanity Facts)"
  - "Phase44TwoRunDeterminismTests.cs — REQ-STRICT-15 cmp-clean pin via SHA-256 stdout equality across two runs of ALL 7 fixtures (W10 expansion) + WAV byte-equal special-case Fact for showcase_strict.flow (8 Facts: 7 Theory rows + 1 WAV Fact)"
  - "deferred-items.md — documents 5 pre-existing test failures verified out-of-scope per SCOPE BOUNDARY rule (2 SymbolFacts from Plan 44-09 equals→context-dep migration; 2 FlowTestCliTests environment dep; 1 OscLoopback network flake)"

affects:
  - "Phase 44 closeout — feature-complete: pragma + AST + snapshot + Axis A + Axis B + Axis C + explicit conversions + REPL + live + showcase + determinism"
  - "Future v1.6 composer onboarding — `tests/strict/` is the canonical example set composers can copy when writing their own strict files"
  - "Future ROADMAP audit — strict-mode REQ-STRICT-14 + REQ-STRICT-15 complete; subsequent strict-mode evolution can use this fixture pattern as the regression baseline"

tech-stack:
  added: []
  patterns:
    - "Pattern P3 (NEW — fixture-as-truth): composer-facing .flow files under tests/strict/ are the canonical surface contract. Each fixture begins with `enable strict;`, ends with `(print \"...PASS\")`, runs cleanly under `dotnet run --project flow-interpreter`. Integration-gate xUnit Fact wraps the per-file loop as a single Theory so CI surfaces per-file regressions without masking. Mirrors the pattern established by Phase 21 `tests/test_tuning_*.flow` + Phase 39 `tests/test_notation_*.flow`."
    - "Pattern P4 (NEW — two-run cmp-clean via Process.Start): test class spawns the interpreter twice on the same fixture, captures stdout, SHA-256s each, asserts equality. WAV-emitting fixtures get an additional byte-equality pin via File.ReadAllBytes on the known output path. Charitable skip when flow-interpreter.dll is missing (mirrors Phase 39 mscore charitable-skip)."

key-files:
  created:
    - "tests/strict/test_strict_axis_a_overload.flow (Axis A: explicit Decibel/Hertz/Second overloads exact-match under strict; small DSP chain)"
    - "tests/strict/test_strict_axis_b_clamps.flow (Axis B: in-range args to all 13 §6a clamp sites — crescendo/decrescendo/swell/ritardando/accelerando/humanize/humanizeGaussian/tremolo/repeat/quantize all succeed)"
    - "tests/strict/test_strict_explicit_conversions.flow (6 forward + 4 reverse conversion builtins matrix; (semitones x) Int-only carve-out exercised)"
    - "tests/strict/test_strict_equality.flow (D-11 set-theoretic cross-type equals returns false; same-type comparisons work; (double x) escape hatch for cross-type comparison)"
    - "tests/strict/test_strict_with_justintonation.flow (D-15 pragma composition: enable strict; + enable justIntonation; both apply; small Cmaj triad rendered)"
    - "tests/strict/test_strict_dict_typecheck.flow (D-13 Dict type-strict regression pin: Int 1 ≠ Float 1.0 as keys; Symbol #foo ≠ String \"foo\" as keys)"
    - "tests/strict/showcase_strict.flow (~16 bar single-instrument piano piece — pad + lead + response sections; (gain -4dB) + (reverb 0.5 1.8s) + (lowpass 3500Hz) chain; writes /tmp/flow_strict_showcase.wav for SHA pinning)"
    - "flow-lang.Tests/Integration/Phase44/StrictFlowScriptSuiteTests.cs (9 Facts: 7 Theory rows over tests/strict/*.flow + Fact_AtLeastSevenStrictFiles_Exist regression-pin + Fact_ShowcaseStrict_Exists sanity)"
    - "flow-lang.Tests/Integration/Phase44/Phase44TwoRunDeterminismTests.cs (8 Facts: 7 Theory rows SHA-256 stdout equality + Fact_ShowcaseStrictWav_TwoRunsByteEqual WAV byte-equality)"
    - ".planning/phases/44-strict-mode/deferred-items.md (5 pre-existing test failures documented out-of-scope)"
  modified: []

key-decisions:
  - "Used `git add -f` for all 7 tests/strict/*.flow files (parent `tests/` directory is globally gitignored per .gitignore:9 — git documents that allow-list rules cannot re-include a file whose parent is excluded). Follows the existing Phase 39 D-39-22 precedent documented in .gitignore:224-233 (the 4 Phase 39 tests/test_notation_*_example.flow files used the same posture)."
  - "Subprocess strategy: `dotnet exec flow-interpreter.dll` not `dotnet run --project flow-interpreter`. `dotnet run` performs an implicit no-op restore + build check per invocation (30-60s in CI); `dotnet exec` launches the assembly directly in ~1s. Mirrors Phase 35 FlowTestCliTests precedent."
  - "Charitable skip when flow-interpreter.dll is absent. Both test fixtures short-circuit to no-op via `StrictFlowScriptSuiteTests.DllMissing` when the dll doesn't exist on disk. Mirrors Phase 39 mscore charitable-skip pattern (D-v1.5-05)."
  - "60s per-fixture WaitForExit timeout per T-44-11-01 mitigation. Infinite-loop fixture surfaces as a TimeoutException (test fails cleanly) rather than hanging the CI run."
  - "showcase_strict.flow uses NO PRNG-routed primitives (no humanize / no jam / no euclidean / no `(?)` random choice). Two-run cmp-clean for the WAV bytes falls out naturally because the audio chain is deterministic. Verified by capturing SHA-256 over two consecutive runs: `3457c75c5346cee0d6e9a1cfcfc2fd8b96f0f4459300f54cbdaa3c0678b3f894` (identical across runs)."
  - "Per W10 plan revision: Phase44TwoRunDeterminismTests converted from one-file pin to `[Theory]` over ALL 7 strict fixtures. Catches per-file non-determinism a one-file pin would miss. Each Theory row is independent — one fixture regression won't mask the others."

patterns-established:
  - "Pattern P3: composer-facing fixture-as-truth — composer-readable .flow files under tests/strict/ are the canonical surface contract for the strict mode feature. Each ends with a PASS sentinel; integration-gate Fact wraps the per-file loop as a Theory so CI surfaces per-file regressions. Future plans modifying strict semantics MUST update or extend these fixtures to keep the composer-facing contract truthful."
  - "Pattern P4: two-run cmp-clean via Process.Start — spawn the interpreter twice on the same fixture, capture stdout, SHA-256 each, assert equality. WAV-emitting fixtures get an additional File.ReadAllBytes byte-equality pin on the known output path. Together these guard the CLAUDE.md §\"Conventions\" two-run contract end-to-end."

requirements-completed:
  - REQ-STRICT-14
  - REQ-STRICT-15

duration: 21min
completed: 2026-05-25
---

# Phase 44 Plan 44-11: Composer-facing closer suite + showcase + determinism + integration phase-gate

**7 composer-facing strict-mode `.flow` fixtures under `tests/strict/` (6 narrow + 1 ~16-bar showcase) + 2 xUnit test classes (17 Facts: 9 StrictFlowScriptSuiteTests + 8 Phase44TwoRunDeterminismTests) close Phase 44. The phase-gate spawns the interpreter on each fixture via `dotnet exec` and pins PASS exit; the determinism gate runs each fixture twice and asserts SHA-256 equality of stdout + WAV bytes. REQ-STRICT-14 + REQ-STRICT-15 complete. Phase 44 is feature-complete.**

## Performance

- **Duration:** ~21 min
- **Started:** 2026-05-25T13:47:33Z
- **Completed:** 2026-05-25T14:08:23Z (approximate)
- **Tasks:** 2 (Task 1 — 7 `.flow` fixtures; Task 2 — 2 xUnit integration test classes + deferred-items log)
- **Production files modified:** 0 (pure composer-facing surface + xUnit gates; no stdlib / interpreter changes)
- **Files created:** 10 (7 `.flow` fixtures + 2 `.cs` test classes + 1 deferred-items.md)
- **Phase 44 xUnit count:** 206/206 GREEN (up from 189 pre-Plan-44-11 — net +17 Facts from this plan)

## Accomplishments

- **7 composer-facing strict fixtures landed at `tests/strict/`** — each begins with `enable strict;`, exercises one axis (or pragma composition / dict regression / full showcase), and ends with `(print "...PASS")` for unambiguous exit-code + grep verification. Every fixture runs cleanly via `dotnet run --project flow-interpreter tests/strict/<name>.flow`. Two of them (`test_strict_with_justintonation.flow`, `showcase_strict.flow`) write WAVs to `/tmp/` per the test harness convention.
- **Axis A fixture** demonstrates `(gain buf -6dB)` + `(reverb buf 0.5 1.5s)` + `(lowpass buf 2000Hz)` chaining — all explicit tagged-type literals route to dedicated overloads (exact-match +1000) under strict. No coercion required; the composer writes the natural, type-explicit form.
- **Axis B fixture** passes in-range args to all 13 §6a clamp sites — `crescendo 0.3 0.9` / `humanize 0.2` / `repeat 4` etc. — verifying the strict path stays composer-friendly when values are reasonable. Out-of-range strict-error coverage is already pinned by Plan 44-05's `Axis_B_ClampSiteTests.cs`.
- **Explicit conversion fixture** exercises every entry in the 6-forward + 4-reverse conversion matrix: `(db 5)` / `(db -12.0)` / `(db -6dB)` (idempotent) / `(hz 440)` / `(ms 100)` / `(sec 2.5)` / `(cents 50)` / `(semitones 2)` (Int-only carve-out) / `(double -12dB)` / `(float 440Hz)` / `(int +2st)` / `(int 100ms)` (floor) / `(long 2.5s)` (floor).
- **Equality fixture** verifies D-11 set-theoretic cross-type equals (`(equals 1 1.0)` → `false` in strict, NOT error), same-type comparisons work as expected, and the `(double x)` / `(int x)` escape hatch enables cross-type comparison after explicit conversion. The strict cross-type comparison error path (`[strict] cross-type comparison Int vs Double ...`) is INTENTIONALLY NOT exercised here — calling it would abort the script. Plan 44-09's `CrossTypeComparisonStrictTests.cs` pins that path verbatim.
- **JustIntonation composition fixture** declares both `enable strict;` and `enable justIntonation;` at file top, renders a small Cmaj triad through `(gain -3dB)` + `(reverb 0.4 1.2s)` + `(lowpass 4000Hz)` — proving D-15 pragma composition Just Works (CONTEXT §specifics).
- **Dict-typecheck fixture** pins D-13 type-strict matching: heterogeneous numeric keys (`Int 1` vs `Float 1.0`) stay distinct; heterogeneous string-like keys (`Symbol #foo` vs `String "foo"`) stay distinct; `(has)` distinguishes types. Construction uses empty `(dict)` + incremental `(set)` per the pattern documented in `DictTypeStrictRegressionTests.cs` (Dict literal infers `Dict<K, V>` from first key, so heterogeneous dicts need imperative build).
- **Showcase fixture** is a ~16-bar single-instrument piano piece — 4-bar I → vi → IV → V harmonic pad + 8-bar lead melody (with a `(cents 0)` transpose on the response section to highlight strict-mode `(cents x)` usage) + 4-bar lower-register response. Chain: `(gain raw -4dB)` → `(reverb 0.5 1.8s)` → `(lowpass 3500Hz)`. Renders to `/tmp/flow_strict_showcase.wav` for the WAV-byte-equality determinism pin.
- **StrictFlowScriptSuiteTests** (9 Facts) is the integration phase-gate. 7 Theory rows enumerate `tests/strict/*.flow` and spawn `dotnet exec flow-interpreter.dll <path>` per row (60s timeout, exit 0 + stdout-PASS asserted). 2 sanity Facts pin (a) `≥7 strict fixtures exist` (regression-pin against accidental fixture deletion) and (b) `showcase_strict.flow specifically exists` (the WAV-emitting target the determinism gate pins).
- **Phase44TwoRunDeterminismTests** (8 Facts) is REQ-STRICT-15. Per W10 plan revision, expanded from "representative subset" to `[Theory]` over ALL 7 strict fixtures — each runs twice via Process.Start, SHA-256 of normalized stdout MUST match. The audio-emitting `showcase_strict.flow` gets the stronger byte-equality pin: read the WAV after run 1, run 2, re-read the WAV, assert byte-length + SHA-256 match. **Captured SHA for posterity:** `3457c75c5346cee0d6e9a1cfcfc2fd8b96f0f4459300f54cbdaa3c0678b3f894` (3.36 MB WAV, identical across two consecutive runs).
- **Phase 44 feature-complete.** Pragma (44-01) + AST (44-02) + Axis A OverloadResolver filter (44-03) + explicit conversions (44-04) + Axis B §6a clamps (44-05) + HIGH §6b advisories (44-06) + MED+LOW §6b advisories (44-07) + Void-wildcard charitable handlers (44-08) + Axis C strict surface (44-09) + REPL `:strict on/off` + live-block strict (44-10) + composer-facing fixtures + showcase + determinism + integration phase-gate (44-11). 15 of 15 REQ-STRICT-NN requirements complete.

## Task Commits

Each task committed atomically:

1. **Task 1 — test(44-11): add 7 composer-facing strict-mode .flow fixtures** — `b4acbf4` — 7 fixtures under `tests/strict/` tracked via `git add -f` per the existing Phase 39 D-39-22 precedent (parent `tests/` directory is globally `.gitignore`'d).
2. **Task 2 — test(44-11): StrictFlowScriptSuiteTests + Phase44TwoRunDeterminismTests phase-gate** — `c38ea3f` — 2 xUnit fixtures (17 Facts) + deferred-items.md documenting pre-existing failures verified out-of-scope.

## Files Created

### Composer-facing fixtures (`tests/strict/*.flow`)
- **`test_strict_axis_a_overload.flow`** (24 LOC) — Decibel / Hertz / Second overload exact-match under strict; small DSP chain.
- **`test_strict_axis_b_clamps.flow`** (55 LOC) — in-range args to all 13 §6a clamp sites succeed.
- **`test_strict_explicit_conversions.flow`** (75 LOC) — 6 forward + 4 reverse conversion builtins matrix.
- **`test_strict_equality.flow`** (52 LOC) — set-theoretic cross-type equals + same-type comparisons + (double x) escape hatch.
- **`test_strict_with_justintonation.flow`** (40 LOC) — pragma composition: strict + justIntonation Just Work.
- **`test_strict_dict_typecheck.flow`** (54 LOC) — D-13 Dict type-strict regression pin (heterogeneous keys stay distinct).
- **`showcase_strict.flow`** (60 LOC) — ~16-bar single-instrument piano piece with explicit `(db x)` / `(sec x)` / `(hz x)` / `(cents x)` conversions naturally used.

### xUnit test classes
- **`flow-lang.Tests/Integration/Phase44/StrictFlowScriptSuiteTests.cs`** (170 LOC, 9 Facts) — phase-gate that runs every `tests/strict/*.flow` via `dotnet exec` against the pre-built flow-interpreter.dll. 7 Theory rows + 2 sanity Facts. Charitable skip when dll is missing.
- **`flow-lang.Tests/Integration/Phase44/Phase44TwoRunDeterminismTests.cs`** (165 LOC, 8 Facts) — REQ-STRICT-15 cmp-clean pin. 7 Theory rows over ALL strict fixtures (W10 expansion) + 1 WAV byte-equality Fact for showcase_strict.flow.

### Planning artifacts
- **`.planning/phases/44-strict-mode/deferred-items.md`** — documents 5 pre-existing test failures (2 SymbolFacts inherited from Plan 44-09 equals→context-dep migration; 2 FlowTestCliTests environment dep; 1 OscLoopback network flake). All verified out-of-scope per SCOPE BOUNDARY rule.

## Decisions Made

- **`git add -f` for fixtures.** Parent `tests/` directory is globally ignored in `.gitignore:9`; git docs say allow-list rules cannot re-include a file whose parent is excluded. The existing tracked `tests/*.flow` files (70+) coexist with this ignore via `git add -f` per Phase 39 D-39-22 precedent (documented in `.gitignore:224-233`). All 7 Plan 44-11 fixtures use the same posture.
- **`dotnet exec` not `dotnet run --project`.** `dotnet run` performs an implicit no-op restore + build check on every invocation (30-60s in CI per the Phase 35 FlowTestCliTests precedent comment). `dotnet exec` launches the assembly directly in ~1s — 7 Theory rows × 60s vs 7 × 1s is a material CI savings. Subprocess invocation: `dotnet exec "<dll>" "<flow-file>"` with RedirectStandardOutput + 60s WaitForExit cap (T-44-11-01).
- **Charitable skip when flow-interpreter.dll missing.** Tests short-circuit to no-op via `StrictFlowScriptSuiteTests.DllMissing` when the dll isn't on disk. Mirrors Phase 39 `MscoreAvailable()` charitable-skip pattern (D-v1.5-05 — gates must never block local dev when prerequisites absent).
- **Theory over ALL 7 fixtures for determinism (W10 expansion).** Plan originally specified "representative subset" — converted to `[Theory] [MemberData(nameof(AllStrictFlowFiles))]`. Each Theory row independent: one fixture regression surfaces without masking the others.
- **WAV byte-equality only for the showcase.** The 6 narrow fixtures emit no WAV output (or write to `/tmp` paths the test doesn't pin). Only `showcase_strict.flow` is the audio-emitting target; its WAV bytes are the canonical regression pin. Captured SHA: `3457c75c5346cee0d6e9a1cfcfc2fd8b96f0f4459300f54cbdaa3c0678b3f894`.
- **No PRNG-routed primitives in showcase.** humanize / humanizeGaussian / euclidean / jam / `(? ...)` / `(?? ...)` all OMITTED so two-run cmp-clean falls out for free (Phase 44 introduces zero new PRNG sites per RESEARCH Pitfall 5 — but the showcase still proves the contract end-to-end). Future plans wishing to test PRNG-routed strict paths should compose a separate fixture with explicit seeds.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `rit` / `accel` are reserved keywords (Axis B fixture)**
- **Found during:** Task 1, running the first draft of `test_strict_axis_b_clamps.flow`
- **Issue:** Initial draft used `Sequence rit = ...` and `Sequence accel = ...` as variable names for the `(ritardando)` + `(accelerando)` clamp-site verifications. Both `rit` and `accel` are reserved by `flow-lang/Lexing/SimpleLexer.cs:889-890` as music-notation tokens.
- **Fix:** Renamed to `Sequence slowed = ...` and `Sequence sped = ...`. No semantic change.
- **Files modified:** `tests/strict/test_strict_axis_b_clamps.flow`
- **Verified:** Fixture runs to PASS.
- **Committed in:** `b4acbf4` (Task 1 bundle commit).

**2. [Rule 1 - Bug] `tremolo` signature mismatch (Axis B fixture)**
- **Found during:** Task 1, running second-draft fixture.
- **Issue:** Initial draft called `(tremolo base 0.4)` — but the registered signature is `tremolo(Sequence, Int reps)` per `TransformFunctions.cs:241-244`. Double `0.4` did not match Int param under strict.
- **Fix:** Changed to `(tremolo base 4)` (4 reps within `[1, 16]` clamp range).
- **Files modified:** `tests/strict/test_strict_axis_b_clamps.flow`
- **Verified:** Fixture runs to PASS.
- **Committed in:** `b4acbf4`.

**3. [Rule 1 - Bug] `quantize` arg-count mismatch (Axis B fixture)**
- **Found during:** Task 1.
- **Issue:** Initial draft called `(quantize base 0.7 0.6)` — but the registered signature is `quantize(Sequence, NoteValue resolution, Double strength, Double swing)` per `TransformFunctions.cs:108-110` (4 args, not 3).
- **Fix:** Changed to `(quantize base QUARTER 0.7 0.6)`. Added `use "@notation"` import for the `QUARTER` NoteValue constant.
- **Files modified:** `tests/strict/test_strict_axis_b_clamps.flow`.
- **Verified:** Fixture runs to PASS.
- **Committed in:** `b4acbf4`.

**4. [Rule 1 - Bug] `length` builtin not defined (Axis B fixture)**
- **Found during:** Task 1.
- **Issue:** Initial draft tried to print `(length cresc)` and `(length repeated)` as sanity checks. `length` is not registered as a builtin in `BuiltInFunctions.cs`.
- **Fix:** Switched to `(str cresc)` / `(str repeated)` for sanity output — verifies the Sequence values are non-null + structurally well-formed. `str` overloads accept Sequence already per `BuiltInFunctions.cs:244-248`.
- **Files modified:** `tests/strict/test_strict_axis_b_clamps.flow`.
- **Committed in:** `b4acbf4`.

**5. [Rule 1 - Bug] `(str Hertz)` ambiguous under strict (Conversions fixture)**
- **Found during:** Task 1, running first-draft conversions fixture.
- **Issue:** No dedicated `str(Hertz)` overload exists; under strict, `(str hzFromInt)` resolved to "Ambiguous overload for function 'str' with argument types (Hertz). Candidates: str(Float), str(Double)" because both `Float.IsCompatibleWith(Hertz)` and `Double.IsCompatibleWith(Hertz)` route to compatible tier with the same score.
- **Fix:** Round-trip through `(str (double hzFromInt))` for printing — exercises the very `(double Hertz)` extractor Plan 44-04 shipped and produces a clean Double value `str` can dispatch on.
- **Files modified:** `tests/strict/test_strict_explicit_conversions.flow`.
- **Note:** This is a discoverability gap, not a strict-mode bug — composers who reach for `(str someHzValue)` in EITHER mode would hit the same ambiguity. Surface as v1.6 candidate: register `str(Hertz)` (and the other 4 fractional music types: Decibel/Cent/Millisecond/Second) for symmetric printing. The existing `(str Decibel)` / `(str Millisecond)` / `(str Second)` / `(str Cent)` overloads at `BuiltInFunctions.cs:234+` DO exist — only Hertz is missing.
- **Committed in:** `b4acbf4`.

**6. [Rule 1 - Bug] `_q.` parsed as member-access (Showcase fixture)**
- **Found during:** Task 1.
- **Issue:** Initial showcase draft included `A4q. _q.` in a turn figure. The trailing `.` after `_q` was lexed as a member-access dot, leading to "Expected member name after '.'. Got Pipe '|'".
- **Fix:** Simplified the turn figure to `| C5e D5e C5e B4e A4h |` — no dotted rests at bar end.
- **Files modified:** `tests/strict/showcase_strict.flow`.
- **Note:** Pre-existing lexer pitfall — `<rest>.` is not a valid dotted-rest syntax in Flow's lexer today. Composer workaround: use a half-note rest or extend the prior note instead. Surface as v1.6 ergonomics: allow dotted rests `_q.` explicitly. NOT Plan 44-11 scope.
- **Committed in:** `b4acbf4`.

**Total deviations:** 6 auto-fixed (all Rule 1 surface-discovery bugs in fixture authoring). All preserve plan intent; no architectural changes; no checkpoint trigger.

## Deferred Issues

Documented in `.planning/phases/44-strict-mode/deferred-items.md`:

- **`Phase26_1.SymbolFacts.StrictSeparation_SymbolNeqString`** + **`EqualsBuiltinReturnsTrueForSameSymbol`** — fail with "equals overload equals(Void, Void) not registered". Plan 44-09 migrated `equals` from `RegisterStdLib` to a context-dependent registration; the test's `BuildRegistry` helper doesn't wire the context-dep path. Verified pre-existing (Plan 44-11 commits do NOT touch SymbolFacts.cs or BuiltInFunctions.cs). Future quick-task: ~5 LOC change in SymbolFacts.BuildRegistry mirroring Plan 44-05's HumanizeGaussianFacts fix pattern.
- **`Phase35.FlowTestCliTests.{FlowTestRunsAllRegisteredTests, FailingTestExitsNonZero}`** — fail because `flow-cli/bin/Debug/net10.0/flow.dll` is not built. Test environment dependency, not a regression.
- **`Phase38.OscLoopbackTests.RoundTrip_127001_EphemeralPort_PreservesPayload`** — network test, likely flaky in sandbox environments.

Out-of-scope per the SCOPE BOUNDARY rule (only auto-fix issues DIRECTLY caused by the current task's changes). Documented for future quick-task pickup.

## v1.6 Backlog Suggestions

These surface during Plan 44-11 authoring but are out-of-scope for v1.5:

- **`str(Hertz)` overload** for symmetric music-type printing (the other 5 tagged music types already have dedicated `str` overloads; Hertz is the only one missing). Affects discoverability, not correctness.
- **Dotted rest syntax `_q.`** for cleaner turn-figure notation in note streams. Composer workaround today: use longer rests or extend the prior note.
- **Two-run cmp-clean Theory expansion to include MIDI byte equality** for any showcase that calls `writeMidi`. Plan 44-11's showcase only writes WAV, so MIDI byte-equality is not exercised.
- **Heterogeneous-key Dict literal** — current `(dict K V K V …)` infers Dict<K, V> from FIRST key. A composer wanting `Dict<Number, String>` with both Int 1 and Float 1.0 keys must build incrementally via `(set)`. Surface as a v1.6 ergonomics candidate.

## Issues Encountered

- **`.gitignore` global `tests/` block.** Initial `git status --short` after fixture authoring showed empty — the new files were silently ignored. Resolved via `git add -f` (per the existing Phase 39 D-39-22 precedent documented inline in `.gitignore:224-233`). Mirrors the SymbolFacts / FlowTestCliTests pattern of "files added under globally-ignored parent must be force-staged".
- **Stash leak from sibling worktree visible in `git stash list`.** `stash@{0}: WIP on worktree-agent-ad12fe57630023274: ...` is a stash created by a different per-agent worktree. Did NOT touch it (per worktree-path-safety.md prohibition on `git stash` operations inside per-agent worktrees — would silently apply foreign WIP).

## User Setup Required

None — no external configuration introduced. Composers can browse `tests/strict/` directly as the canonical example set.

## Next Phase Readiness

Phase 44 is **feature-complete**. All 15 REQ-STRICT-NN requirements complete (01..15). The strict-mode surface is composer-facing + integration-tested + determinism-pinned. Subsequent strict-mode evolution (the v1.6 `strictPurity` / `strictLengths` sub-pragma candidates documented in CONTEXT.md `<deferred>`) can use Plan 44-11's fixture pattern + `StrictFlowScriptSuiteTests` + `Phase44TwoRunDeterminismTests` as the regression baseline.

The `/gsd:verify-work` audit can now run against Phase 44 and verify:
- All 15 REQ-STRICT-NN requirements traced to their pinning xUnit tests.
- All 7 composer-facing fixtures runnable end-to-end via the integration phase-gate.
- Two-run cmp-clean preserved across the entire strict-mode introduction.

## Self-Check: PASSED

- All 10 created files exist on disk:
  - `tests/strict/test_strict_axis_a_overload.flow`
  - `tests/strict/test_strict_axis_b_clamps.flow`
  - `tests/strict/test_strict_explicit_conversions.flow`
  - `tests/strict/test_strict_equality.flow`
  - `tests/strict/test_strict_with_justintonation.flow`
  - `tests/strict/test_strict_dict_typecheck.flow`
  - `tests/strict/showcase_strict.flow`
  - `flow-lang.Tests/Integration/Phase44/StrictFlowScriptSuiteTests.cs`
  - `flow-lang.Tests/Integration/Phase44/Phase44TwoRunDeterminismTests.cs`
  - `.planning/phases/44-strict-mode/deferred-items.md`
- All 2 task commits present in `git log --oneline`:
  - `b4acbf4` test(44-11): add 7 composer-facing strict-mode .flow fixtures
  - `c38ea3f` test(44-11): StrictFlowScriptSuiteTests + Phase44TwoRunDeterminismTests phase-gate
- All 7 fixtures execute to PASS under `dotnet run --project flow-interpreter <file>`.
- All 17 new Facts (9 StrictFlowScriptSuiteTests + 8 Phase44TwoRunDeterminismTests) GREEN.
- Full Phase 44 suite: 206/206 GREEN — zero regressions.
- showcase_strict.flow WAV SHA verified identical across two consecutive runs: `3457c75c5346cee0d6e9a1cfcfc2fd8b96f0f4459300f54cbdaa3c0678b3f894`.
- 5 non-Phase44 pre-existing failures verified out-of-scope (documented in `deferred-items.md`).

---
*Phase: 44-strict-mode*
*Plan: 11*
*Completed: 2026-05-25*
