# Flow DAW Features Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add musical context stack, note streams, lambdas, pattern transforms, chords/harmony, song structure, playback, and effects to the Flow language.

**Architecture:** Each feature extends the existing Lexer → Parser → AST → Interpreter pipeline. New token types in `TokenType.cs`, new AST nodes as `record` types, new cases in `Parser.ParseStatement()` / `ExpressionEvaluator.Evaluate()`, and new built-in functions registered via `BuiltInFunctions.cs`.

**Tech Stack:** .NET 9 / C#, PipeWire/PulseAudio for audio playback (via managed bindings or P/Invoke).

---

## Task 1: Musical Context Stack — Runtime Foundation

The musical context is a stack of scoped state (time signature, tempo, swing, key) that notes inherit. This is the foundation everything else builds on.

**Files:**
- Create: `flow-lang/Runtime/MusicalContext.cs`
- Modify: `flow-lang/Runtime/ExecutionContext.cs`
- Modify: `flow-lang/Runtime/StackFrame.cs`

**Step 1: Create MusicalContext data class**

```csharp
// flow-lang/Runtime/MusicalContext.cs
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Holds the current musical context state for a scope.
/// Each scope can override specific properties; null means "inherit from parent".
/// </summary>
public class MusicalContext
{
    public TimeSignatureData? TimeSignature { get; set; }
    public double? Tempo { get; set; }
    public double? Swing { get; set; }  // 0.0 to 1.0 (0.5 = straight, 0.67 = triplet swing)
    public string? Key { get; set; }    // e.g., "Cmajor", "Aminor"

    /// <summary>
    /// Creates a new context with all values inherited (null).
    /// </summary>
    public MusicalContext() { }

    /// <summary>
    /// Creates a copy of this context.
    /// </summary>
    public MusicalContext Clone() => new()
    {
        TimeSignature = TimeSignature,
        Tempo = Tempo,
        Swing = Swing,
        Key = Key
    };
}
```

**Step 2: Add MusicalContext to StackFrame**

In `flow-lang/Runtime/StackFrame.cs`, add:
```csharp
public MusicalContext? MusicalContext { get; set; }
```

**Step 3: Add context resolution to ExecutionContext**

In `flow-lang/Runtime/ExecutionContext.cs`, add a method that walks the stack to resolve the current musical context (merging parent values for any null properties):

```csharp
public MusicalContext GetMusicalContext()
{
    var resolved = new MusicalContext();
    // Walk stack from top to bottom, first non-null wins
    foreach (var frame in _callStack)
    {
        if (frame.MusicalContext != null)
        {
            resolved.TimeSignature ??= frame.MusicalContext.TimeSignature;
            resolved.Tempo ??= frame.MusicalContext.Tempo;
            resolved.Swing ??= frame.MusicalContext.Swing;
            resolved.Key ??= frame.MusicalContext.Key;
        }
        if (resolved.TimeSignature != null && resolved.Tempo != null
            && resolved.Swing != null && resolved.Key != null)
            break; // All resolved
    }
    // Defaults
    resolved.TimeSignature ??= new TimeSignatureData(4, 4);
    resolved.Tempo ??= 120.0;
    resolved.Swing ??= 0.5;
    return resolved;
}
```

**Step 4: Build and verify it compiles**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

**Step 5: Commit**

```bash
git add flow-lang/Runtime/MusicalContext.cs flow-lang/Runtime/ExecutionContext.cs flow-lang/Runtime/StackFrame.cs
git commit -m "feat: add MusicalContext runtime foundation"
```

---

## Task 2: Musical Context Stack — Lexer & Parser

Add `timesig`, `tempo`, `swing`, and `key` as keywords that open scoped blocks.

**Files:**
- Modify: `flow-lang/Lexing/TokenType.cs`
- Modify: `flow-lang/Lexing/SimpleLexer.cs`
- Create: `flow-lang/Ast/Statements/MusicalContextStatement.cs`
- Modify: `flow-lang/Parsing/Parser.cs`

**Step 1: Add token types**

In `flow-lang/Lexing/TokenType.cs`, add to the Keywords section (after `Fn`):
```csharp
Timesig,
Tempo,
Swing,
Key,
LBrace,   // {
RBrace,   // }
```

**Step 2: Add keyword recognition in SimpleLexer**

In `SimpleLexer.cs`, add keyword mappings for `"timesig"`, `"tempo"`, `"swing"`, `"key"` in the keyword recognition section. Add `{` and `}` as single-character token recognition in `NextToken()`.

**Step 3: Create AST node**

```csharp
// flow-lang/Ast/Statements/MusicalContextStatement.cs
using FlowLang.Core;

namespace FlowLang.Ast.Statements;

public enum MusicalContextType { Timesig, Tempo, Swing, Key }

public record MusicalContextStatement(
    SourceLocation Location,
    MusicalContextType ContextType,
    Expression Value,                    // The value expression (e.g., "4/4", 120, 0.6, "Cmajor")
    Expression? Value2,                  // Optional second value (denominator for timesig)
    IReadOnlyList<Statement> Body
) : Statement(Location);
```

**Step 4: Add parser method**

In `Parser.cs`, add `ParseMusicalContextStatement()` that:
1. Reads the context type (already consumed by `Match`)
2. Parses the value (for `timesig`: two ints separated by `/`; for `tempo`: a number; for `swing`: a percentage; for `key`: an identifier)
3. Expects `{`
4. Parses body statements until `}`
5. Returns `MusicalContextStatement`

Add cases in `ParseStatement()` before the type keyword check:
```csharp
if (Match(TokenType.Timesig)) return ParseMusicalContextStatement(MusicalContextType.Timesig);
if (Match(TokenType.Tempo)) return ParseMusicalContextStatement(MusicalContextType.Tempo);
if (Match(TokenType.Swing)) return ParseMusicalContextStatement(MusicalContextType.Swing);
if (Match(TokenType.Key)) return ParseMusicalContextStatement(MusicalContextType.Key);
```

**Step 5: Build and verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

**Step 6: Commit**

```bash
git add flow-lang/Lexing/TokenType.cs flow-lang/Lexing/SimpleLexer.cs flow-lang/Ast/Statements/MusicalContextStatement.cs flow-lang/Parsing/Parser.cs
git commit -m "feat: add musical context block parsing (timesig, tempo, swing, key)"
```

---

## Task 3: Musical Context Stack — Interpreter

