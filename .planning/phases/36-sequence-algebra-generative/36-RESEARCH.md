# Phase 36: Sequence Algebra & Generative — Research

**Researched:** 2026-05-20
**Domain:** Interpreter / stdlib expansion — Tidal-style sequence algebra; generative primitives (Markov / L-system / cellular automata / Lorenz / logistic); chord-aware Markov improvisation; parameterized sections built on Phase 35 pattern AST; universal named-argument call form; PRNG registry keyed by (SourceLocation, generator-name)
**Confidence:** HIGH for codebase-internal claims (every reusable surface inspected); HIGH for D-36-* lock interpretation (CONTEXT.md is precise); MEDIUM for the universal-named-arg scope estimate (211 `registry.Register` sites is mechanical-but-large); MEDIUM for the chaos-map quantization scale-arg type pick (multiple valid shapes); HIGH for the determinism integration pattern (Phase 18/25/27 inheritance + Phase 32 RenderTuning push/pop are the templates)

## Summary

Phase 36 is the v1.5 milestone's largest creative-surface phase — 13 new combinators on `Sequence`, 5 generative primitives (each shipping in 1-shot AND model-train+generate shape for two of them), chord-aware Markov improvisation, parameterized sections built directly on the Phase 35 pattern AST, AND a language-foundation overlap (universal named-argument syntax for the whole language). Across all six deliverables one constraint dominates: every PRNG-driven primitive must thread through a new `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)` so the two-run cmp-clean determinism contract (inherited from Phase 18/25/27/28/29/33) holds without forcing composers to type `seed=` at every call site.

The codebase is well-prepared. Phase 35's `Ast/Patterns/` family is reused verbatim for SECT-01 parameter signatures — section overload becomes pattern dispatch over the param list. `OverloadResolver` already scores via specificity (+1000 exact / +500 compatible / +100 convertible) and rejects ambiguity; we extend it once to recognize named-arg matching against signature param names. `MusicalContext` push/pop is the canonical synthetic-frame mechanism for section parameter binding and for `jam`'s `key=` override. `VariationFunctions.vary()` is the existing seeded-PRNG idiom (six overloads cover seeded vs unseeded + typed vs random + diatonic) — PrngRegistry adopts the shape but routes through `(SourceLocation, name)` keying instead of explicit-or-default seed args. `Span` lands on every AST record already from Phase 35-01, so `SourceLocation` for PrngRegistry keying is a pure read from `expr.Span.Start`.

The hardest correctness work is **named-argument backfill** — 211 `registry.Register` call sites across BuiltInFunctions.cs alone don't currently carry parameter names. `FunctionSignature` is a record of `(Name, InputTypes, IsVarArgs)` with no `ParameterNames` slot. Adding `IReadOnlyList<string>? ParameterNames = null` as a defaulted-positional param (Phase 35 sweep convention) is the right shape, but the backfill itself is a ~1500-line mechanical sweep that should split across two waves to keep the diff reviewable. Plans 36-12 and 36-13 in the slicing below carry this load.

**Primary recommendation:** Plan as **12 plans across 6 waves**. Wave 0 stubs out the determinism contract (PrngRegistry + Phase 36 test fixtures). Wave 1 ships the universal named-arg syntax (lexer + parser + resolver, NO backfill yet). Wave 2 backfills builtin parameter names in two parallel halves (audio/dsp/harmony vs collections/transforms/test) so named-arg syntax becomes usable. Wave 3 ships the 13 combinators in `@patterns`. Wave 4 ships the 5 generative primitives + train/generate split + MarkovModel/LsystemModel reference-identity types. Wave 5 ships parameterized sections + section overloading via pattern dispatch. Wave 6 ships `jam` + the 3 baseline rule packs + tutorial examples. Each wave commits independently; Phase 35's `as`-chain naming, charitable-interpretation discipline, and matchExhaustive pragma plumbing are all reused not reinvented.

## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Tidal Combinator Set (PAT-01, PAT-02)

- **D-36-01:** The 12 ship as: `every` `fast` `slow` `chunk` `phase` `rev` `jux` `sometimes` `iter` `palindrome` `degrade` `superimpose`. Hybrid of REQUIREMENTS.md and research/FEATURES.md — drops the `rarely`/`often` probability ladder (collapsed into `sometimes` with explicit probability arg), drops `cat` (redundant with existing `Transforms.concat`), drops `striate` (sample-domain, Phase 37 territory). Adds `iter` and `palindrome` from research.
- **D-36-02:** A 13th Flow-native helper `sparseSeq` ships alongside fixed-50% `degrade`. `degrade` matches Tidal compat (always ~50% drop); `(sparseSeq prob seq)` accepts custom drop probability. PRNG routed via PrngRegistry.
- **D-36-03:** Transform-arg style is **lambda-required**: `(every 4 (fn s => (fast s 2)) seq)`. Phase 36 does NOT introduce partial application / currying. Composer who wants to reuse a partial can use Phase 35's `as` chain naming.
- **D-36-04:** Cycle unit for cycle-dependent combinators (`every`, `chunk`, `phase`) is **bars** — `every 4` applies fn to bars 0, 4, 8, ...; `chunk N` divides the sequence into N bar-aligned chunks; `phase 0.25` rotates by 25% of bar count. Requires the BarRenderer-exposed bar layout from the existing audio pipeline.
- **D-36-05:** `sometimes` signature is `(sometimes prob fn seq)` — probability is explicit, not implicit. Default-arg overload `(sometimes fn seq)` = 0.5 for ergonomic shortcut.

#### Markov & Generative Primitives (GEN-01..05)

- **D-36-06:** Markov ships in BOTH shapes — one-shot `(markov corpus order length seed)` AND split `(markovTrain corpus order)` → `MarkovModel` + `(markovGenerate model length seed)`. New first-class `MarkovModel` reference-identity value type. Same pattern for L-system: `(lsystem axiom rules iterations)` one-shot + `(lsystemModel axiom rules)` → `LsystemModel` + `(lsystemGenerate model iterations)`.
- **D-36-07:** Markov feature extraction is composer-controlled via named arg — `(markov corpus order length seed features=#pitch)` is the default; `features=<<#pitch #duration>>` extracts a tuple-keyed Markov state for richer output (at the cost of higher sparsity at order 2-3). Uses Phase 36's new universal named-arg syntax.
- **D-36-08:** L-system alphabet, cellular automata initial-seed pattern, Lorenz / logistic argument defaults, and `quantizeToScale` scale-arg type default to REQUIREMENTS.md wording. Researcher fills in specifics; planner picks idiomatic shapes.
- **D-36-09:** All PRNG-driven primitives route through new `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)` per D-v1.5-06. Unseeded calls reseed at `renderSong`/`writeWav` boundary. Two-run cmp-clean determinism preserved on non-`live` paths. Lorenz cross-platform FP divergence documented as platform-specific limitation.

#### `jam` API + Universal Named Arguments (IMPROV-01 + language-foundation overlap)

- **D-36-10:** `jam` signature — required: `over` (Sequence of chords). Optional with defaults: `style=#jazz`, `length=8` (bars), `key=` (defaults to active `key { ... }` musical-context block; composer can override per-call to improvise outside the active key), `seed=` (unseeded → reseed at renderSong boundary), `order=2` (Markov order 1-3).
- **D-36-11:** Universal named-arg rollout — Phase 36 introduces named-argument syntax `(fn name1=val1 name2=val2)` to the WHOLE language. ~150 existing builtin signatures get parameter names backfilled. Composer can call every builtin with named args going forward; positional form remains valid.
- **D-36-12:** Style rule packs are **Flow-file rule packs** at `@improv/styles/*.flow`. Each pack is a top-level `Dict<Symbol, Value>` registered via `(registerStyle #name dict)`. v1.5 ships `#jazz` / `#blues` / `#classical` baseline packs. Composer can ship their own at `~/.config/flow/styles/*.flow` — loaded at FlowEngine init.

#### Parameterized Sections (SECT-01)

- **D-36-13:** Section call syntax inside song expressions uses **parens**: `[verse(C4, 2) chorus]`. Zero-arg sections stay as bare identifier (`verse`).
- **D-36-14:** Repetition operator `*N` composes with parameterized calls — `verse(C4, 2)*3` desugars to 3 calls with the same args.
- **D-36-15:** Section params support **default values**: `section verse(Note root = C4, Int repeats = 2) { ... }`.
- **D-36-16:** Arity / type mismatches are **strict** with Rust-style multi-line diagnostics via Phase 35-03 DiagnosticRenderer.
- **D-36-17:** Section params support **full Phase 35 pattern syntax** — guards, tuple destructure, constructor patterns, music-aware extractors. Pattern AST reused from Phase 35.
- **D-36-18:** **Section overloading** — multiple sections with the same name but different pattern signatures coexist. OverloadResolver picks at call time based on arg shape + Phase 35 pattern matching.

### Claude's Discretion

- L-system alphabet style (Symbol-based per REQ vs turtle-graphics-string per research)
- Cellular automata initial-seed pattern (classic single-1-center vs random density vs hand-picked)
- Chaos-map quantization scale-arg type (`ScaleData` from Harmony module vs `Array[Note]` vs roman-numeral string vs new `Scale` type)
- Section-overload precedence rules when multiple patterns match same call site
- Rule-pack Dict shape contract details (composer-facing fields + their semantics)
- Whether `jam` raises charitable advisory or hard error when style + key are musically incompatible
- Exact plan breakdown — researcher / plan-checker decide how to slice 10-12 plans

### Deferred Ideas (OUT OF SCOPE)

- The other ~60 Tidal combinators (linger / swingBy / stutter / range / segment / etc.) — v1.6
- 2D L-systems with branching (`[` / `]` push/pop), parametric L-systems
- Higher-order Markov chains (variable-order, smoothed)
- ML-backed improvisation (MusicVAE / MusicTransformer / Magenta-tier) — explicit anti-feature
- Style-pack marketplace / registry
- Pattern guards on chord progressions for `jam`
- Retro-scope of named-arg rollout into a separate phase if scope blows past 12 plans (compose context is "write CONTEXT.md first, revisit only if scope explodes")

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PAT-01 | 12 Tidal-style combinators on `Sequence` composing via `->`; live in new `@patterns` stdlib module | §Standard Stack `@patterns` module structure + §Code Examples Pattern 1 + Combinator implementation table |
| PAT-02 | Combinator semantics typed on Flow's `Sequence`; failures (zero-length, divide-by-zero rate) charitably interpreted | §Common Pitfalls Pitfall 2 (charitable-interpretation discipline) + §Code Examples (stderr advisory pattern reuse) |
| GEN-01 | Markov chain primitive `(markov corpus order length seed)` — corpus Sequence or Array[Note]; order ∈ [1,3]; deterministic when seeded | §Architecture Patterns Pattern 2 (Markov train+generate split) + §Code Examples Markov reference impl |
| GEN-02 | L-system primitive `(lsystem axiom rules iterations)` — Symbol alphabet, dict rules, post-pass note mapping; Sequence output | §Architecture Patterns Pattern 3 (L-system alphabet pick — turtle-graphics-string fallback rejected) + §Code Examples |
| GEN-03 | Cellular automata `(cellular rule width steps seed)` 1D + `(life width height steps seed)` 2D | §Architecture Patterns Pattern 4 (CA seed-pattern pick — single-1-center default) + §Code Examples |
| GEN-04 | Chaos maps `(lorenz sigma rho beta length seed)` + `(logistic r length seed)` returning `Array[Double]`; quantization via `(quantizeToScale series scale)` | §Architecture Patterns Pattern 5 (chaos quantization scale-arg type pick — `ScaleData` from existing Harmony module) + §Common Pitfalls Pitfall 4 (Lorenz cross-platform FP) |
| GEN-05 | All GEN-* route through `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)`; unseeded reseed at renderSong/writeWav; two-run cmp-clean preserved | §Architecture Patterns Pattern 6 (PrngRegistry design) + §Common Pitfalls Pitfall 1 (determinism gate) + §Architecture System Diagram |
| SECT-01 | Parameterized sections `section verse(Note root, Int repeats) { ... }` + synthetic-frame param binding + closure over outer musical state preserved | §Architecture Patterns Pattern 7 (section overload via pattern dispatch on param list — full Phase 35 pattern AST reuse) + §Code Examples |
| IMPROV-01 | `(jam over=chords style=#bebop length=8bars seed=N)` chord-aware Markov; `#jazz` / `#blues` / `#classical` baseline packs | §Architecture Patterns Pattern 8 (jam composition: chord-tone weighting + Markov over corpus + rule-pack-driven articulation) + §Code Examples |

## Project Constraints (from CLAUDE.md)

These constrain every plan in Phase 36:

