# Phase 36: Sequence Algebra & Generative - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 36 ships the v1.5 generative-music core on top of the Phase 35 language foundation. Composer surface:

1. **`@patterns` stdlib** — 12 Tidal-style combinators on `Sequence` (`every` `fast` `slow` `chunk` `phase` `rev` `jux` `sometimes` `iter` `palindrome` `degrade` `superimpose`) + 1 Flow-native helper (`sparseSeq` with custom drop probability). All compose via the existing `->` chain.
2. **`@generative` stdlib** — Markov / L-system / cellular automata / Lorenz / logistic primitives. Markov and L-system ship in BOTH shapes: one-shot `(markov corpus order length seed)` for quick exploration AND `(markovTrain corpus order) → MarkovModel + (markovGenerate model length seed)` for reuse. New first-class `MarkovModel` and `LsystemModel` reference-identity value types.
3. **`@improv` stdlib** — `jam` chord-aware Markov improvisation backed by composer-overridable Flow-file rule packs at `@improv/styles/*.flow`. Ships `#jazz`, `#blues`, `#classical` baseline packs.
4. **Parameterized sections (SECT-01)** — `section verse(Note root = C4, Int repeats = 2) { ... }` declares a section with typed parameters supporting Phase 35 pattern syntax (guards + tuple destructure). Called in song expressions as `[verse(C4, 2) chorus]`. Section overloading via OverloadResolver + Phase 35 pattern dispatch (same name, different pattern signatures coexist).
5. **Universal named-argument syntax (LANGUAGE FOUNDATION addition)** — Lexer + Parser + OverloadResolver gain support for `(fn name1=val1 name2=val2)` call form. ~150 existing builtin signatures get parameter names backfilled. Positional call form remains valid; named-arg form is purely additive.
6. **`Runtime/PrngRegistry`** — keyed by `(SourceLocation, generator-name)` per D-v1.5-06; routes all PRNG-driven primitives (Markov / L-system / cellular / Lorenz / `degrade` / `sparseSeq` / `sometimes` / `jux`-when-stochastic). Unseeded calls reseed at `renderSong`/`writeWav` boundary preserving two-run cmp-clean determinism.

**In scope:** PAT-01 PAT-02 GEN-01..05 SECT-01 IMPROV-01 from REQUIREMENTS.md, PLUS the universal named-arg rollout (originally implied only by the `jam` REQ wording; now formally absorbed into Phase 36).

**Out of scope:** L-system parametric arguments / 2D branching, higher-order Markov smoothing, the other ~60 Tidal combinators (linger / swingBy / stutter / range / segment / etc.), ML-backed improvisation (Magenta-tier — explicit anti-feature per research/FEATURES.md).

</domain>

<decisions>
## Implementation Decisions

### Tidal Combinator Set (PAT-01, PAT-02)

- **D-36-01:** The 12 ship as: `every` `fast` `slow` `chunk` `phase` `rev` `jux` `sometimes` `iter` `palindrome` `degrade` `superimpose`. Hybrid of REQUIREMENTS.md and research/FEATURES.md — drops the `rarely`/`often` probability ladder (collapsed into `sometimes` with explicit probability arg), drops `cat` (redundant with existing `Transforms.concat`), drops `striate` (sample-domain, Phase 37 territory). Adds `iter` and `palindrome` from research.
- **D-36-02:** A 13th Flow-native helper `sparseSeq` ships alongside fixed-50% `degrade`. `degrade` matches Tidal compat (always ~50% drop); `(sparseSeq prob seq)` accepts custom drop probability. PRNG routed via PrngRegistry.
- **D-36-03:** Transform-arg style is **lambda-required**: `(every 4 (fn s => (fast s 2)) seq)`. Phase 36 does NOT introduce partial application / currying. Composer who wants to reuse a partial can use Phase 35's `as` chain naming.
- **D-36-04:** Cycle unit for cycle-dependent combinators (`every`, `chunk`, `phase`) is **bars** — `every 4` applies fn to bars 0, 4, 8, ...; `chunk N` divides the sequence into N bar-aligned chunks; `phase 0.25` rotates by 25% of bar count. Requires the BarRenderer-exposed bar layout from the existing audio pipeline.
- **D-36-05:** `sometimes` signature is `(sometimes prob fn seq)` — probability is explicit, not implicit. Default-arg overload `(sometimes fn seq)` = 0.5 for ergonomic shortcut.

