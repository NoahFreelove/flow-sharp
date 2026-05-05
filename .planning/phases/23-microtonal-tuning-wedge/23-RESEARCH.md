# Phase 23: Microtonal Tuning (Wedge) — Research

**Researched:** 2026-05-03
**Domain:** Render-time named-tuning wedge — three pragma-activated tuning systems hijacking `PitchConversion.NoteToFrequency`, with mode-aware ratio tables and one-shot non-12-TET diagnostics. Score-time transforms remain pitch-class agnostic.
**Confidence:** HIGH (locked decisions + existing chokepoint already mapped + ratio tables verified against Wikipedia/Mudcat canonical references)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**JI / Pythagorean Tonic Resolution**
- **D-01:** Under non-12-TET, the 1/1 reference pitch is read from the innermost active `MusicalContext.Key`. Symmetric with how `transpose`/`HarmonyFunctions` already consult `MusicalContext.Current.Key`. Innermost-key-wins matches Phase 24 LINT-03 nested-key semantics.
- **D-02:** No `key` block in scope under non-12-TET → silently root at C major (tonic = C, mode = major). Documented in pragma reference + `PitchConversion.NoteToFrequency` doc comment. Aligns with `feedback_charitable_interpretation` memory.
- **D-03:** Mode SHIFTS the chromatic ratio table — natural minor uses 6/5 minor third, dorian uses 9/8 second + 6/5 third + 9/8 sixth, etc. Phase 23 ships seven mode-specific JI tables AND seven mode-specific Pythagorean tables: major + natural minor + dorian + phrygian + lydian + mixolydian + locrian.
- **D-04:** `ScaleDatabase.ParseKeyName` is extended to recognize the five church-mode suffixes (`dorian`, `phrygian`, `lydian`, `mixolydian`, `locrian`) alongside existing `major`/`minor`. Each parses to a `(root, mode)` tuple consumed both by tuning-table lookup AND by future Phase 24 `scaleLint`. Harmonic minor / melodic minor are out of scope.

**Pragma → Renderer Plumbing**
- **D-05:** Active tuning lives on `MusicalContext.Tuning` as a top-level (NOT Push/Pop) property. The pragma is file-scope — a stack-scoped property would be over-engineered.
- **D-06:** `FlowEngine.Run()` reads the entry-point `Program.Pragmas`, resolves to a tuning value, and sets `MusicalContext.Tuning` once before interpretation begins. `ModuleLoader.cs` does NOT touch tuning state. Imported modules render in the caller's tuning.
- **D-07:** REPL pragma extraction stays per-line per Phase 21 D-07, but the **resolved** `MusicalContext.Tuning` PERSISTS across REPL lines until another tuning pragma replaces it or the session ends. Documented departure from strict pragma-scope semantics — required for usable interactive composition. Departure must appear in pragma reference doc + REPL `--help`.
- **D-08:** Default tuning when no `enable` pragma is declared is `equalTemperament` (12-TET). Explicit `enable equalTemperament;` is functionally a no-op (same numeric output) but IS registered + visible to tooling per MICR-01. Used downstream by Phase 24 `enable scaleLint;`.

**Cent Offset & Spelling Under Non-12-TET**
- **D-09:** Spelling-aware tuning tables: `Eb4` and `D#4` produce **different** rendered frequencies under JI/Pythagorean. The chromatic ratio table keys on `(note name, alteration)`, not on semitone offset from tonic. Honors Pitfall 5 #3.
- **D-10:** Cent offsets compose **additively in cent-space**: `freq = tonic_hz × ratio × 2^(cents/1200)`. Composer can write `E4+5c` to fine-tune the JI third. Charitable: cents never silently disappear.
- **D-11:** `enharmonic()` emits a **one-time-per-session stderr warning** when called inside non-12-TET tuning: `[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)`. Conversion still happens. Documented exception to charitable-interpretation memory because the regression is silent and audible.
- **D-12:** Transforms (`transpose`, `invert`, `retrograde`, `augment`, `diminish`) stay MIDI-based per MICR-02. The MIDI-pitch-number invariant is preserved across tunings. When `FromMidi` produces a different spelling than the input, the renderer uses key-aware spelling via `HarmonyFunctions.GetInKeyEnharmonic` (already plumbed in Phase 14). Silent-respelling case (~21 cent shift at enharmonic junctions under non-12-TET) is documented in Phase 23 doc as a known caveat with `transposePreserveSpelling` strict-mode noted as v1.4 candidate.

**MIDI Export Tuning Awareness**
- **D-13:** Phase 23 scope = synthesizer + audio render path only. MIDI export stays 12-TET. When `writeMidi` is called and `MusicalContext.Tuning != EqualTemperament`, emit one-time stderr warning: `[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend (faithful microtonal MIDI deferred to v1.4)`. No pitch-bend infrastructure in this phase.

**Unknown Tuning Names (MICR-03)**
- **D-14:** Unknown tuning names trip the Phase 21 D-12 unknown-pragma error path (Levenshtein did-you-mean + alphabetized known list). Phase 23 extends the error message in tuning-pragma cases to add a final line: `Full Scala (.scl) loader is documented as deferred to v1.4 — see ADR/REQUIREMENTS.md D-03.`

### Claude's Discretion

- Type shape of `MusicalContext.Tuning` (closed enum vs `ITuning` interface vs sealed-record) — recommendation: **closed enum** + `static class TuningTables` keyed by `(TuningSystem, Mode)`. Defer interface refactor to v1.4 when there's a real second extensibility point.
- File layout under `flow-lang/StandardLibrary/Audio/Tuning/` — single file vs split. Planner decides based on table size.
- Exact ratio values — planner pins ONE canonical 5-limit table and ONE canonical 3-limit Pythagorean table with citations.
- Warning channel for D-11 / D-13 — recommendation: `Console.Error.WriteLine` to match existing `transpose` warning style; one-shot guard via per-session HashSet on a static `RenderingDiagnostics` helper.
- Test placement: split or combined — planner decides.
- Determinism gate: tutorial.flow / showcase.flow — recommendation: keep 12-TET (preserve v1.2 byte-identical pin); add separate `tests/test_tuning_determinism.flow` that pins JI/Pythagorean paths independently.

### Deferred Ideas (OUT OF SCOPE)

- Full Scala (`.scl`) loader — deferred to v1.4 per REQUIREMENTS.md D-03.
- Faithful microtonal MIDI export (per-channel pitch-bend) — deferred to v1.4. Phase 23 emits one-time warning per D-13.
- Spelling-preserving transforms (`transposePreserveSpelling`) — v1.4 candidate.
- Block-scope `tuning { ... }` syntax — deferred per Phase 21 D-02.
- Configurable A4 reference frequency — A4 = 440 Hz hard-coded.
- Mode-aware tables for harmonic minor / melodic minor / blues — future work.
- Pre-resolution warning when `enharmonic()` would change pitch under non-12-TET — flow-lsp work post-v1.3.
- REPL meta-command `:tuning ji` — rejected in favor of D-07.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **MICR-01** | Three named tunings ship via pragma (`enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;`); when active, `Note → frequency` lookup at `PitchConversion.NoteToFrequency` consults the active tuning system. Acceptance: `enable justIntonation;` followed by `play(C4 E4)` produces 5:4 ratio (1.25), not 12-TET ~1.2599. | §Standard Stack (closed-enum `TuningSystem`); §Architecture Patterns (chokepoint at `PitchConversion.NoteToFrequency`); §Code Examples (5-limit table, mode-specific overlay) |
| **MICR-02** | Tuning system applies at render-time only. Existing `transpose`, `invert`, `retrograde`, `augment`, `diminish` transforms remain pitch-class-based and tuning-agnostic. Acceptance: `transpose(seq, 5)` produces same MIDI pitch numbers under every tuning; only rendered frequencies differ. | §Architecture Patterns (transforms-stay-MIDI invariant); §Common Pitfalls (Pitfall 5 mitigation matrix); §Validation Architecture (transform-invariance Facts) |
| **MICR-03** | Full Scala (`.scl`) loader documented as deferred to v1.4. Pragma registry rejects unknown tunings with clear error pointing at the documented future expansion. | §Architecture Patterns (D-14 routes through Phase 21 D-12 + final v1.4 pointer line); §Code Examples (extended error message format) |

</phase_requirements>

## Summary

Phase 23 is a **single-chokepoint render-time wedge**: three named tunings activate via Phase 21 pragmas, route through `PitchConversion.NoteToFrequency`, and produce ratio-correct frequencies for the active tonic + mode. Score-level transforms (`transpose`, `invert`, etc.) stay MIDI-based per MICR-02 — the spelled note + key context flow into the renderer untouched, and the renderer alone is tuning-aware. This is exactly the "tuning is a render-time concern, transforms are a score-time concern" mitigation from PITFALLS.md Pitfall 5 #1.

The phase reuses Phase 21's pragma scanner verbatim (one new entry per tuning name in `PragmaRegistry.KnownPragmas`), Phase 14's `GetInKeyEnharmonic` for diatonic-spelling preservation, and Phase 20's multi-letter enharmonic edges (B# ↔ C, Cb ↔ B) which become semantically meaningful under JI for the first time. The only NEW machinery is: a `TuningSystem` enum, a `Mode` enum, a static ratio-table dictionary keyed by `(TuningSystem, Mode, NoteLetter, Alteration)`, a `Tuning` property on `MusicalContext`, a 3-line bridge in `FlowEngine.Execute`, and a tuning-aware code path in `PitchConversion.NoteToFrequency`. Approximately 600–900 LOC of production code.

