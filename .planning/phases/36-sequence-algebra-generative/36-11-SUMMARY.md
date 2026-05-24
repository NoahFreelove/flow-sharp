---
phase: 36-sequence-algebra-generative
plan: 11
subsystem: standard-library
tags: [improv, jam, markov, style-pack, xdg-discovery, charitable-interpretation, prng, IMPROV-01, GEN-05]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    plan: 05
    provides: ExecutionContext.CurrentCallSite (set per-builtin by ExpressionEvaluator) + RenderingDiagnostics.WarnOnce + the charitable-interpretation contract shape inherited by Phase 36 stochastic primitives
  - phase: 36-sequence-algebra-generative
    plan: 06
    provides: ExecutionContext.PrngRegistry.GetRandom(callSite, name) usage pattern + the "seeded path uses new Random(seed); unseeded routes through PrngRegistry" idiom that JamFunctions follows verbatim
provides:
  - "@improv stdlib module — registerStyle + listStyles + jam (IMPROV-01 / D-36-10)"
  - "StyleRegistry — XDG-conventions style-pack discovery (shipped flow-lang/improv/styles/ + user ~/.config/flow/styles/) with Pitfall 8 last-write-wins + one-shot override advisory"
  - "JamFunctions — chord-aware Markov improvisation with chord-tone vs scale-tone vs chromatic-passing weighted roulette + interval-transition bias"
  - "3 baseline rule packs — jazz / blues / classical with composer-editable .flow source"
  - "flow-lang/improv/styles/README.md — composer-facing Dict shape contract"
  - "ExecutionContext.StyleRegistry — Dictionary<Value, DictData> keyed by Symbol Value"
  - "ExecutionContext.SuppressStyleOverrideAdvisory + StyleOverrideAdvisoriesEmitted — Pitfall 8 dedup state"
affects: [36-12]

# Tech tracking
tech-stack:
  added: []   # Hand-rolled C# — zero new dependencies
  patterns:
    - "PRNG threading via ExecutionContext.PrngRegistry keyed by (CurrentCallSite, \"jam\") for unseeded paths; explicit-seed path uses new Random(seed) — single sanctioned new Random in JamFunctions.cs verified by source-grep gate"
    - "Pack-load order: StyleRegistry.RegisterBuiltinsOnly runs BEFORE the interpreter wires (so registerStyle/listStyles resolve when packs parse); StyleRegistry.LoadShippedAndUserPacks runs AFTER (so the moduleLoader is ready)"
    - "Symbol-keyed DictData lookups in C#: walk Entries linearly comparing Symbol-typed keys by their string Data — avoids constructing fresh Symbol Values per lookup which would defeat the SymbolInternTable's pointer-equality contract"
    - "Charitable-interpretation contract: every degenerate input (empty over, length<=0, unknown style, order out-of-range, missing pack field, style+key incompatibility) emits WarnOnce + returns a usable Sequence; NEVER throws"

