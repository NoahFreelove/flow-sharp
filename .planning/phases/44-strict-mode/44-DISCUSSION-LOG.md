# Phase 44: Strict Mode - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-24
**Phase:** 44-strict-mode
**Areas discussed:** Pragma name + strict-flag plumbing, stdlib-charity boundary precise semantics, Explicit-conversion builtin design, Equality + truthy/stringy precise scope

---

## Pragma Name + Axis Bundling (1.1)

| Option | Description | Selected |
|--------|-------------|----------|
| Single `enable strict;` | One knob flips all three axes (A: no coercion, B: clamps→errors + advisories→errors, C: truthy/stringy/equals). Easiest composer mental model. Matches ROADMAP wording. | ✓ |
| Composable axes: `strictTypes;` + `strictInputs;` + `strictTruthy;` | Each axis is its own pragma. Composer can opt into just Axis A without enabling 117 advisory-to-error sites. | |
| Single `enable strictTypes;` (per AUDIT) | Matches AUDIT.md §6a wording verbatim but mis-implies Axis A only. | |

**User's choice:** Single `enable strict;` (Recommended)
**Notes:** Resolves the AUDIT/ROADMAP wording inconsistency in ROADMAP's favor. Future sub-axes (`strictPurity`, etc.) ship as separate pragmas without rewording this one. Captured as D-01.

---

## StrictMode Flag Plumbing Location (1.2)

| Option | Description | Selected |
|--------|-------------|----------|
| Per-script `ExecutionContext.StrictMode` set when loading file | PragmaScanner detects `enable strict;`; FlowEngine sets `ctx.StrictMode = true` before executing the file. Push/pop on `use "@other"` cross-file. Mirrors how `MusicalContext` push/pops on block entry. | ✓ |
| Per-stack-frame `StackFrame.StrictMode` | Each frame carries its own value. Cleanly handles re-entrancy but adds a field to every frame. | |
| `AsyncLocal<bool>` mirror of `PianoSynthesizer.CurrentReleaseSec` | Thread-local that follows async flow. Overkill — interpreter is single-threaded. | |
| AST attribute on `Program` node | Parser stamps `Program.IsStrict = true`. Equivalent to option 1; differs only in field location. | |

**User's choice:** Per-script `ExecutionContext.StrictMode` set when loading file (Recommended)
**Notes:** Captured as D-02. Consumer-adjacent placement beats AST attribute for field accessibility from OverloadResolver + stdlib sites.

---

## Cross-File Import Strict Propagation (1.3)

| Option | Description | Selected |
|--------|-------------|----------|
| Each file's pragma governs its own code | While executing statements DECLARED in strict file: `ctx.StrictMode = true`. While executing statements DECLARED in non-strict file: `ctx.StrictMode = false`, even when call originated in strict. Matches `enable justIntonation;` + `enable matchExhaustive;` precedent. | ✓ |
| Caller's strict propagates dynamically | `ctx.StrictMode` stays true while ANY frame in the call stack is from a strict file. Maximizes strict's reach but contradicts ROADMAP "stdlib stays charitable". | |
| File pragma applies only to parse-time checks (Axis A), runtime always charitable | Axis A type-coercion at parse + dispatch only. Axis B + truthy stay charitable everywhere. Simpler but neuters 117 advisory promotions. | |

**User's choice:** Each file's pragma governs its own code (Recommended)
**Notes:** Tension flagged: stdlib procs would never see StrictMode=true, neutering Axis B. Resolved in Area 2.1 via `CallerStrictMode` snapshot. Captured as D-03.

---

## Axis B Fire Mechanism (2.1)

| Option | Description | Selected |
|--------|-------------|----------|
| Caller's strict propagates via `CallerStrictMode` snapshot | Interpreter snapshots caller's `ctx.StrictMode` into a separate field at call dispatch. Stdlib clamp/advisory sites read THIS field, not `ctx.StrictMode`. Two distinct semantics, two distinct fields. | ✓ |
| Revise Decision 1.3 — strict propagates fully up the call stack | Reinterpret D-03 to keep one `StrictMode` field that propagates dynamically. | |
| Axis B validation happens at the CALL SITE before dispatch | Requires every stdlib proc to declare valid input ranges as machine-readable metadata. Heavy plumbing. | |
| Stdlib clamp + advisory sites get a `StrictBoundary` helper | Runtime walks call stack to find the nearest non-stdlib frame's declaring file. Slower at hot-path sites. | |

