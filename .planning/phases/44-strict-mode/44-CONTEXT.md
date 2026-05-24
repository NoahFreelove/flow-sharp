# Phase 44: Strict Mode - Context

**Gathered:** 2026-05-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Opt-in `enable strict;` file pragma for composers writing reliable Flow code (test fixtures, shared snippets, large pieces). A single monolithic knob that flips three axes at the input perimeter — no propagation via `use`, stdlib stays charitable by default so non-strict callers see no behavior change. Phase 44 is the v1.5 closeout consumer of Phase 42's AUDIT.md §6a + §6b + §7b site inventory; Phase 43's qualified-import surface is used for organizing strict-mode test fixtures.

**Three axes covered by the single `enable strict;` pragma:**

- **Axis A — No type coercion.** `OverloadResolver`'s convertible (+100) tier is disabled in strict files; only exact (+1000) + compatible (+500) match. `(gain buf -12.0)` errors; `(gain buf -12dB)` required. Six new explicit-conversion builtins fill the gap: `(db x)`, `(hz x)`, `(ms x)`, `(sec x)`, `(cents x)`, `(semitones x)`. Reverse direction: `(double x)`, `(float x)`, `(int x)`, `(long x)` gain overloads for all 6 tagged music types.

- **Axis B — Input-perimeter clamps + advisories become errors.** 13 input-perimeter `Math.Clamp` sites from AUDIT §6a + ~113 of the 117 `WarnOnce` advisory sites from AUDIT §6b throw `[strict] ...` errors when called from strict files (`ctx.CallerStrictMode == true`). Carve-outs: `[live]` block-entry advisory (D-v1.5-07 design-lock) + 4 `[improv]` style-pack discovery advisories (environmental, not composer-surface).

- **Axis C — Truthy / stringy / equality strictness.** Strict: `(and)`/`(or)`/`(not)` require Bool args + return Bool; `if` requires Bool; `print` requires String; `(equals a b)` cross-type returns `false` (set-theoretic); `(gt)`/`(lt)`/`(gte)`/`(lte)` cross-type **errors**. Non-strict mode receives the pre-strict bug fix: `print Int x` auto-strs via `(str x)`; `if Int x` truthy-coerces; `(and)`/`(or)` keep Lisp-style last-truthy return.

**Pre-strict bug fix bundled in scope.** `print` is registered today with signature `[StringType.Instance]` (`flow-lang/StandardLibrary/BuiltInFunctions.cs:150-154`) so `(print 42)` already fails overload resolution in BOTH modes — contradicts ergonomics-first philosophy. Phase 44 fixes non-strict `print`/`if` to be charitable FIRST, then strict re-tightens. Composer gets the right default in both modes.

</domain>

<decisions>
## Implementation Decisions

### Pragma Surface (Area 1)

