# Fixed project migration fixtures — 2026-10-04

Added checked-in source fixtures under `flow-lang.Tests/fixtures/projects` for
schemas 1, 6, 11 and 12. These are hand-authored representative wire formats,
not claimed historical-release captures. Tests read the fixed files, never derive
them by modifying the current serializer output. The fixture README documents
their contents and limits.

Coverage verifies legacy implicit bus numbering, nonsequential explicit buses,
tempo/meter/context identity, asset references, bypassed effect references,
mute/solo, clip source windows/nudges and exact audio offsets above 2^53. Each file
is loaded without media access or source evaluation, reserialized, reconstructed
through evaluated Flow export, edited and undone/redone. Fixtures remain unchanged.
Missing sources/media are intentional repairable references. No playback or
historical plugin-package qualification is claimed by these fixtures.

Focused verification: **4 passed**, `/tmp/flow-migration-fixtures.log`.
The preceding production-code affected regression remains 517 passed from the
track-state handoff; it predates these four tests. No production code changed here.

Next remains saved project render preferences: add versioned sample-rate/render
settings and preserve them at every snapshot construction site (including source
acceptance, processing, automation replacement, plugin duplication, packaging and
Flow resource seeding). Make default offline export arguments use saved settings
while preserving explicit overrides; device-session constraints remain explicit.
Then run preference-preservation tests against these fixed migration inputs and
the affected backend suite. Continue remaining generation-context/capability and
integrated qualification work from the readiness audit afterward.
