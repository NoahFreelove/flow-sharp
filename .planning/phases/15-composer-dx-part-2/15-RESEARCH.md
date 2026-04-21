# Phase 15: Composer DX Part 2 — Research

**Researched:** 2026-04-20
**Domain:** Flow language stdlib/grammar extensions — `reverbTime` musical-context block (DX-07) + `euclidean` overloads with swing/humanize/seed (DX-09)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from 15-CONTEXT.md)

### Locked Decisions (18 total)

**RT60 grammar contract (DX-07):**
- **D-01:** RT60 value range 0.0s–30.0s permissive. Out-of-range handled per D-03.
- **D-02:** `reverbTime 0 { ... }` → disable reverb for the block (dry). Voice renderer short-circuits `Reverb.Apply` when `MusicalContext.ReverbTime` is exactly 0. Charitable "0s tail = no reverb" interpretation.
- **D-03:** Out-of-range handling:
  - Negative RT60 (e.g., `reverbTime -2.5 { ... }`) → parser-level rejection at the literal.
  - RT60 > 30s → silent clamp to 30s (charitable interpretation).
  - RT60 in (0, 0.1) → pass through unchanged; no special grammar handling.
- **D-04:** Nesting with `gain`/`pan` = independent axes. Each context type is its own axis; all inherit through children. Inner `reverbTime` overrides outer `reverbTime`. `gain 0.5 { reverbTime 2.0 { ... } }` composes both without interaction.

**Swing accent semantics (DX-09):**
- **D-05:** Swing range `[-1.0, 1.0]` clamped. Negative = anti-accent (boost off-beats instead of on-beats).
- **D-06:** On-beat definition = hits landing on step positions `0, (steps/hits), 2*(steps/hits), …` (step-grid aligned). Rounds toward the grid division in edge cases.
- **D-07:** Accent magnitude = raw velocity delta on `[0, 1]` scale (no multiplier). `swing = 0.25` → "+0.25 to the accented set's velocity". `swing = 0` → no accent.
- **D-08:** Asymmetric accent — only the accented set moves; the other set stays at default. Positive swing: on-beats move up, off-beats stay. Negative swing: off-beats move up by `|swing|`, on-beats stay. Preserves overall energy.

**Humanize unit and clamping (DX-09):**
- **D-09:** Unit = fractional velocity on `[0, 1]` scale. `humanize = 0.1` → ±0.1 random velocity perturbation.
- **D-10:** Range `[0.0, 1.0]` clamped; out-of-range silently clamps.
- **D-11:** Distribution = uniform over `[-humanize, +humanize]` in Phase 15. Gaussian is deferred to the `enable` pragma system (DEFER-03) as its first opt-in feature (provisional: `enable "gaussian-humanize"`).
- **D-12:** Overflow handling = clamp to `[0, 1]`. Perturbed = `max(0, min(1, base ± jitter))`. Matches `NoteType.Velocity` clamp at `NoteType.cs:244`.

**PRNG seed semantics (DX-09 determinism):**
- **D-17:** `seed: Int` parameter constructs a **local** `System.Random(seed)` scoped to the single `euclidean(...)` call. Does NOT read from or mutate `ExecutionContext.GetRand` / the global seeded RNG. Determinism holds even if other seeded-random calls run between two `euclidean` invocations.
- **D-18:** Byte-identical output contract:
  - Repeat runs on same machine: YES
  - Across .NET patch versions (e.g., 9.0.1 → 9.0.2): YES (empirical; see Pitfall 7)
  - Across .NET major versions (e.g., 9 → 10): explicitly NOT guaranteed.

**Reverb.Apply wiring (DX-07 audio path):**
- **D-13:** Add NEW overload `Reverb.Apply(buffer, rt60Seconds, damping, mix)` in `Audio/DSP/Reverb.cs`. Non-breaking to existing `Apply(buffer, roomSize, damping, mix)`. Internally maps `rt60 → feedback` via Schroeder: `feedback = 10^(-3 * delay / rt60)`. Existing `reverb()` stdlib function in `EffectsFunctions.cs:30-73` unchanged.
- **D-14:** Per-voice reverb applied in SongRenderer voice loop post-synthesis (mirrors `gain`/`pan`). Each voice gets its own `Reverb.Apply` invocation. Fully independent tails.
- **D-15:** Damping + mix fixed at defaults (damping = 0.5, mix = 0.3, matching `ReverbSimple`). Context block exposes only RT60 to composer. Advanced users use explicit `reverb(...)` call for 4-param control.
- **D-16:** When `reverbTime` context is active AND user also calls `reverb(...)` explicitly inside the block: both apply and stack. No warning/error/override — two independent axes.

### Claude's Discretion