Wire the parsed `MusicalContextStatement` into the interpreter so it pushes/pops context during execution.

**Files:**
- Modify: `flow-lang/Interpreter/Interpreter.cs`

**Step 1: Add case to ExecuteStatement switch**

```csharp
case MusicalContextStatement ctx:
    ExecuteMusicalContext(ctx);
    break;
```

**Step 2: Implement ExecuteMusicalContext**

```csharp
private void ExecuteMusicalContext(MusicalContextStatement ctx)
{
    _context.PushFrame();
    try
    {
        var musicalCtx = new MusicalContext();

        switch (ctx.ContextType)
        {
            case MusicalContextType.Timesig:
                var num = _evaluator.Evaluate(ctx.Value);
                var den = _evaluator.Evaluate(ctx.Value2!);
                musicalCtx.TimeSignature = new TimeSignatureData(num.As<int>(), den.As<int>());
                break;
            case MusicalContextType.Tempo:
                var tempoVal = _evaluator.Evaluate(ctx.Value);
                musicalCtx.Tempo = tempoVal.As<double>();
                break;
            case MusicalContextType.Swing:
                var swingVal = _evaluator.Evaluate(ctx.Value);
                musicalCtx.Swing = swingVal.As<double>();
                break;
            case MusicalContextType.Key:
                musicalCtx.Key = ((LiteralExpression)ctx.Value).Value as string;
                break;
        }

        _context.CurrentFrame.MusicalContext = musicalCtx;

        foreach (var stmt in ctx.Body)
        {
            ExecuteStatement(stmt);
            if (_returnValue != null) break;
        }
    }
    finally
    {
        _context.PopFrame();
    }
}
```

**Step 3: Write a test script**

Create `tests/test_musical_context.flow`:
```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        (print "inside 4/4 at 120 bpm")
    }
    timesig 3/4 {
        (print "inside 3/4 at 120 bpm")
    }
}
(print "outside context blocks")
```

**Step 4: Run the test**

Run: `dotnet run --project flow-interpreter tests/test_musical_context.flow`
Expected: Prints all three messages without errors

**Step 5: Commit**

```bash
git add flow-lang/Interpreter/Interpreter.cs tests/test_musical_context.flow
git commit -m "feat: execute musical context blocks with push/pop scoping"
```

---

## Task 4: Note Streams — Lexer

Add the `|` (pipe/bar line) token for note stream delimiters, and the `_` (rest) token.

**Files:**
- Modify: `flow-lang/Lexing/TokenType.cs`
- Modify: `flow-lang/Lexing/SimpleLexer.cs`

**Step 1: Add token types**

In `TokenType.cs`:
```csharp
Pipe,          // | (note stream bar delimiter)
Underscore,    // _ (rest)
Tilde,         // ~ (tie)
```

**Step 2: Add recognition in SimpleLexer**

In `NextToken()`, add cases for `|`, `_` (as single-char tokens), and `~`. The underscore should be recognized as `TokenType.Underscore` when it appears standalone (not part of an identifier).

**Step 3: Build and verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add flow-lang/Lexing/TokenType.cs flow-lang/Lexing/SimpleLexer.cs
git commit -m "feat: add pipe, underscore, and tilde tokens for note streams"
```

---

## Task 5: Note Streams — AST & Parser

Parse `| C4 D4 E4 F4 |` into a `NoteStreamExpression` AST node.

**Files:**
- Create: `flow-lang/Ast/Expressions/NoteStreamExpression.cs`
- Modify: `flow-lang/Parsing/Parser.cs`

**Step 1: Create AST nodes for note stream elements**

```csharp
// flow-lang/Ast/Expressions/NoteStreamExpression.cs
using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A single element in a note stream — a note, rest, or chord.
/// </summary>
public abstract record NoteStreamElement(SourceLocation Location);

/// <summary>
/// A single note with optional duration suffix and modifiers.
/// e.g., C4, C4q, C4q., C4+50c
/// </summary>
public record NoteElement(
    SourceLocation Location,
    string NoteName,          // e.g., "C4", "D#5", "Ebb3"
    string? DurationSuffix,   // w, h, q, e, s, t (null = auto-fit)
    bool IsDotted,            // e.g., C4q.
    bool IsTied,              // e.g., C4h~
    double? CentOffset        // e.g., +50c, -25c (null = none)
) : NoteStreamElement(Location);

