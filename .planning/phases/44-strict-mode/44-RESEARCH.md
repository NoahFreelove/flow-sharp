# Phase 44: Strict Mode — Research

**Researched:** 2026-05-24
**Domain:** Flow language — opt-in `enable strict;` file pragma (Axes A/B/C); v1.5 closeout consumer of Phase 42 AUDIT.md
**Confidence:** HIGH

## Summary

Phase 44 ships `enable strict;` — one file-scope pragma flipping three axes of strictness at the input perimeter:

- **Axis A** disables OverloadResolver's "+100 convertible" tier (numeric widening + inverse-direction music-type widening) so `(gain buf -12.0)` errors and `(gain buf -12dB)` is required. Six new explicit-conversion builtins (`db`/`hz`/`ms`/`sec`/`cents`/`semitones`) plus four reverse-direction overloads (`double`/`float`/`int`/`long` on tagged music types) make refactoring composer-friendly.
- **Axis B** converts the 13 input-perimeter `Math.Clamp` sites (AUDIT §6a) and the ~113 in-scope `WarnOnce` advisory sites (AUDIT §6b minus 5 carve-outs) into `[strict] <tag> ...` errors when the CALLER's file is strict. Stdlib stays charitable by default; the strict bit is read per-caller, not per-callee.
- **Axis C** tightens `(and)`/`(or)`/`(not)` + `if` to require Bool, `(print)` to require String, and the cross-type comparisons `(gt)`/`(lt)`/`(gte)`/`(lte)` to error. Equality returns `false` (set-theoretic, not error). The non-strict path simultaneously receives a charitable-auto-`(str)` for `print` and a charitable truthy-coerce for `if` — the "pre-strict bug fix" bundled in this phase.

The work is mechanically scoped: one PragmaRegistry entry, two new `ExecutionContext` fields, one push/pop hook in proc invocation, one predicate change in `OverloadResolver`, ~126 site rewrites from `WarnOnce(...)` → `if (ctx.CallerStrictMode) ErrorReporter.Report(...); else WarnOnce(...)`, ~17 new C# builtin registrations, and three new test surfaces (`tests/strict/*.flow` positives + `flow-lang.Tests/Phase44/StrictModeNegativeTests.cs` xUnit Facts + showcase). Two-run cmp-clean determinism is preserved (no PRNG sites added; only input-domain checks flip mode).

**Primary recommendation:** Plan a Wave 1 (registry + plumbing — D-04/D-02/D-05 + Axis A + explicit-conv builtins + pre-strict bug fix), Wave 2 (Axis B HIGH-priority module sites: SFZ + patterns + render + match + DSP — 79 sites), Wave 3 (Axis B MEDIUM/LOW + Axis C + REPL/live + showcase + xUnit pins). Strict positive `.flow` tests author alongside Wave 1 to catch regressions early.

## User Constraints (from 44-CONTEXT.md)

### Locked Decisions

