# Phase 15: Composer DX Part 2 - Context

**Gathered:** 2026-04-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship the two widest-surface Composer DX features of v1.2:

1. **DX-07:** A new `reverbTime <seconds> { ... }` musical-context block that applies per-voice reverb RT60, mirroring the existing `gain`/`pan` context pattern.
2. **DX-09:** Two `euclidean` overloads — `euclidean(hits, steps, note, swing)` and `euclidean(hits, steps, note, swing, humanize, seed)` — where `swing` is a velocity accent (not a timing change) and `humanize` is a seeded-deterministic velocity perturbation.

Both features are purely additive stdlib/grammar extensions. No changes to the audio backend, lexer core, or type-system core beyond the specified surface area. Rendering, MIDI export, and playback pipelines remain untouched below the integration points.

</domain>

<decisions>
## Implementation Decisions

### RT60 grammar contract (DX-07 grammar surface)

- **D-01:** RT60 value range: **0.0s – 30.0s (permissive)**. Accommodates dry-to-cathedral across ambient/drone use cases. Anything outside handled per D-03.
- **D-02:** `reverbTime 0 { ... }` → **disable reverb for the block (dry)**. The voice renderer short-circuits `Reverb.Apply` when the active `MusicalContext.ReverbTime` is exactly 0. Matches the charitable "0s tail = no reverb" interpretation.
- **D-03:** Out-of-range handling:
  - Negative RT60 (e.g., `reverbTime -2.5 { ... }`) → **parser-level rejection at the literal**. "Negative reverb tail" has no defensible meaning; erroring here helps composers find typos.
  - RT60 > 30s (e.g., `reverbTime 45 { ... }`) → **silent clamp to 30s**. Produces musically weird but defensible output per the charitable-interpretation philosophy.
  - RT60 in (0, 0.1) → **pass through to renderer unchanged** — produces a near-dry effect naturally; no special grammar handling needed.
- **D-04:** Nesting with sibling context blocks (`gain`, `pan`) = **independent axes**. Each context type is its own axis; all inherit through children. Inner `reverbTime` overrides outer `reverbTime` (same pattern as inner `gain` overriding outer `gain`). `gain 0.5 { reverbTime 2.0 { ... } }` composes both without interaction — voice gets gain 0.5 AND reverb RT60 2.0.

### Swing accent semantics (DX-09 swing parameter)

- **D-05:** Swing parameter range: **[-1.0, 1.0] clamped**. Negative = **anti-accent** (boost off-beats instead of on-beats). Values outside range silently clamp.
- **D-06:** On-beat definition in a euclidean pattern = **hits landing on step positions `0, (steps/hits), 2*(steps/hits), …`** (step-grid aligned). For `euclidean(3, 8)` with hits at step positions [0, 3, 6]: every hit is on-beat (all get accented at positive swing). For `euclidean(5, 8)` with hits at [0, 2, 3, 5, 7]: on-beat = hits at step positions that are multiples of `floor(steps/hits)` = multiples of 1, so all hits — resolves in edge cases by rounding toward the grid division. Matches how a drum machine paints accents on a grid.
- **D-07:** Accent magnitude = **raw velocity delta on [0, 1] scale** (no multiplier, no hidden math). `swing = 0.25` means "+0.25 to the accented set's velocity". Zero math surprise; composers control the exact amount directly. `swing = 0` means no accent.
- **D-08:** **Asymmetric accent** — only the accented set moves; the other set stays at default (base) velocity. With **positive** swing, on-beats move up, off-beats stay at default. With **negative** swing, the "accented set" flips to off-beats: off-beats move up by `|swing|`, on-beats stay at default. Preserves overall energy (no de-accenting of the unaccented side).

### Humanize unit and clamping (DX-09 humanize parameter)

