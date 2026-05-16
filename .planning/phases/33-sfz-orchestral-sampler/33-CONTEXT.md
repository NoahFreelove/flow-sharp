# Phase 33: SFZ Orchestral Sampler - Context

**Gathered:** 2026-05-15
**Status:** Ready for planning
**Source:** PRD Express Path (33-SPEC.md)

<domain>
## Phase Boundary

Phase 33 ships a real SFZ-format orchestral sampler that runs alongside (not on top of) Phase 29's bundled-sample infrastructure. The surface is gated behind `use "@sfz"` so the heavy external-library code path is opt-in.

The composer surface — locked in 33-SPEC.md:

- `use "@sfz";` activates the surface (without it, `loadSfz` and `sampler:NAME` are undefined).
- `(loadSfz #violin)` parses the .sfz file via a shipped 19-symbol GM-orchestral dict joined to a `sfz_root` key in `~/.config/flow/config.toml` (Phase 30 FlowConfig). `(loadSfz "/abs/path.sfz")` bypasses the dict.
- `Sfz violin = (loadSfz #violin)` binds the patch; `renderSong song "sampler:violin"` dispatches through `SfzRenderer`.
- Phase 28 articulation envelope applies on top of the SFZ render (identical baseline ADSR to `SampledInstrumentRenderer`).
- 13 common-subset SFZ opcodes + `<region>`/`<group>`/`<global>` headers; unknown opcodes silently ignored with one-shot stderr advisory per `(patch, opcode-name)`.
- Equal-power 441-frame loop crossfade prevents the click failure mode that would invalidate Phase 34's symphony showcase.
- Phase 29's bundled-sample path (`renderSong song "piano"` etc.) stays byte-identical — SFZ is purely additive.

Blessed external library: VSCO Community CE (CC-BY 4.0). External download only — nothing > 100 KB ships in-repo for SFZ purposes; CI uses a synthetic smoke fixture.

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**8 requirements are locked.** See `33-SPEC.md` for full requirements, boundaries, and acceptance criteria.

