# Phase 33: SFZ Orchestral Sampler — Research

**Researched:** 2026-05-15
**Domain:** SFZ-format orchestral sampler — parser, region matching, varispeed pitch shift, equal-power sustain loop, first-class `Sfz` value type, stdlib import gating, MIDI export prefix-strip
**Confidence:** HIGH (architecture rides on Phase 22 / 28 / 29 / 30 / 32 patterns that already shipped; the SFZ spec subset is small and well-documented)

## Summary

Phase 33 ships an opt-in `use "@sfz"` stdlib module that adds a real SFZ-format
orchestral sampler to Flow. The composer surface — locked by 33-SPEC.md across
8 falsifiable requirements — is `Sfz violin = (loadSfz #violin)` followed by
`renderSong song "sampler:violin"`. Every architectural piece needed is already
in the repository: Phase 22 `FileIO.VarispeedResample` handles pitch shift,
Phase 29 `SampleCache` is the exact template for `SfzSampleCache`, Phase 28
`SynthUtils.GenerateArticulationADSR` + `ApplyEnvelope` layer the articulation
envelope, Phase 30 `FlowConfig` already loads TOML keys (adding `sfz_root` is a
one-property POCO patch), Phase 32 `TuningType` is the precise shape for
`SfzType` (sealed singleton, strict, reference identity), and Phase 32
`ScalaParser` is the closest analog for a hand-rolled INI-style parser with
strict numeric rules.

Two things are NOT already in the repo and dominate the actual implementation
risk: the SFZ parser itself (about 250 LOC; well-bounded by the 13-opcode
subset) and the equal-power 441-frame sustain loop crossfade math inside
`SfzRenderer.Render` (about 30 LOC; the failure mode the spec calls out as
worst-case for Phase 34). Everything else is wiring.

**Primary recommendation:** Follow CONTEXT D-01..D-20 exactly — they encode
the right trade-offs. The 128×128 precomputed grid (D-01/D-02) and per-note
render-time crossfade (D-18/D-19) are the two non-obvious decisions; both pay
off in code clarity and memory cost, and both have already been justified
against alternatives in the discussion log. Implement the parser using the
Phase 32 `ScalaParser.cs` strict-numeric pattern (`CultureInfo.InvariantCulture`,
no `AllowExponent`, no `AllowThousands`, hard step-cap for DoS resistance).

## Project Constraints (from CLAUDE.md)

The phase plan must honor these CLAUDE.md directives — extracted as actionable
gates for the planner:

- **Target framework:** .NET 10 (`<TargetFramework>net10.0</TargetFramework>`).
  Nullable reference types enabled, implicit usings enabled, file-scoped
  namespaces, AST/data records throughout.
- **Minimal dependencies:** NO new NuGet packages. DryWetMidi remains the only
  external dependency. The SFZ parser is hand-rolled C# (CLAUDE.md
  §"Guiding Principle: Minimal Dependencies" + §"Conventions").
- **Linux primary:** SFZ parser is pure C# + filesystem reads — cross-platform on
  its own. PulseAudio playback unchanged. No platform-specific code in the SFZ path.
- **Determinism contract:** Two consecutive renders of the same SFZ-rendered song
  must produce `cmp -clean` identical WAVs (Phase 18/25/27 contract preserved).
  Sample-load order, region iteration order, varispeed cache iteration order all
  use sorted iteration (see Phase 29 `SampleCache.EagerLoad` precedent).
- **Repo size cap:** 5 MB total for `flow-lang/Samples/` (enforced by
  `Phase29.RepoSizeTests`). Phase 33 ships nothing into that directory. Test
  fixtures land in `flow-lang.Tests/fixtures/sfz-smoke/` and must total < 100 KB.
- **Music > rigid correctness (CLAUDE.md memory: charitable-interpretation):**
  Unknown opcodes silently ignore with one-shot stderr advisory. Missing region
  for a (pitch, velocity) renders silence + advisory. Missing `sfz_root` errors
  ONCE with a clear pointer to the config file.
- **No emojis in code, docs, or commit messages** (CLAUDE.md global rule).
- **GSD workflow enforcement:** All file edits flow through GSD commands. Use
  `/gsd:execute-phase` to drive plan execution.
- **No external users yet (memory: pre-public):** Breaking changes can land in
  a single commit. No deprecation windows needed. SPEC contracts ARE the
  freeze line — Phase 34 turns this public.

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Region storage + lookup shape**

- **D-01 [grid-128x128]:** Each parsed `SfzData` carries a precomputed
  `SfzRegion?[128, 128]` grid keyed by `(midiPitch, midiVelocity)`. Built at
  `loadSfz` time, immutable thereafter. Cell value = winning region under SFZ
  last-declared-wins semantics.
- **D-02 [build-encodes-spec]:** Build loop iterates regions in declaration
  order, assigns `grid[k, v] = region` for every `(k ∈ [lokey..hikey],
  v ∈ [lovel..hivel])` cell. Later regions overwrite earlier ones —
  last-declared-wins becomes structurally enforced rather than implicit in
  lookup logic. Lookup is `Grid[midi, vel]`, no scanning.
- **D-03 [side-data-for-fallback]:** Alongside the grid, store
  `SortedByPitch: int[]` — sorted union of all (lokey..hikey) cells that have
  any region coverage. ~512 bytes per patch. Used by REQ-4 nearest-pitch
  fallback.
- **D-04 [memory-cost-bounded]:** ~144 KB per patch, ~1% of sample data size.

**Sample loading strategy**

- **D-05 [parse-vs-load-split]:** `loadSfz` does parsing + region-grid build
  ONLY. Zero `.wav` files hit disk during `loadSfz`. Returns an `Sfz` value
  carrying region metadata + sample-file-path strings.
- **D-06 [eager-on-renderSong-walk]:** When `renderSong song "sampler:NAME"`
  runs, walk the song's note set first, dereference `Grid[k, v]` for each
  unique `(pitch, velocity)` cell, collect distinct regions actually needed,
  eager-load only those regions' `.wav` files into a new `SfzSampleCache`.
  Mirrors Phase 29 D-13/14/15 pattern exactly.
- **D-07 [cache-lifetime-per-flow-engine]:** `SfzSampleCache` lives on
  `FlowEngine`. Lifetime = engine disposal.
- **D-08 [no-lazy-no-stutter]:** Lazy-on-first-use rejected — Phase 29 D-14
  precedent (mid-render disk IO causes stutter).

**Stdlib `@sfz` module shape + binding registry**

- **D-09 [hybrid-module-shape]:** `flow-lang/sfz.flow` runs a `(dict #violin
  "Strings/Violin/violin-Sustain.sfz" ...)` constructor binding the 19-symbol
  GM map, then calls `(__enableSfzModule __sfzInstruments)` that flips
  `ExecutionContext.SfzEnabled = true` and registers the lookup dict.
- **D-10 [c-sharp-always-registered]:** `loadSfz(Symbol)` and `loadSfz(String)`
  builtins registered unconditionally at FlowEngine startup. On call, the
  builtin checks `ExecutionContext.SfzEnabled`; if false, throws
  `UndefinedFunctionError("loadSfz requires 'use \"@sfz\"'")`.
- **D-11 [dict-in-flow-not-csharp]:** The 19-entry GM symbol→relative-path
  mapping lives in the Flow file, not in C#. Composers can read and inspect it.
- **D-12 [binding-registry-on-execution-context]:**
  `ExecutionContext.SfzPatchRegistry: Dictionary<string, SfzData>` is the
  canonical name→patch lookup. Populated by `Interpreter.ExecuteVariableDeclaration`
  when declared type is `SfzType` — the assignment handler writes
  `(name, sfzValue.As<SfzData>())` into the registry alongside the normal
  `CurrentFrame.SetVariable` call.
- **D-13 [sampler-dispatch-reads-registry]:** `SongRenderer` recognizes
  `instrument.StartsWith("sampler:")` and strips the prefix; reads
  `ExecutionContext.SfzPatchRegistry[name]`; on miss, throws
  `UnknownSamplerNameError(name, knownNames=registry.Keys)`.
- **D-14 [no-cross-frame-lookup]:** Flat name→patch map. Cross-frame lookups
  out of scope. Anonymous `(loadSfz #violin) -> renderSong "..."` deferred.

**MIDI export for sampler instruments**

- **D-15 [prefix-strip-into-gm-dict]:** `MidiExport` strips `sampler:` from the
  instrument string and looks up the remaining name in the existing GM-program
  dict. MIDI export of a symphony works without VSCO-CE installed on the
  receiver.
- **D-16 [gm-dict-12-new-entries]:** Add new entries: violin→40, viola→41,
  cello→42, contrabass→43, oboe→68, clarinet→71, bassoon→70, horn→60,
  trombone→57, tuba→58, timpani→47, choir→52, harp→46, guitar→24,
  harpsichord→6, celeste→8. Phase 28's existing entries preserved.
- **D-17 [track-naming]:** MIDI track-name uses the sampler-stripped name
  (e.g. `"violin"`, not `"sampler:violin"`).

**Loop crossfade implementation site**

- **D-18 [per-note-render-time]:** 441-frame equal-power sin/cos crossfade
  math runs inside `SfzRenderer.Render(note, ...)`. ~30 LOC.
- **D-19 [no-pre-computed-loop-cache]:** Pre-computing 5-second looped buffers
  per region rejected (~44 MB extra cache per patch).
- **D-20 [crossfade-not-in-spec-fixture-but-in-test]:** Smoke fixture is
  1-region simple-sustain (no crossfade). Separate unit test renders a
  4-second sustained note from a synthetic 2-region SFZ for the per-sample
  discontinuity check.

### Claude's Discretion

Decisions that follow from the above without separate user input:

- **Error class hierarchy.** `SfzParseException` extends
  `flow-lang/Parsing/TypeParser.cs` `ParseException`.
  `UnknownInstrumentSymbolError`, `UnknownSamplerNameError`,
  `MissingSfzRootError`, `SfzFileNotFoundError` extend appropriate Flow bases.
- **Advisory dedup state location.** `ExecutionContext.SfzDiagnostics:
  HashSet<string>` holds the `(patch-description, opcode-name)` /
  `(patch, missing-region-key)` / `(advisory-key)` set already advised.
  Extend `RenderingDiagnostics.WarnOnce` (Phase 23/32 pattern).
