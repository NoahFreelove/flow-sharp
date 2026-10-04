# Authoring support and expanded MVP audit — 2026-10-04

The backend goal remains incomplete. Prior passing checkpoints did not prove all
requirements in the MVP design brief; the additional findings below stay in scope.

## Authoring change

Ordinary generators now admit the context-dependent `sing` registrar only. This is
in-memory formant synthesis through Flow's existing tuning-aware implementation.
External `tts` and command configuration remain outside the allowed native set.
Formant output is mono; the existing `pan` function explicitly promotes it to stereo
before `dawAudio`, preserving the established stereo asset contract.

Focused native policy suite: **7 passed**, zero failures/skips,
`/tmp/flow-formant-generator-final.log`. A 100 ms vowel produces 4410 frames at
44100 Hz and sample-identical direct/isolated output. External TTS is denied before
its implementation. Existing style parity and file/device denial tests pass.

## Newly confirmed requirement gaps

Source: `docs/plans/2026-10-01-daw-mvp-design-brief.md`, required MVP table.

| Requirement | Current evidence | Next implementation/review |
| --- | --- | --- |
| Metronome and count-in | No implementation found in studio model/engine/host; transport and recording handle acknowledged start but no counted lead-in. | Shared Flow-authored click generation/clock semantics, host preparation and recording admission/count-in, tempo/meter tests. |
| Per-audio-clip gain and basic fades | `AudioClip` stores placement/window/rate/nudge only. Track gain is not independent clip gain. | Versioned clip processing definition, shared Flow equivalent, playback/undo/save/export parity and split/trim policy. |
| WAV range selection | `ProjectBounceFile` always prepares/writes the full project; `PlaybackWaveWriter` requires a fresh full cursor. | Explicit musical/frame range semantics, effect preroll/tails and captured export selection, cancellation/progress and atomic-output tests. |
| Piano-roll copy/paste and grouped gestures | Primitive note Add/Delete/Move/Resize/SetVelocity/SetPitch plus captured EditSequence exist. | Verify supported clipboard capture/paste identity/relative timing and multi-sequence transaction APIs; do not assume primitive Add proves full workflow. |

These are backend prerequisites despite deferring native UI. Native recent-project
menus, zoom/scroll, device panels and keyboard bindings remain frontend work; saved
track presentation metadata should be checked alongside model requirements.

## Sample compatibility assessment

MVP requires Flow instruments, audio clips and assets, not an implicit user-library
scanner. Host-captured asset grants, packaged sample assets, dawSampler and graph
sample playback provide the explicit DAW path. Legacy named sample renders still
reject implicit disk access. Document how users author the equivalent with granted
assets and verify an executable example before closing the sample review. Do not
silently restore ambient disk access or describe every legacy instrument as migrated.

## Next

Run the full core checkpoint to check accumulated compatibility, then implement
these gaps and reconcile the stale historical roadmap/audit rows. Browser/WASM
qualification, current architecture/support docs and complete requirements audit
remain required. Hardware/latency qualification remains separately open. Native
frontend and historical callback-gap investigation remain deferred by the owner.
