# Phase 43: Module Names & Qualified Imports - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-24
**Phase:** 43-module-names-qualified-imports
**Mode:** --auto (single-pass; Claude auto-selected recommended option for each gray area)
**Areas discussed:** Module declaration syntax, Qualified-access mechanics, Collision policy, Filename-derived auto-naming, Beat conversion API surface, Beat-companion overload scope, pitchShift(Hertz) routing

---

## Module declaration syntax

| Option | Description | Selected |
|--------|-------------|----------|
| `module <name>` keyword as first non-comment statement | Reserved keyword; familiar from Haskell / OCaml / F# / Rust; one obvious form | ✓ |
| `module(<name>)` S-expression-style | Consistent with prefix-only arithmetic surface; treats module decl as a builtin call | |
| `Note: module=<name>` comment-attribute | Zero new keywords; piggybacks on existing comment syntax | |

**Auto-selection rationale:** Familiar from related ML-family languages, matches `tempo` / `timesig` / `key` / `swing` / `voicePool` / `tuning` precedent (reserved keywords for declarative file-scope state). S-expression-style would require treating `module` as a special form (more parser complexity). Comment-attribute form has weak semantics — easy to misspell, hard to enforce.

---

## Qualified-access mechanics

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse `MemberAccessExpression` AST + module-registry-first lookup at eval time | Zero new AST surface; backward-compatible with `chord.root` / `song.sections` | ✓ |
| New `QualifiedCallExpression` AST node | Explicit parser disambiguation; cleaner but doubles the dot-syntax dispatch path | |
| `::` qualifier (e.g. `math::sin`) | Borrowed from C++ / Rust path syntax; visually distinct from member access | |

**Auto-selection rationale:** Existing AST is sufficient. Adding a new node would require parser lookahead (`module-name?.fn-name?` vs `value.field?`) — easier to resolve at evaluation time when the module registry exists and types are known. `::` adds visual noise that the unqualified-by-default ergonomics goal explicitly avoids — composers should rarely type qualifiers at all.

---

## Collision policy (unqualified-namespace clash across imported modules)

| Option | Description | Selected |
|--------|-------------|----------|
| Last-import-wins + one-shot advisory; qualified access as escape hatch | Charitable default; composer can ignore or qualify; matches Flow's existing pattern | ✓ |
| First-import-wins + advisory | Stable but less ergonomic if composer wants the later import | |
| Hard error on collision | Forces explicit qualification at every call site; least ergonomic | |
| Silent shadow (no advisory) | Same charitable behavior but no signal that a shadow happened | |

**Auto-selection rationale:** `feedback_charitable_interpretation.md` external memory locks the project to "prefer silent-and-documented assumptions over errors". `feedback_ergonomics_priority.md` locks "pick the lower-friction option for composers". Last-import-wins matches what most composers expect from sequenced `use` lines. The one-shot advisory provides discovery without nagging.

---

## Filename-derived auto-naming for module declarations

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit `module <name>` required; no filename derivation | Files without declaration retain current unqualified-only behavior | ✓ |
| Filename → module name automatically (`audio.flow` → `module audio`) | Less typing; brittle to file renames | |
| Hybrid (filename default, override with explicit `module`) | Lower friction but two states to reason about | |

**Auto-selection rationale:** Explicit > implicit per Flow's design philosophy. Composer-named modules survive file renames. The 13 existing stdlib files getting explicit declarations is a one-line-per-file migration (negligible cost) and the gain is clearer intent + safer rename refactoring.

---

## Beat ↔ Second context-aware conversion API

| Option | Description | Selected |
|--------|-------------|----------|
| Free builtins `(beatToSec Beat) → Second` + `(secToBeat Second) → Beat` | Reads active tempo from MusicalContext; default 120 BPM + advisory when absent | ✓ |
| Method-style `Beat.toSec` / `Second.toBeat` | Requires Flow type-method surface (none today — would be a separate feature) | |
| Implicit coercion via `BeatType.CanConvertTo(SecondType)` | Pure-function FlowType methods have no runtime tempo access — RESEARCH §Pitfall 3 explicitly ruled this out | |
| Require explicit tempo arg `(beatToSec Beat Double)` | More predictable but loses the MusicalContext-aware ergonomics | |

