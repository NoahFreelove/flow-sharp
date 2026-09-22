# Phase 1 semantic fixes

Status: accepted and implemented (2026-09-22). The owner asked for the Phase 1
findings to be fixed before Phase 2 began.

The Phase 1 language contract pinned 7 defects, 7 disputed behaviors and 4 gaps.
Each is now resolved and its contract re-pinned as `preserve`. The disputed items
were settled by Flow's standing rules: ergonomics wins, charitable by default,
never a silently wrong result, and where the documentation states the intent,
follow the documentation.

## Evaluation errors never end the program

- **Builtin failures are located errors.** An exception inside a builtin is
  reported at its call (`lt: Cannot compare non-numeric type: String`), and the
  call evaluates to Void. An exception while executing a statement is reported at
  that statement. Neither reaches `FlowEngine`'s catch-all, so the location-less
  `0:0: error: Unexpected error` abort is gone. Control-flow signals (`break`,
  `continue`, test assertions, cancellation) still propagate.
  Contracts: `generous.mixed-comparison`, `generous.void-argument`.
- **No cascades.** When evaluating a call's arguments reports a new error and
  leaves a Void argument, the call is skipped. One mistake gives one diagnostic,
  not an extra "ambiguous overload on Void" or `[strict] ... got Void`
  (`generous.strict-file` went from 6 errors to 2). A declaration whose value
  cannot bind still declares the name with its type default, so later uses do not
  report "unknown identifier".
- **Pure-Flow tests fail when their body reports an error.** `TestRunner`
  compares `ErrorReporter.ErrorCount` before and after each body.

## Lazy values

- A lazy value is forced until a non-lazy value appears. A stored thunk passed
  to `eval`/`if`/`and`/`or` is therefore evaluated (previously it came back
  wrapped and was never run). Thunks memoize, so side effects run once and a
  failure is reported once and cached (`functional.lazy-variable`).
- `and`/`or` force a lazy operand when they reach it and return its value, not
  the thunk. `(and true lazy (false))` is `false` and no longer steers `if` into
  the wrong branch (`functional.lazy-operand`).
- `Lazy<T>` annotations accept lazy values (`functional.lazy-typed-declaration`).

## Data and conversions

- Integer `add`/`sub`/`mul`/`neg` promote on overflow (Int → Long → Number), as
  oversized literals already did. Narrowing a whole number that does not fit
  (binding to an `Int` variable) is an error naming the value, never a wrap
  (`data.int-overflow`).
- Any array fits a `Voids` slot; `Tuple<<...>>[]` annotations parse; bare `Tuple`
  is the any-arity tuple type (`data.tuple-array-variable`).
- A `Dict<Void, Void>` built from a bare `(dict)` converts to a typed `Dict<K, V>`
  when its entries fit (`data.empty-dict-argument`).
- `str` formats tuples and dicts like their literals (`data.str-collections`).

## Syntax and formatting

- **Paren-less calls at statement head accept parenthesized arguments.**
  `print (str x)` now prints; before, it silently did nothing. The extension
  applies only to the first call of an expression statement on the same line.
  Array literals (`[5 (neg 1)]`) and arguments inside other calls keep treating
  `name (expr)` as two values (`calls.optional-parens-nested-arg`).
- `(t ~> f)` parses inside parentheses (`calls.tuple-unpack-parenthesized`).
- `match` accepts tuple patterns `<<p1, p2>>`, with typed slots and guards
  (`functional.match-tuple-pattern`).
- **One formatting rule.** Interpolation, `str` and `print` format values
  identically: notes bare (`C4`, not `"C4"`), doubles to 10 significant digits
  (`print` used full precision), Void as `()` (interpolation printed `void`).
  Contract: `ergonomics.interpolation-formatting`.

## Procedures and modules

- **`(Nothing)`** discards the values collected so far. As the last statement it
  makes a proc return Void, as `wiki/Functions.md` always said
  (`procedures.nothing-builtin`).
- **Circular imports** are skipped with a one-shot `[module] circular import ...
  skipped` advisory instead of four errors. Each body still runs once, matching
  the documented idempotent import (`modules.circular-import`).
- **Strict procs called from charitable code** keep the existing behavior, and the
  documentation now describes it. Strict mode governs code written in the strict
  file: the proc body stays strict, while call arguments belong to the caller's
  code and follow the caller's rules (`modules.pragma-isolation`). This keeps
  strict an opt-in perimeter without making every call into a strict library
  fail on `3` versus `3.0`.

## Units and music

- **Unit-preserving arithmetic.** Same-unit `add`/`sub` keep the unit (`150ms`).
  Scaling by a number keeps it. Milliseconds and seconds mix in the first
  operand's unit, and dividing like units gives a ratio. `equals`/`lt`/… compare
  durations across ms and s (`(equals 1000ms 1s)` is true). Comparisons between a
  unit and a bare number keep their numeric meaning (`(equals 150ms 150.0)`).
  Contract: `music.unit-arithmetic`.
- `transpose` moves a single `Note` by a Semitone or Int (`music.note-transpose`).

## New standard-library functions

`mod` (floor modulo; the divisor's sign wins so `(mod -1 12)` is 11), `sort`
(stable, numbers/strings/notes) and `split` (empty separator splits into
characters). Contract: `data.sort-mod-split`. These are general-purpose and
belong to the future pure standard library.

## Hosts and tooling

- `ErrorReporter.ErrorCount` counts error-level reports across both diagnostic
  lists. `ErrorReporter.FormatAll` renders both, and `ErrorReporter.ShouldUseColor`
  is the single styling policy (interactive destination only, never with
  `NO_COLOR` or `TERM=dumb`).
- The interpreter CLI, REPL, `flow eval`/`check`/`test`/`doc`, `FlowEngineRunner`
  and the WASM `RunResult.errors` now include rich diagnostics (unknown
  identifiers, match exhaustiveness), which some of them previously dropped. The
  WASM result shape is unchanged. The committed playground AppBundle picks this up
  only after `flow-site/scripts/sync-runtime.sh` is rerun.

## Consequences

Programs that relied on the old behavior change: wrapped Int arithmetic, the
full-precision `print` of Doubles, the quoted Note in interpolation, errors from
circular imports, and passing pure-Flow tests whose body reported errors. Flow is
pre-traction (D-v1.5-01), so these ship without a migration path. The existing
suites (`tests/*.flow`, xUnit) needed no expectation changes.