/// <summary>
/// A rest with optional duration.
/// e.g., _, _h, _q
/// </summary>
public record RestElement(
    SourceLocation Location,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// Simultaneous notes (chord bracket notation).
/// e.g., [C4 E4 G4]q
/// </summary>
public record ChordElement(
    SourceLocation Location,
    IReadOnlyList<string> Notes,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// A bar within a note stream, delimited by | ... |
/// </summary>
public record NoteStreamBar(
    SourceLocation Location,
    IReadOnlyList<NoteStreamElement> Elements
);

/// <summary>
/// A complete note stream expression: | C4 D4 | E4 F4 |
/// Evaluates to a Sequence.
/// </summary>
public record NoteStreamExpression(
    SourceLocation Location,
    IReadOnlyList<NoteStreamBar> Bars
) : Expression(Location);
```

**Step 2: Add parser method**

In `Parser.cs`, add `ParseNoteStream()` that:
1. Reads elements between `|` delimiters
2. Recognizes note literals, rests (`_`), chord brackets (`[...]`), duration suffixes, dots, and ties
3. Splits into bars at each `|`
4. Returns `NoteStreamExpression`

**Step 3: Add case in ParsePrimary**

Before the `LBracket` array literal case:
```csharp
if (Match(TokenType.Pipe))
{
    return ParseNoteStream();
}
```

**Step 4: Write a test script**

Create `tests/test_note_streams.flow`:
```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    (print (str mel))
}
```

**Step 5: Build and verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

**Step 6: Commit**

```bash
git add flow-lang/Ast/Expressions/NoteStreamExpression.cs flow-lang/Parsing/Parser.cs
git commit -m "feat: parse note stream expressions (| C4 D4 E4 F4 |)"
```

---

## Task 6: Note Streams — Evaluator

Evaluate `NoteStreamExpression` into a `Sequence` value using the active musical context.

**Files:**
- Modify: `flow-lang/Interpreter/ExpressionEvaluator.cs`
- Create: `flow-lang/Runtime/NoteStreamCompiler.cs`

**Step 1: Create NoteStreamCompiler**

A helper class that converts a `NoteStreamExpression` + `MusicalContext` into a `SequenceData`:

```csharp
// flow-lang/Runtime/NoteStreamCompiler.cs
namespace FlowLang.Runtime;

/// <summary>
/// Compiles a NoteStreamExpression into a SequenceData using the active MusicalContext.
/// Handles auto-fit duration calculation, rest insertion, and bar validation.
/// </summary>
public class NoteStreamCompiler
{
    public SequenceData Compile(NoteStreamExpression noteStream, MusicalContext context)
    {
        var sequence = new SequenceData();
        var timeSig = context.TimeSignature!;

        foreach (var bar in noteStream.Bars)
        {
            var barData = CompileBar(bar, timeSig);
            sequence.AddBar(barData);
        }

        return sequence;
    }

    private BarData CompileBar(NoteStreamBar bar, TimeSignatureData timeSig)
    {
        // Count non-rest elements to determine auto-fit duration
        // If no explicit durations, divide bar evenly among elements
        // Map duration suffixes: w=whole, h=half, q=quarter, e=eighth, s=sixteenth, t=32nd
        // Handle dotted notes (1.5x duration)
        // Create MusicalNoteData for each element
        // Return BarData in Musical mode with time signature
        // ...
    }
}
```

The auto-fit logic:
- Count elements in the bar
- Calculate total beats from time signature (numerator)
- If all elements have explicit durations, use those
- If no elements have explicit durations, divide beats evenly: `beatsPerNote = numerator / elementCount`
- Map that to the closest NoteValue enum
- Handle mixed (some explicit, some auto-fit) by computing remaining beats

**Step 2: Add case to ExpressionEvaluator.Evaluate()**

```csharp
NoteStreamExpression noteStream => EvaluateNoteStream(noteStream),
```

**Step 3: Implement EvaluateNoteStream**

```csharp
private Value EvaluateNoteStream(NoteStreamExpression noteStream)
{
    var context = _context.GetMusicalContext();
    var compiler = new NoteStreamCompiler();
    var sequence = compiler.Compile(noteStream, context);
    return Value.Sequence(sequence);
}
```

**Step 4: Run the test**

Run: `dotnet run --project flow-interpreter tests/test_note_streams.flow`
Expected: Prints `Sequence[1 bars, 4 beats total]`

**Step 5: Commit**

```bash
git add flow-lang/Runtime/NoteStreamCompiler.cs flow-lang/Interpreter/ExpressionEvaluator.cs
git commit -m "feat: evaluate note streams into Sequences with auto-fit duration"
```

---

## Task 7: Lambda Functions — Fix `fn` Parsing & Evaluation

The `fn` token and `LambdaExpression` AST node already exist, and `ParseLambdaExpression()` is implemented. However the lambda system needs testing and may need fixes to work end-to-end with the `@notation` module.

**Files:**
- Modify: `flow-lang/Parsing/Parser.cs` (if fixes needed)
- Modify: `flow-lang/Interpreter/ExpressionEvaluator.cs` (if fixes needed)
- Create: `tests/test_lambdas.flow`

**Step 1: Write a comprehensive lambda test**

```flow
use "@std"

; Basic lambda assigned to variable
(Note => Note) up5 = fn Note n => (n -> transpose 5st)

; Lambda with map
Note[] notes = (list C4 D4 E4)
Note[] raised = notes -> map (fn Note n => (n -> transpose 5st))

; Lambda with no params
(Void => Int) getRandom = fn => (? 1 10)

; Multi-param lambda
(Int, Int => Int) adder = fn Int a, Int b => (add a b)
Int result = (adder 3 4)
(print (str result))
```

**Step 2: Run the test, identify and fix any issues**

Run: `dotnet run --project flow-interpreter tests/test_lambdas.flow`
Expected: Should print `7`

Common issues to fix:
- `FunctionType` may not be recognized by `IsTypeKeyword` — add `(` as a type keyword start for function types like `(Note => Note)`
- Lambda variable calls may fail in overload resolution — ensure `EvaluateFunctionCall` checks variable-held functions
- The `@notation` module uses `fn` with implicit return from parenthesized body — verify this works

**Step 3: Verify @notation loads**

Run: `dotnet run --project flow-interpreter -e 'use "@notation"'`
Expected: No errors

**Step 4: Commit**

```bash
git add flow-lang/Parsing/Parser.cs flow-lang/Interpreter/ExpressionEvaluator.cs tests/test_lambdas.flow
git commit -m "feat: fix lambda functions end-to-end, unblock @notation"
```

---

## Task 8: Pattern Transforms — Built-in Functions

Register `transpose`, `invert`, `retrograde`, `augment`, `diminish`, `repeat`, `concat`, `up`, `down` as built-in functions that operate on `Sequence` values.

**Files:**
- Create: `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`
- Modify: `flow-lang/StandardLibrary/BuiltInFunctions.cs`
- Modify: `flow-lang/Core/FlowEngine.cs` (if new registration method needed)

**Step 1: Create TransformFunctions**

```csharp
// flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
namespace FlowLang.StandardLibrary.Transforms;

public static class TransformFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        // transpose(Sequence, Semitone) -> Sequence
        // transpose(Sequence, Cent) -> Sequence
        // invert(Sequence) -> Sequence
        // retrograde(Sequence) -> Sequence
        // augment(Sequence) -> Sequence
        // diminish(Sequence) -> Sequence
        // repeat(Sequence, Int) -> Sequence
        // repeat(Sequence, Int, Semitone) -> Sequence (cumulative transposition)
        // concat(Sequence, Sequence) -> Sequence
        // up(Sequence, Int) -> Sequence (octave shift)
        // down(Sequence, Int) -> Sequence
    }
}
```

Each transform creates a new `SequenceData` by transforming the notes in the input sequence. For `transpose`, shift each note's pitch by the semitone/cent amount. For `retrograde`, reverse the note order within each bar. For `augment`/`diminish`, double/halve each note's NoteValue enum.

**Step 2: Register in BuiltInFunctions**

In `RegisterAllImplementations()`, add:
```csharp
Transforms.TransformFunctions.Register(registry);
```

**Step 3: Write test**

```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence transposed = mel -> transpose 5st
    Sequence reversed = mel -> retrograde
    Sequence doubled = mel -> repeat 2
    (print (str transposed))
    (print (str reversed))
    (print (str doubled))
}
```

**Step 4: Run test**

Run: `dotnet run --project flow-interpreter tests/test_transforms.flow`
Expected: Three sequence descriptions printed

**Step 5: Commit**

```bash
git add flow-lang/StandardLibrary/Transforms/TransformFunctions.cs flow-lang/StandardLibrary/BuiltInFunctions.cs tests/test_transforms.flow
git commit -m "feat: add pattern transforms (transpose, invert, retrograde, augment, diminish, repeat)"
```

---

## Task 9: Chords & Harmony — Chord Parsing

Add chord literal recognition (`Cmaj7`, `Dm`, `G7`, etc.) to the lexer, and a chord expansion system.

**Files:**
- Create: `flow-lang/TypeSystem/SpecialTypes/ChordType.cs`
- Create: `flow-lang/StandardLibrary/Harmony/ChordParser.cs`
- Create: `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`
- Modify: `flow-lang/Lexing/TokenType.cs`
- Modify: `flow-lang/Lexing/SimpleLexer.cs`
- Modify: `flow-lang/Runtime/Value.cs`
- Modify: `flow-lang/StandardLibrary/BuiltInFunctions.cs`

**Step 1: Create ChordType and ChordData**

```csharp
// flow-lang/TypeSystem/SpecialTypes/ChordType.cs
namespace FlowLang.TypeSystem.SpecialTypes;

