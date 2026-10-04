# Captured generator maps and identity — 2026-10-04

Flow DAW builds install four pure context APIs before evaluating user source:

- `dawContextInfo`: `Dict<String, String>` with sourceId, sourceRevision,
  contextRevision, seed and timeLimitTicks (declared TimeSpan ticks).
- `dawTempoMap`: `Dict<Double, Double>` from quarter position to quarter-note BPM.
- `dawMeterNumerators` / `dawMeterDenominators`: `Dict<Int, Int>` keyed by one-based bar.

Decimal strings preserve exact integer revisions. Each fresh worker engine captures
immutable dictionaries; changing a returned dictionary cannot alter later reads.
Outside a DAW build these APIs fail explicitly. Packaged plugins receive them
through the reviewed native catalog. The legacy numeric context argument remains.
The existing process protocol already carries full maps and integer revisions;
no protocol bump is needed. Budget transport remains double milliseconds, so this
does not add a guarantee of arbitrary submillisecond transport precision.

This closes map/identity exposure only. Project tuning snapshots, host-owned asset
resolution and ordinary-generator native/module restrictions remain required.
Ordinary generators are still unrestricted; process isolation is not an OS sandbox.
Capability review must account for hidden IO: StyleRegistry reads user style files,
and broad music registrars include playback, sample-file, notation-file and device
operations. Keep general-purpose FlowEngine unchanged while introducing a reviewed
DAW signature set and bundled/pinned module resolution. Preserve promised musical
construction, transformations, patterns and offline rendering when tightening access.

Focused context/API checks: 10 passed. Final affected backend/API checks: **525
passed**, zero failures/skips, `/tmp/flow-generator-context-regression.log`.
Tests cover direct and real isolated builds, revisions above 2^53, complete maps,
immutable dictionaries and explicit unavailability outside a build. Full-core,
browser and hardware qualification remain pending.

Next: tuning/assets and ordinary-generator capabilities, then integrated lifecycle
and full verification from the readiness audit. Native UI and historical callback
gap investigation remain deferred.
