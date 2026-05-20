# Phase 36: Sequence Algebra & Generative - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-20
**Phase:** 36-sequence-algebra-generative
**Areas discussed:** Tidal combinator set, jam API + named args, Markov train+generate vs one-shot, SECT-01 call syntax

---

## Tidal Combinator Set

### Q1: Which 12 combinators ship in v1.5 @patterns?

| Option | Description | Selected |
|--------|-------------|----------|
| REQUIREMENTS.md set | every/fast/slow/chunk/phase/rev/jux/sometimes/often/rarely/degrade/superimpose. Probability ladder via 3 named functions. | |
| Research set | every/fast/slow/chunk/rev/jux/sometimes/iter/palindrome/striate/degrade/cat. Adds iter+palindrome; striate Phase 37 territory; cat redundant. | |
| Hybrid: drop probability triad, add iter+palindrome | every/fast/slow/chunk/phase/rev/jux/sometimes (with prob arg)/iter/palindrome/degrade/superimpose. Hybrid pick. | ✓ |
| Let me dictate | Composer provides own list. | |

**User's choice:** Hybrid. Drops `rarely`/`often` ladder in favor of `sometimes` with explicit probability arg. Keeps `phase` and `superimpose` from REQ. Adds `iter` and `palindrome` from research. Drops `striate` (Phase 37) and `cat` (redundant with existing `Transforms.concat`).

### Q2: How does composer pass a transform to `every`/`sometimes`/`jux`?

| Option | Description | Selected |
|--------|-------------|----------|
| Lambda required | `(every 4 (fn s => (fast s 2)) seq)` — uses Phase 26.1 lambda; no language change. | ✓ |
| Phase 36 ships partial application | `(every 4 (fast 2) seq)` — partial app becomes a Sequence => Sequence value; requires currying support. | |
| Dedicated `as` combinator-arg form | `seq -> (fast 2) as fastDouble -> (every 4 fastDouble) -> render`. Reuses Phase 35 `as` chain naming. | |

**User's choice:** Lambda required. No partial application introduced in Phase 36.

### Q3: How do cycle-dependent combinators define their unit of N?

| Option | Description | Selected |
|--------|-------------|----------|
| Bars | `every 4` = bars 0/4/8/...; `chunk N` = N bar-aligned chunks; `phase 0.25` = 25% bar count. | ✓ |
| Note onsets | `every 4` = notes 0/4/8/...; less musical for sparse vs dense phrases. | |
| Sequence repetitions | `every 4` = every 4th `(repeat seq N)` call. Stateful; breaks pure-functional posture. | |

**User's choice:** Bars. Composer-friendly musical mental model.

### Q4: `degrade` semantics — fixed 50% or parameterized?

| Option | Description | Selected |
|--------|-------------|----------|
| Always parameterized | `(degrade 0.3 seq)`; default 0.5 via overload. | |
| Fixed 50% Tidal-compat | `(degrade seq)` always ~50%. | |
| Both as named functions | `(degrade seq)` fixed 50% + `(sparseSeq prob seq)` for custom. | ✓ |

**User's choice:** Both. `degrade` stays Tidal-compat (fixed 50%); `sparseSeq` is the Flow-native helper for custom probability. 12 combinators + 1 helper.

---

## jam API + Universal Named Arguments

### Q1: How does `jam` accept its args? (Decides whether Phase 36 introduces named-arg syntax universally.)

| Option | Description | Selected |
|--------|-------------|----------|
| Positional with optional trailing dict | `(jam chords #jazz 8 42)` or `(jam chords #jazz 8 (dict #seed 42))`. No new syntax. | |
| Phase 36 introduces named args | `(jam over=chords style=#jazz length=8 seed=42)`. Big language change; affects every builtin. | ✓ |
| Single Dict arg | `(jam (dict #over chords #style #jazz #length 8 #seed 42))`. Pure data; no language change. | |
| Symbol-keyed positional | `(jam chords style: #jazz length: 8)`. Smalltalk-style named-arg sugar. | |

