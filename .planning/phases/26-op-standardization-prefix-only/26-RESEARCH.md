# Phase 26: Op Standardization (Prefix-Only) - Research

**Researched:** 2026-05-04
**Domain:** C# / .NET 10 — Flow interpreter language-grammar surgery (parser + AST + lexer + builtin registry + tokenizer-based source migration)
**Confidence:** HIGH (this phase is entirely contained within the existing Flow codebase — no external libraries, all symbols verified via `grep` against the live tree)

## Summary

Phase 26 is three coordinated changes plus a one-shot mass migration:

1. **AST/parser surgery** — delete `BinaryExpression.cs` (record + enum), gut `ParseAdditive`/`ParseMultiplicative`/the unary-arithmetic branch in `ParseUnary`, rewire `ParseFlowExpression` and two stray `ParseUnary` callers (lines 774, 940) to call `ParsePostfix` directly, and delete `EvaluateBinary` + its switch case.
2. **Builtin completion** — extend `(add)/(sub)/(mul)/(div)` from current 3-type coverage (Int, Float, Double) to 5-type same-type fast paths (Int, Long, Float, Double, Number), ship `(neg)` as a 5-pack, ship `(idiv Int Int) → Int`, and update `flow-lang/std.flow` with matching `internal proc` declarations.
3. **Lexer extension** — at expression-start positions, emit `IntLiteral(-5)` / `FloatLiteral(-3.14)` as single tokens; outside those positions keep `Minus`/`Plus` as standalone tokens.
4. **Source migration** — a tokenizer-based throwaway script rewrites every `.flow` file with infix arithmetic to prefix form, gated by an SHA256 hash check on `examples/output/flow_{tutorial,showcase}.{wav,mid}` pre/post migration.

**Primary recommendation:** Land the AST/parser/registry changes in Commit 1 (build still compiles, all `.flow` files break), run the migration script + verify SHA256 hash gate in Commit 2 (everything green again), then update CLAUDE.md in Commit 3. **Critical landmine:** `EvaluateFunctionCall` does NOT coerce arguments at the implementation boundary, so the D-05 "convertible scoring fallback" path will throw `InvalidCastException` unless the planner adds an explicit `ConvertTo` call after overload resolution OR each new same-type StdLib helper coerces internally. Plan must address this before commit 1.

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Negation Strategy (DA-1)**
- **D-01:** Variable negation `-x` is implemented as a parser shorthand (`-IDENT` → `(neg x)`), never `BinaryExpression`. The `Minus` must not have a value-producing token to its left.
- **D-02:** Negative number literals (`-5`, `-3.14`) lex as single tokens at six positions: expression-start, after `(`, after `,`, after `[`, after `->`, and inside `| ... |` note streams.
- **D-03:** Unary `+` is a no-op shorthand. `+5` parses as `5`; `+x` parses as `x`. Aligns with `+50c` semantics.
- **D-04:** Implementation strategy: track previous-emitted-token type in `SimpleLexer`; when `_lastEmittedType` is one of `(LParen, Comma, LBracket, Arrow, Equals, Colon, statement-start, Pipe-open-of-note-stream)` AND next chars match `[+-]\d`, emit number literal directly.

**Builtin Overload Shape (DA-2)**
- **D-05:** Two-tier overload strategy. Fast path: 5 same-type overloads per op (Int, Long, Float, Double, Number) using direct CLR primitives. Flexible path: mixed-type calls fall through OverloadResolver convertible scoring (+100); narrower widens to wider; wider type's same-type fast path executes.
- **D-06:** Mixed-type return rule — wider operand wins (Int < Long < Float < Double < Number).
- **D-07:** `(neg)` ships 5 per-type overloads. Return type matches input. No Sequence/Note overload.
- **D-08:** `(div Int Int)` auto-promotes to Double. Ship `(idiv Int Int) → Int` as the integer-division escape valve. Other same-type `(div)` overloads return their input type unchanged.
- **D-09:** `(concat String String)` already shipped at `BuiltInFunctions.cs:200`. The String-Add path in `EvaluateBinary` (lines 255–259) is removed entirely. Existing `(concat Array Array)` at `BuiltInFunctions.cs:481` is unchanged.
- **D-10:** Performance philosophy — same-type fast paths use raw CLR primitives, no allocation; mixed-type paths only need to *work*.

**Migration Approach (DA-3)**
- **D-11:** Throwaway tokenizer-based script at `scripts/migrate-26.cs` (or `.csx`). Re-uses `flow-lang/Lexing/SimpleLexer.cs`. Idempotent.
- **D-12:** One-shot, kept as historical record. Not a permanent dotnet tool.
- **D-13:** Single mega-commit after parser change. Three-commit shape: (1) parser/registry/lexer change, (2) migration of all `.flow` files, (3) CLAUDE.md doc update.
- **D-14:** Pre/post SHA256 hash gate during commit 2. Phase 18 + Phase 25 byte-identical xUnit Facts are the persistent guards. **No new Phase 26 byte-identical xUnit test.**

**Legacy-Infix Diagnostic (DA-4)**
- **D-15:** Generic 'unexpected token' parse error is sufficient. No charitable migration hint.
- **D-16:** Flow is pre-public — no legacy compatibility burden.
- **D-17:** Deviation from `feedback_charitable_interpretation.md` is intentional and bounded by D-16.

### Claude's Discretion

- **Exact placement of negative-literal lex logic** in `SimpleLexer.cs` — extend `case '+': case '-':` branch with a peek-back at `_lastEmittedType` (a) or factor out a `TryLexSignedNumber()` helper (b). Recommendation: **(b)** for testability.
- **Naming of integer-division builtin** — `(idiv)` working name. Recommendation: **keep `(idiv)`**.
- **Migration script form** — `.cs` (compiled) vs `.csx` (script). **`dotnet-script` is NOT installed on the dev machine** (verified via `dotnet tool list -g`). Recommendation: **standalone csproj at `scripts/Migrate26/`** invoked via `dotnet run --project scripts/Migrate26 -- <files>`.
- **Order of Long vs Number registrations** — cosmetic. Recommendation: **Long after Float, Number last** (matches widening chain).
- **Whether `(neg)` and `(idiv)` get their own `// Negation` block in `std.flow` or append to existing arithmetic block** — recommendation: **append** to the existing block at lines 38–49.

### Deferred Ideas (OUT OF SCOPE)

- `(neg Sequence)` / `(neg Note)` musical-inversion overloads — `invert(seq)` already exists.
- Charitable migration hint diagnostic ("infix removed in v1.3 — use `(add a b)`") — pre-public.
- `docs/migration-26.md` for end users — no external users.
- Permanent `flow-lang.Tools/Migrate26` dotnet tool — throwaway is sufficient.
- Mixed-type cross-overload matrix (e.g., explicit `(add Int Long)`, all 25 per op).
- Removing unused Pidgin parser-combinator dependency.
- Phase 26 byte-identical regression xUnit test (`ByteIdenticalShowcasePrefixTests.cs`) — Phase 18 + 25 tests are the persistent guards.
- `(mod a b)` / `(rem a b)` / `(pow a b)` builtins.
- `flow-lsp` semantic-tokens explicit removal of `+/-/*//` operator class.
- Updating PROJECT.md milestone goal text.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **STD-01** | Parser/AST cleanup: remove `BinaryExpression` + `BinaryOperator`, delete `ParseAdditive`/`ParseMultiplicative`/`ParseUnary` arithmetic branch, delete `EvaluateBinary` + its switch case | §"Parser/AST Surgery — Complete Deletion List" maps every call site by file:line. Confirmed via `grep -rn "BinaryExpression\|BinaryOperator"` across solution: hits ONLY in `flow-lang/Ast/Expressions/BinaryExpression.cs`, `flow-lang/Parsing/Parser.cs`, `flow-lang/Interpreter/ExpressionEvaluator.cs`. **Zero hits in flow-lsp, flow-midi, flow-lang.Tests.** |
| **STD-02** | Builtin completion: 5-type overloads for `(add)/(sub)/(mul)/(div)`, 5-pack `(neg)`, `(idiv Int Int)`, lexer single-token negative literals at 6 positions | §"Builtin Registration Shape" gives template; §"Lexer Extension — Negative Literal Detection" gives helper sketch. Numeric type chain Int→Long→Float→Double→Number verified in `IntType.cs/LongType.cs/FloatType.cs/DoubleType.cs/NumberType.cs`. Existing `(add)/(sub)/(mul)/(div)` registrations confirmed at `BuiltInFunctions.cs:212-271`. |
| **STD-03** | Migrate all in-repo `.flow` files to prefix; preserve byte-identical `tutorial.flow`/`showcase.flow` output; update CLAUDE.md | §"Migration Targets" counts 97 in-repo `.flow` files (87 tests + 3 examples + 7 stdlib); 41 use infix arithmetic; SHA256 gate path verified at `examples/output/flow_{tutorial,showcase}.{wav,mid}`. CLAUDE.md stale references found at line 148 (lambda example) and line 175 (BinaryExpression row). |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| AST node deletion + dispatch removal | Compiler/AST (`flow-lang/Ast/`, `flow-lang/Interpreter/`) | — | `BinaryExpression` is exclusively an interpreter concern |
| Parser grammar change | Compiler/Parser (`flow-lang/Parsing/Parser.cs`) | — | Recursive-descent path is in this single file |
| Lexer expression-start tracking | Compiler/Lexer (`flow-lang/Lexing/SimpleLexer.cs`) | Parser (consumer of negative literal tokens) | Needs `_lastEmittedType` field on the lexer instance |
| Builtin registration | Standard Library (`flow-lang/StandardLibrary/BuiltInFunctions.cs`, `StdLib.cs`) | TypeSystem (overload resolver) | Same shape as existing arithmetic registrations |
| `internal proc` declarations | Standard Library `.flow` source (`flow-lang/std.flow`) | — | Must mirror C# registrations or builtins are invisible at parse time |
| Source rewriting (one-shot) | Tooling (`scripts/Migrate26/`) | Lexer (re-used) | Throwaway script — re-uses `SimpleLexer` to walk tokens |
| Byte-identical determinism gate | Build/CI ad-hoc | — | One-time SHA256 check during migration commit, no permanent test |
| Documentation | Project docs (`CLAUDE.md`) | — | One-line edits at lines 148 and 175 |