- Exact Schroeder/feedback formula constants (if D-13's `10^(-3*delay/rt60)` needs tuning for existing Reverb delay line length — verify against `Reverb.cs` internals).
- Parser-error message wording for negative RT60 (D-03).
- Test-name conventions and file layout for new `Phase15` test fixtures.
- Choice between extending `euclidean` registration via new signatures vs adding overloaded `FunctionSignature` entries.
- Exact threshold for "near-0" that short-circuits `Reverb.Apply` in D-02 (use `== 0` exact, NOT small-epsilon).

### Deferred Ideas (OUT OF SCOPE)

- **Gaussian humanize distribution** (D-11, DEFER-03): First planned opt-in of `enable` pragma system.
- **Shared reverb bus per RT60** (alternative to D-14): Future `reverbBus { ... }` construct.
- **Micro-timing / groove offsets**: Swing as timing offset (rather than velocity accent). Explicitly deferred to v1.3; requires new `MusicalNoteData` timing field.
- **Damping/mix exposed on `reverbTime` block** (alternative to D-15): `reverbTime 2.5 damping 0.7 mix 0.4 { ... }` syntax.
- **Negative-swing de-accent behavior** (alternative to D-08).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **DX-07** | `reverbTime <seconds> { … }` musical-context block sets per-voice reverb RT60; applied during voice rendering via `Reverb.Apply` with RT60→feedback mapping. Mirrors `gain`/`pan` context pattern. Pre-landing identifier audit: no collision with existing `reverbTime` usage. | Sections **Architecture Patterns** (Pattern 1 & 3), **Standard Stack** (existing infrastructure), **Code Examples** (all reverbTime snippets), **Common Pitfalls** 1/2/3/4. |
| **DX-09** | `euclidean(hits, steps, note, swing)` and `euclidean(hits, steps, note, swing, humanize, seed)` overloads. Swing as velocity accent on on-beats. Humanize perturbs velocity within `±humanize`. Required `seed: Int` for deterministic output. No new `MusicalNoteData` timing field (deferred to v1.3). | Sections **Architecture Patterns** (Pattern 2 & 4), **Common Pitfalls** 5/6/7, **Code Examples** (euclidean sketches), **Runtime State Inventory**. |
</phase_requirements>

## Summary

Both features are purely additive stdlib/grammar extensions with zero new NuGet dependencies and zero changes to the audio backend or lexer core. The existing infrastructure is a direct superset of what these features need:

- **DX-07 (`reverbTime`)** is a 9-file touch that mirrors the shipped `gain`/`pan` pattern exactly — `MusicalContext` field + `MusicalContextType` enum + parser numeric-body case + interpreter switch case + nullable propagation through `ExecutionContext.GetMusicalContext` + early-break predicate update + new `Reverb.Apply` overload + SongRenderer voice-loop invocation + `SimpleLexer` keyword-table entry.
- **DX-09 (`euclidean` swing/humanize)** is a focused edit of `BuiltInFunctions.cs:1033-1074` adding two new `FunctionSignature` registrations alongside the existing one. The determinism contract uses the well-worn local-`new Random(seed)` pattern from `VariationFunctions.cs:76,85,104` — which already ships and passes byte-identical round-trip tests.

**Primary recommendation:** Ship DX-09 first (Wave 1, smaller blast radius — 1 file), then DX-07 (Wave 2, 9 files). Use a new `Shared/MidiReadHelpers.cs` extracted from Phase 14's inline helper (DEFER-05) for the byte-identical MIDI regression test. The critical risk is NOT the implementation — it is the **runtime determinism** of `System.Random(seed)` across .NET versions: Microsoft documents the algorithm as "an implementation detail [that] may change between implementations, platform or even framework versions" [CITED: learn.microsoft.com/dotnet/api/system.random]. Treat D-18's across-patch-versions contract as empirical, not contractual, and pin the observable byte sequence with a regression Fact so any future .NET patch that breaks it fails loudly.

**Secondary recommendation:** The ROADMAP success criterion #3 ("rejects negative or zero values with a clear error") is **contradicted** by CONTEXT D-02 (`reverbTime 0` → dry, no error). The planner MUST follow CONTEXT.md (locked user decision) and flag this as a doc-only follow-up — ROADMAP wording needs correction in the Phase 15 closure plan (Plan NN-04 or equivalent).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `reverbTime` keyword recognition | Lexing (`SimpleLexer.cs:580-608`) | — | Single-line keyword-table entry, same tier as `tempo`/`gain`/`pan`. |
| `reverbTime <N> { ... }` grammar | Parsing (`Parser.cs:100-132, 511-539`) | AST (`MusicalContextStatement.cs:8`) | Parser already has `gain`/`pan` numeric-body parse shape; AST enum addition is trivial. |
| RT60 range validation | Parsing (negative → parse error at literal) + Interpreter (clamp at 30s) | — | D-03 splits validation: parse-time for negatives (no defensible meaning), interpret-time for upper bound (charitable clamp). |
| RT60 scope + inheritance | Runtime (`MusicalContext.cs:35-60, 95-107` + `ExecutionContext.cs:186-212`) | — | Null-propagation model already ships for `Gain`/`Pan`/`Velocity`; add 8th nullable field on same rail. |
| RT60 → feedback math | DSP (`Audio/DSP/Reverb.cs:26`) | — | Pure closed-form transform; new overload wraps existing `ProcessChannel`. Do NOT modify existing `Apply(roomSize,...)`. |
| Per-voice RT60 application | Audio/SongRenderer (`SongRenderer.cs:115-143`) | DSP | Section-snapshot `SectionData.Context` already read at renderer entry (lines 117-119 for `bpm`/`pan`/`gain`); RT60 reads identically. |
| `euclidean` overload dispatch | Stdlib (`BuiltInFunctions.cs:1033-1074`) | TypeSystem (`OverloadResolver`) | Signature-driven dispatch; 3 signatures coexist via specificity scoring. |
| Swing accent + humanize velocity | Stdlib (inside `euclidean` body) | TypeSystem (`NoteType.cs:244` velocity clamp) | Velocity math lives inside the function; clamp is reused via `MusicalNoteData` constructor. |
| Seeded determinism | Stdlib (local `new Random(seed)` inside `euclidean`) | — | D-17 pattern: isolated PRNG scope, NOT `ExecutionContext.GetRand`. Mirrors `VariationFunctions.VarySeeded`. |
| MIDI/WAV output | Audio/Effects (`MidiExport.cs:191-192`, `SongRenderer.cs:184-185`) | — | Consumes `MusicalNoteData.Velocity` unchanged; DX-09 does NOT modify the output pipeline. |

## Project Constraints (from CLAUDE.md)

- **Runtime target:** .NET 9 per CLAUDE.md — ⚠️ **ACTUAL** target is net10.0 per `flow-lang/flow-lang.csproj:4`, `flow-lang.Tests/flow-lang.Tests.csproj:3`, `flow-interpreter/flow-interpreter.csproj:9`. This is a known doc lag (see STATE.md Phase 12 Plan 06 note: "net10.0-vs-net9.0 target-framework doc lag is known"). Research and plans should use **net10.0** as the actual target. [VERIFIED: .csproj contents grepped 2026-04-20]
- **Platform:** Linux primary (PulseAudio); `IAudioBackend` abstraction exists.
- **Dependencies:** Minimal — only `Melanchall.DryWetMidi 8.0.3` and `Pidgin 3.5.1` (latter unused). **No new NuGet packages permitted.**
- **Performance:** Real-time audio playback; no GC pressure in hot paths. Reverb per voice has CPU cost; D-14 accepts this at typical song sizes (<50 voices).
- **Compatibility:** Existing .flow scripts and test suite must continue to work. 13 call sites of `NoteType.Parse`; DX-07 does not touch these.
- **Coding:** C# 13, .NET 9 idioms, file-scoped namespaces, record types for AST nodes, pattern matching.
- **Test framework:** xUnit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 (`flow-lang.Tests/flow-lang.Tests.csproj`).
- **GSD workflow enforcement:** All file-changing work must flow through a GSD command (`/gsd:execute-phase` for planned phase work).
- **User memory — language philosophy:** Keep functional S-expression style, no infix operators, Haskell-inspired. **Implication for tests:** `.flow` test scripts use `(print (str x))` and named-variable assignment conventions, not infix arithmetic.
- **User memory — charitable interpretation:** Prefer silent-and-documented assumptions over errors; music > rigid correctness. **Implication for D-02/D-03:** `reverbTime 0` → dry (not error), `reverbTime 45` → clamp to 30 (not error); only `reverbTime -N` errors because negative has no defensible musical meaning.

## Standard Stack

### Core (existing, no changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET / C# | net10.0 (actual) | Runtime + language | Already in use across all projects. [VERIFIED: csproj grep] |
| Melanchall.DryWetMidi | 8.0.3 | MIDI read/write | Already referenced; read API used in Phase 14 DX-08 regression test. [VERIFIED: flow-lang.csproj:11] |
| xUnit.v3 | 3.2.2 | Test framework | Phase 12 Plan 01 baseline; 81 Facts green at Phase 14 close. [VERIFIED: flow-lang.Tests.csproj:13] |

### Supporting (existing, no changes)
| Library/Module | Purpose | When to Use |
|----------------|---------|-------------|
| `System.Random(int seed)` | Deterministic PRNG | D-17 — local instance per `euclidean` call. **Not thread-safe, but each call creates its own instance, so no sharing.** [CITED: learn.microsoft.com/dotnet/api/system.random.-ctor] |
| `Math.Clamp(value, min, max)` | Range clamping | D-05, D-10, D-12. Overflow to `[0,1]` per D-12. |
| `Math.Pow(10, -3*delay/rt60)` | Schroeder feedback mapping | D-13. Closed-form, no dependency needed. |
| `FlowEngineRunner` (test fixture) | `.flow` script execution with stdout/stderr capture | Existing Phase 12+ test infrastructure. |
| `FlowScriptData.FindTestsRoot()` | Repo-relative path resolution | Phase 14 DynamicsMidiVelocityTests precedent. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff / Why Rejected |
|------------|-----------|------------------------|
| `System.Random(seed)` for D-17 | A pinned xorshift64*/splitmix64 in-repo | Custom PRNG guarantees byte-stability across .NET major versions (which D-18 explicitly does NOT require). Adds code surface for marginal benefit. **Rejected** — CONTEXT locks `System.Random(seed)`. STATE.md Blocker/Concerns line 160 suggested pinned PRNG; CONTEXT superseded with D-17/D-18. |
| New `tailSeconds` parameter in existing `Reverb.Apply(roomSize, ...)` | Extending existing overload | Breaking change; call sites in `EffectsFunctions.cs:46-73` would need touching. **Rejected** — D-13 locks "new overload, non-breaking". |
| Shared reverb bus per RT60 value | Per-voice reverb (D-14) | CPU-efficient "analog studio" texture; blends tails at same RT60. **Deferred** — `reverbBus { ... }` future construct; D-14 ships per-voice for maximum creative range. |
| Adding `TimingOffset` field to `MusicalNoteData` | Keeping velocity-only swing | Would enable micro-timing humanize. **Deferred to v1.3** — REQUIREMENTS.md explicit future item. |
| `InternalFunctionRegistry` adding overloaded signatures | New `FunctionSignature` entries per overload | Specificity scoring already discriminates on arg count. **Both approaches work**; Claude's Discretion per CONTEXT. Existing `reverb`/`compress`/`sidechain` all register 2+ overloads via separate signatures — the canonical pattern. |

### Installation / Setup
No new packages. No csproj changes. No new files in `flow-lang/` except optional new `.flow` test scripts and new xUnit Facts under `flow-lang.Tests/Unit/Phase15/` and `flow-lang.Tests/Integration/Phase15/`.

**Version verification (2026-04-20):**
- `Melanchall.DryWetMidi 8.0.3` — already in csproj, supports `MidiFile.Read` + `GetNotes` + `(byte)Velocity` projection (Phase 14 plan 14-03 empirically confirmed). [VERIFIED: csproj + successful Phase 14 build]
- `xunit.v3 3.2.2` / `xunit.runner.visualstudio 3.1.5` — Phase 12 baseline. [VERIFIED: csproj]

## Architecture Patterns

### System Architecture Diagram — DX-07 (`reverbTime`)

```
┌──────────────────────────────────────────────────────────────────────┐
│ Source: reverbTime 2.5 { | C4 D4 | } │                                │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ SimpleLexer (Lexing/SimpleLexer.cs:580)                              │
│   "reverbTime" → TokenType.ReverbTime (NEW keyword entry)            │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Parser (Parsing/Parser.cs:~105)                                      │
│   Check(TokenType.ReverbTime) + lookahead(IntLit|FloatLit|Minus|Plus)│
│   → ParseMusicalContextStatement(MusicalContextType.ReverbTime)      │
│   → case body (Parser.cs:~540): parse numeric literal                │
│     - If Minus sign consumed ⇒ PARSE ERROR "RT60 cannot be negative" │
│     - If non-numeric ⇒ existing error format                         │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ AST (Ast/Statements/MusicalContextStatement.cs:8)                    │
│   MusicalContextType enum: + ReverbTime member (NEW)                 │
│   → MusicalContextStatement(Location, ReverbTime, Value=2.5, …)      │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Interpreter (Interpreter/Interpreter.cs:~246, new case)              │
│   case MusicalContextType.ReverbTime:                                │
│     evaluate value → double                                          │
│     if > 30.0 ⇒ Math.Min(value, 30.0)  // D-03 silent clamp          │
│     if value == 0.0 ⇒ store 0.0 (D-02 sentinel for dry)             │
│     musicalCtx.ReverbTime = value                                    │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Runtime context stack (Runtime/MusicalContext.cs + ExecutionContext) │
│   MusicalContext.ReverbTime: double? (NEW nullable field)            │
│   ExecutionContext.GetMusicalContext walks stack; ReverbTime ??= …   │
│   Early-break predicate: update to include ReverbTime nullability    │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Section execution (Interpreter.cs:370-425)                           │
│   SectionData.Context = GetMusicalContext() snapshot                 │
│   Snapshot captures ReverbTime at section boundary                   │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ SongRenderer voice loop (StandardLibrary/Audio/SongRenderer.cs:115)  │
│   double? rt60 = section.Context?.ReverbTime;                        │
│   foreach voice:                                                     │
│     if rt60.HasValue && rt60.Value != 0.0:                           │
│       voice buffer ← Reverb.Apply(voice.Buffer, rt60, 0.5f, 0.3f)    │
│     // gain/pan applied in existing MixVoicesToStereoBuffer (:161-163)│
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Reverb.Apply NEW overload (Audio/DSP/Reverb.cs, new method)          │
│   public static AudioBuffer Apply(buffer, rt60, damping, mix):       │
│     delayAvg = (CombDelays.Average() * rateScale)                    │
│     feedback = Math.Pow(10, -3.0 * (delayAvg / sampleRate) / rt60)   │
│     feedback = Math.Clamp(feedback, 0.0, 0.99)  // numeric safety    │
│     → same ProcessChannel(input, feedback(derived), damping, ...)    │
└──────────────────────┬───────────────────────────────────────────────┘
                       ▼
                 Final AudioBuffer (stereo) → WAV/playback
```

### System Architecture Diagram — DX-09 (`euclidean` overloads)

```
┌───────────────────────────────────────────────────────────────────────┐
│ Source: Sequence g = (euclidean 3 8 C4 0.3 0.1 42)                    │
└──────────────────────┬────────────────────────────────────────────────┘
                       ▼
┌───────────────────────────────────────────────────────────────────────┐
│ Overload resolver (TypeSystem/OverloadResolver.cs)                    │
│   3 signatures registered for "euclidean":                            │
│     1. (Int, Int, Note) → Sequence           [EXISTING, lines 1033]   │
│     2. (Int, Int, Note, Double) → Sequence   [NEW — swing-only]       │
│     3. (Int, Int, Note, Double, Double, Int) [NEW — swing+humanize+seed]│
│   6-arg call resolves to overload 3 by specificity.                   │
└──────────────────────┬────────────────────────────────────────────────┘
                       ▼
┌───────────────────────────────────────────────────────────────────────┐
│ BuiltInFunctions.cs euclidean body (edit lines 1033-1074)             │
│   Phase A: Bjorklund (unchanged) → bool[] pattern                     │
│   Phase B: On-beat detection — hit indices ≡ multiples of (steps/hits)│
│   Phase C: Compute base velocity = context.Velocity ?? 0.63           │
│   Phase D: For swing_only overload: velocity per hit                  │
│     - On-beat hits (positive swing): vel = base + swing               │
│     - Off-beat hits (positive swing): vel = base                      │
│     - Negative swing: flip accented set                               │
│     - swing = 0: all hits at base                                     │
│   Phase E: For humanize overload: also apply per-hit jitter           │
│     - Create LOCAL System.Random(seed) — D-17                         │
│     - For each hit: jitter ∈ [-humanize, +humanize] uniform           │
│     - vel = max(0, min(1, vel + jitter))  — D-12                      │
│   Phase F: MusicalNoteData ctor clamps vel ∈ [0,1] as belt-and-braces │
└──────────────────────┬────────────────────────────────────────────────┘
                       ▼
┌───────────────────────────────────────────────────────────────────────┐
│ Existing downstream (NO changes):                                     │
│   SequenceData → NoteStreamCompiler (reads note.Velocity)             │
│   MidiExport.cs:191-192 → byte velocity = (byte)Clamp(vel*127, 1, 127)│
│   SongRenderer synthesis → velocity scales sample amplitude           │
└───────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure (file-scoped delta)

```
flow-lang/
├── Ast/Statements/
│   └── MusicalContextStatement.cs          # EDIT: enum + ReverbTime
├── Runtime/
│   └── MusicalContext.cs                   # EDIT: ReverbTime field + Clone() + ToString()
│                                           # EDIT (ExecutionContext.cs): GetMusicalContext walk + early-break predicate
├── Parsing/
│   └── Parser.cs                           # EDIT: 2 sites (:105, :539) mirroring gain/pan
├── Lexing/
│   └── SimpleLexer.cs                      # EDIT: keyword table :580 + TokenType enum
├── Interpreter/
│   └── Interpreter.cs                      # EDIT: ExecuteMusicalContext switch (~:246)
├── StandardLibrary/
│   ├── Audio/
│   │   ├── DSP/Reverb.cs                   # EDIT: add new overload (NEW method, keep existing)
│   │   └── SongRenderer.cs                 # EDIT: voice loop (:115-143) + RenderSectionWithTimeline (:240-280)
│   └── BuiltInFunctions.cs                 # EDIT: euclidean :1033-1074 (add 2 signatures + new body)
└── std.flow                                # EDIT: add 2 internal proc euclidean declarations

flow-lang.Tests/
├── Unit/Phase15/
│   ├── ReverbTimeContextTests.cs           # NEW: unit Facts for D-01..D-04
│   ├── EuclideanSwingTests.cs              # NEW: unit Facts for D-05..D-08
│   ├── EuclideanHumanizeTests.cs           # NEW: unit Facts for D-09..D-12, D-17
│   └── ReverbApplyRt60Tests.cs             # NEW: unit Facts for D-13 new overload
├── Integration/Phase15/
│   ├── EuclideanByteIdenticalTests.cs      # NEW: D-18 MIDI byte-identical regression
│   └── ReverbTimeRenderTests.cs            # NEW: D-02 dry short-circuit + D-14 per-voice
└── Shared/
    └── MidiReadHelpers.cs                  # NEW (DEFER-05 trigger): Phase 14 inline helper → Shared

tests/
├── test_reverb_time.flow                   # NEW: Theory row + .flow-level sanity
├── test_euclidean_swing.flow               # NEW: Theory row
├── test_euclidean_humanize.flow            # NEW: Theory row
└── output/
    ├── phase15_euclidean_run1.mid          # Regression artifact (gitignored or checked in per plan)
    └── phase15_euclidean_run2.mid
```

### Pattern 1: Musical context block — add a new axis

**What:** Each of `tempo`, `timesig`, `swing`, `key`, `dynamics`, `rit`, `accel`, `pan`, `gain` is a nullable field on `MusicalContext` that inherits through child frames via `ExecutionContext.GetMusicalContext()`. Adding `reverbTime` follows the exact same 8-step recipe.

**When to use:** Any new scoped musical parameter that should inherit, override, and snapshot into `SectionData`.

**Recipe (sequential, all 8 steps required):**

```csharp
// 1. AST enum (flow-lang/Ast/Statements/MusicalContextStatement.cs:8)
public enum MusicalContextType {
    Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain,
    ReverbTime  // NEW
}

// 2. Runtime field (flow-lang/Runtime/MusicalContext.cs:41)
public double? ReverbTime { get; set; }  // NEW, nullable, seconds

// 3. Clone() (MusicalContext.cs:51-60)
public MusicalContext Clone() => new() {
    TimeSignature = TimeSignature, Tempo = Tempo, Swing = Swing,
    Key = Key, Velocity = Velocity, Pan = Pan, Gain = Gain,
    ReverbTime = ReverbTime  // NEW
};

// 4. ToString() (MusicalContext.cs:95-106)
if (ReverbTime != null) parts.Add($"reverbTime={ReverbTime}");  // NEW

// 5. TokenType + lexer keyword (flow-lang/Lexing/TokenType.cs & SimpleLexer.cs:580)
TokenType.ReverbTime,  // in enum
"reverbTime" => TokenType.ReverbTime,  // in keyword switch

// 6. Parser numeric-body detection (Parser.cs:~128, mirror gain/pan)
if (Check(TokenType.ReverbTime) && _current + 1 < _tokens.Count
    && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
        or TokenType.Minus or TokenType.Plus)) {
    Advance();
    return ParseMusicalContextStatement(MusicalContextType.ReverbTime);
}
// Note: Per D-03, negative parse-rejection happens INSIDE the case body below,
// NOT by refusing to enter the parse path — we MUST consume the sign to point
// the error at the literal.

