---
phase: 13-nyquist-validation-backfill
plan: 05
subsystem: testing
tags: [validation, backfill, nyquist, phase10, vocalization, voc-01, voc-02, formant-synthesis, tts-hook, two-pass-strict, xunit, test-04, phase-exit]

requires:
  - phase: 10-vocalization
    provides: "VOC-01 FormantSynthesizer.SynthesizeVowel (Phase 10-01), VOC-02 TtsHook.SetCommand/GetCommand/RunTts (Phase 10-02)"
  - phase: 12-stability
    provides: "FlowEngineRunner fixture + FlowScriptData Theory harness + RequiredSentinels dictionary + Unit/CollectionsTests.cs Assert.Throws<T> pattern"
  - phase: 13-01
    provides: "Integration/Unit/Phase{NN}/ directory convention + two-pass strict authorship template (replicated across 13-02/13-03/13-04)"
  - phase: 13-02
    provides: "Pass 1 -> Pass 2 drift documentation pattern (DX-02 Double format drift under Pitfall 5)"
  - phase: 13-03
    provides: "API-shape divergence documentation pattern (AudioCore.Mix signature, SynthesizerFactory namespace)"
  - phase: 13-04
    provides: "Zero-Divergences outcome pattern + boolean-result-concat sentinel idiom + 77-test baseline"

provides:
  - "Retroactive 10-VALIDATION.md promotion from nyquist_compliant: false to nyquist_compliant: true (TEST-04 phase-10-promote-not-waive path; closes ROADMAP criterion 2)"
  - "FormantSynthesizerTests.SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames Fact pinning VOC-01 D-18 canonical 88200-sample observable"
  - "FormantDataTests.GetFormants_UnknownVowel_ThrowsArgumentException Fact pinning VOC-01 exact unknown-vowel error message"
  - "TtsHookTests.SetCommand_RoundTrips_ViaGetCommand Fact pinning VOC-02 static-API round-trip with Pitfall-9 global-state restore"
  - "TtsHookTests.SetCommand_Empty_ThrowsArgumentException Fact pinning VOC-02 empty-command validation (Assert.Contains per 2-arg ctor paramName suffix)"
  - "Tightened RequiredSentinels[test_vocalization.flow] with 4 empirical PASS sentinels (sing-ah, 5-vowels, consonant-syllables, vocal-mix)"
  - "TEST-04 closure in REQUIREMENTS.md (shipped 21e773d, 2026-04-19) + STATE.md Phase 14 advance + ROADMAP.md Phase 13 5/5 Complete"
  - "Phase 13 aggregate: 5 VALIDATION.md docs at nyquist_compliant: true; 13 new Facts/sentinels; 81/81 suite green"

affects: [14-composer-dx-part-1, 15-composer-dx-part-2, 16-tutorial-refresh]

tech-stack:
  added: []
  patterns:
    - "Promote-not-waive path for a phase with existing draft VALIDATION.md (Phase 13 criterion 2 chose promote) — preserves existing Manual-Only table verbatim, appends Observable Invariants / Pass 1 Draft / Pass 2 Implementation Map / Divergences, updates Test Infrastructure table to reflect post-Phase-12 xUnit harness"
    - "2-arg ArgumentException ctor surfaces paramName in Message — when pinning exception messages that route through ArgumentException(message, paramName), use Assert.Contains (framework appends ' (Parameter ...)'); when they route through ArgumentException(message) only, Assert.Equal holds. Third API-shape divergence surfaced by two-pass strict in the 13-series (after 13-02 DX-02 Double format, 13-03 AudioCore.Mix signature)"
    - "Global-static-restore pattern in finally for test hygiene — when a unit test mutates a mutable static (TtsHook._ttsCommand), capture the original value via the public getter, mutate in the try body, restore in finally. Mirrors Phase 12 Plan 03 ThunkTests test-double enablement ethos (don't pollute shared state across Facts)"
    - "Pitfall 8 (syllable sample-count non-linearity) documented: D-18 88200-sample pin applies only to pure vowels, not consonant-vowel syllables (crossfade math at FormantSynthesizer.cs:128 produces non-linear totals — e.g. na frames: 28004, sa frames: 16097 for 0.5s @ 44.1kHz). Future pinning of syllable counts must derive from consonantLength + vowelLength - crossfadeSamples, not duration * sampleRate alone"
    - "Phase-exit rollup commit bundles REQUIREMENTS.md + STATE.md + ROADMAP.md into a single atomic docs commit (13-05 Task 3 matches Phase 12's 12-06 closure pattern but without a dedicated phase-exit plan — 13-05 is both the final plan AND the closure rollup)"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase10/FormantSynthesizerTests.cs
    - flow-lang.Tests/Unit/Phase10/FormantDataTests.cs
    - flow-lang.Tests/Unit/Phase10/TtsHookTests.cs
    - .planning/phases/13-nyquist-validation-backfill/13-05-SUMMARY.md
  modified:
    - .planning/phases/10-vocalization/10-VALIDATION.md
    - flow-lang.Tests/FlowScriptData.cs
    - .planning/REQUIREMENTS.md
    - .planning/STATE.md
    - .planning/ROADMAP.md

