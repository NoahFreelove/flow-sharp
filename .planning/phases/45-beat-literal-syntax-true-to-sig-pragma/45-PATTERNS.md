# Phase 45: Beat Literal Syntax & True-to-Sig Pragma — Pattern Map

**Mapped:** 2026-05-25
**Files analyzed:** 21 (10 CREATE + 11 MODIFY)
**Analogs found:** 21 / 21 (every file has a precedent in Phases 21/22/26.1/28/30/32/43/44)

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---------------------|------|-----------|----------------|---------------|
| **CREATE** | | | | |
| `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` | AST node | parse → eval | `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` | exact (single-property + Loc record, eval-time-resolved literal) |
| `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs` (or extend `BeatConversionFunctions.cs`) | stdlib registration | context-dependent CRUD | `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs` | exact (RegisterContextDependent recipe, reads MusicalContext) |
| `examples/beat/intro.flow` | tutorial | composer-facing | `examples/scala/intro.flow` | exact (pragma-on tutorial, `.flow` chapter format, renders MIDI+WAV) |
| `examples/beat/cut-time.flow` | tutorial | composer-facing | `examples/scala/intro.flow` | exact (same pragma + timesig demo pattern) |
| `tests/test_beat_literal.flow` | integration smoke | parse/eval roundtrip | `tests/test_pragma_isolation.flow` | role-match (positive smoke test) |
| `tests/test_beat_pragma_off.flow` | integration smoke | parse/eval roundtrip | `tests/test_pragma_isolation.flow` | role-match |
| `tests/test_beat_pragma_on.flow` | integration smoke | parse/eval roundtrip | `tests/test_match_exhaustive_pragma.flow` (similar enable-pragma form) | role-match |
| `tests/test_beat_cross_file.flow` | integration smoke | cross-file boundary | `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` | exact (cross-file pragma isolation pair) |
| `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` | xUnit Facts | unit (lexer+parser) | `flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs` | role-match (xUnit Facts under Integration/PhaseNN/) |
| `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` | xUnit Facts/Theory | unit (pragma+eval) | `flow-lang.Tests/Integration/Phase44/ExecutionContextStrictModeTests.cs` | exact (pragma-bit field test pattern) |
| **MODIFY** | | | | |
| `flow-lang/Lexing/TokenType.cs` | enum extension | static | existing `SemitoneLiteral`/`DecibelLiteral`/`CentLiteral`/`TimeLiteral`/`HertzLiteral` cases (lines 61-65) | exact |
| `flow-lang/Lexing/SimpleLexer.cs` | lexer branch | char-by-char dispatch | existing `c` suffix branches (lines 623-635 signed + 766-776 unsigned) | exact (single-char suffix + identifier-guard) |
| `flow-lang/Lexing/PragmaRegistry.cs` | registry entry | static dict | existing `["strict"]` entry at line 36 | exact |
| `flow-lang/Parsing/Parser.cs` | parser arm | token → AST | existing `SymbolLiteral` arm at line 1366-1367 + literal-token-set at 2103-2109 | exact |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | evaluator arm | AST → Value | existing `SymbolLiteralExpression` arm at line 46 | exact |
| `flow-lang/Runtime/ExecutionContext.cs` | context field | runtime state | Phase 44 `StrictMode` field at line 468 | exact (single bool field; NO companion `CallerBeatTrueToSig` per D-04) |
| `flow-lang/Runtime/ModuleLoader.cs` | push/pop | save-set-restore | Phase 44 `StrictMode` push/pop at lines 125-126 + 203 | exact |
| `flow-lang/Core/FlowEngine.cs` | pragma applier | top-level Execute | Phase 44 `ApplyStrictPragma` at lines 352-355 + call at 296 | exact |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs:547-555` | constructor migration | plain → context-dependent | Phase 43 `BeatConversionFunctions.RegisterContextDependent` at line 1023 wire-up | exact |
| `CLAUDE.md` | doc row | table extension | existing Music Types Quick Reference rows for `-12dB` / `100ms` / `+50c` / `+2st` / `440Hz` | exact |
| `.planning/REQUIREMENTS.md` | requirements section | static | Phase 44 REQ-STRICT-01..15 section | exact |

## Pattern Assignments

### `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` (AST node, parse → eval)

**Analog:** `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` (entire file, 15 lines)

**Imports + xmldoc + record shape pattern** (lines 1-14):
```csharp
using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A symbol literal expression like <c>#kick</c>, <c>#snare</c> (Phase 26.1 SYM-01).
/// The leading <c>#</c> is consumed at lex time; <see cref="Name"/> is the body without <c>#</c>.
/// Evaluation interns the symbol via <c>ExecutionContext.SymbolInternTable</c> for pointer-equality.
/// </summary>
public record SymbolLiteralExpression(
    SourceLocation Location,
    string Name,
    Span? Span = null
) : Expression(Location);
```

**Phase 45 BeatLiteralExpression — substitute `Name` → `RawValue` (double) and rewrite xmldoc:**
```csharp
using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A beat literal expression like <c>0.5b</c>, <c>2b</c>, <c>-1b</c> (Phase 45 D-01).
/// Carries the raw source double exactly as written; the multiplier formula
/// <c>final = pragma_on ? raw × (4.0 / denom) : raw</c> applies at eval time
/// in <see cref="FlowLang.Interpreter.ExpressionEvaluator.EvaluateBeatLiteral"/>,
/// reading <see cref="FlowLang.Runtime.ExecutionContext.BeatTrueToSig"/> +
/// <see cref="FlowLang.Runtime.MusicalContext.TimeSignature"/>.
/// </summary>
public record BeatLiteralExpression(
    SourceLocation Location,
    double RawValue,
    Span? Span = null
) : Expression(Location);
```

---

### `flow-lang/Lexing/TokenType.cs` (enum extension)

**Analog:** existing music-literal enum cluster at lines 60-67 of the same file.

**Existing cluster (lines 60-67)**:
```csharp
NoteLiteral,        // A+, C--, etc.
SemitoneLiteral,    // +1st, -5st
CentLiteral,        // +50c, -25c (microtones)
TimeLiteral,        // 100ms, 2.5s
DecibelLiteral,     // -3dB, +6dB
HertzLiteral,       // 800Hz, 1.5kHz (Phase 26.2 ERG-04)
ChordLiteral,       // Cmaj7, Dm, Gsus4
SymbolLiteral,      // #foo (Phase 26.1 SYM-01) — the leading '#' is a token boundary; lexeme is the body without '#'
```

**Phase 45 insertion — add one line in the same cluster:**
```csharp
BeatLiteral,        // 0.5b, 2b, +1b, -2b (Phase 45 D-06/D-07) — eval-time pragma multiplier in ExpressionEvaluator.EvaluateBeatLiteral
```

---

### `flow-lang/Lexing/SimpleLexer.cs` (lexer branches, two insertions)

**Analog (signed branch):** lines 623-635 — the `c` cent suffix branch in `TryLookAheadSpecialLiteral`.

**Existing `c` signed branch (lines 623-635):**
```csharp
// Try "c" suffix (cent - microtone)
if (!IsAtEnd() && Peek() == 'c' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    text = sb.ToString();

    // Parse as cent
    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, out double centValue))
    {
        return new Token(TokenType.CentLiteral, text, start, centValue, Span: new Span(start, CurrentLocation()));
    }
}
```

**Phase 45 signed `+Nb` / `-Nb` branch — insert between `st` (608-621) and `c` (623-635) per D-06:**
```csharp
// Try "b" suffix (beat literal — Phase 45 D-06)
if (!IsAtEnd() && Peek() == 'b' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    text = sb.ToString();

    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double beatValue))
    {
        return new Token(TokenType.BeatLiteral, text, start, beatValue,
                         Span: new Span(start, CurrentLocation()));
    }
}
```

**Analog (unsigned branch):** lines 766-776 — the `c` cent suffix `else if` branch in `ScanNumberOrSpecialLiteral`.

**Existing `c` unsigned `else if` branch (lines 766-776):**
```csharp
// Try "c" suffix (cent) - but not if followed by a letter (could be 'c' in a longer identifier)
else if (Peek() == 'c' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    var text = sb.ToString();

    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, out double centValue))
    {
        return new Token(TokenType.CentLiteral, text, start, centValue, Span: new Span(start, CurrentLocation()));
    }
}
```

**Phase 45 unsigned `Nb` branch — insert as new `else if` between `c` (766-776) and `s` (778-788) per D-07. CRITICAL: must be `else if` not `if`, the chain is order-significant:**
```csharp
// Try "b" suffix (beat literal — Phase 45 D-07)
else if (Peek() == 'b' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    var text = sb.ToString();

    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double beatValue))
    {
        return new Token(TokenType.BeatLiteral, text, start, beatValue,
                         Span: new Span(start, CurrentLocation()));
    }
}
```

---

### `flow-lang/Lexing/PragmaRegistry.cs` (registry entry)

**Analog:** existing `["strict"]` entry at line 36 (Phase 44 D-02).

**Existing entries (lines 27-37)**:
```csharp
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
        ["justIntonation"] = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
        ["pythagorean"] = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
        ["equalTemperament"] = "12-tone equal temperament (default). Explicit form for tooling-visible intent.",
        ["scaleLint"] = "Phase 31 D-03: scale-lint is now default-on; this pragma is accepted as a no-op for v1.3 backward compat.",
        ["matchExhaustive"] = "Phase 35 D-v1.5-05: promote non-exhaustive match warnings to errors. File-scope only; does NOT propagate via use imports (Pitfall 4).",
        ["strict"] = "Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports."
    };