- **Determinism preservation.** Sample-load order during the renderSong-walk
  is determined by walking the song in declaration order and the region grid
  in `(pitch ascending, velocity ascending)` order. Mirrors Phase 29 D-31 /
  `SampleCache.EagerLoad`'s `OrderBy(p => p)` + `OrderBy(v => v,
  StringComparer.Ordinal)` pattern.
- **`SfzType` shape.** Sealed singleton extending `FlowType`;
  `IsCompatibleWith(SfzType.Instance)` strict; `GetSpecificity()` returns a
  unique number ≥ 150 (above existing music types). Mirrors `TuningType`
  exactly.
- **`Sfz` value internal model.** `SfzData` (immutable) holds: `Description:
  string`, `BasePath: string`, `Regions: List<SfzRegion>`, `Grid:
  SfzRegion?[128, 128]`, `SortedByPitch: int[]`.
- **`SfzRegion` field set.** All 13 opcode values + the 3 header levels'
  inherited defaults flattened per-region: `SamplePath`, `PitchKeycenter`,
  `LoKey`, `HiKey`, `LoVel`, `HiVel`, `LoopMode` (enum), `LoopStart`,
  `LoopEnd`, `AmpegAttack`, `AmpegRelease`, `Volume`, `Pan`.
- **Header inheritance applied AT PARSE TIME** by flattening. Runtime never
  traverses headers.

### Deferred Ideas (OUT OF SCOPE — DO NOT RESEARCH)

- Full SFZ v2 opcode coverage (Phase 33.x follow-up if Phase 34 needs)
- `writeSfz` / SFZ export
- Real-time SFZ hot reload
- Anonymous `Sfz` value flow without intermediate binding
- Per-articulation SFZ region selection (`locc64`/`hicc64`/`trigger`)
- More than 19 instrument symbols in the shipped dict
- Pre-computed loop body cache
- Sfz cache LRU eviction policy

## Phase Requirements

SPEC-1..SPEC-8 are the 8 falsifiable requirements locked in 33-SPEC.md. The
planner must map each plan/wave back to one or more of these IDs.

| ID | Description | Research Support |
|----|-------------|------------------|
| SPEC-1 | `use "@sfz"` stdlib import gates the surface; without it `loadSfz` raises `UndefinedFunctionError`, `sampler:` raises `UnknownInstrumentError` | §"Module Gating Pattern" + Phase 32 / `audio.flow` hybrid-import precedent |
| SPEC-2 | Symbol-keyed lookup via shipped 19-entry dict + config-resolved `sfz_root`; absolute path overload bypasses dict; unknown symbol errors with 19-symbol list | §"FlowConfig integration" + §"19-symbol GM dict" |
| SPEC-3 | Parser handles 13-opcode subset + `<region>`/`<group>`/`<global>` headers; unknown opcodes silently ignored + one-shot stderr advisory per `(patch, opcode-name)` | §"SFZ Parser Patterns" + Phase 32 ScalaParser strictness precedent |
| SPEC-4 | Region matching by `(pitch, velocity)`; nearest-key resample fallback via `FileIO.VarispeedResample` | §"Region Matching + Nearest-Pitch Fallback" |
| SPEC-5 | Equal-power 441-frame sin/cos loop crossfade; no per-sample amplitude jump > 0.05 in 4-second sustained body | §"Equal-Power Sustain Loop Crossfade" |
| SPEC-6 | `Sfz` first-class value type; binding to typed variable populates `ExecutionContext.SfzPatchRegistry`; `sampler:NAME` dispatch | §"First-Class Value Type Pattern" + Phase 32 `TuningType` precedent |
| SPEC-7 | CI smoke test renders synthetic SFZ fixture (< 100 KB total); non-empty WAV; RMS > −40 dBFS; discontinuity check on sustained body | §"CI Smoke Test Fixture" |
| SPEC-8 | Phase 28 articulation envelope applies on top of SFZ render; 6 articulations produce 6 distinct buffers within ±5% of locked rules | §"Articulation Envelope Hook" + Phase 29 `SampledInstrumentRenderer.cs:120-130` precedent |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Module gating (`use "@sfz"`) | flow-lang/sfz.flow (Flow stdlib) | ExecutionContext (C#) | D-09 hybrid: Flow owns the dict, C# owns the gate flag. Composers can edit the dict without C# rebuild. |
| Builtin overload registration | `BuiltInFunctions.cs` → `InternalFunctionRegistry` | FlowEngine startup | D-10: `loadSfz(Symbol)` and `loadSfz(String)` always-registered; check gate at call time. |
| Symbol→relative-path lookup | flow-lang/sfz.flow Dict<Symbol, String> | ExecutionContext field | D-11: 19-entry GM dict, frozen at module-load. |
| `sfz_root` config read | `flow-cli/Config/FlowConfigLoader.cs` (TOML deserializer) | `FlowConfig.Active` singleton (flow-lang/Runtime) | Phase 30 precedent. Add `string? SfzRoot` to `FlowConfigPoco`. |
| SFZ file parsing | `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` | `ScalaParser.cs` precedent | Hand-rolled INI-style; strict numeric per Phase 32 D-18. |
| Region grid build | `SfzParser.cs` (D-02 build encodes spec) | `SfzData` ctor | Last-declared-wins becomes write-order; lookup is a single array index. |
| First-class `Sfz` value | `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` | `Value.Sfz(SfzData)` factory | Mirrors Phase 32 `TuningType`. |
| Patch registry | `ExecutionContext.SfzPatchRegistry: Dictionary<string, SfzData>` | `Interpreter.ExecuteVariableDeclaration` populates on `SfzType` binding | D-12: typed-variable assignment writes to the registry. |
| Sample loading | `SfzSampleCache.cs` (new class) | `FileIO.LoadWavInternal` | D-06/D-07/D-08: eager-on-renderSong-walk; per-FlowEngine lifetime; mirror Phase 29 `SampleCache`. |
| Region matching at render | `SfzRenderer.cs` → `Grid[midi, vel]` | `SortedByPitch[]` nearest-pitch fallback | D-01 grid lookup is one array index; fallback finds nearest then varispeed-shifts. |
| Varispeed pitch shift | `FileIO.VarispeedResample(buffer, ratio)` | `Math.Pow(2.0, semitonesShift / 12.0)` | Phase 22 DX-15 primitive — verbatim reuse, zero new code. |
| Equal-power loop crossfade | `SfzRenderer.Render` (D-18 per-note) | sin/cos math, 441-frame window | ~30 LOC. No new cache. |
| Articulation envelope | `SynthUtils.GenerateArticulationADSR` + `SynthUtils.ApplyEnvelope` | `SfzRenderer.Render` (last step) | Phase 28 SPEC-5 locked rules. Baseline ADSR matches Phase 29 `SampledInstrumentRenderer` (lines 120-130). |
| Instrument-string dispatch | `SongRenderer.RenderSong` (new `sampler:` branch BEFORE existing per-instrument switch) | reads `ExecutionContext.SfzPatchRegistry` | D-13: prefix-recognize → strip → lookup → dispatch to `SfzRenderer.Render`. |
| MIDI export prefix-strip | `MidiExport.ResolveGmProgram` | Extended 16-entry GM dict | D-15/D-16: strip `sampler:` then prefix-match into the dict. |
| One-shot stderr advisories | `RenderingDiagnostics.WarnOnce` (Phase 23/32 pattern) | `ExecutionContext.SfzDiagnostics: HashSet<string>` | Three channels: unknown opcodes, missing regions, missing `sfz_root`. |
| CI smoke test | `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` | `flow-lang.Tests/fixtures/sfz-smoke/` (< 100 KB) | Phase 29 `SampledInstrumentSmokeTests.cs` precedent. |

## Standard Stack

### Core (verified existing in repository)

| Component | Verified Site | Phase 33 Reuse |
|-----------|--------------|----------------|
| `FileIO.LoadWavInternal(path)` | `flow-lang/StandardLibrary/Audio/FileIO.cs:362` [VERIFIED: code read] | `SfzSampleCache.EagerLoad` calls for each needed region's WAV |
| `FileIO.VarispeedResample(source, ratio)` | `flow-lang/StandardLibrary/Audio/FileIO.cs:338` [VERIFIED: code read] | SPEC-4 nearest-pitch fallback: `Math.Pow(2.0, semitonesShift / 12.0)` |
| `SynthUtils.GenerateArticulationADSR(...)` + `ApplyEnvelope(...)` | `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:126-130` [VERIFIED: code read] | SPEC-8: same baseline `baseAttack=0.005, baseDecay=0.05, baseSustain=1.0, baseRelease=0.05` |
| `RenderingDiagnostics.WarnOnce(key, message)` | `flow-lang/Diagnostics/RenderingDiagnostics.cs` [VERIFIED: code read] | All three advisory channels (unknown opcodes, missing regions, missing `sfz_root`) |
| `FlowConfig.Active` singleton | `flow-lang/Runtime/FlowConfig.cs:55` [VERIFIED: code read] | Read `sfz_root` once at module-import time |
| `FlowConfigPoco` (TOML POCO) | `flow-lang/Runtime/FlowConfig.cs:19` [VERIFIED: code read] | Add `string? SfzRoot { get; init; }`; Tomlyn auto-deserializes `sfz_root` key via `JsonNamingPolicy.SnakeCaseLower` |
| `TuningType` (sealed singleton; strict; reference identity) | `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` [VERIFIED: code read] | Template for new `SfzType` — same 5-method shape (`Name`, `GetSpecificity`, `IsCompatibleWith`, `CanConvertTo`, sealed singleton) |
| `Value.Tuning(ResolvedTuning)` factory | `flow-lang/Runtime/Value.cs:60` [VERIFIED: code read] | Template for `Value.Sfz(SfzData)` factory |
| `SongRenderer.RenderSong` dispatch site | `flow-lang/StandardLibrary/Audio/SongRenderer.cs:97-132` [VERIFIED: code read] | Insert `instrument.StartsWith("sampler:")` branch BEFORE the existing per-instrument switch |
| `INoteSynthesizer` interface | `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:16` [VERIFIED: code read] | `SfzRenderer` does NOT implement `INoteSynthesizer` directly — `SongRenderer`'s `sampler:` branch calls `SfzRenderer.Render(note, sampleRate, durationBeats, bpm, patchData)` with the matched patch from the registry. Mirrors `SampledInstrumentRenderer` shape (also not `INoteSynthesizer`-implementing). |
| `Interpreter.ExecuteVariableDeclaration` | `flow-lang/Interpreter/Interpreter.cs:588` [VERIFIED: code read] | Insert single branch at line ~646 (after `_context.DeclareVariable`): `if (varDecl.Type is SfzType) _context.SfzPatchRegistry[varDecl.Name] = value.As<SfzData>();` |
| Phase 32 `ScalaParser` strictness pattern | `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs:54` [VERIFIED: code read] | `NumberStyles.Float & ~AllowExponent & ~AllowThousands` + `CultureInfo.InvariantCulture` + 10000-step DoS cap (T-32-PARSE-01) |

### Alternatives Considered (and rejected)

| Instead of | Could Use | Why Rejected |
|------------|-----------|--------------|
| Hand-rolled SFZ parser | external SFZ library (e.g., `SFZero`, `Sforzando`) | Rejected by CONTEXT user directive + CLAUDE.md "no new NuGet deps". SFZ format is INI-style; a 13-opcode subset is well-bounded. |
| Precomputed 128×128 grid (D-01) | Flat List + linear scan | Discussed in 33-DISCUSSION-LOG.md; user pushed back on the initial flat-list recommendation. Grid build IS the SFZ last-declared-wins spec rule, making it structurally enforced. ~144 KB per patch is invisible against ~50 MB of sample data. |
| Per-note render-time crossfade (D-18) | Pre-computed loop buffer | ~44 MB extra cache per patch on top of ~50 MB sample data. Per-note math is ~30 ns/frame for 441 frames per loop transition — < 1% of render time. Memory cost dominates. |
| Equal-power sin/cos crossfade | Linear crossfade | Equal-power preserves perceived loudness across the transition (constant power = `cos² + sin² = 1`). Linear crossfade creates a perceived dip in the middle. Phase 33 SPEC-5 spectral-centroid check is the test gate. |
| 441-frame window (10 ms @ 44.1 kHz) | Larger / smaller | 441 frames = 10 ms is the standard short crossfade length that masks discontinuities without smearing transient detail. Locked by SPEC-5. |
| `loadSfz` returns first-class `Sfz` | `loadSfz` returns Buffer | Buffer would lose region metadata; the patch needs to be queried per-note at render time. First-class value matches Phase 32 `TuningType` shape. |
| `sampler:NAME` instrument string | New `renderSong song sfzValue` overload | Anonymous-flow path explicitly deferred (CONTEXT D-14). Named binding through `Sfz violin = ...` provides the key for the registry. Avoids a new `renderSong(Song, Sfz) → Buffer` overload. |

**Installation:** No new dependencies. The SFZ parser is hand-rolled C# in `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs`.

**Version verification:** N/A — no new packages.

## Package Legitimacy Audit

> Skipped — phase installs no external packages.

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Composer's .flow file                       │
└─────────────────────────────────────────────────────────────────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │ use "@sfz";             │ Sfz violin =            │ renderSong
        │                         │   (loadSfz #violin)     │   song "sampler:violin"
        ▼                         ▼                         ▼
┌──────────────────┐   ┌─────────────────────┐   ┌──────────────────────┐
│  ModuleLoader    │   │  loadSfz(Symbol)    │   │  SongRenderer        │
│  loads sfz.flow  │   │   builtin           │   │  .RenderSong         │
│                  │   │                     │   │                      │
│  • runs (dict …) │   │  1. Check           │   │  1. Check            │
│    binding 19    │   │     SfzEnabled flag │   │     instrument       │
│    GM symbols    │   │  2. Resolve symbol  │   │     starts with      │
│                  │   │     via dict        │   │     "sampler:"       │
│  • calls         │   │  3. Join with       │   │  2. Strip prefix     │
│    (__enableSfz  │   │     sfz_root from   │   │  3. Look up in       │
│    Module …)     │   │     FlowConfig      │   │     SfzPatchRegistry │
│    marker        │   │  4. Parse .sfz file │   │  4. Walk song notes  │
│                  │   │     (D-05 no .wav)  │   │  5. Eager-load .wav  │
└──────────────────┘   │  5. Build 128×128   │   │     via SfzSample    │
        │              │     region grid     │   │     Cache (D-06)     │
        ▼              │  6. Return          │   │  6. Per-note:        │
┌──────────────────┐   │     Value.Sfz(...)  │   │     SfzRenderer      │
│ Execution        │   └─────────────────────┘   │     .Render          │
│ Context fields   │            │                └──────────────────────┘
│                  │            │                          │
│ • SfzEnabled     │            │                          ▼
│ • SfzInstruments │            │           ┌──────────────────────────┐
│   (Dict<Symbol,  │            │           │  SfzRenderer.Render      │
│   String>)       │            │           │                          │
│ • SfzPatch       │            │           │  1. Grid[midi, vel]      │
│   Registry       │            │           │     → SfzRegion?         │
│   (Dictionary<   │            │           │  2. If null:             │
│   string,        │            ▼           │     nearest-pitch from   │
│   SfzData>)      │   ┌─────────────────┐  │     SortedByPitch[]      │
│ • SfzDiagnostics │   │ Interpreter     │  │     + VarispeedResample  │
│   (HashSet<      │◄──┤ .ExecuteVariable│  │     by semitone delta    │
│   string>)       │   │  Declaration:   │  │  3. Apply Volume + Pan   │
└──────────────────┘   │  if Type is     │  │     opcodes              │
                       │  SfzType:       │  │  4. If loop_mode ∈       │
                       │   write to      │  │     {continuous, sustain}│
                       │   SfzPatch      │  │     extend body via      │
                       │   Registry      │  │     441-frame sin/cos    │
                       └─────────────────┘  │     equal-power          │
                                            │     crossfade            │
                                            │  5. Phase 28             │
                                            │     ArticulationADSR     │
                                            │     applied on top       │
                                            │     (D-08 base ADSR)     │
                                            │  6. Return AudioBuffer   │
                                            └──────────────────────────┘
                                                       │
                                                       ▼
                                       ┌────────────────────────────┐
                                       │ SongRenderer mixes voices, │
                                       │ pans, applies reverb       │
                                       │ → final stereo WAV         │
                                       └────────────────────────────┘
```

