---
quick_id: 260627-syb
title: Add windy-field buffer-DSP soundscape example to flow-site playground
date: 2026-06-28
status: complete
commit: e4c9555
---

# Quick Task 260627-syb — Summary

## What shipped

A new flowlang.dev playground example, **`windy-field`** — a layered ambient soundscape
built entirely from buffer/DSP functions, as a deliberate contrast to the existing
note-stream/instrument examples.

- `flow-site/static/examples/windy-field.flow` (new) — the soundscape source.
- `flow-site/static/examples/manifest.json` — appended a 9th entry (`windy-field`,
  "Windy field soundscape") as the last array element.

## Composition (what it demonstrates)

Five layers mixed down with a master reverb:
1. **Wind bed** — `noise` → `lowpass` → `highpass`, long `fadeIn`/`fadeOut`, `gain`.
2. **Gust** — `noise` → `bandpass`, fades, `pan`, placed mid-scene with `createSilence` +
   `appendBuffers`.
3. **Rustling grass** — `noise` → `highpass` → `granular` → `volume`.
4. **Distant bird** — a `| ... |` note-stream motif → `renderSong ... "triangle"` (buffer) →
   `lowpass` → `delay` → `reverb` → `pan` → offset via `createSilence`/`appendBuffers`.
5. **Mixdown** — `mix` ×3 → master `reverb` → `gain`.

Tours ~15 buffer/effect builtins: `noise`, `lowpass`, `highpass`, `bandpass`, `granular`,
`reverb`, `delay`, `gain`, `volume`, `pan`, `fadeIn`, `fadeOut`, `mix`, `appendBuffers`,
`createSilence` (+ `renderSong` for the note-stream→buffer bridge).

## Web-target safety

Every function stays in the `FlowTarget=Web` build (full synthesis + DSP). The only audible
web caveat — sampled instruments are silent — is sidestepped by rendering the bird with the
oscillator-based `triangle` synth rather than a sampled instrument.

## Verification

- `manifest.json` parses as valid JSON; 9 entries; `windy-field` is last.
- Rendered the committed `.flow` via the interpreter (CI-safe `writeWav`, not `play`):
  exit 0, **no runtime errors or advisories**, output 17.51 s stereo, peak 0.575 FS
  (no clipping), RMS −23.4 dBFS, render wall-time ~1.5 s (well under the 30 s WASM cap).

## Notes

- The new `.flow` was **force-added** (`git add -f`): the root `.gitignore` has a blanket
  `*.flow` rule, so every example under `flow-site/static/examples/` is tracked only via
  force-add. This matches how the existing 8 examples were committed.
- The committed `static/wasm/` AppBundle does not auto-update; this example needs no runtime
  change (uses only already-shipped functions), so no `sync-runtime.sh` re-run is required.
