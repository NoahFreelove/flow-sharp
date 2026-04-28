# Phase 21: Pragma System + H-Alias — Pattern Map

**Mapped:** 2026-04-26
**Files analyzed:** 14 (4 new src + 7 modified src + 7 new tests / fixtures)
**Analogs found:** 14 / 14 (100%)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Lexing/PragmaScanner.cs` (NEW) | lex-stage / source transform | request-response (string → tuple) | `flow-lang/Lexing/SimpleLexer.cs` (manual char-by-char scanner) | role-match (pre-lex stage; same scanning idiom) |
| `flow-lang/Lexing/PragmaSet.cs` (NEW) | value-record (parse-time data) | transform | `flow-lang/Core/SourceLocation.cs` + `flow-lang/TypeSystem/Fraction.cs` | exact (immutable record value type) |
| `flow-lang/Lexing/PragmaRegistry.cs` (NEW) | static closed-set registry | request-response (lookup) | `flow-lang/Parsing/Parser.NoteStream.cs` `MusicTwentyOneShorthand` static dictionary + `TokenType` closed enum | exact (closed-set + helper accessors) |
| `flow-lang/Core/FlowEngine.cs` (MODIFIED) | orchestrator | pipeline | self (existing pipeline at line 59-93) | self-extension |
| `flow-lang/Lexing/SimpleLexer.cs` (MODIFIED) | lexer | streaming token | self (existing `TryParseNote` at line 689) | self-extension |
| `flow-lang/Lexing/Token.cs` (MODIFIED) | value-record | transform | self (existing record at line 8) | self-extension (additive field) |
| `flow-lang/Parsing/Parser.cs` (MODIFIED) | parser | request-response | self (existing constructor at line 32) | self-extension (additive ctor param) |
| `flow-lang/Parsing/Parser.NoteStream.cs` (MOSTLY UNCHANGED) | parser (partial) | streaming | self | self (no signature change required) |
| `flow-lang/Runtime/ModuleLoader.cs` (MODIFIED) | module loader | file-I/O → pipeline | self (existing `LoadModule` at line 37-111) | self-extension |
| `flow-lang/Ast/Program.cs` (MODIFIED) | AST root record | transform | self (record at line 8) | self-extension (additive field) |
| `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` (NEW) | xUnit unit test | event-driven (Fact-per-criterion) | `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs` | exact (lexer/parser unit-Fact pattern) |
| `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` (NEW) | xUnit unit test | event-driven | `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs` | exact (pure-function unit Facts) |
| `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` (NEW) | xUnit unit test | event-driven | `flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs` | exact (FlowEngineRunner stdout-substring Facts) |
| `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` (NEW) | xUnit integration test | file-I/O + event-driven | `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` + `Integration/Phase18/ByteIdenticalShowcaseTests.cs` | role-match (paired-fixture cross-file isolation) |
| `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` (NEW pair) | flow integration script | file-I/O | `tests/test_range.flow`, `tests/test_enharmonic_edges.flow` | exact (Phase 20 acceptance script style) |
| `tests/test_h_alias.flow`, `tests/test_h_identifier.flow` (NEW) | flow integration script | file-I/O | `tests/test_enharmonic_edges.flow` | exact |

---

## Pattern Assignments

### `flow-lang/Lexing/PragmaSet.cs` (NEW — value-record)

**Analog:** `flow-lang/Core/SourceLocation.cs` (record with static `Unknown` member) + `flow-lang/TypeSystem/Fraction.cs` (immutable record-style value with helper methods)

**Imports / namespace pattern** (`SourceLocation.cs` lines 1-6):
```csharp
namespace FlowLang.Core;

/// <summary>
/// Represents a location in source code for error reporting and debugging.
/// </summary>
public record SourceLocation(int Line, int Column, string? FileName = null)
{
    public static SourceLocation Unknown { get; } = new(0, 0, null);
```

**Static-default-instance + helper-method pattern** (`SourceLocation.cs` lines 6-15):
```csharp
public record SourceLocation(int Line, int Column, string? FileName = null)
{
    public static SourceLocation Unknown { get; } = new(0, 0, null);