Files referenced by tier (see `## Standard Stack` table for canonical paths):

- **Composer surface (.flow):** `flow-lang/sfz.flow` (new, ~50 LOC)
- **Type system:** `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` (new, ~40 LOC)
- **Builtin registration:** add to `flow-lang/StandardLibrary/BuiltInFunctions.cs`
  or new sibling `Sfz/SfzBuiltins.cs` mirroring `Tuning/ScalaBuiltins.cs`
- **Parser:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (new, ~250 LOC)
- **Data model:** `flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs` + `SfzRegion.cs`
  + `SfzLoopMode.cs` (new, ~150 LOC combined)
- **Sample cache:** `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs`
  (new, ~200 LOC; parallel to `SampleCache.cs`)
- **Renderer:** `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` (new, ~200 LOC)
- **Execution-context fields:** patch `flow-lang/Runtime/ExecutionContext.cs`
  with four new fields
- **Interpreter hook:** single branch in `flow-lang/Interpreter/Interpreter.cs`
  `ExecuteVariableDeclaration` at line ~646
- **SongRenderer hook:** single branch in `flow-lang/StandardLibrary/Audio/SongRenderer.cs`
  at line ~100 (BEFORE existing instrument dispatch)
- **MIDI export hook:** patch `flow-lang/StandardLibrary/Audio/MidiExport.cs`
  `ResolveGmProgram` (line 60) to strip `sampler:` and extend the dict
- **Config:** patch `flow-lang/Runtime/FlowConfig.cs` `FlowConfigPoco` with
  `string? SfzRoot`
- **Tests:** `flow-lang.Tests/Integration/Phase33/` (new directory) +
  `flow-lang.Tests/fixtures/sfz-smoke/` (new directory, < 100 KB)
- **Docs:** `examples/symphony/README.md` + `examples/symphony/sfz_smoke.flow`

### Pattern 1: Hybrid Stdlib Module + C# Builtin Gate

**What:** A `.flow` file does composer-visible setup (constants, dicts);
imports trigger a side-effecting C# call that flips a runtime flag and
registers data on the ExecutionContext. Builtins are registered unconditionally
at FlowEngine startup but check the flag on call.

**When to use:** Opt-in stdlib features that need to coexist with a "without
the import, the function is undefined" diagnostic. CONTEXT D-09/D-10/D-11 chose
this over (a) parser-recognized magic import names that auto-register C# state
and (b) pure-Flow forward-decls.

**Example pattern:**

```csharp
// In ExecutionContext.cs — new fields
public bool SfzEnabled { get; set; } = false;
public Dictionary<Value, string> SfzInstruments { get; } = new();  // Symbol → relative path
public Dictionary<string, SfzData> SfzPatchRegistry { get; } = new();
public HashSet<string> SfzDiagnostics { get; } = new();

// In a new SfzBuiltins.cs (mirroring ScalaBuiltins.cs)
public static void Register(InternalFunctionRegistry registry, ExecutionContext context)
{
    // __enableSfzModule(Dict<Symbol, String>) — internal marker called from sfz.flow
    var enableSig = new FunctionSignature("__enableSfzModule",
        new[] { /* DictType ... */ });
    registry.Register("__enableSfzModule", enableSig,
        args => EnableSfzModule(args, context));

    // loadSfz(Symbol) — always registered; checks SfzEnabled at call time
    var sigSym = new FunctionSignature("loadSfz", new[] { SymbolType.Instance });
    registry.Register("loadSfz", sigSym, args => LoadSfzSymbol(args, context));

    // loadSfz(String) — same gating
    var sigStr = new FunctionSignature("loadSfz", new[] { StringType.Instance });
    registry.Register("loadSfz", sigStr, args => LoadSfzString(args, context));
}

private static Value LoadSfzSymbol(IReadOnlyList<Value> args, ExecutionContext ctx)
{
    if (!ctx.SfzEnabled)
        throw new UndefinedFunctionError(
            "loadSfz requires 'use \"@sfz\"' at the top of your script");
    // ... lookup symbol → relative path → join with sfz_root → parse
}
```

```flow
// flow-lang/sfz.flow
Note: SFZ orchestral sampler — opt-in via `use "@sfz"`

Note: 19-entry GM orchestral symbol → relative path map
Note: (relative paths join with sfz_root from ~/.config/flow/config.toml)
internal proc __enableSfzModule(Dict: instruments)

Dict instruments = (dict
  #violin       "Strings/Violin/violin-Sustain.sfz"
  #viola        "Strings/Viola/viola-Sustain.sfz"
  #cello        "Strings/Cello/cello-Sustain.sfz"
  #contrabass   "Strings/Contrabass/contrabass-Sustain.sfz"
  #flute        "Woodwinds/Flute/flute-Sustain.sfz"
  Note: ...etc, 19 entries total
)

(__enableSfzModule instruments)
```

**Why this works:** The `.flow` file is plain Flow — composers can read it,
inspect the dict via `(get instruments #violin)` from their own scripts, and
the dict edit doesn't require a C# rebuild. The C# side does the actual
heavy lifting (file parsing, region grid build, varispeed math) while the
Flow side carries the metadata.

### Pattern 2: SFZ Parser (Hand-Rolled INI-style)

**What:** Single-pass line-by-line parser. Headers in angle brackets switch
the current accumulator; `opcode=value` lines populate the active region's
dictionary. Header inheritance is applied at parse time by flattening (D
of "Claude's Discretion").

**When to use:** Per CLAUDE.md "Minimal Dependencies" — no external parser
library. The SFZ format is INI-style and the 13-opcode subset is well-bounded.

**Example pattern (verified shape; modeled on Phase 32 `ScalaParser.cs`):**

```csharp
// In flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
using System.Globalization;

public sealed class SfzParser
{
    private const int MaxRegionCount = 10000;  // T-33-PARSE-01 DoS guard
    private static readonly NumberStyles FloatStyle =
        NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands;

    // Whitelist — anything else is silently ignored + one-shot advisory
    private static readonly HashSet<string> KnownOpcodes = new(StringComparer.Ordinal)
    {
        "sample", "lokey", "hikey", "pitch_keycenter", "lovel", "hivel",
        "loop_mode", "loop_start", "loop_end",
        "ampeg_attack", "ampeg_release", "volume", "pan"
    };

    public static SfzData Parse(string content, string filePath,
        HashSet<string> diagnosticsSink, string patchDescription)
    {
        // Line-by-line; current accumulator state tracks <global>, <group>, <region>
        // On <region> header, flatten current <global> + <group> defaults into a new
        // SfzRegion; subsequent opcode= lines override the inherited values.
        // Skip `// ` comments. Allow inline opcodes on same line (split by whitespace).

        // Strict numeric: int.TryParse / double.TryParse with the locked styles.
        // Unknown opcode? RenderingDiagnostics.WarnOnce keyed by
        // (patchDescription, opcode-name); continue parsing.

        // After loop: build 128×128 grid (D-02) + SortedByPitch[] (D-03).
        // Return SfzData(description, basePath, regions, grid, sortedByPitch).
    }
}
```

**Quirks to handle:**

- **Comments:** `// ` line comments. SFZ also allows `<!-- -->` blocks but the
  13-opcode subset typically uses `//` only. Strip everything after the first
  `//` on each line before tokenizing.