## Standard Stack

### Core (Existing — No Changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Target framework — `dotnet build` already configured. [VERIFIED: solution builds] |
| C# 13 | Latest | Language | Record types + pattern matching used throughout. [VERIFIED: existing AST nodes] |
| `FlowLang.Lexing.SimpleLexer` | repo | Tokenization | `public class SimpleLexer` — re-usable from migration script. [VERIFIED: SimpleLexer.cs:12] |
| `FlowLang.Lexing.Token` | repo | Token record | `public record Token(...)` with `Type`, `Text`, `Location`, `Value`, `OriginalText`. [VERIFIED: Token.cs] |
| `FlowLang.Diagnostics.ErrorReporter` | repo | Required ctor arg for SimpleLexer | `public class ErrorReporter` — instantiable from migration script. [VERIFIED: ErrorReporter.cs:6] |

### New External Dependencies
**None.** This phase introduces zero new NuGet packages or third-party libraries. Per the project's minimal-dependencies stance and the pre-public memory, the migration tool is hand-rolled C# re-using the existing lexer.

### Migration Tool Form Factor

| Option | Pros | Cons | Decision Driver |
|--------|------|------|-----------------|
| `.csx` script via `dotnet-script` | Faster to iterate; no project file | **`dotnet-script` is NOT installed** [VERIFIED: `dotnet tool list -g` shows only `dotnet-ef`] | Would require user install step before commit 1 can land |
| Standalone csproj `scripts/Migrate26/` | Integrates cleanly with `dotnet run`; references `flow-lang.csproj` for `SimpleLexer` access | Requires a 6-line `.csproj` file | Zero install steps; works from clean checkout |
| `dotnet run --project scripts/Migrate26 -- <files>` | One command | Extra ProjectReference | Recommended |

**Recommendation:** Standalone csproj. The CONTEXT.md preference for `.csx` was conditional on `dotnet-script` being available — verified absent.

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                  COMMIT 1: Code Change                  │
└─────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ SimpleLexer.cs   │ ─→ │ Parser.cs        │ ─→ │ ExpressionEval.. │
│ (extend)         │    │ (delete ParseAdd,│    │ (delete          │
│ - track          │    │  ParseMul, unary │    │  EvaluateBinary, │
│   _lastEmitted   │    │  arith branch;   │    │  switch case)    │
│ - emit signed    │    │  rewire 3 sites: │    │                  │
│   number literals│    │  668, 689, 940;  │    │                  │
│   at 6 positions │    │  add -IDENT →    │    │                  │
│                  │    │  (neg) shorthand)│    │                  │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                  │
        ┌─────────────────────────┴────────────────────┐
        ▼                                              ▼
┌──────────────────┐                          ┌──────────────────┐
│ BuiltInFunctions │                          │ std.flow         │
│ .cs (extend)     │                          │ (mirror new      │
│ - 5-type per op  │                          │  internal proc   │
│ - (neg) 5-pack   │                          │  decls)          │
│ - (idiv) Int/Int │                          │                  │
│ - (div Int Int)  │                          │                  │
│   → Double promo │                          │                  │
└──────────────────┘                          └──────────────────┘
                                  │
                                  ▼
                  ┌──────────────────────────────┐
                  │ StdLib.cs (extend)           │
                  │ - AddLong / AddNumber / etc. │
                  │ - NegInt / NegLong / ...     │
                  │ - IDivInt                    │
                  └──────────────────────────────┘
        ════════════════════════════════════════════════
        State: code compiles, but every existing .flow
        with infix arithmetic FAILS to parse.
        ════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────┐
│            COMMIT 2: Migration of .flow Files          │
└─────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────┐    ┌──────────────────────────┐    ┌──────────────────┐
│ Pre-migration    │ ─→ │ scripts/Migrate26/       │ ─→ │ Post-migration   │
│ SHA256 gate      │    │  - re-use SimpleLexer    │    │ SHA256 verify    │
│ - run            │    │  - precedence climber    │    │ - rerun          │
│   tutorial.flow  │    │    (Plus/Minus < Star/   │    │   tutorial.flow  │
│ - run            │    │    Slash precedence)     │    │ - rerun          │
│   showcase.flow  │    │  - skip strings,         │    │   showcase.flow  │
│ - sha256sum      │    │    comments,             │    │ - sha256sum      │
│   *.wav *.mid    │    │    note streams          │    │   *.wav *.mid    │
│ → /tmp/pre.txt   │    │  - idempotent            │    │ → /tmp/post.txt  │
└──────────────────┘    └──────────────────────────┘    └──────────────────┘
                                                                │
                                                                ▼
                                                     ┌──────────────────┐
                                                     │ diff pre post    │
                                                     │ MUST be empty    │
                                                     │ else abort+bisect│
                                                     └──────────────────┘

┌─────────────────────────────────────────────────────────┐
│              COMMIT 3: CLAUDE.md update                 │
└─────────────────────────────────────────────────────────┘
        - line 148: rewrite lambda example (no `n * 2` infix)
        - line 175: delete BinaryExpression row, add note
                    about prefix-only arithmetic
```

### Component Responsibilities

| File | Lines | Action |
|------|-------|--------|
| `flow-lang/Ast/Expressions/BinaryExpression.cs` | 1–20 (entire file) | **Delete** — record + enum (4 values: Add/Subtract/Multiply/Divide) [VERIFIED: only file content] |
| `flow-lang/Parsing/Parser.cs` | 668 (`ParseAdditive` → `ParsePostfix`), 689 (same), 713–728 (`ParseAdditive` body), 730–745 (`ParseMultiplicative` body), 747–763 (`ParseUnary` body), 774 (`ParseUnary` → `ParsePostfix` for array index), 940 (`ParseUnary` → `ParsePostfix` for func call args) | **Edit/delete** — see "Parser/AST Surgery" section below for the full call graph |
| `flow-lang/Parsing/Parser.cs` | 121, 130, 140, 450, 465, 527, 542, 556 | **PRESERVE** — these are musical context blocks (`pan`, `gain`, `reverbTime`, `tempo`, `swing`) that consume `Minus`/`Plus` separately. **CRITICAL LANDMINE** — see Pitfalls. |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | 39 (switch case), 250–335 (`EvaluateBinary` method) | **Delete** |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | 212–271 (existing add/sub/mul/div Int/Float/Double registrations) | **Add** Long + Number registrations. **Add** `(neg)` 5-pack. **Add** `(idiv Int Int)`. **Modify** `divIntSignature` impl to promote to Double per D-08. |
| `flow-lang/StandardLibrary/StdLib.cs` | 176–294 (existing arithmetic helpers) | **Add** new static methods: `AddLong`, `AddNumber`, `SubLong`, `SubNumber`, `MulLong`, `MulNumber`, `DivLong`, `DivNumber` (note: `DivInt` modified to return Double per D-08), `NegInt`/`NegLong`/`NegFloat`/`NegDouble`/`NegNumber`, `IDivInt`. Pattern: `args[0].As<long>() + args[1].As<long>()` style. |
| `flow-lang/std.flow` | 38–49 (existing arithmetic block) | **Append** new `internal proc` declarations: 5 per op × 4 ops − 3 already there = ~17 new declarations + 5 `(neg)` + 1 `(idiv)` = ~23 lines added. |
| `flow-lang/Lexing/SimpleLexer.cs` | Add field `_lastEmittedType` near `_position`/`_line`/`_column` (line 18). Modify `Tokenize()` to track. Add `TryLexSignedNumber()` helper near line 319 (next to `TryLookAheadSpecialLiteral`). | New helper of ~30 lines + 2 lines of state tracking |
| `scripts/Migrate26/Migrate26.csproj` | NEW | Standalone csproj with `<ProjectReference Include="../../flow-lang/flow-lang.csproj" />` |
| `scripts/Migrate26/Program.cs` | NEW | Tokenizer-walker + precedence climber + emitter (~200 lines) |
| `CLAUDE.md` | 148 (lambda example with `n * 2`/`a + b`), 175 (`BinaryExpression` AST table row) | Edits per "Documentation Updates" |

### Recommended Project Structure

```
scripts/
  Migrate26/                 # NEW (Phase 26 only)
    Migrate26.csproj         # ProjectReference → flow-lang
    Program.cs               # Token walker + precedence emitter
    README.md                # one-liner: "throwaway, kept as historical record"
```

## Standard Stack — Implementation Templates

### Pattern 1: Same-Type Builtin Registration (D-05)

```csharp
// flow-lang/StandardLibrary/BuiltInFunctions.cs (adjacent to lines 212-271)

var addLongSignature = new FunctionSignature("add", [LongType.Instance, LongType.Instance]);
registry.Register("add", addLongSignature, StdLib.AddLong);

var addNumberSignature = new FunctionSignature("add", [NumberType.Instance, NumberType.Instance]);
registry.Register("add", addNumberSignature, StdLib.AddNumber);

// Repeat for sub, mul, div × Long, Number = 8 new registrations.
// Modify divIntSignature impl to promote to Double per D-08:
registry.Register("div", divIntSignature, StdLib.DivIntPromote); // returns Double
```

### Pattern 2: StdLib Helper Shape

```csharp
// flow-lang/StandardLibrary/StdLib.cs (after line 294)

public static Value AddLong(IReadOnlyList<Value> args)
{
    var a = args[0].As<long>();
    var b = args[1].As<long>();
    return Value.Long(a + b);
}

public static Value AddNumber(IReadOnlyList<Value> args)
{
    // Number is BigInteger-backed
    var a = args[0].As<BigInteger>();
    var b = args[1].As<BigInteger>();
    return Value.Number(a + b);
}

public static Value NegInt(IReadOnlyList<Value> args) => Value.Int(-args[0].As<int>());
public static Value NegLong(IReadOnlyList<Value> args) => Value.Long(-args[0].As<long>());
public static Value NegFloat(IReadOnlyList<Value> args) => Value.Float(-args[0].As<double>()); // FloatType backs double
public static Value NegDouble(IReadOnlyList<Value> args) => Value.Double(-args[0].As<double>());
public static Value NegNumber(IReadOnlyList<Value> args) => Value.Number(-args[0].As<BigInteger>());

