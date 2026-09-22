# Flow without music

Tutorial programs that use only the general-purpose language: no tempo, tracks
or audio setup. Each `.flow` file has a matching `.out` file with its exact
output; `LanguageContractTests.ExampleRuns` checks them in the core test tier.

```bash
dotnet run --project flow-interpreter examples/language/collections.flow
```

| Program | Shows |
| --- | --- |
| `collections.flow` | Arrays, `filter`/`map`/`reduce`, `zip`, ranges, a user-defined remainder. |
| `dict-aggregation.flow` | Counting and totalling with persistent `Dict` values folded by `reduce`. |
| `tuple-pipelines.flow` | Tuples as records through `->`, `~>`, `unpack` and destructuring. |
| `recursion-trees.flow` | Recursive tree traversal over nested tuples and arrays. |
| `lazy.flow` | Deferred branches with `if`/`or` and inline `eval lazy (...)`. |
| `pattern-dispatch.flow` | A command interpreter using `match` on symbols, literals and guards. |
| `modules/report.flow` | A two-module utility with unqualified and `module.proc` calls. |

## Gaps these programs work around

Recorded rather than silently fixed. Each links to its pinned contract under
`contracts/language/` when one exists.

- No modulo, sorting or string splitting builtins. `collections.flow` builds a remainder from `idiv`.
- An array of tuples cannot be stored in a variable (`data.tuple-array-variable`). `pattern-dispatch.flow` returns it from a proc.
- A bare `(dict)` reduce seed fails with a typed reducer (`data.empty-dict-argument`). `dict-aggregation.flow` seeds from a typed variable.
- `~>` is rejected inside parentheses (`calls.tuple-unpack-parenthesized`). Results are bound first.
- A lazy second operand to `and`/`or` is not forced (`functional.lazy-operand`). `lazy.flow` guards with nested `if`s.
- A thunk stored in a variable cannot be forced, so memoized values and cached failures are not observable from Flow (`functional.lazy-variable`).
- `match` has no tuple patterns (`functional.match-tuple-pattern`). Commands are unpacked with `@`.
- `str` does not format a bare tuple or dict (`data.str-collections`).