public class ChordData
{
    public string Root { get; }         // "C", "D", "E", etc.
    public string Quality { get; }      // "maj", "min", "dim", "aug", "dom7", "maj7", etc.
    public int Octave { get; }          // Default 4
    public string? BassNote { get; }    // For slash chords: "E" in C/E
    public string[] NoteNames { get; }  // Expanded notes: ["C4", "E4", "G4"]

    // Constructor, expansion logic
}

public sealed class ChordType : FlowType
{
    public static ChordType Instance { get; } = new();
    public override string Name => "Chord";
    public override int GetSpecificity() => 136;
}
```

**Step 2: Create ChordParser**

A static class that parses chord symbol strings into `ChordData` objects. Handles:
- Root note (A-G with optional # or b)
- Quality (maj, min, dim, aug, 7, maj7, m7, dim7, m7b5, 9, 11, 13, sus2, sus4, alt, 7#9, 7b13)
- Slash chords (C/E)
- Octave override (Cmaj:3)

Expands each chord to an array of note names using interval tables.

**Step 3: Create HarmonyFunctions**

Register built-in functions:
- `voicing(Chord, String) -> Chord` — apply voicing (close, open, drop2, drop3, shell)
- `arpeggio(Chord, String) -> Sequence` — convert to sequential notes (up, down, updown)
- `chordNotes(Chord) -> Note[]` — extract notes as array

**Step 4: Add chord recognition to lexer**

In `SimpleLexer.cs`, when scanning an identifier that starts with A-G and is followed by chord quality characters (m, maj, dim, aug, 7, sus, etc.), emit a `ChordLiteral` token instead of an `Identifier`.

This requires careful lookahead to distinguish `Cmaj7` (chord) from `Counter` (identifier). The heuristic: if the identifier starts with a single uppercase A-G letter followed by known chord quality suffixes, it's a chord.

**Step 5: Add `Value.Chord()` factory method**

In `Value.cs`:
```csharp
public static Value Chord(ChordData value) => new(value, ChordType.Instance);
```

**Step 6: Register harmony functions in BuiltInFunctions**

**Step 7: Write test**

```flow
use "@std"

Chord c = Cmaj7
(print (str c))

Chord[] prog = (list Dm7 G7 Cmaj7)
(print (str prog))
```

**Step 8: Build and run test**

Run: `dotnet run --project flow-interpreter tests/test_chords.flow`

**Step 9: Commit**

```bash
git add flow-lang/TypeSystem/SpecialTypes/ChordType.cs flow-lang/StandardLibrary/Harmony/ flow-lang/Lexing/ flow-lang/Runtime/Value.cs flow-lang/StandardLibrary/BuiltInFunctions.cs tests/test_chords.flow
git commit -m "feat: add chord literals, parsing, voicings, and arpeggiation"
```

---

## Task 10: Chords & Harmony — Roman Numerals in Key Context

Inside a `key` block, parse roman numeral literals (I, ii, IV, V, vi, etc.) and expand them to chords based on the active key.

**Files:**
- Create: `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs`
- Modify: `flow-lang/Lexing/SimpleLexer.cs`
- Modify: `flow-lang/Lexing/TokenType.cs`
- Modify: `flow-lang/Runtime/NoteStreamCompiler.cs`

**Step 1: Create ScaleDatabase**

Maps key names to scales, and scale degrees to chord qualities:
- Major key: I=maj, ii=min, iii=min, IV=maj, V=maj, vi=min, vii=dim
- Minor key: i=min, ii=dim, III=maj, iv=min, v=min, VI=maj, VII=maj
- Include common jazz extensions: ii=min7, V=dom7, I=maj7, vi=min7

**Step 2: Add roman numeral token recognition**

The lexer should recognize sequences of I, V, i, v characters (optionally followed by numbers like 7) as `RomanNumeralLiteral` tokens when they match known patterns: I, II, III, IV, V, VI, VII (and lowercase variants), optionally followed by 7, maj7, etc.

**Step 3: Integrate with NoteStreamCompiler**

When the compiler encounters a roman numeral element inside a note stream, it resolves it using the active key from `MusicalContext` and expands it to a chord.

**Step 4: Write test**

```flow
use "@std"

