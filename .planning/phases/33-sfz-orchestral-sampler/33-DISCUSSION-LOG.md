# Phase 33: SFZ Orchestral Sampler - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 33-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-15
**Phase:** 33-sfz-orchestral-sampler
**Areas discussed:** Region storage + lookup shape, Sample loading strategy, Stdlib `@sfz` module shape + binding registry, MIDI export + loop-crossfade implementation site

---

## Region storage + lookup shape

First pass — initial recommendation was the flat list. User asked for deeper pros/cons; on writing out the actual memory + speed + code-clarity numbers, the recommendation reversed to the precomputed grid.

| Option | Description | Selected |
|--------|-------------|----------|
| Precomputed 128×128 grid | Build-time encodes SFZ last-declared-wins as write-order; lookup is one array index. ~144 KB per patch (~1% of audio data). Code: build is the spec rule, lookup is unbuggable. | ✓ |
| Flat List + linear scan | Simplest, ~10 LOC, ~13 KB per patch. Lookup loop is O(N) and the last-wins semantics are implicit in 'keep scanning after a match.' Reader has to trace the loop to understand. | |
| Dict<int midi, List<Region>> + filter | Middle ground: O(1) pitch lookup + tiny velocity scan. ~15 KB per patch. More complex than A or C; loses both A's simplicity and C's structural-correctness story. | |

**User's choice:** Precomputed 128×128 grid.
**Notes:** User pushed back on the initial "flat list" recommendation asking for proper pros/cons. The actual numbers — ~144 KB per patch vs ~13 KB, both invisible against ~50 MB of sample data per patch — made code clarity the deciding factor. The grid build IS the spec rule expressed as write-order semantics; lookup becomes unbuggable. Side-data sorted-by-pitch index added for SPEC REQ-4 nearest-pitch fallback (all three options need this).

---

## Sample loading strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Eager-on-renderSong-walk (Phase 29 pattern) | `loadSfz` parses + builds region grid only; renderSong walks the song's note set, dereferences `Grid[k,v]`, eager-loads only the regions actually needed. Mirrors Phase 29 D-13/14/15. | ✓ |
| Eager-on-loadSfz | Load every .wav at parse time. Simple but a top-of-script `(loadSfz #violin)` for a song that uses 8 notes still pays full 50 MB load. | |
| Lazy-on-first-use (per-note) | First note triggers its sample load mid-render. Causes audible stutter. Phase 29 explicitly rejected this. | |
| Two-stage: lazy + warmup hint | Default lazy, expose `(warmupSfz patch song)`. Two API surfaces; composers will forget. | |

**User's choice:** Eager-on-renderSong-walk (Phase 29 pattern).
**Notes:** Phase 29 D-14 already locked this for the bundled-sample path. Reusing the same pattern in `SfzSampleCache` keeps the mental model uniform across the codebase.

---

## Stdlib `@sfz` module shape + binding registry

Two coupled questions in one turn:

### Module shape

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid: Flow file owns the dict, C# always-registers loadSfz, import sets a gate flag | `flow-lang/sfz.flow` runs the `(dict ...)` literal and a `(__enableSfzModule ...)` marker call that flips `ExecutionContext.SfzEnabled = true`. loadSfz is registered unconditionally; checks the flag on call. | ✓ |
| Pure-Flow file with forward-decls | Mirrors Phase 26.2 ERG-05's audio.flow `forward proc gain(Buffer, Decibel)` pattern. Cleaner conceptually but forward-decl-of-a-builtin stretches the pattern. | |
| C# marker-import | `use "@sfz"` triggers `RegisterSfzBuiltins()` C#-side. No Flow file. Dict edits require C# rebuild. | |

### Binding registry

| Option | Description | Selected |
|--------|-------------|----------|
| ExecutionContext.SfzPatchRegistry: Dictionary<string, SfzData> populated on typed-Sfz variable assignment | Interpreter writes `(name, sfzValue)` into the registry when a typed Sfz variable declaration executes. SongRenderer reads via `sampler:NAME` prefix dispatch. | ✓ |
| Walk variable scope at dispatch time | No separate registry. SongRenderer asks ExecutionContext for the variable. Cross-frame lookups awkward. | |
| FlowEngine-owned process-wide singleton | Survives REPL evals but breaks isolation across nested FlowEngines. | |

