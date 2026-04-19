# Stack Research — v1.2 Stability & Composer DX

**Domain:** Brownfield interpreter milestone — no new product surface, no new subsystem.
**Researched:** 2026-04-18
**Confidence:** HIGH (existing infrastructure verified by direct file inspection; no new dependencies proposed)

---

## Verdict

**No new NuGet packages. No version changes. No tooling changes.**

Every v1.2 target — the C1–C7 bug fixes, the test-unblocking trio (`range(Int, Int)`, `break`/`continue`, `bpm`/`createStereoTrack`/`renderBars`), the Nyquist validation debt, and the full Tier A DX bundle (sequence slicing, enharmonic helpers, `reverbTime` context, MIDI velocity from dynamics, euclidean swing/humanize) — can be implemented by editing existing files. The existing infrastructure is a direct superset of what these features need.

The "minimal dependencies" prior from CLAUDE.md holds without qualification for this milestone.

---

## Recommended Stack (unchanged from v1.1)

### Core Runtime — Existing, No Changes

| Technology | Version | Purpose | Why (Unchanged) |
|------------|---------|---------|-----------------|
| .NET | **net10.0** (per csproj) | Runtime | Both `flow-lang.csproj` and `flow-interpreter.csproj` target `net10.0`. CLAUDE.md says "net9.0" but the repo has moved to net10. `dotnet --list-sdks` shows 10.0.106 installed. No migration needed for v1.2. |
| C# 13/14 | Latest via SDK 10 | Language | Record types, pattern matching, file-scoped namespaces, primary constructors already used throughout. |
| Pidgin | 3.5.1 | Parser combinator (unused) | Referenced in csproj but the actual parser is hand-written recursive descent (`Parsing/Parser.cs`). Leave as-is for v1.2; removal is a cleanup candidate for a later milestone — not worth the churn now. |
| Melanchall.DryWetMidi | **8.0.3** | MIDI file read/write | Confirmed current stable as of 2025-12-15. Targets .NET Standard 2.0, compatible with net10. Used by `StandardLibrary/Audio/MidiExport.cs` for Standard MIDI File writing and the existing MIDI-import path. No upgrade needed for Tier A MIDI-velocity work — `MidiExport.cs` already maps `note.Velocity` to MIDI velocity bytes (line 192). |
| PulseAudio (P/Invoke) | System | Audio playback | `Audio/PulseAudioSimpleBackend.cs`. Not touched by any v1.2 Tier A feature. |

### New Dependencies

**None.** No NuGet package is required for any v1.2 requirement.

---

## Tier A Feature → Infrastructure Mapping

Each feature's required primitives already exist. The work is **extension**, not **addition**.

### 1. Sequence slicing & phrase-edit (`slice(seq, start, end)`, `loopEdit(...)`)

| Need | Existing Support | Status |
|------|------------------|--------|
| Iterate/slice `MusicalNoteData[]` inside a `Sequence` value | `Collections.cs` has `Take`, `Drop`, `Init`, `Reverse` (lines 91, 114, 127, 130) — all operate on arrays and return new arrays | ✓ Direct reuse: compose `Drop(start) -> Take(end - start)` or add a dedicated `slice` signature in `BuiltInFunctions.cs` |
| Flow-surface registration | `InternalFunctionRegistry.Register(FunctionSignature, ...)` already used ~100 times | ✓ Pattern is well-established |
| Type plumbing for `Sequence` | `TypeSystem/SpecialTypes/Sequence` exists; transforms in `TransformFunctions.cs` already accept and return sequences | ✓ |

**No new dependency. Touches:** `StandardLibrary/BuiltInFunctions.cs` (new signatures), optional `audio.flow` convenience wrappers.

### 2. Enharmonic helpers (`H` = `B`, `Db` ↔ `C#`, `enharmonic()`)

| Need | Existing Support | Status |
|------|------------------|--------|
| Recognize `H` as alias for `B` in lexer | `Lexing/SimpleLexer.cs:543-564` already has note-vs-identifier lookahead that catches `A-G + digit`; extend to accept `H` | ✓ Local edit, no new infrastructure |
| Recognize `Db`, `Eb`, `Gb`, `Ab`, `Bb` as pitch tokens | `NoteType.Parse()` (used throughout `BuiltInFunctions.cs`) already handles sharps/flats; verify flat-letter handling for bare tokens (not just inside note-stream `\|...\|` blocks) | ✓ Extend existing parser |
| MIDI number round-trip for enharmonic equivalence | `PitchConversion.GetMidiNote(noteName, octave, alteration)` at `PitchConversion.cs:34-50` already computes MIDI number from any spelling. `enharmonic()` is a pure respell on top. | ✓ Pure function, new `BuiltInFunctions.cs` registration |