- **Ergonomics first.** Composer-facing surface is the primary design axis. Implementation strictness yields to ergonomics when they conflict (charitable interpretation discipline).
- **Genre-agnostic.** Improv rule packs (`#jazz` / `#blues` / `#classical`) must NOT bias the rest of Flow's surface; baseline packs are starting points, not opinions.
- **Pre-public-no-deprecation latitude ACTIVE** (D-v1.5-01). Single-commit breaking syntax changes are permitted (named-arg syntax addition; `MarkovModel` / `LsystemModel` type additions). In-repo migrators only — no `flow migrate` subcommand required.
- **Two-run cmp-clean determinism contract** (Phase 18/25/27/28/29/33 inheritance). Two consecutive runs at the same git SHA must produce byte-identical WAV+MIDI output for offline-render paths. The `live { ... }` block (Phase 38) is the only explicit opt-out; Phase 36 is NOT a live path.
- **RMS-windowed regression** (SPEC-8 ±0.5 dB / 100ms). For behavior changes that legitimately move bytes (none expected in Phase 36 — Phase 36 is additive), the baseline-comparison helper at `flow-lang.Tests/Helpers/RmsRegressionTests.AssertRmsWithinTolerance` is the gate.
- **No reflection-heavy additions.** Phase 41 WASM playground is on Mono-WASM jiterpreter; new reflection in v1.5 is rejected unless gated behind `[DynamicallyAccessedMembers]`. The named-arg backfill is direct list construction (not reflection over C# method signatures).
- **`tempo` / `timesig` / `key` / `swing` / `voicePool` / `tuning` are reserved context-block keywords.** Section names + parameter names must not collide; Phase 36 adds NO new context-block keywords.
- **Prefix-only arithmetic.** No infix `+ - * /`. All combinator implementations + generative formulas use `(add)` / `(sub)` / `(mul)` / `(div)` / `(neg)`.
- **GSD Workflow Enforcement.** Edits go through GSD commands; Phase 36 execution runs through `/gsd:execute-phase 36`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Lexer recognizes `Identifier=` in call position (named-arg syntax) | Lexing (`SimpleLexer.cs`) | TokenType | Named-arg is a parser-level construct, but the lexer's two-token peek is the cheapest disambiguator |
| Parser recognizes named args in `FunctionCallExpression` | Parsing (`Parser.cs` argument list) | AST (`FunctionCallExpression` carries `Dictionary<string, Expression>? NamedArgs`) | Parser collects positional args first, then named args; AST node knows both shapes |
| Named-arg resolution against signature param names | TypeSystem (`OverloadResolver` extension + `FunctionSignature.ParameterNames` defaulted field) | StandardLibrary (backfill ~211 registry calls) | Resolver is the single dispatch authority; signature carries param names so resolver can match `name=val` to the right slot |
| `@patterns` module — 13 combinators | StandardLibrary (new `Patterns/PatternFunctions.cs`) | Runtime (`SequenceData` extension) | Combinators are pure functions over `SequenceData` + bar metadata; module file `flow-lang/patterns.flow` declares the names + types |
| `@generative` module — 5 primitives + 2 split models | StandardLibrary (new `Generative/MarkovFunctions.cs`, `LsystemFunctions.cs`, `CellularFunctions.cs`, `ChaosFunctions.cs`) | Runtime (`MarkovModel`, `LsystemModel` reference types) | Each primitive is a pure stateless function; the model types are reference-identity wrappers carrying immutable transition tables |
| `Runtime/PrngRegistry` — `(SourceLocation, name)` → seeded `Random` | Runtime (`PrngRegistry.cs`) | ExecutionContext (existing `GetRand` extended to delegate) | Single source of truth for all stochastic primitives; reseeded at `renderSong`/`writeWav` boundary |
| Parameterized sections (SECT-01) | AST (`SectionDeclaration.Parameters: IReadOnlyList<Pattern>`) | Parsing (`ParseSectionDeclaration` extended) + Interpreter (synthetic frame push/pop on call site) | Reuses Phase 35 Pattern AST verbatim — guards, tuple destructure, constructor patterns. Resolver handles overload at call time |
| Section-call syntax inside Song expressions | Parsing (`ParseSongExpression` recognizes `Identifier(args...)` and `Identifier(args)*N`) | AST (`SectionCall` node carries name + positional args + repeat count) | Distinct from `FunctionCallExpression` — section calls live inside the SongExpression element list, not in general expression context |
| `@improv` module — `jam` + rule packs | StandardLibrary (new `Improv/JamFunctions.cs` + `Improv/StyleRegistry.cs`) | flow-lang/improv/styles/*.flow + ~/.config/flow/styles/*.flow | Pack loader scans both dirs at FlowEngine init; `(registerStyle #name dict)` builtin is the registration hook |
| Section / `jam` `key=` override mechanism | Runtime (`MusicalContext` push/pop) | Interpreter (synthetic frame on call site, popped after) | Same shape as Phase 32 `tuning t { ... }` block and Phase 28 `voicePool 32 { ... }` block |
| Combinator chain ergonomics | Parsing (existing `->` parse-time transform + Phase 35 `as` chain naming) | — | Composers naturally write `seq -> (every 4 (fn s => (fast s 2))) as varied -> render` — no new operator |
| `MarkovModel` / `LsystemModel` type registration | TypeSystem (new `SpecialTypes/MarkovModelType.cs`, `LsystemModelType.cs`) | Value (`Value.MarkovModel(...)` factory) | Reference-identity types follow the Phase 32 `Tuning` precedent and Phase 33 `Sfz` precedent — same shape, same equality discipline |

## Standard Stack

### Core (existing — Phase 36 is internal stdlib expansion)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Already in use across flow-lang/flow-interpreter/flow-cli/flow-lsp. [VERIFIED: Phase 35 verification] |
| C# 13 | Latest | Language | Records + switch expressions used throughout. [VERIFIED: Phase 35 verification] |
| xUnit.v3 | 3.2.2 | Unit tests | Existing `flow-lang.Tests/` infrastructure. [VERIFIED: 1364/1426 GREEN baseline post-Phase-35] |

No new external NuGet packages. Phase 36 is purely internal additive expansion.

### Supporting (existing — Phase 36 reuses each verbatim)

| Component | Location | Reuse Plan |
|-----------|----------|------------|
| `Ast/Patterns/{Pattern, LiteralPattern, WildcardPattern, BindingPattern, ConstructorPattern, GuardPattern, MatchArm}.cs` | Phase 35 Plan 35-05 | Section params (D-36-17) reuse the whole family verbatim; section-overload resolution (D-36-18) is pattern dispatch on the param list. [VERIFIED: 35-VERIFICATION.md confirms each file] |
| `Interpreter/PatternMatcher.cs` | Phase 35 Plan 35-05/06 | Naive linear scan over arms — extended to take a `Value[]` array (param tuple) instead of single scrutinee for section dispatch. Or wrapped via a virtual tuple. [CITED: 35-05-SUMMARY.md] |
| `ConstructorPattern.IsChordLiteral` / `IsRomanNumeral` / `IsArticulationSymbol` discriminator flags | Phase 35 Plan 35-06 | Section params can match `section pivot(Cmaj7) { ... }` and `section transposed(<<root, offset>>) { ... }` directly via these flags. [VERIFIED: 35-06-SUMMARY.md] |
| `Diagnostics/DiagnosticRenderer` + `LevenshteinHelper` | Phase 35 Plan 35-03 | D-36-16 arity/type mismatches render via the Rust-style multi-line renderer; "did you mean?" suggestions fall out of the existing Levenshtein helper. [VERIFIED: 35-03-SUMMARY.md] |
| `Lexing/PragmaSet` + `PragmaRegistry.KnownPragmas` | Phase 21 + Phase 35 | If we need any per-file pragma toggles for Phase 36 (e.g., a stricter section-arity-check pragma), the shape is locked. NOT expected to need any in Phase 36, but the surface is there. |
| `Runtime/MusicalContext` push/pop stack | Phase 23/28/32 inheritance | Section parameter binding and `jam` `key=` override push synthetic frames; popped on return. Identical to Phase 32's `tuning t { ... }` mechanism. [VERIFIED: MusicalContext.cs Tempo/Key/etc. carrier fields] |
| `Runtime/ExecutionContext.SnapshotState / RestoreState` | Phase 35 Plan 35-04 | New PrngRegistry surface must be added to the 11-surface snapshot/restore list. [VERIFIED: ExecutionContext.cs lines 519-555] |
| `Lexing/SimpleLexer.TryLexSignedNumber` expression-start set | Phase 35 Plan 35-05 | Adding `=` (Assign) to the named-arg context may require touching this set — check whether `name=-5` should lex `-5` as a signed literal. **Open Question 4 below.** |
| `TypeSystem/OverloadResolver.Resolve` | Existing | Extended to accept `IReadOnlyList<string>? namedArgs` and match against `FunctionSignature.ParameterNames` when present. [VERIFIED: OverloadResolver.cs lines 22-83] |
| `StandardLibrary/InternalFunctionRegistry.Register` | Existing | The 211 call sites in BuiltInFunctions.cs (`grep -c "registry.Register"` confirms) gain optional param-names argument. [VERIFIED: BuiltInFunctions.cs] |
| `StandardLibrary/Composition/VariationFunctions` | Existing seeded `vary()` | PRNG threading idiom for Phase 36 — same `Random(seed)` shape, but constructed by PrngRegistry from the source-location key instead of caller-passed seed. [VERIFIED: VariationFunctions.cs lines 60-104] |
| `StandardLibrary/Harmony/{ChordParser, ScaleDatabase, HarmonyFunctions}` | Existing | `jam` reads chord tones from `ChordParser.Parse(chord).Notes`; rule packs reference scales by name resolved via `ScaleDatabase`. Chord-aware Markov uses `ChordData.Quality` to weight chord-tone vs scale-tone choices. [VERIFIED: Harmony/ directory listing] |
| `StandardLibrary/Audio/SongRenderer` | Existing | Section calls (with positional args, defaults, `*N` repeat) hand off to existing SongRenderer voice/section dispatch. Synthetic MusicalContext frame push/pop is the integration mechanism. |
| `StandardLibrary/Audio/BarRenderer` (the bar layout) | Existing (Phase 28) | Cycle-dependent combinators (`every`, `chunk`, `phase`) read bar-aligned positions from the existing bar layout. D-36-04 cycle unit = bars. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-roll Markov / L-system / CA / Lorenz | NumSharp / MathNet.Numerics for chaos maps | Reject. Pure math primitives, ~50-150 LOC each, no library justifies the dep weight. Same conclusion as STACK.md research §Hand-Roll: Markov / L-system / CA / Lorenz. |
| ScaleData type for chaos quantization | `Array[Note]` literal | `ScaleData` is already shipped via Harmony module (`ScaleDatabase.GetScaleNotes`); reusing it gives composers `(quantizeToScale series cmajor)` for free without inventing a new type. **Picked.** Array[Note] is the fallback if scale isn't preregistered. |
| Turtle-graphics string L-system alphabet (`"F+F-F"`) | Symbol-array L-system (`<<#A, #+, #A>>`) | REQUIREMENTS.md GEN-02 says Symbol-based; research/FEATURES.md suggested turtle-graphics-string. Composer left to researcher discretion via D-36-08. **Picked Symbol-based** — type-safe, composes with Phase 26.1 tuples for rule values, matches REQ wording; turtle-graphics is a runtime ergonomic shortcut composers can build atop. |
| Reflection-based parameter-name extraction for named-arg backfill | Explicit `ParameterNames` arg on each Register call | Reject reflection per CLAUDE.md WASM-readiness constraint. Explicit param-names list is mechanical-but-trim — adds ~211 list constructions across BuiltInFunctions.cs and friends. |
| Pre-Phase 35 separate AST family for section parameters | Reuse Phase 35 Pattern AST verbatim | D-36-17 explicitly says reuse — section params ARE Phase 35 patterns. **Picked.** |
| Section-overload precedence by source-order (first-declared wins) | Specificity scoring like OverloadResolver | OverloadResolver already does specificity scoring (+1000/+500/+100) and ambiguity rejection. Reusing it for sections lets composers reason about section dispatch via the SAME rules they already know from function overloads. **Picked specificity scoring.** Section-overload precedence rules in §Architecture Patterns Pattern 7 below. |
| Linear `Random.NextDouble()` chord-tone vs scale-tone weighting in `jam` | Rule-pack-driven weighted tables | D-36-12 specifies Flow-file rule packs as composer-editable musical content — weights live in the pack, not in C#. **Picked rule-pack-driven.** |

### Installation

No new packages. All changes are additive C# + new `.flow` stdlib files:

```text
# New stdlib modules
flow-lang/patterns.flow              # @patterns — re-exports the 13 combinators with type sigs
flow-lang/generative.flow            # @generative — re-exports markov / lsystem / cellular / lorenz / logistic
flow-lang/improv.flow                # @improv — re-exports jam + registerStyle + style discovery
flow-lang/improv/styles/jazz.flow    # baseline rule pack
flow-lang/improv/styles/blues.flow
flow-lang/improv/styles/classical.flow

# New C# under StandardLibrary
flow-lang/StandardLibrary/Patterns/PatternFunctions.cs        # 13 combinators
flow-lang/StandardLibrary/Generative/MarkovFunctions.cs       # markov + markovTrain + markovGenerate
flow-lang/StandardLibrary/Generative/LsystemFunctions.cs      # lsystem + lsystemModel + lsystemGenerate
flow-lang/StandardLibrary/Generative/CellularFunctions.cs     # cellular + life
flow-lang/StandardLibrary/Generative/ChaosFunctions.cs        # lorenz + logistic + quantizeToScale
flow-lang/StandardLibrary/Improv/JamFunctions.cs              # jam
flow-lang/StandardLibrary/Improv/StyleRegistry.cs             # registerStyle + style pack discovery

# New Runtime
flow-lang/Runtime/PrngRegistry.cs                              # (SourceLocation, name) keyed PRNGs
flow-lang/Runtime/MarkovModelData.cs                           # MarkovModel reference type
flow-lang/Runtime/LsystemModelData.cs                          # LsystemModel reference type

# New TypeSystem
flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs           # Type singleton
flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs

# New AST extension fields (additive defaulted params, NOT new node types)
# - FunctionCallExpression.NamedArgs?: Dictionary<string, Expression>
# - FunctionSignature.ParameterNames?: IReadOnlyList<string>
# - SectionDeclaration.Parameters?: IReadOnlyList<Pattern>
# - SectionDeclaration.DefaultValues?: IReadOnlyList<Expression?>

# New token type
# - TokenType.NamedArgIdentifier OR (more likely) inline disambiguation via lexer two-token peek
```

**Version verification:** Every package referenced is existing in the repo's package graph; no new packages.

```bash
# No new NuGet adds
dotnet list flow-lang/flow-lang.csproj package | grep -E "(Pidgin|Melanchall|xunit)"  # baseline check only
```

## Package Legitimacy Audit

> Required when external packages install. Phase 36 installs NO new packages.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| — | — | — | — | — | — | N/A — zero new external dependencies in Phase 36 |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*slopcheck was UNAVAILABLE at research time, but the audit is moot — Phase 36 adds no NuGet/pip/cargo dependencies. All work is internal C# + new `.flow` stdlib files within the existing `flow-lang` solution.*

## Architecture Patterns

### System Architecture Diagram

```text
                    ┌─────────────────────────────────────────────────┐
                    │   Composer-facing surface (Flow source code)    │
                    │  - (every 4 (fn s => (fast s 2)) seq) -> render │
                    │  - (markov corpus 2 16 seed)                    │
                    │  - section verse(Note root, Int repeats) {...}  │
                    │  - (jam over=chords style=#jazz length=8)       │
                    │  - (fn arg1=val1 arg2=val2 ...)  ← named args   │
                    └─────────────────────┬───────────────────────────┘
                                          │
                                          ▼
        ┌───────────────────────────────────────────────────────────────┐
        │  Lexer/Parser pipeline (Phase 36 extensions in BOLD)          │
        │  - SimpleLexer: detect `Identifier=` 2-token peek (BOLD)      │
        │  - Parser: FunctionCallExpression.NamedArgs population (BOLD) │
        │  - Parser: SectionDeclaration.Parameters via Phase 35         │
        │            Pattern AST (BOLD)                                 │
        │  - Parser: SongExpression recognizes verse(args)*N (BOLD)     │
        └───────────────────────┬───────────────────────────────────────┘
                                │
                                ▼
        ┌───────────────────────────────────────────────────────────────┐
        │  OverloadResolver (Phase 36 extension in BOLD)                │
        │  - Existing: positional arg type-matching + specificity score │
        │  - NEW: named-arg matching against signature.ParameterNames   │
        │  - NEW: section overload as Pattern dispatch on param tuple   │
        └───────┬───────────────────────────────────────────────────────┘
                │ (resolved signature + bound arg names)
                ▼
        ┌──────────────────────────────────────────────────────────────┐
        │  ExpressionEvaluator dispatch                                │
        │  - existing: function call eval                              │
        │  - existing: pattern-match arm body                          │
        │  - NEW: section call → push synthetic MusicalContext frame   │
        │  - NEW: jam key= → push synthetic frame                      │
        └───────┬──────────────────────────────────────────────────────┘
                │
                ▼
        ┌──────────────────────────────────────────────────────────────┐
        │  StandardLibrary (Phase 36 new modules in BOLD)              │
        │  - existing: Transforms, Harmony, Audio, Composition         │
        │  - **NEW: Patterns/PatternFunctions.cs (13 combinators)**    │
        │  - **NEW: Generative/{Markov,Lsystem,Cellular,Chaos}**       │
        │  - **NEW: Improv/{JamFunctions, StyleRegistry}**             │
        └───────┬──────────────────────────────────────────────────────┘
                │ (every stochastic call requests PRNG)
                ▼
        ┌──────────────────────────────────────────────────────────────┐
        │  Runtime/PrngRegistry (Phase 36 NEW)                         │
        │  - Key: (SourceLocation.FilePath, Line, Col, name)           │
        │  - Reseed: at renderSong / writeWav entry → all unseeded     │
        │    requests get fresh deterministic seeds derived from key   │
        │  - Snapshot: included in TestSnapshot for hermetic isolation │
        └───────┬──────────────────────────────────────────────────────┘
                │
                ▼
        ┌──────────────────────────────────────────────────────────────┐
        │  ExecutionContext + MusicalContext stack (existing)          │
        │  - Phase 28 voicePool, Phase 32 tuning, Phase 36 section     │
        │    params + jam key= override all push/pop synthetic frames  │
        └──────────────────────────────────────────────────────────────┘
```

Data flow trace for the canonical `(jam over=chords style=#jazz seed=42)` call:

1. **Composer source:** `(jam over=chords style=#jazz seed=42)` in user .flow file.
2. **Lexer** emits `LParen`, `Identifier("jam")`, `Identifier("over")`, `Assign`, `Identifier("chords")`, `Identifier("style")`, `Assign`, `SymbolLiteral("jazz")`, `Identifier("seed")`, `Assign`, `IntLiteral(42)`, `RParen`. Detection of `Identifier=` is a 2-token peek in the parser (NOT a new TokenType — keeps lexer state simple).
3. **Parser** builds `FunctionCallExpression("jam", positional=[], named={"over":VarExpr("chords"), "style":SymbolLit("jazz"), "seed":IntLit(42)})`.
4. **OverloadResolver** matches against `jam`'s registered signature `(jam: over=Sequence, style=Symbol=#jazz, length=Int=8, key=String?=null, seed=Int?=null, order=Int=2)`. Named args bind by name; missing optional args fill defaults.
5. **ExpressionEvaluator** evaluates positional + named args. If `key=` non-null, push synthetic `MusicalContext{Key=...}` frame.
6. **Improv.JamFunctions.Jam** dispatches: resolve `#jazz` from `StyleRegistry`; for each bar, walk `over` chord progression; for each beat, weighted-sample note from chord-tone-vs-scale-tone distribution per rule pack; PRNG drawn from `PrngRegistry.GetRandom((srcLoc, "jam"))`.
7. **MusicalContext frame** popped after expression eval.
8. Result: `Sequence` of melodic notes; composer continues with `-> render` or stores via `as`.

### Recommended Project Structure

```text
flow-lang/
├── Ast/
│   ├── Expressions/
│   │   ├── FunctionCallExpression.cs    # +NamedArgs?: Dict<string, Expression>
│   │   └── SongExpression.cs            # element list now includes SectionCallElement
│   ├── Statements/
│   │   └── SectionDeclaration.cs        # +Parameters?: IReadOnlyList<Pattern>
│   │                                    # +DefaultValues?: IReadOnlyList<Expression?>
│   ├── Patterns/ (Phase 35 — REUSED)
│   └── (new) Elements/SectionCallElement.cs   # for SongExpression item with args+repeat
├── Runtime/
│   ├── PrngRegistry.cs                   # NEW — (SrcLoc, name) → seeded Random
│   ├── MarkovModelData.cs                # NEW — reference-identity model
│   ├── LsystemModelData.cs               # NEW — reference-identity model
│   └── ExecutionContext.cs               # extended Snapshot/Restore to include PrngRegistry
├── TypeSystem/
│   ├── FunctionSignature.cs              # +ParameterNames?: IReadOnlyList<string>
│   ├── OverloadResolver.cs               # extended for named-arg matching
│   └── SpecialTypes/
│       ├── MarkovModelType.cs            # NEW
│       └── LsystemModelType.cs           # NEW
├── Parsing/
│   └── Parser.cs                         # named-arg recognition; section param parsing
├── Lexing/
│   └── SimpleLexer.cs                    # no new TokenType; 2-token peek in parser
├── Interpreter/
│   ├── ExpressionEvaluator.cs            # section call frame push/pop; jam key= override
│   └── PatternMatcher.cs                 # extended for tuple-of-args matching
├── StandardLibrary/
│   ├── BuiltInFunctions.cs               # 211 Register sites gain param-names arg (split across 2 plans)
│   ├── Patterns/ (NEW)
│   │   └── PatternFunctions.cs           # every/fast/slow/chunk/phase/rev/jux/sometimes/
│   │                                     # iter/palindrome/degrade/superimpose + sparseSeq
│   ├── Generative/ (NEW)
│   │   ├── MarkovFunctions.cs            # markov, markovTrain, markovGenerate
│   │   ├── LsystemFunctions.cs           # lsystem, lsystemModel, lsystemGenerate
│   │   ├── CellularFunctions.cs          # cellular, life
│   │   └── ChaosFunctions.cs             # lorenz, logistic, quantizeToScale
│   └── Improv/ (NEW)
│       ├── JamFunctions.cs               # jam (chord-aware Markov)
│       └── StyleRegistry.cs              # registerStyle + scan flow-lang/improv/styles + ~/.config/flow/styles
├── patterns.flow                          # NEW — @patterns stdlib module
├── generative.flow                        # NEW — @generative stdlib module
├── improv.flow                            # NEW — @improv stdlib module
└── improv/styles/
    ├── jazz.flow                          # NEW — baseline rule pack
    ├── blues.flow                         # NEW
    └── classical.flow                     # NEW

examples/
├── generative/
│   ├── markov_jazz.flow                   # NEW — tutorial: one-shot + train/generate + jam
│   └── tidal_combinators.flow             # NEW — all 12 combinators + sparseSeq
└── sections/
    └── parameterized.flow                 # NEW — section verse(Note root, Int repeats)

tests/
├── test_patterns_every.flow               # NEW — PAT-01 every combinator
├── test_patterns_chain.flow               # NEW — PAT-01 composition via ->
├── test_markov_oneshot.flow               # NEW — GEN-01 one-shot
├── test_markov_train_generate.flow        # NEW — GEN-01 split
├── test_lsystem_oneshot.flow              # NEW — GEN-02
├── test_cellular_rule30.flow              # NEW — GEN-03 1D
├── test_lorenz_quantize.flow              # NEW — GEN-04
├── test_section_params.flow               # NEW — SECT-01 basic
├── test_section_overload.flow             # NEW — SECT-01 overload via pattern dispatch
├── test_section_pattern_destructure.flow  # NEW — SECT-01 + Phase 35 patterns
├── test_jam_jazz.flow                     # NEW — IMPROV-01 baseline
├── test_jam_key_override.flow             # NEW — IMPROV-01 key= override
├── test_named_args.flow                   # NEW — D-36-11 universal named args
└── test_prng_determinism.flow             # NEW — GEN-05 two-run cmp-clean
```

### Pattern 1: Tidal Combinator Skeleton (PAT-01)

**What:** Each of the 13 combinators is a pure `Sequence → Sequence` (or `Sequence → ... → Sequence` for multi-arg) function. Cycle-dependent combinators (every / chunk / phase) read bar-aligned positions from the existing `BarRenderer` layout.

**When to use:** Every Phase 36 combinator follows this shape. The lambda-required transform-arg style (D-36-03) means no currying — the lambda is a first-class `Function`-typed Value.

**Example (`every`):**

```csharp
// Source: StandardLibrary/Transforms/TransformFunctions.cs:50-73 (legato/portamento pattern)
public static class PatternFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        // every(Int n, Function fn, Sequence seq) → Sequence
        var everySig = new FunctionSignature(
            "every",
            [IntType.Instance, FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["n", "fn", "seq"]);  // D-36-11 named args

        registry.Register("every", everySig, args =>
        {
            int n = args[0].As<int>();
            var fn = args[1].As<FunctionValue>();
            var seq = args[2].As<SequenceData>();

            if (n <= 0)
            {
                RenderingDiagnostics.WarnOnce($"every:invalid-n:{srcLoc}",
                    "every: cycle count must be positive; sequence unchanged");
                return Value.Sequence(seq);  // charitable per PAT-02
            }
            if (seq.Bars.Count == 0)
            {
                RenderingDiagnostics.WarnOnce($"every:empty:{srcLoc}",
                    "every: empty sequence; unchanged");
                return Value.Sequence(seq);
            }

            // Apply fn to bars 0, N, 2N, ...; leave others unchanged
            var newBars = new List<BarData>();
            for (int i = 0; i < seq.Bars.Count; i++)
            {
                if (i % n == 0)
                {
                    var subSeq = new SequenceData();
                    subSeq.AddBar(seq.Bars[i]);
                    var transformed = InvokeLambda(fn, [Value.Sequence(subSeq)]);
                    foreach (var bar in transformed.As<SequenceData>().Bars)
                        newBars.Add(bar);
                }
                else
                {
                    newBars.Add(seq.Bars[i]);
                }
            }

            return Value.Sequence(new SequenceData(newBars));
        });
    }
}
```

**Combinator Implementation Table:**

| Combinator | Signature | Behavior | PRNG? |
|------------|-----------|----------|-------|
| `every` | `(Int n, Fn, Sequence) → Sequence` | Apply fn to bars 0, N, 2N, ... | no |
| `fast` | `(Sequence, Double factor) → Sequence` | Halve durations × `factor` per note; bars stay | no |
| `slow` | `(Sequence, Double factor) → Sequence` | Inverse of `fast`; multiply durations | no |
| `chunk` | `(Int n, Fn, Sequence) → Sequence` | Divide into N bar-aligned chunks; apply fn to one chunk per cycle (rotating) | no |
| `phase` | `(Double offset, Sequence) → Sequence` | Rotate sequence by `offset × barCount` bars (e.g., 0.25 = 1/4 rotation) | no |
| `rev` | `(Sequence) → Sequence` | Reverse note order (mirror of existing `retrograde`) | no |
| `jux` | `(Fn, Sequence) → Sequence` | Stereo split — left channel is original; right is `fn(seq)` (composes into stereo via existing Pan when Phase 37 ships; v1.5 ships as voice-block merge) | no (deterministic fn) |
| `sometimes` | `(Double prob, Fn, Sequence) → Sequence` AND `(Fn, Sequence) → Sequence` | Apply fn to each bar with probability `prob` (default 0.5) | **yes** |
| `iter` | `(Int n, Sequence) → Sequence` | Cycle a rotation of N steps per bar | no |
| `palindrome` | `(Sequence) → Sequence` | Concatenate seq with rev(seq); period doubles | no |
| `degrade` | `(Sequence) → Sequence` | Drop each bar with fixed 50% probability (Tidal compat) | **yes** |
| `sparseSeq` | `(Double prob, Sequence) → Sequence` | Drop each bar with composer-specified prob (Flow-native) | **yes** |
| `superimpose` | `(Fn, Sequence) → Sequence` | Layer original + fn(seq) as a voice block | no |

### Pattern 2: Markov Train+Generate Split (GEN-01 / D-36-06)

**What:** Markov ships in two shapes — one-shot exploration (`(markov corpus 2 16 seed)`) and reusable model (`(markovTrain corpus 2)` returns `MarkovModel`; `(markovGenerate model 16 seed)` consumes it).

**When to use:** Composers exploring quickly call one-shot; composers writing a piece that reuses a trained model call train once, generate many.

**MarkovModel data shape:**

```csharp
// Runtime/MarkovModelData.cs
public class MarkovModelData
{
    public int Order { get; }
    public IReadOnlyDictionary<ImmutableArray<int>, IReadOnlyList<(int State, double Weight)>> Transitions { get; }
    public IReadOnlyList<int> StateAlphabet { get; }  // MIDI pitches or tuple-encoded states
    public string FeatureMode { get; }  // "pitch" | "pitch+duration" etc. per D-36-07
    // Reference-identity equality (NO records, NO structural equality)
}

// TypeSystem/SpecialTypes/MarkovModelType.cs
public sealed class MarkovModelType : FlowType
{
    public static readonly MarkovModelType Instance = new();
    public override string Name => "MarkovModel";
    public override int GetSpecificity() => 148;  // unique among music types, post Tuning(146) / Sfz(147)
    public override bool IsCompatibleWith(FlowType other) => other is MarkovModelType;
    public override bool CanConvertTo(FlowType other) => other is MarkovModelType;
}
```

**Example (markovTrain + markovGenerate):**

```csharp
// MarkovFunctions.cs
registry.Register("markovTrain",
    new FunctionSignature("markovTrain",
        [SequenceType.Instance, IntType.Instance],
        ParameterNames: ["corpus", "order"]),
    args =>
    {
        var corpus = args[0].As<SequenceData>();
        int order = Math.Clamp(args[1].As<int>(), 1, 3);  // GEN-01 range
        var model = TrainMarkov(corpus, order, featureMode: "pitch");
        return Value.MarkovModel(model);
    });

registry.Register("markovGenerate",
    new FunctionSignature("markovGenerate",
        [MarkovModelType.Instance, IntType.Instance, IntType.Instance],
        ParameterNames: ["model", "length", "seed"]),
    args =>
    {
        var model = args[0].As<MarkovModelData>();
        int length = args[1].As<int>();
        int seed = args[2].As<int>();
        var rng = new Random(seed);
        return Value.Sequence(GenerateMarkov(model, length, rng));
    });

// Unseeded overload routes through PrngRegistry
registry.Register("markovGenerate",
    new FunctionSignature("markovGenerate",
        [MarkovModelType.Instance, IntType.Instance],
        ParameterNames: ["model", "length"]),
    args =>
    {
        var model = args[0].As<MarkovModelData>();
        int length = args[1].As<int>();
        // PrngRegistry returns a deterministic Random keyed by (callSite.Span, "markovGenerate")
        // Reseeded at renderSong/writeWav boundary so two-run cmp-clean holds.
        var rng = _context.PrngRegistry.GetRandom(callSite.Span, "markovGenerate");
        return Value.Sequence(GenerateMarkov(model, length, rng));
    });
```

**Same shape for L-system per D-36-06** (`lsystem` one-shot, `lsystemModel` + `lsystemGenerate` split).

### Pattern 3: L-System with Symbol Alphabet (GEN-02 — Claude's Discretion picked Symbol-based)

**What:** L-system uses Phase 26.1 `Symbol` alphabet (`#A`, `#B`, `#+`, `#-`) instead of turtle-graphics strings. Rules are a `Dict<Symbol, Array[Symbol]>` (or `Dict<Symbol, Sequence>` — picked tuple of symbols for type clarity).

**Rationale for Symbol pick (D-36-08 Claude's Discretion):** Symbols are type-safe, compose with Phase 26.1 tuples, and match REQUIREMENTS.md GEN-02 wording verbatim. Turtle-graphics strings are a runtime ergonomic shortcut composers can BUILD on top of the Symbol-based core via their own `(stringToSymbols "F+F-F")` helper. Symbol-based is the lower foundation.

**Example:**

```flow
# Composer surface — Algae growth (Lindenmayer's canonical example)
use "@generative"

Dict<Symbol, Array[Symbol]> rules = (dict
  #A <<#A, #B>>
  #B <<#A>>)

Array[Symbol] result = (lsystem #A rules 4)  # → <<#A, #B, #A, #A, #B, #A, #B, #A>>

# Post-pass: map symbols to notes via composer-provided function
Sequence seq = (lsystemToSequence result
  (fn Symbol s =>
    (if (eq s #A) C4 (if (eq s #B) E4 _))))
```

**Alphabet rule:** Symbol body must be a valid Phase 26.1 Symbol literal (interned). Any Symbol may serve as an alphabet member. Reserved meta-symbols like `#+` / `#-` / `#[` / `#]` are NOT special-cased by the L-system core — they're just Symbols the composer's post-pass interprets if desired (preserves 2D-branching extensibility for v1.6 without locking semantics in v1.5).

### Pattern 4: Cellular Automata Seed (GEN-03 — Claude's Discretion picked single-1-center default)

**Rationale for single-1-center pick (D-36-08 Claude's Discretion):** Classic Wolfram convention — best surfaces the "interesting" emergent patterns from rules 30 / 90 / 110 / 184. Random density and hand-picked are useful for advanced composers but the DEFAULT must showcase the rule's behavior.

**Surface:**

```csharp
// (cellular rule width steps seed) — default seed = single 1 at center
// (cellularSeeded rule width steps seed initialPattern) — explicit Array[Bool] seed
registry.Register("cellular",
    new FunctionSignature("cellular",
        [IntType.Instance, IntType.Instance, IntType.Instance, IntType.Instance],
        ParameterNames: ["rule", "width", "steps", "seed"]),
    args =>
    {
        int rule = args[0].As<int>();
        int width = args[1].As<int>();
        int steps = args[2].As<int>();
        int seed = args[3].As<int>();

        // Default seed pattern: single 1 at center
        var initial = new bool[width];
        initial[width / 2] = true;

        var grid = RunElementaryCa(rule, initial, steps);
        return Value.Sequence(CaGridToSequence(grid));
    });
```

For 2D Game of Life (`life`), default seed is composer-supplied via the `seed` int (used to seed a `Random` that fills the grid at 30% density). NO single canonical 2D seed exists; deterministic random fill is the right baseline.

### Pattern 5: Chaos Map Quantization (GEN-04 — Claude's Discretion picked `ScaleData`)

**Rationale for `ScaleData` pick (D-36-08 Claude's Discretion):** Already shipped via Harmony module (`ScaleDatabase.GetScaleNotes(key)`). Reusing gives composers `(quantizeToScale lorenzX cmajor)` — zero new type to learn. The fallback path for arbitrary scales is to accept `Array[Note]` directly via overload.

**Surface:**

```csharp
// (lorenz sigma rho beta length seed) → Array[Double] (x-axis only by default)
registry.Register("lorenz",
    new FunctionSignature("lorenz",
        [DoubleType.Instance, DoubleType.Instance, DoubleType.Instance, IntType.Instance, IntType.Instance],
        ParameterNames: ["sigma", "rho", "beta", "length", "seed"]),
    args =>
    {
        double sigma = args[0].As<double>();
        double rho = args[1].As<double>();
        double beta = args[2].As<double>();
        int length = args[3].As<int>();
        int seed = args[4].As<int>();
        // Phase 36 D-36-09: Lorenz initial conditions derived from seed (deterministic).
        // Lorenz cross-platform FP divergence documented as platform-specific limitation.
        return Value.Array(RunLorenz(sigma, rho, beta, length, seed));
    });

// quantizeToScale overloads
registry.Register("quantizeToScale",
    new FunctionSignature("quantizeToScale",
        [ArrayType.Of(DoubleType.Instance), StringType.Instance],
        ParameterNames: ["series", "scaleName"]),  // "cmajor", "aminor", ...
    args => { /* lookup ScaleData via ScaleDatabase.GetScaleNotes(args[1]) and quantize */ });