key Cmajor {
    timesig 4/4 {
        Sequence prog = | I IV V I |
        (print (str prog))
    }
}
```

**Step 5: Run test**

Run: `dotnet run --project flow-interpreter tests/test_roman_numerals.flow`

**Step 6: Commit**

```bash
git add flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs flow-lang/Lexing/ flow-lang/Runtime/NoteStreamCompiler.cs tests/test_roman_numerals.flow
git commit -m "feat: add roman numeral chord resolution within key context"
```

---

## Task 11: Song Structure — Section & Song Types

Add `section` and `Song` as language constructs for arrangement.

**Files:**
- Create: `flow-lang/TypeSystem/SpecialTypes/SectionType.cs`
- Create: `flow-lang/TypeSystem/SpecialTypes/SongType.cs`
- Create: `flow-lang/Ast/Statements/SectionDeclaration.cs`
- Create: `flow-lang/Ast/Expressions/SongExpression.cs`
- Modify: `flow-lang/Lexing/TokenType.cs`
- Modify: `flow-lang/Lexing/SimpleLexer.cs`
- Modify: `flow-lang/Parsing/Parser.cs`
- Modify: `flow-lang/Interpreter/Interpreter.cs`
- Modify: `flow-lang/Interpreter/ExpressionEvaluator.cs`
- Modify: `flow-lang/Runtime/Value.cs`

**Step 1: Create SectionData and SongData runtime types**

```csharp
// SectionData holds: name, musical context, list of named sequences
// SongData holds: ordered list of section references, rendered combines them
```

**Step 2: Add token types**

`Section`, `Song` keywords.

**Step 3: Create AST nodes**

`SectionDeclaration` — named block with optional parent (`: parentName`), contains musical context + body statements.
`SongExpression` — `[sectionName sectionName*2 ...]` list of section references with optional repeat counts.

**Step 4: Add parser methods**

`ParseSectionDeclaration()` — parses `section name { ... }` and `section name : parent { ... }`
Song array syntax — `[intro verse chorus]` parsed as `SongExpression` when inside a `Song` variable declaration.

**Step 5: Add interpreter execution**

Section declarations register the section in the execution context (like a proc, but stores musical data).
Song expressions resolve section names and build a `SongData`.

**Step 6: Add render function**

Register `render(Song) -> Buffer` built-in that:
1. Iterates through sections in order
2. For each section, renders all sequences to voices/tracks
3. Mixes down to a single buffer

**Step 7: Write test**

```flow
use "@std"
use "@audio"

section intro {
    tempo 120 {
        timesig 4/4 {
            Sequence melody = | C4 E4 G4 C5 |
        }
    }
}

section verse {
    tempo 120 {
        timesig 4/4 {
            Sequence melody = | A4 C5 E5 D5 | C5 A4 G4 E4 |
        }
    }
}

Song mysong = [intro verse verse]
(print (str mysong))
```

**Step 8: Run test**

Run: `dotnet run --project flow-interpreter tests/test_song_structure.flow`

**Step 9: Commit**

```bash
git add flow-lang/TypeSystem/SpecialTypes/SectionType.cs flow-lang/TypeSystem/SpecialTypes/SongType.cs flow-lang/Ast/Statements/SectionDeclaration.cs flow-lang/Ast/Expressions/SongExpression.cs flow-lang/Lexing/ flow-lang/Parsing/Parser.cs flow-lang/Interpreter/ flow-lang/Runtime/Value.cs tests/test_song_structure.flow
git commit -m "feat: add section declarations, Song type, and arrangement syntax"
```

---

## Task 12: Generative Features — Random in Note Streams

Extend note stream parsing to support `?` and `??` random choice syntax within note streams.

**Files:**
- Modify: `flow-lang/Ast/Expressions/NoteStreamExpression.cs`
- Modify: `flow-lang/Parsing/Parser.cs`
- Modify: `flow-lang/Runtime/NoteStreamCompiler.cs`

**Step 1: Add RandomChoiceElement to AST**

```csharp
/// <summary>
/// Random note choice: (? C4 D4 E4) or (?? C4 D4 E4)
/// Optional weights: (? C4:50 E4:30 G4:20)
/// </summary>
public record RandomChoiceElement(
    SourceLocation Location,
    IReadOnlyList<(string Note, int? Weight)> Choices,
    bool IsSeeded     // true = ??, false = ?
) : NoteStreamElement(Location);