**Pragma Surface (Area 1):**
- **D-01:** Pragma name is `enable strict;` — single monolithic knob covering Axes A + B + C. NOT `enable strictTypes;` and NOT a three-pragma split. Composer mental model: "strict mode on, strict mode off". Future sub-axes (`strictPurity`, etc.) can ship as separate pragmas.
- **D-02:** `ExecutionContext.StrictMode` boolean field set when loading the strict file. ModuleLoader push/pops `StrictMode` per the imported file's own pragma. No `AsyncLocal`; no `StackFrame.StrictMode`; no AST `Program.IsStrict` attribute.
- **D-03:** File-scope semantics — each file's pragma governs only statements DECLARED in that file. Cross-file calls: ModuleLoader binds each proc to its declaring file's strict bit; Interpreter sets `ctx.StrictMode` from that bit on proc entry. Stdlib procs (declared in `flow-lang/*.flow` or C#-registered) always run with `ctx.StrictMode = false`.
- **D-04:** `PragmaRegistry.KnownPragmas[\"strict\"] = \"Opt-in strict mode: ...\"` single-line addition.

**Axis B Mechanism + Scope (Area 2):**
- **D-05:** TWO distinct fields on `ExecutionContext`:
  - `ctx.StrictMode` — per-declaring-file (D-02/D-03), governs Axis A.
  - `ctx.CallerStrictMode` — snapshot at call dispatch of the CALLER's `StrictMode`. Stdlib clamp + advisory sites read THIS field.
- **D-06:** Axis B scope: ALL 13 §6a clamps + ~113 of 117 §6b advisories. Carve-outs (stay charitable): `[live]` block-entry (D-v1.5-07 design-lock) + 4 `[improv]` style-pack discovery advisories. Net ~126 errored-in-strict sites.
- **D-07:** Error format `[strict] <existing-tag> <issue>`. Keep WarnOnce sentinel body verbatim; prepend `[strict] ` prefix; route through `ErrorReporter` when `ctx.CallerStrictMode == true`.

**Explicit-Conversion Builtins (Area 3):**
- **D-08:** Forward direction:
  - `(db x)`, `(hz x)`, `(ms x)`, `(sec x)`, `(cents x)`: accept Int + Long + Float + Double + idempotent on target tagged type.
  - `(semitones x)`: Int ONLY (whole-numbers-by-design per `CentType.cs:24-27` pattern).
- **D-09:** All 6 forward builtins available in BOTH modes.
- **D-10:** Reverse direction: `(double x)`/`(float x)`/`(int x)`/`(long x)` backfill overloads for all 6 tagged music types (Decibel/Hertz/Cent/Millisecond/Second/Semitone). Always-available; lossy-floor for `(int 100ms)` → 100.

**Equality + Truthy / Stringy (Area 4):**
- **D-11:** `(equals a b)` cross-type → `false` (set-theoretic, same as non-strict). `(gt)`/`(lt)`/`(gte)`/`(lte)` cross-type → error `[strict] cross-type comparison <T1> vs <T2> — use explicit (double x) / (int x)`.
- **D-12:** Strict: `(and)`/`(or)`/`(not)` + `if` Bool-only. Non-strict (post pre-strict bug fix): `(and)`/`(or)` charitable-truthy + return last-truthy; `(not)` charitable; `if Int x` truthy-coerces; `print Int x` auto-strs.
- **D-13:** Dict lookup stays type-strict by design — Phase 26.1 hashes Dict keys by type+value. xUnit pin in strict to lock.

**Test Infrastructure (Area 4.3):**
- **D-14:** Positive `tests/strict/test_*.flow` files (each begins `enable strict;`); Negative `flow-lang.Tests/Phase44/StrictModeNegativeTests.cs` xUnit Facts pin ~126 error strings verbatim. Use Phase 43 qualified imports for shared fixtures.

**Live + REPL (Area 4.3):**
- **D-15:** Strict applies INSIDE `live { }` when the enclosing file declares `enable strict;`. Initial parse + live-reload re-eval both run strict. `[live]` entering-block advisory stays charitable (D-06 carve-out).
- **D-16:** REPL strict is a sticky session flag. `enable strict;` at the prompt flips ON; `:strict on` / `:strict off` meta-commands explicit toggle. Per-line input inherits the flag.

### Claude's Discretion
- Implementation-internal ordering of OverloadResolver tier-disable (single predicate vs branch in scoring loop).
- Internal naming of `ctx.CallerStrictMode` field (`StrictModeAtCallSite` acceptable).
- Whether to vendor `flow-lang.Tests/baselines/Phase44/` for any audio-affecting strict-positive tests.
- Plan-phase task ordering of HIGH vs MED vs LOW Axis B promotion (D-06 ships all in-scope in one phase regardless).
- Whether `(neg)`/`(idiv)`/`(concat)` need any strict tightening beyond OverloadResolver tier disable. Default: no.

### Deferred Ideas (OUT OF SCOPE)
- Future sub-axis pragmas (`strictPurity`, `strictLengths`, etc.).
- Module-level `module mymod strict;` export contracts.
- Cosmetic explicit-overload backfill for the 70+ AUDIT §5b candidates.
- `Int → NoteValue` explicit conversion.
- `readMidi(String) → Song` / `readMusicXML(String) → Song` registry builtins.
- Promote `scripts/StdlibAuditor` to CI health check.
- `FunctionSignature.ReturnType` field.
- Strict-mode propagation rules for `--watch` mid-session pragma edits.

## Phase Requirements

Per ROADMAP line 408 — "Requirements: TBD (defined at plan-phase)". The planner must author REQ-STRICT-NN entries against `REQUIREMENTS.md` covering at minimum:

| Suggested REQ-ID | Behavior | Research Support |
|---|---|---|
| REQ-STRICT-01 | `enable strict;` pragma recognized; unknown-pragma path covers typo recovery | §Pattern 1 (PragmaRegistry single-line registration) |
| REQ-STRICT-02 | `ctx.StrictMode` set at file load; restored on cross-file proc entry | §Pattern 2 (ProcDeclaration push/pop); §Integration Point 2 |
| REQ-STRICT-03 | `ctx.CallerStrictMode` snapshotted at call dispatch | §Pattern 3 (Call-boundary snapshot at `EvaluateFunctionCall`) |
| REQ-STRICT-04 | Axis A: OverloadResolver disables "convertible" tier in strict | §Pattern 4 (predicate gate in Matches()); §Pitfall 1 |
| REQ-STRICT-05 | 6 forward-direction explicit-conv builtins registered (db/hz/ms/sec/cents/semitones) | §Code Examples §Explicit-Conv Builtin |
| REQ-STRICT-06 | 4 reverse-direction extractor overloads on tagged music types | §Pattern 5 |
| REQ-STRICT-07 | All 13 §6a clamps emit `[strict]` error when CallerStrictMode | §Code Examples §Axis B Site Rewrite; §Site Inventory |
| REQ-STRICT-08 | All in-scope §6b advisories (~113) emit `[strict]` error when CallerStrictMode | §Site Inventory; §Pitfall 2 carve-outs |
| REQ-STRICT-09 | Axis C: `(and)`/`(or)`/`(not)` + `if` Bool-only in strict; charitable in non-strict | §Pattern 6 (per-builtin strict-mode branch); §Pre-strict bug fix |
| REQ-STRICT-10 | `(print)` auto-strs non-strict; requires String in strict | §Code Examples §Print Pre-Strict |
| REQ-STRICT-11 | Cross-type `(gt)`/`(lt)`/`(gte)`/`(lte)` error in strict; `(equals)` returns false | §Code Examples §Comparison Strict Branch |
| REQ-STRICT-12 | Strict pragma applies inside `live { }` block; reload re-applies | §Pattern 7 (LiveReloadManager re-eval) |
| REQ-STRICT-13 | REPL `:strict on`/`:strict off` meta-commands toggle sticky flag | §Pattern 8 (Repl.HandleCommand) |
| REQ-STRICT-14 | Positive `.flow` strict tests + negative xUnit pins | §Validation Architecture |
| REQ-STRICT-15 | Two-run cmp-clean determinism preserved (no new PRNG sites) | §Pitfall 5 |

## Project Constraints (from CLAUDE.md)

These directives bind the planner; research recommendations honor each one:

- **Genre-agnostic, music-only scope.** Strict mode is a composer-facing reliability knob; it does NOT introduce general-purpose-language features.
- **Ergonomics-first; charitable interpretation is the DEFAULT** (`feedback_charitable_interpretation`, `feedback_ergonomics_priority`). Strict opts INTO reversing this default for ONE file at a time. Carve-outs preserve `[live]` + `[improv]` style-pack charity per D-06.
- **Pre-traction no-deprecation latitude (D-v1.5-01).** Phase 44 ships pragma + builtins + error wording in one commit; no migrators needed.
- **Two-run cmp-clean determinism contract.** Strict mode introduces NO new PRNG sites. The conversion `WarnOnce(...) → ErrorReporter.Report(...)` is mechanical text manipulation; downstream audio output is unaffected (strict errors abort before render — non-strict path is byte-identical to today).
- **Charitable preserved for chaos primitives' cross-platform divergence (D-36-09).** Strict mode does not promise cross-platform FP determinism for `lorenz`/`logistic`. Strict ERRORS on degenerate chaos params are deterministic same-platform; cross-platform divergence stays unchanged.
- **Sample-by-sample buffer ops; no GC pressure in hot paths.** Strict-mode checks fire ONCE at builtin entry (input perimeter), not in inner loops. No hot-path impact.
- **External deps unchanged** — only Pidgin reference + DryWetMidi 8.0.3. Phase 44 ships pure C# + Flow source.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Pragma recognition | Lexing (`PragmaRegistry` + `PragmaScanner`) | — | Existing closed-set pattern; one-line addition |
| File-scope strict-bit storage | Runtime (`ExecutionContext.StrictMode`) | AST (per-`ProcDeclaration` `IsStrict` bool, optional fast path) | Consumer-adjacent placement per D-02; AST flag avoids per-call fileName→PragmaSet lookup |
| Call-site strict-bit snapshot | Interpreter (`EvaluateFunctionCall` save/restore around dispatch) | Runtime (`ExecutionContext.CallerStrictMode`) | One dispatch site covers both unqualified + qualified call surfaces (Phase 43) |
| Axis A tier filtering | TypeSystem (`FunctionSignature.Matches` + `OverloadResolver`) | Runtime (reads `ctx.StrictMode`) | Disable inverse-direction widening + `CanConvertTo` clauses in `Matches` when strict |
| Axis B error routing | Stdlib (per-site `if (ctx.CallerStrictMode) ErrorReporter.Report; else WarnOnce`) | Diagnostics (`ErrorReporter`) | Per-site mechanical rewrite; existing infrastructure |
| Axis C truthy/stringy/equality | Stdlib (`StdLib.cs` per-builtin) + Interpreter (`if` dispatch) | TypeSystem (`Utils.LooseEquals` / `CompareNumeric`) | Mode-aware branching per builtin |
| Explicit-conv builtins | Stdlib (`BuiltInFunctions.cs` + new file `ConversionFunctions.cs`) | TypeSystem (uses existing `*Type.Instance` constructors) | Always-available registration; mirror `(float x)`/`(int x)` pattern |
| REPL meta-command | flow-interpreter (`Repl.HandleCommand`) | Runtime (per-session `ctx.StrictMode`) | Sticky session flag; mirrors `:help`/`:quit`/`:stop` family |
| Live block strict re-eval | flow-interpreter (`LiveReloadManager.RenderScript`) | FlowEngine (`Execute` re-runs PragmaScanner.Scan) | Automatic — full engine re-execute hits the pragma path |
| Test fixtures | tests/strict/*.flow (positive) + flow-lang.Tests/Phase44/ (negative xUnit) | flow-lang.Tests/Helpers | Mirrors Phase 35/36 layout; Phase 43 qualified imports for shared fixtures |

## Standard Stack

### Core (already in repo — Phase 44 reuses)

| Surface | Site | Purpose | Why It's the Right Tool |
|---------|------|---------|--------------------------|
| `PragmaRegistry.KnownPragmas` | `flow-lang/Lexing/PragmaRegistry.cs:27` | Closed-set registry; one-line strict entry | Existing pattern (Phase 21/23/35); levenshtein typo recovery free |
| `PragmaScanner.Scan` | `flow-lang/Lexing/PragmaScanner.cs:84` | Pre-lex extraction; D-12 unknown-pragma error path | Already handles comments, blank lines, whitespace, position preservation (Pitfall G CRLF) |
| `PragmaSet.Has(name)` | `flow-lang/Lexing/PragmaSet.cs:27` | O(1) pragma lookup | HashSet-backed; thread-safe read |
| `ExecutionContext.ProgramPragmaSet` | `flow-lang/Runtime/ExecutionContext.cs:368` | Per-engine pragma fallback set | Already declared but NEVER set today; Phase 44 may opt to leave it unset and use the new dedicated fields instead |
| `OverloadResolver.Resolve` | `flow-lang/TypeSystem/OverloadResolver.cs:49` | Specificity-scored dispatch | Axis A wires here |
| `FunctionSignature.Matches` + `CalculateSpecificity` | `flow-lang/TypeSystem/FunctionSignature.cs:78` + `:140` | Match predicate + specificity scoring | Three score tiers exist: +1000 exact, +500 IsCompatibleWith, +100 CanConvertTo |
| `RenderingDiagnostics.WarnOnce(key, message)` | (singleton in `flow-lang/Diagnostics/`) | One-shot stderr advisory | Existing per-site pattern at all 117 §6b sites; flips to ErrorReporter under strict |
| `ErrorReporter.Report` / `.ReportError` | `flow-lang/Diagnostics/` | Error accumulation (CLAUDE.md model) | Strict errors aggregate cleanly; existing |
| `ModuleLoader.LoadModule` | `flow-lang/Runtime/ModuleLoader.cs:49` | Per-import file load + PragmaScanner.Scan | Each module gets its OWN PragmaSet (line 83-85) — Phase 44 reads `pragmaSet.Has("strict")` here to set the imported file's strict bit |
| `Repl.HandleCommand` switch arm | `flow-interpreter/Repl.cs:216-223` | Meta-command dispatcher | `:strict on`/`:strict off` arms added |
| `LiveReloadManager.RenderScript` | `flow-interpreter/LiveReloadManager.cs:836` | Fresh `FlowEngine().Execute(source, filePath)` per reload | Strict re-applies automatically through `PragmaScanner.Scan` |

### Supporting (existing, lightly extended)

| Surface | Site | Purpose | Phase 44 Use |
|---------|------|---------|--------------|
| `ProcDeclaration` record | `flow-lang/Ast/Statements/ProcDeclaration.cs:9` | User-proc AST node | Add `bool IsStrict = false` field; threaded from `_pragmaSet.Has("strict")` at construction site (`Parser.cs:384`) |
| `Parser._pragmaSet` | `flow-lang/Parsing/Parser.cs` | Per-parse-session PragmaSet | Already captured for MatchExpression (Plan 35-06); reuse for `ProcDeclaration.IsStrict` |
| `ExpressionEvaluator.EvaluateFunctionCall` | `flow-lang/Interpreter/ExpressionEvaluator.cs:222` | Single call dispatch site | Snapshot `ctx.CallerStrictMode = ctx.StrictMode` before invoke; restore after; mirrors the existing `prevCallSite` save/restore at lines 399-409 |
| `Interpreter.ExecuteUserFunctionWithCaptures` | `flow-lang/Interpreter/Interpreter.cs:1105` | User-proc entry | Push/pop `ctx.StrictMode = proc.IsStrict` mirroring the `PushFrame`/`PopFrame` try/finally |
| `BuiltInFunctions.RegisterStdLib` | `flow-lang/StandardLibrary/BuiltInFunctions.cs:151` | Where `print`/`if`/`and`/`or` are registered | Pre-strict bug fix: ADD wildcard `print(Void)` + truthy-coerce `if(Void, Lazy<Void>, Lazy<Void>)` overloads; existing `print(String)` + `if(Bool,...)` stay |

### Alternatives Considered

| Instead of | Could Use | Why Rejected |
|------------|-----------|--------------|
| `ExecutionContext.StrictMode` field | `AsyncLocal<bool>` | Flow interpreter is single-threaded; AsyncLocal is overkill (CONTEXT D-02 explicit rejection) |
| `ExecutionContext.StrictMode` field | `StackFrame.StrictMode` per-frame | Strict is file-scoped, not proc-scoped; per-frame is wrong granularity (CONTEXT D-02 explicit rejection) |
| `ProcDeclaration.IsStrict` bool flag | `Map<fileName, PragmaSet>` on ExecutionContext + lookup by `proc.Location.FileName` at proc-entry | The AST flag is cheaper (zero hash lookups per call) and matches the MatchExpression.CapturedPragmas precedent — both file-scoped pragma effects captured at parse |
| Disable +100 tier via score branch | Filter `Matches()` to refuse convertible-only candidates | Filtering at `Matches()` is simpler — convertible matches never enter the ranked pool. Disabling at `CalculateSpecificity` keeps them ranked as score-0, complicating "multiple convertible matches with no winner" diagnostics |
| Modify `Utils.LooseEquals` for cross-type equality | Wrap call site in `EqualsStrict` | LooseEquals returns false for cross-type-non-numeric ALREADY (line 82-83). The `1 == 1.0` numeric coercion at line 73-76 is the ONLY behavior to flip in strict — feasible via a new code path keyed on `ctx.CallerStrictMode` |

**Installation:** no new packages — Phase 44 is 100% in-repo C# + Flow.

**Version verification:** all referenced files exist at cited line numbers as of this research's HEAD (`e898512 docs(phase-43): complete phase execution`). The git status shows phase 43 just shipped; phase 44 work begins clean.

## Architecture Patterns

### System Architecture Diagram

```
.flow source file
   │
   ▼
[PragmaScanner.Scan] ────► PragmaSet (has "strict"?)
   │                            │
   │     ┌──────────────────────┘
   ▼     ▼
[SimpleLexer] ───► [Parser] ───► AST  (ProcDeclaration.IsStrict = pragmaSet.Has("strict"))
                       │
                       ▼
            FlowEngine.Execute / ModuleLoader.LoadModule
                       │
                       ▼
            ExecutionContext.StrictMode = pragmaSet.Has("strict")  ◄── per file load
                       │
                       ▼
            Interpreter.Execute(program)
                       │
                       │ proc invocation
                       ▼
            ExecuteUserFunctionWithCaptures(proc, args)
                  ▼ try { ctx.StrictMode = proc.IsStrict ... } finally { restore }
                       │
                       ▼  expression eval reaches a function call
            ExpressionEvaluator.EvaluateFunctionCall
                  ▼ try { ctx.CallerStrictMode = ctx.StrictMode ... } finally { restore }
                       │
                       ├─► OverloadResolver.Resolve  ──► reads ctx.StrictMode for Axis A tier gate
                       │
                       ▼
            registered builtin lambda (e.g., Crescendo)
                  ▼ if (ctx.CallerStrictMode) ErrorReporter.Report("[strict] crescendo startVel ...")
                  ▼ else { Math.Clamp(...); WarnOnce(...) }   ◄── Axis B per-site branch
                       │
                       ▼
                  charitable result OR strict error accumulated
```

### Pattern 1: PragmaRegistry single-line registration (D-04)

**What:** Add `"strict"` entry to `PragmaRegistry.KnownPragmas`.
**When to use:** New file-scope pragma adoption (Phase 21 precedent).
**Example:**
```csharp
// flow-lang/Lexing/PragmaRegistry.cs:27
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "...",
        ["justIntonation"] = "...",
        ["pythagorean"] = "...",
        ["equalTemperament"] = "...",
        ["scaleLint"] = "...",
        ["matchExhaustive"] = "...",
        // Phase 44 D-04 — single-line addition
        ["strict"] = "Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports.",
    };
```

Levenshtein typo recovery (`SuggestNearest`) handles `enable stric;` / `enable Strict;` free. Capitalization mismatch: `enable Strict;` will levenshtein to `strict` (distance 1 within the `max(2, len/3)=2` threshold) and fire the existing "Did you mean 'strict'?" error.

### Pattern 2: Per-proc strict-bit capture at parse time

**What:** Extend `ProcDeclaration` record with `bool IsStrict = false` and set from `_pragmaSet.Has("strict")` at the parser's construction site.
**When to use:** AST flag for fast lookup at proc-entry; mirrors `MatchExpression.CapturedPragmas` precedent.
**Example:**
```csharp
// flow-lang/Ast/Statements/ProcDeclaration.cs:9 — extended
public record ProcDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body,
    bool IsInternal,
    Span? Span = null,
    bool IsStrict = false        // Phase 44 D-02/D-03 — captured at parse time
) : Statement(Location);

// flow-lang/Parsing/Parser.cs:384 — threaded
return new ProcDeclaration(
    location, name, parameters, body, isInternal,
    Span: new Span(location, PreviousToken.Location),
    IsStrict: _pragmaSet?.Has("strict") ?? false);    // Phase 44 D-02/D-03
```

### Pattern 3: Call-boundary snapshot of CallerStrictMode (D-05)

**What:** At `EvaluateFunctionCall`, save/restore `ctx.CallerStrictMode = ctx.StrictMode` around the builtin/user-proc invoke.
**When to use:** This is THE call-dispatch boundary that resolves the "stdlib stays charitable when called from non-strict" contract.
**Example:**
```csharp
// flow-lang/Interpreter/ExpressionEvaluator.cs:399-409 — adjacent to prevCallSite save/restore
var prevCallSite = _context.CurrentCallSite;
var prevCallerStrict = _context.CallerStrictMode;        // Phase 44 D-05
_context.CurrentCallSite = call.Location;
_context.CallerStrictMode = _context.StrictMode;         // Phase 44 D-05
try
{
    return overload.Implementation!(argValues);
}
finally
{
    _context.CurrentCallSite = prevCallSite;
    _context.CallerStrictMode = prevCallerStrict;        // Phase 44 D-05
}
```

Apply the SAME save/restore on the qualified-call branch at lines 240-253 (Phase 43 `(mod.fn args)` dispatch), and on `_invoker.ExecuteUserFunctionWithCaptures` user-proc invocation paths (lines 254-255, 413-415).

### Pattern 4: OverloadResolver Axis A tier filter (D-01)

**What:** When `ctx.StrictMode == true`, disable the "+100 convertible" tier inside `FunctionSignature.Matches()`.
**Mechanism:** the existing `Matches()` (lines 78-135) accepts a candidate when ANY of three clauses holds: arg.IsCompatibleWith(param), arg.CanConvertTo(param), OR param.IsCompatibleWith(arg) (the inverse-direction widening). In strict, drop clauses 2 + 3 and accept ONLY arg.IsCompatibleWith(param) OR arg.Equals(param). Equivalent: require `CalculateSpecificity(argTypes) >= 500`.
**Why both clauses 2 + 3:** Numeric widening (Int → Long → Float → Double) lives in clause 2 (`Int.CanConvertTo(Double)`); music-type widening (Decibel → Double for `(gain buf -12.0)` case) lives in clause 3 (`Decibel.IsCompatibleWith(Double)` is true; resolver tries `param.IsCompatibleWith(arg)` because the arg is `Double` and param `Decibel`). Both are implicit conversions and both belong to strict's "no coercion" rule.
**Plumbing:** `Matches()` is currently a pure FunctionSignature method; pass `ExecutionContext` or a `bool strict` flag through `OverloadResolver.Resolve` → `Matches` (Resolve already has access to `_errorReporter`; threading the strict bit is straightforward). Alternative: read `ctx.StrictMode` via a thread-local accessor. Recommend the explicit-parameter route per Pitfall 4.
**Example:**
```csharp
// flow-lang/TypeSystem/FunctionSignature.cs — extended signature
public bool Matches(IReadOnlyList<FlowType> argTypes, bool strictMode = false)
{
    // ... existing IsVarArgs path with same strict filter applied to fixed slots ...
    for (int i = 0; i < InputTypes.Count; i++)
    {
        bool exactOrCompat = argTypes[i].Equals(InputTypes[i])
                          || argTypes[i].IsCompatibleWith(InputTypes[i]);
        if (strictMode)
        {
            if (!exactOrCompat) return false;
        }
        else
        {
            if (!exactOrCompat
                && !argTypes[i].CanConvertTo(InputTypes[i])
                && !InputTypes[i].IsCompatibleWith(argTypes[i]))
                return false;
        }
    }
    return true;
}
```

Default-false keeps EVERY existing call site byte-identical for non-strict files. `OverloadResolver.Resolve` passes the bit forward; `ExecutionContext.ResolveFunction` reads `ctx.StrictMode` at its entry.

### Pattern 5: Explicit-conversion builtins (D-08/D-09/D-10)

**What:** Six forward builtins (`db`/`hz`/`ms`/`sec`/`cents`/`semitones`) + four reverse extractor overloads.
**Forward builtin registration template** (mirrors Phase 32 `loadScala`):
```csharp
// new file: flow-lang/StandardLibrary/ConversionFunctions.cs
private static void RegisterDecibelConversions(InternalFunctionRegistry registry)
{
    foreach (var (sourceType, materializer) in new (FlowType, Func<Value, double>)[]
    {
        (IntType.Instance,    v => (double)v.As<int>()),
        (LongType.Instance,   v => (double)v.As<long>()),
        (FloatType.Instance,  v => v.As<double>()),     // Flow Float is CLR double
        (DoubleType.Instance, v => v.As<double>()),
        (DecibelType.Instance, v => v.As<double>()),    // D-08 idempotent
    })
    {
        var sig = new FunctionSignature("db", [sourceType], ParameterNames: ["x"]);
        registry.Register("db", sig, args => Value.Decibel(materializer(args[0])));
    }
}
```
Plus equivalent registrations for `hz`/`ms`/`sec`/`cents`. The `semitones` builtin gets ONLY the `Int` overload (D-08 whole-numbers-by-design); calls with Float/Double/Long error with `[strict] (semitones) requires Int — got <Type> <value>`.

**Reverse direction backfill** (D-10): in `BuiltInFunctions.cs` near the existing `(int x)` / `(double x)` site, add new overloads:
```csharp
// (double x) accepting all 6 tagged music types
foreach (var taggedType in new FlowType[] {
    DecibelType.Instance, HertzType.Instance, CentType.Instance,
    MillisecondType.Instance, SecondType.Instance, SemitoneType.Instance })
{
    var sig = new FunctionSignature("double", [taggedType], ParameterNames: ["value"]);
    registry.Register("double", sig, args => Value.Double(args[0].As<double>()));
    // semitone is Int-backed — args[0].As<int>() then cast
}
```
For `(int Decibel)` etc. use floor semantics matching the existing `doubleToInt` path.

### Pattern 6: Per-builtin strict-mode branch for Axis C (D-11/D-12)

**What:** Inside `(and)`/`(or)`/`(not)`/`if`/`(equals)`/`(gt/lt/gte/lte)`/`(print)` implementations, check `ctx.CallerStrictMode` and branch.
**Plumbing requirement:** these implementations currently sit in `StdLib.cs` as `static Value <Name>(IReadOnlyList<Value> args)`. Strict-mode awareness requires `ExecutionContext` access. Three options:
- (a) Change the lambda signature globally to include ctx (large diff across hundreds of sites).
- (b) Re-register via the `RegisterContextDependentFunctions` path (existing precedent for `map`/`filter`/`reduce`/`each` per `BuiltInFunctions.cs` comment at line 117) — captures ctx in a closure.
- (c) Read `ctx.CallerStrictMode` via a thread-local accessor.

**Recommend (b)** — minimal blast radius, matches the existing context-dependent pattern. Move `Print`/`If`/`And`/`Or`/`Equals`/`LessThan`/etc. to the context-dependent registration path, OR introduce a thin shim that reads the active `ExecutionContext` from a static accessor wired at FlowEngine init.

**Example (Axis C `(print)` strict + non-strict pre-strict bug fix):**
```csharp
// In RegisterContextDependentFunctions(registry, context):
var printStringSig = new FunctionSignature("print", [StringType.Instance], ParameterNames: ["s"]);
registry.Register("print", printStringSig, args =>
{
    Console.WriteLine(args[0].As<string>());
    return Value.Void();
});

// D-12 pre-strict bug fix — wildcard Void overload accepts ANY type,
// auto-strs in non-strict, errors in strict.
var printAnySig = new FunctionSignature("print", [VoidType.Instance], ParameterNames: ["s"]);
registry.Register("print", printAnySig, args =>
{
    if (context.CallerStrictMode)
    {
        errorReporter.ReportError(
            $"[strict] (print) requires String — got {args[0].Type}",
            context.CurrentCallSite);
        return Value.Void();
    }
    // Non-strict charitable — auto-str the value
    Console.WriteLine(StdLib.AutoStr(args[0]));
    return Value.Void();
});
```

The Void-wildcard overload is lower-specificity than the explicit `String` overload, so existing `(print "hello")` calls still hit the String path byte-identical. Only `(print 42)` (currently failing) and `(print "x" 5)` (rare) reach the wildcard.

**Example (`(and)` Lisp-style last-truthy in non-strict — already implemented):**
The existing `StdLib.And` (line 452-473) returns `Value.Bool(false)` short-circuit OR `Value.Bool(rres)` — but BOTH paths return Bool. To preserve "Lisp-style last-truthy" per D-12 wording, the non-strict path should return the actual underlying truthy Value, not a Bool. Check existing `.flow` tests to determine whether this is a behavior change vs. cosmetic; if it's a behavior change, the pre-strict bug fix per CONTEXT may instead be "keep Bool return, but accept Void wildcard inputs charitably."

**Note**: `(not x)` is NOT currently registered (verified via grep + `flow-lang/test.flow:39` comment "the interpreter does not register a built-in `not`"). Phase 44 must register both strict (Bool-only) and non-strict (Void-wildcard charitable) overloads for `(not)`.

### Pattern 7: Live block re-eval through full engine.Execute

**What:** `LiveReloadManager.RenderScript` constructs a fresh `FlowEngine`, calls `engine.Execute(source, filePath)`, which runs `PragmaScanner.Scan` → sets strict bit. Strict mode applies INSIDE `live { }` automatically because re-eval is a full re-parse.
**Why it works:** `live { body }` re-eval is NOT a partial AST patch; it's a full source re-execute (per `LiveReloadManager.cs:836-883`). The pragma is captured fresh each reload.
**Composer-visible effect:** Edit a `live` block, save file. On next quantize boundary, the new body runs with strict checks. If composer introduces a strict violation, ErrorReporter captures it and the existing `live` reload error reporting kicks in (CONTEXT D-15).

### Pattern 8: REPL sticky strict flag (D-16)

**What:** Two routes flip the per-session strict bit:
1. Typing `enable strict;` at the REPL — PragmaScanner picks it up; the existing `engine.Execute` per-line evaluation pulls it through PragmaScanner.Scan → sets `ctx.StrictMode`.
2. Typing `:strict on` / `:strict off` meta-command — `Repl.HandleCommand` switch arm directly sets `engine.Context.StrictMode = true/false` and prints confirmation.
**Subtle requirement:** since per-line evaluation rebuilds the PragmaSet from the JUST-TYPED line, `enable strict;` typed at line N will set strict for line N's execution but NOT for line N+1's execution unless we persist. The session-flag approach: REPL maintains its own `_sessionStrict` bool; before each `engine.Execute(line, fileName)` call, mutate `engine.Context.StrictMode = _sessionStrict` (UNLESS the parsed line ITSELF carries `enable strict;`, in which case observed strict bit also updates session flag).
**Example:**
```csharp
// In Repl.HandleCommand (around line 216-223):
return command.ToLower() switch
{
    ":quit" or ":q" or ":exit" => false,
    ":help" or ":h" => ShowHelp(),
    ":clear" or ":cls" => ClearScreen(),
    ":stop" => StopAudio(),
    ":strict on" => SetSessionStrict(true),       // Phase 44 D-16
    ":strict off" => SetSessionStrict(false),      // Phase 44 D-16
    _ => UnknownCommand(command)
};
```

### Recommended Project Structure (Phase 44 additions)

```
flow-lang/
├── StandardLibrary/
│   └── ConversionFunctions.cs        # NEW — D-08/D-09/D-10 builtins
├── Runtime/
│   └── ExecutionContext.cs           # MODIFIED — +StrictMode, +CallerStrictMode
├── Ast/Statements/
│   └── ProcDeclaration.cs            # MODIFIED — +bool IsStrict = false
├── Parsing/
│   └── Parser.cs                     # MODIFIED — thread _pragmaSet to new ProcDeclaration ctor
├── TypeSystem/
│   ├── FunctionSignature.cs          # MODIFIED — Matches(argTypes, bool strictMode = false)
│   └── OverloadResolver.cs           # MODIFIED — forward strict bit through Resolve
├── Interpreter/
│   ├── ExpressionEvaluator.cs        # MODIFIED — CallerStrictMode snapshot + qualified-call branch
│   └── Interpreter.cs                # MODIFIED — push/pop StrictMode in ExecuteUserFunctionWithCaptures
├── Lexing/
│   └── PragmaRegistry.cs             # MODIFIED — single-line "strict" entry
├── StandardLibrary/                  # MODIFIED — ~126 sites for WarnOnce → strict-aware branch
│   ├── Transforms/TransformFunctions.cs   # 13 §6a clamp sites (lines 106-107, 649-650, 657-658, 666-667, 785, 821, 904, 960, 1106)
│   ├── Audio/Sfz/{SfzBuiltins,SfzParser,SfzRenderer,SfzSampleCache}.cs   # 22 advisories
│   ├── Patterns/PatternFunctions.cs        # 17 advisories
│   ├── Improv/JamFunctions.cs              # 16 advisories
│   ├── Generative/{ChaosFunctions,MarkovFunctions,LsystemFunctions,CellularFunctions}.cs   # 32 advisories combined
│   ├── Notation/{AbcImport,AbcLexer,MmlImport}.cs   # 15 advisories combined
│   ├── Network/OscFunctions.cs             # 3 advisories
│   ├── Audio/DSP/{GranularFunctions,PitchShiftFunctions,StretchEngine,StretchFunctions}.cs   # 5 advisories
│   ├── Audio/{SampledInstrumentRenderer,InputFunctions,SongRenderer,MidiExport}.cs   # 9 advisories
│   ├── Audio/Tuning/ScalaBuiltins.cs        # 2 advisories
│   └── Harmony/HarmonyFunctions.cs          # 1 advisory
└── Ast/Expressions/MatchExpression.cs       # 1 advisory (existing matchExhaustive pragma path)

flow-interpreter/
├── Repl.cs                            # MODIFIED — :strict on/off meta-commands
└── LiveReloadManager.cs               # NO CHANGE — full engine.Execute path covers strict re-apply

tests/strict/                          # NEW DIRECTORY (D-14)
├── test_strict_axis_a_overload.flow
├── test_strict_axis_b_clamps.flow
├── test_strict_explicit_conversions.flow
├── test_strict_equality.flow
├── test_strict_with_justintonation.flow
├── test_strict_dict_typecheck.flow
└── showcase_strict.flow               # CONTEXT §specifics — small piece using (db x), (hz x), (cents x)

flow-lang.Tests/Phase44/               # NEW (D-14)
├── StrictModeNegativeTests.cs          # ~126 xUnit Facts pinning [strict] error strings
├── ExplicitConversionTests.cs          # forward + reverse direction round-trip
├── PrintCharitablyTests.cs             # non-strict (print 42) auto-str works
├── IfTruthyCoerceTests.cs              # non-strict (if Int x ...) truthy works
├── OverloadResolverStrictTierTests.cs  # +100 tier disabled when ctx.StrictMode=true
├── ModuleLoaderStrictPropagationTests.cs   # strict file imports non-strict, calls into stdlib charitably
├── LiveBlockStrictTests.cs             # strict { live 1bar { ... } } enforces inside
└── ReplStrictMetaCommandTests.cs       # :strict on/off toggle behavior
```

### Anti-Patterns to Avoid

- **Mutating `ctx.StrictMode` without restore.** Always paired push/pop in try/finally. A throw inside a strict-file proc must leave the caller's strict bit unchanged on unwind.
- **Reading `ctx.StrictMode` from stdlib lambdas.** Stdlib reads `ctx.CallerStrictMode`. Reading `ctx.StrictMode` would make all stdlib internal calls (e.g., `swing` internally calling `transpose`) inherit strict from the OUTER caller, which contradicts D-03's "stdlib stays charitable internally."
- **Auto-promoting `[live]` to strict error.** Hard carve-out per D-06 / D-v1.5-07. Live coding must never die mid-set.
- **Inferring strict from the call-site file in OverloadResolver.** Axis A uses the EXECUTING frame's strict bit (`ctx.StrictMode` set by D-02/D-03 per the declaring file). A strict file calling `@audio.gain` resolves overloads under @audio's NON-strict mode (correct per D-03). Axis B uses `ctx.CallerStrictMode` (set at dispatch) so the same `@audio.gain` errors on out-of-range input.
- **Reusing `ProgramPragmaSet`.** Currently declared but unused; tempting to wire it. Don't — it has fuzzy semantics ("the active program-level set"). Use the dedicated `StrictMode` + `CallerStrictMode` fields per D-02/D-05.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Pragma name closed-set + did-you-mean | Per-pragma case statements | Add row to `PragmaRegistry.KnownPragmas` | Existing levenshtein + alphabetized known-names + scala-pragma pointer logic is centralized |
| File-scope pragma push/pop | New global stack class | Read `pragmaSet.Has("strict")` at `FlowEngine.Execute` / `ModuleLoader.LoadModule` boundaries; capture into `ExecutionContext.StrictMode` once | The PragmaScanner already runs per file load with proper isolation (line 83-92 of ModuleLoader) |
| Strict-bit threading through type system | Static `ThreadLocal<bool>` | Explicit `bool strictMode = false` parameter on `Matches()` / `Resolve()` | Single-threaded interpreter; explicit-param keeps tests deterministic and avoids spooky-action-at-a-distance |
| Error sentinel format | Per-site ad-hoc strings | `[strict] <existing-WarnOnce-body>` prefix verbatim | D-07 + AUDIT §6a Column 5 — xUnit pins each string; verbatim wording is load-bearing |
| Music-type construction in (db x) etc. | New constructor surface | Use existing `Value.Decibel(double)`, `Value.Hertz(double)`, etc. — these already exist on `Value.cs` for music literals | Saves overload-table thrash; idempotent path falls through to identity |
| Test harness for ~126 strict error strings | Per-test reflection over StdLib | Static array of `(callSite, strictArgs, expectedSentinel)` tuples → xUnit Theory | Mirrors Phase 32 `TuningDescriptionTests` / Phase 42 `ClampGrepConsistencyTests` |

**Key insight:** the entire Phase 44 mechanic is "lift an existing bit (pragma → mode) from parse-time to runtime, plumb it through dispatch, then BRANCH on it at ~126 leaf sites." No new algorithms; every primitive already exists.

## Runtime State Inventory

Phase 44 is NOT a rename/refactor/migration phase — no runtime state needs migrating. Section omitted per researcher convention.

(No external services, no stored data, no OS-registered state, no secrets, no build artifacts affected by Phase 44 changes. The pragma name `strict` is brand-new; no prior versions to migrate.)

## Common Pitfalls

### Pitfall 1: Conflating "+100 tier" with "CanConvertTo clause"

**What goes wrong:** Plan-author reads "disable +100 tier" and removes only the `else if (argType.CanConvertTo(paramType)) score += 100` branch in `CalculateSpecificity` — assuming `(gain buf -12.0)` would then fail because it can't reach +500 or +1000. But specificity scoring runs AFTER `Matches()` filters, and `Matches()` accepts the call via clause 3 (`InputTypes[i].IsCompatibleWith(argTypes[i])` — i.e., `Decibel.IsCompatibleWith(Double)=true`) which currently scores ZERO points and would still win as the only candidate.
**Why:** The "+100 tier" naming in CONTEXT D-01 is conceptual; both `arg.CanConvertTo(param)` and the inverse `param.IsCompatibleWith(arg)` are implicit conversions. Both must be excluded under strict.
**How to avoid:** Filter at `Matches()` (Pattern 4 above) so convertible-only candidates are excluded from the pool, not the score. Pin xUnit Fact: `(gain buf -12.0)` in a strict file produces a `No matching overload for function 'gain'` error (existing OverloadResolver error message — no need for a new `[strict]` prefix; the missing-conversion guidance is implicit). Optionally beef the error with a hint when strict + a non-strict candidate would have matched.
**Warning signs:** A strict test for `(gain buf -12.0)` PASSES when it should fail. Smoking gun: the +500 IsCompatibleWith path on the inverse direction was missed.

### Pitfall 2: Forgetting carve-outs at site rewrite time

**What goes wrong:** A mass `sed` over `WarnOnce(...)` → `if (ctx.CallerStrictMode) ErrorReporter.Report(...); else WarnOnce(...)` accidentally also rewrites `[live]` block-entry advisory (Interpreter.cs:476) and the 4 `[improv]` style-pack discovery advisories (StyleRegistry.cs:156, 244, 258, 265).
**Why:** D-06 explicit carve-outs aren't textually marked in the source.
**How to avoid:** Plan tasks enumerate carve-outs explicitly:
  - `flow-lang/Interpreter/Interpreter.cs:476` — `[live] entering live block` advisory (STAYS charitable per D-v1.5-07).
  - `flow-lang/StandardLibrary/Improv/StyleRegistry.cs:156, 244, 258, 265` — 4 `[improv]` style-pack discovery advisories (STAY charitable per D-06).
  - These 5 sites must remain `RenderingDiagnostics.WarnOnce(...)` unmodified.
**xUnit pin:** `LiveBlockChartiableInStrictTests` — a strict file containing `live 1bar { ... }` MUST still emit the `[live] entering live block ...` advisory and MUST NOT error from the live-entry path.

### Pitfall 3: Pre-strict bug fix collides with existing `(print String)` registration

**What goes wrong:** Adding `print(Void)` overload changes ambiguity resolution for `(print someString)`. Today only `print(String)` exists; the call resolves uniquely. After adding `print(Void)`, OverloadResolver could rank both at +1000 vs +500 — but `String.Equals(String)=true` gives +1000, `String.IsCompatibleWith(Void) is via Void-wildcard → +500`. The String-specific overload wins by specificity.
**Why:** VoidType is the wildcard sentinel per `OverloadResolver.cs:434-436` ("VoidType is used as a wildcard"). Confirm that Void at param slot scores +500 (compatible) not +1000.
**How to avoid:** Verify OverloadResolver behavior with a unit test BEFORE registering the wildcard. Existing `equals`/`lt`/`gt` already use Void-wildcard signatures (line 438-472) — pattern is proven.
**Warning signs:** `(print "hello")` in non-strict mode produces wrong output (e.g., type-coerced str via the wildcard path instead of direct write). Pin xUnit: `(print "hello")` in non-strict produces `"hello\n"` byte-identical via the explicit-String path.

### Pitfall 4: Threading strict bit through `OverloadResolver.Resolve`

**What goes wrong:** `OverloadResolver` doesn't currently see `ExecutionContext`. Adding an `ExecutionContext` parameter to every caller (Interpreter, ExpressionEvaluator, ExecutionContext.ResolveFunction itself) is invasive.
**Why:** Resolver is intentionally pure-function today.
**How to avoid:** Pass `bool strictMode` (NOT the full context) through the existing `Resolve()` signature, defaulted false. `ExecutionContext.ResolveFunction` reads `this.StrictMode` once at entry and forwards. The change is one-parameter-deep across ~3 call sites.
**Warning signs:** Tests that construct an OverloadResolver directly with `new OverloadResolver(errorReporter)` need updating only if they exercise strict mode — defaulted `false` keeps old test behavior.

### Pitfall 5: Two-run cmp-clean determinism

**What goes wrong:** A site rewrite adds a new condition that reads a non-deterministic value (clock, PRNG, etc.) along the strict-error path. Non-strict path stays byte-identical, but strict-mode introduction quietly breaks a regression baseline.
**Why:** Strict mode promises ZERO new PRNG sites (CLAUDE.md "Conventions"; CONTEXT D-14).
**How to avoid:** Every site rewrite is shape `if (ctx.CallerStrictMode) errorReporter.Report(LITERAL_STRING_BUILT_FROM_ARGS); else { existing-charitable-body }`. The literal string is composed from `args[i].As<T>()` + sentinel verbatim. No `DateTime.Now`, no `Random`, no `Guid.NewGuid()`. Pin xUnit: `TwoRunDeterminismTests` runs a representative subset of `tests/strict/*.flow` twice, asserts SHA-equal.
**Warning signs:** A new GUID, timestamp, or random number in any `[strict]` error message.

### Pitfall 6: `live { }` block strict re-eval misses pragma when block is in a non-strict file

**What goes wrong:** A composer drops `live 1bar { (print 42) }` in a non-strict file expecting Phase 44's pre-strict bug fix (auto-str). Live reload renders the body via fresh engine — but the OUTER file's strict bit determines whether `(print 42)` errors or auto-strs. Both paths should give the SAME observable behavior (the file isn't strict; auto-str works). The pitfall is when composer EDITS the file to add `enable strict;` while live is running — reload triggers, parses new strict bit, errors on `(print 42)`.
**Why:** D-15 explicitly: "Live-reload re-eval also applies strict checks to the new body."
**How to avoid:** Document in Phase 44 plan that adding `enable strict;` to a file with active `live { }` blocks may surface new errors on the next reload. Composer-facing message at error site already names the line; no additional UX needed. xUnit: `LiveBlockReloadAddStrictPragmaTests` — start non-strict live, add pragma + violation, assert reload error captured + audio fades (existing Phase 38 LIVE-03 stale-closure gate handles the body-drop).

### Pitfall 7: `enable strict;` mid-file (after first statement) error message

**What goes wrong:** Composer writes `Int x = 5; enable strict;` — PragmaScanner's D-11 path fires "pragmas must appear before any other statement" error. This is correct behavior; just ensure the error mentions `strict` explicitly (the existing PragmaScanner code at lines 167-173 already includes the pragma name in the error).
**How to avoid:** No change needed; this is free behavior from existing infrastructure. xUnit: `PragmaScannerStrictAfterStatementErrorTests`.

### Pitfall 8: `enable strict; enable justIntonation;` composition

**What goes wrong:** Composer assumes one pragma overrides the other. They don't — both apply (D-09 set semantics in PragmaScanner; line 154 "duplicate is silent, set semantics" — duplicates of the SAME pragma are silent; different pragmas compose).
**How to avoid:** Confirm via positive test `tests/strict/test_strict_with_justintonation.flow` (CONTEXT §specifics calls this out explicitly). xUnit: `PragmaSet.Has("strict") && pragmaSet.Has("justIntonation")` both true.

## Runtime State Inventory

Phase 44 is greenfield in the sense that no prior version of strict mode exists. No data/config/OS state needs migrating. The only runtime state introduced is the pair of `ExecutionContext` boolean fields, which initialize to `false` on every engine construct — no migration semantics.

## Code Examples

### Pragma registration — one-line addition

```csharp
// flow-lang/Lexing/PragmaRegistry.cs:27 — D-04
["strict"] = "Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports.",
```

### ExecutionContext field additions

```csharp
// flow-lang/Runtime/ExecutionContext.cs — near other per-context bools

/// <summary>
/// Phase 44 D-02 / D-03 — true when the currently-EXECUTING proc was DECLARED
/// in a file that began with `enable strict;`. Push/pop in
/// <see cref="Interpreter.Interpreter.ExecuteUserFunctionWithCaptures"/>.
/// Read by <see cref="TypeSystem.OverloadResolver"/> (via
/// <see cref="ResolveFunction"/>) to disable Axis A coercion tiers.
/// </summary>
public bool StrictMode { get; set; } = false;

/// <summary>
/// Phase 44 D-05 — snapshot of <see cref="StrictMode"/> at the call boundary
/// of the most recently-invoked builtin. Set in
/// <see cref="Interpreter.ExpressionEvaluator.EvaluateFunctionCall"/> right
/// before the builtin lambda runs, restored after. Stdlib clamp + advisory
/// sites read THIS field for Axis B routing — NOT <see cref="StrictMode"/>
/// (which would force stdlib internal-to-internal calls to inherit strict).
/// </summary>
public bool CallerStrictMode { get; set; } = false;
```

### Axis B site rewrite — `crescendo` startVel clamp (TransformFunctions.cs:649)

```csharp
// BEFORE
private static Value Crescendo(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double startVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
    double endVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
    return Value.Sequence(ApplyVelocityGradient(seq, startVel, endVel));
}

// AFTER (context-dependent variant — captures `context` + `errorReporter` in closure)
private static Value Crescendo(IReadOnlyList<Value> args, ExecutionContext ctx, ErrorReporter er)
{
    var seq = args[0].As<SequenceData>();
    double startRaw = args[1].As<double>();
    double endRaw = args[2].As<double>();
    if (ctx.CallerStrictMode)
    {
        if (startRaw < 0.0 || startRaw > 1.0)
        {
            er.ReportError(
                $"[strict] crescendo startVel {startRaw} outside [0.0, 1.0]",
                ctx.CurrentCallSite);
            return Value.Void();
        }
        if (endRaw < 0.0 || endRaw > 1.0)
        {
            er.ReportError(
                $"[strict] crescendo endVel {endRaw} outside [0.0, 1.0]",
                ctx.CurrentCallSite);
            return Value.Void();
        }
        return Value.Sequence(ApplyVelocityGradient(seq, startRaw, endRaw));
    }
    double startVel = Math.Clamp(startRaw, 0.0, 1.0);
    double endVel = Math.Clamp(endRaw, 0.0, 1.0);
    return Value.Sequence(ApplyVelocityGradient(seq, startVel, endVel));
}
```

The 13 §6a clamp sites follow this exact template — verify each error string against AUDIT §6a Column 5 wording.

### Axis B site rewrite — `[markov] order clamped to [1, 3]` advisory

```csharp
// flow-lang/StandardLibrary/Generative/MarkovFunctions.cs — representative
// BEFORE
if (order > 3 || order < 1)
{
    RenderingDiagnostics.WarnOnce(
        $"markov:order-clamped:{ctx.CurrentCallSite}",
        $"[markov] order {order} clamped to [1, 3]");
    order = Math.Clamp(order, 1, 3);
}

// AFTER
if (order > 3 || order < 1)
{
    if (ctx.CallerStrictMode)
    {
        er.ReportError(
            $"[strict] [markov] order {order} clamped to [1, 3]",
            ctx.CurrentCallSite);
        return Value.Void();
    }
    RenderingDiagnostics.WarnOnce(
        $"markov:order-clamped:{ctx.CurrentCallSite}",
        $"[markov] order {order} clamped to [1, 3]");
    order = Math.Clamp(order, 1, 3);
}
```

### Comparison strict branch — `(gt 1 "2")` cross-type errors

```csharp
// flow-lang/StandardLibrary/StdLib.cs — context-dependent variant
public static Value GreaterThan(IReadOnlyList<Value> args, ExecutionContext ctx, ErrorReporter er)
{
    if (ctx.CallerStrictMode && !args[0].Type.Equals(args[1].Type))
    {
        er.ReportError(
            $"[strict] cross-type comparison {args[0].Type} vs {args[1].Type} — use explicit (double x) / (int x)",
            ctx.CurrentCallSite);
        return Value.Void();
    }
    return Value.Bool(Utils.CompareNumeric(args[0], args[1]) > 0);
}
```

### Print pre-strict — Void-wildcard overload

```csharp
// flow-lang/StandardLibrary/BuiltInFunctions.cs RegisterStdLib — near line 162
var printAnySig = new FunctionSignature("print", [VoidType.Instance], ParameterNames: ["s"]);
registry.Register("print", printAnySig, args =>  // OR context-dependent variant
{
    // Non-strict charitable — auto-str via the same path str() uses
    Console.WriteLine(StdLib.AutoStr(args[0]));
    return Value.Void();
});
// Existing print(String) at line 158-162 stays — higher-specificity overload wins for String args
```

Add a helper `StdLib.AutoStr(Value v)` that mirrors the existing `str` dispatch (uses `value.ToString()` for primitives, `seq.ToString()` for Sequence, etc.). The strict-error branch is gated inside the wildcard impl as shown above.

### Explicit-conv builtin registration

```csharp
// flow-lang/StandardLibrary/ConversionFunctions.cs — new file
public static class ConversionFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterDecibel(registry);
        RegisterHertz(registry);
        RegisterMillisecond(registry);
        RegisterSecond(registry);
        RegisterCent(registry);
        RegisterSemitone(registry);
        // Reverse direction overloads — wire into existing (double x) / (float x) / (int x) / (long x)
        RegisterReverseExtractors(registry);
    }
    private static void RegisterDecibel(InternalFunctionRegistry registry)
    {
        // 5 overloads: Int, Long, Float, Double, Decibel (idempotent)
        ...
    }
    private static void RegisterSemitone(InternalFunctionRegistry registry)
    {
        // 1 overload: Int only (D-08 whole-numbers-by-design)
        var sig = new FunctionSignature("semitones", [IntType.Instance], ParameterNames: ["x"]);
        registry.Register("semitones", sig, args => Value.Semitone(args[0].As<int>()));
        // (semitones Float|Double|Long) calls fall through to OverloadResolver
        // "no matching overload" error. Strict-mode-specific message wording
        // can be customized if we want a friendlier hint.
    }
}
```

Wire into `BuiltInFunctions.RegisterAllImplementations` near line 41 (alongside `RegisterMath`).

### REPL `:strict on/off` meta-command

```csharp
// flow-interpreter/Repl.cs HandleCommand — extend the switch (around line 216-223)
private bool _sessionStrict = false;     // D-16 sticky flag

return command.ToLower() switch
{
    ":quit" or ":q" or ":exit" => false,
    ":help" or ":h" => ShowHelp(),
    ":clear" or ":cls" => ClearScreen(),
    ":stop" => StopAudio(),
    ":strict on" => SetStrict(true),     // Phase 44 D-16
    ":strict off" => SetStrict(false),    // Phase 44 D-16
    _ => UnknownCommand(command)
};

private bool SetStrict(bool on)
{
    _sessionStrict = on;
    Console.WriteLine($"[strict] {(on ? "on" : "off")}");
    return true;
}

// Before each engine.Execute(line, fileName) call:
// _engine.Context.StrictMode = _sessionStrict;
// (if line starts with `enable strict;` PragmaScanner will set it true and we
// observe → update _sessionStrict to true after Execute returns)
```

## State of the Art

| Old Approach (today) | Current Approach (Phase 44) | When Changed | Impact |
|--------------------|--------------------------|--------------|--------|
| `print Int x` fails with "No matching overload for 'print'" | `print Int x` non-strict auto-strs; strict errors `[strict] (print) requires String — got Int` | Phase 44 | Ergonomics-priority fix for ALL composers (not strict-only) |
| `if Int x { ... }` fails with "No matching overload for 'if'" | Non-strict truthy-coerces `x ≠ 0`; strict errors `[strict] (if) requires Bool — got Int` | Phase 44 | Same |
| `(gain buf -12.0)` accepted via inverse `Decibel.IsCompatibleWith(Double)=true` clause | In strict files: errors with "No matching overload for 'gain'". Non-strict: unchanged | Phase 44 Axis A | Only strict-file composers see change |
| `(swing seq res 1.5 0.5)` clamps strength to 1.0 + emits no advisory | Non-strict unchanged; strict errors `[strict] swing strength 1.5 outside [0.0, 1.0]` | Phase 44 Axis B | Only strict-file composers; 13 sites |
| `[markov] order clamped to [1, 3]` advisory always charitable | Non-strict unchanged; strict errors `[strict] [markov] order N clamped to [1, 3]` | Phase 44 Axis B | ~113 in-scope advisory sites |
| `(equals 1 1.0)` returns true | Returns false in BOTH modes (set-theoretic; D-11 same as non-strict for this builtin) | Phase 44 Axis C | Behavior change for everyone — confirm with Composer Review |

**WAIT — D-11 wording:** "`(equals a b)` cross-type → returns `false` in strict (per ROADMAP — set-theoretic, not error). Same as non-strict for this builtin." Today `Utils.LooseEquals` returns TRUE for `(equals 1 1.0)` via the numeric coercion path at lines 73-76. CONTEXT D-11 says the strict behavior is "same as non-strict for this builtin" — but the AUDIT/ROADMAP wording says strict returns FALSE. There's a conflict here that the planner must resolve. Recommend reading D-11 literally: strict returns `false` for cross-type. Non-strict KEEPS current LooseEquals behavior (numeric coercion returns true for `(equals 1 1.0)`). Document the discrepancy as an Open Question.

**Deprecated/outdated:**
- None — strict mode is purely additive.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ProcDeclaration.IsStrict` AST flag is acceptable despite CONTEXT D-02's "no AST Program.IsStrict attribute" wording (which is `Program`-level, not per-proc) | Pattern 2 | Planner must consult D-02 alternative — fileName→PragmaSet map on ExecutionContext at file-load |
| A2 | `OverloadResolver` strict-tier filtering is best done in `Matches()` clauses 2+3 (excluding both `arg.CanConvertTo(param)` AND `param.IsCompatibleWith(arg)`) | Pattern 4, Pitfall 1 | If only clause 2 dropped, `(gain buf -12.0)` continues to match — strict contract regresses |
| A3 | Phase 44 promotes `(print)`/`if`/`(and)`/`(or)`/`(not)`/`(equals)`/`(gt)`/`(lt)`/`(gte)`/`(lte)` to context-dependent registration (capturing ExecutionContext + ErrorReporter in closures) | Pattern 6 | If instead a static accessor is used (Option c), thread-local reads must be tested for hermetic-isolation across `flow-lang.Tests` |
| A4 | The 117 `WarnOnce` count from AUDIT §6b minus 5 carve-outs (`[live]` + 4 `[improv]` style-pack) = 112 advisory sites. AUDIT §6b says ~113. The exact count is ~112-113 depending on whether the `improv:userOverride` advisory at StyleRegistry.cs:156 is counted as in-scope (composer-surface) or out-of-scope (environmental). Recommend Plan-Phase Wave 0 grep + reconcile | Site Inventory | Off-by-one in xUnit Fact count |
| A5 | D-11 `(equals 1 1.0)` strict behavior: returns FALSE (CONTEXT D-11 says "set-theoretic"); the parenthetical "same as non-strict for this builtin" is a discrepancy with current `LooseEquals` behavior. Planner must confirm with composer before locking | State of the Art table | Composer-facing equality semantics change for non-strict could regress Phase 26.1 SYM-01 test fixtures |
| A6 | `(not)` is NOT registered today (`flow-lang/test.flow:39` note confirms) — Phase 44 must add it. Both strict (Bool-only) and non-strict (charitable wildcard) overloads | Pattern 6, Code Examples | Composers using ad-hoc local `not` lambdas may collide with the new builtin name |
| A7 | The `live` re-eval path (`LiveReloadManager.RenderScript`) re-runs `FlowEngine.Execute(source, filePath)` which re-runs `PragmaScanner.Scan` — therefore strict re-applies automatically with zero new plumbing | Pattern 7 | If verified false, D-15 requires explicit re-eval hook |
| A8 | The 13 §6a clamp sites are entirely in `TransformFunctions.cs` (lines 106-107, 649-650, 657-658, 666-667, 785, 821, 904, 960, 1106) and NO separate `Swing.cs` exists. CONTEXT integration-points wording mentions `Swing.cs` but the actual swing clamp lives at TransformFunctions:106-107 inside `quantize` | Site Inventory | Plan-phase searches for missing `Swing.cs` and gets confused |
| A9 | OverloadResolver passes a `bool strictMode = false` defaulted parameter through `Resolve` → `Matches` — backward compatible | Pattern 4, Pitfall 4 | If a third-party caller exists for `OverloadResolver` directly, it inherits non-strict (correct default) |

## Open Questions (RESOLVED)

1. **D-11 equality semantics discrepancy.**
   - What we know: CONTEXT D-11 says "(equals a b) cross-type → returns false in strict (per ROADMAP — set-theoretic, not error). Same as non-strict for this builtin." Current `Utils.LooseEquals` returns TRUE for `(equals 1 1.0)` via numeric coercion.
   - What's unclear: Does Phase 44 (a) flip `Utils.LooseEquals` to type-strict (breaking change to non-strict callers), (b) keep non-strict charitable + only strict returns false (matches D-12 pattern for other operators), (c) deprecate `(equals)` cross-type entirely?
   - Recommendation: Option (b) — non-strict keeps existing LooseEquals behavior; strict path returns Value.Bool(false) for cross-type. Confirm with composer at plan-phase. Pin an xUnit Fact in the strict suite either way.
   - **RESOLVED:** Option (b) adopted in Plan 44-09 Task 2 — `Utils.LooseEqualsStrict` short-circuits cross-type to `false` under `ctx.CallerStrictMode`; non-strict `LooseEquals` unchanged. CrossTypeComparisonStrictTests pins both behaviors (Fact_EqualsIntDouble_Strict_ReturnsFalse + Fact_EqualsIntDouble_NonStrict_ReturnsTrue).

2. **`(and)` / `(or)` Lisp-style last-truthy return.**
   - What we know: CONTEXT D-12 says non-strict `(and)`/`(or)` "keep Lisp-style last-truthy return". Current `StdLib.And` returns `Value.Bool(rres)` — always Bool, never the actual underlying value.
   - What's unclear: Is "last-truthy" wording aspirational (composer-stated desired behavior) or descriptive (current behavior)? If aspirational, this is a behavior change.
   - Recommendation: Read existing test fixtures (`tests/test_lazy_eval.flow` etc.) for current `(and)` semantics. If aspirational, ship under Phase 44; if descriptive, current code already complies. Likely aspirational — implication: pre-strict bug fix may include "promote `(and)`/`(or)` return type to Void wildcard, return left or right Value directly."
   - **RESOLVED:** Aspirational, per composer's 44-DISCUSSION-LOG Area 4.2 choice (line 151: "Non-strict: returns last truthy `"foo"`"). Plan 44-08 Task 3 implements non-strict `(and)`/`(or)` last-truthy semantics as a v1.5 breaking change (pre-traction latitude per D-v1.5-01 + project_pre_public_no_legacy_burden memo). Strict `(and Bool Bool)` still returns Bool (Plan 44-09 unaffected). xUnit Facts pin `(and 1 "foo")` → `"foo"`, `(or false 42)` → `42`, `(and false 1)` → `false`, `(or "" "fallback")` → `"fallback"`.

3. **OverloadResolver tier disable: predicate on `Matches()` vs filter on ranked candidates.**
   - What we know: Two implementation routes (see Pattern 4 alternatives table).
   - What's unclear: Which is cheaper for the ~5x existing `OverloadResolver` test fixtures?
   - Recommendation: Filter in `Matches()` per Pitfall 1. Drop-in single-predicate change; ranked-candidate filtering complicates ambiguous-overload diagnostics (line 235-244).
   - **RESOLVED:** Plan 44-03 adopts the `Matches()`-filter route per the recommendation; ranked-candidate filtering rejected (OverloadResolverStrictTierTests verify the +100 tier is disabled while +500/+1000 paths are preserved).

4. **Pre-strict bug fix may break existing `(print someValue)` patterns in tests.**
   - What we know: `print(String)` overload is currently the only registration. Adding `print(Void)` Void-wildcard makes `(print 42)` work.
   - What's unclear: Are there existing tests that depend on `(print 42)` FAILING?
   - Recommendation: Grep `tests/test_*.flow` for `(print <non-string>)` patterns. If any tests assert the failure, update them (they are testing pre-strict-bug behavior).
   - **RESOLVED:** Plan 44-08 Task 1 verify block executes `tests/test_buffer_printing.flow` + `tests/test_comprehensive.flow` after the Void-wildcard `print` registration lands; any test asserting the pre-bug failure is updated in that task per its acceptance criteria. Pitfall 3 ensures `(print "hello")` continues routing to the explicit String overload via +1000 score (Fact_PrintHelloStillRoutesToStringPath pins).

5. **`enable strict;` and `live` block edit-time check timing.**
   - What we know: Phase 38 LIVE-01 advisory fires at live-block ENTRY (run time, not parse time). Phase 44 strict checks fire at OverloadResolver/builtin-entry time (also run time).
   - What's unclear: Per D-15, composer "gets type safety AT EDIT TIME via Phase 38 LIVE-03 stale-closure gating" — but type safety is a parse-time + dispatch-time check, not an edit-time check. Edit-time would require LSP integration.
   - Recommendation: D-15 wording "at edit time" means "at the moment composer types `enable strict;` and saves; on next reload, body runs strict." Not an LSP claim. Plan-phase clarifies.
   - **RESOLVED:** Plan 44-10 Task 2 LiveBlockStrictTests (Fact_LiveReloadAddStrictPragma_BodyRerunStrict) pins the "next reload" behavior — Pattern 7 auto-apply via fresh-engine PragmaScanner re-eval. No LSP plumbing; the "edit time" wording is interpreted as "next save-then-reload cycle." Plan 44-10 manual-verification section documents that watch-mode file-system-event timing is inherited from Phase 38 LIVE-02 (see W12 note).

## Environment Availability

Phase 44 has NO external tool/runtime/service dependencies. Pure C# 13 + .NET 10 (already required by repo). No new NuGets. No new system libraries. PulseAudio + CoreAudio + DryWetMidi unchanged.

Audit harness from Phase 42 (`scripts/StdlibAuditor`) is used at plan-phase Wave 0 for re-confirming §6a + §6b counts but is NOT a runtime dependency of strict mode itself.

Skip remainder per researcher convention.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (latest in `flow-lang.Tests.csproj`) + `.flow` script test convention (no unit framework — output-verified) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "Category=Phase44"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-STRICT-01 | `enable strict;` pragma recognized | unit (xUnit) | `dotnet test --filter "FullyQualifiedName~PragmaRegistryStrictTests"` | ❌ Wave 0 |
| REQ-STRICT-01 | Unknown `enable strict;` typo gets levenshtein suggestion | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerStrictTypoTests"` | ❌ Wave 0 |
| REQ-STRICT-02 | `ctx.StrictMode` set after PragmaScanner.Scan when pragma present | unit | `dotnet test --filter "FullyQualifiedName~ExecutionContextStrictModeTests"` | ❌ Wave 0 |
| REQ-STRICT-02 | Strict file imports non-strict module — module procs run non-strict | unit | `dotnet test --filter "FullyQualifiedName~ModuleLoaderStrictPropagationTests"` | ❌ Wave 0 |
| REQ-STRICT-03 | `ctx.CallerStrictMode` snapshotted at call dispatch | unit | `dotnet test --filter "FullyQualifiedName~CallerStrictModeSnapshotTests"` | ❌ Wave 0 |
| REQ-STRICT-04 | OverloadResolver disables +100 tier in strict | unit | `dotnet test --filter "FullyQualifiedName~OverloadResolverStrictTierTests"` | ❌ Wave 0 |
| REQ-STRICT-05 | 6 forward conv builtins all 4 numeric + idempotent | unit | `dotnet test --filter "FullyQualifiedName~ExplicitConversionForwardTests"` | ❌ Wave 0 |
| REQ-STRICT-06 | 4 reverse extractors all 6 tagged types | unit | `dotnet test --filter "FullyQualifiedName~ExplicitConversionReverseTests"` | ❌ Wave 0 |
| REQ-STRICT-07 | 13 §6a clamp sites error in strict with verbatim message | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Axis_B_ClampSiteTests"` | ❌ Wave 0 |
| REQ-STRICT-08 | ~113 §6b advisory sites error in strict with verbatim message | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Axis_B_AdvisorySiteTests"` | ❌ Wave 0 |
| REQ-STRICT-09 | Strict `(and)`/`(or)`/`(not)`/`if` Bool-only | unit | `dotnet test --filter "FullyQualifiedName~AxisCBoolRequiredTests"` | ❌ Wave 0 |
| REQ-STRICT-10 | Non-strict (print 42) auto-strs | unit | `dotnet test --filter "FullyQualifiedName~PrintCharitablyTests"` | ❌ Wave 0 |
| REQ-STRICT-11 | Cross-type `(gt 1 "2")` errors in strict | unit | `dotnet test --filter "FullyQualifiedName~CrossTypeComparisonStrictTests"` | ❌ Wave 0 |
| REQ-STRICT-12 | `enable strict;` + `live` block: body runs strict on reload | unit | `dotnet test --filter "FullyQualifiedName~LiveBlockStrictTests"` | ❌ Wave 0 |
| REQ-STRICT-13 | REPL `:strict on/off` toggles `ctx.StrictMode` | unit | `dotnet test --filter "FullyQualifiedName~ReplStrictMetaCommandTests"` | ❌ Wave 0 |
| REQ-STRICT-14 | `tests/strict/*.flow` positive integration suite runs to completion | integration | `for f in tests/strict/test_*.flow; do dotnet run --project flow-interpreter "$f"; done` | ❌ Wave 0 |
| REQ-STRICT-15 | Two-run cmp-clean preserved across strict mode introduction | integration | `dotnet test --filter "FullyQualifiedName~Phase44TwoRunDeterminismTests"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "Category=Phase44"` (~30s — Phase 44 negative + positive suites)
- **Per wave merge:** `dotnet test` (~2-3min — full suite green)
- **Phase gate:** Full suite green + `tests/strict/*.flow` all run to completion before `/gsd:verify-work`

### Wave 0 Gaps

The Phase44 test directory does NOT exist today; entire suite is Wave 0:

- [ ] `flow-lang.Tests/Phase44/` directory creation
- [ ] `tests/strict/` directory creation
- [ ] `flow-lang.Tests/Phase44/StrictModeNegativeTests.cs` — ~126 Facts pinning error strings (use xUnit `[Theory]` + `[InlineData]` for the ~126 site-rewrites; verbatim strings extracted from AUDIT §6a Column 5 + per-site §6b sentinel bodies)
- [ ] `flow-lang.Tests/Phase44/ExplicitConversionTests.cs`
- [ ] `flow-lang.Tests/Phase44/OverloadResolverStrictTierTests.cs`
- [ ] `flow-lang.Tests/Phase44/PrintCharitablyTests.cs`
- [ ] `flow-lang.Tests/Phase44/IfTruthyCoerceTests.cs`
- [ ] `flow-lang.Tests/Phase44/CallerStrictModeSnapshotTests.cs`
- [ ] `flow-lang.Tests/Phase44/ModuleLoaderStrictPropagationTests.cs`
- [ ] `flow-lang.Tests/Phase44/PragmaRegistryStrictTests.cs`
- [ ] `flow-lang.Tests/Phase44/LiveBlockStrictTests.cs`
- [ ] `flow-lang.Tests/Phase44/ReplStrictMetaCommandTests.cs`
- [ ] `flow-lang.Tests/Phase44/AxisCBoolRequiredTests.cs`
- [ ] `flow-lang.Tests/Phase44/CrossTypeComparisonStrictTests.cs`
- [ ] `flow-lang.Tests/Phase44/Phase44TwoRunDeterminismTests.cs`
- [ ] `tests/strict/test_strict_axis_a_overload.flow`
- [ ] `tests/strict/test_strict_axis_b_clamps.flow`
- [ ] `tests/strict/test_strict_explicit_conversions.flow`
- [ ] `tests/strict/test_strict_equality.flow`
- [ ] `tests/strict/test_strict_with_justintonation.flow`
- [ ] `tests/strict/test_strict_dict_typecheck.flow`
- [ ] `tests/strict/showcase_strict.flow`

xUnit framework already installed; existing `Phase42/ClampGrepConsistencyTests.cs` provides the template (file:line pinning + per-site verbatim string assertion).

## Site Inventory (Verified Against AUDIT-Data)

### §6a 13 Input-Perimeter Clamps (Phase 44 Axis B HIGH)

All 13 sites confirmed in `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (file:line per `42-AUDIT-data/input-clamps.txt`). The CONTEXT integration-points note mentioning `Swing.cs` is inaccurate — the swing clamps live in `TransformFunctions.cs:106-107` inside the `quantize(Sequence, NoteValue, strength, swing)` builtin (verified by reading the file). No separate `Swing.cs` exists.

| # | Builtin | Site (file:line) | Param | Error Sentinel (D-07 with `[strict]` prefix) |
|---|---------|------------------|-------|----------------------------------------------|
| 1 | quantize | TransformFunctions.cs:106 | strength | `[strict] swing strength {value} outside [0.0, 1.0]` |
| 2 | quantize | TransformFunctions.cs:107 | swing | `[strict] swing factor {value} outside [-1.0, 1.0]` |
| 3 | crescendo | TransformFunctions.cs:649 | startVel | `[strict] crescendo startVel {value} outside [0.0, 1.0]` |
| 4 | crescendo | TransformFunctions.cs:650 | endVel | `[strict] crescendo endVel {value} outside [0.0, 1.0]` |
| 5 | decrescendo | TransformFunctions.cs:657 | startVel | `[strict] decrescendo startVel {value} outside [0.0, 1.0]` |
| 6 | decrescendo | TransformFunctions.cs:658 | endVel | `[strict] decrescendo endVel {value} outside [0.0, 1.0]` |
| 7 | swell | TransformFunctions.cs:666 | edgeVel | `[strict] swell edgeVel {value} outside [0.0, 1.0]` |
| 8 | swell | TransformFunctions.cs:667 | peakVel | `[strict] swell peakVel {value} outside [0.0, 1.0]` |
| 9 | humanize | TransformFunctions.cs:785 | amount | `[strict] humanize amount {value} outside [0.0, 1.0]` |
| 10 | humanizeGaussian | TransformFunctions.cs:821 | amount | `[strict] humanizeGaussian amount {value} outside [0.0, 1.0]` |
| 11 | vary | TransformFunctions.cs:904 | amount | `[strict] vary amount {value} outside [0.0, 1.0]` |
| 12 | legato | TransformFunctions.cs:960 | amount | `[strict] legato amount {value} outside [0.0, 1.0]` |
| 13 | repeat | TransformFunctions.cs:1106 | reps | `[strict] repeat reps {value} outside [1, 16]` |

The remaining 59 `Math.Clamp` sites in `TransformFunctions.cs` (and elsewhere) are output-protection (MIDI byte clamps, velocity recomputes, internal DSP coefficients) — culled per AUDIT Pitfall 4. Pin xUnit `Phase44ClampGrepConsistencyTests` to assert exactly 13 input-perimeter sites remain (mirrors Phase 42 `ClampGrepConsistencyTests`).

### §6b 117 Advisory Sites Grouped (Phase 44 Axis B by priority)

Verified `WarnOnce` count per module via grep (matches AUDIT §6b ±0 across all modules). Total:

**HIGH (in-scope, ~79 sites):**
- `Audio/Sfz/` (SfzBuiltins 3 + SfzParser 16 + SfzRenderer 6 + SfzSampleCache 2) = 22
- `Patterns/PatternFunctions.cs` = 17 (every/chunk/jux/sometimes/degrade/sparseSeq — all degenerate-input only)
- `Generative/ChaosFunctions.cs` = 17 (treat as MEDIUM per AUDIT §7b)
- `Improv/JamFunctions.cs` = 16 (IN-SCOPE; only StyleRegistry.cs's 4 are out)
- `Audio/SongRenderer.cs` = 2 + `Audio/SampledInstrumentRenderer.cs` = 3 + `Ast/Expressions/MatchExpression.cs`'s `match-non-exhaustive` at ExpressionEvaluator.cs:569 = 1 (HIGH per AUDIT §7b)
- `Audio/DSP/` (GranularFunctions 1 + PitchShiftFunctions 1 + StretchEngine 2 + StretchFunctions 1) = 5

**MEDIUM (in-scope, ~28 sites):**
- `Notation/AbcImport.cs` = 8 + `AbcLexer.cs` = 2 + `MmlImport.cs` = 5 = 15
- `Generative/MarkovFunctions.cs` = 6 + `LsystemFunctions.cs` = 6 + `CellularFunctions.cs` = 3 = 15 (already counted with ChaosFunctions above? — AUDIT splits these)
- `Network/OscFunctions.cs` = 3
- `Audio/Tuning/ScalaBuiltins.cs` = 2
- `Harmony/HarmonyFunctions.cs` = 1
- `Audio/BeatConversionFunctions.cs` = 3 (Phase 43 addition — verify against AUDIT)

**LOW (in-scope, ~6 sites):**
- `Audio/InputFunctions.cs` = 3 (`[audio-in]`)
- `Audio/MidiExport.cs` = 1
- `Audio/SampledInstrumentRenderer.cs`'s piano-specific sites = 3 (or split)

**CARVE-OUTS (5 sites STAY charitable):**
- `Interpreter/Interpreter.cs:476` — `[live] entering live block ...` (D-v1.5-07 design-lock)
- `StandardLibrary/Improv/StyleRegistry.cs:156, 244, 258, 265` — 4 `[improv]` style-pack discovery (LOW priority per AUDIT §7b)

**Total verified: ~120-127 in-scope sites.** The AUDIT estimate of "~113" is close but the exact count requires Wave 0 reconciliation. Plan-phase Wave 0 task: grep + categorize each WarnOnce site explicitly into IN-SCOPE / CARVE-OUT, produce a manifest file `flow-lang.Tests/Phase44/strict-error-manifest.csv` used by xUnit Theory generator.

### §6c Charitable-Fallback Discovery Sweep (AUDIT §8 Limitation 5)

`42-AUDIT-data/charitable-sites.txt` (110 entries) contains pointers to bespoke `if (x < 0) x = 0` patterns that may have escaped the `Math.Clamp` regex. AUDIT estimates <5 misses.

**Plan-phase Wave 0 task:** spot-check 20 random entries from `charitable-sites.txt`. If any match the "input-perimeter clamp" shape (i.e., the clamp acts on `args[N].As<T>()` direct read), add to the strict error site list. If <5 found (matching AUDIT estimate), document them; if >5, escalate to composer review.

## Sources

### Primary (HIGH confidence — verified in this session)
- `.planning/phases/44-strict-mode/44-CONTEXT.md` (16 locked decisions D-01..D-16)
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` (load-bearing §6a + §6b + §7b + §8)
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/input-clamps.txt` (verified 13 entries)
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/advisory-sites.txt` (verified 117 entries)
- `.planning/ROADMAP.md` §"Phase 44: Strict Mode" (goal + three-axes + pre-strict bug fix + Phase 42 dependency)
- `flow-lang/Lexing/PragmaRegistry.cs:1-63` (closed-set + levenshtein)
- `flow-lang/Lexing/PragmaScanner.cs:1-258` (scan flow + D-11 + D-12 + CRLF + comment handling)
- `flow-lang/TypeSystem/OverloadResolver.cs:1-248` (full file read)
- `flow-lang/TypeSystem/FunctionSignature.cs:1-176` (Matches + CalculateSpecificity scoring)
- `flow-lang/TypeSystem/FlowType.cs:1-74` (base + virtual IsCompatibleWith/CanConvertTo)
- `flow-lang/TypeSystem/SpecialTypes/CentType.cs:1-48` (pattern reference for D-08)
- `flow-lang/TypeSystem/SpecialTypes/SemitoneType.cs` (Int-only widening pattern)
- `flow-lang/TypeSystem/PrimitiveTypes/{Int,Double,Float,Long}Type.cs` (CanConvertTo widening lattice)
- `flow-lang/Runtime/ExecutionContext.cs:1-892` (full file read; existing fields + Phase 35/36/38 precedents)
- `flow-lang/Runtime/ModuleLoader.cs:1-264` (per-file PragmaScanner.Scan at line 83-92)
- `flow-lang/Core/FlowEngine.cs:200-330` (Execute flow + ApplyTuningPragma)
- `flow-lang/Interpreter/ExpressionEvaluator.cs:222-417` (EvaluateFunctionCall with qualified-call branch + prevCallSite save/restore pattern at 399-409)
- `flow-lang/Interpreter/ExpressionEvaluator.cs:523-573` (EvaluateMatch + Phase 35 CapturedPragmas usage at 560)
- `flow-lang/Interpreter/Interpreter.cs:161-185, 446-490, 1095-1160` (musical context block; live block; ExecuteUserFunctionWithCaptures)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:151-475` (print/if/and/or/equals/lt/gt registrations)
- `flow-lang/StandardLibrary/StdLib.cs:28-560` (Print/If/IfStrict/And/AndBool/Or/OrBool/Equals/StrictEquals/LessThan/GreaterThan/LessThanOrEqual/GreaterThanOrEqual implementations)
- `flow-lang/StandardLibrary/Utils.cs:1-131` (LooseEquals / CompareNumeric / ToComparableNumber numeric coercion lattice)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:95-1110` (13 §6a clamp sites + ~5 inner output-protection clamps verified)
- `flow-lang/StandardLibrary/Improv/StyleRegistry.cs:140-270` (4 `[improv]` carve-out advisories)
- `flow-lang/StandardLibrary/Improv/JamFunctions.cs:170-810` (16 in-scope `[jam]` advisories)
- `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs:120-720` (degenerate-input-only advisories — no well-formed PAT-01 surface breaks)
- `flow-lang/Parsing/Parser.cs:312-384, 1785-1795` (ProcDeclaration + MatchExpression construction with `_pragmaSet`)
- `flow-lang/Ast/Statements/ProcDeclaration.cs:1-24`
- `flow-lang/Lexing/PragmaSet.cs:1-30`
- `flow-interpreter/Repl.cs:160-260` (HandleCommand meta-command family)
- `flow-interpreter/LiveReloadManager.cs:836-883` (RenderScript fresh-engine + Execute)
- `flow-lang/test.flow:39` (confirms `(not x)` not built-in today)
- `flow-lang.Tests/Phase35/`, `flow-lang.Tests/Phase36/`, `flow-lang.Tests/Integration/Phase42/` (test directory layout precedents)

### Secondary (MEDIUM)
- CLAUDE.md §"Conventions" (two-run cmp-clean + RMS regression pattern + cross-platform chaos caveat)
- CLAUDE.md §"Language Features" (six-keyword reservation; pragma list)
- External memory `feedback_strict_mode_design.md` (user's design pattern for strict — file-scoped opt-in)
- External memory `feedback_charitable_interpretation.md` (user-locked default Phase 44 opts into reversing)
- External memory `project_pre_public_no_legacy_burden.md` (D-v1.5-01 single-commit latitude)

### Tertiary (LOW — none used)
- None — every claim above is verifiable in the repo at the cited file:line.

## Metadata

**Confidence breakdown:**
- Pragma + file-scope plumbing: HIGH — exact pattern shipped 4 times before (Phase 21/23/35/Phase 32).
- OverloadResolver tier disable: HIGH — single predicate change with clear test coverage path.
- §6a 13 clamp sites: HIGH — verified file:line.
- §6b advisory site count: MEDIUM — exact count needs Wave 0 grep reconciliation (~120 vs AUDIT's ~117).
- D-11 equality semantics: MEDIUM — discrepancy between CONTEXT D-11 wording and `Utils.LooseEquals` current behavior flagged as Open Question.
- D-12 last-truthy `(and)`/`(or)`: MEDIUM — likely behavior change vs. current Value.Bool-only return.
- Live + REPL integration: HIGH — re-uses Phase 38 entry points unchanged.
- Two-run determinism preservation: HIGH — mechanical rewrites, no PRNG additions.
- Test infrastructure: HIGH — Phase 35/36/42 precedents direct.

**Research date:** 2026-05-24
**Valid until:** ~2026-06-23 (30 days — code structure stable; CONTEXT decisions locked)