The single non-trivial design call is **how the renderer reaches the active tuning**: `PitchConversion.NoteToFrequency` is a static helper currently called by every synthesizer with no access to `ExecutionContext`. Two viable options surface in §Architecture Patterns; both leave the public 3-arg API stable and add a tuning-aware overload.

**Primary recommendation:** Implement as Wave 1 = `TuningSystem`/`Mode` enums + ratio tables + xUnit `TuningRatioFacts` (RED→GREEN). Wave 2 = pragma registration + `MusicalContext.Tuning` + FlowEngine bridge + `PitchConversion` tuning-aware overload threaded through synthesizer call chain. Wave 3 = `ScaleDatabase.ParseKeyName` church-mode extension (D-04) + `RenderingDiagnostics` one-shot helper (D-11/D-13) + writeMidi guard. Wave 4 = closure (.flow smoke scripts + determinism gate verification + REQUIREMENTS/STATE/ROADMAP/VERIFICATION).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Pragma extraction (`enable justIntonation;`) | Lexing pre-scan (`PragmaScanner`) | — | Phase 21 chokepoint — closed-set entry added; no new lexer state. |
| Pragma → tuning resolution | Core orchestrator (`FlowEngine.Execute`) | — | D-06: single bridge site; resolves `Program.Pragmas` → `MusicalContext.Tuning` once before interpretation. |
| Active tuning storage | Runtime state (`MusicalContext`) | Render-time read at synthesizers | D-05: top-level (non-stacked) property; symmetric with how synths already read tempo/key. |
| Frequency computation under non-12-TET | Audio render-time (`PitchConversion.NoteToFrequency`) | — | Single chokepoint — every synth + vocalization call site already routes through this. |
| Mode/key-aware tonic resolution | Harmony / scale layer (`ScaleDatabase.ParseKeyName`) | Read at render-time by tuning lookup | D-04 extension benefits both this phase AND Phase 24 `scaleLint`. |
| Score-level transforms (`transpose`, `invert`, …) | Score-time (`TransformFunctions`) | — | MICR-02 invariant: transforms stay MIDI-based; never see `Tuning`. |
| Diatonic-spelling preservation post-transform | Harmony layer (`HarmonyFunctions.GetInKeyEnharmonic`) | Read by `TransformFunctions` | Already plumbed in Phase 14; D-12 reuses verbatim. |
| One-shot non-12-TET warnings | Diagnostics (new `RenderingDiagnostics` static helper) | Called from `enharmonic()` + `writeMidi` | D-11 / D-13: dedup via per-session HashSet; matches existing `Console.Error.WriteLine` style. |
| MIDI export | `flow-midi/Midi/` (no change to pitch-bend) + `MidiExport.WriteMidi` (warning guard added) | — | D-13: out of scope to faithfully encode microtonal MIDI; warning only. |

## Standard Stack

### Core (existing, no change)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | `[VERIFIED: flow-lang.csproj]` — already in use; no new TFM. |
| C# 13 records / file-scoped namespaces | n/a | AST + immutable enums | `[VERIFIED: CLAUDE.md]` — house style; new `TuningSystem` + `Mode` enums sit alongside `TokenType` / `DurationValue`. |

### New (this phase)
| Symbol | Location | Purpose |
|--------|----------|---------|
| `TuningSystem` enum | `flow-lang/StandardLibrary/Audio/Tuning/TuningSystem.cs` | Closed-set: `EqualTemperament`, `JustIntonation`, `Pythagorean`. Aligns user-facing pragma vocabulary with C# identifiers per CONTEXT.md §Specifics. |
| `Mode` enum | `flow-lang/StandardLibrary/Audio/Tuning/Mode.cs` | Closed-set: `Major` (Ionian), `Minor` (Aeolian), `Dorian`, `Phrygian`, `Lydian`, `Mixolydian`, `Locrian`. |
| `TuningTables` static class | `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` | `Dictionary<(TuningSystem, Mode), double[12]>` keyed by tuning + mode, indexed by chromatic semitone offset from tonic with separate sharp/flat columns where they diverge. |
| `RatioMath` helper | `flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs` | `double TonicHzFromKey(string keyName, ITuningSystem tuning)`; cent-offset composition `freq × 2^(cents/1200)` per D-10. |
| `RenderingDiagnostics` static helper | `flow-lang/Diagnostics/RenderingDiagnostics.cs` | One-shot stderr warning channel with per-session `HashSet<string>` dedup. Used by D-11 (`enharmonic` non-12-TET) + D-13 (`writeMidi` non-12-TET). |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Closed enum `TuningSystem` | `ITuningSystem` interface + sealed records | Interface future-proofs for v1.4 Scala loader; closed enum matches `TokenType`/`DurationValue` house style + Phase 21 D-17. **Per CONTEXT.md Claude's Discretion: closed enum recommended; defer interface refactor to v1.4.** `[CITED: 23-CONTEXT.md §Claude's Discretion]` |
| Mode-specific tables for non-major/minor | Single chromatic table indexed by semitone offset | Per D-03 the mode SHIFTS the table — natural minor's 6/5 minor third differs from major's 5/4 major third. Mode-keyed dictionary is the only correct shape. `[VERIFIED: 23-CONTEXT.md D-03 + Mudcat JustInt mode tables]` |
| Threading `ExecutionContext` into synthesizers | Static `MusicalContext.Current` accessor (ambient) | Synthesizers currently call `PitchConversion.NoteToFrequency(note)` with no `ExecutionContext`. **See §Architecture Patterns: Pattern A vs Pattern B.** Recommendation: thread the resolved tuning through the call chain; do not introduce thread-local ambient state. |

**Installation:** No new NuGet packages. Pure C# additions to `flow-lang/StandardLibrary/Audio/Tuning/` and `flow-lang/Diagnostics/`.

**Version verification:** N/A — no external dependencies added per CLAUDE.md "minimal dependencies" constraint.

## Architecture Patterns

### System Architecture Diagram

```
┌──────────────────────────┐
│  .flow source file       │
│  (may declare            │
│   enable justIntonation;)│
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────────────────────┐
│  PragmaScanner.Scan() (Phase 21)         │
│  → (PragmaSet, transformedSource)        │
│   - "justIntonation" / "pythagorean" /   │
│     "equalTemperament" registered in     │
│     PragmaRegistry.KnownPragmas (D-08).  │
│   - Unknown tuning name → D-14 error     │
│     with v1.4 Scala-loader pointer.      │
└────────────┬─────────────────────────────┘
             │
             ▼
┌──────────────────────────┐
│  Parser → Program with   │
│  Pragmas: PragmaSet      │
│  (Phase 21 D-08)         │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────────────────────┐
│  FlowEngine.Execute (D-06 BRIDGE):       │
│  resolve Program.Pragmas →               │
│  MusicalContext.Tuning =                 │
│    TuningSystem.JustIntonation /         │
│    .Pythagorean / .EqualTemperament      │
│  (set ONCE before interpretation;        │
│   ModuleLoader untouched per D-06)       │
└────────────┬─────────────────────────────┘
             │
             ▼
┌──────────────────────────┐
│  Interpreter walks AST   │
│  Score layer:            │
│    transforms operate    │
│    on MIDI numbers       │
│    (MICR-02 invariant —  │
│     tuning INVISIBLE)    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────────────────────┐
│  Render layer (synthesizers, vocaliz.):  │
│  PitchConversion.NoteToFrequency(note,   │
│      tuning, key, mode)                  │
│   ┌──────────────────────────────────┐   │
│   │ if tuning == EqualTemperament:   │   │
│   │   return 12-TET formula          │   │
│   │   (DEFAULT — byte-identical to   │   │
│   │    pre-Phase-23 path).           │   │
│   │                                  │   │
│   │ else (JI / Pythagorean):         │   │
│   │   1. tonicHz = 12-TET freq       │   │
│   │      of (key.root, octave)       │   │
│   │   2. ratio = TuningTables[       │   │
│   │      (tuning, mode)][            │   │
│   │      semitoneFromTonic(          │   │
│   │      note.letter, note.alt,      │   │
│   │      key.root)]                  │   │
│   │   3. freq = tonicHz * ratio      │   │
│   │      * 2^(centOffset/1200)       │   │
│   │      (D-10 cent additivity)      │   │
│   │   4. return freq                 │   │
│   └──────────────────────────────────┘   │
└──────────────────────────────────────────┘
             │
             ├── enharmonic() called inside non-12-TET → RenderingDiagnostics.WarnOnce("enharmonic-noneq", ...) (D-11)
             │
             └── writeMidi() called inside non-12-TET → RenderingDiagnostics.WarnOnce("midi-noneq", ...) (D-13)
                                                         (MIDI export still emits 12-TET — pitch-bend deferred to v1.4)