```

**Phase 45 insertion — add one trailing entry per D-03 (verbatim text):**
```csharp
["beat-true-to-sig"] = "Opt-in: Nb literals and (beat N) constructor calls multiply by 4/denominator at eval time, reading active timesig. So in 'timesig 6/8 { }' with pragma on, 1b = 1 eighth. File-scoped, no propagation via use imports."
```

**OPEN QUESTION (RESEARCH Pitfall 7 / A1):** verify `PragmaScanner.cs` accepts hyphens in pragma identifiers BEFORE this entry takes effect — Wave 1 first task per the suggested ordering.

---

### `flow-lang/Parsing/Parser.cs` (parser arm + literal-token-set)

**Analog (parser arm):** existing `SymbolLiteral` arm at lines 1366-1367 — emits dedicated AST record (NOT `LiteralExpression`).

**Existing sibling arms (lines 1340-1367):**
```csharp
if (Match(TokenType.BoolLiteral))
    return new LiteralExpression(PreviousToken.Location, (bool)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.NoteLiteral))
    return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.SemitoneLiteral))
    return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.CentLiteral))
    return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.TimeLiteral))
    return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.DecibelLiteral))
    return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

// Phase 26.2 ERG-04 — HertzLiteral routes to LiteralExpression with raw text;
// ExpressionEvaluator.TryParseSpecialLiteral resolves "800Hz" / "1.5kHz" to Value.Hertz(canonical-Hz double).
if (Match(TokenType.HertzLiteral))
    return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.ChordLiteral))
    return new ChordLiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

