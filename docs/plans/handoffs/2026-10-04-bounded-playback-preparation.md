# Bounded playback preparation host — 2026-10-04

P6-23 supplies control-thread scheduling around project playback publication.
Backend readiness remains in progress.

## Implemented

`ProjectPlaybackPreparationHost` owns at most one running preparation and one
pending ticket. Request captures the current immutable document snapshot through
`ProjectPlaybackCoordinator`, replaces any older pending ticket, and cancels the
running preparation. The next build starts only after the running build exits;
there is no grace-period overlap or task chain proportional to the edit count.

The host update loop calls `Poll` on the control thread to accept completed work,
start the newest pending build, retry queue publication and observe audio-owner
acknowledgements. Workers resolve assets/prepare graphs but never mutate the
project or queue. Exceptions become preparation failures; old playback survives.
Even a worker that returns successfully after cancellation cannot publish output.

`DisposeAsync` cancels and joins work, discards pending requests and prevents
further Request/Poll calls. Repeated disposal joins the same shutdown. This is
cooperative preparation of bounded built-in graphs/assets, not an isolation
boundary for arbitrary user programs. Unresponsive IO can delay cancellation and
shutdown; it cannot cause overlapping builds. The host does not own audio output.

The public constructor uses the production project compiler/asset resolver. A
friend-assembly test hook permits deterministic scheduling/failure tests.

## Verification

All **281** affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting
regression tests passed (`/tmp/flow-preparation-host-regression.log`). After the
final shutdown-status adjustment, all eight focused publication/preparation tests
passed (`/tmp/flow-preparation-host-final.log`). New scheduling tests hold a worker while 100 edits
arrive, verify only the first and final snapshots are built with one worker at a
time, exercise failure followed by successful retry, and verify shutdown waits
for the running worker without starting pending work.

Existing analyzer warnings remain; `git diff --check` passed. No physical-device,
full-solution or Web publish validation was performed in this step.

## Next integration

Compose this host with isolated Flow generation/atomic source acceptance and the
Linux output-session owner. Preserve a single queue producer; only a joined device
allows control-thread offline consumption. The output session currently collects
retired transports while resetting an offline queue: this is serialized control
ownership and needs explicit integration coverage with publication acknowledgement.
Device disconnect/reconnect, offline publication and shutdown ordering are the
next host-level checks. Initial preparation still precedes device startup.
Native frontend and historical callback-gap diagnosis remain deferred.
