# Phase 14: Composer DX Part 1 — Pattern Map

**Mapped:** 2026-04-19
**Files analyzed:** 14 (5 modified + 9 created)
**Analogs found:** 13 / 14 (LexerTests has no prior analog — new pattern)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Collections.cs` (modify) | stdlib function | transform (array/sequence) | self — `Take`/`Drop` at lines 117-147 | exact |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modify, slice) | registration | request-response | self — `take`/`drop` registrations at lines 369-373 | exact |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modify, enharmonic) | context-dependent registration | request-response | self — `SongFunctions.Register(registry, context)` call at line 668 | exact |
| `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (modify) | parser/formatter | transform (string ↔ triple) | self — current `Parse` (lines 21-73) and `Format` (lines 142-155) | exact |
| `flow-lang/Lexing/SimpleLexer.cs` (modify, dispatch order) | lexer dispatch | token stream | self — current dispatch block at lines 617-663 | exact (swap order) |
| `flow-lang/Lexing/SimpleLexer.cs` (modify, alteration pickup) | lexer alteration pickup | token stream | self — bounded peek at lines 545-565 | exact (generalize) |
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (modify, enharmonic) | context-dependent stdlib | request-response (reads context) | `Composition/SongFunctions.cs` Register(registry, context) method (lines 10-23); existing `resolveNumeral` registration at `HarmonyFunctions.cs:99-110` for key-context pattern | role-match |
| `flow-lang.Tests/Unit/Phase14/SliceTests.cs` (create) | unit test (direct C# API) | request-response | `flow-lang.Tests/Unit/CollectionsTests.cs` (lines 1-41) | exact |
| `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` (create) | unit test (direct C# API) | request-response | `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` (lines 1-26) — static-method-direct pattern | exact |
| `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` (create) | unit test (needs ExecutionContext) | request-response | `flow-lang.Tests/Unit/InterpreterTests.cs` (ExecuteMusicalContextTests, lines 1-53) — uses `FlowEngineRunner` because the built-in needs `ExecutionContext` | role-match |
| `flow-lang.Tests/Unit/Phase14/LexerTests.cs` (create) | unit test (chord-vs-note tokenization) | request-response | none — **NEW PATTERN**. Nearest shape: `InterpreterTests.cs` RunSource + stdout assertions; must assert token stream produced by SimpleLexer | no direct analog |
| `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` (create) | integration test (file I/O + MIDI read) | file-I/O + batch | `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` (lines 1-38) for structure; `MidiFile.Read`/`GetNotes` usage is new — see RESEARCH.md §"Code Examples" | role-match |
| `tests/test_slice.flow` (create) | .flow regression | stdout-sentinel | `tests/test_transpose_int.flow` (lines 1-13) | exact |
| `tests/test_flat_literals.flow` (create) | .flow regression | stdout-sentinel | `tests/test_transpose_int.flow` | exact |
| `tests/test_enharmonic.flow` (create) | .flow regression | stdout-sentinel | `tests/test_dynamics.flow` (lines 1-40) for `key` context block pattern | role-match |
| `tests/test_dynamics_midi_velocity.flow` (create) | .flow regression (writes MIDI) | file-I/O | `tests/test_midi_export.flow` (lines 1-48) | exact |

---

## Pattern Assignments

### `flow-lang/StandardLibrary/Collections.cs` (stdlib function, transform)

**Analog:** self — `Take`/`Drop` at lines 117-147 of the same file.

**Core pattern** (`Collections.cs:117-147`):

```csharp
public static Value Take(IReadOnlyList<Value> args)
{
    var arr = args[0];
    var n = args[1];

    if (arr.Type is not ArrayType arrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");
    if (n.Type is not IntType)
        throw new InvalidOperationException($"Expected Int, got {n.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    var count = n.As<int>();
    if (count < 0) count = 0;
    return Value.Array(elements.Take(count).ToArray(), arrayType.ElementType);
}

public static Value Drop(IReadOnlyList<Value> args)
{
    var arr = args[0];
    var n = args[1];

    if (arr.Type is not ArrayType arrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");
    if (n.Type is not IntType)
        throw new InvalidOperationException($"Expected Int, got {n.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    var count = n.As<int>();
    if (count < 0) count = 0;
    return Value.Array(elements.Skip(count).ToArray(), arrayType.ElementType);
}
```

**Copy this pattern for:**
- `SliceArray(IReadOnlyList<Value> args)` — same type guards (`ArrayType` + `IntType`), same `Math.Max(0, start)` clamp style, LINQ `Skip(s).Take(e-s).ToArray()`. Preserve `arrayType.ElementType` through `Value.Array(...)` (critical for typed-array round-trip).
- `SliceSequence(IReadOnlyList<Value> args)` — extract `SequenceData` via `args[0].As<SequenceData>()`, iterate `seq.Bars[s..e]`, build a new `SequenceData` via `AddBar` (which enforces musical-bar invariant per CONTEXT `SequenceType.cs:32-41` note). Return `Value.Sequence(result)`.

**Gotcha:** Clamp BOTH sides (D-01 silent clamping). `Math.Max(0, start)` AND `Math.Min(count, end)`. `start >= end` (post-clamp) returns empty — this is a new code path not present in Take/Drop.

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` (registration — slice)

**Analog:** self — `take`/`drop` registrations at lines 369-373.

**Imports/context pattern** (`BuiltInFunctions.cs:369-373`):

```csharp
var takeSignature = new FunctionSignature("take", [new ArrayType(VoidType.Instance), IntType.Instance]);
registry.Register("take", takeSignature, Collections.Take);

var dropSignature = new FunctionSignature("drop", [new ArrayType(VoidType.Instance), IntType.Instance]);
registry.Register("drop", dropSignature, Collections.Drop);
```

**Copy this pattern for:** slice (Array[T] overload) using `new ArrayType(VoidType.Instance)` as the wildcard element type. The second overload uses `SequenceType.Instance`:

```csharp
var sliceArraySignature = new FunctionSignature("slice",
    [new ArrayType(VoidType.Instance), IntType.Instance, IntType.Instance]);
registry.Register("slice", sliceArraySignature, Collections.SliceArray);

var sliceSeqSignature = new FunctionSignature("slice",
    [SequenceType.Instance, IntType.Instance, IntType.Instance]);
registry.Register("slice", sliceSeqSignature, Collections.SliceSequence);
```

**Gotcha:** Overload resolver disambiguates by arg 0 type (Array vs Sequence) — verified in RESEARCH `InternalFunctionRegistry.cs:100-104`. Register order does not matter; `OverloadResolver` scores all candidates.

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` (registration — enharmonic, context-dependent)

**Analog:** `SongFunctions.Register(registry, context)` call inside `RegisterContextDependentFunctions` at line 668.

**Pattern** (`BuiltInFunctions.cs:665-672`):

```csharp
public static void RegisterContextDependentFunctions(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    Audio.SongRenderer.RegisterContextDependent(registry, context);
    Composition.SongFunctions.Register(registry, context);
    // ... etc.
    var randSignature = new FunctionSignature("?", []);
    registry.Register("?", randSignature, args => StdLib.Rand(args, context));
```

**Copy this pattern for:** Add one new line near line 668 in `RegisterContextDependentFunctions`:

```csharp
Harmony.HarmonyFunctions.RegisterContextDependent(registry, context);
```

**Gotcha:** This is the ONLY edit needed in BuiltInFunctions.cs for enharmonic. The implementation and registration both live in HarmonyFunctions.cs (D-06 — no new file).

---

### `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (parser/formatter extension)

**Analog:** self — existing `Parse` at lines 21-73, existing `Format` at lines 142-155. The file is its own reference.

**Current Parse alteration-switch** (`NoteType.cs:54-63`) — **to be replaced by sum-based scan**:

```csharp
// Parse alteration if any
if (remaining.Length > 0)
{
    alteration = remaining switch
    {
        "++" => 2,
        "+" => 1,
        "-" => -1,
        "--" => -2,
        _ => throw new ArgumentException($"Invalid alteration: {remaining}")
    };
}
```

**Current Format** (`NoteType.cs:142-155`) — **to be replaced by run-based emission**:

```csharp
public static string Format(char note, int octave, int alteration)
{
    string alterationStr = alteration switch
    {
        2 => "++",
        1 => "+",
        0 => "",
        -1 => "-",
        -2 => "--",
        _ => throw new ArgumentException($"Invalid alteration: {alteration}")
    };

    return $"{note}{octave}{alterationStr}";
}
```

**Current range check** (`NoteType.cs:67-70, 78-88`) — **to be shifted to post-alteration MIDI** per D-09:

```csharp
// Validate range: E0 to E10
if (!IsValidNoteRange(note, octave))
{
    throw new ArgumentException($"Note {note}{octave} is out of valid range (E0 to E10)");
}
// ...
private static bool IsValidNoteRange(char note, int octave)
{
    int noteValue = GetNoteValue(note, octave);
    int minNote = GetNoteValue('E', 0);  // E0
    int maxNote = GetNoteValue('E', 10); // E10
    return noteValue >= minNote && noteValue <= maxNote;
}
```

**Key reusable helpers already present** (`NoteType.cs:93-137`):

```csharp
private static int GetNoteValue(char note, int octave) { /* letter+octave → MIDI-like */ }
public static int ToMidiNote(char note, int octave, int alteration) { /* adds alteration */ }
public static (char note, int octave, int alteration) FromMidiNote(int midiNote) { /* inverse */ }
```

**Implementation target** (from RESEARCH §"`NoteType.Parse` — sum-based alteration scan", lines 189-257):
- Three-phase scan: pre-octave alt chars → octave digits → post-octave alt chars
- Accept `+`/`#` → sharpCount++, `-`/`b` → flatCount++, digit → octave phase, anything else → `ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}")`
- `alteration = sharpCount - flatCount` (any int)
- Post-alt range check: `midi = GetNoteValue(note, octave) + alteration`, compare to `GetNoteValue('E', 0)` and `GetNoteValue('E', 10)`. Error text MUST keep the existing format `"Note {noteStr} is out of valid range (E0 to E10)"` (D-09 explicit).

**Format replacement** (run-based, preserves round-trip):

```csharp
public static string Format(char note, int octave, int alteration)
{
    string altStr = alteration switch
    {
        0 => "",
        > 0 => new string('+', alteration),
        < 0 => new string('-', -alteration),
    };
    return $"{note}{octave}{altStr}";
}
```

**Gotchas:**
- **Round-trip MANDATORY** (D-08): `Parse(Format(x)) == x` for any int alteration. Run-based emission satisfies this; numeric-suffix like `B+3` would NOT (parsed as letter B, octave 3, alt +1).
- **Three Format call sites** (`NoteType.cs:236` ToString; `Value.cs:168` round-trip; `TransformFunctions.cs:121` warning text) — all are round-trip-safe under run emission.
- `IsValidNoteRange(note, octave)` may become unused under D-09. Remove or leave as dead code — RESEARCH recommends removing.

---

### `flow-lang/Lexing/SimpleLexer.cs` (dispatch-order fix — chord before note)

**Analog:** self — current dispatch block at lines 613-664 of the same file.

**Current dispatch order** (`SimpleLexer.cs:613-664`):

```csharp
// If it's an identifier, check if it's a special literal
if (type == TokenType.Identifier)
{
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
                _position--;
                _column--;
                return new Token(TokenType.NoteLiteral, notePartText, start, notePartValue);
            }
        }
    }

    // Try to parse as Semitone (+/-Nst)
    if (TryParseSemitone(text, out var semitoneValue)) { ... }
    if (TryParseTime(text, out var timeValue, out var timeUnit)) { ... }
    if (TryParseDecibel(text, out var decibelValue)) { ... }

    // Try to parse as Chord (Cmaj7, Dm, Gsus4, etc.)
    if (ChordParser.IsChordSymbol(text))
    {
        return new Token(TokenType.ChordLiteral, text, start, text);
    }
}
```

**Required modification** (RESEARCH §Regression Risk Analysis — chord-vs-note ordering bug): move `ChordParser.IsChordSymbol(text)` check to run **BEFORE** `TryParseNote`. Under the extended Parse surface, `Bb7` now successfully parses as Note `(B, 7, -1)` — breaking `tests/test_chords.flow`. Chord-first dispatch resolves this.

**Copy this pattern:** Hoist the chord branch above the note branch:

```csharp
if (type == TokenType.Identifier)
{
    // (NEW — moved from bottom) Try chord FIRST so `Bb7`, `Dm`, `F#dim` keep
    // tokenizing as ChordLiteral under the extended NoteType.Parse surface.
    if (ChordParser.IsChordSymbol(text))
    {
        return new Token(TokenType.ChordLiteral, text, start, text);
    }

    // Now try note (A-G followed by optional octave and alteration)
    if (TryParseNote(text, out var noteValue)) { ... }
    // ... rest unchanged (duration suffix, semitone, time, decibel)
}
```

**Gotcha:** `ChordParser.IsChordSymbol` has strict shape `[A-G][#b]?(maj|m|dim|aug|sus[24]|add\d|M\d|\d)...` — it does NOT match bare `Db4`, `Bb`, `C4`, `F#` (no quality suffix), so note literals without chord modifiers still fall through to `TryParseNote`. The Lexer regression test MUST assert:
1. `Dm` → ChordLiteral (unchanged)
2. `Bb7` → ChordLiteral (was error pre-extension; would become NoteLiteral under naive order)
3. `Db4` → NoteLiteral(D, 4, -1) (new behavior)
4. `Bb` → NoteLiteral(B, 4, -1) (new behavior — bare flat in note stream)

---

### `flow-lang/Lexing/SimpleLexer.cs` (alteration pickup generalization)

**Analog:** self — current bounded peek at lines 543-565.

**Current pattern** (`SimpleLexer.cs:543-565`):

```csharp
// Special case: Check if this looks like a note (A-G + digits) followed by alteration (+/-)
// We need to peek ahead for alterations because +/- are token boundaries
if (text.Length >= 2)
{
    char firstChar = char.ToUpper(text[0]);
    if (firstChar >= 'A' && firstChar <= 'G' && char.IsDigit(text[1]))
    {
        // This could be a note like A3, check for alteration
        if (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
        {
            char alterationChar = Peek();
            sb.Append(Advance()); // Consume first +/-

            // Check for double alteration (++ or --)
            if (!IsAtEnd() && Peek() == alterationChar)
            {
                sb.Append(Advance()); // Consume second +/-
            }

            text = sb.ToString();
        }
    }
}
```

**Replacement** (RESEARCH §Lexer extension, lines 310-329):

```csharp
if (text.Length >= 2)
{
    char firstChar = char.ToUpper(text[0]);
    if (firstChar >= 'A' && firstChar <= 'G')
    {
        // Pick up any run of +/- chars (alterations) following the identifier.
        // 'b' and '#' are already absorbed as identifier characters.
        while (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
        {
            sb.Append(Advance());
        }
        text = sb.ToString();
    }
}
```

**Gotchas:**
- Drop the `char.IsDigit(text[1])` gate — pickup must fire even for bare `Bb` (length 2, no octave digit).
- Drop the "double alteration only" bound — the loop is unbounded.
- **Duration-suffix path** (`SimpleLexer.cs:625-639`) still works for extended inputs like `Cb4h`: identifier becomes `Cb4h`, last char `h` stripped, `TryParseNote("Cb4")` succeeds under extended Parse. Verified mentally per RESEARCH.

---

### `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (enharmonic built-in + context registration)

**Analog A (file structure / context-dependent Register pattern):** `flow-lang/StandardLibrary/Composition/SongFunctions.cs:10-23`.

```csharp
public static class SongFunctions
{
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var createSongSignature = new FunctionSignature("createSong", [StringType.Instance]);
        registry.Register("createSong", createSongSignature, args => CreateSong(args, context));

        var addBarSignature = new FunctionSignature("addBarToSong", [SongType.Instance, StringType.Instance]);
        registry.Register("addBarToSong", addBarSignature, args => AddBarToSong(args, context));
        // ...
    }

    private static Value AddSequenceToSong(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var song = args[0].As<SongData>();
        var seq = args[1].As<SequenceData>();
        // ...
        var section = new SectionData(name, sequences, context.GetMusicalContext());
        // ...
    }
}
```

**Analog B (existing harmony registration that uses keyName):** `HarmonyFunctions.cs:99-110` `resolveNumeral`:

```csharp
// resolveNumeral(String, String) -> Chord
var resolveNumeralSignature = new FunctionSignature("resolveNumeral",
    [StringType.Instance, StringType.Instance]);
registry.Register("resolveNumeral", resolveNumeralSignature, args =>
{
    var numeral = args[0].As<string>();
    var keyName = args[1].As<string>();
    var chordData = ScaleDatabase.ResolveRomanNumeral(numeral, keyName);
    if (chordData == null)
        return Value.Void();
    return Value.Chord(chordData);
});
```

**Copy pattern:** Add a NEW static method to `HarmonyFunctions` named `RegisterContextDependent`, leaving the existing `Register(registry)` untouched (D-06: enharmonic is additive — no modifications to existing harmony registrations).

```csharp
// In HarmonyFunctions.cs — NEW method alongside existing Register
public static void RegisterContextDependent(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    var enharmonicSig = new FunctionSignature("enharmonic", [NoteType.Instance]);
    registry.Register("enharmonic", enharmonicSig, args => Enharmonic(args, context));
}

private static Value Enharmonic(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
{
    string noteStr = args[0].As<string>();  // Note values stored as string — see Value.cs:32
    var (letter, octave, alteration) = NoteType.Parse(noteStr);

    var musicalCtx = context.GetMusicalContext();
    string? key = musicalCtx.Key;

    // In-key branch (D-04) — see RESEARCH §"In-key algorithm"
    if (key != null)
    {
        // Lookup via ScaleDatabase; if input MIDI matches any scale-tone MIDI, return that spelling
        // Else fall through to no-key rule
    }

    // No-key / Cmaj / Amin fallback (D-05): flip sharp↔flat; naturals unchanged
    if (alteration == 0)
        return Value.Note(NoteType.Format(letter, octave, 0));
    // ... compute enharmonic via MIDI
}
```

**Note values are strings** (per `Value.cs:32` — `public static Value Note(string value) => new(value, NoteType.Instance);`). When unwrapping a `Value` of `NoteType` via `args[0].As<string>()`, you get the original noteStr like `"Db4"`. Run `NoteType.Parse` to get the triple.

**Gotchas:**
- **Pitfall 3 in RESEARCH:** `ScaleDatabase.GetScaleNotes("Dbmajor")` may return sharp-spelled notes (`"Cs", "Ds"`) due to `ChromaticNotes` asymmetry. Do NOT echo its output blindly — do a MIDI-value comparison instead, and pick the letter whose key-signature affinity (flat key → flat spelling) matches.
- **Null-key fallback:** `musicalCtx.Key` can be `null`. Always guard with `if (key != null)` before using.
- **Naturals return unchanged** (D-05 — no `E↔Fb`, `F↔E#`, `B↔Cb`, `C↔B#` edge respelling).
- **Double-sharps are non-involutive** (`F##4` → `G4`, then `G4` → `G4`). Document in Fact.

---

### `flow-lang.Tests/Unit/Phase14/SliceTests.cs` (create — direct C# API Facts)

**Analog:** `flow-lang.Tests/Unit/CollectionsTests.cs` (lines 1-41).

**Imports + structure pattern** (`CollectionsTests.cs:1-41`):

```csharp
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit;

public class CollectionsTests
{
    [Fact]
    public void Init_EmptyArray_ThrowsInvalidOperationException()
    {
        var emptyArray = Value.Array(new List<Value>(), VoidType.Instance);
        var ex = Assert.Throws<InvalidOperationException>(
            () => Collections.Init(new[] { emptyArray }));
        Assert.Equal("Cannot get init of empty array", ex.Message);
    }

    [Fact]
    public void Init_MultipleElements_ReturnsAllButLast()
    {
        var arr = Value.Array(
            new List<Value> { Value.Int(1), Value.Int(2), Value.Int(3) },
            IntType.Instance);
        var result = Collections.Init(new[] { arr });
        var elements = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elements.Count);
        Assert.Equal(1, elements[0].As<int>());
        Assert.Equal(2, elements[1].As<int>());
    }
}
```

**Copy pattern for SliceTests:**
- Place in `flow-lang.Tests/Unit/Phase14/SliceTests.cs` (new directory — Phase 14 convention per D-15, directory convention from Phase 13 D-09).
- Namespace: `FlowLang.Tests.Unit.Phase14`.
- Construct `Value.Array(...)` with `IntType.Instance` element type.
- Pass args array directly to `Collections.SliceArray(new[] { arr, Value.Int(start), Value.Int(end) })`.
- Assert on `.Count` (observable-value pin per Phase 13 D-11; no byte hashes).
- For sequence overload, construct via `Value.Sequence(new SequenceData { /* add bars */ })` and assert `.Bars.Count`.

**Facts to write** (from RESEARCH §"Wave 0 Gaps" and §"Phase Requirements → Test Map"):
- `Array_NormalRange` — `[1,2,3,4,5]` slice(1,4) → `[2,3,4]`
- `Array_NegativeStartClamps` — slice(-5, 2) → `[1,2]`
- `Array_EndExceedsCountClamps` — slice(3, 100) → `[4,5]`
- `Array_InvertedRangeEmpty` — slice(3, 2) → `[]`
- `Sequence_ReturnsCorrectBarCount` — multi-bar sequence + count check
- `Array_PreservesElementType` — typed-array round-trip pin

**Gotcha:** Direct-C# Facts do NOT need `use "@std"` prelude (that's only for `FlowEngineRunner.RunSource` paths). Here we're calling `Collections.SliceArray` as a plain C# static method.

---

### `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` (create — direct static-method Facts)

**Analog:** `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` (lines 1-26).

**Pattern** (`FormantDataTests.cs:1-26`):

```csharp
using FlowLang.StandardLibrary.Audio.Vocalization;
using Xunit;

namespace FlowLang.Tests.Unit.Phase10;

/// <summary>
/// VOC-01 unknown-vowel regression test: GetFormants rejects non-canonical
/// vowel phonemes with a helpful ArgumentException. ...
/// </summary>
public class FormantDataTests
{
    [Fact]
    public void GetFormants_UnknownVowel_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => FormantData.GetFormants("xyz"));
        Assert.Equal("Unknown vowel phoneme: 'xyz'. Valid: ah, ee, eh, oh, oo", ex.Message);
    }
}
```

**Copy pattern for NoteTypeTests:**
- `using FlowLang.TypeSystem.SpecialTypes;` for `NoteType.Parse`/`Format`.
- Namespace: `FlowLang.Tests.Unit.Phase14`.
- No `FlowEngineRunner` needed — Parse/Format are pure static functions.
- Assert triple equality: `var (n, o, a) = NoteType.Parse("Db4"); Assert.Equal('D', n); Assert.Equal(4, o); Assert.Equal(-1, a);`.
- Assert Format exact string: `Assert.Equal("B4----", NoteType.Format('B', 4, -4));`.
- Round-trip Facts: iterate -5..+5, assert `Parse(Format(...)) == original`.
- Error text pin: `Assert.Equal("Note Cb0 is out of valid range (E0 to E10)", ex.Message)` — mirrors existing `NoteType.cs:69` format.

**Gotcha:** Format canonical style is Claude's Discretion per D-08 (see CONTEXT.md §Claude's Discretion). The chosen style MUST satisfy round-trip — run-based `+N`/`-N` is RECOMMENDED over numeric suffix (which would collide with octave digits).

---

### `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` (create — needs ExecutionContext)

**Analog:** `flow-lang.Tests/Unit/InterpreterTests.cs` `ExecuteMusicalContextTests` (lines 1-53).

**Pattern — needs `use "@std"` prelude** (`InterpreterTests.cs:20-36`):

```csharp
[Fact]
public void BadTempo_BodyStillRuns_ErrorReported()
{
    using var runner = new FlowEngineRunner();
    // `use "@std"` is required for `print` to resolve — the stdlib module
    // registers the `internal proc print` declaration that binds to the
    // C# StdLib.Print implementation. Without it, `print` is unresolved
    // even though the C# implementation is registered at engine init.
    var (_, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
tempo -5 {
    (print ""body-ran"")
}
(print ""after-block"")
");
    Assert.Contains("body-ran", stdout);
    Assert.True(errorCount >= 1);
    Assert.Contains("Tempo must be positive", stderr);
}
```

**Copy pattern for EnharmonicTests:**
- `[Collection("FlowScripts")]` attribute on the class to serialize Console.SetOut (RESEARCH Pitfall 4, InterpreterTests.cs:14).
- Because `enharmonic` requires ExecutionContext, Facts must drive it through `FlowEngineRunner.RunSource` with a `.flow` snippet rather than calling the C# method directly.
- `use "@std"` prelude MUST be in every `RunSource` snippet (Phase 12 Plan 04 discovery — cited in CONTEXT canonical_refs `flow-lang.Tests/Unit/InterpreterTests.cs`).
- To access the result, the `.flow` snippet uses `(print (str (enharmonic Db4)))` or similar, and the Fact asserts on stdout substring.

**Facts to write** (from RESEARCH §"Test surface" for enharmonic):
- `NoKey_FlatToSharp` — `enharmonic Db4` (no key) → `C#4`
- `NoKey_SharpToFlat` — `enharmonic F#3` (no key) → `Gb3`
- `NoKey_NaturalUnchanged` — `enharmonic C4` → `C4` (D-05)
- `NoKey_EdgeNaturalUnchanged` — `enharmonic E4` → `E4` (no `E→Fb` per D-05)
- `InKey_Dbmajor` — inside `key Dbmajor { }` block, `enharmonic C#4` → `Db4`
- `InKey_ChromaticNotInScale_FallsBack` — chromatic non-diatonic → no-key rule
- `DoubleSharp_NonInvolutive` — `enharmonic F##4` → `G4` (documented)

**Gotcha:** The Fact assertion is a string-contains check against stdout, not direct struct equality. Example:

```csharp
var (_, stdout, _, _) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic Db4)))
");
Assert.Contains("C#4", stdout);  // or the exact Format output
```

---

### `flow-lang.Tests/Unit/Phase14/LexerTests.cs` (create — NEW PATTERN)

**Analog:** NONE — this is a new pattern. Flow has no prior unit tests that assert on the Lexer token stream directly.

**Nearest available shape:** `flow-lang.Tests/Unit/InterpreterTests.cs` pattern (RunSource + stdout assertion), but assertion surface differs — we care about token type, not runtime output.

**Recommended approach:** Direct C# call to `SimpleLexer.Tokenize(string)` (or equivalent entry point), then inspect the returned `Token[]` / `List<Token>`.

**Pattern template** (extrapolate from how FlowEngine invokes the lexer — `flow-lang/Core/FlowEngine.cs` for reference):

```csharp
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// Phase 14 DX-06 regression: under the extended NoteType.Parse surface,
/// `Bb7` newly parses as Note(B, 7, -1). To avoid breaking tests/test_chords.flow,
/// SimpleLexer must run ChordParser.IsChordSymbol BEFORE TryParseNote. These Facts
/// pin the required dispatch order.
/// </summary>
public class LexerTests
{
    [Fact]
    public void Bb7_TokenizesAsChordLiteral()
    {
        var lexer = new SimpleLexer("Bb7");
        var tokens = lexer.Tokenize();
        // First token should be ChordLiteral, not NoteLiteral
        Assert.Equal(TokenType.ChordLiteral, tokens[0].Type);
    }

    [Fact]
    public void Dm_TokenizesAsChordLiteral()
    {
        var lexer = new SimpleLexer("Dm");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.ChordLiteral, tokens[0].Type);
    }

    [Fact]
    public void Db4_TokenizesAsNoteLiteral()
    {
        var lexer = new SimpleLexer("Db4");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.NoteLiteral, tokens[0].Type);
    }

    [Fact]
    public void Bb_TokenizesAsNoteLiteral()
    {
        var lexer = new SimpleLexer("Bb");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.NoteLiteral, tokens[0].Type);
    }
}
```

**Gotcha:**
- Planner should **verify the exact SimpleLexer constructor and entry-point method signature** at plan time by reading `flow-lang/Lexing/SimpleLexer.cs` lines 1-50 (class declaration). The snippet above assumes `new SimpleLexer(source).Tokenize()` — adjust if the actual API differs.
- No `[Collection("FlowScripts")]` needed — no console output capture required.
- No `use "@std"` prelude needed — direct lexer invocation bypasses engine.

---

### `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` (create — integration + file I/O)

**Analog A (class structure + FlowEngineRunner usage):** `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` (lines 1-38).

```csharp
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase06;

/// <summary>
/// FIX-02 x AUDIO-06 regression gate: [doc]
/// </summary>
[Collection("FlowScripts")]
public class SectionGainBareExpressionTests
{
    [Fact]
    public void GainNestedInSection_RendersNonZeroFrames()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
section s { gain 0.5 { | C4 D4 E4 F4 | } }
Song sg = [s]
Buffer b = (renderSong sg ""sine"")
Int frames = (getFrames b)
(print $""frames: {(str frames)}"")
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.DoesNotContain("frames: 0\n", stdout);
        Assert.Contains("frames:", stdout);
    }
}
```

**Analog B (DryWetMidi read API + file-path resolution):** RESEARCH §"Code Examples" lines 797-847 shows the exact verified shape — no existing Fact in the repo reads MIDI files. **Inline the helper inside the Fact** per D-15; promote to `Shared/MidiReadHelpers.cs` only if a second Fact needs it (Claude's Discretion — defer to Pass 2).

**Copy pattern for DynamicsMidiVelocityTests:**

```csharp
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests;
using Xunit;

namespace FlowLang.Tests.Integration.Phase14;

[Collection("FlowScripts")]
public class DynamicsMidiVelocityTests
{
    [Fact]
    public void Crescendo_EmitsExpectedVelocityGradient()
    {
        using var runner = new FlowEngineRunner();
        var originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(FlowScriptData.FindTestsRoot())!;
            var testScript = Path.Combine("tests", "test_dynamics_midi_velocity.flow");
            var outputMidi = Path.Combine("tests", "output", "dynamics_velocity.mid");
            if (File.Exists(outputMidi)) File.Delete(outputMidi);

            var (success, stdout, stderr, errorCount) = runner.RunFile(testScript);

            Assert.True(success, $"Script failed: {stderr}");
            Assert.True(File.Exists(outputMidi), $"MIDI file not written: {outputMidi}");

            var midiFile = MidiFile.Read(outputMidi);
            var velocities = midiFile.GetNotes()
                .Select(n => (byte)n.Velocity)
                .ToArray();

            // Expected gradient — planner computes from actual script values at plan time
            Assert.Equal(new byte[] { 31, 47, 63, 79, 95 }, velocities);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
```

**Gotchas:**
- **Working directory:** `runner.RunFile(testScript)` resolves `writeMidi "/path/..."` against `Environment.CurrentDirectory`. The Fact must cd to the repo root (parent of `tests/`) so the relative `writeMidi` path inside the `.flow` script writes to a predictable location. Use `FlowScriptData.FindTestsRoot()` to locate `tests/`, then take its parent.
- **DryWetMidi imports:** `using Melanchall.DryWetMidi.Core;` for `MidiFile.Read`; `using Melanchall.DryWetMidi.Interaction;` for the `GetNotes()` extension. Both available from the existing 8.0.3 package — no new NuGet (RESEARCH §Standard Stack).
- **Two-pass strict authorship** (D-13): Pass 1 authors the Fact from REQUIREMENTS alone; Pass 2 runs against real code. If Pass 2 RED, minimal gap-fix lands in same plan with Divergence entry in VERIFICATION.md.
- **Velocity byte math:** `(byte)Math.Clamp((int)(v * 127), 1, 127)` — truncates toward zero, floors at 1 (MIDI velocity 0 = note off). RESEARCH §DX-08 Velocity Chain Audit Stage 4 shows the exact mapping.
- **FlowScriptData auto-registers** the `.flow` script as a Theory row via `tests/*.flow` glob (`FlowScriptData.cs:8`). No explicit wiring needed; the script must exit cleanly with no stderr.

---

### `tests/test_slice.flow` (create — stdlib-function exercise)

**Analog:** `tests/test_transpose_int.flow` (lines 1-13).

**Pattern** (`test_transpose_int.flow:1-13`):

```flow
use "@std"

Sequence s = | C4 D4 E4 |

Note: transpose with Int (should work like Semitone)
Sequence t = s -> transpose(2)
(print "transpose with int: ok")

Note: transpose with Semitone literal (existing behavior)
Sequence t2 = s -> transpose(+2st)
(print "transpose with semitone: ok")

(print "test_transpose_int: PASSED")
```

**Copy pattern:**
- `use "@std"` prelude first line.
- `Note:` prefix for inline comments (Flow doesn't require `//`).
- Exercise each overload with a known input, `(print ...)` a success sentinel per variant.
- Terminal `(print "test_XXX: PASSED")` sentinel (RequiredSentinels pattern in FlowScriptData — optional but recommended).

**Target content:**

```flow
use "@std"

Note: DX-05 slice — Array overload
Array[Int] arr = [1, 2, 3, 4, 5]
Array[Int] mid = slice arr 1 4
Array[Int] neg = slice arr -5 2
Array[Int] over = slice arr 3 100
Array[Int] emp = slice arr 3 2
(print (str (length mid)))
(print (str (length neg)))
(print (str (length over)))
(print (str (length emp)))

Note: DX-05 slice — Sequence overload
Sequence seq = | C4 D4 | E4 F4 | G4 A4 |
Sequence middle = slice seq 1 2
(print (str middle))

(print "test_slice: PASSED")
```

**Gotcha:** The auto-glob adds this as a Theory row. If you want specific sentinels pinned in `FlowScriptData.RequiredSentinels`, the planner adds them (matches `test_transpose_int.flow` at `FlowScriptData.cs:75-79`). Otherwise default assertion is "success + clean stderr".

---

### `tests/test_flat_literals.flow` (create — stdlib-function exercise)

**Analog:** `tests/test_transpose_int.flow`.

Same structure as `test_slice.flow`. Exercise the extended Parse surface:

```flow
use "@std"

Note: DX-06 — flat letters in note streams
Sequence flats = | Db4 Eb4 Gb4 Ab4 Bb4 Cb4 Fb4 |
(print (str flats))

Note: DX-06 — mixed alteration composition
Sequence mixed = | Bb-+bbb |
(print (str mixed))

(print "test_flat_literals: PASSED")
```

**Gotcha:** The planner must verify that the extended lexer + Parse produce the expected triples. `Cb4` should MIDI to 59 (in range), `Fb` has alt -1 and defaults to octave 4.

---

### `tests/test_enharmonic.flow` (create — key-context integration)

**Analog:** `tests/test_dynamics.flow` (lines 1-40) for the `key Keyname { ... }` context-block pattern.

**Pattern** (`test_dynamics.flow:23-36`):

```flow
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            dynamics f {
                Sequence forteMel = | C4 D4 E4 F4 |
                (print (str forteMel))
                dynamics pp {
                    Sequence softMel = | C4 D4 E4 F4 |
                    (print (str softMel))
                }
            }
        }
    }
}
```

**Copy pattern for enharmonic:**

```flow
use "@std"

Note: DX-06 — enharmonic, no key context
(print (str (enharmonic Db4)))   Note: expect C#4
(print (str (enharmonic C4)))    Note: expect C4 (natural unchanged)

Note: DX-06 — in-key respelling
key Dbmajor {
    (print (str (enharmonic C#4)))   Note: expect Db4 in Dbmajor
}

(print "test_enharmonic: PASSED")
```

**Gotcha:** The `(str (enharmonic X))` output format depends on the canonical emission chosen for Format (D-08, Claude's Discretion). Use run-based `+`/`-` for round-trip safety.

---

### `tests/test_dynamics_midi_velocity.flow` (create — MIDI write)

**Analog:** `tests/test_midi_export.flow` (lines 1-48).

**Pattern** (`test_midi_export.flow:1-29`):

```flow
use "@std"
use "@audio"

(print "=== MIDI Export Tests ===")

Note: Test 1: 3/4 waltz in G major at 140 BPM
tempo 140 {
    timesig 3/4 {
        key Gmajor {
            section waltz {
                | G4q B4q D5q |
                | D5h G4q |
            }

            section ending {
                | G4h. |
            }

            Song song = [waltz waltz ending]

            (writeMidi "/tmp/test_flow_export.mid" song)
            (print "MIDI file written to /tmp/test_flow_export.mid")
            (print "PASS: writeMidi completed without error")
        }
    }
}
```

**Copy pattern for test_dynamics_midi_velocity:**

```flow
use "@std"
use "@audio"

Note: DX-08 — deterministic 5-note crescendo for MIDI velocity regression
Sequence base = | C4 D4 E4 F4 G4 |
Sequence curve = base -> crescendo 0.25 0.75
section s { curve }
Song song = [s]
(writeMidi "tests/output/dynamics_velocity.mid" song)
(print "dynamics_velocity: PASSED")
```

**Gotchas:**
- **Write path:** The DX-08 Fact reads from a relative path (`tests/output/dynamics_velocity.mid`). The Fact sets `Environment.CurrentDirectory` to repo root before `RunFile`. The script must use that same relative path. RESEARCH §"Pitfall 7" confirms `writeMidi` flushes synchronously.
- **Output directory must exist or `writeMidi` must auto-mkdir.** FlowScriptData comment at lines 52-54 notes auto-mkdir was FIXED by plan 12-05 (`exportWav auto-mkdir`). Planner should verify `writeMidi` shares this behavior; if not, the Fact's `Directory.CreateDirectory(...)` pre-step is the fallback.
- **Planner computes the expected byte sequence from actual script values at plan time**, not hardcoded from RESEARCH. See RESEARCH §"Expected velocity bytes for verification test" for the formula: `(int)(velocity * 127)` per note, clamped to 1..127.
- **Open Question 2 in RESEARCH:** `dynamics f` may or may not map to 0.8 — verify at plan time in `Interpreter.cs` / grep for `"ff"`, `"f"`, `"mp"` velocity constants. DX-08 uses `crescendo 0.25 0.75` specifically to bypass this ambiguity (explicit numeric bounds).

---

## Shared Patterns

### Phase 14 test directory convention (Phase 13 D-09)
**Source:** Existing `flow-lang.Tests/Unit/Phase08/`, `Phase10/`, `Integration/Phase06/`, `Phase07/`, `Phase09/`.
**Apply to:** All new Phase 14 Facts.
**Pattern:**
- Unit tests → `flow-lang.Tests/Unit/Phase14/XxxTests.cs` with namespace `FlowLang.Tests.Unit.Phase14`.
- Integration tests → `flow-lang.Tests/Integration/Phase14/XxxTests.cs` with namespace `FlowLang.Tests.Integration.Phase14`.
- Directory must be created fresh — no Phase14/ exists yet.

### `[Collection("FlowScripts")]` serialization
**Source:** `InterpreterTests.cs:14`, `SectionGainBareExpressionTests.cs:16`.
**Apply to:** Every Fact that uses `FlowEngineRunner` (which calls `Console.SetOut`).
**Pattern:**
```csharp
[Collection("FlowScripts")]   // serialize Console.SetOut across parallel Facts
public class YourTests { ... }
```
**Skip for:** Direct C# API Facts (SliceTests, NoteTypeTests, LexerTests) that don't touch `FlowEngineRunner`.

### `use "@std"` prelude in RunSource snippets
**Source:** `InterpreterTests.cs:20-25` explicit comment block.
**Apply to:** Every `runner.RunSource(@"...")` snippet that calls `print` or any stdlib function.
**Pattern:**
```csharp
var (_, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
... your flow code ...
");
```
**Skip for:** `runner.RunFile(path)` calls — the script itself has its own `use "@std"` at line 1.
**Gotcha:** Phase 12 Plan 04 discovered this — `print` is unresolved without the prelude even though the C# impl is registered at engine init.

### Observable-value pins (Phase 13 D-11)
**Source:** `FormantDataTests.cs:24` (error text equality), `SectionGainBareExpressionTests.cs:35-36` (stdout substring).
**Apply to:** All new Facts.
**Allowed pin types:**
- Exact error message text: `Assert.Equal("Note Cb0 is out of valid range (E0 to E10)", ex.Message)`
- Numeric counts: `Assert.Equal(2, elements.Count)`
- Exact byte sequences: `Assert.Equal(new byte[]{31, 47, 63, 79, 95}, velocities)`
- stdout substring: `Assert.Contains("C#4", stdout)`
**Forbidden:** Buffer byte hashes, audio waveform checksums (Phase 13 explicit exclusion).

### Atomic commits per feature (ROADMAP criterion 3)
**Source:** Phase 12 D-18, Phase 13 D-06.
**Apply to:** All four Phase 14 plans.
**Pattern:** Each bisectable feature lands in one commit. Plan 14-02 may ship two commits (A: NoteType/Lexer, B: enharmonic) per D-17 — but each commit is itself atomic and test-complete.

### Wave-1 parallel execution
**Source:** Phase 12 / 13 D-06.
**Apply to:** Plans 14-01, 14-02, 14-03 (zero file overlap per D-20).
**Pattern:** All three plans can run in parallel worktree-style; planner does not sequence them. 14-04 is strictly last and depends on all prior commits.

---

## No Analog Found

Files with no close match in the codebase:

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `flow-lang.Tests/Unit/Phase14/LexerTests.cs` | Lexer-token unit test | request-response | No prior unit tests assert on SimpleLexer token output. Nearest shape is `InterpreterTests.cs` but its assertion surface is stdout, not `TokenType`. Planner must verify SimpleLexer's construction API at plan time (see LexerTests section above). |

All other files have at least role-match analogs. The LexerTests pattern is new but the implementation is mechanically simple — construct a `SimpleLexer(text)`, call its Tokenize entry point, inspect the first token's `Type` field.

---

## Metadata

**Analog search scope:**
- `flow-lang/StandardLibrary/Collections.cs`, `BuiltInFunctions.cs`, `Composition/SongFunctions.cs`, `Harmony/HarmonyFunctions.cs`
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs`
- `flow-lang/Lexing/SimpleLexer.cs`
- `flow-lang/Runtime/Value.cs` (Value.Note factory)
- `flow-lang.Tests/Unit/CollectionsTests.cs`, `InterpreterTests.cs`, `Phase08/MixTests.cs`, `Phase10/FormantDataTests.cs`
- `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs`, `Phase07/`, `Phase09/TutorialTests.cs`
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs`, `FlowScriptData.cs`
- `tests/test_transpose_int.flow`, `test_dynamics.flow`, `test_midi_export.flow`, `test_crescendo.flow`

**Files scanned:** 18
**Pattern extraction date:** 2026-04-19

---

## PATTERN MAPPING COMPLETE