    public override string ToString()
    {
        if (FileName != null)
            return $"{FileName}:{Line}:{Column}";
        return $"{Line}:{Column}";
    }
}
```

**Apply this shape to:** `PragmaSet` should expose `public static readonly PragmaSet Empty = new(...)` (mirrors `SourceLocation.Unknown`) and a `Has(string)` method (mirrors `Fraction.ToString()`-style helper). RESEARCH §"PragmaSet record (D-02)" lines 412-423 already sketches this; the shape matches `SourceLocation` exactly.

**`PragmaDeclarationSite` pattern** — minimal record-with-2-fields, identical to `flow-lang/Ast/Statements/ImportStatement.cs` line 8:
```csharp
public record ImportStatement(
    SourceLocation Location,
    string FilePath) : Statement(Location);
```

---

### `flow-lang/Lexing/PragmaRegistry.cs` (NEW — static closed-set)

**Analog:** `flow-lang/Parsing/Parser.NoteStream.cs:19-32` — closed static dictionary with documentation per entry, accessed via `IsKnown`/lookup helpers.

**Closed-set static dictionary pattern** (`Parser.NoteStream.cs` lines 13-32):
```csharp
/// <summary>
/// TUP-02 music21 shorthand convention: {N ...}q resolves to {N:M ...}q
/// where M is looked up from this table. SPEC TUP-02 LOCKS entries 3, 5, 6, 7, 9.
/// Counts 2, 4, 8, 10, 11 are music21-aligned per RESEARCH §"Code Examples" §1.
/// Counts ≥ 12 raise a parse error citing the lookup-table bounds.
/// </summary>
private static readonly IReadOnlyDictionary<int, int> MusicTwentyOneShorthand =
    new Dictionary<int, int>
    {
        { 2, 3 },   // duplet
        { 3, 2 },   // triplet (LOCKED by SPEC TUP-02)
        { 4, 6 },   // quadruplet
        { 5, 4 },   // quintuplet (LOCKED)
        { 6, 4 },   // sextuplet (LOCKED)
        { 7, 4 },   // septuplet (LOCKED)
        { 8, 6 },
        { 9, 8 },   // nonuplet (LOCKED)
        { 10, 8 },
        { 11, 8 },
    };
```

**Apply this shape to:** `PragmaRegistry.KnownPragmas` should be `public static readonly IReadOnlyDictionary<string, string>` initialized inline with `StringComparer.Ordinal` and per-entry summary strings. RESEARCH §"PragmaRegistry (D-12, D-17)" lines 441-446 sketches this verbatim. Phase 21 ships ONE entry (`hAsB`) per D-17 — analogous to `MusicTwentyOneShorthand` shipping a fixed table; future phases extend by adding entries, not by changing the shape.

**Naming convention:** `IsKnown(name)` mirrors existing predicate-style accessor `ChordParser.IsChordSymbol(text)` (cited at SimpleLexer.cs:637).

**Levenshtein:** RESEARCH §"PragmaRegistry" lines 475-497 supplies a complete Wagner-Fischer DP. Hand-roll, no library — matches CLAUDE.md "Minimal Dependencies" guiding principle.

---

### `flow-lang/Lexing/PragmaScanner.cs` (NEW — pre-lex stage)

**Analog:** `flow-lang/Lexing/SimpleLexer.cs:798-844` (`SkipWhitespaceAndComments`) — manual char-by-char walk with line-end / `//` comment / line-continuation handling. Same scanning idiom; same state-machine shape.

**Manual char walk pattern** (`SimpleLexer.cs` lines 798-834):
```csharp
private void SkipWhitespaceAndComments()
{
    while (!IsAtEnd())
    {
        char c = Peek();

        if (char.IsWhiteSpace(c))
        {
            Advance();
        }
        else if (c == '\\' && PeekNext() == '\n')
        {
            // Line continuation: backslash followed by newline
            Advance(); // Skip backslash
            Advance(); // Skip newline
            _line--;
            _column = 1;
        }
        else if (c == '/' && PeekNext() == '/')
        {
            // Line comment: skip to end of line
            while (!IsAtEnd() && Peek() != '\n')
            {
                Advance();
            }
        }
        ...
```

**Apply this shape to:** `PragmaScanner.Scan` walks source line-by-line (lineStart..lineEndExclNewline), handles blank/comment/pragma/normal-source classification, never uses regex. RESEARCH §"PragmaScanner.Scan skeleton" lines 514-650 supplies a full sketch.

**Critical determinism rule** (Pitfall F + Pitfall G in RESEARCH):
- Fast path: if `source.IndexOf("enable", StringComparison.Ordinal) < 0` → return `(PragmaSet.Empty, source)` — RETURN THE ORIGINAL STRING REFERENCE, not a copy. This preserves byte-identical determinism for every legacy `.flow` file.
- When stripping a pragma line, copy spaces for every non-newline char and preserve the EXACT trailing newline sequence (`\n` or `\r\n`).

**Error accumulation pattern** (project convention — CLAUDE.md "Error accumulation: ErrorReporter collects errors rather than throwing"):
```csharp
// From flow-lang/Diagnostics/ErrorReporter.cs:33-36
public void ReportError(string message, Core.SourceLocation? location = null)
{
    Report(FlowError.Create(message, location));
}
```

PragmaScanner consumes an `ErrorReporter` parameter; emits D-11 / D-12 errors via `errors.ReportError(msg, nameLoc)` and continues scanning so multiple errors accumulate per pass.

---

### `flow-lang/Lexing/Token.cs` (MODIFIED — additive field)

**Analog:** self (existing record).

**Existing record** (Token.cs lines 8-20):
```csharp
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,
    object? Value = null)
{
    public override string ToString()
    {
        if (Value != null)
            return $"{Type}('{Text}', {Value}) at {Location}";
        return $"{Type}('{Text}') at {Location}";
    }
}
```

**Apply:** Add `string? OriginalText = null` as the FIFTH (trailing) positional parameter. Optional + defaulting to null = backward-compatible at every existing `new Token(...)` call site. Add `public string DiagnosticText => OriginalText ?? Text;` helper. Keep `ToString` unchanged (renderer/MIDI consume `Text`, NOT `DiagnosticText`). RESEARCH §"Token canonical-vs-original wiring (D-15)" lines 274-291 sketches this.

---

### `flow-lang/Lexing/SimpleLexer.cs` (MODIFIED — ctor param + TryParseNote H gate)

**Analog:** self.

**Existing ctor pattern** (SimpleLexer.cs lines 14-27):
```csharp
public class SimpleLexer
{
    private readonly string _source;
    private readonly ErrorReporter _errorReporter;
    private readonly string? _fileName;
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;
    private readonly Queue<Token> _pendingTokens = new();

    public SimpleLexer(string source, ErrorReporter errorReporter, string? fileName = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _fileName = fileName;
    }
```

**Apply:** Add `private readonly PragmaSet _pragmaSet;` field; add trailing optional ctor param `PragmaSet? pragmaSet = null` (assign `_pragmaSet = pragmaSet ?? PragmaSet.Empty;`). The `?? Empty` fallback is the same null-guard idiom the existing ctor uses for `source`/`errorReporter` — null is rejected via `throw`, but for the new optional param we accept null and substitute Empty. Identical pattern to RESEARCH §"Pattern 2" lines 256-263.

**Existing `TryParseNote`** (SimpleLexer.cs lines 689-721):
```csharp
private bool TryParseNote(string text, out string noteValue)
{
    noteValue = text;

    if (text.Length == 0)
        return false;

    // Only recognize uppercase note names as note literals (A-G)
    char firstChar = text[0];
    if (firstChar < 'A' || firstChar > 'G')
        return false;

    // Don't tokenize bare single letters as notes - they could be variable names
    if (text.Length == 1)
        return false;

    try
    {
        var (note, octave, alteration) = NoteType.Parse(text);
        noteValue = text;
        return true;
    }
    catch
    {
        return false;
    }
}
```

**Apply:** Insert H-substitution block BEFORE the A-G check, as in RESEARCH lines 670-705. Critical: the bare-`H` rule (length==1 → false) must apply just like bare-A through bare-G (Pitfall C in RESEARCH). The probe-with-`B + text[1..]` then `NoteType.Parse(probe)` path returns the canonical `noteValue`.

**Token construction site that wires `OriginalText`** (SimpleLexer.cs lines 643-665):
```csharp
// Try to parse as Note (A-G followed by optional octave and alteration)
if (TryParseNote(text, out var noteValue))
{
    return new Token(TokenType.NoteLiteral, text, start, noteValue);
}

// Check for note + duration suffix (e.g., C4h, D5q, E3w)
if (text.Length >= 3)
{
    char lastChar = text[^1];
    if (lastChar is 'w' or 'h' or 'q' or 'e' or 's' or 't')
    {
        string notePartText = text[..^1];
        if (TryParseNote(notePartText, out var notePartValue))
        {
            // Rewind position by 1 so the duration suffix becomes a separate token
            _position--;
            _column--;
            return new Token(TokenType.NoteLiteral, notePartText, start, notePartValue);
        }
    }
}
```

**Apply (Pitfall D + RESEARCH §Token canonical-vs-original wiring lines 712-731):** Both `new Token(TokenType.NoteLiteral, ...)` calls above must compute `string? originalText = (text != noteValue) ? text : null;` (or `notePartText != notePartValue` for the inner one) and pass it as the 5th arg. Currently the third positional `text` (the FIRST `new Token` line 645) is passed as the Token's `Text` — under canonicalization, `text` is the H-original and `noteValue` is the B-canonical; we want `Text=noteValue`, `OriginalText=text`. RESEARCH lines 712-720 specifies exactly this swap.

---

### `flow-lang/Parsing/Parser.cs` (MODIFIED — ctor param)

**Analog:** self.

**Existing ctor** (Parser.cs lines 18-36):
```csharp
public partial class Parser
{
    private readonly List<Token> _tokens;
    private readonly ErrorReporter _errorReporter;
    private int _current = 0;
    ...
    public Parser(List<Token> tokens, ErrorReporter errorReporter)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }
```

**Apply:** Add `private readonly PragmaSet _pragmaSet;` between `_errorReporter` and the int fields. Add trailing `PragmaSet? pragmaSet = null` to ctor; assign `_pragmaSet = pragmaSet ?? PragmaSet.Empty;`. Mirror SimpleLexer's null-fallback choice.

**Parse() return-statement extension** — existing line 70:
```csharp
return new Program(SourceLocation.Unknown, statements);
```
becomes
```csharp
return new Program(SourceLocation.Unknown, statements, _pragmaSet);
```
This depends on `Program.cs` having gained the third positional field.

---

### `flow-lang/Parsing/Parser.NoteStream.cs` (MOSTLY UNCHANGED)

**Note:** Per CONTEXT.md and RESEARCH (line 32 + Pitfall D), the H→B substitution happens at LEX TIME, so Parser.NoteStream sees only canonicalized B-rooted tokens. No pattern change needed here — the partial class continues to access `_pragmaSet` if a future use case requires note-stream-context-aware parsing, but Phase 21 doesn't need it. Document this clearly so the executor doesn't add speculative wiring.

---

### `flow-lang/Ast/Program.cs` (MODIFIED — additive field)

**Analog:** self (record at line 8).

**Existing** (Program.cs lines 8-10):
```csharp
public record Program(
    SourceLocation Location,
    IReadOnlyList<Statement> Statements) : AstNode(Location);
```

**Apply** (RESEARCH §Program AST extension lines 809-817):
```csharp
public record Program(
    SourceLocation Location,
    IReadOnlyList<Statement> Statements,
    PragmaSet Pragmas) : AstNode(Location)
{
    // Backward-compat overload for tests / LSP that don't care about pragmas
    public Program(SourceLocation location, IReadOnlyList<Statement> statements)
        : this(location, statements, PragmaSet.Empty) { }
}
```

The backward-compat 2-arg overload protects the ~16 existing `new Program(...)` (or just-2-arg call sites in tests / LSP) — additive change without breaking existing call sites. Cross-cutting pattern same as Token additive field.

---

### `flow-lang/Core/FlowEngine.cs` (MODIFIED — pipeline)

**Analog:** self (existing pipeline lines 59-93).

**Existing pipeline shape** (FlowEngine.cs lines 59-93):
```csharp
public bool Execute(string source, string? fileName = null)
{
    _errorReporter.Clear();

    try
    {
        // 1. Lex source into tokens
        var lexer = new SimpleLexer(source, _errorReporter, fileName);
        var tokens = lexer.Tokenize();

        if (_errorReporter.HasErrors)
            return false;

        // 2. Parse tokens into AST
        var parser = new Parser(tokens, _errorReporter);
        var program = parser.Parse();

        if (_errorReporter.HasErrors)
            return false;

        // 3. Type check AST (skipped for now - types checked at runtime)

        _diagnosticOutput?.WriteLine($"[verbose] Executing {fileName ?? "<eval>"}");

        // 4. Interpret AST
        _interpreter.Execute(program);

        return !_errorReporter.HasErrors;
    }
    catch (Exception ex)
    {
        _errorReporter.ReportError($"Unexpected error: {ex.Message}", SourceLocation.Unknown);
        return false;
    }
}
```

**Apply:** Insert step 0 (PragmaScanner.Scan) before step 1 (lex). Thread `pragmaSet` into the SimpleLexer ctor (4th arg) and the Parser ctor (3rd arg). RESEARCH §"FlowEngine integration" lines 740-771 supplies the exact diff. The `if (_errorReporter.HasErrors) return false;` guard after each stage is the existing convention — replicate after the new pre-scan stage.

---

### `flow-lang/Runtime/ModuleLoader.cs` (MODIFIED — mirror FlowEngine)

**Analog:** self (existing `LoadModule` lines 64-79) + the FlowEngine pipeline above.

**Existing module-load pipeline** (ModuleLoader.cs lines 64-79):
```csharp
// 2. Read file contents
var source = File.ReadAllText(resolvedPath);

// 3. Lex and parse with an isolated reporter
var localReporter = new Diagnostics.ErrorReporter();
var lexer = new Lexing.SimpleLexer(source, localReporter, resolvedPath);
var tokens = lexer.Tokenize();

if (localReporter.HasErrors)
{
    _diagnosticOutput?.WriteLine($"[verbose] Failed to lex module: {resolvedPath}");
    _errorReporter.ReportError($"Module '{resolvedPath}' failed to parse due to syntax errors.", errorLocation);
    return ModuleLoadResult.Error;
}

var parser = new Parsing.Parser(tokens, localReporter);
var program = parser.Parse();
```

**Apply:** Insert a `PragmaScanner.Scan(source, resolvedPath, localReporter)` call between steps 2 and 3, threading the resulting `(pragmaSet, transformedSource)` into the SimpleLexer + Parser. RESEARCH §"ModuleLoader integration" lines 778-798 supplies the diff. The `localReporter` (line 67) is the EXACT pattern that closes Pitfall 4 / D-06 structurally — pragma errors in an imported module surface to a separate reporter, never leak into the importer's `_errorReporter`.

---

### `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` (NEW — xUnit unit tests)

**Analog:** `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs` — uses ErrorReporter + SimpleLexer + Parser directly (no FlowEngineRunner) for pure parse-time facts.

**Imports + helper-method pattern** (TupletBracketTests.cs lines 1-52):
```csharp
using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Unit.Phase19;

/// <summary>
/// TUP-01 / TUP-02 / TUP-03 — bracket-form tuplet acceptance Facts.
/// ...
/// </summary>
public class TupletBracketTests
{
    private static SequenceData CompileNoteStream(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");
        ...
    }

    private static (bool hasErrors, string formatted) TryCompileNoteStream(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        parser.Parse();
        return (reporter.HasErrors, reporter.FormatErrors());
    }
```

**Apply:** PragmaScannerFacts.cs uses an analogous static helper:
```csharp
private static (PragmaSet pragmas, string transformed, ErrorReporter reporter) Scan(string source)
{
    var reporter = new ErrorReporter();
    var (pragmas, transformed) = PragmaScanner.Scan(source, fileName: null, reporter);
    return (pragmas, transformed, reporter);
}
```

Then one `[Fact]` per acceptance criterion, naming convention `Method_Scenario_ExpectedResult` (e.g., `EnableHAsB_AtTop_Recognized`). Per RESEARCH Wave 0 list (line 909): 6+ Facts.

**Fact-per-criterion shape** (TupletBracketTests.cs lines 54-70):
```csharp
[Fact]
public void TripletQuarterGroup_ProducesThreeOneTwelfthNotes()
{
    // TUP-01: | {3:2 C4 D4 E4}q | → 3 notes each with DurationFraction = 1/3 quarter (= 1/12 whole)
    var seq = CompileNoteStream("| {3:2 C4 D4 E4}q |");
    var bar = seq.Bars[0];
    Assert.Equal(3, bar.MusicalNotes.Count);
    foreach (var note in bar.MusicalNotes)
    {
        Assert.NotNull(note.DurationFraction);
        Assert.Equal(new Fraction(1, 3), note.DurationFraction);
    }
}
```

---

### `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` (NEW — xUnit unit tests)

**Analog:** Same as above — PragmaRegistry is a pure static class so Facts directly call `PragmaRegistry.IsKnown` / `SuggestNearest` / `AlphabetizedKnownNames` with `Assert.True/Equal/Contains`. Use the TupletBracketTests imports stripped of the FlowEngine-runtime ones (no need for ExecutionContext / SequenceData here).

```csharp
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase21;

public class PragmaRegistryFacts
{
    [Fact]
    public void IsKnown_HAsB_ReturnsTrue()
    {
        Assert.True(PragmaRegistry.IsKnown("hAsB"));
    }
    ...
}
```

Per RESEARCH Wave 0 list (line 910): 4+ Facts (IsKnown true, IsKnown false, AlphabetizedKnownNames sorted, SuggestNearest finds close, SuggestNearest returns null for far-away typo).

---

### `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` (NEW — integration-style unit tests)

**Analog:** `flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs` — uses `FlowEngineRunner` + `[Collection("FlowScripts")]` + stdout substring assertions. Exact match for end-to-end H→B verification facts.

**Test class header pattern** (EnharmonicEdgesTests.cs lines 1-32):
```csharp
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase20;

/// <summary>
/// Phase 20 DEFER-04 multi-letter enharmonic edge Facts.
/// ...
/// </summary>
[Collection("FlowScripts")]
public class EnharmonicEdgesTests
{
    [Fact]
    public void NoKey_E4_RespellsFb4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic E4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("F4-", stdout);
    }
```

**Apply:** HAliasFacts uses identical structure. Each Fact embeds inline `.flow` source (verbatim string) including `enable hAsB;` at the top, runs via `FlowEngineRunner`, asserts `errorCount == 0` and stdout-substring matches the canonical `B`-rooted output (or asserts MIDI equivalence per RESEARCH line 889).

**`[Collection("FlowScripts")]`** is mandatory — the collection serializes tests that touch global Console.SetOut so they don't race. Mirror Phase 20 verbatim.

Per RESEARCH Wave 0 list (line 911): 7+ Facts.

---

### `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` (NEW — paired-fixture integration test)

**Analog:** `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` (FlowEngineRunner + RunSource pattern) crossed with `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (file-on-disk fixture pattern). The Phase 21 isolation fact is closer to the Phase 7 shape because we don't need byte-comparison; we only need to assert that the importer's parse fails when it uses `H4q` while the imported module declares `enable hAsB;`.

**Imports + class header pattern** (RepLAutoImportTests.cs lines 1-26):
```csharp
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase07;

[Collection("FlowScripts")]
public class RepLAutoImportTests
{
    [Fact]
    public void AutoImportedModulesResolve_StdAudioCollections()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
...
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
    }
}
```

**File-on-disk fixture pattern** (ByteIdenticalShowcaseTests.cs lines 32-50):
```csharp
private static void RunTwiceAndCompare(bool isMidi)
{
    string testsRoot = FlowScriptData.FindTestsRoot();
    string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
    string scriptPath = Path.Combine(repoRoot, "examples", "showcase.flow");
    Assert.True(File.Exists(scriptPath), $"showcase.flow missing at {scriptPath}");
    ...
    using (var runner1 = new FlowEngineRunner())
    {
        var (success1, _, stderr1, errorCount1) = runner1.RunSource(sourceRun1);
        ...
    }
}
```

**Apply:** PragmaIsolationFacts.cs does ONE Fact: `RunFile("tests/test_pragma_isolation.flow")` via FlowEngineRunner; the .flow file `use`s `tests/test_pragma_isolation_module.flow` (which declares `enable hAsB;` and emits an H-note successfully internally), then in the importer body uses `H4q` and asserts a parse error / non-zero errorCount.

Use `runner.RunFile(...)` (FlowEngineRunner.cs:23) when the test is a path to a real file (preferred for paired-fixture isolation Facts), or `RunSource(...)` (line 31) for inline literals.

---

### `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` (NEW pair)

**Analog:** `tests/test_enharmonic_edges.flow` + `tests/test_range.flow` — Phase 20 acceptance scripts.

**Convention** (test_enharmonic_edges.flow lines 1-26):
```flow
use "@std"

