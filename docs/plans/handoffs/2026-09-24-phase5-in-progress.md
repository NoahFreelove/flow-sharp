# Phase 5 in progress — 2026-09-24

Phases 0–4 are complete. Phase 5 has its first two implementation slices; its full
gate is **not complete**. Read `CLAUDE.md`, `docs/TESTING.md`, the restructuring
roadmap, progress ledger and `docs/decisions/2026-09-24-composition-snapshot-boundary.md`.

## Implemented

- `48259fa`: BCL-only `Flow.Music.Model` assembly and immutable detached score
  snapshots. `FlowLang.Music.CompositionCompiler` converts evaluated `SongData`
  without retaining AST/runtime values. Six model tests cover ownership, native
  construction, Flow parity, provenance, timing, musical data and cancellation.
- `9fdb8ef`: all four legacy song render paths assemble sections/repeats with one
  final allocation and linear copying. Four tests cover exact sample bits,
  ownership, bounded-copy cancellation, overflow and allocation growth. The
  128-repeat fixture drops assembly allocation from 135,302,544 to 2,097,392 bytes.

The model preserves authored duration, not rendered tails/frame rounding.
Compiler IDs are structural/deterministic, not persistent across edits. Resolved
pitch frequencies are detached, but reusable tuning descriptions are still open.
Old render/export consumers still use `SongData`; no native snapshot rendering or
export is claimed yet. Buffer output remains contiguous, not streaming.

## Verification

`5017ab3` updates the render timeout test: faster finite song assembly can finish
before its deadline, so the fixture now renders small buffers continuously.
All 17 cancellation/model tests pass. All-tier verification at `b3834d9` passes
**3,022 main + 21 MIDI tests**, **19 skips**, zero failures and zero tracked-file
mutations, including audio/corpus and Web publish checks. Evidence is in
`docs/baselines/phase5/`. Run with `MSBUILDDISABLENODEREUSE=1` to avoid idle
MSBuild workers retaining the existing Web test helpers' output pipes. The trimmed
Web bundle also passes the Node session smoke through the unchanged JS adapter.

## Next work

1. Establish explicit render options/context and native-host snapshot render/export
   proof. Replace ambient session adapters per the session-lifetime decision.
2. Route Flow through the same model while preserving tuning, articulation, ties,
   rests, overlap, portamento, parallel voices and per-section tempo behavior.
3. Consolidate note transforms and MIDI adapters. The boundary decision records
   the parser/export audit: retain existing CLI quantization and characterize
   serial/parallel export differences before migration; separate semantic fixes.
4. Extract bounded output/progress/cancellation and explicit memory ownership.
   Preserve byte/PCM baselines and measure realistic long-song memory behavior.
5. Close the full Phase 5 gate only when non-Flow hosts construct, render and
   export the same model and compatibility/performance evidence passes.

## Working rules

Commit each slice and update `docs/plans/progress/flow-restructuring.md`. Stage
explicit paths, use `Co-Authored-By: Codex <noreply@openai.com>`, and do not push.
Never hand-edit frozen WASM JavaScript or refresh the website bundle incidentally.
Run full suites serially, including across checkouts: legacy fixtures share fixed
`/tmp` WAV paths. Do not edit tracked files while the verifier hashes them.
Two ignored local Flow fixtures explain the working checkout's extra tests; leave
them alone. Check broad ignore patterns when adding `.flow`, test or Markdown files.
