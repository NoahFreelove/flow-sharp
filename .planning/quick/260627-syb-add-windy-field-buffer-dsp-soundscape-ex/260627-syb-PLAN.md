---
quick_id: 260627-syb
title: Add windy-field buffer-DSP soundscape example to flow-site playground
date: 2026-06-28
status: planned
---

# Quick Task 260627-syb: Add windy-field buffer-DSP soundscape example

## Goal

Add a new playground example, `windy-field`, to `flow-site/static/examples/`. It is a
layered ambient soundscape ("windy field") built entirely from buffer/DSP functions —
a deliberate contrast to the existing note-stream/instrument examples. Demonstrates a
broad slice of Flow's buffer toolbox: `noise`, `lowpass`/`highpass`/`bandpass`,
`granular`, `reverb`, `delay`, `gain`/`volume`/`pan`, `fadeIn`/`fadeOut`, `mix`,
`appendBuffers`, `createSilence`, plus a note-stream bird motif rendered to a buffer
via the `triangle` synth and processed with FX.

## Web-target constraint

Every function used stays in the `FlowTarget=Web` build (full synthesis + DSP). The only
audible caveat — sampled instruments are silent on web — is avoided by rendering the bird
motif with the oscillator-based `triangle` synth (not a sampled instrument). Validated by
desktop render: 17.5 s stereo, peak 0.56 FS (no clipping), clean (no errors/advisories),
render wall-time 1.46 s (comfortably under the 30 s WASM cap even allowing for the
single-threaded slowdown).

## Tasks

### Task 1 — Add the example file + manifest entry
- **files:** `flow-site/static/examples/windy-field.flow` (new),
  `flow-site/static/examples/manifest.json` (append one array element)
- **action:** Write `windy-field.flow` with the validated soundscape (final line `(play master)`).
  Append a manifest entry `{ id: "windy-field", label: "Windy field soundscape", blurb: ..., file: "windy-field.flow" }`
  as the last array element, preserving the file's tab indentation.
- **verify:** `manifest.json` parses as JSON; the `.flow` renders clean via the interpreter
  (`writeWav` to a temp path — no audio device in CI).
- **done:** Both files present; manifest valid JSON with the new entry last; render produces
  non-silent stereo audio with no errors.

## must_haves

- **truths:**
  - The example uses only Web-safe synthesis/DSP functions (no sampled instruments, SFZ, OSC, MIDI, mic, or `live`).
  - The bird motif renders with the `triangle` oscillator synth so it is audible on web.
- **artifacts:**
  - `flow-site/static/examples/windy-field.flow`
  - `windy-field` entry appended to `flow-site/static/examples/manifest.json`
- **key_links:**
  - `flow-site/static/examples/manifest.json` (playground example registry)
  - `flow-lang/audio.flow` (buffer/DSP surface used by the example)