- **Multiple opcodes per line:** Headers and opcodes can share a line (e.g.,
  `<region> sample=violin_A4.wav lokey=57 hikey=64 pitch_keycenter=60`).
  Split by whitespace AFTER recognizing the header token.
- **`pitch_keycenter` accepts MIDI number OR scientific note name:**
  e.g. `pitch_keycenter=60` and `pitch_keycenter=C4` are equivalent.
  [CITED: openmpt.org/Manual:_SFZ_Implementation] For Phase 33's 13-opcode
  subset we accept BOTH — MIDI number is primary; scientific notation falls
  back to `PitchConversion` (existing helper). Document the contract.
- **Sample path resolution:** Sample paths are relative to the .sfz file's
  directory. `<control> default_path=...` is OUT of the 13-opcode subset
  per SPEC-3 — silently ignored with advisory.
- **Header inheritance:** `<global>` opcodes inherit into every `<group>`; a
  `<group>`'s opcodes inherit into every `<region>` under it. Apply at parse
  time by maintaining `currentGlobal: Dictionary<string,string>` and
  `currentGroup: Dictionary<string,string>` and merging into each new region's
  dictionary before applying region-specific overrides.
- **Defaults from spec** [CITED: sfzformat.com/opcodes/]:
  - `lokey=0`, `hikey=127`, `pitch_keycenter=60`, `lovel=1`, `hivel=127`
  - `loop_mode="no_loop"` (or `"loop_continuous"` if loop_start/end are present)
  - `loop_start=0`, `loop_end=0`
  - `ampeg_attack=0` seconds, `ampeg_release=0.001` seconds (spec) — note that
    Phase 33 reuses Phase 29's near-transparent baseline (`baseRelease=0.05s`)
    when `ampeg_release` is at its default; the SFZ value overrides the
    baseline when explicit.
  - `volume=0` dB (range -144 to +6)
  - `pan=0` (range -100 to +100; map to Flow's `[-1.0, +1.0]` voice pan)
- **`loop_mode` valid values** [VERIFIED: sfzformat.com/opcodes/loop_mode]:
  `no_loop`, `one_shot`, `loop_continuous`, `loop_sustain`. Unknown values
  fall back to `no_loop` + advisory.

### Pattern 3: First-Class Value Type — Mirror `TuningType`

**What:** A sealed singleton extending `FlowType`. Strict compatibility (no
numeric coercion). Reference identity (two `loadSfz` calls produce distinct
values even with the same arguments). A `Value.Sfz(SfzData)` factory wraps
the runtime data.

**When to use:** Per CONTEXT Claude's Discretion + Phase 32 SPEC precedent —
new music-typed values that have intrinsic identity (a tuning system, a
sampler patch) and no meaningful numeric conversion.

**Example pattern (verified shape; modeled on `TuningType.cs`):**

```csharp
// flow-lang/TypeSystem/SpecialTypes/SfzType.cs
namespace FlowLang.TypeSystem.SpecialTypes;

public sealed class SfzType : FlowType
{
    private SfzType() { }
    public static SfzType Instance { get; } = new();
    public override string Name => "Sfz";
    // Specificity above existing music types per Claude's Discretion.
    // Existing slots: Tuning=137, Section=138, Beat=139, Song=140, Hertz=144.
    // SfzType=150 keeps clearance above all music types; the next free slot is fine.
    public override int GetSpecificity() => 150;
    public override bool IsCompatibleWith(FlowType target) => target is SfzType;
    public override bool CanConvertTo(FlowType target) => target is SfzType;
}
```

```csharp
// Add to flow-lang/Runtime/Value.cs (after Value.Tuning factory at line 60)
public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data) =>
    new(data, SfzType.Instance);
```

### Pattern 4: Region Matching + Nearest-Pitch Fallback

**What:** `Grid[midi, vel]` returns the winning region or null. On null, scan
`SortedByPitch[]` for the closest pitch with any coverage, then varispeed-shift
that region's sample by the pitch delta.

**Example pattern:**

```csharp
// In SfzRenderer.cs
public AudioBuffer Render(MusicalNoteData note, int sampleRate,
    double durationBeats, double bpm, SfzData patch)
{
    if (note.IsRest)
        return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

    int midi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
    int vel = Math.Clamp((int)Math.Round(note.Velocity * 127.0), 0, 127);

    SfzRegion? region = patch.Grid[midi, vel];
    int semitonesShift = 0;
    if (region is null)
    {
        // Nearest-pitch fallback per SPEC-4. SortedByPitch is sorted ascending.
        int nearestPitch = FindNearest(patch.SortedByPitch, midi);
        // ... walk velocity slots at that pitch (prefer in-range, else nearest velocity)
        region = patch.Grid[nearestPitch, vel] ?? FindAnyAtPitch(patch, nearestPitch);
        if (region is null)
        {
            RenderingDiagnostics.WarnOnce(
                sentinelKey: $"sfz:missing:{patch.Description}:{midi}:{vel}",
                message: $"[sfz] no region for ({midi}, {vel}) in '{patch.Description}' — rendered as rest");
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
        }
        semitonesShift = midi - nearestPitch;
    }

    // ... varispeed-shift via FileIO.VarispeedResample(rawWav,
    //     Math.Pow(2.0, semitonesShift / 12.0)) and continue.
}
```

### Pattern 5: Equal-Power Sustain Loop Crossfade

**What:** When the source frame is within the last 441 samples of `loop_end`,
blend with the corresponding offset from `loop_start` using
`cos(πt/2N) * a + sin(πt/2N) * b`. Constant power.

**Example pattern:**

```csharp
// Inside SfzRenderer.Render — for each output frame
// Source code paraphrase (D-18 ~30 LOC inside SfzRenderer.Render).

// 441 frames = 10 ms at 44.1 kHz. Locked by SPEC-5.
const int CrossfadeFrames = 441;

int loopLen = region.LoopEnd - region.LoopStart;   // body length in source frames
if (loopLen <= 0 || region.LoopMode == SfzLoopMode.NoLoop ||
    region.LoopMode == SfzLoopMode.OneShot)
{
    // No loop — copy and zero-pad as today.
    return BasicCopy(sourceBuffer, targetFrames);
}

// Loop-continuous / loop-sustain path
var output = new float[targetFrames];
for (int dst = 0; dst < targetFrames; dst++)
{
    int absSrc = dst;
    if (dst >= region.LoopEnd)
    {
        // We're past the loop_end; wrap into the loop body.
        int wrapped = ((dst - region.LoopEnd) % loopLen) + region.LoopStart;
        absSrc = wrapped;
    }

    // Equal-power crossfade window in the last CrossfadeFrames before loop_end
    int distToLoopEnd = region.LoopEnd - absSrc;
    if (distToLoopEnd > 0 && distToLoopEnd <= CrossfadeFrames && dst >= region.LoopStart)
    {
        int crossIndex = CrossfadeFrames - distToLoopEnd;  // 0..N-1
        float t = (float)crossIndex / CrossfadeFrames;
        float wA = MathF.Cos(MathF.PI * t / 2.0f);   // last samples of body
        float wB = MathF.Sin(MathF.PI * t / 2.0f);   // first samples after loop_start
        int srcA = absSrc;
        int srcB = region.LoopStart + crossIndex;
        output[dst] = wA * sourceBuffer[srcA] + wB * sourceBuffer[srcB];
    }
    else
    {
        output[dst] = sourceBuffer[Math.Min(absSrc, sourceBuffer.Length - 1)];
    }
}
```

**Why equal-power (not linear):** `cos²(x) + sin²(x) = 1` for all x, so the
combined power across the transition is constant. A linear crossfade has
`(1-t)² + t² = 1 - 2t + 2t²` which dips to 0.5 at t=0.5 — audible loudness sag
at the loop boundary. SPEC-5's spectral-centroid acceptance criterion specifically
checks this.

### Pattern 6: Articulation Envelope Hook (Phase 28 SPEC-5)

**What:** After region match + loop expansion + amplitude application, call
`SynthUtils.GenerateArticulationADSR` and `SynthUtils.ApplyEnvelope` exactly as
Phase 29's `SampledInstrumentRenderer` does.

**Example pattern (verified from `SampledInstrumentRenderer.cs:120-130`):**

```csharp
// After region buffer assembly + Volume/Pan applied
float[] envelope = SynthUtils.GenerateArticulationADSR(
    note.Articulation,
    baseAttack:  region.AmpegAttack > 0  ? region.AmpegAttack  : 0.005,
    baseDecay:                                                   0.05,
    baseSustain:                                                 1.0,
    baseRelease: region.AmpegRelease > 0 ? region.AmpegRelease : 0.05,
    frames: targetFrames,
    sampleRate: sampleRate,
    isPercussion: false);
SynthUtils.ApplyEnvelope(fitted, envelope);
```

**Why the `region.AmpegAttack > 0` guard:** SFZ default is 0, but Phase 33's
locked baseline matches Phase 29 (near-transparent: 0.005 s). The SFZ value
overrides the baseline only when the composer explicitly authored it. This is
the contract called out in SPEC-8 acceptance criterion: "A region with
`ampeg_attack=0.5` produces a measurably slower attack (>200 ms) than a region
with `ampeg_attack=0.005`."

### Pattern 7: Determinism via Sorted Iteration

**What:** Every map-iteration in the eager-load path uses sorted-ascending
keys + ordinal string comparison. Two consecutive renders produce identical
file-load order, identical varispeed cache iteration order, identical RNG
state.

**Example pattern (verified from `SampleCache.cs:88-92`):**

```csharp
foreach (var pitch in pitches.OrderBy(p => p))
{
    foreach (var velocity in velocities.OrderBy(v => v, StringComparer.Ordinal))
    {
        // ... load WAV
    }
}
```

For Phase 33: walk regions in declaration order (`patch.Regions` is a
`List<SfzRegion>` already in declaration order), then within the renderSong-
walk eager-load phase, iterate the collected unique-regions set in
`(samplePath, pitch, velocity)` lexicographic order.

### Anti-Patterns to Avoid

- **Reading `sfz_root` on every `loadSfz` call.** Read ONCE at module-import
  time and cache on the ExecutionContext (or read at first `loadSfz` call and
  cache thereafter). Spec says "reads the `sfz_root` key from
  `~/.config/flow/config.toml` once on first import". A repeated read is a
  determinism risk if the user edits the file mid-script.
- **Hand-rolling another WAV loader.** `FileIO.LoadWavInternal` already handles
  16/24/32-bit PCM and resamples to 44.1 kHz. Reuse — do NOT duplicate.
- **Hand-rolling pitch shift math.** `FileIO.VarispeedResample` is verbatim
  reusable. The semitone → ratio formula is `Math.Pow(2.0, semitones / 12.0)`.
- **Putting SFZ-specific logic in `SampledInstrumentRenderer.cs`.** Phase 29's
  bundled-sample path must stay byte-identical. SFZ is purely additive — new
  files in `Audio/Sfz/`, new branch in `SongRenderer`, no edits to existing
  render paths.
- **Building a separate FlowEngine-static cache.** `SfzSampleCache` lives on
  the `FlowEngine` instance (mirrors Phase 29). Static-mutable-state precedent
  exists (`FlowEngine.CurrentSampleCache`) but is the wrong shape — multiple
  Sfz patches share one cache but each patch can co-load with the bundled
  Phase 29 cache.
- **Failing on unknown opcodes.** SFZ libraries in the wild include hundreds
  of opcodes outside the 13-entry subset. Hard-failing makes VSCO-CE unloadable.
  Silently ignore + one-shot advisory is the locked behavior (SPEC-3 + CLAUDE.md
  charitable-interpretation memory).
- **Hard-coded `~/.flow/samples/VSCO-CE` as fallback for `sfz_root`.** Spec
  says missing-key errors with a clear pointer to the config. No silent default —
  this is the one place charitable interpretation does NOT apply, because the
  composer needs to know where to install VSCO-CE.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WAV file loading | New WAV reader | `FileIO.LoadWavInternal(path)` | Already supports 16/24/32-bit PCM + 44.1 kHz resample |
| Pitch shift math | New resampler | `FileIO.VarispeedResample(buffer, ratio)` | Phase 22 DX-15 linear-interp; verified working |
| Articulation envelope | New ADSR | `SynthUtils.GenerateArticulationADSR` + `ApplyEnvelope` | Phase 28 SPEC-5 locked rules; Phase 29 baseline ADSR works |
| One-shot advisory dedup | New HashSet logic | `RenderingDiagnostics.WarnOnce` | Phase 23/32 pattern; thread-safe; tests can reset |
| TOML config reading | New parser | `FlowConfigLoader.LoadFromXdg` + `FlowConfig.Active` | Phase 30 already wired; add one POCO property |
| Symbol intern table | New dict | `ExecutionContext.SymbolInternTable` | Phase 26.1 already provides pointer equality |
| Sample cache | New class from scratch | `SampleCache.cs` as a template | Mirror the structure exactly; eager-load idempotency proven |
| Sealed first-class type | New shape | `TuningType.cs` as a template | 5-method shape; reference identity convention |
| GM program lookup dict | New dict | `MidiExport.ResolveGmProgram` extension | Add new entries; keep existing prefix-match order |
| MIDI note → name string | New helper | `PitchConversion` namespace | Already used in Phase 29's `SampleCache.MidiToPitchName` |

**Key insight:** Phase 33 has 11 reusable building blocks already in the
repository. The actual *new* logic is the SFZ parser (~250 LOC) and the loop
crossfade math (~30 LOC). Everything else is wiring + a few new files
mirroring proven shapes. The fact that every architectural piece has a Phase
22/28/29/30/32 precedent is what made the ambiguity score so low (0.11) in
the SPEC.

## Runtime State Inventory

> Not applicable — Phase 33 is greenfield (purely additive). No rename,
> refactor, migration, or string replacement. Phase 29's `SampledInstrumentRenderer`
> path remains untouched.

## Common Pitfalls

### Pitfall 1: Symbol Equality vs Dict Lookup
**What goes wrong:** `Dict<Symbol, String>` lookups depend on Symbol pointer
equality (Phase 26.1 SYM-01). If `__sfzInstruments` is loaded on one
ExecutionContext and `loadSfz #violin` is called from another, the `#violin`
literal interns into a DIFFERENT Value instance — the dict miss looks like
"unknown symbol" even though the name string matches.
**Why it happens:** `SymbolInternTable` is per-ExecutionContext (verified at
`ExecutionContext.cs:85`). REPL eval boundaries reuse the context; one-shot
scripts use one context.
**How to avoid:** Store the dict on the ExecutionContext (CONTEXT D-09
already does this via `__enableSfzModule`). All `loadSfz` calls read it
through the same context — pointer equality holds.
**Warning signs:** A test that constructs a fresh `FlowEngine` and calls
`loadSfz` directly via `runner.Execute` works; a test that calls
`FlowEngine.Context.SfzInstruments` lookup directly from C# breaks because
the C# code synthesizes a Symbol Value via a different intern table.

### Pitfall 2: `sfz_root` Read Timing
**What goes wrong:** `FlowConfig.Active` is mutable; if a test resets it
(`FlowConfig.Reset()`) after `use "@sfz"` has captured the value, subsequent
`loadSfz` calls see the new (default-null) value and error on missing root.
**Why it happens:** Test isolation pollutes the singleton.
**How to avoid:** Read `sfz_root` ONCE at the moment of the first `loadSfz`
call within a given `ExecutionContext` and cache on the context itself
(e.g., `ExecutionContext.ResolvedSfzRoot: string?`). Subsequent calls in the
same context use the cached value. This also keeps determinism — a script
can't accidentally pick up a mid-run config edit.
**Warning signs:** A `[Collection("FlowScripts")]`-serialized test passes alone but fails when run after another Phase 30 test.

### Pitfall 3: Loop Crossfade with `loop_end > sample.Length`
**What goes wrong:** A malformed .sfz can declare `loop_end=999999` when the
sample is only 22050 frames long. The crossfade math reads past the buffer
end → `IndexOutOfRangeException` or random memory at runtime.
**Why it happens:** SFZ format does not enforce `loop_end <= sampleLength`.
**How to avoid:** At parse time, clamp `loop_end = Math.Min(loop_end,
sampleLength - 1)` — but the sample isn't loaded yet at parse time. The
correct point is in `SfzRenderer.Render`, AFTER loading the sample, before
the loop: `effectiveLoopEnd = Math.Min(region.LoopEnd, sourceBuffer.Length - 1)`.
**Warning signs:** A `loop_end` value larger than the loaded WAV's frame count.

### Pitfall 4: 128×128 Grid Allocation Cost on Many Patches
**What goes wrong:** A symphony with 12 patches loads `12 × 128 × 128 × (8
bytes for object reference) = 1.5 MB` of grid memory. Not catastrophic, but
worth noting against the "~144 KB per patch" estimate in CONTEXT D-04.
**Why it happens:** Reference-array allocation in .NET is 8 bytes per cell on
64-bit. CONTEXT D-04 mentions "16-bit indices if region count < 65536 to halve
it" — that would be a `short[128, 128]` of region indices + a `List<SfzRegion>`
side table.
**How to avoid:** Use 16-bit region-index grid (`short?[128, 128]` or
`short[128, 128]` with `-1` sentinel) for patches with < 32767 regions
(every realistic patch). Cuts grid memory in half. CONTEXT D-04 hints at this
optimization; planner can decide whether to ship the 16-bit form now or in
a follow-up.
**Warning signs:** Symphony-scale memory pressure during Phase 34 dev.

### Pitfall 5: Region Eager-Load Order Determinism
**What goes wrong:** The renderSong-walk collects unique regions into a
`HashSet<SfzRegion>`. `HashSet<T>` iteration order is implementation-defined
in .NET (and can change between runs). Two consecutive runs may load WAVs in
different order → different RNG state at any downstream call site → broken
two-run byte-identical contract.
**Why it happens:** The Phase 18/25/27 determinism contract assumes byte-
identical pipelines.
**How to avoid:** Mirror Phase 29 `SampleCache.EagerLoad` exactly — wrap the
collected set in `.OrderBy(r => r.SamplePath, StringComparer.Ordinal)
.ThenBy(r => r.PitchKeycenter)` before iterating in eager-load. The Phase
29 precedent is at `SampleCache.cs:88-92` and is the locked pattern.
**Warning signs:** A test that passes on one machine and fails on another;
a CI run that flakes intermittently.

### Pitfall 6: MIDI Export Prefix-Strip Order
**What goes wrong:** `ResolveGmProgram("sampler:flute")` is called BEFORE the
existing `lower.StartsWith("flute")` branch fires. If the prefix-strip happens
INSIDE the existing function but the new entries are AFTER `flute`, the
prefix-stripped name `"flute"` still routes to flute. Correct. But if a
developer adds entries BEFORE the prefix-strip, the `sampler:` prefix bleeds
through and the dispatch routes to GM 0 (piano) — the fallback.
**Why it happens:** Order matters in a `StartsWith` chain. The prefix-strip
must happen at the TOP of `ResolveGmProgram`, before any existing
`StartsWith` check.
**How to avoid:** Add the prefix-strip as line 63 of `MidiExport.cs` (BEFORE
the `piano` check). Comment it explicitly: `// D-15: strip "sampler:" prefix
before GM dispatch so MIDI export works without VSCO-CE installed.`
**Warning signs:** A symphony's MIDI export plays back as all-piano on
external receivers.

### Pitfall 7: SFZ `pan` Range Mismatch
**What goes wrong:** SFZ `pan=100` is hard-right; Flow's `Voice.Pan = 1.0` is
hard-right. The conversion factor is `flow_pan = sfz_pan / 100.0`. Forgetting
this maps SFZ pan -100 → Flow pan -100.0 → past hard-left → renderer wraps or
clips silently.
**Why it happens:** Different normalization conventions across audio formats.
**How to avoid:** In `SfzRenderer.Render`, apply
`voicePan = region.Pan / 100.0` when wrapping the rendered buffer in a Voice.
Or apply in the parser when populating `SfzRegion.Pan` — normalize once on
read. Document the field's units explicitly (`/// Pan in Flow's [-1.0, +1.0]
range, NOT SFZ's [-100, +100]`).
**Warning signs:** Stereo image collapse or unexpected channel emphasis.

### Pitfall 8: `volume` Opcode Sign Convention
**What goes wrong:** `volume=0` means "unity gain" in SFZ (0 dB), not "silent".
A naive implementation `gain = region.Volume` would treat 0 as silent.
**Why it happens:** SFZ `volume` is decibels; Flow's `Voice.Gain` is linear.
**How to avoid:** Convert at read time: `linearGain = Math.Pow(10.0,
region.Volume / 20.0)`. SFZ default of 0 dB → linearGain 1.0. SFZ -6 dB →
linearGain ≈ 0.501.
**Warning signs:** Every patch silent; or massively loud first note.

### Pitfall 9: `lovel=0` vs `lovel=1`
**What goes wrong:** SFZ spec defaults `lovel=1` (not 0). Per
[VERIFIED: sfzformat.com/opcodes/]. If the parser populates the grid with
`lovel=1` and a note arrives at velocity 0.0 → MIDI velocity 0 → no region
matches → silence.
**Why it happens:** MIDI velocity 0 typically means note-off, not note-on.
Flow's `note.Velocity` is a `[0.0, 1.0]` double; `0.0` is allowed.
**How to avoid:** Clamp the rendered velocity to `[1, 127]` when computing
the grid index (or treat velocity 0 as "use region for vel=1"). Document the
mapping in `SfzRenderer.Render`.
**Warning signs:** Some notes silently render to zero buffer; tests at
velocity=0 fail; charitable-interpretation breaks.

### Pitfall 10: Multiple Same-Name Sfz Bindings
**What goes wrong:** A script does
`Sfz violin = (loadSfz #violin)` then later
`Sfz violin = (loadSfz "alt-violin.sfz")`. The second assignment overwrites
the first in `SfzPatchRegistry["violin"]` — `renderSong song "sampler:violin"`
suddenly plays the second patch even if earlier code paths "should" see the
first.
**Why it happens:** Flow variables can be reassigned. The patch registry
mirrors the variable scope but doesn't track shadowing.
**How to avoid:** Document the contract explicitly: "last-bound-wins per
variable name within an ExecutionContext." Add a doc-comment on the
`SfzPatchRegistry` field. This matches Flow's variable semantics; surprising
behavior would be NOT to update.
**Warning signs:** Composer reports a "wrong instrument played" bug.

### Pitfall 11: Header Tokenization with Adjacent Opcodes
**What goes wrong:** A line like `<region> sample=violin.wav lokey=60` has a
header AND an opcode AND another opcode on the same line. A naive
line-by-line parser that handles ONLY one token per line drops the trailing
opcodes.
**Why it happens:** SFZ format allows whitespace-separated tokens per line.
**How to avoid:** After header recognition, continue scanning the rest of the
line for `key=value` tokens. Treat the line as `{ HeaderToken } { Opcode }*`.
The grammar fits a simple state machine.
**Warning signs:** Regions parse with default values instead of authored
opcode values; smoke fixture silently renders silence.

### Pitfall 12: Symbol-Type Argument Resolution in `loadSfz`
**What goes wrong:** `loadSfz(Symbol)` and `loadSfz(String)` overloads must
not be ambiguous on the OverloadResolver. If `SymbolType` accidentally has
`IsCompatibleWith(StringType)` returning true (or vice versa), the dispatch
collapses.
**Why it happens:** Phase 26.1 SYM-01 explicitly separates Symbol from String
(`(equals #foo "foo")` is false). Verify this is preserved.
**How to avoid:** Manually check `SymbolType.IsCompatibleWith` — it must
return true ONLY for `SymbolType`. The OverloadResolver scoring (exact +1000,
compatible +500) breaks ties cleanly when each type is strict.
**Warning signs:** `(loadSfz #violin)` returns the wrong overload, or errors
ambiguously.

## Code Examples

### Example 1: SfzType (new file, ~40 LOC)

```csharp
// Source: mirrors flow-lang/TypeSystem/SpecialTypes/TuningType.cs (VERIFIED)
namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 33 — first-class value type for an SFZ-format sampler patch.
/// Returned by (loadSfz Symbol) / (loadSfz String) builtins; consumed by
/// the "sampler:NAME" instrument-string dispatcher in SongRenderer.
///
/// Specificity 150 — slotted above all existing music types (TuningType=137,
/// SectionType=138, BeatType=139, SongType=140, HertzType=144). Reference
/// identity per CONTEXT Claude's Discretion: two (loadSfz #violin) calls
/// produce distinct Sfz values even with identical resolved paths.
/// </summary>
public sealed class SfzType : FlowType
{
    private SfzType() { }
    public static SfzType Instance { get; } = new();
    public override string Name => "Sfz";
    public override int GetSpecificity() => 150;
    public override bool IsCompatibleWith(FlowType target) => target is SfzType;
    public override bool CanConvertTo(FlowType target) => target is SfzType;
}
```

### Example 2: ExecutionContext patch (4 new fields)

```csharp
// In flow-lang/Runtime/ExecutionContext.cs, after the existing SymbolInternTable
// declaration at line 85 (VERIFIED).

/// <summary>
/// Phase 33 — flips true on `use "@sfz"` import via __enableSfzModule.
/// Gates the loadSfz and sampler:NAME paths. Default false.
/// </summary>
public bool SfzEnabled { get; set; } = false;

/// <summary>
/// Phase 33 — 19-entry GM orchestral Symbol → relative-path map populated
/// from flow-lang/sfz.flow via __enableSfzModule. Read by loadSfz(Symbol).
/// Empty until the module imports.
/// </summary>
public Dictionary<Value, string> SfzInstruments { get; } = new();

/// <summary>
/// Phase 33 — name → patch data registry. Populated by
/// Interpreter.ExecuteVariableDeclaration when a typed Sfz variable is
/// declared (D-12). Read by SongRenderer's sampler:NAME dispatcher.
/// </summary>
public Dictionary<string, FlowLang.StandardLibrary.Audio.Sfz.SfzData>
    SfzPatchRegistry { get; } = new();

/// <summary>
/// Phase 33 — one-shot stderr advisory dedup set. Keyed by
/// "{patch-description}:{advisory-channel}:{detail}". Three channels:
/// "opcode:{name}", "region:{midi}:{vel}", "config:sfz_root_missing".
/// Used by SfzParser and SfzRenderer via RenderingDiagnostics.WarnOnce.
/// </summary>
public HashSet<string> SfzDiagnostics { get; } = new();
```

### Example 3: SongRenderer dispatch branch (single insertion)

```csharp
// Insert into flow-lang/StandardLibrary/Audio/SongRenderer.cs RenderSong
// at line 100, BEFORE the existing FlowEngine.CurrentSampleCache?.EagerLoad call.

public static Value RenderSong(IReadOnlyList<Value> args)
{
    var song = args[0].As<SongData>();
    string synthType = (string)args[1].Data!;

    SynthUtils.ResetNoiseRng();

    // Phase 33 D-13: sampler:NAME dispatch BEFORE the Phase 29 bundled-sample path.
    // Strip prefix, look up in SfzPatchRegistry, render via SfzRenderer.
    if (synthType.StartsWith("sampler:", StringComparison.Ordinal))
    {
        string patchName = synthType.Substring("sampler:".Length);
        // FlowEngine.CurrentExecutionContext exposure: planner decides whether to
        // thread the context here or add a static accessor matching FlowEngine
        // .CurrentSampleCache. The cleanest path is a new
        // FlowEngine.CurrentExecutionContext static property.
        var ctx = FlowEngine.CurrentExecutionContext;
        if (ctx is null || !ctx.SfzPatchRegistry.TryGetValue(patchName, out var patch))
            throw new InvalidOperationException(
                $"Unknown sampler patch '{patchName}'. " +
                $"Known: [{string.Join(", ", ctx?.SfzPatchRegistry.Keys ?? Enumerable.Empty<string>())}]. " +
                $"Did you forget `Sfz {patchName} = (loadSfz #...)`?");

        // Eager-load only the regions this song actually uses.
        FlowEngine.CurrentSfzSampleCache?.EagerLoad(song, patch);

        // ... render via SfzRenderer.Render per section, identical mixing pipeline
    }

    FlowEngine.CurrentSampleCache?.EagerLoad(song, synthType);
    // ... existing Phase 29 path
}
```

### Example 4: Minimal SFZ smoke fixture (< 50 KB)

```sfz
// flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz — synthetic test fixture
// SPEC-7: self-contained, < 100 KB, no external dependencies.

