# Phase 20: Cheap DEFER Closures + Multi-letter Enharmonic Edges — Research

**Researched:** 2026-04-26
**Domain:** Three independent low-blast-radius v1.2 closures — `range` stdlib (DEFER-01), multi-letter enharmonic edges (DEFER-04), slice negative-from-end indexing (DEFER-05)
**Confidence:** HIGH — every load-bearing claim grounded in directly-read source files at HEAD (`e2cdbe5` + closure docs); CONTEXT.md does not yet exist (this RESEARCH precedes phase discussion), so all decisions are flagged as Claude's discretion or quoted from REQUIREMENTS.md.

---

## Summary

Phase 20 is the cheapest phase in v1.3 by blast-radius — three orthogonal closures across three independent files (`Collections.cs`, `HarmonyFunctions.cs`, `BuiltInFunctions.cs` registration) with stdlib `.flow` proc declarations in `collections.flow` + `std.flow`. Total LOC delta: ~120 production + ~150 tests. All three items have been pre-mapped in `.planning/research/ARCHITECTURE.md` §7 (Feature 3, 5, 6 — each tagged LBR). Plan 12-06's `deferred-items.md` already drafted DEFER-01's three-step fix.

The single non-trivial design call is **DEFER-04's algorithm** — Phase 14 explicitly skipped multi-letter edges and pinned 4 xUnit Facts asserting naturals-unchanged (`NoKey_NaturalUnchanged_C4/E4/B4/F4`). DEFER-04 inverts those expectations. The new algorithm must (a) decide which direction E/F/B/C should respell to (E→Fb up vs E→D## down — LOCKED to Fb per REQUIREMENTS.md acceptance), (b) handle octave bumps at B↔C boundary (B4 → Cb5 *crosses* octave; C4 → B#3 *drops* octave), (c) preserve round-trip pitch equivalence across every chromatic note.