key-decisions:
  - "[Plan 13-05] Phase 10 VALIDATION.md promoted-not-waived: existing draft's 2 Manual-Only rows (formant audio quality, TTS external command) preserved verbatim as legitimate subjective/environmental verifications, NOT coverage gaps. All automatable requirements shipped as 4 new pure-C# Facts; ROADMAP Phase 13 criterion 2 satisfied via promote path per CONTEXT D-16/D-17."
  - "[Plan 13-05] VOC-02 empty-command assertion SHIFTED from Assert.Equal (drafted in Pass 1 from TtsHook.cs:19-20 literal read) to Assert.Contains (Pass 2 empirical adjustment) because TtsHook.SetCommand uses ArgumentException(message, paramName) — the 2-arg ctor appends ' (Parameter ''command'')' to Message at runtime. VOC-01 unknown-vowel assertion stays at Assert.Equal because FormantData.GetFormants uses the 1-arg ArgumentException(message) ctor. Third API-shape divergence in the 13-series (after DX-02 Double format + AudioCore.Mix signature)."
  - "[Plan 13-05] Pitfall 8 (syllable sample-count non-linearity) empirically confirmed: the D-18 88200-sample canonical pin at 2.0s @ 44100Hz applies ONLY to pure vowels. Consonant-vowel syllables produce off-count buffers (na: 28004 frames, sa: 16097 frames for 0.5s) due to the 15ms crossfade math at FormantSynthesizer.cs:108-147 with combined length = consonantLength + vowelLength - crossfadeSamples. The Unit Fact intentionally restricts the pin to the pure-vowel case; future syllable-count pinning must derive expected values from the combine math."
  - "[Plan 13-05] Phase-exit rollup bundled into Task 3: TEST-04 marked [x] Shipped in REQUIREMENTS.md (commit 21e773d + per-plan commit enumeration for future bisect), STATE.md advanced to Phase 14 (3/6 v1.2 phases complete), ROADMAP.md Phase 13 row 4/5 In progress -> 5/5 Complete. Phase 13 does NOT spin a dedicated rollup plan (unlike Phase 11 11-06 or Phase 12 12-06) because the closure is 3 small doc edits and fits within the final plan's atomic-commit budget."
  - "[Plan 13-05] Global-static restore pattern shipped in TtsHookTests.SetCommand_RoundTrips_ViaGetCommand: try { SetCommand('echo'); assert; } finally { SetCommand(original); }. Without this, Fact ordering could leak state across runs. Mirrors Phase 12 Plan 03 ThunkTests test-hygiene ethos."
  - "[Plan 13-05] Test Infrastructure table staleness fix (Pass 2 adjustment): existing draft described '.flow test scripts executed directly' / 'for test in tests/test_*.flow...'; Pass 2 updated to reflect post-Phase-12 xUnit.v3 3.2.2 + dotnet test flow-sharp.sln reality. Recorded under Divergences as a documentation-vs-reality drift, not a requirement drift."