key-files:
  created:
    # ----- Task 1 (prior agent) -----
    - "flow-lang/StandardLibrary/Improv/StyleRegistry.cs (272 lines — RegisterBuiltinsOnly + LoadShippedAndUserPacks + registerStyle/listStyles C# implementations + Pitfall 8 last-write-wins + override advisory)"
    - "flow-lang/improv.flow (51 lines — @improv stdlib forward decls: 6 jam overloads + registerStyle + listStyles)"
    - "flow-lang/improv/styles/jazz.flow (40 lines — baseline #jazz rule pack: chord-tone-heavy strong beats + light chromatic passing)"
    - "flow-lang/improv/styles/blues.flow (34 lines — baseline #blues rule pack: looser chord-tone grip + heavy chromatic passing + bent-note articulations)"
    - "flow-lang/improv/styles/classical.flow (35 lines — baseline #classical rule pack: tight chord-tone on strong beats + zero chromatic_passing + legato/tenuto articulations)"
    - "flow-lang/improv/styles/README.md (179 lines — composer-facing Dict shape contract + load-order semantics + every-field semantics + charitable-interpretation list + audit recipe + security note)"
    - "flow-lang.Tests/Phase36/StyleRegistryTests.cs (284 lines — 6 facts: shipped-pack auto-load + registerStyle adds to registry + listStyles in insertion order + unknown-style fallback semantics + user-pack override w/ advisory + malformed-pack charitable)"
    # ----- Task 2 (this agent) -----
    - "flow-lang/StandardLibrary/Improv/JamFunctions.cs (~650 lines — replaces Task 1 stub; full chord-aware Markov algorithm + 6 arity overloads + charitable-interpretation contract)"
    - "flow-lang.Tests/Phase36/JamFunctionsTests.cs (10 facts — bar count + classical chord-tone bias + key= override differentiation + unknown-style fallback + order clamp + length-0 empty + minimal call + TestRunner integration smoke)"
    - "flow-lang.Tests/Phase36/JamDeterminismTests.cs (2 facts — source-grep gate <=1 new Random + style+key incompatibility charitable advisory)"
    - "tests/test_jam_jazz.flow (51 lines — composer-facing seeded determinism + writeWav target for scripts/test_two_run_determinism.sh)"
    - "tests/test_jam_key_override.flow (32 lines — composer-facing key= override surface for chromatic pivot bars)"
    - "tests/test_jam_styles.flow (49 lines — composer-facing exercise of all 3 baseline packs + listStyles audit)"
  modified:
    # ----- Task 1 (prior agent) -----
    - "flow-lang/Core/FlowEngine.cs (+15 lines — StyleRegistry.RegisterBuiltinsOnly + JamFunctions.RegisterContextDependent wiring before moduleLoader + interpreter init; StyleRegistry.LoadShippedAndUserPacks AFTER moduleLoader+interpreter are wired)"
    - "flow-lang/Runtime/ExecutionContext.cs (+25 lines — StyleRegistry Dictionary<Value, DictData> + StyleOverrideAdvisoriesEmitted HashSet<string> + SuppressStyleOverrideAdvisory bool; SnapshotState/RestoreState extended for hermetic-isolation contract)"
    - "flow-lang/flow-lang.csproj (+~8 lines — improv.flow + improv/styles/*.flow CopyToOutputDirectory entries)"
    - "flow-lang.Tests/Fixtures/FlowEngineRunner.cs (+~15 lines — GetEngine() accessor for engine-state probing during init)"

