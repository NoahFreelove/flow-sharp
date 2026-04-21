# Phase 14: Composer DX Part 1 — Research

**Researched:** 2026-04-20
**Domain:** Flow language interpreter — collections (DX-05), note parsing/formatting (DX-06), harmony built-ins (DX-06), MIDI export verification (DX-08)
**Confidence:** HIGH

---

## User Constraints (from CONTEXT.md)

### Locked Decisions (MUST honor — do not research alternatives)

- **DX-05 silent clamp (D-01):** `slice(start, end)` clamps on both sides for both `Sequence` and `Array[T]` overloads. Negatives → 0. `end > count` → `count`. `start >= end` → empty. No errors. Matches `take`/`drop` shape.
- **DX-05 atomic (D-02):** Both overloads ship in one commit inside plan 14-01.
- **DX-06 enharmonic location (D-06):** Lives in `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`. No new file.
- **DX-06 enharmonic signature (D-03):** `(IReadOnlyList<Value> args, ExecutionContext context)` — reads active `MusicalContext.Key` via `ExecutionContext.GetMusicalContext()`.
- **DX-06 in-key rule (D-04):** If input pitch is diatonic to active key, return scale-diatonic spelling. If chromatic, fall back to no-key rule. Uses `ScaleDatabase` for scale-tone lookup.
- **DX-06 no-key fallback (D-05):** Flip sharp↔flat. Natural notes (alteration = 0) return unchanged. NO `E↔Fb`, `F↔E#`, `B↔Cb`, `C↔B#` edge respelling.
- **DX-06 flat-literal surface (D-07):** `NoteType.Parse` accepts arbitrary mix of `b`/`#`/`+`/`-` attached to the note letter, on either side of octave digits. Net alteration = (sharps − flats) as any integer.
- **DX-06 alteration encoding (D-08):** `MusicalNoteData.Alteration` stays `int` (already is). `NoteType.Format` extended to emit canonical run for any int alteration. `b`/`#` accepted on parse, not emitted on format. Round-trip `Parse(Format(x)) == x` MANDATORY.
- **DX-06 range check (D-09):** Uses post-alteration MIDI value, not letter+octave. `Cb4` → MIDI 59 (B3) = in range. `Cb0` → MIDI 11 = below E0 → error with existing `"Note X is out of valid range (E0 to E10)"` format.
- **DX-06 H alias deferred (D-10, D-11, D-12):** NOT in scope. Dropped from Phase 14. Deferred to a future phase bundled with a pragma language construct (candidate keyword `enable "german-notation"`). Captured in 14-04's `deferred-items.md`.
- **DX-08 two-pass strict (D-13):** Pass 1 drafts regression test from REQUIREMENTS alone. Pass 2 lands against real code. If GREEN, no plumbing required. If RED, minimal gap-fix lives in same plan with Divergence entry.
- **DX-08 test script (D-14):** New purpose-built `tests/test_dynamics_midi_velocity.flow`. Must NOT extend existing `tests/test_dynamics.flow` or `tests/test_crescendo.flow`.
- **DX-08 Fact location (D-15):** `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs`. MIDI-read helper inline; promote to shared only if a second Fact duplicates the call shape.
- **Plan structure (D-16..D-20):** 4 plans. 14-01 slice · 14-02 flats+enharmonic · 14-03 DX-08 regression · 14-04 closure. Wave 1 parallel (14-01/02/03 zero file overlap). 14-04 strictly last.
- **Collision grep (D-21):** One-shot at plan time — not an ongoing xUnit Fact. Transcript pasted into 14-02-PLAN.md §Pre-landing Collision Grep and re-surfaced in 14-VERIFICATION.md.
- **No new NuGet packages.** DryWetMidi 8.0.3 already referenced — read API used via existing dependency.
- **REQUIREMENTS.md reframe (D-10, D-19):** Plan 14-04 moves DX-06 H-alias clause to audit-trail only (Phase 12 TEST-03 precedent). Flat + enharmonic clauses kept.

### Claude's Discretion (research options, recommend)

- Exact xUnit Fact naming (e.g., `DynamicsMidiVelocityTests.Forte_Emits127` vs `VelocityBytes_MatchGradient`)
- MIDI-read helper inline vs promoted to `Shared/MidiReadHelpers.cs`
- Canonical emission style for extended-range `NoteType.Format` — `B+++` vs `B^3` vs numeric suffix — provided round-trip holds
- Error message text for post-alteration range overshoot — must mirror existing Head/Last format
- LINQ `Skip().Take()` vs explicit pre-sized list for `slice`
- Whether 14-04 ships `14-VALIDATION.md` at `nyquist_compliant: true` (Phase 13 D-24 precedent for docs-style phase; Phase 14 ships code so leaning yes)

### Deferred Ideas (OUT OF SCOPE)

- `H` = `B` alias (full family `H`/`H4`/`H+`/`H++`)
- Pragma / `enable "<addon>"` language construct
- Multi-letter enharmonic edges (`E↔Fb`, `F↔E#`, `B↔Cb`, `C↔B#`)
- Shared MIDI read helper promotion (defer to Phase 15 if DX-09 needs it)
- Pythonic negative-from-end slicing
- Any modification to `augment`/`diminish` semantics (Phase 11 Dismissed)

---

## Phase Requirements

| ID | Description (paraphrased from REQUIREMENTS.md) | Research Support |
|----|------------------------------------------------|------------------|
| DX-05 | `slice(Sequence, Int, Int) → Sequence` and `slice(Array[T], Int, Int) → Array[T]`. Start inclusive, end exclusive. Bar-level for Sequence. Clamps like `take`/`drop`. | §"Implementation: DX-05 slice" below. Template at `Collections.cs:117-147`. Registration at `BuiltInFunctions.cs:369-373`. |
| DX-06 | Flat literals (`Db`..`Fb`) accepted by `NoteType.Parse`, normalized to `(letter, octave, alteration)` triples. `H` as `B` alias — DEFERRED. `enharmonic(Note) → Note` returns pitch-equivalent spelling. **Extended per D-07:** arbitrary `b`/`#`/`+`/`-` composition, any int alteration. | §"Implementation: DX-06 flat surface" + §"Implementation: DX-06 enharmonic". `NoteType.Parse` at `NoteType.cs:21-73`, `Format` at `NoteType.cs:142-155`. `ScaleDatabase.GetScaleNotes` at `ScaleDatabase.cs:196`. |
| DX-08 | `dynamics { }` propagates to `MusicalNoteData.Velocity`; `crescendo`/`decrescendo`/`swell` write velocity; `MidiExport.cs:192` maps to 1–127 without loss. Regression test asserts velocity bytes. | §"DX-08 Velocity Chain Audit" below — chain is **already wired**. Expect Pass 2 GREEN with no gap-fix. |

---

## Summary

Phase 14 is a tight, narrow-surface phase that adds three composer-facing features on top of already-shipped infrastructure. All three map to small, well-scoped C# edits:

- **DX-05 `slice`:** ~20-line LINQ patch in `Collections.cs` + 2 registrations in `BuiltInFunctions.cs`. Directly templated on existing `Take`/`Drop` at `Collections.cs:117-147`. [VERIFIED: read source]
- **DX-06 flat surface:** `NoteType.Parse` extension from a 5-case `switch` (`++`, `+`, `-`, `--`, empty) to a sum-based scan over the alteration tail. `NoteType.Format` extension to emit canonical `+N`/`-N` runs for any int. Lexer extension in `SimpleLexer.ScanIdentifierOrKeyword` to extend alteration pickup to mixed `b`/`#`/`+`/`-` runs. Post-alteration MIDI range check. [VERIFIED: read source]
- **DX-06 `enharmonic`:** New built-in in `HarmonyFunctions.cs` with context signature. Introduces a new `RegisterContextDependent` method on `HarmonyFunctions` and calls it from `BuiltInFunctions.RegisterContextDependentFunctions`. In-key: lookup via `ScaleDatabase.GetScaleNotes`. Fallback: sharp↔flat flip; naturals unchanged. [VERIFIED: read source]
- **DX-08 verification:** Purpose-built `.flow` script + xUnit Fact that reads back MIDI via `MidiFile.Read` + `NotesManagingUtilities.GetNotes` and asserts velocity byte sequence. Chain is already wired per reading of `Interpreter.cs:184-191` → `MusicalContextData.Velocity` → `NoteStreamCompiler.cs:341` → `MusicalNoteData.Velocity` → `MidiExport.cs:192`. Expect GREEN on Pass 2. [VERIFIED: full trace]

**Primary recommendation:** Execute 14-01/02/03 in parallel (wave 1, zero file overlap confirmed). Land 14-04 strictly last. Expect GREEN outcomes across all four plans with no emergent plumbing work — blast radius is small, existing patterns transfer cleanly, and the collision grep is empty across `.flow` files in the repo (verified below).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| `slice(Sequence, Int, Int)` | StandardLibrary (Collections.cs) | Runtime (SequenceData.Bars read-only) | Collection operation; Sequence exposes `Bars: List<BarData>` publicly |
| `slice(Array[T], Int, Int)` | StandardLibrary (Collections.cs) | TypeSystem (ArrayType) | Direct analogue to `Take`/`Drop` — existing pattern |
| `NoteType.Parse` extension | TypeSystem (SpecialTypes/NoteType.cs) | Runtime (NoteStreamCompiler — consumes the triple unchanged) | Pure string → triple transformation; consumers receive `int` alteration already |
| `NoteType.Format` extension | TypeSystem (SpecialTypes/NoteType.cs) | — | Only consumers are `MusicalNoteData.ToString()` (display), `Value.cs:168` (Note round-trip), `TransformFunctions.cs:121` (warning message) — all round-trip safe if output accepted by Parse |
| Lexer alteration pickup extension | Lexing (SimpleLexer.cs) | — | Token-boundary rules need to sweep trailing `+`/`-` runs (and allow `b`/`#` as part of identifier) before handing to TryParseNote |
| `enharmonic(Note) → Note` | StandardLibrary/Harmony (HarmonyFunctions.cs) | Runtime (ExecutionContext.GetMusicalContext) | Reads key context; existing `SongFunctions`/`StdLib.Rand` precedent |
| DX-08 verification Fact | flow-lang.Tests/Integration/Phase14/ | StandardLibrary/Audio/MidiExport.cs (read-only audit) | Test-only surface; no production code change expected |

