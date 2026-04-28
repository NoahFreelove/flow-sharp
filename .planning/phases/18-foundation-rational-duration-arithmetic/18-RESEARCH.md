# Phase 18: Foundation — Rational Duration Arithmetic — Research

**Researched:** 2026-04-26
**Domain:** Numeric primitive (rational fraction) + minimal data-class extension (MusicalNoteData)
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from milestone-level decisions; no per-phase CONTEXT.md exists yet)

### Locked Decisions
- **D-01 (v1.3):** Tuplet bracket syntax is `{N:M ...}` (braces) — affects Phase 19, NOT Phase 18, but Fraction must support these ratios.
- **D-05 (v1.3):** MIDI TPQN cap when tuplets force auto-elevation is 9600 — affects Phase 19, not Phase 18 directly.
- **Hand-rolled `Fraction` struct (~50 LOC) at `flow-lang/TypeSystem/Fraction.cs`.** NO external dependencies (Fractions / Rationals / BigRational rejected as overkill per ARCHITECTURE.md §3 + STACK.md guiding principle).
- **`MusicalNoteData.DurationFraction` is an OPTIONAL field that OVERRIDES `DurationValue` enum when set.** Existing power-of-2 path stays unchanged when null. Architecture pinned in ARCHITECTURE.md §3.
- **Byte-identical determinism contract is binding.** All ~70 existing `.flow` test scripts + `examples/tutorial.flow` + `examples/showcase.flow` must produce byte-identical WAV+MIDI before/after Phase 18 (regression gate via `cmp` — same gate Phases 15/16 used).
- **Stack:** .NET 10, C# 13, file-scoped namespaces, `record` types preferred for immutable data. No new NuGet packages.

### Claude's Discretion
- **Fraction API surface:** exact operators / methods (within minimum-for-FRAC-01 + Phase 19 needs).
- **GCD algorithm:** Euclidean recursive vs iterative vs Stein binary — pick simplest correct.
- **Type-system placement:** primitive helper struct (NOT a `FlowType`) vs registered `FlowType`.
- **MusicalNoteData ctor shape:** 13-param ctor vs builder vs With-method (preserving byte-identity is the binding constraint).
- **`GetBeats` return type:** stay `double` (lossy at consumption boundary) vs change to `Fraction` (lossless) — see Open Question 1.
- **Test layout:** `flow-lang.Tests/Unit/Phase18/` directory matching v1.2 convention.

### Deferred Ideas (OUT OF SCOPE for Phase 18)
- Tuplet `{N:M ...}q` parser & AST (`TupletElement`) — Phase 19 (TUP-01..08).
- `C4/12` arbitrary-fractional-duration lexer/parser changes — Phase 19 (TUP-04).
- MIDI tick auto-elevation, TPQN cap of 9600 — Phase 19 (TUP-06).
- Bar-fit validator extensions for tuplet sums (Pitfall 2) — Phase 19 (TUP-05).
- Migrating `GetBeats` callers (BarRenderer, MidiExport, etc.) to consume `Fraction` instead of `double` — explicitly downstream; Phase 18 only adds the storage and wiring at `MusicalNoteData.GetBeats`.
- `TransformFunctions` augment/diminish re-validation against tuplet-aware sequences (Pitfall 9, TUP-07) — Phase 19.
- Promoting `MusicalNoteData` from class → record — would change construction syntax across 30+ call sites and risks byte-identity. Keep as class.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FRAC-01 | `Fraction(int Num, int Denom)` value type at `flow-lang/TypeSystem/Fraction.cs`; normalizes via GCD on construction; supports +, *, ==, <; NEVER uses `double`. Ships with unit Facts pinning `1/3 + 1/3 + 1/3 == 1`, `2/4 == 1/2`, `3/12 == 1/4`. | §1 Standard Stack — `readonly record struct Fraction`; §3 Architecture Patterns — Fraction API surface; §6 Code Examples |
| FRAC-02 | `MusicalNoteData` gains optional `Fraction? DurationFraction` field that overrides `DurationValue` enum when set. Power-of-2 path unchanged when null. All ~70 `.flow` tests + tutorial.flow + showcase.flow must remain byte-identical (cmp regression gate). | §3 Architecture Patterns — additive-field migration; §4 Don't Hand-Roll; §5 Common Pitfalls — Pitfall 1 (silent enum-path regression); §7 Validation Architecture — cmp regression gate |
</phase_requirements>

---

## Summary

Phase 18 ships a hand-rolled `Fraction(int Num, int Denom)` value type and threads an optional `Fraction? DurationFraction` field through `MusicalNoteData`. This is **foundation** work — neither requirement directly produces visible musical output. Phase 18's success criterion is the inverse of every other phase: **nothing existing should change**. The new code paths must be wired but dormant until Phase 19 (Tuplets) starts feeding non-null `DurationFraction` values into them.