key-decisions:
  # ===== Task 1 (prior agent) — captured for completeness =====
  - "**Style packs as Flow files at flow-lang/improv/styles/*.flow (D-36-12)** — packs are MUSICAL CONTENT, not engine internals, so they live where composers can read and tweak them. Avoids forcing composer to learn C# to extend the surface."
  - "**Two-phase StyleRegistry init (Task 1)** — RegisterBuiltinsOnly runs early (so the pack files' (registerStyle ...) calls have something to bind); LoadShippedAndUserPacks runs AFTER the interpreter + moduleLoader are wired (so the pack files can `use \"@improv\"` and reference (dict ...)). Tried single-phase init first — fell over on the `use` line because moduleLoader wasn't ready. Wave structure makes this safe to discover late."
  - "**Pitfall 8 last-write-wins via insertion order** — shipped packs load FIRST (alphabetical order from Directory.GetFiles); user packs load SECOND. A user pack with the same Symbol overwrites the shipped entry via standard Dictionary indexer assignment. The one-shot override advisory fires only on shipped→user collisions (not on back-to-back shipped re-loads in the same process — gated by SuppressStyleOverrideAdvisory)."
  - "**Dict<Void, Void> wildcard for the registerStyle signature** — composer-supplied packs have heterogeneous Dict values (Dict-of-Symbol, Tuple, Dict-of-Dict), so the registry builtin can't constrain to a specific Dict<Symbol, T> generic instantiation. Same wildcard convention as @std's (get)/(set)/(each) Dict ops."

  # ===== Task 2 (this agent) =====
  - "**Beat-strength heuristic (locked v1.5)** — 4/4-implicit, 8 eighth-note slots per bar. Slot 0 = Strong (bar downbeat); slot 4 = Strong (mid-bar beat 3); slots 2, 6 = Weak (beats 2 + 4); slots 1, 3, 5, 7 = Syncopated (off-beat eighths). Maps tightly to the README's Dict-shape semantics. Future versions may accept timesig-aware templates beyond 4/4; v1.5 keeps the locked grid for predictable output and simpler README documentation."
  - "**Three-tier candidate pool per category** — ChordTone = MIDI pitches whose pitch class is in the chord; ScaleTone = pitches in scale BUT NOT in chord (the README documents this explicitly so composers see scale_tone weight as the not-already-chord-tone slot); ChromaticPassing = pitches in neither. The disjoint construction matches composer intuition — \"give scale_tone weight 0.5\" means 50% chance of a non-chord scale tone, not 50% chance of any-scale-tone-including-chord-tones (which would have been a hidden bias)."
  - "**Interval-transition weight mapping** — delta=0 → Repeat; |delta|=1 → Chromatic (the bent-note slot per the README); |delta|=2 → StepUp/StepDown by sign (the canonical scale-step); 3..12 → LeapUp/LeapDown by sign; >12 → 0.0. The ±1 → Chromatic mapping is intentional: the rule-pack shape treats #chromatic as the single-semitone passing-tone slot, with #step_up / #step_down being the two-semitone whole-step. Captured in the JamFunctions.ScoreIntervalTransition xmldoc."
  - "**Pitch range hardcoded to MIDI [48, 84] (C3..C6)** — three-octave working range for the candidate pool. Lets the interval-transition bias actually move pitch around (a single-octave pool would clamp too tight). Composers wanting wider range can post-process with octave() / up() / down() transforms."
  - "**Style+key musical-incompatibility heuristic (D-36-08)** — count fraction of pitch classes from the `over` chord progression that are OUTSIDE the active key's scale. If the fraction exceeds 50% AND the pack has any non-zero scale_tone weight, fire a one-shot stderr advisory keyed by `jam:style-key-mismatch:{style}:{key}`. NEVER a hard error per D-36-08 — Flow's ergonomics-first goal means composers see the warning but the sequence still renders. The advisory's threshold (50%) is empirical — F#major + Cmajor7 chord progression in JamDeterminismTests fires it; a normal in-key ii-V-I-VI does not."
  - "**MusicalContext key resolution priority: override > musical-context > default** — composer's per-call `key=` arg wins (D-36-10's explicit chromatic-pivot use case). Fallback to active MusicalContext.Key (from a `key Cmajor { ... }` block). Final fallback to \"Cmajor\" with a one-shot stderr advisory at the call-site. The advisory's sentinel includes CurrentCallSite so subsequent jam calls at different positions each get one heads-up; a tight loop reusing the same call-site dedups."
  - "**Charitable empty-chord-bar fallback** — when a `over` bar is rest-only (no pitched notes), the chord-tone candidate pool is empty. Rather than throwing, jam emits a per-call-site one-shot advisory and improvises on scale tones only for that bar. Matches the Phase 36-05 PatternFunctions charitable-passthrough precedent."
  - "**xUnit test scoping limitation discovered** — Flow variables declared inside musical-context blocks (`tempo / timesig / key`) live in their PUSHED frame and pop when the block ends. FlowEngineRunner.GetVariable reads GlobalFrame so it can't see those. JamFunctionsTests therefore declares Sequences at top level and passes the key as an explicit `key=` arg to jam. The composer-facing tests in tests/*.flow CAN nest under context blocks because they assert via (test ...) lazy thunks evaluated AFTER the engine returns — but the in-process xUnit pattern requires top-level declarations."
  - "**Two consecutive unseeded jam calls at the same source position do NOT round-trip via assertNotesMatch** — they SHARE the same PrngRegistry-routed Random instance per D-v1.5-06, so the second call sees state advanced by the first. The composer-facing test pattern (mirrored from Plan 36-05) is: bind unseeded result to a variable ONCE, then assert reference-equal. Two-run cmp-clean determinism is verified by scripts/test_two_run_determinism.sh against the writeWav output, not by in-script assertion."

requirements-completed: [IMPROV-01]
# GEN-05 (two-run cmp-clean determinism for stochastic generative primitives)
# is REINFORCED here: the seeded jam path is byte-deterministic, and unseeded
# jam routes through PrngRegistry which resets at writeWav boundary, preserving
# the Phase 18/25/27/28/29/33 two-run cmp-clean contract.

# Metrics
duration: ~80min (Task 1 prior agent + Task 2 this agent)
completed: 2026-05-22
---

