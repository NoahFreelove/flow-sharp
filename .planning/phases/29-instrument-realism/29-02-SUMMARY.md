---
phase: 29-instrument-realism
plan: 02
subsystem: audio
tags: [sample-cache, sampler, varispeed, audiobuffer, flowengine, sample-based-synthesis, xunit, integration-tests]

# Dependency graph
requires:
  - phase: 29-instrument-realism
    provides: 21 CC-BY sample WAVs at flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/ (Plan 29-01)
  - phase: 28-articulation-and-polyphony
    provides: SynthUtils.GenerateArticulationADSR + Articulation.Legato enum (envelope helper consumed by Plan 03)
  - phase: 22-tuning-and-microtonal
    provides: FileIO.LoadWavInternal + FileIO.VarispeedResample (DX-15 sampler primitive)
provides:
  - "SampleCache class: per-FlowEngine cache for bundled instrument samples; idempotent EagerLoad keyed by (song, instrument); manifest-driven raw + varispeed-shifted dictionaries"
  - "SampledInstrumentRenderer class: INoteSynthesizer-shaped Render method; piano pp/ff crossfade + single-velocity linear scaling; trim/pad to authored duration; Phase 28 envelope hook left as PLAN-03 PLACEHOLDER"
  - "FlowEngine.SampleCache (instance) + FlowEngine.CurrentSampleCache (static accessor) for static-renderer reach-through"
  - "SongRenderer.RenderSong + RenderSongWithLambda + RenderSongWithTimeline now invoke SampleCache.EagerLoad on entry"
  - "SynthesizerFactory.Create(synthType, SampleCache?) overload — cache-injection seam for Plan 03/04 delegation"
  - "44 KB synthetic test-sample WAV fixture (.NET-side tests that don't need the full bundle)"
  - "9 new green xUnit Facts (3 SampleCacheTests + 6 SampledInstrumentSmokeTests across all 6 tonal instruments)"