patterns-established:
  - "Pattern: Promote-not-waive VALIDATION.md upgrade — when a phase already has a draft VALIDATION.md at nyquist_compliant: false, the 2-pass authorship appends new sections (Observable Invariants, Pass 1 Draft, Pass 2 Implementation Map, Divergences) rather than rewriting the body, preserves any pre-existing Manual-Only table verbatim (legitimate subjective/environmental rows are NOT gaps), updates Test Infrastructure table to reflect current harness reality, and flips the frontmatter in the Task 2 commit."
  - "Pattern: ArgumentException ctor-arity discrimination for Message pinning — 1-arg ctor (ArgumentException(message)) produces exact equality; 2-arg ctor (ArgumentException(message, paramName)) appends ' (Parameter ...)' so pins must use Assert.Contains. Grep for `throw new ArgumentException(` with 1 vs 2 string args to choose assertion kind."
  - "Pattern: Pitfall-8 syllable sample-count non-linearity — formant-synthesized consonant-vowel syllables do NOT obey duration × sampleRate arithmetic because of crossfade blending (15ms overlap). Pin only pure vowels at duration × sampleRate; future syllable pinning derives expected from consonantLength + vowelLength - crossfadeSamples."
  - "Pattern: Phase-exit rollup in the final plan (no dedicated closure plan) — when a phase's closure is <=3 small doc edits (REQUIREMENTS.md row flip + traceability row + footer; STATE.md position advance; ROADMAP.md progress row), fold them into the final plan's Task N rather than spinning a dedicated 11-06/12-06-style rollup plan. Saves overhead when closure is trivial."

requirements-completed: [TEST-04]

duration: 14min
completed: 2026-04-20
---

# Phase 13 Plan 05: Phase 10 VALIDATION.md Promotion + TEST-04 Closure Summary

**Retroactive Phase 10 VALIDATION.md promoted from draft to nyquist_compliant: true via 4 new pure-C# Facts (FormantSynthesizer 88200-pin, FormantData unknown-vowel, TtsHook round-trip + empty-command); TEST-04 closed as Shipped 21e773d across 5 plans + 16 observable-value pins; Phase 13 closed at 5/5 Complete; 81/81 suite GREEN (baseline 77 + 4 new).**

## Performance

- **Duration:** ~14 min
- **Started:** 2026-04-20T03:41:09Z
- **Completed:** 2026-04-20T03:47:28Z (plus summary authorship)
- **Tasks:** 3 (Pass 1 draft + Pass 2 Facts/sentinel + promotion + phase-exit rollup)
- **Files modified:** 6 (1 promoted VALIDATION.md + 3 created Facts + 1 modified FlowScriptData + 3 modified traceability/state/roadmap files)

## Accomplishments

- `.planning/phases/10-vocalization/10-VALIDATION.md` promoted from `nyquist_compliant: false` draft to `nyquist_compliant: true`, `status: passed`, `wave_0_complete: true`, with `backfilled: true` + `promoted: 2026-04-19` frontmatter markers. Existing Manual-Only table (formant audio quality + TTS external command via espeak-ng) preserved verbatim as legitimate subjective/environmental items per CONTEXT D-16. Test Infrastructure table updated to reflect post-Phase-12 xUnit.v3 3.2.2 harness.
- `flow-lang.Tests/Unit/Phase10/FormantSynthesizerTests.cs` — new `[Fact] SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames` pinning the D-18 canonical observable: `FormantSynthesizer.SynthesizeVowel("ah", 261.63, 2.0)` returns `AudioBuffer { Frames: 88200, Channels: 1, SampleRate: 44100 }` (2.0s × 44100Hz under IEEE-754).
- `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` — new `[Fact] GetFormants_UnknownVowel_ThrowsArgumentException` pinning exact `ArgumentException` message `"Unknown vowel phoneme: 'xyz'. Valid: ah, ee, eh, oh, oo"`. `Assert.Equal` holds because `FormantData.GetFormants` uses the 1-arg `ArgumentException(message)` ctor with no paramName suffix.
- `flow-lang.Tests/Unit/Phase10/TtsHookTests.cs` — new `[Fact] SetCommand_RoundTrips_ViaGetCommand` (with try/finally global-static restore per Pitfall 9) + `[Fact] SetCommand_Empty_ThrowsArgumentException` (using `Assert.Contains` because `TtsHook.SetCommand` uses the 2-arg `ArgumentException(message, paramName)` ctor which appends ` (Parameter 'command')` to `Message`). NO subprocess invocation — `RunTts` stays Manual-Only.
- `flow-lang.Tests/FlowScriptData.cs` — appended `RequiredSentinels["test_vocalization.flow"]` with 4 empirical PASS sentinels captured via `dotnet run --project flow-interpreter tests/test_vocalization.flow`. Converts the existing Plan 12-01 Theory row from `errorCount==0`-only to substring-pinned.
- `.planning/REQUIREMENTS.md` — TEST-04 row flipped from `[ ] Pending` to `[x] Shipped 21e773d, 2026-04-19` with full per-plan commit enumeration (13-01..13-05, 15 commits total). Traceability row + footer updated.
- `.planning/STATE.md` — Phase 13 (executing) → Phase 14 (not started); completed_phases 2 → 3, completed_plans 16 → 17, percent 94 → 100; Phase 13 P05 metrics row appended (14 min, 3 tasks, 6 files).
- `.planning/ROADMAP.md` — Phase 13 phase-list entry `[ ] → [x]` (completed 2026-04-20); 13-05-PLAN.md row marked complete with commits + empirical findings; progress table row 4/5 In progress → 5/5 Complete.
- Full `dotnet test flow-sharp.sln`: **81/81 GREEN** (baseline 77 at HEAD of 13-04 + 4 new Unit/Phase10 Facts; sentinel append is additive on existing Theory row, no row-count change).

