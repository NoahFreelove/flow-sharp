# Phase 43: Module Names & Qualified Imports - Context

**Gathered:** 2026-05-24
**Status:** Ready for planning
**Mode:** auto (single-pass; recommended-option auto-selection per `modes/auto.md`)

<domain>
## Phase Boundary

Address growing stdlib name-collision pressure (already-real: `gain` vs `volume`; imminent: `math.sin` vs other `sin`) by introducing:

1. **File-level module declarations** — `module <name>` as the first non-comment statement at the top of `.flow` files. Reserved keyword. No filename-derived auto-naming.
2. **Qualified access** — `math.sin` syntax routes to a registered module's function. Unqualified-by-default (composers keep typing `sin`, not `math.sin`); qualified access is the explicit escape hatch when a composer wants disambiguation or when collisions are otherwise ambiguous.
3. **`use` import extension** — `use "@math"` continues to import the module's exported procs into the unqualified namespace AND registers the module name `math` for qualified-access lookups.
4. **Collision policy** — last-import-wins for unqualified names with a one-shot `[module] '{name}' from '{module}' shadows '{name}' from '{prior_module}' — qualify with '{module}.{name}'` advisory at registration time. No hard error; composer can opt into qualified access.
5. **AUDIT.md §7a Phase 43 HIGH-priority backfill** — concurrent with the module work:
   - `(beatToSec Beat) → Second` + `(secToBeat Second) → Beat` tempo-context-aware conversion builtins (closes the `Beat` orphan-type gap from `42-AUDIT.md §1`).
   - Beat-companion overloads where AUDIT explicitly names them: `delay(Buffer, Beat)`, `renderBarAtBeat(Sequence, Beat)`.

**Explicitly out of scope for Phase 43** (deferred elsewhere):

- `pitchShift(Buffer, Hertz)` — AUDIT §7a routes here as LOW but flags a design decision. Hertz-shift semantics (target absolute pitch) differ from cents-relative shift semantics; conflating them in one overload would confuse composers. Defer to v1.6-backlog as `pitchShiftTo(Buffer, Hertz, refHz)`.
- Strict-mode flips of advisory sites — those are Phase 44 (`enable strictTypes;`).
- Cross-module re-exports (`module foo re-exports @bar`) — v1.6+ topic if it becomes a real ergonomic problem.
- Module aliasing in import (`use "@math" as m`) — v1.6+ if composers ask. Don't speculatively build it now.

</domain>

<decisions>
## Implementation Decisions

### Syntax

- **D-01:** `module <name>` declaration is the first non-comment statement of a `.flow` file. The `module` keyword becomes reserved. Modules names follow identifier rules (`[a-zA-Z_][a-zA-Z0-9_]*`). Files without a `module` declaration are NOT registered for qualified access; their procs still export into the unqualified namespace as today (preserves existing composer-script + test-script behavior).
- **D-02:** Qualified access uses the existing `x.y` `MemberAccessExpression` AST node. No new AST surface. Disambiguation between module-qualified call (`math.sin(0.5)` → call `sin` from `math`) and existing instance-member access (`chord.root` → field/method on `Chord` value) happens at evaluation time in `ExpressionEvaluator`: the LHS is checked against the module registry FIRST; if the LHS is a registered module name, dispatch as a module-qualified function call; otherwise fall through to the existing member-access dispatch path. Backward-compatible — existing `chord.root` / `song.sections` / `<<a, b>>@0` patterns are unaffected.
- **D-03:** Reserved keyword choice: **`module`**. Familiar from Haskell / OCaml / F# / Rust. Single obvious form. No alternatives (`mod`, `namespace`, `pkg`, etc.) — Flow stays Anglophone-musical-DSL-style.

### Resolution & Collision

- **D-04:** **Last-import-wins** for unqualified names. When `use "@a"` and `use "@b"` both export `sin`, the later `use` line's `sin` wins for unqualified calls. Emit one-shot stderr advisory once per shadow pair:
  ```
  [module] 'sin' from 'b' shadows 'sin' from 'a' — qualify with 'a.sin' or 'b.sin' to disambiguate
  ```
  Composers can ignore the advisory (charitable default) or move to qualified access on the affected call sites. Matches `feedback_charitable_interpretation` memory.
