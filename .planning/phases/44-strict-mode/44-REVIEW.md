---
phase: 44-strict-mode
reviewed: 2026-05-25T00:00:00Z
depth: standard
files_reviewed: 40
files_reviewed_list:
  - flow-interpreter/Repl.cs
  - flow-lang/Ast/Statements/ProcDeclaration.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang/Interpreter/ExpressionEvaluator.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/Lexing/PragmaRegistry.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Runtime/ModuleLoader.cs
  - flow-lang/StandardLibrary/Audio/DSP/GranularFunctions.cs
  - flow-lang/StandardLibrary/Audio/DSP/PitchShiftFunctions.cs
  - flow-lang/StandardLibrary/Audio/DSP/StretchEngine.cs
  - flow-lang/StandardLibrary/Audio/DSP/StretchFunctions.cs
  - flow-lang/StandardLibrary/Audio/InputFunctions.cs
  - flow-lang/StandardLibrary/Audio/MidiExport.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/StandardLibrary/ConversionFunctions.cs
  - flow-lang/StandardLibrary/Generative/CellularFunctions.cs
  - flow-lang/StandardLibrary/Generative/ChaosFunctions.cs
  - flow-lang/StandardLibrary/Generative/LsystemFunctions.cs
  - flow-lang/StandardLibrary/Generative/MarkovFunctions.cs
  - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
  - flow-lang/StandardLibrary/Improv/JamFunctions.cs
  - flow-lang/StandardLibrary/InternalFunctionRegistry.cs
  - flow-lang/StandardLibrary/Network/OscFunctions.cs
  - flow-lang/StandardLibrary/Notation/AbcImport.cs
  - flow-lang/StandardLibrary/Notation/AbcLexer.cs
  - flow-lang/StandardLibrary/Notation/MmlImport.cs
  - flow-lang/StandardLibrary/Notation/NotationIoBuiltins.cs
  - flow-lang/StandardLibrary/Patterns/PatternFunctions.cs
  - flow-lang/StandardLibrary/StdLib.cs
  - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
  - flow-lang/TypeSystem/FunctionSignature.cs
  - flow-lang/TypeSystem/OverloadResolver.cs
  - flow-lang/std.flow
findings:
  critical: 3
  warning: 9
  info: 6
  total: 18
status: issues_found
---

# Phase 44: Code Review Report

**Reviewed:** 2026-05-25
**Depth:** standard
**Files Reviewed:** 40
**Status:** issues_found

## Summary

Phase 44 ships strict mode for Flow as a file-scoped `enable strict;` pragma that
(a) disables OverloadResolver implicit-conversion clauses (Plan 44-03),
(b) elevates ~150 charitable `WarnOnce` advisories to `[strict]` errors via a
per-dispatch `CallerStrictMode` snapshot (Plans 44-05..44-08), and
(c) tightens `print`/`if`/`not`/`and`/`or`/comparisons to require their canonical
types under strict (Plans 44-08..44-09). The propagation discipline
(`ExecutionContext.StrictMode` push/pop in `ExecuteUserFunctionWithCaptures` +
`CallerStrictMode` snapshot in `EvaluateFunctionCall`) is sound for the
synchronous interpreter loop and is paired with try/finally restore at every
mutation site I audited.

The most serious defects fall outside the synchronous interpreter contract:
the OSC listener (Phase 38) reads `context.CallerStrictMode` from a long-lived
background `Task.Run` thread where the value is unpredictable — what should be
"the strict bit at the time `oscListen` was called" is instead "whatever the
foreground thread happens to be doing now"; the `~>` and runtime `->` flow
operators bypass the new `CallerStrictMode` save/restore sandwich entirely;
strict-elevated `WarnOnce` sites in hot paths (`micBuffer`, `granular`, every
combinator) lose their per-process dedup, flooding the `ErrorReporter` on every
call. Several `WarnOnce` → `[strict]` rewrites were missed (e.g.
`every n <= 0`), and `ConversionFunctions.RegisterReverseExtractors` registers
all four primitive→primitive cross-casts unconditionally — including the
no-op identity casts that already worked under the existing widening rules.

## Critical Issues

### CR-01: OSC listener reads `CallerStrictMode` from a background thread where the value is stale/torn