Downstream agents MUST read `33-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- New `flow-lang/sfz.flow` stdlib module gating the SFZ surface
- New `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` for the 13-opcode + 3-header common subset
- New `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` implementing `INoteSynthesizer` (region matching, varispeed fallback, sustain loop with equal-power crossfade)
- New `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` first-class value type (mirrors Phase 32 `TuningType` shape)
- New `(loadSfz Symbol)` + `(loadSfz String)` builtin overloads
- New `sfz_root` key in flow config (`~/.config/flow/config.toml`) wired through Phase 30 `FlowConfig.Load()`
- Shipped frozen `Dict<Symbol, String>` mapping 19 GM orchestral symbols to VSCO-CE relative paths
- New `sampler:NAME` instrument-string dispatcher in `SongRenderer`
- `ExecutionContext.SfzPatchRegistry` for typed-Sfz variable binding
- CI smoke test with self-contained synthetic SFZ + WAV fixtures (< 100 KB)
- Composer-facing docs at `examples/symphony/README.md` + a 4-bar `examples/symphony/sfz_smoke.flow` runnable example
- Phase 28 articulation envelope hook on top of SFZ render output
- One-shot stderr advisories for unrecognized opcodes, missing regions, missing `sfz_root` config

**Out of scope (from SPEC.md):**
- Full SFZ v2 spec — opcodes outside the 13 listed silently ignored
- `writeSfz` / SFZ export (read-only)
- Real-time SFZ editing / hot reload
- Bundled orchestral library in-repo
- Retrofitting Phase 29's `SampledInstrumentRenderer` to consume SFZ underneath
- Anonymous `Sfz` value flow without intermediate binding
- More than 19 instrument symbols in the shipped dict
- Adding SFZ-specific timbre to MIDI export (MIDI follows GM-program prefix-strip per Phase 28)

</spec_lock>

<decisions>
## Implementation Decisions

Decisions captured in this discussion sit on top of 33-SPEC.md (which locks WHAT). These cover HOW the planner should structure the build.

### Region storage + lookup shape

- **D-01 [grid-128x128]:** Each parsed `SfzData` carries a precomputed `SfzRegion?[128, 128]` grid keyed by `(midiPitch, midiVelocity)`. The cell value is the winning region for that `(pitch, velocity)` pair under SFZ last-declared-wins semantics. Grid is built at `loadSfz` time, immutable thereafter.
- **D-02 [build-encodes-spec]:** The build loop iterates regions in declaration order and assigns `grid[k, v] = region` for every `(k ∈ [lokey..hikey], v ∈ [lovel..hivel])` cell the region covers. Later regions overwrite earlier ones — the SFZ spec rule becomes structurally enforced rather than implicit in lookup logic. Lookup is `Grid[midi, vel]`, no scanning, no branches.
- **D-03 [side-data-for-fallback]:** Alongside the grid, store `SortedByPitch: int[]` — the sorted union of all (lokey..hikey) cells that have any region coverage. SPEC REQ-4's nearest-pitch fallback (when `Grid[midi, vel] == null`, find the closest covered pitch and varispeed-shift) uses this index. ~512 bytes per patch.
- **D-04 [memory-cost-bounded]:** ~144 KB per patch (13 KB regions + 131 KB grid as 4-byte indices, or 16-bit indices if region count < 65536 to halve it). For a 6-patch symphony: under 1 MB total — about 1% of the audio sample data that the same patches load.

### Sample loading strategy

- **D-05 [parse-vs-load-split]:** `loadSfz` does parsing + region-grid build ONLY. Zero `.wav` files hit disk during `loadSfz`. Returns an `Sfz` value carrying region metadata + sample-file-path strings (not loaded samples).
- **D-06 [eager-on-renderSong-walk]:** When `renderSong song "sampler:NAME"` runs, walk the song's note set first, dereference `Grid[k, v]` for each unique `(pitch, velocity)` cell, collect the set of distinct regions actually needed, then eager-load only those regions' `.wav` files into a new `SfzSampleCache`. Mirrors Phase 29 `SampleCache` D-13/14/15 pattern (`flow-lang/StandardLibrary/Audio/SampleCache.cs`).
- **D-07 [cache-lifetime-per-flow-engine]:** `SfzSampleCache` lives on `FlowEngine` (parallel to Phase 29's `SampleCache`). Lifetime = engine disposal. Re-rendering the same song hits the cache; rendering a different song with the same patch reuses already-loaded regions, eager-loads only newly-needed ones.
- **D-08 [no-lazy-no-stutter]:** Lazy-on-first-use is explicitly rejected (Phase 29 D-14 ruled it out for the same reason — mid-render disk IO causes stutter). Phase 33 follows the same precedent.

### Stdlib `@sfz` module shape + binding registry

- **D-09 [hybrid-module-shape]:** `flow-lang/sfz.flow` is a normal Flow stdlib file (loaded via `use "@sfz"`). It runs a `(dict #violin "Strings/Violin/violin-Sustain.sfz" ...)` constructor binding the 19-symbol GM map to a known variable `__sfzInstruments`, then calls a side-effecting marker builtin `(__enableSfzModule __sfzInstruments)` that flips `ExecutionContext.SfzEnabled = true` and registers the lookup dict.
- **D-10 [c-sharp-always-registered]:** The `loadSfz(Symbol)` and `loadSfz(String)` builtins are registered unconditionally at `FlowEngine` startup (no special parser handling for `use "@sfz"`). On call, the builtin checks `ExecutionContext.SfzEnabled`; if false, throws `UndefinedFunctionError("loadSfz requires 'use \"@sfz\"'")`. Same gating shape for the `sampler:NAME` dispatch in `SongRenderer`.
- **D-11 [dict-in-flow-not-csharp]:** The 19-entry GM symbol→relative-path mapping lives in the Flow file, NOT in C#. Composers can read the dict from their own Flow code if they want to inspect or extend it. Edits land in `sfz.flow` without a C# rebuild.
- **D-12 [binding-registry-on-execution-context]:** `ExecutionContext.SfzPatchRegistry: Dictionary<string, SfzData>` is the canonical name→patch lookup. Populated by `Interpreter.ExecuteVariableDeclaration` when the declared type is `SfzType` — the assignment handler writes `(name, sfzValue.As<SfzData>())` into the registry alongside the normal `CurrentFrame.SetVariable` call.
- **D-13 [sampler-dispatch-reads-registry]:** `SongRenderer` recognizes `instrument.StartsWith("sampler:")` and strips the prefix to get the bound name; reads `ExecutionContext.SfzPatchRegistry[name]`; on miss, throws `UnknownSamplerNameError(name, knownNames=registry.Keys)`. Mirrors Phase 32's TuningStack 'state lives on ExecutionContext' pattern.
- **D-14 [no-cross-frame-lookup]:** The registry is a flat name→patch map keyed by variable name. Cross-frame lookups (proc-local Sfz used in a sibling renderSong call) are out of scope — the user's discussion confirmed `Sfz` bindings in Phase 33 are top-level / file-scope. Anonymous `(loadSfz #violin) -> renderSong "..."` flow is explicitly deferred per SPEC out-of-scope.

### MIDI export for sampler instruments

- **D-15 [prefix-strip-into-gm-dict]:** `MidiExport` strips `sampler:` from the instrument string and looks up the remaining name in the existing GM-program dict. Result: MIDI export of a symphony works without VSCO-CE installed on the receiver — they get a GM-compatible file with sensible instrument programs.
- **D-16 [gm-dict-12-new-entries]:** Add 12 new entries to the GM-program dict for sampler symbols not already covered by Phase 28: violin→40, viola→41, cello→42, contrabass→43, oboe→68, clarinet→71, bassoon→70, horn→60, trombone→57, tuba→58, timpani→47, choir→52, harp→46, guitar→24, harpsichord→6, celeste→8. Phase 28's existing entries (brass→56, sax→65, flute→73, strings→48, organ→19, bell→14, piano→0, drums→9-channel) are preserved. Final fallback: program 0 (piano) for unknown names.
- **D-17 [track-naming]:** MIDI track-name meta-event uses the sampler-stripped name (e.g. `"violin"`, not `"sampler:violin"`). Receivers see the musical intent, not the Flow-internal dispatch tag.

### Loop crossfade implementation site

- **D-18 [per-note-render-time]:** The 441-frame equal-power sin/cos crossfade math runs inside `SfzRenderer.Render(note, ...)`. For each frame the renderer maps the output index to a frame within `[loop_start..loop_end]`; if the source frame is within the last 441 samples of the loop, blends with the corresponding offset from `loop_start` using `cos(πt/2N) * a + sin(πt/2N) * b`. ~30 LOC inside SfzRenderer.
- **D-19 [no-pre-computed-loop-cache]:** Pre-computing a 5-second looped buffer per region at sample-load time is rejected. Cost: ~880 KB per region × 50 regions per patch = ~44 MB extra cache per patch, on top of the ~50 MB raw sample data the same patch already loads. The per-note crossfade math is ~30 ns per frame for 441 frames per loop transition — well under 1% of render time. Memory cost dominates the speed savings.
- **D-20 [crossfade-exercised-by-smoke-fixture]:** The Phase 33 CI smoke fixture (`flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz`, shipped by Plan 33-01) is a 2-region SFZ — one region covering MIDI 48..71 (centered on C4) with `loop_mode=loop_continuous` + `loop_start=2205` + `loop_end=4410` (within a 4410-frame / 100 ms WAV body — the loop covers the second half of the buffer) exercising the crossfade path, and a second region covering MIDI 72..127 (centered on G5) for the nearest-pitch fallback. Plan 33-06's SfzLoopCrossfadeTests renders a 4-second sustained note through the C4 region and runs the per-sample discontinuity check from SPEC acceptance criterion #8 (the renderer extends the looped body across the authored duration by repeatedly traversing `loop_start..loop_end` with the equal-power crossfade at each boundary; SfzLoopCrossfadeTests' deeper synthetic test uses a 1-second WAV with `loop_start=22050`/`loop_end=44100` to validate the same algorithm at a larger frame count). The smoke fixture is intentionally richer than a 1-region simple-sustain SFZ — it serves both the crossfade fact AND the region-lookup-grid facts from a single artifact while staying under the SPEC-7 < 100 KB fixture budget.