# Phase 36 Plan 36-11: `@improv` stdlib — `jam` chord-aware Markov + style packs Summary

**The headline IMPROV-01 surface lands: composer writes `(jam over=chords style=#jazz length=8 seed=42)` and gets a chord-aware melodic Sequence. Style packs are MUSICAL CONTENT — composer-editable `.flow` files at `flow-lang/improv/styles/*.flow` (shipped: jazz / blues / classical) + `~/.config/flow/styles/*.flow` (user-supplied, override shipped via Pitfall 8 last-write-wins). The `jam` algorithm runs chord-tone-vs-scale-tone-vs-chromatic-passing weighted roulette per beat with interval-transition bias relative to the previous note, articulation tagging per beat-strength, and `key=` override for chromatic pivots. Charitable interpretation everywhere (D-v1.5-05 / D-36-08): degenerate inputs WarnOnce + return a usable Sequence; style+key musical incompatibility is an advisory, not an error. PRNG routed via `ExecutionContext.PrngRegistry` (unseeded) or `new Random(seed)` (explicit-seed) per D-v1.5-06 / D-36-09 — source-grep gate enforces exactly 1 `new Random(` in JamFunctions.cs.**

## Performance

- **Duration:** ~80 min total (Task 1: ~50 min prior agent; Task 2: ~30 min this agent)
- **Started:** 2026-05-22 (Task 1)
- **Completed:** 2026-05-22 (Task 2)
- **Tasks:** 2 of 2
- **Files created:** 13 (Task 1: 7; Task 2: 6)
- **Files modified:** 4 (Task 1: 4; Task 2: 0 modifications beyond the JamFunctions.cs stub replacement which is counted under created)

## Accomplishments

### Task 1 (prior agent — committed in `4e8957d`)

- **StyleRegistry.cs (272 lines)** — XDG-conventions style-pack discovery:
  - `RegisterBuiltinsOnly` wires the `registerStyle` + `listStyles` C# builtins; called early in FlowEngine init so the pack files' `(registerStyle ...)` calls bind.
  - `LoadShippedAndUserPacks` runs after interpreter + moduleLoader wire, scans `{AppContext.BaseDirectory}/improv/styles/*.flow` (shipped) then `~/.config/flow/styles/*.flow` (user) in deterministic ordinal order.
  - Per Pitfall 8: user packs OVERRIDE shipped on Symbol-name collision; emit one-shot stderr advisory keyed by `improv:override:{name}`.
  - Charitable: per-file try/catch; malformed pack fires `styleRegistry:loadFail:{file}` + CONTINUES; FlowEngine init NEVER aborts on a bad user pack.
- **registerStyle + listStyles builtins** — Symbol → Dict insertion (overridable via last-write-wins); `(listStyles)` returns the registered Symbols as `Symbol[]` in insertion order. Composers use `(listStyles)` to audit which packs actually loaded.
- **3 baseline rule packs** — jazz / blues / classical .flow files ship alongside the stdlib modules and copy to output via the csproj.
- **README.md (179 lines)** — composer-facing Dict shape contract. Every required field documented (`#beat_weights`, `#interval_transitions`, `#rhythmic_template`, `#articulation_distribution`). Beat-strength definitions, load-order semantics, charitable-interpretation list, audit recipe, security note (T-36-27 disposition: accept).
- **6 xUnit facts in StyleRegistryTests.cs** — auto-load contract, registerStyle composer surface, listStyles order, unknown-style fallback, user-pack-override with stderr advisory verification, malformed-pack charitable contract.

### Task 2 (this agent — committed in `1291b87`)

- **JamFunctions.cs (~650 lines)** — replaces the Task 1 empty-Sequence stub with the full chord-aware Markov algorithm:
  - 6 registered arity overloads from `(jam over)` to `(jam over style length key seed order)`. Default style = `#jazz`, default length = 8, default order = 2 (clamped to [1,3] with WarnOnce on out-of-range).
  - `GenerateJam` runs the RESEARCH §Pattern 8 algorithm: style-pack lookup → PRNG resolution → active-key resolution → per-bar chord-tone extraction + scale-tone candidate pool → per-slot beat-strength classification → weighted category roulette → within-category interval-transition roulette → articulation tagging.
  - Pitch range MIDI [48, 84] (C3..C6) — three octaves; lets the interval-transition bias actually steer pitch movement.
  - PRNG-SANCTIONED `new Random(seed)` exactly once in the explicit-seed path; unseeded routes through `ExecutionContext.PrngRegistry.GetRandom(CurrentCallSite, "jam")`.
  - Charitable contract: empty `over`, length<=0, unknown style, order out-of-range, missing pack field, style+key incompatibility — all WarnOnce + return usable Sequence.