- **D-05:** Module registration happens at `use` time. The `ModuleLoader` already loads the file and runs its top-level statements; it should additionally parse the `module <name>` declaration and register `name → ExportedProcSet` in a process-global `ModuleRegistry`. `use "@x"` continues to expose the procs in the caller's `ExecutionContext` UNQUALIFIED (back-compat); the qualified-access path is an additional lookup.
- **D-06:** Duplicate module-name registrations (two files declare `module math`): emit a one-shot advisory `[module] duplicate module name 'math' — last load wins`. Same charitable pattern. Not an error.

### Stdlib Module Assignments

- **D-07:** Migrate existing `flow-lang/*.flow` stdlib files to declare their module names. Assignments below are the recommended default; planner may adjust:

  | File | Module | Notes |
  |------|--------|-------|
  | `audio.flow` | `audio` | obvious |
  | `bars.flow` | `bars` | obvious |
  | `collections.flow` | `collections` | obvious |
  | `composition.flow` | `composition` | obvious |
  | `generative.flow` | `generative` | obvious |
  | `improv.flow` | `improv` | obvious |
  | `notation-io.flow` | `notation` | export/import (MusicXML, LilyPond, ABC, MML) |
  | `notation.flow` | `notes` | mostly bar-level Note helpers — rename to avoid `notation` collision with notation-io |
  | `osc.flow` | `osc` | obvious |
  | `patterns.flow` | `patterns` | obvious |
  | `sfz.flow` | `sfz` | obvious |
  | `std.flow` | _no module declaration_ | always-on prelude — keeps existing unqualified-only behavior |
  | `test.flow` | `test` | obvious |

  The `notation.flow` ↔ `notation-io.flow` collision is the only real conflict. Recommend the planner re-evaluate whether to fold one into the other vs. rename `notation.flow` → `notes` module (the latter is the captured recommendation).

### Beat Backfill (AUDIT §7a HIGH-priority concurrent work)

- **D-08:** Add two new free builtins at `flow-lang/StandardLibrary/Audio/` (or `Harmony/` — planner discretion):
  - `(beatToSec Beat) → Second` — reads active `tempo` from `ExecutionContext.MusicalContext` stack.
  - `(secToBeat Second) → Beat` — reads active `tempo` from the same stack.

  When no `tempo` is active (e.g., outside any `tempo N { ... }` block), default to `120 BPM` AND emit a one-shot stderr advisory `[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)`. Matches Flow's existing default-and-warn pattern. Two-run cmp-clean preserved (default is deterministic).
- **D-09:** Add Beat-companion overloads ONLY where `42-AUDIT.md §1` explicitly lists them:
  - `delay(Buffer, Beat)` — implemented as `delay(buf, beatToSec(beat))`.
  - `renderBarAtBeat(Sequence, Beat)` — converts to seconds before invoking the existing bar-render path.

  Do NOT speculatively add Beat overloads to `reverb` / `stretch` / `pitchShift` / etc. Composers can compose `(delay buf (beatToSec 0.5b))` if needed. Tight scope per `feedback_ergonomics_priority` "don't bloat the API for hypothetical needs".
- **D-10:** The Beat-orphan-pin xUnit fixture (`AuditHarnessTests.cs` line ~151 from Phase 42) currently asserts `BeatType appears in the orphans array`. After Phase 43 ships the Beat-companion overloads, this fixture flips to assert `BeatType is NO LONGER in the orphans array`. Update the test in lockstep with the production change; document the flip in `43-VERIFICATION.md` and in the test's XML comment.

### Migration Tooling

- **D-11:** Pre-traction no-deprecation latitude (`project_pre_public_no_legacy_burden`) is ACTIVE. Breaking syntax ships in one commit. In-repo `.flow` migrators are sufficient (no `flow migrate` subcommand needed yet). The migration touches:
  - The 13 `flow-lang/*.flow` stdlib files (add `module <name>` declarations).
  - Any `.flow` test script under `tests/test_*.flow` that defined a proc with a name colliding with a now-qualified module function — none expected per current grep, but the migrator runs the test suite and flags any post-migration regression.
- **D-12:** No composer-facing `flow migrate` CLI subcommand. The in-repo migration is a one-shot sed/Python pass committed alongside the language change. If a third-party fork appears (see external memory `project_pre_public_no_legacy_burden`), reconsider.