affects: [29-03, 29-04, 29-05, 29-06, 29-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Static accessor for engine-owned services (FlowEngine.CurrentSampleCache mirrors the SynthUtils.ResetNoiseRng static-mutable-state precedent — single-engine-per-process project convention)"
    - "Deterministic sample-load iteration (Pitfall 5: sorted pitch list + ordinal velocity sort before file load) — preserves Phase 18/25/27 two-run byte-identical contract"
    - "Cache-injection factory overload pattern (Create(synthType) → Create(synthType, cache?)) — old callers still work; new callers thread cache without surface-area change"
    - "Test-class [Collection(\"FlowScripts\")] serialization for cwd-mutating integration tests (mandatory whenever Environment.CurrentDirectory is touched)"

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/SampleCache.cs
    - flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs
    - flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs
    - flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs
    - flow-lang.Tests/Fixtures/Phase29/tiny_test_sample.wav
  modified:
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
    - flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs
    - .gitignore

key-decisions:
  - "EagerLoad walks the InstrumentManifest entry, not the SongData note set: the bundle is small (≤ 10 samples per tonal instrument), and load-everything-once amortizes flat across the whole song regardless of which pitches it touches. Matches SPEC eager-load wording: 'all samples needed for this song under this instrument', interpreted at the instrument grain."
  - "Static CurrentSampleCache accessor (not ExecutionContext threading): single-engine-per-process is an established project convention (SynthUtils.ResetNoiseRng uses identical static-mutable-state). Dispose nulls the static only when it still points at the disposing engine, so back-to-back test engines don't clobber each other."
  - "Cache-injection seam (Plan 5) accepts the cache argument but doesn't consume it yet — preserves Plan 03/04 atomicity (those plans will rewrite the tonal synth classes as delegating shells, not modify NoteSynthesizer again)."
  - "RenderSongWithLambda also calls EagerLoad with an empty instrument name — uniform code path; SampleCache no-ops for unknown instrument keys."

patterns-established:
  - "Pattern: SampledInstrumentRenderer template — Plan 03/04 tonal-synth shells will adopt this for delegation"
  - "Pattern: deterministic-sort-before-iterate for any new cache walks added in Plans 03-07 (Pitfall 5 generalization)"
  - "Pattern: Phase29 test classes must declare [Collection(\"FlowScripts\")] when mutating cwd — generalize for Plans 03/04 cache-dependent integration tests"

requirements-completed: [REQ-1, REQ-4]

# Metrics
duration: 25min
completed: 2026-05-11
---

# Phase 29 Plan 02: SampleCache + SampledInstrumentRenderer Infrastructure Summary

**Per-FlowEngine sample cache (eager-loaded from the Plan 29-01 bundle) plus a sample-based renderer with piano pp/ff crossfade — the two infrastructure halves the Plan 03/04 delegation will plug into.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-11T04:34:01Z
- **Completed:** 2026-05-11T04:59:05Z
- **Tasks:** 8 (all autonomous, no checkpoints)
- **Files created:** 5
- **Files modified:** 4

## Accomplishments

- `SampleCache` per-engine singleton with manifest-driven eager-load (10 piano + 11 other tonal samples) and varispeed-shift memoization — REQ-4 acceptance gate green.
- `SampledInstrumentRenderer` produces a fitted AudioBuffer from `(MusicalNoteData, sampleRate, durationBeats, bpm, RenderTuning)`, with piano pp/ff crossfade and single-velocity amplitude scaling for the other 5 tonal instruments — REQ-1 acceptance gate (renderer half) green.
- All three `SongRenderer.RenderSong*` entries call `EagerLoad` on entry; idempotent and no-op safe for non-sampled instruments / lambda callers.
- `FlowEngine` owns the cache via both an instance property and a static accessor; `Dispose` correctly clears the static only when it still points to the disposed engine.
- `SynthesizerFactory.Create` gains a cache-aware overload — Plan 03/04 will swap the tonal switch arms to delegate to `SampledInstrumentRenderer` via this seam without touching `NoteSynthesizer.cs` again.
- 9 new green xUnit Facts: 3 cover the cache (speedup, idempotency, nearest-pitch math); 6 cover end-to-end rendering through each tonal instrument.
- Full pre-Phase-29 suite stays green: **flow-lang.Tests 1020/1020** (1011 baseline + 9 new), **flow-midi.Tests 13/13**.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create SampleCache class** — `fc124d0` (feat)
2. **Task 2: Create SampledInstrumentRenderer class** — `9163d66` (feat)
3. **Task 3: Wire FlowEngine to own SampleCache** — `1bb8d29` (feat)
4. **Task 4: Wire SongRenderer.RenderSong to call EagerLoad** — `218114a` (feat)
5. **Task 5: Update SynthesizerFactory.Create for cache injection** — `baac01f` (feat)
6. **Task 6: Create tiny test sample fixture WAV** — `c1592eb` (test)
7. **Task 7: Write SampleCacheTests** — `cc79317` (test)
8. **Task 8: Write SampledInstrumentSmokeTests + serialize Phase29 tests** — `41623ec` (test)

## Files Created/Modified

### Created

- `flow-lang/StandardLibrary/Audio/SampleCache.cs` — per-engine cache: raw + shifted dictionaries, deterministic-order EagerLoad, NearestSamplePitch, GetVarispeed memoization, HasInstrument predicate, RawSampleCount diagnostic.
- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` — INoteSynthesizer-shaped Render with piano crossfade vs single-velocity branch, trim/pad to authored duration, Phase 28 envelope hook left as PLAN-03 placeholder comment.
- `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` — 3 Facts: speedup, idempotency, nearest-pitch lookup math.
- `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` — 6 Theory rows (one per tonal instrument); each row does direct-API + end-to-end checks.
- `flow-lang.Tests/Fixtures/Phase29/tiny_test_sample.wav` — synthetic 0.5 s / 440 Hz / 16-bit mono PCM WAV at 44 144 bytes (under the 100 KB cap).

### Modified

- `flow-lang/Core/FlowEngine.cs` — `_sampleCache` field, `SampleCache` instance property, static `CurrentSampleCache` accessor, constructor instantiation, Dispose-nulling.
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — `using FlowLang.Core;` + 3 `FlowEngine.CurrentSampleCache?.EagerLoad(...)` calls (one per RenderSong* entry).
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — split `Create(synthType)` into a back-compat shim and a new `Create(synthType, SampleCache?)` cache-aware overload.
- `.gitignore` — unignore rule for `flow-lang.Tests/Fixtures/Phase29/*.wav` (mirrors existing `flow-lang/Samples` and `flow-lang.Tests/baselines` unignores).

## Decisions Made

- **Manifest-walking EagerLoad (not song-note-walking):** Phase 29 ships ≤ 10 samples per tonal instrument; iterating the manifest is simpler than scanning a SongData note tree, and the resulting cache works for any subsequent renderSong call that touches the same instrument. SPEC wording ("all samples needed for this song under this instrument") is honored at the instrument grain.
- **Static `CurrentSampleCache` accessor over ExecutionContext threading:** matches existing `SynthUtils.ResetNoiseRng` static-mutable-state precedent. Dispose-time null-check guards back-to-back FlowEngine construction in tests. SPEC threat-register accepts this (T-29-04).
- **`RenderSongWithLambda` also calls EagerLoad with empty instrument name:** uniform code path; SampleCache no-ops on unknown instruments so the call is free and keeps the three entries symmetric.
- **`SynthesizerFactory.Create(synthType, SampleCache?)` overload accepts the cache but doesn't yet use it:** Plan 03/04 will rewrite the tonal switch arms to construct delegating shells; the signature is in place so those plans don't re-touch `NoteSynthesizer.cs`.

## Deviations from Plan

The plan-sketch test code referenced APIs that don't exist (`engine.Run`, C-style `renderSong(s, "piano")` argument syntax) and `(print)`/`(length)` builtins available only with `use "@std"`. Three Rule-1-class corrections were made during Task 7-8 to match the actual Flow surface:

### Auto-fixed Issues

**1. [Rule 1 - Plan code bug] `engine.Run` API does not exist — rewrote tests to use FlowEngineRunner.RunSource**
- **Found during:** Task 7 (SampleCacheTests)
- **Issue:** Plan's example test code called `engine.Run(script, "<name>")` expecting a tuple `(Success, Stderr, ...)` return. The actual FlowEngine API exposes `Execute(source, fileName?)` returning `bool`. The standard test fixture `FlowEngineRunner` (in `flow-lang.Tests/Fixtures/FlowEngineRunner.cs`) wraps this with the expected tuple-shape return.
- **Fix:** Rewrote all `engine.Run` call sites to use `using var runner = new FlowEngineRunner(); var (success, _, stderr, _) = runner.RunSource(...)`.
- **Files modified:** flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs, flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs
- **Verification:** Tests build and pass (1020/1020 suite green).
- **Committed in:** cc79317, 41623ec.

**2. [Rule 1 - Plan code bug] C-style `renderSong(s, "piano")` is not Flow syntax — rewrote to S-expression form**
- **Found during:** Task 7 (first SampleCacheTests run failure: "Unexpected token Comma ','")
- **Issue:** Flow uses S-expression call syntax `(renderSong s "piano")`, not Python/C-style `renderSong(s, "piano")`. Every existing renderSong call site in `tests/*.flow` and `examples/*.flow` uses the S-expression form.
- **Fix:** Updated all test-script strings to `(renderSong songVar "instrument")`.
- **Files modified:** flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs, flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs
- **Verification:** Parser-level errors gone; tests pass.
- **Committed in:** cc79317, 41623ec.

**3. [Rule 3 - Blocking] Tests needed `use "@audio"` (renderSong is declared in audio.flow stdlib module)**
- **Found during:** Task 7 (second run failure: "Cannot convert Flow type 'Void' to 'Buffer'")
- **Issue:** `renderSong` is declared `internal proc` in `flow-lang/audio.flow`; without `use "@audio"`, the parser sees it as an unknown function and the variable binding gets Void.
- **Fix:** Prepend `use "@audio"` to every test-script heredoc.
- **Files modified:** flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs, flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs
- **Verification:** Tests pass.
- **Committed in:** cc79317, 41623ec.

**4. [Rule 3 - Blocking] Same-engine repeat-render hit "section already defined" — renamed sections per run**
- **Found during:** Task 7 (third run failure: "Section 'demo' is already defined")
- **Issue:** The speedup test runs two `RunSource` calls against the same FlowEngine. The interpreter's global scope persists across calls (intentional REPL semantics), so re-declaring `section demo` / `Song s` failed at parse time. Plan's example script used identical bodies for both runs.
- **Fix:** Split into `scriptA` / `scriptB` with `demoA` / `demoB` sections and `songA` / `songB` variables — different parsing identity, same musical content / same cache key (same instrument).
- **Files modified:** flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs
- **Verification:** Speedup test green; the cache hit still fires on run 2 because the instrument key matches the already-loaded manifest.
- **Committed in:** cc79317.

**5. [Rule 3 - Blocking] Parallel test runner caused cwd contention failures in full-suite runs**
- **Found during:** Task 8 full-suite re-run (filter-passed tests failed in full run)
- **Issue:** Phase 29 tests mutate `Environment.CurrentDirectory` to point at the repo root so SampleCache resolves its default `"flow-lang/Samples"` path. xUnit parallel-by-class collided this with other cwd-mutating suites (Phase 18 ByteIdentical, Phase 22 Voicing, etc.), randomly corrupting which directory was active when `EagerLoad` ran — producing `RawSampleCount == 0` and `NearestSamplePitch == input` (no-pitches-loaded fallback).
- **Fix:** Decorate both Phase29 test classes with `[Collection("FlowScripts")]` so xUnit serializes them with the existing cwd-mutating collection.
- **Files modified:** flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs, flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs
- **Verification:** `dotnet test flow-lang.Tests --no-build` exits clean (1020/1020).
- **Committed in:** 41623ec.

**6. [Rule 2 - Missing critical] `.gitignore` `*.wav` rule blocked the new test fixture from being tracked**
- **Found during:** Task 6 (`git check-ignore` reported the fixture was gitignored).
- **Issue:** The repo-wide `*.wav` ignore rule would have silently dropped the new `tiny_test_sample.wav` fixture from git, breaking CI on any clean checkout.
- **Fix:** Added an unignore block for `flow-lang.Tests/Fixtures/Phase29/**/*.wav` mirroring the existing `flow-lang/Samples` and `flow-lang.Tests/baselines` unignores.
- **Files modified:** .gitignore
- **Verification:** `git check-ignore -v` switches from `.gitignore:15:*.wav` to `.gitignore:81:!...!*.wav` (unignore wins).
- **Committed in:** c1592eb (part of the fixture commit).

**7. [Rule 2 - Missing critical] SampleCache: added `RawSampleCount` diagnostic + null-guard / case normalization in `EagerLoad`/`NearestSamplePitch`/`GetVarispeed`/`HasInstrument`**
- **Found during:** Task 1 (wrote SampleCache); Task 7 needed observability.
- **Issue:** Plan sketch did not expose any way for tests to verify eager-load actually populated the cache. Plan also did not specify null/empty-string handling for the instrument argument — at API boundaries this is required for the lambda-RenderSong path (empty instrument name) and for the EagerLoad-on-null-song case.
- **Fix:** Added `public int RawSampleCount => _rawCache.Count;` diagnostic; added `instrument = (instrument ?? string.Empty).ToLowerInvariant();` at every public entry; added `if (song is null) return;` guard at the top of `EagerLoad`.
- **Files modified:** flow-lang/StandardLibrary/Audio/SampleCache.cs
- **Verification:** All three SampleCacheTests Facts pass; null-input scenarios no longer NRE.
- **Committed in:** fc124d0.

---

**Total deviations:** 7 auto-fixed (3 Rule 1 — plan-code bugs, 3 Rule 3 — blocking issues during execution, 2 Rule 2 — missing critical functionality / gitignore + diagnostic API).
**Impact on plan:** All deviations were necessary to make the plan's success criteria achievable on the actual codebase. No scope creep — no new requirements added; same 8 tasks shipped with the same acceptance criteria met. The Rule-1 plan-code corrections (API mismatch, syntax) are typical when a plan is written from spec without first round-tripping through actual builds; documenting them here so Plan 03's author can sanity-check `renderSong` syntax and `use "@audio"` upfront.

## Issues Encountered

- The worktree was created against `be8c966` (a prior release-tag commit, far behind `dev`). The execution preamble's branch-check ran `git reset --hard ab2a598` to bring the worktree to the correct Plan 29-01 base. Standard parallel-executor setup; no plan-impact.

## User Setup Required

None — no external service configuration introduced.

## Threat Flags

No new security surface introduced. The SPEC threat-register (T-29-V5-02 mitigate, T-29-V5-03 accept, T-29-04 accept) covers everything this plan shipped:
- All file I/O uses paths built from fixed-string allowlisted instrument names + integer MIDI values + fixed velocity labels — no user-controlled path segments reach `Path.Combine`.
- `SongData` enters `EagerLoad` only via the trusted Flow interpreter; no external untrusted input.
- The static `CurrentSampleCache` accessor cleared on Dispose; project's single-engine-per-process convention covers leakage concerns.

## Next Phase Readiness

**Ready for Plan 03 (Wave 2):**
- `SampledInstrumentRenderer` exists with a stable `Render(MusicalNoteData, ...)` signature matching `INoteSynthesizer.RenderNote`. Plan 03 will rewrite each tonal Synthesizer class (Piano/Brass/Sax/Strings/Flute/Bell) to construct a `SampledInstrumentRenderer` instance (using the cache from `SynthesizerFactory.Create(synthType, cache)`) and forward `RenderNote` calls through.
- `SampleCache.EagerLoad` is wired into all `RenderSong*` entries, so the cache is populated before any tonal synth's `RenderNote` fires.
- The `PLAN-03 PLACEHOLDER` comment in `SampledInstrumentRenderer.Render` marks the exact insertion point for Phase 28 articulation envelope application — Plan 03 will replace the comment with a `SynthUtils.GenerateArticulationADSR + ApplyEnvelope` call.

**Blockers / concerns:** None. The 1020/1020 green suite and the deviation-tracked discoveries leave Plan 03 with a clean delegation seam.

## Self-Check

Verification of all artifacts and commits before close-out.

### Created files
- `flow-lang/StandardLibrary/Audio/SampleCache.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` — FOUND
- `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` — FOUND
- `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` — FOUND
- `flow-lang.Tests/Fixtures/Phase29/tiny_test_sample.wav` — FOUND

### Modified files
- `flow-lang/Core/FlowEngine.cs` — MODIFIED (verified `SampleCache` references present)
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — MODIFIED (verified `EagerLoad` references present)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — MODIFIED (verified `SampleCache` parameter present)
- `.gitignore` — MODIFIED (unignore rule present)

### Commits
- fc124d0 (Task 1) — FOUND
- 9163d66 (Task 2) — FOUND
- 1bb8d29 (Task 3) — FOUND
- 218114a (Task 4) — FOUND
- baac01f (Task 5) — FOUND
- c1592eb (Task 6) — FOUND
- cc79317 (Task 7) — FOUND
- 41623ec (Task 8) — FOUND

### Suite
- `dotnet build flow-sharp.sln` — 0 errors, 12 warnings (all pre-existing in unrelated files).
- `dotnet test flow-lang.Tests` — 1020/1020 PASS.
- `dotnet test flow-midi.Tests` — 13/13 PASS.

## Self-Check: PASSED

---
*Phase: 29-instrument-realism*
*Plan: 02*
*Completed: 2026-05-11*
