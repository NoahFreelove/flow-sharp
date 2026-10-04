# Project migration fixtures

These fixed, hand-authored wire-format examples are read directly by
`ProjectMigrationFixtureTests`; tests must not regenerate them from the current
serializer. They are representative schema examples, not files captured from
historical releases.

- v1 omits assets, automation, explicit bus numbers, effects and track state.
- v6 stores stable nonsequential buses and external-asset metadata.
- v11 adds a bypassed track insert reference.
- v12 stores mute/solo state.

Each contains tempo/meter changes, linked source windows, positive/negative nudge
and an audio frame offset above the exact-integer range of double. Sources and
the external WAVE are intentionally unavailable: these files establish migration
and repairable-reference preservation without evaluating code or reading assets.
They do not qualify playback, plugin-package migrations or every intermediate
schema feature. Existing dedicated plugin/automation tests remain necessary.