```

### Recommended Project Structure
```
flow-lang/
├── Lexing/
│   └── PragmaRegistry.cs          # +3 entries (justIntonation, pythagorean, equalTemperament)
├── Runtime/
│   └── MusicalContext.cs          # +Tuning property (top-level, non-stacked) + Clone update
├── Core/
│   └── FlowEngine.cs              # +pragma → MusicalContext.Tuning bridge in Execute (~3 lines)
├── StandardLibrary/
│   ├── Audio/
│   │   ├── PitchConversion.cs     # NoteToFrequency tuning-aware overload (default arg path = current)
│   │   └── Tuning/                # NEW directory
│   │       ├── TuningSystem.cs    # closed enum
│   │       ├── Mode.cs            # closed enum
│   │       ├── TuningTables.cs    # static Dictionary<(TuningSystem, Mode), ...>
│   │       └── RatioMath.cs       # tonic Hz lookup, cent-offset composition
│   └── Harmony/
│       └── ScaleDatabase.cs       # ParseKeyName extended for 5 church-mode suffixes (D-04)
├── Diagnostics/
│   └── RenderingDiagnostics.cs    # NEW: one-shot stderr warning helper (D-11, D-13)
flow-lang.Tests/
└── Unit/Phase23/
    ├── TuningRatioFacts.cs         # 5:4 ratio, 3:2, mode-shift assertions
    ├── PragmaTuningFacts.cs        # registry recognition + D-14 error message
    ├── ParseKeyNameFacts.cs        # 5 church-mode suffixes (D-04)
    └── RenderingDiagnosticsFacts.cs  # one-shot dedup contract
flow-lang.Tests/Integration/Phase23/
    └── ByteIdenticalDefaultTuningTests.cs  # default-path (no pragma) byte-identical to pre-23
tests/
├── test_tuning_ji.flow             # MICR-01 smoke
├── test_tuning_pythagorean.flow    # MICR-01 smoke (3-limit)
├── test_tuning_equal.flow          # D-08 explicit no-op smoke
└── test_tuning_determinism.flow    # JI/Pythagorean independent byte-identical pin
```

### Pattern 1: Tuning Resolution at Render-Time Chokepoint

**What:** A single static method (`PitchConversion.NoteToFrequency`) is the only place note→Hz translation happens. Every synthesizer + the vocalization path already routes through it.

**When to use:** Always. Modifying it once propagates tuning awareness across the entire audio pipeline with zero call-site churn.

**Critical implementation question — how does the renderer reach the active tuning?**

The current static signature `PitchConversion.NoteToFrequency(MusicalNoteData note)` has no `ExecutionContext` — synthesizers like `PianoSynthesizer.RenderNote` call it directly. Two viable patterns surface, both keeping the existing 1-arg overload stable:

**Pattern A (RECOMMENDED): Thread the resolved tuning through the call chain.**

Add a 4th parameter to `INoteSynthesizer.RenderNote` carrying a small `RenderTuning` struct (`TuningSystem System, Mode Mode, char TonicLetter, int TonicAlteration`). `SongRenderer.RenderSection` resolves it once from `_context.GetMusicalContext()` and passes it down. Synthesizer call sites change `PitchConversion.NoteToFrequency(note)` → `PitchConversion.NoteToFrequency(note, renderTuning)`. The 1-arg overload stays — defaults to 12-TET.

Pros: explicit, testable, no hidden state, no thread-local concerns, REPL persistence (D-07) just sets `MusicalContext.Tuning` and the next render reads it.

Cons: every synthesizer signature changes — but per CONTEXT.md `<canonical_refs>` line 96, "no change to call sites since `NoteToFrequency` itself becomes tuning-aware via `MusicalContext.Current.Tuning`" — which contradicts this. **The CONTEXT.md note assumed an ambient `MusicalContext.Current` accessor exists, but it does NOT — see §Verified Code Insights below.** The planner must reconcile.

**Pattern B: Static `MusicalContext.Current` ambient accessor (NEW).**

Add a `static MusicalContext.Current { get; set; }` accessor written by `FlowEngine.Execute` and read by `PitchConversion.NoteToFrequency` directly. Default = `null` → 12-TET path. No synthesizer signature change.

Pros: matches the CONTEXT.md `<canonical_refs>` description; zero call-site churn.

Cons: introduces global mutable state on a class that was previously pure data. REPL persistence (D-07) becomes implicit. Must be carefully reset between FlowEngine sessions or tests will leak state. Thread safety is not a current concern for flow-lang (single-threaded interpretation) but adding ambient state hardens that assumption.

**Recommendation:** Pattern A. The call-site churn is mechanical (~9 synthesizer files + `VocalizationFunctions.cs:59` + `SongRenderer.RenderSection` resolution), all changes are additive parameter passing, and the testing story is dramatically simpler (no global-state setup/teardown). The CONTEXT.md note in `<canonical_refs>` should be treated as an **assumption that requires planner correction** — Pattern A trades a 10-file mechanical churn for clean testability + no global state.

### Pattern 2: Mode-Keyed Ratio Tables with Spelling-Aware Indexing

**What:** A `Dictionary<(TuningSystem, Mode), ChromaticRatioTable>` where `ChromaticRatioTable` is keyed by `(NoteLetter, Alteration)` — NOT semitone offset.

**Why per D-09:** In 5-limit JI, `Eb4` = 6/5 ratio (1.200) and `D#4` = 75/64 ratio (1.171875) — different pitches. A semitone-indexed table cannot represent this. The table key must include the spelling.

**Implementation:**
```csharp
// Source: 23-CONTEXT.md D-09 + Wikipedia Five-limit_tuning chromatic table
public sealed record ChromaticRatioTable(IReadOnlyDictionary<(char Letter, int Alteration), double> Ratios);

internal static class TuningTables
{
    public static readonly ChromaticRatioTable JustMajor = new(new Dictionary<(char, int), double>
    {
        [('C',  0)] = 1.0,        // 1/1
        [('C', +1)] = 25.0/24.0,  // 25/24 (5-limit chromatic semitone — distinct from Db)
        [('D', -1)] = 16.0/15.0,  // 16/15 (5-limit diatonic semitone)
        [('D',  0)] = 9.0/8.0,    // 9/8
        [('D', +1)] = 75.0/64.0,  // 75/64 (distinct from Eb)
        [('E', -1)] = 6.0/5.0,    // 6/5
        [('E',  0)] = 5.0/4.0,    // 5/4 — the canary acceptance ratio for MICR-01
        [('F',  0)] = 4.0/3.0,    // 4/3
        [('F', +1)] = 45.0/32.0,  // 45/32 (5-limit augmented fourth, asymmetric scale)
        [('G', -1)] = 64.0/45.0,  // 64/45 (5-limit diminished fifth)
        [('G',  0)] = 3.0/2.0,    // 3/2
        [('G', +1)] = 25.0/16.0,  // 25/16 (distinct from Ab)
        [('A', -1)] = 8.0/5.0,    // 8/5
        [('A',  0)] = 5.0/3.0,    // 5/3
        [('A', +1)] = 125.0/72.0, // 125/72 (rare; resolves through enharmonic to Bb)
        [('B', -1)] = 9.0/5.0,    // 9/5 (also 16/9 in some sources — see Pitfall 2)
        [('B',  0)] = 15.0/8.0,   // 15/8
    });
    // ... other 6 modes for JI + 7 modes for Pythagorean
}
```

The `(letter, alt)` index is computed from `note.NoteName` + `note.Alteration` after the tonic is identified — the table indexes by **scale-degree spelling** relative to the active tonic, not absolute pitch. Tonic resolution: `key Cmajor { ... }` → tonic letter `'C'`, tonic alteration `0`, mode `Major`.

### Pattern 3: Score-Time vs Render-Time Separation (MICR-02 Invariant)

**What:** Transforms (`transpose`, `invert`, `retrograde`, `augment`, `diminish`) operate on MIDI numbers and produce spelled output. They never see `MusicalContext.Tuning`.

**Why:** Pitfall 5 #1 — "tuning is a render-time concern, transforms are a score-time concern." If transforms became tuning-aware, every transform would have to round-trip ratio space, lose precision, and the user would pay for microtonality even on 12-TET projects.

**Implementation:** No code change to `TransformFunctions.cs` is needed for the invariant — the transforms already work in MIDI space. The only addition per D-12: the doc comment on `TransposeSemitone` gains a caveat noting that under non-12-TET, `FromMidi` may produce a different spelling than the input, and the renderer's tuning lookup will use the new spelling's ratio. `transposePreserveSpelling` strict-mode is mentioned as a v1.4 candidate.

### Anti-Patterns to Avoid

