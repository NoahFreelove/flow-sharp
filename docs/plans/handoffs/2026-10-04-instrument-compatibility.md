# Instrument compatibility checkpoint — 2026-10-04

Verification checkpoint after P7-21–26: oscillators/filter, ADSR, independent graph
voices, project/Flow instrument persistence, event-delimited rendering and saw/
square/subtractive synth example. This is not a backend-completion claim.

## Verified current state

- Full core verifier: solution Desktop build passed; **3,305 language/backend +
  21 MIDI tests passed**, zero failures. Fourteen language tests were unexecuted/
  skipped. Tracked-content mutation audit was empty. Evidence:
  `/tmp/flow-instruments-core/verification.json`, build/test logs and TRX files in
  that directory. Command: `python3 scripts/ci/verify.py --tier core --artifacts
  /tmp/flow-instruments-core`.
- Web-target shared backend, music model, static binding, native invocation policy
  and Phase47/48 checks: **304 passed, 7 skipped**, zero failures.
  `/tmp/flow-instruments-web.log`. Platform and long-running tests were excluded.
- Actual WebAssembly publish passed: `/tmp/flow-instruments-wasm-publish.log`;
  generated `flow-lang/bin/Release/net10.0/browser-wasm/AppBundle`.
- Restored Desktop solution Release build passed: zero errors, 296 warnings,
  `/tmp/flow-instruments-desktop-restore.log`. Web/Desktop share intermediate
  target assets, so Desktop restoration followed the Web checks.

No browser UI, native JUI window, physical device, callback deadline or underrun
qualification is implied. Existing hardware gaps remain explicit and deferred.

## Next implementation

Plugin host currently accepts only AudioEffect manifests. Extend instrument-kind
packages through output validation, public parameter targets, project compilation
and source-instance values, while retaining isolated construction and no interpreter
on the callback. Instrument graphs use pitch/gate/velocity ports; these are not
external audio ports from the plugin manifest. Public controls must affect all
prepared voices coherently or require preparation explicitly.

Continue sampler graph support and examples, public automation/device instances/
presets, asset capabilities, MIDI input/basic recording and integrated workflows.
Native frontend remains deferred until backend readiness; the verified JUI NuGet
package remains the intended integration surface.