**No new dependency. Touches:** `Lexing/SimpleLexer.cs`, `Parsing/Parser.cs` (if bare chord/note expressions need updating), `StandardLibrary/Audio/PitchConversion.cs`, `StandardLibrary/BuiltInFunctions.cs`.

**Integration note:** `MusicalContext.ValidKeys` already enumerates enharmonic key spellings (`Dbmajor` and `Csharpmajor` coexist, lines 17-22) — no key-validation changes needed. Audit item `ScaleDatabase.cs:33-42` flags brittle enharmonic key parsing as a follow-up, but that is not part of Tier A.

### 3. `reverbTime { }` musical context block

| Need | Existing Support | Status |
|------|------------------|--------|
| Push/pop scoped numeric state | `Runtime/MusicalContext.cs` already tracks seven scoped properties (TimeSignature, Tempo, Swing, Key, Velocity, Pan, Gain). The fix for C1 (context-frame leak) will stabilize this code path; once fixed, adding one more property is a one-field addition | ✓ Direct extension of a fresh, audit-clean subsystem |
| Parse `reverbTime N { ... }` block | Existing `tempo N { ... }`, `swing N { ... }`, `gain N { ... }`, `pan N { ... }` all follow the same grammar. `TokenType.Tempo`, `TokenType.Swing`, `TokenType.Pan`, `TokenType.Gain` already exist in the lexer keyword switch (lines 580-589) | ✓ Add `reverbTime` keyword + AST dispatch mirror |
| Apply scoped reverb time at render | `Audio/DSP/Reverb.cs:26` takes `(input, roomSize, damping, mix)`. A `reverbTime` context becomes a roomSize/damping override at the render site (or a new `tailSeconds` parameter — design choice for REQUIREMENTS phase) | ✓ Pure DSP tweak; Schroeder reverb already parameterized |

**No new dependency. Touches:** `Lexing/SimpleLexer.cs`, `Parsing/Parser.cs`, `Ast/Statements/MusicalContextStatement.cs`, `Runtime/MusicalContext.cs`, `Interpreter/Interpreter.cs` (`ExecuteMusicalContext`), `StandardLibrary/Audio/DSP/Reverb.cs`, `StandardLibrary/Audio/SequenceRenderer.cs` or `SongRenderer.cs`.

**Dependency on bug fix:** C1 (context-frame leak) MUST be fixed before adding new context properties, or the leak compounds. This orders the phases naturally — stability first, then DX.

### 4. MIDI velocity from dynamic transforms

| Need | Existing Support | Status |
|------|------------------|--------|
| Dynamic transforms write `note.Velocity` | `TransformFunctions.cs` — `Crescendo`, `Decrescendo`, `Swell` (lines 399-435) already produce per-note velocity envelopes. Grep confirmed the dispatch is wired up in `RegisterDynamicTransforms`. | ✓ Already present |
| MIDI export reads `note.Velocity` | `Audio/MidiExport.cs:192` — `byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);` | ✓ Already maps 0.0–1.0 → 1–127 |
| End-to-end continuity | The loop appears closed in principle; the audit notes suggest it is untested / unvalidated for nested transforms | ✓ Verification work, not new code — may need a Nyquist-style validation pass |

**No new dependency. Touches:** `StandardLibrary/Audio/MidiExport.cs` (likely no change beyond validation), `StandardLibrary/Audio/SequenceRenderer.cs` (ensure dynamic-transform output is preserved through section/bar rendering to MIDI, not just to audio), tests.

**DryWetMidi integration point:** `MidiExport.cs` line 192 already uses DryWetMidi's `SevenBitNumber` wrapper for velocity. No DryWetMidi API change needed. The existing v8.0.3 NoteOnEvent/NoteOffEvent surface is sufficient.

**Audit follow-up (M-1 from CODEBASE-AUDIT-2026-04-18.md):** `MidiExport.cs:195` has a velocity floor of 1 (vs rest threshold). Consider addressing alongside this feature if the same file is being touched.

### 5. Euclidean swing/humanize parameters