- **Putting `Tuning` on `StackFrame.MusicalContext`** (the per-frame stacked variant). The pragma is file-scope; D-05 explicitly forbids stacked. Putting it stacked would conflict with the no-block-scope-pragma decision (Phase 21 D-02) and create surprising override semantics when sections nest.
- **Modifying `ModuleLoader` to propagate tuning state.** D-06 explicitly forbids it. Imports execute in caller's tuning per CLAUDE.md "imports execute in caller's context."
- **Routing the warning through `ErrorReporter`.** ErrorReporter accumulates errors that fail the run; D-11 / D-13 are warnings that do NOT fail. Use `Console.Error.WriteLine` per existing `TransposeSemitone:276` pattern.
- **Hand-rolling `Math.Pow(2, cents/1200.0)` per-sample.** Compute it once per note in `PitchConversion.NoteToFrequency` and let the synthesizer use the resulting Hz. Avoids hot-path allocation.
- **Treating `enable equalTemperament;` as a no-op at the registry level.** D-08 requires the pragma to be REGISTERED + visible to tooling, even if it produces byte-identical output. Phase 24 `scaleLint` will read the user's intent.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Pragma extraction | New scanner | Phase 21's `PragmaScanner` + `PragmaRegistry.KnownPragmas` | Closed-set design, Levenshtein did-you-mean, file-scope enforcement all already shipped. |
| Did-you-mean error for unknown tuning name | Custom Levenshtein | `PragmaRegistry.SuggestNearest` (Phase 21) | D-14 routes unknown tuning through the existing path; only the error message append is new. `[VERIFIED: PragmaRegistry.cs:38-54]` |
| Diatonic spelling preservation post-transform | Spelling-respelling logic in `TransformFunctions` | `HarmonyFunctions.GetInKeyEnharmonic` | Already plumbed in Phase 14; D-12 reuses verbatim. `[VERIFIED: HarmonyFunctions.cs:88-125]` |
| Multi-letter enharmonic edges (B# ↔ C, Cb ↔ B) | Custom edge handling | `HarmonyFunctions.Enharmonic` (Phase 20 DEFER-04) | Round-trip-correct; spelling-aware JI inherits this. `[VERIFIED: 20-VERIFICATION.md]` |
| Key-name parsing | Hand-rolled regex | `ScaleDatabase.ParseKeyName` (extended for D-04) | Single point of truth; benefits Phase 24 too. `[VERIFIED: ScaleDatabase.cs:152-191]` |
| Per-note cent-offset plumbing | New field | `MusicalNoteData.CentOffset` | Already on the type since Phase 14; renderer reads after the ratio multiply per D-10. `[VERIFIED: NoteType.cs:218]` |
| One-shot stderr warning channel | New diagnostic system | `Console.Error.WriteLine` + `HashSet<string>` static dedup | Matches `TransformFunctions.TransposeSemitone:276` pattern. Composer-facing one-line warnings. |
| MIDI file format authoring | Pitch-bend per-channel allocation | Existing `MidiExport.WriteMidi` (UNCHANGED) + warning emit | D-13: Phase 23 ONLY emits a one-time warning. Faithful microtonal MIDI deferred to v1.4. `[VERIFIED: MidiExport.cs:127]` |

**Key insight:** Phase 23 is structurally a "wedge" — its production-code blast radius is small precisely because Phase 14 (enharmonic-in-key), Phase 20 (multi-letter edges), and Phase 21 (pragma scanner) already shipped the prerequisite plumbing. The new work is concentrated in the ratio tables + the render-time lookup; everything else extends existing chokepoints by 1–3 lines.

## Runtime State Inventory

> Phase 23 is primarily an additive feature phase, not a rename/refactor. Most categories are N/A.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — `Tuning` is render-time state, not persisted. | None. |
| Live service config | None — no external services. | None. |
| OS-registered state | None. | None. |
| Secrets/env vars | None. | None. |
| Build artifacts | None — pure library code addition; .NET will rebuild on `dotnet build`. | None. |

**Nothing found in any category — Phase 23 introduces no persistent runtime state changes; the wedge is entirely additive in-process.**

## Common Pitfalls

### Pitfall 1: PitchConversion.NoteToFrequency has NO ExecutionContext access (CONTEXT.md assumption requires correction)

**What goes wrong:** CONTEXT.md `<canonical_refs>` line 96 states synthesizers route through `PitchConversion.NoteToFrequency` with "no change to call sites since `NoteToFrequency` itself becomes tuning-aware via `MusicalContext.Current.Tuning`." But `MusicalContext.Current` does **not** exist as a static accessor — see verification below.

**Verification (`[VERIFIED: grep MusicalContext.Current flow-lang/]`):**
- `MusicalContext` class lives in `flow-lang/Runtime/MusicalContext.cs` and exposes only instance state.
- The actual ambient accessor is `_context.GetMusicalContext()` on `ExecutionContext`, which walks the call stack and merges per-frame `MusicalContext` overrides into one resolved value.
- Synthesizers do NOT have access to `_context` — they receive `MusicalNoteData` and primitive `int sampleRate, double durationBeats, double bpm` parameters per `INoteSynthesizer.RenderNote`.

**Why it happens:** The CONTEXT.md author assumed a `Current` accessor existed because Phase 22 D-07 voicing-fallback work used a similar mental model. Reality is the resolved context is computed once per top-level operation (e.g., `RenderSong` reads `section.Context` per-section).

**How to avoid (locks Pattern A from §Architecture Patterns):**
1. Either thread a `RenderTuning` struct through `INoteSynthesizer.RenderNote(note, sampleRate, beats, bpm, RenderTuning tuning)` (Pattern A — recommended).
2. Or introduce a NEW static `MusicalContext.Current` ambient accessor (Pattern B — matches CONTEXT.md mental model but adds global state).

**Decision required from planner:** Pick A or B before Wave 2 work begins. Either pattern satisfies all locked decisions.

**Warning signs:**
- A synthesizer file gains an `ExecutionContext` field — Pattern B would not require this; Pattern A would not require this either; if you see this, the implementation is taking a third path that's neither.
- `PitchConversion.NoteToFrequency` is being called from synthesizers without the new tuning argument AND there's no static accessor — the JI path will silently fall back to 12-TET.

### Pitfall 2: Competing JI ratio sources — pinning the canonical table

**What goes wrong:** 5-limit JI has multiple variants in the wild. The major second is `9/8` in some tables and `10/9` in others. The B note (major seventh from C) is sometimes `15/8` and sometimes `16/9`/`9/5` depending on whether the table is asymmetric (Wikipedia's "preferred" form) or symmetric. Citing the wrong source produces a mathematically defensible but non-canonical table — and tests pinning specific Hz values fail across "JI implementations."

**Verification (`[VERIFIED: Wikipedia Five-limit_tuning + Mudcat JustInt mode tables]`):**
- Wikipedia's asymmetric major scale: `1, 16/15, 9/8, 6/5, 5/4, 4/3, 45/32, 3/2, 8/5, 5/3, 9/5, 15/8`.
- Mudcat (Olson) major: `1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8`.
- These agree on **diatonic** scale degrees but diverge on **chromatic** (raised/lowered) ones — the asymmetric Wikipedia table is the standard reference for chromatic 12-tone JI.

**How to avoid:**
1. Pin Wikipedia's asymmetric 5-limit table as the JI canonical reference. Cite verbatim in `TuningTables.cs` doc comment.
2. For chromatic tones not in the asymmetric table (D#, F#, G#, A#), use the 5-limit chromatic semitone construction (`25/24` from natural via the syntonic-comma path) and document it.
3. Pythagorean: chain-of-fifths starting at C, using sharps for raised tones (Wikipedia convention): `C=1/1, C#=2187/2048, D=9/8, D#=19683/16384, E=81/64, F=4/3, F#=729/512, G=3/2, G#=6561/4096, A=27/16, A#=59049/32768, B=243/128`.
4. Wolf fifth: in Pythagorean tuning the chain ends at G#-Eb (or equivalently F#-Db) which is ~678.49 cents instead of 701.96 cents. **Phase 23 ships C-rooted Pythagorean only**; the wolf appears between G# and Eb (i.e., Ab=Eb*… mismatch). Document explicitly that under `enable pythagorean;` with `key Cmajor`, modulating to keys near the chain extremes will sound "wrong" — this is faithful to Pythagorean tuning, not a bug.

**Warning signs:**
- A `TuningRatioFacts` test asserts `JI(D, major) == 10/9` and the renderer returns `9/8` — the asymmetric vs symmetric ambiguity is biting.
- A user reports "wolf fifth" sounds — confirm the active key + interval; if it's truly the wolf interval per the chain-of-fifths definition, the tuning is correct.

### Pitfall 3: Mode tables overlay the chromatic table — distinguishing diatonic vs chromatic ratios

**What goes wrong:** The 7 mode tables only define ratios for the 7 diatonic scale degrees. Chromatic notes (notes outside the mode's diatonic set) need to come from somewhere. Naively re-using the major-mode chromatic ratios under dorian produces wrong frequencies for accidentals.

**How to avoid:** Ratio table data structure shape: each `(TuningSystem, Mode)` entry stores **all 12 chromatic spellings** (with sharp/flat distinct under JI per D-09). The mode's diatonic degrees override the chromatic defaults; non-diatonic chromatic tones use a consistent 5-limit (or 3-limit) chromatic-semitone construction from the nearest diatonic note.

For Pythagorean, this is straightforward — every chromatic tone is on the chain of fifths. For 5-limit JI, the chromatic tones use the 25/24 syntonic semitone construction. Document the construction rule in `TuningTables.cs`.

**Warning signs:**
- A `.flow` script with `enable justIntonation; key Cdorian { | F#4 | }` produces a frequency that doesn't match either Wikipedia's asymmetric chromatic F# (45/32 from the relative major) or a syntonic-semitone construction — the chromatic fallback is broken.

### Pitfall 4: REPL persistence (D-07) breaks the per-line pragma reset assumption

**What goes wrong:** Phase 21 D-07 says each REPL line gets a fresh `PragmaSet`. D-07 in Phase 23 explicitly DEPARTS from this for tuning — the resolved `MusicalContext.Tuning` persists across lines. If `MusicalContext.Tuning` is computed inside the per-line PragmaScanner pipeline, persistence is impossible. If it's computed inside `FlowEngine.Execute` from the line's PragmaSet, persistence requires explicit logic.

**How to avoid:**
1. In `FlowEngine.Execute`, if `Program.Pragmas` contains a tuning pragma, set `MusicalContext.Tuning` (or whichever ambient store).
2. If `Program.Pragmas` does NOT contain a tuning pragma, **leave** `MusicalContext.Tuning` unchanged — do NOT reset to default.
3. REPL session ends → tuning resets (the `FlowEngine` instance is disposed).
4. A new `FlowEngine` instance defaults `MusicalContext.Tuning` to `EqualTemperament`.

This is a surgical departure from Phase 21 D-07: PragmaSet semantics stay per-line; the **resolution** is what persists.

**Warning signs:**
- A REPL test that expects `enable justIntonation;` on line 1 to affect line 3 fails — the persistence logic is wrong.
- A non-REPL test (single-shot script execution) sees `MusicalContext.Tuning` "leak" from a previous test run — the FlowEngine constructor isn't initializing the property.

### Pitfall 5: writeMidi warning fires more than once per session

**What goes wrong:** Composers iterating in REPL mode call `writeMidi` repeatedly. A naive warning that fires every time floods the console.

**How to avoid:** `RenderingDiagnostics.WarnOnce(string sentinelKey, string message)` uses a static `HashSet<string> _emitted` to dedup. First call with a given key → emit; subsequent calls with the same key → no-op.

```csharp
// Source: derived from TransformFunctions.TransposeSemitone:276 + new dedup layer
public static class RenderingDiagnostics
{
    private static readonly HashSet<string> _emitted = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public static void WarnOnce(string sentinelKey, string message)
    {
        lock (_lock)
        {
            if (!_emitted.Add(sentinelKey)) return;
        }
        Console.Error.WriteLine(message);
    }

    /// <summary>For tests — clear the dedup set between runs.</summary>
    internal static void ResetForTesting()
    {
        lock (_lock) { _emitted.Clear(); }
    }
}
```

**Warning signs:**
- Test output is non-deterministic (warning order matters across tests) — test-ordering issue; use `[Collection]` to serialize.
- Warning never fires under JI even though `enharmonic()` was called — sentinel key collision, or the call site isn't gated on `tuning != EqualTemperament`.

### Pitfall 6: Default-path byte-identical regression breaks during refactor

**What goes wrong:** Modifying `PitchConversion.NoteToFrequency` is the lowest-level audio change in the entire codebase. Any tiny floating-point reformulation breaks `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`.

**How to avoid:**
1. Keep the existing 1-arg `NoteToFrequency(MusicalNoteData)` AND 3-arg `NoteToFrequency(char, int, int)` overloads UNCHANGED in body — they continue to call `440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0)`.
2. Add NEW overloads that take a tuning argument; they branch on `tuning == EqualTemperament` and call into the existing 1-arg path for the default case.
3. Run `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` at the end of each commit. RED here = revert.
4. Per Pitfall 1 mitigation: synthesizers should use the new tuning-aware overload, but `tuning == EqualTemperament` MUST produce bit-identical output to the old path (literally call the same code).

**Warning signs:**
- `cmp output1.wav output2.wav` shows differences after the synthesizer signature change — refactor introduced a non-12-TET path even when tuning is unset.
- `Math.Pow(2.0, (midiNote - 69) / 12.0)` was rewritten as `Math.Pow(2.0, (midiNote - 69.0) / 12.0)` — promoting `69` to `double` early changes IEEE-754 results in ~1 in 10000 cases.

## Code Examples

### Pragma registration (D-08, D-14)
```csharp
// Source: extends flow-lang/Lexing/PragmaRegistry.cs
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"]            = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
        ["justIntonation"]  = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
        ["pythagorean"]     = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
        ["equalTemperament"]= "12-tone equal temperament (default). Explicit form for tooling-visible intent.",
    };
```

### FlowEngine bridge (D-06)
```csharp
// Source: extends flow-lang/Core/FlowEngine.cs Execute() ~line 86 (after parser, before interpreter)
//         Pattern A: thread tuning into a render-time accessor on _context.MusicalContext.Tuning
//         (or Pattern B: MusicalContext.Current = ...).
//
// Ordering note: this MUST happen AFTER parse (so `program.Pragmas` is populated) and
// BEFORE `_interpreter.Execute(program)` (so synthesizers see the resolved tuning).
//
// D-07 REPL persistence: only OVERWRITE on explicit pragma; otherwise leave the
// previous value in place across REPL lines.
private void ApplyTuningPragma(Ast.Program program)
{
    if (program.Pragmas.Has("justIntonation"))
        _context.SetTuning(TuningSystem.JustIntonation);
    else if (program.Pragmas.Has("pythagorean"))
        _context.SetTuning(TuningSystem.Pythagorean);
    else if (program.Pragmas.Has("equalTemperament"))
        _context.SetTuning(TuningSystem.EqualTemperament);
    // else: D-07 persistence — leave previous value untouched.
}
```

### TuningTables ratio entry (D-03, D-09 — JI Major mode, partial)
```csharp
// Source: Wikipedia Five-limit_tuning asymmetric chromatic table (canonical 5-limit reference);
//         spelling-aware (Eb 6/5 ≠ D# 75/64 per D-09).
//
//   Reference: https://en.wikipedia.org/wiki/Five-limit_tuning
//
// Diatonic degrees (C major from C tonic): 1/1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8.
// Chromatic alterations use the 5-limit chromatic semitone (25/24) when constructing
// raised tones (D#, F#, G#, A#) and the 5-limit diatonic semitone (16/15) for lowered
// tones (Db, Eb, Gb, Ab, Bb).
public static readonly ChromaticRatioTable JustMajor = ChromaticRatioTable.Build(
    naturals: new() {
        ['C'] = 1.0,
        ['D'] = 9.0/8.0,
        ['E'] = 5.0/4.0,
        ['F'] = 4.0/3.0,
        ['G'] = 3.0/2.0,
        ['A'] = 5.0/3.0,
        ['B'] = 15.0/8.0,
    },
    sharps: new() {                  // raised: natural × 25/24
        ['C'] = 25.0/24.0,           // C# = 1 × 25/24
        ['D'] = 75.0/64.0,           // D# = 9/8 × 25/24
        ['F'] = 25.0/18.0,           // F# = 4/3 × 25/24 (alternative: 45/32 from Wikipedia asymmetric — see Pitfall 2)
        ['G'] = 25.0/16.0,           // G# = 3/2 × 25/24
        ['A'] = 125.0/72.0,          // A# = 5/3 × 25/24
    },
    flats: new() {                   // lowered: natural × 16/15 (or canonical 5-limit table values)
        ['D'] = 16.0/15.0,           // Db
        ['E'] = 6.0/5.0,             // Eb (canonical minor third — DIFFERENT from D# 75/64 per D-09)
        ['G'] = 64.0/45.0,           // Gb (asymmetric tritone)
        ['A'] = 8.0/5.0,             // Ab
        ['B'] = 9.0/5.0,             // Bb
    });
```

### Mode-shifted tables (D-03 — Mudcat-verified)
```csharp
// Source: https://mudcat.org/olson/JUSTINT.html (canonical mode tables for 5-limit JI)
//
//                 1     2     3     4     5     6     7
//   Ionian:     1/1   9/8   5/4   4/3   3/2   5/3  15/8       — major (already shown above)
//   Dorian:     1/1   9/8   6/5   4/3   3/2   5/3   9/5       — diff at 3rd (6/5), 7th (9/5)
//   Phrygian:   1/1  27/25  6/5   4/3   3/2   8/5   9/5       — diff at 2nd (27/25), 6th (8/5), 7th (9/5)
//   Lydian:     1/1   9/8   5/4  25/18  3/2   5/3  15/8       — diff at 4th (25/18 — augmented fourth)
//   Mixolydian: 1/1   9/8   5/4   4/3   3/2   5/3   9/5       — diff at 7th (9/5)
//   Aeolian:    1/1   9/8   6/5   4/3   3/2   8/5   9/5       — natural minor (3rd, 6th, 7th lowered)
//   Locrian:    1/1  27/25  6/5   4/3  36/25  8/5   9/5       — diff at 5th (36/25 — diminished fifth)
//
// For Pythagorean, derive the same modes by walking the chain-of-fifths from the tonic
// in the appropriate ±direction (e.g., dorian = +5, +6, +7 fifths down; +4, +5 fifths up).

public static readonly Dictionary<(TuningSystem, Mode), ChromaticRatioTable> Tables = new()
{
    [(TuningSystem.JustIntonation, Mode.Major)]      = JustMajor,
    [(TuningSystem.JustIntonation, Mode.Minor)]      = JustAeolian,
    [(TuningSystem.JustIntonation, Mode.Dorian)]     = JustDorian,
    [(TuningSystem.JustIntonation, Mode.Phrygian)]   = JustPhrygian,
    [(TuningSystem.JustIntonation, Mode.Lydian)]     = JustLydian,
    [(TuningSystem.JustIntonation, Mode.Mixolydian)] = JustMixolydian,
    [(TuningSystem.JustIntonation, Mode.Locrian)]    = JustLocrian,
    [(TuningSystem.Pythagorean, Mode.Major)]         = PythMajor,
    // ... 6 more Pythagorean modes
    [(TuningSystem.EqualTemperament, _)]             = NotUsed_EqualTemperamentBypassesTableLookup,
};
```

### PitchConversion tuning-aware overload (Pattern A — recommended)
```csharp
// Source: extends flow-lang/StandardLibrary/Audio/PitchConversion.cs
//
// Default 1-arg path UNCHANGED — preserves byte-identical output for tutorial.flow + showcase.flow
// per Pitfall 6 mitigation (the existing 1-arg overload literally calls the same code).
public static double NoteToFrequency(MusicalNoteData note)
{
    if (note.IsRest) return 0.0;
    return NoteToFrequency(note.NoteName, note.Octave, note.Alteration);
}

// NEW: tuning-aware overload. tuning.System == EqualTemperament short-circuits to the
// default path so explicit `enable equalTemperament;` produces byte-identical output (D-08).
public static double NoteToFrequency(MusicalNoteData note, RenderTuning tuning)
{
    if (note.IsRest) return 0.0;

    // D-08 byte-identical fast path
    if (tuning.System == TuningSystem.EqualTemperament)
    {
        double eqFreq = NoteToFrequency(note.NoteName, note.Octave, note.Alteration);
        // D-10: cent offset is additive even in 12-TET (existing behavior preserved)
        if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0)
            eqFreq *= Math.Pow(2.0, note.CentOffset.Value / 1200.0);
        return eqFreq;
    }

    // Non-12-TET path: ratio × tonic Hz × cent offset
    double tonicHz = NoteToFrequency(tuning.TonicLetter, note.Octave, tuning.TonicAlteration);
    double ratio = TuningTables.LookupRatio(tuning.System, tuning.Mode, note.NoteName, note.Alteration);
    // Octave displacement: if note is more than one octave from tonic, ratio applies
    // within-octave; multiply by 2^k for k-octave shifts.
    int octaveDelta = ComputeOctaveDelta(note, tuning);
    double freq = tonicHz * ratio * Math.Pow(2.0, octaveDelta);

    // D-10: cent offset is additive in cent-space, AFTER the ratio multiply
    if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0)
        freq *= Math.Pow(2.0, note.CentOffset.Value / 1200.0);

    return freq;
}
```

### One-shot warning (D-11, D-13)
```csharp
// Source: extends flow-lang/StandardLibrary/Harmony/HarmonyFunctions.Enharmonic
//         AND  flow-lang/StandardLibrary/Audio/MidiExport.WriteMidi
//
// D-11 enharmonic warning emit point:
private static Value Enharmonic(IReadOnlyList<Value> args, ExecutionContext context)
{
    var musicalCtx = context.GetMusicalContext();
    if (musicalCtx?.Tuning != null && musicalCtx.Tuning != TuningSystem.EqualTemperament)
    {
        RenderingDiagnostics.WarnOnce(
            "enharmonic-non-equal-temperament",
            "[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)");
    }
    // ... existing enharmonic logic unchanged
}

// D-13 writeMidi warning emit point:
public static Value WriteMidi(IReadOnlyList<Value> args)
{
    // ... existing setup
    var musicalCtx = /* obtain — see note below */;
    if (musicalCtx?.Tuning != null && musicalCtx.Tuning != TuningSystem.EqualTemperament)
    {
        RenderingDiagnostics.WarnOnce(
            "writemidi-non-equal-temperament",
            "[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend " +
            "(faithful microtonal MIDI deferred to v1.4)");
    }
    // ... existing 12-TET MIDI export logic unchanged
}
```

**Note on `writeMidi` context access:** `MidiExport.WriteMidi` is currently registered without an `ExecutionContext` (line 569-570 in `BuiltInFunctions.cs`). Phase 23 may need to migrate it to `RegisterContextDependentFunctions` — same pattern as `enharmonic`. Planner decides.

### D-14 unknown-tuning error message extension
```csharp
// Source: extends Phase 21 D-12 unknown-pragma error path.
// When the unknown pragma name resembles a tuning name (Levenshtein ≤ 3 from any registered
// tuning), append the v1.4 Scala-loader pointer line. Otherwise, fall through to the existing
// Phase 21 generic unknown-pragma error.
//
// Single source-of-truth deferral string per CONTEXT.md §Specifics.
private const string ScalaLoaderDeferralPointer =
    "Full Scala (.scl) loader is documented as deferred to v1.4 — see ADR/REQUIREMENTS.md D-03.";

// Suggested implementation: in the PragmaScanner unknown-pragma error builder, after the
// "Did you mean '{nearest}'?\nKnown pragmas: {alphabetized_csv}." block, check if `typed` is
// within Levenshtein distance 3 of any of {justIntonation, pythagorean, equalTemperament}.
// If so, append "\n" + ScalaLoaderDeferralPointer.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hard-coded `440 * 2^((midi-69)/12)` in `PitchConversion.NoteToFrequency` | Tuning-aware overload with `EqualTemperament` short-circuit preserving the old path | Phase 23 (this phase) | Default behavior byte-identical; new tunings additive. |
| `MusicalContext` carries 8 properties (TimeSig, Tempo, Swing, Key, Velocity, Pan, Gain, ReverbTime) | Adds 9th property `Tuning` (top-level, non-stacked per D-05) | Phase 23 | Non-breaking; existing `Clone()` extended; `GetMusicalContext()` aggregation unchanged for the new field. |
| `PragmaRegistry.KnownPragmas` has 1 entry (`hAsB`) | 4 entries: + `justIntonation`, `pythagorean`, `equalTemperament` | Phase 23 | Per Phase 21 D-17 — closed-set growth was reserved for this phase. |
| `ScaleDatabase.ParseKeyName` recognizes `major`/`minor` suffix | + `dorian`, `phrygian`, `lydian`, `mixolydian`, `locrian` per D-04 | Phase 23 | Generalizes existing `EndsWith("major")`/`EndsWith("minor")` pattern; benefits Phase 24 too. |
| `enharmonic()` runs unconditionally | Emits one-shot stderr warning under non-12-TET (D-11) | Phase 23 | Output unchanged; warning channel new; conversion still happens. |
| `writeMidi` runs unconditionally | Emits one-shot stderr warning under non-12-TET (D-13) | Phase 23 | MIDI bytes unchanged (12-TET); warning channel new. |

**Deprecated/outdated:**
- None. Phase 23 is purely additive.

**Interactions with prior shipped phases:**
- Phase 14 enharmonic-in-key (`HarmonyFunctions.GetInKeyEnharmonic`) — reused by D-12 for diatonic-spelling preservation post-transform. No change.
- Phase 18-19 fraction/tuplet duration arithmetic — orthogonal; tuning operates on pitch, not duration.
- Phase 20 multi-letter enharmonic edges (`B# ↔ C`, `Cb ↔ B`) — reused under JI when the chromatic alteration resolves through `HarmonyFunctions.Enharmonic`. The B# vs C distinction becomes audible under JI for the first time.
- Phase 21 pragma scanner — extended (closed-set growth per D-17 reservation).
- Phase 22 voicings/inversions/quantize/legato/portamento — orthogonal; render through synthesizers which now consult tuning.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Pattern A (thread `RenderTuning` through `INoteSynthesizer.RenderNote`) is preferable to Pattern B (static ambient `MusicalContext.Current`) | §Architecture Patterns Pattern 1, Pitfall 1 | Planner may legitimately prefer Pattern B for matching CONTEXT.md `<canonical_refs>` line 96; either pattern satisfies all locked decisions. Decision must be made before Wave 2. `[ASSUMED]` |
| A2 | Wikipedia's asymmetric 5-limit JI table is the canonical reference | §Code Examples (JustMajor table), Pitfall 2 | Mudcat's mode-specific tables agree on diatonics but Wikipedia's asymmetric chromatic table (45/32, 64/45) is the standard for 12-tone JI. If user prefers Helmholtz-Ellis extended notation, the planner should re-pin to that. `[CITED: en.wikipedia.org/wiki/Five-limit_tuning]` |
| A3 | The 5-limit chromatic semitone (25/24) is the correct construction for raised non-diatonic tones in JI mode tables | §Code Examples (JustMajor sharps), Pitfall 3 | Some 5-limit tables use `135/128` (the larger chromatic semitone) instead. If user reports F# sounding "off" under JI, this is the suspected cause. `[ASSUMED]` |
| A4 | `writeMidi` registration may need migration to `RegisterContextDependentFunctions` to read `MusicalContext.Tuning` | §Code Examples (D-13 emit point) | If the migration is non-trivial, an alternative is to thread tuning through `SongData` itself or to have `SongRenderer.RenderSection` set a thread-local before `WriteMidi` runs. Planner decides. `[ASSUMED]` |
| A5 | A `RenderTuning` struct (or equivalent context object) carrying `(System, Mode, TonicLetter, TonicAlteration)` is the right granularity | §Architecture Patterns Pattern 1 | If the planner discovers more state is needed (e.g., reference-pitch override beyond A4=440Hz), the struct grows; nothing is locked here. `[ASSUMED]` |
| A6 | Pythagorean wolf fifth lands at G#-Eb when C is the tonic | §Common Pitfalls Pitfall 2 | Could also land at F#-Db depending on which side of the chain is preferred. Phase 23 should pick one explicitly and document it. `[ASSUMED]` |

**If this table has entries:** the planner and discuss-phase should confirm A1, A4, A5, A6 before the implementation locks them. A2 and A3 are CITED but the user may prefer different canonical tables — confirm before pinning ratio Facts.

## Open Questions

1. **Does the planner prefer Pattern A (threaded tuning) or Pattern B (static ambient)?**
   - What we know: Pattern A is recommended for testability + no global state; Pattern B matches the CONTEXT.md `<canonical_refs>` mental model.
   - What's unclear: the planner's preference between additive parameter churn vs ambient state.
   - Recommendation: Pattern A. Both satisfy all locked decisions.

2. **Are sharps (#) or flats (b) the canonical convention for the Pythagorean chain?**
   - What we know: Wikipedia uses sharps (`C#=2187/2048`, …, `A#=59049/32768`); medieval theory often used flats (`Bb`, `Eb`, `Ab`, `Db`, `Gb`).
   - What's unclear: which the user expects when writing `Cb` vs `B` under `enable pythagorean;`.
   - Recommendation: sharps as default, with `D-09` spelling-awareness handling flats by walking the chain in the opposite direction. Document the choice.

3. **Is `25/16` (5-limit) or `1.587…` (Pythagorean from C: `6561/4096`) the right G# under JI?**
   - What we know: For pure 5-limit JI, `25/16` is constructed via `(5/4) × (5/4)` (two stacked major thirds — the augmented fifth). For pure Pythagorean, `6561/4096` is on the chain.
   - What's unclear: how strictly the user wants Phase 23 to "stay 5-limit" — Wikipedia's asymmetric table sometimes blends primes for the augmented intervals.
   - Recommendation: pin both ratios in code with a comment that 5-limit purity may differ from Wikipedia's asymmetric augmented-tone choice. Test Facts assert the chosen ratio explicitly.

4. **Does `enable equalTemperament;` need to be tracked separately from "no pragma" for tooling visibility (D-08)?**
   - What we know: D-08 says yes — Phase 24 `scaleLint` reads tuning intent.
   - What's unclear: how the difference is exposed — is it a boolean on `MusicalContext` (`TuningExplicitlyDeclared`)?
   - Recommendation: The `Tuning` property is `EqualTemperament` by default, AND a separate `TuningPragmaSites: IReadOnlyList<PragmaDeclarationSite>` (or similar) on `Program.Pragmas` already gives Phase 24 the declaration provenance. No separate boolean needed.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + test | ✓ | net10.0 | — |
| `dotnet` CLI | Test orchestration | ✓ | (existing toolchain) | — |
| PulseAudio | `play()` smoke tests (optional) | (existing) | — | xUnit ratio Facts cover correctness; PulseAudio path is acceptance-only |
| Wikipedia / Mudcat (research only) | Citing canonical ratios | ✓ (research already complete) | — | Pinned in `TuningTables.cs` doc comments |

**No external dependencies added.** Phase 23 is pure C# additions to `flow-lang/`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing, used by Phase 18-22) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"` |
| Full suite command | `dotnet test flow-lang.Tests` |
| .flow integration loop | `for test in tests/test_tuning_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MICR-01 | `enable justIntonation;` produces 5:4 (1.25) C-E ratio | unit (Fact pinning ratio) | `dotnet test --filter "TuningRatioFacts.JustMajor_CtoE_Is5to4"` | ❌ Wave 1 |
| MICR-01 | `enable pythagorean;` produces 81:64 (~1.265625) C-E ratio | unit (Fact) | `dotnet test --filter "TuningRatioFacts.PythagoreanMajor_CtoE_Is81to64"` | ❌ Wave 1 |
| MICR-01 | `enable equalTemperament;` produces byte-identical output to no-pragma file | integration | `dotnet test --filter "ByteIdenticalDefaultTuningTests.ExplicitEqualTemperament_ProducesIdenticalOutput"` | ❌ Wave 2 |
| MICR-01 | All 7 mode tables shift correctly (JI dorian's 6/5 third, etc.) | unit Theory rows (7 modes × 2 tunings) | `dotnet test --filter "TuningModeShiftFacts"` | ❌ Wave 1 |
| MICR-01 | Spelling-aware: Eb4 (6/5) ≠ D#4 (75/64) under JI | unit (Fact pinning both ratios) | `dotnet test --filter "TuningRatioFacts.JI_Eb_DistinctFrom_DSharp"` | ❌ Wave 1 |
| MICR-01 | Cent offsets compose additively (D-10): `E4+5c` under JI is 5:4 × 2^(5/1200) | unit (Fact) | `dotnet test --filter "TuningRatioFacts.CentOffsetIsAdditive"` | ❌ Wave 1 |
| MICR-02 | `transpose(seq, 5)` produces same MIDI numbers under all 3 tunings | unit (parametric Fact across `[InlineData]` rows) | `dotnet test --filter "TransformInvarianceFacts.TransposeIsTuningAgnostic"` | ❌ Wave 2 |
| MICR-02 | `invert`, `retrograde`, `augment`, `diminish` MIDI invariance | unit (Theory: 4 transforms × 3 tunings) | `dotnet test --filter "TransformInvarianceFacts"` | ❌ Wave 2 |
| MICR-02 | `enharmonic()` non-12-TET emits one-shot warning (D-11) | unit (capture stderr) | `dotnet test --filter "RenderingDiagnosticsFacts.EnharmonicWarnsOnceUnderJI"` | ❌ Wave 3 |
| MICR-03 | Unknown tuning name (e.g., `enable maqam;`) error includes Scala v1.4 pointer line | unit (Fact asserting error string) | `dotnet test --filter "PragmaTuningFacts.UnknownTuning_ErrorIncludesScalaPointer"` | ❌ Wave 2 |
| MICR-03 | Unknown tuning routes through Phase 21 D-12 path (Levenshtein) | unit (Fact) | `dotnet test --filter "PragmaTuningFacts.UnknownTuning_DidYouMean"` | ❌ Wave 2 |
| D-04 | `ParseKeyName` recognizes `Cdorian`, `Aphrygian`, `Glydian`, `Bmixolydian`, `Dlocrian` | unit (Theory rows) | `dotnet test --filter "ParseKeyNameFacts.RecognizesChurchModes"` | ❌ Wave 3 |
| D-13 | `writeMidi` non-12-TET emits one-shot warning | unit (capture stderr) | `dotnet test --filter "RenderingDiagnosticsFacts.WriteMidiWarnsOnceUnderJI"` | ❌ Wave 3 |
| Determinism gate | `tutorial.flow` + `showcase.flow` byte-identical post-Phase-23 (default 12-TET path) | integration (existing `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` MUST stay GREEN) | `dotnet test --filter "ByteIdentical"` | ✓ exists (must remain GREEN) |
| Determinism gate | `tests/test_tuning_determinism.flow` JI/Pythagorean independent byte-identical pin | integration | new test scaffold | ❌ Wave 4 |

### Sampling Rate
- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"` (~5 sec)
- **Per wave merge:** `dotnet test flow-lang.Tests --filter "ByteIdentical|Phase23"` (~30 sec)
- **Phase gate:** `dotnet test flow-lang.Tests` (full suite, expected ~410+ Facts including ~30 new Phase 23 Facts) — must be GREEN before `/gsd-verify-work`. Plus `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` — every script must exit 0.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` — pins all 14 mode tables (7 JI + 7 Pythagorean) at the diatonic level + spelling-aware Eb/D# distinction + cent additivity (covers MICR-01 + D-09 + D-10).
- [ ] `flow-lang.Tests/Unit/Phase23/TuningModeShiftFacts.cs` — 14 [Theory] rows pinning the canonical scale-degree ratios per Mudcat reference (one row per mode).
- [ ] `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs` — registry recognition + D-14 error message extension.
- [ ] `flow-lang.Tests/Unit/Phase23/ParseKeyNameFacts.cs` — 5 church-mode suffixes per D-04.
- [ ] `flow-lang.Tests/Unit/Phase23/TransformInvarianceFacts.cs` — MICR-02 transforms-stay-MIDI invariant; 4 transforms × 3 tunings = 12 [InlineData] rows.
- [ ] `flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs` — one-shot warning dedup contract; D-11 + D-13 emit points.
- [ ] `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs` — explicit `enable equalTemperament;` produces byte-identical output to no-pragma file (D-08).
- [ ] `tests/test_tuning_ji.flow` — MICR-01 acceptance smoke (canonical 5:4 ratio test).
- [ ] `tests/test_tuning_pythagorean.flow` — MICR-01 acceptance smoke (canonical 81:64 ratio test).
- [ ] `tests/test_tuning_equal.flow` — D-08 explicit no-op smoke.
- [ ] `tests/test_tuning_determinism.flow` — JI/Pythagorean independent byte-identical pin (per CONTEXT.md Claude's Discretion recommendation).
- [ ] `examples/tutorial.flow` + `examples/showcase.flow` — VERIFY (do not modify) byte-identical post-Phase-23.
- [ ] No new framework install — xUnit + .NET 10 toolchain already in place.

## Project Constraints (from CLAUDE.md)

- **Runtime constraint:** .NET 10 / net10.0 — `TuningSystem`/`Mode` enums + `TuningTables` static class + `RatioMath` helper all target net10.0. No new NuGet packages.
- **C# conventions:** File-scoped namespaces, record types for AST, closed-enum design (matches `TokenType`/`DurationValue`/Phase 21 `PragmaRegistry` precedent). All new files follow `namespace FlowLang.StandardLibrary.Audio.Tuning;` form.
- **Minimal-deps philosophy:** No external library — all ratio math is pure C# `double` arithmetic with `Math.Pow` for cent-offset composition. Per CLAUDE.md "Guiding Principle: Minimal Dependencies."
- **Charitable interpretation memory:** D-02 silently roots at C major (no error when no key block); D-10 cents-never-disappear; D-11 / D-13 documented exceptions because the regression is silent and audible. Memory at `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md`.
- **Language philosophy memory:** D-08 closed-enum tuning system + S-expression-aligned no-arg pragma syntax. Memory at `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md`.
- **Project skills (`.claude/skills/`):** No project-specific skills directory exists — `find . -name "SKILL.md"` returns empty. Standard CLAUDE.md guidelines apply.
- **GSD workflow:** All edits via GSD entry points (`/gsd:execute-phase` for planned phase work). Atomic commits per task with `feat(23-NN): ...` / `test(23-NN): ...` conventional-commit prefix per Phase 18-22 precedent.
- **Performance:** Real-time audio playback requires no-GC-pressure in hot paths. Ratio lookup is one dictionary access + one `double` multiply per note — well below the existing `Math.Pow` cost. Cent-offset composition is one extra `Math.Pow` per note ONLY when `CentOffset.HasValue && != 0`.
- **Compatibility:** All ~70 existing `.flow` test scripts MUST remain byte-identical (regression gate via `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`). The `tuning == EqualTemperament` short-circuit guarantees this.

## Sources

### Primary (HIGH confidence)
- **Internal codebase (verified via Read tool):**
  - `flow-lang/StandardLibrary/Audio/PitchConversion.cs:13-50` — exact NoteToFrequency signatures and 12-TET formula (`440 * 2^((midiNote-69)/12)`).
  - `flow-lang/Runtime/MusicalContext.cs:35-62` — exact 8 properties (TimeSig, Tempo, Swing, Key, Velocity, Pan, Gain, ReverbTime); `Clone()` shape that `Tuning` must extend.
  - `flow-lang/Runtime/ExecutionContext.cs:186-213` — `GetMusicalContext()` walks call stack, merges per-frame overrides; `MusicalContext.Current` static accessor does NOT exist (Pitfall 1 verification).
  - `flow-lang/Core/FlowEngine.cs:59-101` — `Execute()` pipeline order: PragmaScanner → SimpleLexer → Parser → Interpreter; `program.Pragmas` is populated post-parse.
  - `flow-lang/Lexing/PragmaRegistry.cs:11-30` — closed-set shape; `KnownPragmas` dictionary; alphabetized listing for D-12 errors.
  - `flow-lang/Lexing/PragmaSet.cs:14-37` — `PragmaSet` record with `Has(name)` method (Phase 21 D-08 / D-05).
  - `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:152-191` — `TryParseKey` with `EndsWith("major")` / `EndsWith("minor")` extension shape for D-04.
  - `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:21-125` — `RegisterContextDependent` pattern for context-aware built-ins; `GetInKeyEnharmonic` for D-12 spelling preservation.
  - `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:262-303` — `TransposeSemitone` / `TransposeCent` `Console.Error.WriteLine` warning style (D-11/D-13 template).
  - `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:218,244` — `MusicalNoteData.CentOffset` field; D-10 cent-additive math after the ratio multiply.
  - `flow-lang/StandardLibrary/Audio/MidiExport.cs:127-137` — `WriteMidi` entry point; D-13 warning emit guard site.
  - `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs:17` — synthesizer call site for `PitchConversion.NoteToFrequency(note)` (Pattern A churn site).
  - `flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs:59` — vocalization call site (also Pattern A churn).
  - `flow-lang/StandardLibrary/Audio/SongRenderer.cs:87-145` — `RenderSong` / `RenderSection` flow; `section.Context` is the per-section musical context source.
  - `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` — Phase 21 xUnit test pattern (uses `FlowScriptData.FindTestsRoot()`, `Assert.Same(PragmaSet.Empty, ...)`).
  - `flow-lang.Tests/Unit/Phase18/FractionTests.cs` — Phase 18 xUnit Fraction Facts pattern (template for `TuningRatioFacts`).
  - `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` — two-runner byte-identical pattern; templates `tests/test_tuning_determinism.flow` smoke.
- **Internal planning docs (verified via Read tool):**
  - `.planning/phases/23-microtonal-tuning-wedge/23-CONTEXT.md` — locked decisions D-01..D-14, Claude's Discretion items, in-scope/out-of-scope boundaries.
  - `.planning/REQUIREMENTS.md:73-75,103,148-150` — MICR-01/02/03 acceptance text + v1.4 Scala-loader deferral note.
  - `.planning/research/PITFALLS.md:135-162` — Pitfall 5 (microtonal tuning state vs transforms) — foundational constraint document; AUDIT-VERIFIED marker text for D-11.
  - `.planning/research/STACK.md:130-142` — pre-existing tuning research; ratio-table approach validated.
  - `.planning/research/ARCHITECTURE.md:265-308` — pre-existing `ITuningSystem` sketch; informed Claude's-Discretion enum-vs-interface call.
  - `.planning/phases/21-pragma-system-h-alias/21-CONTEXT.md:1-106` — Phase 21 D-02/D-06/D-07/D-12/D-17 plumbing rules that Phase 23 reuses.
  - `.planning/phases/20-cheap-defer-closures-multi-letter-enharmonic-edges/20-VERIFICATION.md:1-77` — DEFER-04 multi-letter enharmonic edges shipped (B# ↔ C, Cb ↔ B); foundational for spelling-aware JI.
  - `.planning/ROADMAP.md:159-168` — 4 success criteria + dependency on Phase 21 pragma system.

### Secondary (MEDIUM confidence — Wikipedia + Mudcat verified independently)
- [Five-limit tuning (Wikipedia)](https://en.wikipedia.org/wiki/Five-limit_tuning) — asymmetric 12-tone 5-limit chromatic table from C tonic; Eb=6/5 vs D#=75/64 distinction; tritone pair (45/32, 64/45) for asymmetric scale.
- [Pythagorean tuning (Wikipedia)](https://en.wikipedia.org/wiki/Pythagorean_tuning) — 12-tone Pythagorean chromatic table from chain of fifths; sharp convention; wolf fifth at chain extreme (~678.49 cents).
- [Just Intonation Music Scales (Mudcat — Olson)](https://mudcat.org/olson/JUSTINT.html) — 5-limit JI mode tables for Ionian/Dorian/Phrygian/Lydian/Mixolydian/Aeolian/Locrian; canonical reference for D-03 mode-specific tables.
- [List of intervals in 5-limit just intonation (Wikipedia)](https://en.wikipedia.org/wiki/List_of_intervals_in_5-limit_just_intonation) — corroborates 25/24 chromatic semitone construction; 81/80 syntonic comma reference.
- [Pythagorean comma (Wikipedia)](https://en.wikipedia.org/wiki/Pythagorean_comma) — confirms 531441/524288 ≈ 23.46 cents = the closure error in the chain of fifths; informs documentation of wolf fifth.
- [Just intonation (Wikipedia)](https://en.wikipedia.org/wiki/Just_intonation) — Helmholtz-Ellis notation context; cited for completeness but not used as ratio source.

### Tertiary (LOW confidence — flagged for validation)
- [5-Limit Just Intonation (Loophole Letters blog)](https://loophole-letters.vercel.app/5limit-just-intonation) — informal blog source; ratios cross-verified against Wikipedia.
- [Pythagorean tuning (Microtonal Encyclopedia, Miraheze)](https://microtonal.miraheze.org/wiki/Pythagorean_tuning) — wiki-style fallback source; cross-verified against Wikipedia.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — closed-enum + ratio-table approach matches Phase 21 D-17 house style exactly; no new dependencies; CLAUDE.md guides "minimal dependencies" verified.
- Architecture: HIGH on Pattern 1 chokepoint identification (verified via grep); MEDIUM on Pattern A vs Pattern B choice (Pitfall 1 documents the CONTEXT.md mismatch — planner decision required).
- Mode tables: HIGH on Ionian + Aeolian (Wikipedia + Mudcat agree); MEDIUM on Dorian/Phrygian/Lydian/Mixolydian/Locrian (only Mudcat explicitly tabulates these); LOW on chromatic accidentals within non-Ionian/Aeolian modes (must construct via syntonic-semitone rule documented in Pitfall 3).
- Pitfalls: HIGH on Pitfall 1 (verified by grep), Pitfall 6 (existing tests already enforce). MEDIUM on Pitfall 2 (canonical sources cited but variants exist), Pitfall 5 (warning channel pattern verified, dedup logic new).
- Validation Architecture: HIGH on test framework + sampling rate (matches Phase 18-22 pattern verbatim); MEDIUM on Wave 0 gap completeness — planner may add or trim Facts.

**Research date:** 2026-05-03
**Valid until:** 2026-06-03 (Wikipedia/Mudcat ratio sources are stable; internal codebase is the moving target — re-verify any line:column references if files have changed since this research date).