**User's choice:** Introduce named args. Major language-level decision.

### Q2: How broad is the named-arg rollout in Phase 36?

| Option | Description | Selected |
|--------|-------------|----------|
| Universal: every builtin accepts named args | ~150 signatures need names backfilled. Coherent language; biggest scope. | ✓ |
| Opt-in per builtin | Only specifically-registered builtins accept named-arg calls. Smaller, inconsistent. | |
| Phase 36 generatives only | Lexer/parser get syntax universally but OverloadResolver matches names for Phase 36 builtins only. | |
| Pull into Phase 35.1 mini-phase | Defer the language change; ship Phase 36 with positional+Dict for `jam`. | |

**User's choice:** Universal. Every builtin gains parameter names.

### Q3 (composer-raised during discussion): "Wouldn't `jam` also need a key to improvise in? Like an override?"

Composer raised this mid-flow. Locked: yes — `jam` gets a `key=` named arg that defaults to the active musical-context block. Override allowed per-call (e.g., chromatic / pivot section that breaks the surrounding key for a few bars).

### Q4: Which jam params are required vs optional with defaults?

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal required: just `over` | `(jam chords)` valid. Defaults: style=#jazz, length=8, key=active, seed=unseeded, order=2. | ✓ |
| Required: over + style | `(jam chords #blues)` minimum. | |
| Required: over + length | `(jam chords 8)` minimum. | |
| Let me dictate exact list | Free text. | |

**User's choice:** Minimal required. `over` is the only required param.

### Q5: Rule pack format — C# class, Flow file, or hybrid?