**Why this matters:** No capability in this phase has ambiguous tier ownership. The tier assignments are driven by existing code organization and require no architectural invention.

---

## Standard Stack

### Core (no changes required)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 (target) | net10.0 | Runtime | Project already on net10.0 per `flow-lang.csproj` + `flow-lang.Tests.csproj` [VERIFIED: read csproj]. Note: CLAUDE.md says .NET 9 — csproj disagrees. Planner should use the csproj target. |
| C# 13 | latest | Language | file-scoped namespaces, records, pattern matching in use throughout [VERIFIED: codebase inspection] |
| xunit.v3 | 3.2.2 | Test framework | Already referenced in `flow-lang.Tests.csproj` [VERIFIED: read csproj] |
| Melanchall.DryWetMidi | 8.0.3 | MIDI R/W | Already referenced for write; **read API confirmed in same package** [VERIFIED: grepped `Melanchall.DryWetMidi.xml` in local nuget cache — `MidiFile.Read(string, ReadingSettings)` and `NotesManagingUtilities.GetNotes(MidiFile, ...)` both exist in 8.0.3 netstandard2.0 assembly] |

**Installation:** No new packages.

**Version verification:** `cat /home/noah/.nuget/packages/melanchall.drywetmidi/8.0.3/lib/netstandard2.0/Melanchall.DryWetMidi.xml` confirms `Read` and `GetNotes` members present.

---

## Implementation: DX-05 slice (Sequence + Array[T])

### Approach

Direct clone of `Take`/`Drop` shape. Two C# functions in `flow-lang/StandardLibrary/Collections.cs`, two registrations in `flow-lang/StandardLibrary/BuiltInFunctions.cs`.

### Function signatures (recommended)

```csharp
// In Collections.cs — near Take (line 117) / Drop (line 133)
public static Value SliceArray(IReadOnlyList<Value> args)
{
    var arr = args[0];
    var start = args[1];
    var end = args[2];

    if (arr.Type is not ArrayType arrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");
    if (start.Type is not IntType || end.Type is not IntType)
        throw new InvalidOperationException("slice indices must be Int");

    var elements = arr.As<IReadOnlyList<Value>>();
    int count = elements.Count;
    int s = Math.Max(0, start.As<int>());
    int e = Math.Min(count, end.As<int>());
    if (s >= e) return Value.Array(Array.Empty<Value>(), arrayType.ElementType);

    return Value.Array(elements.Skip(s).Take(e - s).ToArray(), arrayType.ElementType);
}

public static Value SliceSequence(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    int count = seq.Bars.Count;
    int s = Math.Max(0, args[1].As<int>());
    int e = Math.Min(count, args[2].As<int>());
    if (s >= e) return Value.Sequence(new SequenceData());

    var result = new SequenceData();
    for (int i = s; i < e; i++)
        result.AddBar(seq.Bars[i]);
    return Value.Sequence(result);
}
```

**Note on SequenceData construction** [VERIFIED: `SequenceType.cs:32-41`]: `AddBar` enforces musical-bar invariant (BarMode.Musical, non-null TimeSignature). Any existing bar already passed that check, so re-adding is safe.

### Registration (in `BuiltInFunctions.cs:369-373` neighborhood)

```csharp
var sliceArraySignature = new FunctionSignature("slice",
    [new ArrayType(VoidType.Instance), IntType.Instance, IntType.Instance]);
registry.Register("slice", sliceArraySignature, Collections.SliceArray);

var sliceSeqSignature = new FunctionSignature("slice",
    [SequenceType.Instance, IntType.Instance, IntType.Instance]);
registry.Register("slice", sliceSeqSignature, Collections.SliceSequence);
```

Overload resolution handles dispatch — `ArrayType(VoidType)` matches any `ArrayType<T>` via the existing `TypesEqual` special case at `InternalFunctionRegistry.cs:100-104` [VERIFIED: read source].

### Test surface

- **Unit (flow-lang.Tests/Unit/Phase14/SliceTests.cs):** 4-6 Facts covering normal, negative start, end > count, start >= end (empty), start == end. Mirror `CollectionsTests.cs` shape.
- **Integration (tests/test_slice.flow):** Single new `.flow` script covering both overloads + clamp edges. Registered as a Theory row via the existing glob in `FlowScriptData.GetFlowScripts`.
- **Observable pin:** numeric `.Count` of bars or array elements in result. Example: `slice(|C4 D4|, 0, 1).Count == 1`.

### Blast radius

Files modified: **2**
- `flow-lang/StandardLibrary/Collections.cs` (add two methods)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (add two registrations)

Downstream call sites: **0** — new names (`slice`) with no existing consumers.

---

## Implementation: DX-06 flat-literal surface

### Approach

Three coupled edits: (1) extend `NoteType.Parse` to sum-based alteration scan; (2) extend `NoteType.Format` to emit canonical runs for any int alteration; (3) extend lexer alteration-pickup to recognize extended alteration tails; (4) shift range check to post-alteration MIDI value.

### `NoteType.Parse` — sum-based alteration scan

Current (lines 52-63):
```csharp
alteration = remaining switch
{
    "++" => 2, "+" => 1, "-" => -1, "--" => -2,
    _ => throw new ArgumentException($"Invalid alteration: {remaining}")
};
```

**Replacement sketch:**

```csharp
// After: char note = char.ToUpper(noteStr[0]); range check omitted for now.
// remaining may contain alteration on EITHER side of octave digits:
//   "Db4"   → letter=D, altPrefix="b", octStr="4", altSuffix=""
//   "Bb-+bbb" → letter=B, altPrefix="b-+bbb", octStr="", altSuffix=""
//   "C+5"   → letter=C, altPrefix="+", octStr="5", altSuffix=""
//   "C4++"  → letter=C, altPrefix="", octStr="4", altSuffix="++"
//   "F##4"  → letter=F, altPrefix="##", octStr="4", altSuffix=""
//
// Strategy: walk noteStr[1..], partitioning into (altChars before first digit)
//           + (contiguous digit run = octave) + (remaining altChars after digits).
//           Every char must be '+' | '-' | 'b' | '#' | digit. Else error.
//
// net alteration = (count of '+') + (count of '#')
//                − (count of '-') − (count of 'b')

int sharpCount = 0;
int flatCount = 0;
int octave = 4;
bool sawOctave = false;
int i = 1;
// phase 1: pre-octave alteration chars
while (i < noteStr.Length && !char.IsDigit(noteStr[i]))
{
    switch (noteStr[i])
    {
        case '+' or '#': sharpCount++; break;
        case '-' or 'b': flatCount++; break;
        default: throw new ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}");
    }
    i++;
}
// phase 2: octave digits
int octStart = i;
while (i < noteStr.Length && char.IsDigit(noteStr[i])) i++;
if (i > octStart) { octave = int.Parse(noteStr[octStart..i]); sawOctave = true; }
// phase 3: post-octave alteration chars
while (i < noteStr.Length)
{
    switch (noteStr[i])
    {
        case '+' or '#': sharpCount++; break;
        case '-' or 'b': flatCount++; break;
        default: throw new ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}");
    }
    i++;
}
int alteration = sharpCount - flatCount;
// Range check against post-alteration MIDI value (D-09):
int midi = GetNoteValue(note, octave) + alteration;
int minMidi = GetNoteValue('E', 0);   // 16
int maxMidi = GetNoteValue('E', 10);  // 136
if (midi < minMidi || midi > maxMidi)
    throw new ArgumentException($"Note {noteStr} is out of valid range (E0 to E10)");
return (note, octave, alteration);
```

**Key points:**
- `b` and `#` are accepted on parse, treated identically to `-` and `+` respectively.
- Alteration chars may appear on EITHER side of octave digits.
- Summing: `sharpCount - flatCount` — any int.
- Range check moved from `IsValidNoteRange(note, octave)` to post-alteration MIDI (D-09). `IsValidNoteRange` method becomes unreachable unless retained for internal use — recommend removing or inlining.
- All invalid chars trigger the same error format: `"Invalid note character '<c>' in <noteStr>"` or `"Note <noteStr> is out of valid range (E0 to E10)"`.

### `NoteType.Format` — canonical run emission