**File:** `flow-lang/StandardLibrary/Network/OscFunctions.cs:386-405,415-425,458-481,495-507,529-552`
**Issue:** `StartListener` captures `context` into a `Task.Run` body that runs
for the lifetime of the listener (potentially seconds or minutes), then reads
`context.CallerStrictMode` at error-reporting sites (`connect failed`,
`bundle depth > 8`, etc.) on that background thread. `CallerStrictMode` is a
per-dispatch snapshot owned by the synchronous evaluator loop —
`ExpressionEvaluator.EvaluateFunctionCall` writes it inside a `try/finally`
that restores the previous value as soon as the foreground call returns
(lines 437-449). By the time an OSC packet arrives, the foreground thread has
unwound that sandwich many times over; whatever value the background read sees
is whichever call happens to be executing in the foreground at that instant,
or 0 if the foreground is idle. The intent (per the XML doc at lines 350-358)
is "treat listener-bind failure as a strict event so composer can react", but
"the composer's strict bit at oscListen call time" is NOT what the code
reads.
Same problem in `DispatchPacket` (line 468), `DispatchBundleContents`
(line 514 via recursion), `InvokeHandlerWithRateLimit` (line 527), and the
`Task.Delay(...).ContinueWith` continuation at line 495 — every error/advisory
on the listener task reads a value that was last written by some unrelated
foreground call.

Independently, concurrent access to `ExecutionContext` from the background
listener is racy on more than just `CallerStrictMode`: `InvokeHandler` at
line 575 calls `context.Invoker!.ExecuteUserFunctionWithCaptures`, which
mutates `StrictMode`, `_callStack`, and `_overloadResolveCache` — all unprotected
by locks. If the composer's `live` block fires concurrently with an inbound
OSC packet, two threads will be inside `_overloadResolveCache.Clear()` /
`Dictionary.Add()` / `_callStack.Push(...)` at the same time. Concurrent
`Dictionary` mutation is documented as undefined behavior; concurrent
`Stack<T>` mutation will corrupt internal indices.
**Fix:**
```csharp
// In StartListener — capture the strict bit at listen-time as an immutable local.
bool listenerStrict = context.CallerStrictMode;
// ...
if (listenerStrict)
{
    context.ErrorReporter.ReportError(
        $"[strict] [osc] handler exception — bind failed on port {port}: {ex.Message}",
        /* location captured at listen time */);
}
```
Capture `CallerStrictMode` (and `CurrentCallSite` for diagnostics) into locals
BEFORE the `Task.Run`, and pass the captured locals into every subsequent
helper that lives on the background thread (`DispatchPacket`, `DispatchBundleContents`,
`InvokeHandlerWithRateLimit`). For the broader thread-safety problem, either
(a) marshal all `InvokeHandler` invocations back onto the foreground thread
via a thread-safe queue drained by the next live-block evaluation, or
(b) wrap every `ExecutionContext` mutation site behind a lock (heavier but
preserves the "handler runs as if inline" composer mental model). The current
design quietly invokes Flow procs from a non-evaluator thread with no synchronization.

