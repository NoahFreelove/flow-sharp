# Phase 5 in progress — 2026-09-27

Phases 0–4 are complete. Phase 5 has detached score, linear assembly, native sine
rendering and snapshot MIDI export slices; its full gate is **not complete**. Read
`CLAUDE.md`, `docs/TESTING.md`, the restructuring roadmap, the progress ledger and
`docs/decisions/2026-09-24-composition-snapshot-boundary.md`. The previous handoff
(`2026-09-24-phase5-in-progress.md`) describes the earlier slices.

## Implemented since the last handoff

- `3a69b3a`: `Flow.Music.IO` (`flow-music-io/`, model + DryWetMidi + BCL only).
  `MidiCompositionExporter` writes snapshots as SMF; it owns `InstrumentRouting`
  and `KeySignatures`, which flow-lang's legacy routing/key surfaces delegate to.
  Byte-identical to legacy `writeMidi` for three Flow corpora; intentional
  score-timing divergences are listed in the boundary decision and pinned by
  `MusicModel/SnapshotMidiExportTests`. `scripts/MusicHost` optionally writes MIDI.
- `c419c14`: independent-review fix — note-offs round from absolute ends (a
  repeated key cannot end its successor early), meter and 28-bit delta validation,
  native host writes MIDI only after in-memory validation.

Legacy `writeMidi` output is unchanged. The Web bundle now includes
`Flow.Music.IO.wasm`; the committed website bundle was **not** refreshed.

## Verification

At `c419c14`: **3,041 main + 21 MIDI tests**, **19 skips**, zero failures, zero
tracked-file mutations; the regenerated Web bundle passes the unchanged-adapter
smoke; the native host MIDI is stable (SHA-256 recorded) and readable by
`midi2flow`. Evidence: `docs/baselines/phase5/midi-*.json` and the README there.

## Open decisions for the owner

1. Reroute Flow `writeMidi` through the snapshot exporter? This fixes the
   overfull-bar section overlap and voice-block legato/portamento omission, but
   changes bytes for those scores and for non-integer ticks (swing/humanize).
2. Fix the GM routing quirk where `harp` shadows `harpsichord` (program 6 is
   unreachable)? Changes MIDI/MusicXML program numbers for harpsichord tracks.

## Next work

1. Shared editing transforms on the model (transpose/quantize/velocity first),
   with Flow builtins adapting to them; needs a decision on converting snapshot
   edits back to legacy `SequenceData` or keeping the builtins on legacy data.
2. Extend snapshot rendering to instrument/effect contracts with explicit render
   services (caches/RNG/release), replacing ambient adapters.
3. Route Flow render/export consumers through snapshots, preserving selected
   byte/PCM baselines; MIDI rerouting waits on decision 1.
4. Bounded output/progress for the full instrument path; measure long songs.
5. Close Phase 5 only when non-Flow hosts construct, render and export the same
   model and compatibility/performance evidence passes.

## Working rules

Commit each slice and update `docs/plans/progress/flow-restructuring.md`. Stage
explicit paths and do not push. Never hand-edit frozen WASM JavaScript or refresh
the website bundle incidentally. Run full suites serially with
`MSBUILDDISABLENODEREUSE=1`; legacy fixtures share fixed `/tmp` WAV paths. Do not
edit tracked files while the verifier hashes them. Two ignored local Flow fixtures
explain the working checkout's extra tests; leave them alone.
