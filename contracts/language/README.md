# Flow language contract

Executable record of how Flow behaves today, including how features interact.
Each contract is a short program plus its expected output, diagnostics, a
rationale and a compatibility status. `LanguageContractTests` runs every one
(core test tier). A refactor that changes any result fails the build: record the
new behavior as a deliberate decision and update the contract with it.

Written for restructuring roadmap Phase 1 (section 2). The contract describes
semantics, not implementation history. Recorded on 2026-09-22 against `dev`;
the defects, disputed behaviors and gaps found then were resolved the same day
(`docs/decisions/2026-09-22-phase1-semantic-fixes.md`).

## Format

```flow
// contract: <area>.<name>        unique id; <area> is the directory name
// status: preserve|generous|disputed|defect|gap
// rationale: why this behavior matters or what is wrong with it; indented
//    continuation lines extend the rationale
// errors: N                      evaluation/parse error count (default 0)
// stderr: text                   repeatable; must appear in normalized stderr
use "@std"
...
```

- Expected stdout is the sibling `<name>.out`, compared exactly apart from line
  endings and trailing newlines.
- stderr is normalized before matching: ANSI styling removed, absolute repository
  paths made relative.
- `.flow` files without a `contract:` header (for example `modules/lib/`) are helper
  modules imported by contracts.
- Each contract runs in a fresh `FlowEngine` with advisory deduplication reset.

## Status vocabulary

| Status | Meaning | Changing it requires |
| --- | --- | --- |
| `preserve` | Characteristic behavior of the language. | A language decision recorded under `docs/decisions/`. |
| `generous` | Intentional forgiving behavior (clamps, fallbacks, truthiness). | A decision; strict-mode alternatives preferred over removal. |
| `disputed` | Current behavior pinned, but it looks inconsistent or contradicts documentation. | A decision on the intended semantics. Until then, keep it. |
| `defect` | Current behavior is wrong (crash, lost control flow, silent no-op). Pinned so a fix is visible. | A fix that updates the contract in the same change. |
| `gap` | Unsupported form; the expected diagnostic is pinned. | Adding the feature, which updates the contract. |

Do not fix a `defect` or resolve a `disputed` entry as a side effect of
restructuring. Keep semantic changes out of extraction commits.

## Index

### Calls and composition

| Contract | Status | Behavior |
| --- | --- | --- |
| `calls.prefix-arithmetic` | preserve | Prefix arithmetic, `div` promotion, negative literal arguments. |
| `calls.infix-rejected` | preserve | Infix `+` is a parse error that suggests the prefix builtin. |
| `calls.optional-parens` | preserve | Statement-position calls with simple arguments omit parentheses. |
| `calls.optional-parens-nested-arg` | preserve | At statement head, `print (str x)` calls with a parenthesized argument. |
| `calls.flow-operator` | preserve | `->` chains builtins, procs and lambdas; equals nested calls. |
| `calls.flow-as-binding` | preserve | `-> f as NAME` binds an intermediate result. |
| `calls.tuple-unpack-flow` | preserve | `~>` spreads tuples, falls through to `->`; `unpack` call form. |
| `calls.tuple-unpack-parenthesized` | preserve | `(t ~> f)` works inside parentheses. |

### Procedures

| Contract | Status | Behavior |
| --- | --- | --- |
| `procedures.implicit-return` | preserve | Zero, one or several collected values give Void, the value, or an array. |
| `procedures.explicit-return` | preserve | `return X` discards collected values and short-circuits. |
| `procedures.trailing-void` | preserve | A trailing void call keeps an earlier collected value. |
| `procedures.nothing-builtin` | preserve | A final `(Nothing)` discards collected values and returns Void. |
| `procedures.bare-return` | preserve | Bare `return` is a parse error. |
| `procedures.recursion` | preserve | Recursive procs with lazily deferred base cases. |
| `procedures.overloads` | preserve | User overloads by type; Int widens to a Double parameter. |
| `procedures.named-args` | preserve | Named arguments in any order after positionals; unknown names reported. |
| `procedures.named-args-order` | preserve | Positional-after-named and duplicate names are parse errors. |
| `procedures.varargs` | preserve | `Type...` varargs versus plural array parameters. |

