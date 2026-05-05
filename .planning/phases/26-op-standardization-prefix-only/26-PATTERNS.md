# Phase 26: Op Standardization (Prefix-Only) - Pattern Map

**Mapped:** 2026-05-04
**Files analyzed:** 16 (7 modified, 9 created)
**Analogs found:** 16 / 16 (100% coverage — every file has a strong in-repo analog)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| **MODIFIED FILES (7)** | | | | |
| `flow-lang/Ast/Expressions/BinaryExpression.cs` | ast-node | (deletion) | n/a — entire file is removed | n/a |
| `flow-lang/Parsing/Parser.cs` | parser | recursive-descent / token-stream | `flow-lang/Parsing/Parser.cs:765-789` (`ParsePostfix`, the destination of the rewire) | self-analog |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | interpreter | switch-dispatch + value coercion | `flow-lang/Runtime/Value.cs:84-194` (`ConvertTo` — the helper the new ~5-line coercion calls) | exact (calls the helper) |
| `flow-lang/Lexing/SimpleLexer.cs` | lexer | char-stream → token-stream | `flow-lang/Lexing/SimpleLexer.cs:319-438` (`TryLookAheadSpecialLiteral`) | exact (same look-ahead+rewind shape) |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | registration / DI | static-init / table-build | `flow-lang/StandardLibrary/BuiltInFunctions.cs:212-271` (existing add/sub/mul/div Int+Float+Double) | exact (mechanical reproduction) |
| `flow-lang/StandardLibrary/StdLib.cs` | utility / pure-function | request-response (in-args→out-Value) | `flow-lang/StandardLibrary/StdLib.cs:176-294` (existing arithmetic helpers) | exact |
| `flow-lang/std.flow` | config / declaration | static manifest | `flow-lang/std.flow:38-49` (existing arithmetic block) | exact |
| `CLAUDE.md` | docs | n/a | self-analog (other AST-table rows) | exact |
| **CREATED FILES (9)** | | | | |
| `flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs` | test (unit Fact) | request-response | `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` (full-engine via FlowEngineRunner) | role-match |
| `flow-lang.Tests/Unit/Phase26/NegOverloadFacts.cs` | test (unit Fact) | request-response | `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs:81-98` (`CallHumanizeGaussian` registry-direct path) | exact (preferred) |
| `flow-lang.Tests/Unit/Phase26/IntegerDivisionFacts.cs` | test (unit Fact) | request-response | same as NegOverloadFacts | exact |
| `flow-lang.Tests/Unit/Phase26/MixedTypeArithmeticFacts.cs` | test (unit Fact) | request-response | same as NegOverloadFacts | exact |
| `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs` | test (unit Fact, Theory) | request-response | `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs:20-62` (Theory + InlineData matrix) | exact |
| `flow-lang.Tests/Unit/Phase26/UnaryMinusShorthandFacts.cs` | test (unit Fact) | request-response | `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs:38-58` (FlowEngineRunner stdout-substring assertion) | exact |
| `flow-lang.Tests/Unit/Phase26/InfixRejectedFacts.cs` | test (unit Fact, Theory) | request-response | `HAliasFacts.cs` + `PragmaScannerFacts.EnableAfterStatement_RaisesError` (line 107-116) | exact |
| `scripts/Migrate26/Migrate26.csproj` | build / config | static manifest | `flow-lang/flow-lang.csproj` (project shape) + `flow-lang.Tests/flow-lang.Tests.csproj` (`<ProjectReference>` syntax) | role-match |
| `scripts/Migrate26/Program.cs` | tool / token-walker | batch-transform (file→file) | `flow-lang/Lexing/SimpleLexer.cs` (re-used as library) — no other migration scripts in repo | greenfield (no script analog; uses lexer as library) |

---

## Pattern Assignments

### P-01 — `BinaryExpression.cs` (delete entire file)

**Action:** Delete the file outright. No analog needed — it is removal, not creation.

**Verification after delete:**
```bash
! grep -rn "BinaryExpression\|BinaryOperator" flow-lang/ flow-lsp/ flow-midi/
```
Must produce zero hits across the solution per RESEARCH §"Files referencing `BinaryExpression`" (verified zero off-target hits).

**Current contents** (`flow-lang/Ast/Expressions/BinaryExpression.cs:1-20`, full file):
```csharp
using FlowLang.Core;
namespace FlowLang.Ast.Expressions;
public record BinaryExpression(
    SourceLocation Location,
    Expression Left,
    BinaryOperator Operator,
    Expression Right) : Expression(Location);

public enum BinaryOperator
{
    Add, Subtract, Multiply, Divide
}
```

---

### P-02 — `Parser.cs` (delete `ParseAdditive` / `ParseMultiplicative`; rewire callers; add `-IDENT` shorthand)

**Analog:** Self — `ParsePostfix` at lines 765-789 is the rewire destination.

**Imports already present** (no changes):
```csharp
// flow-lang/Parsing/Parser.cs:1-15 (header — no new imports needed for this edit)
```

**Existing `ParseAdditive` to DELETE** (`flow-lang/Parsing/Parser.cs:713-728`):
```csharp
private Expression ParseAdditive()
{
    var left = ParseMultiplicative();
    while (Match(TokenType.Plus, TokenType.Minus))
    {
        var op = PreviousToken.Type == TokenType.Plus
            ? BinaryOperator.Add
            : BinaryOperator.Subtract;
        var location = PreviousToken.Location;
        var right = ParseMultiplicative();
        left = new BinaryExpression(location, left, op, right);
    }
    return left;
}
```

