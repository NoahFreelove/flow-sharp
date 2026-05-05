# Migrate26

Throwaway one-shot migration tool for Phase 26 (prefix-only operator standardization).
Re-uses `flow-lang/Lexing/SimpleLexer.cs` to walk tokens; for every infix
`Plus`/`Minus`/`Star`/`Slash` between value-producing tokens, emits the prefix form
`(add A B)` / `(sub A B)` / `(mul A B)` / `(div A B)`. String concatenation becomes
`(concat A B)`. Parser shorthand `-IDENT` collapses to `(neg IDENT)`.

Idempotent — running twice produces zero diff.

Invoke: `dotnet run --project scripts/Migrate26 -- tests/ examples/ flow-lang/`

Kept as historical record per CONTEXT D-12. Not a permanent dotnet tool.

## Wave Sequence

- **Wave 0** (this commit): csproj scaffold + stub `Program.cs` that compiles and prints
  a "not yet implemented" notice. Establishes the project structure so Wave 2 has a
  drop-in target for the walker.
- **Wave 1** (plan 26-02): parser/lexer/builtin changes that make the migration target
  possible — deletes `BinaryExpression`, removes `ParseAdditive`/`ParseMultiplicative`,
  ships Long+Number arithmetic overloads + `(neg)` 5-pack + `(idiv)`, extends lexer for
  negative-literal positions.
- **Wave 2** (plan 26-03): fills in `Program.cs` with the token walker + precedence
  climber. Tested against a curated input/output pair set.
- **Wave 3** (plan 26-04): runs the walker against `tests/`, `examples/`, and
  `flow-lang/*.flow` (~82 files). Includes the SHA256 byte-identical hash gate for
  `examples/output/{tutorial,showcase}.{wav,mid}` per D-14.