- **JamFunctionsTests.cs (10 facts)** — covers bar count, classical chord-tone bias (>= 70% on strong beats with seed=7 over Cmaj7), key= override produces different sequences under same seed (Cmajor vs Fsharpmajor), unknown-style fallback advisory + still produces sequence, order clamp from 5 → 3, length-0 returns empty + advisory, minimal `(jam over)` call works, TestRunner end-to-end smoke.
- **JamDeterminismTests.cs (2 facts)** — source-grep CI gate (exactly 1 `new Random(` in JamFunctions.cs after comment stripping) + style+key incompatibility charitable advisory.
- **3 composer-facing .flow tests** — test_jam_jazz.flow (seeded determinism + writeWav target), test_jam_key_override.flow (chromatic-pivot surface), test_jam_styles.flow (all 3 packs + listStyles audit).
- **Two-run cmp-clean determinism verified**: `bash scripts/test_two_run_determinism.sh tests/test_jam_jazz.flow --render-cmd "dotnet run --project flow-cli --no-build -- run <SCRIPT>"` → identical SHA-256 `b8022e1eb87a301c1d697a878e1ec9d4b8037147ec3715e6a8a75a7751258c22` across both runs.

## Task Commits

1. **Task 1 — StyleRegistry + 3 baseline rule packs + @improv stdlib + 6 StyleRegistryTests** — `4e8957d` (feat)
2. **Task 2 — jam chord-aware Markov + 12 xUnit facts + 3 composer .flow tests** — `1291b87` (feat)

## Files Created/Modified

### Created
**Task 1 (prior agent):**
- `flow-lang/StandardLibrary/Improv/StyleRegistry.cs` — 272 lines; RegisterBuiltinsOnly + LoadShippedAndUserPacks + registerStyle/listStyles builtins + Pitfall 8 override advisory machinery
- `flow-lang/StandardLibrary/Improv/JamFunctions.cs` — Task 1 78-line stub (Task 2 replaces with ~650 lines)
- `flow-lang/improv.flow` — 51 lines; @improv stdlib forward decls (registerStyle / listStyles / 6 jam arity overloads)
- `flow-lang/improv/styles/jazz.flow` — 40 lines; #jazz baseline pack
- `flow-lang/improv/styles/blues.flow` — 34 lines; #blues baseline pack
- `flow-lang/improv/styles/classical.flow` — 35 lines; #classical baseline pack
- `flow-lang/improv/styles/README.md` — 179 lines; composer-facing Dict shape contract
- `flow-lang.Tests/Phase36/StyleRegistryTests.cs` — 284 lines; 6 facts

**Task 2 (this agent):**
- `flow-lang/StandardLibrary/Improv/JamFunctions.cs` — ~650 lines; chord-aware Markov implementation (replaces Task 1 stub)
- `flow-lang.Tests/Phase36/JamFunctionsTests.cs` — 10 facts
- `flow-lang.Tests/Phase36/JamDeterminismTests.cs` — 2 facts
- `tests/test_jam_jazz.flow` — 51 lines; composer-facing + writeWav target
- `tests/test_jam_key_override.flow` — 32 lines; composer-facing key= override
- `tests/test_jam_styles.flow` — 49 lines; composer-facing all-3-packs + listStyles

