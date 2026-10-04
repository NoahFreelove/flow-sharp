# Transport-preserving project publication — 2026-10-04

P8-20 keeps transport running when a newly prepared project replaces the active
project. Full backend readiness remains open.

## Implemented

- `QueuedSinePlayback.TryReplace` accepts `PlaybackReplacementMode`. `Reset`
  remains the default for existing callers. `PreserveTransport` transfers the
  audio owner's actual cursor, playing/paused/stopped state and loop at the
  installation boundary. The project coordinator now selects this mode.
- Commands already queued before publication are applied to the old transport
  before transfer. A queued seek/pause/loop is therefore not silently lost.
  Commands arriving while replacement is pending remain rejected for retry,
  and stop retains priority over replacement and all queued commands.
- Preserve absolute frame position (elapsed time), not musical quarter position
  across tempo edits. A shortened project clamps the cursor to its end. Loop end
  clamps to the project end; a loop whose start no longer exists is disabled.
  Playing at the new end without a valid loop becomes stopped. Paused remains
  paused. Empty replacements are handled without invalid loops.
- Transport transfer runs only on the audio owner, allocates nothing, and retains
  the existing one-pending/one-retired ownership and reclamation bounds.
- Seeking the prepared replacement resets its DSP and retriggers held notes.
  This does **not** migrate voice/effect state, retain delay tails, or crossfade
  waveform discontinuities. Those lifecycle requirements remain separate work.
- Offline/device reset continues to stop playback deliberately. Connecting an
  output never auto-plays. These semantics are unchanged by project publication.

## Verification

All **332** affected backend/module regression tests passed, zero failures
(`/tmp/flow-preserved-transport.log`). Eight new cases cover exact sample-position
transfer, queued commands, loop wrapping/clamping/removal, empty replacement,
pause, stop priority, zero allocation on installation, and the project coordinator
keeping playback active through an edit. Existing default-reset, concurrent
publication/retirement and mixer-preview tests still pass.

Full core gate passed: desktop solution build; **3,264** language/backend tests
and **21** MIDI tests, zero failures, 14 unexecuted/skipped main tests, no tracked
file mutations during verification. Evidence:
`/tmp/flow-preserved-transport-core/verification.json` and adjacent TRX/build logs.
This core tier excludes Platform and LongRunning tests; the affected run above
includes the host tests. Web publishing and physical hardware qualification were
not repeated for this step.

## Next

Continue public Flow plugin authoring beyond fixed catalog devices: declarative
metadata/ports/parameter handles and composable signal/voice primitives must
support the roadmap's synth, sampler and composed-effect examples without a C#
class per plugin. State/tail continuity, MIDI input/recording and integrated
project/export workflows remain open. JUI package readiness is established in
[the integration note](2026-10-04-jui-package.md); native frontend and historical
callback-gap diagnosis remain deferred.
