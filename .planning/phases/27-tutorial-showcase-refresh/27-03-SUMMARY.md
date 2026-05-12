---
phase: 27-tutorial-showcase-refresh
plan: 03
subsystem: tutorial-graduation-and-showcase-and-pragma-companions
tags: [tutorial-graduation-song, showcase-replace, pragma-companions, polyrhythmic-minimal, just-intonation, h-as-b, byte-identical]
requires: [27-02]
provides: [tutorial-graduation-v1.3-refactor, showcase-v1.3-replacement, pragma-companions]
affects: [examples/tutorial.flow, examples/showcase.flow, examples/pragmas/h_alias.flow, examples/pragmas/microtonal_ji.flow, .gitignore]
tech-stack:
  added: []
  patterns: [graduation-song-pattern, path-string-inline-writeWav, fixed-seed-byte-identical]
key-files:
  created:
    - examples/pragmas/h_alias.flow
    - examples/pragmas/microtonal_ji.flow
  modified:
    - examples/tutorial.flow
    - examples/showcase.flow
    - .gitignore
key-decisions:
  - "Tutorial graduation effects chain rewritten: rawMix -> filtered (lowpass 1.2kHz) -> delayed (delay 250ms 0.5 0.4) -> reverbed (reverb 0.5 1.8s) -> finalMix (volume reverbed 0.85). Replaces the legacy reverb 0.25 -> lowpass 4000.0 -> gain negTwo chain."
  - "Showcase fully replaced (not edited) with v1.3 polyrhythmic-minimal piece. Same fixed seeds (euclidean 7, humanizeGaussian 314) so byte-identical sentinels stay green."
  - ".gitignore exception added for examples/pragmas/**/*.flow because the global *.flow ignore was hiding the new companion files (mirrors the pre-existing vscode-extension/tests exception)."
  - "Tutorial.flow stays 12-TET — no top-level 'enable' pragma. Sub-section B 'Note:' prose mentioning the pragma names is comment-only and does not activate them."
requirements-completed: [QOL-04]
duration: ~15 min
completed: 2026-05-10
---

# Phase 27 Plan 03: Tutorial Graduation + Showcase Replacement + Pragma Companions Summary

Refactored the tutorial graduation song with Phase 26.2 audible features (D-103), replaced examples/showcase.flow with the v1.3 polyrhythmic-minimal piece (D-201/D-202), and created the two pragma companion files under examples/pragmas/ (D-401/D-402).

## What was built

| File | Status | Content |
|------|--------|---------|
| examples/tutorial.flow | Modified | Graduation effects chain rewritten with 4 audible D-103 Phase 26.2 features (1.2kHz Hertz, 250ms Ms delay, 1.8s Second reverb, volume 0.85 linear). Existing per-section gain blocks (0.6, 1.0) and reverbTime 2.5 preserved. |
| examples/showcase.flow | Replaced | v1.3 polyrhythmic-minimal piece — Dict<Symbol, Note> drum kit, {3:2 ...}q tuplet groove, fixed-seed euclidean kick + humanizeGaussian melody, full Phase 26.2 chain (1.2kHz / 250ms / 1.8s / volume 0.7). 60 lines. |
| examples/pragmas/h_alias.flow | Created | 38 lines. `enable hAsB;` at line 1, H/B alias demo + Song/Buffer/render/writeWav/writeMidi inside tempo+timesig+key block. |
| examples/pragmas/microtonal_ji.flow | Created | 42 lines. `enable justIntonation;` at line 1, 5/4 vs 12-TET ratio prose + Cmaj triad render inside tempo+timesig+key block. |
| .gitignore | Modified | Added `!examples/pragmas/**/*.flow` exception so the new companion files track despite the global *.flow ignore. |

## Verification

```
$ for f in examples/tutorial.flow examples/showcase.flow examples/pragmas/h_alias.flow examples/pragmas/microtonal_ji.flow; do
    dotnet run --project flow-interpreter "$f" > /dev/null 2>&1 && echo "PASS: $f"
  done
PASS: examples/tutorial.flow
PASS: examples/showcase.flow
PASS: examples/pragmas/h_alias.flow
PASS: examples/pragmas/microtonal_ji.flow

$ for out in flow_tutorial flow_showcase h_alias microtonal_ji; do
    [ -s "examples/output/$out.wav" ] && [ -s "examples/output/$out.mid" ] && echo "OK: $out"
  done
OK: flow_tutorial / flow_showcase / h_alias / microtonal_ji  (all four pairs)

$ dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcase|FullyQualifiedName~Phase18.ByteIdenticalTutorial|FullyQualifiedName~Phase25.ByteIdenticalShowcaseGaussian"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

## Deviations from Plan

**[Rule 1 — Bug] Plan's 'no enable pragma' grep matched 'Note:' comment prose** (Task 1) — Found during: Task 1 verify | Issue: Plan grep `! grep -q "enable justIntonation\|enable hAsB\|enable pythagorean\|enable scaleLint" examples/tutorial.flow` returns false because Wave 2 sub-section B inserted `Note: enable justIntonation; ...` etc. as documentation prose. Those are Flow `Note:` comments — they do NOT activate the pragma at runtime. | Fix: Verified intent semantically with `grep -E "^enable " examples/tutorial.flow` (no match → no top-level active pragma) and confirmed Phase 18 ByteIdenticalTutorialTests stays green (file remains 12-TET). The plan's regex was overly broad. | Files modified: none (intent met without code change). | Verification: 2/2 ByteIdenticalTutorial tests passing. | Commit hash: c46bb22 (commit message documents the deviation).

**[Rule 1 — Bug] Pragma companion files blocked by `*.flow` global gitignore** (Task 3) — Found during: Task 3 first commit | Issue: `.gitignore` line 8 is `*.flow` which silently blocks `git add` for any new .flow file outside an existing exception. The pre-existing tracked .flow files (tutorial.flow, showcase.flow) are exempt because they were committed before the rule was added. | Fix: Added a 4-line exception block to .gitignore (`!examples/pragmas/`, `!examples/pragmas/**`, `!examples/pragmas/**/*.flow`) mirroring the existing vscode-extension/tests/ exception above it. | Files modified: .gitignore. | Verification: `git check-ignore -v` confirms the negation pattern matches; both pragma files staged + committed. | Commit hash: covered in Task 3 commit.

**Total deviations:** 2 auto-fixed (2 × Rule 1 bug). **Impact:** none — both deviations resolve cleanly without changing the runtime intent of any file.

## Issues Encountered

None remaining.

## Self-Check: PASSED

- All 3 tasks executed and committed atomically.
- All 4 scripts (tutorial / showcase / h_alias / microtonal_ji) exit 0 with non-empty WAV + MIDI artifacts.
- All 6 byte-identical regression sentinels (Phase 18 ByteIdenticalTutorialTests × 2 + Phase 18 ByteIdenticalShowcase × 2 + Phase 25 ByteIdenticalShowcaseGaussian × 2) passing.
- Both companion files keep Song / Buffer / renderSong / writeWav / writeMidi INSIDE the tempo+timesig+key musical-context block (B7 fix; tutorial graduation pattern).
- Path-string-inline writeWav + writeMidi contract held across all 4 scripts (Pitfall 7).
- No active 'enable' pragma in tutorial.flow (12-TET preserved).
- Ready for Wave 4 (Phase27ByteIdenticalPragmaTests xUnit class).