/// <summary>
/// Probabilistic note: C4?70 (70% chance of playing, 30% rest)
/// </summary>
public record ProbabilisticNoteElement(
    SourceLocation Location,
    string NoteName,
    string? DurationSuffix,
    int Probability,  // 0-100
    bool IsSeeded
) : NoteStreamElement(Location);
```

**Step 2: Extend note stream parser**

In `ParseNoteStream()`, when encountering `(` followed by `?` or `??`, parse the choice list. When encountering a note followed by `?` or `??` and a number, parse as probabilistic.

**Step 3: Extend NoteStreamCompiler**

When compiling a `RandomChoiceElement`, use the `?`/`??` random functions from the existing runtime to select a note. For probabilistic notes, roll against the probability and emit either the note or a rest.

**Step 4: Register Euclidean rhythm function**

Add `euclidean(Int hits, Int steps, Note pitch) -> Sequence` built-in using the Bjorklund algorithm.

**Step 5: Write test**

```flow
use "@std"

timesig 4/4 {
    Sequence rand = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |
    (print (str rand))
}

Sequence euclid = (euclidean 5 8 C4)
(print (str euclid))
```

**Step 6: Run test and commit**

```bash
git commit -m "feat: add random choice and probabilistic notes in note streams, euclidean rhythms"
```

---

## Task 13: Playback — Audio Backend Abstraction

Create the `IAudioBackend` interface and PipeWire/PulseAudio implementations.

**Files:**
- Create: `flow-lang/Audio/IAudioBackend.cs`
- Create: `flow-lang/Audio/PipeWireBackend.cs`
- Create: `flow-lang/Audio/PulseAudioBackend.cs`
- Create: `flow-lang/Audio/AudioPlaybackManager.cs`

**Step 1: Define IAudioBackend interface**

```csharp
// flow-lang/Audio/IAudioBackend.cs
namespace FlowLang.Audio;

public interface IAudioBackend : IDisposable
{
    /// <summary>
    /// Initialize the audio backend and connect to the audio server.
    /// </summary>
    bool Initialize(int sampleRate, int channels);

    /// <summary>
    /// Play a buffer of float samples. Blocks until playback completes.
    /// </summary>
    void Play(float[] samples, int sampleRate, int channels);

    /// <summary>
    /// Stop any currently playing audio.
    /// </summary>
    void Stop();

    /// <summary>
    /// List available audio output devices.
    /// </summary>
    IReadOnlyList<string> GetDevices();

    /// <summary>
    /// Set the active output device.
    /// </summary>
    bool SetDevice(string deviceName);

    /// <summary>
    /// Whether this backend is available on the current system.
    /// </summary>
    static abstract bool IsAvailable();
}
```

**Step 2: Implement PipeWireBackend**

Use P/Invoke to call `libpipewire` or `libpulse` (PipeWire implements the PulseAudio API). The simplest approach is to use the PulseAudio Simple API (`pa_simple`) which PipeWire supports:

- `pa_simple_new()` — open a stream
- `pa_simple_write()` — write samples
- `pa_simple_drain()` — wait for playback to finish
- `pa_simple_free()` — close

This gives us cross-compatibility with both PipeWire and PulseAudio systems.

**Step 3: Create AudioPlaybackManager**

Auto-detects available backends, provides a singleton access point:

```csharp
public class AudioPlaybackManager
{
    private IAudioBackend? _backend;

    public IAudioBackend GetBackend()
    {
        _backend ??= DetectBackend();
        return _backend;
    }

    private IAudioBackend DetectBackend()
    {
        // Try PipeWire first, then PulseAudio
        if (PipeWireBackend.IsAvailable()) return new PipeWireBackend();
        if (PulseAudioBackend.IsAvailable()) return new PulseAudioBackend();
        throw new PlatformNotSupportedException("No audio backend available");
    }
}
```

**Step 4: Build and verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

**Step 5: Commit**

```bash
git add flow-lang/Audio/
git commit -m "feat: add IAudioBackend abstraction with PipeWire/PulseAudio implementation"
```

---

## Task 14: Playback — `play`, `loop`, `preview` Built-in Functions

Register playback functions that use the audio backend.

**Files:**
- Create: `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs`
- Modify: `flow-lang/StandardLibrary/BuiltInFunctions.cs`
- Modify: `flow-lang/Core/FlowEngine.cs`

**Step 1: Create PlaybackFunctions**

```csharp
// flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
namespace FlowLang.StandardLibrary.Audio;

public static class PlaybackFunctions
{
    private static AudioPlaybackManager? _manager;

    public static void Register(InternalFunctionRegistry registry)
    {
        _manager = new AudioPlaybackManager();

        // play(Buffer) -> Void
        var playBufferSig = new FunctionSignature("play", [BufferType.Instance]);
        registry.Register("play", playBufferSig, PlayBuffer);

        // play(Sequence) -> Void (renders then plays)
        var playSeqSig = new FunctionSignature("play", [SequenceType.Instance]);
        registry.Register("play", playSeqSig, PlaySequence);

        // loop(Buffer) -> Void
        // loop(Buffer, Int) -> Void (N times)
        // preview(Buffer) -> Void (low quality)
        // playAt(Buffer, Double) -> Void (tempo override)

        // audioDevices() -> String[]
        // setAudioDevice(String) -> Void
    }

    private static Value PlayBuffer(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var backend = _manager!.GetBackend();
        backend.Play(buffer.ToFloatArray(), buffer.SampleRate, buffer.Channels);
        return Value.Void();
    }
}
```

**Step 2: Register in BuiltInFunctions**

Add `PlaybackFunctions.Register(registry);` to `RegisterAllImplementations()`.

**Step 3: Wire AudioPlaybackManager through FlowEngine**

The manager should be created once in `FlowEngine` and passed to `PlaybackFunctions.Register()`.

**Step 4: Write test**

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.3)
tone -> play
```

**Step 5: Run test (requires audio hardware)**

Run: `dotnet run --project flow-interpreter tests/test_playback.flow`
Expected: Hear a 440Hz tone for 0.5 seconds

**Step 6: Commit**

```bash
git add flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs flow-lang/StandardLibrary/BuiltInFunctions.cs flow-lang/Core/FlowEngine.cs tests/test_playback.flow
git commit -m "feat: add play, loop, preview built-in functions with audio output"
```

---

## Task 15: Playback — REPL Integration & `--watch` Mode

Make `play` work in the REPL and add file watching.

**Files:**
- Modify: `flow-interpreter/Program.cs` (or equivalent REPL entry point)
- Modify: `flow-interpreter/Repl.cs` (if exists)

**Step 1: Ensure REPL evaluates `play` correctly**

The REPL already evaluates expression statements. `tone -> play` should work if `play` is registered as a built-in. Verify the REPL session maintains audio backend state between evaluations.

**Step 2: Add `--watch` flag**

In the CLI entry point, add a `--watch` option that:
1. Parses and executes the script once
2. Sets up a `FileSystemWatcher` on the script file
3. On change, re-executes the script
4. Stops any currently playing audio before re-executing

```csharp
if (args.Contains("--watch"))
{
    var filePath = args[0];
    var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!);
    watcher.Filter = Path.GetFileName(filePath);
    watcher.Changed += (s, e) =>
    {
        Console.WriteLine("Change detected, re-rendering...");
        engine.Execute(File.ReadAllText(filePath), filePath);
    };
    watcher.EnableRaisingEvents = true;

    // Initial execution
    engine.Execute(File.ReadAllText(filePath), filePath);
    Console.WriteLine("Watching for changes... (Ctrl+C to stop)");
    Thread.Sleep(Timeout.Infinite);
}
```

**Step 3: Add `--device` flag**

Parse `--device "name"` from CLI args and call `setAudioDevice` before execution.

**Step 4: Test REPL**

```
$ dotnet run --project flow-interpreter
flow> use "@audio"
flow> Buffer t = (createSineTone 0.5 440.0 0.3)
flow> t -> play
```

**Step 5: Commit**

```bash
git add flow-interpreter/
git commit -m "feat: add REPL playback support and --watch file monitoring mode"
```

---

## Task 16: Effects — DSP Functions

Implement core audio effects as built-in functions.

**Files:**
- Create: `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs`
- Create: `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs`
- Create: `flow-lang/StandardLibrary/Audio/DSP/Filter.cs`
- Create: `flow-lang/StandardLibrary/Audio/DSP/Compressor.cs`
- Create: `flow-lang/StandardLibrary/Audio/DSP/Delay.cs`
- Modify: `flow-lang/StandardLibrary/BuiltInFunctions.cs`

**Step 1: Implement DSP algorithms**

Each DSP module operates on `AudioBuffer` and returns a new `AudioBuffer`:

- **Reverb** — Schroeder reverb (4 comb filters + 2 allpass filters). Parameters: room size (0-1), damping (0-1), wet/dry mix (0-1).
- **Filter** — Biquad filter (lowpass, highpass, bandpass). Parameters: cutoff frequency (Hz), resonance (Q).
- **Compressor** — Simple dynamic range compressor. Parameters: threshold (dB), ratio, attack (ms), release (ms).
- **Delay** — Feedback delay line. Parameters: delay time (ms or beat fraction), feedback (0-1), wet/dry mix (0-1).
- **Gain** — Simple amplitude scaling. Parameter: gain (dB).

**Step 2: Register as built-in functions**

```csharp
// reverb(Buffer, Double) -> Buffer                     (room size only)
// reverb(Buffer, Double, Double, Double) -> Buffer     (room, damping, mix)
// lowpass(Buffer, Double) -> Buffer                    (cutoff Hz)
// highpass(Buffer, Double) -> Buffer
// bandpass(Buffer, Double, Double) -> Buffer            (low, high Hz)
// compress(Buffer, Double, Double) -> Buffer            (threshold dB, ratio)
// delay(Buffer, Double, Double, Double) -> Buffer       (time ms, feedback, mix)
// gain(Buffer, Double) -> Buffer                        (gain dB)
```

**Step 3: Write test**

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 1.0 440.0 0.5)
Buffer processed = tone -> lowpass 800.0 -> reverb 0.3 -> gain -3.0dB
(exportWav processed "processed.wav")
```

**Step 4: Run test**

Run: `dotnet run --project flow-interpreter tests/test_effects.flow`
Expected: Creates `processed.wav` with filtered, reverbed, attenuated tone

**Step 5: Commit**

```bash
git add flow-lang/StandardLibrary/Audio/EffectsFunctions.cs flow-lang/StandardLibrary/Audio/DSP/ flow-lang/StandardLibrary/BuiltInFunctions.cs tests/test_effects.flow
git commit -m "feat: add audio effects (reverb, filter, compression, delay, gain)"
```

---

## Task 17: Integration — End-to-End Song Test

Write a comprehensive test that uses all the new features together to compose a short piece.

**Files:**
- Create: `tests/test_full_song.flow`

**Step 1: Write the integration test**

```flow
use "@std"
use "@audio"