registry.Register("quantizeToScale",
    new FunctionSignature("quantizeToScale",
        [ArrayType.Of(DoubleType.Instance), ArrayType.Of(NoteType.Instance)],
        ParameterNames: ["series", "scaleNotes"]),
    args => { /* direct Array[Note] scale */ });
```

**Default canonical Lorenz params** (research-validated): `σ=10, ρ=28, β=8/3` (the butterfly). Composer overrides via positional or named args.

### Pattern 6: PrngRegistry (GEN-05 / D-36-09)

**What:** Singleton-per-ExecutionContext registry keyed by `(SourceLocation, generator-name)` returning a deterministic `Random`. Reseeded at `renderSong` / `writeWav` boundary.

**Why:** Without this, every stochastic call must take an explicit `seed=` arg to preserve determinism. With it, composers write `(degrade seq)` and get deterministic-at-source-position behavior automatically. Composer can still pass `seed=N` to override.

```csharp
// Runtime/PrngRegistry.cs
public class PrngRegistry
{
    private readonly Dictionary<(SourceLocation Site, string Name), Random> _registry = new();
    private int _renderBoundarySalt = 0;  // bumped at renderSong/writeWav entry

    /// <summary>
    /// Returns a deterministic Random for the given call site + generator name.
    /// Same (site, name) returns the same Random across the SAME render pass —
    /// reseeded at the next renderSong/writeWav entry so subsequent renders
    /// don't accumulate PRNG state.
    /// </summary>
    public Random GetRandom(SourceLocation site, string name)
    {
        var key = (site, name);
        if (!_registry.TryGetValue(key, out var rng))
        {
            int seed = ComputeDeterministicSeed(site, name, _renderBoundarySalt);
            rng = new Random(seed);
            _registry[key] = rng;
        }
        return rng;
    }

