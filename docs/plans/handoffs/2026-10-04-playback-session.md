# DAW playback session and offline publication — 2026-10-04

P6-24 composes project playback preparation and Linux output lifecycle. The full
backend-readiness goal remains active.

## Implemented

- New `flow-studio-host` composition library references the engine and Linux adapter.
  The project/music/audio layers remain independent of the platform. The solution
  includes the new library; only desktop tests reference it.
- `ProjectPlaybackSession` owns the preparation host and output session. The host
  creates it with a valid routed project, requests preparation after document edits
  or accepted generation, and calls Poll from its single control/update owner.
  The same pattern handles undo/redo without changing document history itself.
- Offline output polling now settles stop and replacement commands, allowing edits
  to become the acknowledged active snapshot without opening hardware. It consumes
  the queue only when no stream remains owned. A faulted state alone is insufficient:
  a failed close may retain callbacks and must not permit a second consumer.
- Connect/disconnect synchronize publication acknowledgement. Reconnect uses the
  latest accepted prepared project, stopped at zero; no automatic playback resume.
  Device failures stay visible even if project preparation succeeds while offline.
- Disposal closes/joins audio before cancelling/joining preparation. Failed device
  closure leaves the session owned and usable for explicit retry; it does not claim
  successful shutdown or open another device. After successful shutdown, repeated
  disposal is safe and new session work is rejected.
- The output-session offline reset and coordinator retirement collection share one
  control owner. No additional callback locks or worker access to the queue are added.

## Verification

All 19 focused playback/output/preparation tests passed:
`/tmp/flow-playback-session.log`.

All **284** affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting
regression tests passed, 0 failed (`/tmp/flow-playback-session-regression.log`).
`git diff --check` passed. No full-solution run or Web publish was performed.

Three new integrated tests cover offline move/undo preparation, active-snapshot
acknowledgement, reconnect with actual sample assertions, retained callback ownership
on failed close, retryable shutdown, and device-failure status during offline edits.
Device behavior is simulated; these checks do not qualify physical unplug recovery
or real-device latency. Existing analyzer/package warnings remain.

## Next

Connect isolated Flow generation to this host's source acceptance and preparation
requests; add end-to-end generated-source editing/save/reopen/playback checks.
The initial project still needs an explicit graph/routing before session creation.
A convenient new-project factory, unified frontend diagnostics/meters, real MIDI
input/recording and the remaining Flow device/plugin contracts remain open.
Native frontend and historical callback-gap investigation remain deferred.