### Markov & Generative Primitives (GEN-01..05)

- **D-36-06:** Markov ships in BOTH shapes — one-shot `(markov corpus order length seed)` AND split `(markovTrain corpus order)` → `MarkovModel` + `(markovGenerate model length seed)`. New first-class `MarkovModel` reference-identity value type. Same pattern for L-system: `(lsystem axiom rules iterations)` one-shot + `(lsystemModel axiom rules)` → `LsystemModel` + `(lsystemGenerate model iterations)`.
- **D-36-07:** Markov feature extraction is composer-controlled via named arg — `(markov corpus order length seed features=#pitch)` is the default; `features=<<#pitch #duration>>` extracts a tuple-keyed Markov state for richer output (at the cost of higher sparsity at order 2-3). Uses Phase 36's new universal named-arg syntax.
- **D-36-08:** L-system alphabet, cellular automata initial-seed pattern, Lorenz / logistic argument defaults, and `quantizeToScale` scale-arg type default to REQUIREMENTS.md wording. Researcher fills in specifics; planner picks idiomatic shapes. (Composer declined to discuss these — accepting researcher / planner judgment.)
- **D-36-09:** All PRNG-driven primitives route through new `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)` per D-v1.5-06. Unseeded calls reseed at `renderSong`/`writeWav` boundary. Two-run cmp-clean determinism preserved on non-`live` paths. Lorenz cross-platform FP divergence documented as platform-specific limitation.

### `jam` API + Universal Named Arguments (IMPROV-01 + language-foundation overlap)