### CR-02: `EvaluateFlowExpression` and `EvaluateTupleUnpackFlow` skip the `CallerStrictMode` save/restore sandwich

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:550-551,677-689`
**Issue:** Every other call-dispatch site in `ExpressionEvaluator` snapshots
`CallerStrictMode` around the dispatch (lines 249-261 qualified internal,
268-278 qualified user, 437-450 unqualified internal, 461-472 unqualified user).
`EvaluateFlowExpression` at line 540-551 and `EvaluateTupleUnpackFlow` at
lines 652-690 BOTH invoke either `overload.Implementation!(args)` or
`_invoker.ExecuteUserFunctionWithCaptures(...)` without writing
`CallerStrictMode = _context.StrictMode` first. This is exactly the
"never mutate without paired restore" Anti-Pattern 1 inversion the rest of
the file is careful to avoid — except here the bug is missing the SET, not
missing the RESTORE, with the result that strict-aware builtins invoked via
the runtime `->` form (e.g. `f -> g` where `g` is a function variable) or
via `tup ~> fn` read whatever stale `CallerStrictMode` the previous
foreground call left behind. While `->` is documented as a parse-time
transform, `EvaluateFlowExpression` is the runtime fall-through used when
the RHS is a variable resolving to a `FunctionOverload` (line 540 guard),
and `~>` is always evaluated at runtime.
**Fix:**
```csharp
// EvaluateFlowExpression line 549-551
var args = new List<Value> { leftVal };
var prevCallerStrict = _context.CallerStrictMode;
_context.CallerStrictMode = _context.StrictMode;
try
{
    if (overload.IsInternal) return overload.Implementation!(args);
    return _invoker.ExecuteUserFunctionWithCaptures(overload.Declaration!, args, overload.CapturedVariables);
}
finally
{
    _context.CallerStrictMode = prevCallerStrict;
}
```
Apply the same sandwich at lines 676-680 (Tuple LHS unpack branch) and
lines 685-689 (non-tuple LHS fallthrough). Without these, strict-aware
builtins called via `->` or `~>` see arbitrary `CallerStrictMode` values.

### CR-03: Strict-elevated `WarnOnce` sites bypass per-process dedup, flooding `ErrorReporter`

**File:** `flow-lang/StandardLibrary/Audio/InputFunctions.cs:138-149,184-196`; `flow-lang/StandardLibrary/Audio/DSP/GranularFunctions.cs:155-162`; `flow-lang/StandardLibrary/Audio/DSP/PitchShiftFunctions.cs:197-204`; `flow-lang/StandardLibrary/Audio/DSP/StretchFunctions.cs:166-173`; and most strict-elevation sites across `Patterns/`, `Generative/`, `Improv/`, `Notation/`, and `Sfz/`
**Issue:** The non-strict path emits `RenderingDiagnostics.WarnOnce(sentinelKey, body)`,
which is per-process-deduped by sentinel — composers iterating in a live
session see each advisory exactly once. The strict path replaces that with
`ctx.ErrorReporter.ReportError(...)` without any dedup. Each call appends a
fresh entry to `ErrorReporter.Errors`. Hot-loop call sites — `(micBuffer 0.05s)`
inside a `live` block (every 50ms), every grain in `(granular buf grain=1ms ...)`,
every bar processed by `(every 4 cb seq)` with a degenerate sequence —
generate one strict error per call. Within a few seconds the reporter holds
thousands of identical errors; the formatted output buries any actual problem
the composer was looking for, and the reporter never resets between top-level
script invocations the way `RenderingDiagnostics._emitted` does (which has an
explicit `ResetForTesting` hook + per-engine reset in `RestoreState` line 1224).

Concretely in `InputFunctions.MicBuffer` (lines 138-149): the
`[strict] [audio-in] mic stream attenuated -20 dB on open` error fires
on every single `micBuffer` call in strict mode. The non-strict sibling at
line 146 emits ONE `[audio-in]` advisory per process via the
`"audio-in-attenuate:open"` sentinel.

In `PatternFunctions` (every `IsEmptySeqAdvisory` callsite + similar): a
strict combinator applied to an empty sequence inside a higher-order loop
(e.g. `each chunks (fn s => (every 4 cb s))` where some chunk is empty)
records one strict error per iteration.
**Fix:** Either (a) introduce a parallel per-strict-key dedup set on
`ExecutionContext` keyed by sentinel string, and emit each strict error at
most once per process; or (b) keep the `WarnOnce(key, ...)` call in BOTH
modes and additionally write to `ErrorReporter` via a different
single-entry-per-process gate. The simplest patch matching the existing
`SfzDiagnostics` pattern is:
```csharp
// In ExecutionContext:
public HashSet<string> StrictAdvisoryDedup { get; } = new();