public static Value IDivInt(IReadOnlyList<Value> args)
{
    var a = args[0].As<int>();
    var b = args[1].As<int>();
    if (b == 0) throw new InvalidOperationException("Integer division by zero");
    return Value.Int(a / b);
}

public static Value DivIntPromote(IReadOnlyList<Value> args)
{
    var a = args[0].As<int>();
    var b = args[1].As<int>();
    if (b == 0) throw new InvalidOperationException("Division by zero");
    return Value.Double((double)a / b); // D-08: auto-promote to Double
}
```

[VERIFIED via `flow-lang/StandardLibrary/StdLib.cs:176-294` — pattern matches existing AddInt/AddFloat/AddDouble shape exactly.]

### Pattern 3: Lexer Expression-Start Tracking (D-04)

```csharp
// flow-lang/Lexing/SimpleLexer.cs (add near line 18)
private TokenType? _lastEmittedType = null;

// Modify the call to NextToken() in Tokenize() to record _lastEmittedType after emit:
public List<Token> Tokenize()
{
    var tokens = new List<Token>();
    while (!IsAtEnd())
    {
        SkipWhitespaceAndComments();
        if (IsAtEnd()) break;
        var token = NextToken();
        if (token != null)
        {
            tokens.Add(token);
            _lastEmittedType = token.Type; // NEW
        }
    }
    tokens.Add(new Token(TokenType.Eof, "", new SourceLocation(_line, _column, _fileName)));
    return tokens;
}

// New helper near TryLookAheadSpecialLiteral (line 319):
private Token? TryLexSignedNumber(SourceLocation start)
{
    // Only at expression-start positions (D-04):
    // null (statement-start), LParen, Comma, LBracket, Arrow, Assign (=), Colon (:), Pipe
    bool isExprStart = _lastEmittedType is null
        or TokenType.LParen
        or TokenType.Comma
        or TokenType.LBracket
        or TokenType.Arrow
        or TokenType.Assign
        or TokenType.Colon
        or TokenType.Pipe
        or TokenType.Semicolon;        // for explicit statement separators
    if (!isExprStart) return null;

    char sign = Peek();
    if (sign != '+' && sign != '-') return null;
    if (!char.IsDigit(PeekNext())) return null;

    // D-03: + at expression-start is silently absorbed (handled by parser-side strip OR
    // by the lexer: if sign == '+', advance past '+' and fall through to number scan).
    // Recommended: lexer absorbs '+' here, returns positive IntLiteral / FloatLiteral.
    // For '-': capture sign, scan digits + optional decimal, return negative-valued literal.

    // ... (~25 lines of digit/decimal scanning, parallel to existing ScanNumber)
    // Return new Token(TokenType.IntLiteral, "-5", start, value: -5)
    //  or new Token(TokenType.FloatLiteral, "-3.14", start, value: -3.14)
}
```

[CITED: existing typed-literal lex flow at `SimpleLexer.cs:81-86` and `TryLookAheadSpecialLiteral` at lines 319–438 — same prev-position gating pattern.]

**Critical:** This new helper MUST run BEFORE the existing `TryLookAheadSpecialLiteral` block at lines 81–86 — OR the ordering must be: try suffix-typed (`-3dB`, `+50c`, `-5st`) first; if no suffix matches, try `TryLexSignedNumber()`; if still no match, fall through to `SingleChar(Plus/Minus)`. Otherwise `-3` followed by something else might be greedily caught by the new path.

### Pattern 4: Parser `-IDENT` Shorthand (D-01)

```csharp
// flow-lang/Parsing/Parser.cs — replaces deleted ParseUnary
// New entry to ParsePrimary or new wrapper ParseUnaryShorthand
private Expression ParseUnaryShorthand()
{
    // D-03: silently strip '+' at expression-start (no node emitted)
    if (Match(TokenType.Plus))
    {
        // No-op; just continue
    }
    // D-01: '-' followed by identifier → (neg IDENT)
    if (Check(TokenType.Minus) && _current + 1 < _tokens.Count
        && _tokens[_current + 1].Type == TokenType.Identifier)
    {
        var loc = CurrentToken.Location;
        Advance(); // consume '-'
        var name = Advance().Text;
        return new FunctionCallExpression(loc, "neg", [new VariableExpression(loc, name)]);
    }
    return ParsePostfix();
}
```

### Pattern 5: Migration Script — Tokenizer Walker (D-11)

```csharp
// scripts/Migrate26/Program.cs (sketch)
using FlowLang.Lexing;
using FlowLang.Diagnostics;

// For each .flow file:
foreach (string file in args)
{
    string source = File.ReadAllText(file);
    var lexer = new SimpleLexer(source, new ErrorReporter());
    var tokens = lexer.Tokenize();

    // Walk tokens; for each value-producing-token Plus/Minus/Star/Slash value-producing-token
    // emit prefix form. Use a recursive precedence climber: Plus/Minus < Star/Slash.
    // Skip everything inside Pipe...Pipe (note streams).
    // Skip StringLiteral content (the lexer already gives us a StringLiteral token, so its
    // body is opaque — no inspection needed).
    // Skip Comment tokens (line comments).
    // Skip Note: comment lines (the lexer absorbs these in SkipWhitespaceAndComments).
    string output = Rewrite(tokens, source);
    File.WriteAllText(file, output);
}
```

### Anti-Patterns to Avoid

- **Regex sed sweep on `.flow` source.** Would mis-handle `//` comments containing `+`, `Note:` lines containing `*`, string literals containing arithmetic glyphs, and note-stream `-3dB`/`-5st`/`/N` syntax. Tokenizer-based is correct.
- **Hand-rolling SHA256 verification in C#.** Use `sha256sum` or `cmp` from the shell — already proven in Phase 18 + 25.
- **Adding a new Phase 26 byte-identical xUnit test.** Per D-14, Phase 18 + 25 are sufficient; sibling test would be duplication.
- **Forgetting to update `std.flow`.** Lesson from Phase 25 D-25: registry registration without a matching `internal proc` declaration is invisible to user scripts.
- **Adding mixed-type cross-overload registrations** (e.g., `(add Int Long)`). Per D-05, mixed types fall through OverloadResolver convertible scoring (+100). Adding 25 explicit overloads per op would bloat the registry.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Token classification of `.flow` source | Custom regex / character scanner | Re-use `FlowLang.Lexing.SimpleLexer` | Lexer already handles 78 token types, comments, strings, note streams, typed literals. Re-implementing would re-introduce the very bugs the old `EvaluateBinary` had. |
| SHA256 file hashing | Inline `SHA256.Create()` C# in migration script | `sha256sum` shell command (verified in Phase 18 / 25 patterns) | Faster to invoke, output is machine-readable, no ceremony |
| Operator precedence climbing | Naive left-to-right token replacement | Mini Pratt parser inside migration script | `a + b * c` must become `(add a (mul b c))`, not `(mul (add a b) c)`. Precedence-aware rewrite is non-negotiable. |
| Idempotence checking | Skipping already-migrated files via heuristic | Run script twice; second pass produces zero diff | Simpler to test than to gate |
| Module loading inside migration script | Re-implement `ModuleLoader`/`PragmaScanner` | Skip — only need lexer's token stream, not semantics | Migration is purely syntactic |

**Key insight:** The migration script does **NOT** need to re-implement parsing or evaluation. It walks the lexer token stream and rewrites a narrow class of token-pair patterns (`value-token Plus/Minus/Star/Slash value-token`). The lexer already does all the heavy lifting (string content, comments, note streams, typed literals).

## Runtime State Inventory

