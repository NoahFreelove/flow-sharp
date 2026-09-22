# Phase 1 record: language contract and seams

Recorded 2026-09-22 on Linux (Ubuntu 26.04, SDK 10.0.112, runtime 10.0.12)
against `dev`. Restructuring roadmap Phase 1 (section 10) gate:

| Gate item | Evidence |
| --- | --- |
| Characteristic programs have expected outcomes | 69 contracts in [`contracts/language/`](../../../contracts/language/README.md) plus 7 non-musical examples in [`examples/language/`](../../../examples/language/README.md), run by `LanguageContractTests` in the core tier |
| Architecture rules specific enough to test | Six decisions under [`docs/decisions/2026-09-22-*`](../../decisions/), each with verification steps; dependency direction already enforced by `DependencyDirectionTests` |
| No accidental behavior change | Phase 1 changed no runtime code. Contracts pin current behavior, including defects. |
| Minimal host against current code | [`scripts/MinimalHost`](../../../scripts/MinimalHost/Program.cs) and [`minimal-host.json`](minimal-host.json) |

## Contract summary

| Status | Count | Meaning |
| --- | ---: | --- |
| preserve | 48 | Characteristic behavior |
| generous | 3 | Intentional forgiving behavior |
| disputed | 7 | Inconsistent or contradicts documentation; needs a decision |
| defect | 7 | Wrong behavior pinned so fixes are deliberate |
| gap | 4 | Unsupported forms with their diagnostics |

Areas: calls/composition (8), procedures (10), functional behavior (14), data (10),
ergonomics (6), forgiving/strict semantics (8), modules (6), music (7). Deep
suites that already pin overload tiers, strict sites, qualified modules, thunk
caching and render determinism are referenced rather than duplicated.

## Findings for owner review

Phase 1 records these; it does not fix them. Each needs a decision or a
deliberate fix commit that updates its contract.

### Defects

| Contract | Finding |
| --- | --- |
| `functional.lazy-operand` | `(and true lazy (false))` returns the unforced thunk, which is truthy, so `if` takes the wrong branch silently. |
| `functional.lazy-variable` | A thunk held in a variable is never forced: `eval`/`if` wrap the variable in a second thunk (the sweep-0614 lazy-slot deferral). Side effects never run, failures never report. Cached-failure semantics are unobservable from Flow. |
| `functional.lazy-typed-declaration` | Documented `Lazy<Int>` annotations abort the program with a `0:0` internal error. |
| `generous.void-argument` | An unknown identifier passed to `add` is reported, then aborts the program with a `0:0` conversion error. |
| `generous.mixed-comparison` | `(lt 1 "2")` aborts the program with a `0:0` internal error. |
| `data.tuple-array-variable` | An array of tuples cannot be stored in a `Voids` variable (abort), and `Tuple<<...>>[]` does not parse. |
| `data.empty-dict-argument` | A bare `(dict)` reduce seed aborts when the reducer's parameter is typed. |

Four defects share one mechanism: an internal exception escapes evaluation and is
reported as `0:0: error: Unexpected error`, ending the program instead of
accumulating a located diagnostic.

### Disputed behavior

| Contract | Question |
| --- | --- |
| `calls.optional-parens-nested-arg` | `print (str x)` silently does nothing. Should it parse or be reported? |
| `procedures.nothing-builtin` | `(Nothing)` does not force Void as `wiki/Functions.md` states. Fix the docs or the builtin? |
| `data.int-overflow` | Int arithmetic wraps while literals promote. |
| `ergonomics.interpolation-formatting` | Interpolation quotes Notes and prints Void as `void`; `str` prints `C4` and `()`. |
| `modules.circular-import` | Cycles report four errors but still run every body; the wiki says silent no-op. |
| `modules.pragma-isolation` | A strict module's proc widens Int arguments when called from charitable code; the wiki says it "stays strict". |
| `music.unit-arithmetic` | `(add 100ms 50ms)` prints `150` (unit dropped); `(equals 1000ms 1s)` is false. |