| Need | Existing Support | Status |
|------|------------------|--------|
| Bjorklund euclidean algorithm | `StandardLibrary/BuiltInFunctions.cs:1028-1071` — `Bjorklund(hits, steps)` helper already exists and is used by the registered `euclidean(Int, Int, Note)` signature | ✓ Core algorithm present |
| Swing amount in musical context | `MusicalContext.Swing` (0.0–1.0, field at line 37) already exists and has a `swing { ... }` block surface. Applying it in the euclidean generator is a read from `ExecutionContext.GetMusicalContext()` | ✓ |
| Humanize (timing/velocity jitter) | `StandardLibrary/StdLib.cs` has `random()` / `randomInt()` / `choose()` (confirmed via CLAUDE.md built-in list). Gaussian nudge is a one-liner (Box-Muller over `random()`). | ✓ No RNG library needed |

**No new dependency. Touches:** `StandardLibrary/BuiltInFunctions.cs` (new `euclidean` overload with swing/humanize params or an extension function).

---

## Test-Unblocking Trio

All three are pure interpreter/stdlib additions. No new dependency.

| Missing | Where to Add | Existing Precedent |
|---------|--------------|-------------------|
| `range(Int, Int)` | `StandardLibrary/BuiltInFunctions.cs` (collections registration block) | `range(Int)` (existing single-arg form per CLAUDE.md stdlib list) is already there; add 2-arg overload using `OverloadResolver`. |
| `break` / `continue` in loops | `Interpreter/Interpreter.cs` (add break/continue control-flow sentinels mirroring `_returnValue`) | `TokenType.Break` and `TokenType.Continue` are already defined in the lexer (lines 593-594), so parsing is already in place — this is interpreter-only work. |
| `bpm()`, `createStereoTrack`, `renderBars` | `StandardLibrary/BuiltInFunctions.cs` + `StandardLibrary/Audio/*.cs` | Either implement as thin wrappers over `ExecutionContext.GetMusicalContext()` / existing `Track.cs` / `BarRenderer.cs`, or remove from `test_full_song.flow`. Both options are scope choices, not dependency choices. |

---

## Critical Bug Fixes — Dependency Impact

Zero new dependencies. All C1–C7 fixes are internal edits:

| Fix | File | Change Class |
|-----|------|-------------|
| C1 — context frame leak | `Interpreter/Interpreter.cs` ~133-289 | Control-flow restructure (try/finally around pop) |
| C2 — `_returnValue` short-circuit | `Interpreter/Interpreter.cs:73-74` | Condition narrowing |
| C3 — envelope div-by-zero | `EnvelopeProcessor.cs:108,120,150,156,169` | `Math.Max(1, frames)` guards |
| C4 — fade div-by-zero | `BufferHelpers.cs:130,159` | Same guard |
| C5 — augment/diminish swap | `TransformFunctions.cs:248,269` | Swap `+1`/`-1` on enum arithmetic |
| C6 — `init([])` semantics | `Collections.cs` | Add empty-check + error |
| C7 — Thunk exception caching | `Runtime/Thunk.cs:35-45` | Catch + cache exception, re-raise on subsequent `Force()` |

---

## Supporting Libraries

No additions for v1.2.

The .NET BCL already supplies everything the Tier A bundle needs:
- `System.Math` for Bjorklund and panning math
- `System.Random` (via existing `StdLib.cs`) for humanize jitter
- `System.Text.StringBuilder` for error formatting
- `System.IO.FileSystemWatcher` (already used for watch mode) — not touched in v1.2

---

## Development Tools

Unchanged from v1.1:

| Tool | Purpose | Notes |
|------|---------|-------|
| `dotnet build` | Compile solution | SDK 10.0.106 present locally |
| `dotnet run --project flow-interpreter -- …` | Run scripts / REPL | |
| `.flow` test scripts | Regression | 70+ tests in `tests/`; ~46 passing today per audit; v1.2 should bring all to green |
| `git` | Source control | |

No linter, no code-formatter tooling proposed to add — stay lean.

---

## Installation

No new packages. No commands required. Existing csproj:

```xml
<PackageReference Include="Melanchall.DryWetMidi" Version="8.0.3" />
<PackageReference Include="Pidgin" Version="3.5.1" />
```

is already correct for v1.2.

---

## Alternatives Considered (Why No New Library)