### Modified (Task 1)
- `flow-lang/Core/FlowEngine.cs` — wired StyleRegistry.RegisterBuiltinsOnly + JamFunctions.RegisterContextDependent before interpreter init; StyleRegistry.LoadShippedAndUserPacks after
- `flow-lang/Runtime/ExecutionContext.cs` — StyleRegistry Dictionary + StyleOverrideAdvisoriesEmitted HashSet + SuppressStyleOverrideAdvisory flag; SnapshotState/RestoreState extended
- `flow-lang/flow-lang.csproj` — improv.flow + improv/styles/*.flow CopyToOutputDirectory entries
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — GetEngine() accessor for init-state probing

## Decisions Made

(Captured exhaustively in the frontmatter `key-decisions` block. Key shapes:)

- **Beat-strength heuristic locked at 4/4-implicit 8-slot grid for v1.5.** Slot 0 + slot 4 = Strong; slot 2 + slot 6 = Weak; everything else = Syncopated. Future versions may accept timesig-aware templates beyond 4/4.
- **Three-tier candidate pool per category is DISJOINT.** ScaleTone = in-scale AND NOT in-chord. ChromaticPassing = in-neither. Composer's "0.5 scale_tone weight" means 50% chance of a non-chord scale tone — not 50% chance of any-scale-tone-including-chord-tones.
- **Interval-transition mapping: ±1 → Chromatic, ±2 → Step, 3..12 → Leap.** The README documents the slot semantics; the C# scoring function matches.
- **Style+key musical-incompatibility threshold is 50%.** When > 50% of `over` chord-progression pitch classes are outside the active key's scale AND the pack has non-zero scale_tone weight, fire the one-shot advisory.
- **Active-key resolution: keyOverride > MusicalContext.Key > "Cmajor" default + advisory.** Charitable.
- **Working pitch range MIDI [48, 84] (C3..C6).** Three-octave pool gives interval-transition bias room to steer pitch movement.
- **xUnit tests use top-level Flow declarations.** Variables inside `tempo / timesig / key` blocks live in their pushed frame and pop. The composer-facing `(test ...)` framework can nest under blocks because lazy thunks evaluate after engine returns.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Initial composer test used two unseeded `(jam chords)` calls in assertNotesMatch**
- **Found during:** Task 2 composer-test `tests/test_jam_jazz.flow` first run
- **Issue:** Two consecutive unseeded calls at the same source position SHARE PrngRegistry state per D-v1.5-06 — the second sees state advanced by the first, so they return DIFFERENT sequences. The test was asserting the wrong contract.
- **Fix:** Bind unseeded result to a variable once, then assert `assertNotesMatch unseeded unseeded` (reference-equal). The two-run cmp-clean contract is verified by `scripts/test_two_run_determinism.sh` against the file's writeWav output. Matches the Plan 36-05 PatternDeterminismTests precedent.
- **Files modified:** tests/test_jam_jazz.flow
- **Commit:** 1291b87

**2. [Rule 1 — Bug] xUnit tests used variables declared inside musical-context blocks**
- **Found during:** Task 2 — initial JamFunctionsTests run
- **Issue:** Flow variables declared inside `tempo / timesig / key` blocks live in the pushed frame and pop when the block ends. FlowEngineRunner.GetVariable reads GlobalFrame, so `Sequence improvised = ... ; Assert.Equal(..., runner.GetVariable("improvised"))` raised `Variable 'improvised' not found`.
- **Fix:** Restructure tests to declare Sequences at top level and pass the active key as an explicit `key=` arg to jam. The composer-facing .flow tests can still nest under context blocks because their `(test ...)` framework evaluates lazy thunks AFTER the engine returns.
- **Files modified:** flow-lang.Tests/Phase36/JamFunctionsTests.cs
- **Commit:** 1291b87

**3. [Rule 1 — Bug] Initial determinism test used chord literal `Gs7`**
- **Found during:** Task 2 — initial JamDeterminismTests.StyleKeyIncompatibilityIsCharitable run
- **Issue:** `Gs7` in a `| ... |` stream lexes as note `G7` (octave 7 G), not as the `Gs7` chord. The natural-letter-plus-7 collision is a known lexer ambiguity per the ChordParser docs (bare-digit qualities like `5`/`6`/`7`/`9` are gated to note-literal interpretation in note-stream context).
- **Fix:** Use composable chord names with the major-7 / minor-7 quality token to avoid the bare-7 ambiguity. The new progression `| Csmaj7 | Asm7 | Fsmaj7 | Bfmaj7 |` produces the desired out-of-key chord set.
- **Files modified:** flow-lang.Tests/Phase36/JamDeterminismTests.cs
- **Commit:** 1291b87

**4. [Rule 1 — Bug] `progression` is a reserved keyword — cannot be a variable name**
- **Found during:** Task 2 — first run of tests/test_jam_styles.flow
- **Issue:** Parser raised `Expected variable name. Got Progression 'progression'`. Phase ? added a `progression` keyword for chord progression DSL (per the recommended-stack notes in CLAUDE.md).
- **Fix:** Renamed the test's chord variable to `chords` (matching the test_jam_jazz.flow precedent).
- **Files modified:** tests/test_jam_styles.flow
- **Commit:** 1291b87

**5. [Rule 1 — Bug] Style+key incompatibility test failed when run alongside other tests**
- **Found during:** Task 2 — full Phase 36 suite run
- **Issue:** `RenderingDiagnostics` dedup is per-process. The `jam:style-key-mismatch:#blues:Cmajor` sentinel had already been emitted by another test, so the test's stderr-contains assertion failed.
- **Fix:** Add `RenderingDiagnostics.ResetForTesting()` at the start of the test + tag the test class with `[Collection("FlowScripts")]` to serialize with other diagnostic-sensitive Phase 36 tests.
- **Files modified:** flow-lang.Tests/Phase36/JamDeterminismTests.cs
- **Commit:** 1291b87

All 5 auto-fixes are localized; none changed the plan's scope or contracts.

### Pre-existing flake outside scope

Phase 35 `MatchExhaustivenessDefaultTests` suite has 1 flake when run alongside Phase 36 (passes in isolation; fails when other tests share the RenderingDiagnostics dedup state). Confirmed pre-existing via `git stash && dotnet test --filter Phase35` — same failure without any of this plan's changes. Out of scope for Plan 36-11.

## Test Results

### Phase 36 Plan 36-11 suite (both tasks)

```
dotnet test --filter "FullyQualifiedName~Phase36.Jam|FullyQualifiedName~Phase36.StyleRegistry"
→ Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18
  • StyleRegistryTests:   6/6  (Task 1)
  • JamFunctionsTests:   10/10 (Task 2)
  • JamDeterminismTests:  2/2  (Task 2)
```

### Phase 36 regression

```
dotnet test --filter "FullyQualifiedName~Phase36"
→ Passed!  - Failed: 0, Passed: 173, Skipped: 0, Total: 173
```

### Composer-facing acceptance

```
flow test tests/test_jam_jazz.flow
→ PASS  jam jazz seeded is deterministic
→ PASS  jam jazz default args work (unseeded reuse)
Total: 2; Passed: 2; Failed: 0

flow test tests/test_jam_key_override.flow
→ PASS  key= override is deterministic for Cmajor
→ PASS  key= override is deterministic for Fsharpmajor
Total: 2; Passed: 2; Failed: 0

flow test tests/test_jam_styles.flow
→ PASS  jam jazz pack is deterministic
→ PASS  jam blues pack is deterministic
→ PASS  jam classical pack is deterministic
→ PASS  shipped style packs are loaded at engine init
→ PASS  blues pack is loaded
→ PASS  classical pack is loaded
Total: 6; Passed: 6; Failed: 0
```

### Two-run determinism gate

```
bash scripts/test_two_run_determinism.sh tests/test_jam_jazz.flow \
  --render-cmd "dotnet run --project flow-cli --no-build -- run <SCRIPT>"
→ Run A: b8022e1eb87a301c1d697a878e1ec9d4b8037147ec3715e6a8a75a7751258c22
→ Run B: b8022e1eb87a301c1d697a878e1ec9d4b8037147ec3715e6a8a75a7751258c22
→ Two-run determinism: PASS (identical SHA-256)
```

### Source-grep CI gate

```
grep -v '^[[:space:]]*//' flow-lang/StandardLibrary/Improv/JamFunctions.cs | grep -c 'new Random('
→ 1   (the single sanctioned explicit-seed path)
```

## Threat Surface Scan

No new threat surface beyond the plan's `<threat_model>` register:

| Threat | Disposition | Status |
|--------|-------------|--------|
| T-36-27 (Tampering — malicious user style pack) | accept | ✓ Style packs are Flow code with same privilege as any other script; loader is charitable about non-`(registerStyle ...)` top-level statements; README documents the security posture |
| T-36-28 (Pitfall 8 load-order integrity) | mitigate | ✓ Shipped FIRST, user SECOND, deterministic alphabetical within each dir; one-shot override advisory keyed by `improv:override:{name}` |
| T-36-29 (Style + key musical incompatibility silent surprise) | mitigate | ✓ Heuristic-based one-shot stderr advisory; JamDeterminismTests.StyleKeyIncompatibilityIsCharitable pins |
| T-36-30 (Unseeded jam wall-clock Random) | mitigate | ✓ PrngRegistry-routed unseeded path; source-grep gate caps at 1 (the explicit-seed path) |
| T-36-V12 (V12 File Handling — XDG user packs) | mitigate | ✓ Same XDG posture as Phase 30; per-file try/catch + one-shot advisory on load failure; FlowEngine init NEVER aborts on a bad pack |

No new threat flags emerged.

## What This Unblocks

- **Plan 36-12 (Phase 36 GEN-05 phase gate)** — the two-run cmp-clean harness invocation against `tests/test_jam_jazz.flow` is the final Phase 36 stochastic-primitive verification artifact for IMPROV-01. With this plan committed, every Phase 36 generative primitive (patterns / markov / lsystem / cellular / chaos / jam) has its determinism gate.
- **v1.6 community style packs** — the `~/.config/flow/styles/` XDG convention is live. Composers can ship their own packs without any C# code changes; a future phase can add a registry/marketplace concept on top.
- **v1.6 jam extensions (deferred)** — pattern guards on chord progressions for `jam` (e.g., `when=(fn c => (= c.Quality "dom7"))`) is a v1.6 candidate per Phase 36 CONTEXT § Deferred Ideas. The Plan 36-11 surface ships with the simpler always-improvise contract.

## Self-Check: PASSED

**Files asserted:**
- `[ -f flow-lang/StandardLibrary/Improv/StyleRegistry.cs ]` → FOUND (Task 1)
- `[ -f flow-lang/StandardLibrary/Improv/JamFunctions.cs ]` → FOUND (~650 lines, Task 2)
- `[ -f flow-lang/improv.flow ]` → FOUND (Task 1)
- `[ -f flow-lang/improv/styles/jazz.flow ]` → FOUND (Task 1)
- `[ -f flow-lang/improv/styles/blues.flow ]` → FOUND (Task 1)
- `[ -f flow-lang/improv/styles/classical.flow ]` → FOUND (Task 1)
- `[ -f flow-lang/improv/styles/README.md ]` → FOUND (Task 1)
- `[ -f flow-lang.Tests/Phase36/StyleRegistryTests.cs ]` → FOUND (Task 1)
- `[ -f flow-lang.Tests/Phase36/JamFunctionsTests.cs ]` → FOUND (Task 2)
- `[ -f flow-lang.Tests/Phase36/JamDeterminismTests.cs ]` → FOUND (Task 2)
- `[ -f tests/test_jam_jazz.flow ]` → FOUND (Task 2)
- `[ -f tests/test_jam_key_override.flow ]` → FOUND (Task 2)
- `[ -f tests/test_jam_styles.flow ]` → FOUND (Task 2)

**Commits asserted:**
- `4e8957d` (Task 1) → FOUND in `git log --oneline`
- `1291b87` (Task 2) → FOUND in `git log --oneline`

**No-regression assertions:**
- Phase 36: 173/173 PASS (full suite incl. all prior 36-01..10 + new 18 from this plan)
- Two-run cmp-clean: SHA-256 match across 2 renders of tests/test_jam_jazz.flow
- Source-grep gate: 1 hit for `new Random(` in JamFunctions.cs (the explicit-seed path; gate cap is 1)

## Issues Encountered

**Phase 35 MatchExhaustivenessDefaultTests pre-existing flake** — 1/80 Phase 35 facts fails when other tests share the RenderingDiagnostics dedup state. Confirmed pre-existing (failed before any of this plan's changes via `git stash`). Not a regression from Plan 36-11. The orchestrator may want to track this independently — the broader Phase 36 work has accumulated several similar RenderingDiagnostics serialization issues that PatternEveryTests / StyleRegistryTests already mitigate via `[Collection("FlowScripts")]`.

---
*Phase: 36-sequence-algebra-generative*
*Plan: 11*
*Completed: 2026-05-22*
