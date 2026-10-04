# Host-managed generator sample assets — 2026-10-04

`GeneratorAssets` captures immutable PCM under project GUIDs (at most 32 assets,
8 MiB decoded total). `dawAssetSample(String)` returns the supplied DawAudio value;
unknown IDs fail without attempting a path lookup. The existing three-argument
`dawProjectSample` project reconstruction API is unchanged. Assets can be returned
as audio or used by existing sample-graph APIs.

`GeneratorBuildRequest.Assets` carries the detached set. Process request/reply
protocol is now version 5; older workers fail version checks. The existing total
16 Mi-character request bound also applies, so sufficiently large code plus asset
payload may be rejected even below the decoded-audio limit. PCM uses the existing
generated-content codec; no disk paths enter the worker asset API.

`ProjectGeneratorHost.RequestBuildWithAssets` captures selected IDs and the project
snapshot on its control owner. Selection/count/decoded-budget validation occurs
before enqueueing. The worker resolves only selected project audio references,
verifies their recorded hashes/formats and supplies captured PCM to the isolated
process. Missing/changed files fail the build instead of producing silent samples.
Acceptance additionally checks document ChangeVersion when assets were requested,
including edit/undo ABA changes. Failure/staleness preserves the last accepted source.

Asset selection is explicit on each host build request; it is not inferred by
executing or parsing source. This does not yet persist per-source asset grants or
map legacy named sample instruments/styles to those assets. Those integrations and
tuning context remain required authoring work; do not mark the whole context gap
closed. General Flow file-loading defaults are unchanged.

Focused assets, host, process and API checks: **17 passed**,
`/tmp/flow-generator-assets-tests.log`. Coverage includes exact direct/isolated PCM,
identity/count validation, missing grants, hash change rejection, accepted output,
and stale edit/undo rejection. Initial attempt collided with the existing
`dawProjectSample` overload; the getter was renamed before final verification.

Final affected backend/API regression: **537 passed**, zero failures/skips,
`/tmp/flow-generator-assets-regression.log`. `git diff --check` passed. Full-core,
browser/publish and hardware checks remain pending.

Next: tuning snapshot and remaining asset/style authoring contracts, then integrated
lifecycle and full core/browser verification. Native UI and historical callback-gap
diagnosis remain deferred; no physical audio/MIDI device qualification is implied.
