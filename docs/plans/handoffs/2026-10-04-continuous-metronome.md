# Continuous metronome — 2026-10-04

`PreparedMetronome` now follows captured tempo/meter maps on the live transport,
including seek, loop and recording beyond an empty project's end. It uses the
same Flow-authored instrument graph as counted recording, with denominator beats
and accented bar starts. Tests compare its samples with the finite click schedule
and verify allocation-free rendering, paused silence and playback beyond EOF.

`ProjectPlaybackSession.SetMetronome` prepares and publishes the setting through
the existing stale-safe coordinator. Clicks mix after the master graph, do not
extend project duration and are absent from ordinary offline compilation. This is
a host setting, default off, not a persisted project edit or undo action. Toggle
before arming or after finishing a take: changing prepared playback during a take
is a recording discontinuity and is rejected. Compound meters click each
denominator beat (six clicks per 6/8 bar).

Affected backend, platform, hosting, music-model and module-surface regression:
**576 passed**, zero failures/skips, `/tmp/flow-metronome-regression.log`.
These checks do not establish hardware latency or final full-core compatibility.

Next: executable captured-sample example, documentation reconciliation, current
browser/WASM and full-core verification, then requirement-by-requirement readiness
audit. Native UI and historical callback-gap diagnosis remain deferred.
