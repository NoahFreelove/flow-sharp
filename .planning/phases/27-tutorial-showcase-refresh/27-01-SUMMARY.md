---
phase: 27-tutorial-showcase-refresh
plan: 01
subsystem: tutorial
tags: [tutorial, language-features, weaves, symbols, tuples, dict, prefix-arithmetic, hertz, ms-fx, gain-volume, second-decay-reverb]
requires: []
provides: [tutorial-v1.3-language-feature-weaves]
affects: [examples/tutorial.flow]
tech-stack:
  added: []
  patterns: [chapter-divider-S4, demo-body-S5]
key-files:
  created: []
  modified:
    - examples/tutorial.flow
key-decisions:
  - "Renamed Dict<Symbol, Int> 'doubled' → 'dictDoubled' in chapter 4.6 to avoid collision with chapter 4 Int[] 'doubled' (Flow scope flattens; pre-existing collision discovered at runtime)."
requirements-completed: [QOL-04]
duration: ~10 min
completed: 2026-05-10
---

# Phase 27 Plan 01: Tutorial v1.3 Language-Feature Weaves Summary

Wove Phase 26.1/26.2 language surface (Symbols, Tuples + ~>, Dict, prefix-only arithmetic, Hertz literals, Ms-typed FX, gain vs volume, Second-decay reverb) into existing tutorial.flow chapters via four new half-numbered chapters (1.5, 4.5, 4.6, 9.5) plus inline weaves into chapters 2, 9, and 16.

## What was built

| Chapter | Status | Content |
|---------|--------|---------|
| 1.5 (NEW) | Inserted | Symbol literals `#foo`, interning identity, strict-separation-from-String |
| 2 | Modified | Replaced legacy `Operator style: 10 + 25` print with explicit no-infix prefix-only prose teaching `(idiv)` / `(neg)` / `(concat)` |
| 4.5 (NEW) | Inserted | Tuple literal, indexing `tup@N`, destructuring, `~>` parse-time + non-tuple fallthrough, `(unpack)` runtime |
| 4.6 (NEW) | Inserted | Dict<K, V> with both constructors + 14-op surface (12 ops shown) |
| 9 | Modified | `(lowpass dry 1.2kHz)` Hertz literal + `(delay 250ms 0.5 0.4)` Ms literal; Phase 26.2 ergonomics prose |
| 9.5 (NEW) | Inserted | gain (dB) vs volume (linear) with FOOTGUN call-out + clipping/negative-rejection notes |
| 16 | Appended | `(reverb buf 0.5 1.8s)` Second-decay reverb form distinguished from per-section reverbTime block |

## Line counts

- Before: 684 lines
- After: 830 lines (+146 lines)
- Three commits: fc061ed (1.5 + ch2), dd39ec6 (4.5 + 4.6), 62b510f (ch9 + 9.5 + ch16)

## Verification

```
$ dotnet run --project flow-interpreter examples/tutorial.flow
EXIT=0
$ ls examples/output/flow_tutorial.{wav,mid}
flow_tutorial.wav (5503724 bytes), flow_tutorial.mid (non-empty)
$ dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorial"
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

All 4 plan-level grep checks pass (4 chapter additions present, 5 hits on Hz/Ms/s literals, no `3.0kHz` escape hatch, no legacy `Operator style: 10 + 25` print).

## Deviations from Plan

**[Rule 1 - Bug] Local `doubled` variable collision in chapter 4.6** — Found during: Task 2 | Issue: Tutorial.flow chapter 4 already declares `Int[] doubled` at line 137; my Dict<Symbol, Int> demo line `Dict<Symbol, Int> doubled = (map velocities ...)` triggered a runtime "Variable 'doubled' already declared in this scope" error (Flow scope flattens; user-level rule: tutorial reuses simple names across chapters but cannot redeclare the same name). | Fix: Renamed the Dict variable to `dictDoubled`; both `Dict<Symbol, Int> dictDoubled = ...` and the print statement use the new name. | Files modified: examples/tutorial.flow (chapter 4.6 only). | Verification: tutorial re-ran clean to exit 0; grep audit unchanged (the renamed var is still Dict<Symbol, Int>). | Commit hash: dd39ec6.

**Total deviations:** 1 auto-fixed (Rule 1 - bug). **Impact:** none — name change is local to chapter 4.6, all surface contracts preserved.

## Issues Encountered

None.

## Self-Check: PASSED

- All 3 tasks executed and committed atomically.
- Tutorial.flow exits 0 with non-empty WAV (5.5 MB) + MIDI artifacts.
- Phase 18 ByteIdenticalTutorialTests: 2/2 passed (deterministic two-run gate).
- All 4 plan-level grep audits pass.
- All 3 task-level `<verify>` grep gauntlets pass.
- Ready for Wave 2 (chapter 19.5 v1.3 Music Capabilities mega-chapter).