### Claude's Discretion

Decisions that follow from the above without separate user input. Planner may refine if research surfaces a better shape.

- **Error class hierarchy.** `SfzParseException` extends the existing `flow-lang/Parsing/TypeParser.cs` `ParseException`. `UnknownInstrumentSymbolError`, `UnknownSamplerNameError`, `MissingSfzRootError`, `SfzFileNotFoundError` each extend the appropriate Flow base (`InvalidOperationException` for runtime, `ParseException` for parse-time). Reuses Flow's established `{file}:{line}:{col} — expected X got 'Y'` format where applicable.
- **Advisory dedup state location.** `ExecutionContext.SfzDiagnostics: HashSet<string>` holds the `(patch-description, opcode-name)` / `(patch, missing-region-key)` / `(advisory-key)` set already advised. `RenderingDiagnostics.WarnOnce(key, message)` (Phase 32 pattern at `flow-lang/StandardLibrary/Audio/Tuning/`) extends with an SFZ-specific overload that reads/writes this set.
- **Determinism preservation.** Sample-load order during the renderSong-walk eager phase is determined by walking the song in declaration order and the region grid in `(pitch ascending, velocity ascending)` order — deterministic across runs. Mirrors Phase 29 D-31 / `SampleCache.EagerLoad`'s `OrderBy(p => p)` + `OrderBy(v => v, StringComparer.Ordinal)` pattern.
- **`SfzType` shape.** Sealed singleton extending `FlowType`; `IsCompatibleWith(SfzType.Instance)` strict (no numeric coercion); `GetSpecificity()` returns a unique number ≥ 150 (above existing music types). Reference identity; `(equals sfzA sfzB)` is true iff they were produced by the same `(loadSfz ...)` call. Mirrors Phase 32 `TuningType` exactly.
- **`Sfz` value internal model.** `SfzData` (immutable) holds: `Description: string` (first non-comment line of the .sfz, or filename if none), `BasePath: string` (directory of the .sfz file — used to resolve relative sample paths), `Regions: List<SfzRegion>` (parsed regions in declaration order — kept for diagnostics + the fallback nearest-pitch sort index), `Grid: SfzRegion?[128, 128]` (the lookup index), `SortedByPitch: int[]` (the nearest-pitch fallback side data).
- **`SfzRegion` field set.** All 13 opcode values + the 3 header levels' inherited defaults flattened per-region: `SamplePath: string`, `PitchKeycenter: int`, `LoKey: int`, `HiKey: int`, `LoVel: int`, `HiVel: int`, `LoopMode: SfzLoopMode` (enum: NoLoop, OneShot, LoopContinuous, LoopSustain), `LoopStart: int`, `LoopEnd: int`, `AmpegAttack: double`, `AmpegRelease: double`, `Volume: double`, `Pan: double`.
- **Header inheritance.** `<global>` defaults cascade into every `<group>`; `<group>` defaults cascade into every `<region>` declared under it. Inheritance is applied AT PARSE TIME by flattening — runtime never traverses headers.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 33 SPEC + this CONTEXT
- `.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md` — **LOCKED REQUIREMENTS — MUST read before planning.** 8 falsifiable requirements + boundaries + acceptance criteria.
- `.planning/phases/33-sfz-orchestral-sampler/33-CONTEXT.md` — this file. Implementation decisions D-01..D-20 + Claude's-Discretion items.