**User's choice:** Caller's strict propagates via `CallerStrictMode` snapshot (Recommended)
**Notes:** Captured as D-05. Two-field design resolves the D-03/D-06 tension cleanly: per-declaring-file governs Axis A; per-call-site snapshot governs Axis B.

---

## Axis B Scope (2.2)

| Option | Description | Selected |
|--------|-------------|----------|
| All Axis B sites in scope: 13 clamps + 117 advisories minus carve-outs | Ship full Axis B coverage minus `[live]` design-lock + 4 `[improv]` style-pack discovery. Net ~126 sites. | ✓ |
| HIGH only, defer MED+LOW to v1.6 | Phase 44 ships only AUDIT §7b HIGH = 13 clamps + 59 advisories. MED+LOW deferred. | |
| All sites including `[live]` block-entry | Strictest interpretation: strict files cannot use `live { }` blocks. Restrictive. | |
| Composer-tunable via sub-pragmas | Three-level dial (`strict` / `strictAll` / `strictLive`). Contradicts D-01 single-pragma simplicity. | |

**User's choice:** All Axis B sites in scope minus carve-outs (Recommended)
**Notes:** Captured as D-06. Carve-outs: `[live]` (D-v1.5-07 design-lock), 4 `[improv]` style-pack discovery advisories (environmental, not composer-surface).

---

## Error Message Format (2.3)

| Option | Description | Selected |
|--------|-------------|----------|
| `[strict] <site-tag> <issue>` matching existing advisory sentinel | Reuse existing WarnOnce sentinel verbatim, swap throw via ErrorReporter, prepend `[strict]`. Composer mental model: "advisory becomes error with `[strict]` prefix". | ✓ |
| Pure `[strict] <issue>` without site tag | Drop per-site tag. Composer loses subsystem signal. | |
| Free-form per-site | No template; ad-hoc per site. ~126 ad-hoc strings to maintain. | |
| Structured error with severity + suggestion | Multi-line with remediation hint. Heaviest to author. | |

**User's choice:** `[strict] <site-tag> <issue>` matching existing advisory sentinel (Recommended)
**Notes:** Captured as D-07. xUnit Facts pin every error string verbatim; AUDIT §6a Column 5 is the canonical source for the 13 clamp messages (substitute `enable strictTypes` → `enable strict` per D-01).

---

## Explicit-Conversion Builtin Inputs (3.1)

| Option | Description | Selected |
|--------|-------------|----------|
| Accept all numeric widening + idempotent for target type | Each builtin accepts Int/Long/Float/Double + passes through if already target type. `(semitones x)` accepts only Int per CentType.cs pattern. | ✓ |
| Strict-only — only accept `Double` | Composers must `(double x)` first if Int/Float. Two-call chain everywhere. Composer ergonomics regression. | |
| Accept all numeric + reject already-tagged inputs | `(db -12dB)` errors. Hostile to function composition. | |
| Accept all numeric + string parsing | `(db "-12")` parses string. Adds an input perimeter strict is supposed to eliminate. | |

**User's choice:** Accept all numeric widening + idempotent for target type (Recommended)
**Notes:** Captured as D-08. `(semitones x)` Int-only follows CentType.cs whole-numbers-by-design rule; lossy `(semitones 2.5)` errors regardless of mode.

---

## Explicit-Conversion Builtin Availability (3.2)

| Option | Description | Selected |
|--------|-------------|----------|
| Always available, mode-independent | Register unconditionally. Composers can use defensively even in non-strict files. Mirrors `(float x)`/`(int x)`/`(double x)`. | ✓ |
| Strict-only — error if called from non-strict | Forces strict adoption to use them. Hostile to incremental migration. | |
| Available but advisory in non-strict | Emits `[advisory] (db) explicit conversion...`. Noise. | |

**User's choice:** Always available, mode-independent (Recommended)
**Notes:** Captured as D-09. Supports incremental refactor toward strict — composers can test-drive conversions one call at a time.

---

## Reverse-Direction Conversion (3.3)

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — unwrap tagged to raw numeric | `(double -12dB)` → -12.0, `(float 440Hz)` → 440.0f, `(int +2st)` → 2. Available both modes. Plan-phase backfills overloads in BuiltInFunctions.cs. | ✓ |
| No — must compose via arithmetic widening | `(double -12dB)` errors. Composers must use arithmetic tricks. Bad for refactoring toward strict. | |
| Yes for `(double x)`/`(float x)` only | Lossy `(int -12dB)` rejected. Pedantic; doesn't match `(int 2.5)` floor behavior. | |

