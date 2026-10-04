# Saved track colors — 2026-10-04

`ProjectTrack.ColorRgb` stores an optional opaque sRGB integer from 0 to 0xFFFFFF.
Null means automatic frontend color. `ProjectTrackCommands.SetColor` is a captured
undoable edit; equal values do not add history and invalid values fail atomically.
Project schema 17 persists it. Older projects default to null and reject nondefault
colors under an older version tag. Routing construction validates the range.

`dawTrackColor(track, rgb)` provides equivalent Flow construction, using -1 for
automatic color. Project Flow export preserves it without changing graph/source
ownership. Existing track transformations retain metadata through record copies.
The API snapshots now record 730 registrations and 724 reachable signatures;
the six historical unreachable signatures are unchanged.

Verification: focused persistence/migration/Flow reconstruction checks **24 passed**;
affected backend, platform, hosting, music-model and module-surface regression
**579 passed**, zero failures/skips (`/tmp/flow-track-color-regression.log`). Tests
cover undo/redo/no-op, invalid edits, old-file defaults, version rejection and
unchanged routing/audio. The existing envelope migration assertion now edits the
JSON version structurally instead of depending on the current serialized version.

The preceding full-core checkpoint passed 3,541 tests with 14 skips; it predates
this change. Web/WASM refresh, final Desktop qualification and the requirements
audit remain required. Native frontend/hardware gates are not closed by these tests.

Cross-target follow-up: Web-target shared backend/music-model, Phase47/48,
native-policy and static-binding checks passed **449 tests**, 7 skipped, zero
failures (`/tmp/flow-backend-web-final.log`). Actual WebAssembly publication
succeeded and emitted the AppBundle (`/tmp/flow-backend-wasm-final.log`). The
Desktop Release solution was then restored and built successfully
(`/tmp/flow-backend-desktop-final.log`). These are build/test checks, not browser
UI interaction or native JUI qualification. Final full-core/audit work remains.
