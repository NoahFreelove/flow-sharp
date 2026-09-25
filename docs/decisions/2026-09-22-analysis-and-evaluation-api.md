# Public analysis and evaluation APIs

Status: implemented for restructuring Phase 4 (2026-09-24), with the conservative
analysis scope and compatibility adapters documented below. The Phase 1 sketch
remains the longer-term direction, not a promise of complete static type inference.

## Context

At the start of restructuring, the embedding surface was `FlowEngine.Execute(source, fileName) -> bool` plus
`ErrorReporter`, `SourceMap`, `GetLastExpressionResult` and
`ExecuteScriptAndGetResult`. `flow check` executed the program with console
suppression rather than analyzing it. Most checking happens at evaluation time. The
[language contract](../../contracts/language/README.md#what-analysis-can-prove)
originally reported only parse errors before execution; other failures
surfaced only on the executed path. Since the Phase 1 semantic fixes, internal
failures during evaluation are reported at a source location and never end the
program; `FlowEngine`'s location-less catch-all remains only as a last resort.

## Decision

Expose separate operations (roadmap 6.2), each returning data rather than
writing to the console:

```text
Parse(source, sourceId, syntaxOptions)          -> SyntaxTree + Diagnostics
Analyze(tree, catalog, analysisOptions)         -> AnalysisResult (never executes)
Evaluate(tree, session, cancellation)           -> EvaluationResult
CompileMusic(value, musicOptions)               -> CompositionSnapshot      (music profile)
PrepareRender / RenderOffline(...)              -> RenderResult             (music profile)
```

1. **One diagnostic model.** Every diagnostic has a stable code, severity, message,
   primary span (source id, line, column, length) and optional notes or
   suggestions. The legacy `FlowError` list and the rich `FlowDiagnostic` list merge
   into it (`ErrorReporter.ErrorCount`/`FormatAll` already present them together).
   Formatting (color, Rust-style boxes) is a host concern driven by the host's
   terminal policy, including `NO_COLOR` (`ErrorReporter.ShouldUseColor`).
2. **`EvaluationResult` distinguishes outcomes:** success (with the last value and
   exports), invalid program (diagnostics), cancelled, budget exhausted, missing
   capability or module, and host failure. Internal exceptions become host
   failures with debugging detail. They are never reported as user diagnostics at
   `0:0`.
3. **Analysis promises only what it can show.** It resolves declarations, lexical
   names, imports (from descriptors or parsed source), known call arity and argument
   names, and provable type incompatibilities. Anything dynamic is marked unknown,
   not assumed correct. Analysis never runs procedures or module bodies.
4. **`flow check` becomes analysis-only.** An explicit execution check, if kept, gets
   a different name.
5. **Output is an injected sink** (see the session lifetime decision). `Evaluate`
   never touches `Console`.
6. **Cancellation reaches evaluation.** It is checked at loop back-edges, calls,
   long builtins, module loads and render chunks. Charitable fallbacks never swallow
   cancellation or budget errors.

## Implemented in Phase 2

`FlowEngine.Evaluate(source, fileName, EvaluationOptions)` returns an
`EvaluationResult` with outcome `Succeeded`, `Failed`, `Cancelled` or `TimedOut`, the
errors and rich diagnostics, the last value, and the elapsed time.
`EvaluationOptions` carries the host's `CancellationToken` and a wall-clock
`TimeLimit`. Output goes to the engine's `EngineOptions.Output`/`Diagnostics` sinks.
Internal failures during evaluation are located diagnostics (Phase 1 semantic
fixes). `HostFailure` and coded diagnostics were added in Phase 4 (below).

## Implemented in Phase 4

`LanguageAnalysis.Parse(source, sourceId)` returns `SyntaxTree`, preserving tokens,
partial AST, legacy errors and coded diagnostics. `Describe(tree)` exposes module
imports and procedure signatures with documentation and source spans, without
implementation delegates. `Analyze(tree, IModuleSourceProvider, AnalysisOptions)`
follows top-level imports with a module budget and cancellation; it never calls
the interpreter. The filesystem provider uses explicit roots and search paths.

`flow check` uses this path and reports its scope in the success message. Stable
codes are `flow.syntax.pragma`, `flow.syntax.lex`, `flow.syntax.parse` and
`flow.module.read`, `flow.module.missing`, `flow.module.limit`. Imported diagnostics
retain their source identities. The LSP parse adapter and documentation collector
share `Parse`; LSP syntax diagnostics retain codes and spans. Completion/hover
metadata comes from parsed standard-library declarations, with no dummy runtime
registration or audio construction.

Shared analysis also emits conservative warnings for names without a visible
source declaration (`flow.binding.unknown`), incompatible argument counts/names
on source procedures (`flow.call.arguments`), and incompatible primitive literal
argument types (`flow.call.types`). It tracks procedure, lambda, block and pattern
scopes, imported top-level declarations and flow-operator input arguments. It
allows numeric widening according to the declaring file's strictness.

These are warnings because declaration order, deferred/unreachable code and dynamic
bindings can affect execution. `Success` means no syntax/import errors; warnings
are reported separately. Internal procedures can have host overloads/defaults not
expressed in `.flow` declarations, so their calls are not statically rejected.
Argument values, non-literal inference, named-varargs dispatch, qualified/member
access, musical expressions/section expansion, nested/conditional imports, dynamic
exports, capabilities and termination remain runtime checks. Imported module bodies
are parsed and described but not independently bound against an invented isolated
scope: imports execute in the caller's scope. These are explicit limits of this
phase, not evidence that arbitrary programs are statically valid.

The LSP runs this same analysis with an overlay of open document text. Completion
and hover use reachable module descriptors; stdlib visibility follows actual
imports and retains module ownership. Imported syntax errors appear on the use
statement as `flow.module.invalid` with related source locations; the original
code is retained in the message. The old parse/index entry points remain adapters.

`FlowEngine.Evaluate(SyntaxTree, EvaluationOptions)` evaluates a previously parsed
tree in the engine's explicit session, refusing erroneous partial syntax.
`EvaluationResult.CodedDiagnostics` combines legacy and rich diagnostics without
losing existing rich spans, notes or suggestions. Source evaluation uses the same
syntax-stage codes and `flow.evaluation` for runtime diagnostics; cancellation and
timeout have `flow.evaluation.cancelled` / `flow.evaluation.timeout`. Unexpected
host exceptions have outcome `HostFailure`, code `flow.host.failure`, a located
summary and host-only `HostException`. The compatibility `Failed` outcome still
covers invalid programs and missing modules/capabilities; exhaustive per-runtime-
error codes and whole-program inference are future API refinements.

CLI/REPL/watch adapters explicitly provide output, diagnostics and config for each
engine session. The browser uses fresh per-run sessions with default config and
maps coded diagnostics to its frozen `RunError` fields and kind values. It uses the
diagnostic source identity for snippets. The browser JS API does not expose a new
analysis operation in this phase; the C# API is available to tooling without
constructing an evaluation session. The committed website bundle is not refreshed.

## Compatibility

`FlowEngine.Execute` keeps returning `bool` and populating `ErrorReporter`. The
CLI check output now includes analysis diagnostic codes and its limited scope. The frozen WASM
`flow-runtime.js` result shape (`errors[]` with `kind`, `message`, `line?`,
`column?`) is produced by an adapter from the new model.

## Verification

- Every language contract produces the same stdout, outcome and coded diagnostics
  through source and parsed-tree evaluation (`EvaluationAdapterTests`).
- A subprocess check fixture with file writes, playback, looping and imported
  initializer output completes without causing those effects.
- Static tests cover imports/cycles, scopes, call shapes, dynamic uncertainty,
  cancellation/budgets, unsaved buffers and cross-file diagnostic locations.
- Real LSP completion/hover run under Linux strace with no sample/device/native
  audio access; the assembly scan rejects runtime/sample/style construction.
- Final evidence is in [the Phase 4 record](../baselines/phase4/README.md).