### Roadmap + Project
- `.planning/ROADMAP.md` §"Phase 33: SFZ Orchestral Sampler" — original phase entry with 6 success criteria.
- `.planning/ROADMAP.md` §"Phase 34: Symphony Showcase" — downstream consumer of Phase 33; the symphony will load VSCO-CE via the surface this phase ships.
- `.planning/REQUIREMENTS.md` — project-wide requirements anchor.

### Prior phase artifacts that directly inform the build
- `.planning/phases/29-instrument-realism/29-CONTEXT.md` §"Sample Loading (REQ-4)" — D-13/14/15 SampleCache pattern Phase 33 mirrors via `SfzSampleCache`.
- `.planning/phases/29-instrument-realism/29-SPEC.md` — eager-load determinism contract Phase 33 must preserve (D-31).
- `flow-lang/StandardLibrary/Audio/SampleCache.cs` — Phase 29 cache class; the `SfzSampleCache` shape parallels this exactly.
- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` — Phase 29 sample-based render path; the Phase 28 articulation envelope hook (lines 120-130) is the pattern `SfzRenderer.Render` will copy.
- `.planning/phases/32-full-scala-scl-tuning-loader/32-CONTEXT.md` §"Tuning context stacking" — TuningStack pattern Phase 33 mirrors via `SfzPatchRegistry`. Also §D-15 — composer-surface forms; Phase 33's 2-form `loadSfz #symbol` / `loadSfz "path"` is a deliberate subset.
- `.planning/phases/32-full-scala-scl-tuning-loader/32-CONTEXT.md` §"Research-surfaced decisions" D-18 — parser strictness precedent (`CultureInfo.InvariantCulture`, no scientific notation).
- `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` — Phase 32's strict-reference-identity music-type pattern; `SfzType.cs` follows this shape exactly.
- `.planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-CONTEXT.md` §"MIDI export" — Phase 28 GM-program dict that Phase 33 extends with 12+ new entries.