    /// <summary>
    /// Called at renderSong/writeWav entry. Clears the cache so the next
    /// pass starts from fresh reseeded Randoms. Salt is currently zero; if we
    /// ever want "non-reproducible mode" (e.g., `live` blocks per D-v1.5-07),
    /// the salt becomes a non-deterministic input.
    /// </summary>
    public void ResetAtRenderBoundary()
    {
        _registry.Clear();
    }

    private static int ComputeDeterministicSeed(SourceLocation site, string name, int salt)
    {
        // Stable hash combining file path + line + col + name + salt.
        // C# string GetHashCode is randomized per process — DON'T use it.
        // Use a stable Fowler-Noll-Vo or simple FNV-1a variant.
        unchecked
        {
            uint hash = 2166136261;
            hash = (hash ^ (uint)(site.FilePath?.GetDeterministicHash() ?? 0)) * 16777619;
            hash = (hash ^ (uint)site.Line) * 16777619;
            hash = (hash ^ (uint)site.Column) * 16777619;
            hash = (hash ^ (uint)name.GetDeterministicHash()) * 16777619;
            hash = (hash ^ (uint)salt) * 16777619;
            return unchecked((int)hash);
        }
    }
}
```

**Integration points:**

- `ExecutionContext.PrngRegistry` — single instance per FlowEngine
- `FlowEngine.WriteWav` / `FlowEngine.RenderSong` / `AudioPlaybackManager.RenderForPlayback` entry — calls `PrngRegistry.ResetAtRenderBoundary()` first thing
- `TestSnapshot.PrngRegistryState` — captured/restored per Phase 35 hermetic isolation contract
- Existing `VariationFunctions.vary()` migrates to delegate through PrngRegistry when no explicit seed passed; preserves byte-identical output via the deterministic-seed-from-site scheme

**Compatibility with `VariationFunctions.vary()` seeded overloads:** Existing seeded overloads (sig3, sig4, sig6) pass `int seed` explicitly — they keep their current `new Random(seed)` path. Unseeded overloads (sig1, sig2, sig5) currently call `new Random()` (non-deterministic from wall-clock — a latent bug already!). PrngRegistry migration fixes this — they become `PrngRegistry.GetRandom(callSite, "vary")`. This is technically a behavior change, but the prior behavior was non-deterministic noise; the migration produces deterministic noise. Document in vary's plan comment.

### Pattern 7: Section Overload via Pattern Dispatch (SECT-01 / D-36-17 / D-36-18)

**What:** Section declarations carry an optional `Parameters: IReadOnlyList<Pattern>` list (reusing Phase 35 Pattern AST). At call time, OverloadResolver dispatches to the section whose param patterns match.

**Precedence rule (Claude's Discretion):** Reuse OverloadResolver's existing specificity scoring:
- Literal pattern: +1000 (most specific)
- Constructor pattern with music-aware extractor (chord/numeral/articulation): +800
- Tuple-destructure pattern with annotated types: +600
- Binding pattern with type annotation: +500
- Binding pattern (untyped): +200
- Wildcard `_`: +100 (least specific)

Section overload behaves like function overload — multiple matches resolved by sum of specificity scores; identical-score matches raise `Ambiguous section overload` error via DiagnosticRenderer.

**Example:**

```flow
# Multiple verse overloads with different parameter shapes
section verse(Note root) {
  # Single-note version — simple pivot
  | $root q $root q E4 q G4 q |
}