This is a refactor phase; explicit answers per category:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | None — Flow has no runtime persistence layer beyond audio output files. WAV/MIDI files in `examples/output/` are regenerated each run, not persistent state | None — verified by inspecting `examples/output/` contents (4 WAV + 4 MIDI, all output artifacts) |
| **Live service config** | None — Flow is a single-process interpreter, no external services, no UI/database-backed config | None |
| **OS-registered state** | None — no installed binary, systemd unit, or scheduled task carries Flow's state | None |
| **Secrets/env vars** | None — Flow does not read env vars beyond `DOTNET_ROOT` style runtime config | None |
| **Build artifacts / installed packages** | `flow-lang/bin/`, `flow-lang/obj/`, `flow-interpreter/bin/`, etc. are stale after AST node deletion. `dotnet build` rebuild handles this. **No** stale `.egg-info`/global packages — the project ships nothing | `dotnet build` (already part of every commit's CI flow) |
| **In-tree caches that pin old AST** | LSP semantic-token cache (per-document, in-memory) — flushed on next document open. NO disk cache to clean | None |

**The canonical question — "After every file in the repo is updated, what runtime systems still have the old string cached?"** — Answer: none. Phase 26 is purely textual + binary; no databases, no live services, no OS registrations.

## Parser/AST Surgery — Complete Deletion List

[VERIFIED: each entry below is a hit from `grep -rn "BinaryExpression\|BinaryOperator\|ParseAdditive\|ParseMultiplicative\|EvaluateBinary"` across the entire solution.]

### Files referencing `BinaryExpression` or `BinaryOperator`

```
flow-lang/Ast/Expressions/BinaryExpression.cs              (entire file: delete)
flow-lang/Parsing/Parser.cs:720,721,724,737,738,741,752,753,759
flow-lang/Interpreter/ExpressionEvaluator.cs:39,250,257,272-275,285-288,298-301,311-314,324-327
```

**Zero hits in:** `flow-lsp/`, `flow-midi/`, `flow-lang.Tests/`, `flow-interpreter/`. CONFIRMS this surgery does NOT cascade into LSP, MIDI, or test fixtures. [VERIFIED via `grep -rn` excluding bin/obj.]

### Files referencing `ParseAdditive` or `ParseMultiplicative`

```
flow-lang/Parsing/Parser.cs:668  → ParseFlowExpression's `var left = ParseAdditive()`. REPLACE with `ParsePostfix()` (or `ParseUnaryShorthand()` if D-01 shorthand wraps).
flow-lang/Parsing/Parser.cs:673  → ParseFlowExpression's `var right = ParseAdditive()` (after `Match(Arrow)`). REPLACE.
flow-lang/Parsing/Parser.cs:689  → flow-args parsing `args.Add(ParseAdditive())`. REPLACE.
flow-lang/Parsing/Parser.cs:713  → method definition `ParseAdditive`. DELETE.
flow-lang/Parsing/Parser.cs:715  → ParseAdditive body's `var left = ParseMultiplicative()`. (deleted with method)
flow-lang/Parsing/Parser.cs:723  → (deleted with method)
flow-lang/Parsing/Parser.cs:730  → method definition `ParseMultiplicative`. DELETE.
flow-lang/Parsing/Parser.cs:732  → ParseMultiplicative body's `var left = ParseUnary()`. (deleted with method)
```

### `ParseUnary` callers — landmine

```
flow-lang/Parsing/Parser.cs:732  → inside ParseMultiplicative (deleted with method)
flow-lang/Parsing/Parser.cs:740  → inside ParseMultiplicative loop (deleted with method)
flow-lang/Parsing/Parser.cs:747  → method definition `ParseUnary`. DELETE the arithmetic branch (lines 749-760); keep the method as a thin pass-through OR replace with ParseUnaryShorthand.
flow-lang/Parsing/Parser.cs:755  → recursive call inside ParseUnary's arithmetic branch (deleted)
flow-lang/Parsing/Parser.cs:774  → ★ inside ParsePostfix array-index handler. NOT in arithmetic context. REWIRE to ParsePostfix or to a dedicated index-expr parser.
flow-lang/Parsing/Parser.cs:940  → ★ inside ParsePrimary's optional-paren args loop. NOT in arithmetic context. REWIRE to ParsePostfix.
```

★ **CRITICAL:** Lines 774 and 940 are NOT arithmetic call sites. They use `ParseUnary` as a "lightweight expression parser" for argument-position contexts (array index, optional-paren func call args). After deleting `ParseUnary`, both must be rewired. Recommendation: change them to call `ParsePostfix()` directly (skipping the now-trivial wrapper) or to a renamed helper.

If `ParseUnary` is **kept as a no-op pass-through** (per CONTEXT.md's "becomes a thin pass-through to `ParsePostfix` OR is deleted entirely if unused"), lines 774 and 940 don't need changes — but the recommendation is to delete the wrapper for clarity, since the arithmetic branch is its only reason to exist.

### `EvaluateBinary` references

```
flow-lang/Interpreter/ExpressionEvaluator.cs:39   → switch dispatch case `BinaryExpression bin => EvaluateBinary(bin)`. DELETE the case line.
flow-lang/Interpreter/ExpressionEvaluator.cs:250-335  → entire `EvaluateBinary` method (incl. the BigInteger/Double/Float/Long/Int branches AND the String-Add branch at 255-259). DELETE.
```

### Musical-context Plus/Minus references — PRESERVE [VERIFIED reading lines]

```
flow-lang/Parsing/Parser.cs:121  →  pan { ... } context-block detection
flow-lang/Parsing/Parser.cs:130  →  gain { ... } context-block detection
flow-lang/Parsing/Parser.cs:140  →  reverbTime { ... } context-block detection
flow-lang/Parsing/Parser.cs:450,451  →  tempo sign consumption (Match(TokenType.Minus)/Plus)
flow-lang/Parsing/Parser.cs:465,466  →  swing sign consumption
flow-lang/Parsing/Parser.cs:527,528  →  pan sign consumption
flow-lang/Parsing/Parser.cs:542,543  →  gain sign consumption
flow-lang/Parsing/Parser.cs:556     →  reverbTime negative-rejection
```

These consume `Minus`/`Plus` as **standalone tokens**. After Phase 26's lexer change, if `tempo -120` is parsed at expression-start, the lexer might emit `IntLiteral(-120)` as a single token — and the `Match(TokenType.Minus)` checks at lines 450/465/527/542/556 would silently fail.

**Mitigation (CRITICAL):** The lexer's `_lastEmittedType` gate must NOT include the tokens `Tempo`, `Swing`, `Pan`, `Gain`, `ReverbTime` in the expression-start set. After consuming `tempo`, the next position is NOT a generic expression start — it's a context-block-value start, which uses its own sign handling. Recommendation: explicitly EXCLUDE the music-context keywords from the expression-start gate, OR add them as explicit cases that fall through to standalone Minus/Plus emission.

This subtlety is the single biggest landmine in the lexer change — see Pitfalls.

## Lexer Extension — Negative Literal Detection

### Current State

`flow-lang/Lexing/SimpleLexer.cs` already has a sign-handling helper at lines 81–86 + 319–438:

```csharp
// Line 81-86 (existing):
if ((c == '+' || c == '-') && char.IsDigit(PeekNext()))
{
    var lookahead = TryLookAheadSpecialLiteral();
    if (lookahead != null)
        return lookahead;
}
// Falls through to SingleChar(TokenType.Plus/Minus) at line 108-109
```

`TryLookAheadSpecialLiteral` matches `-Nst`, `+Ndb`, `-Nc`, `+Nms`, `-Ns` and rewinds if no suffix matches.

### Phase 26 Extension

Insert a NEW step between the typed-literal lookahead and the fallthrough to `SingleChar`:

```csharp
if ((c == '+' || c == '-') && char.IsDigit(PeekNext()))
{
    // Step 1: try typed literals (existing — preserves -3dB/-5st/+50c)
    var typed = TryLookAheadSpecialLiteral();
    if (typed != null) return typed;

    // Step 2 (NEW): try plain signed number at expression-start
    var signed = TryLexSignedNumber(start);
    if (signed != null) return signed;

    // Step 3: fall through to SingleChar (preserves the parser-shorthand `-IDENT` path
    // and the musical-context tempo/pan/gain sign consumption path).
}
```

**`_lastEmittedType` set per D-04** (six expression-start positions + `Pipe`):
- `null` (start of file)
- `TokenType.LParen` — first token inside `(`
- `TokenType.Comma` — argument separator
- `TokenType.LBracket` — array literal element start
- `TokenType.Arrow` — flow operator RHS
- `TokenType.Assign` — `Int x = -5`
- `TokenType.Colon` — `proc name(Int: x, Double: y)` parameter default? Or some other use case — verify with planner. **Actually:** in Flow, `:` appears in proc signatures (`Int: x`). Not at expression-start. The `Colon` listing in CONTEXT.md D-04 may be over-broad. Recommend planner trim to: `LParen, Comma, LBracket, Arrow, Assign, Pipe, statement-start (null)`.
- `TokenType.Pipe` — note-stream open
- `TokenType.Semicolon` — explicit statement separator
- Any keyword that introduces an expression context (`Return`, etc.) — add as needed

### Inside Note Streams (D-02 + D-04)

**Question:** Does the existing `TryLookAheadSpecialLiteral` correctly catch `-3` in note-stream context? Today, `-3st` becomes `SemitoneLiteral`. `-3` (no suffix) falls through to `SingleChar(Minus)` followed by `IntLiteral(3)`. After Phase 26, `-3` inside `| ... |` should be a single `IntLiteral(-3)`.

The expression-start rule "after `Pipe`" is the correct trigger. Inside `| C4 -3 D4 |`, after `C4` the next position is NOT expression-start (the previous token is `NoteLiteral`, not in the gate set). So `-3` would parse as `Minus IntLiteral(3)` — **BUG vs. D-02**.

**Resolution:** D-02 says "Inside `| ... |` note streams, the same rule applies" — meaning negative literals should work AT EVERY ELEMENT BOUNDARY inside a note stream. The element boundaries in note streams are whitespace, but the lexer doesn't track whitespace as a token. So either:
- (a) Inside note-stream context, treat the previous-token-being-a-NoteLiteral as expression-start (whitespace-followed-by-sign-followed-by-digit is one literal). This requires the lexer to know "we're inside `|...|`".
- (b) Restrict D-02's note-stream rule to just-after-`Pipe` (the entry point), and let the typed-literal handling (`-3st`, `-3dB`) cover the rest.

**Recommendation for planner:** Verify with user. If (b) is acceptable (matches CONTEXT.md `Pipe-open-of-note-stream` phrasing), the simpler implementation suffices. If (a) is needed, add a `_inNoteStream` flag toggled on `Pipe` open/close.

### Potential conflict with D-03 (`+` no-op)

D-03 says `+5` and `+x` parse as `5` and `x`. The lexer's `TryLexSignedNumber` for `+5` should emit `IntLiteral(5)` (sign absorbed). For `+x` (Plus followed by identifier), the lexer should emit `Plus` then `Identifier`, and the parser strips the `Plus` at expression-start (per `ParseUnaryShorthand` Pattern 4 above).

## Builtin Registration Shape

### Existing Coverage [VERIFIED at `BuiltInFunctions.cs:212-271`]

| Op | Int/Int | Long/Long | Float/Float | Double/Double | Number/Number | Mixed |
|----|---------|-----------|-------------|---------------|---------------|-------|
| add | ✓ (215) | ✗ | ✓ (220) | ✓ (256) | ✗ | OverloadResolver fallback |
| sub | ✓ (240) | ✗ | ✓ (225) | ✓ (261) | ✗ | OverloadResolver fallback |
| mul | ✓ (245) | ✗ | ✓ (230) | ✓ (266) | ✗ | OverloadResolver fallback |
| div | ✓ (250) | ✗ | ✓ (235) | ✓ (271) | ✗ | OverloadResolver fallback |
| neg | ✗ | ✗ | ✗ | ✗ | ✗ | Currently NOT registered (handled inline by `ParseUnary` arithmetic branch) |
| idiv | ✗ | ✗ | ✗ | ✗ | ✗ | Currently does not exist |
| concat (String,String) | ✓ (200) | — | — | — | — | — |
| concat (Array,Array) | ✓ (481) | — | — | — | — | — |

### Phase 26 Additions

Per D-05/D-07/D-08:
- **+8 registrations:** Long-Long and Number-Number for each of {add, sub, mul, div}.
- **+5 registrations:** `(neg)` for {Int, Long, Float, Double, Number}.
- **+1 registration:** `(idiv Int Int) → Int`.
- **+1 modification:** `(div Int Int)` impl changes from `Value.Int(a/b)` to `Value.Double((double)a/b)` per D-08.

**Total deltas:** 14 new registrations, 1 impl change.

### `std.flow` Declarations [VERIFIED at `std.flow:38-49`]

Existing block:
```
internal proc add (Int: a, Int: b)
internal proc add (Float: a, Float: b)
internal proc add (Double: a, Double: b)
internal proc sub (Int: a, Int: b)
internal proc sub (Double: a, Double: b)    ← note: NO sub Float? this is an asymmetry
internal proc mul (Int: a, Int: b)
internal proc mul (Double: a, Double: b)
internal proc div (Int: a, Int: b)
internal proc div (Double: a, Double: b)
```

**Discovery:** the existing `std.flow` arithmetic block is INCOMPLETE. C# registers `subFloat`, `mulFloat`, `divFloat` (lines 222–235 of `BuiltInFunctions.cs`), but `std.flow` only lists `add Float`. The Float `internal proc` declarations for sub/mul/div are MISSING. This is a latent bug; Phase 26 should fix it incidentally by ensuring all 5 same-type overloads are declared.

[VERIFIED via `grep "internal proc add\|internal proc sub\|internal proc mul\|internal proc div" std.flow` matching only 9 lines — should be 20 (4 ops × 5 types)].

### Mixed-Type Coercion Boundary — CRITICAL LANDMINE

**Problem:** `EvaluateFunctionCall` at `ExpressionEvaluator.cs:204-207` passes argValues unconverted to the resolved overload's `Implementation!(argValues)`. The OverloadResolver picks the best match (e.g., for `(add 5 3.0)` it picks `(add Double Double)` via Int→Double convertible), but the implementation `StdLib.AddDouble` then calls `args[0].As<double>()`. **`As<T>()` does NOT coerce — it does an `is T` check and throws `InvalidCastException` on mismatch.** [VERIFIED: `Value.cs:200-210`.]

This means today's `(add 5 3.0)` would crash. Phase 26 must address this. Two viable strategies:

| Strategy | Implementation | Tradeoff |
|----------|---------------|----------|
| **(A) Coerce in invoker** | After overload resolution, before calling `Implementation!`, walk each arg-vs-param-type pair and call `argValues[i].ConvertTo(matchedSig.InputTypes[i])`. | Single change site (`EvaluateFunctionCall`); benefits ALL builtins, not just arithmetic |
| **(B) Coerce in each helper** | Each `StdLib.AddLong` etc. handles arbitrary input types via `Convert.ToInt64(args[0].Data)` style | Per-helper noise; matches existing `EvaluateBinary` Number branch pattern (`left.Data is BigInteger bl ? bl : new BigInteger(Convert.ToInt64(left.Data))`) |

**Recommendation: (A).** It fixes a latent bug across the entire builtin system, not just arithmetic. The change is ~5 lines in `EvaluateFunctionCall`:

```csharp
// After overload resolution, before Implementation!(argValues):
if (overload.IsInternal)
{
    var sig = overload.Signature;
    for (int i = 0; i < argValues.Count && i < sig.InputTypes.Count; i++)
    {
        if (!argValues[i].Type.Equals(sig.InputTypes[i])
            && argValues[i].Type.CanConvertTo(sig.InputTypes[i]))
        {
            argValues[i] = argValues[i].ConvertTo(sig.InputTypes[i]);
        }
    }
    return overload.Implementation!(argValues);
}
```

`ConvertTo` already supports the full numeric chain [VERIFIED: `Value.cs:84-194`]. Strategy (A) makes mixed-type calls Just Work without per-helper boilerplate.

**Risk of strategy A:** If any existing builtin RELIES on receiving a strictly-typed argument that DIFFERS from its declared signature (e.g., a builtin signature listing `Void`/wildcard but the impl expecting raw `int`), the coercion step might break it. Audit needed during planning. The likeliest at-risk builtin is `(equals)` / `(lt)` / `(gt)` etc. registered with `[VoidType.Instance, VoidType.Instance]` at lines 322–350 — `VoidType` is treated as wildcard, but `ConvertTo` shouldn't fire because `VoidType.IsCompatibleWith(anything)` returns false → `CanConvertTo` returns false → no conversion.

[VERIFIED: `FlowType.cs:18` — `IsCompatibleWith(other)` defaults to `Equals(target)`; `VoidType` does not override this. So Void parameters never trigger coercion.]

## Migration Targets

### File counts [VERIFIED via `find` excluding bin/obj]

| Directory | Total `.flow` | With infix arithmetic | Notes |
|-----------|---------------|------------------------|-------|
| `tests/` | 87 | ~38 | Includes one `tests/std.flow` (test fixture, not the real stdlib) |
| `examples/` | 3 | 2 | `tutorial.flow`, `showcase.flow` (1 line, in a comment), `long_demo.flow` |
| `flow-lang/` (stdlib) | 7 | 4 | `audio.flow` (`*`, `/`), `composition.flow` (`*`), `notation.flow`, `bars.flow`, `collections.flow`, `std.flow`, `test.flow` |
| **Total** | **97** | **~41** | CONTEXT.md's "82 of 97" likely includes false-positive matches like `//` comment slashes |

Conservative pre-grep: `grep -lE "[a-zA-Z0-9_)]\s+[+*/-]\s+[a-zA-Z0-9_(]"` returns 41 hits across all three dirs. Some of those are false positives (e.g., `showcase.flow` only matches because of `i / VI / iv / v shape` inside a `//` comment). Realistic actual touched-file count: **~38-41**.

### Specific files needing migration

[VERIFIED examples in tutorial.flow]:
- Line 51-58 chapter 2 explicitly demos infix: `Int quick = 10 + 25`, `Int sum = (add 10 25)`, `(print "Operator style: 10 + 25 = ...")` — chapter 2 is the dual-style demonstration. Migration needs to rewrite the operator-style lines AND remove the explanatory text.
- Lines 69, 73, 108, 113 (lambdas + countdown loop): `n * 2`, `n * n`, `countdown - 1`, `fn Int n => n * 2`.
- Late chapter: `Double negTwo = (sub 0.0 2.0)` — already prefix; this is the negation idiom for declaration RHS today. After Phase 26, it could become `Double negTwo = -2.0` (lexer single-token).

[VERIFIED in flow-lang stdlib]:
- `flow-lang/audio.flow:86`: `Double framesD = seconds * srDouble`
- `flow-lang/audio.flow:94`: `framesD / srD`
- `flow-lang/composition.flow:80,111`: `(intToDouble bar) * (intToDouble beatsPerBar)`
- `flow-lang/composition.flow:136`: `(intToDouble bars) * (intToDouble beatsPerBar)`

[VERIFIED in showcase.flow]: only line 12 hits the regex, but it's `// i / VI / iv / v shape` — a `//` line comment. **Showcase.flow is effectively migration-free.** This dramatically de-risks the SHA256 byte-identical gate for showcase output.

### String concat usage — none found

`grep '" \+ \|+ "'` against `tests/`, `examples/`, `flow-lang/` returned **zero matches**. Existing string concatenation in the codebase already uses `(concat a b)` or string interpolation `$"..."`. The migration script's String-Add branch is a defensive future-proofing, not a current need. **Don't worry about string concat path correctness too much** — there are no real test cases.

### Idempotence verification

After running the migration, running it AGAIN must produce zero diff. This is the smoke test. The script's "skip if already prefix" logic works because:
- `(add a b)` lexes as `LParen, Identifier("add"), Identifier("a"), Identifier("b"), RParen`. There's no `Plus`/`Minus`/`Star`/`Slash` token between value-tokens — nothing to rewrite.
- Negative literals like `-5` post-Phase-26 lex as a single `IntLiteral(-5)` — no `Minus` token visible.

So idempotence is structural, not heuristic.

## Migration Script — Architecture Detail

### Entry point

```csharp
// scripts/Migrate26/Program.cs
using FlowLang.Lexing;
using FlowLang.Diagnostics;

class Program
{
    static int Main(string[] args)
    {
        // Args: list of .flow files OR directories (glob expanded)
        var files = ExpandPaths(args);
        foreach (var file in files)
        {
            string before = File.ReadAllText(file);
            string after = Migrate(before);
            if (before != after)
            {
                File.WriteAllText(file, after);
                Console.WriteLine($"migrated: {file}");
            }
        }
        return 0;
    }
}
```

### Token-stream walking (the cheap part)

```csharp
static string Migrate(string source)
{
    var lexer = new SimpleLexer(source, new ErrorReporter());
    var tokens = lexer.Tokenize();
    // Build a linear token-index → source-text map (using Token.Location).
    // For each token, know its absolute byte offset in `source` (compute from Line/Column
    // by scanning `source` once into a line-offset array).
    // Walk tokens; locate spans that are infix-arithmetic; emit prefix-form replacements.
    return RewriteSpans(source, tokens);
}
```

### Span detection — this is where it gets non-trivial

The walker must:
1. **Skip `| ... |` note streams.** When a `Pipe` token appears at top level, advance until the matching closing `Pipe`. Note streams have their own typed-literal arithmetic (`-3dB`, `+50c`, `C4/12`) that must NOT be touched.
2. **Skip lambda bodies until `end`** — wait, do lambdas have `end`? Per `flow-lang/Parsing/Parser.cs` lambda parsing, lambdas are `fn Type name => expr`. The body is a single expression — but that expression CAN contain infix arithmetic and MUST be migrated. **Lambdas are NOT a skip case; their body is a normal expression.**
3. **Skip `proc` body...end blocks?** No — `proc` bodies contain normal expressions and MUST be migrated.
4. **Handle precedence.** `a + b * c` → `(add a (mul b c))`, not `(mul (add a b) c)`. The simplest implementation: a recursive descent walker that mirrors the deleted `ParseAdditive`/`ParseMultiplicative` precedence (multiplicative binds tighter), but emits source-text instead of AST nodes.
5. **Handle parenthesized expressions.** `(a + b) * c` → `(mul (add a b) c)`. The parens disambiguate.
6. **Handle string concat.** `"abc" + x` → `(concat "abc" x)`. Detect via `StringLiteral` followed by `Plus` followed by value-token. (May not occur in practice — see "String concat usage — none found" above — but defensive.)
7. **Handle unary minus on a variable.** `-x` → `(neg x)`. After lexer change, `-x` lexes as `Minus, Identifier(x)` (the lexer doesn't single-token `-x` because `x` isn't a digit). Migration script emits `(neg x)`.
8. **Handle unary minus on a literal.** `Int x = -5` after lexer change is already `IntLiteral(-5)`. Migration script does nothing.
9. **Handle unary plus.** `+5` post-lexer-change is `IntLiteral(5)`. `+x` is `Plus, Identifier(x)` — migration script strips the leading `Plus`.

### Riskiest edge case

**Mixed-type arithmetic chains crossing parentheses, such as the existing `flow-lang/composition.flow:80`:**
```
Double beatPos = (intToDouble bar) * (intToDouble beatsPerBar)
```
Migration target:
```
Double beatPos = (mul (intToDouble bar) (intToDouble beatsPerBar))
```

The walker must recognize that `(intToDouble bar)` is a **complete sub-expression** (LParen…RParen) and treat it as a single value-producing unit, then see `Star` followed by another `(intToDouble beatsPerBar)`, and emit the wrapping `(mul ...)`. This is straightforward if the walker tracks paren depth.

**The HIGHEST-risk edge case**: nested infix in a function-call arg, e.g.:
```
(print (str (mul 2 (add a b))))     ← no migration needed (already prefix)
(print (str a + b))                 ← migrate the inner `a + b`
(callFunc a + b c)                  ← optional-paren func call: arg list is `[a + b, c]`?
                                       Or `[a, +b, c]` (treating +b as no-op `b`)?
                                       Or `[a, b, c]` after `+` strip?
```

The optional-paren func-call grammar at `Parser.cs:931-944` consumes args via `ParseUnary`. After Phase 26, the migration script must understand that `(callFunc a + b c)` has an ambiguous-but-resolvable shape. **Recommend planner test this case explicitly** with a single test file before the mass migration, and document the rule (likely: `+` strips if it has no LHS value-token; otherwise it's binary).

## SHA256 Byte-Identical Hash Gate

### Existing infrastructure [VERIFIED]

- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — runs showcase.flow twice in-process and `Assert.Equal(bytes1, bytes2)`. Uses `tests/output/phase18_showcase_run{1,2}.{wav,mid}`.
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` — same pattern for tutorial.flow.
- `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` — Phase 25 byte-identical guard.
- `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs` — tuning byte-identical guard.

These run the script in-process and substitute the output path. They are the **persistent guards** for byte-identical determinism. Per D-14, **Phase 26 does not add a sibling test.**

### Output paths [VERIFIED via `ls examples/output/`]

```
examples/output/flow_tutorial.wav
examples/output/flow_tutorial.mid
examples/output/flow_showcase.wav
examples/output/flow_showcase.mid
examples/output/long_demo.wav
examples/output/long_demo.mid
```

**Note:** filenames use `flow_` prefix (e.g., `flow_tutorial.wav`), NOT `tutorial.wav` as CONTEXT.md occasionally implied. CONTEXT.md is approximate; planner should use the verified names.

### Recommended hash gate protocol (during commit 2)

```bash
# Pre-migration (BEFORE running scripts/Migrate26)
dotnet run --project flow-interpreter examples/tutorial.flow
dotnet run --project flow-interpreter examples/showcase.flow
sha256sum examples/output/flow_tutorial.wav \
          examples/output/flow_tutorial.mid \
          examples/output/flow_showcase.wav \
          examples/output/flow_showcase.mid \
  > /tmp/hashes-pre-26.txt

# Run migration
dotnet run --project scripts/Migrate26 -- tests/ examples/ flow-lang/

# Rebuild (in case stdlib .flow files changed function signatures)
dotnet build

# Post-migration
dotnet run --project flow-interpreter examples/tutorial.flow
dotnet run --project flow-interpreter examples/showcase.flow
sha256sum examples/output/flow_tutorial.wav \
          examples/output/flow_tutorial.mid \
          examples/output/flow_showcase.wav \
          examples/output/flow_showcase.mid \
  > /tmp/hashes-post-26.txt

# Verify
diff /tmp/hashes-pre-26.txt /tmp/hashes-post-26.txt
# Empty diff = byte-identical = SHIP. Any difference = abort, investigate, bisect.
```

### Confidence in the gate

**HIGH** — the determinism contract is already battle-tested by Phase 18, 23, 25 byte-identical Facts. Phase 26 changes are **purely syntactic** at the source level — the AST evaluation produces identical Value sequences, which feed into identical Sequence/Buffer construction, which produces identical WAV/MIDI bytes. There is no PRNG involvement, no humanize, no tuning change.

**Edge case:** if Phase 26 inadvertently changes the order of side-effecting calls (e.g., `(add (sideEffect a) (sideEffect b))` vs `a + b`), output COULD differ. Mitigation: there are no side-effecting builtins in the migration path; arithmetic is pure.

## Common Pitfalls

### Pitfall 1: Musical-context blocks break silently

**What goes wrong:** `tempo -120 { ... }` — after Phase 26 lexer, `-120` lexes as `IntLiteral(-120)`, but `Parser.cs:450-453` expects to see `Minus` then `IntLiteral`. The `Match(TokenType.Minus)` fails (no Minus token), then `Check(TokenType.IntLiteral)` succeeds, but `tempoSign` stays `1` and the result is wrong (positive 120 instead of -120) — though semantically `tempo -120` is illegal anyway, so the bug is moot. But `pan -0.5 { ... }` IS legal and SHOULD remain working.

**Why it happens:** The lexer's expression-start gate currently includes `Tempo`, `Swing`, `Pan`, `Gain`, `ReverbTime` keywords as "previous emitted token" — but those keywords are followed by a context-block-value, not a generic expression. The new gate must EXCLUDE these.

**How to avoid:** Explicitly EXCLUDE music-context keywords from the `_lastEmittedType` expression-start set. After `tempo`/`swing`/`pan`/`gain`/`reverbTime`, `Minus`/`Plus` must continue to lex as standalone tokens. The musical-context block parser at lines 450–567 then consumes them itself.

**Warning signs:** Any test exercising `pan -0.5`, `gain -3.0`, `tempo` (just to make sure context blocks still work) failing post-Phase 26.

### Pitfall 2: `EvaluateFunctionCall` does not coerce — mixed-type arithmetic crashes

**What goes wrong:** `(add 5 3.0)` resolves to `(add Double Double)` via convertible scoring (+100). `argValues[0]` is `Value{Data=int 5, Type=IntType}`. `StdLib.AddDouble` calls `args[0].As<double>()` which does `Data is double` — false. **`InvalidCastException` thrown.**

**Why it happens:** Today, `EvaluateBinary` does its own type promotion before invoking the C# arithmetic primitives. Phase 26 routes the same calls through builtin overloads, but `EvaluateFunctionCall` doesn't have promotion logic — it just hands argValues directly to the impl.

**How to avoid:** Add coercion to `EvaluateFunctionCall` (Strategy A above). Specifically, after `_context.TryResolveFunction(call.Name, argTypes)` returns a non-null overload, walk argValues and call `.ConvertTo(matchedSig.InputTypes[i])` for each one whose type doesn't match. This fixes the latent bug across the entire builtin system, not just arithmetic.

**Warning signs:** Any new test mixing types — `(add 5 3.0)`, `(mul 2L 3.0)`, `(neg 5)` in a Long context — throwing `InvalidCastException` at runtime.

### Pitfall 3: Migration script not skipping note streams

**What goes wrong:** Inside `| C4 -3dB D4 |`, the migration script sees `Minus` between `NoteLiteral` and `DecibelLiteral`-or-`IntLiteral`, mis-classifies it as infix arithmetic, and emits `(sub C4 3dB)` — corrupting note streams.

**Why it happens:** Token walker doesn't track "am I inside `|...|`?".

**How to avoid:** The walker maintains a `_inNoteStream` flag toggled on `Pipe`. Inside note streams, ALL `Plus`/`Minus`/`Star`/`Slash` tokens are passed through unchanged.

**Warning signs:** `tests/test_note_streams.flow`, `tests/test_dx_arpeggio.flow`, `examples/showcase.flow` rendering wrong notes after migration. The byte-identical SHA256 gate catches this.

### Pitfall 4: `ParseUnary` deletion breaks array indexing

**What goes wrong:** `arr@-1` (negative index for last element). Today, `ParsePostfix` at line 774 calls `ParseUnary` to parse the index. `ParseUnary` handles unary `-`. After Phase 26, if the lexer emits `IntLiteral(-1)` (single token, since `@` is in the expression-start set OR... wait, is it?), `ParsePostfix` parses fine. But if the lexer DOESN'T emit `IntLiteral(-1)` (because `@` isn't in the expression-start gate), the parser sees `Minus, IntLiteral(1)` and fails.

**Why it happens:** `@` as expression-start is not explicitly in CONTEXT.md D-04's list. But it should be — it's where an index expression begins.

**How to avoid:** Either (a) add `TokenType.At` to the lexer's expression-start gate, or (b) keep a minimal `ParseUnary` shim that handles `-IDENT` → `(neg IDENT)` and `-Number → IntLiteral(-N)` as a fallback for residual `Minus` tokens reaching ParsePostfix.

**Recommendation:** Both. Add `At` to the gate AND keep the parser shorthand for `-IDENT`. The shorthand is needed regardless (per D-01).

**Warning signs:** `tests/test_slice_negative.flow` (Phase 20 DEFER-05) failing: `arr@-1` returning wrong index or parse error. The Phase 20 byte-identical infrastructure should catch this.

### Pitfall 5: Migration script can't find `SimpleLexer` (project reference broken)

**What goes wrong:** `scripts/Migrate26/Migrate26.csproj` is created with a wrong relative path to `flow-lang.csproj`, or the InternalsVisibleTo isn't set, or the lexer's required dependencies (`PragmaSet`, `ErrorReporter`) aren't reachable.

**Why it happens:** Cross-project references between a sibling `scripts/Migrate26/` and the existing `flow-lang/` need to traverse `..`.

**How to avoid:** `<ProjectReference Include="..\..\flow-lang\flow-lang.csproj" />`. `SimpleLexer`, `Token`, `ErrorReporter`, `PragmaSet` are all `public`. No `InternalsVisibleTo` needed. [VERIFIED: Token.cs, SimpleLexer.cs, ErrorReporter.cs all show `public class/record`.]

**Warning signs:** Build error "type or namespace `SimpleLexer` not found" when running `dotnet run --project scripts/Migrate26`.

### Pitfall 6: `std.flow` declarations missing for new builtins

**What goes wrong:** Phase 26 registers `(add Long Long)` in C# but forgets to add `internal proc add (Long: a, Long: b)` to `std.flow`. User scripts trying `(add 5L 6L)` fail with "function 'add' overload not found" — even though it's registered.

**Why it happens:** Lesson from Phase 25 D-25 (cited in CONTEXT.md Integration Points). `internal proc` decls in `std.flow` register the **parser-visible** signature; without them, the C# registration is invisible.

**How to avoid:** Plan must include explicit task "update `std.flow`". Cross-check: count of new C# `registry.Register` calls === count of new `internal proc` decls.

**Warning signs:** Tests that exercise new Long/Number builtins erroring with "no matching overload."

### Pitfall 7: `(div Int Int)` semantic change breaks existing scripts

**What goes wrong:** Phase 26 D-08 changes `(div Int Int)` to auto-promote to Double. Existing scripts that rely on integer truncation (e.g., `Int half = (div n 2)` where the user expected `Int`) suddenly receive a `Double`, and the var-decl coercion downcasts (lossy) — possibly silently changing semantics.

**Why it happens:** The asymmetry in D-08 is intentional but unannounced.

**How to avoid:** During migration, `grep -rE '\(div [a-zA-Z0-9_]+ [a-zA-Z0-9_]+\)'` over all `.flow` files post-migration. Visually inspect every `(div A B)` site to confirm:
- If both operands are `Int`-typed, decide whether the user wanted truncation (use `(idiv)`) or fractional (`(div)` is fine).
- The migration script could OPTIONALLY warn: `"line N: (div Int Int) auto-promotes — confirm intent or use (idiv)"`.

**Warning signs:** Tests that compute integer halving/doubling producing fractional results. Phase 18 + 25 byte-identical tests will catch any showcase/tutorial regression.

### Pitfall 8: Showcase already uses prefix — migration is mostly a tutorial-and-stdlib problem

**What goes wrong:** Effort budgeted for "82 file migration" turns out to be mostly idempotent (no changes needed). The actual migration work is concentrated in `tutorial.flow` (multi-line dual-style chapter) and `flow-lang/audio.flow`, `flow-lang/composition.flow` — maybe 10 files with substantive changes.

**How to avoid:** Treat this as good news. Plan time for handling the dense cases (tutorial chapter rewrite) carefully, and keep the migration sweep itself a single `dotnet run`.

### Pitfall 9: Lambda body infix breaks in CLAUDE.md examples

**What goes wrong:** CLAUDE.md line 148: `Lambda functions: fn Int x => x * 2, fn Int a, Int b => a + b` — these examples use INFIX. After Phase 26, this code is a parse error.

**How to avoid:** D-14 / commit 3 explicitly updates this. Recommended replacement:
```
- Lambda functions: `fn Int x => (mul x 2)`, `fn Int a, Int b => (add a b)`
```

**Warning signs:** A composer reading CLAUDE.md, copying the lambda example, getting a parse error — the worst possible developer-experience outcome.

### Pitfall 10: Number type's missing `CanConvertTo` override

**What goes wrong:** `NumberType.cs` has no `CanConvertTo` override [VERIFIED: NumberType.cs lines 1-17 — no override]. Per the base `FlowType.CanConvertTo`, this returns `IsCompatibleWith(target)` which returns `Equals(target)` — meaning `Number CANNOT convert to anything else`. Mixed-type calls like `(add 5N 3.0)` would resolve to `(add Number Number)` via Double→Number convertible (+100), and the result is correctly Number — fine. But `(add 5N 3)` would resolve to `(add Number Number)` via Int→Number, and the Int gets coerced to Number, also fine. **The asymmetric chain works because Number is the widest.**

But: if any caller expects `Number → Double` (e.g., feeding a Number into a `(sin Double)` builtin), it can't convert. This is a pre-existing limitation, not a Phase 26 regression. Document as out-of-scope.

## Code Examples

### Example 1: Existing arithmetic registration pattern

```csharp
// flow-lang/StandardLibrary/BuiltInFunctions.cs:212-216 (existing — VERIFIED)
var addIntSignature = new FunctionSignature(
    "add",
    [IntType.Instance, IntType.Instance]);
registry.Register("add", addIntSignature, StdLib.AddInt);

// flow-lang/StandardLibrary/StdLib.cs:176-181 (existing — VERIFIED)
public static Value AddInt(IReadOnlyList<Value> args)
{
    var a = args[0].As<int>();
    var b = args[1].As<int>();
    return Value.Int(a + b);
}
```

### Example 2: Number-type arithmetic via BigInteger (existing — to be preserved)

```csharp
// flow-lang/Interpreter/ExpressionEvaluator.cs:266-278 (existing — VERIFIED)
if (left.Type is NumberType || right.Type is NumberType)
{
    BigInteger l = left.Data is BigInteger bl ? bl : new BigInteger(Convert.ToInt64(left.Data));
    BigInteger r = right.Data is BigInteger br ? br : new BigInteger(Convert.ToInt64(right.Data));
    return bin.Operator switch
    {
        BinaryOperator.Add => Value.Number(l + r),
        ...
    };
}
```

This pattern (`Data is BigInteger bl ? bl : new BigInteger(Convert.ToInt64(left.Data))`) is the model for the new `StdLib.AddNumber` — but with the coercion handled at the invoker boundary (Strategy A above), the helper simplifies to just:
```csharp
public static Value AddNumber(IReadOnlyList<Value> args)
    => Value.Number(args[0].As<BigInteger>() + args[1].As<BigInteger>());
```

### Example 3: Existing typed-literal lex pattern (to extend)

```csharp
// flow-lang/Lexing/SimpleLexer.cs:81-86 (existing — VERIFIED)
if ((c == '+' || c == '-') && char.IsDigit(PeekNext()))
{
    var lookahead = TryLookAheadSpecialLiteral();
    if (lookahead != null)
        return lookahead;
}
// Falls through to SingleChar(Plus/Minus) at lines 108-109
```

Phase 26 adds a parallel `TryLexSignedNumber()` after the typed-literal try.

## State of the Art

This is an internal-language refactor — no external "state of the art" applies. All references are within the existing codebase.

**Within-codebase precedent for similar work:**

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Pre-Phase 21: pragmas via parser sniffing | Phase 21 PragmaScanner pre-pass over token stream | 2026-04-26 | Pre-pass pattern is the model for Phase 26 migration script (lexer-only, no parser/eval) |
| Pre-Phase 18: power-of-2 durations only | Phase 18 Fraction-backed duration with byte-identical regression Facts | 2026-04-26 | Established the byte-identical regression contract Phase 26 inherits |
| Pre-Phase 25: humanize() uniform PRNG only | Phase 25 humanizeGaussian() side-by-side; preserves uniform path byte-identical | 2026-05-04 | Established the "preserve byte-identical" pattern via SHA256 hash gate, sister to D-14 |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `dotnet-script` is not installed and unlikely to be a clean-checkout dependency | Migration Tool Form Factor | If user has `dotnet-script` and prefers `.csx`, switch is trivial |
| A2 | `ConvertTo` strategy A (coerce at invocation boundary) does not break any existing builtin | Mixed-Type Coercion Boundary | Audit needed; Void-typed wildcards verified safe but other edge cases possible |
| A3 | `_lastEmittedType` exclusion of music-context keywords (Tempo/Swing/Pan/Gain/ReverbTime) is sufficient to preserve those blocks' sign handling | Pitfall 1 | If any other context-keyword path is missed, `pan -0.5` style still breaks |
| A4 | Inside `\| ... \|` note streams, the existing typed-literal handling covers all signed-literal cases that matter (no plain `-3` without a suffix appears in real note streams) | Lexer Extension | If there are existing note-stream call sites with plain `-N`, they're broken; SHA256 gate catches them via showcase/tutorial |
| A5 | Migration script's precedence climber correctly handles every infix expression in 41 source files including nested parens, unary minus on non-literal expressions, and subscript `arr@N` interactions | Migration Script — Architecture | Wrong rewrite produces parse error post-migration → bisect to faulty file |
| A6 | `(div Int Int) → Double` semantic change does not silently corrupt any existing test output | Pitfall 7 | Phase 18 + 25 SHA256 gates catch tutorial/showcase regressions; other tests' .flow scripts may need manual review (mass `grep '(div ' tests/*.flow`) |
| A7 | The 41 file count (loose grep) is the upper bound; actual migration touches ~38 files after excluding `//` comment matches | Migration Targets | Underestimated count → larger commit 2 diff than expected (cosmetic, not functional) |
| A8 | `flow-lsp/` has zero `BinaryExpression` references and updates "automatically" via shared lexer/parser dependency | CONTEXT.md "no flow-lsp touch" | [VERIFIED via grep — zero hits — A8 promoted to confirmed fact] |
| A9 | `flow-lang.Tests/` test fixtures have zero hardcoded `BinaryExpression` references | CONTEXT.md | [VERIFIED via grep — zero hits — A9 promoted to confirmed fact] |
| A10 | The `std.flow` Float-arithmetic asymmetry (only `add Float` declared, missing sub/mul/div) is a latent bug; Phase 26 incidentally fixes it by ensuring all 5 same-type decls are present | std.flow Declarations | If the asymmetry was intentional (e.g., type-inference relied on Float being silently absent), unexpected resolution paths could change |

## Open Questions

1. **Should `Colon` truly be in the expression-start gate?**
   - What we know: CONTEXT.md D-04 lists `Colon`. Flow uses `:` in proc parameter declarations (`Int: x`) and in note-stream chord brackets and somewhere else.
   - What's unclear: Is there ANY position where a negative literal directly follows `:`?
   - Recommendation: Planner traces concrete examples. Likely safe to drop `Colon` — proc params are followed by an identifier, not a literal. If no example surfaces, omit `Colon` from the gate to reduce risk.

2. **What's the preferred handling for `+x` vs `+5`?**
   - What we know: D-03 says both are no-ops. `+5` becomes `5`; `+x` becomes `x`.
   - What's unclear: Lexer-side or parser-side? Per Pattern 4 above, lexer absorbs `+5` (returns `IntLiteral(5)`); parser strips `+` before identifier. Two implementations.
   - Recommendation: Lexer absorbs `+number`, parser strips `+identifier`. Two-place handling matches the asymmetry between literal-token and identifier-token contexts.

3. **Migration script: should it WARN on `(div Int Int)` ambiguity?**
   - What we know: Pitfall 7 surfaces the semantic change.
   - What's unclear: Whether the user wants explicit warnings vs silent migration.
   - Recommendation: Plan a flag `--warn-int-div` that prints suggestions but doesn't change behavior. Default off. Spot-check after migration.

4. **Should the Phase 26 plan address the `EvaluateFunctionCall` coercion bug as a separate fix or bundled?**
   - What we know: The coercion bug is independent of Phase 26 but Phase 26 first exposes it via the convertible-scoring fallback path.
   - What's unclear: User may want a separate phase / commit for it.
   - Recommendation: Bundle into commit 1. The fix is small (~5 lines) and Phase 26's overload strategy depends on it. Mark as "Phase 26 incidental fix: invocation-boundary coercion."

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All commits (build, run interpreter, run migration script) | ✓ | net10.0 (project target) | None — required |
| `dotnet` CLI | Build + script invocation | ✓ | (whatever .NET 10 ships) | None — required |
| `dotnet-script` (`.csx` runner) | Optional alternative migration script form | ✗ | — | **Use standalone csproj** at `scripts/Migrate26/` |
| `sha256sum` | SHA256 hash gate during commit 2 | ✓ (Linux base utils) | — | `openssl dgst -sha256` if absent |
| `cmp` / `diff` | Hash comparison | ✓ (Linux base utils) | — | inline shell `if [ ... ]` |
| `git` | Commits | ✓ | — | None |
| PulseAudio | NOT required for migration; required only if `(play)`/`(loop)` runs (none in tutorial/showcase pre-migration verification) | n/a — tutorial.flow uses `writeWav`, not `play` | — | None needed |

**Missing dependencies with fallback:** `dotnet-script` — switch to standalone csproj.

**Missing dependencies, blocking:** None.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit + custom `FlowEngineRunner` test fixture |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcase\|Phase18.ByteIdenticalTutorial\|Phase25.ByteIdenticalShowcaseGaussian"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| STD-01 | `BinaryExpression` symbol absent from compiled solution | static | `! grep -rn "BinaryExpression\|BinaryOperator" flow-lang/ flow-lsp/ flow-midi/` post-commit-1 | ✅ shell command |
| STD-01 | Bare infix `(a + b)` produces parse error | unit | New `flow-lang.Tests/Unit/Phase26/InfixRejectedFacts.cs` (recommended) — Theory `[InlineData("Int x = 1 + 2")]` asserts `errorCount > 0` | ❌ Wave 0 |
| STD-02 | Each new same-type overload (Long/Number for add/sub/mul/div) produces correct result | unit | New `flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs` — Facts for `(add 5L 6L) == 11L`, `(mul 1000000000000N 2N) == 2000000000000N`, etc. | ❌ Wave 0 |
| STD-02 | `(neg)` 5-pack produces correct sign-flipped value | unit | `flow-lang.Tests/Unit/Phase26/NegOverloadFacts.cs` — 5 Facts | ❌ Wave 0 |
| STD-02 | `(idiv 1 2) == 0`, `(div 1 2) == 0.5` | unit | `flow-lang.Tests/Unit/Phase26/IntegerDivisionFacts.cs` — 2 Facts | ❌ Wave 0 |
| STD-02 | Mixed-type widening: `(add Float Double) → Double`, `(mul Long Number) → Number` | unit | `flow-lang.Tests/Unit/Phase26/MixedTypeArithmeticFacts.cs` — 6 Facts (one per widening pair) | ❌ Wave 0 |
| STD-02 | Negative literal lexes as single token at expression-start positions | unit | `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs` — Theory matrix (6 lex positions × 2 number types = 12 rows) | ❌ Wave 0 |
| STD-02 | `-x` parser shorthand → `(neg x)` | unit | `flow-lang.Tests/Unit/Phase26/UnaryMinusShorthandFacts.cs` — Fact | ❌ Wave 0 |
| STD-03 | All 97 in-repo `.flow` files run without errors | smoke | `for f in tests/*.flow; do dotnet run --project flow-interpreter "$f" || echo "FAIL: $f"; done` | ✅ shell loop |
| STD-03 | tutorial.flow + showcase.flow byte-identical across 2 runs | integration | `dotnet test --filter "Phase18.ByteIdentical"` | ✅ existing |
| STD-03 | tutorial.flow + showcase.flow byte-identical PRE vs POST migration | one-shot | sha256sum gate during commit 2 (manual procedure documented in plan) | ✅ shell |
| STD-03 | Build succeeds | build | `dotnet build` | ✅ existing |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet test --filter "Phase26"` (when Phase 26 unit tests exist)
- **Per wave merge:** `dotnet test` (full suite ≥287 Facts post-Phase 25)
- **Phase gate (commit 2):** SHA256 hash diff empty
- **Phase gate (commit 3):** Full suite green; `grep -rn "BinaryExpression"` empty across solution

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs` — covers STD-02 same-type fast paths
- [ ] `flow-lang.Tests/Unit/Phase26/NegOverloadFacts.cs` — covers STD-02 (neg) 5-pack
- [ ] `flow-lang.Tests/Unit/Phase26/IntegerDivisionFacts.cs` — covers STD-02 (idiv) + (div) Int/Int promotion
- [ ] `flow-lang.Tests/Unit/Phase26/MixedTypeArithmeticFacts.cs` — covers STD-02 OverloadResolver convertible-scoring path
- [ ] `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs` — covers STD-02 lexer 6-position matrix
- [ ] `flow-lang.Tests/Unit/Phase26/UnaryMinusShorthandFacts.cs` — covers D-01 parser shorthand
- [ ] `flow-lang.Tests/Unit/Phase26/InfixRejectedFacts.cs` — covers STD-01 (bare infix produces parse error)
- [ ] `scripts/Migrate26/Migrate26.csproj` — migration tool entry point
- [ ] `scripts/Migrate26/Program.cs` — token walker + precedence climber

*(Existing test infrastructure — Phase 18, 23, 25 byte-identical Facts — covers STD-03 byte-identical regression. No NEW byte-identical Facts needed per D-14.)*

## Security Domain

> Skipped — `security_enforcement` is not configured for this project, AND Phase 26 has no external surface, no input validation concerns, no auth, no crypto. The phase is a pure-internal interpreter refactor.

## Sources

### Primary (HIGH confidence)
- `flow-lang/Ast/Expressions/BinaryExpression.cs` — full file content (record + enum, 20 lines)
- `flow-lang/Parsing/Parser.cs:113-174,440-570,650-790,920-960,1097-1162` — verified parser shape, musical-context Plus/Minus consumption, IsArgumentStart helper
- `flow-lang/Interpreter/ExpressionEvaluator.cs:31-56,170-215,250-358` — verified Evaluate switch, EvaluateFunctionCall (NO coercion), EvaluateBinary body
- `flow-lang/Lexing/SimpleLexer.cs:1-200,319-438,880-922` — verified lexer Tokenize loop, TryLookAheadSpecialLiteral, Peek/Advance helpers
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:190-353` — verified all existing arithmetic registrations
- `flow-lang/StandardLibrary/StdLib.cs:170-294` — verified all StdLib arithmetic helpers
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — full file, verified TryGetImplementation (no coercion), SignaturesMatch, Register
- `flow-lang/Runtime/Value.cs:1-228` — verified Value factory methods, ConvertTo (full numeric chain), As<T> (NO coercion)
- `flow-lang/TypeSystem/OverloadResolver.cs` — full file, verified Resolve scoring 1000/500/100, ambiguity detection
- `flow-lang/TypeSystem/FunctionSignature.cs` — full file, verified Matches + CalculateSpecificity
- `flow-lang/TypeSystem/PrimitiveTypes/{Int,Long,Float,Double,Number}Type.cs` — verified CanConvertTo chain
- `flow-lang/std.flow` — full file, verified existing arithmetic block at lines 38-49 + asymmetry (missing sub/mul/div Float)
- `examples/tutorial.flow` — verified dual-style chapter 2; output paths flow_tutorial.{wav,mid}
- `examples/showcase.flow` — verified ~zero infix arithmetic
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — verified byte-identical test pattern
- `examples/output/` directory listing — verified output filenames
- `dotnet tool list -g` — verified `dotnet-script` is NOT installed
- `find ... -name "*.flow"` — verified 97 total in-repo files (87 tests + 3 examples + 7 stdlib)

### Secondary (MEDIUM confidence)
- CONTEXT.md `<canonical_refs>` section — line numbers approximately match my own grep but planner should re-verify before each edit (line numbers can drift)
- ROADMAP.md Phase 26 success criteria — referenced verbatim into `<phase_requirements>`

### Tertiary (LOW confidence)
- None — every claim in this research was verified against live code or live tooling.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every library/version is confirmed in the existing csproj/source
- Architecture (parser/AST surgery scope): HIGH — full grep confirms zero off-target hits
- Lexer extension (negative literal positions): MEDIUM — D-04's `Colon` membership is ambiguous; expression-start vs context-block-start distinction needs careful per-keyword exclusion
- Builtin overload registration: HIGH — pattern is mechanical reproduction of existing add/sub/mul/div Int/Float/Double registrations
- Mixed-type coercion landmine: HIGH — the bug is verified concretely; Strategy A fix is the cleanest mitigation
- Migration script: MEDIUM — the token-walker shape is clear; the precedence climber + edge cases (optional-paren func call args, nested parens with unary minus) need careful one-file-at-a-time verification before mass apply
- SHA256 gate: HIGH — protocol is identical to Phase 18/25 patterns; output paths verified
- CLAUDE.md updates: HIGH — exact line numbers verified

**Research date:** 2026-05-04
**Valid until:** 2026-05-18 (14 days; codebase is actively evolving — verify line numbers on Parser.cs before each edit)
