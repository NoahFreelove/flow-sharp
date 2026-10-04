# Project clip gain/fades and Flow parity — 2026-10-04

Completed the project integration from `2026-10-04-clip-envelope-kernel.md`.

- AudioClip stores an optional source-anchored AudioClipEnvelope. Existing clips
  default to unity/no fades. Placement/trim/split/repeat/duplicate preserve it.
- ProjectClipEnvelopeCommands.Set captures one undo action, anchors new settings
  to the visible source window and suppresses equal-setting history entries.
- Arrangement schema 2 and project schema 16 serialize envelopes. Older formats
  remain readable; old tags carrying nondefault envelope data are rejected.
- dawClipEnvelope and FlowProjectExporter reconstruct the same clip/envelope.
  Frame counts use string arguments for exact long values. Module surface now
  reports 729 native signatures / 723 reachable, with snapshots updated and verified.
- ArrangementCompiler passes envelopes into the shared PreparedPcmPlayback kernel.
  No additional UI-only effect implementation exists.
- Offline processor input captures gain/fades into the PCM; accepted replacement
  resets the envelope to avoid double application. Undo restores the original.

Trim preserves source anchoring; it does not resize the fade. Split does not create
new fades at its cut. Re-setting settings reanchors to the current window. The
contract document records overlap multiplication, edge clamping and processor policy.

## Verification

**558 passed**, zero failures/skips, `/tmp/flow-clip-envelope-final.log`.
Includes studio model, platform-host, music model, hosting and module-surface checks.
New integration verifies audible gain change, no-op/history, JSON, evaluated Flow
export and exact split-window playback parity. It rejects envelope data under an
old project schema. Processor coverage verifies envelope capture once, accepted
output reset, undo/redo and saved replacement. Existing migration fixtures pass.
One initial failure was an outdated future-schema assertion (version 2 is now
supported); it now rejects version 3 and still checks unknown fields.

## Continue

Implement metronome/count-in through the shared Flow music/graph pipeline and
recording activation semantics. Then deliver the captured-sample authoring example,
reconcile historical roadmap/readiness rows and current architecture/support docs,
and complete final browser/WASM/core and requirement-by-requirement checks.
Native frontend and historical callback-gap diagnosis remain deferred. Physical
device/latency qualification remains separate and open.
