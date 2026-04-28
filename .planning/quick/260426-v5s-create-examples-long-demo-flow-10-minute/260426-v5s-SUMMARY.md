---
id: 260426-v5s
name: long-demo-flow
type: quick
description: Create examples/long_demo.flow — ~10 minute Flow showcase song
status: complete
date: 2026-04-27
duration_seconds: 593
output_artifacts:
  - examples/long_demo.flow
  - examples/output/long_demo.wav (gitignored — regenerated on run)
  - examples/output/long_demo.mid (gitignored — regenerated on run)
---

# Quick Task 260426-v5s — Summary

## What shipped

`examples/long_demo.flow` — a single, runnable Flow script that generates a **9 minute 53 second** WAV (~10 min target) plus a parallel MIDI file, walking through 46 numbered feature spotlights every ~10–20 seconds.

Run:

```bash
dotnet run --project flow-interpreter examples/long_demo.flow
```

Outputs:

- `examples/output/long_demo.wav` — ~100 MB stereo 44.1 kHz, 593 s
- `examples/output/long_demo.mid` — ~6.7 KB Standard MIDI File of the 35-section piano main piece

## Feature coverage (46 spotlights)

**Piano main piece (35 sections, rendered by a single `renderSong … "piano"` call, ~394 s)**

01 `gain X { }` block · 02 plain note streams · 03 inline `pp p mp mf f ff` markers · 04 `dynamics ff { }` block · 05 `crescendo` · 06 `decrescendo` · 07 `swell` · 08 accent `>` · 09 `stacc` · 10 `ten` · 11 `marc` · 12 `(ghost X)` · 13 `(grace X)` · 14 `trill +2st` · 15 `tremolo 4` · 16 `transpose +7st` · 17 `retrograde` · 18 `invert` · 19 `up 1` / `down 1` · 20 `humanize 0.25` · 21 `(vary seq 0.3 42)` seeded · 22 `{3:2 …}q` triplets · 23 `{5:4 …}q` quintuplets · 24 roman numerals in `key` · 25 `progression | I:2 vi IV V I IV V I |` · 26 flat literals · 27 sharp literals · 28 `enharmonic` in `key Dbmajor` · 29 `swing 0.55 { }` · 30 `(euclidean 5 16 …)` basic · 31 `(euclidean … swing humanize seed)` · 32 `timesig 3/4` waltz · 33 `key Aminor` modal shift · 34 `reverbTime 2.5 { }` · 35 chord brackets `[C4 E4 G4]w`

**Buffer-level + synth gallery + outro (~199 s)**

36 effect chain `reverb -> lowpass -> compress` · 37 `strings` preset · 38 `organ` preset · 39 `bell` preset · 40 `brass` preset · 41 `sax` preset · 42 `drums` preset (kick C2 / snare D2 / hat F#2) · 43 `appendBuffers` sequential timeline · 44 `tempoRamp 100 → 50` ritardando · 45 `fadeIn 1.5 / fadeOut 4.0` · 46 dual `writeWav` + `writeMidi` export

Plus: `$"…"` string interpolation throughout the print lines.

## Parser quirks discovered & worked around

Two limitations surfaced while building this demo. They are worth flagging for future Flow-language work but did not require code changes for this task — the script works around them:

1. **Multi-bar tuplet brackets in a single sequence don't parse.** `| {3:2 C4 D4 E4}q F4q ... |` works in a single bar, but `| {3:2 ...}q ... | {3:2 ...}q ... |` (a tuplet bracket after a bar separator) errors with `Unexpected token LBrace '{'`. Worked around by declaring each tuplet bar as its own one-bar Sequence and stitching them via `(concat …)` — see sections `tripBlock` and `quintBlock`.
2. **`progression | … |` accepts only a single `|…|` block.** Multi-bar form `progression | I IV | V vi |` errors with `Empty note stream`. Worked around by inlining all numerals into one block: `progression | I:2 vi IV V I IV V I |`.

Other modern-syntax features (chord brackets, inline dynamics, articulations, ornaments, transforms, `swing` block, `gain` block, `reverbTime` block, `tempoRamp`, `appendBuffers`, `vary`, seeded `euclidean`, multi-letter flat/sharp literals, key-aware `enharmonic`, dual export) all worked as documented.

## Verification

- Script runs end-to-end with no parser or runtime errors.
- WAV: 26,176,517 frames @ 44.1 kHz stereo = **593 s ≈ 9 m 53 s** — within the "like 10 minutes" ask.
- MIDI: 6,709 bytes, exported from the same `mainPiece` Song value used for WAV rendering.
- Generated artifacts (`*.wav`, `*.mid` in `examples/output/`) are gitignored — only the `.flow` source is committed.

## Files changed

- `examples/long_demo.flow` (new — 459 lines)
- `.planning/STATE.md` (Quick Tasks Completed table + Last activity line)
- `.planning/quick/260426-v5s-create-examples-long-demo-flow-10-minute/260426-v5s-PLAN.md` (new)
- `.planning/quick/260426-v5s-create-examples-long-demo-flow-10-minute/260426-v5s-SUMMARY.md` (new — this file)

## Atomic commits

- `feat(quick-260426-v5s): add examples/long_demo.flow — ~10 min Flow feature showcase`
- `docs(quick-260426-v5s): plan + summary + STATE.md update`