if (Match(TokenType.SymbolLiteral))
    return new SymbolLiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);
```

**Phase 45 insertion — diverges from `CentLiteral`/`TimeLiteral`/`DecibelLiteral`/`HertzLiteral` pattern (those go to flat `LiteralExpression(text)`); follows `ChordLiteral`/`SymbolLiteral` pattern (dedicated record). Token.Value carries the parsed double from the lexer per Token.cs:30:**
```csharp
if (Match(TokenType.BeatLiteral))
{
    // Phase 45 D-09: Token.Value carries the parsed double; preserve through to eval
    // so the multiplier formula can read the raw value with current pragma + timesig state.
    double rawValue = (double)PreviousToken.Value!;
    return new BeatLiteralExpression(PreviousToken.Location, rawValue,
                                     Span: PreviousToken.EffectiveSpan);
}
```

**Analog (literal-token-set):** lines 2098-2111 — `IsExpressionStartingToken` enumerates all literal token types.

**Existing literal-token-set (lines 2098-2111):**
```csharp
return type is TokenType.IntLiteral
    or TokenType.FloatLiteral
    or TokenType.StringLiteral
    or TokenType.BoolLiteral
    or TokenType.NoteLiteral
    or TokenType.SemitoneLiteral
    or TokenType.CentLiteral
    or TokenType.TimeLiteral
    or TokenType.DecibelLiteral
    or TokenType.HertzLiteral
    or TokenType.ChordLiteral
    or TokenType.SymbolLiteral
    or TokenType.InterpolatedStringStart
    or TokenType.Identifier;
```

**Phase 45 insertion — add one `or` clause alongside other music literals:**
```csharp
or TokenType.BeatLiteral    // Phase 45 D-09 — top-level expression-form
```

---

### `flow-lang/Interpreter/ExpressionEvaluator.cs` (evaluator arm)

**Analog:** existing `SymbolLiteralExpression` arm at line 46 (entire switch lines 35-58).

**Existing switch dispatch (lines 35-58):**
```csharp
public virtual Value Evaluate(Expression expr)
{
    return expr switch
    {
        LiteralExpression lit => EvaluateLiteral(lit),
        VariableExpression var => EvaluateVariable(var),
        FunctionCallExpression call => EvaluateFunctionCall(call),
        ArrayIndexExpression idx => EvaluateArrayIndex(idx),
        ArrayLiteralExpression arrLit => EvaluateArrayLiteral(arrLit),
        TupleLiteralExpression tupLit => EvaluateTupleLiteral(tupLit),
        ChordLiteralExpression chordLit => EvaluateChordLiteral(chordLit),
        SymbolLiteralExpression symLit => EvaluateSymbolLiteral(symLit),
        LambdaExpression lambda => EvaluateLambda(lambda),
        // ...
        _ => throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported")
    };
}
```

**Phase 45 insertion — new arm between `SymbolLiteralExpression` and `LambdaExpression`:**
```csharp
BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),   // Phase 45 D-10
```

**New helper method (multiplier formula — implementation lives in same file per D-10):**
```csharp
private Value EvaluateBeatLiteral(BeatLiteralExpression beatLit)
{
    // Phase 45 D-10 multiplier formula:
    //   final = pragma_on ? raw × (4.0 / denom) : raw
    // GetMusicalContext() returns three-tier-fallback resolved instance;
    // TimeSignature defaults to 4/4 via FlowConfig.Active.DefaultTimesig (ExecutionContext.cs:852).
    int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
    double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0;
    return Value.Beat(beatLit.RawValue * multiplier);
}
```

---

### `flow-lang/Runtime/ExecutionContext.cs` (context field)

**Analog:** Phase 44 `StrictMode` field at line 468 (xmldoc + property at lines 439-468).

**Existing field (lines 439-468):**
```csharp
// ===== Phase 44 — strict mode (D-02 / D-03 / D-05) =====

/// <summary>
/// Phase 44 Plan 44-01 D-02 / D-03 — per-DECLARING-file strict-mode bit.
/// Set by <see cref="Core.FlowEngine.Execute"/> for the top-level file
/// (<c>_context.StrictMode = pragmaSet.Has("strict")</c> in
/// <c>ApplyStrictPragma</c>) and by
/// <see cref="ModuleLoader.LoadModule"/> for each imported file
/// (save-set-restore around <c>interpreter.Execute(program)</c>). Default
/// <c>false</c>.
///
/// <para>
/// File-scope per D-03: each file's pragma governs ONLY statements
/// declared in that file. A strict file that <c>use</c>s a non-strict
/// stdlib module runs the imported module's procs with
/// <c>StrictMode == false</c>; the importer's bit is restored once the
/// import returns. The bit lives on ExecutionContext (not StackFrame)
/// because Flow has dynamic-scope semantics for imports — the active bit
/// is whatever the most-recent file-load boundary set.
/// </para>
/// ...
/// </summary>
public bool StrictMode { get; set; } = false;
```

**Phase 45 insertion — single bool field per D-04; NO companion `CallerBeatTrueToSig` per Pitfall 3 (no leaf-site asymmetry needed). Insert in a new Phase 45 section AFTER the strict-mode section:**
```csharp
// ===== Phase 45 — beat-true-to-sig pragma (D-03 / D-04) =====