## Task Commits

Each task was committed atomically:

1. **Task 1: Pass 1 — Requirements-only 10-VALIDATION.md draft (preserving existing body)** — `331d059` (docs)
2. **Task 2a: Pass 2 — Author Phase 10 validation Facts + sentinel** — `81f348c` (test)
3. **Task 2b: Pass 2 — Promote 10-VALIDATION.md to nyquist_compliant** — `21e773d` (docs)
4. **Task 3: Close TEST-04 — REQUIREMENTS.md + STATE.md + ROADMAP.md phase exit** — `7d47ae8` (docs)

**Phase 13 aggregate per-plan primary commits (for bisect traceability):**
- 13-01: `ff901fa` + `4cf0ccd` + `39d53f3` (Phase 6 VALIDATION.md, VerboseFlag + SectionGainBareExpression Facts, FIX-01 sentinel)
- 13-02: `fb1a1ae` + `ed64dec` + `9d7575f` (Phase 7 VALIDATION.md, RepLAutoImport Fact, test_comments/test_math/test_writewav sentinels)
- 13-03: `ea1d95a` + `511085f` + `b077491` (Phase 8 VALIDATION.md, Mix + SynthesizerFactory Unit Facts, test_mix/test_gain_context/test_synth_presets sentinels)
- 13-04: `ade6fbd` + `1a41ada` + `1cb508d` (Phase 9 VALIDATION.md, TutorialTests Fact, test_tempo_ramp sentinel)
- 13-05: `331d059` + `81f348c` + `21e773d` + `7d47ae8` (Phase 10 VALIDATION.md promotion, FormantSynthesizer + FormantData + TtsHook Facts, test_vocalization sentinel, TEST-04 closure rollup)

## Files Created/Modified

- `.planning/phases/10-vocalization/10-VALIDATION.md` — MODIFIED: frontmatter promoted (status draft → passed, nyquist_compliant false → true, wave_0_complete false → true, backfilled: true added, promoted: 2026-04-19 added); Test Infrastructure table rewritten for xUnit reality; existing Manual-Only table preserved verbatim; four new sections appended (Observable Invariants, Pass 1 Draft, Pass 2 Implementation Map, Divergences). Per-Task Verification Map and Wave 0 Requirements sections retained as historical record per plan instructions.
- `flow-lang.Tests/Unit/Phase10/FormantSynthesizerTests.cs` — NEW: single `[Fact] SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames`. Imports `FlowLang.StandardLibrary.Audio.Vocalization`. XML `<summary>` comment cites `FormantSynthesizer.cs:22-24` + `AudioCore.cs:16-32` for the API shape. No `[Collection("FlowScripts")]` attribute — pure-API call, does not touch Console.
- `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` — NEW: single `[Fact] GetFormants_UnknownVowel_ThrowsArgumentException`. `Assert.Equal` on the literal message. XML `<summary>` comment cites `FormantData.cs:69-76` and notes the 1-arg ArgumentException ctor choice vs 2-arg (why Assert.Equal holds).
- `flow-lang.Tests/Unit/Phase10/TtsHookTests.cs` — NEW: two `[Fact]` methods (round-trip + empty-command). Try/finally global-static restore in the round-trip Fact. `Assert.Contains` in the empty-command Fact. XML `<summary>` comment documents Pitfall 9 + the ctor-arity reasoning.
- `flow-lang.Tests/FlowScriptData.cs` — MODIFIED: appended `RequiredSentinels["test_vocalization.flow"]` entry with 4 empirical PASS sentinels. Dictionary key does NOT collide with any prior 13-* plan's entries.
- `.planning/REQUIREMENTS.md` — MODIFIED: TEST-04 row, traceability row, footer all updated (3 edits; single commit).
- `.planning/STATE.md` — MODIFIED: frontmatter (completed_phases 2→3, completed_plans 16→17, percent 94→100, stopped_at rewritten), Current Position block (Phase 13 → Phase 14, status rewritten, progress bar updated), Performance Metrics (Phase 13 P05 row added), Session Continuity (Stopped at rewritten, Resume file cleared).
- `.planning/ROADMAP.md` — MODIFIED: Phase 13 phase-list entry `[x]`, 13-05-PLAN.md row `[x]`, progress table row 4/5 In progress → 5/5 Complete.