### Phase 30 config integration
- `flow-cli/Config/FlowConfigLoader.cs` — XDG path resolution at `~/.config/flow/config.toml`; `FlowConfig.Active` singleton readable from anywhere in the engine. Phase 33 adds a `sfz_root: string?` field to `FlowConfigPoco`.
- `flow-cli/Config/FlowConfigPoco.cs` — TOML POCO. The new `sfz_root` key follows the existing `install_path` / `default_tempo` / `default_timesig` / `default_audio_device` / `stdlib_search_path` shape.

### Phase 22 + Phase 21 varispeed + pragma plumbing
- `flow-lang/StandardLibrary/Audio/FileIO.cs:290-355` — `LoadWavInternal` + `VarispeedResample(buffer, ratio)`. Phase 33 reuses `VarispeedResample` verbatim for SPEC REQ-4 nearest-pitch fallback. NO duplication.
- `flow-lang/Parsing/Parser.cs` (pragma parser) + `flow-lang/Runtime/ModuleLoader.cs` (`use "@name"` resolver) — the module-import + side-effect-on-load shape Phase 33's `@sfz` follows.

### CLAUDE.md anchors (project-wide conventions)
- `CLAUDE.md` §"Music Types Quick Reference" — the table Phase 33 extends with an `Sfz` row.
- `CLAUDE.md` §"Conventions" — pre-Phase-28 byte-identical determinism dropped; RMS-windowed regression baselines under `flow-lang.Tests/baselines/`. Phase 33 fixtures land at `flow-lang.Tests/fixtures/sfz-smoke/` per SPEC REQ-7.
- `CLAUDE.md` §"Guiding Principle: Minimal Dependencies" — no new NuGet packages; SFZ parser is hand-rolled C#.

### External SFZ format reference (for researcher only — not read by planner)
- SFZ Format Reference: https://sfzformat.com/opcodes/ — opcode definitions for the 13-entry common subset.
- VSCO Community CE: https://github.com/sgossner/VCSL or its successor — the blessed orchestral library Phase 34 will consume.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`FileIO.VarispeedResample(buffer, ratio)`** — Phase 22 DX-15 linear-interpolation varispeed at `flow-lang/StandardLibrary/Audio/FileIO.cs:338`. Phase 33's nearest-pitch fallback (SPEC REQ-4) passes `Math.Pow(2.0, semitonesShift / 12.0)` as the ratio. Zero new resample code.
- **`FileIO.LoadWavInternal(path)`** — `flow-lang/StandardLibrary/Audio/FileIO.cs:362`. Reads 16/24/32-bit PCM, resamples to 44.1 kHz. Phase 33 `SfzSampleCache.EagerLoad` calls this for each needed region's sample path.
- **`SampleCache.TrimLeadingSilence(buffer)` pattern** — `flow-lang/StandardLibrary/Audio/SampleCache.cs:190` (internal). Onset-aligns multi-velocity samples. SFZ samples are typically engineer-trimmed already; Phase 33 may or may not need this — researcher decides based on a couple of VSCO fixture probes.
- **`SynthUtils.GenerateArticulationADSR(...)` + `SynthUtils.ApplyEnvelope(...)`** — Phase 28 envelope helper. `SfzRenderer.Render` calls these with `baseAttack=0.005, baseDecay=0.05, baseSustain=1.0, baseRelease=0.05` (same baseline as `SampledInstrumentRenderer`, `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:126-130`).
- **`RenderingDiagnostics.WarnOnce(key, message)`** — Phase 23/32 advisory dedup pattern at `flow-lang/StandardLibrary/Audio/Tuning/RenderingDiagnostics.cs`. Phase 33 reuses for the 3 advisory channels (unknown opcodes, missing regions, missing `sfz_root`).
- **`FlowConfig.Active` singleton** — Phase 30 config access. New `sfz_root` field plugs into `FlowConfigPoco` + auto-deserialized.