**Auto-selection rationale:** AUDIT.md §2 + RESEARCH Pitfall 3 already ruled out the type-method and implicit-coercion paths. Free builtins are the only viable shape. Default-and-warn behavior matches Flow's existing musical-context patterns (Phase 28 voice-pool default, Phase 32 tuning default).

---

## Beat-companion overload scope

| Option | Description | Selected |
|--------|-------------|----------|
| Only `delay(Buffer, Beat)` + `renderBarAtBeat(Sequence, Beat)` (AUDIT-listed) | Tight scope; composers compose `(delay buf (beatToSec 0.5b))` for other builtins | ✓ |
| Speculative Beat overloads on `reverb` / `stretch` / `compress` / etc. | Broader API; risks bloat for hypothetical needs | |
| No Beat overloads — only ship `beatToSec` / `secToBeat`, composers always wrap | Smallest surface; loses the "use Beat where it's natural" ergonomics | |

**Auto-selection rationale:** AUDIT.md §1 explicitly named which builtins matter (`delay`, `renderBarAtBeat`). `feedback_ergonomics_priority.md` "Pick the lower-friction option for composers even when it costs implementation complexity; easy cases fast, flexible cases flexible" supports building exactly those two. Speculative additions can come from real composer requests, not from anticipating need.

---

## pitchShift(Buffer, Hertz) routing

| Option | Description | Selected |
|--------|-------------|----------|
| Defer to v1.6-backlog as `pitchShiftTo(Buffer, Hertz, refHz)` | Hertz-shift (target absolute pitch) is semantically distinct from cents-relative shift; deserves its own builtin | ✓ |
| Add `pitchShift(Buffer, Hertz)` overload now (24 → 25 overloads) | Composer's "natural" shift-to-absolute-pitch pattern works; risks confusion vs cents-relative semantics | |
| Document as "not supported — use cents/semitones" + add advisory if a Hertz literal is passed | Cheapest; doesn't add a new builtin | |

**Auto-selection rationale:** AUDIT.md §7a explicitly flagged this finding as "needs a design decision before backfill (may be intentional that pitchShift is relative-only)". Conflating absolute-pitch and relative-pitch in one overload would surprise composers downstream. Defer to v1.6-backlog as a properly-named separate builtin.

---

## Claude's Discretion

- Where the module-registry lives (process-global static vs per-FlowEngine instance field) — planner picks based on existing `InternalFunctionRegistry` pattern.
- Exact stderr advisory wording — pattern locked (`[module] ...`) but precise phrasing is planner-discretion.
- Whether the module-registry lookup lives inline in `ExpressionEvaluator` or in a new `MemberAccessResolver` helper — existing dispatcher style guides this.
- Phase 43 test-fixture file layout (mirrors Phase 42's `flow-lang.Tests/Integration/Phase42/`).
- Whether `notation.flow` (current name) merges into `notation-io.flow` vs. gets renamed to `module notes` — flagged in D-07 for execution-time evaluation. Either resolution is acceptable.

---

## Deferred Ideas

- **`pitchShift(Buffer, Hertz)`** → v1.6-backlog as `pitchShiftTo(Buffer, Hertz, refHz)`.
- **Cross-module re-exports** (`module foo re-exports @bar`) → v1.6+ if real ergonomic pressure surfaces.
- **Module aliasing on import** (`use "@math" as m`) → v1.6+; unqualified-by-default removes most of the demand.
- **Composer-facing `flow migrate` subcommand** → v1.6+ or when traction appears (no third-party forks today per `project_pre_public_no_legacy_burden`).
- **Strict-mode flips of D-04 / D-06 / D-08 advisories** → Phase 44 territory under `enable strictTypes;`.
- **`gain` vs `volume`** ROADMAP-goal example → already-resolved by Phase 26.2 (deliberate same-namespace split); not a Phase 43 finding.