- **D-36-10:** `jam` signature — required: `over` (Sequence of chords). Optional with defaults: `style=#jazz`, `length=8` (bars), `key=` (defaults to active `key { ... }` musical-context block; composer can override per-call to improvise outside the active key), `seed=` (unseeded → reseed at renderSong boundary), `order=2` (Markov order 1-3).
- **D-36-11:** **Universal named-arg rollout** — Phase 36 introduces named-argument syntax `(fn name1=val1 name2=val2)` to the WHOLE language, not just `jam`. ~150 existing builtin signatures get parameter names backfilled (extracted from existing C# lambda capture or registered explicitly). Composer can call every builtin with named args going forward; positional form remains valid. Lexer recognizes `Identifier=` in call position; OverloadResolver matches named args against signature param names.
- **D-36-12:** Style rule packs are **Flow-file rule packs** at `@improv/styles/*.flow`. Each pack is a top-level `Dict<Symbol, Value>` registered via `(registerStyle #name dict)`. v1.5 ships `#jazz` / `#blues` / `#classical` baseline packs. Composer can ship their own at `~/.config/flow/styles/*.flow` — loaded at FlowEngine init. The Dict shape (scale_weights / interval_transitions / rhythmic_template / articulation_distribution) is locked by the researcher and documented in `@improv/styles/README.md` as the composer-facing contract.

### Parameterized Sections (SECT-01)

- **D-36-13:** Section call syntax inside song expressions uses **parens**: `[verse(C4, 2) chorus]`. Zero-arg sections stay as bare identifier (`verse`). Heterogeneous bracket contents are fine — composer's eye distinguishes parameterized from zero-arg at a glance.
- **D-36-14:** Repetition operator `*N` composes with parameterized calls — `verse(C4, 2)*3` desugars to `[verse(C4, 2) verse(C4, 2) verse(C4, 2)]` (3 calls with the same args). Parser handles `*N` postfix on parameterized section calls.
- **D-36-15:** Section params support **default values**: `section verse(Note root = C4, Int repeats = 2) { ... }`. Call as `[verse() chorus]` uses both defaults; `[verse(D4) chorus]` overrides root, defaults repeats. Defaults work with positional AND named-arg call forms (per D-36-11).
- **D-36-16:** Arity / type mismatches are **strict** with Rust-style multi-line diagnostics via Phase 35-03 DiagnosticRenderer. Composer gets source-quoted span, caret pointer at the offending arg, and a label like `expected Note here, got Int` or `expected 2 args, got 3`.
- **D-36-17:** Section params support **full Phase 35 pattern syntax** — guards (`section pivotMod(Chord c when (= c.Quality "maj7")) { ... }`), tuple destructure (`section transposed(<<Note root, Semitone offset>> spec) { ... }`), constructor patterns, music-aware extractors (chord literal / roman numeral / articulation symbol). Pattern AST reused from Phase 35.
- **D-36-18:** **Section overloading** — multiple sections with the same name but different pattern signatures coexist. `section verse(Note root)` and `section verse(<<Note root, Int repeats>>)` both register; OverloadResolver picks at call time based on arg shape + Phase 35 pattern matching. Reuses the existing function-dispatch OverloadResolver.

### Claude's Discretion (deferred to researcher / planner)

- L-system alphabet style (Symbol-based per REQ wording vs turtle-graphics-string per research) — composer accepts researcher's call.
- Cellular automata initial-seed pattern (classic single-1-center vs random density vs hand-picked patterns).
- Chaos-map quantization scale-arg type (`ScaleData` from Harmony module vs `Array[Note]` vs roman-numeral string vs new `Scale` type).
- Section-overload precedence rules when multiple patterns match the same call site.
- Rule-pack Dict shape contract details (composer-facing fields + their semantics).
- Whether `jam` raises a charitable advisory or hard error when style + key combination is musically incompatible (e.g., `style=#blues` + chromatic key).
- Exact plan breakdown — researcher / plan-checker decide how to slice 10-12 plans.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### v1.5 milestone-level
- `.planning/PROJECT.md` — Core value, v1.5 milestone goal, key constraints
- `.planning/REQUIREMENTS.md` lines 9-20 — Locked decisions D-v1.5-01 through D-v1.5-11 (esp. D-v1.5-06 PrngRegistry contract, D-v1.5-10 Phase 35 dependency)
- `.planning/REQUIREMENTS.md` lines 45-64 — PAT-01..02, GEN-01..05, SECT-01, IMPROV-01 requirement wording (treat as floor; D-36-* decisions in THIS file refine and extend)
- `.planning/ROADMAP.md` Phase 36 section — Goal + success criteria + plan slot

### Phase 35 dependency-root (must understand surface before building on it)
- `.planning/phases/35-language-foundation/35-VERIFICATION.md` — What Phase 35 actually shipped (LANG-01..04, TEST-01..02, HK-01..04 verified)
- `.planning/phases/35-language-foundation/35-05-SUMMARY.md` — Pattern AST family (`Ast/Patterns/`), MatchExpression, PatternMatcher naive linear scan (D-v1.5-11)
- `.planning/phases/35-language-foundation/35-06-SUMMARY.md` — Music-aware extractors (ConstructorPattern + IsChordLiteral / IsRomanNumeral / IsArticulationSymbol discriminator flags), matchExhaustive pragma policy (D-v1.5-05), CapturedPragmas AST-attached threading
- `.planning/phases/35-language-foundation/35-07-SUMMARY.md` — `-> CALL as NAME` chain naming (LANG-03); FlowExpression.IntermediateName defaulted param; reused by Phase 36 combinator chains
- `.planning/phases/35-language-foundation/35-03-SUMMARY.md` — Rust-style DiagnosticRenderer (used by D-36-16 arity-mismatch reporting)

### v1.5 research (composer's source-of-truth picks)
- `.planning/research/FEATURES.md` — Phase 36 differentiator framing (Tidal-style algebra grafted onto statically-typed language; first-class generative primitives; chord-aware Markov improv)
- `.planning/research/STACK.md` lines 50-53, 278-285 — Hand-roll recommendation for Markov / L-system / cellular / Lorenz; explicit STACK-level API sketches (which are now refined by D-36-06 / D-36-07)
- `.planning/research/PITFALLS.md` — Pitfall 6 (Generative-primitive determinism break) backing D-v1.5-06 + D-36-09
- `.planning/research/SUMMARY.md` lines 9-13, 40, 47, 61, 97-100 — Phase 36 deliverables map and dependency-tree position

### Existing code (researcher must scout)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — Existing transpose / invert / retrograde / repeat shape; D-36-01 combinators extend this style (consider whether to split into `@patterns` module or absorb)
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` — Existing seeded `vary()` builtin with PRNG threading (PrngRegistry can adopt the seed-handling pattern but route through (SourceLocation, name) key)
- `flow-lang/Ast/Statements/SectionDeclaration.cs` — Current Section AST node (parameterless); D-36-13..18 extend with param list + Phase 35 patterns + overload support
- `flow-lang/Parsing/Parser.cs` lines 484-508 (`ParseSectionDeclaration`) — Current parser; D-36-13..18 modify
- `flow-lang/TypeSystem/OverloadResolver.cs` — Reused for section-overload dispatch per D-36-18
- `flow-lang/Lexing/SimpleLexer.cs` + `flow-lang/Lexing/TokenType.cs` — Named-arg syntax addition per D-36-11 (recognize `Identifier=` in call position)
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` + `BuiltInFunctions.cs` — ~150 signatures need parameter names backfilled per D-36-11

### Composer-facing examples to ship
- `examples/generative/markov_jazz.flow` (new) — Tutorial chapter showing one-shot `markov` + `markovTrain`/`markovGenerate` reuse + `jam` with style override
- `examples/generative/tidal_combinators.flow` (new) — All 12 combinators + sparseSeq exercised in one playable example
- `examples/sections/parameterized.flow` (new) — `section verse(Note root, Int repeats)` + overloading + Phase 35 patterns in signature

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **OverloadResolver** (`flow-lang/TypeSystem/OverloadResolver.cs`) — drives function dispatch; D-36-18 reuses it for section overload. Adds: Phase 35 pattern-matching at signature time. Already handles specificity scoring (exact match +1000 / compatible +500 / convertible +100).
- **Pattern AST family** (`flow-lang/Ast/Patterns/` from Phase 35) — reused verbatim in section signatures per D-36-17. ConstructorPattern with music-aware discriminator flags lets section params match on `Cmaj7` / `V7` / `#staccato` directly.
- **VariationFunctions** (`flow-lang/StandardLibrary/Composition/VariationFunctions.cs`) — existing seeded `vary()` builtin with Random-seeded mutation. PrngRegistry adoption pattern: same seed-handling shape but key by `(SourceLocation, "vary")` instead of explicit seed-or-default param.
- **DiagnosticRenderer** (`flow-lang/Diagnostics/` from Phase 35-03) — D-36-16 arity-mismatch reporting uses the multi-line Rust-style renderer directly.
- **FlowExpression.IntermediateName** (from Phase 35-07) — composers can name intermediate combinator results: `seq -> (transpose 2) as melody -> (every 4 (fn s => (fast s 2))) as varied -> render`. Reduces nested-call cognitive load.
- **MusicalContext stack** (`flow-lang/Runtime/MusicalContext.cs`) — D-36-10 `key=` override pushes a synthetic frame onto this stack for the jam evaluation, popped after. SECT-01 (per REQ) does the same for section parameter binding.

### Established Patterns
- **Charitable interpretation** (D-v1.5-05) — Phase 36 transforms follow this: zero-length seq → unchanged + stderr advisory; divide-by-zero rate (e.g., `(fast seq 0)`) → unchanged + stderr advisory. NOT errors. Composer can opt into strict via `enable matchExhaustive` (extends to other transforms via future pragmas).
- **One-shot stderr advisories** — RenderingDiagnostics.WarnOnce keyed on a sentinel. D-36 transforms emit advisories with sentinel `f"{transform-name}:{Span}"` so identical failure points dedup per process.
- **Defaulted-parameter AST extension** — Phase 35 LANG-03 pattern (extending FlowExpression with IntermediateName via defaulted param, no new AST node) reused for any AST additions in Phase 36 — keeps the migration single-commit-friendly per D-v1.5-01.
- **Two-run cmp-clean determinism** — Phase 18/25/27/28/29/33 inheritance; D-36-09 preserves it via PrngRegistry. Verifier runs `examples/generative/*.flow` twice via `flow render` (or equivalent) and SHA-256-compares output.

### Integration Points
- **SongRenderer** — D-36-13..14 section call (with positional args, defaults, `*N` repeat) hands off to existing SongRenderer voice/section dispatch. Synthetic MusicalContext frame push/pop is the integration mechanism.
- **FlowEngine.ExecuteScriptAndGetResult** — composer-facing API; named-arg call form per D-36-11 must work transparently. Existing positional callers in tests/ must not regress.
- **flow-cli `flow test`** — Phase 36 ships test files like `tests/test_patterns_every.flow`, `tests/test_markov_train_generate.flow`, `tests/test_jam_jazz.flow`. All composer-facing tests gate Phase 36 verification per the test framework (TEST-01 + TEST-02 from Phase 35).
- **PrngRegistry single source of truth** — D-36-09 puts PrngRegistry under `flow-lang/Runtime/`. Phase 37 granular jitter (DSP-01) and Phase 37 sampler round-robin (SAMP-01) will route through the same registry — Phase 36 establishes the contract.
- **`@improv/styles/` discovery** — FlowEngine init scans `flow-lang/improv/styles/*.flow` (shipped baselines) + `~/.config/flow/styles/*.flow` (user packs) and registers via the shipped `(registerStyle #name dict)` builtin. User packs override shipped packs on name collision (last-write-wins).

</code_context>

<specifics>
## Specific Ideas

- **Key override on jam** (composer-raised mid-discussion): the composer explicitly wants `key=` as a named arg on `jam` so they can improvise outside the active musical-context block. Use case: a chromatic / pivot section that breaks the surrounding key for a few bars. Captured in D-36-10.
- **Style packs as Flow files** (composer-picked over C# class form): composer's reasoning is that style packs are MUSICAL CONTENT, not engine internals — they should live where composers can read and tweak them. This shapes the entire `@improv` module layout. Captured in D-36-12.
- **Markov train+generate split** (composer-picked over one-shot-only): composer wants to TRAIN ONCE, GENERATE MANY — pass a `MarkovModel` value around the way they pass `Sequence` or `Chord` today. Captured in D-36-06.
- **Section pattern overload** (composer-picked over single signature): composer wants `section verse(Note root)` and `section verse(<<Note root, Int repeats>>)` to coexist — the polymorphic section is more expressive for pivot / transformation sections. Captured in D-36-18.

</specifics>

<deferred>
## Deferred Ideas

- **Named-argument scope creep concern**: The universal named-arg rollout per D-36-11 is language-foundation work. If the planner later finds the scope blows past 12 plans, the rollout can be retro-scoped into a new "Phase 35.1 — Named-argument syntax" mini-phase under D-v1.5-01 pre-traction latitude. Composer indicated preference to write CONTEXT.md first and revisit if scope explodes; do not pre-emptively split.
- **The other ~60 Tidal combinators** (linger / swingBy / stutter / range / segment / etc.) — pick another dozen for v1.6 based on composer feedback after v1.5 ships.
- **2D L-systems with branching** (`[` / `]` push/pop), **parametric L-systems with arguments** — out of scope for v1.5; researcher picks a non-branching turtle subset.
- **Higher-order Markov chains** (variable-order, smoothed) — overkill for v1.5 baseline; revisit if Markov outputs feel mechanical.
- **ML-backed improvisation** (MusicVAE / MusicTransformer / Magenta-tier) — explicit anti-feature per research/FEATURES.md. Document a recipe for piping Flow output to external Magenta if composer wants it.
- **Style packs from the community** — `~/.config/flow/styles/` already supports user packs per D-36-12. A future phase could add a registry/marketplace, but v1.5 just supports the file-system convention.
- **Pattern guards on chord progressions for `jam`** — e.g., `(jam over=chords style=#blues when=(fn c => (= c.Quality "dom7")) ...)` to only improvise over dominant chords. Interesting but defers to v1.6 — D-36-10 ships with the simpler always-improvise contract.

</deferred>

---

*Phase: 36-sequence-algebra-generative*
*Context gathered: 2026-05-20*