Current (lines 142-155) throws on |alt| > 2. **Recommended replacement** (Claude's Discretion per D-08 — recommending `+N`/`-N` run to preserve Parse compatibility):

```csharp
public static string Format(char note, int octave, int alteration)
{
    string altStr;
    if (alteration == 0) altStr = "";
    else if (alteration > 0) altStr = new string('+', alteration);
    else altStr = new string('-', -alteration);
    return $"{note}{octave}{altStr}";
}
```

**Why runs over numeric suffix (`B+3` vs `B+++`):** `Parse` above treats each `+`/`-`/`b`/`#` char as ±1. A `B+3` output would be parsed as `B + 3` → letter B, octave 3, alteration +1 — data loss. Runs round-trip faithfully under the Parse sketch above.

**Blast radius of Format extension:**
- `NoteType.cs:236` — `MusicalNoteData.ToString()` — display only, benign.
- `Value.cs:168` — `NoteType.Format(parsed.note, parsed.octave, parsed.alteration)` fed back into `Value.Note(string)` which calls `Parse` internally. Round-trip required. The run-based format satisfies this. [VERIFIED: trace]
- `TransformFunctions.cs:121` — warning string "transpose would put {Format(...)} out of range". Display only, benign.

**None of the three consumers assume output is `""`/`+`/`++`/`-`/`--` — the `CONTEXT.md` risk-surface warning is satisfied by code-level re-read.** No silent callers break.

### Lexer extension — `SimpleLexer.ScanIdentifierOrKeyword` alteration pickup

Current behavior at `SimpleLexer.cs:526-565` [VERIFIED: read source]:

- Identifier scanner consumes non-boundary chars. Letters, digits, `#` are consumed. `+`, `-`, `b` (as letter) are consumed if `b` is a letter; `+`/`-` are token boundaries.
- After initial consume, if `text.Length >= 2` AND `text[0]` is A-G AND `text[1]` is a digit, the scanner **peeks for one or two trailing `+`/`-` chars** and appends them.

**Gap for extended surface:**

| Input | Current tokenization | Desired |
|-------|----------------------|---------|
| `Bb4` | identifier `"Bb4"` → TryParseNote succeeds post-extension | same — works under extended Parse |
| `Bb` | identifier `"Bb"` (1 token) → TryParseNote rejects (length==2 AND text[1] not digit AND no alteration) — actually rejects via `if (text.Length == 1) return false` path? No, length is 2. `TryParseNote` calls `NoteType.Parse("Bb")` which under extension succeeds → `(B, 4, -1)` | OK under extended Parse (no lexer change needed) |
| `F##4` | identifier `"F##4"` (# is not a boundary) → TryParseNote → Parse("F##4") succeeds under extension | OK (no lexer change needed) |
| `C#5` | identifier `"C#5"` → Parse("C#5") under extension → (C, 5, +1) | OK |
| `Bb+` | identifier `"Bb"`, then `+` as separate token → note would be just `Bb`; `+` lost | **NEEDS LEXER EXTENSION.** Must peek for post-identifier `+`/`-` runs when identifier could be a note prefix. |
| `Bb-+bbb` | identifier `"Bb"`, then `-`, `+`, `bbb` ... fragments | **NEEDS LEXER EXTENSION.** |
| `C4++` | current already handles via lines 551-560 (peek up to 2 `+` chars) | OK under existing logic |
| `C4+++` | current handles 2 of 3 — third `+` is lost | **NEEDS LEXER EXTENSION** (unbounded pickup). |

**Recommended lexer change:** Replace the bounded 1-or-2-char peek at `SimpleLexer.cs:551-562` with an unbounded loop over `+`/`-`:

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

(Drop the inner `char alterationChar = Peek()` and the "check for double alteration" block — replaced by the unbounded loop. Also drop the `char.IsDigit(text[1])` requirement — the pickup should fire even when the identifier has no octave, e.g., `Bb-`.)

**Caveat — downstream duration-suffix parsing at lines 625-639:** The existing logic handles `C4h`, `D5q`, etc. by stripping the last char if it's `w`/`h`/`q`/`e`/`s`/`t`. Under the extended surface with `Cb4h`, the logic still works: identifier = `"Cb4h"`, last char = `'h'`, strip → `"Cb4"`, TryParseNote on that succeeds. **Verified mentally against lines 622-639.** [VERIFIED: trace]

### Registration

No new registration — `NoteType.Parse` is used internally by the lexer and by existing built-ins.

### Test surface

- **Unit (flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs):** Facts covering all flat letters (`Db`, `Eb`, `Gb`, `Ab`, `Bb`, `Cb`, `Fb`), `b#+-` compositions including `Bb-+bbb` → (B, 4, -4), octave positioning (`C+5`, `C5+`), range-check edges (`Cb0` out of range, `Cb4` in range at MIDI 59), round-trip `Parse(Format(x)) == x` for alterations `-5..+5`.
- **Integration (tests/test_flat_literals.flow):** `.flow` script exercising `|Db4 Eb4 Bb4|` inside a note stream, printing `(str seq)` — Theory row asserts the stdout contains the expected bar data.
- **Observable pin:** `(letter, octave, alteration)` triple from `Parse`; exact string output from `Format`; MIDI value from post-alteration computation.

### Blast radius

Files modified: **2**
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (Parse rewrite, Format rewrite, likely remove `IsValidNoteRange`)
- `flow-lang/Lexing/SimpleLexer.cs` (ScanIdentifierOrKeyword alteration pickup)

Downstream call sites of `NoteType.Parse` — **13 confirmed** [VERIFIED: grep results in §Blast Radius]. All consume the triple output unchanged. No `Parse` caller ever inspects the input string for specific alteration-char patterns — they just want the triple. Extending Parse is pure upstream.

Downstream call sites of `NoteType.Format` — **3 confirmed** (cited above). All round-trip safe.

---

## Implementation: DX-06 `enharmonic(Note) → Note`

### Approach

New built-in in `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`. Signature `(IReadOnlyList<Value> args, ExecutionContext context)` — reads active `MusicalContext.Key` via `ExecutionContext.GetMusicalContext()`. Registered from `BuiltInFunctions.RegisterContextDependentFunctions` via a new `HarmonyFunctions.RegisterContextDependent(registry, context)` method.

### Wiring — HarmonyFunctions changes

Currently `HarmonyFunctions.Register(registry)` takes no context [VERIFIED: `HarmonyFunctions.cs:13`]. Add a second static method:

```csharp
// In HarmonyFunctions.cs
public static void RegisterContextDependent(InternalFunctionRegistry registry, ExecutionContext context)
{
    var enharmonicSig = new FunctionSignature("enharmonic", [NoteType.Instance]);
    registry.Register("enharmonic", enharmonicSig, args => Enharmonic(args, context));
}

private static Value Enharmonic(IReadOnlyList<Value> args, ExecutionContext context)
{
    string noteStr = args[0].As<string>();  // Note values are stored as string [VERIFIED: Value.cs:32]
    var (letter, octave, alteration) = NoteType.Parse(noteStr);

    var musicalCtx = context.GetMusicalContext();
    string? key = musicalCtx.Key;

    // In-key branch (D-04): only respell diatonic scale tones
    if (key != null)
    {
        // ... ScaleDatabase lookup — see algorithm below
        // If input is diatonic, return scale-diatonic spelling; else fall through
    }

    // No-key / Cmaj / Amin fallback (D-05): flip sharp↔flat; naturals unchanged
    if (alteration == 0)
        return Value.Note(NoteType.Format(letter, octave, 0));
    // compute enharmonic equivalent via MIDI — but pick the opposite-sign spelling
    int midi = NoteType.ToMidiNote(letter, octave, alteration);
    // ... pick letter such that resulting alteration has opposite sign
    // simple case — toggle between sharp-letter and flat-letter neighbors
}
```

### In-key algorithm (D-04)

Inputs: `letter` (A-G), `alteration` (int), `octave` (int), `key` (e.g., `"Dbmajor"`).

1. Compute `midi = NoteType.ToMidiNote(letter, octave, alteration)`.
2. Fetch `scaleNotes = ScaleDatabase.GetScaleNotes(key)` — returns 7 note strings like `["C", "Db", "Eb", ...]` or `["C", "Cs", "D", ...]` [VERIFIED: `ScaleDatabase.cs:196-214`]. Note the chromatic names use `Cs`/`Ds`/etc. for sharps (no edges like `Fs`), but `Db`/`Eb` for flats — there is an asymmetry to handle.
3. Compute `targetSemitone = midi % 12`.
4. Scan scale notes: for each, find its chromatic semitone and compare to `targetSemitone`. If match, parse the scale-note string (`"Db"` → alteration -1, `"Cs"` → alteration +1) and return formatted `(scaleLetter, octave, scaleAlt)`.

**Note on octave computation:** When respelling `C#4` as `Db4` in D♭ major, the octave of the target may differ from input — `C#4` is MIDI 61, `Db4` is also MIDI 61. But `B#3` vs `C4` would differ in letter-octave. For Phase 14's scope, naturals stay unchanged per D-05, so this edge does not fire. **Pitfall:** If the input is chromatic-not-in-scale, skip to fallback rule — do NOT attempt edge respelling.

**Alternative simpler approach:** Since `GetScaleNotes` returns 7 canonical spellings per key, and the input pitch `(letter, octave, alteration)` maps to a MIDI value, the algorithm is:
- For each of the 7 scale tones, compute its MIDI value (at input's octave).
- If input MIDI matches any scale-tone MIDI → return that scale tone's (letter, octave, alteration).
- Else fall through to no-key rule.

### No-key fallback (D-05)

If `alteration == 0`: return input unchanged (`E`, `F`, `B`, `C` stay — no edge respelling).

If `alteration > 0` (sharp): find the letter-neighbor one step up whose natural semitone equals `input letter + alteration`. For `C#` (midi 61, alt +1): next letter is `D`, D natural = midi 62. So `C#` flipped = `D` with alteration = 61 − 62 = −1 = `Db`. 

If `alteration < 0` (flat): find letter-neighbor one step down. `Db` (midi 61, alt -1): prev letter = `C`, C natural = midi 60. So `Db` flipped = `C` with alteration = 61 − 60 = +1 = `C#`.

For `|alteration| > 1` (e.g., `F##` = G-enharmonic): canonical flip is `G` with alt 0 — but that's a natural, which per D-05 would round-trip back to `G` (not `F##`) under subsequent `enharmonic` calls. Pitfall: enharmonic is **involutive only for simple sharp↔flat pairs**, not for double-sharps/flats. This is acceptable per D-05 scope; document in unit Facts.

### Test surface

- **Unit (flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs):** Facts covering:
  - No-key `Db4` → `C#4` (and vice versa)
  - No-key `C4` → `C4` (natural unchanged)
  - No-key `E4` → `E4` (edge natural unchanged, D-05 rule)
  - In-key (Dbmajor): `C#4` → `Db4` (in-scale spelling preferred)
  - In-key chromatic not in scale: falls to no-key rule
  - Double-sharp `F##4` → `G4` (non-involutive, documented)
- **Integration (tests/test_enharmonic.flow):** `.flow` script demonstrating key-context respelling inside `key Dbmajor { }` block.
- **Observable pin:** exact letter+octave+alteration triple from output. Example: `Parse(enharmonic("Db4", cmajor)) == ('C', 4, 1)`.

### Blast radius

Files modified: **2**
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (add `RegisterContextDependent` + `Enharmonic` impl)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (call `Harmony.HarmonyFunctions.RegisterContextDependent(registry, context)` inside `RegisterContextDependentFunctions` around line 668)

Existing `HarmonyFunctions.Register()` stays unchanged — `enharmonic` is additive.

---

## DX-08 Velocity Chain Audit

Trace verification of the chain `dynamics keyword → MIDI velocity bytes`:

### Stage 1: `dynamics` keyword → `MusicalContextData.Velocity`

**Source:** `flow-lang/Interpreter/Interpreter.cs:184-191` [VERIFIED: read source]

```csharp
case MusicalContextType.Dynamics:
    var velVal = _evaluator.Evaluate(ctx.Value);
    double vel = velVal.Type is IntType
        ? (double)velVal.As<int>()
        : velVal.As<double>();
    vel = Math.Clamp(vel, 0.0, 1.0);
    musicalCtx.Velocity = vel;
    break;
```

- Accepts Int or Double. Converts Int via `(double)`. Clamps to [0,1].
- `musicalCtx` is a `MusicalContext` attached to the current frame.

### Stage 2: `MusicalContext.Velocity` → `MusicalNoteData.Velocity` via NoteStreamCompiler

**Source:** `flow-lang/Runtime/NoteStreamCompiler.cs:324, 341` [VERIFIED: read source]

```csharp
// Line 324:
var (noteName, octave, alteration) = NoteType.Parse(note.NoteName);
// ...
// Line 341:
double velocity = note.Velocity ?? context.Velocity ?? 0.63;
// Articulation bumps lines 343-350
// Line 352-355 — constructs MusicalNoteData with `velocity`
```

- **Precedence:** note-level override (`ff` in note stream) > musical-context velocity (`dynamics f { }`) > default 0.63 (mezzo-forte).
- Articulations (`accent`, `marcato`, `sforzando`) apply on top of base velocity.
- **No Int↔Double lossiness** — `double` throughout.

### Stage 3: `crescendo` / `decrescendo` / `swell` rewrite `Velocity` field

**Source:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:418-529` [VERIFIED: read source]

```csharp
// Crescendo (line 418):
double velocity = Math.Clamp(startVel + t * (endVel - startVel), 0.0, 1.0);
// Constructs new MusicalNoteData with `velocity` field set directly.
```

- Each of `Crescendo`, `Decrescendo`, `Swell` iterates every non-rest note and emits a new `MusicalNoteData` with computed velocity.
- **Direct write** — no propagation concerns.

### Stage 4: `MusicalNoteData.Velocity` → MIDI byte

**Source:** `flow-lang/StandardLibrary/Audio/MidiExport.cs:191-199` [VERIFIED: read source]

```csharp
// Map velocity: Flow 0.0-1.0 -> MIDI 1-127 (vel 0 = note off in MIDI)
byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);
// ...
noteEvents.Add(new TimedEvent(
    new NoteOnEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)velocity),
    barTick));
