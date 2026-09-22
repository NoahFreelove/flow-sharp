# Phase 1 record: language contract and seams

Recorded 2026-09-22 on Linux (Ubuntu 26.04, SDK 10.0.112, runtime 10.0.12)
against `dev`. Restructuring roadmap Phase 1 (section 10) gate:

| Gate item | Evidence |
| --- | --- |
| Characteristic programs have expected outcomes | 69 contracts in [`contracts/language/`](../../../contracts/language/README.md) plus 7 non-musical examples in [`examples/language/`](../../../examples/language/README.md), run by `LanguageContractTests` in the core tier |
| Architecture rules specific enough to test | Six decisions under [`docs/decisions/2026-09-22-*`](../../decisions/), each with verification steps; dependency direction already enforced by `DependencyDirectionTests` |
| No accidental behavior change | Phase 1 itself changed no runtime code. The follow-up fixes changed behavior deliberately, one contract at a time, recorded in a decision. |
| Minimal host against current code | [`scripts/MinimalHost`](../../../scripts/MinimalHost/Program.cs) and [`minimal-host.json`](minimal-host.json) |

## Contract summary

Phase 1 recorded 69 contracts: 48 preserve, 3 generous, 7 disputed, 7 defect
and 4 gap. On the same day the owner asked for every finding to be fixed before
Phase 2. After the fixes there are **70 contracts: 67 preserve, 3 generous**,
none disputed, defect or gap. The resolutions are recorded in
[the semantic-fixes decision](../../decisions/2026-09-22-phase1-semantic-fixes.md).

Areas: calls/composition (8), procedures (10), functional behavior (14), data (11),
ergonomics (6), forgiving/strict semantics (8), modules (6), music (7). Deep
suites that already pin overload tiers, strict sites, qualified modules, thunk
caching and render determinism are referenced rather than duplicated.

## Findings and their resolution

| Finding (Phase 1 status) | Resolution |
| --- | --- |
| Lazy `and`/`or` operand returned a truthy thunk (defect) | Operands forced when reached; value returned |
| Stored thunks never forced (defect) | Deep forcing; memoized, failures reported once |
| `Lazy<Int>` declaration aborted (defect) | Lazy-to-Lazy conversion |
| Unknown identifier in arithmetic aborted (defect) | Builtin failures located; failed arguments skip the call (no cascade) |
| `(lt 1 "2")` aborted (defect) | Located error, execution continues |
| Arrays of tuples not storable (defect) | `Voids` accepts any array; `Tuple<<...>>[]` and bare `Tuple` types |
| Bare `(dict)` reduce seed aborted (defect) | Untyped dicts convert when entries fit |
| `print (str x)` silently dropped (disputed) | Statement-head paren-less calls accept parenthesized arguments |
| `(Nothing)` vs docs (disputed) | `(Nothing)` discards collected values, as documented |
| Int overflow wrapped (disputed) | Promotion to Long/Number; out-of-range Int binding is an error |
| Interpolation formatting differed (disputed) | One formatter for interpolation, `str`, `print` |
| Circular imports: errors vs docs (disputed) | Skipped with one-shot advisory, as documented |
| Strict proc widening vs docs (disputed) | Behavior kept (body strict, caller's arguments); docs corrected |
| Unit arithmetic dropped units (disputed) | Unit-preserving arithmetic; durations compare across ms/s |
| `(t ~> f)`, tuple patterns, `str` tuple/dict, single-Note `transpose` (gaps) | Implemented |
| No `mod`/`sort`/`split` (example gaps) | Added |
| CLI forced color on rich diagnostics | `ErrorReporter.ShouldUseColor` honors `NO_COLOR`, `TERM=dumb`, redirection |
| Two diagnostic lists; hosts dropped rich diagnostics | `ErrorCount` + `FormatAll`; CLI, REPL, flow-cli commands, `FlowEngineRunner` and WASM errors use both |
| Pure-Flow tests passed despite reported errors | `TestRunner` fails a body that reports errors |

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

Construction took about 94–100 ms and 2.0–2.3 MB allocated on this thread in a cold
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
