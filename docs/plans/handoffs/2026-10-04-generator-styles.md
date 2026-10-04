# Captured DAW improvisation styles — 2026-10-04

Ordinary DAW generators can now use `@improv` registerStyle/listStyles/jam.
`GeneratorStyles` evaluates the three embedded baseline packs in a private,
native-policy-restricted engine with the bundled-module resolver and the build's
cancellation token. It copies only registered style data into the generator.
Both style registries are marked explicitly initialized, so native calls do not
enter standalone Flow's implicit shipped/user directory discovery path.

Baseline jazz/blues/classical packs are now embedded on Desktop as well as Web.
In-source registerStyle remains available and overrides the captured baseline.
This does not enable arbitrary style-file imports or native file/device access.
Packaged plugin native policy remains narrower. Ordinary standalone Flow keeps
its existing style discovery behavior; no user files were modified.

## Verification

- Focused native-policy suite: **5 passed**, `/tmp/flow-generator-styles-final.log`.
- Backend, module-surface and existing Phase36 regressions: **742 passed**, zero
  failures/skips, `/tmp/flow-generator-styles-regression.log`.
- Seeded jam produces identical note pitches in repeat direct builds and the real
  isolated worker. Inline style registration succeeds. An extra style pack placed
  temporarily in the runtime style directory is not discovered; only the three
  baseline styles initialize, followed by the explicit inline style. Test cleans
  up its file. Existing denied file/device calls still fail.
- Existing standalone improvisation/style tests pass in the broader suite.

## Continue

Review remaining in-memory authoring capabilities and document the supported
sample path. `sing` is an in-memory formant synthesizer but shares its family with
external `tts`/`setTtsCommand`; review admitting only its context-dependent registrar.
Legacy named sampled instruments still deliberately reject implicit disk access;
DAW sample grants and captured sampler graphs already exist. Compare against MVP
requirements rather than implicitly enabling ambient sample files.

Then reconcile historical roadmap/audit rows and run final core/browser/WASM
qualification. This change adds embedded resource bytes and needs the Web budget
gate checked. Frontend and historical callback-gap diagnosis remain deferred;
physical device/latency qualification is still open.
