# Mixed generator results — 2026-10-04

P7-06 extends generator results beyond scores. One source can return named score,
audio, effect graph and instrument layers together. The backend-readiness goal
remains active; this is not a Phase 7 completion claim.

## Implemented

- Existing `dawResult song` and `dawResult name song` remain compatible. Named
  `DawAudio`, `AudioGraph` and `DawInstrument` overloads return `DawResult`;
  `dawCombine` composes old score results and mixed results without MIDI writes.
- A result holds 1–32 outputs. Identity is unique within each role; a score and
  instrument may share a name. Duplicate outputs fail before publication.
- Output is detached from interpreter values. The model now references Audio as
  well as Music.Model to hold the same immutable PCM, graph and instrument
  definitions used for playback. It still does not reference the interpreter or UI.
- Generated content uses strict version-1 JSON. Graphs preserve node identities,
  versioned devices, input order, parameters and bypass; PCM uses little-endian
  IEEE float base64. The initial instrument codec accepts `flow.sine@1` only.
- Worker protocol version 2 transports the whole result. Request/source/context
  identity checks, hard cancellation/timeout and last-good publication remain in
  place. Old protocol replies are rejected rather than partially accepted.
- Inline audio is limited to 16 MiB across a result. Content JSON is limited to
  36 Mi characters, within the existing 40 Mi-character outer response limit.
  This is bounded inline interchange, not the final large-audio asset transport.

## Verification

All 18 direct/isolated generator tests passed, including a real child process
returning all four roles with identical detached contents, audio-only/graph-only
results, duplicate rejection preserving last-good output, malformed PCM and unknown
instrument/schema versions. Log: `/tmp/flow-mixed-results-final.log`.

The affected StudioModel, PlatformAudio, MusicModel, StaticBinding and Hosting
regression run passed **214 tests**, 0 failed. Log:
`/tmp/flow-mixed-results-regression.log`. Existing analyzer warnings remain;
the full solution suite was not run. `git diff --check` passed.

## Next work

Add atomic successful-result acceptance to the project document, preserving clip
placement and source windows while reconciling stable output bindings. Capture the
accepted result for redo rather than rerunning code. Current generation does not
automatically wire returned audio, graphs or instruments into project playback.

Then extend durable project source/device/asset persistence, file-backed large
audio transfer, full generation context, sampler/stateful effects and automation.
Native JUI and historical callback-gap diagnosis remain deferred. No new physical
device qualification or Web publish was performed.