// 7. Parser case body (Parser.cs:~540, mirror Gain case at :526-539)
case MusicalContextType.ReverbTime: {
    var rtLoc = CurrentToken.Location;
    bool negative = false;
    if (Match(TokenType.Minus)) negative = true;
    else if (Match(TokenType.Plus)) { /* sign noise */ }
    if (negative) {
        throw new ParseException(
            $"reverbTime cannot be negative (RT60 is a time in seconds); got '-' at {rtLoc}");
    }
    if (Check(TokenType.IntLiteral))
        value = new LiteralExpression(rtLoc, (double)(int)Advance().Value!);
    else if (Check(TokenType.FloatLiteral))
        value = new LiteralExpression(rtLoc, (double)Advance().Value!);
    else
        throw new ParseException(
            $"Expected numeric reverbTime value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    break;
}

// 8. Interpreter case (Interpreter.cs:~246, after Gain case at :231-245)
case MusicalContextType.ReverbTime: {
    var rtVal = _evaluator.Evaluate(ctx.Value);
    double rt60 = rtVal.Type is IntType ? (double)rtVal.As<int>() : rtVal.As<double>();
    // D-03: silent clamp to 30s (negative already rejected at parse time)
    rt60 = Math.Min(rt60, 30.0);
    // D-02: 0.0 is preserved as a sentinel for "dry" — no error, no clamp-up
    musicalCtx.ReverbTime = rt60;
    break;
}

// Also update ExecutionContext.GetMusicalContext (ExecutionContext.cs:186-212):
resolved.ReverbTime ??= frame.MusicalContext.ReverbTime;
// And add ReverbTime to the early-break predicate at line 201-205.
```

### Pattern 2: Local seeded PRNG for byte-identical output

**What:** For any built-in that takes a `seed: Int` parameter and promises byte-identical output, construct a fresh `new System.Random(seed)` **scoped to that single call**. Do NOT use `ExecutionContext.GetRand(fixedRng: true)` — that RNG is shared across calls and consumes state across boundaries.

**When to use:** DX-09 humanize; any future deterministic-generative built-in.

**Example (verbatim pattern from `VariationFunctions.cs:76`):**

```csharp
// Existing, shipped example — vary with seed
private static Value VarySeeded(IReadOnlyList<Value> args) {
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    int seed = args[2].As<int>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
    //                                                         ^^^^^^^^^^^^^^^^^^
    // Local instance; scoped to this call; byte-identical across calls with same seed.
}
```

**Anti-example (what NOT to do — from `TransformFunctions.cs:667`):**

```csharp
// DO NOT MIRROR THIS for DX-09 — static shared RNG defeats determinism
private static readonly Random HumanizeRng = new();
// ...
double velJitter = (HumanizeRng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
// ^^ Shared state across calls. Two calls to humanize() produce different
// output on the second run. This is the EXISTING humanize() function —
// distinct from DX-09's euclidean humanize parameter.
```

### Pattern 3: Schroeder reverb RT60 → feedback mapping

**What:** The existing `Reverb.ProcessChannel` uses `float feedback = 0.7f + roomSize * 0.28f` (a direct roomSize mapping). D-13 requires an RT60-based mapping: `feedback = 10^(-3 * delayT / RT60)` where `delayT` is the average comb-filter delay time in seconds. A single comb filter with delay `D` samples at sample-rate `fs` decays by ~60 dB (RT60 reference) when `feedback^N == 10^-3` with `N = RT60 / (D/fs)`, giving `feedback = 10^(-3 / N) = 10^(-3 * (D/fs) / RT60)`.

**Why:** The 4 parallel comb filters in `Reverb.cs:10` have different delays (1116, 1188, 1277, 1356 samples at 44.1kHz). Using the average delay for the mapping gives the best single-coefficient approximation.

**Example:**

```csharp
// NEW overload in flow-lang/StandardLibrary/Audio/DSP/Reverb.cs
// Kept separate from the existing Apply(roomSize, …) overload per D-13 (non-breaking).
public static AudioBuffer Apply(AudioBuffer input, double rt60Seconds, float damping, float mix)
{
    // Guard: rt60 == 0 is the caller's responsibility (D-02 dry short-circuit lives in
    // the SongRenderer, not here). If the caller sends 0, treat as "minimal tail" to
    // avoid division-by-zero.
    if (rt60Seconds <= 0.0) rt60Seconds = 0.001;

    // Compute feedback from average comb delay
    double rateScale = input.SampleRate / 44100.0;
    double avgDelaySamples = (1116 + 1188 + 1277 + 1356) / 4.0 * rateScale;  // = 1234.25 at 44.1kHz
    double avgDelaySeconds = avgDelaySamples / input.SampleRate;             // ~0.028 seconds
    double feedback = Math.Pow(10.0, -3.0 * avgDelaySeconds / rt60Seconds);
    feedback = Math.Clamp(feedback, 0.0, 0.99);  // numeric safety; existing code caps at 0.98

    damping = Math.Clamp(damping, 0f, 1f);
    mix = Math.Clamp(mix, 0f, 1f);

    var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);

    // Process each channel — reuse existing ProcessChannel but with derived feedback
    // (rather than roomSize-derived feedback). Requires either:
    //   A. Extract ProcessChannel to accept a feedback parameter directly (preferred —
    //      existing ProcessChannel at :58-88 already receives `feedback` via local
    //      calculation, simple to pass through), OR
    //   B. Inline a variant of ProcessChannel in the new overload.
    // Recommendation: Option A. Change ProcessChannel signature to accept
    //   (float[] input, float feedback, float damping, double rateScale)
    // and update the existing Apply(roomSize,…) to compute feedback locally:
    //   float feedback = 0.7f + roomSize * 0.28f;
    // then call ProcessChannel(dry, feedback, damping, rateScale).
    // This is a strict refactor — no behavior change for existing callers.

    for (int ch = 0; ch < input.Channels; ch++) {
        var dry = ExtractChannel(input, ch);
        var wet = ProcessChannel(dry, (float)feedback, damping, rateScale);
        for (int frame = 0; frame < input.Frames; frame++) {
            float mixed = dry[frame] * (1f - mix) + wet[frame] * mix;
            result.SetSample(frame, ch, mixed);
        }
    }

    return result;
}
```

**Why two feedback sources in the DSP core work:** The comb filter math `output[n] = input[n] + feedback * lpf(output[n-delay])` is feedback-coefficient-driven. Both the existing `0.7 + roomSize*0.28` mapping (physical-room metaphor) and the new `10^(-3*t/rt60)` mapping (time-domain decay law) produce valid feedback values — they are just different dials driving the same knob. `feedback` naturally lives in the range `[0, 0.99]`; RT60 of 0.1s gives feedback ~0.14, RT60 of 30s gives feedback ~0.998 (capped at 0.99).

### Pattern 4: On-beat detection in a Bjorklund euclidean pattern

**What:** Given hits distributed by Bjorklund across N steps, "on-beat" per D-06 means hits whose step-position index is a multiple of `floor(steps / hits)`. For pathological cases where `steps / hits` rounds to 1, every hit is on-beat — this is the charitable interpretation when the grid divides finely.

**Example:**

```csharp
// Inside new euclidean overload body
bool[] pattern = Bjorklund(hits, steps);  // existing, unchanged
int gridStep = Math.Max(1, steps / hits); // D-06: floor division; guard against 0

// Collect hit step-indices
var hitIndices = new List<int>();
for (int i = 0; i < steps; i++) if (pattern[i]) hitIndices.Add(i);

// On-beat = hit whose step-index is a multiple of gridStep
bool IsOnBeat(int stepIndex) => (stepIndex % gridStep) == 0;

// Apply swing (D-05/D-07/D-08)
double swingClamped = Math.Clamp(swing, -1.0, 1.0);
bool accentOnBeats = swingClamped >= 0;  // D-08: positive → on-beats; negative → off-beats
double accentAmount = Math.Abs(swingClamped);

// Per-hit velocity
var velocities = new Dictionary<int, double>();
foreach (int i in hitIndices) {
    double v = baseVelocity;
    bool onBeat = IsOnBeat(i);
    bool accented = (accentOnBeats && onBeat) || (!accentOnBeats && !onBeat);
    if (accented) v += accentAmount;
    velocities[i] = v;  // clamp at constructor
}
```

### Anti-Patterns to Avoid

- **Inventing a new AST node for `reverbTime`:** There's already `MusicalContextStatement` with an enum dispatch — adding an enum member is the idiomatic path. Creating `ReverbTimeStatement` would fragment the 9 other music-context types.
- **Using `ExecutionContext.GetRand(fixedRng: true)` for D-17:** Shared across script lifetime; consumes state if other seeded calls run between two `euclidean` invocations. Violates D-18 byte-identical contract.
- **Short-circuiting `Reverb.Apply` inside the DSP method for `rt60 == 0`:** The dry sentinel belongs in the renderer per D-02 ("voice renderer short-circuits"). Keep DSP methods pure (input → output); move control flow to the caller.
- **Adding `Voice.ReverbTime` field:** D-14 explicitly says "do NOT add to Voice; flow through MusicalContext." Adding a field would create a second state path parallel to `section.Context?.ReverbTime` and require per-voice bookkeeping the rest of the pipeline does not need.
- **Modifying existing `Reverb.Apply(buffer, roomSize, damping, mix)`:** D-13 locks "new overload, non-breaking." Breaking the existing signature would churn `EffectsFunctions.cs:46-73` and any user scripts calling `reverb(buffer, 0.5)`.
- **Using `Math.Abs(swing)` without clamp in D-05:** User passes `swing = -1.5` → clamp first to `-1.0` per D-05, then `Math.Abs(-1.0) = 1.0`. Skipping the clamp means `Math.Abs(-1.5) = 1.5`, which produces out-of-range velocity jitter.
- **Adding a `humanize: Double` overload without `seed: Int`:** Would silently consume the global static RNG (like the existing `humanize` built-in). All humanize overloads in DX-09 REQUIRE `seed` per REQUIREMENTS.md DX-09 wording.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Nullable-field inheritance through call-stack frames | Custom walk of stack frames | Existing `ExecutionContext.GetMusicalContext()` walk at `:186-212` | 7 fields already work this way; early-break predicate already handles the null-skip optimization; adding one more field is idiomatic. |
| Byte-identical PRNG | Custom xorshift/splitmix in-repo | `System.Random(seed)` per D-17 | Microsoft documents algorithm as unstable across **majors** but empirically stable across **patches**. D-18 explicitly accepts the major-break. [CITED: learn.microsoft.com/dotnet/api/system.random] |
| MIDI file reading in tests | Raw byte parsing of SMF format | `Melanchall.DryWetMidi 8.0.3` `MidiFile.Read(path)` + `GetNotes()` | Already referenced; Phase 14 DX-08 Fact empirically validated the read path. |
| Bjorklund euclidean distribution | Re-implement the algorithm | Existing `BuiltInFunctions.Bjorklund(hits, steps)` at `:1080` | Already ships; existing euclidean built-in uses it; DX-09 extends the wrapper, not the core. |
| Velocity clamping | Manual `if v > 1 v = 1` | `MusicalNoteData` constructor auto-clamps at `NoteType.cs:244` | D-12 locks "clamp, not reflect"; the constructor already does it. |
| RT60 → feedback coefficient | Look up from a table | Closed-form `Math.Pow(10, -3*t/rt60)` | Schroeder's original formula; 1 line of math. |
| WAV file writing (for `writeWav` test output) | - | Existing `FileIO.cs:58-60` auto-mkdir path (Phase 12 Plan 05 fix) | `writeWav` and `writeMidi` already handle parent-directory creation. |

**Key insight:** Every technique Phase 15 needs already exists in the codebase. The phase is a composition exercise, not an invention exercise.

## Runtime State Inventory

Phase 15 is purely additive (new grammar + new stdlib surface). No rename/refactor/migration. The checklist below is completed explicitly per the research protocol:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | None — verified by `grep -rn "reverbTime" .` across the whole repo. Zero hits in database files (none exist), Mem0 (not used), ChromaDB (not used), or any persistent store. Euclidean output is regenerated per run; no cached rhythms. | None. |
| **Live service config** | None — no external services that this project interacts with (no n8n/Datadog/Cloudflare Tunnel). `PulseAudioSimpleBackend` is a P/Invoke binding, not a configured service. | None. |
| **OS-registered state** | None — no scheduled tasks, pm2 processes, or systemd units registered by this project. | None. |
| **Secrets and env vars** | None — `reverbTime` is not an env var. No secrets consumed by DX-07 or DX-09 paths. | None. |
| **Build artifacts / installed packages** | The `flow-lang/bin/Debug/net9.0/` folder listed in `flow-lang.csproj:40` is stale (project targets `net10.0`). Not a Phase 15 blocker but worth noting: `dotnet build` outputs to `net10.0/`, not `net9.0/`. | None required for Phase 15 feature work. Optional cleanup out of scope. |

**The canonical question answered:** After Phase 15 code lands, no runtime systems have the old identifier `reverbTime` cached, stored, or registered — because this identifier did not previously exist. The pre-landing collision grep (below) confirms this.

### Pre-landing collision grep (one-shot, matches Phase 14 D-21 precedent)

```bash
grep -rn "reverbTime" flow-lang/*.flow examples/ tests/ --include='*.flow' 2>/dev/null
# Result: 0 hits — clean [VERIFIED 2026-04-20]
```

This transcript should be re-surfaced in the Phase 15 VERIFICATION.md at closure per ROADMAP success criterion #5.

## Common Pitfalls

### Pitfall 1: Early-break predicate in `GetMusicalContext` is stale after adding 8th field

**What goes wrong:** `ExecutionContext.GetMusicalContext()` at `ExecutionContext.cs:201-205` has an `if (resolved.X != null && resolved.Y != null && …) break;` optimization. Adding `ReverbTime` without updating this predicate means the walk stops before finding a parent frame's `ReverbTime` value when all 7 existing fields are resolved but `ReverbTime` is still null. Silent inheritance break.

**Why it happens:** The predicate is a copy-paste pattern and easy to miss in PR review.

**How to avoid:** Update the predicate to require `resolved.ReverbTime != null` as the 8th clause. Add a regression Fact with a 3-level nested context (outer `reverbTime 2.0 { tempo 120 { timesig 4/4 { ... } } }`) that asserts `GetMusicalContext().ReverbTime == 2.0` at the innermost scope.

**Warning signs:** `reverbTime` works when declared at the innermost scope but mysteriously fails to inherit from grandparent. Test with ≥2 levels of nesting.

### Pitfall 2: Section snapshot semantics vs per-note semantics

**What goes wrong:** `SectionData.Context` is **a snapshot taken once at section-body entry** (`Interpreter.cs:375`), not a live reference. If a note-stream inside a section changes `reverbTime` via an inner block, the renderer sees the section's snapshot, NOT the inner value. Composer expects fine-grained per-voice-group RT60 variation; gets section-wide.

**Why it happens:** The `MusicalContextStatement` body runs against the context stack, but the SECTION captures the stack at entry — subsequent inner blocks update the stack for children but don't propagate up to the section snapshot.

**How to avoid:** Document the semantics explicitly: `reverbTime` is **section-scoped, not voice-scoped**, in Phase 15. If a user writes:

```flow
section s {
    reverbTime 2.0 {
        Sequence a = | C4 D4 |   // gets RT60 2.0
    }
    reverbTime 4.0 {
        Sequence b = | E4 F4 |   // ALSO gets RT60 2.0 — section snapshot wins
    }
}
```

both sequences share the section's snapshot. This is current `gain`/`pan` behavior too (same pipeline). If per-voice RT60 variation is needed, user wraps each in its own section. Deferred idea "reverbBus" covers the alternative.

**Warning signs:** Tests that set `reverbTime` twice in one section and expect two different tails. File a note in `deferred-items.md` if a user surfaces this need.

### Pitfall 3: `reverbTime 0` short-circuit comparison

**What goes wrong:** Using `Math.Abs(rt60 - 0.0) < epsilon` for the dry short-circuit (D-02). The parser produces the literal value verbatim (e.g., `reverbTime 0` → exactly 0.0, `reverbTime 0.0001` → exactly 0.0001). An epsilon comparison would accidentally short-circuit near-zero values, defeating the composer's intent to get near-dry-but-not-dry.

**Why it happens:** Float-equality anxiety is a reflex; the literal-from-parser path is exact.

**How to avoid:** CONTEXT D-02 + Claude's Discretion bullet 4 explicitly say: **`rt60 == 0.0` exact comparison**, no epsilon. Regression Fact: `reverbTime 0` produces dry output (no reverb applied); `reverbTime 0.1` produces near-dry but NOT dry (reverb call happens).

**Warning signs:** Tests around `reverbTime 0.01` or `reverbTime 0.001` suddenly produce dry output. Run `reverbTime 0.0001` and confirm `Reverb.Apply` IS invoked.

### Pitfall 4: Re-parse-error when negative RT60 is inside a larger expression

**What goes wrong:** D-03 says negative RT60 → parser error. But the parser consumes the `-` sign before the literal (`Parser.cs:515-516, 530-531`). If the error is raised by the `value < 0` check in the Interpreter case, the parse succeeds and the error lives in `ErrorReporter` as a runtime-like error instead of a parse error. Tests that assert error location will find it at the `{` rather than at the `-`.

**Why it happens:** The existing gain/pan path accepts signed numerics and validates in the interpreter. Mirroring blindly would inherit that behavior.

**How to avoid:** Raise the error at PARSE TIME inside the `ReverbTime` case in `Parser.cs` when `Match(TokenType.Minus)` returns true. Throw `ParseException` with the `-` token's location so the error pinpoints the sign. See Pattern 1 step 7 above.

**Warning signs:** Error message points at column of `{` rather than column of `-`. Fix by moving the validation from interpreter case to parser case.

### Pitfall 5: Overload resolution ambiguity between 3-arg and 4-arg euclidean

**What goes wrong:** Registering three `euclidean` overloads means `OverloadResolver` must discriminate between `(Int, Int, Note)`, `(Int, Int, Note, Double)`, and `(Int, Int, Note, Double, Double, Int)`. The resolver scores on argument count + exact type match (specificity +1000). If a user calls `(euclidean 3 8 C4 0.3)`, arg count is 4 — unambiguously the 4-arg overload. If a user calls `(euclidean 3 8 C4)`, arg count is 3 — unambiguously the existing overload. No ambiguity in practice.

**Why it's worth flagging:** Phase 12 Plan 05 (see STATE.md Accumulated Context) surfaced that `VoidType`-wildcard matching could accidentally pair `LazyType` args with strict overloads. Double-check that the new `euclidean` signatures use concrete types (`DoubleType`, `IntType`), not `VoidType`, so no wildcard matching.

**How to avoid:** Signatures for DX-09:

```csharp
// 4-arg: swing only
new FunctionSignature("euclidean",
    [IntType.Instance, IntType.Instance, NoteType.Instance, DoubleType.Instance]);

// 6-arg: swing + humanize + seed
new FunctionSignature("euclidean",
    [IntType.Instance, IntType.Instance, NoteType.Instance,
     DoubleType.Instance, DoubleType.Instance, IntType.Instance]);
```

Add corresponding `internal proc euclidean (...)` declarations in `flow-lang/std.flow` (required per Phase 14 Plan 02 finding — stdlib procs need both C# registration AND .flow declarations to be callable from user scripts).

**Warning signs:** `(euclidean 3 8 C4 0.3)` produces "No matching overload for function 'euclidean'". Check both the C# registration AND the `std.flow` proc declaration.

### Pitfall 6: Base velocity source for accent math

**What goes wrong:** D-07 says "`swing = 0.25` means +0.25 to the accented set's velocity". But velocity is relative to WHAT? If there's a `dynamics f { euclidean 3 8 C4 0.3 }` block, the accented-set base is 0.88 (forte = 0.88), and +0.3 on top would saturate. If there's no dynamics context, base is 0.63 (mf default). The code must read `MusicalContext.Velocity` at the time the euclidean executes.

**Why it happens:** The existing `euclidean` doesn't read velocity — it stores MusicalNoteData with default 0.63 via the constructor. DX-09 has to plumb velocity reading.

**How to avoid:** Inside the new euclidean body, read `context.Velocity ?? 0.63` via `ExecutionContext.GetMusicalContext()` — but note that `BuiltInFunctions` lambdas signature is `IReadOnlyList<Value> args`, NOT `args + context`. Use `RegisterContextDependent` pattern (see `HarmonyFunctions.cs` `resolveNumeral` registration, `SongRenderer.cs:30-36`) to get context access. Alternatively: the base velocity is just `0.63` regardless of dynamics (explicit, simpler). **Recommend: read MusicalContext.Velocity** for consistency with `NoteStreamCompiler.cs:341` which does `note.Velocity ?? context.Velocity ?? 0.63`.

**Warning signs:** `dynamics f { euclidean 3 8 C4 0.3 }` produces notes at 0.93 velocity but composer expected "forte + accent" to mean "forte=0.88 + 0.3 accent, clamped". Decide the semantics explicitly and document.

### Pitfall 7: `System.Random(seed)` algorithm instability across .NET majors

**What goes wrong:** Microsoft [CITED: learn.microsoft.com/dotnet/api/system.random] documents the algorithm as "an implementation detail [that] may change between implementations, platform or even framework versions." D-18 explicitly accepts breaking across majors. But a user on .NET 10 who authored a piece seeded `42` against .NET 9 might NOT understand why their piece sounds different.

**Why it happens:** The "code is the score" contract is stronger than Microsoft's guarantee.

**How to avoid:**
1. Document D-18 in the user-facing Flow docs (a future QOL-04 or inline tutorial comment): "euclidean(…, humanize, seed) is byte-identical within a .NET major version; audio may differ across .NET major upgrades."
2. Regression Fact in `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` that pins the exact MIDI byte sequence (e.g., `[75, 63, 81, 58, ...]` from `euclidean(3, 8, C4, 0.0, 0.1, 42)`). If the test flips red on .NET patch update, investigate immediately.
3. Skip the test (not fail) if running on a .NET major other than net10. Use `RuntimeInformation.FrameworkDescription` check.

**Warning signs:** Test suite goes red after unrelated .NET SDK update. If the update is a major version (e.g., 10 → 11), confirm the byte sequence changed and update the pinned values with a Divergence entry.

**Empirical note:** System.Random has been stable at the net core level since net5 in practice across patch versions; there are no public reports of RNG-algorithm changes within a major line. Treat D-18's "across 9.0.x" clause as empirically-reliable rather than contractually-guaranteed. [VERIFIED: Microsoft docs language; no counter-evidence in ecosystem]

### Pitfall 8: Existing `humanize` built-in conflates with DX-09 humanize parameter

**What goes wrong:** `TransformFunctions.cs:660` registers `humanize(Sequence, Double) → Sequence` as a top-level built-in. Its behavior (static shared `HumanizeRng`) is the OPPOSITE of DX-09's humanize parameter (local seeded `Random`). Documentation and tests must be careful not to confuse the two.

**Why it happens:** Both features are called "humanize" in the Flow stdlib.

**How to avoid:** In the test files, use `EuclideanHumanizeTests` (not `HumanizeTests`) for Phase 15. Document in `euclidean` stdlib doc: "Note: the `humanize` PARAMETER to euclidean is deterministic with seed; the `humanize` BUILT-IN applied to a Sequence is nondeterministic. They are separate features."

**Warning signs:** A user reports that their `euclidean(...,humanize=0.1, seed=42)` produces different output each run. Ask: are they calling `(seq -> humanize 0.1)` afterward (the nondeterministic built-in)?

## Code Examples

Verified patterns from existing sources + new sketches for Phase 15.

### reverbTime: minimal end-to-end (.flow)

```flow
// Source: new — DX-07 demonstrates per-section RT60
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        reverbTime 2.5 {
            section hall {
                Sequence a = | C4 D4 E4 F4 |
            }
            Song song = [hall]
            Buffer buf = (renderSong song "piano")
            (writeWav "tests/output/phase15_reverbtime.wav" buf)
            (print "reverbTime 2.5s: PASSED")
        }
    }
}
```

### reverbTime: dry short-circuit (D-02)

```flow
// Source: new — asserts 0.0 → no Reverb.Apply invocation (observable via shorter tail length or output-file hash)
use "@std"
use "@audio"

tempo 120 {
    reverbTime 0 {
        section dry {
            Sequence a = | C4 D4 E4 F4 |
        }
        Song song = [dry]
        Buffer buf = (renderSong song "piano")
        // Compare frame count against a reference "no reverb" render — should match exactly
        (print (concat "dry frames: " (str (getFrames buf))))
    }
}
```

### reverbTime: nesting independence (D-04)

```flow
// Source: new — DX-07 nested with gain; both axes independent
use "@std"
use "@audio"

tempo 120 {
    gain 0.5 {
        reverbTime 2.0 {
            section s {
                Sequence a = | C4 D4 E4 F4 |  // gets gain 0.5 AND rt60 2.0
            }
            // Confirmed via SongRenderer: section.Context has both Gain=0.5 AND ReverbTime=2.0
        }
    }
}
```

### euclidean: swing-only overload (D-05, D-07, D-08)

```flow
// Source: new — DX-09 4-arg variant
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            // 3 hits in 8 steps, positive swing accents on-beats (step 0, step ~2.67 → 2, step ~5.33 → 5)
            Sequence beat = (euclidean 3 8 C4 0.3)
            (visualize beat)

            // Negative swing: accents flip to off-beats
            Sequence beat_anti = (euclidean 3 8 C4 (sub 0.0 0.3))
            (visualize beat_anti)
        }
    }
}
```

### euclidean: humanize + seed (D-09, D-10, D-11, D-12, D-17, D-18)

```flow
// Source: new — DX-09 6-arg variant, byte-identical determinism regression
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        // Deterministic: seed 42 → always identical velocity perturbation
        Sequence beat_a = (euclidean 3 8 C4 0.0 0.15 42)
        Sequence beat_b = (euclidean 3 8 C4 0.0 0.15 42)
        // beat_a and beat_b must be bit-for-bit identical in MIDI output

        section s1 { beat_a }
        section s2 { beat_b }
        Song song_a = [s1]
        Song song_b = [s2]
        (writeMidi "tests/output/phase15_seed42_run1.mid" song_a)
        (writeMidi "tests/output/phase15_seed42_run2.mid" song_b)
        // Regression Fact in flow-lang.Tests compares the two files byte-for-byte
    }
}
```

### Reverb.Apply new overload (Pattern 3, implementation sketch)

See **Pattern 3** above for the full implementation.

### Expected velocity bytes for DX-09 determinism Fact

Example: `(euclidean 3 8 C4 0.3 0.1 42)` — 3 hits, 8 steps, C4, swing 0.3 (positive, on-beat accent), humanize 0.1, seed 42.

With base velocity 0.63, Bjorklund(3, 8) producing hit-positions `[0, 3, 6]` (all on-beat under D-06 rule with gridStep = 8/3 = 2):
- Hit 0: on-beat → 0.63 + 0.3 = 0.93, jitter from seed 42 first NextDouble() call.
- Hit 3: on-beat → 0.93 + jitter.
- Hit 6: on-beat → 0.93 + jitter.

**The planner MUST empirically compute the exact byte sequence by running the script once and recording it.** Do NOT compute from theory — `System.Random(42).NextDouble()` value depends on .NET version and is empirical. Phase 14's DX-08 Fact (commit 152e593) followed this pattern: authored the test, ran it once, recorded observed bytes, re-ran to confirm byte-identical across runs.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Infix-heavy music DSL syntax | S-expression functional style | Project-wide from v1.0 | Test scripts use `(euclidean 3 8 C4 0.3 0.1 42)` not `euclidean(3, 8, C4, swing=0.3, …)`. |
| Shared static Random for humanize | Local seeded `new Random(seed)` | VariationFunctions.cs (shipped in Phase 4) | D-17 mirrors this pattern. |
| Parser errors thrown mid-parse | `ErrorReporter` accumulation (soft-failure) | Phase 12 FIX-07a | For DX-07 D-03 negative-rejection, the existing `ParseException` throw is OK (parse-time is hard-fail), but the returns→breaks fix at `Interpreter.cs:292` means in-body musical-context errors are soft. |
| Copy-paste feedback mapping in Reverb | Schroeder closed-form `10^(-3t/rt60)` | Phase 15 (this phase, D-13) | New overload coexists with roomSize version. |

**Deprecated/outdated:**
- `Pidgin 3.5.1` dependency in `flow-lang.csproj:12` — referenced but not used. Project-level future cleanup (see REQUIREMENTS.md "Future Requirements" → "Pidgin dependency removal").
- `net9.0` references in CLAUDE.md — ACTUAL target is `net10.0`. Documentation lag (see STATE.md Plan 12-06).

## Assumptions Log

All claims in this research were verified against codebase grep or official Microsoft documentation. The table is populated honestly:

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Pidgin 3.5.1 is referenced but unused by the actual parser | State of the Art; Standard Stack | Low — not on the Phase 15 critical path; noted for future cleanup. |
| A2 | `System.Random(seed)` is empirically stable across .NET patch versions (e.g., 10.0.1 → 10.0.2) | Pitfall 7; Standard Stack | Medium — Microsoft documents algorithm as unstable across versions; D-18 accepts major breaks. If a patch version breaks determinism, the byte-identical regression Fact catches it immediately. |
| A3 | Average-delay Schroeder mapping `feedback = 10^(-3 * avgDelayT / rt60)` is the right single-coefficient approximation for a 4-parallel-comb network | Pattern 3; Architecture Diagram DX-07 | Medium — could produce subjectively-wrong tail lengths. Empirical test: render `reverbTime 2.0`, measure decay from -0dB to -60dB in the output buffer, confirm ~2 seconds. If off by a factor, adjust the constant (e.g., use longest delay, not average). The user accepted this as Claude's Discretion per CONTEXT. |
| A4 | Parser's `Match(TokenType.Minus)` consumes the `-` token before evaluating the literal, so parse-time negative-rejection must happen in the `ReverbTime` case body | Pattern 1 step 7; Pitfall 4 | Low — verified by reading Parser.cs:515-516 (Pan case). |
| A5 | `SectionData.Context` is a snapshot taken once per section, not live | Pitfall 2 | Low — verified by reading Interpreter.cs:375 (`var musicalContext = _context.GetMusicalContext()` then passed to new SectionData at line 425). |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | All build + test | ✓ | 10.0.106 | — |
| `Melanchall.DryWetMidi` | MIDI read in regression Fact | ✓ (transitively via csproj) | 8.0.3 | — |
| xUnit.v3 | Test execution | ✓ (transitively via csproj) | 3.2.2 | — |
| PulseAudio | Only for real-time playback (not required for DX-07 offline render) | ✓ | System | `writeWav` + file-based test assertions |
| `dotnet` CLI | Build, test, script execution | ✓ | 10.0.106 | — |

No external services, databases, message queues, or network dependencies are involved. Phase 15 is pure in-process code execution. [VERIFIED: `dotnet --version` returned 10.0.106, 2026-04-20]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (no separate `xunit.runner.json`) |
| Quick run command | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase15" --nologo` |
| Full suite command | `dotnet test flow-sharp.sln --nologo` |

### Phase Requirements → Test Map

Each test is a concrete xUnit `[Fact]` with an observable-value pin (error text, byte array, numeric frame count, or MIDI byte sequence). Manual-only tests are NOT permitted; every criterion below MUST be automated to < 30 seconds.

| Req ID | Behavior | Test Type | Automated Command | File / Fact |
|--------|----------|-----------|-------------------|-------------|
| DX-07 + D-01 | `reverbTime 2.5 { }` parses and stores in context | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_Positive_StoresInContext"` | `Unit/Phase15/ReverbTimeContextTests.cs` — NEW |
| DX-07 + D-02 | `reverbTime 0 { }` produces dry output (no Reverb.Apply) | integration | `dotnet test --filter "ReverbTimeRenderTests.Zero_ShortCircuitsReverb"` | `Integration/Phase15/ReverbTimeRenderTests.cs` — NEW. Assert: output frame-count identical to render WITHOUT `reverbTime 0` wrapper; OR assert `Reverb.Apply` not invoked via a test-double sentinel. Observable pin: byte-identical comparison of WAV files. |
| DX-07 + D-03 (negative) | `reverbTime -2.5 { }` raises parse error at the `-` location | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_Negative_ParseError"` | `Unit/Phase15/ReverbTimeContextTests.cs` — NEW. Assert error count == 1, error text contains "reverbTime cannot be negative". |
| DX-07 + D-03 (clamp) | `reverbTime 45 { }` silently clamps to 30.0 in context | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_AboveMax_ClampsTo30"` | `Unit/Phase15/ReverbTimeContextTests.cs` — NEW. Assert `GetMusicalContext().ReverbTime == 30.0`. |
| DX-07 + D-04 | Nested `gain 0.5 { reverbTime 2.0 { } }` composes independently | unit | `dotnet test --filter "ReverbTimeContextTests.Nested_WithGain_Independent"` | `Unit/Phase15/ReverbTimeContextTests.cs` — NEW. Assert both Gain and ReverbTime resolve at innermost frame. |
| DX-07 + D-13 | `Reverb.Apply(buffer, rt60, damping, mix)` new overload maps RT60 → feedback correctly | unit | `dotnet test --filter "ReverbApplyRt60Tests.Rt60_ProducesExpectedDecay"` | `Unit/Phase15/ReverbApplyRt60Tests.cs` — NEW. Pin: for `rt60 = 2.0s` at 44100Hz, render 3-second impulse, assert sample at 2.0s is ~-60dB (tolerance ±3dB). |
| DX-07 + D-14 | Per-voice reverb applied in SongRenderer | integration | `dotnet test --filter "ReverbTimeRenderTests.PerVoice_Applies"` | `Integration/Phase15/ReverbTimeRenderTests.cs` — NEW. Render section with `reverbTime 2.0`; compare WAV tail-length vs reference no-reverb render. |
| DX-07 + D-16 | Explicit `reverb()` + context RT60 stack | integration | `dotnet test --filter "ReverbTimeRenderTests.Explicit_And_Context_Stack"` | `Integration/Phase15/ReverbTimeRenderTests.cs` — NEW. Observable: output WAV byte-count greater than single-reverb baseline. |
| DX-09 + D-05 swing range | `swing = 1.5` clamps to 1.0 | unit | `dotnet test --filter "EuclideanSwingTests.Swing_AboveMax_ClampsTo1"` | `Unit/Phase15/EuclideanSwingTests.cs` — NEW. Assert max velocity in result == base + 1.0 (clamped). |
| DX-09 + D-05 negative | `swing = -0.5` anti-accents off-beats | unit | `dotnet test --filter "EuclideanSwingTests.NegativeSwing_AccentsOffBeats"` | `Unit/Phase15/EuclideanSwingTests.cs` — NEW. For `(euclidean 3 8 C4 -0.3)`, assert off-beat indices (those NOT at multiples of gridStep) have higher velocity than on-beat indices. |
| DX-09 + D-06 on-beat | On-beat = step indices ≡ 0 (mod gridStep) | unit | `dotnet test --filter "EuclideanSwingTests.OnBeat_DetectionMatchesGrid"` | `Unit/Phase15/EuclideanSwingTests.cs` — NEW. For (3, 8): gridStep = 2, on-beats at step 0, 2, 4, 6 (all hits happen to land on even steps). For (5, 8): gridStep = 1, all hits on-beat. |
| DX-09 + D-07 delta | `swing = 0.25` adds exactly 0.25 to accented set | unit | `dotnet test --filter "EuclideanSwingTests.AccentAmount_IsRawDelta"` | `Unit/Phase15/EuclideanSwingTests.cs` — NEW. Assert accented hit velocity == base + 0.25 (± epsilon). |
| DX-09 + D-08 asymmetry | Only accented set moves; other stays at base | unit | `dotnet test --filter "EuclideanSwingTests.Asymmetric_UnaccentedStaysAtBase"` | `Unit/Phase15/EuclideanSwingTests.cs` — NEW. |
| DX-09 + D-09 humanize unit | `humanize = 0.1` → jitter in ±0.1 | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_JitterInRange"` | `Unit/Phase15/EuclideanHumanizeTests.cs` — NEW. Assert all perturbed velocities ∈ [base - 0.1, base + 0.1]. |
| DX-09 + D-10 clamp | `humanize = 2.0` clamps to 1.0 | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_AboveMax_ClampsTo1"` | `Unit/Phase15/EuclideanHumanizeTests.cs` — NEW. |
| DX-09 + D-11 distribution | Uniform distribution over `[-humanize, +humanize]` | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_Uniform_NotGaussian"` | `Unit/Phase15/EuclideanHumanizeTests.cs` — NEW. Run `euclidean` 1000 times with different seeds; histogram the perturbations into 10 buckets over [-0.1, +0.1]; assert all buckets within ±30% of uniform expected count (100 each). Loose tolerance prevents false-red on statistical noise. |
| DX-09 + D-12 overflow | Perturbed velocity clamps to `[0, 1]`, not reflects | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_Overflow_Clamps"` | `Unit/Phase15/EuclideanHumanizeTests.cs` — NEW. Set dynamics ff (0.98) + humanize 0.5 + seed that forces +0.5 jitter → velocity saturates at 1.0, not wraps. |
| DX-09 + D-17 isolation | Two `euclidean` calls do NOT cross-contaminate PRNG despite other RNG consumption between them | unit | `dotnet test --filter "EuclideanHumanizeTests.LocalPrng_IsolatedAcrossCalls"` | `Unit/Phase15/EuclideanHumanizeTests.cs` — NEW. Sequence: `euclidean(..., seed=42)` → `vary(seq, 0.3, seed=99)` (consumes global seeded RNG) → `euclidean(..., seed=42)` again. Assert: both `euclidean` results are byte-identical. |
| DX-09 + D-18 byte-identical | Two runs of identical script produce byte-identical MIDI | integration | `dotnet test --filter "EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi"` | `Integration/Phase15/EuclideanByteIdenticalTests.cs` — NEW. Uses `Shared/MidiReadHelpers.GetVelocityBytes` (promoted from Phase 14 inline per DEFER-05). Run the script twice to two different output paths; assert `File.ReadAllBytes(p1) == File.ReadAllBytes(p2)`. Also pin the expected bytes (recorded empirically on first authoring, per Phase 14 DX-08 pattern). |
| DX-09 + D-18 across runs | Same seed produces same output in fresh process | integration (script-level) | existing `FlowScriptData` Theory row | `tests/test_euclidean_humanize.flow` — NEW. Double-execution pattern via stdout sentinel. |
| ROADMAP #1 swing=velocity-accent | `euclidean(3,8,C4,swing)` applies swing as velocity (not timing) | unit | `dotnet test --filter "EuclideanSwingTests.Swing_ChangesVelocity_NotTiming"` | `Unit/Phase15/EuclideanSwingTests.cs` — NEW. Assert note durations identical across `swing=0` and `swing=0.5`; only velocities differ. |
| ROADMAP #2 byte-identical MIDI+WAV | Two renders produce byte-identical MIDI AND WAV | integration | `EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi` + `EuclideanByteIdenticalTests.SameSeed_ByteIdenticalWav` | `Integration/Phase15/EuclideanByteIdenticalTests.cs` — NEW. |
| ROADMAP #3 rejects negative (CONTEXT overrides "and zero") | `reverbTime -2.5` errors; `reverbTime 0` dry per D-02 | unit | `ReverbTimeContextTests.Parse_Negative_ParseError` + `ReverbTimeContextTests.Parse_Zero_ProducesDry` | As above. **⚠️ ROADMAP criterion #3 wording "rejects negative or zero" CONTRADICTS CONTEXT D-02 — plan to correct ROADMAP in phase closure (analogous to Phase 12 TEST-03 REQUIREMENTS reframe).** |
| ROADMAP #4 nesting resolves correctly | Nested reverbTime inside tempo/key resolves | unit | `ReverbTimeContextTests.Nested_InsideTempoAndKey_Resolves` | `Unit/Phase15/ReverbTimeContextTests.cs` — NEW. |
| ROADMAP #4 early-break predicate | 8th field update doesn't break walk | unit | `ReverbTimeContextTests.GetMusicalContext_AllFieldsResolvedSearchesReverbTime` | `Unit/Phase15/ReverbTimeContextTests.cs` — NEW. Put ReverbTime only at root, all other fields at inner frames; assert inner `GetMusicalContext()` still sees ReverbTime. |
| ROADMAP #5 collision grep | No identifier collisions | one-shot grep | bash one-liner; transcript pinned in 15-VERIFICATION.md | Not automated; matches Phase 14 D-21 pattern. |

### Sampling Rate

- **Per task commit:** `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase15" --nologo` (< 10 seconds expected for Phase 15 Facts).
- **Per wave merge:** `dotnet test flow-sharp.sln --nologo` (full suite; Phase 14 baseline was 81 Facts ~30s).
- **Phase gate:** Full suite green before `/gsd-verify-work`. All Phase 15 Facts GREEN. `tests/test_*.flow` Theory rows GREEN.

### Wave 0 Gaps

Items to create before any implementation work begins:

- [ ] `flow-lang.Tests/Unit/Phase15/` — directory
- [ ] `flow-lang.Tests/Integration/Phase15/` — directory
- [ ] `flow-lang.Tests/Shared/MidiReadHelpers.cs` — promoted from Phase 14 inline helper (DEFER-05 trigger; reused by `EuclideanByteIdenticalTests`). Signature:
  ```csharp
  internal static class MidiReadHelpers {
      public static byte[] GetVelocityBytes(string midiPath);
      public static int[] GetNoteNumbers(string midiPath);
      public static byte[] ReadAllBytes(string midiPath);
  }
  ```
- [ ] `tests/test_reverb_time.flow` — script-level sanity + Theory row entry in `FlowScriptData.cs`
- [ ] `tests/test_euclidean_swing.flow` — script-level sanity + Theory row
- [ ] `tests/test_euclidean_humanize.flow` — script-level sanity + Theory row
- [ ] `tests/output/.gitignore` entry (if not present) for new WAV/MIDI regression artifacts

No framework install needed; xUnit.v3 already ships in the solution. No new NuGet packages.

## Security Domain

Flow is a single-user, local-execution scripting language for music production. The workflow executes user-authored `.flow` scripts against a trusted local runtime.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | No auth surface (local execution). |
| V3 Session Management | no | No sessions. |
| V4 Access Control | no | Runs with user privileges; writes to user-chosen paths. |
| V5 Input Validation | yes (limited) | Parser validates RT60 negatives (D-03); `Math.Clamp` on swing/humanize. |
| V6 Cryptography | no | No crypto operations in DX-07 or DX-09. |

### Known Threat Patterns for {this stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| File-write to arbitrary paths via `writeMidi` / `writeWav` | Tampering (on user's own disk) | Existing behavior — user controls the path argument. Phase 12 Plan 05 added auto-mkdir; no additional mitigation needed. |
| Integer overflow in `steps * hits` on humongous inputs | Denial of Service (memory exhaustion from array allocation) | Existing `euclidean` check `hits > steps` throws. DX-09 should also add reasonable upper bounds (e.g., steps ≤ 1024) with a clear error message. **Recommendation:** planner adds a guard `if (steps > 1024) throw …`. |
| PRNG seed collision (two scripts both seed 42 → same output) | Not a threat — it's the feature (D-18) | Documented in user-facing docs. |

Phase 15 introduces no new security surface beyond these existing controls.

## Open Questions

1. **Base velocity source for DX-09 accent math**
   - What we know: Existing `NoteStreamCompiler.cs:341` uses `note.Velocity ?? context.Velocity ?? 0.63`. The existing `euclidean` built-in does NOT read context (it stores default 0.63).
   - What's unclear: Should DX-09's swing/humanize operate against `context.Velocity ?? 0.63` or a hard-coded 0.63?
   - Recommendation: **Read context.Velocity**, matching NoteStreamCompiler. This makes `dynamics f { euclidean 3 8 C4 0.3 }` produce "forte accented" output naturally. Register the euclidean overloads as context-dependent via `RegisterContextDependent` (pattern from `SongRenderer.cs:30-36`). Raise as a Plan-time design decision if ambiguous.

2. **`tests/output/` artifact management**
   - What we know: Phase 14 wrote `tests/output/dynamics_velocity.mid` and compared read-back bytes.
   - What's unclear: Should Phase 15's regression MIDI/WAV files be checked into git (for bisect diffs) or gitignored (for clean working tree)?
   - Recommendation: **Gitignore.** The Facts regenerate the artifacts on each run. No need to carry binary diffs.

3. **Reverb feedback upper cap**
   - What we know: Existing `ProcessChannel` hard-computes `feedback = 0.7 + roomSize * 0.28` (max 0.98). New overload uses `10^(-3*t/rt60)`; at rt60 = 30s, feedback ≈ 0.993.
   - What's unclear: Should the new overload cap at 0.98 (match existing) or 0.99 (freshly computed)?
   - Recommendation: **Cap at 0.99** (Pattern 3 example); this matches the 30s clamp intent — composers ask for 30s, we give them ~30s. Capping at 0.98 would silently shorten the tail.

4. **`euclidean` hits == 0 or hits > steps edge cases**
   - What we know: Existing code throws `InvalidOperationException("euclidean: hits must be > 0")` etc. at `BuiltInFunctions.cs:1042-1044`.
   - What's unclear: Does DX-09 keep the same hard errors, or soften them per charitable-interpretation?
   - Recommendation: **Keep hard errors** for `hits <= 0` and `steps <= 0` (no defensible meaning); **keep the existing "hits > steps"** path (Bjorklund's edge case) but per DX-09 consistency, perhaps clamp `hits = min(hits, steps)` silently. CONTEXT does not rule on this; escalate during plan-check.

## Sources

### Primary (HIGH confidence)

- **CLAUDE.md** — project instructions, in repo root; [VERIFIED: read verbatim 2026-04-20]
- **.planning/phases/15-composer-dx-part-2/15-CONTEXT.md** — all 18 decisions [VERIFIED: read verbatim]
- **.planning/REQUIREMENTS.md** lines 52, 54 — DX-07 and DX-09 wording [VERIFIED]
- **.planning/ROADMAP.md** lines 174-185 — Phase 15 success criteria [VERIFIED]
- **.planning/STATE.md** — milestone state, blockers, accumulated decisions [VERIFIED]
- **.planning/phases/14-composer-dx-part-1/14-CONTEXT.md** — D-13 two-pass strict precedent [VERIFIED]
- **.planning/phases/14-composer-dx-part-1/deferred-items.md** — DEFER-02/03/05 [VERIFIED]
- **Microsoft Learn — System.Random constructor** — algorithm stability caveat [CITED: https://learn.microsoft.com/en-us/dotnet/api/system.random.-ctor?view=net-10.0]
- **Codebase (grepped)**:
  - `flow-lang/Ast/Statements/MusicalContextStatement.cs:8` [VERIFIED]
  - `flow-lang/Runtime/MusicalContext.cs:35-60,95-107` [VERIFIED]
  - `flow-lang/Runtime/ExecutionContext.cs:186-212` [VERIFIED]
  - `flow-lang/Parsing/Parser.cs:100-132,480-566` [VERIFIED]
  - `flow-lang/Interpreter/Interpreter.cs:131-292,370-432` [VERIFIED]
  - `flow-lang/Lexing/SimpleLexer.cs:580-608`, `TokenType.cs` [VERIFIED]
  - `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs:1-160` (full file) [VERIFIED]
  - `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs:30-73` [VERIFIED]
  - `flow-lang/StandardLibrary/Audio/SongRenderer.cs:1-190` [VERIFIED]
  - `flow-lang/StandardLibrary/Audio/Voice.cs:1-40` (full file) [VERIFIED]
  - `flow-lang/StandardLibrary/Audio/MidiExport.cs:180-210` [VERIFIED]
  - `flow-lang/StandardLibrary/BuiltInFunctions.cs:1000-1110` [VERIFIED]
  - `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:60-110` [VERIFIED]
  - `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:660-700` [VERIFIED]
  - `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:200-250` [VERIFIED]
  - `flow-lang/TypeSystem/SpecialTypes/SectionType.cs:1-40` (full file) [VERIFIED]
  - `flow-lang/std.flow:122-135` (humanize + euclidean proc declarations) [VERIFIED]
  - `flow-lang/flow-lang.csproj`, `flow-lang.Tests/flow-lang.Tests.csproj` [VERIFIED]

### Secondary (MEDIUM confidence)

- **.planning/research/STACK.md + SUMMARY.md + PITFALLS.md (Phase-11 era)** — early Phase 11 research; confirms Schroeder RT60 mapping formula and identifier-collision discipline [VERIFIED against source material]
- **Phase 14 DynamicsMidiVelocityTests.cs** — empirical confirmation that DryWetMidi 8.0.3 `MidiFile.Read` + `GetNotes` + `(byte)Velocity` projection works [VERIFIED: test file read + commit 152e593]
- **Phase 12 Plan 05 notes on `if`-overload wildcard matching** — surfaced the `InternalFunctionRegistry.TypesEqual` `LazyType` gotcha; relevant to Pitfall 5 [VERIFIED from STATE.md accumulated context]

### Tertiary (LOW confidence — flagged)

- **Empirical `System.Random(seed)` stability across .NET patch versions** — inferred from lack of public counter-evidence, not from a Microsoft guarantee. Regression Fact is the mitigation.

## Metadata

**Confidence breakdown:**

- Standard stack: **HIGH** — all libraries verified in-repo; no new NuGets; versions confirmed from csproj.
- Architecture patterns: **HIGH** — mirror of shipped `gain`/`pan` patterns; 7 prior context fields work identically.
- Pitfalls: **HIGH** — 8 pitfalls all traced to specific code sites or prior-phase learnings.
- Schroeder RT60 mapping: **MEDIUM** — standard DSP formula; exact constant might need tuning (flagged in Open Questions #3).
- PRNG stability across .NET versions: **MEDIUM** — empirical assumption (see A2).

**Research date:** 2026-04-20
**Valid until:** 2026-05-20 (30 days for stable areas; re-verify System.Random stability if .NET patch lands in this window)