/// <summary>
/// Phase 45 D-04 — per-DECLARING-file beat-true-to-sig pragma bit.
/// Set by <see cref="Core.FlowEngine.Execute"/> for the top-level file
/// (<c>_context.BeatTrueToSig = pragmaSet.Has("beat-true-to-sig")</c> in
/// <c>ApplyBeatTrueToSigPragma</c>) and by <see cref="ModuleLoader.LoadModule"/>
/// for each imported file (save-set-restore around the imported
/// <c>interpreter.Execute(program)</c>). Default <c>false</c>.
///
/// <para>
/// Read site: <see cref="FlowLang.Interpreter.ExpressionEvaluator.EvaluateBeatLiteral"/>
/// and <c>BeatConstructorFunctions.RegisterContextDependent</c>. Computes the
/// multiplier formula <c>final = pragma_on ? raw × (4.0 / denom) : raw</c>
/// against the active <see cref="MusicalContext.TimeSignature"/> at literal /
/// constructor invocation time.
/// </para>
///
/// <para>
/// Single-field design (NO companion <c>CallerBeatTrueToSig</c>): unlike
/// Phase 44's strict-mode two-field design, Phase 45 has no leaf-clamp-site
/// asymmetry — the multiplier reads the EXECUTING file's pragma bit, which
/// is correct for both <c>Nb</c> literals and <c>(beat N)</c> calls
/// (45-PATTERNS.md Pitfall 3).
/// </para>
/// </summary>
public bool BeatTrueToSig { get; set; } = false;
```

---

### `flow-lang/Runtime/ModuleLoader.cs` (push/pop save-set-restore)

**Analog:** Phase 44 `StrictMode` push/pop at lines 125-126 (set) and 203 (restore).

**Existing wiring (lines 117-204):**
```csharp
// 4. Execute in current context (no new frame - imports add to current scope).
//
// Phase 44 Plan 44-01 D-03 — per-DECLARING-file strict-mode bit: save the
// caller's StrictMode, set it to THIS module's pragma bit for the duration
// of the imported Execute, then restore on the way out (try/finally is
// mandatory per Anti-Pattern 1 — never mutate StrictMode without a paired
// restore). The restore runs even when interpreter.Execute throws or the
// ModuleRegistry hook below errors, so the importer's bit cannot leak the
// imported file's value into subsequent statements.
var interpreter = ParentInterpreter ?? new Interpreter.Interpreter(context, _errorReporter, this);
var prevStrict = context.StrictMode;
context.StrictMode = pragmaSet.Has("strict");
try
{
    interpreter.Execute(program);
    // ... ModuleRegistry registration block ...
}
finally
{
    // Phase 44 Plan 44-01 D-03 / Anti-Pattern 1 — restore the caller's
    // StrictMode regardless of how the imported file's Execute exited
    // (success, error-via-reporter, or thrown exception caught by the
    // outer try). The outer try/finally below cleans _currentlyLoading;
    // this inner finally cleans the strict-bit save.
    context.StrictMode = prevStrict;
}
```

**Phase 45 parallel save-set-restore — insert immediately after the strict block:**
```csharp
// Phase 45 D-04 — per-DECLARING-file beat-true-to-sig bit. Parallels
// Phase 44's StrictMode discipline (Anti-Pattern 1: never mutate without
// paired restore in finally). MUST set BEFORE the try block and restore
// INSIDE the finally so the importer's bit cannot leak the imported
// file's value into subsequent statements.
var prevStrict = context.StrictMode;
context.StrictMode = pragmaSet.Has("strict");
var prevBeatTrueToSig = context.BeatTrueToSig;                     // NEW
context.BeatTrueToSig = pragmaSet.Has("beat-true-to-sig");         // NEW
try
{
    interpreter.Execute(program);
    // ... ModuleRegistry registration block UNCHANGED ...
}
finally
{
    context.StrictMode = prevStrict;
    context.BeatTrueToSig = prevBeatTrueToSig;                     // NEW
}
```

---

### `flow-lang/Core/FlowEngine.cs` (top-level pragma applier)

**Analog:** Phase 44 `ApplyStrictPragma` at lines 352-355 + call at line 296.

**Existing call site (lines 289-296):**
```csharp
_context.ResetBlockTuningStack();
ApplyTuningPragma(program);
// Phase 44 Plan 44-01 D-02: file-scope strict-mode bit. ApplyStrictPragma
// mirrors ApplyTuningPragma — top-level Execute is the file-load
// boundary for the active script; imported modules are handled separately
// by ModuleLoader.LoadModule's save-set-restore (D-03). Order is
// tuning-first / strict-second by convention; the two pragmas are
// independent and neither reads the other's state.
ApplyStrictPragma(program);
```

**Existing helper (lines 352-355):**
```csharp
private void ApplyStrictPragma(Ast.Program program)
{
    _context.StrictMode = program.Pragmas.Has("strict");
}
```

**Phase 45 insertion — add call after `ApplyStrictPragma(program);`:**
```csharp
ApplyStrictPragma(program);
// Phase 45 D-04: file-scope beat-true-to-sig bit. Mirrors ApplyStrictPragma
// exactly — single-line body, overwrites on every Execute (no persistence
// branch; same rationale as strict per the comment above).
ApplyBeatTrueToSigPragma(program);    // NEW