## Decisions Made

- **Promote-not-waive for Phase 10 (D-16/D-17):** the existing draft's two Manual-Only items (formant audio quality: subjective, requires listening; TTS external command: requires espeak-ng installed) are legitimate verification modes, NOT coverage gaps. Preserved verbatim in the promoted file. All automatable requirements (88200 pin, unknown-vowel exception, TtsHook round-trip, empty-command validation) shipped as 4 pure-C# Unit Facts. Phase 13 criterion 2 satisfied via the promote path.
- **VOC-02 empty-command assertion shift — Assert.Equal → Assert.Contains:** Pass 1 drafted `Assert.Equal("TTS command cannot be null or whitespace", ex.Message)` based on the literal throw statement at `TtsHook.cs:19-20`. Pass 2 recognized that `TtsHook.SetCommand` uses the 2-arg `ArgumentException(message, paramName)` ctor with `nameof(command)`, which means the runtime-produced `Message` property appends `" (Parameter 'command')"`. Shipped with `Assert.Contains` to preserve semantic intent without brittleness. Third API-shape divergence documented in the 13-series (after 13-02's DX-02 Double format drift and 13-03's AudioCore.Mix signature). VOC-01 unknown-vowel assertion stays at `Assert.Equal` because `FormantData.GetFormants` uses the 1-arg ctor (no paramName suffix).
- **Pitfall 8 syllable sample-count non-linearity documented empirically:** The D-18 88200-sample pin applies ONLY to pure vowels. Empirical stdout from `test_vocalization.flow` showed `na frames: 28004` and `sa frames: 16097` for 0.5s @ 44.1kHz — the 15ms crossfade math at FormantSynthesizer.cs:108-147 produces non-linear totals (combined = consonantLength + vowelLength - crossfadeSamples). The Unit Fact intentionally restricts the pin to `sing("ah", ...)`; future syllable pinning must derive expected values from the combine math.
- **Phase-exit rollup bundled into Task 3 (no dedicated 13-06 closure plan):** Phase 13's closure is 3 small doc edits (REQUIREMENTS.md row flip + traceability + footer; STATE.md position advance + metrics; ROADMAP.md progress row), fitting within the final plan's atomic-commit budget. Contrast: Phase 11 had 11-06 and Phase 12 had 12-06 because those closures carried substantive VERIFICATION.md rollups. Phase 13 does not need a VERIFICATION.md (it authored 5 VALIDATION.md files; the aggregate verdict is the file count + the suite-green gate).
- **Global-static-restore pattern (Pitfall 9) shipped in TtsHookTests:** `try { TtsHook.SetCommand("echo"); assert; } finally { TtsHook.SetCommand(original); }`. Without this, Fact ordering could leak state across test runs. Mirrors Phase 12 Plan 03 ThunkTests test-hygiene ethos.

## Deviations from Plan

**None.** Plan executed exactly as written.

- No Rule 1/2/3 auto-fixes triggered (no bugs, no missing-critical-functionality, no blocking issues).
- No architectural checkpoints (Rule 4).
- Pass 1 → Pass 2 divergences are DOCUMENTED in 10-VALIDATION.md §Divergences (VOC-02 Assert.Equal → Assert.Contains shift is the substantive one), but these are expected outputs of the two-pass strict protocol, not deviations from the plan. The plan explicitly scopes `## Divergences` as the Pass 2 honest-documentation target per CONTEXT D-13/D-14/D-15.
- No Skip annotations; no Ultra-Important Findings; no Phase 16 deferrals; no Rule 2 test additions beyond the plan's 4 Facts.