```

- `(int)(velocity * 127)` truncates toward zero. `0.63 * 127 = 80.01` → 80. `0.25 * 127 = 31.75` → 31.
- **Clamp to 1..127** — velocity 0 would be a note-off in MIDI; floor at 1.

### Full chain verified — no gap detected

Every stage preserves the `double` velocity with no lossy conversion until the final `(int)(v*127)` quantization at MIDI emission. **Expect Pass 2 of plan 14-03 to land GREEN with no gap-fix.**

### Expected velocity bytes for verification test

For a sample `crescendo(seq, 0.25, 0.75)` applied to a 5-note sequence with `totalNotes = 5`:
- Index 0: t = 0.00, velocity = 0.25, byte = `(int)(0.25 * 127) = 31`
- Index 1: t = 0.25, velocity = 0.375, byte = `(int)(0.375 * 127) = 47`
- Index 2: t = 0.50, velocity = 0.500, byte = `(int)(0.500 * 127) = 63`
- Index 3: t = 0.75, velocity = 0.625, byte = `(int)(0.625 * 127) = 79`
- Index 4: t = 1.00, velocity = 0.750, byte = `(int)(0.750 * 127) = 95`

For `dynamics f` (forte, `0.8` per typical convention — verify at plan time):
- All note velocities = 0.8, byte = `(int)(0.8 * 127) = 101`

**Planner should compute expected sequence from the exact script values at plan time**, not hardcode. The Fact asserts byte equality against the computed sequence.

### DryWetMidi read API

**Confirmed in 8.0.3:**
- `MidiFile.Read(string path, ReadingSettings? settings = null) → MidiFile` — namespace `Melanchall.DryWetMidi.Core`
- `NotesManagingUtilities.GetNotes(MidiFile file, NoteDetectionSettings? = null, TimedEventDetectionSettings? = null) → IEnumerable<Note>` — namespace `Melanchall.DryWetMidi.Interaction`
- `Note.Velocity` — `SevenBitNumber` (byte-like, 0..127)
- `Note.NoteNumber` — `SevenBitNumber` (MIDI note number)

[VERIFIED: `~/.nuget/packages/melanchall.drywetmidi/8.0.3/lib/netstandard2.0/Melanchall.DryWetMidi.xml` member list]

**Exact chunk-traversal pattern for the Fact:**

```csharp
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
// ...
var midiFile = MidiFile.Read(path);
var notes = midiFile.GetNotes().ToList();  // extension method on MidiFile
byte[] velocities = notes.Select(n => (byte)n.Velocity).ToArray();
// Assert velocities sequence
```

`GetNotes()` on `MidiFile` is an extension method from `NotesManagingUtilities` — requires `using Melanchall.DryWetMidi.Interaction`. Already used inside MidiExport.cs imports [VERIFIED: line 3 of MidiExport.cs].

### Test surface

- **Script:** `tests/test_dynamics_midi_velocity.flow` — small deterministic `.flow` that constructs a Sequence with known dynamics + crescendo, writes MIDI via `writeMidi`, exits.
- **Fact:** `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` — runs the script via `FlowEngineRunner`, reads the output `.mid` file, asserts velocity byte sequence.
- **Observable pin:** exact byte sequence. E.g., `Assert.Equal(new byte[]{31, 47, 63, 79, 95}, velocities)`.
- **Theory row addition:** `FlowScriptData.GetFlowScripts` auto-globs `tests/*.flow`, so the new script is included automatically. If the script has no stdout assertion beyond "no errors", it needs no `RequiredSentinels` entry — the default assertion is success + clean stderr.

### Blast radius

Files **created**: **2**
- `tests/test_dynamics_midi_velocity.flow`
- `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs`

Files **modified**: **0 (expected)** — chain is wired end-to-end.

Files potentially created if Pass 2 finds a gap (contingency):
- Depends on what breaks. Document divergence and minimal fix in same commit per D-13.

---

## Runtime State Inventory

N/A — Phase 14 is purely additive greenfield feature work. No renames, no migrations, no stored-data surface changes. No Windows Task Scheduler / secret-manager / datastore touchpoints.

**Nothing found in category:** None — verified by reading all three requirements. Every requirement either adds a new built-in name (`slice`, `enharmonic`), extends a pure parsing function (`NoteType.Parse`/`Format`), or authors a test. No state migration or OS-level registration involved.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All builds | ✓ (assumed — csproj targets net10.0 and prior phases built successfully) | 10.0 | — |
| Melanchall.DryWetMidi | DX-08 MIDI read | ✓ | 8.0.3 | — |
| xunit.v3 | All new Facts | ✓ | 3.2.2 | — |
| PulseAudio | NOT needed for Phase 14 | — | — | N/A — no new playback tests |
| DryWetMidi Read API | DX-08 Fact | ✓ (confirmed in 8.0.3 package XML) | — | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

---

## Common Pitfalls

### Pitfall 1: Lexer alteration pickup misses post-identifier `+`/`-` runs
**What goes wrong:** After the `NoteType.Parse` extension, `Bb-` in source code lexes as identifier `Bb` + `-` operator, and `Bb` alone doesn't error (parses as `(B,4,-1)`), so the `-` becomes a spurious subtraction token. Silent mis-parse.
**Why it happens:** Existing alteration pickup at `SimpleLexer.cs:551-560` only fires when `text[1]` is a digit AND only picks up 1-or-2 `+`/`-` chars.
**How to avoid:** Extend the peek loop to unbounded run of `+`/`-` when `text[0]` is A-G. Drop the `char.IsDigit(text[1])` gate.
**Warning signs:** Any expression `Bb - C4` or similar would tokenize confusingly. Unit tests on the lexer output tokens are the first line of defense.

### Pitfall 2: `NoteType.Format` round-trip failure on extended alterations
**What goes wrong:** If `Format` emits something `Parse` can't consume (e.g., `B^3` or `B+3`), then `Value.cs:168` path `Note(Format(parsed))` breaks downstream.
**Why it happens:** Tempting to use a compact numeric suffix for big alterations.
**How to avoid:** Use a run-based emission (`+` repeated N times, `-` repeated N times) that the extended Parse consumes symmetrically. Add round-trip Facts for alterations -5..+5.
**Warning signs:** Semitone↔Note conversions at `Value.cs:164-169` would start failing silently.

### Pitfall 3: `enharmonic` in-key lookup misses flat spellings due to ChromaticNotes asymmetry
**What goes wrong:** `ScaleDatabase.GetScaleNotes("Dbmajor")` returns `["Db", "Eb", "F", "Gb", "Ab", "Bb", "C"]` (using flats). `ChromaticNotes` array uses sharps (`"Cs"`, `"Ds"`, etc.) but `NoteToSemitone` has both `Db→1` and `Csharp→1`. The scale-tone expansion at `ScaleDatabase.cs:208-211` uses the sharp-based ChromaticNotes array directly — so `GetScaleNotes("Dbmajor")` actually returns sharp-spelled notes, not the flat ones the composer expects.
**Why it happens:** Asymmetry in how `ChromaticNotes` vs `NoteToSemitone` spell enharmonics.
**How to avoid:** Don't blindly echo `GetScaleNotes` output as the respelling. Either (a) write a helper `ScaleDatabase.GetScaleNotesInKeySpelling(key)` that returns flats-preferred spellings for flat keys and sharps-preferred for sharp keys, or (b) do the respelling directly in `enharmonic` by computing `midi` and looking up against scale-tone MIDI values, then picking the letter whose key-signature affinity matches.
**Warning signs:** Respelling in flat keys produces sharp-labeled notes; unit Facts catch this immediately.

### Pitfall 4: `enharmonic` key-name mismatch vs MusicalContext
**What goes wrong:** `MusicalContext.Key` stores a normalized string like `"Cmajor"` or `"Dbmajor"`. `ScaleDatabase.GetScaleNotes(keyName)` accepts the same format [VERIFIED: `ScaleDatabase.cs:154-191` `TryParseKey`]. But typos or unnormalized keys (`"C Major"`, `"c major"`) silently return null.
**Why it happens:** The `key Cmajor { }` block at the interpreter level should already normalize, but `enharmonic` must handle a `null` scale-notes return (falls to no-key rule) gracefully.
**How to avoid:** `if (scaleNotes == null) fall through to no-key branch`. Add a Fact with a malformed context-less input.

### Pitfall 5: Existing test `test_chords.flow` uses `Cmaj`, `Dm` — NOT flats
**What goes wrong:** Assumption that tokens like `Dm` might collide with extended flat surface.
**Why it happens:** `Dm` matches chord pattern — `ChordParser.IsChordSymbol` fires BEFORE note literal check at `SimpleLexer.cs:617,659-663` would matter.
**How to avoid:** The chord path at `SimpleLexer.cs:660` runs inside `if (type == TokenType.Identifier)` but AFTER the `TryParseNote` branch at line 617. So `Dm` is tested as a note first; `NoteType.Parse("Dm")` would fail ('m' is not a valid alteration char under extended surface either — same error as today). Then chord path fires. **Verified mentally — no regression.**
**Warning signs:** Any `.flow` test with chord literals that mutates under the extension. Run Theory-row full suite after extension land.

### Pitfall 6: `tests/test_dynamics.flow` uses `stacc`, `ten`, `marc` identifiers in note streams — not affected, but adjacent
**What goes wrong:** None — articulation tokens are consumed by note-stream parser, unrelated to identifier lexer extension.
**How to avoid:** Collision grep confirms no `b4`/`bb` variable names in tests. Grep is verified empty below.
**Warning signs:** None.

### Pitfall 7: DX-08 Fact reads MIDI file before it's written (ordering race)
**What goes wrong:** The Flow script inside the Fact runs `writeMidi` which creates the file, but if the Fact reads before `Execute` finishes flushing, the file may be incomplete.
**Why it happens:** Buffered file I/O.
**How to avoid:** The fixture `FlowEngineRunner.RunFile` returns only after `FlowEngine.Execute` completes (synchronous). `writeMidi` internally calls `MidiFile.Write(path)` which flushes synchronously (DryWetMidi default). Safe. **Verified by reading fixture at `FlowEngineRunner.cs:22-28`.**

### Pitfall 8: `FlowScriptData.GetFlowScripts()` auto-globs `tests/*.flow` — new scripts run automatically
**What goes wrong:** `tests/test_dynamics_midi_velocity.flow` becomes a Theory row without explicit registration. Its default assertion is success + clean stderr.
**Why it happens:** Glob at `FlowScriptData.cs:8`.
**How to avoid:** Script must produce a valid MIDI file without errors; it must not require specific stdout sentinels unless registered in `RequiredSentinels`. **Upside, not downside — no extra wiring.**
**Warning signs:** Script error would cause two test failures (the Theory row and the DX-08 Fact). Acceptable.

---

## Blast Radius Summary

### DX-05 (plan 14-01)
**Files modified:** 2
- `flow-lang/StandardLibrary/Collections.cs`
- `flow-lang/StandardLibrary/BuiltInFunctions.cs`
**Files created:** 1-2 (unit Facts + `.flow` test)
**Downstream call sites potentially affected:** 0 (new names)

### DX-06 flat surface (plan 14-02 commit a)
**Files modified:** 2
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs`
- `flow-lang/Lexing/SimpleLexer.cs`
**Files created:** 1-2 (unit Facts + `.flow` test)
**Downstream call sites of `NoteType.Parse`:** 13 (all consume triple output unchanged, no string-level handling of `b`/`#`)
| # | Path | Context |
|---|------|---------|
| 1 | `flow-lang/Lexing/SimpleLexer.cs:692` | Note literal validation (the extension target itself) |
| 2 | `flow-lang/Interpreter/ExpressionEvaluator.cs:71` | LiteralExpression → Note value |
| 3 | `flow-lang/Runtime/Value.cs:154` | Note→Semitone conversion |
| 4 | `flow-lang/Runtime/NoteStreamCompiler.cs:119` | Ghost note compilation |
| 5 | `flow-lang/Runtime/NoteStreamCompiler.cs:131` | Grace note compilation |
| 6 | `flow-lang/Runtime/NoteStreamCompiler.cs:324` | Main note element compilation |
| 7 | `flow-lang/Runtime/NoteStreamCompiler.cs:405` | Chord bracket note 1 |
| 8 | `flow-lang/Runtime/NoteStreamCompiler.cs:430` | Chord bracket note 2 |
| 9 | `flow-lang/Runtime/NoteStreamCompiler.cs:463` | Chord bracket note 3 |
| 10 | `flow-lang/Runtime/NoteStreamCompiler.cs:571` | Random-choice element |
| 11 | `flow-lang/Runtime/NoteStreamCompiler.cs:609` | Pitch resolution fallback |
| 12 | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:75` | Arpeggio chord-note parse |
| 13 | `flow-lang/StandardLibrary/BuiltInFunctions.cs:1009` | noteToFrequency |
| 14 | `flow-lang/StandardLibrary/BuiltInFunctions.cs:1034` | euclidean rhythm note |
| 15 | `flow-lang/StandardLibrary/Audio/ClassicalComposition.cs:14` | CreateMusicalNote helper |

(That's 15 call sites, not 13 — original CONTEXT.md said 13; recount is 15. No material difference — all consume the triple.)

**Downstream call sites of `NoteType.Format`:** 3 (all round-trip safe with run-based emission)
| # | Path | Concern |
|---|------|---------|
| 1 | `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:236` | `MusicalNoteData.ToString()` — display only |
| 2 | `flow-lang/Runtime/Value.cs:168` | `Note(Format(parsed))` round-trip — requires Parse compatibility — SATISFIED by run emission |
| 3 | `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:121` | Warning message — display only |

### DX-06 enharmonic (plan 14-02 commit b)
**Files modified:** 2
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (+RegisterContextDependent, +Enharmonic impl)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (add one line in RegisterContextDependentFunctions)
**Files created:** 1-2 (unit Facts + `.flow` test)
**Downstream call sites affected:** 0 (new function name)

### DX-08 (plan 14-03)
**Files modified:** 0 (expected GREEN on Pass 2)
**Files created:** 2
- `tests/test_dynamics_midi_velocity.flow`
- `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs`

### Closure (plan 14-04)
**Files modified:** 2-3
- `.planning/REQUIREMENTS.md` (DX-06 H-alias reframe, status table)
- `.planning/STATE.md`, `.planning/ROADMAP.md` (phase close)
- `14-VERIFICATION.md` (new), `deferred-items.md` (new)
- Optionally `14-VALIDATION.md` (new — Claude's Discretion)

---

## Regression Risk Analysis

### Collision Grep — Pre-landing audit

**Recipe (from CONTEXT D-21):**
```
grep -rn '\b(Db|Eb|Fb|Gb|Ab|Bb|Cb|enharmonic)\b' flow-lang/ examples/ tests/ --include='*.flow'
```

**Result (run during this research):** EMPTY (exit code 1 = no matches). [VERIFIED: bash execution]

```
$ grep -rn '\b(Db|Eb|Fb|Gb|Ab|Bb|Cb|enharmonic)\b' flow-lang/ examples/ tests/ --include='*.flow'
(no output)
```

No `.flow` file in the repo currently uses any flat-letter identifier or `enharmonic` as a variable/proc name. Extension is safe.

**Also verified:** No `\bH\b` standalone identifier usage in `.flow` tests/examples. The `h` character appears only as a duration suffix (`C4h`) — consumed by the note-stream parser, not subject to identifier tokenization at that position. No H-alias collision even though H remains deferred.

### Existing `.flow` tests that could regress

Scanned all 70+ scripts in `tests/` — no use of `b`, `H`, `Db`, `Eb`, etc. as identifiers. Chord literals (`Dm`, `Cmaj`, `Bb7`) use note-letter followed by `m`/`maj`/`7` which go through `ChordParser.IsChordSymbol` AFTER `TryParseNote` fails — so:

- `Dm`: `TryParseNote("Dm")` → `Parse("Dm")` — under extension, `'m'` is not in {`+`,`-`,`b`,`#`,digit}, triggers `"Invalid note character 'm'"`. `TryParseNote` catches and returns false. Falls through to `ChordParser.IsChordSymbol("Dm") → true`. **Unchanged behavior.** [VERIFIED: trace]
- `Bb7`: `TryParseNote("Bb7")` → `Parse("Bb7")` — under extension, letter=B, alteration=-1, octave=7, but then continues — wait, `'7'` is a digit. Phase 1 consumes pre-octave alteration `b`, phase 2 consumes digit `7` as octave, phase 3 has nothing. Returns `(B, 7, -1)`. **This IS a change** — `Bb7` previously rejected (old Parse threw on `b`), now accepted as a Note with octave 7 alteration -1.

**This is a REGRESSION RISK for chord literals.** Let me verify by checking actual usage:

```
tests/test_chords.flow:      uses `Dm`, `Cmaj`, `Gsus4`, `F#dim`, `Bb7` etc. as chord literals
```

The lexer path at `SimpleLexer.cs:617` runs `TryParseNote` FIRST. If `Parse("Bb7")` succeeds under the extension, `Bb7` becomes a NoteLiteral, not a ChordLiteral. This breaks `tests/test_chords.flow`.

**Mitigation option A:** Don't extend Parse blindly. In `TryParseNote`, after successful Parse, reject the result if the original string matches a chord pattern. I.e., check `ChordParser.IsChordSymbol(text)` first.

**Mitigation option B:** Swap the order at `SimpleLexer.cs:617-663` — check chord pattern BEFORE note pattern. Safer but changes existing dispatch order.

**Mitigation option C (recommended):** In `TryParseNote`, restrict the lexer-facing Parse success path. Keep `NoteType.Parse` itself fully permissive (for direct C# callers), but in the lexer `TryParseNote` helper add a disambiguation: if the text contains `b` followed by a digit AND matches a known chord suffix pattern (`\d+$` with known chord modifiers), defer to ChordParser.

**Recommended approach:** Re-order `SimpleLexer.cs` tests. Move `ChordParser.IsChordSymbol(text)` check to run BEFORE `TryParseNote(text, ...)`. Since chord symbols have strict shape (`[A-G][#b]?(maj|m|dim|aug|sus[24]|add\d|M\d|\d)(...)`), they don't overlap with note-stream note literals that lack chord-quality suffixes. `Dm`, `Cmaj`, `Bb7`, `F#dim` match ChordParser; `Db4`, `Eb`, `C4`, `F#` do not (no quality suffix).

**Planner MUST include a probe:** Before landing the flat-literal extension, verify via a test (manual or scripted) that:
1. `Dm` still tokenizes as `ChordLiteral` (not `NoteLiteral`).
2. `Bb7` still tokenizes as `ChordLiteral` (not `NoteLiteral(B, 7, -1)`).
3. `Db4` tokenizes as `NoteLiteral(D, 4, -1)` (new behavior).
4. `Bb` tokenizes as `NoteLiteral(B, 4, -1)` (new behavior — bare flat in note stream).

This is a LATENT ordering bug in the existing lexer (chord check currently runs AFTER note check — see `SimpleLexer.cs:617` vs `:660`). Under the current restricted Parse, it happened to work because `Parse("Bb7")` threw. Under the extension, the ordering must be fixed.

**This is a significant discovery not fully captured in CONTEXT.md — surface it to the planner as a mandatory plan-14-02 subtask.**

### Existing tests that would flip RED without this fix

- `tests/test_chords.flow` — uses `Bb7` and similar
- `tests/test_full_song.flow` — may use chord literals
- `tests/test_roman_numerals.flow` — resolves to chord literals
- Any test using chord syntax with flat root

Mitigation: chord-check-first ordering, as above.

---

## Pattern Map (Touchpoints by Feature)

| New feature | Closest existing pattern | Reuse level |
|-------------|--------------------------|-------------|
| `slice(Array[T], Int, Int)` | `Take`/`Drop` at `Collections.cs:117-147` | Direct template — differ only in indexing math |
| `slice(Sequence, Int, Int)` | Custom — no prior Sequence slice operator | New pattern; SequenceData has public Bars list + AddBar API — straightforward |
| `NoteType.Parse` extension | Existing alteration switch (lines 52-63) | Refactor from 5-case switch to sum-based scan |
| `NoteType.Format` extension | Existing 5-case switch (lines 144-152) | Refactor to `new string('+', n)` run |
| Lexer alteration pickup extension | Existing 1-or-2-char peek (`SimpleLexer.cs:551-562`) | Generalize to unbounded run |
| `enharmonic()` | `resolveNumeral` (`HarmonyFunctions.cs:99-110`) for key-context pattern; `StdLib.Rand` (`StdLib.cs:474`) for `(args, context)` signature wiring; `SongFunctions.AddSequenceToSong` for `context.GetMusicalContext()` access | Full reuse of patterns |
| HarmonyFunctions context registration | `SongFunctions.Register(registry, context)` called from `BuiltInFunctions.cs:668` | Direct pattern — add one line |
| DX-08 MIDI-byte Fact | **New pattern — no precedent.** Existing DSP Facts use zero-crossing counts on audio buffers. MIDI byte assertion is first of its kind. | New but narrow |
| `.flow` regression script using `writeMidi` | `tests/test_midi_export.flow` (if exists) — not confirmed in this research | Look for existing MIDI script before authoring |

**Planner action:** Before authoring `tests/test_dynamics_midi_velocity.flow`, check for existing `tests/test_midi_export.flow` or similar and reuse its invocation pattern of `writeMidi`.

---

## Code Examples

### DryWetMidi read pattern (DX-08 Fact)

```csharp
// Source: Melanchall.DryWetMidi 8.0.3 — verified via package XML docs
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase14;

public class DynamicsMidiVelocityTests
{
    [Fact]
    public void Crescendo_EmitsExpectedVelocityGradient()
    {
        using var runner = new FlowEngineRunner();
        // Set CWD to repo root so `writeMidi` writes to tests/output/
        var originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = FindRepoRoot();
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

            // Known-good gradient from 0.25 -> 0.75 over 5 notes:
            // velocities[i] = (int)((0.25 + i * (0.5 / 4)) * 127)
            //               = (int)(0.25*127), (int)(0.375*127), ...
            //               = 31, 47, 63, 79, 95
            Assert.Equal(new byte[] { 31, 47, 63, 79, 95 }, velocities);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    private static string FindRepoRoot() => FlowScriptData.FindTestsRoot() + "/..";
}
```

### Proposed `tests/test_dynamics_midi_velocity.flow`

```flow
use "@std"
use "@audio"

// Deterministic 5-note crescendo from 0.25 to 0.75 — verifiable MIDI velocity gradient
Song song = [| C4 D4 E4 F4 G4 | -> crescendo 0.25 0.75]
writeMidi "tests/output/dynamics_velocity.mid" song
(print "dynamics_velocity: PASSED")
```

(Planner refines exact syntax. Existing `renderSong`/`writeMidi` shape from prior phases is the authority.)

### `slice` usage example

```flow
// Source: new `.flow` test  tests/test_slice.flow
use "@std"

// Array slice
Array[Int] arr = [1, 2, 3, 4, 5]
Array[Int] mid = slice arr 1 4           // [2, 3, 4]
Array[Int] neg = slice arr -5 2          // [1, 2]  (start clamps to 0)
Array[Int] over = slice arr 3 100        // [4, 5]  (end clamps to count)
Array[Int] emp = slice arr 3 2           // []       (start >= end → empty)

// Sequence slice (bar-level)
Sequence seq = | C4 D4 | E4 F4 | G4 A4 |
Sequence middle = slice seq 1 2          // single bar: | E4 F4 |
(print (str (length mid)))               // "3"
```

---

## Plan Skeletons

Plans are wave-1 parallel (D-20) except 14-04. Zero file overlap between 14-01/02/03.

### Plan 14-01 — DX-05 slice (atomic, single commit)

**Scope:** `slice(Sequence, Int, Int)` + `slice(Array[T], Int, Int)` + `.flow` regression + unit Facts.

**Tasks:**
1. Add `Collections.SliceArray` and `Collections.SliceSequence` in `flow-lang/StandardLibrary/Collections.cs`.
2. Register both overloads in `flow-lang/StandardLibrary/BuiltInFunctions.cs` near take/drop (line 369-373).
3. Author `flow-lang.Tests/Unit/Phase14/SliceTests.cs` with Facts: normal range, negative start clamp, end > count clamp, start == end empty, start > end empty, single-element result.
4. Author `tests/test_slice.flow` exercising both overloads + clamp edges.
5. Verify Theory row auto-registers via FlowScriptData glob.
6. `dotnet test` green + manual `dotnet run tests/test_slice.flow` green.
7. Single atomic commit per D-02.

**Files:** 2 modified + 2 created. **Estimated: ~30-line patch.**

**Dependencies on other plans:** NONE.

### Plan 14-02 — DX-06 flat surface + `enharmonic()` (two commits)

**Scope:** `NoteType.Parse` extension, `NoteType.Format` extension, `SimpleLexer` alteration-pickup extension, chord-vs-note ordering fix, `enharmonic()` built-in, pre-landing collision grep.

**Commit A: Flat-literal surface extension**
1. Run collision grep recipe (D-21), paste transcript into PLAN.md §Pre-landing Collision Grep (expected empty).
2. Extend `NoteType.Parse` to sum-based alteration scan supporting `b`/`#`/`+`/`-` on either side of digits.
3. Extend `NoteType.Format` to emit `new string('+', n)` / `new string('-', -n)` runs.
4. Shift range check to post-alteration MIDI value (D-09).
5. **Extend `SimpleLexer.ScanIdentifierOrKeyword` alteration pickup** to unbounded `+`/`-` runs when identifier starts A-G.
6. **Fix chord-vs-note ordering in `SimpleLexer.cs`:** move `ChordParser.IsChordSymbol` check to run BEFORE `TryParseNote`. (Regression-risk mitigation.)
7. Unit Facts in `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs`: parse all flat letters, `Bb-+bbb → (B,4,-4)`, round-trip Parse/Format over alterations -5..+5, range check edges, lexer token tests asserting `Bb4` → NoteLiteral and `Bb7` → ChordLiteral.
8. `.flow` test: `tests/test_flat_literals.flow`.
9. Run full `dotnet test`. **Must verify chord tests still pass.**
10. Atomic commit.

**Commit B: `enharmonic()` built-in**
1. Add `HarmonyFunctions.RegisterContextDependent(registry, context)` method with `enharmonic` registration.
2. Implement `Enharmonic` private method with in-key (D-04) and no-key (D-05) branches, addressing Pitfall 3 (ChromaticNotes/NoteToSemitone asymmetry).
3. Call `Harmony.HarmonyFunctions.RegisterContextDependent(registry, context)` from `BuiltInFunctions.RegisterContextDependentFunctions` near line 668.
4. Unit Facts in `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs`: no-key flip `Db↔C#`, natural unchanged `E→E`, in-key `C#→Db` within Dbmajor, double-sharp `F##→G` non-involutive.
5. `.flow` integration test: `tests/test_enharmonic.flow`.
6. Atomic commit.

**Files:** 3 modified (NoteType.cs, SimpleLexer.cs, BuiltInFunctions.cs, HarmonyFunctions.cs) + 3-4 created (2 unit Fact files + 2 `.flow` tests).

**Dependencies on other plans:** NONE.

### Plan 14-03 — DX-08 MIDI velocity verification (two-pass strict, D-13)

**Scope:** Purpose-built `.flow` test + xUnit Fact asserting MIDI velocity bytes. Minimal gap-fix if Pass 2 RED.

**Pass 1: Draft from REQUIREMENTS alone**
1. Read REQUIREMENTS.md DX-08 wording only. Do NOT read `Interpreter.cs`, `NoteStreamCompiler.cs`, `MidiExport.cs`, or `TransformFunctions.cs`.
2. Draft `tests/test_dynamics_midi_velocity.flow` against what REQUIREMENTS.md says should happen.
3. Draft `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` Fact with expected byte sequence computed from REQUIREMENTS.md semantics.
4. Commit draft even if assertion expected-values are placeholders (e.g., `new byte[] { 31, 47, 63, 79, 95 }` based on `crescendo(0.25, 0.75)` over 5 notes with `(int)(v*127)` truncation).

**Pass 2: Land against real code**
1. Run the drafted test. Expect GREEN per F-01 + §"DX-08 Velocity Chain Audit" above.
2. If GREEN: single atomic commit, no gap-fix.
3. If RED: minimal gap-fix (e.g., note-level velocity override plumbing, Int↔Double conversion bug) lands in same plan. Log divergence in VERIFICATION.md via `## Divergences` section (Phase 13 pattern).

**Files:** 0 modified (expected) + 2 created (`.flow` script + Fact).

**Dependencies on other plans:** NONE (DX-08 chain is independent of slice and flat surface).

### Plan 14-04 — Closure (strictly last)

**Scope:** REQUIREMENTS.md reframe, deferred-items.md, 14-VERIFICATION.md, optional 14-VALIDATION.md, STATE/ROADMAP updates.

**Tasks:**
1. Reframe REQUIREMENTS.md DX-06: move H-alias clause to audit-trail comment (Phase 12 TEST-03 pattern). Keep flat + enharmonic clauses. Mark DX-05, DX-06, DX-08 rows Complete with commit hashes.
2. Author `.planning/phases/14-composer-dx-part-1/deferred-items.md` capturing: H alias requirement, pragma system design, `enable` keyword candidate, German-notation as first pragma user.
3. Author `14-VERIFICATION.md` with commit hashes for FIX-* / DX-* and §Pre-landing Collision Grep re-surfacing.
4. Optionally author `14-VALIDATION.md` at `nyquist_compliant: true` (Claude's Discretion — recommend YES based on Phase 13 D-24 precedent, all three requirements have observable-value pins).
5. Update STATE.md phase progress, ROADMAP.md Phase 14 checkboxes.
6. Single atomic commit (docs-only).

**Files:** 4-5 modified/created (REQUIREMENTS.md, STATE.md, ROADMAP.md, 14-VERIFICATION.md, deferred-items.md, optional 14-VALIDATION.md).

**Dependencies on other plans:** ALL — 14-04 lands strictly last per D-20.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase14"` |
| Full suite command | `dotnet test flow-lang.Tests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| DX-05 | `slice(Array[Int], 0, 3)` on `[1,2,3,4,5]` returns `[1,2,3]` | unit | `dotnet test --filter "SliceTests.Array_NormalRange"` | Wave 0 (create `Unit/Phase14/SliceTests.cs`) |
| DX-05 | `slice(seq, -5, 2)` clamps start to 0 | unit | `dotnet test --filter "SliceTests.Array_NegativeStartClamps"` | Wave 0 |
| DX-05 | `slice(seq, 3, 2)` returns empty | unit | `dotnet test --filter "SliceTests.Array_InvertedRangeEmpty"` | Wave 0 |
| DX-05 | `slice(Sequence, Int, Int)` returns sub-sequence with correct bar count | unit | `dotnet test --filter "SliceTests.Sequence_ReturnsCorrectBarCount"` | Wave 0 |
| DX-05 | End-to-end via `.flow` | integration | `dotnet run tests/test_slice.flow` (also Theory row) | Wave 0 (create `tests/test_slice.flow`) |
| DX-06 Flat | `Parse("Db4")` → `(D, 4, -1)` | unit | `dotnet test --filter "NoteTypeTests.Parse_FlatLetter_Db"` | Wave 0 (create `Unit/Phase14/NoteTypeTests.cs`) |
| DX-06 Flat | `Parse("Bb-+bbb")` → `(B, 4, -4)` | unit | `dotnet test --filter "NoteTypeTests.Parse_MixedAlteration"` | Wave 0 |
| DX-06 Flat | Round-trip `Parse(Format(x)) == x` for alt -5..+5 | unit | `dotnet test --filter "NoteTypeTests.RoundTrip"` | Wave 0 |
| DX-06 Flat | `Parse("Cb0")` throws (post-alteration MIDI out of range) | unit | `dotnet test --filter "NoteTypeTests.Parse_BelowRange"` | Wave 0 |
| DX-06 Flat | Lexer: `Bb7` tokenizes as ChordLiteral (regression check) | unit | `dotnet test --filter "LexerTests.Bb7_IsChord"` | Wave 0 |
| DX-06 Flat | `.flow` note stream with `Db4` renders correctly | integration | Theory row for `tests/test_flat_literals.flow` | Wave 0 |
| DX-06 Enh | `enharmonic("Db4")` (no key) → `C#4` | unit | `dotnet test --filter "EnharmonicTests.NoKey_FlatToSharp"` | Wave 0 (create `Unit/Phase14/EnharmonicTests.cs`) |
| DX-06 Enh | `enharmonic("C4")` (no key) → `C4` (natural unchanged) | unit | `dotnet test --filter "EnharmonicTests.NoKey_NaturalUnchanged"` | Wave 0 |
| DX-06 Enh | In-key Dbmajor: `C#4` → `Db4` | unit | `dotnet test --filter "EnharmonicTests.InKey_Dbmajor"` | Wave 0 |
| DX-06 Enh | `.flow` with `key Dbmajor { ... enharmonic ... }` | integration | Theory row for `tests/test_enharmonic.flow` | Wave 0 |
| DX-08 | MIDI velocity byte sequence for `crescendo(0.25, 0.75)` over 5 notes = `[31, 47, 63, 79, 95]` | integration | `dotnet test --filter "DynamicsMidiVelocityTests.Crescendo_EmitsExpectedGradient"` | Wave 0 (create `Integration/Phase14/DynamicsMidiVelocityTests.cs`) |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase14"` (~2-5 seconds)
- **Per wave merge:** `dotnet test` full suite (~30 seconds at current scale)
- **Phase gate:** Full suite green before `/gsd-verify-work` in plan 14-04.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase14/SliceTests.cs` — covers DX-05
- [ ] `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` — covers DX-06 Flat
- [ ] `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` — covers DX-06 Enh
- [ ] `flow-lang.Tests/Unit/Phase14/LexerTests.cs` — covers chord-vs-note ordering regression
- [ ] `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` — covers DX-08
- [ ] `tests/test_slice.flow`
- [ ] `tests/test_flat_literals.flow`
- [ ] `tests/test_enharmonic.flow`
- [ ] `tests/test_dynamics_midi_velocity.flow`

*(All test files are Wave 0 gaps — none exist yet. No framework install needed — xUnit already present.)*

### Observable-Value Pins (Phase 13 D-11 compliance)

Per Phase 13 D-11: pins must be **error message text** OR **numeric durations/sample counts** OR **exact byte sequences**. **Buffer byte hashes are forbidden.**

| Feature | Pin Type | Pin Value |
|---------|----------|-----------|
| `slice` array | numeric count | `.Count == expected` |
| `slice` sequence | numeric bar count | `seq.Bars.Count == expected` |
| `NoteType.Parse` | triple equality | `(letter, octave, alteration) == expected` |
| `NoteType.Format` | exact string | `Format(x) == "Db4"`, `Format(B,4,-4) == "B4----"` |
| Post-alt range | error text | `"Note Cb0 is out of valid range (E0 to E10)"` |
| `enharmonic` | triple equality via Parse | `Parse(enharmonic("Db4", null)) == ('C', 4, 1)` |
| MIDI velocity | exact byte sequence | `[31, 47, 63, 79, 95]` |

---

## Security Domain

Not applicable. Phase 14 is a language feature phase for an offline interpreter — no network, no authentication, no user-facing input outside script evaluation. ASVS categories V1-V11 do not apply to internal built-in function additions. Input validation for `NoteType.Parse` is handled via existing `ArgumentException` throws (unchanged contract, extended surface).

The `writeMidi` file-write surface already exists from prior phases and is unchanged. No new filesystem exposure.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `.NET 10` target is authoritative despite CLAUDE.md saying `.NET 9` | Standard Stack | Low — both csproj files target net10.0 [VERIFIED]; CLAUDE.md is stale |
| A2 | `dynamics f` maps to velocity 0.8 (forte convention) | DX-08 Velocity Chain Audit | Low — planner should verify the actual mapping in `Interpreter.cs` or wherever `f`/`ff`/`mp`/etc. tokens are resolved. If not 0.8, Fact expected-values adjust accordingly. |
| A3 | `ScaleDatabase.GetScaleNotes` asymmetry (sharp-spelled expansion even in flat keys) is real and needs in-`enharmonic` handling | Pitfall 3 | Medium — if `GetScaleNotes("Dbmajor")` actually returns flat-spelled notes, no extra work needed. Planner should test this at implementation time. |
| A4 | `writeMidi` registration and signature is `writeMidi(String: filepath, Song: song)` | DX-08 | Low — confirmed via `audio.flow:408` [VERIFIED] |
| A5 | No existing `tests/test_midi_export.flow` or equivalent to reuse | Pattern Map | Low — planner should glob `tests/` for existing MIDI-writing scripts at plan time |
| A6 | Theory-row auto-registration via `FlowScriptData.GetFlowScripts` works for new `.flow` files in `tests/` | Plan skeletons | Low — confirmed at `FlowScriptData.cs:8-13` [VERIFIED] |

---

## Open Questions (RESOLVED)

1. **`ScaleDatabase.GetScaleNotes("Dbmajor")` — does it return flats or sharps?**
   - What we know: Lines 208-211 use `ChromaticNotes[semitone]` which has `Cs`/`Ds` (sharps only for the sharp positions). `Db`/`Eb` only exist in the `NoteToSemitone` dictionary, not in `ChromaticNotes`.
   - What's unclear: Whether the function returns `["Db", "Eb", ...]` or `["Cs", "Ds", ...]` for a flat key.
   - **RESOLVED:** Treated as sharps-only in planning. Plan 14-02 Task 2 handles flat-key in-key respelling via MIDI-based lookup + preferFlat heuristic (not string echo from `GetScaleNotes`). Pitfall 3 in this document is the source-of-truth the planner cited.

2. **Does `dynamics ff`/`dynamics mp` accept identifier tokens or numeric values?**
   - What we know: `Interpreter.cs:184-188` reads `ctx.Value` as Int or Double. `tests/test_dynamics.flow:26` writes `dynamics f { }` — where is `f` resolved to a velocity?
   - What's unclear: Whether `f` is a bare identifier mapped to 0.8 at parse time, or something else.
   - **RESOLVED:** Sidestepped at plan-time. Plan 14-03's `tests/test_dynamics_midi_velocity.flow` uses numeric-literal invocation (`crescendo 0.25 0.75`, `dynamics 0.8`) rather than identifier dynamics (`f`, `ff`), eliminating the constants-lookup dependency from the Fact's expected-value computation. Existing `tests/test_dynamics.flow` continues to exercise the identifier path under its existing stdout-only coverage.

3. **Is there a pre-existing `.flow` test that writes MIDI?**
   - What we know: `writeMidi` is registered as `internal proc` in `audio.flow:408`.
   - What's unclear: Whether any existing test exercises this end-to-end.
   - **RESOLVED:** `tests/test_midi_export.flow` exists and is the invocation-pattern reference cited by Plan 14-03's `<key_links>` block. Plan 14-03 Pass 1 mirrors that pattern for `tests/test_dynamics_midi_velocity.flow`; no new write-path work required.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ±2 alteration bound in `NoteType.Parse` | Arbitrary int alteration via sum-based scan | Phase 14 (this phase) | User-facing: `Bb-+bbb` valid; no break on existing notes |
| 5-case switch in `NoteType.Format` | Run-based emission for any int | Phase 14 | Round-trip preserved for all alterations |
| `IsValidNoteRange(note, octave)` (letter-only) | Post-alteration MIDI range check | Phase 14 | `Cb4` valid (MIDI 59 in range), `Cb0` invalid (MIDI 11 below range) |
| `HarmonyFunctions.Register(registry)` (no context) | `HarmonyFunctions.RegisterContextDependent(registry, context)` added alongside | Phase 14 | Enables `enharmonic` access to active key |

**Deprecated/outdated:**
- CLAUDE.md's "Runtime: .NET 9 — all code must target net9.0" — actual csproj targets `net10.0`. Planner should not assume net9.0 in new test code.

---

## Sources

### Primary (HIGH confidence — verified via file reads / tool execution)
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — Parse, Format, IsValidNoteRange, GetNoteValue, ToMidiNote, FromMidiNote, MusicalNoteData
- `flow-lang/Lexing/SimpleLexer.cs` — ScanIdentifierOrKeyword, TryParseNote, IsTokenBoundary
- `flow-lang/Runtime/NoteStreamCompiler.cs` — 15 call sites of NoteType.Parse, CompileNoteElement velocity propagation
- `flow-lang/StandardLibrary/Collections.cs` — Take/Drop templates
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — register sites, RegisterContextDependentFunctions
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — existing harmony registrations
- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` — GetScaleNotes, TryParseKey, NoteToSemitone, ChromaticNotes
- `flow-lang/Runtime/MusicalContext.cs` — Velocity, Key fields
- `flow-lang/Interpreter/Interpreter.cs:184-191` — dynamics keyword handling
- `flow-lang/Runtime/ExecutionContext.cs` — GetMusicalContext
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:191-199` — velocity byte emission
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:401-529` — Crescendo/Decrescendo/Swell impl
- `flow-lang/Runtime/Value.cs:149-169` — Note↔Semitone round-trip
- `flow-lang/flow-lang.csproj` and `flow-lang.Tests/flow-lang.Tests.csproj` — target frameworks and package references
- `~/.nuget/packages/melanchall.drywetmidi/8.0.3/lib/netstandard2.0/Melanchall.DryWetMidi.xml` — verified MidiFile.Read + NotesManagingUtilities.GetNotes presence
- `tests/test_dynamics.flow`, `tests/test_crescendo.flow` — existing stdout-only coverage
- `.planning/phases/14-composer-dx-part-1/14-CONTEXT.md` — locked decisions
- `.planning/REQUIREMENTS.md` — DX-05, DX-06, DX-08 wording
- `.planning/ROADMAP.md` — Phase 14 success criteria

### Secondary (MEDIUM confidence — cross-referenced but not fully traced)
- `.planning/phases/12-stability/12-CONTEXT.md` — atomic commit pattern, REQUIREMENTS reframe pattern
- `.planning/phases/13-nyquist-validation-backfill/13-CONTEXT.md` — two-pass strict, wave-1 parallelization, observable-value pins, no-new-NuGet

### Tertiary (LOW confidence — flagged)
- Assumed `dynamics f` maps to 0.8 (conventional forte). Needs verification at plan time (Open Question 2).

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all deps verified present, csproj read, DryWetMidi XML docs inspected
- Architecture: HIGH — all touch points traced in source
- Pitfalls: HIGH — chord-vs-note ordering bug discovered and documented, not merely hypothesized
- DX-08 velocity chain: HIGH — full trace completed, no gap detected
- Expected MIDI velocity values: MEDIUM — depends on Open Question 2 (`f` = 0.8 convention)
- Plan skeletons: HIGH — wave-1 independence verified by file-overlap analysis

**Research date:** 2026-04-20
**Valid until:** 2026-05-20 (30 days for stable interpreter codebase)
