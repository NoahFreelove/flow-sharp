# Persistent track mute/solo — 2026-10-04

Project schema 12 adds `ProjectTrack.Muted` and `Solo`, defaulting false for older
projects. Older schema tags reject nondefault new flags. `SetMuted` and `SetSolo`
are captured undoable commands; unchanged values preserve the snapshot/history.
Track identity and stable input-bus mapping survive these edits and reordering.

Multiple solo tracks are audible together. Mute takes precedence over solo; if
all solo tracks are muted, other tracks remain excluded. ProjectCompiler lowers
the resulting silent buses through ordinary `flow.gain` nodes after track inserts
and before the shared mixer. This also gates autonomous track effects and live
monitoring, using the existing DSP implementation. Notes/sources and track effect
state are still processed; timeline length and authored material are preserved.

An explicit stem selection replaces saved solo selection but retains saved mute.
The stem manifest describes this. Shared/master graph processing remains active,
including autonomous shared signals: mute controls the named track bus, not an
arbitrary downstream branch of user-authored shared code. MIDI note export remains
an authored-note interchange path, so mixer mute/solo does not erase its notes.

The six-argument `dawTrack` overload exposes both flags. Flow project export emits
them explicitly; earlier three/four-argument overloads remain audible by default.
The native surface has 715 registered signatures, 709 reachable.

As with other structural project edits, applying this state prepares a replacement
playback graph. It does not promise seamless DSP-state transfer or a click-free
mute ramp; those real-time transition guarantees remain separate qualification.

Focused track, migration, Flow export, stem and API checks: 14 passed,
`/tmp/flow-track-state-tests.log`. Additional regression checks include muted live
monitoring and suppression of a track insert's autonomous signal and delay tail.

Affected backend regression plus API snapshot verification without update mode:
**517 passed**, zero failures/skips, `/tmp/flow-track-state-regression.log`.
`git diff --check` passed. Full core/browser and hardware qualification remain open.

Next: saved project render/sample-rate preferences, committed migration fixtures,
then generation context/capabilities and integrated backend qualification from the
readiness audit. Native UI and historical callback-gap investigation stay deferred.