## Issues Encountered

- **One API-shape recognition during Pass 2 (handled as documented divergence, not an issue):** `TtsHook.SetCommand` uses the 2-arg `ArgumentException(message, paramName)` ctor, so `Message` appends `" (Parameter 'command')"` at runtime. Shifted the Fact's assertion kind from the Pass 1 Assert.Equal draft to Assert.Contains. Documented in §Divergences. This is the 13-series's third API-shape divergence — a normal output of two-pass strict authorship, not an execution issue.

## User Setup Required

None — no external service configuration required. Phase 10 already exists; the tests invoke static in-process APIs only; no subprocess invocation (`RunTts` remains Manual-Only).

## Next Phase Readiness

- **Phase 13 closed: all 5 plans complete; all 5 VALIDATION.md files at nyquist_compliant: true; 16 observable-value pins; 13 new Facts/sentinel-tightenings across 13-01..13-05; 81/81 suite GREEN.**
- **Phase 14 (Composer DX Part 1) ready to start.** Requirements: DX-05 (slice), DX-06 (enharmonic helpers Db/Eb/H + enharmonic(Note) function), DX-08 (MIDI velocity end-to-end). Per STATE.md §Blockers/Concerns: identifier-collision grep required pre-landing for DX-06 (`H`, `Db`, `Eb`, `Fb`, `Cb`, `Bb`, `Gb`, `Ab`, `enharmonic`). DX-08 MIDI velocity propagation via NoteStreamCompiler may already be partially implemented — verification pass may reduce new-code work.
- Phase 13 ROADMAP criterion 1 (requirements-first authorship) satisfied via two-pass strict protocol across all 5 plans.
- Phase 13 ROADMAP criterion 2 (Phase 10 promote-or-waive) satisfied via the promote path.
- Phase 13 ROADMAP criterion 3 (per-phase observable-value pin) satisfied: Phase 6 VerboseFlag + SectionGain (2 pins), Phase 7 RepL + 3 sentinels (4 pins), Phase 8 Mix + SynthesizerFactory + 3 sentinels (5 pins), Phase 9 Tutorial + 3 sentinels (4 pins), Phase 10 Formant 88200 + unknown-vowel + TtsHook round-trip + empty + 4 sentinels (8 pins for Phase 10 alone, bringing aggregate to 23 pins — the plan's "16" count is conservative and counts each Phase's primary pin only; aggregate including Theory-row sentinels is higher).

### Threat Surface Scan

No new security-relevant surface introduced. Per plan §threat_model:

- `T-13-05-01` (Tampering via `TtsHook.SetCommand(string)` routing to process execution): **mitigated** — Facts mock via `SetCommand("echo")` which is a benign builtin. Facts do NOT invoke `RunTts` (no subprocess executed). Threat register entry holds at **severity: low**.
- `T-13-05-02` (Tampering via global `TtsHook._ttsCommand` static): **mitigated** — Tests restore original value in `finally` block (research Pitfall 9). Verified in TtsHookTests.cs:31-48: `try { SetCommand("echo"); ... } finally { SetCommand(original); }` pattern. Threat register entry holds at **severity: low**.

No network I/O, no file writes outside test-framework runtime directories, no new trust boundaries crossed.

### Known Stubs

None. All assertions pin concrete observable values:
- VOC-01: `buffer.Frames == 88200` (exact), `buffer.Channels == 1` (exact), `buffer.SampleRate == 44100` (exact)
- VOC-01 unknown-vowel: exact `ArgumentException` message equality
- VOC-02 round-trip: `GetCommand()` exact equality to `"echo"`
- VOC-02 empty-command: `Assert.Contains` substring match on the literal validation message (the ` (Parameter ...)` suffix is runtime-framework metadata, not user-facing)
- test_vocalization sentinels: 4 exact PASS-string substrings empirically captured

No placeholder text, no hardcoded empty values, no TODO/FIXME markers in authored files.

### Threat Flags

None. No new network endpoints, no new auth paths, no new file-system access patterns, no new schema at trust boundaries. Files created are test-only (`flow-lang.Tests/Unit/Phase10/*.cs`) invoking existing public APIs.

### Verification Evidence

- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase10"` → Failed: 0, Passed: 4, Total: 4 (FormantSynthesizer×1, FormantData×1, TtsHook×2)
- `dotnet test flow-sharp.sln` → Failed: 0, Passed: 81, Total: 81, Duration: 15s (baseline 77 + 4 new Facts; sentinel append is additive on existing Theory row)
- `grep -q "^nyquist_compliant: true$" .planning/phases/10-vocalization/10-VALIDATION.md` → match
- `grep -q "^status: passed$" .planning/phases/10-vocalization/10-VALIDATION.md` → match
- `grep -q "^promoted: 2026-04-19$" .planning/phases/10-vocalization/10-VALIDATION.md` → match
- `grep -q "^backfilled: true$" .planning/phases/10-vocalization/10-VALIDATION.md` → match
- `grep -q "Formant audio quality" .planning/phases/10-vocalization/10-VALIDATION.md` → match (Manual-Only preserved)
- `grep -q "espeak-ng integration" .planning/phases/10-vocalization/10-VALIDATION.md` → match (Manual-Only preserved)
- `grep -q "88200" .planning/phases/10-vocalization/10-VALIDATION.md` → match (D-18 pin documented)
- `grep "test_vocalization.flow" flow-lang.Tests/FlowScriptData.cs` → match
- `grep -c "Process.Start\|new Process\|RunTts" flow-lang.Tests/Unit/Phase10/TtsHookTests.cs` → 0 (no subprocess invocation)
- `grep "^\- \[x\] \*\*TEST-04" .planning/REQUIREMENTS.md` → match (TEST-04 Shipped)
- `grep "TEST-04 | Phase 13 | Shipped" .planning/REQUIREMENTS.md` → match
- `grep "^Phase: 14" .planning/STATE.md` → match
- `grep "5/5 | Complete" .planning/ROADMAP.md` → match
- Empirical stdout capture: `dotnet run --project flow-interpreter tests/test_vocalization.flow` → all 4 sentinel PASS strings present verbatim in stdout

## Self-Check

### Files Created/Modified (verification)

- `.planning/phases/10-vocalization/10-VALIDATION.md` — FOUND (at `nyquist_compliant: true`, `status: passed`, `wave_0_complete: true`, `backfilled: true`, `promoted: 2026-04-19`; Manual-Only rows preserved; 4 new sections appended)
- `flow-lang.Tests/Unit/Phase10/FormantSynthesizerTests.cs` — FOUND (`namespace FlowLang.Tests.Unit.Phase10`, single Fact, XML `<summary>` comment)
- `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` — FOUND (single Fact, Assert.Equal on literal message)
- `flow-lang.Tests/Unit/Phase10/TtsHookTests.cs` — FOUND (two Facts, try/finally restore in round-trip, Assert.Contains in empty-command)
- `flow-lang.Tests/FlowScriptData.cs` — FOUND (appended `RequiredSentinels["test_vocalization.flow"]` with 4 strings)
- `.planning/REQUIREMENTS.md` — FOUND (TEST-04 `[x]` Shipped 21e773d, traceability row updated, footer updated)
- `.planning/STATE.md` — FOUND (Phase 14, completed_phases 3, completed_plans 17, progress bar updated, metrics row appended)
- `.planning/ROADMAP.md` — FOUND (Phase 13 `[x]`, 13-05-PLAN.md `[x]`, progress table 5/5 Complete)

### Commits (verification)

- `331d059` docs(13-05): pass 1 — append VOC-01/VOC-02 invariants to 10-VALIDATION.md — FOUND
- `81f348c` test(13-05): pass 2 — author Phase 10 validation Facts + sentinel — FOUND
- `21e773d` docs(13-05): pass 2 — promote 10-VALIDATION.md to nyquist_compliant — FOUND
- `7d47ae8` docs(13-05): close TEST-04 — REQUIREMENTS.md + STATE.md + ROADMAP.md phase exit — FOUND

### Test Suite (verification)

- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase10"` → 4/4 pass (FormantSynthesizer×1, FormantData×1, TtsHook×2)
- `dotnet test flow-sharp.sln` → **81/81 GREEN**, 0 failed, 0 skipped (baseline was 77 at HEAD of 13-04; +4 new Facts)

## Self-Check: PASSED

---
*Phase: 13-nyquist-validation-backfill*
*Completed: 2026-04-20*