### Claude's Discretion

- Where the registry lives (process-global vs. per-FlowEngine). Either is defensible; planner picks based on existing `InternalFunctionRegistry` patterns.
- Exact stderr advisory wording — pattern is fixed (`[module] ...`) but specific phrasing planner-discretion.
- Whether the module-registry lookup happens in `ExpressionEvaluator` directly or via a `MemberAccessResolver` helper class. Existing dispatcher patterns guide this.
- Test-fixture file layout: `flow-lang.Tests/Integration/Phase43/` per Phase 42's precedent.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 42 audit deliverable (load-bearing for Phase 43 backfill)

- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §1 — Beat orphan finding (anchor).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §2 — Missing-conversion table (`Beat ↔ Second` HIGH-priority routing to Phase 43).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §5a — `pitchShift(Buffer, Hertz)` design-decision flag (deferred per D-01).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §7a — Phase 43 routing table (HIGH/HIGH/LOW priorities).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/type-signature-graph.json` — Machine-readable orphans/asymmetries/overload-gap candidates for the Beat backfill.

### Existing import/module infrastructure (must extend, not rewrite)

- `flow-lang/Runtime/ModuleLoader.cs` — current `use "@x"` resolver + circular-import detection + stdlib path resolution. Phase 43 extends this with module-name parsing + registration.
- `flow-lang/Parsing/Parser.cs` — `ParseImportStatement()` is the current `use` handler; `MemberAccessExpression` is already emitted for `x.y`. Phase 43 adds a top-of-file `module <name>` parser branch + extends the resolution path for `MemberAccessExpression` in `ExpressionEvaluator`.
- `flow-lang/Ast/Statements/ImportStatement.cs` — current AST node.
- `flow-lang/Ast/Expressions/MemberAccessExpression.cs` — reused for qualified access per D-02.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — dispatcher for `MemberAccessExpression` evaluation; Phase 43 adds the module-registry-first-check branch.

### Beat backfill targets

- `flow-lang/TypeSystem/SpecialTypes/BeatType.cs` — the orphan type (no signature changes needed in Phase 43; Phase 43 adds USES of `Beat` at builtin call sites).
- `flow-lang/Runtime/MusicalContext.cs` — `ActiveTempo` reader used by `beatToSec` / `secToBeat`.
- `flow-lang/StandardLibrary/Audio/DSP/` — current `delay` builtin registration site.
- `flow-lang/StandardLibrary/Audio/ClassicalComposition.cs` — `renderBarAtBeat` registration site (or equivalent — planner verifies).

### Project rules + conventions

- `CLAUDE.md` — full project conventions (especially "Goals & Non-Goals", "Music Types Quick Reference" — the `Beat` row is explicitly the gap Phase 43 closes).
- External memory `project_pre_public_no_legacy_burden.md` — confirms breaking-change-in-one-commit latitude.
- External memory `feedback_ergonomics_priority.md` — drove D-09 tight-scope decision.
- External memory `feedback_charitable_interpretation.md` — drove D-04 + D-08 advisory-not-error decisions.

### Phase 42 audit harness (still useful after Phase 43)

- `scripts/StdlibAuditor/Program.cs` — re-run after Phase 43 ships to confirm `Beat` exits the orphan list. The xUnit pin in `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` flips polarity per D-10.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`MemberAccessExpression` AST node** (`flow-lang/Ast/Expressions/MemberAccessExpression.cs`) — already handles `x.y` syntax. Reused for qualified access per D-02; no new AST type required.
- **`ModuleLoader`** (`flow-lang/Runtime/ModuleLoader.cs`) — already loads `.flow` stdlib files via `use "@x"`. Phase 43 adds a small `module <name>` parser hook + a `ModuleRegistry` field. The existing circular-import detection + `AdditionalSearchPaths` (Phase 30 XDG config) stay untouched.
- **`MusicalContext.ActiveTempo`** (`flow-lang/Runtime/MusicalContext.cs`) — the live tempo reader `beatToSec` / `secToBeat` consume. Phase 28 voice-pool work, Phase 32 tuning work, and Phase 37 piano-release work all wired this pattern (`ExecutionContext.MusicalContext.Active*` reads). Mirror that style for D-08.
- **`InternalFunctionRegistry.EnumerateSignatures`** (added in Phase 17 LSP work, surfaced again in Phase 42) — the audit harness consumes this same API to verify Phase 43's Beat-companion registrations land in the live registry. No new internal API needed.

### Established Patterns

- **Charitable-default-with-advisory** — Phase 36 D-v1.5-05, Phase 37 `[granular] unknown windowing → Hann + advisory`, Phase 39 D-39-17 `[abc] unknown ornament dropped + advisory`. Phase 43 collision and tempo-default advisories MUST follow the same `[module] ...` / `[beatToSec] ...` bracket-tag convention. Use the existing `WarnOnce` helper (117 sites inventoried in Phase 42 — well-precedented).
- **Backward-compatible-on-extension** — Phase 26.1 added `~>` tuple-unpack flow while leaving `->` untouched; Phase 32 added `tuning t { }` block while leaving file-scope pragmas working. Phase 43's module work MUST leave existing `use "@x"` working unqualified-by-default; opt-in qualified access only fires when the composer types a `<module-name>.<fn-name>` pair AND the LHS matches the registry.
- **Reserved-keyword adds are cheap** — `tempo`, `timesig`, `key`, `swing`, `voicePool`, `tuning` were all added without breakage. `module` joins this set per D-03.
- **One-commit breaking changes** — Phase 26 `op-standardization-prefix-only` and Phase 26.1 are recent examples. The 13-file stdlib migration ships in one commit per D-11.

### Integration Points

- `ModuleLoader` ↔ `Parser`: parser emits a new `ModuleDeclarationStatement` (or reuses `ExpressionStatement` with a tagged literal — planner picks) for the top-of-file `module <name>` line; `ModuleLoader` reads it during `use` resolution and registers the name.
- `ExpressionEvaluator` ↔ `ModuleRegistry`: new check inside the `MemberAccessExpression` evaluator branch — registry lookup first, instance-member fallback second.
- `ClassicalComposition.cs` (or wherever `renderBarAtBeat` lives) ↔ `BuiltInFunctions.cs`: new signature overloads register the Beat companions per D-09.
- `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs`: polarity-flip on the Beat-orphan fixture per D-10 — the fixture becomes a regression that fails if Phase 43 doesn't backfill.

</code_context>

<specifics>
## Specific Ideas

- The example in the ROADMAP goal — `math.sin` — is illustrative. There is no `math.flow` stdlib today. Phase 43 does NOT add a `math` module; it adds the *mechanism* for qualified access, and the stdlib migration covers the 13 existing `.flow` files per D-07.
- If/when a future phase adds `math.flow`, it gets a `module math` declaration like any other stdlib file.
- The `gain` vs `volume` collision named in the ROADMAP goal is real today (Phase 26.2 split them deliberately) but it's a SAME-namespace overload-resolution case, not a cross-module case — so it doesn't need Phase 43's mechanism. AUDIT.md doesn't list it as a Phase 43 finding. Out of scope.

</specifics>

<deferred>
## Deferred Ideas

- **`pitchShift(Buffer, Hertz)` → v1.6-backlog** — Hertz-shift semantics (target absolute pitch given a reference) differ from cents-relative shift semantics (delta). Better as a new builtin `pitchShiftTo(Buffer, Hertz, refHz)` with explicit reference pitch. AUDIT.md §5a flagged this needs a design decision; Phase 43 punts.
- **Cross-module re-exports** (`module foo re-exports @bar`) — v1.6+ if it becomes a real ergonomic problem. Today no composer is hitting this.
- **Module aliasing on import** (`use "@math" as m`) — v1.6+ if composers ask. Don't speculatively build it. The unqualified-by-default default mostly removes the demand.
- **Composer-facing `flow migrate` subcommand** — v1.6+ or when traction appears (`project_pre_public_no_legacy_burden`). The in-repo one-shot migrator is enough for now.
- **Strict-mode flips of advisories from D-04 / D-06 / D-08** — Phase 44 territory (`enable strictTypes;`).
- **Whether `notation.flow` should merge into `notation-io.flow` instead of being renamed `notes`** — flagged in D-07 as planner-revisit during execution.

</deferred>

---

*Phase: 43-module-names-qualified-imports*
*Context gathered: 2026-05-24 via /gsd:discuss-phase 43 --auto*
