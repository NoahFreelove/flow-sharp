# Architecture Research — v1.2 Stability & Composer DX

**Domain:** Flow Language interpreter (C# .NET) — composer DX feature additions + bug fixes
**Researched:** 2026-04-18
**Confidence:** HIGH (integration points verified against HEAD); MEDIUM on swing/humanize musical fit

---

## ⚠️ Audit Re-Verification (CRITICAL — conflicts with Pitfalls agent)

The architecture researcher verified each C1–C7 claim against actual source. Four may be false positives. **The pitfalls researcher disagrees on C1 and C5** — treat this section as a hypothesis requiring human verification, not ground truth.

| # | Audit Claim | Arch researcher | Pitfalls researcher | Evidence |
|---|-------------|-----------------|----------------------|----------|
| C1 | `ExecuteMusicalContext` leaks frames on early-return | Likely false (try/finally pops) | Real bug — early returns skip block body | Both agents read the same file and disagree |
| C2 | `_returnValue` short-circuits | Partial; guard exists, behavior unclear | Real (pairs with C1 fix) | Needs repro |
| C3 | `EnvelopeProcessor` div-by-zero | Likely false (loop body gated on N≥1) | `Math.Max(1, frames)` is wrong fix; skip zero-length segments | Agents agree fix is not trivial |
| C4 | `BufferHelpers` div-by-zero | Likely false (same pattern) | Same as C3 | Needs repro |
| C5 | `augment`/`diminish` swapped | Likely false (correct semantics) | Confirmed swapped at TransformFunctions.cs:247,268 | **Needs human verification** |
| C6 | `init([])` silent empty | CONFIRMED | CONFIRMED | Real bug |
| C7 | `Thunk` cache corruption | CONFIRMED | Refined: `_isEvaluated` NOT set on exception; re-evaluation loops | Real bug, different mechanism |

**Action required before planning:** Phase A must include an explicit "reproduce or close" spike for C1/C2/C3/C4/C5 before fixing anything. Changing working code risks regressions.

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                       SOURCE (.flow)                                 │
│   reverbTime 0.6 { | C4 D4 E4 |h }                                   │
│   slice(seq, 0, 4)          Cb4  (enharmonic)                        │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────────┐
│  LEXER (SimpleLexer.cs)                                              │
│   - Keyword table ~580  [+reverbTime, +Cb/Bs enharmonics]            │
│   - Note detection ~676 [+flat/double-flat alterations]              │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ Token[]
┌──────────────────────────┴──────────────────────────────────────────┐
│  PARSER (Parser.cs)                                                  │
│   - Stmt dispatch ~101-132  [+if Match(TokenType.ReverbTime)]        │
│   - ParseMusicalContextStmt ~420-540 [+case ReverbTime]              │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ AST
┌──────────────────────────┴──────────────────────────────────────────┐
│  INTERPRETER (Interpreter.cs)                                        │
│   - ExecuteMusicalContext ~130-290  [+case ReverbTime]               │
│   - ExecuteStatement ~71-128 (C2 guard lives here)                   │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────────┐
│  RUNTIME                                                             │
│   MusicalContext.cs     [+double? ReverbTime; Clone()+ToString()]    │
│   ExecutionContext.cs   [GetMusicalContext resolution update]        │
│   Thunk.cs              [C7 fix: exception caching]                  │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────────┐
│  STANDARD LIBRARY                                                    │
│   BuiltInFunctions.cs   [+slice, +euclidean humanize/swing]          │
│   Collections.cs        [C6: init([]) error]                         │
│   Transforms/           (dynamic transforms already write Velocity)  │
│   Audio/DSP/Reverb.cs   [+reverbTimeSeconds parameter]               │
│   Audio/MidiExport.cs   (already reads note.Velocity line 192)       │
│   Audio/SongRenderer.cs [reads resolved ctx.ReverbTime]              │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Feature-by-Feature Integration

### Feature 1 — `slice(seq, start, end)`
**Files MODIFIED:** `BuiltInFunctions.cs`, optionally `audio.flow`. **NEW:** None.
**Signatures:** `slice(Sequence, Int, Int) → Sequence` and `slice(Array[T], Int, Int) → Array[T]`.
**Data flow:** Pure function; uses `sequence.Bars.Skip/Take` + `AddBar()` to preserve `TotalBeats`.

### Feature 2 — Enharmonic helpers (`H` = B, `Db` ↔ `C#`)
**Files MODIFIED:** `NoteType.cs` (Parse line 21), `SimpleLexer.cs` (~676, ~580), `BuiltInFunctions.cs` (add `enharmonic()`). **NEW:** None.
**Design:** Parse-time normalization — `Db4` → `(D, 4, -1)` triple. No parallel alteration system.
**Collision risk (Pitfalls):** `H` as global alias breaks `Int H` variables. Scope the alias to note-stream (`| ... |`) context only.
**Coupling:** `ChordParser.cs:173-175` also calls `NoteType.Parse` — retest after broadening alterations.

### Feature 3 — `reverbTime { ... }` context
**Files MODIFIED (ordered):**
1. `TokenType.cs` — add `ReverbTime`
2. `SimpleLexer.cs` — keyword switch (~580)
3. `MusicalContextStatement.cs` — add enum case
4. `Parser.cs` — dispatch (~101-132), switch case (~420). Use `Pan`/`Gain` lookahead guard (function-name conflict)
5. `MusicalContext.cs` — `public double? ReverbTime`; extend `Clone()` + `ToString()`
6. `ExecutionContext.cs:186` — `resolved.ReverbTime ??= frame.MusicalContext.ReverbTime;` **update early-break predicate at 201-205** (adding 8th property)
7. `Interpreter.cs:~137` — new `case MusicalContextType.ReverbTime` mirroring `Gain`/`Pan` validation
8. `Audio/DSP/Reverb.cs:26` — add `reverbTimeSeconds` param (RT60 formula `feedback = 10^(−3 × delaySeconds / rt60)`) OR compute `roomSize` from `reverbTime` at call site
9. `Audio/SongRenderer.cs` or voice render site — read `ctx.ReverbTime`, thread into reverb call

**Collision risk (Pitfalls):** grep `examples/`, `tests/`, stdlib `.flow` for `reverbTime` identifier before adding keyword.
**Coupling with C1:** No hard dependency (even if C1 real, fixes are orthogonal). Still, do ExecuteMusicalContext hardening first.

### Feature 4 — MIDI velocity from dynamics
**Major finding: partially already implemented.**
- `MusicalNoteData.Velocity` exists (NoteType.cs:184, 0–1 range)
- `crescendo`/`decrescendo`/`swell` write to `Velocity` via `ApplyVelocityGradient` (TransformFunctions.cs:395-474)
- `MidiExport.cs:192` reads `note.Velocity`, maps to MIDI 1–127

**Open questions (Pitfalls agent flags as 1-hour vs 1-day deciding):**
1. Does `dynamics { }` context propagate to `MusicalNoteData.Velocity` at compile time in `NoteStreamCompiler`?
2. Should MIDI `Velocity` floor of 1 (vs 0) be a rest threshold?

**Files MODIFIED (if gaps):** `NoteStreamCompiler.cs` (verify), `MidiExport.cs:192` (optional). **NEW:** None.

### Feature 5 — Euclidean swing/humanize
**Files MODIFIED:** `BuiltInFunctions.cs` (extend `euclidean` line 1013-1054). **NEW:** None.
**Constraint:** `MusicalNoteData` has no timing-offset field. Real micro-timing requires data-model change.
**Recommendation:** v1.2 = swing-as-velocity-accent + velocity humanize (uses existing `Velocity`). Defer micro-timing jitter to v1.3.
**Determinism (Pitfalls):** `euclidean` with humanize MUST take a required `seed` param; `System.Random` isn't stable across .NET patch versions. "Code is the score" reproducibility is a core value.

### Bug C6 — `init([])`
**File:** `Collections.cs:84-92`. Throw to match `head`/`last`. Breaking change risk LOW.

### Bug C7 — `Thunk.Force`
**File:** `Thunk.cs:27-46`. Cache exception, re-throw on retry. Matches `Lazy<T>` semantics.

---

## Data Flow Changes

### MIDI Velocity End-to-End

```
  [Source .flow]
  dynamics mf { | C4 D4 E4 F4 | } -> crescendo(0.3, 0.9)
        ↓
  [Parser] MusicalContextStatement(Dynamics, 0.63, body)
        ↓
  [Interpreter.ExecuteMusicalContext]
      CurrentFrame.MusicalContext.Velocity = 0.63
        ↓
  [NoteStreamCompiler] ← CRITICAL PATH, needs verification
      MusicalNoteData(…, velocity: ctx.Velocity ?? 0.63)
        ↓
  [crescendo transform]
      rewrites Velocity start→end gradient
        ↓
  [MidiExport.cs:192]
      byte velocity = Clamp((int)(note.Velocity * 127), 1, 127)
        ↓
  [MIDI file]
```

**Uncertain hop:** NoteStreamCompiler → MusicalNoteData. If NoteStreamCompiler uses default 0.63 and ignores active `MusicalContext.Velocity`, context silently doesn't propagate. Roadmap must include explicit verification task.

### reverbTime End-to-End

```
  [Source] reverbTime 0.6 { …voices… }
        ↓
  MusicalContextStatement(ReverbTime, 0.6, body)
        ↓
  CurrentFrame.MusicalContext.ReverbTime = 0.6
        ↓
  Voice render: ctx = GetMusicalContext(); if ctx.ReverbTime: Reverb.Apply(…)
        ↓
  Mixed buffer with reverb tail
```

---

## Build Order

### Phase A — Stability (independent; can run parallel within phase)
1. **Audit spike** — reproduce or close C1/C2/C3/C4/C5 (<1hr each).
2. **C6 + C7** — both real bugs, independent files.
3. **Test unblocking** — `range(Int, Int)`, `break`/`continue`, `bpm`/`createStereoTrack`/`renderBars`.
4. **Nyquist validation** — v1.1 phases 6-9 retroactive.

### Phase B — Composer DX (ordered by surface area)
5. **Feature 4: MIDI velocity** (verification first) — smallest if already works.
6. **Feature 1: slice()** — pure addition.
7. **Feature 5: Euclidean swing/humanize** — reuses velocity infra.
8. **Feature 2: Enharmonic helpers** — NoteType.Parse touch.
9. **Feature 3: reverbTime** — widest blast radius (9 files). LAST.

### Phase C — Tutorial refresh
10. `examples/tutorial.flow` demonstrating v1.1 + v1.2.

### Rationale
- Stability first → clean validation bar for DX work.
- Within Phase B: smallest surface before largest.
- (4) → (5) reuses velocity mechanism.
- (3) last so earlier DX work stabilizes.

---

## Integration Points Summary

| Feature/Bug | NEW files | MODIFIED files | Blocks? | Blocked by? |
|-------------|-----------|----------------|---------|-------------|
| `slice()` | none | BuiltInFunctions.cs, audio.flow | none | none |
| Enharmonics | none | NoteType.cs, SimpleLexer.cs, BuiltInFunctions.cs | none | none |
| `reverbTime` | none | 9 files across lexer/parser/AST/runtime/stdlib | none | none |
| MIDI velocity | none | NoteStreamCompiler.cs (verify), MidiExport.cs (opt) | feature 5 | none |
| Euclidean swing | none | BuiltInFunctions.cs | none | (4) if shared |
| C6 init([]) | none | Collections.cs | none | none |
| C7 Thunk | none | Thunk.cs | none | none |
| Audit spike (C1–C5) | none | TBD (only real bugs) | Phase B start? | none |
| Tutorial | — | examples/tutorial.flow | none | all features |

---

## Architectural Patterns To Reuse

1. **Musical context as scoped stack** — add `ReverbTime` mirroring `Gain`/`Pan`.
2. **Built-in registration via `FunctionSignature` + lambda** — `slice` and euclidean fit directly.
3. **Immutable AST records + interpreter switch-dispatch** — `MusicalContextType.ReverbTime` uses existing dispatch.
4. **Error accumulation via `ErrorReporter.ReportError()`** — all new validation reports, doesn't throw.
5. **Value factory methods** — `slice` returns `Value.Sequence(...)`.

## Anti-Patterns To Avoid

1. Mutating `SequenceData.Bars` directly — use `AddBar()`.
2. Parallel alteration system for flats — reuse `(char, int, int)` triple.
3. `reverbTime` as function — use context block for consistency with `gain`/`pan`.
4. "Fixing" C1–C5 without reproducing first.
5. Baking micro-timing swing into note position in v1.2 — defer.

---

## Key File Paths (absolute)

- `flow-lang/Interpreter/Interpreter.cs` (71-128 C2, 130-290 reverbTime)
- `flow-lang/Runtime/MusicalContext.cs` (add ReverbTime)
- `flow-lang/Runtime/ExecutionContext.cs` (186-212 resolution + early-break)
- `flow-lang/Runtime/Thunk.cs` (C7)
- `flow-lang/Runtime/NoteStreamCompiler.cs` (verify velocity propagation)
- `flow-lang/Lexing/TokenType.cs`, `flow-lang/Lexing/SimpleLexer.cs` (570-605, 670-701)
- `flow-lang/Parsing/Parser.cs` (100-132, 420-540)
- `flow-lang/Ast/Statements/MusicalContextStatement.cs`
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (21-73, 174-211)
- `flow-lang/StandardLibrary/Collections.cs` (84-92)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (1013-1054)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (239-279, 395-474)
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` (100-172 — C3 re-audit)
- `flow-lang/StandardLibrary/Audio/BufferHelpers.cs` (115-168 — C4 re-audit)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` (192)
- `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` (26)
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs`

---

## Confidence & Gaps

- **HIGH:** Integration points for reverbTime, slice, MIDI velocity current state, C6/C7 real.
- **MEDIUM:** Euclidean swing-via-velocity musical acceptability (prototype first).
- **UNKNOWN — requires human verification:** C1, C2, C3, C4, C5 (agents disagree; audit was self-declared speculative).
- **LOW — explicit task in Feature 4:** Whether `NoteStreamCompiler` (647 lines) reads `MusicalContext.Velocity`.

---

*Architecture research for: Flow Language v1.2*
*Researched: 2026-04-18*