section intro {
    tempo 100 {
        timesig 4/4 {
            Sequence chords = | Cmaj7 | Fmaj7 | Cmaj7 | Fmaj7 |
            Sequence bass = | C3q _ _ _ | F3q _ _ _ | C3q _ _ _ | F3q _ _ _ |
        }
    }
}

section verse {
    tempo 100 {
        timesig 4/4 {
            swing 55% {
                Sequence melody = | C4 D4 E4 G4 | A4 G4 E4 D4 | C4 E4 G4 C5 | A4q. G4e E4h |
                Sequence chords = | Am7 | Dm7 | G7 | Cmaj7 |
                Sequence bass = | A2q _ _ _ | D3q _ _ _ | G2q _ _ _ | C3q _ _ _ |
            }
        }
    }
}

section chorus {
    tempo 105 {
        timesig 4/4 {
            Sequence melody = | C5h G4h | A4h E4h | F4 G4 A4 C5 | G4w |
            Sequence chords = | C G | Am F | Dm G | C C |
        }
    }
}

Song mysong = [intro verse chorus verse chorus]
Buffer rendered = mysong -> render

; Apply master effects chain
Buffer mastered = rendered
    -> highpass 80.0
    -> compress -18.0dB 3.0
    -> reverb 0.15
    -> gain -3.0dB