| Option | Description | Selected |
|--------|-------------|----------|
| Internal C# class per style | Each pack is a class implementing IStyleRulePack. Composer can't ship own packs in v1.5. | |
| Flow-file rule packs in @improv/styles/*.flow | Each pack is a top-level Dict registered via `(registerStyle #name dict)`. Composer can ship `~/.config/flow/styles/myStyle.flow`. | ✓ |
| Hybrid: shipped packs C#, composer extensibility via Dict | v1.5 ships 3 C# packs (fast/type-safe) + runtime `registerStyle` for composer packs. | |

**User's choice:** Flow-file packs. Style is musical content, lives where composers can read and tweak.

---

## Markov: train+generate vs one-shot

### Q1: Markov API shape — one-shot, split, or both?

| Option | Description | Selected |
|--------|-------------|----------|
| Both ship | One-shot `(markov ...)` + split `(markovTrain ...)` → MarkovModel + `(markovGenerate ...)`. New value type. Same for L-system. | ✓ |
| One-shot only (REQ wording) | Just `(markov corpus order length seed)`. Composer re-passes corpus each call. | |
| Split only (research recommendation) | Just `(markovTrain)` + `(markovGenerate)`. More explicit but more verbose for simple cases. | |

**User's choice:** Both. Composer can pick per use case. New `MarkovModel` and `LsystemModel` first-class value types.

### Q2: Markov state — what features extracted from Sequence corpus?

| Option | Description | Selected |
|--------|-------------|----------|
| Pitch only | State = MIDI pitch. Simple, low sparsity. | |
| Pitch + duration | State = `<<pitch, duration>>` tuple. Richer but quadratically more state. | |
| Pitch + duration + articulation | 3D state. High sparsity. Probably overkill for baseline. | |
| Composer chooses via named arg | `features=#pitch` (default) or `features=<<#pitch #duration>>`. Uses new named-arg syntax. | ✓ |

**User's choice:** Composer chooses via named arg. Default `features=#pitch`.

---

## SECT-01 Call Syntax

### Q1: Parameterized section call syntax inside song expressions?

| Option | Description | Selected |
|--------|-------------|----------|
| Parens: `[verse(C4, 2) chorus]` | Most familiar. Heterogeneous bracket (zero-arg stays as bare identifier). | ✓ |
| Tuple: `[verse<<C4, 2>> chorus]` | Reuses Phase 26.1 tuple literal. Visually heavier. | |
| Whitespace: `[verse C4 2 chorus]` | Lisp-style positional. Ambiguous arity parsing. | |
| Named args: `[verse(root=C4, repeats=2) chorus]` | Reuses Phase 36 named-arg syntax. Verbose for simple cases. | |

**User's choice:** Parens form.

### Q2: How do parameterized sections compose with `verse*N` repetition?

| Option | Description | Selected |
|--------|-------------|----------|
| Compose: `verse(C4, 2)*3` repeats the parameterized call | Parser treats `verse(args)*N` as 3 same-arg calls. | ✓ |
| Require explicit repetition | Composer writes `verse(C4, 2)` N times in song expression. | |
| Reuse `repeats` param convention | Sections declare own `repeats` param; no special syntax. | |

**User's choice:** Compose. `verse(C4, 2)*3` desugars to 3 calls with same args.

### Q3: Section params — default values, and arity-mismatch handling?

| Option | Description | Selected |
|--------|-------------|----------|
| Defaults supported + arity-strict | Defaults work; mismatches → Rust-style FlowDiagnostic via Phase 35-03 renderer. | ✓ |
| Defaults supported + arity-charitable | Too many args → stderr advisory + drop extras. Charitable per D-v1.5-05. | |
| No defaults, arity-strict | Every arg required at every call. Simpler. | |
| Defaults via named args only | Defaults require named-arg call form. | |

**User's choice:** Defaults supported + arity-strict.

### Q4: Section params — plain types or full Phase 35 pattern syntax?

| Option | Description | Selected |
|--------|-------------|----------|
| Plain types only for v1.5 | `section verse(Note root, Int repeats) { ... }`. Composer destructures in body. | |
| Full Phase 35 patterns in signatures | Guards + tuple destructure + constructor patterns + music-aware extractors. | ✓ |
| Tuple destructure only | Compromise — allow `<<Note root, Int repeats>>` shape but no guards. | |

**User's choice:** Full Phase 35 patterns in section signatures.

### Q5: Multiple sections with the same name but different pattern signatures — supported?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — sections overload like functions | OverloadResolver picks at call time based on arg shape + Phase 35 patterns. | ✓ |
| No — one section name = one signature | `section verse` defined once. | |
| Yes but arity-only | Overload by parameter count only; no pattern-guarded overload. | |

**User's choice:** Yes — sections overload like functions. Reuses OverloadResolver + Phase 35 pattern dispatch.

---

## Claude's Discretion

The composer explicitly deferred these to the researcher / planner:

- L-system alphabet style (Symbol-based vs turtle-graphics).
- Cellular automata initial-seed pattern.
- Chaos-map `(quantizeToScale series scale)` scale-arg type.
- Section-overload precedence rules when multiple patterns match the same call.
- Rule-pack Dict shape contract details.
- `jam` advisory-vs-error policy on style+key incompatibility.
- Final plan breakdown (plan-checker / planner decide how to slice 10-12 plans).

## Deferred Ideas

- **Named-arg scope creep escape hatch** — if planner finds Phase 36 exceeds 12 plans, the universal named-arg rollout can be retro-scoped into a "Phase 35.1 — Named-argument syntax" mini-phase under D-v1.5-01 pre-traction latitude. Composer wants to write CONTEXT.md first and revisit only if scope explodes.
- The other ~60 Tidal combinators (linger / swingBy / stutter / range / segment / ...) — pick another dozen for v1.6 based on real composer feedback.
- 2D L-systems with branching, parametric L-systems with arguments.
- Higher-order Markov (variable-order, smoothed).
- ML-backed improvisation (Magenta-tier).
- Style-pack registry/marketplace (v1.5 supports file-system convention only).
- Pattern guards on chord progressions for `jam` (e.g., `when=(fn c => (= c.Quality "dom7"))`) — defers to v1.6.

---

*Discussion duration: ~30 minutes. 14 questions across 4 selected areas.*