### Established Patterns

- **Per-FlowEngine sample cache** — Phase 29 D-13/14/15 ate the design space. `SfzSampleCache` is a new class with the same lifetime + idempotent-eager-load shape.
- **Stdlib import as side-effecting trigger** — `audio.flow` forward-declarations (Phase 26.2 ERG-05) prove `use "@name"` can run statements that register C# state. `@sfz` reuses this — no parser changes.
- **First-class music value type, strict, reference identity** — Phase 32's `TuningType` is the template for `SfzType`. No numeric coercion; equals-by-reference; specificity above existing music types.
- **Musical-context stack-of-N pattern on ExecutionContext** — Phase 32 D-12 added `TuningStack` to MusicalContext. Phase 33's `SfzPatchRegistry` (flat dict) is the simpler analog — no nesting needed because patches don't push/pop with context blocks.
- **MIDI export GM-program prefix-strip** — Phase 28 already routes `piano`/`brass`/etc. through a name→program dict. Phase 33 adds `sampler:` prefix-strip + 12 new entries.
- **Determinism via sorted iteration** — Phase 29's `SampleCache.EagerLoad` uses `OrderBy(p => p)` + `OrderBy(v => v, StringComparer.Ordinal)` to keep file-load order identical across runs. Phase 33 follows the same shape.

### Integration Points

- **`SongRenderer.RenderSong(...)` instrument-string dispatch** — `flow-lang/StandardLibrary/Audio/SongRenderer.cs:97`. Phase 33 adds an `instrument.StartsWith("sampler:")` branch BEFORE the existing per-instrument synth dispatch. New branch reads `ExecutionContext.SfzPatchRegistry` + calls `SfzRenderer.Render`.
- **`Interpreter.ExecuteVariableDeclaration(decl)`** — wherever type checking on `SfzType` happens, register the (name, sfzValue) in `ExecutionContext.SfzPatchRegistry`. Single new branch on `decl.Type is SfzType`.
- **`FlowConfigPoco`** — `flow-cli/Config/FlowConfigPoco.cs`. Add `[DataMember(Name = "sfz_root")] public string? SfzRoot { get; set; }` (matches existing key shape). Update `FlowConfigPoco.Defaults` to leave it null.
- **`flow-lang/StandardLibrary/BuiltInFunctions.cs`** — registers all builtins at FlowEngine startup. Phase 33 adds `loadSfz(Symbol → Sfz)` + `loadSfz(String → Sfz)` overloads here. Both check `ExecutionContext.SfzEnabled` on entry.
- **`flow-lang/Runtime/ExecutionContext.cs`** — gains three new fields: `SfzEnabled: bool`, `SfzPatchRegistry: Dictionary<string, SfzData>`, `SfzDiagnostics: HashSet<string>`. The `__sfzInstruments` Dict lives on a fourth field as `Dictionary<Symbol, string>`.
- **`flow-lang/StandardLibrary/Audio/MidiExport.cs`** — `WriteMidi` instrument-name → GM program dict. Phase 33 patches the dict-build to include the 12 new entries AND adds the `sampler:` prefix-strip in the program lookup.
- **`flow-lang/sfz.flow`** — new stdlib file. ~50 lines: a `(dict ...)` literal + the `(__enableSfzModule)` marker call.
- **`flow-lang/StandardLibrary/Audio/Sfz/`** — new subdirectory. SfzParser.cs (~250 LOC est), SfzRenderer.cs (~200 LOC est), SfzData.cs (~80 LOC est), SfzRegion.cs (~50 LOC est), SfzSampleCache.cs (~200 LOC est, parallel to SampleCache.cs), SfzLoopMode.cs (enum, ~15 LOC).
- **`flow-lang/TypeSystem/SpecialTypes/SfzType.cs`** — new file. ~40 LOC (mirrors TuningType.cs).

