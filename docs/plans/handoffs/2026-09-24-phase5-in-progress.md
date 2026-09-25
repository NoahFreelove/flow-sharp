# Phase 5 in progress — 2026-09-24

Phases 0–4 are complete. Phase 5 has detached score, linear assembly and native sine rendering slices; its full
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
Old Flow render/export consumers still use `SongData`. The new `Flow.Audio`
sine renderer consumes snapshots directly and streams bounded stereo blocks;
`scripts/MusicHost` constructs a score and exports WAV without any language or
compatibility assembly. Legacy buffer output remains contiguous.

The native renderer has explicit options/cancellation/progress, a section note
budget, per-sequence pool stealing, exact legacy sine timing/mix math and a shared
`NoteDuration` policy used by `BarRenderer`. It rejects nonzero reverb. No sampled
instrument, Flow lambda, SFZ, effect or MIDI migration is claimed. The host's PCM16
encoder is an example without dither; legacy WAV byte contracts stay unchanged.
Ten native rendering tests include bit-exact legacy comparisons, Flow/native
parity, block-size independence, repeat allocation, cancellation and concurrency.

## Verification

Latest implementation: `a6d720a` (isolated streaming sine rendering and roadmap
update). Full all-tier verification passes **3,032 main + 21 MIDI tests**, **19
skips**, no failures and no tracked-file mutations. The generated Web bundle
passes the unchanged JS adapter smoke. `scripts/MusicHost` writes a valid
294,000-frame stereo WAV and loads no language/compatibility assembly. Evidence:
`docs/baselines/phase5/native-*.json`. This supersedes the initial-slice counts
below; Phase 5's full instrument/export gate is still open.

`5017ab3` updates the render timeout test: faster finite song assembly can finish
before its deadline, so the fixture now renders small buffers continuously.
All 17 cancellation/model tests pass. All-tier verification at `b3834d9` passes
**3,022 main + 21 MIDI tests**, **19 skips**, zero failures and zero tracked-file
mutations, including audio/corpus and Web publish checks. Evidence is in
`docs/baselines/phase5/`. Run with `MSBUILDDISABLENODEREUSE=1` to avoid idle
MSBuild workers retaining the existing Web test helpers' output pipes. The trimmed
Web bundle also passes the Node session smoke through the unchanged JS adapter.

## Next work

1. Extend the snapshot rendering path to existing instrument/effect contracts and
   explicit render services (caches/RNG/release), replacing ambient adapters per
   the session-lifetime decision. Native dry sine/WAV proof already exists.
2. Route Flow through the same snapshot model while preserving current musical
   behavior and all selected byte/PCM baselines. Add snapshot MIDI export proof.
3. Consolidate shared editing transforms and MIDI adapters. The boundary decision
   records the parser/export audit: preserve CLI quantization and characterize
   serial/parallel export differences before migration; separate semantic fixes.
4. Extend bounded output/progress/cancellation to the full instrument path and
   measure realistic long-song memory. The native sine path already streams with
   repeat-independent PCM storage; the legacy path still allocates a full buffer.
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