**User's choice:** Hybrid module shape + ExecutionContext.SfzPatchRegistry.
**Notes:** The hybrid shape lets the GM symbol→path dict live in Flow code (composers can read it) while keeping `loadSfz` as a real C# builtin. ExecutionContext.SfzPatchRegistry mirrors Phase 32's TuningStack ("state lives on ExecutionContext") shape.

---

## MIDI export + loop-crossfade implementation site

Two coupled questions in one turn:

### MIDI export for sampler:NAME

| Option | Description | Selected |
|--------|-------------|----------|
| Strip prefix + reuse GM dict | `sampler:violin` strips `sampler:`, looks up `violin` in the existing GM-program dict, adds 12 new entries for the 19 sampler symbols. MIDI export works without VSCO-CE installed on receiver. | ✓ |
| Single dedicated MIDI fallback (program 0) | Every sampler:* exports as piano + track-name comment. Bad MIDI quality for symphony exports. | |
| Out-of-scope + stderr advisory | MIDI export of sampler errors. Forces choice between SFZ-rich audio OR MIDI export. | |

### Loop crossfade implementation site

| Option | Description | Selected |
|--------|-------------|----------|
| Per-note at render time | 441-frame sin/cos crossfade math inside `SfzRenderer.Render` per note. ~30 LOC. No new caches. | ✓ |
| Pre-compute looped buffer once per region at load time | ~44 MB extra cache per patch — unacceptable against ~50 MB of raw sample data the same patch already loads. | |
| Streaming generator (yield-per-sample) | Doesn't fit Flow's buffer-based render pipeline. Out of scope. | |

**User's choice:** Strip-prefix-into-GM-dict + per-note render-time crossfade.
**Notes:** Strip-prefix means MIDI exports of symphonies work for receivers without VSCO-CE installed — the file uses sensible GM programs even when the audio rendering wouldn't. Per-note crossfade keeps memory tight (no pre-computed loop buffer cache) and the math cost is well under 1% of render time.

---

## Claude's Discretion

These flow from the SPEC + the decisions above without separate user input. Captured in 33-CONTEXT.md "Claude's Discretion" subsection:

- Error class hierarchy (`SfzParseException` extends `ParseException`; `UnknownInstrumentSymbolError` / `UnknownSamplerNameError` / `MissingSfzRootError` / `SfzFileNotFoundError` extend appropriate Flow base classes).
- Advisory dedup state location (`ExecutionContext.SfzDiagnostics: HashSet<string>` + `RenderingDiagnostics.WarnOnce` overload).
- Determinism preservation (sorted iteration mirroring Phase 29 D-31 / `SampleCache.EagerLoad`).
- `SfzType` shape (sealed singleton mirroring Phase 32 TuningType).
- `SfzData` internal model (Description / BasePath / Regions / Grid / SortedByPitch).
- `SfzRegion` field set (all 13 opcodes + inherited header defaults flattened at parse time).
- Header inheritance applied AT PARSE TIME by flattening.

## Deferred Ideas

Captured in 33-CONTEXT.md `<deferred>` section:

- Full SFZ v2 opcode coverage (out of scope per 33-SPEC; may land as Phase 33.x follow-up).
- `writeSfz` / SFZ export (out of scope; v1.5+).
- Real-time SFZ hot reload (out of scope; v1.5+).
- Anonymous Sfz value flow without intermediate binding (deferred to v1.5+).
- Per-articulation SFZ region selection (locc64/hicc64/trigger opcodes ignored; Phase 28 envelope still applies).
- More than 19 instrument symbols in the shipped `@sfz` dict.
- Pre-computed loop body cache (alternative path if per-note becomes a hot spot).
- Sfz cache LRU eviction policy (long-running REPL scenarios).
