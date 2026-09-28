# Phase 5 complete — 2026-09-27

Phases 0–5 of the restructuring roadmap are complete. Read `CLAUDE.md`,
`docs/TESTING.md`, the roadmap, the progress ledger, and
`docs/decisions/2026-09-24-composition-snapshot-boundary.md` before Phase 6.
Gate evidence and carried limits: `docs/baselines/phase5/README.md`.

## What Phase 5 delivered

- `Flow.Music.Model` (BCL only): immutable composition snapshots, timing, and
  `Editing` (shared grid-correct quantize and transpose used by Flow builtins).
- `Flow.Audio` (model only): streaming dry sine snapshot renderer with options,
  cancellation, progress and shared note-duration policy.
- `Flow.Music.IO` (model + DryWetMidi): snapshot SMF export, the single GM
  routing and key tables. Flow `writeMidi` writes through it.
- Linear legacy song assembly; `scripts/MusicHost` (native WAV/MIDI) and
  `scripts/RenderScaleProbe` (15-min render: 9.0 GB → 327 MB allocated).

Owner decisions applied: `writeMidi` via snapshots, harpsichord → GM 6,
grid-correct quantize, JUI + GLFW 3.4 as the desktop UI direction.

## Carried into Phase 6

- Sampled/SFZ/drum/wavetable instruments, section reverb and lambda instruments
  still render through legacy full-buffer code and ambient services
  (`RenderServices.Current`, `PianoSynthesizer.CurrentReleaseSec`). Port them as
  stateful block processors with explicit services, not as a second offline port.
  Parity notes from the survey: pool stealing uses rendered buffer length
  (sampled notes include release tails); drums draw from the noise RNG in
  section → sequence → note order; `SampleCache` preload keys on `SongData`;
  `BarRenderer` writes parent meters into voice bars during render.
- Legacy song output is one contiguous buffer; streaming exists natively only.
- Structural compiler IDs; persistent edit IDs belong to the Phase 8 project model.
- Reusable tuning description; other transforms; `midiOut`, MusicXML, LilyPond.

## Phase 6 starting points

1. Block-processor contract (prepare/reset/process/params/tail/dispose) and a
   transport clock driving snapshots, per roadmap §7.4–7.6; start from the sine
   renderer and oscillator math (`NoteSynthesizer.cs` inline formulas are the
   byte contract; `BlepOscillator.PolyBlep`).
2. Callback-capable Linux device adapter and the managed-vs-native measurement
   (48 kHz / 256 frames, worst callback < 70% of deadline).
3. UI: JUI (`../jui/docs/integration.md`) through GLFW 3.4 with a thin
   `LibraryImport` layer. Needs JUI M3a canvas/controls, M3b scroll areas and C#
   bindings (JUI M5, or interim bindings) — coordinate ordering with the owner.

## Working rules

Commit each slice and update the ledger. Stage explicit paths; do not push.
Never hand-edit frozen WASM JavaScript or refresh the website bundle
incidentally. Run full suites serially with `MSBUILDDISABLENODEREUSE=1` (without
it, Web-publish tests can hang for over an hour on idle build workers). Do not
edit tracked files while the verifier hashes them. Two ignored local Flow
fixtures explain extra tests in this checkout; leave them alone.
