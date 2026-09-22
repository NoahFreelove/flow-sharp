# Public analysis and evaluation APIs

Status: proposed shape accepted for restructuring Phase 1 (2026-09-22). Names settle in Phases 2 and 4.

## Context

Today's embedding surface is `FlowEngine.Execute(source, fileName) -> bool` plus
`ErrorReporter`, `SourceMap`, `GetLastExpressionResult` and
`ExecuteScriptAndGetResult`. `flow check` executes the program with console
suppression; it does not analyze. Most checking happens at evaluation time. The
[language contract](../../contracts/language/README.md#what-analysis-can-prove)
shows that only parse errors are reported before execution; other failures
surface only on the executed path. Since the Phase 1 semantic fixes, internal
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

## Compatibility

`FlowEngine.Execute` keeps returning `bool` and populating `ErrorReporter`. The
CLI keeps its current text output until diagnostic codes exist. The frozen WASM
`flow-runtime.js` result shape (`errors[]` with `kind`, `message`, `line?`,
`column?`) is produced by an adapter from the new model.

## Verification to add

- Every contract with `errors:` produces the same count and messages through
  `Evaluate` as through `FlowEngine.Execute`. No contract reports a location-less
  (`0:0`) diagnostic.
- `Analyze` on `contracts/language/**` reports the parse-time failures and executes
  nothing: no stdout, no module side effects such as the `textutil body runs`
  print.
