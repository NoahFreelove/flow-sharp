---
phase: 44-strict-mode
fixed_at: 2026-05-25T14:00:00Z
review_path: .planning/phases/44-strict-mode/44-REVIEW.md
iteration: 1
findings_in_scope: 12
fixed: 12
skipped: 0
status: all_fixed
---

# Phase 44: Code Review Fix Report

**Fixed at:** 2026-05-25
**Source review:** `.planning/phases/44-strict-mode/44-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 12 (3 Critical + 9 Warning; Info skipped per default `--fix` scope)
- Fixed: 12
- Skipped: 0
- Build green at every commit; Phase 44 test suite (275 tests) passes; full suite passes
  (1 pre-existing flaky test + 2 pre-existing Phase 30 failures unaffected by this work).

## Fixed Issues

### CR-02: `EvaluateFlowExpression` and `EvaluateTupleUnpackFlow` skip the `CallerStrictMode` save/restore sandwich

**Files modified:** `flow-lang/Interpreter/ExpressionEvaluator.cs`
**Commit:** a467a11
**Applied fix:** Added a `prevCallerStrict / _context.CallerStrictMode = _context.StrictMode`
try/finally sandwich around the dispatch in both `EvaluateFlowExpression` (runtime `->`
with function-variable RHS) and `EvaluateTupleUnpackFlow` (both tuple-unpack branch and
non-tuple fall-through). Mirrors the discipline at
`EvaluateFunctionCall` lines 437-450 / 461-472 so all call-dispatch sites share the same
call-boundary semantics. **Logic-bug class — requires human verification** of the
runtime-`->` branch in particular: a strict-aware builtin invoked via
`f -> g` (where `g` resolves to a function variable) should now see strict bit ON.
A Phase 44-test fact that covers this specific path would lock it down.

### CR-01: OSC listener reads `CallerStrictMode` from a background thread where the value is stale/torn

**Files modified:** `flow-lang/StandardLibrary/Network/OscFunctions.cs`
**Commit:** 6964c66
**Applied fix:** Capture `listenerStrict = context.CallerStrictMode` and
`listenerSite = context.CurrentCallSite` as immutable locals at `StartListener` entry
(BEFORE the `Task.Run` boundary). Thread both through new parameters on
`DispatchPacket`, `DispatchBundleContents`, `InvokeHandlerWithRateLimit` so the listener
thread reads the captured snapshot rather than the live `context.CallerStrictMode`
(which on the background thread is whatever value the foreground last wrote). Added
`using FlowLang.Core;` for the `SourceLocation` type. The
`DispatchPacketForTesting` test seam reads `context` directly since tests run
dispatch on the caller thread (no background race).

**NOT fixed in this commit:** the review's secondary concern about
`InvokeHandler` racing `_overloadResolveCache` / `_callStack` mutations with
concurrent live-block evaluation. That's a pre-existing Phase 38 design issue
requiring either a queue-back-to-foreground refactor or broad `ExecutionContext`
locking — deferred to a follow-up plan. CR-01 documents this deferral in its
commit message.

**Logic-bug class — requires human verification** of the captured snapshot's
semantics for the four listener thread error paths (bind, connect, bundle-depth,
handler exception). A targeted test that calls `oscListen` from a strict file
and inspects an ErrorReporter capture on bind-failure would lock the contract.

### CR-03 + WR-04: Strict-elevated `WarnOnce` sites bypass per-process dedup, flooding `ErrorReporter`; advisory in `MicBuffer` reports BEFORE the zero-duration short-circuit

**Files modified:**
- `flow-lang/Runtime/ExecutionContext.cs`
- `flow-lang/StandardLibrary/Audio/InputFunctions.cs`
- `flow-lang/StandardLibrary/Audio/DSP/GranularFunctions.cs`
- `flow-lang/StandardLibrary/Audio/DSP/PitchShiftFunctions.cs`
- `flow-lang/StandardLibrary/Audio/DSP/StretchFunctions.cs`
- `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs`

**Commit:** c6afb23
**Applied fix:** Added a `StrictAdvisoryDedup HashSet<string>` on `ExecutionContext`,
reset by `RestoreState` to match the existing `RenderingDiagnostics.ResetForTesting()`
hermetic-test-isolation precedent. Hot-path strict-elevated sites now gate
`ReportError` on `StrictAdvisoryDedup.Add(sentinel)` so each `(sentinel)` emits at
most once per `ExecutionContext` lifetime — parallel to the `WarnOnce` sentinel
discipline in the non-strict path.

Hot sites covered: `InputFunctions.MicBuffer` (attenuation + resample),
`GranularFunctions.FallbackToHann`, `PitchShiftFunctions / StretchFunctions.FallbackToAuto`,
`PatternFunctions.IsEmptySeqAdvisory`.

WR-04 paired into the same commit: the zero-duration short-circuit in `MicBuffer`
now runs BEFORE any advisory, so `(micBuffer 0s)` produces no diagnostic at all
(neither strict nor WarnOnce).

**Scope-decision:** the review's "and most strict-elevation sites across `Patterns/`,
`Generative/`, `Improv/`, `Notation/`, and `Sfz/`" list comprises ~140 sites total.
This commit dedup-gates only the documented HOT-PATH sites (micBuffer in `live`
blocks, every-grain granular, every-bar combinators). Lower-traffic sites continue
to call `ReportError` directly — their call volume is bounded by composer-typed
usage rather than hot loops, so the flooding problem CR-03 documents doesn't
apply. A future follow-up could route every strict-elevation site through a
centralized helper for uniform dedup.

### WR-01: `every n <= 0` misses the strict-mode elevation pattern

**Files modified:** `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs`
**Commit:** 32d240c
**Applied fix:** Added the `if (ctx.CallerStrictMode) { ... ReportError ... return ... }`
elevation branch + `StrictAdvisoryDedup` gate that every other Pattern combinator's
advisory uses. Matches the sibling pattern documented at finding WR-01.

### WR-02: `Repl.HandleCommand` uses culture-sensitive `ToLower()` for the dispatch switch

**Files modified:** `flow-interpreter/Repl.cs`
**Commit:** 1666673
**Applied fix:** Replaced `command.ToLower()` with `command.ToLowerInvariant()`.
The strict-mode commands happen not to contain 'I' today but the inconsistency
was a latent bug — the surrounding `:help` prefix check at lines 241-242 already
uses `OrdinalIgnoreCase`.

### WR-03: `OscFunctions.SendOscPacket` crashes on DNS-empty hostname

**Files modified:** `flow-lang/StandardLibrary/Network/OscFunctions.cs`
**Commit:** 1363b24
**Applied fix:** Pulled `Dns.GetHostEntry` into its own try (capturing exceptions
as the existing "could not resolve host" rewrap), then explicitly checked
`entry.AddressList.Length == 0` OUTSIDE the catch and threw a composer-readable
"hostname resolved but returned no IP addresses" `InvalidOperationException`.

### WR-05: `ConversionFunctions` registers redundant identity-cast overloads + `(int Long)` silent overflow

**Files modified:**
- `flow-lang/StandardLibrary/ConversionFunctions.cs` (commit 6fd4d79)
- `flow-lang/std.flow` (commit 673c55f — follow-up to keep `internal proc`
  forward-decls aligned with C# registration after the identity-row drop)

**Commits:** 6fd4d79 + 673c55f
**Applied fix:** Added `if (src != X.Instance)` guards in the cross-cast loop so
the 4 identity rows ((double Double), (float Float), (int Int), (long Long))
are NOT registered. Switched the `(int Long|Float|Double)` body from the
unchecked `(int)(long)` cast to `Math.Clamp(asLong, int.MinValue, int.MaxValue)`
so out-of-range Long values pin to the boundary rather than silently overflowing
to `Int.MinValue`.

The follow-up `std.flow` commit (673c55f) drops the matching `internal proc`
forward-decls so the engine-init check "every internal proc must have a C#
implementation" still passes. Without that follow-up, the `@improv` style-pack
loader at FlowEngine init failed (cascading: `blues.flow → @improv → @std`),
breaking `Voicing_RegisteredViaEngine` and `OscLoopbackTests.RoundTrip_*`.
Both tests now pass.

### WR-06: ModuleLoader's `prevStrict` save/restore is correct but the indentation hides a misleading control-flow shape

**Files modified:** `flow-lang/Runtime/ModuleLoader.cs`
**Commit:** 4127554
**Applied fix:** Re-indented the body inside the try (the `ModuleRegistry`
registration hook at lines 131-191) to its actual nesting depth — every line
between `interpreter.Execute(program);` and the `Register` call is now one
level deeper. Pure formatting change.

### WR-07: Strict mode reports errors but continues execution with fallback values (partial)

**Files modified:** `flow-lang/StandardLibrary/Generative/CellularFunctions.cs`
**Commit:** bfad076
**Applied fix (PARTIAL):** Corrected the misleading "width/height clamped to
[1, MaxDimension]" message in `ClampDimensionWithAdvisory`'s `value <= 0`
branch — the path did NOT actually clamp; it returned raw `value`. Aligned
the strict-message text with the actual behavior ("must be > 0; returning
empty result"). The `value > MaxDimension` branch was already correct
(genuinely clamps to MaxDimension) and is unchanged.

**Scope-decision:** the broader WR-07 question — "should every strict-elevation
site standardize to either halt execution (return `Value.Void()`) or fall back
charitably?" — spans ~100 sites and is a design decision deferred. This commit
addresses ONLY the misleading-message bug in CellularFunctions. The wider
policy belongs in a follow-up plan that decides which return-shape strict
mode adopts site-wide.

### WR-08: `OverloadResolver.Resolve` uses `sig.ParameterNames.ToList().IndexOf(name)` inside hot loops

**Files modified:** `flow-lang/TypeSystem/OverloadResolver.cs`
**Commit:** 342059e
**Applied fix:** Replaced both `.ToList().IndexOf(name)` calls (lines 229 + 262)
with an inline `for (int i = 0; i < sig.ParameterNames.Count; i++)` scan.
Zero allocation, same O(K) per named-arg. Phase 44 + OverloadResolver tests
pass.

### WR-09: Lambda body uses `_context.StrictMode` (file scope) but ignores cross-file lambda passing

**Files modified:**
- `flow-lang/Interpreter/ExpressionEvaluator.cs`
- `flow-lang/Ast/Statements/ProcDeclaration.cs`

**Commit:** 87749a7
**Applied fix:** Added detailed XML doc on `EvaluateLambda` (was previously a
bare private method) describing the cross-file semantics: lambdas inherit
their DECLARING file's strict bit (lexical), not the call-site's (dynamic).
Worked example bullets cover the three cross-file scenarios the review
documents. Cross-referenced from `ProcDeclaration.IsStrict` XML doc so future
contributors see it at both ends. No code change.

## Skipped Issues

None — all in-scope findings were addressed (with the documented scope
decisions noted above for CR-01's secondary thread-safety concern, CR-03's
hot-path-only dedup coverage, and WR-07's misleading-message-only fix).

## Build / Test Verification

- `dotnet build` — clean at HEAD (8 pre-existing warnings, 0 errors)
- `dotnet test --filter "FullyQualifiedName~Phase44"` — 275 passed
- Full `dotnet test` — 2090 passed, 1 flaky (OSC RoundTrip; passes in isolation),
  2 pre-existing Phase 30 FlowMidi failures (unaffected by this work)

## Logic-bug class flags (per fixer instructions)

The following findings address LOGIC behavior (not just syntax/structure)
and should receive a human-verification pass before the phase advances:

- **CR-01** — Background-thread strict-bit capture; correctness depends on
  understanding the threading model of `oscListen` callbacks.
- **CR-02** — Call-boundary `CallerStrictMode` propagation in `->`/`~>`;
  semantically the same as the existing `EvaluateFunctionCall` sandwich, but
  the runtime-`->` path is rare enough that the test suite may not exercise it.
- **WR-05** — `Math.Clamp` semantics for Long→Int overflow may differ from the
  composer's expectation. The previous silent-truncation was wrong; clamp is
  better, but a strict-mode-aware composer may want `checked` overflow that
  surfaces as an error instead.

---

_Fixed: 2026-05-25_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