// 4. Interpret AST
_interpreter.Execute(program);
```

**Phase 45 new helper — mirror `ApplyStrictPragma` shape exactly:**
```csharp
/// <summary>
/// Phase 45 D-04 — bridges <c>program.Pragmas</c> →
/// <see cref="Runtime.ExecutionContext.BeatTrueToSig"/> for the top-level
/// file. Mirrors <see cref="ApplyStrictPragma"/>'s parse-then-set posture.
/// Imported files are handled by <see cref="ModuleLoader.LoadModule"/>'s
/// save-set-restore (D-04 file-scope semantics).
///
/// Overwrites on every Execute (no persistence branch): absence of
/// <c>enable beat-true-to-sig;</c> MUST set the bit to false so a prior REPL
/// session's pragma does not bleed into a fresh non-pragma file.
/// </summary>
private void ApplyBeatTrueToSigPragma(Ast.Program program)
{
    _context.BeatTrueToSig = program.Pragmas.Has("beat-true-to-sig");
}
```

---

### `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs` + `BuiltInFunctions.cs:547-555` (constructor migration)

**Analog:** Phase 43 `BeatConversionFunctions.cs` (entire file, 116 lines) — the canonical `RegisterContextDependent` recipe.

**Existing `BeatConversionFunctions.RegisterContextDependent` (lines 45-75) — closure captures `context` parameter:**
```csharp
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    // beatToSec(Beat) → Second
    var beatToSecSig = new FunctionSignature(
        "beatToSec",
        [BeatType.Instance],
        ParameterNames: ["beats"]);
    registry.Register("beatToSec", beatToSecSig, args =>
    {
        // BeatType backs double per BeatType.cs:25-28; same convention as
        // Cent/Millisecond/Decibel — `args[0].As<double>()` reads it directly.
        double beats = args[0].As<double>();

        // Read effective tempo through the three-tier fallback helper (always non-null).
        double bpm = context.GetMusicalContext().Tempo ?? 120.0;

        // Separately detect whether any *explicit* tempo block is in scope by walking
        // the StackFrame chain. GetMusicalContext() injects the 120 BPM default at
        // tier 3, so we can't use its return value to detect "no tempo block".
        if (!AnyFrameHasTempo(context.CurrentFrame))
        {
            RenderingDiagnostics.WarnOnce(
                "beatToSec-no-tempo",
                "[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
        }

        double seconds = beats * (60.0 / bpm);
        return Value.Second(seconds);
    });
    // ...
}
```

**Existing `(beat N)` registration to MIGRATE — `BuiltInFunctions.cs:547-555`:**
```csharp
// ===== Phase 26.1 Beat constructor (DICT-01 Tuple-of-hashables acceptance) =====
// Flow has no `Beat` literal at top level — durations like `q`, `h`, `e`, `s`, `w`
// exist only as note-stream suffixes (inside `| C4q D4h |`). DICT-01's
// Tuple-of-hashables key acceptance needs to construct Beat values in user source.
// (beat Double) wraps a fractional-beat double in a Beat-typed Value so that
// `<<C4, (beat 0.25)>>` produces a Tuple<<Note, Beat>> usable as a Dict key.
registry.Register("beat", new FunctionSignature("beat", [DoubleType.Instance],
        ParameterNames: ["value"]),
    args => Value.Beat(args[0].As<double>()));
```

**Phase 45 new file (recommended per D-05: own class for clarity):**
```csharp
// flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs (NEW)
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 45 D-05 — pragma-aware (beat Double) → Beat constructor.
/// Migrates the plain Register call at <c>BuiltInFunctions.cs:547-555</c>
/// to RegisterContextDependent so the lambda has access to
/// <see cref="ExecutionContext.BeatTrueToSig"/> + active
/// <see cref="MusicalContext.TimeSignature"/> at call time. Multiplier
/// formula matches <c>ExpressionEvaluator.EvaluateBeatLiteral</c> exactly:
/// <c>final = pragma_on ? raw × (4.0 / denom) : raw</c>.
///
/// Mirrors Phase 43's BeatConversionFunctions.RegisterContextDependent recipe.
/// Preserves Phase 26.1 DICT-01 acceptance (Tuple-of-hashables Dict key) —
/// the signature shape is identical, only the lambda body changes.
/// </summary>
public static class BeatConstructorFunctions
{
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var sig = new FunctionSignature("beat", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("beat", sig, args =>
        {
            double raw = args[0].As<double>();
            int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
            double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0;
            return Value.Beat(raw * multiplier);
        });
    }
}
```

**Wire-up in `BuiltInFunctions.cs` — analog at lines 1016-1027 already wires `BeatConversionFunctions`:**
```csharp
// Existing wire-up site (lines 1016-1027):
public static void RegisterContextDependentFunctions(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    Audio.SongRenderer.RegisterContextDependent(registry, context);
    Harmony.HarmonyFunctions.RegisterContextDependent(registry, context);
    Audio.EffectsFunctions.RegisterContextDependent(registry, context);
    Audio.BeatConversionFunctions.RegisterContextDependent(registry, context);
    // ... add Phase 45 line here:
    Audio.BeatConstructorFunctions.RegisterContextDependent(registry, context);   // NEW Phase 45 D-05
    // ...
}
```

**DELETE the existing `BuiltInFunctions.cs:547-555` block** — Phase 45 D-05 migration replaces it.

---

### `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` (xUnit pragma tests)

**Analog:** `flow-lang.Tests/Integration/Phase44/ExecutionContextStrictModeTests.cs` (entire file, 82 lines).

**Existing test class shape (lines 1-50):**
```csharp
using System;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase44;