<global>
ampeg_attack=0.005
ampeg_release=0.05

<group>
volume=0
pan=0

<region>
sample=C4_sine.wav
pitch_keycenter=60
lokey=48
hikey=71
lovel=1
hivel=127
loop_mode=loop_continuous
loop_start=2205
loop_end=4410

<region>
sample=G5_sine.wav
pitch_keycenter=79
lokey=72
hikey=127
lovel=1
hivel=127
loop_mode=no_loop
```

The two WAV files are 100ms sine bursts at A=440Hz × 2^((midi-69)/12) — a few
KB each at 44.1 kHz 16-bit mono. A `Phase33SfzSmokeTests.GenerateFixtures()`
unit method can synthesize and commit them; the script lives in
`flow-lang.Tests/Tools/Phase33FixtureGenerator.cs`. Total fixture size target:
< 50 KB.

### Example 5: Composer-facing example

```flow
// examples/symphony/sfz_smoke.flow — 4-bar runnable example for Phase 34 tutorial

use "@audio"
use "@sfz"

tempo 100 {
  key Cmajor {
    Sfz violin = (loadSfz #violin)

    section opening {
      | C4q D4q E4q F4q G4h G4h C5w |
    }

    Song song = [opening]
    Buffer mix = (renderSong song "sampler:violin")
    (writeWav "sfz_smoke.wav" mix)
  }
}
```

## State of the Art

| Old Approach (pre-Phase 33) | New Approach | Trigger | Impact |
|--------------|------------------|--------------|--------|
| Phase 29 bundled samples only (21 WAVs, 6 instruments, 1-5 pitches each) | SFZ libraries (VSCO-CE: ~400 MB, 19+ instruments, multi-velocity, full chromatic) | Composer chooses via `use "@sfz"` | Phase 34 symphony showcase becomes feasible; v1.5 sampled-drums/articulations backlog superseded for any opt-in instrument |
| Single-velocity-per-pitch (most Phase 29 bundles) | Multi-velocity via SFZ `<region> lovel=X hivel=Y` | Composer's SFZ library declares them | Real velocity-driven timbre |
| No sustain looping (Phase 29 buffer pads with zero) | Equal-power 441-frame sin/cos crossfade | `loop_mode=loop_continuous` or `loop_sustain` | Sustained orchestral notes hold cleanly without 1-second cutoff |
| No SFZ format support anywhere in Flow | 13-opcode common subset + 3 headers | Phase 33 ships | Symphony Showcase + 3rd-party CC-licensed orchestral libraries become composer-accessible |

**Deprecated/outdated:** Nothing. Phase 33 is purely additive — Phase 29's
bundled-sample path stays byte-identical and remains the zero-config default
for `renderSong song "piano"`/`"brass"`/etc.

## Assumptions Log

All assumptions in this research that were NOT verified against the codebase
or authoritative documentation. The planner should treat these as candidates
for further validation during planning.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The 19-symbol GM dict's VSCO-CE relative paths (e.g., `"Strings/Violin/violin-Sustain.sfz"`) are accurate filenames in the 1.1.0 SFZ release | §"Locked Decisions D-09" | Composers report "file not found" when running `loadSfz #violin` against a real VSCO-CE install. Mitigation: planner should download VSCO-CE 1.1.0 and verify the 19 paths before locking the dict. The `versilian-studios.com/vsco-community/` SFZ release is the canonical source. |
| A2 | `pitch_keycenter` accepting scientific note names (e.g., `C4`) in addition to MIDI numbers is in scope for Phase 33's 13-opcode subset | §"Pattern 2 quirks" | SFZ libraries authored with `pitch_keycenter=C4` parse to default (60) instead of authored pitch. Mitigation: Phase 33 SPEC-3 says "the 13 listed opcodes" — `pitch_keycenter` accepts integer per the spec; scientific notation is a parser convenience that can be added in a Phase 33.x follow-up. RESEARCH recommendation: parse integer first; advisory + default-to-60 on non-integer is acceptable v1.4 behavior. |
| A3 | SFZ libraries in the wild typically use `loop_start` / `loop_end` values that fit within the loaded sample buffer | §"Pitfall 3" | A malformed library can crash the renderer. Mitigation: clamp at render time per Pitfall 3's prescription. Adds 1-line defensive guard, no real risk. |
| A4 | `FlowEngine.CurrentExecutionContext` static accessor (analogous to `FlowEngine.CurrentSampleCache`) is the cleanest way to thread the ExecutionContext to the static `SongRenderer.RenderSong` | §"Example 3" | If the planner decides to refactor `SongRenderer.RenderSong` to take an ExecutionContext parameter, all callsites change. Mitigation: this is a discretion call — both approaches work; the static accessor is the smaller diff. |
| A5 | A `short[128, 128]` index grid (Pitfall 4's optimization) is unnecessary for Phase 33 — the `SfzRegion?[128, 128]` 144-KB-per-patch shape is fine | §"Locked Decisions D-04" + §"Pitfall 4" | At 12-patch symphony scale, 1.5 MB of grid memory is invisible against ~600 MB of WAV samples. Mitigation: ship `SfzRegion?[,]` now; optimize to short-index if Phase 34 profiling shows it matters. |
| A6 | The CI smoke test's "non-zero RMS" + "discontinuity check" + "RMS > -40 dBFS" thresholds are achievable with a 2-region synthetic sine-burst fixture | §"Example 4" | If the fixture's loop body produces near-zero RMS under the locked envelope (because the 100ms sine bursts are too short for the 4-second sustained note), the test gates would need to drop to RMS > -60 dBFS or the fixture length needs to grow. Mitigation: planner allocates a sub-task to validate fixture RMS empirically before locking the threshold. |
| A7 | SFZ `<control>` header + `default_path=` opcode being out-of-scope is fine for the smoke fixture and the 19 VSCO-CE patches | §"Pattern 2 quirks" | Many SFZ libraries declare `<control> default_path=Samples/` and write relative sample paths. If VSCO-CE uses `<control>` blocks, ignoring it forces our parser to resolve paths relative to the .sfz file itself (which the spec also allows). Mitigation: verify VSCO-CE's actual structure during planning; if `<control>` is common, add it as a Phase 33.x follow-up or extend the 13-opcode subset to 14. |

**If this table is empty:** N/A — 7 items pending user confirmation.

## Open Questions (RESOLVED)

1. **VSCO-CE 1.1.0 actual directory structure + canonical .sfz filenames per instrument**
   - What we know: Top-level dirs are Brass / Keys / Miscellania Raw /
     Percussion / Strings / VSCO 1 Percussion / Woodwinds [CITED:
     github.com/sgossner/VSCO-2-CE/tree/SFZ]. The 1.1.0 SFZ release zip
     contains numerous individual .sfz files per instrument.
   - What's unclear: The exact filename for "violin sustain" — is it
     `Strings/Violin/violin-Sustain.sfz`, `SViolin.sfz`,
     `Strings/Solo Violin/SViolin sus.sfz`, or something else? The web
     searches surface "SViolin variants (with keyswitches, pizzicato,
     spiccato, tremolo, vibrato)" — meaning there are multiple sustain
     variants per instrument.
   - Recommendation: During planning, allocate one task to download VSCO-CE
     1.1.0 SFZ release, extract, and dump the actual filenames for the 19
     GM instruments. Use those as the canonical paths in the shipped Flow
     dict (`flow-lang/sfz.flow`). Without this, A1 is the highest-risk
     assumption.
   - **RESOLVED:** Plan 33-01 Task 1 produces `33-VSCO-PATH-AUDIT.md` via a
     WebFetch probe of `github.com/sgossner/VSCO-2-CE/tree/SFZ`. Plan 33-05
     consumes the audit to populate `flow-lang/sfz.flow`. Rows that cannot be
     verified ship a best-effort path with an inline `Note: TBD per audit row`
     comment; a real-install error message surfaces the unresolvable path.
     **Disposition:** sourced to Plan 33-01 Task 1 + Plan 33-05 Task 1.

2. **`pitch_keycenter` scientific-notation support**
   - What we know: OpenMPT's SFZ implementation accepts both `pitch_keycenter=60`
     and `pitch_keycenter=C4` [CITED: openmpt.org/Manual:_SFZ_Implementation].
     SPEC-3 says "13 listed opcodes" without specifying which value forms.
   - What's unclear: Whether Phase 33 must accept the scientific-notation
     form to load real VSCO-CE patches.
   - Recommendation: Implement integer-only parsing for v1.4; emit an
     "unrecognized opcode value" advisory + fallback to 60 if a non-integer
     is encountered. Add scientific-notation in a Phase 33.x follow-up if
     Phase 34 needs it.
   - **RESOLVED:** Integer-only parsing for v1.4 (Plan 33-04 Task 1's strict-
     numeric posture covers this — `pitch_keycenter=C4` will hit the malformed-
     numeric advisory + spec default 60). Scientific-notation deferred to v1.5
     UNLESS Plan 33-01's VSCO-CE probe surfaces actual VSCO 1.1.0 `.sfz` files
     using `pitch_keycenter=C4` form. **Disposition:** Plan 33-04 (integer-
     only parser); v1.5 backlog (scientific notation if VSCO probe escalates).

3. **`<control>` header + `default_path=` opcode**
   - What we know: Some SFZ libraries use `<control> default_path=Samples/`
     to declare a path prefix for all `sample=` opcodes in the file.
   - What's unclear: Whether VSCO-CE 1.1.0 uses this pattern. If yes,
     ignoring it (Phase 33 SPEC-3 says non-13-opcode opcodes are silently
     ignored) means sample paths resolve relative to the .sfz file directory
     instead of `default_path` — and many sample lookups fail with
     `FileNotFoundError`.
   - Recommendation: Validate against VSCO-CE during planning. If common,
     escalate to a Phase 33.x scope expansion or extend the 13-opcode subset
     to 14 (adding `default_path=`). The opcode itself is trivial; the
     question is whether SPEC-3 needs to be relaxed.
   - **RESOLVED:** Plan 33-01 Task 1's audit is extended to ALSO probe a
     representative VSCO-CE `.sfz` for `<control>` / `default_path=` usage.
     If the audit finds either is in common use, Plan 33-04's opcode whitelist
     extends to 14 (adding `default_path=`) AND parses `<control>` as a fourth
     header that cascades its `default_path=` into every region's `sample=`
     lookup at parse time. If the audit shows VSCO-CE does NOT use the
     pattern, the v1.4 13-opcode subset stands. **Disposition:** Plan 33-01
     Task 1 audit deliverable gates the Plan 33-04 opcode-whitelist count;
     SPEC-3 is conditionally relaxed.

4. **`ExecutionContext` access from `SongRenderer.RenderSong` static method**
   - What we know: `FlowEngine.CurrentSampleCache` is the precedent for
     surfacing per-engine state to a static renderer.
   - What's unclear: Whether to add a parallel `FlowEngine.CurrentExecutionContext`
     static accessor, or thread the context through new overloads. Phase 32
     `tuning t { ... }` blocks took the parameter-threading route (see
     `SongRenderer.ResolveRenderTuning(MusicalContext)`).
   - Recommendation: Static accessor is the smaller diff and matches Phase
     29's pattern. Document the single-engine-per-process precondition
     explicitly (CLAUDE.md memory: pre-public, no concurrent FlowEngine
     support yet).
   - **RESOLVED:** Use `FlowEngine.CurrentExecutionContext` static accessor —
     same shape as Phase 29's `FlowEngine.CurrentSampleCache`. Single-engine-
     per-process precondition is implicit in the pre-public posture
     (CLAUDE.md memory: no concurrent FlowEngine support). **Disposition:**
     Plan 33-07 Task 1 ships `FlowEngine.CurrentExecutionContext` static +
     Dispose cleanup.

5. **Smoke fixture: synthesized vs committed**
   - What we know: SPEC-7 says total fixture < 100 KB. Two 100ms sine WAVs +
     a 1-KB .sfz are < 50 KB.
   - What's unclear: Whether the WAVs should be committed as binary fixtures
     OR synthesized at test-startup by a `[ClassDataAttribute]` generator.
   - Recommendation: Commit them. Determinism contract requires byte-identical
     fixtures across runs; synthesizing at test-startup is one more thing to
     reproduce identically. Commit + `.gitattributes` flag binary +
     `RepoSizeTests` extension to enforce the < 100 KB cap on the new
     directory.
   - **RESOLVED:** Commit binary WAV fixtures + `.sfz` + LICENSE under
     `flow-lang.Tests/fixtures/sfz-smoke/`. Phase33FixtureGenerator (Plan
     33-01 Task 2) is the deterministic-recipe regenerator. `RepoSizeTests`
     enforces < 100 KB cap. **Disposition:** Plan 33-01 Task 2 + Task 3.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build | ✓ (existing) | net10.0 | — |
| C# 13 | Language features | ✓ (existing) | latest | — |
| `dotnet test` (xunit.v3) | Phase 33 tests | ✓ (existing) | xunit.v3 3.2.2 | — |
| `xunit.runner.visualstudio` | Test runner | ✓ (existing) | 3.1.5 | — |
| `Microsoft.NET.Test.Sdk` | Test SDK | ✓ (existing) | 17.13.0 | — |
| Tomlyn | TOML config | ✓ (existing) | bundled with flow-cli | — |
| PulseAudio | Playback only (NOT required for `writeWav`) | system-dependent | — | Tests use `writeWav`, not playback |
| VSCO-CE 1.1.0 SFZ release | Manual UAT | composer-supplied | 1.1.0 | NOT a CI dependency — synthetic fixture covers CI |
| `slopcheck` | Package legitimacy | N/A | — | No new packages this phase |

**Missing dependencies with no fallback:** None — phase ships with zero new
dependencies.

**Missing dependencies with fallback:** VSCO-CE is composer-supplied; CI uses
the synthetic smoke fixture (SPEC-7).

## Validation Architecture

> `workflow.nyquist_validation` is not explicitly disabled in
> `.planning/config.json`; treating as enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 (verified at `flow-lang.Tests/flow-lang.Tests.csproj:13`) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase33" --logger "console;verbosity=minimal"` |
| Full suite command | `dotnet test flow-sharp.sln --logger "console;verbosity=minimal"` |
| Phase gate | `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"` exits 0 |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| SPEC-1 | `loadSfz` undefined without `use "@sfz"` | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzGatingTests.LoadSfz_WithoutImport_Errors"` | ❌ Wave 0 |
| SPEC-1 | `sampler:NAME` errors without `use "@sfz"` | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzGatingTests.SamplerDispatch_WithoutImport_Errors"` | ❌ Wave 0 |
| SPEC-2 | `loadSfz #violin` resolves dict + sfz_root | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzSymbolLookupTests"` | ❌ Wave 0 |
| SPEC-2 | `loadSfz #unknownSymbol` errors with 19-symbol list | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzSymbolLookupTests.UnknownSymbol_Errors"` | ❌ Wave 0 |
| SPEC-2 | Missing `sfz_root` config errors with config-path pointer | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzConfigTests.MissingRoot_Errors"` | ❌ Wave 0 |
| SPEC-3 | Parser accepts 13 known opcodes | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzParserTests.AllKnownOpcodes_Parse"` | ❌ Wave 0 |
| SPEC-3 | Parser silently ignores unknown opcodes + one-shot advisory | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzParserTests.UnknownOpcode_AdvisoryOnce"` | ❌ Wave 0 |
| SPEC-3 | Header inheritance (`<global>`/`<group>`/`<region>`) flattens at parse time | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzParserTests.HeaderInheritance"` | ❌ Wave 0 |
| SPEC-3 | Strict numeric (rejects `1.5e2`, `100,5`) | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzParserTests.StrictNumeric"` | ❌ Wave 0 |
| SPEC-4 | 2-region overlap routes (pitch, vel) correctly | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzRegionMatchTests.TwoRegionOverlap"` | ❌ Wave 0 |
| SPEC-4 | Nearest-pitch fallback varispeed-shifts | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzRegionMatchTests.NearestPitchFallback_SpectralFingerprint"` | ❌ Wave 0 (synthesizable inputs — generated by test) |
| SPEC-4 | Velocity overlap: vel 0..63 vs 64..127 splits correctly | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzRegionMatchTests.VelocityOverlap"` | ❌ Wave 0 |
| SPEC-5 | 4-sec sustained `C4w` with crossfade: no per-sample jump > 0.05 | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzLoopCrossfadeTests.DiscontinuityCheck"` | ❌ Wave 0 (synthetic 2-region fixture, generated by test) |
| SPEC-5 | Equal-power vs linear: spectral centroid within ±2% of baseline | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzLoopCrossfadeTests.EqualPowerSpectralCheck"` | ❌ Wave 0 |
| SPEC-6 | `Sfz violin = (loadSfz ...)` + `renderSong song "sampler:violin"` → non-zero RMS | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzBindingTests.Render_NonEmpty"` | ❌ Wave 0 (uses smoke fixture) |
| SPEC-6 | `"sampler:doesnotexist"` errors with unknown-name message + known-names list | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzBindingTests.UnknownName_Errors"` | ❌ Wave 0 |
| SPEC-7 | CI smoke renders fixture; non-empty WAV; RMS > -40 dBFS; discontinuity check | integration | `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"` | ❌ Wave 0 (fixture at `fixtures/sfz-smoke/`) |
| SPEC-8 | 6 articulations on a sampler-rendered C4q produce 6 distinct buffers; ±5% audible duration match | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzArticulationTests.SixArticulations"` | ❌ Wave 0 |
| SPEC-8 | `ampeg_attack=0.5` produces > 200 ms attack vs `ampeg_attack=0.005` | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzArticulationTests.AmpegAttackOverride"` | ❌ Wave 0 |
| det. | Two-run byte-identical determinism on smoke fixture | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzDeterminismTests.TwoRun_CmpClean"` | ❌ Wave 0 |
| reg. | Existing `renderSong song "piano"` byte-identical pre/post Phase 33 | regression | `dotnet test --filter "FullyQualifiedName~Phase29.RmsBaselineTests"` (existing) | ✅ exists |
| size | Phase 33 in-repo artifacts < 100 KB | integration | `dotnet test --filter "FullyQualifiedName~Phase33.RepoSizeTests"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase33" --logger "console;verbosity=minimal"`
  (Phase 33 tests only — typically < 30 seconds)
- **Per wave merge:** `dotnet test flow-sharp.sln --logger "console;verbosity=minimal"`
  (full Flow test suite — guards against Phase 29 byte-identical regression)
- **Phase gate:** Full suite green + `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"` green before `/gsd:verify-work`

### Wave 0 Gaps

All Phase 33 test files are gaps. Wave 0 should establish:

- [ ] `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` — SPEC-7 smoke
      test; renders the smoke fixture, asserts non-empty + RMS + discontinuity
- [ ] `flow-lang.Tests/Integration/Phase33/SfzGatingTests.cs` — SPEC-1 import
      gating tests
- [ ] `flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs` — SPEC-2
      symbol resolution + 19-symbol list error
- [ ] `flow-lang.Tests/Integration/Phase33/SfzConfigTests.cs` — SPEC-2
      missing-config-key error
- [ ] `flow-lang.Tests/Unit/Phase33/SfzParserTests.cs` — SPEC-3 opcode
      whitelist + advisory dedup + strict numeric + header inheritance
- [ ] `flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs` — SPEC-4 grid
      lookup + nearest-pitch fallback (spectral fingerprint helper from
      Phase 29 `Phase29Fft.cs`)
- [ ] `flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs` — SPEC-5
      per-sample discontinuity check + equal-power spectral check
- [ ] `flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs` — SPEC-6
      typed-variable binding + sampler dispatch + unknown-name error
- [ ] `flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs` — SPEC-8
      6-articulation distinctness + ampeg_attack override
- [ ] `flow-lang.Tests/Integration/Phase33/SfzDeterminismTests.cs` — two-run
      byte-identical contract
- [ ] `flow-lang.Tests/Integration/Phase33/RepoSizeTests.cs` — < 100 KB cap
      on `fixtures/sfz-smoke/`
- [ ] `flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz` — 2-region synthetic
      fixture
- [ ] `flow-lang.Tests/fixtures/sfz-smoke/C4_sine.wav` + `G5_sine.wav` —
      synthetic sine bursts, committed
- [ ] `flow-lang.Tests/Tools/Phase33FixtureGenerator.cs` — helper that
      regenerates the fixture WAVs from a known seed (for future spec
      revisions)
- [ ] No framework install needed — xunit.v3 is already wired.

## Security Domain

`security_enforcement` is not explicitly disabled. Including this section.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | no | N/A — no user authentication |
| V3 Session Management | no | N/A — no sessions |
| V4 Access Control | no | N/A — interpreter is single-user local |
| V5 Input Validation | yes | Hand-rolled SfzParser with strict numeric guards (NumberStyles mask) + 10000-region DoS cap (T-33-PARSE-01); file-path canonicalization via `Path.GetFullPath` before `File.ReadAllText`; reject paths containing `..` segments OUTSIDE the configured `sfz_root` |
| V6 Cryptography | no | N/A — no crypto |
| V12 File and Resource | yes | `File.ReadAllText` posture matches Flow's existing `writeWav` / `writeMidi` / `loadScala` IO; threat T-33-IO-01 acknowledged and accepted at Phase 32 precedent |

### Known Threat Patterns for the SFZ Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed SFZ → unbounded region count → memory exhaustion | Denial of Service | `MaxRegionCount = 10000` constant + early-throw `SfzParseException` (mirrors Phase 32 `MaxStepCount` at `ScalaParser.cs:48`) |
| Malformed `loop_end > sampleLength` → IndexOutOfRangeException | Denial of Service | Clamp at render time per Pitfall 3 (`Math.Min(loop_end, source.Length - 1)`) |
| Malformed numeric (`1.5e2`, `100,5`) → parser confusion | Tampering | `NumberStyles.Float & ~AllowExponent & ~AllowThousands` + `CultureInfo.InvariantCulture` (Phase 32 D-18 precedent) |
| SFZ with absolute sample path pointing outside `sfz_root` (e.g., `sample=/etc/passwd`) | Information Disclosure / Tampering | The sample is loaded as WAV — non-WAV files fail with `InvalidDataException`. As a defense-in-depth, the SfzParser could reject absolute paths in `sample=` opcodes when the .sfz was resolved through the symbol dict (i.e., `sfz_root`-scoped loads). For absolute-path-resolved .sfz files, the composer accepts responsibility — same posture as `writeWav("/etc/foo")`. Mitigation: document the contract; planner may add an optional flag for strict path-containment. |
| Unicode opcode names trying to slip past whitelist | Tampering | `KnownOpcodes` `HashSet<string>` with `StringComparer.Ordinal` rejects case mismatch and unicode tricks |
| Race condition on `ExecutionContext.SfzDiagnostics` HashSet during concurrent renders | Tampering | `RenderingDiagnostics.WarnOnce` already lock-protects its set; reuse the same pattern for the per-context set if concurrent FlowEngines are ever supported |

## Sources

### Primary (HIGH confidence)

- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` — read in full
- `flow-lang/StandardLibrary/Audio/SampleCache.cs` — read in full
- `flow-lang/StandardLibrary/Audio/FileIO.cs:280-380` — varispeed primitives
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs:1-260` — dispatch site
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — INoteSynthesizer interface + SynthesizerFactory
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:40-75` — GM program dict
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs:1-250` — strict-numeric parser pattern
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` — builtin registration template
- `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` — first-class music-type template
- `flow-lang/Runtime/ExecutionContext.cs:1-150` — context field surface
- `flow-lang/Runtime/MusicalContext.cs` — TuningStack pattern (informs flat-registry decision)
- `flow-lang/Runtime/FlowConfig.cs` — POCO + Active singleton
- `flow-cli/Config/FlowConfigLoader.cs` — TOML deserialization site
- `flow-lang/Runtime/Value.cs:22-65` — Value factory methods
- `flow-lang/Interpreter/Interpreter.cs:588-647` — ExecuteVariableDeclaration site
- `flow-lang/Diagnostics/RenderingDiagnostics.cs` — WarnOnce
- `flow-lang/Core/FlowEngine.cs` — engine startup + cache lifecycle
- `flow-lang/audio.flow` — stdlib module shape template
- `.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md` — 8 locked requirements
- `.planning/phases/33-sfz-orchestral-sampler/33-CONTEXT.md` — D-01..D-20 + Claude's Discretion
- `.planning/phases/33-sfz-orchestral-sampler/33-DISCUSSION-LOG.md` — alternatives audit trail
- `.planning/ROADMAP.md` §"Phase 33" + §"Phase 34" entries
- `CLAUDE.md` (read at session start) — Conventions, Minimal Dependencies, music types

### Secondary (MEDIUM confidence — verified with official source)

- [SFZ Format opcodes reference](https://sfzformat.com/opcodes/) — opcode types, defaults, ranges
- [SFZ Format loop_mode opcode](https://sfzformat.com/opcodes/loop_mode/) — `no_loop`/`one_shot`/`loop_continuous`/`loop_sustain` semantics
- [SFZ Format ampeg_attack opcode](https://sfzformat.com/opcodes/ampeg_attack/) — seconds, default 0
- [SFZ Format ampeg_release opcode](https://sfzformat.com/opcodes/ampeg_release/) — spec default 0.001s; ARIA uses 0.03s
- [SFZ Format Envelope Generators](https://sfzformat.com/modulations/envelope_generators/) — Delay-Attack-Hold-Decay-Sustain-Release segment naming
- [SFZ Basic File tutorial](https://sfzformat.com/tutorials/basic_sfz_file/) — file syntax, `<control>`/`<global>`/`<group>`/`<region>` headers, `//` comments, `default_path=`
- [OpenMPT SFZ Implementation Manual](https://wiki.openmpt.org/Manual:_SFZ_Implementation) — `pitch_keycenter` accepts MIDI number OR scientific notation; `volume` -144 to +6 dB; `pan` -100 to +100
- [VSCO 2 Community Edition Versilian Studios](https://versilian-studios.com/vsco-community/) — CC0 license, 3 GB free download
- [VSCO-2-CE GitHub repo](https://github.com/sgossner/VSCO-2-CE) — top-level dir structure (Brass/Strings/Woodwinds/Percussion/Keys/Miscellania Raw/VSCO 1 Percussion)
- [VSCO-2-CE 1.1.0 SFZ release](https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0) — confirmed SFZ release exists; dropbox mirror available
- [VCSL repo (CC0)](https://github.com/sgossner/VCSL) — successor library, sample WAVs only, SFZ via releases

### Tertiary (LOW confidence — flagged in Assumptions Log)

- Exact 19 VSCO-CE relative paths for the GM symbol dict — Assumption A1; needs manual verification against the actual 1.1.0 SFZ zip during planning
- `pitch_keycenter` scientific-notation acceptance in real VSCO-CE patches — Assumption A2; depends on how the patches were authored
- `<control> default_path=` prevalence in VSCO-CE — Assumption A7; depends on authored convention

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every reusable component verified by direct code read
- Architecture: HIGH — every pattern has a Phase 22/28/29/30/32 precedent in the repository
- Pitfalls: MEDIUM-HIGH — the 12 pitfalls draw from direct code-read of Phase 29 + Phase 32 plus SFZ-format quirks pulled from sfzformat.com (verified)
- Code examples: HIGH for shape (verified against repo); MEDIUM for exact LOC counts
- VSCO-CE specifics: LOW (Assumption A1 + A2 + A7) — needs manual verification during planning

**Research date:** 2026-05-15
**Valid until:** Stable until Phase 33 ships — the underlying repo patterns are
locked v1.4 surface. SFZ-format references are stable (the spec hasn't moved
in years). VSCO-CE 1.1.0 is a frozen release. The only volatile data is the
exact 19-entry dict, which the planner pins manually during Plan 33-01.