// At every strict-elevation site, before ReportError:
if (!ctx.StrictAdvisoryDedup.Add(sentinelKey)) return; // already reported this process
ctx.ErrorReporter.ReportError($"[strict] ...", ctx.CurrentCallSite);
```
Without this, strict mode is unusable inside any iterative composer workflow
(REPL `live` block, `flow watch` hot reload, render-and-tweak loops) once
the first degenerate call fires.

## Warnings

### WR-01: `every n <= 0` misses the strict-mode elevation pattern applied to its siblings

**File:** `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs:217-223`
**Issue:** Every other `WarnOnce` site in `PatternFunctions.cs` is preceded
by an `if (ctx.CallerStrictMode) { ErrorReporter.ReportError(...); return ...; }`
elevation branch (lines 138-144 in `IsEmptySeqAdvisory`, 324-329 in `fast/slow`,
408-413 in `chunk n<=0`, 449-454 in `chunk lambda non-Sequence`, 503-508 in
`phase NaN`, 517-522 in `phase empty`, etc. — 16 sites total). The `every`
combinator's `n <= 0` charitable branch at 217-223 was not converted:
```csharp
if (n <= 0)
{
    RenderingDiagnostics.WarnOnce(
        $"every:invalid-n:{ctx.CurrentCallSite}",
        $"[every] n must be > 0 (got {n}) at {ctx.CurrentCallSite}; sequence unchanged");
    return Value.Sequence(seq);
}
```
Strict composers calling `(every 0 cb seq)` see a `WarnOnce` advisory
instead of the `[strict] [every] n must be > 0` error the rest of the
manifest implies. This violates the "every Axis B advisory site is elevated"
contract documented in `44-PATTERNS.md` and the strict-error-manifest.
**Fix:**
```csharp
if (n <= 0)
{
    if (ctx.CallerStrictMode)
    {
        ctx.ErrorReporter.ReportError(
            $"[strict] [every] n must be > 0 (got {n}) at {ctx.CurrentCallSite}",
            ctx.CurrentCallSite);
        return Value.Sequence(seq);
    }
    RenderingDiagnostics.WarnOnce(
        $"every:invalid-n:{ctx.CurrentCallSite}",
        $"[every] n must be > 0 (got {n}) at {ctx.CurrentCallSite}; sequence unchanged");
    return Value.Sequence(seq);
}
```
Audit the strict-error-manifest.csv to find any other missed sites
— a string-grep for `RenderingDiagnostics.WarnOnce` that lacks an immediately-preceding
`ctx.CallerStrictMode` branch will expose them.

### WR-02: `Repl.HandleCommand` uses culture-sensitive `ToLower()` for the dispatch switch

**File:** `flow-interpreter/Repl.cs:252`
**Issue:** `command.ToLower()` uses `CultureInfo.CurrentCulture`. In the
Turkish locale (`tr-TR`), uppercase `'I'` lowercases to dotless `'ı'`
(U+0131), so `:HELP` → `:help` works on `en-US` but produces `:heLp`-style
mismatches when characters outside ASCII enter the picture, and `:HELP foo`
which previously matched the prefix check at lines 241-242 (which uses
`OrdinalIgnoreCase` — the right call) won't actually dispatch through the
switch. The strict-mode commands `:strict on`/`:strict off` don't contain
'I' so they happen to be safe today, but the inconsistency is a latent
bug. Other consumers (`flow-lang` itself) standardize on
`StringComparison.Ordinal` for command/keyword equality (e.g.
`PragmaRegistry.cs:28` uses `StringComparer.Ordinal`).
**Fix:** Replace `command.ToLower()` with `command.ToLowerInvariant()`
(or migrate the switch to explicit `string.Equals(command, ":strict on",
StringComparison.OrdinalIgnoreCase)` checks). Test seam at line 287
inherits the bug, so `ReplStrictMetaCommandTests` running under a non-ASCII
locale would surface this.

### WR-03: `OscFunctions.SendOscPacket` crashes on DNS-empty hostname

**File:** `flow-lang/StandardLibrary/Network/OscFunctions.cs:317-318`
**Issue:**
```csharp
var entry = Dns.GetHostEntry(host);
addr = entry.AddressList[0];
```
`Dns.GetHostEntry` can return an `IPHostEntry` with an empty
`AddressList` (e.g., a host name with only IPv6 records when IPv6 is
disabled, or a poorly-configured DNS server returning success with no A
records). The `[0]` access throws `IndexOutOfRangeException` which the
surrounding try-catch at line 320 catches, but only the original
`Dns.GetHostEntry` throw is anticipated — the catch then surfaces the
exception text into a Flow-level `InvalidOperationException` with the
generic "could not resolve host" message. The error message at line 322
will mention "Index was outside the bounds of the array" instead of the
intended "no IP addresses for host". This is misleading for composers
debugging a DNS configuration issue.
**Fix:**
```csharp
var entry = Dns.GetHostEntry(host);
if (entry.AddressList.Length == 0)
    throw new InvalidOperationException(
        $"[osc] oscSend: hostname '{host}' resolved but returned no IP addresses");