The hardest engineering problem is **not** writing the Fraction struct (it's ~60 LOC of standard rational arithmetic) — it's preserving byte-identical determinism across the existing `.flow` test corpus. Adding a 13th parameter to `MusicalNoteData`'s constructor touches >30 call sites in `NoteStreamCompiler`, `TransformFunctions`, `HarmonyFunctions`, `VariationFunctions`, `BuiltInFunctions`, and `ProgressionCompiler`. Every site must continue passing the same logical args; the new parameter must default to `null`. If even one site accidentally widens or narrows behavior, the WAV/MIDI cmp gate fires and the phase regresses.

**Primary recommendation:** Land Fraction first (FRAC-01) as a self-contained commit with unit-Fact coverage. Land DurationFraction wiring second (FRAC-02) using a **defaulted-parameter** pattern at the constructor — no call site change required for any of the 30+ existing `new MusicalNoteData(...)` sites. Verify the cmp gate after each commit independently.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Rational arithmetic primitive (Fraction) | TypeSystem (helper struct) | — | Sibling of `ArrayType`; pure value type; not a `FlowType` because it's never spelled by users in source — it's a C# implementation detail (per ARCHITECTURE.md §3 "does NOT need to be a `FlowType`") |
| Optional duration override | TypeSystem.SpecialTypes (MusicalNoteData) | Runtime (NoteStreamCompiler, BarRenderer, MidiExport — read-only via GetBeats) | Storage at the data class; consumers branch on `DurationFraction.HasValue` inside `GetBeats` — no consumer code changes |
| Beats math (GetBeats) | TypeSystem.SpecialTypes (MusicalNoteData) | — | Single chokepoint; ARCHITECTURE.md §3 confirms this is "load-bearing — touches one chokepoint" |
| Byte-identical regression gate | Tests (xunit Facts + cmp shell smoke) | — | Pin via existing `Phase15/EuclideanByteIdenticalTests.cs` pattern + `cmp` two-run smoke on tutorial.flow + showcase.flow |

---

## Standard Stack

### Core (Existing — No Changes)
| Tech | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | [VERIFIED: codebase] Already in use; csproj pins `<TargetFramework>net10.0</TargetFramework>` |
| C# 13 | latest | Language | [VERIFIED: codebase] Record types, file-scoped namespaces, pattern matching already used throughout |
| xUnit.v3 | 3.2.2 | Test framework | [VERIFIED: flow-lang.Tests.csproj line 13] Existing test infra; `[Fact]` and `[Theory]` patterns in 280+ existing Facts |
| xunit.runner.visualstudio | 3.1.5 | Test adapter | [VERIFIED: csproj line 14] Plan 12-01 substituted this for the non-existent `xunit.v3.runner.visualstudio` per RETROSPECTIVE log |

### New
| Tech | Version | Purpose | Why |
|------|---------|---------|-----|
| `readonly record struct Fraction` | hand-rolled | Rational arithmetic primitive | [VERIFIED: PITFALLS.md Pitfall 1 + ARCHITECTURE.md §3] No external dep; ~60 LOC; stack-allocated (no GC pressure); `int Num` + `int Denom` covers Flow's needs (durations stay in `int` range — PPQN 480 × 1024 bars × 32 = ~1.5e7) |

### Alternatives Considered & Rejected
| Instead of | Could Use | Tradeoff | Verdict |
|------------|-----------|----------|---------|
| Hand-rolled `Fraction` | `System.Numerics.BigInteger` num/denom | Overkill — values stay in int range | [REJECTED — ARCHITECTURE.md §3] |
| Hand-rolled `Fraction` | NuGet `Fractions` / `Rationals` / `BigRational` | Violates STACK.md "minimal dependencies" + zero-NuGet rule | [REJECTED — REQUIREMENTS.md "No new NuGet packages of any kind"] |
| `readonly record struct` | `class Fraction` | Heap allocation, GC pressure in hot paths (BarRenderer per-note); record-class semantics arguably overkill | [REJECTED — PITFALLS.md §11 Open Risk #1] |
| `readonly record struct` | `readonly struct Fraction` (manual Equals) | More boilerplate; record gives `==` and `GetHashCode` for free | Defer — record struct preferred unless toolchain issues surface |

### No Installation Required
No new NuGet packages. No `dotnet add package` step.

---

## Architecture Patterns

### Pattern 1: Additive-Field Migration via Defaulted Parameter
**What:** Add a new optional parameter at the END of an existing constructor signature, defaulted to `null`. All existing call sites continue to compile and execute identically.

**When to use:** Adding optional state to a class with many existing construction sites where every call site must remain byte-identical in behavior.

**Why this matters for FRAC-02:** `new MusicalNoteData(...)` appears in 30+ locations across `NoteStreamCompiler.cs`, `TransformFunctions.cs`, `HarmonyFunctions.cs`, `VariationFunctions.cs`, `BuiltInFunctions.cs`, `ProgressionCompiler.cs`. A defaulted `Fraction? durationFraction = null` parameter means **zero call-site edits** — the byte-identical gate becomes a structural certainty rather than a per-site audit.

**Example (canonical shape):**
```csharp
// Source: ARCHITECTURE.md §3 + grep of existing flow-lang/TypeSystem/SpecialTypes/NoteType.cs:234
public MusicalNoteData(
    char noteName, int octave, int alteration, int? durationValue,
    bool isRest,
    double? centOffset = null, bool isTied = false, double velocity = 0.63,
    Articulation articulation = Articulation.Normal, bool isDotted = false,
    SourceLocation? sourceLocation = null, int sourceLength = 0,
    Fraction? durationFraction = null)   // NEW — defaulted, last position
{
    // ... existing assignments unchanged ...
    DurationFraction = durationFraction;
}
```

[CITED: existing NoteType.cs lines 234-248] — current ctor already uses defaulted parameters for `centOffset`, `isTied`, `velocity`, `articulation`, `isDotted`, `sourceLocation`, `sourceLength`. Adding a 13th defaulted parameter follows the same pattern.

### Pattern 2: GetBeats Branch on HasValue
**What:** Single chokepoint method (`GetBeats`) decides which duration representation to use. Existing path runs when `DurationFraction == null`; new path runs when set.

**Code shape:**
```csharp
// Source: ARCHITECTURE.md §3 (extends existing NoteType.cs:253-261)
public double GetBeats(int timeSigDenominator)
{
    if (DurationFraction.HasValue)
    {
        // Fraction is in QUARTER-NOTE units (matches music21 convention)
        // beats = quarterNotes × (timeSigDenominator / 4)
        var f = DurationFraction.Value;
        return (double)f.Num * timeSigDenominator / (f.Denom * 4.0);
    }
    // Existing power-of-2 path — UNCHANGED
    if (!DurationValue.HasValue) return 1.0;
    double fraction = NoteValueType.ToFraction((NoteValueType.Value)DurationValue.Value);
    if (IsDotted) fraction *= 1.5;
    return fraction * timeSigDenominator;
}
```

**Why this preserves byte-identity:** When `DurationFraction == null` (the only state Phase 18 ever produces — no syntax exists yet to set it), the existing `if (!DurationValue.HasValue) return 1.0; ...` branch executes identically to today. All 21 GetBeats call sites (verified via grep — see §6) read the same `double` they read today.

### Pattern 3: Fraction Normalization on Construction
**What:** Every `Fraction(num, denom)` immediately reduces to lowest terms via GCD; sign carried on numerator; zero denominator throws.

**Why:** `2/4` and `1/2` must hash and compare equal. `3/12` and `1/4` must compare equal. FRAC-01 acceptance Facts pin this directly.

**Example (canonical):**
```csharp
// Source: standard rational-number normalization; GCD impl mirrors PolyrhythmFunctions.cs:117
public readonly record struct Fraction
{
    public int Num { get; }
    public int Denom { get; }

    public Fraction(int num, int denom)
    {
        if (denom == 0)
            throw new DivideByZeroException("Fraction denominator cannot be zero.");
        // Normalize sign onto numerator
        if (denom < 0) { num = -num; denom = -denom; }
        int g = Gcd(Math.Abs(num), denom);
        Num = num / g;
        Denom = denom / g;
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    public static Fraction operator +(Fraction l, Fraction r) =>
        new(l.Num * r.Denom + r.Num * l.Denom, l.Denom * r.Denom);
    public static Fraction operator *(Fraction l, Fraction r) =>
        new(l.Num * r.Num, l.Denom * r.Denom);
    public static bool operator <(Fraction l, Fraction r) =>
        l.Num * r.Denom < r.Num * l.Denom;
    public static bool operator >(Fraction l, Fraction r) => r < l;
    // == and != come free from `record struct`
    public override string ToString() => $"{Num}/{Denom}";
}
```

[CITED: PolyrhythmFunctions.cs:117] — existing GCD pattern in this codebase: `private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);` Reuse this idiom verbatim for stylistic consistency.

### Anti-Patterns to Avoid
- **Promoting `MusicalNoteData` from class to record:** [VERIFIED: ARCHITECTURE.md §3 + grep] Would change construction semantics (positional `with` syntax, value equality vs reference equality) at every existing call site. Phase 18 keeps it as `class` — promotion can happen in a future cleanup phase if ever needed.
- **Adding `DurationFraction` as a required parameter:** Forces edits at 30+ call sites; each edit is a byte-identity risk. Defaulted parameter eliminates the risk structurally.
- **Using `double DurationScale` instead of `Fraction`:** [REJECTED — ARCHITECTURE.md §3 + PITFALLS.md Pitfall 1] Floating-point drift accumulates at ~1e-15 per op. Three triplet quarters at TPQN=480 truncate to 479 ticks instead of 480 (loses 1 tick per beat). The whole point of Phase 18 is to AVOID `double` arithmetic for tuplet-tier math.
- **Registering Fraction as a FlowType (PrimitiveType / SpecialType):** [VERIFIED: ARCHITECTURE.md §3 "does NOT need to be a `FlowType`"] Users never write `Fraction f = ...;` in `.flow` source. It's a C# implementation primitive, sibling to `ArrayType.cs` in the `flow-lang/TypeSystem/` directory but not part of the user-facing type lattice.
- **Implementing `Fraction.Reduce()` as a separate public method:** Reduction must happen in the constructor. A separate method invites callers to forget to call it, producing un-normalized fractions that compare unequal to their normalized form (`2/4 != 1/2`).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Modular GCD impl | A new GCD helper class | Inline `Gcd` static method | [VERIFIED: PolyrhythmFunctions.cs:117] One already exists in the codebase; copy the 1-line idiom |
| Equality + hash on Fraction | Manual `Equals` / `GetHashCode` overrides | `readonly record struct` | C# 13 generates value-equality automatically for record structs |
| Operator-overload boilerplate | `Add(Fraction)`, `Multiply(Fraction)` named methods | Operator overloads (`+`, `*`, `<`) | More idiomatic; pinned in FRAC-01 acceptance via `1/3 + 1/3 + 1/3 == 1` |
| Test infra | New test project / framework | Existing `flow-lang.Tests` xUnit.v3 setup with `Phase18/` subdirectory | [VERIFIED: ls flow-lang.Tests/Unit/] Phase14, Phase15, Phase17 dirs already exist |
| Byte-identical regression harness | New cmp wrapper | Existing `EuclideanByteIdenticalTests.cs` pattern (Phase 15 Plan 05) | [VERIFIED: flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs] Two `FlowEngineRunner` instances, run twice, byte-equal assertion. Apply same pattern to a Phase 18 smoke fixture |

**Key insight:** Every primitive Phase 18 needs already exists in the codebase or in the BCL. The C# value-type ecosystem and existing GCD idiom reduce FRAC-01 to ~60 LOC of straightforward code. The hand-roll budget is small precisely because hand-rolling is the right answer here.

---

## Common Pitfalls

### Pitfall 1: Silent regression of the existing power-of-2 path

**What goes wrong:** A subtle change in `GetBeats` (e.g. reordering branches, changing the dotted-multiplier expression, accidentally storing `DurationFraction` for non-tuplet notes) changes the `double` returned for an existing test, which propagates to BarRenderer's sample count, which shifts a single sample in the WAV — `cmp` fires, regression detected.

**Why it happens:** GetBeats is read by 21 sites across BarRenderer, MidiExport, BarType, ClassicalComposition, MusicalConversions, VisualizationFunctions. Any drift is amplified by the SongRenderer's render-then-mix pipeline.

**How to avoid:**
1. **Add `DurationFraction` to MusicalNoteData CTOR with default `= null`. Set it ONLY in code paths that exist after Phase 19 lands.** Phase 18 must not produce a single non-null DurationFraction in the wild.
2. **Test the GetBeats branch shape explicitly:** unit Fact `GetBeats_DurationFractionNull_ReturnsExistingPath` asserts identical behavior to today.
3. **Run the cmp gate after each commit:** `dotnet run --project flow-interpreter examples/tutorial.flow && cmp examples/output/flow_tutorial.wav baseline_tutorial.wav` — any mismatch is a STOP.
4. **Capture the baseline FIRST** before touching `MusicalNoteData`: Wave 0 task captures `examples/output/{tutorial,showcase}.{wav,mid}` to `.planning/phases/18-foundation-rational-duration-arithmetic/baseline/`. All later tasks compare against this baseline.

**Warning signs:**
- Any GetBeats-using test (BarRenderer, MidiExport sample-count, total-duration arithmetic) fails AFTER Phase 18 lands but BEFORE Phase 19 starts.
- `cmp examples/output/flow_tutorial.wav baseline/flow_tutorial.wav` reports any byte difference.
- A single `.flow` script in `tests/test_*.flow` produces different stdout (sentinel mismatch).

### Pitfall 2: Constructor parameter ordering breaks existing call sites silently

**What goes wrong:** Adding `Fraction? durationFraction` in the MIDDLE of the parameter list (e.g. between `isDotted` and `sourceLocation`) silently shifts which positional argument is which at call sites that pass parameters positionally.

**Why it happens:** [VERIFIED: grep results] Many call sites use positional arguments (e.g. `TransformFunctions.cs:127` passes 12 positional args). C# does NOT warn when positional args pass type-check but mean different things.

**How to avoid:** **Append the new parameter at the END of the parameter list**, after `sourceLength`. Since it's nullable and defaulted, no call site needs to provide it. Any future caller that wants to set it can do so by name.

**Warning signs:** Build succeeds but tests start producing different MusicalNoteData fields than expected (e.g. wrong velocity values, wrong source length).

### Pitfall 3: Fraction with zero denominator silently treated as `0/0` or NaN-like state

**What goes wrong:** A user-input or tuplet-parser bug produces `new Fraction(1, 0)` and the struct silently stores it; later arithmetic divides by zero, producing meaningless values that don't immediately crash but propagate as corrupt durations.

**How to avoid:** Constructor throws `DivideByZeroException` (or `ArgumentException`) immediately. FRAC-01 acceptance Facts should include a negative test: `Assert.Throws<DivideByZeroException>(() => new Fraction(1, 0))`.

### Pitfall 4: Integer overflow in Fraction arithmetic

**What goes wrong:** `Fraction(1, 999983) + Fraction(1, 999979)` computes `(999979 + 999983) * (999983 * 999979)` for the un-reduced numerator/denominator before normalization. `999983 * 999979 ≈ 9.99e11` — overflows `int.MaxValue` (~2.15e9), producing nonsense.

**How to avoid:** For Phase 18 acceptance Facts, all denominators stay small (3, 4, 12). Document the int-range constraint explicitly: `Fraction is bounded by int range; denominators above ~46340 may overflow under multiplication. Phase 19's tuplet denominators stay ≤ 13 per D-05's TPQN cap interaction.` If overflow becomes a problem, switch to `long` num/denom (single-line edit).

**Warning signs:** A unit Fact like `(2 ^ 31 - 1) / 1 + 1/1` produces a negative result. Not realistic for Phase 18 inputs but worth documenting.

### Pitfall 5: ToString format collides with NoteValueType.Format

**What goes wrong:** Someone refactors `MusicalNoteData.ToString()` to surface `DurationFraction` and the diagnostic output of all 70+ test scripts changes (each test script's stdout shifts, sentinel mismatches everywhere).

**How to avoid:**
- `Fraction.ToString()` returns `"3/4"` style — concise, human-readable.
- `MusicalNoteData.ToString()` is **NOT modified** in Phase 18. Only the new field exists; ToString continues to format from `DurationValue` (existing path). When `DurationFraction` is non-null (Phase 19+), ToString can be updated, but Phase 18 keeps ToString byte-identical.

**Warning signs:** Any test script's stdout changes character-for-character.

---

## Code Examples

Verified patterns from existing codebase + canonical references:

### Existing GCD idiom (mirror for Fraction)
```csharp
// Source: flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs:117-118
private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
private static int Lcm(int a, int b) => a / Gcd(a, b) * b;
```

### Existing defaulted-parameter ctor pattern (mirror for FRAC-02)
```csharp
// Source: flow-lang/TypeSystem/SpecialTypes/NoteType.cs:234
public MusicalNoteData(
    char noteName, int octave, int alteration, int? durationValue,
    bool isRest,
    double? centOffset = null, bool isTied = false, double velocity = 0.63,
    Articulation articulation = Articulation.Normal, bool isDotted = false,
    FlowLang.Core.SourceLocation? sourceLocation = null, int sourceLength = 0)
```

### Existing byte-identical Fact pattern (mirror for the cmp regression gate)
```csharp
// Source: flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs (referenced from 15-05-SUMMARY.md)
[Fact]
public void TwoConsecutiveRuns_ProduceIdenticalBytes()
{
    using var runner1 = new FlowEngineRunner();
    var bytes1 = runner1.RunSourceCapturingMidi(SourceText);

    using var runner2 = new FlowEngineRunner();
    var bytes2 = runner2.RunSourceCapturingMidi(SourceText);

    Assert.Equal(bytes1, bytes2);
}
```

### Canonical FRAC-01 acceptance Facts (pinned by REQUIREMENTS.md)
```csharp
// Source: REQUIREMENTS.md FRAC-01 acceptance examples
[Fact] public void TripletThirds_SumToOne() {
    var third = new Fraction(1, 3);
    Assert.Equal(new Fraction(1, 1), third + third + third);
}
[Fact] public void TwoFourths_NormalizeToOneHalf() {
    Assert.Equal(new Fraction(1, 2), new Fraction(2, 4));
}
[Fact] public void ThreeTwelfths_NormalizeToOneFourth() {
    Assert.Equal(new Fraction(1, 4), new Fraction(3, 12));
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `double DurationFraction = 2.0/3.0` for triplets | `Fraction(2, 3)` rational arithmetic | Industry-standard since music21 (~2008); Lilypond uses similar `Moment` rational; flow-sharp v1.3 catches up | Avoids ε-drift across thousand-bar pieces; exact MIDI tick conversion |
| Extending `NoteValue` enum with `TUPLET_THIRD`, `TUPLET_FIFTH`, ... | Generic `Fraction? DurationFraction` field | music21 DurationTuple model (canonical) | Composes under nesting; works for arbitrary ratios; no combinatorial enum explosion |

**Deprecated/outdated:**
- Adding tuplet support by extending `NoteValueType.Value` enum — [REJECTED ARCHITECTURE.md §3] doesn't compose under nested tuplets; explodes combinatorially with dotted-tuplets.

---

## Project Constraints (from CLAUDE.md)

Verbatim directives that bind Phase 18 plans:

- **Static typing with type inference** — Fraction is statically typed; `int Num` / `int Denom` explicit.
- **Music-specific types … Note, Chord, Song, etc.** — Fraction is a HELPER, not a music-specific type. Don't add it to the user-facing type lattice (FRAC-01 says `flow-lang/TypeSystem/`, not `flow-lang/TypeSystem/SpecialTypes/`).
- **All AST nodes are C# `record` types (immutable)** — no AST changes in Phase 18, but the precedent guides Fraction to be a `readonly record struct`.
- **File-scoped namespaces throughout** — `namespace FlowLang.TypeSystem;` for `Fraction.cs`.
- **External dependency: Pidgin parser combinator (referenced but unused)** — do NOT add new dependencies for Fraction.
- **Existing .flow scripts and test suite must continue to work** — binding constraint; this IS the byte-identical determinism contract.
- **No GC pressure in hot paths** — `readonly record struct` is stack-allocated; avoid `class Fraction`.
- **GSD Workflow Enforcement: do not make direct repo edits outside a GSD workflow** — applies to plan execution, not research.

---

## Runtime State Inventory

> Phase 18 is greenfield code addition. No rename, no refactor, no migration. Skipping per RESEARCH template guidance — no runtime state survives outside the codebase that would need updating.

**Stored data:** None — no databases or datastores reference Phase 18 entities (Fraction, DurationFraction).
**Live service config:** None — no external services configured for Flow.
**OS-registered state:** None.
**Secrets/env vars:** None.
**Build artifacts:** Standard `bin/` and `obj/` rebuild on `dotnet build` — no special handling needed. `.planning/phases/18-.../baseline/` will hold pre-Phase-18 WAV/MIDI for cmp comparison; this is a one-shot capture, not persistent state.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build / test | ✓ | net10.0 | — |
| `dotnet` CLI | Build / test | ✓ | (system-installed) | — |
| `cmp` | Byte-identical regression gate | ✓ | (POSIX util, in glibc/coreutils) | — |
| PulseAudio | Tutorial.flow / showcase.flow execution | (existing) | — | Existing fallback: `IAudioBackend` abstraction silences when unavailable |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase18" --no-build` |
| Full suite command | `dotnet test flow-sharp.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FRAC-01 | `Fraction(1,3) + Fraction(1,3) + Fraction(1,3) == Fraction(1,1)` | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.TripletThirds_SumToOne"` | ❌ Wave 0 |
| FRAC-01 | `Fraction(2,4) == Fraction(1,2)` (normalization on construction) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.TwoFourths_NormalizeToOneHalf"` | ❌ Wave 0 |
| FRAC-01 | `Fraction(3,12) == Fraction(1,4)` (normalization with non-trivial GCD) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.ThreeTwelfths_NormalizeToOneFourth"` | ❌ Wave 0 |
| FRAC-01 | `Fraction(1,3) * Fraction(1,4) == Fraction(1,12)` | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.MultiplicationProducesProduct"` | ❌ Wave 0 |
| FRAC-01 | `Fraction(1,3) < Fraction(1,2)` (comparison without `double`) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.LessThanIsRational"` | ❌ Wave 0 |
| FRAC-01 | `new Fraction(1, 0)` throws DivideByZeroException | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.ZeroDenominator_Throws"` | ❌ Wave 0 |
| FRAC-01 | Negative-denominator normalization (sign on numerator) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.NegativeDenom_SignOnNumerator"` | ❌ Wave 0 |
| FRAC-01 | `Fraction(3,4).ToString() == "3/4"` | unit | `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests.ToString_FormatNumSlashDenom"` | ❌ Wave 0 |
| FRAC-02 | `MusicalNoteData` ctor accepts optional `durationFraction` parameter (compile + assignment) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.MusicalNoteDataTests.DurationFraction_OptionalCtorParam"` | ❌ Wave 0 |
| FRAC-02 | `GetBeats` returns existing-path value when `DurationFraction == null` (regression pin) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.MusicalNoteDataTests.GetBeats_DurationFractionNull_UsesEnumPath"` | ❌ Wave 0 |
| FRAC-02 | `GetBeats` returns Fraction-derived value when `DurationFraction != null` (e.g., `Fraction(1,3)` × timeSig 4 = 4/3 beats) | unit | `dotnet test --filter "FullyQualifiedName~Phase18.MusicalNoteDataTests.GetBeats_DurationFractionSet_OverridesEnum"` | ❌ Wave 0 |
| FRAC-02 | tutorial.flow produces byte-identical WAV vs. pre-Phase-18 baseline | integration | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorialTests.Tutorial_TwoRunsProduceIdenticalWav"` | ❌ Wave 0 |
| FRAC-02 | tutorial.flow produces byte-identical MIDI vs. pre-Phase-18 baseline | integration | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorialTests.Tutorial_TwoRunsProduceIdenticalMidi"` | ❌ Wave 0 |
| FRAC-02 | showcase.flow produces byte-identical WAV vs. pre-Phase-18 baseline | integration | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcaseTests.Showcase_TwoRunsProduceIdenticalWav"` | ❌ Wave 0 |
| FRAC-02 | showcase.flow produces byte-identical MIDI vs. pre-Phase-18 baseline | integration | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcaseTests.Showcase_TwoRunsProduceIdenticalMidi"` | ❌ Wave 0 |
| FRAC-02 | All 54 `tests/test_*.flow` scripts run to completion (regression smoke) | integration | Use existing `flow-lang.Tests/FlowScriptTests.cs` Theory-based harness — automatic coverage | ✅ existing |

### Sampling Rate
- **Per task commit:** `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase18"` (~1-3s expected for unit tests; ~30-60s for integration tests with cmp).
- **Per wave merge:** `dotnet test flow-sharp.sln` (full suite, ~17s based on Phase 17 baseline of 287 tests).
- **Phase gate:** Full suite green + manual cmp smoke verifying baseline `examples/output/flow_tutorial.{wav,mid}` and `examples/output/flow_showcase.{wav,mid}` against pre-Phase-18 captures.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase18/FractionTests.cs` — covers FRAC-01 acceptance + normalization + arithmetic + edge cases
- [ ] `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` — covers FRAC-02 ctor wiring + GetBeats branching
- [ ] `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` — covers FRAC-02 byte-identity gate against tutorial.flow
- [ ] `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — covers FRAC-02 byte-identity gate against showcase.flow
- [ ] `.planning/phases/18-foundation-rational-duration-arithmetic/baseline/` — capture pre-Phase-18 WAV+MIDI as binary fixtures for cmp comparison (one-shot Wave 0 task)

*(Framework install: not needed — xunit.v3 + xunit.runner.visualstudio + Microsoft.NET.Test.Sdk already pinned in csproj at lines 12-14.)*

---

## Open Questions

### 1. Should `GetBeats` return `Fraction` instead of `double`?

**What we know:**
- Current signature: `public double GetBeats(int timeSigDenominator)` — returns `double`.
- 21 call sites (per grep) consume the `double`. Many do further `double` arithmetic (sample-count math in `BarRenderer:51,260`; tick math in `MidiExport:184,195,216,259`; sum aggregation in `BarType:141,152,169`).
- ARCHITECTURE.md §3 chose to keep `double` at the consumption boundary: "Keep `double` only at the audio-rendering boundary where samples-per-note is computed."

**What's unclear:**
- Whether Phase 19's tuplet bar-fit validator (TUP-05) needs `Fraction`-typed beats to detect "exact fit" vs "drift" cleanly. If yes, GetBeats SHOULD return Fraction (or a sibling `GetBeatsFraction` should be added).

**Recommendation:** **Keep `GetBeats` returning `double` in Phase 18.** Add a sibling `GetBeatsFraction(int timeSigDenominator) → Fraction?` method that returns the rational form when `DurationFraction` is non-null, else `null` (so callers know to fall back to the double path). Phase 19 can adopt `GetBeatsFraction` for its bar-fit validator. This minimizes Phase 18's blast radius — zero changes to existing 21 GetBeats consumers.

**Risk if wrong:** Phase 19 plan discovers it needs Fraction-typed beats and has to retrofit `GetBeatsFraction` post-hoc. Low risk — the addition is mechanical and additive.

### 2. Should `Fraction.ToString()` simplify trivial cases (e.g. `1/1` → `"1"`)?

**What we know:**
- `Fraction(1, 1).ToString() == "1/1"` is unambiguous but verbose.
- `MusicalNoteData.ToString()` is unchanged in Phase 18, so user-facing diagnostic output isn't affected yet.

**Recommendation:** **Always emit `Num/Denom`** (i.e. `"1/1"`, not `"1"`). Predictable, parseable, and Phase 19's tuplet diagnostic prose can wrap it (e.g. `"tuplet ratio 3/2"`). Avoid conditional formatting that would surprise Phase 19's plan-time ToString assertions.

### 3. Should the cmp baseline be captured automatically via a script or manually?

**What we know:**
- Phase 15 / 16 used manual `cmp` runs documented in plans.
- Wave 0 task could either (a) hand-capture baselines and commit them as binary fixtures, or (b) capture-then-immediately-compare in a single Fact (no committed baseline).

**Recommendation:** **Option (a) — commit binary baselines** at `.planning/phases/18-.../baseline/{tutorial,showcase}.{wav,mid}`. Provides an auditable artifact, mirrors Phase 17's `tests/__fixtures__/` pattern (the LSP semantic-tokens golden test). Caveat: WAV files are ~5MB each — verify with the user that `.planning/phases/18-.../baseline/` is acceptable for git. If not, fall back to (b) — runtime capture with two `FlowEngineRunner` instances and direct `Assert.Equal(bytes1, bytes2)` comparison (the Phase 15 EuclideanByteIdentical pattern).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | All 30+ existing `new MusicalNoteData(...)` call sites use positional or trailing-named parameters such that appending `Fraction? durationFraction = null` at the end of the ctor signature requires zero call-site edits | §3 Pattern 1 | LOW — mitigated by C# compiler errors at any call site that breaks; mitigation is mechanical |
| A2 | The 54 `tests/test_*.flow` scripts + tutorial.flow + showcase.flow form an acceptable proxy for "all 70+ existing .flow tests" mentioned in FRAC-02 | §6 Validation | LOW — actual count is `ls tests/test_*.flow \| wc -l = 54`; FRAC-02 said "~70" loosely. Verify count with user before pinning a number in plan acceptance |
| A3 | Committing 5MB WAV/MIDI baselines under `.planning/phases/18-.../baseline/` is acceptable to the user for the byte-identical regression gate | §11 Open Q 3 | MEDIUM — if user objects to repo binary fixtures, fall back to two-runner Fact pattern (no baseline committed) — no functional difference |
| A4 | `int` num/denom is sufficient for all Phase 18 + Phase 19 tuplet ratios (3:2, 5:4, 7:8, 11:13 etc. with TPQN auto-elevation up to 9600) | §6 Pitfall 4 | LOW — denominators stay in single/double digits; numerator products in `+` op stay well under int.MaxValue. Switch to `long` is a single-line edit if needed |
| A5 | `readonly record struct` for Fraction generates appropriate `==` / `!=` / `GetHashCode` such that `Fraction(2,4) == Fraction(1,2)` works correctly **after** the constructor normalizes | §3 Pattern 3 | NONE — `readonly record struct` value-equality compares fields; since both fractions get normalized in the ctor, both have identical Num/Denom fields, so `==` returns true. Verified by FRAC-01 acceptance Fact |
| A6 | `MusicalNoteData` does NOT need to be promoted to a record type for FRAC-02 to work — defaulted-parameter ctor at `class` is sufficient | §3 Pattern 1 + §3 Anti-Patterns | LOW — record promotion would be an additional refactor; keeping as class preserves all 30+ call sites byte-identically |
| A7 | The FRAC-01 negative-denom normalization rule is `if (denom < 0) { num = -num; denom = -denom; }` — sign carried on numerator | §3 Pattern 3 | LOW — standard convention; if alternate (sign on denominator) is preferred, single-line change |
| A8 | xunit.v3 3.2.2 supports `record struct` parameters in `[InlineData]` Theory rows | §6 Validation Wave 0 | LOW — fall back to `[Fact]` per case if Theory unsupported; minor verbosity cost only |

**Note on assumption confirmation:** Items A1, A2, A4, A5 are LOW risk and can proceed without user confirmation. Item A3 (5MB binary fixtures) MEDIUM risk — discuss-phase or planner should confirm. Item A8 trivially fixable.

---

## Sources

### Primary (HIGH confidence)
- Codebase: `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:211-279` (MusicalNoteData class + GetBeats) — read in full
- Codebase: `flow-lang/TypeSystem/SpecialTypes/NoteValueType.cs:1-100` (existing duration enum + ToFraction double helper) — read
- Codebase: `flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs:117-118` (existing GCD idiom) — verified via grep
- Codebase: `flow-lang/Runtime/NoteStreamCompiler.cs:1-80, 67-610` (consumers of MusicalNoteData ctor) — read first 80 lines + grepped all 30+ call sites
- Codebase: `flow-lang/TypeSystem/ArrayType.cs:1-40` (sibling helper-type pattern in TypeSystem/) — read
- Codebase: `flow-lang/TypeSystem/FlowType.cs` (base class — confirms Fraction is NOT a FlowType) — read
- Codebase: `flow-lang.Tests/flow-lang.Tests.csproj` (xunit.v3 3.2.2 pinned) — read
- Codebase: `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` (existing byte-identical Fact pattern) — verified via ls
- `.planning/research/ARCHITECTURE.md` §3 + §9 (full Fraction strategy + anti-patterns) — read in full
- `.planning/research/PITFALLS.md` Pitfall 1 (lines 12-37) — read
- `.planning/REQUIREMENTS.md` FRAC-01 + FRAC-02 (lines 23-25) + Traceability table — read
- `CLAUDE.md` project instructions — read in initial context

### Secondary (MEDIUM confidence)
- music21 [DurationTuple model](https://music21.org/music21docs/usersGuide/usersGuide_19_duration2.html) — cited in ARCHITECTURE.md §3 as the canonical Fraction-based duration approach (verified by ARCHITECTURE.md researcher; not independently re-verified for this RESEARCH.md)
- Phase 15 Plan 05 byte-identical determinism precedent — `.planning/STATE.md` accumulated context lines 200-203

### Tertiary (LOW confidence)
- None.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — pinned by csproj + ARCHITECTURE.md, no version drift risk
- Architecture: HIGH — directly inherits ARCHITECTURE.md §3's locked decisions, no new architectural choices required
- Pitfalls: HIGH — codebase-grounded (grep on 30+ call sites, 21 GetBeats consumers); byte-identity contract is a specific, falsifiable mechanism
- Validation Architecture: HIGH — mirrors Phase 15 Plan 05 byte-identical pattern verbatim

**Research date:** 2026-04-26
**Valid until:** ~30 days (stable C# language features, no fast-moving dependencies). If Phase 19 starts more than 30 days from now, re-verify the 30+ MusicalNoteData call sites haven't grown since this grep.
