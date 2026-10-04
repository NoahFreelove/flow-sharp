# Clip envelope playback kernel — 2026-10-04

This is the playback foundation, not completion of project clip gain/fades.

`Flow.Music.Model.AudioClipEnvelope` captures source-frame anchor/window, gain and
linear fade-in/out lengths. Overlapping fades multiply. Fade-in begins at zero;
fade-out reaches zero on the final source frame. Zero fade length bypasses that
fade. Gain remains independent of asset data. Anchoring to source frames allows
split pieces to share the original envelope without adding new edge fades.

`ScheduledAudioClip` accepts the optional envelope. PreparedPcmPlayback evaluates
it per rendered frame after linear sample-rate conversion, using the fractional
source position. No callback allocation or interpreter call is added. Null envelope
and unity gain retain the previous exact sample path. GainAt subtracts integer
anchors/endpoints before conversion to double, including source anchors >2^53.

Validation: **14 passed**, zero failures/skips, `/tmp/flow-clip-envelope.log`.
Tests cover expected gain samples, whole/split playback identity, seeking into a
fade, unchanged source PCM, overlapping fades, fractional rate conversion, large
source anchors and invalid settings. Existing PCM/clip playback and bounce tests
also passed.

## Next required integration

1. Add optional envelope to AudioClip and preserve it in every reconstruction:
   move/nudge/align, trim, split/repeat/duplicate, track moves and processor commits.
   Splits preserve the envelope; decide/document explicit trim and processor policies.
2. Version ArrangementJson (currently 1) and ProjectJson (currently 15), preserving
   legacy default unity/no fades and rejecting nondefault fields under old tags.
3. Add captured project command and public Flow equivalent using the same envelope
   and kernel; export the settings in FlowProjectExporter. Do not introduce a
   separate UI-only fade implementation. Update module snapshots after API addition.
4. Pass clip envelope through ArrangementCompiler to ScheduledAudioClip. Verify
   save/reopen, undo/redo, split/trim, evaluated Flow export and actual playback parity.

Remaining goal also includes metronome/count-in, explicit captured-sample example,
documentation reconciliation and final browser/WASM/core/requirements audit. Native
frontend and historical callback-gap diagnosis remain deferred; physical hardware
qualification remains separately open.
