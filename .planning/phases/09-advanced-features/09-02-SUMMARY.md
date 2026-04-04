---
phase: 09-advanced-features
plan: 02
subsystem: docs
tags: [tutorial, examples, flow-script, onboarding]

requires:
  - phase: 01-core-language
    provides: loops, string interpolation
  - phase: 02-audio-features
    provides: audio rendering, effects, WAV export
  - phase: 04-composition
    provides: sections, songs, chord progressions
provides:
  - Interactive tutorial script at examples/tutorial.flow
  - Self-contained onboarding experience for new users
affects: []

tech-stack:
  added: []
  patterns: [tutorial-as-executable-script]

key-files:
  created:
    - examples/tutorial.flow
  modified: []

key-decisions:
  - "Used Note: comments instead of // since line comments not yet in lexer"
  - "Used end proc terminator for procs to avoid parser ambiguity"
  - "Used exportWav (actual registered name) not writeWav"
  - "Avoided bare identifier after -> when next line starts with ( to prevent greedy arg parsing"

patterns-established:
  - "Tutorial pattern: print explanatory text, then demonstrate with working code"

requirements-completed: [QOL-02]

duration: 5min
completed: 2026-04-04
---

# Phase 9 Plan 2: Interactive Tutorial Summary

**Self-contained 348-line tutorial script teaching Flow from variables through full song composition with WAV export**

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-04T02:25:33Z
- **Completed:** 2026-04-04T02:30:55Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Created progressive tutorial covering 10 topic areas: variables, arithmetic, procs, flow operator, collections, loops, note streams, musical context, sections/songs, effects, transforms
- Tutorial runs start-to-finish without errors and produces a WAV file
- Graduation piece composes a multi-section "Sunrise" piece with effects chain

## Task Commits

Each task was committed atomically:

1. **Task 1: Create interactive tutorial script** - `2e231f0` (feat)

## Files Created/Modified
- `examples/tutorial.flow` - Interactive tutorial script (348 lines, 112 print statements)

## Decisions Made
- Used `Note:` comments (not `//`) because the lexer's `SkipWhitespaceAndComments` only recognizes `Note:` prefix, not C-style line comments
- Used `end proc` terminator (not `}`) since procs use `end`-delimited bodies
- Used `exportWav` function name (the actual registered built-in) rather than `writeWav` mentioned in some docs
- Assigned flow operator results to intermediate variables when the next line starts with `(` to avoid the parser's greedy parenthesized-argument consumption

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed comment syntax from // to Note:**
- **Found during:** Task 1
- **Issue:** Plan specified using `//` line comments but the lexer only supports `Note:` prefix comments
- **Fix:** Replaced all `//` comments with `Note:` style
- **Files modified:** examples/tutorial.flow
- **Verification:** Script runs without parse errors

**2. [Rule 1 - Bug] Fixed proc declaration syntax**
- **Found during:** Task 1
- **Issue:** Used `proc Int name(Int n) { }` but parser expects `proc name(Int: n) ... end proc`
- **Fix:** Changed to correct `proc name(Type: param) ... end proc` syntax
- **Files modified:** examples/tutorial.flow
- **Verification:** Procs execute correctly

**3. [Rule 1 - Bug] Fixed Float type and mod function usage**
- **Found during:** Task 1
- **Issue:** Float literal `3.14` is Double; `mod` function doesn't exist
- **Fix:** Changed `Float` to `Double`, replaced `mod` example with `sub`
- **Files modified:** examples/tutorial.flow
- **Verification:** All arithmetic examples work

**4. [Rule 1 - Bug] Avoided flow operator greedy parsing**
- **Found during:** Task 1
- **Issue:** `x -> func` followed by `(print ...)` causes parser to consume print as function argument
- **Fix:** Used intermediate String variables or chained forms to avoid bare identifier -> before parenthesized expressions
- **Files modified:** examples/tutorial.flow
- **Verification:** Flow operator examples work correctly

---

**Total deviations:** 4 auto-fixed (4 Rule 1 bugs)
**Impact on plan:** All fixes were necessary for the script to run. No scope creep.

## Issues Encountered
None beyond the syntax differences documented as deviations.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all tutorial sections contain working demonstrations.

## Next Phase Readiness
- Tutorial ready for users to learn Flow
- Could be extended with advanced topics (MIDI export, custom oscillators) in future phases

## Self-Check: PASSED

---
*Phase: 09-advanced-features*
*Completed: 2026-04-04*