### Outside the contract

- **CLI ignores `NO_COLOR` for rich diagnostics.** `flow-interpreter/Program.cs`
  calls `FormatDiagnostics(..., useColor: true)` unconditionally, so ANSI codes
  reach redirected stderr and `NO_COLOR=1` sessions. Legacy single-line errors are
  uncolored, so one run mixes both styles.
- **Two diagnostic lists.** `ErrorReporter.Errors` and `ErrorReporter.Diagnostics`
  accumulate separately. `FlowEngineRunner` (and therefore `FlowScriptTests`)
  counts and prints only the first, so an unknown-identifier or match-exhaustiveness
  error in a `tests/*.flow` script would not fail its test. A full CLI scan found
  none of the 152 tracked scripts emits such a diagnostic today, so no failure is
  currently masked. `LanguageContractTests` counts both lists.
- **Cascading diagnostics.** A failed call yields Void, and the enclosing
  `str`/`print` then reports its own ambiguity error (see `generous.strict-file`,
  where 2 source mistakes produce 6 errors).
- **Standard library gaps met by the examples:** no modulo, sorting or string
  splitting; `str` has no Tuple/Dict overload; `match` lacks tuple patterns; `~>`
  is rejected inside parentheses.

## Minimal host

`scripts/MinimalHost` references only `flow-lang`. It runs a dictionary/collection
program with no music and records what the engine initializes. Reproduce with:

```bash
dotnet run --project scripts/MinimalHost -c Release -- --json docs/baselines/phase1/minimal-host.json
```

The program runs correctly. Remaining direct dependencies a language-only host
cannot avoid today:

| Dependency | Evidence (`minimal-host.json`) | Removal seam |
| --- | --- | --- |
| Static closure of `flow-lang.dll` | References DryWetMidi, NAudio.Core, NAudio.Wasapi, Rug.Osc, Tomlyn. They are not loaded for this program, but must ship with it. | Assembly split (Phase 3) |
| Process statics | `FlowEngine.CurrentSampleCache`, `CurrentSfzSampleCache`, `CurrentExecutionContext` all published by construction | Session-owned services (Phase 2) |
| Eager module loading | `std.flow`, `collections.flow`, `bars.flow`, `improv.flow` loaded, 3 improv style packs registered | `language` profile (Phase 3) |
| Builtin registration | About 600 signatures registered. Implementation namespaces are mostly musical (`Audio*`, `Harmony`, `Transforms`, `Patterns`, `Midi`, `Network`, `Notation`, `Composition`, `Improv`); `FlowLang.StandardLibrary` itself mixes general and musical helpers | Descriptor-driven module catalog (Phase 3) |
| Output capture | Host must redirect process-global `Console` streams | Output sink (Phase 2) |
| Loaded-module and implementation lists | Only reachable through private fields (the probe uses reflection) | Public catalog/metadata APIs (Phase 3–4) |
| Native libraries | None loaded for this program. PulseAudio/rtmidi/JACK load only on use. | Platform adapters (keep lazy) |

Construction took about 94–98 ms and 1.97 MB allocated on this thread in a cold
process: indicative only, not a benchmark. See the Phase 0 performance baseline for
measured numbers.

## Dependency direction baseline

[`language-dependency-edges.json`](language-dependency-edges.json): 38 edges from 19
language-core types into forbidden namespaces. Most go to
`TypeSystem.SpecialTypes` (18), then `StandardLibrary.Audio.*` (11),
`StandardLibrary.Harmony` (6), and one each to `Midi`, `Network` and `FlowLang.Audio`.
Largest sources: `Runtime.Value` (6), `Runtime.ExecutionContext` (4),
`Interpreter.ExpressionEvaluator` (3), `Interpreter.Interpreter` (3). The lexer and
parser also appear, which the grammar-policy decision targets.

## Verification

See the progress ledger for the commands and results of the Phase 1 verification
run.