section verse(<<Note root, Int repeats>>) {
  # Tuple-destructure version — repeat with offset
  for i in (range 0 repeats) {
    | $root q E4 q G4 q B4 q |
  }
}

section verse(Cmaj7) {
  # Constructor-pattern match on chord literal — special verse for Cmaj7 context
  | C4 q E4 q G4 q B4 q | C5 q B4 q G4 q E4 q |
}

# Composer calls them:
Song song = [
  verse(C4)                    # → first overload (Note pattern, specificity 500)
  verse(<<D4, 3>>)             # → second overload (Tuple pattern, specificity 600)
  verse(Cmaj7)                 # → third overload (Constructor pattern w/ music-extractor, specificity 800)
]
```

**Parsing:** `ParseSectionDeclaration` extended to recognize `(Param, Param, ...)` after section name. Each Param parses as a Pattern (via Phase 35's `ParsePattern`) optionally followed by `= DefaultExpression`.

**Synthetic frame mechanism (existing — already used by `tuning t { ... }` and `voicePool 32 { ... }`):**

```csharp
// ExpressionEvaluator.EvaluateSectionCall (NEW)
private Value EvaluateSectionCall(SectionCallElement call)
{
    var section = _context.SectionRegistry[call.Name];  // resolve overload first
    var paramBindings = MatchPatternsAgainstArgs(section.Parameters, call.Args);

    var frame = new StackFrame(parent: _context.CurrentFrame);
    foreach (var (name, value) in paramBindings)
        frame.DeclareVariable(name, value);

    _context.PushFrame(frame);
    try
    {
        var result = ExecuteSectionBody(section.Body);
        return result;
    }
    finally
    {
        _context.PopFrame();
    }
}
```

### Pattern 8: `jam` API Composition (IMPROV-01 / D-36-10)

**What:** `jam` is a thin orchestrator combining four reusable pieces: (1) chord progression iteration, (2) rule-pack-driven note weighting, (3) chord-aware Markov over a corpus (or scale fallback when no corpus passed), (4) optional `key=` override via synthetic MusicalContext frame.

**Signature:** `jam(Sequence over, Symbol style=#jazz, Int length=8, String? key=null, Int? seed=null, Int order=2) → Sequence`

```csharp
// Improv/JamFunctions.cs
registry.Register("jam",
    new FunctionSignature("jam",
        [SequenceType.Instance, SymbolType.Instance, IntType.Instance,
         StringType.Instance, IntType.Instance, IntType.Instance],
        ParameterNames: ["over", "style", "length", "key", "seed", "order"],
        OptionalAfter: 1),  // only `over` is required
    args =>
    {
        var chords = args[0].As<SequenceData>();
        var styleSymbol = args[1].As<Value>();  // #jazz / #blues / #classical / composer's own
        int length = args[2].As<int>();
        string? keyOverride = args[3].As<string?>();
        int? seed = args[4].As<int?>();
        int order = Math.Clamp(args[5].As<int>(), 1, 3);

        // 1. Look up rule pack
        if (!_context.StyleRegistry.TryGet(styleSymbol, out var rulePack))
        {
            RenderingDiagnostics.WarnOnce($"jam:unknown-style:{styleSymbol}:{srcLoc}",
                $"jam: unknown style '{styleSymbol}'; falling back to #jazz");
            rulePack = _context.StyleRegistry.Get(Value.Symbol("jazz"));
        }

        // 2. Push key override if provided
        var frame = new StackFrame(parent: _context.CurrentFrame);
        if (keyOverride != null)
            frame.MusicalContext = new MusicalContext { Key = keyOverride };
        _context.PushFrame(frame);

        try
        {
            // 3. PRNG: explicit seed wins; otherwise PrngRegistry
            var rng = seed.HasValue
                ? new Random(seed.Value)
                : _context.PrngRegistry.GetRandom(srcLoc, "jam");

            // 4. For each bar in `length`, walk chord progression cyclically;
            //    weighted-sample notes per rule pack's chord-tone vs scale-tone distribution
            //    + apply Markov transitions per chord context.
            return Value.Sequence(GenerateJam(chords, rulePack, length, rng, order));
        }
        finally
        {
            _context.PopFrame();
        }
    });
```

**Rule-pack Dict shape contract (Claude's Discretion — D-36-12 details):**

```flow
# flow-lang/improv/styles/jazz.flow — registered at FlowEngine init
use "@improv"

Dict<Symbol, Value> jazzPack = (dict
  # 1. Chord-tone vs scale-tone bias on strong beats vs weak beats.
  #     Values in [0.0, 1.0]; chord_tone_weight + scale_tone_weight need not sum to 1
  #     (rule-pack normalizes internally).
  #beat_weights (dict
    #strong (dict #chord_tone 0.75  #scale_tone 0.20  #chromatic_passing 0.05)
    #weak   (dict #chord_tone 0.30  #scale_tone 0.50  #chromatic_passing 0.20))

  # 2. Interval-transition preferences — diatonic stepwise heavily preferred;
  #     chromatic passing tones allowed; large leaps discouraged unless resolving downward.
  #interval_transitions (dict
    #step_up 0.30      #step_down 0.30
    #leap_up 0.10      #leap_down 0.15
    #chromatic 0.10    #repeat 0.05)

  # 3. Rhythmic template — eighth-note swing-heavy
  #rhythmic_template <<e e e e e e e e>>  # Phase 26.1 tuple of NoteValues

  # 4. Articulation distribution — accents on offbeats per jazz convention
  #articulation_distribution (dict
    #downbeat #legato
    #offbeat  #accent
    #syncopated #marcato))

(registerStyle #jazz jazzPack)
```

This contract is documented in `flow-lang/improv/styles/README.md` (composer-facing). Composer adding a new style follows the same shape; missing fields fall back to charitable defaults (uniform weights, no articulation override).

**Style + key incompatibility (Claude's Discretion):** **Charitable advisory, NOT hard error** (matches Flow's broader posture per CLAUDE.md ergonomics-first goal). When `style=#blues` is requested under a chromatic / non-diatonic key context, `jam` emits a one-shot stderr advisory (`RenderingDiagnostics.WarnOnce` keyed `jam:style-key-mismatch:{style}:{key}:{srcLoc}`) and proceeds with best-effort — the rule pack's scale_tone weights will simply produce more chromatic passing tones than typical for that style. Composer chooses to read the advisory or ignore it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Function-call AST node | A new `NamedArgCallExpression` | Extend `FunctionCallExpression` with `NamedArgs?: Dict<string, Expression>` | Two call shapes for one concept fragments AST traversal; the optional-named-args field is the Phase 22 / Phase 35 defaulted-positional-param pattern |
| Section call AST | A general-purpose `Call(name, args)` node | New `SectionCallElement` inside SongExpression's element list | Section calls only appear in Song expression context; conflating with `FunctionCallExpression` invites ambiguity at parser dispatch |
| Section-overload dispatch logic | A separate `SectionResolver` | Extend existing `OverloadResolver` to accept Pattern lists | `OverloadResolver` already has specificity scoring + ambiguity rejection; sections become a second client |
| Roman-numeral resolution in patterns | Re-parse roman literals at match time | Reuse Phase 35 Plan 35-06 `MatchRomanNumeral` (which already does this against active key context) | Phase 35 already shipped this; Phase 36 section param patterns get it for free |
| Chord-quality extraction in `jam` | Walk ChordData fields manually | Reuse `ChordParser.Parse(chord).Notes` / `.Root` / `.Quality` | Already shipped; chord-tone vs scale-tone weighting reads `.Notes` directly |
| Markov transition tables | A new HashMap data structure | `Dictionary<ImmutableArray<int>, IReadOnlyList<(int, double)>>` | BCL types are sufficient; ImmutableArray is a record key |
| L-system rule storage | Custom record | Phase 26.1 `Dict<Symbol, Array[Symbol]>` (or `Dict<Symbol, Sequence>`) | Reusing dict surface keeps composer-facing API uniform |
| Lorenz integration step | NumSharp / MathNet.Numerics | Hand-rolled forward-Euler (~15 LOC) | Phase 36 STACK.md research locked hand-roll; library overkill for a 3-state ODE |
| Chord-aware Markov chord-tone test | `note in chord.Notes` linear scan | Same — but read from cached `ChordData.Notes` | Existing `Notes` field already enumerated; cache is tight |
| File-system discovery of style packs | Custom directory walker | Reuse existing `ModuleLoader` pattern (used by Phase 30 `~/.config/flow/config.toml` + Phase 33 SFZ root) | Same XDG-config conventions; same `Directory.GetFiles("*.flow")` shape |

**Key insight:** Phase 36 is almost entirely composition of existing surfaces. The new code is glue + 5 generative formulas. The temptation to invent new abstractions for "Tidal-ness" or "generative-ness" should be resisted — `Sequence → Sequence` functions don't need a new module type; they're just stdlib functions.

## Runtime State Inventory

> Phase 36 is a greenfield additive phase. No renames or migrations of existing data. Skipping the rename/refactor inventory but flagging the few touch-points that affect existing runtime state:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — Phase 36 introduces no new datastore | none |
| Live service config | None — no external service config | none |
| OS-registered state | None — no OS registrations | none |
| Secrets / env vars | None | none |
| Build artifacts | None — `dotnet build` regenerates everything; no egg-info / pkg-info equivalents | none |

**Latent state changes Phase 36 introduces (NOT migrations of existing data, but new runtime state):**

1. `ExecutionContext.PrngRegistry` — new field; included in `TestSnapshot` / `SnapshotState` / `RestoreState` per the Phase 35 11-surface contract (so per-test isolation works).
2. `ExecutionContext.StyleRegistry` — new field; populated at FlowEngine init from `flow-lang/improv/styles/*.flow` + `~/.config/flow/styles/*.flow`. Included in TestSnapshot.
3. Section overload bucket on `ExecutionContext.SectionRegistry` — current type is `Dictionary<string, SectionData>`; D-36-18 requires it to become `Dictionary<string, List<SectionData>>` to support multiple sections with the same name. Migration touch: existing single-section callers update to `[section]` list lookup with name-only dispatch when count == 1 (backward-compatible).

## Common Pitfalls

### Pitfall 1: Generative-primitive determinism break (CRITICAL — gates the entire phase)

**What goes wrong:** A composer writes `(degrade seq)` and gets different output on two consecutive runs because `degrade` constructs `new Random()` from wall-clock. CI's two-run cmp-clean diff explodes. The Phase 18/25/27/28/29/33 byte-identical inheritance contract breaks.

**Why it happens:** Stochastic primitives are tempting to write as "I just need a Random." Without an architectural force-function, every author reaches for `new Random()` and the determinism contract dies by a thousand papercuts.

**How to avoid:** PrngRegistry is the single source of truth. Plan 36-01 ships it; every subsequent Phase 36 plan that introduces stochasticity MUST route through it (verified by source-grep CI gate: `grep -r "new Random()" StandardLibrary/{Patterns,Generative,Improv}/ | wc -l` must equal 0). Composer-passed seeds construct explicit `Random(seed)` directly; unseeded paths consume `PrngRegistry.GetRandom(callSite.Span, name)`.

**Warning signs:**
- `examples/generative/markov_jazz.flow` produces different SHA-256 on two runs.
- `tests/test_prng_determinism.flow` fails.
- `RmsRegressionTests.AssertRmsWithinTolerance` baseline shifts between identical-source runs.

### Pitfall 2: Combinator failure modes — silent damage vs charitable advisory

**What goes wrong:** Composer calls `(fast seq 0)` — divides every duration by zero. Result is either a runtime exception (composer's playback dies mid-set) or NaN durations that the audio renderer silently swallows (composer hears nothing and doesn't know why). Same problem for `(every 0 fn seq)`, `(chunk 0 fn seq)`, etc.

**Why it happens:** Math edge cases compose poorly with audio output. NaN propagates silently; division by zero throws.

**How to avoid:** Charitable interpretation per PAT-02 — every combinator with a numeric divisor / cycle count checks for `<= 0` AND empty Sequence, emits `RenderingDiagnostics.WarnOnce` with a per-Span sentinel, returns input unchanged. Tested via composer-facing fixture: `tests/test_patterns_edge_cases.flow` covers zero-divisor and empty-Sequence paths for every combinator.

**Warning signs:**
- Audio output is silent for a section that should have notes.
- stderr lacks the warning when input is degenerate.

### Pitfall 3: Section overload precedence collisions (SECT-01)

**What goes wrong:** Composer writes two section overloads with the SAME pattern shape (e.g., both have `(Note root)` signature). OverloadResolver scores them identically. Composer calls `verse(C4)` and gets the wrong one (or worse — gets an "Ambiguous overload" error that the composer doesn't understand because they thought their patterns were distinguishable).

**Why it happens:** Pattern overload distinguishability is harder than function overload distinguishability — function overloads differ by argument TYPES; section overloads can differ by pattern SHAPE (literal vs binding vs constructor) which is more subtle.

**How to avoid:**
1. OverloadResolver returns Rust-style `Ambiguous section overload` error via DiagnosticRenderer, naming BOTH conflicting sections and their source locations.
2. Section overload resolution runs at SECTION DECLARATION TIME (not at call time) — a pre-flight check verifies all `section X` declarations have distinguishable patterns. Same-shape ambiguity is caught at parse time, not at composer's first call site.
3. Document the specificity scoring table (above) in `examples/sections/parameterized.flow` so composers can reason about precedence.

**Warning signs:**
- Section call dispatches to wrong overload at runtime.
- `Ambiguous section overload` error in Rust-style diagnostic at declaration time.

### Pitfall 4: Lorenz cross-platform FP divergence (GEN-04 / GEN-05)

**What goes wrong:** Lorenz attractor uses chained floating-point math. Two platforms (x86-64 vs ARM64) may produce subtly different results due to FPU precision differences and `Math.*` library quirks. Composer's `tests/test_lorenz_quantize.flow` passes on Linux x64 but produces different bytes on macOS ARM64.

**Why it happens:** Chaotic dynamical systems amplify floating-point error exponentially. After ~50 iterations, two runs starting at identical seeds but on different platforms will diverge.

**How to avoid:** Documented as a platform-specific limitation in D-36-09. Two-run cmp-clean on the SAME platform is preserved (PrngRegistry deterministic seeding + same-platform IEEE 754 reproducibility). Cross-platform reproducibility is NOT guaranteed for Lorenz/logistic outputs. Document explicitly in:
- `examples/generative/markov_jazz.flow` comment header
- `CLAUDE.md` Pitfalls section (already noted in PITFALLS.md Pitfall research)
- `flow-lang/generative.flow` module-level doc-comment

**Warning signs:**
- CI fails on macOS / Windows when running Lorenz fixtures with shared baselines.
- Phase 41 cross-platform binaries produce different `examples/generative/lorenz_*.flow` output.

**Mitigation strategy (NOT shipped in v1.5):** Replace `Math.Sin/Cos/Sqrt` with a software-only deterministic library (e.g., MathNet's deterministic mode). Out of scope for v1.5.

### Pitfall 5: Named-arg backfill scope creep

**What goes wrong:** D-36-11's "~150 existing builtin signatures get parameter names backfilled" understates the actual surface — `grep -c "registry.Register" BuiltInFunctions.cs` returns **211**. Add Composition/, Transforms/, TestFramework/, Harmony/, Audio/ — total registered functions is closer to 350. Backfilling all of them in one commit produces an unreviewable ~3000-line diff. Splitting wrong creates orphan calls where some builtins accept named args and others don't (silent UX hole).

**Why it happens:** Mechanical sweep scope is invisible until you grep for the pattern.

**How to avoid:**
1. Plan 36-12 + Plan 36-13 split the backfill in two halves by module — Plan 36-12 covers audio/dsp/harmony/transforms (composer-facing music surfaces); Plan 36-13 covers collections/stdlib/test (utility surfaces). Each plan ships ~100-150 sites; diffs stay reviewable.
2. Backfill is preceded by Plan 36-11 (the syntax + resolver itself) — composer can immediately call any backfilled site with named args; un-backfilled sites still accept positional and emit a `RenderingDiagnostics.WarnOnce` if a named-arg form is attempted ("function 'foo' does not yet support named arguments").
3. After both backfill plans land, source-grep CI gate: `grep -c "ParameterNames" BuiltInFunctions.cs` must equal `grep -c "registry.Register" BuiltInFunctions.cs`.

**Warning signs:**
- Plan 36-12 / 36-13 diff exceeds 2000 lines (split further).
- Composer reports "named args work for X but not Y" (orphan call site).

### Pitfall 6: `MarkovModel` / `LsystemModel` reference identity vs value equality

**What goes wrong:** Composer writes `MarkovModel m1 = (markovTrain corpus 2); MarkovModel m2 = (markovTrain corpus 2);` and expects `(eq m1 m2)` to be true (structural equality — both trained on same corpus + order). Result: false (reference identity). Composer confused.

**Why it happens:** Tuning (Phase 32) and Sfz (Phase 33) are reference-identity types — `(eq tuning1 tuning2)` is reference compare. Composers may forget this discipline applies to MarkovModel / LsystemModel too.

**How to avoid:**
1. Document in `Music Types Quick Reference` table in CLAUDE.md (extended): MarkovModel and LsystemModel are reference identity, NOT structural equality.
2. Provide `(markovEqual m1 m2)` / `(lsystemEqual m1 m2)` builtins for structural compare (cheap — compare order + transition table).
3. Composer-facing test `tests/test_markov_train_generate.flow` exercises both `(eq m1 m2)` (false on different trainings) and `(markovEqual m1 m2)` (true when corpus + order match).

**Warning signs:**
- Composer reports surprising behavior comparing two trained models.

### Pitfall 7: Section param closures over outer musical context (SECT-01)

**What goes wrong:** Composer writes `key Cmajor { tempo 120 { section verse(Note root) { /* uses root */ } } }`. The section captures `Cmajor` + 120 BPM at DECLARATION time. Calling `verse(C4)` from outside the original key/tempo block — the section USE the OUTER context, not the declaration context. Composer expected closure semantics; got dynamic-scope semantics.

**Why it happens:** Flow's existing musical-context is dynamic-scope (push/pop stack). Composers familiar with lexical-scope languages assume closures.

**How to avoid:** Document the semantic explicitly — section bodies execute against the **callsite's** active MusicalContext, NOT the declaration's. Section parameters bind in a new synthetic frame that inherits from the callsite frame, not the declaration frame. This matches Flow's broader semantics (musical context is a render-time stack).

**Warning signs:**
- Composer reports "my verse plays at the wrong tempo when I call it from a different section."

### Pitfall 8: Style-pack collision and load-order

**What goes wrong:** Composer ships `~/.config/flow/styles/jazz.flow` overriding the shipped `flow-lang/improv/styles/jazz.flow`. Load order matters: if shipped packs load second, user's overrides are clobbered.

**Why it happens:** Same problem as Phase 30 XDG config — load order is invisible to composers.

**How to avoid:**
1. **Load order locked at FlowEngine init: shipped packs FIRST, user packs SECOND.** User packs always override (last-write-wins). Document in `flow-lang/improv/styles/README.md`.
2. When a user pack overrides a shipped pack, emit a one-shot stderr advisory at init: `[improv] user style '#jazz' overrides shipped pack`. Charitable — composer KNOWS they overrode.
3. `(listStyles)` builtin lists all registered styles + their source paths so composers can audit.

**Warning signs:**
- Composer's pack changes don't take effect (load order reversed).

### Pitfall 9: Cycle-dependent combinators on sequences with no bars (D-36-04)

**What goes wrong:** `(every 4 (fn s => (fast s 2)) emptySeq)` where `emptySeq.Bars.Count == 0`. Index math `i % n` divides by zero or skips everything. Same for `(chunk N fn emptySeq)` and `(phase 0.25 emptySeq)`.

**Why it happens:** Bar-based cycle math assumes bar count > 0.

**How to avoid:** Each cycle-dependent combinator's charitable-interpretation guard (Pitfall 2 above) handles the empty-Sequence case: return input unchanged + stderr advisory.

**Warning signs:**
- Combinator returns empty sequence silently when composer expects a transformed one.

## Code Examples

### Combinator chain (PAT-01) — using Phase 35 `as` for intermediate naming

```flow
use "@patterns"

# Composer's source — note the Phase 35 `as` chain naming
Sequence final = (
  | C4 q D4 q E4 q F4 q | G4 q A4 q B4 q C5 q |
)
  -> (every 4 (fn s => (fast s 2))) as varied
  -> (sometimes 0.3 rev) as randomized
  -> (jux (fn s => (transpose s 7))) as stereo
  -> render

# `varied`, `randomized`, `stereo` available as bindings for inspection
(inspect varied)
```

### Markov train+generate (GEN-01 / D-36-06)

```flow
use "@generative"

# Train once
Sequence corpus = | C4 D4 E4 F4 G4 F4 E4 D4 C4 |
MarkovModel m = (markovTrain corpus 2)

# Generate many — deterministic at fixed git SHA via PrngRegistry
Sequence riff1 = (markovGenerate m 16 42)   # explicit seed
Sequence riff2 = (markovGenerate m 16)      # unseeded — PrngRegistry deterministic at this source line
Sequence riff3 = (markovGenerate m 16)      # different source line → different PrngRegistry seed
```

### L-system with Symbol alphabet (GEN-02)

```flow
use "@generative"

Dict<Symbol, Array[Symbol]> rules = (dict
  #A <<#A, #B>>
  #B <<#A>>)

Array[Symbol] algae = (lsystem #A rules 4)
# → <<#A, #B, #A, #A, #B, #A, #B, #A>>  (Lindenmayer canonical Fibonacci-growth result)

# Compose to notes via composer-defined mapping
Sequence seq = (lsystemToSequence algae
  (fn Symbol s =>
    (if (eq s #A) C4q (if (eq s #B) E4q _q))))
```

### Parameterized section with overload (SECT-01 / D-36-18)

```flow
# Overload 1 — simple Note pattern
section verse(Note root) {
  | $root q E4 q G4 q B4 q |
}

# Overload 2 — tuple destructure with type annotations
section verse(<<Note root, Int repeats>>) {
  for i in (range 0 repeats) {
    | $root q E4 q G4 q B4 q |
  }
}

# Overload 3 — constructor pattern, music-aware extractor
section verse(Cmaj7) {
  # Special-cased verse for Cmaj7 contexts
  | C4 q E4 q G4 q B4 q | C5 q B4 q G4 q E4 q |
}

# Composer uses them:
Song song = [
  verse(C4)                          # → Overload 1
  verse(<<D4, 3>>)                   # → Overload 2
  verse(Cmaj7)                       # → Overload 3 (specificity 800 wins over 500)
  verse(C4)*3                        # → Overload 1, repeated 3× via D-36-14
]
```

### `jam` with style + key override (IMPROV-01)

```flow
use "@improv"

Sequence chords = | Cmaj7 Am7 Dm7 G7 |

# Default — improvise in active key context
key Cmajor {
  Sequence solo = (jam over=chords)         # style=#jazz, length=8, key=Cmajor (inherited)
  Sequence longSolo = (jam over=chords length=16 seed=42)
}

# Override key for a chromatic / pivot bar
key Cmajor {
  Sequence pivot = (jam over=chords key="Fmajor" length=2 style=#blues)
}
```

### Universal named args (D-36-11)

```flow
# Existing positional form (unchanged — backward compatible)
Sequence s1 = (transpose seq 2)
Sequence s2 = (delay buf 100ms 0.5 0.4)

# New named-arg form — equivalent, more readable
Sequence s3 = (transpose seq amount=2)
Sequence s4 = (delay buf timeMs=100ms feedback=0.5 mix=0.4)

# Mix positional + named (positional first, named after)
Sequence s5 = (compress buf -12dB ratio=4.0 attack=5ms release=100ms)
```

### Two-run cmp-clean determinism gate (GEN-05)

```flow
# tests/test_prng_determinism.flow
use "@test"
use "@generative"

(test "markov unseeded is deterministic at fixed source position" (lazy
  Sequence m1 = (markov | C4 D4 E4 F4 | 2 8)
  Sequence m2 = (markov | C4 D4 E4 F4 | 2 8)  # different source line → different seed
  Sequence m3 = (markov | C4 D4 E4 F4 | 2 8)  # same source line as m2 on second run...

  # ...but cmp test runs the file twice (CI) and checks SHA-256 of rendered WAV
  (assertNotesMatch m1 m1)  # trivially true; the actual gate is render-cmp
))
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Tidal in Haskell — dynamic typing | Tidal-style combinators on statically-typed Sequence | Phase 36 v1.5 | Type-checked combinator chains; LSP hover shows types; differentiator vs peer tools |
| Custom oscillator partials (v1.0 Phase 3) | Lambda-typed combinator args (D-36-03) | Phase 36 | Functional consistency — same `Function` Value composes |
| Single-section names | Section overloading via Pattern AST | Phase 36 (SECT-01 / D-36-18) | Polymorphic sections — composer writes `verse(C4)` vs `verse(<<D4, 3>>)` vs `verse(Cmaj7)` |
| Hand-passed seeds (`(vary seq 0.5 42)`) | PrngRegistry by source-location | Phase 36 (GEN-05) | Determinism without ceremony; seed=N still available as override |
| Positional-only calls | Universal named args (D-36-11) | Phase 36 | Self-documenting at call site; default-arg expression unblocked |
| 1-shot Markov | Train + Generate split (D-36-06) | Phase 36 | Reusable models; pass `MarkovModel` between sections like `Sequence` |
| `(? C4 E4 G4)` random choice | Markov / L-system / CA / Lorenz first-class | Phase 36 | Generative primitives as stdlib, not composer-rolled |

**Deprecated/outdated:**
- None. Phase 36 is purely additive.

## Assumptions Log

> Claims tagged `[ASSUMED]` in this research. Planner + discuss-phase should confirm before they become locked decisions.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `FunctionSignature.ParameterNames` is the right shape for named-arg backfill (vs e.g. a parallel `Dictionary<FunctionSignature, string[]>` registry) | §Standard Stack supporting components, §Don't Hand-Roll | Wrong shape costs ~1 day of refactoring during Plan 36-11 implementation; both shapes work, this one is more cohesive |
| A2 | 211 + ~140 = ~350 total registered builtin sites (BuiltInFunctions.cs + Composition + Transforms + Audio + Harmony + TestFramework) | §Common Pitfalls Pitfall 5 | Underestimate; backfill plans may need a third split. Final count discovered when Plan 36-11 lands. |
| A3 | Lorenz cross-platform FP divergence is acceptable per D-36-09 (NOT a regression) | §Common Pitfalls Pitfall 4 | If composer / CI insists on cross-platform-identical Lorenz, we need a deterministic-math library (out of scope for v1.5). Confirm at discuss-phase. |
| A4 | Section overload precedence via OverloadResolver specificity scoring is the right model | §Architecture Patterns Pattern 7 | If composer wants source-order precedence (first-declared wins) instead, the resolver code is ~30 LOC change; not a huge cost |
| A5 | `ScaleData` (existing Harmony module type) is acceptable as `quantizeToScale` 2nd-arg type | §Architecture Patterns Pattern 5 | Composer may prefer `Array[Note]` exclusively; trivial to add as overload — no real risk |
| A6 | Style-pack collision: shipped first, user-config second, last-write-wins | §Common Pitfalls Pitfall 8 | Reverse order is also reasonable (some teams want shipped to override "for safety"). Composer's call. |
| A7 | Charitable-advisory (not hard error) on style+key musical incompatibility in `jam` | §Architecture Patterns Pattern 8, §Common Pitfalls | If composer wants hard error, ~5 LOC swap from `WarnOnce` to `ReportError`. No real risk. |
| A8 | Symbol-based L-system alphabet is the right pick (REQ wording) over turtle-graphics-string | §Architecture Patterns Pattern 3 | Composer can always build turtle-graphics atop Symbol-based as user code; reverse direction (turtle-string → Symbol) requires parser change |
| A9 | Cellular automata default initial-seed is single-1-center (Wolfram convention) | §Architecture Patterns Pattern 4 | Composer may prefer random-density default; one-line change in `cellular` builtin |
| A10 | PRNG seed-derivation hash function = FNV-1a stable variant (NOT C# `string.GetHashCode()` which is randomized per process) | §Architecture Patterns Pattern 6 | Critical correctness — if we accidentally use process-randomized hash, two-run cmp-clean breaks. CI source-grep gate catches it; flagged explicitly |
| A11 | 12-plan slicing is correct (6 waves × ~2 plans/wave on average) | §Summary, §Recommended Project Structure | If too tight, slice 13-14; if too loose, slice 10. Plan-checker iterates. |
| A12 | `SectionRegistry` becoming `Dictionary<string, List<SectionData>>` is backward-compatible at the call sites that read it today | §Runtime State Inventory | One callsite at `SongRenderer.ResolveSection` — straightforward migration; if multiple call sites exist, slightly more work but still mechanical |

## Open Questions

1. **Backfill scope — actual size of the mechanical sweep**
   - What we know: 211 `registry.Register` in BuiltInFunctions.cs; Composition + Transforms + Audio + Harmony add more.
   - What's unclear: Exact total. Plans 36-12 / 36-13 may need a third split.
   - Recommendation: Plan 36-11 ships syntax + resolver only; Plans 36-12 / 36-13 begin backfill on grep-driven module boundaries; if either plan exceeds 2000 lines, split further.

2. **Named-arg interaction with varargs (e.g., `(dict K V K V ...)`)**
   - What we know: `IsVarArgs` is a `FunctionSignature` property; varargs registered builtins don't have a "name" for the var-position.
   - What's unclear: Whether named args even make sense for varargs (likely no — composer always passes positional for the var-tail).
   - Recommendation: Named args ONLY apply to non-varargs builtins. Resolver short-circuits with "named arg `X` cannot be used with variadic function" diagnostic.

3. **PrngRegistry under `live { ... }` block (Phase 38)**
   - What we know: Phase 38's `live` block explicitly opts out of determinism (D-v1.5-07).
   - What's unclear: Should PrngRegistry's salt become non-zero (wall-clock or counter) inside `live`?
   - Recommendation: DEFER to Phase 38 — Phase 36 ships salt=0 always; Phase 38 introduces the live-mode salt as part of its own determinism opt-out implementation.

4. **Signed literal disambiguation after `=`**
   - What we know: Phase 35 added `TokenType.Match` / `When` to `TryLexSignedNumber`'s expression-start set so `(match -5 ...)` lexes `-5` as one token.
   - What's unclear: Does `(fn arg=-5)` need `TokenType.Assign` added too? Likely YES per the same idiom.
   - Recommendation: Plan 36-11 (named-arg syntax) adds `TokenType.Assign` to `TryLexSignedNumber`'s expression-start set; verified by `tests/test_named_args.flow` covering `(fn arg=-5)`.

5. **Section overload distinguishability — declaration-time check or call-time?**
   - What we know: Function overload ambiguity is detected at CALL time by OverloadResolver.
   - What's unclear: Whether Phase 36 should pre-flight check section overloads at DECLARATION time (catch earlier) or wait until first call.
   - Recommendation: Pre-flight at declaration time. Section overloads are statically-knowable; catching at declaration time gives composer immediate feedback. Plan 36-09 ships the declaration-time check.

6. **`jam` corpus argument — optional or required?**
   - What we know: D-36-10 lists `over` as required, doesn't mention an optional `corpus` arg for Markov training.
   - What's unclear: Does `jam` train internally from a hardcoded corpus per style pack? Or does composer pass a corpus?
   - Recommendation: For v1.5, the rule pack provides the Markov transition tables (style packs ship pre-trained); composer doesn't pass a corpus. v1.6 may add `corpus=` for composer-supplied training. Plan 36-10 documents this in the rule-pack contract README.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All Phase 36 work | ✓ (already in use) | 10.0 | — |
| `flow` CLI binary (Phase 30) | `flow test` runner for composer-facing fixtures | ✓ (Phase 30 shipped) | — | — |
| Phase 35 Pattern AST | SECT-01 / D-36-17 reuse | ✓ (Phase 35 closed 2026-05-19) | — | — |
| Phase 35 DiagnosticRenderer | D-36-16 arity-mismatch diagnostics | ✓ (Phase 35 closed) | — | — |
| Phase 35 `as` chain naming | Combinator chain ergonomics | ✓ (Phase 35 closed) | — | — |
| Phase 35 test framework | Composer-facing test fixtures | ✓ (Phase 35 closed; `flow test` CLI shipped) | — | — |
| slopcheck | Package legitimacy gate | ✗ | — | N/A — Phase 36 ships zero new packages; gate is moot |
| ctx7 | Library docs lookup | ✗ | — | N/A — Phase 36 uses no external library docs |

**Missing dependencies with no fallback:** none
**Missing dependencies with fallback:** none — slopcheck and ctx7 are research-time-only; Phase 36 has no external deps to verify

## Validation Architecture

> `workflow.nyquist_validation = true` in `.planning/config.json` — this section is required.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 (C# unit tests) + Phase 35 pure-Flow `(test ...)` framework |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); `flow-cli/Commands/TestCommand.cs` (`flow test`) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase36"` (C# Phase 36 facts only) |
| Full suite command | `dotnet test` + `for f in tests/test_*.flow; do flow test "$f"; done` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PAT-01 | `every` combinator composes via `->`; cycle unit = bars | unit + composer | `dotnet test --filter "Phase36.PatternEveryTests"` + `flow test tests/test_patterns_every.flow` | ❌ Wave 0 |
| PAT-01 | All 12 combinators + sparseSeq registered + invocable | composer | `flow test tests/test_patterns_chain.flow` | ❌ Wave 0 |
| PAT-02 | Charitable interpretation on zero-divisor (`fast seq 0`); zero-length input | unit | `dotnet test --filter "Phase36.PatternChalkyEdgeCasesTests"` | ❌ Wave 0 |
| GEN-01 | One-shot `(markov corpus 2 16 seed)` deterministic | composer | `flow test tests/test_markov_oneshot.flow` | ❌ Wave 0 |
| GEN-01 | Split `markovTrain` + `markovGenerate`; MarkovModel reference identity | unit + composer | `dotnet test --filter "Phase36.MarkovModelTests"` + `flow test tests/test_markov_train_generate.flow` | ❌ Wave 0 |
| GEN-02 | L-system w/ Symbol alphabet; one-shot + split shapes | composer | `flow test tests/test_lsystem_oneshot.flow` + `flow test tests/test_lsystem_train_generate.flow` | ❌ Wave 0 |
| GEN-03 | Cellular `(cellular 30 16 32 seed)` rule 30 produces canonical chaos | composer | `flow test tests/test_cellular_rule30.flow` | ❌ Wave 0 |
| GEN-03 | 2D `(life ...)` deterministic | composer | `flow test tests/test_cellular_life.flow` | ❌ Wave 0 |
| GEN-04 | Lorenz returns Array[Double]; quantize via ScaleData | composer | `flow test tests/test_lorenz_quantize.flow` | ❌ Wave 0 |
| GEN-04 | Logistic deterministic at seed | composer | `flow test tests/test_logistic.flow` | ❌ Wave 0 |
| GEN-05 | Two-run cmp-clean: same source SHA → byte-identical WAV+MIDI | integration | `bash scripts/test_two_run_determinism.sh examples/generative/markov_jazz.flow` | ❌ Wave 0 |
| GEN-05 | PrngRegistry source-location keying produces distinct streams at distinct lines | unit | `dotnet test --filter "Phase36.PrngRegistryTests"` | ❌ Wave 0 |
| SECT-01 | Basic `section verse(Note root)` with positional call | composer | `flow test tests/test_section_params.flow` | ❌ Wave 0 |
| SECT-01 | Section overload via OverloadResolver pattern dispatch | composer | `flow test tests/test_section_overload.flow` | ❌ Wave 0 |
| SECT-01 | Phase 35 pattern syntax in section signatures (guards, tuple destructure, constructor) | composer | `flow test tests/test_section_pattern_destructure.flow` | ❌ Wave 0 |
| SECT-01 | `*N` repeat composes with parameterized calls | composer | `flow test tests/test_section_repeat.flow` | ❌ Wave 0 |
| SECT-01 | Default values; arity / type mismatches render Rust-style diagnostic | composer + unit | `flow test tests/test_section_defaults.flow` + `dotnet test --filter "Phase36.SectionDiagnosticsTests"` | ❌ Wave 0 |
| IMPROV-01 | `(jam over=chords)` with active key context | composer | `flow test tests/test_jam_jazz.flow` | ❌ Wave 0 |
| IMPROV-01 | `key=` override pushes synthetic frame | composer | `flow test tests/test_jam_key_override.flow` | ❌ Wave 0 |
| IMPROV-01 | All 3 baseline rule packs (#jazz / #blues / #classical) registered + invocable | composer | `flow test tests/test_jam_styles.flow` | ❌ Wave 0 |
| D-36-11 | Named-arg syntax `(fn name=val ...)` parses + resolves | unit + composer | `dotnet test --filter "Phase36.NamedArgsParserTests"` + `flow test tests/test_named_args.flow` | ❌ Wave 0 |
| D-36-11 | Positional form still works for every backfilled builtin | unit | `dotnet test --filter "Phase36.NamedArgBackcompatTests"` (sample of 20 builtins from each module) | ❌ Wave 0 |
| D-36-11 | Backfill completeness — every registered builtin carries ParameterNames | unit (grep) | `dotnet test --filter "Phase36.ParameterNamesCoverageTest"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build` + `dotnet test --filter "Phase36"` (fast — Phase 36 facts only)
- **Per wave merge:** `dotnet test` (full xUnit suite — verify no Phase 35 / earlier regressions)
- **Phase gate:** Two-run cmp-clean integration test against `examples/generative/markov_jazz.flow`, `examples/generative/tidal_combinators.flow`, `examples/sections/parameterized.flow` — SHA-256 byte-identical on consecutive runs of both WAV and MIDI

### Wave 0 Gaps

All Phase 36 test files are new:

- [ ] `flow-lang.Tests/Phase36/PatternEveryTests.cs` — PAT-01 `every` cycle unit (bars)
- [ ] `flow-lang.Tests/Phase36/PatternChalkyEdgeCasesTests.cs` — PAT-02 charitable interpretation
- [ ] `flow-lang.Tests/Phase36/MarkovModelTests.cs` — GEN-01 model identity + train/generate split
- [ ] `flow-lang.Tests/Phase36/PrngRegistryTests.cs` — GEN-05 source-location keying + reseed boundary + snapshot/restore
- [ ] `flow-lang.Tests/Phase36/SectionOverloadTests.cs` — SECT-01 / D-36-18 overload dispatch
- [ ] `flow-lang.Tests/Phase36/SectionDiagnosticsTests.cs` — D-36-16 Rust-style arity/type errors
- [ ] `flow-lang.Tests/Phase36/NamedArgsParserTests.cs` — D-36-11 lexer + parser
- [ ] `flow-lang.Tests/Phase36/NamedArgBackcompatTests.cs` — positional form preserved
- [ ] `flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs` — backfill completeness gate
- [ ] `tests/test_patterns_every.flow` — composer-facing PAT-01
- [ ] `tests/test_patterns_chain.flow` — all 12 combinators + sparseSeq exercised
- [ ] `tests/test_patterns_edge_cases.flow` — PAT-02 charitable paths
- [ ] `tests/test_markov_oneshot.flow` — GEN-01 one-shot
- [ ] `tests/test_markov_train_generate.flow` — GEN-01 split
- [ ] `tests/test_lsystem_oneshot.flow` — GEN-02
- [ ] `tests/test_lsystem_train_generate.flow` — GEN-02 split (lsystemModel)
- [ ] `tests/test_cellular_rule30.flow` — GEN-03 1D
- [ ] `tests/test_cellular_life.flow` — GEN-03 2D
- [ ] `tests/test_lorenz_quantize.flow` — GEN-04 + quantizeToScale
- [ ] `tests/test_logistic.flow` — GEN-04 logistic
- [ ] `tests/test_section_params.flow` — SECT-01 basic
- [ ] `tests/test_section_overload.flow` — SECT-01 / D-36-18
- [ ] `tests/test_section_pattern_destructure.flow` — D-36-17
- [ ] `tests/test_section_repeat.flow` — D-36-14 `*N`
- [ ] `tests/test_section_defaults.flow` — D-36-15
- [ ] `tests/test_jam_jazz.flow` — IMPROV-01 baseline
- [ ] `tests/test_jam_key_override.flow` — IMPROV-01 key=
- [ ] `tests/test_jam_styles.flow` — IMPROV-01 all 3 packs
- [ ] `tests/test_named_args.flow` — D-36-11 surface
- [ ] `tests/test_prng_determinism.flow` — GEN-05
- [ ] `scripts/test_two_run_determinism.sh` — phase-gate integration script (renders a file twice, SHA-256-cmp)

Framework install: NONE — xUnit.v3 + `flow test` CLI already exist; no new test infrastructure needed.

## Security Domain

> Phase 36 introduces no new authentication, network, or data-handling surfaces. All work is internal interpreter expansion + composer-side stdlib + Flow-file rule packs loaded from filesystem at engine init.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — no auth in Phase 36 |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A — interpreter has no users |
| V5 Input Validation | **yes** | Composer-supplied corpus / rules / chord progressions to generative primitives are pre-validated by existing type-checker; charitable-interpretation discipline (PAT-02) handles malformed inputs as advisories not crashes |
| V6 Cryptography | no | N/A — no crypto |
| V12 File Handling | **yes** | Style-pack loader reads `~/.config/flow/styles/*.flow` — same XDG-config posture as Phase 30 config + Phase 33 SFZ root |

### Known Threat Patterns for `flow-lang` (Phase 36)

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malicious user style pack at `~/.config/flow/styles/*.flow` executing arbitrary Flow code | Tampering / Elevation | Flow scripts have no network/filesystem write surface beyond explicit composer-invoked builtins; rule packs only define Dict values, no side-effecting calls expected. Loader could rate-limit or warn on packs containing non-`(registerStyle ...)` top-level statements; v1.5 ships charitable (load anyway, document the convention). |
| Corpus passed to Markov containing maliciously-crafted patterns that produce stack-overflow at generation | DoS | Markov order capped at 3 (GEN-01); transition table is finite-sized; `length` arg capped at reasonable composer-facing max (e.g., 10000 — same default-cap convention as `range`). |
| L-system iteration count producing exponential explosion | DoS | `iterations` arg capped (e.g., 20 — well past musical use, well short of OOM). Charitable advisory when capped. |
| Lorenz initial-condition exploit (specific seed forcing degenerate output) | Integrity | Not a real exploit — Lorenz is deterministic chaos, all valid seeds produce valid output. No mitigation needed. |
| PrngRegistry key collision producing same random stream for distinct call sites | Integrity (determinism contract) | Stable hash (FNV-1a variant) over `(file, line, col, name, salt)`; collision probability < 2^-32 per pair. Two-run cmp-clean test catches in practice. |

**No new attack surface beyond what Phase 30 (config) and Phase 33 (SFZ root) already established.** Filesystem reads honor XDG conventions; no shell exec; no network; no eval of arbitrary user input outside the Flow type system.

## Sources

### Primary (HIGH confidence — codebase-verified)

- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/36-sequence-algebra-generative/36-CONTEXT.md` (D-36-01..18, all locked decisions, Claude's Discretion areas)
- `/home/noah/Desktop/projects/flow-sharp/.planning/REQUIREMENTS.md` lines 45-64 (PAT-01..02, GEN-01..05, SECT-01, IMPROV-01) + lines 9-20 (D-v1.5-01..11)
- `/home/noah/Desktop/projects/flow-sharp/.planning/ROADMAP.md` Phase 36 section (goal + success criteria)
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/35-language-foundation/35-VERIFICATION.md` (Phase 35 shipped surface — 10/10 verified)
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/35-language-foundation/35-05-SUMMARY.md` (Pattern AST family)
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/35-language-foundation/35-06-SUMMARY.md` (Music-aware extractors, CapturedPragmas)
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/35-language-foundation/35-07-SUMMARY.md` (`-> CALL as NAME` IntermediateName)
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/35-language-foundation/35-03-SUMMARY.md` (DiagnosticRenderer + LevenshteinHelper)
- `/home/noah/Desktop/projects/flow-sharp/CLAUDE.md` (Phase 28 articulation rules; two-run cmp-clean contract; prefix-only arithmetic; music-types literal syntax; GSD enforcement)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Ast/Patterns/Pattern.cs` + family (Phase 35 reuse target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Ast/Statements/SectionDeclaration.cs` (current parameterless shape — extension target for SECT-01)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Parsing/Parser.cs` lines 484-508 (ParseSectionDeclaration — extension target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/TypeSystem/OverloadResolver.cs` (existing specificity scoring — section overload reuse target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/TypeSystem/FunctionSignature.cs` (existing 3-field record — ParameterNames extension target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/ExecutionContext.cs` (existing PRNG state + SnapshotState/RestoreState — PrngRegistry integration target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/MusicalContext.cs` (existing push/pop stack — section param frame + jam key= override target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/InternalFunctionRegistry.cs` (existing registration pattern)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/BuiltInFunctions.cs` (211 `registry.Register` sites — backfill target; verified count via `grep -c`)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (combinator-style registration shape — pattern for `@patterns` module)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Composition/VariationFunctions.cs` (existing seeded `vary()` PRNG idiom — PrngRegistry migration target)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Lexing/TokenType.cs` + `SimpleLexer.cs` (named-arg disambiguation surface)

### Secondary (MEDIUM confidence — v1.5 research documents)

- `/home/noah/Desktop/projects/flow-sharp/.planning/research/FEATURES.md` lines 41-50 (Phase 36 differentiator framing; Tidal pattern algebra + music-aware extractors + parameterized sections + generative primitives + improv API)
- `/home/noah/Desktop/projects/flow-sharp/.planning/research/STACK.md` lines 50-53, 278-313 (hand-roll recommendations + chord-aware Markov shape + L-system surface options)
- `/home/noah/Desktop/projects/flow-sharp/.planning/research/PITFALLS.md` Pitfall 6 (Generative-primitive determinism break — D-v1.5-06 backing) + Pitfall 11 (Pattern matching exhaustiveness) + Pitfall 22 (Improv API distinguishability)
- `/home/noah/Desktop/projects/flow-sharp/.planning/research/SUMMARY.md` lines 60-101 (Phase 36 deliverables map; sub-order; differentiator framing)

### Tertiary (LOW confidence — external references for context, NOT used as authoritative source for Phase 36 design)

- [Tidal Cycles documentation](https://tidalcycles.org/) — combinator naming conventions (every / fast / slow / rev / jux / sometimes / iter / palindrome / degrade / superimpose / chunk / phase)
- [Strudel.cc REPL](https://strudel.cc/) — peer-tool baseline for sequence algebra in JS
- [Cherry Audio Lorenz Attractor module ranges](https://store.cherryaudio.com/modules/lorenz-attractor) — canonical σ=10, ρ=28, β=8/3
- [Stanford CCRMA — Growing Music: L-Systems](https://ccrma.stanford.edu/~elisse/256A/final/growing%20music%20-%20musical%20interpretations%20of%20l-systems.pdf) — alphabet conventions (turtle vs symbolic)
- [Listening to Elementary Cellular Automata](https://medium.com/code-music-noise/listening-to-elementary-cellular-automata-661018229362) — musically-interesting CA rules (30 / 90 / 110 / 184)

## Metadata

**Confidence breakdown:**
- Codebase-internal claims (existing surfaces, file paths, function signatures): HIGH — every claim verified by reading source
- D-36-* decision interpretation: HIGH — CONTEXT.md is precise; Claude's Discretion areas explicitly flagged and resolved with rationale
- Universal named-arg backfill scope estimate: MEDIUM — 211 sites in BuiltInFunctions.cs verified by grep; total across all modules estimated; final count surfaces during Plan 36-11 execution
- Generative primitive math (Markov / L-system / CA / Lorenz formulas): HIGH — standard textbook implementations, no novel research
- Chaos-map quantization scale-arg type pick (`ScaleData`): MEDIUM — multiple valid shapes; pick justified by existing Harmony module reuse
- PrngRegistry determinism integration: HIGH — Phase 18/25/27/28/29/33 inheritance well-documented; Phase 32 RenderTuning push/pop is the same shape
- Rule-pack Dict shape contract: MEDIUM — composer-facing; v1.6 may iterate based on real-world style pack contributions

**Research date:** 2026-05-20
**Valid until:** 2026-06-20 (30 days — stable language-foundation territory; revisit if Phase 35 backfill surfaces unexpected complications, or if user adds D-36-* decisions after discuss-phase iteration)