### Functional behavior

| Contract | Status | Behavior |
| --- | --- | --- |
| `functional.lambda-capture` | preserve | Snapshot capture, lambdas returning lambdas, function types. |
| `functional.multi-statement-lambda` | preserve | Parenthesized statement-block lambda bodies. |
| `functional.proc-lexical-scope` | preserve | Procs see globals, never caller locals. |
| `functional.higher-order` | preserve | Functions as arguments to builtins and procs. |
| `functional.short-circuit` | preserve | `if` evaluates only the taken branch; first-operand `and`/`or` short-circuit. |
| `functional.lazy-inline` | preserve | `(eval lazy (...))` forces an inline thunk. |
| `functional.lazy-variable` | preserve | Stored thunks are forced by `eval`/`if`/`and`/`or`, memoized; failures reported once. |
| `functional.lazy-typed-declaration` | preserve | `Lazy<Int>` annotations accept lazy values. |
| `functional.lazy-operand` | preserve | `and`/`or` force lazy operands when reached and return their values. |
| `functional.match-basics` | preserve | Literal, symbol, guard and wildcard arms. |
| `functional.match-non-exhaustive` | generous | Fall-through warns and yields Void. |
| `functional.match-exhaustive-pragma` | preserve | `enable matchExhaustive;` makes fall-through an error. |
| `functional.match-tuple-pattern` | preserve | Tuple patterns `<<p1, p2>>` with typed slots and guards. |
| `functional.loops` | preserve | `for`/`while` with gated `(break)`/`(continue)`. |

### Data

| Contract | Status | Behavior |
| --- | --- | --- |
| `data.numeric-widening` | preserve | Widening chain; large literals become Long/BigInteger. |
| `data.int-overflow` | preserve | Integer arithmetic promotes on overflow; out-of-range Int binding is an error. |
| `data.equality` | preserve | `equals`/`sequals`, structural arrays and tuples, symbols are not strings. |
| `data.arrays` | preserve | `@` indexing, negative indices via expressions, literal separators. |
| `data.tuples` | preserve | Indexing, destructuring, empty and singleton tuples. |
| `data.dicts` | preserve | Insertion order, persistent updates, right-biased merge. |
| `data.conversions` | preserve | `double`/`int`/`long`/`float` accept every numeric type, including their own. |
| `data.sort-mod-split` | preserve | Floor `mod`, stable `sort`, `split`; located errors on misuse. |
| `data.str-collections` | preserve | `str` formats arrays, tuples and dicts like literals. |
| `data.tuple-array-variable` | preserve | `Tuple<<...>>[]`, `Voids` and bare `Tuple` hold tuple values. |
| `data.empty-dict-argument` | preserve | A bare `(dict)` works as a typed reduce seed. |
| `data.void-variable` | preserve | A `Void` variable takes the type of its initial value. |
| `data.defaults` | preserve | Uninitialized declarations take type defaults. |

### Ergonomics

| Contract | Status | Behavior |
| --- | --- | --- |
| `ergonomics.comments` | preserve | Five comment forms and their positions. |
| `ergonomics.separators` | preserve | Semicolons and backslash line continuation. |
| `ergonomics.interpolation` | preserve | `$"..."` expressions, pipes and brace escapes. |
| `ergonomics.interpolation-formatting` | preserve | Interpolation, `str` and `print` share one formatting rule. |
| `ergonomics.plural-types` | preserve | `Ints` means `Int[]`. |
| `ergonomics.diagnostic-span` | preserve | Location, excerpt and did-you-mean for unknown identifiers. |

### Forgiving and strict semantics