</code_context>

<specifics>
## Specific Ideas

- **User's pragma-vs-stdlib reframe (Round 3 of spec-phase):** The user explicitly preferred `use "@sfz"` over a raw `enable sfz;` pragma. The reasoning: "If there is a standard naming convention it'd be nice to be able to just import some std library 'sfz' which enables sfz and you can just load by name like loadSfz violin or loadSfz cello and it uses our dictionary/symbol feature." This drove D-09/D-10/D-11.

- **User's symbol-as-config-key idea (Round 4 of spec-phase):** "Using the flow config when installing the language would be handy to have a symbol auto populated for the filepath, so like (setSfzRoot #sfzRoot). I'm not sure if that works syntactically but I would love for symbols to be expanded for use case almost like a data type." The cleanest interpretation we landed on: `@sfz` reads `sfz_root` from `~/.config/flow/config.toml` at module load; if missing, errors clearly with how to populate it. Composer doesn't need to invoke `setSfzRoot` per-script — install + config writes the path once.

- **User's "precomputed grid" preference (Area 1, this discussion):** When presented with three region-storage options the user pushed for a deeper pros/cons analysis and selected the precomputed 128×128 grid. The deciding factor was code clarity — the grid build encodes SFZ last-declared-wins as write-order, making the spec rule structurally enforced rather than implicit in lookup logic. Memory cost (~144 KB per patch) is invisible against the ~50 MB of sample data the same patch loads.

</specifics>

<deferred>
## Deferred Ideas

Captured during discussion but belong outside Phase 33:

- **Full SFZ v2 opcode coverage** — `fil_type`, `cutoff`, `cutoff_cc*`, `lfo*`, `eq1_freq`, `bend_up`, `xfin_lokey`, etc. Out of scope per 33-SPEC. May land in a Phase 33.x follow-up if Phase 34's symphony showcase reveals a missing opcode that meaningfully changes the audio.

- **`writeSfz` / SFZ export** — out of scope per 33-SPEC. v1.5+ if a use case emerges.

- **Real-time SFZ hot reload** — out of scope per 33-SPEC. v1.5+ if composers find iteration-on-patch friction painful.

- **Anonymous Sfz value flow** — `(loadSfz #violin) -> renderSong "..."` without intermediate binding. The user confirmed in spec-phase that explicit `Sfz violin = (loadSfz ...)` binding is sufficient. Deferred to v1.5+ if it surfaces during Phase 34 authorship.

- **Per-articulation SFZ region selection** — orchestral libraries commonly have `[trigger=note_on locc64=0 hicc64=63]` style switching for staccato vs legato. Phase 33 ignores `locc64`/`hicc64`/`trigger` opcodes (silently per D-09 of SPEC). Phase 28's articulation envelope still applies on top of the chosen region, so the OUTPUT still respects articulation rules — just not via SFZ region switching. Could be added as a future enhancement.

- **More than 19 instrument symbols in the shipped @sfz dict** — non-GM instruments (e.g. `#dulcimer`, `#sitar`) require the absolute-path overload until a future phase formalizes them.

- **Pre-computed loop body cache** — Phase 33 chose per-note render-time crossfade (D-18). If profiling later shows the crossfade math as a hot spot, the pre-compute path (D-19 alternative) remains a clean follow-up.

- **Sfz cache eviction policy** — Phase 33 follows Phase 29's "cache lives until FlowEngine disposal" model. If a long-running REPL session loads many patches and exhausts memory, an LRU eviction policy is a v1.5 enhancement.

</deferred>

---

*Phase: 33-sfz-orchestral-sampler*
*Context gathered: 2026-05-15*