Note: DEFER-04 multi-letter enharmonic edges (Phase 20 plan 20-02)

Note: E ↔ Fb (same octave) — E4 MIDI 64 → Fb4 MIDI 64
(print (str (enharmonic E4)))
...

(print "test_enharmonic_edges: PASSED")
```

**Apply** — file format conventions (verbatim from existing Phase 20 scripts):
1. First line: `use "@std"` (or relevant module imports).
2. `Note: ...` comments referencing the requirement ID and rationale.
3. One feature exercise per `Note:` block, with `(print ...)` confirming success.
4. Final line: `(print "test_<name>: PASSED")` — used as the integration-loop success signal.

**Pragma-specific addendum:** `tests/test_h_alias.flow` opens with `enable hAsB;` BEFORE the `use "@std"` line. The PragmaScanner's prefix-region rule (D-03) accepts comments / blank lines before pragmas, so the file can have a top header comment, then `enable hAsB;`, then `use "@std"`, then body. Document this layout convention in the test file's leading `Note:` block so future composers writing pragma-aware test scripts copy it.

**`tests/test_h_identifier.flow`:** asserts `Int H = 5;\n(print (str H))` runs error-free and emits `5\n` (no `enable hAsB;` declared — the bare `H` must stay an Identifier). This is the regression gate for Pitfall C.

---

## Shared Patterns

### Authentication / Authorization
**Not applicable** — flow-lang is a single-user CLI with no auth.

### Error Handling — Accumulate via ErrorReporter
**Source:** `flow-lang/Diagnostics/ErrorReporter.cs:33-36`

```csharp
public void ReportError(string message, Core.SourceLocation? location = null)
{
    Report(FlowError.Create(message, location));
}
```

**Apply to:** `PragmaScanner.cs` (D-11 / D-12 emissions), `PragmaRegistry.cs` (the `SuggestNearest` returns null on no match — caller emits; PragmaScanner is the caller). All errors accumulate; pre-scan stage continues past first error. Mirrors CLAUDE.md "Error accumulation" principle. Do NOT throw; do NOT halt-on-first-error.

### Constructor Optional-Param + Null-Fallback
**Source:** `flow-lang/Lexing/SimpleLexer.cs:22-27` (existing) and `flow-lang/Parsing/Parser.cs:32-36` (existing).

```csharp
public SimpleLexer(string source, ErrorReporter errorReporter, string? fileName = null)
{
    _source = source ?? throw new ArgumentNullException(nameof(source));
    _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    _fileName = fileName;
}
```

**Apply to:** SimpleLexer + Parser when adding `PragmaSet? pragmaSet = null` as the trailing optional ctor param. Use `_pragmaSet = pragmaSet ?? PragmaSet.Empty;` (NOT throw — pragmas have a meaningful default). Required-args throw on null; optional-with-default-instance falls back to the static `.Empty`.

### Record + Static Default-Instance
**Source:** `flow-lang/Core/SourceLocation.cs:6-8`

```csharp
public record SourceLocation(int Line, int Column, string? FileName = null)
{
    public static SourceLocation Unknown { get; } = new(0, 0, null);
```

**Apply to:** `PragmaSet.Empty` mirrors `SourceLocation.Unknown` shape exactly. RESEARCH §"PragmaSet record" line 416 already specifies `public static readonly PragmaSet Empty = new(...)`. Records-with-static-default-instance is the project's house style for value-record sentinels.

### Closed-Set Static Lookup with Per-Entry Documentation
**Source:** `flow-lang/Parsing/Parser.NoteStream.cs:13-32` (the `MusicTwentyOneShorthand` table)

**Apply to:** `PragmaRegistry.KnownPragmas` — `IReadOnlyDictionary<string, string>` initialized inline, `StringComparer.Ordinal`, one-line trailing comment per entry citing the originating phase / decision. Phase 21 ships ONE entry; Phase 23 / 24 will append theirs.

### XUnit Test Class Header
**Source:** `flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs:30-32`

```csharp
[Collection("FlowScripts")]
public class EnharmonicEdgesTests
```

**Apply to:** Every Phase 21 Facts class that uses `FlowEngineRunner` (HAliasFacts, PragmaIsolationFacts). The collection prevents Console.SetOut races. PragmaScannerFacts + PragmaRegistryFacts do NOT need the collection (they don't touch FlowEngineRunner / Console).

### Flow Integration-Script PASSED Sentinel
**Source:** `tests/test_enharmonic_edges.flow:26` and `tests/test_range.flow:23`

```flow
(print "test_enharmonic_edges: PASSED")
```

**Apply to:** Last line of every `tests/test_*.flow` script for Phase 21. The `for t in tests/test_*.flow; do ...; done` integration loop relies on this sentinel + zero error-count to confirm success.

---

## No Analog Found

None. Every Phase 21 file has a strong existing analog in the codebase. The closest "novel" piece is the `PragmaScanner.Scan` line-walker, but its idiom (manual char-by-char with line-tracking + `\n` / `\r\n` awareness) is identical to `SimpleLexer.SkipWhitespaceAndComments` (SimpleLexer.cs:798-844).

---

## Metadata

**Analog search scope:**
- `flow-lang/Lexing/` — SimpleLexer.cs, Token.cs, TokenType.cs
- `flow-lang/Parsing/` — Parser.cs, Parser.NoteStream.cs, TypeParser.cs
- `flow-lang/Core/` — FlowEngine.cs, SourceLocation.cs
- `flow-lang/Runtime/` — ModuleLoader.cs, MusicalContext.cs
- `flow-lang/Ast/` — Program.cs, Statements/ImportStatement.cs
- `flow-lang/TypeSystem/` — Fraction.cs (record-struct value-type pattern)
- `flow-lang/Diagnostics/` — ErrorReporter.cs (error-accumulation pattern)
- `flow-lang.Tests/Unit/Phase19/` — TupletBracketTests.cs (lexer/parser unit-Fact pattern)
- `flow-lang.Tests/Unit/Phase20/` — EnharmonicEdgesTests.cs (FlowEngineRunner stdout-substring pattern)
- `flow-lang.Tests/Integration/Phase07/` — RepLAutoImportTests.cs (RunSource integration pattern)
- `flow-lang.Tests/Integration/Phase18/` — ByteIdenticalShowcaseTests.cs (file-on-disk fixture pattern)
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — shared runner harness
- `tests/` — test_enharmonic_edges.flow, test_range.flow (Phase 20 acceptance script convention)

**Files scanned:** 14 production files + 7 test files = 21 total.
**Pattern extraction date:** 2026-04-26
