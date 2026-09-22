# Flow without music

Tutorial programs that use only the general-purpose language: no tempo, tracks
or audio setup. Each `.flow` file has a matching `.out` file with its exact
output; `LanguageContractTests.ExampleRuns` checks them in the core test tier.

```bash
dotnet run --project flow-interpreter examples/language/collections.flow
```

| Program | Shows |
| --- | --- |
| `collections.flow` | Arrays, `filter`/`map`/`reduce`, `sort`, `mod`, `zip`, ranges. |
| `dict-aggregation.flow` | `split` input, then counting and totalling with `Dict` values folded by `reduce` from a bare `(dict)`. |
| `tuple-pipelines.flow` | Tuples as records through `->`, `~>` (also inside parentheses), `unpack` and destructuring. |
| `recursion-trees.flow` | Recursive tree traversal over nested tuples and arrays. |
| `lazy.flow` | Deferred work with `if`/`and`/`or`, inline `eval lazy (...)`, and a memoized stored thunk. |
| `pattern-dispatch.flow` | A command interpreter: `match` on symbols, literals, guards and tuple patterns over a `Tuple<<Symbol, Int>>[]` program. |
| `modules/report.flow` | A two-module utility with unqualified and `module.proc` calls. |

## History

The first versions of these programs (Phase 1) worked around language gaps:
no `mod`/`sort`/`split`, no tuple patterns in `match`, `~>` rejected inside
parentheses, arrays of tuples not storable in variables, a bare `(dict)` reduce
seed failing, and lazy `and` operands left unforced. All of these were fixed on
2026-09-22; see `docs/decisions/2026-09-22-phase1-semantic-fixes.md`.