**Existing `ParseMultiplicative` to DELETE** (`Parser.cs:730-745`):
```csharp
private Expression ParseMultiplicative()
{
    var left = ParseUnary();
    while (Match(TokenType.Star, TokenType.Slash))
    {
        var op = PreviousToken.Type == TokenType.Star
            ? BinaryOperator.Multiply
            : BinaryOperator.Divide;
        var location = PreviousToken.Location;
        var right = ParseUnary();
        left = new BinaryExpression(location, left, op, right);
    }
    return left;
}
```

**Existing `ParseUnary` arithmetic branch to DELETE** (`Parser.cs:747-763`):
```csharp
private Expression ParseUnary()
{
    if (Match(TokenType.Minus, TokenType.Plus))
    {
        var op = PreviousToken.Type == TokenType.Minus
            ? BinaryOperator.Subtract
            : BinaryOperator.Add;
        var location = PreviousToken.Location;
        var right = ParseUnary();
        var zero = new LiteralExpression(location, 0);  // ← 0 ± x trick — gone
        return new BinaryExpression(location, zero, op, right);
    }
    return ParsePostfix();
}
```

**Replace `ParseUnary` with `ParseUnaryShorthand`** (D-01 + D-03; from RESEARCH Pattern 4):
```csharp
// flow-lang/Parsing/Parser.cs — replaces deleted ParseUnary
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

**Three caller-site rewires** (`Parser.cs:668, 689, 774, 940`):
- Line 668 (`ParseFlowExpression`): `var left = ParseAdditive()` → `var left = ParseUnaryShorthand()` (or `ParsePostfix()` if shorthand collapses)
- Line 689 (flow-args): `args.Add(ParseAdditive())` → `args.Add(ParseUnaryShorthand())`
- Line 774 (`ParsePostfix` array-index): `var index = ParseUnary()` → `var index = ParseUnaryShorthand()`
- Line 940 (optional-paren args loop): `args.Add(ParseUnary())` → `args.Add(ParseUnaryShorthand())`

**PRESERVE musical-context Plus/Minus consumers** (`Parser.cs:121, 130, 140, 450, 451, 465, 466, 527, 528, 542, 543, 556`):
The code at `Parser.cs:446-568` (Tempo/Swing/Pan/Gain/ReverbTime context-block parsing) consumes `Match(TokenType.Minus)` / `Match(TokenType.Plus)` directly. **Verbatim sample to preserve** (`Parser.cs:446-458`, Tempo case):
```csharp
case MusicalContextType.Tempo:
{
    int tempoSign = 1;
    var tempoLoc = CurrentToken.Location;
    if (Match(TokenType.Minus)) tempoSign = -1;
    else if (Match(TokenType.Plus)) tempoSign = 1;
    if (Check(TokenType.IntLiteral))
        value = new LiteralExpression(tempoLoc, tempoSign * (int)Advance().Value!);
    ...
}
```
**This pattern repeats at Swing(461), Pan(523), Gain(538), ReverbTime(553).** All five must remain working post-Phase-26. The lexer side handles this by EXCLUDING music-context keywords from the negative-literal expression-start gate (see P-04 below).

---

### P-03 — `ExpressionEvaluator.cs` (delete `EvaluateBinary` + switch case; add ~5 lines of coercion)

**Existing switch case to DELETE** (`flow-lang/Interpreter/ExpressionEvaluator.cs:39`):
```csharp
public virtual Value Evaluate(Expression expr)
{
    return expr switch
    {
        LiteralExpression lit => EvaluateLiteral(lit),
        VariableExpression var => EvaluateVariable(var),
        FunctionCallExpression call => EvaluateFunctionCall(call),
        ArrayIndexExpression idx => EvaluateArrayIndex(idx),
        BinaryExpression bin => EvaluateBinary(bin),     // ← DELETE this line
        ArrayLiteralExpression arrLit => EvaluateArrayLiteral(arrLit),
        ...
```

**Entire `EvaluateBinary` method (lines 250-335) DELETED** — see P-01 verification grep.

**ADD coercion in `EvaluateFunctionCall`** (`ExpressionEvaluator.cs:170-215`). This fixes the D-05 mixed-type landmine (RESEARCH Pitfall 2). The current method:
```csharp
private Value EvaluateFunctionCall(FunctionCallExpression call)
{
    var argValues = call.Arguments.Select(Evaluate).ToList();
    var argTypes = argValues.Select(v => v.Type).ToList();
    var overload = _context.TryResolveFunction(call.Name, argTypes);
    ...
    if (overload.IsInternal)
    {
        return overload.Implementation!(argValues);   // ← UNCONDITIONAL pass-through (the bug)
    }
    ...
}
```

**Insert ~5 lines BEFORE `Implementation!(argValues)`** (RESEARCH §"Mixed-Type Coercion Boundary"):
```csharp
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

**Why `Value.ConvertTo` is the helper:** verified at `flow-lang/Runtime/Value.cs:84-194` — already implements full Int → Long → Float → Double → Number widening chain, plus Note↔Semitone, time conversions, and Void[]→T[]. **No new conversion logic needed.**

**Risk audit:** `Void`-typed wildcard parameters (e.g., `equals (Void: a, Void: b)` at `std.flow:67`) never trigger coercion because `VoidType.IsCompatibleWith` returns false → `CanConvertTo` returns false → the `if`-guard short-circuits. [VERIFIED RESEARCH §"Risk of strategy A".]

---

### P-04 — `SimpleLexer.cs` (add `_lastEmittedType` field + `TryLexSignedNumber` helper)

**Analog:** `TryLookAheadSpecialLiteral` at `SimpleLexer.cs:319-438`.

**Existing imports** (`SimpleLexer.cs:1-7` — no new imports):
```csharp
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.SpecialTypes;
using System.Text;
namespace FlowLang.Lexing;
```

**Add field near existing state** (`SimpleLexer.cs:14-21`):
```csharp
public class SimpleLexer
{
    private readonly string _source;
    private readonly ErrorReporter _errorReporter;
    private readonly string? _fileName;
    private readonly PragmaSet _pragmaSet;
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;
    private readonly Queue<Token> _pendingTokens = new();
    private TokenType? _lastEmittedType = null;   // NEW — Phase 26 D-04
```

**Modify `Tokenize` to track emissions** (`SimpleLexer.cs:34-50`):
```csharp
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
            _lastEmittedType = token.Type;   // NEW
        }
    }
    tokens.Add(new Token(TokenType.Eof, "", new SourceLocation(_line, _column, _fileName)));
    return tokens;
}
```

**Existing sign-handling block to EXTEND** (`SimpleLexer.cs:78-86`):
```csharp
// Check for special literals that start with +/- before treating them as operators
// Semitones: +/-Nst (e.g., +1st, -5st)
// Decibels: +/-NdB (e.g., +6dB, -3dB)
if ((c == '+' || c == '-') && char.IsDigit(PeekNext()))
{
    var lookahead = TryLookAheadSpecialLiteral();
    if (lookahead != null)
        return lookahead;
    // NEW: try plain signed-number at expression-start
    var signed = TryLexSignedNumber(start);
    if (signed != null) return signed;
}
// Falls through to SingleChar(Plus/Minus) at lines 108-109
```

**New helper** (insert near line 319, parallel to `TryLookAheadSpecialLiteral`). Look-ahead+rewind shape copied from existing helper at `SimpleLexer.cs:319-438`:
```csharp
private Token? TryLexSignedNumber(SourceLocation start)
{
    // D-04: expression-start positions only.
    // EXCLUDE music-context keywords (Tempo, Swing, Pan, Gain, ReverbTime) so that
    // `pan -0.5 { ... }` continues to lex `-` as a standalone Minus token —
    // the musical-context block parser at Parser.cs:450/465/527/542/556 consumes
    // Match(TokenType.Minus) explicitly. (RESEARCH Pitfall 1.)
    bool isExprStart = _lastEmittedType is null
        or TokenType.LParen
        or TokenType.Comma
        or TokenType.LBracket
        or TokenType.Arrow
        or TokenType.Assign
        or TokenType.Pipe
        or TokenType.Semicolon;
    // NOTE: Colon is intentionally NOT in this set — proc params (Int: x) are
    // followed by an identifier, never a literal. (RESEARCH Open Question 1.)
    if (!isExprStart) return null;

    int savePos = _position;
    int saveLine = _line;
    int saveCol = _column;

    char sign = Peek();
    if (sign != '+' && sign != '-') return null;
    if (!char.IsDigit(PeekNext())) return null;

    // D-03: '+' at expression-start is silently absorbed — return positive literal.
    bool negative = (sign == '-');
    Advance();   // consume sign

    // Parallel to ScanNumber at SimpleLexer.cs:440+: scan digits and optional decimal.
    var sb = new StringBuilder();
    if (negative) sb.Append('-');
    while (!IsAtEnd() && char.IsDigit(Peek()))
        sb.Append(Advance());

    bool isFloat = false;
    if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
    {
        isFloat = true;
        sb.Append(Advance());                 // consume '.'
        while (!IsAtEnd() && char.IsDigit(Peek()))
            sb.Append(Advance());
    }

    string text = sb.ToString();
    if (isFloat && double.TryParse(text, out double dval))
        return new Token(TokenType.FloatLiteral, text, start, dval);
    if (!isFloat && int.TryParse(text, out int ival))
        return new Token(TokenType.IntLiteral, text, start, ival);

    // Parse failure — rewind so SingleChar(Plus/Minus) gets the chance.
    _position = savePos;
    _line = saveLine;
    _column = saveCol;
    return null;
}
```

**Look-ahead + rewind discipline** mirrors existing `TryLookAheadSpecialLiteral` lines 322-339 — save `_position`/`_line`/`_column`, attempt match, restore on failure. **Verbatim rewind block** to copy:
```csharp
// SimpleLexer.cs:336-339 (the "rewind on no match" idiom — used identically in Phase 26 helper)
_position = savePos;
_line = saveLine;
_column = saveCol;
return null;
```

---

### P-05 — `BuiltInFunctions.cs` (add 8 + 5 + 1 = 14 new registrations, 1 modified impl)

**Analog (verbatim template):** `BuiltInFunctions.cs:212-271`. Each per-type overload follows the exact same shape:
```csharp
// flow-lang/StandardLibrary/BuiltInFunctions.cs:212-216 (existing — verified)
var addIntSignature = new FunctionSignature(
    "add",
    [IntType.Instance, IntType.Instance]);
registry.Register("add", addIntSignature, StdLib.AddInt);
```

**Insert immediately AFTER line 271** (per CONTEXT D-Discretion: Long after Float, Number last):
```csharp
// ===== Phase 26 (STD-02): Long + Number same-type fast paths =====

var addLongSignature = new FunctionSignature("add", [LongType.Instance, LongType.Instance]);
registry.Register("add", addLongSignature, StdLib.AddLong);
var subLongSignature = new FunctionSignature("sub", [LongType.Instance, LongType.Instance]);
registry.Register("sub", subLongSignature, StdLib.SubLong);
var mulLongSignature = new FunctionSignature("mul", [LongType.Instance, LongType.Instance]);
registry.Register("mul", mulLongSignature, StdLib.MulLong);
var divLongSignature = new FunctionSignature("div", [LongType.Instance, LongType.Instance]);
registry.Register("div", divLongSignature, StdLib.DivLong);

var addNumberSignature = new FunctionSignature("add", [NumberType.Instance, NumberType.Instance]);
registry.Register("add", addNumberSignature, StdLib.AddNumber);
var subNumberSignature = new FunctionSignature("sub", [NumberType.Instance, NumberType.Instance]);
registry.Register("sub", subNumberSignature, StdLib.SubNumber);
var mulNumberSignature = new FunctionSignature("mul", [NumberType.Instance, NumberType.Instance]);
registry.Register("mul", mulNumberSignature, StdLib.MulNumber);
var divNumberSignature = new FunctionSignature("div", [NumberType.Instance, NumberType.Instance]);
registry.Register("div", divNumberSignature, StdLib.DivNumber);

// ===== Phase 26 (STD-02): (neg) 5-pack =====
var negIntSignature    = new FunctionSignature("neg", [IntType.Instance]);
registry.Register("neg", negIntSignature, StdLib.NegInt);
var negLongSignature   = new FunctionSignature("neg", [LongType.Instance]);
registry.Register("neg", negLongSignature, StdLib.NegLong);
var negFloatSignature  = new FunctionSignature("neg", [FloatType.Instance]);
registry.Register("neg", negFloatSignature, StdLib.NegFloat);
var negDoubleSignature = new FunctionSignature("neg", [DoubleType.Instance]);
registry.Register("neg", negDoubleSignature, StdLib.NegDouble);
var negNumberSignature = new FunctionSignature("neg", [NumberType.Instance]);
registry.Register("neg", negNumberSignature, StdLib.NegNumber);

// ===== Phase 26 (STD-02): (idiv Int Int) → Int =====
var idivIntSignature = new FunctionSignature("idiv", [IntType.Instance, IntType.Instance]);
registry.Register("idiv", idivIntSignature, StdLib.IDivInt);
```

**Modify the existing `divSignature` registration at line 247-250** (D-08: auto-promote to Double):
```csharp
// BEFORE:
var divSignature = new FunctionSignature("div", [IntType.Instance, IntType.Instance]);
registry.Register("div", divSignature, StdLib.DivInt);

// AFTER:
var divSignature = new FunctionSignature("div", [IntType.Instance, IntType.Instance]);
registry.Register("div", divSignature, StdLib.DivIntPromote);   // D-08: now returns Double
```

`StdLib.DivInt` may be retained (renamed to `StdLib.IDivInt` is conceptually identical — Int truncation with a div-by-zero guard) or kept as a private helper. Prefer creating a NEW `IDivInt` and a NEW `DivIntPromote` so the diff is review-friendly.

---

### P-06 — `StdLib.cs` (new arithmetic helpers)

**Analog (verbatim template):** `StdLib.cs:176-181` (`AddInt`):
```csharp
public static Value AddInt(IReadOnlyList<Value> args)
{
    var a = args[0].As<int>();
    var b = args[1].As<int>();
    return Value.Int(a + b);
}
```

**New helpers to APPEND after line 294** (BigInteger pattern from `ExpressionEvaluator.cs:266-278`):
```csharp
// ===== Phase 26 Long arithmetic (D-05 fast path) =====
public static Value AddLong(IReadOnlyList<Value> args)
    => Value.Long(args[0].As<long>() + args[1].As<long>());
public static Value SubLong(IReadOnlyList<Value> args)
    => Value.Long(args[0].As<long>() - args[1].As<long>());
public static Value MulLong(IReadOnlyList<Value> args)
    => Value.Long(args[0].As<long>() * args[1].As<long>());
public static Value DivLong(IReadOnlyList<Value> args)
{
    var a = args[0].As<long>();
    var b = args[1].As<long>();
    if (b == 0L) throw new InvalidOperationException("Division by zero");
    return Value.Long(a / b);
}

// ===== Phase 26 Number arithmetic (BigInteger; D-05 fast path) =====
public static Value AddNumber(IReadOnlyList<Value> args)
    => Value.Number(args[0].As<BigInteger>() + args[1].As<BigInteger>());
public static Value SubNumber(IReadOnlyList<Value> args)
    => Value.Number(args[0].As<BigInteger>() - args[1].As<BigInteger>());
public static Value MulNumber(IReadOnlyList<Value> args)
    => Value.Number(args[0].As<BigInteger>() * args[1].As<BigInteger>());
public static Value DivNumber(IReadOnlyList<Value> args)
{
    var a = args[0].As<BigInteger>();
    var b = args[1].As<BigInteger>();
    if (b.IsZero) throw new InvalidOperationException("Division by zero");
    return Value.Number(a / b);
}

// ===== Phase 26 (neg) 5-pack =====
public static Value NegInt(IReadOnlyList<Value> args)
    => Value.Int(-args[0].As<int>());
public static Value NegLong(IReadOnlyList<Value> args)
    => Value.Long(-args[0].As<long>());
public static Value NegFloat(IReadOnlyList<Value> args)
    => Value.Float(-args[0].As<double>());   // FloatType is double-backed in Value.Float (see Value.cs:25)
public static Value NegDouble(IReadOnlyList<Value> args)
    => Value.Double(-args[0].As<double>());
public static Value NegNumber(IReadOnlyList<Value> args)
    => Value.Number(-args[0].As<BigInteger>());

// ===== Phase 26 integer-division (D-08) =====
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
    return Value.Double((double)a / b);   // D-08: (div Int Int) → Double
}
```

**`using` directive needed:** `StdLib.cs` likely already has `using System.Numerics;` for any existing BigInteger handling — if not, add it (Value.cs:4 already shows `using System.Numerics;` so the convention is established).

---

### P-07 — `std.flow` (append `internal proc` declarations)

**Analog (verbatim template):** `std.flow:38-49`:
```
internal proc add (Int: a, Int: b)
internal proc add (Float: a, Float: b)
internal proc add (Double: a, Double: b)

internal proc sub (Int: a, Int: b)
internal proc sub (Double: a, Double: b)

internal proc mul (Int: a, Int: b)
internal proc mul (Double: a, Double: b)

internal proc div (Int: a, Int: b)
internal proc div (Double: a, Double: b)
```

**INCOMPLETE TODAY (RESEARCH Assumption A10):** Float decls are missing for sub/mul/div even though C# registers them. Phase 26 fixes this incidentally.

**REPLACE the entire block at lines 38-49 with** (preserves widening-chain order Int→Long→Float→Double→Number, adds (neg) and (idiv) per D-Discretion "append to existing block"):
```
Note: Arithmetic — 5-type same-type fast paths per Phase 26 D-05
internal proc add (Int: a, Int: b)
internal proc add (Long: a, Long: b)
internal proc add (Float: a, Float: b)
internal proc add (Double: a, Double: b)
internal proc add (Number: a, Number: b)

internal proc sub (Int: a, Int: b)
internal proc sub (Long: a, Long: b)
internal proc sub (Float: a, Float: b)
internal proc sub (Double: a, Double: b)
internal proc sub (Number: a, Number: b)

internal proc mul (Int: a, Int: b)
internal proc mul (Long: a, Long: b)
internal proc mul (Float: a, Float: b)
internal proc mul (Double: a, Double: b)
internal proc mul (Number: a, Number: b)

Note: (div Int Int) auto-promotes to Double per D-08; use (idiv) for Int truncation
internal proc div (Int: a, Int: b)
internal proc div (Long: a, Long: b)
internal proc div (Float: a, Float: b)
internal proc div (Double: a, Double: b)
internal proc div (Number: a, Number: b)

Note: Negation — 5-pack per Phase 26 D-07
internal proc neg (Int: x)
internal proc neg (Long: x)
internal proc neg (Float: x)
internal proc neg (Double: x)
internal proc neg (Number: x)

Note: Integer division (truncating) per Phase 26 D-08
internal proc idiv (Int: a, Int: b)
```

**Cross-check rule:** count of new `registry.Register` calls in P-05 must equal count of new `internal proc` lines here. Phase 25 D-25 lesson — without the `.flow` declaration the C# registration is invisible to user scripts (RESEARCH Pitfall 6).

---

### P-08 — `CLAUDE.md` updates (commit 3)

**Edit at line 148** (lambda example with infix — would parse-error post-Phase-26):
```
BEFORE:  Lambda functions: `fn Int x => x * 2`, `fn Int a, Int b => a + b`
AFTER:   Lambda functions: `fn Int x => (mul x 2)`, `fn Int a, Int b => (add a b)`
```

**Edit at line ~175** (AST table — `BinaryExpression` row no longer exists):
```
BEFORE:  | BinaryExpression | Binary operations (+, -, *, /, ==, !=, <, >, etc.) |
AFTER:   (delete the row entirely; arithmetic is via (add)/(sub)/(mul)/(div)/(neg)/(idiv) builtins)
```

**Add to `### Core` Language Features** (sibling to `Flow operator` bullet):
```
- Prefix-only arithmetic via `(add)/(sub)/(mul)/(div)/(neg)/(idiv)` and `(concat)` builtins (no infix `+ - * /`)
```

---

### P-09 — `flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs` (new)

**Analog (preferred — registry-direct, no FlowEngine spin-up):** `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs:41-98`. This pattern builds an `InternalFunctionRegistry`, calls `TryGetImplementation`, and asserts on the returned `Value` directly — fastest test, no parser involvement.

**Imports to copy** (HumanizeGaussianFacts.cs:1-12):
```csharp
using System;
using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;
```

**Registry build pattern** (verbatim from HumanizeGaussianFacts.cs:41-46, but call `BuiltInFunctions.Register` instead):
```csharp
private static InternalFunctionRegistry BuildRegistry()
{
    var registry = new InternalFunctionRegistry();
    BuiltInFunctions.Register(registry);   // entry point that registers everything
    return registry;
}
```

**Invocation idiom** (HumanizeGaussianFacts.cs:81-98 — preserves the exact `TryGetImplementation` API):
```csharp
private static Value Call(InternalFunctionRegistry registry, string name,
                          IReadOnlyList<FlowType> argTypes, IReadOnlyList<Value> args)
{
    var sig = new FunctionSignature(name, argTypes);
    if (!registry.TryGetImplementation(name, sig, out var fn, out _) || fn is null)
        throw new InvalidOperationException($"{name} overload {sig} not registered");
    return fn(args);
}

[Fact]
public void AddLong_SameType_ReturnsLong()
{
    var registry = BuildRegistry();
    var result = Call(registry, "add",
        [LongType.Instance, LongType.Instance],
        [Value.Long(5L), Value.Long(6L)]);
    Assert.IsType<LongType>(result.Type);
    Assert.Equal(11L, result.As<long>());
}

// Mirror for: AddNumber, SubLong, SubNumber, MulLong, MulNumber, DivLong, DivNumber.
// 8 Facts total per CONTEXT D-05.
```

---

### P-10 — `Phase26/NegOverloadFacts.cs` (new)

**Analog:** Same registry-direct pattern as P-09. **5 Facts**, one per type per CONTEXT D-07:
```csharp
[Fact]
public void NegInt_FlipsSign()
{
    var registry = BuildRegistry();
    var result = Call(registry, "neg", [IntType.Instance], [Value.Int(42)]);
    Assert.IsType<IntType>(result.Type);
    Assert.Equal(-42, result.As<int>());
}
// Mirror for: NegLong, NegFloat, NegDouble, NegNumber.
```

---

### P-11 — `Phase26/IntegerDivisionFacts.cs` (new)

**Analog:** P-09 registry-direct. **2 Facts** per CONTEXT D-08:
```csharp
[Fact]
public void DivIntInt_AutoPromotesToDouble()
{
    var registry = BuildRegistry();
    var result = Call(registry, "div",
        [IntType.Instance, IntType.Instance],
        [Value.Int(1), Value.Int(2)]);
    Assert.IsType<DoubleType>(result.Type);   // D-08: result is Double, NOT Int
    Assert.Equal(0.5, result.As<double>(), 1e-12);
}

[Fact]
public void IDivIntInt_TruncatesToInt()
{
    var registry = BuildRegistry();
    var result = Call(registry, "idiv",
        [IntType.Instance, IntType.Instance],
        [Value.Int(1), Value.Int(2)]);
    Assert.IsType<IntType>(result.Type);
    Assert.Equal(0, result.As<int>());
}
```

---

### P-12 — `Phase26/MixedTypeArithmeticFacts.cs` (new)

**Analog:** P-09 registry-direct, BUT must go through the full `EvaluateFunctionCall` coercion path (P-03) to validate the convertible-scoring fallback. **Use `FlowEngineRunner` (HAliasFacts.cs:38-58 pattern)** for these — running `(add 5 3.0)` end-to-end exercises the OverloadResolver + the new coercion code together:
```csharp
[Collection("FlowScripts")]
public class MixedTypeArithmeticFacts
{
    [Fact]
    public void AddIntDouble_WidensToDouble()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Double r = (add 5 3.0)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("8", stdout);   // 5 + 3.0 = 8.0
    }
    // Mirror for: AddFloatDouble→Double, AddLongNumber→Number, MulIntLong→Long,
    //   MulFloatDouble→Double, SubLongNumber→Number.
    // 6 Facts per RESEARCH §"Phase 26 Additions" mixed-pair coverage.
}
```

---

### P-13 — `Phase26/NegativeLiteralLexFacts.cs` (new)

**Analog (Theory + InlineData matrix):** `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs:20-62`.

**Direct lexer-only invocation** — drives `SimpleLexer` and asserts on `Token` shape, no parser/eval:
```csharp
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

public class NegativeLiteralLexFacts
{
    private static List<Token> Lex(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize();
    }

    [Theory]
    // (description, source, expected token type, expected value)
    [InlineData("statement-start int",       "Int x = -5;",                   TokenType.IntLiteral,    -5)]
    [InlineData("statement-start float",     "Double y = -3.14;",             TokenType.FloatLiteral,  -3.14)]
    [InlineData("after LParen",              "(add (-5) 3)",                  TokenType.IntLiteral,    -5)]
    [InlineData("after Comma",               "(add 5, -7)",                   TokenType.IntLiteral,    -7)]
    [InlineData("after LBracket",            "Voids a = [-1, 2, 3]",          TokenType.IntLiteral,    -1)]
    [InlineData("after Arrow (flow op RHS)", "5 -> add -3",                   TokenType.IntLiteral,    -3)]
    [InlineData("after Pipe (note stream)",  "Sequence s = | -5 C4 |",        TokenType.IntLiteral,    -5)]
    public void NegativeLiteralLexesAsSingleToken(string desc, string source,
                                                  TokenType expectedType, object expectedValue)
    {
        var tokens = Lex(source);
        Assert.Contains(tokens, t => t.Type == expectedType
            && t.Value != null
            && t.Value.Equals(expectedValue));
    }

    [Fact]
    public void TempoMinus_PreservesStandaloneMinus()
    {
        // Pitfall 1 — music-context keywords MUST be excluded from expression-start gate
        // so that Parser.cs:450 `if (Match(TokenType.Minus))` continues to fire.
        var tokens = Lex("tempo -120 { (print 1) }");
        // After 'tempo', a Minus token must still be standalone.
        var tempoIdx = tokens.FindIndex(t => t.Text == "tempo");
        Assert.True(tempoIdx >= 0);
        Assert.Equal(TokenType.Minus, tokens[tempoIdx + 1].Type);
        Assert.Equal(TokenType.IntLiteral, tokens[tempoIdx + 2].Type);
        Assert.Equal(120, tokens[tempoIdx + 2].Value);  // unsigned 120, sign consumed by parser
    }
}
```

---

### P-14 — `Phase26/UnaryMinusShorthandFacts.cs` (new)

**Analog:** `HAliasFacts.cs:38-58` (FlowEngineRunner stdout-substring assertion).
```csharp
[Collection("FlowScripts")]
public class UnaryMinusShorthandFacts
{
    [Fact]
    public void MinusIdent_LowersToNegCall()
    {
        // D-01 acceptance: `-x` parses as `(neg x)`. We exercise this end-to-end via
        // a print roundtrip — if the shorthand lowered correctly, the negative value
        // appears in stdout. If it produced a parse error or kept BinaryExpression,
        // the run fails or the wrong value prints.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Int x = 5
Int y = -x
(print (str y))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("-5", stdout);
    }

    [Fact]
    public void PlusIdent_StripsSilently()
    {
        // D-03: `+x` parses as `x` (Plus token absorbed at expression-start).
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Int x = 5
Int y = +x
(print (str y))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Contains("5", stdout);
    }
}
```

---

### P-15 — `Phase26/InfixRejectedFacts.cs` (new)

**Analog:** `PragmaScannerFacts.cs:107-116` (`EnableAfterStatement_RaisesError` — same "this should fail" idiom):
```csharp
[Collection("FlowScripts")]
public class InfixRejectedFacts
{
    [Theory]
    [InlineData("Int x = 1 + 2;")]
    [InlineData("Int x = 5 - 3;")]
    [InlineData("Int x = 4 * 2;")]
    [InlineData("Int x = 10 / 5;")]
    [InlineData(@"Int x = 1; Int y = x + 1;")]
    public void BareInfix_ProducesParseError(string source)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errors) = runner.RunSource("use \"@std\"\n" + source);
        Assert.True(errors > 0, $"expected parse error, got success. stderr: {stderr}");
        // D-15: generic 'unexpected token' — no charitable migration hint required.
    }
}
```

---

### P-16 — `scripts/Migrate26/Migrate26.csproj` (new)

**Analog:** `flow-lang/flow-lang.csproj` (project shape) + `flow-lang.Tests/flow-lang.Tests.csproj:18-21` (`<ProjectReference>` syntax pattern):
```xml
<ItemGroup>
  <ProjectReference Include="..\flow-lang\flow-lang.csproj" />
  <ProjectReference Include="..\flow-lsp\flow-lsp.csproj" />
</ItemGroup>
```

**Full template for `scripts/Migrate26/Migrate26.csproj`:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>FlowLang.Migrate26</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\flow-lang\flow-lang.csproj" />
  </ItemGroup>
</Project>
```

**Path traversal**: `scripts/Migrate26/` is two levels below repo root; `flow-lang/flow-lang.csproj` is one level below — `..\..\flow-lang\flow-lang.csproj` is the correct relative path. (RESEARCH Pitfall 5.)

**Invocation:** `dotnet run --project scripts/Migrate26 -- tests/ examples/ flow-lang/`

---

### P-17 — `scripts/Migrate26/Program.cs` (new — token walker + precedence climber)

**Analog (lexer reuse):** `flow-lang/Lexing/SimpleLexer.cs` is consumed as a library. **No analog for the walker itself** — there is no prior migration script in the repo. This is greenfield, but the lexer-only-no-parser pattern follows `flow-lang/Core/PragmaScanner.cs` (Phase 21) which similarly does a pre-pass over source with the lexer's primitives.

**Skeleton from RESEARCH §"Migration Script — Architecture Detail"** (lines 684-722):
```csharp
using FlowLang.Lexing;
using FlowLang.Diagnostics;

namespace FlowLang.Migrate26;

internal class Program
{
    static int Main(string[] args)
    {
        var files = ExpandPaths(args);   // glob expand directories → .flow files
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

    static string Migrate(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        var tokens = lexer.Tokenize();
        // Skip note-stream content (Pipe...Pipe regions).
        // Walk: detect `value-token  Plus|Minus|Star|Slash  value-token`.
        // Apply precedence: Plus/Minus < Star/Slash (mirror deleted ParseAdditive/ParseMultiplicative).
        // Emit prefix calls: `a + b * c` → `(add a (mul b c))`.
        // Special: StringLiteral + value → `(concat ...)`.
        // Special: leading Plus identifier → strip; leading Minus identifier → `(neg id)`.
        return RewriteSpans(source, tokens);
    }
}
```

**Key constraints** (RESEARCH §"Span detection"):
1. `Pipe...Pipe` regions are pass-through (note streams have their own arithmetic semantics).
2. Comments + `Note:` lines are absorbed by `SimpleLexer.SkipWhitespaceAndComments` — invisible to the walker.
3. Precedence climber must mirror deleted `ParseAdditive`/`ParseMultiplicative` (Star/Slash binds tighter than Plus/Minus).
4. Idempotent — running twice produces zero diff (structural property: `(add a b)` has no Plus/Minus token between value tokens).

---

## Shared Patterns

### S-01 — Per-type Builtin Registration

**Source:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:212-216` (the `add Int Int` template).

**Apply to:** P-05 (every new same-type and `(neg)` overload).

```csharp
var <op><Type>Signature = new FunctionSignature("<op>", [<Type>Type.Instance, <Type>Type.Instance]);
registry.Register("<op>", <op><Type>Signature, StdLib.<Op><Type>);
```

The local `var` naming convention (`addIntSignature`) is project-canonical. Follow it for review-friendly diffs.

### S-02 — Per-type StdLib Helper

**Source:** `flow-lang/StandardLibrary/StdLib.cs:176-181` (`AddInt`).

**Apply to:** P-06 (every new helper).

Single-line expression-bodied form is preferred when no div-by-zero guard is needed (per RESEARCH Pattern 2). Keep brace-form only when extra logic is required (`DivLong`, `DivNumber`, `IDivInt`, `DivIntPromote`).

### S-03 — `internal proc` Decl Mirror in `std.flow`

**Source:** `flow-lang/std.flow:38-49`.

**Apply to:** P-07. **Critical:** every C# `registry.Register` MUST have a matching `internal proc` line. Phase 25 D-25 lesson: missing decl = builtin invisible to user scripts (RESEARCH Pitfall 6).

### S-04 — Look-ahead + Rewind Lexer Helper

**Source:** `flow-lang/Lexing/SimpleLexer.cs:319-438` (`TryLookAheadSpecialLiteral`).

**Apply to:** P-04 (`TryLexSignedNumber`). The save/restore idiom:
```csharp
int savePos = _position;
int saveLine = _line;
int saveCol = _column;
// ... try to match ...
// On failure:
_position = savePos;
_line = saveLine;
_column = saveCol;
return null;
```

### S-05 — xUnit Fact Convention (registry-direct)

**Source:** `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs:41-98`.

**Apply to:** P-09, P-10, P-11. Fastest test path — no parser, no Engine spin-up, no stdout capture. Asserts directly on `Value.Type` and `Value.As<T>()`.

### S-06 — xUnit Fact Convention (FlowEngineRunner end-to-end)

**Source:** `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs:38-58`.

**Apply to:** P-12, P-14, P-15. Required when the Fact must exercise the parser/lexer/evaluator pipeline together (mixed-type coercion in `EvaluateFunctionCall`, parser shorthand lowering, parse-error path). Always use `[Collection("FlowScripts")]` to serialize tests that share `Console.SetOut/SetError`.

### S-07 — xUnit Theory + InlineData Matrix

**Source:** `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs:20-62`.

**Apply to:** P-13 (lex position matrix), P-15 (infix-rejection matrix).

### S-08 — `<ProjectReference>` Cross-Project Linking

**Source:** `flow-lang.Tests/flow-lang.Tests.csproj:18-21`.

**Apply to:** P-16. The `Migrate26.csproj` ProjectReference uses `..\..\flow-lang\flow-lang.csproj` (two levels up because `scripts/Migrate26/` is nested deeper than `flow-lang.Tests/`).

---

## Ordering Constraints

These are not strictly "patterns" but downstream agents must respect them:

1. **Within Commit 1:** P-04 (lexer) → P-02 (parser) → P-03 (evaluator) → P-05/P-06/P-07 (builtins). The lexer changes the token stream the parser sees; the parser must be updated next; the evaluator coercion fix must land WITH the parser change to avoid the D-05 InvalidCastException; the builtin registrations can be added at any point in this commit but should land alongside everything else for atomicity.
2. **The 5-line coercion fix in P-03 is a HARD prerequisite** for P-12 (mixed-type arithmetic Facts). Without it, `(add 5 3.0)` throws `InvalidCastException` and the Facts fail spuriously.
3. **Wave 0 tests (P-09 through P-15) can be authored BEFORE Commit 1** — they will fail until Commit 1 lands. This is the test-first protocol.
4. **Migration script (P-16, P-17) ships in Commit 2**, AFTER Commit 1. The script depends on the new lexer (single-token negative literals), so it must be built against the post-Commit-1 `flow-lang.csproj`.

---

## No Analog Found

Every file has a strong analog. No greenfield-without-precedent files in this phase.

**Notes on weak-but-acceptable analogs:**
- `scripts/Migrate26/Program.cs` — no prior migration script. Greenfield code, but the lexer-as-library pattern matches `flow-lang/Core/PragmaScanner.cs` (Phase 21 pre-lex scanner consuming the lexer's char-stream primitives). PragmaScanner is the closest "use the lexer for non-evaluation purposes" precedent.
- `scripts/Migrate26/Migrate26.csproj` — no other `.csproj` under `scripts/`. The shape is borrowed from `flow-lang.Tests.csproj` minus the test framework PackageReferences plus an `<OutputType>Exe</OutputType>`.

---

## Metadata

**Analog search scope:** `flow-lang/`, `flow-lang.Tests/`, `flow-lsp/`, `flow-midi/`, `scripts/`, `examples/`, `tests/`, `.planning/phases/{18,21,24,25}/`.

**Files inspected (representative):**
- Source: `Parser.cs`, `ExpressionEvaluator.cs`, `SimpleLexer.cs`, `BuiltInFunctions.cs`, `StdLib.cs`, `InternalFunctionRegistry.cs`, `Value.cs`, `Token.cs`, `BinaryExpression.cs`, `std.flow`.
- Tests: `Phase25/HumanizeGaussianFacts.cs`, `Phase24/DiatonicSpellingsFacts.cs`, `Phase21/HAliasFacts.cs`, `Phase21/PragmaScannerFacts.cs`, `Phase18/FractionTests.cs`, `Integration/Phase18/ByteIdenticalShowcaseTests.cs`, `Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs`, `Fixtures/FlowEngineRunner.cs`.
- Build: `flow-lang/flow-lang.csproj`, `flow-lang.Tests/flow-lang.Tests.csproj`.

**Pattern extraction date:** 2026-05-04