[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class ExecutionContextStrictModeTests : IDisposable
{
    public ExecutionContextStrictModeTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static ExecutionContext NewContext()
    {
        var reporter = new ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new ExecutionContext(reporter, registry);
    }

    [Fact]
    public void Fact_StrictMode_DefaultsFalse()
    {
        var ctx = NewContext();
        Assert.False(ctx.StrictMode,
            "ExecutionContext.StrictMode must default to false per Phase 44 D-02.");
    }

    [Fact]
    public void Fact_StrictMode_Settable()
    {
        var ctx = NewContext();
        ctx.StrictMode = true;
        Assert.True(ctx.StrictMode);
        ctx.StrictMode = false;
        Assert.False(ctx.StrictMode);
    }
}
```

**Phase 45 should mirror exactly** — same `Trait("Category", Phase45TestCategory.Phase45)`, same `Collection("FlowScripts")`, same `IDisposable` + `RenderingDiagnostics.ResetForTesting()` pattern, same `NewContext()` helper. Add Theory grid for the multiplier matrix (Signal 4 in RESEARCH §Validation Architecture, ~15 cases).

**Need to create:** `flow-lang.Tests/Integration/Phase45/Phase45TestCategory.cs` (mirror Phase44TestCategory.cs):
```csharp
namespace FlowLang.Tests.Integration.Phase45;

public static class Phase45TestCategory
{
    public const string Phase45 = "Phase45";
}
```

---

### `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` (xUnit lexer/parser tests)

**Analog:** `flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs` lines 1-90 (FlowEngine.Execute + stdout/stderr capture pattern).

**Existing stderr capture helper (lines 50-65):**
```csharp
private static string CaptureStderr(Action action)
{
    var original = Console.Error;
    var sb = new StringBuilder();
    var writer = new StringWriter(sb);
    Console.SetError(writer);
    try
    {
        action();
    }
    finally
    {
        Console.SetError(original);
    }
    return sb.ToString();
}
```

**Existing Fact shape (lines 74-90+):**
```csharp
[Fact]
public void BeatToSec_OutsideTempoBlock_DefaultsTo120BpmAndFiresAdvisory()
{
    string source = "use \"@audio\"; Second s = (beatToSec 1.0); (print (str s))";
    string stdout = "";
    string stderr = CaptureStderr(() =>
    {
        var origOut = Console.Out;
        var outSb = new StringBuilder();
        Console.SetOut(new StringWriter(outSb));
        try
        {
            using var engine = new FlowEngine();
            engine.Execute(source, "<beatToSec_default_tempo>");
            stdout = outSb.ToString();
        }
        finally
        // ...
    });
    // Assert on stdout + stderr
}
```

**Phase 45 should mirror this shape** for end-to-end smoke tests + use direct lexer-only invocation for token-shape Facts (Signal 1: 15 cases enumerated in RESEARCH §Signal 1). Pattern:
- Direct `SimpleLexer` invocation for token-shape asserts (no FlowEngine needed)
- Direct `Parser` invocation for AST-shape asserts (cast result to `BeatLiteralExpression`)
- FlowEngine.Execute for end-to-end multiplier verification

---

### `examples/beat/intro.flow` + `examples/beat/cut-time.flow` (tutorial files)

**Analog:** `examples/scala/intro.flow` (entire file, multi-chapter pragma-on tutorial).

**Existing tutorial header pattern (lines 1-20):**
```flow
enable justIntonation;

use "@std"
use "@audio"
use "@composition"

Note: ============================================================
Note:  Chapter: Scala Microtonal Tunings (Phase 32)
Note:  Run: dotnet run --project flow-interpreter examples/scala/intro.flow
Note: ============================================================
Note:
Note:  Flow ships three named tunings out of the box: ...

(print "")
(print "=== Scala Microtonal Tunings ===")
```

**Phase 45 `examples/beat/intro.flow` should mirror exactly** — file-top pragma, header banner, chapters delimited by `Note: ---`-style comments, `(print)` chapter banners, `tempo N { timesig 6/8 { section X { ... } } }` blocks, MIDI + WAV render at the end. ~50-80 lines per D-12.

---

### `tests/test_beat_*.flow` (4 positive smoke tests)

**Analog:** `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` (the cross-file pragma pair).

**Existing cross-file fixture (entire file, 13 lines):**
```flow
use "@std"

use "./test_pragma_isolation_module.flow"

Note: PRAG-02 isolation fixture (Plan 21-02 baseline — passes integration loop)
Note: The actual H4q-rejection assertion lives in PragmaIsolationFacts.cs which
Note: uses RunSource(...) with an inline source that exercises the importer's
Note: H4q rejection. This file proves the module loads cleanly when the importer
Note: does NOT declare the pragma — and that the importer NEVER inherits the
Note: module's enable hAsB; declaration.

(print "test_pragma_isolation: PASSED")
```

**Phase 45 cross-file test pair** mirrors this structure exactly:
- `tests/test_beat_cross_file.flow` — pragma-on importer
- `tests/test_beat_cross_file_helper.flow` — pragma-off helper, declares `proc bumpBeat Beat b => (beat 1)` per RESEARCH Pitfall 3

The three other test files (`test_beat_literal.flow`, `test_beat_pragma_off.flow`, `test_beat_pragma_on.flow`) use the same `(print "...PASSED")` end-marker convention; runner is `for test in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$test"; done`.

---

### `CLAUDE.md` (Music Types Quick Reference table)

**Analog:** existing table rows for `-12dB` / `100ms` / `+50c` / `+2st` / `1.5` (Beat-tagged) / `440Hz` in the table around line 99 (Music Types Quick Reference section).

**Existing rows (verbatim from CLAUDE.md table):**
```markdown
| `-12dB` | `Decibel` | `Double`, `Float` | `gain`, `compress`/`sidechain` threshold |
| `100ms` | `Millisecond` | `Double`, `Float` | `delay`, `compress`/`sidechain` attack/release; → `Second` |
| `2.5s` | `Second` | `Double`, `Float` | `reverb` decay; → `Millisecond` |
| `+50c` | `Cent` | `Double`, `Float` | `transpose` cent-precision |
| `+2st` | `Semitone` | `Int` (whole-numbers-by-design) | `transpose` semitone-precision |
| `1.5` (Beat-tagged) | `Beat` | `Double`, `Float` | beat-position arithmetic |
| `440Hz` / `1.5kHz` | `Hertz` | `Double`, `Float` | filters, `createSineTone`/etc. (kHz → canonical Hz at lex time) |
```

**Phase 45 — REPLACE the existing `1.5 (Beat-tagged)` row (it's now misleading post-Phase-45 since literal form exists) with D-13 verbatim row:**
```markdown
| `0.5b` (Beat literal) | `Beat` | `Double`, `Float` | beat-position arithmetic; `enable beat-true-to-sig;` opt-in retunes literal to active timesig's beat unit (default 4/4 → `1b = quarter`) |
```

Plus a one-line addition to the "Music-Specific" section per D-13 mentioning the pragma family expansion.

---

### `.planning/REQUIREMENTS.md` (REQ-BEAT-NN section)

**Analog:** Phase 44 REQ-STRICT-01..15 section (existing in `.planning/REQUIREMENTS.md` per CLAUDE.md context).

**Phase 45 should mirror the Phase 44 REQ-STRICT section header style** and drop in ~21 REQ-BEAT-NN entries enumerated in RESEARCH §Phase Requirements:
- REQ-BEAT-LEX-01..04 (lexer)
- REQ-BEAT-AST-01..04 (AST + parser + evaluator)
- REQ-BEAT-PRAGMA-01..04 (registry + context + engine + module-loader)
- REQ-BEAT-CONSTRUCTOR-01..02 (constructor migration + DICT-01 regression)
- REQ-BEAT-TEST-01..07 (positive .flow + xUnit + two-run cmp-clean)
- REQ-BEAT-DOC-01..04 (CLAUDE.md + tutorials)

## Shared Patterns

### Pragma Push/Pop Discipline (CRITICAL — Anti-Pattern 1 from Phase 44 PATTERNS)

**Source:** `flow-lang/Runtime/ModuleLoader.cs:117-204`
**Apply to:** Every file-scope pragma carrier on `ExecutionContext` (Phase 45 `BeatTrueToSig`)

**Rule:** Never mutate the pragma bit without a paired restore. `prev = ctx.X; ctx.X = pragmaSet.Has(...); try { Execute } finally { ctx.X = prev; }`. The restore MUST be in `finally` so it runs even when the imported file's Execute throws or the inner ModuleRegistry block errors.

```csharp
// Concrete shape (Phase 44 strict + Phase 45 beat-true-to-sig parallel):
var prevStrict = context.StrictMode;
context.StrictMode = pragmaSet.Has("strict");
var prevBeatTrueToSig = context.BeatTrueToSig;                     // NEW Phase 45
context.BeatTrueToSig = pragmaSet.Has("beat-true-to-sig");         // NEW Phase 45
try
{
    interpreter.Execute(program);
    // ... existing ModuleRegistry block unchanged ...
}
finally
{
    context.StrictMode = prevStrict;
    context.BeatTrueToSig = prevBeatTrueToSig;                     // NEW Phase 45
}
```

### Top-Level Execute Pragma Application

**Source:** `flow-lang/Core/FlowEngine.cs:289-355`
**Apply to:** Every file-scope pragma (Phase 45 `beat-true-to-sig` mirrors Phase 44 `strict`)

**Rule:** Each pragma has its own `Apply*Pragma(program)` private helper. Helper is single-line: `_context.X = program.Pragmas.Has("pragma-name");`. Helper is called from `Execute` AFTER parse and BEFORE `_interpreter.Execute(program)`. Helper OVERWRITES on every Execute (no persistence branch — absence of the pragma MUST clear the bit so a prior REPL session doesn't leak state).

### Music-Literal Lexer Branch (single-char suffix with identifier-guard)

**Source:** `flow-lang/Lexing/SimpleLexer.cs:623-635` (signed `c`) + `:766-776` (unsigned `c`)
**Apply to:** Phase 45 `b` suffix branches (both signed + unsigned)

**Rule:** Single-char suffix MUST use `Peek() == 'X' && !char.IsLetter(PeekNext())` to disambiguate from identifiers starting with that letter (e.g., `1bar`, `2beats`, `bpm`). Without the guard, the lexer greedy-matches the prefix and corrupts identifier lex.

**Unsigned branch is `else if` chained** (not `if`) — chain order matters: each branch is first-char dispatch; the first matching `else if` wins. Insert new branches as additional `else if` blocks.

### `RegisterContextDependent` Pragma-Aware Builtin

**Source:** `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs` (Phase 43 D-08 canonical recipe)
**Apply to:** Phase 45 `(beat N)` constructor migration

**Rule:** When a builtin needs to read `ExecutionContext` state per-call (active tempo, timesig, pragma bits, tuning), register through `RegisterContextDependent(registry, context)` where the closure captures `context` parameter. Lambda reads `context.GetMusicalContext()...` + `context.PragmaBit` fresh on every invocation. Wire into `BuiltInFunctions.RegisterContextDependentFunctions` (line 1016) alongside existing context-dependent registrations.

### Own AST Record for Eval-Time-Resolved Literals

**Source:** `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` (Phase 26.1 SYM-01)
**Apply to:** Phase 45 `BeatLiteralExpression`

**Rule:** When a literal's final value depends on runtime context (intern table, pragma + musical context, etc.), it gets its OWN `record` type with the raw source payload + Loc + optional Span. NOT a flag-on-`LiteralExpression` (per Phase 45 D-01 explicit rejection). The evaluator switch arm reads the runtime context and constructs the final `Value` at eval time.

### Two-Run Cmp-Clean Determinism

**Source:** CLAUDE.md "Conventions" section + Phase 28 baselines
**Apply to:** Phase 45 tutorial WAVs (`examples/beat/intro.flow` + `examples/beat/cut-time.flow`)

**Rule:** Phase 45 adds NO PRNG sites (no `granular` / `markov` / `lsystem` / `jam` invocation). Tutorial WAVs are pure synthesis. Two runs of the same `.flow` file MUST produce byte-identical WAV output. Recommended: commit reference renders under `flow-lang.Tests/baselines/Phase45/` (Phase 28 precedent) — optional per RESEARCH Open Question 3, but recommended.

### xUnit Test Class Shape

**Source:** `flow-lang.Tests/Integration/Phase44/ExecutionContextStrictModeTests.cs` + `Phase44TestCategory.cs`
**Apply to:** Phase 45 xUnit Facts

**Rule:** Test classes carry `[Trait("Category", Phase45TestCategory.Phase45)]` + `[Collection("FlowScripts")]` + implement `IDisposable` with `RenderingDiagnostics.ResetForTesting()` in ctor + Dispose. Helper `NewContext()` builds a fresh `ExecutionContext` with `ErrorReporter` + `InternalFunctionRegistry`. Create Phase45TestCategory.cs as a one-line constant class.

## No Analog Found

All 21 Phase 45 files have a close analog in the codebase. No gaps.

## Metadata

**Analog search scope:**
- `flow-lang/Ast/Expressions/` (all 15 expression records — closest match: `SymbolLiteralExpression.cs`)
- `flow-lang/Lexing/` (TokenType, SimpleLexer, PragmaRegistry, PragmaScanner — all primary analogs in same dir)
- `flow-lang/Parsing/Parser.cs` (lines 1340-1370 music-literal arms + 2098-2112 token-set check)
- `flow-lang/Interpreter/ExpressionEvaluator.cs` (lines 35-58 switch dispatch + lines 75-150 TryParseSpecialLiteral)
- `flow-lang/Runtime/` (ExecutionContext, ModuleLoader, MusicalContext — Phase 44 StrictMode precedent dominant)
- `flow-lang/Core/FlowEngine.cs` (lines 289-355 pragma application order)
- `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs` (Phase 43 RegisterContextDependent canonical)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (lines 547-555 current registration to delete + 1016-1027 wire-up)
- `flow-lang.Tests/Integration/Phase42/`, `Phase43/`, `Phase44/` (test conventions: Trait, Collection, IDisposable, TestCategory class)
- `tests/test_*.flow` (cross-file pragma pair fixture + general `.flow` smoke test conventions)
- `examples/scala/intro.flow` + `examples/sections/parameterized.flow` (composer tutorial conventions)

**Files scanned:** ~25 (including all directly cited canonical refs in RESEARCH.md Sources §Primary)

**Pattern extraction date:** 2026-05-25

---

## PATTERN MAPPING COMPLETE

**Phase:** 45 - Beat Literal Syntax & True-to-Sig Pragma
**Files classified:** 21 (10 CREATE + 11 MODIFY)
**Analogs found:** 21 / 21

### Coverage
- Files with exact analog: 19
- Files with role-match analog: 2 (`tests/test_beat_literal.flow` + `tests/test_beat_pragma_off.flow` — fall under generic "positive smoke test" role, closest match is pragma isolation pair which is shape-similar but not behavior-identical)
- Files with no analog: 0

### Key Patterns Identified
- **Pragma push/pop is load-bearing (Anti-Pattern 1):** Phase 44's `StrictMode` save-set-restore in `ModuleLoader.cs:125-203` is the canonical shape; Phase 45 `BeatTrueToSig` mirrors line-for-line. Restore MUST be in `finally`.
- **Two-stage pragma application:** Top-level entry file via `FlowEngine.ApplyXPragma(program)` + imported files via `ModuleLoader.LoadModule` save-set-restore. Both write the same `ExecutionContext.X` field.
- **Music-literal lexer suffix idiom:** Two functions (`TryLookAheadSpecialLiteral` for signed expression-start + `ScanNumberOrSpecialLiteral` for unsigned after-digits). Single-char suffixes use `Peek() == X && !char.IsLetter(PeekNext())` identifier-guard. The unsigned scanner is `else if` chained — insertion-order matters.
- **Own AST record vs flag-on-LiteralExpression:** Eval-time-context-dependent literals get their own `record` type (`SymbolLiteralExpression`, `ChordLiteralExpression`, Phase 45's `BeatLiteralExpression`). Static-text literals route through flat `LiteralExpression(text)` with `TryParseSpecialLiteral` re-parse at eval time (current `CentLiteral`/`TimeLiteral`/`DecibelLiteral`/`HertzLiteral`).
- **`RegisterContextDependent` recipe:** Phase 43's `BeatConversionFunctions.cs` is the canonical 30-line template — closure captures `context`, reads `context.GetMusicalContext()...` fresh per call. Wire-up at `BuiltInFunctions.cs:1016` alongside existing context-dependent registrations.
- **Single field vs two field (Pitfall 3):** Phase 44's `StrictMode` + `CallerStrictMode` two-field design solves leaf-clamp-site asymmetry (stdlib stays charitable when called from non-strict). Phase 45 has no equivalent asymmetry — `ctx.BeatTrueToSig` alone is sufficient. Do NOT add `CallerBeatTrueToSig`.

### File Created
`/home/noah/Desktop/projects/flow-sharp/.planning/phases/45-beat-literal-syntax-true-to-sig-pragma/45-PATTERNS.md`

### Ready for Planning
Pattern mapping complete. Planner can now reference these analog snippets directly in each plan's action steps. Suggested 6-wave breakdown from RESEARCH.md `Ready for Planning` section is dependency-correct and well-scoped.
