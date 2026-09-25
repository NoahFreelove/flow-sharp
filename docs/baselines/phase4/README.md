# Phase 4 — completed 2026-09-24

Phase 4 of the restructuring roadmap (honest analysis and shared tooling) is
complete. Implementation: `ab5ea71`, `39eb9bc`, `33c7207`, `c7476b5`; final
compatibility cleanup: `85725de`. Phase 5 is next.

## Gate evidence

| Gate | Result |
| --- | --- |
| Check cannot execute script effects | CLI subprocess fixture includes writes, playback, infinite looping and imported initializer output; no effects occur and check terminates. |
| Shared analysis and module/signature discovery | CLI and editor diagnostics use `LanguageAnalysis`; completion/hover consume reachable descriptors; open document overlays and related import-error spans are tested. |
| No runtime introspection for editor metadata | LSP IL scan rejects runtime registration, engine/audio/sample-cache/style-registry calls. Real completion/hover under Linux strace has zero device/sample/native audio accesses. |
| Diagnostic codes/spans and evaluation compatibility | Every language contract produces identical output/outcomes/coded diagnostics through source and parsed-tree evaluation. Explicit sessions and HostFailure are covered. |
| Browser compatibility | Trimmed Web bundle boots through unchanged JavaScript; repeat runs print `42` independently, invalid syntax returns a located `parse` error, music arithmetic prints `150ms`. |

Full all-tier verifier at `c7476b5`: **3,012 main tests + 21 MIDI tests passed**,
**19 skips**, **0 failures**, **0 tracked-content mutations**. This includes the
Desktop solution build, characterization/dependency gates, long tests and Web
publish tests. See [all-verification.json](all-verification.json). The final
suffix-alias/dead-helper cleanup passed 25 binding/completion/import tests and
94 evaluation/browser adapter tests. Earlier core evidence remains in
[core-verification.json](core-verification.json).

This is the working checkout: it contains two ignored local Flow fixtures,
`test_break_builtin.flow` and `test_markov_corpus_array.flow`, explaining the two
extra main tests versus a clean clone. No fresh-clone gate is claimed for Phase 4.
The 19 skips retain their prerequisite/Web-only reasons in the TRX/logs under
`/tmp/flow-phase4-all`. Hardware listening and remote CI were not performed.

## Reproduce

```sh
python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-phase4-all
python3 scripts/ci/analysis_smoke.py flow-lsp/bin/Debug/net10.0/flow-lsp \
  --trace --artifacts /tmp/flow-phase4-editor
node scripts/ci/wasm-session-smoke.mjs \
  "$PWD/flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/flow-runtime.js" \
  /tmp/flow-phase4-wasm.json
```

Run suites serially, including across checkouts, and do not edit tracked files
while the verifier hashes them. `--trace` requires Linux strace. The all-tier Web
publish supplies the AppBundle for the Node smoke. The editor trace and stderr are
under `/tmp/flow-phase4-editor`; summaries are committed as
[editor-verification.json](editor-verification.json) and
[wasm-verification.json](wasm-verification.json). The WASM smoke emitted the existing
missing Mono symbol-file warning; it did not affect execution.

## Explicit limits

Static analysis checks syntax/top-level imports and issues conservative warnings
for lexical names and source-procedure call shape/primitive literal mismatches.
It does not execute module bodies or claim full type inference. Declaration order,
dynamic exports, nested imports, host overloads/defaults, non-literal values,
qualified/member dispatch, musical expansion, capabilities and termination remain
runtime concerns. Check success means no syntax/import errors, not proof that
execution succeeds. All seven language examples and zero-error language contracts
also pass check without static warnings.

The browser adapts coded C# results to its existing JSON shape; the frozen JS API
has no new analysis export. The committed website WASM bundle is unchanged.
The precise API scope is in the
[analysis/evaluation decision](../../decisions/2026-09-22-analysis-and-evaluation-api.md).