- **D-09:** Unit = **fractional velocity on [0, 1] scale**. `humanize = 0.1` means "±0.1 random velocity perturbation". Consistent with how `swing`, `gain`, and base velocity are already expressed. MIDI byte mapping falls out naturally (~0.1 ≈ ±13 MIDI units).
- **D-10:** Range = **[0.0, 1.0] clamped**. Composers can reach extreme jitter (full ±1.0) if they want. Out-of-range inputs silently clamp.
- **D-11:** Distribution = **uniform over [-humanize, +humanize]** in Phase 15. Gaussian distribution is deferred until the `enable` pragma system ships (see DEFER-03 from Phase 14 `deferred-items.md`). Gaussian becomes **the first planned opt-in feature** of that pragma — syntax to be decided at DEFER-03 design time (provisional: `enable "gaussian-humanize"`). Phase 15 ships uniform only.
- **D-12:** Overflow handling = **clamp to [0, 1]** (not reflect, not re-roll). Perturbed = `max(0, min(1, base ± jitter))`. Predictable, deterministic, matches the existing `NoteType.Velocity` clamp in `NoteType.cs:244`.

### PRNG seed semantics (DX-09 determinism guarantee)

- **D-17:** The `seed: Int` parameter constructs a **local** `System.Random(seed)` scoped to the single `euclidean(...)` call. It does **not** read from or mutate `ExecutionContext.GetRandom` / the global seeded RNG. Rationale: determinism must hold even if other seeded-random calls run between two `euclidean` invocations in the same script. Local RNG also isolates PRNG consumption count, which D-12's "clamp, don't re-roll" already preserves.
- **D-18:** Byte-identical output contract applies across:
  - Repeat runs of the same script on the same machine
  - Repeat runs across .NET 9 patch versions (9.0.x)
  - Does NOT cross to future .NET majors if `System.Random` algorithm changes; this is explicitly acceptable (per ROADMAP success criterion #2 wording "across .NET patch versions", not "across major versions").

### Reverb.Apply wiring strategy (DX-07 audio path)

- **D-13:** Add **new overload** `Reverb.Apply(buffer, rt60Seconds, damping, mix)` in `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs`. Non-breaking to existing `Apply(buffer, roomSize, damping, mix)` callers. Internally maps `rt60 → feedback coefficient` using Schroeder's formula: `feedback = 10^(-3 * delay / rt60)`. Existing stdlib `reverb()` function (in `EffectsFunctions.cs:30-73`) is unchanged; the new context-block path uses the new overload.
- **D-14:** Per-voice reverb is applied in the **SongRenderer voice loop post-synthesis** — same flow as `gain`/`pan` reading `MusicalContext` fields. Each voice gets its own `Reverb.Apply` invocation per render pass. Fully independent tails (maximum creative range — see Deferred Ideas for shared-bus alternative if later needed). CPU cost: N reverb instances per song; acceptable at typical song sizes (<50 voices).
- **D-15:** Damping and mix values are **fixed sensible defaults** baked into the context-block code path (damping = 0.5, mix = 0.3, matching current `ReverbSimple` defaults in `EffectsFunctions.cs`). The context block exposes **only** RT60 to the composer. Advanced users who want full 4-parameter control continue to use the explicit `reverb(...)` stdlib call — both paths coexist.
- **D-16:** When `reverbTime` context is active AND the user also calls `reverb(...)` explicitly inside the block: **both apply and stack** (charitable interpretation). Explicit reverb runs on the voice buffer, then the context's RT60 reverb runs on top. May produce muddy output at high combined values, but the composer explicitly asked for both. No warning, no error, no silent override — two independent axes.

### Claude's Discretion

- Exact Schroeder/feedback formula constants (if D-13's `10^(-3*delay/rt60)` needs tuning for the existing Reverb delay line length — planner/researcher verifies against `Reverb.cs` internals)
- Parser-error message wording for negative RT60 (D-03)
- Specific test-name conventions and file layout for new `Phase15` test fixtures
- Choice between extending `euclidean` registration in `BuiltInFunctions.cs` via new signatures vs adding overloaded `FunctionSignature` entries (implementation detail of `InternalFunctionRegistry`)
- Exact threshold for "near-0" that short-circuits `Reverb.Apply` in D-02 (use `== 0` exact, not a small-epsilon comparison, since the parser produces the literal value unchanged)

</decisions>

<specifics>
## Specific Ideas

- **Charitable interpretation throughout** (per user philosophy, captured as durable memory): prefer silent-and-documented assumptions over parser/runtime errors for all value-range decisions. Error only when input has no defensible musical interpretation (e.g., negative RT60). Every silent assumption (silent clamp at 30s, clamp at [0,1] for humanize, clamp at [-1,1] for swing) MUST be documented in the feature's user-facing doc so composers can learn the behavior.
- **Phase 13 two-pass strict authorship discipline** (CONTEXT D-13 from Phase 14) carries forward as a research/planning discipline: for any regression-style test in this phase (if applicable), Pass 1 drafts from REQUIREMENTS wording alone, Pass 2 validates against real code. Extend to DX-09's byte-identical MIDI assertion if that regression style fits.
- **Raw velocity delta framing for swing** was explicitly preferred over "multiplier" framing — composers should pin exact velocity amounts, not chase multiplier side-effects. This reflects a broader preference for transparent numeric semantics over opaque musical-intuition math.

</specifics>

<canonical_refs>
## Canonical References

### Phase-scoped requirements
- `.planning/REQUIREMENTS.md` §DX-07 (line 52) — `reverbTime <seconds> { ... }` grammar and voice-render integration requirement; pre-landing identifier-audit clause.
- `.planning/REQUIREMENTS.md` §DX-09 (line 54) — `euclidean` overload surface, swing-as-velocity-accent mandate, seed-required-for-determinism clause, explicit "no new `MusicalNoteData` timing field" constraint (micro-timing deferred to v1.3).

### Upstream context from Phase 14
- `.planning/phases/14-composer-dx-part-1/deferred-items.md` §DEFER-02, §DEFER-03 — Pragma / `enable` keyword deferral. DX-09's gaussian-humanize (D-11) is explicitly pinned as the first planned opt-in once DEFER-03's pragma system ships.
- `.planning/phases/14-composer-dx-part-1/14-CONTEXT.md` §D-01 — Silent two-sided clamping precedent (slice), now generalized via charitable-interpretation memory.
- `.planning/phases/14-composer-dx-part-1/14-CONTEXT.md` §D-13 — Two-pass strict authorship discipline (reference for DX-09 determinism-regression testing if applicable).

### Codebase touchpoints (exact file:line pairs surfaced during scouting)
- `flow-lang/Ast/Statements/MusicalContextStatement.cs:8` — `MusicalContextType` enum; add `ReverbTime` member.
- `flow-lang/Runtime/MusicalContext.cs:35-60, 95-107` — add `ReverbTime` nullable double field, update `Clone()` and `ToString()`.
- `flow-lang/Parsing/Parser.cs:122, 131, 511-539` — add `reverbTime` token detection and numeric-body parse case mirroring `gain`/`pan`.
- `flow-lang/Interpreter/Interpreter.cs:215-245` — add `ReverbTime` case in the context-block execution switch.
- `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs:26` — add the RT60 overload per D-13.
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs:30-73` — existing `RegisterReverb()`/`ReverbSimple()`/`ReverbFull()`; D-15 reuses `damping=0.5, mix=0.3` defaults from here.
- `flow-lang/StandardLibrary/Audio/Voice.cs:6-40` — existing `Gain`/`Pan` fields; per D-14 do NOT add `ReverbTime` here (flow through `MusicalContext` like other context axes).
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1033-1074` — existing `euclidean` function; expand per DX-09 with two new overloads + swing/humanize/seed logic.
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:211-248` — `MusicalNoteData.Velocity` field and constructor clamp; no changes needed (D-12 reuses existing clamp).
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:191-192` — MIDI velocity byte mapping; no changes needed (DX-09 feeds through unchanged).
- `flow-lang/Runtime/ExecutionContext.cs:40-72` — existing seeded PRNG pattern (`GetRandom(bool fixedRng)`, `SetSeed(int seed)`); reference only, D-17 uses a LOCAL `new Random(seed)` inside `euclidean`, not this context.
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:76, 85, 104` — prior `new Random(seed)` usage pattern to mirror.

### Discipline references
- User memory `feedback_charitable_interpretation.md` — Charitable-interpretation philosophy driving D-01 through D-16 error-vs-clamp boundaries.
- `CLAUDE.md` — Overload resolution (specificity scoring) relevant to D-13's new Reverb.Apply overload coexisting with the existing one.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`MusicalContext` nullable-field flow** (`MusicalContext.cs:35-42`): `Gain`, `Pan`, `Velocity` are already nullable doubles. `ReverbTime` follows exactly this pattern — null means "unset, fall through to parent context", concrete value means "use this value in the renderer".
- **Parser value-body parse helper** (`Parser.cs:511-539`): parses `<keyword> <numeric> { <body> }` for `gain`/`pan`. The new `reverbTime` case reuses this exact shape with a different keyword token and range validation (D-03).
- **`ReverbSimple` default constants** (`EffectsFunctions.cs:46-60`): `damping=0.5f, mix=0.3f`. D-15 reuses these verbatim so the context block and the existing stdlib `reverb()` call share tuned values.
- **`NoteType` constructor velocity clamp** (`NoteType.cs:244`): existing `Math.Clamp(velocity, 0.0, 1.0)`. D-12 relies on this — humanize-perturbed velocity going through the constructor is automatically clamped.
- **`System.Random(seed)` pattern** in `VariationFunctions.cs:76, 85, 104`: existing "new Random seeded per-call" usage. D-17 mirrors this exactly.

### Established Patterns
- **Two independent axes via `MusicalContext`**: `gain` and `pan` already flow through nullable fields without any coupling. D-04 + D-14 simply add a third axis on the same rail.
- **Local seeded PRNG per determinism-critical call**: `VariationFunctions` does this for `mutate`/`shuffle`; D-17 applies the same pattern to `euclidean`. Consistent precedent.
- **Two-pass strict authorship** (Phase 13 D-11, reaffirmed Phase 14 D-13): three consecutive zero-divergence wins (13-01, 13-04, 14-03). Apply to DX-09 byte-identical MIDI regression if that style fits.

### Integration Points
- **`SongRenderer` voice loop** (location to be confirmed by planner — scouting pointed at `SongRenderer.cs` but didn't pin the exact line): per D-14, reads `MusicalContext.ReverbTime`, wraps the voice buffer through `Reverb.Apply(buffer, rt60Seconds, 0.5f, 0.3f)` before mixing into master. Mirrors how `Gain`/`Pan` are already consumed in the same loop.
- **MIDI export path** (`MidiExport.cs:191-192`): already reads `MusicalNoteData.Velocity` and maps to MIDI byte. No changes needed — DX-09's swing+humanize modifications happen upstream in `euclidean` when `MusicalNoteData` is constructed, so the MIDI export path reads the final velocity transparently.
- **WAV export path**: same principle — `renderSong` → voice synthesis → velocity-scaled sample generation → WAV write. No changes needed; DX-09 velocity changes flow through unchanged.

</code_context>

<deferred>
## Deferred Ideas

- **Gaussian humanize distribution** (cross-ref D-11, DEFER-03): First planned opt-in feature of the `enable` pragma system once DEFER-03 ships. Provisional syntax `enable "gaussian-humanize"`.
- **Shared reverb bus per RT60** (alternative to D-14): A future `reverbBus { ... }` construct could let composers explicitly opt into shared-tail-texture behavior (voices at the same RT60 blend tails in one reverb instance). Preserves D-14's per-voice default while making the CPU-efficient "analog studio" texture reachable. Not in v1.2 scope.
- **Micro-timing / groove offsets** (REQUIREMENTS.md DX-09 explicitly defers this): `swing` applied as timing offset (rather than velocity accent) would require a new `MusicalNoteData` timing field. Explicitly deferred to v1.3.
- **Damping/mix exposed on `reverbTime` block** (alternative to D-15): Syntax like `reverbTime 2.5 damping 0.7 mix 0.4 { ... }` would give composers full control without dropping to the `reverb(...)` stdlib. Not in scope — the `reverb(...)` stdlib already covers advanced-control use cases.
- **Negative-swing de-accent behavior** (alternative interpretation of D-08 for negative swing): instead of "off-beats move up by |swing|", could mean "on-beats move down by |swing|". Locked in D-08 as "the accented set flips sign", but leaving this note for future reconsideration if composers report the current semantics feels wrong.

</deferred>

---

*Phase: 15-composer-dx-part-2*
*Context gathered: 2026-04-20*
