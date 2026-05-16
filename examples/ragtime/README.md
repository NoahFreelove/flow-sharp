# Ragtime Showcase — `ragtime.flow`

A solo-piano upbeat ragtime piece in F major, ~58 seconds, single-movement
ABA form. The companion showcase to `examples/symphony/symphony.flow` —
the symphony is pensive and orchestral; this piece is upbeat and
single-instrument. Together they demonstrate Flow's genre-agnostic claim
inside the v1.4 release.

## Run

Both showcase pieces require [VSCO Community CE 1.1.0](https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0)
installed and `sfz_root` set in `~/.config/flow/config.toml`. See
[`../symphony/README.md`](../symphony/README.md) § "Setup" for the
one-time install steps — the ragtime piece reuses the same configuration.

```bash
dotnet run --project flow-cli -- render examples/ragtime/ragtime.flow -o ignored.wav
```

The `-o` flag is ignored at Phase 30 — the `(writeWav)` call inside the
source is the real output path. The render lands at
`examples/output/ragtime.wav` + `examples/output/ragtime.mid` (both
gitignored).

Play the result:

```bash
aplay examples/output/ragtime.wav   # Linux ALSA
afplay examples/output/ragtime.wav  # macOS
```

## What it demonstrates

- **Solo SFZ instrument** — `Sfz piano = (loadSfz #piano)` → VSCO-CE's
  `UprightPiano.sfz`. The simplest possible SFZ render pipeline (one
  patch, one `renderSong "sampler:piano"` call, mix → write).
- **Stride bass + syncopated melody via voice blocks (Phase 28
  polyphony)** — every bar uses a `{voice ...}{voice ...}` block to
  layer the LH stride (bass + chord alternating) under the RH melody.
  This is the canonical ragtime texture and shows Phase 28's voice
  pool handling parallel piano voices cleanly.
- **All 5 Phase 28 articulation tokens** — `stacc` on the LH bass
  notes (sharp percussive feel), `>` (accent) on the syncopated RH
  off-beat downbeats, `leg` (legato) on the A' melodic passing line,
  `marc` (marcato) on the section-shift downbeat and final cadence,
  `ten` (tenuto) on the B-section cadential horn-call rhythm.
- **Tuplet bracket** — one `{3:2 F4 G4 A4}q` triplet flourish in the
  A-section third bar (Phase 19 tuplet syntax).
- **Chord brackets** — `[A2 C3 F3]q` style stacked notes for the
  stride LH harmony positions.
- **Single-instrument mix-stack** — `(reverb 0.15 1.5s)` small-room
  reverb + `(compress -10dB 3 50ms 100ms)` gentle compression. Far
  less processing than the symphony's 5-instrument stack — solo
  piano sits cleanly in the mix on its own.

## Reproduce two-run determinism

Same contract as the symphony — Phase 28's two-run cmp-clean
determinism holds end-to-end on the real VSCO-CE library:

```bash
cd /tmp && rm -f ragtime.wav && \
  dotnet run --project ~/Desktop/projects/flow-sharp/flow-cli -- render \
  ~/Desktop/projects/flow-sharp/examples/ragtime/ragtime.flow -o ignored.wav && \
  cp ragtime.wav /tmp/ragtime_a.wav && rm -f ragtime.wav && \
  dotnet run --project ~/Desktop/projects/flow-sharp/flow-cli -- render \
  ~/Desktop/projects/flow-sharp/examples/ragtime/ragtime.flow -o ignored.wav && \
  cp ragtime.wav /tmp/ragtime_b.wav && \
  cmp /tmp/ragtime_a.wav /tmp/ragtime_b.wav && echo "byte-identical"
```

Same inputs → same bytes. The composer's compositional iterations are
fully reversible without surprise rendering drift.

## Known quirks

- The SFZ parser logs two non-fatal advisories per render
  (`ampeg_dynamic`, `group_label` — opcodes outside Phase 33's
  14-entry common-subset whitelist). Audible output is unaffected.
- The render is intentionally NOT wrapped in `humanizeGaussian` even
  though ragtime traditionally swings. The current Flow interpreter
  produces an empty render when `humanizeGaussian` wraps a sequence
  containing voice blocks (a v1.5 follow-up). The piece's swing feel
  comes from the articulation contrast (sharp `stacc` LH under
  longer-duration RH notes) instead — which works out fine, since
  classical ragtime is typically played close to straight anyway.