addr = entry.AddressList[0];
```

### WR-04: Strict-mode advisory in `MicBuffer` reports BEFORE the zero-duration short-circuit

**File:** `flow-lang/StandardLibrary/Audio/InputFunctions.cs:138-155`
**Issue:** The `[strict] [audio-in] mic stream attenuated -20 dB on open`
error fires unconditionally at line 140 before the `if (durationSeconds <= 0.0)`
zero-duration short-circuit at line 151. So even `(micBuffer 0s)` — which the
comment at line 153 calls "a composer no-op" — fires the strict
attenuation error. In non-strict mode this is fine (WarnOnce dedupes to one
emission per process), but in strict mode (CR-03 compounding), it adds one
error per noop call.
**Fix:** Move the advisory below the zero-duration short-circuit so it
only fires when the device is actually opened:
```csharp
if (durationSeconds <= 0.0)
    return new AudioBuffer(0, DefaultChannels, TargetSampleRate);

// Now emit the attenuation advisory.
if (ctx is not null && ctx.CallerStrictMode) { /* ... */ }
else RenderingDiagnostics.WarnOnce(...);
```

### WR-05: `ConversionFunctions` registers redundant identity-cast overloads for primitive numeric types

**File:** `flow-lang/StandardLibrary/ConversionFunctions.cs:229-255`
**Issue:** The "Phase 44 Plan 44-09 Task 2 — primitive numeric cross-casts"
block registers `(double Int)`, `(double Long)`, `(double Float)`,
`(double Double)`, `(float Int)`, ..., 4×4 = 16 overloads. Four of these are
IDENTITY casts that should be unnecessary even under strict — e.g.
`(double 1.0)` where the arg is already Double has no strict-mode
ambiguity. The other 12 are the genuine strict-mode escape hatches. The
identity overloads work fine, but their registration:
- Adds 4 entries to the function table for no benefit
- Risks `Ambiguous overload` when composed with the Phase 26 widening
  rules under strict (the existing widening path is suppressed under
  strict, but ANY other Double-source overload would now have an extra
  competitor)
- The `(int Long)` overload at line 248-250 silently truncates Long values
  outside Int range — `(int 5000000000L)` produces `Int.MinValue` via the
  unchecked `(int)(long)v.As<long>()` cast at line 250. The doc at lines
  220-228 promises "floor matches StdLib.DoubleToInt" but says nothing
  about Long-overflow. The existing `StdLib.DoubleToInt` only handles
  the Double case.
**Fix:** Drop the 4 identity registrations (`(double Double)`,
`(float Float)`, `(int Int)`, `(long Long)`) — they're no-ops. For `(int Long)`,
either clamp to `[int.MinValue, int.MaxValue]` (matching `Math.Clamp` semantics)
or use `checked((int)...)` and let the OverflowException surface as a
composer-visible error. The current silent overflow is worse than either
alternative.

### WR-06: ModuleLoader's `prevStrict` save/restore is correct but the indentation hides a misleading control-flow shape

**File:** `flow-lang/Runtime/ModuleLoader.cs:127-201`
**Issue:** The try/finally that restores `StrictMode` is structurally correct:
the `try` opens at line 127, the `finally` opens at line 193, the closing
brace of the try-body is at line 192. But the body inside the try (lines
131-191) is indented at the wrong level — it visually appears to be OUTSIDE
the try block. Reading the code top-down, the ModuleRegistry registration
hook at lines 145-191 looks like a sibling of the try/finally, not enclosed
by it. A reader doing a quick visual scan to verify "does the ModuleRegistry
hook also get its strict bit restored on throw?" sees the wrong answer; only
a careful brace match confirms enclosure. The control flow is correct; the
code is misleading.
**Fix:** Re-indent the body inside the try to its actual depth — every line
between 131 and 191 should be one level deeper:
```csharp
try
{
    interpreter.Execute(program);

    if (program.Statements.Count > 0
        && program.Statements[0] is ModuleDeclarationStatement modDecl)
    {
        // ... nested body indented at the correct depth ...
    }
}
finally
{
    context.StrictMode = prevStrict;
}
```
Pure formatting change — no semantic effect, but materially reduces the
risk that a future edit misplaces a statement outside the try by accident.

### WR-07: Strict mode reports errors but continues execution with fallback values, producing surprising downstream behavior

**File:** `flow-lang/StandardLibrary/Audio/DSP/GranularFunctions.cs:154-167`; `flow-lang/StandardLibrary/Audio/DSP/PitchShiftFunctions.cs:194-209`; `flow-lang/StandardLibrary/Audio/DSP/StretchFunctions.cs:163-178`; `flow-lang/StandardLibrary/Generative/CellularFunctions.cs:396-413`
**Issue:** Several strict-elevation sites report the `[strict]` error AND
continue executing with a fallback value, e.g.
```csharp
// GranularFunctions.FallbackToHann
if (ctx.CallerStrictMode)
{
    ctx.ErrorReporter.ReportError($"[strict] [granular] unknown windowing symbol '#{sym}' — falling back to #hann. ...");
    return WindowKind.Hann;   // execution continues!
}
```
Because `ErrorReporter.ReportError` only accumulates — it does not throw —
the granular DSP runs on the fallback windowing and produces audio output.
The composer sees an error AND the wrong audio. Worse, in
`CellularFunctions.ClampDimensionWithAdvisory` line 406, the strict path
returns the **raw** (potentially negative/zero) `value` rather than a
clamped-to-safe-range value; callers happen to have an extra
`if (width <= 0)` guard that catches this, but that's defense by accident.
The strict-error-manifest pattern needs to decide: either strict halts execution
(via `throw` or `Value.Void()` short-circuit), or strict matches non-strict
behavior with a noisier diagnostic. The current mix is the worst of both —
the composer can't tell whether the operation succeeded.
**Fix:** Standardize: either every strict-elevation site returns
`Value.Void()` (the existing `TransformFunctions.CrescendoStrict` pattern at
line 275) and the caller bails out, or every site falls back charitably and
the strict error is purely informational. The mixed contract is the bug.

### WR-08: `OverloadResolver.Resolve` uses `sig.ParameterNames.ToList().IndexOf(name)` inside hot loops

**File:** `flow-lang/TypeSystem/OverloadResolver.cs:229,262`
**Issue:**
```csharp
foreach (var name in namedArgTypes.Keys)
{
    int slot = sig.ParameterNames.ToList().IndexOf(name);
    // ...
}
```
`ParameterNames` is already an `IReadOnlyList<string>` — `.ToList()` allocates
a NEW `List<string>` PER named-arg, then `IndexOf` scans it linearly. The
same pattern appears at line 262 in the re-ordering pass. The named-arg
call surface is on a hot path (every Phase 36-style call uses it). For a
signature with K parameter names and N named args, this is O(K×N×N)
allocations — every named arg allocates a fresh list. Two `IndexOf` passes
per named-arg key means triple-counting at minimum. The class doc comment
in `ExecutionContext.cs` at lines 71-82 documents that named-arg dispatch
intentionally bypasses the overload cache — so this is the only resolution
path for named-arg calls, and it allocates on every dispatch.

This is out-of-scope for the "performance not a v1 goal" CLAUDE.md
note (`Non-Goals` line 19), BUT the allocation churn forces GC pressure
inside the audio rendering loop where it can produce audible glitches,
violating the `realtime audio = efficient buffer ops with no GC pressure
in hot paths` constraint at CLAUDE.md line 285.
**Fix:** Drop the `.ToList()` and write the linear scan directly:
```csharp
int slot = -1;
for (int i = 0; i < sig.ParameterNames.Count; i++)
{
    if (sig.ParameterNames[i] == name) { slot = i; break; }
}
```
Two scans (lines 229, 262) can fold into a single `Dictionary<string, int>`
built once per signature at registration time, but the inline loop is
already zero-allocation and matches the rest of the file's posture.

### WR-09: Lambda body uses `_context.StrictMode` (file scope) but ignores cross-file lambda passing

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:725-753`
**Issue:** `EvaluateLambda` captures `IsStrict: _context.StrictMode` onto the
synthesized `ProcDeclaration` at line 741. This matches the file-scope
contract for lambdas DECLARED in that file. But the doc at lines 732-738
claims it preserves "the surrounding lexical scope at creation time" — that's
WHAT it does, but the implication that this gives lexical strict-scope across
module boundaries is misleading. A strict file's lambda passed to a
non-strict module's higher-order function (e.g. `each`, `map`) executes
with `_context.StrictMode = true` during the body, but per Plan 44-08's
charitable design, those same builtins do not re-read `StrictMode` mid-body —
they look at `CallerStrictMode`, which during the lambda body is whatever
the IMMEDIATE caller (the non-strict higher-order builtin's frame) set, not
the strict file's bit.

So a strict-file lambda invoking `(print 5)` inside a non-strict `map` will
NOT raise the `[strict] (print) requires String` error: `EvaluateFunctionCall`
sees `_context.StrictMode == true` (set by `ExecuteUserFunctionWithCaptures`
for the lambda body), AND it snapshots `CallerStrictMode = _context.StrictMode`
at line 440 — so `print` reads `CallerStrictMode == true` and DOES raise.
Actually re-tracing: this works correctly. But the doc claim that lambdas
"inherit the strict bit of the surrounding lexical scope" obscures the
subtlety. Composer mental model breaks for "lambda created in strict file,
held inside non-strict library state via `(registerStyle)`, fired later".
The lambda body executes with strict semantics — surprising if you handed
your lambda to a charitable library.
**Fix:** Document the cross-file semantics explicitly in the XML doc on
`EvaluateLambda` and on `ProcDeclaration.IsStrict`. Consider whether
"pre-traction breaking-change latitude" (memory `project_pre_public_no_legacy_burden`)
justifies revisiting whether lambdas should be call-site scoped instead.

## Info

### IN-01: `ScalaBuiltins.Register` (legacy 1-arg) is dead code in production

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs:36-44`
**Issue:** Comment says "Tests / harnesses that don't have an
ExecutionContext call this overload", but `FlowEngine.cs:132` only calls
`RegisterContextDependent`. Confirmed via grep: zero production call sites.
Tests should call `RegisterContextDependent` with a test `ExecutionContext`
to match production behavior.
**Fix:** Either delete `Register(InternalFunctionRegistry)` or add a
visible `[Obsolete("Phase 44 — use RegisterContextDependent(registry, context)")]`
attribute so test code that drifts gets a compile warning.

### IN-02: `PrintAny` falls through to `Value.ToString()` for music types — likely produces wrong format for Sequence/Bar/Song

**File:** `flow-lang/StandardLibrary/StdLib.cs:644-651`
**Issue:** `AutoStr` covers ~15 primitive + music types explicitly, then
falls through to `v.ToString()` (line 650) for Sequence/Bar/Chord/Song/etc.
For a Sequence with 100 bars, `Value.ToString()` may produce a multi-line
or extremely long string that is unhelpful in the REPL. Non-strict
`(print mySong)` (which routes here for any Song-typed arg under Plan 44-08)
will dump the raw runtime representation. Composers expect the equivalent
of `inspect` (Phase 38 visualization), not the raw `ToString()`.
**Fix:** Either explicitly dispatch Sequence/Song/Chord to their existing
notation helpers (`(str song)` / `(notation seq)`), OR emit a warning that
`(print Song)` is calling-fallthrough so the composer knows to wrap with
`(notation ...)`. Out of strict-mode scope, but worth flagging for v1.6.

### IN-03: `InternalFunctionRegistry.TryGetImplementation` two-pass lookup adds linear-time overhead but lacks dedup of the wildcard pass

**File:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs:30-67`
**Issue:** Plan 44-08 added a "prefer exact signature match" first pass to
disambiguate the Void-wildcard `print(Void)` overload from the existing
`print(String)`. The two-pass scan is correct, but if no exact match exists
and the wildcard pass picks the FIRST wildcard-compatible match, that's
the wildcard-fallback order, NOT the strict-aware ordering. Result: when
a registry holds both `print(String)` and `print(Void)`, calling
`(print 5)` goes to Pass 1 (no exact match, since Int != String and Int !=
Void), then Pass 2 (wildcard) which finds `print(String)` first via
`TypesEqual` returning true on the Void-wildcard sub-case at line 158-160
... wait, no, Int.Equals(String) is false, Void.Equals(Int) is false, and
the wildcard sub-case at line 158-160 only triggers when one side IS
VoidType. Int vs String is just false. So Pass 2 actually picks `print(Void)`
correctly.

This is correct, just non-obvious. The audit value here is "the two-pass
discipline doesn't add ambiguity for the documented strict-mode cases" —
but a future contributor adding a third Void-overload (e.g. `print(Void)`
with different impl) would silently bind to the FIRST registered, hidden
by the wildcard fall-through.
**Fix:** Add an XML-doc warning on the Pass 2 loop that "later
wildcard-matching registrations are SILENTLY shadowed" so future
contributors expand the prefer-exact-pass when adding new strict-aware
Void overloads.

### IN-04: Many `[strict]` error messages include `at {ctx.CurrentCallSite}` even when `ctx.CurrentCallSite` is also passed as the location arg

**File:** `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs:225-228,242-245,287-290`; `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` (multiple sites); `flow-lang/StandardLibrary/Generative/ChaosFunctions.cs:228-232,302-305`
**Issue:**
```csharp
ctx.ErrorReporter.ReportError(
    $"[strict] [markov] features unsupported — unrecognised features tuple at {ctx.CurrentCallSite}",
    ctx.CurrentCallSite);
```
The `at {ctx.CurrentCallSite}` interpolation inside the message duplicates
the location that's already the second argument of `ReportError`. The
formatted diagnostic output then shows the location TWICE. Phase 35
LANG-04's Rust-style renderer (`Program.FormatErrorsForEmit`) prepends the
location once; the inline `at ...` text then repeats. Cosmetic but
ubiquitous — affects >40 strict messages.
**Fix:** Drop the `at {ctx.CurrentCallSite}` interpolation from the message
body; rely on the second-arg location for rendering. This also avoids
serializing the `SourceLocation.ToString()` inside the message string,
which makes the error text dependent on the source location format —
a future SourceLocation format change would break every snapshot test.

### IN-05: `_overloadResolveCache` correctness contract documents three bypass gates but tests-only-state can leak

**File:** `flow-lang/Runtime/ExecutionContext.cs:84-148,1167-1172`
**Issue:** The cache key includes `(Name, ArgTypes[], StrictMode)` — strict
correctness is preserved. The `InvalidateOverloadCache` is called on
`DeclareFunction` (mutation chokepoint) and defensively in `RestoreState`.
But there's no invalidation when `context.StrictMode` flips mid-execution
via `:strict on`/`:strict off` REPL meta-commands at `Repl.cs:273-279`. The
REPL writes `_engine.Context.StrictMode = on` directly without invalidating
the cache. While the cache key already encodes `StrictMode`, the
INVARIANT that "same name + arg types under strict resolve to a strict
overload" needs the strict bit READ AT RESOLVE TIME to match what was
written into the cache key. The cache stores `(name, argTypes, strict=true)`
and `(name, argTypes, strict=false)` as separate entries — so a strict-then-
non-strict resolve sequence is correct.

Where this actually leaks: between `RestoreState` (which calls
`InvalidateOverloadCache` defensively) and the next user call, nothing
prevents stale entries from prior phase tests. The defensive invalidation
is correct, but the contract document at lines 72-83 says "FORWARD RISK
RESOLVED" — actually the test-isolation cache survives across the
`Snapshot/Restore` boundary only because of the defensive clear. The
"FORWARD RISK RESOLVED" claim doesn't acknowledge the test path.
**Fix:** Documentation-only: extend the XML doc on `OverloadCacheKey` to
note that the strict bit is part of correctness only because the test
boundary defensively clears (line 1172) — without that explicit clear,
a strict test's pre-warmed cache would survive into a non-strict test's
resolve and the strict-bit discriminator would correctly produce a miss
... actually no, the discriminator is sound. The doc is fine. Lower
priority but worth a comment.

### IN-06: `Parser.ParseProcDeclaration` captures `IsStrict` for both top-level and nested proc decls — verify body procs work

**File:** `flow-lang/Parsing/Parser.cs:384-394`
**Issue:** `ProcDeclaration` is created with `IsStrict: _pragmaSet?.Has("strict")`.
The `_pragmaSet` field is the PER-FILE pragma set assigned in the Parser ctor
at line 45. Nested `proc` declarations (proc declared inside another proc's
body) share the same `_pragmaSet` instance — which is correct because Flow
pragmas are file-scope. A nested proc declared inside a strict file inherits
`IsStrict=true`. Likewise a proc declared inside a `live` block. Good.

But: a proc declared via a TextWriter-injected REPL line that DOESN'T contain
`enable strict;` runs through a FRESH Parser with a fresh PragmaScanner —
the REPL's `lineToExecute = _sessionStrict ? "enable strict;\n" + input : input`
trick (Repl.cs:103) injects the pragma at the front of every line so the
PragmaScanner sees it. But REPL multi-line input via `\` continuation
(line 195-221) joins lines AFTER the sticky check — the strict prefix is
correctly the first thing. Looks OK; just verify multi-line REPL with
`:strict on` is tested.
**Fix:** Add a multi-line REPL fact to `ReplStrictMetaCommandTests` that
covers (1) `:strict on` + `\`-continued multi-line `proc` decl, (2) the
proc's `IsStrict` should be `true`. The current Plan 44-10 test list per
44-10-SUMMARY.md may or may not cover this.

---

_Reviewed: 2026-05-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