| Temptation | Why Skip for v1.2 |
|------------|-------------------|
| **A randomness/statistics library** (MathNet.Numerics) for euclidean humanize | Two-line Box-Muller over existing `random()` is adequate. MathNet.Numerics would add ~2 MB of DSP/stats code for one Gaussian sample. |
| **An FFT library** (FftSharp, NAudio.Dsp) for reverb-time modeling | `reverbTime { }` is a time-domain parameter (RT60 → feedback coefficient via `feedback = 10^(-3 * delayTime / RT60)`). Closed-form, no spectral work. |
| **A LINQ-backed slice helper package** | `Take(n).Skip(m)` already expresses sequence slicing. `Collections.cs` conventions already wrap LINQ. |
| **Test framework (xUnit, NUnit)** for the test-unblocking work | Flow tests are `.flow` scripts executed by the interpreter; adding a C# test framework would be a separate, parallel test surface. Defer unless/until C# logic needs fine-grained assertions. Out of scope for v1.2. |
| **Upgrade DryWetMidi 8.0.3 → 9.0.0-prerelease1** | 8.0.3 is the latest **stable** (released 2025-12-15). Prerelease in a brownfield milestone targeting stability is the wrong risk profile. |
| **Remove Pidgin** | Cleanup, not a v1.2 goal. Orthogonal to everything in the milestone. Defer. |

---

## What NOT to Use (Carry-Over Guidance)

Unchanged from v1.0/v1.1 stack research — restated for completeness:

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **NAudio** | Windows-centric (COM/MME/WASAPI); would duplicate existing PulseAudio + hand-rolled DSP | Existing `Audio/` subsystem |
| **CSCore** | Windows-focused, heavy | Existing `Audio/` subsystem |
| **NWaves** | Last update 2021; abandonware signal. Would create a second parallel DSP stack. | Existing hand-rolled `Audio/DSP/` (Reverb, Filter, Compressor, Delay, Panner, SidechainCompressor) |
| **managed-midi** | Marked "past project" on GitHub | DryWetMidi 8.0.3 (already present) |
| **System.Numerics SIMD vectorization** | Premature optimization; v1.2 is correctness/DX, not throughput | Stay sample-by-sample; profile first if needed in a later perf milestone |

---

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Melanchall.DryWetMidi 8.0.3 | .NET Standard 2.0+ (confirmed runs on net10) | NuGet metadata and direct import in `MidiExport.cs` both verified |
| Pidgin 3.5.1 | .NET Standard 2.0+ | Unused; kept for csproj stability |
| .NET 10.0.106 SDK | C# 13/14 | Matches `TargetFramework>net10.0` in both csproj files |

---

## Integration Points for Downstream Consumers

**REQUIREMENTS phase** — when writing REQ-IDs, note that:

- No REQ-ID should include "add NuGet package" or "upgrade dependency" as an acceptance criterion.
- Tier A REQ-IDs 1, 2, 3, 4, 5 can be scheduled in parallel from a dependency-graph standpoint (no shared new subsystem).
- The C1 fix is a **prerequisite** for the `reverbTime` REQ (adding a new context property on top of a broken push/pop will compound the leak). Sequence: stability → DX.
- `MidiExport.cs` is touched by both audit follow-up M-1 (velocity floor) and Tier A #4 (velocity from dynamics) — worth a single coordinated REQ if the milestone wants to batch file touches.

**ROADMAP phase** — phase structure implied by this stack analysis:

1. **Stability phase (C1–C7 + test trio):** Interpreter + stdlib only. No external API surface. No new deps. Fast merge loop.
2. **DX phase (Tier A 1–5):** Extension of existing subsystems. Each feature is isolated to 1–3 files.
3. **Tutorial refresh:** Pure `.flow` content work. No code changes in `flow-lang/`.

No phase introduces a new technology boundary, so phase ordering is driven by risk (stability first) not stack dependencies.

---

## Sources

- Direct file inspection (authoritative for this milestone):
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/flow-lang.csproj` — current deps
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/MusicalContext.cs` — scoped property pattern
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Audio/PitchConversion.cs` — MIDI pitch math
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Audio/MidiExport.cs` — DryWetMidi integration
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` — Schroeder reverb parameters
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/BuiltInFunctions.cs` — Bjorklund, euclidean registration, dynamic transforms registration
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — Crescendo/Decrescendo/Swell velocity envelopes
  - `/home/noah/Desktop/projects/flow-sharp/flow-lang/Lexing/SimpleLexer.cs:543-608` — identifier/note/keyword lookahead
  - `/home/noah/Desktop/projects/flow-sharp/.planning/CODEBASE-AUDIT-2026-04-18.md` — full audit
  - `/home/noah/Desktop/projects/flow-sharp/CLAUDE.md` — minimal-deps philosophy
- [Melanchall.DryWetMidi on NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi) — v8.0.3 confirmed current stable as of 2025-12-15; .NET Standard 2.0 target, net10 compatible (HIGH confidence, fetched 2026-04-18).
- Existing CLAUDE.md stack narrative (v1.0/v1.1 research output) — restated and re-verified against the current codebase; no drift found.

---

*Stack research for: v1.2 Stability & Composer DX*
*Researched: 2026-04-18*
