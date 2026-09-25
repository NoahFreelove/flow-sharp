# Phase 4 in progress — 2026-09-24

Superseded by [the completed Phase 4 handoff](2026-09-24-phase4-complete.md).
The notes below describe the earlier interim state.

Phase 3 is complete. Continue roadmap Phase 4 (honest analysis/shared tooling).
Read `CLAUDE.md`, `docs/TESTING.md`, the Phase 4 section of the restructuring
roadmap, the progress ledger and analysis/evaluation API decision first.

## Implemented

- `ab5ea71`: shared `FlowLang.Analysis` parsing, coded syntax diagnostics,
  declaration descriptors and bounded/cancellable top-level import discovery.
  `flow check` now analyzes without executing. LSP parsing shares the frontend.
- `39eb9bc`: LSP builtin completion/hover signatures come from parsed `.flow`
  declarations; startup no longer registers implementations through dummy runtime
  objects. Documentation parsing also shares the frontend. Configured module
  search paths are supported by check.
- Interim evidence is in `docs/baselines/phase4/`: core build, 2,871 main + 21 MIDI
  tests pass; LSP startup smoke passes. No all-tier/fresh-clone/Web gate this slice.

## Next work

Start with import-aware lexical binding and known call arity/named-argument
analysis using module descriptors. Preserve dynamic uncertainty: analysis success
currently proves only syntax and top-level import discovery. `AnalysisResult`
explicitly lists unchecked items. Read language contracts before adding diagnostics
so runtime-dependent names and overloads are not falsely rejected.

LSP currently shares Parse and descriptors, not Analyze's import graph. Its
`ModulesVisibleThrough` rules are still coarse and its name index can collapse
module ownership; replace these as import-aware binding is shared. Then address
provable type mismatches, unified evaluation diagnostics/HostFailure and explicit
CLI/browser adapters as required by the roadmap. Do not mark Phase 4 complete yet.

## Working rules

Commit each slice and update `docs/plans/progress/flow-restructuring.md`. Stage
explicit paths, not `git add .`; commit trailer is
`Co-Authored-By: Codex <noreply@openai.com>`. Do not push. Do not hand-edit the frozen
WASM JavaScript or claim the committed website bundle includes these changes.
Do not edit tracked files while the verifier hashes them. Run suites serially,
including across worktrees (legacy fixtures share a fixed `/tmp` WAV path).
Two ignored local fixtures, `test_break_builtin.flow` and
`test_markov_corpus_array.flow`, explain working-checkout versus clean-clone counts;
leave them alone. New `.flow` and some Markdown files may need explicit force-add
because of broad ignore patterns. Keep semantic fixes separate from extraction.