| Contract | Status | Behavior |
| --- | --- | --- |
| `generous.error-accumulation` | preserve | Errors are collected; later statements still run. |
| `generous.void-argument` | preserve | A failed argument is reported once; enclosing calls are skipped, no cascade. |
| `generous.clamp-advisory` | generous | Out-of-range input clamps with a one-shot advisory. |
| `generous.truthy-if` | generous | Non-Bool conditions use truthiness outside strict mode. |
| `generous.mixed-comparison` | preserve | Builtin failures such as `(lt 1 "2")` are located errors; execution continues. |
| `generous.strict-file` | preserve | `enable strict;` removes widening and truthiness in its file. |
| `generous.unknown-pragma` | preserve | Unknown pragma is an error with a suggestion. |
| `generous.quoted-strings` | preserve | Quoted text is never a music literal. |

### Modules

| Contract | Status | Behavior |
| --- | --- | --- |
| `modules.relative-import` | preserve | Relative imports run in the caller's scope; exports are unqualified. |
| `modules.import-idempotent` | preserve | A module body runs once per engine. |
| `modules.qualified-access` | preserve | `module NAME` enables `NAME.fn`; last-import-wins with shadow advisory. |
| `modules.qualified-errors` | preserve | Distinct diagnostics for unknown modules and missing procs. |
| `modules.circular-import` | preserve | Cycles are skipped with a one-shot advisory; each body runs once. |
| `modules.pragma-isolation` | preserve | Strict governs code in the strict file: its proc bodies stay strict, callers' arguments follow the caller. |
| `modules.core-library` | preserve | `@core` holds the essential general-purpose builtins (lists via `@collections`); `@std` imports it and adds the music-typed surface. |
| `modules.no-prelude` | preserve | No implicit prelude: without an import even `print` is undeclared; interactive hosts import `@std` themselves. |

### Music

| Contract | Status | Behavior |
| --- | --- | --- |
| `music.note-stream` | preserve | Streams compile to Sequences; transforms chain; `inspect` output. |
| `music.octave-context` | preserve | `octave N { }` default octave; explicit octave wins. |
| `music.units` | preserve | Unit literal formatting (10 significant digits, like doubles); kHz canonicalizes to Hz. |
| `music.unit-arithmetic` | preserve | Unit-preserving arithmetic; unit × unit is a plain number; durations compare across ms and s. |
| `music.audio-arguments` | preserve | Whole-number delay times are ms; empty-buffer default prevents cascades. |
| `music.harmony` | preserve | Chord literals and roman-numeral resolution. |
| `music.note-transpose` | preserve | `transpose` moves a single Note. |
| `music.context-scope` | preserve | Musical context is dynamic (seen where called, innermost wins, restored on exit); variables stay lexical. |
| `music.sections` | preserve | Parameterized section calls in song literals. |

## Covered elsewhere

The contract does not duplicate deep suites that already pin these behaviors:

| Behavior | Existing coverage |
| --- | --- |
| Overload ranking tiers and unit-aware resolution | `tests/test_type_ergonomics.flow`, `flow-lang.Tests/Phase36/NamedArgsResolverTests.cs` |
| Strict-mode sites and propagation | `flow-lang.Tests/Integration/Phase44/` |
| Qualified modules, shadowing, duplicate module names | `flow-lang.Tests/Integration/Phase43/` |
| Thunk memoization and cached exceptions (C# level) | `flow-lang.Tests/Unit/ThunkTests.cs` |
| Seeded generation and two-run determinism | `flow-lang.Tests/Integration/Phase44/Phase44TwoRunDeterminismTests.cs`, `Phase28`/`Phase36` suites |
| Tuning, voices, articulation and output timing | `flow-lang.Tests/Integration/Phase28/`, `Phase32/`, `Phase37/`, RMS baselines |
| Beat literals and per-file `beat-true-to-sig` | `tests/test_beat_*.flow`, `flow-lang.Tests/Integration/Phase45/` |

## What analysis can prove

Most checking happens during evaluation. Type annotations are enforced when a
value is bound, overloads are resolved from runtime argument types, and many
failures are only discovered on the executed path (for example a missing
function or an out-of-range Int binding). Parse errors
(`calls.infix-rejected`, `procedures.named-args-order`, `generous.unknown-pragma`)
are the only failures reported before execution. Failures during evaluation are
always reported at a source location and never end the program early. A future
analyzer must not claim more than this contract demonstrates; see
`docs/decisions/2026-09-22-analysis-and-evaluation-api.md`.
