# Acknowledged monitor activation — 2026-10-04

P9-09 carries monitor selection through bounded playback preparation and exposes
admission-only handles after audio acknowledgment. Native input fan-out remains next.

## Implemented

PlaybackPreparation captures MonitoredTrack alongside the immutable project snapshot.
ProjectPlaybackCoordinator.SelectMonitorTrack selects host state without editing the
project; ProjectPlaybackSession.SetMonitoredTrack also requests bounded preparation.
Ordinary edits/repreparation preserve selection. Removing the selected track clears
selection and closes its endpoint. Superseded selections invalidate outstanding/ready
build tickets, so a result for an old selection cannot become the offered endpoint.

TryGetMonitor returns a PlaybackMonitorEndpoint only for the selected, acknowledged
source generation. The handle exposes track/generation, MIDI admission and bounded
fault/drop/stealing diagnostics, not DSP cursors or graphs. One input producer owns
TryWrite. Each call checks current generation, pending replacement and closure.
A publication racing an admitted write can only reach the old instrument; it cannot
redirect that packet to a different track. Retained old handles cannot close a newer
monitor because they refer to their own prepared source.

Preparing or failing a same-selection build keeps the last working monitor. Successful
publication closes the old handle; the replacement is unavailable until acknowledged.
A selection change closes admission immediately. Closing during pending publication
also closes the pending source, preventing it from reactivating on a later boundary.
Explicit reprepare supplies a fresh instance after closure or queue overflow.

Closure disables the prepared graph's stopped/paused audition path as well as its
live MIDI admission. This matters when a Flow mixer can generate sound independently
of MIDI input. During ordinary playing, clips and shared effect tails still render.
A closed instance cannot be reopened; the worker prepares a replacement. Selection
and all activation/closure operations leave project history unchanged.

ProjectPlaybackSession closes endpoints on disconnect, output failure and shutdown.
Offline preparation does not offer usable input handles. Explicit Connect schedules
fresh preparation when a selected monitor has been closed, then waits for ordinary
audio acknowledgment. Connecting does not start arrangement transport.

## Evidence

All **443** affected backend, platform, music, hosting and module tests passed,
zero failures (`/tmp/flow-monitor-activation-regression.log`). A final additional
track-switch assertion passed with all four focused activation tests
(`/tmp/flow-monitor-track-switch.log`). `git diff --check` passed.

Coverage includes no admission before acknowledgment; stale handles after replacement;
a real change to a different track; old close not affecting the new track; failed-build
fallback; invalidated old-selection results; closure during pending publication;
reprepare recovery; selected-track deletion; offline selection; disconnect/reconnect;
and silencing a self-generating mixer while stopped. Native hardware was not opened.
The full core/Web publication checkpoint in the preceding routing handoff predates
this activation slice; these figures are the current affected-suite evidence.

## Next: shared input owner

Connect native MIDI to these acknowledged handles while retaining the existing
recording capture path. Prefer one native input owner that fans packets out to the
monitor and recorder; do not open independent polling ports for the same keyboard.
Subscriber removal must join any already-entered callback before a recording take can
complete. Poll device/monitor errors, close retired endpoints, and distinguish monitor
queue loss from recording-input loss. Replacement resets held monitor notes rather
than migrating active DSP state. No input subscription may use unacknowledged monitors.

ProjectMidiRecordingHost currently opens its own input through the injected factory;
its validation, arming buffer, frame mapping, captured take and join-before-complete
contracts remain intact. The new owner should compose/refactor that lifecycle rather
than bypassing it. Native input parsing and fan-out must remain outside audio callbacks.

Device/preset workflows, integrated export/recovery and hardware qualification remain
open. Native JUI frontend and historical callback-gap investigation are deferred.
The overall backend goal remains active.