(exportWav mastered "full_song.wav")
mastered -> play
```

**Step 2: Run the test**

Run: `dotnet run --project flow-interpreter tests/test_full_song.flow`
Expected: Creates `full_song.wav` and plays it

**Step 3: Commit**

```bash
git add tests/test_full_song.flow
git commit -m "test: add end-to-end full song integration test"
```

---

---

## Robustness Requirements (Apply to ALL Tasks)

Every task must address these concerns. No shortcuts.

### Validation & Error Handling

**Task 1 — MusicalContext Runtime:**
- Tempo must be positive (> 0). Report error via `_errorReporter` if user passes `tempo 0` or `tempo -5`.
- Swing must be between 0.0 and 1.0. Clamp or error outside range.
- Time signature validation already exists in `TimeSignatureData` (numerator > 0, denominator is power of 2) — reuse it.
- Key must be a recognized key string. Maintain a set of valid keys (all 12 major + 12 minor + modes if desired). Report error for unrecognized keys.
- `GetMusicalContext()` must handle the case where no context exists on the stack at all (use defaults: 4/4, 120 BPM, 0.5 swing, no key).

**Task 2 — MusicalContext Parser:**
- `timesig` block: validate that the `/` separator is present between numerator and denominator. Error on `timesig 4 { }` (missing denominator).
- `tempo` block: accept both `Int` and `Float` literals. Error on non-numeric values.
- `swing` block: parse percentage (`60%`) or float (`0.6`). Add `Percent` token if needed, or parse `Int` followed by `%` identifier.
- `key` block: accept identifier like `Cmajor`, `Aminor`, `Fsharp_minor`. Validate against known keys at parse time or runtime.
- All blocks must require `{` ... `}` — error if body is missing.
- Nested contexts of the same type should work (inner overrides outer).

**Task 4-6 — Note Streams:**
- Notes in a stream must be valid note literals (A-G, octave 0-10, optional accidentals). Invalid notes should report error with location.
- Duration suffixes must be one of `w, h, q, e, s, t`. Anything else is an error.
- A bar with explicit durations that exceed the time signature's beats should report a warning (not error — allow overflow for tied notes across bars).
- A bar with explicit durations that underflow should be padded with rests (or report a warning).
- Empty bars `| |` should be valid (a bar of rest).
- The `~` tie must connect two notes of the same pitch. Error if pitches differ.
- Auto-fit with mixed explicit/implicit durations: explicit notes consume their declared duration, remaining beats are divided among implicit notes. If there aren't enough remaining beats, report error.
- Dotted notes: duration is 1.5x. A dotted note that overflows the bar is an error.
- Cent offsets on notes (`C4+50c`): must be within a reasonable range (e.g., -1200c to +1200c, i.e., +/- 1 octave). Beyond that, transpose instead.

**Task 7 — Lambdas:**
- Lambda bodies must be single expressions (not statement blocks). This is already the case in the parser but should be enforced with a clear error.
- Lambda parameter types must be valid Flow types. Function types like `(Note => Note)` require parsing nested type signatures — ensure the `TypeParser` handles this.
- Recursive lambdas are NOT supported (a lambda can't reference itself). Detect and report error.
- Closures: lambdas should capture variables from their enclosing scope. Verify that captured variables are read-only (Flow is immutable-leaning). If a captured variable is reassigned after lambda creation, the lambda should see the original value (snapshot capture, not reference capture).

**Task 8 — Pattern Transforms:**
- `transpose` with `Semitone`: each note in the sequence shifts. Notes at the boundary of the valid range (E0-E10) should clamp or error.
- `transpose` with `Cent`: store cent offset on the note. Rendering must account for fractional semitones.
- `invert`: mirrors intervals around the first note. If the sequence is empty, return empty (no error). If single note, return unchanged.
- `retrograde`: reverse within each bar, not across bars (preserve bar structure). Empty bars stay empty.
- `augment`/`diminish`: doubling/halving note values. A whole note augmented becomes... what? Either error, or introduce a "double whole" (breve). For now, clamp at whole note with a warning.
- `repeat` with transform: the transform function must accept and return a `Sequence`. Type-check at call time.
- `concat`: sequences must have compatible time signatures (or the result becomes multi-meter). Store per-bar time signatures.

**Task 9 — Chords:**
- Chord parsing must be unambiguous with note parsing. `C4` is a note (C, octave 4). `Cmaj` is a chord. `C` alone could be either — default to note `C4`. `Cm` is a chord (C minor). This disambiguation must be handled carefully in the lexer.
- Invalid chord qualities (e.g., `Cxyz`) should report a clear error: "Unknown chord quality 'xyz'".
- Slash chord bass notes must be valid note letters (A-G). `C/X` is an error.
- Octave override `Cmaj:3` — the `:` is already used for parameter syntax. Consider using a different delimiter or parsing contextually within note streams only.

**Task 10 — Roman Numerals:**
- Roman numerals are only valid inside a `key` context. If no key is active, report: "Roman numeral 'IV' requires an active key context. Use `key Cmajor { ... }` to set a key."
- Case matters: `I` = major, `i` = minor. `iv` = minor 4th, `IV` = major 4th.
- Extensions: `ii7` = minor 7th chord on 2nd degree. `V7` = dominant 7th on 5th degree.
- Augmented 6th chords and other chromatic alterations are out of scope for now. Document this.

**Task 11 — Song Structure:**
- Section names must be unique within a scope. Duplicate names should error.
- Section inheritance (`: parent`): the parent must be defined before the child. Error if parent doesn't exist.
- Song arrangement `[intro verse*2 chorus]`: section names must reference defined sections. Error for undefined references.
- Repeat count must be positive integer. `verse*0` should error or be treated as omission.
- Recursive section inheritance (A : B, B : A) must be detected and reported as an error.
- Empty sections are valid but produce silence.

**Task 12 — Generative Features:**
- `(? C4 E4 G4)` with no elements should error: "Random choice requires at least one option."
- Weights in `(? C4:50 E4:30 G4:20)` should sum to 100. If they don't, either normalize or error. Recommend normalize with a warning.
- Negative weights should error.
- `??` seeded random must be deterministic: same seed, same sequence, every time. Test this explicitly.
- Euclidean rhythms: `hits` must be <= `steps`. `hits` and `steps` must be positive. Error otherwise.

**Task 13-14 — Playback:**
- If no audio backend is available, `play` should report a clear error: "No audio output available. Install PipeWire or PulseAudio."
- `play` on an empty buffer should be a no-op (not an error).
- `play` should handle Ctrl+C gracefully — stop playback and return to REPL, not crash.
- Audio buffer format conversion: the internal buffer uses `double` samples but audio APIs typically want `float` or `int16`. Convert with proper clamping (no overflow).
- Sample rate mismatch: if the buffer's sample rate differs from the device's, either resample or report a warning.
- Thread safety: `play` blocks the main thread. `loop` should be interruptible. Consider running playback on a background thread with cancellation token.

**Task 16 — Effects:**
- All effects must return NEW buffers, never modify the input (Flow values are conceptually immutable).
- Reverb: room size must be in [0, 1]. Values outside this range should clamp with warning.
- Filter cutoff: must be positive and less than Nyquist frequency (sampleRate / 2). Error if above Nyquist.
- Compressor ratio must be >= 1.0 (1.0 = no compression). Threshold must be negative dB or zero. Error on positive threshold (that's expansion, not compression).
- Delay time must be positive. Feedback must be in [0, 1) — feedback of 1.0 creates infinite feedback, which should warn. Feedback > 1.0 should error.
- Gain: no restrictions on range, but warn if result would clip (peak > 0 dB).
- Effect chain order matters. Document that effects apply left-to-right in a chain.

### Testing Standards

Every task must include:
1. **Happy path test** — the feature works as designed
2. **Edge case tests** — empty inputs, boundary values, single elements
3. **Error case tests** — invalid inputs produce clear error messages (not crashes)
4. **Regression test** — existing features still work after the change (run `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done`)

### Code Quality

- All new public methods must have XML doc comments (`///`)
- New files must use file-scoped namespaces (matching existing convention)
- All AST nodes must be immutable `record` types
- No `null!` suppressions in new code — use nullable reference types properly
- New types must implement `ToString()` for debugging
- Error messages must include location information (line:column) via `SourceLocation`

---

## Summary of Task Dependencies

```
Task 1 (MusicalContext runtime)
  └→ Task 2 (MusicalContext lexer/parser)
      └→ Task 3 (MusicalContext interpreter)
          └→ Task 4 (Note stream lexer)
              └→ Task 5 (Note stream AST/parser)
                  └→ Task 6 (Note stream evaluator)
                      └→ Task 8 (Pattern transforms)
                      └→ Task 12 (Generative features)

Task 7 (Lambda functions) — independent, can run in parallel with Tasks 4-6

Task 9 (Chord parsing) — depends on Task 6
  └→ Task 10 (Roman numerals) — depends on Task 9

Task 11 (Song structure) — depends on Task 6

Task 13 (Audio backend) — independent infrastructure
  └→ Task 14 (Playback functions) — depends on Task 13
      └→ Task 15 (REPL & watch) — depends on Task 14

Task 16 (Effects) — independent, only needs existing AudioBuffer

Task 17 (Integration test) — depends on all above
```

## Parallel Execution Opportunities

These groups can run concurrently:
- **Group A:** Tasks 1-6, 8, 12 (musical context + note streams + transforms + generative)
- **Group B:** Task 7 (lambdas)
- **Group C:** Tasks 13-15 (playback infrastructure)
- **Group D:** Task 16 (effects)
- **Group E:** Tasks 9-10 (chords/harmony, after Group A's Task 6)
- **Group F:** Task 11 (song structure, after Group A's Task 6)