**User's choice:** Yes — unwrap tagged to raw numeric (Recommended)
**Notes:** Captured as D-10. `(double x)`/`(float x)`/`(int x)`/`(long x)` gain overloads for all 6 tagged music types.

---

## Equality + Comparison Cross-Type (4.1)

| Option | Description | Selected |
|--------|-------------|----------|
| All 4 comparisons require same-type args in strict | `(equals 1 1.0)` → false (locked); `(gt 1 1.0)` → error. Consistent with Axis A: convertible (+100) tier disabled. | ✓ |
| Only `(equals)` rejects cross-type; comparisons coerce | Inconsistent: equality strict, ordering loose. Hostile mental model. | |
| Comparisons coerce; equals returns false because it's a semantic decision | Inconsistent with "no coercion" Axis A spirit; comparisons become a hole in strict. | |

**User's choice:** All 4 comparisons require same-type args in strict (Recommended)
**Notes:** Captured as D-11. Asymmetry: `equals` returns false (set-theoretic, defensible); `gt`/`lt`/`gte`/`lte` error (no defined cross-type ordering).

---

## Bool Ops + Truthy (4.2)

| Option | Description | Selected |
|--------|-------------|----------|
| Strict: Bool args + Bool return; non-strict: charitable truthy | Strict: `(and 1 "foo")` errors. Non-strict: returns last truthy `"foo"`. `if Int x` non-strict truthy-coerces; pre-strict bug fix retains. | ✓ |
| Strict requires Bool args but PRESERVES last-truthy return | Half-measure: only `if` requires Bool in strict, `(and)` keeps last-truthy. Complicates the rule. | |
| Both modes require Bool everywhere | Tighten globally. Breaks composer ergonomics for existing scripts. | |

**User's choice:** Strict: Bool args + Bool return; non-strict: charitable truthy (Recommended)
**Notes:** Captured as D-12. Non-strict mode receives pre-strict bug fix: `print Int x` auto-strs; `if Int x` truthy-coerces; `(and)`/`(or)` keep Lisp-style last-truthy.

---

## Final Scope (4.3, multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| Test infrastructure: `tests/strict/*.flow` positive + xUnit `StrictModeNegativeTests.cs` | Two-track testing; xUnit pins ~126 error strings verbatim. Use Phase 43 qualified imports for shared fixtures. | ✓ |
| Strict applies INSIDE `live { }` blocks (file-load + re-eval both) | Composer gets type safety AT EDIT TIME via live-reload. `[live]` advisory itself stays charitable per D-06 carve-out. | ✓ |
| REPL sticky session flag + `:strict on/off` meta-command | Mirrors Phase 38 REPL polish family. `:strict on` / `:strict off` toggle. | ✓ |
| Dict lookup baseline + PragmaRegistry entry (no-discussion lock-ins) | Dict baseline: Phase 26.1 already hashes by type+value, no change. PragmaRegistry: single-line addition. | ✓ |

**User's choice:** All four selected.
**Notes:** Captured as D-13, D-14, D-15, D-16.

---

## Claude's Discretion

- Implementation-internal ordering of OverloadResolver tier-disable logic (Axis A) — `StrictTierFilter` predicate vs inline branch.
- Internal naming of `ctx.CallerStrictMode` field — synonym like `StrictModeAtCallSite` acceptable.
- Whether to vendor `flow-lang.Tests/baselines/Phase44/` directory for audio-affecting strict tests.
- Plan-phase task ordering of HIGH/MED/LOW Axis B promotion within the single phase.
- Whether `(neg x)`/`(idiv x y)`/`(concat x y)` need strict-mode tightening beyond OverloadResolver +100 disable.

## Deferred Ideas

- Future sub-axis pragmas (`strictPurity`, `strictLengths`, etc.) — D-01 leaves room; not Phase 44.
- Strict mode for module-level export contracts (e.g., `module mymod strict;`) — v1.6+.
- Cosmetic explicit-overload backfill for the 70+ §5b candidates — v1.6-backlog.
- `Int → NoteValue` explicit conversion — v1.6-backlog.
- `readMidi(String)` / `readMusicXML(String)` registry builtins — v1.6-backlog (AUDIT §7c).
- Promote `scripts/StdlibAuditor` to CI health check — v1.6-backlog (AUDIT §7c).
- `FunctionSignature.ReturnType` field addition — v1.6-backlog (AUDIT §8 Limitation 1).
- Strict-mode propagation rules for `--watch` reload incidents (rename `enable strict;` mid-session) — fall back to existing reload error reporting.