Plan structure recommendation: **3 atomic plans**, one per DEFER, plus one closure plan — **NOT** a single bundle. Rationale: each DEFER touches a distinct file (Collections / Harmony / Collections respectively for DEFER-01/04/05), each has independent acceptance criteria, and DEFER-05 is a behavioral change that warrants its own bisectable commit. Bundling into one plan complicates rollback if one item regresses. (Alternative — one bundled plan — is acceptable per Phase 12 atomic-commit precedent if user prefers fewer commits; flagged as Claude's discretion.)

**Primary recommendation:** Land DEFER-01 first (zero-risk pure addition), then DEFER-04 (Phase 14 Fact migration required), then DEFER-05 (existing test_slice.flow + SliceTests.cs Facts continue to pass for the cases they pin; new Facts pin negative-from-end). Each plan ships as one atomic commit per Plan 12-02 / Plan 18-01 precedent (RED + GREEN bundled inside the same commit, bisectable but HEAD never broken).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

**CONTEXT.md does not exist yet for Phase 20.** Phase 20 was planned via `/gsd-research-phase` directly (or this research precedes `/gsd-discuss-phase`). All claims below are flagged as either:

- **REQ-LOCKED** — quoted verbatim from `.planning/REQUIREMENTS.md` (which has gone through milestone-discussion)
- **PRECEDENT** — bound by an established v1.2 / Phase 18-19 pattern that any reasonable user would not relitigate
- **DISCRETION** — Claude's design call, eligible for user override at `/gsd-discuss-phase` time

### REQ-LOCKED (from REQUIREMENTS.md, locked at milestone discussion)

- **DEFER-01 acceptance:** `(range 0 5)` → `[0,1,2,3,4]`; `(range 0 10 2)` → `[0,2,4,6,8]`; `(range 5 0 -1)` → `[5,4,3,2,1]`. Standard semantics: start inclusive, end exclusive, default step 1, negative step iterates backward, empty array when range is unsatisfiable.
- **DEFER-04 acceptance:** `enharmonic(E4)` → `Fb4`; `enharmonic(F4)` → `E#4`; `enharmonic(B4)` → `Cb5` (octave +1); `enharmonic(C4)` → `B#3` (octave −1); round-trip Fact `enharmonic(enharmonic(n))` returns a pitch-equivalent note for every chromatic note.
- **DEFER-05 acceptance:** `(slice [1,2,3,4,5] -3 5)` → `[3,4,5]`; `(slice [1,2,3,4,5] 0 -1)` → `[1,2,3,4]`. Behavioral change to v1.2 silent two-sided clamp (Phase 14 DX-05 / `CONTEXT D-01`). Documentation updates the slice contract; existing positive-index call sites unchanged.

### PRECEDENT-LOCKED (binding patterns)

- **Atomic commit per plan** — RED tests + GREEN production code in the same commit (Plan 12-02, 18-01, 19-01..05 all follow). HEAD never broken; `git bisect` lands on the introducing commit cleanly.
- **xUnit Facts under `flow-lang.Tests/Unit/Phase20/`** — mirrors Phase 13/14/15/17/18/19 directory convention.
- **Stdlib functions need both C# registration AND `.flow` `internal proc` declaration** — Phase 14 Plan 02 discovered this the hard way for `enharmonic()`. Skipping the proc declaration produces `"Function 'X' not found"` at parse time even though the C# delegate is registered.
- **Collision grep before landing keywords** — Phase 14 D-21 / Phase 15 reverbTime audit pattern. (Phase 20 introduces no new keywords, but the grep is still a closure-checklist gate.)
- **`internal proc` declaration syntax** — `Voids: arr` for `Array[T]` (any element type, encoded as `ArrayType(VoidType.Instance)` in C#). Negative literals require explicit `(sub 0 N)` binding due to parser binary-subtraction precedence (Phase 14 plan 14-01 Rule 1 fix).
- **`tests/test_*.flow` regression contract** — every existing test file must continue to run with same exit code and (where pinned) same sentinels. ~340 xUnit Facts must stay GREEN.

### DISCRETION (Claude's call, override-eligible)

- **DEFER-04 no-key flip direction for naturals** — REQUIREMENTS pins E4→Fb4, F4→E#4, B4→Cb5, C4→B#3. *Discretion:* the symmetric inverse (Fb4→E4, E#4→F4, Cb5→B4, B#3→C4) is the obvious choice; double-sharps/double-flats remain non-involutive (F##→G stays as today).
- **DEFER-04 in-key behavior on edge naturals** — when an in-key match exists for E4 in F major (E is diatonic in F major), should `enharmonic(E4)` in `key Fmajor` return Fb4 or E4? *Discretion:* preserve in-key spelling (return E4 unchanged) because the existing in-key branch is for diatonic respelling, not edge-respelling. Edge-respelling fires only on the no-key fallback path. *(Alternative: always edge-respell naturals; flag for `/gsd-discuss-phase`.)*
- **DEFER-05 negative-index normalization rule** — Python semantics: `idx < 0` becomes `idx + len`; values still out of range after normalization clamp to `[0, len]`. *Discretion:* preserve silent-clamp for post-normalization out-of-range; only the negative-index INTERPRETATION changes.
- **DEFER-01 step parameter type** — Int (matches REQUIREMENTS examples). 0-step is undefined; *Discretion:* throw `InvalidOperationException("range step cannot be zero")` mirroring the Phase 12-02 empty-array message style.
- **Plan structure** — 3 atomic plans + 1 closure (recommended) OR 1 atomic plan covering all three (alternative). Both consistent with Phase 12 / Phase 14 atomic-commit precedent. *Discretion:* recommend 3+1 for bisectability; user may collapse during `/gsd-discuss-phase`.
- **DEFER-04 algorithm for non-edge naturals** — REQUIREMENTS specifies edges only for E/F/B/C. Naturals D, G, A have no immediate enharmonic edge (D is between C# and Eb; closest "multi-letter" spellings would be D = C## or Ebb, which are double-accidental territory). *Discretion:* for D/G/A naturals, preserve unchanged (return as-is). Only E/F/B/C get edge-respelled.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **DEFER-01** | `range(Int, Int) → Array[Int]` and `range(Int, Int, Int) → Array[Int]` registered in stdlib | §3 (one new method `Collections.Range`, two `FunctionSignature` registrations in `BuiltInFunctions.RegisterCollections`, two `internal proc range` declarations in `collections.flow`) |
| **DEFER-04** | Multi-letter enharmonic edges: E↔Fb, F↔E#, B↔Cb (octave +1), C↔B# (octave −1) | §4 (extend `HarmonyFunctions.Enharmonic` no-key fallback for naturals where letter ∈ {E,F,B,C}; existing flip helper `ComputeFlippedSpelling` is the model — adapt for naturals) |
| **DEFER-05** | `slice` accepts negative-from-end indices Python-style; existing positive-index callers unchanged | §5 (modify `Collections.SliceArray` and `Collections.SliceSequence` — normalize negative indices BEFORE clamp; net 4-line edit per function) |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `range(start, end)` / `range(start, end, step)` stdlib | Standard Library / Built-ins (`Collections.cs`) | Stdlib `.flow` declarations (`collections.flow`) | Pure function over Int → Array[Int]; no AST node, no parser changes. Identical pattern to `take`, `drop`, `slice`. |
| Multi-letter enharmonic edges | Standard Library / Harmony (`HarmonyFunctions.cs`) | None | `Enharmonic` is already a context-dependent built-in registered in `RegisterContextDependent`. Naturals branch (line 44) currently returns unchanged — extend it. |
| Slice negative-from-end | Standard Library / Built-ins (`Collections.cs`) | xUnit Facts (Phase14/SliceTests.cs migration) | Modifies existing `SliceArray` + `SliceSequence` — pure index-normalization edit before existing clamp. |

All three capabilities live in the **Standard Library tier**. Zero changes to lexer, parser, AST, interpreter, runtime, or audio pipeline. This is why the phase is LBR end-to-end.

---

## Standard Stack

### Core (existing, no changes)

| Component | File | Role |
|-----------|------|------|
| `Collections` static class | `flow-lang/StandardLibrary/Collections.cs` | Hosts SliceArray, SliceSequence, Take, Drop, etc. — Range goes here. |
| `HarmonyFunctions` static class | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | Hosts `Enharmonic` private method (line 37) and helpers. |
| `BuiltInFunctions.RegisterCollections` | `flow-lang/StandardLibrary/BuiltInFunctions.cs:414` | Registers Collections delegates with `InternalFunctionRegistry`. |
| `collections.flow` | `flow-lang/collections.flow` | Stdlib `.flow` module with `internal proc` declarations. Imported by `std.flow`. |
| `InternalFunctionRegistry.Register` | `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` | Maps `FunctionSignature` → C# delegate. |
| `OverloadResolver` | `flow-lang/TypeSystem/OverloadResolver.cs` | Resolves `range(0,5)` to 2-arg overload, `range(0,10,2)` to 3-arg overload via specificity scoring. |

### Supporting (existing, no changes)

| Component | Role |
|-----------|------|
| `NoteType.Parse(string)` (`NoteType.cs:34`) | Parses note string → `(letter, octave, alteration)`. Used at `Enharmonic` entry. |
| `NoteType.Format(letter, octave, alteration)` (`NoteType.cs:175`) | Run-based emitter; `Parse(Format(x)) == x` for any int alteration. Called to build the result `Value.Note(...)`. |
| `NoteType.GetNoteValue(letter, octave)` / `ToMidiNote` | MIDI conversion for round-trip pitch equivalence verification. |
| `xUnit` (existing test framework) | All tests live as `[Fact]` methods under `flow-lang.Tests/Unit/Phase20/` (NEW directory). |

### Alternatives Considered

| Instead of | Could Use | Why Not |
|------------|-----------|---------|
| Register range under Collections | Register under StdLib (math) | Keep collections-related ops together; matches BuiltInDocs.cs:38 placement (already documented as a Collections function). |
| Modify `SliceArray` in-place | Add new `sliceFromEnd` overload, deprecate old behavior | DEFER-05 in REQUIREMENTS pins `(slice ...)` not `(sliceFromEnd ...)`. Behavioral change is the explicit intent (Phase 14 deferred-items DEFER-06 noted this option but REQUIREMENTS.md DEFER-05 supersedes it as a hard cut-over). |
| Pragma-gate DEFER-05 negative semantics | `enable negativeSlice;` opt-in | REQUIREMENTS DEFER-05 calls it a "behavioral change" not "opt-in feature". Phase 21 ships pragmas — gating DEFER-05 on Phase 21 increases coupling unnecessarily. |
| Pragma-gate DEFER-04 edge respelling | `enable enharmonicEdges;` opt-in | Same — REQUIREMENTS treats this as a closure of v1.2's deferred surface, not a new gated mode. ARCHITECTURE.md §7 Feature 5 originally suggested gating; superseded by REQUIREMENTS.md DEFER-04 which has no pragma clause. |
| Hand-roll integer LCM/GCD for range | (n/a) | Range needs no math — pure int loop. |

**Installation:** No new packages. `dotnet build` only.

**Version verification:** N/A (zero new dependencies; Phase 20 is closure work over the existing standard library).

---

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                  Phase 20 Touch Points                         │
└─────────────────────────────────────────────────────────────────┘

    user .flow script
         │
         ▼
   ┌──────────────┐
   │  Parser      │   (no changes — these calls already parse)
   └──────────────┘
         │ AST: FunctionCallExpression("range", [0, 5])
         │      FunctionCallExpression("slice", [arr, -3, 5])
         │      FunctionCallExpression("enharmonic", [E4])
         ▼
   ┌──────────────────────────────────────────────────┐
   │  ExpressionEvaluator                             │
   │   ↓ resolves via OverloadResolver                │
   │   ↓ dispatches to InternalFunctionRegistry       │
   └──────────────────────────────────────────────────┘
         │
         ▼
   ┌──────────────────────────────────────────────────┐
   │  StandardLibrary (Phase 20 changes here only)    │
   ├──────────────────────────────────────────────────┤
   │                                                  │
   │  Collections.Range          (NEW — DEFER-01)    │
   │  Collections.SliceArray     (MODIFIED — DEFER-05)│
   │  Collections.SliceSequence  (MODIFIED — DEFER-05)│
   │  HarmonyFunctions.Enharmonic(MODIFIED — DEFER-04)│
   │                                                  │
   └──────────────────────────────────────────────────┘
         │
         ▼
      Value (Array[Int] / Sequence / Note)

   Stdlib `.flow` declarations (REQUIRED — symbol resolution at parse time):
     collections.flow:  internal proc range (Int: start, Int: end)
                        internal proc range (Int: start, Int: end, Int: step)
                        (slice declarations already exist — no change)
     std.flow:          (enharmonic declaration already exists at line 114)
```

### Recommended Project Structure (no new directories)

```
flow-lang/
├── StandardLibrary/
│   ├── Collections.cs                  # Range method NEW; Slice* methods MODIFIED
│   ├── BuiltInFunctions.cs             # Two range FunctionSignature lines added
│   └── Harmony/
│       └── HarmonyFunctions.cs         # Enharmonic naturals branch MODIFIED
├── collections.flow                    # Two `internal proc range` lines added
└── std.flow                            # No changes (enharmonic already declared at line 114)

flow-lang.Tests/
├── Unit/
│   ├── Phase14/
│   │   ├── EnharmonicTests.cs          # 4 NoKey_NaturalUnchanged_* Facts INVERT (migration)
│   │   └── SliceTests.cs               # Existing 9 Facts STAY GREEN (cases pinned coincide)
│   └── Phase20/                        # NEW directory
│       ├── RangeTests.cs               # NEW — DEFER-01 unit Facts
│       ├── EnharmonicEdgesTests.cs     # NEW — DEFER-04 edge + round-trip Facts
│       └── SliceNegativeTests.cs       # NEW — DEFER-05 negative-from-end Facts
└── (existing structure unchanged)

tests/
├── test_range.flow                     # NEW (or extend test_collections.flow)
├── test_enharmonic_edges.flow          # NEW (or extend test_enharmonic.flow)
├── test_slice_negative.flow            # NEW (or extend test_slice.flow)
└── test_custom_oscillator.flow         # No code change; Test 4 newly UN-blocks
```

### Pattern 1: Stdlib Function Registration (DEFER-01)

**What:** Adding a built-in callable from `.flow` scripts.
**When to use:** Any pure-function addition to the standard library.
**Example (verified pattern from existing `slice` registration at `BuiltInFunctions.cs:454-460`):**

```csharp
// Source: flow-lang/StandardLibrary/BuiltInFunctions.cs:445-449 (take/drop precedent)
var takeSignature = new FunctionSignature("take", [new ArrayType(VoidType.Instance), IntType.Instance]);
registry.Register("take", takeSignature, Collections.Take);

// New for DEFER-01:
var range2Signature = new FunctionSignature("range", [IntType.Instance, IntType.Instance]);
registry.Register("range", range2Signature, Collections.Range);

var range3Signature = new FunctionSignature("range", [IntType.Instance, IntType.Instance, IntType.Instance]);
registry.Register("range", range3Signature, Collections.Range);
```

```csharp
// Source: flow-lang/StandardLibrary/Collections.cs (NEW method, mirroring Take/Drop shape)
public static Value Range(IReadOnlyList<Value> args)
{
    int start = args[0].As<int>();
    int end = args[1].As<int>();
    int step = args.Count >= 3 ? args[2].As<int>() : 1;
    if (step == 0)
        throw new InvalidOperationException("range step cannot be zero");

    var result = new List<Value>();
    if (step > 0)
        for (int i = start; i < end; i += step) result.Add(Value.Int(i));
    else
        for (int i = start; i > end; i += step) result.Add(Value.Int(i));

    return Value.Array(result, IntType.Instance);
}
```

```flow
# Source: flow-lang/collections.flow (NEW lines, mirroring lines 13-15 take/drop/slice precedent)
internal proc range (Int: start, Int: end)
internal proc range (Int: start, Int: end, Int: step)
```

### Pattern 2: Negative Index Normalization (DEFER-05)

**What:** Python-style "negative-means-from-end" interpretation.
**When to use:** Whenever an index parameter accepts negatives as semantic positions.
**Example (Python `arr[-1]` semantics, verified vs CPython source):**

```csharp
// Phase 14 D-01 silent-clamp behavior (REPLACED):
//   int s = Math.Max(0, startVal.As<int>());
//   int e = Math.Min(count, endVal.As<int>());
//
// DEFER-05 Python-semantics replacement:
int rawStart = startVal.As<int>();
int rawEnd   = endVal.As<int>();

// Normalize: negative means from end
int normStart = rawStart < 0 ? rawStart + count : rawStart;
int normEnd   = rawEnd   < 0 ? rawEnd   + count : rawEnd;

// Clamp post-normalization (preserves silent-clamp for STILL-out-of-range values)
int s = Math.Clamp(normStart, 0, count);
int e = Math.Clamp(normEnd,   0, count);

if (s >= e) return Value.Array(Array.Empty<Value>(), arrayType.ElementType);
return Value.Array(elements.Skip(s).Take(e - s).ToArray(), arrayType.ElementType);
```

**Verification matrix** for `slice([1,2,3,4,5], start, end)`:

| start | end | Old (silent clamp) | New (Python) | Acceptance |
|-------|-----|---------------------|--------------|------------|
| 1 | 4 | [2,3,4] | [2,3,4] | UNCHANGED |
| -5 | 2 | [1,2] (clamp -5→0) | [1,2] (-5+5=0) | COINCIDES |
| 3 | 100 | [4,5] (clamp 100→5) | [4,5] (post-norm 100 stays > 5, clamp → 5) | UNCHANGED |
| 3 | 2 | [] | [] | UNCHANGED |
| **-3** | **5** | [1,2,3,4,5] (clamp -3→0) | **[3,4,5]** (-3+5=2) | **NEW BEHAVIOR (REQ-LOCKED)** |
| **0** | **-1** | [] (clamp -1→0; 0≥0 empty) | **[1,2,3,4]** (-1+5=4) | **NEW BEHAVIOR (REQ-LOCKED)** |
| -100 | 3 | [1,2,3] (clamp -100→0) | [1,2,3] (post-norm -95, clamp → 0) | UNCHANGED |

The critical observation: **all existing positive-index callers and the two negative-clamping cases pinned in `Phase14/SliceTests.cs` coincide between old and new semantics.** Only callers that *intended* "from-end" via negatives experience a change — and DEFER-05's whole point is that today there are zero such callers (this is what DEFER-04 in `14-deferred-items.md` calls "no existing test should rely on negative indices clamping (they'd have been written explicitly with positive indices)").

### Pattern 3: Multi-letter Enharmonic Edge (DEFER-04)

**What:** Spell a natural pitch as its enharmonic neighbor across a letter-boundary.
**When to use:** Inside `Enharmonic` no-key fallback when input letter ∈ {E, F, B, C}.
**Example (algorithm for natural-edge respelling):**

```csharp
// Source: extends HarmonyFunctions.cs:43-47 (the D-05 "naturals return unchanged" branch)

// D-05 naturals-unchanged is REPLACED for E/F/B/C only:
if (alteration == 0)
{
    // DEFER-04: edge naturals respell to their multi-letter neighbor.
    // E ↔ Fb (same octave): E4 (MIDI 64) → Fb4 (F=65, alt=-1, MIDI 64) ✓
    // F ↔ E# (same octave): F4 (MIDI 65) → E#4 (E=64, alt=+1, MIDI 65) ✓
    // B ↔ Cb (octave +1):   B4 (MIDI 71) → Cb5 (C5=72, alt=-1, MIDI 71) ✓
    // C ↔ B# (octave -1):   C4 (MIDI 60) → B#3 (B3=59, alt=+1, MIDI 60) ✓
    // D, G, A: no edge — return unchanged (Claude's discretion, REQ silent on these).
    return letter switch
    {
        'E' => Value.Note(NoteType.Format('F', octave,     -1)),  // Fb same oct
        'F' => Value.Note(NoteType.Format('E', octave,     +1)),  // E# same oct
        'B' => Value.Note(NoteType.Format('C', octave + 1, -1)),  // Cb next oct
        'C' => Value.Note(NoteType.Format('B', octave - 1, +1)),  // B# prev oct
        _   => Value.Note(NoteType.Format(letter, octave, 0)),    // D/G/A unchanged
    };
}
```

**Round-trip verification (acceptance Fact):**

```csharp
// Source: NEW EnharmonicEdgesTests.cs (driving via FlowEngineRunner pattern from
//         Phase14/EnharmonicTests.cs; enharmonic() requires ExecutionContext)
[Theory]
[InlineData("C4", 60)]  [InlineData("D4", 62)]  [InlineData("E4", 64)]  [InlineData("F4", 65)]
[InlineData("G4", 67)]  [InlineData("A4", 69)]  [InlineData("B4", 71)]  [InlineData("C5", 72)]
[InlineData("Cs4", 61)] [InlineData("Db4", 61)] [InlineData("Ds4", 63)] // sharp/flat already round-trip per Phase 14
public void Enharmonic_RoundTrip_PitchEquivalent(string noteStr, int expectedMidi)
{
    var result1 = RunEnharmonic(noteStr);
    var result2 = RunEnharmonic(result1);
    var (l, o, a) = NoteType.Parse(result2);
    Assert.Equal(expectedMidi, NoteType.ToMidiNote(l, o, a));
}
```

**Inverse-respell verification (the non-natural input branch):**

The existing `ComputeFlippedSpelling` (line 234-252) handles non-natural inputs (Db, F#, etc.) and ALREADY produces the correct inverse for edge cases. Specifically:
- `enharmonic(Fb4)` → input letter F, alteration -1, MIDI 64. Goes to non-natural flat branch. `LetterDown('F') = 'E'`, `downOct = 4` (F is not 'C'). `downNaturalMidi = ToMidiNote('E', 4) = 64`. Result alteration = `64 - 64 = 0`. Returns `Format('E', 4, 0)` = `"E4"` ✓
- `enharmonic(E#4)` → input letter E, alteration +1, MIDI 65. Goes to non-natural sharp branch. `LetterUp('E') = 'F'`, `upOct = 4` (E is not 'B'). `upNaturalMidi = ToMidiNote('F', 4) = 65`. Result alteration = `65 - 65 = 0`. Returns `Format('F', 4, 0)` = `"F4"` ✓
- `enharmonic(Cb5)` → input letter C, alteration -1, MIDI 71. Goes to non-natural flat branch. `LetterDown('C') = 'B'`, `downOct = 4` (C IS 'C', so octave-1). `downNaturalMidi = ToMidiNote('B', 4) = 71`. Result alteration = 0. Returns `Format('B', 4, 0)` = `"B4"` ✓
- `enharmonic(B#3)` → input letter B, alteration +1, MIDI 60. Goes to non-natural sharp branch. `LetterUp('B') = 'C'`, `upOct = 4` (B IS 'B', so octave+1). `upNaturalMidi = ToMidiNote('C', 4) = 60`. Result alteration = 0. Returns `Format('C', 4, 0)` = `"C4"` ✓

All four edge inverses already work via the existing `ComputeFlippedSpelling` — **DEFER-04's only new code is the 5-line natural-edge switch above.**

### Anti-Patterns to Avoid

- **Adding `range` to BuiltInFunctions.cs without `internal proc range` in `collections.flow`:** Phase 14 Plan 02 hit this exact bug for `enharmonic()` — Fact failed with "Function 'enharmonic' not found" despite C# registration. Symbol resolution at parse time requires the proc declaration. Mitigation: Phase 20 plan checklist must include both files for DEFER-01.
- **Treating Phase 14 `NoKey_NaturalUnchanged_*` Facts as pinned regression baseline:** They were the explicit Phase 14 D-05 contract (naturals unchanged). DEFER-04 inverts that contract. Plan must MIGRATE those 4 Facts (rename and re-pin) — deleting them silently leaves a coverage gap.
- **Implementing DEFER-05 by replacing `Math.Max(0, x)` with `Math.Abs(x)`:** Wrong. Negative-from-end is `x + len` not `|x|`. (Sounds obvious but is a known beginner mistake — Pythonic semantics are *additive*, not *absolute*.)
- **Using `IReadOnlyList<Value>.Take(end - start)` without first asserting `start >= 0`:** LINQ Skip/Take silently produce empty for negative inputs. Behavior is correct but masks bugs in the normalization layer. Mitigation: explicit clamp before Skip/Take per Pattern 2.
- **Bundling all three DEFERs into one commit without RED tests up front:** Phase 12 atomic-commit precedent allows RED+GREEN in one commit, but the three DEFERs land cleanly as 3 separate atomic commits because they touch 3 distinct files. Bisectability wins.
- **Not removing `["test_custom_oscillator.flow"] = "Function 'range' not found"` from `FlowScriptData.cs:57`:** This is the documented removal step from `12-stability/deferred-items.md`. Without removing it, the `test_custom_oscillator.flow` Theory row stays in the ExpectedErrorScripts branch even after `range` is registered — false-green.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Range generation | Custom enumerator class with lazy iteration | Plain `for` loop with `List<Value>` accumulator | Range result is bounded (composition-time, not audio-time); LINQ Enumerable.Range exists but returns IEnumerable<int> not Value, requires conversion shim — same LOC. |
| Negative index normalization | Custom Index/Range type / regex of bracket syntax | Inline `if (idx < 0) idx += len;` then existing `Math.Clamp` | C# 8's `Index` type is for `arr[^1]` syntax, not for runtime int values. Hand-coded normalization is 2 lines. |
| Note letter neighbor lookup | New chromatic-circle data structure | Reuse existing `LetterUp`/`LetterDown` helpers (`HarmonyFunctions.cs:254-266`) | The existing helpers handle the B↔C wrap; the natural-edge switch is just two lines per letter. |
| Round-trip pitch equivalence test | Custom Note equality with cent tolerance | `NoteType.ToMidiNote(letter, oct, alt) == expectedMidi` | MIDI numbers are exact integers; equality is the right test (no floating-point in this domain). |
| Stdlib `.flow` proc declaration | Custom autodetection | Manual `internal proc` in `collections.flow` | Phase 14 Plan 02 confirmed this is the contract. ~70 existing stdlib functions all declare both halves. |

**Key insight:** Phase 20 is *all* "use the existing patterns" work. Three closures × three precedents (Take/Drop = range pattern, Phase 14 enharmonic flip = DEFER-04 pattern, Phase 14 slice clamp = DEFER-05 modification site). No novel infrastructure.

---

## Common Pitfalls

### Pitfall 1: Phase 14 EnharmonicTests Facts silently pin OPPOSITE behavior

**What goes wrong:** `Phase14/EnharmonicTests.cs` ships 4 `NoKey_NaturalUnchanged_C4/E4/B4/F4` Facts that explicitly assert `enharmonic(C4) == "C4"`, `enharmonic(E4) == "E4"`, etc. DEFER-04 inverts these to `enharmonic(E4) == "Fb4"`. Running tests after the C# change without migrating the Facts produces 4 RED Facts attributed to "regression" when they are actually "DEFER-04 acceptance".

**Why it happens:** Phase 14 D-05 was a deliberate scope-cut: the original requirement included multi-letter edges, but they were deferred to DEFER-04 (`14-deferred-items.md` line 101-118). The Facts encoded the deferred-state contract.

**How to avoid:**
1. Plan must explicitly identify these 4 Facts as MIGRATION targets, not regression candidates.
2. Two acceptable migration shapes:
   - (a) **Rename + re-pin:** Rename to `NoKey_NaturalEdgeRespells_C4` etc., update assertions to `"B#3"`, `"Fb4"`, `"Cb5"`, `"E#4"`. Preserves test count.
   - (b) **Delete + replace in Phase20/EnharmonicEdgesTests.cs:** Delete the four Phase 14 Facts; new Phase 20 Facts cover the inverted assertions plus round-trip + non-edge naturals (D, G, A unchanged).
3. Recommend (a) for audit-trail clarity (the Phase14 directory tells the v1.2-deferred story).

**Warning signs:** Phase 14 Fact failures listed as "regression" without migration acknowledgment. Plan checklist that doesn't enumerate the 4 specific Facts.

### Pitfall 2: `tests/test_enharmonic.flow` line 6-7 prints `(enharmonic E4)` and `(enharmonic C4)`

**What goes wrong:** `tests/test_enharmonic.flow` is a Theory row in `FlowScriptData.cs` (no RequiredSentinels — runs via errorCount==0 gate). It currently prints `E4` (line 6) and `C4` (line 7) to stdout. After DEFER-04 it will print `Fb4` and `B#3`. The Theory row stays GREEN (no error count change), but the implicit "documents naturals-unchanged behavior" intent of the script is broken.

**Why it happens:** The script was written in Phase 14 to demonstrate the no-edge-respelling contract. DEFER-04 reverses that.

**How to avoid:** Plan must update `tests/test_enharmonic.flow` lines 6-7 with new comments and either (a) update expected output documentation, or (b) move those lines into a new `test_enharmonic_edges.flow` that explicitly demonstrates edges. Recommend (b) — keep `test_enharmonic.flow` as the unchanged-Phase-14-baseline (modulo natural input changes — see below) and add a new file for DEFER-04.

**Subtle:** The `(print (str (enharmonic C4)))` and `(print (str (enharmonic E4)))` calls are on lines 6-7 of `test_enharmonic.flow`. Under Phase 14 these print `C4` and `E4`. Under DEFER-04 they print `B#3` and `Fb4`. Without RequiredSentinels, the Theory row tolerates ANY stdout — so the change is silent. This is exactly the kind of "silent semantic drift" Pitfall 5 in PITFALLS.md warns about. Mitigation: ADD RequiredSentinels for the post-DEFER-04 outputs OR refactor lines 6-7 into a new file with sentinels.

**Warning signs:** Plan ships without inspecting `tests/test_enharmonic.flow` stdout under new behavior.

### Pitfall 3: `range` overload disambiguation `range(0, 5)` vs `range(0, 5, default)`

**What goes wrong:** `OverloadResolver` (specificity scoring at `OverloadResolver.cs:61`) resolves `range(0, 5)` to the 2-arg overload (specificity score: 3 exact matches × 1000 = 3000 for 2-arg vs N/A for 3-arg with arity mismatch). So OverloadResolver should not be ambiguous — but PITFALLS.md Pitfall 10 explicitly raised the concern that "Audit §2 hardening (overload ambiguity)" might land on this surface.

**Why it happens:** Pitfall 10 anticipated a potential issue. In practice, OverloadResolver requires exact arity match — `range(0,5)` with 2 args cannot match a 3-arg signature. So the concern is actually moot for well-formed signatures.

**How to avoid:** Plan must include a Fact asserting both forms resolve correctly:
```csharp
[Fact] public void Range_TwoArg_ResolvesCleanly()    { Assert.Equal(5, RunRange("(range 0 5)").Count); }
[Fact] public void Range_ThreeArg_ResolvesCleanly()  { Assert.Equal(5, RunRange("(range 0 10 2)").Count); }
[Fact] public void Range_NegativeStep_ResolvesCleanly() { Assert.Equal(5, RunRange("(range 5 0 (sub 0 1))").Count); }  // see Pitfall 4
```

**Warning signs:** OverloadResolver throws "ambiguous match" for `range(0,5)` — would mean a 3-arg overload was registered without explicit arity gating.

### Pitfall 4: Negative integer literals in `.flow` parse as binary subtraction

**What goes wrong:** `(range 5 0 -1)` is REQUIREMENTS.md acceptance verbatim. Parsing in Flow's grammar interprets `-1` as `(sub <prior-token> 1)` due to binary-subtraction precedence. Phase 14 plan 14-01 hit this exact bug for `slice arr -5 2` and Plan 12-05 hit it for `1.0 -1.0`. Workaround: bind to a variable: `Int negOne = (sub 0 1)` then `(range 5 0 negOne)`.

**Why it happens:** Lexer emits integer literals as positive Ints; the unary minus is parsed as binary subtraction when preceded by an expression in argument position.

**How to avoid:**
1. The xUnit Facts that drive via `Collections.Range(args)` directly with `Value.Int(-1)` bypass the parser — these will work.
2. The `.flow` script test (e.g., `tests/test_range.flow`) must use `Int negOne = (sub 0 1); (range 5 0 negOne)` shape.
3. Document this in the test file with a comment matching `tests/test_slice.flow:6` precedent.

**Warning signs:** REQUIREMENTS.md acceptance copy-pasted into `.flow` script verbatim and parse-error on `-1`.

### Pitfall 5: `test_custom_oscillator.flow` Theory row flip from RED-baseline to GREEN

**What goes wrong:** `FlowScriptData.cs:57` declares `["test_custom_oscillator.flow"] = "Function 'range' not found"` — the file is currently in the **ExpectedErrorScripts** dictionary, gated to assert that `range` is missing. Once DEFER-01 lands, the file will run cleanly and emit zero errors. Without removing this entry, the Theory row will assert "expected error 'Function range not found' must appear in stderr" → FAIL because stderr is now empty.

**Why it happens:** Plan 12-06 deferred the actual `range` fix and pinned the test to the pre-fix baseline as a Theory-row stabilizer. The pin is now stale.

**How to avoid:**
1. Plan checklist for DEFER-01 must include: remove `FlowScriptData.cs:57` (the `["test_custom_oscillator.flow"]` ExpectedErrorScripts entry).
2. Documented in `12-stability/deferred-items.md` step 4: "Remove the pre-fix baseline entry from FlowScriptData.cs so test_custom_oscillator flips to the GREEN default branch."
3. Verify `test_custom_oscillator.flow` actually runs to completion under DEFER-01 — Test 4 (line 86) is the only blocker, but the script also has Tests 1-3 that previously passed via Plan 12-05. Sanity check: run the script after fix and confirm exit 0.

**Warning signs:** RED `test_custom_oscillator.flow` Theory row after DEFER-01 lands with stderr empty assertion failure.

### Pitfall 6: DEFER-05 changes silent contract — Pitfall 10 from PITFALLS.md

**What goes wrong:** Per PITFALLS.md Pitfall 10: "v1.2 silent two-sided clamp behavior is replaced by negative-from-end semantics... Existing positive-index call sites continue to work". The risk is unintended user-script breakage from scripts that *accidentally* passed -1 expecting it to clamp.

**Why it happens:** Phase 14 D-01 silently treated negatives as 0. Any user script that did `slice(seq, 0, -1)` got an empty array and may have layered downstream code around that (e.g., `if (empty result) { ... }` branch).

**How to avoid:**
1. Repository grep before landing DEFER-05: `grep -rn "slice.*,.*,.*-" tests/` — currently empty per ARCHITECTURE.md §7 Feature 6 expectation, but verify at plan time.
2. Plan VERIFICATION.md must include the empty-grep transcript (Phase 14 D-21 / Phase 15 reverbTime audit precedent).
3. Acknowledge the breaking change in QOL-04 / Phase 26 tutorial refresh: a "Breaking changes since v1.2" section noting `slice(*, 0, -1)` returning `[1,2,3,4]` instead of `[]`. (Phase 26 territory, but flag for Phase 20 closure docs.)

**Warning signs:** Existing test fails after DEFER-05 lands; grep transcript missing from VERIFICATION.md.

### Pitfall 7: DEFER-04 sequencing — must precede DEFER-02/03 (H-alias)

**What goes wrong:** Phase 21 (DEFER-02/03) needs DEFER-04 as a prerequisite. PITFALLS.md Pitfall 10: "DEFER-02/03 (H alias via pragma): Cannot land without DEFER-04 enharmonic edges. Rationale: if H becomes B, then H♯ becomes B♯, and B♯ is already a valid note. But H♯ becomes B♯, which only works if multi-letter enharmonic edges are present — otherwise B♯ has no defined alteration > +1."

**Why it happens:** v1.3 ROADMAP §1 lists this as binding pre-ordering #3. Phase 20 must close DEFER-04 cleanly before Phase 21 starts.

**How to avoid:** Phase 20 ROADMAP placement is correct (Phase 20 → Phase 21). Plan must ensure DEFER-04 lands and is verified before any Phase 21 work begins. Phase 20 closure docs explicitly state "DEFER-04 closed; Phase 21 unblocked."

**Warning signs:** Phase 21 spike begins before Phase 20 verification gate is green.

### Pitfall 8: Round-trip Fact must use pitch-equivalence, not string-equality

**What goes wrong:** `enharmonic(enharmonic(E4))` returns `enharmonic(Fb4)` returns `E4` — pitch-equivalent AND string-equivalent. But `enharmonic(enharmonic(F##4))` returns `enharmonic(G4)` (since F## flips to G via existing logic) returns `Fb4` (under DEFER-04 G-natural is unchanged...wait, G is NOT in the edge set). Let me reconsider: `F##4` (MIDI 67) → existing flip → `G4` (natural) → DEFER-04 → G unchanged → `G4`. So round trip `F##4 → G4 → G4` — NOT involutive (loses the double-sharp form). HarmonyFunctions.cs:34 explicitly notes "Double-sharps and double-flats may collapse to naturals (F##4 → G4) — documented non-involutive."

**Why it happens:** Some inputs collapse to naturals which under DEFER-04 with the D/G/A discretion choice stay unchanged. Round-trip pitch is preserved but the original spelling is not recoverable.

**How to avoid:** Round-trip Fact uses `NoteType.ToMidiNote(...) == expectedMidi`, NOT `result == originalString`. REQUIREMENTS.md is explicit: "Round-trip Fact: `enharmonic(enharmonic(n))` returns a note pitch-equivalent to `n` for every chromatic note." Pitch-equivalent ≠ spelling-equivalent.

**Warning signs:** Round-trip Fact uses string equality and fails on F##4 / Bbb4 inputs.

---

## Code Examples

Verified patterns from existing source:

### Existing — `slice` registration as `range` registration template

```csharp
// Source: flow-lang/StandardLibrary/BuiltInFunctions.cs:454-460 (verified at HEAD)
var sliceArraySignature = new FunctionSignature("slice",
    [new ArrayType(VoidType.Instance), IntType.Instance, IntType.Instance]);
registry.Register("slice", sliceArraySignature, Collections.SliceArray);

var sliceSeqSignature = new FunctionSignature("slice",
    [SequenceType.Instance, IntType.Instance, IntType.Instance]);
registry.Register("slice", sliceSeqSignature, Collections.SliceSequence);
```

### Existing — `enharmonic` flip non-natural pattern

```csharp
// Source: flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:234-252 (verified at HEAD)
private static (char letter, int oct, int alt) ComputeFlippedSpelling(char letter, int octave, int alteration, int inputMidi)
{
    if (alteration > 0)
    {
        // sharp → letter UP. If letter is 'B', upper neighbor 'C' is in the next octave.
        char up = LetterUp(letter);
        int upOct = (letter == 'B') ? octave + 1 : octave;
        int upNaturalMidi = NoteType.GetNoteValue(up, upOct);
        return (up, upOct, inputMidi - upNaturalMidi);
    }
    else
    {
        // flat → letter DOWN. If letter is 'C', lower neighbor 'B' is in the previous octave.
        char down = LetterDown(letter);
        int downOct = (letter == 'C') ? octave - 1 : octave;
        int downNaturalMidi = NoteType.GetNoteValue(down, downOct);
        return (down, downOct, inputMidi - downNaturalMidi);
    }
}
```

### Existing — current `Enharmonic` natural branch (Phase 14 D-05)

```csharp
// Source: flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:43-47 (REPLACE for DEFER-04)
// D-05: naturals return unchanged, full stop — no edge respelling.
if (alteration == 0)
{
    return Value.Note(NoteType.Format(letter, octave, 0));
}
```

### Existing — current `SliceArray` clamp (Phase 14 D-01)

```csharp
// Source: flow-lang/StandardLibrary/Collections.cs:170-176 (MODIFY for DEFER-05)
var elements = arr.As<IReadOnlyList<Value>>();
int count = elements.Count;
int s = Math.Max(0, startVal.As<int>());
int e = Math.Min(count, endVal.As<int>());
if (s >= e)
    return Value.Array(Array.Empty<Value>(), arrayType.ElementType);
return Value.Array(elements.Skip(s).Take(e - s).ToArray(), arrayType.ElementType);
```

### Existing — Phase 14 enharmonic Fact infrastructure

```csharp
// Source: flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs (uses FlowEngineRunner because
//         enharmonic() is RegisterContextDependent — needs ExecutionContext for MusicalContext.Key)
// Phase 20 EnharmonicEdgesTests.cs follows the same pattern.
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `range` documented but unregistered (BuiltInDocs.cs:38 only) | `range` registered as 2-arg + 3-arg in BuiltInFunctions.cs + collections.flow proc decls | Phase 20 DEFER-01 | `(range 0 5)` works in `.flow` scripts; `test_custom_oscillator.flow` Test 4 unblocks; FlowScriptData.cs:57 entry removed. |
| `enharmonic` returns naturals unchanged (Phase 14 D-05) | `enharmonic(E4) → Fb4`, `enharmonic(F4) → E#4`, `enharmonic(B4) → Cb5`, `enharmonic(C4) → B#3` | Phase 20 DEFER-04 | 4 Phase 14 Facts MIGRATE; Phase 21 (H-alias) unblocked; round-trip pitch invariant strengthened. |
| `slice` silent-clamps negatives to 0 (Phase 14 D-01) | `slice` interprets negatives as from-end Python-style | Phase 20 DEFER-05 | `(slice [1,2,3,4,5] 0 -1)` returns `[1,2,3,4]` instead of `[]`; existing positive-index callers and Phase 14 SliceTests.cs Facts unchanged (cases coincide). |

**Deprecated/outdated:**
- `tests/test_enharmonic.flow:6-7` natural-input prints (`(enharmonic E4)`, `(enharmonic C4)`): outputs change post-DEFER-04. Update file or migrate to new test file.
- `Phase14/EnharmonicTests.cs::NoKey_NaturalUnchanged_*` (4 Facts): explicit Phase 14 D-05 contract; invert/migrate.
- `FlowScriptData.cs:57` ExpectedErrorScripts entry: stale post-DEFER-01.
- `14-deferred-items.md::DEFER-06` (slice negative-from-end): closes via REQUIREMENTS.md DEFER-05; strikethrough preserved per handling protocol §3.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `(slice arr -5 2)` and existing `Array_NegativeStartClamps` Fact return the same 2 elements under both old (silent clamp) and new (Python) semantics | Pattern 2 verification matrix | If wrong, Phase14/SliceTests.cs needs migration too — increases plan scope by ~3 Facts. **Verified by inspection:** -5 + 5 (count) = 0; clamp(0, 0, 5) = 0; same result as Math.Max(0, -5) = 0. SAFE. |
| A2 | OverloadResolver disambiguates `range(0, 5)` (2 args) vs `range(0, 10, 2)` (3 args) by arity exact-match before specificity scoring | Pitfall 3 | If wrong, may need to register only 3-arg form with optional step semantics, or use a single varargs signature. **Verified at Phase 14 plan 14-01** — slice ships 2 overloads (Array vs Sequence) with same arity, OverloadResolver disambiguates by arg-0 type. Different-arity overloads are an EASIER case (arity mismatch is grounds for rejection before specificity). LOW risk. |
| A3 | D, G, A naturals should remain unchanged under DEFER-04 (no edge respelling) | DEFER-04 Pattern 3 | REQUIREMENTS.md is silent on D/G/A. If user wants D → C## or D → Ebb (double-accidental respelling), plan needs rework. RECOMMEND surfacing at /gsd-discuss-phase. Default "unchanged" is conservative. |
| A4 | `tests/test_enharmonic.flow` runs as a Theory row with errorCount==0 gate (no RequiredSentinels) | Pitfall 2 | Verified by grep — confirmed not in `RequiredSentinels` dictionary. SAFE. |
| A5 | `(range 5 0 (sub 0 1))` parses correctly in `.flow` (negative step via binary-subtraction binding) | Pitfall 4 | Phase 14 plan 14-01 establishes this exact precedent for `slice arr negFive 2` — same pattern. SAFE. |
| A6 | `enharmonic(enharmonic(F##4))` round-trips to G4 (pitch-equivalent to F##4, MIDI 67) but not string-equivalent | Pitfall 8 | HarmonyFunctions.cs:34 already documents F##4 → G4 collapse as non-involutive. REQUIREMENTS.md says "pitch-equivalent" not "string-equivalent". SAFE if Fact uses MIDI equality. |
| A7 | Existing 340 xUnit Facts + ~70 .flow tests pass post-Phase-20 with the documented migration of 4 Phase14 EnharmonicTests Facts | Pitfall 1 | If a test we missed asserts `enharmonic(natural)` unchanged, it RED-fails. **Mitigation:** plan grep `grep -rn "enharmonic.*[CEFB][0-9]" tests/ flow-lang.Tests/` before landing. |

**If user confirms DISCRETION choices (A3, plan structure 3+1 vs 1):** All `[ASSUMED]` claims fall away; research is fully verified.

---

## Open Questions (RESOLVED)

All 5 questions resolved at plan-time via auto-mode defaults locked as D-USER-A..F. Decisions documented inline below.

1. **Plan structure: 3 atomic plans + closure (4 total) vs 1 bundled plan + closure (2 total)?**
   - **RESOLVED → D-USER-A: 3+1 (4 plans)**, sequential execution. Each DEFER independently revertable; closure plan handles docs-only. Matches Phase 12 / 14 / 19 multi-plan atomic-commit precedent.

2. **DEFER-04 in-key edge behavior — key Fmajor + `enharmonic(E4)` → ?**
   - **RESOLVED → D-USER-B: Preserve in-key diatonic spelling.** When key is active and pitch is diatonic to the key, return scale-diatonic spelling (returns `E4`, not `Fb4`). Edge-respelling fires only on no-key fallback or chromatic input. Matches Phase 14 D-04 in-key rule precedent.

3. **DEFER-04 D/G/A naturals — return unchanged or respell to nearest double-accidental?**
   - **RESOLVED → D-USER-C: Return unchanged** for D/G/A. Only E/F/B/C naturals get edge respelling (the SPEC's explicit list). Double-accidental respelling deferred to a future phase if user requests.

4. **DEFER-05 normalization with extreme negatives (e.g., `slice(arr5, -100, 3)`) — clamp normStart to 0 or error?**
   - **RESOLVED → D-USER-D: Silent clamp post-normalization** (Python convention). `slice(arr5, -100, 3)` normalizes to `slice(arr5, 0, 3)` → `[1, 2, 3]`. Matches Phase 14 D-01 silent-clamp tradition.

5. **Should Phase 20 update `examples/tutorial.flow` and `examples/showcase.flow` for the new behaviors?**
   - **RESOLVED → D-USER-E: Wait for Phase 26** (Tutorial + Showcase Refresh — QOL-04). Phase 20 ships only production code + xUnit Facts + new test_*.flow scripts. Tutorial inclusion is QOL-04 territory.

**Additional locked decisions (D-USER-F):** Migration items in 20-04 closure include FlowScriptData.cs:57 ExpectedErrorScripts removal and 14-deferred-items.md / 12-deferred-items.md strikethrough. The Phase14 EnharmonicTests rename + assertion-inversion lands inside the 20-02 atomic commit (NOT 20-04) because the atomic-commit / HEAD-never-broken invariant (Phase 12-02 / 18-01 precedent) requires the rename to land with the production code change — placing it in 20-04 would leave Phase14 facts RED across waves 2-3.

---

## Environment Availability

> Skipped — Phase 20 is pure code/config changes within existing C# codebase. No external dependencies, runtimes, services, or CLIs introduced beyond the already-confirmed .NET 10 + dotnet build chain.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (existing — `flow-lang.Tests` project) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (existing) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase20"` |
| Full suite command | `dotnet test flow-sharp.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DEFER-01 | `(range 0 5)` returns Array[Int] of length 5 with values 0..4 | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.TwoArg_DefaultStep"` | ❌ Wave 0 |
| DEFER-01 | `(range 0 10 2)` returns Array[Int] of length 5 with values 0,2,4,6,8 | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.ThreeArg_PositiveStep"` | ❌ Wave 0 |
| DEFER-01 | `(range 5 0 -1)` returns Array[Int] of length 5 with values 5..1 | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.NegativeStep_IteratesBackward"` | ❌ Wave 0 |
| DEFER-01 | `(range 0 0)` returns empty Array[Int] | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.EmptyWhenStartEqualsEnd"` | ❌ Wave 0 |
| DEFER-01 | `(range 5 0)` (default step +1, no progress) returns empty | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.UnsatisfiableReturnsEmpty"` | ❌ Wave 0 |
| DEFER-01 | `range` step=0 throws | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.ZeroStepThrows"` | ❌ Wave 0 |
| DEFER-01 | `test_custom_oscillator.flow` runs to completion (Test 4 unblocked) | integration | `dotnet test --filter "FullyQualifiedName~test_custom_oscillator"` | ✅ (FlowScriptData.cs entry must be REMOVED) |
| DEFER-04 | `enharmonic(E4)` returns `Fb4` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_E4_RespellsFb4"` | ❌ Wave 0 |
| DEFER-04 | `enharmonic(F4)` returns `E#4` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_F4_RespellsEsharp4"` | ❌ Wave 0 |
| DEFER-04 | `enharmonic(B4)` returns `Cb5` (octave +1) | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_B4_RespellsCb5"` | ❌ Wave 0 |
| DEFER-04 | `enharmonic(C4)` returns `B#3` (octave −1) | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_C4_RespellsBsharp3"` | ❌ Wave 0 |
| DEFER-04 | Round-trip pitch equivalence for every chromatic note | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.RoundTrip_PitchEquivalent"` | ❌ Wave 0 |
| DEFER-04 | Phase14/EnharmonicTests.cs NoKey_NaturalUnchanged_* MIGRATE (4 Facts) | unit | `dotnet test --filter "FullyQualifiedName~Phase14.EnharmonicTests"` | ✅ (Phase 14 file — UPDATE in plan) |
| DEFER-04 | D, G, A naturals remain unchanged | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_NonEdgeNaturalsUnchanged"` | ❌ Wave 0 |
| DEFER-05 | `(slice [1,2,3,4,5] -3 5)` returns `[3,4,5]` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Array_NegativeStart_FromEnd"` | ❌ Wave 0 |
| DEFER-05 | `(slice [1,2,3,4,5] 0 -1)` returns `[1,2,3,4]` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Array_NegativeEnd_FromEnd"` | ❌ Wave 0 |
| DEFER-05 | `slice` Sequence overload accepts negative-from-end | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Sequence_NegativeStart_FromEnd"` | ❌ Wave 0 |
| DEFER-05 | Existing Phase14/SliceTests.cs Facts continue to pass | unit | `dotnet test --filter "FullyQualifiedName~Phase14.SliceTests"` | ✅ (no migration — cases coincide) |
| DEFER-05 | Repository grep `slice.*,.*,.*-` empty in tests/ | manual | `grep -rn "slice.*,.*,.*-" tests/ \| wc -l` | ✅ verified at research time |
| All | Full xUnit suite GREEN | regression | `dotnet test flow-sharp.sln` | ✅ existing 340/340 baseline |
| All | All `tests/*.flow` Theory rows GREEN | integration | `dotnet test --filter "FullyQualifiedName~FlowScriptTests"` | ✅ existing harness |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase20"` (fast, ~2s for new Facts)
- **Per wave merge:** `dotnet test flow-sharp.sln` (full suite, ~17s baseline + new Facts)
- **Phase gate:** Full suite GREEN before `/gsd-verify-work`. Plus repository grep transcript in VERIFICATION.md.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase20/` directory creation
- [ ] `flow-lang.Tests/Unit/Phase20/RangeTests.cs` — covers DEFER-01 (6 Facts)
- [ ] `flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs` — covers DEFER-04 (≥6 Facts: 4 edges + round-trip Theory + non-edge naturals)
- [ ] `flow-lang.Tests/Unit/Phase20/SliceNegativeTests.cs` — covers DEFER-05 (≥3 Facts: array negative-start, array negative-end, sequence negative)
- [ ] `tests/test_range.flow` — `.flow` integration test (or extend existing `tests/test_collections.flow` if present)
- [ ] `tests/test_enharmonic_edges.flow` — `.flow` integration test (or extend `tests/test_enharmonic.flow` carefully — see Pitfall 2)
- [ ] `tests/test_slice_negative.flow` — `.flow` integration test (or extend `tests/test_slice.flow`)
- [ ] Framework install: NONE — existing xUnit infrastructure covers all phase requirements

---

## Security Domain

> Skipped — Phase 20 is closure work over a single-user CLI tool with no network surface, no auth, no user-supplied data beyond `.flow` source files.
>
> The single security-relevant note from PITFALLS.md is the "TPQN auto-elevation accepts unbounded LCM input" DoS — that's Phase 19 territory (TUP-06 capped at 9600). Phase 20 introduces zero new attack surface.

---

## Project Constraints (from CLAUDE.md)

CLAUDE.md mandates that affect Phase 20 plans:

- **GSD Workflow Enforcement:** All file edits go through GSD commands (`/gsd:execute-phase`, `/gsd:quick`, etc.). No direct repo edits.
- **C# Conventions:** .NET 10, nullable reference types, file-scoped namespaces, all under `FlowLang.*` namespace, AST nodes are records, pattern matching for dispatch.
- **Constraints:** Existing 70+ `.flow` test scripts MUST continue to work; tutorial.flow + showcase.flow byte-identical determinism contract holds (Phase 18 / Phase 19 regression gate).
- **Minimal Dependencies:** No new packages. Phase 20 explicitly adds zero.
- **Performance:** Real-time audio playback efficiency; "no GC pressure in hot paths". Phase 20 changes are all in score-build / parse-time paths, NOT in audio rendering. SAFE.
- **Conventions not yet established:** plans may follow Phase 12 / Phase 14 / Phase 18 / Phase 19 precedent freely.
- **Charitable Interpretation (MEMORY.md):** Prefer silent-and-documented assumptions over errors; music > rigid correctness. Applies to DEFER-05 silent-clamp post-normalization decision (Q4) and DEFER-04 D/G/A unchanged decision (A3).
- **Functional S-expression style (MEMORY.md):** All built-ins called via `(name args...)` form, no infix. Phase 20 does not introduce any new syntax — pure stdlib additions.

---

## Sources

### Primary (HIGH confidence)
- `flow-lang/StandardLibrary/Collections.cs` (357 lines, full read) — slice + range placement target
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (407 lines, full read) — Enharmonic implementation site
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:414-475` (RegisterCollections) — registration template
- `flow-lang/collections.flow` (26 lines, full read) — proc declaration template
- `flow-lang/std.flow` (158 lines, full read) — confirms enharmonic already declared at line 114
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:34, 121, 138, 175` — Parse/GetNoteValue/ToMidiNote/Format helpers
- `flow-lang.Tests/Unit/Phase14/SliceTests.cs` (130 lines, full read) — existing slice Facts; verified cases coincide
- `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs:27-145` — 4 NoKey_NaturalUnchanged_* Facts requiring migration
- `flow-lang.Tests/FlowScriptData.cs:1-110` — `["test_custom_oscillator.flow"]` ExpectedErrorScripts entry to remove; RequiredSentinels for slice/enharmonic empty (verified)
- `tests/test_slice.flow`, `tests/test_enharmonic.flow` — full read; verified Theory-row gating (errorCount==0 only)
- `.planning/REQUIREMENTS.md:49-51` — DEFER-01/04/05 acceptance verbatim
- `.planning/STATE.md:268-278` — Phase 20 next-up status, Phase 19 closure context
- `.planning/ROADMAP.md:107-116` — Phase 20 success criteria
- `.planning/research/ARCHITECTURE.md:400-432` — Feature 3, 5, 6 file-level touch maps (DEFER-01/04/05)
- `.planning/research/PITFALLS.md:327-362` — Pitfall 10 hidden dependencies; DEFER-05 breaking-change classification
- `.planning/phases/12-stability/deferred-items.md` — DEFER-01 fix proposal (3-step plan)
- `.planning/phases/14-composer-dx-part-1/deferred-items.md:101-167` — DEFER-04 + DEFER-06(=DEFER-05) origin
- CLAUDE.md (full read) — project constraints, conventions
- MEMORY.md (full read) — language philosophy, charitable interpretation precedents

### Secondary (MEDIUM confidence)
- Phase 14 plan 14-01 / 14-02 lessons re: stdlib registration + proc declaration coupling — confirmed by 14-deferred-items.md and STATE.md plan-14-02 entry
- Phase 18 / 19 atomic-commit pattern (RED + GREEN bundled per plan) — `STATE.md` plans 18-01..02, 19-01..05

### Tertiary (LOW confidence)
- None. All claims sourced from directly-read code or pinned design documents.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every C# class and method referenced verified at HEAD
- Architecture: HIGH — three independent stdlib closures, identical patterns to existing slice/take/drop/enharmonic
- Pitfalls: HIGH — eight pitfalls each with concrete grep-able warning signs and verified mitigations grounded in existing test infrastructure

**Research date:** 2026-04-26
**Valid until:** Phase 20 completion (no ecosystem changes affect this work — fully internal stdlib additions)

**Plan structure recommendation (final):** **3 atomic plans + 1 closure plan = 4 plans total**, executed in waves:
- **Wave 1 — Plan 20-01:** DEFER-01 `range` stdlib (lowest risk, pure addition)
- **Wave 2 — Plan 20-02:** DEFER-04 multi-letter enharmonic edges (medium risk, 4 Phase14 Facts migrate)
- **Wave 3 — Plan 20-03:** DEFER-05 slice negative-from-end (medium risk, behavioral change but Pattern 2 verification matrix shows existing tests coincide)
- **Wave 4 — Plan 20-04:** Closure docs (REQUIREMENTS.md Pending → Shipped; ROADMAP.md row update; STATE.md milestone progress; 20-VERIFICATION.md rollup; 14-deferred-items.md DEFER-04 + DEFER-06 strikethrough per handling protocol §3)

Plans 20-01, 20-02, 20-03 can technically run **in parallel** (zero file overlap: Collections.cs is shared between 20-01 (Range method) and 20-03 (Slice* edits) but the edits are non-overlapping; HarmonyFunctions.cs is touched only by 20-02). Recommend **sequential** by dependency clarity — Wave 1 → Wave 2 → Wave 3 → Wave 4 — matching v1.3 binding pre-ordering #3 (DEFER-04 closure must precede Phase 21).