- **D-01:** Pragma name is `enable strict;` — single monolithic knob covering Axes A + B + C. NOT `enable strictTypes;` (per-AUDIT-wording — mis-implies Axis A only) and NOT a three-pragma split (`strictTypes`/`strictInputs`/`strictTruthy`). Composer mental model: "strict mode on, strict mode off". Future sub-axes (`strictPurity`, etc.) can ship as separate pragmas without rewording this one.
- **D-02:** `ExecutionContext.StrictMode` boolean field set when loading the strict file. ModuleLoader push/pops `StrictMode` per the imported file's own pragma — mirrors how `MusicalContext` push/pops on block entry. No `AsyncLocal` (Flow interpreter is single-threaded today); no `StackFrame.StrictMode` (file-scoped, not proc-scoped); no AST `Program.IsStrict` attribute (consumer-adjacent placement preferred).
- **D-03:** File-scope semantics: each file's pragma governs only statements DECLARED in that file. Cross-file calls: ModuleLoader binds each proc to its declaring file's strict bit; Interpreter sets `ctx.StrictMode` from that bit on proc entry. Matches `enable justIntonation;` + `enable matchExhaustive;` precedent. Stdlib procs (declared in `flow-lang/*.flow` or C#-registered) always run with `ctx.StrictMode = false` per their own non-strict declaring context.
- **D-04:** PragmaRegistry single-line registration: add `["strict"] = "Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports."` to `flow-lang/Lexing/PragmaRegistry.cs:27`.

### Axis B Mechanism + Scope (Area 2)

- **D-05:** TWO distinct strict-mode fields on `ExecutionContext` to resolve the per-file/per-call-site tension introduced by D-03:
  - `ctx.StrictMode` — per-declaring-file (set by D-02/D-03), governs Axis A (parse-time + OverloadResolver dispatch in the executing file).
  - `ctx.CallerStrictMode` — snapshot at call dispatch of the CALLER's `StrictMode` value. Stdlib clamp + advisory sites read THIS field, not `ctx.StrictMode`. Resolves the contradiction: stdlib stays charitable when called from non-strict, errors when called from strict, AND stdlib's own declaring-file `StrictMode` stays `false` (so internal stdlib-to-stdlib calls remain charitable).
- **D-06:** Axis B scope: ALL 13 input-perimeter clamps from AUDIT §6a + ~113 of the 117 advisory sites from AUDIT §6b. Carve-outs (stay charitable):
  - `[live]` block-entry advisory — D-v1.5-07 design-lock (live coding requires charitable defaults to never die mid-set).
  - 4 `[improv]` style-pack discovery advisories (LOW priority per AUDIT §7b — environmental, not composer-surface).
  - Net errored-in-strict site count: **~126** (13 + ~113).
- **D-07:** Error message format = `[strict] <existing-site-tag> <issue>`. Mechanism: keep the existing `WarnOnce` sentinel string body verbatim, prepend `[strict] ` prefix, route through `ErrorReporter` (per CLAUDE.md error-accumulation convention) when `ctx.CallerStrictMode == true`. The existing `else { WarnOnce(...); clamp+fallback; }` non-strict path is untouched. xUnit Facts pin every error string verbatim — load-bearing for AUDIT.md §6a Column 5 "Phase 44 Strict-Mode Error Proposal" wording.

### Explicit-Conversion Builtins (Area 3)

- **D-08:** Forward direction (raw numeric → tagged music type) — 6 builtins:
  - `(db x)`, `(hz x)`, `(ms x)`, `(sec x)`, `(cents x)`: accept `Int` + `Long` + `Float` + `Double` + idempotent on target tagged type (`(db -12dB)` → `-12dB` no-op). Lossy `Double → target` follows existing C-style truncation/floor.
  - `(semitones x)`: accepts ONLY `Int` (whole-numbers-by-design per `CentType.cs:24-27` pattern — Semitone has `IsCompatibleWith(Int)` true, NOT Float/Double). Lossy `(semitones 2.5)` errors regardless of mode with `[strict] (semitones) requires Int — got Double 2.5`.
- **D-09:** All 6 forward-direction builtins are AVAILABLE IN BOTH MODES (always-available registration in `BuiltInFunctions.cs`). Composers refactoring TOWARD strict can test-drive conversions one call at a time. Mirrors `(float x)` / `(int x)` / `(double x)` / `(long x)` precedent (always available).
- **D-10:** Reverse direction (tagged music type → raw numeric) — backfill overloads for `(double x)` / `(float x)` / `(int x)` / `(long x)` accepting all 6 tagged music types (`Decibel`, `Hertz`, `Cent`, `Millisecond`, `Second`, `Semitone`). `(double -12dB)` → `-12.0`, `(float 440Hz)` → `440.0f`, `(int +2st)` → `2`, `(int 100ms)` floors → `100`. Always available both modes. Plan-phase task: backfill these overloads + xUnit pin each in `BuiltInFunctions.cs`.

### Equality + Truthy / Stringy (Area 4)

- **D-11:** Cross-type equality + comparison rules in strict:
  - `(equals a b)` cross-type → returns `false` in strict (per ROADMAP — set-theoretic, not error). `(equals 1 1.0)` → `false`. Same as non-strict for this builtin.
  - `(gt a b)` / `(lt a b)` / `(gte a b)` / `(lte a b)` cross-type → **error** in strict (no defined cross-type ordering). Error format: `[strict] cross-type comparison <T1> vs <T2> — use explicit (double x) / (int x)`.
  - Asymmetry rationale: equality returning `false` is a defensible answer ("1 is not 1.0 — different types"); ordering requires a defined cross-type rule which strict refuses to invent.
- **D-12:** `(and)` / `(or)` / `(not)` + `if` Bool requirements:
  - Strict: all four require Bool args. `(and)`/`(or)` return Bool. `(not Int)` errors with `[strict] (not) requires Bool — got Int`.
  - Non-strict (post pre-strict bug fix): `(and)`/`(or)` charitable-truthy + return last-truthy (Lisp-style, unchanged). `(not x)` non-strict: charitable-truthy where `0`/`""`/`null`/empty-collection are false. `if Int x` non-strict: truthy-coerces (`x ≠ 0` is true). `print Int x` non-strict: auto-strs via `(str x)`.
- **D-13:** Dict lookup stays type-strict by design — no Phase 44 change. Phase 26.1 already hashes Dict keys by type+value: `(get d 1)` and `(get d 1.0)` look up DIFFERENT keys (Int 1 ≠ Float 1.0). xUnit regression-pins this behavior in strict to prevent inadvertent loosening.

### Test Infrastructure (Area 4.3)

- **D-14:** Two-track testing per CLAUDE.md "Conventions" + Phase 42 RmsRegressionTests precedent:
  - **Positive `.flow` tests:** `tests/strict/test_*.flow` files (e.g., `test_strict_axis_a_overload.flow`, `test_strict_axis_b_clamps.flow`, `test_strict_explicit_conversions.flow`, `test_strict_equality.flow`). Each begins with `enable strict;` and must run to completion (validates strict files work with explicit conversions + no coercion).
  - **Negative xUnit tests:** `flow-lang.Tests/Phase44/StrictModeNegativeTests.cs` Facts pinning each of the ~126 strict error strings verbatim (no `.flow` negatives — they'd error on parse/dispatch). Schema-checked. Asserts `ErrorReporter` collected the expected `[strict] ...` message.
  - Use Phase 43 qualified imports (`use "@strict-fixtures"`) for shared test helpers across positive `.flow` tests.
  - Two-run cmp-clean determinism preserved (no PRNG sites added by Phase 44).

### Live Coding + REPL Interaction (Area 4.3)

- **D-15:** Strict applies INSIDE `live { }` blocks when the enclosing file declares `enable strict;`:
  - Initial file load + parse + Axis A dispatch run strict (D-02/D-03 file-scope semantics).
  - Live-reload re-eval also applies strict checks to the new body (composer gets type safety AT EDIT TIME via Phase 38 LIVE-03 stale-closure gating + 64-sample crossfade).
  - The `[live] entering live block` advisory itself STAYS charitable (Decision D-06 carve-out, D-v1.5-07 design-lock).
- **D-16:** REPL strict mode is a sticky session flag mirroring Phase 38 REPL polish:
  - Typing `enable strict;` at the REPL flips `ctx.StrictMode = true` for the rest of the session.
  - `:strict on` and `:strict off` meta-commands toggle explicitly (matches `:help fn` / `:quit` / `:clear` / `:stop` family).
  - Per-line input inherits the sticky session flag.

### Claude's Discretion

- Implementation-internal ordering of OverloadResolver tier-disable logic (Axis A). Plan-phase decides whether to add a `StrictTierFilter` predicate to `OverloadResolver.cs` or branch inside the existing scoring loop.
- Internal naming of `ctx.CallerStrictMode` field (e.g., `StrictModeAtCallSite` is acceptable substitute). The TWO-field design is locked (D-05); the field name itself is not load-bearing.
- Whether to vendor a `flow-lang.Tests/baselines/Phase44/` directory for any audio-affecting strict-positive `.flow` tests (most strict tests won't render audio). Plan-phase decides per-test.
- Plan-phase task ordering of HIGH vs MED vs LOW Axis B promotion. AUDIT §7b prioritization is an authoring hint, not a phase-internal scope split (D-06 ships all in-scope sites in one phase).
- Whether `(neg x)` / `(idiv x y)` / `(concat x y)` need any strict-mode tightening beyond what falls out of the OverloadResolver +100 tier disable. Default: no special-casing.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 44 Roadmap Anchor
- `.planning/ROADMAP.md` §"Phase 44: Strict Mode" — Goal statement + the three-axes structure + the pre-strict bug fix call-out + Phase 42 dependency declaration.
- `.planning/REQUIREMENTS.md` (Phase 44 REQ-STRICT-NN entries to be defined at plan-phase per ROADMAP line "Requirements: TBD (defined at plan-phase)").

### Load-Bearing Audit (Phase 42 Deliverable)
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §6a — **13 input-perimeter clamps** (Axis B scope). Column 5 "Phase 44 Strict-Mode Error Proposal" supplies the canonical error message strings; D-07 pins format with `[strict] ` prefix.
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §6b — **117 advisory sites grouped by 19 stdlib modules** with HIGH/MEDIUM/LOW priorities. D-06 in-scope: HIGH + MED + LOW minus `[live]` + 4 `[improv]` carve-outs.
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §6c — 110 charitable-fallback markers (DISCOVERY SWEEP per AUDIT §8 Limitation 5; ≤5 bespoke `if (x < 0) x = 0` clamps may have escaped the §6a regex; plan-phase scans during authoring).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §7b — Phase 44 Candidates prioritization table (HIGH/MEDIUM/LOW routing).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/input-clamps.txt` — Raw file:line refs for the 13 input-perimeter clamps.
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/advisory-sites.txt` — Raw file:line refs for all 117 `WarnOnce` advisory sites.
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/charitable-sites.txt` — 110 charitable-fallback pointers for the discovery sweep.

### Pragma System (Phase 21 Precedent)
- `flow-lang/Lexing/PragmaRegistry.cs` — Closed-set registry; D-04 adds `["strict"]` entry on line 27.
- `flow-lang/Lexing/PragmaScanner.cs` — `enable <pragma>;` parser; D-12 unknown-pragma error wiring already covers `strict` once registered.
- `flow-lang/Lexing/PragmaSet.cs` — Phase 21 D-02 pragma carrier type.
- `flow-lang/Runtime/MusicalContext.cs:97` — Block-scope pragma precedent comment (`enable justIntonation;`); D-02 mirrors this push/pop model at file scope.

### Axis A Target
- `flow-lang/TypeSystem/OverloadResolver.cs` — Specificity scoring: exact (+1000), compatible (+500), convertible (+100). Axis A disables the (+100) tier when `ctx.StrictMode == true` in the executing frame.
- `flow-lang/TypeSystem/SpecialTypes/CentType.cs:24-27` — Reference pattern for music tagged-type `IsCompatibleWith`. D-08 follows this pattern for `Semitone` (whole-numbers-by-design Int-only rule).
- `flow-lang/Runtime/Value.cs` — CLR wrapper + Flow type info; D-10 reverse-direction overloads consume the wrapped numeric.

### Runtime Plumbing
- `flow-lang/Runtime/ExecutionContext.cs` — D-02 adds `StrictMode` field; D-05 adds `CallerStrictMode` field.
- `flow-lang/Runtime/ModuleLoader.cs` — D-03 push/pop strict per declaring file at proc entry.
- `flow-lang/Runtime/StackFrame.cs` — Frame ownership (NOT used for strict state per D-02; file-scope dispatch lives in ExecutionContext).
- `flow-lang/Interpreter/Interpreter.cs` — Statement dispatch; D-05 reads `CallerStrictMode` snapshot at call boundary.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — D-01 charitable truthy + auto-str logic on `if` / `print` non-strict paths (D-12 + ROADMAP pre-strict bug fix).

### Pre-Strict Bug Fix Sites
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:150-154` — `print` registered as `[StringType.Instance]`; D-12 makes non-strict charitable (auto-`(str)`), strict re-tightens.
- `flow-lang/StandardLibrary/stdlib.cs` (or wherever `if` is registered) — `if` Bool-only today; D-12 makes non-strict truthy-coerce, strict re-tightens.

### Test Infrastructure Precedents
- `flow-lang.Tests/Helpers/RmsRegressionTests.AssertRmsWithinTolerance` — CLAUDE.md "Conventions" RMS-windowed pattern (not directly applicable here — strict tests are mostly non-rendering — but documents the xUnit Facts + .flow integration smoke split).
- `flow-lang/improv/styles/*.flow` — Phase 43 qualified-imports precedent (composer-editable Flow files); `tests/strict/*.flow` follows the same surface for sharing fixtures.
- `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` — Reference-identity types allowlist precedent (AUDIT §8 Limitation 2); D-13 Dict lookup regression-pin follows a similar HashSet baseline.

### Phase 43 Surface (Optional Plumbing)
- `flow-lang/Runtime/ExecutionContext.cs` `ModuleRegistry` — Phase 43 D-02 qualified-call routing; D-14 strict test fixtures use this surface for `use "@strict-fixtures"`.
- `flow-lang/Parsing/Parser.cs` 4-token-lookahead qualified-call disambiguator (Plan 43-03) — no strict-mode change here; only relevant for strict-mode tests organizing fixtures.

### Live Coding + REPL (Phase 38 Precedents)
- `flow-lang/Runtime/LiveBlockRegistry.cs` — Phase 38 LIVE-01 FNV-1a `BlockId(SourceLocation)` routing; D-15 strict applies to initial parse + live-reload re-eval per declaring file.
- `flow-lang/Interpreter/LambdaCaptureAuditor.cs` — Phase 38 LIVE-03 stale-closure gate; D-15 strict checks run via the existing re-eval path (no new plumbing).
- `flow-interpreter/LiveStatusPanel.cs` — Phase 38 LIVE-02 4-row ANSI panel; D-16 REPL `:strict on/off` advisory text routes through `PublishAdvisory` (sticky line).
- `flow-interpreter/ReplCommands.cs` (or equivalent) — Phase 38 REPL-02 meta-command family (`:help fn` / `:quit` / `:clear` / `:stop`); D-16 adds `:strict on` + `:strict off`.

### Project-Level + Memory
- `CLAUDE.md` §"Conventions" — Two-run cmp-clean determinism contract; D-14 preserves this (no PRNG sites added).
- `CLAUDE.md` §"Language Features" — Pragma reservation list + tuning-pragma precedent ("six keywords... reserved").
- External memory `feedback_strict_mode_design.md` — User's strict-mode design pattern: file-scoped opt-in pragma, input-perimeter only, charitable stays the default everywhere else. D-01..D-16 implement this pattern verbatim.
- External memory `feedback_charitable_interpretation.md` — The user-locked default this phase OPTS INTO REVERSING for strict files only. Tension flagged in ROADMAP "Tension flag" — resolved by file-scoped opt-in + carve-outs (D-06 `[live]` + `[improv]`).
- External memory `project_pre_public_no_legacy_burden.md` — D-v1.5-01 single-commit-no-deprecation latitude. Phase 44 ships all error wording + builtin registration in one commit per plan.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PragmaRegistry.KnownPragmas` dictionary — D-04 single-line addition. Existing levenshtein "did-you-mean" suggester (`LevenshteinHelper.SuggestNearest`) handles typo recovery for free (`enable stric;` → "did you mean strict?").
- `PragmaScanner` already routes unknown pragmas through `D-12` error path (ErrorReporter) — strict-mode entry test for free once registered.
- `ExecutionContext.MusicalContext` push/pop precedent — D-02 strict push/pop on file-load mirrors this exactly (per file, not per block — but same lifecycle shape).
- `WarnOnce` infrastructure throughout stdlib — D-07 keeps the existing sentinel string, just swaps the consumer (WarnOnce vs ErrorReporter) at the call site.
- `ErrorReporter` already collects errors instead of throwing per CLAUDE.md error model — D-07 strict errors aggregate cleanly with existing error surfaces.
- `OverloadResolver` specificity scoring — D-01 disabling the +100 convertible tier in strict is a single predicate change, not a rewrite.
- `Value.cs` CLR-wrapper + Flow type info — D-10 reverse-direction conversion overloads consume the underlying CLR value via existing `.As<T>()` extractors.

### Established Patterns
- **File-scope pragma push/pop on load** (Phase 21 + Phase 23 + Phase 35): `enable hAsB;`, `enable justIntonation;`, `enable matchExhaustive;` all follow declare-at-file-top + scanner-detected + ExecutionContext-flagged + no-propagation-via-`use`. D-01..D-04 replicate this surface verbatim.
- **Per-site `if (strict) throw; else fallback` opt-in idiom** (cited in ROADMAP Axis B description): ~126 sites change from `WarnOnce(...)` to `if (ctx.CallerStrictMode) ErrorReporter.Report(...); else WarnOnce(...)`. Per-site `[strict] <tag>` prefix is mechanical.
- **Always-available conversion builtins** (`(float x)`/`(int x)`/`(double x)`/`(long x)`): D-09 + D-10 mirror this surface — mode-independent registration, accept-numeric + idempotent + lossy-floor convention.
- **xUnit Facts pinning error strings verbatim** (Phase 42 ClampGrepConsistencyTests, Phase 32 TuningDescriptionTests): D-14 negative xUnit suite follows this precedent for ~126 strings.
- **Two-run cmp-clean determinism** (CLAUDE.md Conventions): D-14 preserves — strict mode itself is deterministic (no PRNG additions); existing PRNG-routed sites (`granular`, `markov`, `lsystem`, `jam`, etc.) unaffected because strict only changes their input-domain check, not their output sampling.
- **REPL meta-command family** (`:help` / `:quit` / `:clear` / `:stop` / `:help fn`): D-16 adds `:strict on` / `:strict off` per the same registration site.

### Integration Points
- `flow-lang/Lexing/PragmaRegistry.cs:27` — D-04 one-line addition.
- `flow-lang/Runtime/ExecutionContext.cs` — D-02 + D-05 two new fields.
- `flow-lang/Runtime/ModuleLoader.cs` — D-03 proc-entry strict bit lookup (Bind declaring file's strict bit to each `ProcDeclaration` at module load; restore on proc entry).
- `flow-lang/Interpreter/Interpreter.cs` — D-05 `CallerStrictMode` snapshot on call dispatch.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — D-12 + pre-strict bug fix: `if` non-strict truthy-coerce; `print` non-strict auto-str; strict-mode error paths.
- `flow-lang/TypeSystem/OverloadResolver.cs` — D-01 disable +100 tier when `ctx.StrictMode == true`.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — D-08 + D-09 + D-10 builtin registrations.
- `flow-lang/StandardLibrary/{Patterns,Generative,Improv,Audio/DSP,Audio/Sfz,Notation,Harmony}/` — ~126 sites for D-06/D-07 advisory-to-error rewiring.
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` lines 649/650/657/658/666/667/785/821/904/960/1106 — Plus `Swing.cs` strength + factor + `repeat reps` — the 13 input-perimeter clamp sites from AUDIT §6a.
- `flow-interpreter/ReplCommands.cs` (or equivalent meta-command registration site) — D-16 `:strict on` + `:strict off`.
- `tests/strict/*.flow` (new directory) + `flow-lang.Tests/Phase44/StrictModeNegativeTests.cs` (new file) — D-14 test infrastructure.

</code_context>

<specifics>
## Specific Ideas

- **"JS-mode-off" framing.** Composer mental model from D-01: `enable strict;` is one knob that turns off JavaScript-style implicit coercion + silent input clamping + truthy hand-waving. Plan-phase docs + tutorials should reach for this framing rather than the Axis-A/B/C taxonomy (taxonomy is for implementation discussion, not composer onboarding).
- **AUDIT.md §6a Column 5 is binding for the 13 clamp messages.** Plan-phase author MUST copy those error strings verbatim (with the `enable strictTypes` → `enable strict` substitution per D-01). The proposed text was composer-approved at Phase 42 closeout (2026-05-24 auto-approval via `/gsd:execute-phase --auto`).
- **REPL `:strict off` is a real meta-command, not just a re-`enable` toggle.** D-16 explicitly: typing `enable strict;` flips ON sticky; `:strict off` flips OFF. Plan-phase ensures these are symmetric.
- **Showcase a strict file in `tests/strict/`** (analogous to Phase 34's `examples/symphony/symphony.flow` integration smoke) — a small piece (~16 bars, single instrument) that demonstrates a composer using `(db x)`, `(hz x)`, `(cents x)` explicit conversions naturally, NOT a contrived type-error gauntlet. Optional but discoverable.
- **`enable strict;` + `enable justIntonation;` compose.** Both file-scope pragmas. Strict file using JI tuning should Just Work — neither pragma is exclusive of the other. Plan-phase verifies via `tests/strict/test_strict_with_justintonation.flow`.

</specifics>

<deferred>
## Deferred Ideas

- **Future sub-axis pragmas (`strictPurity`, `strictLengths`, etc.).** D-01 leaves room for future composable strict knobs that don't fit the monolithic `enable strict;` scope. Not Phase 44.
- **Strict mode for module-level (`module mymod`) export contracts.** Phase 43 introduced `module <name>` declarations; future v1.6+ might add `module <name> strict;` to opt entire modules into strict. Not Phase 44 — file-scope is the v1.5 contract.
- **Cosmetic explicit-overload backfill for the 70+ `§5b` candidates** (`abs`/`add`/`crescendo`/etc.) per AUDIT §7c. Works today via widening in non-strict; works in strict via compatible (+500) tier. Not load-bearing for Phase 44.
- **`Int → NoteValue` explicit conversion** per AUDIT §7c (cosmetic). v1.6-backlog.
- **`readMidi(String) → Song` + `readMusicXML(String) → Song` registry builtins** per AUDIT §7c. v1.6-backlog.
- **Promote `scripts/StdlibAuditor` to CI health check** (RESEARCH §Open Question 3, AUDIT §7c LOW). Recurring audit catches Axis B regressions. v1.6-backlog.
- **`FunctionSignature.ReturnType` field addition** per AUDIT §8 Limitation 1. Audit-harness improvement, no composer-facing impact. v1.6-backlog.
- **Strict mode propagation rules for `--watch` reload incidents.** Phase 38 LIVE-02 file-watch already 200ms-debounces; D-15 ensures strict re-applies on reload. Edge cases (rename `enable strict;` mid-session) are out of scope — fall back to existing reload error reporting.

</deferred>

---

*Phase: 44-strict-mode*
*Context gathered: 2026-05-24*
