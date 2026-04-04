---
phase: 06-diagnostics-bug-fixes
plan: 02
subsystem: type-system, interpreter, stdlib
tags: [overload-resolution, semitone, cent, section, bare-expression, error-reporting]

requires:
  - phase: 06-01
    provides: "verbose logging diagnostics for debugging"
provides:
  - "SemitoneType.IsCompatibleWith(IntType) for transpose(seq, 2) overload matching"
  - "CentType.IsCompatibleWith(DoubleType/FloatType) for cent-based overloads"
  - "vary() accessible from @std without @composition import"
  - "Section bare expression capture with _anon_ naming for non-silent rendering"
  - "Error masking test confirming non-existent functions produce exit code 1"
affects: [audio-rendering, transforms, song-rendering]

tech-stack:
  added: []
  patterns:
    - "IsCompatibleWith overrides on music types for widening (Semitone<-Int, Cent<-Double)"
    - "Bare expression capture pattern: track ExpressionStatement results during section body"

key-files:
  created:
    - tests/test_transpose_int.flow
    - tests/test_vary.flow
    - tests/test_section_bare_expr.flow
    - tests/test_error_masking.flow
  modified:
    - flow-lang/TypeSystem/SpecialTypes/SemitoneType.cs
    - flow-lang/TypeSystem/SpecialTypes/CentType.cs
    - flow-lang/std.flow
    - flow-lang/Interpreter/Interpreter.cs

key-decisions:
  - "IsCompatibleWith (not CanConvertTo) for Semitone<-Int -- compatible is higher scoring in overload resolution"
  - "CentType accepts both Float and Double for consistency"
  - "Bare expression sequences use _anon_N naming, deduplicated against named variables"
  - "FIX-03 already worked (exit code 1 on errors) -- added test to lock behavior"

patterns-established:
  - "Music type widening via IsCompatibleWith overrides in SpecialTypes"
  - "Section bare expression capture with _anon_ prefix auto-naming"

requirements-completed: [FIX-01, FIX-02, FIX-03]

duration: 6min
completed: 2026-04-04
---

# Phase 06 Plan 02: Bug Fixes Summary

**Fixed Sequence overload resolution (Semitone accepts Int, Cent accepts Double), section bare expression capture, and error reporting verification with 4 new integration tests**

## Performance

- **Duration:** 6 min
- **Started:** 2026-04-04T00:56:53Z
- **Completed:** 2026-04-04T01:03:21Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- transpose(sequence, 2) now works with plain Int argument via SemitoneType.IsCompatibleWith(IntType)
- vary() accessible from @std without requiring @composition import
- Bare note streams inside sections (e.g., `| C4 D4 E4 |`) now produce audio instead of silence
- Error masking behavior verified: non-existent functions produce error + exit code 1

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix Sequence overload resolution** - `e804de9` (fix)
2. **Task 2: Fix section bare expression capture and error masking** - `293ac6c` (fix)

## Files Created/Modified
- `flow-lang/TypeSystem/SpecialTypes/SemitoneType.cs` - Added IsCompatibleWith(IntType) override
- `flow-lang/TypeSystem/SpecialTypes/CentType.cs` - Added IsCompatibleWith(DoubleType/FloatType) override
- `flow-lang/std.flow` - Added 6 vary internal proc declarations
- `flow-lang/Interpreter/Interpreter.cs` - Bare expression capture in ExecuteSectionDeclaration
- `tests/test_transpose_int.flow` - Integration test for transpose with Int
- `tests/test_vary.flow` - Integration test for vary with @std only
- `tests/test_section_bare_expr.flow` - Integration test for bare expressions in sections
- `tests/test_error_masking.flow` - Integration test for error reporting on missing functions

## Decisions Made
- Used IsCompatibleWith (not CanConvertTo) for Semitone<-Int widening because compatible scores +500 in overload resolution vs +100 for convertible, making the match reliable
- CentType accepts both Float and Double for consistency with numeric widening expectations
- Bare expression sequences use `_anon_N` naming pattern, deduplicated against named variables to avoid double-counting
- FIX-03 (error masking) was already working correctly -- exit code 1 is returned on errors. Added test to lock the behavior and prevent regression.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test_vary.flow syntax (comma vs space in flow calls)**
- **Found during:** Task 1
- **Issue:** Plan template used commas in flow operator calls (`s -> vary(0.5, "pitch")`), but Flow uses space-separated args in parenthesized calls
- **Fix:** Changed multi-arg vary calls to parenthesized syntax: `(vary s 0.5 "pitch")`
- **Files modified:** tests/test_vary.flow
- **Verification:** Test passes with correct syntax
- **Committed in:** e804de9

**2. [Rule 1 - Bug] Fixed test_section_bare_expr.flow API usage**
- **Found during:** Task 2
- **Issue:** Plan template used getSections/sectionSequences incorrectly (wrong types, nonexistent length(Buffer)). Also used `writeWav` which is actually `exportWav`
- **Fix:** Simplified test to render song to buffer, export WAV, verify non-empty output (352KB WAV)
- **Files modified:** tests/test_section_bare_expr.flow
- **Verification:** Test passes, WAV file contains audio data
- **Committed in:** 293ac6c

---

**Total deviations:** 2 auto-fixed (2 bugs in test templates)
**Impact on plan:** Both auto-fixes were corrections to test file syntax/API usage. Core fixes implemented exactly as planned.

## Issues Encountered
None beyond the test template corrections documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three FIX items resolved with test coverage
- Type system widening pattern (IsCompatibleWith) established for future music type compatibility needs
- Section bare expression pattern ready for any future section-related features

---
*Phase: 06-diagnostics-bug-fixes*
*Completed: 2026-04-04*
