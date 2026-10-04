# Oscillator/filter primitives and compatibility refresh — 2026-10-04

P7-21 adds signal-driven oscillator and filter primitives. Instrument voices,
envelopes and full backend readiness remain open.

## Compatibility evidence before these two primitives

The accumulated plugin packages, native policy, instance values and atomic preview
passed Web-target shared backend/Phase47/48 tests: **289 passed, 7 skipped**, zero
failures (`/tmp/flow-plugin-web-refresh.log`). Actual WebAssembly publication passed
(`/tmp/flow-plugin-web-publish.log`), producing
`flow-lang/bin/Release/net10.0/browser-wasm/AppBundle`. Desktop solution restoration
and build then passed with zero errors (`/tmp/flow-plugin-refresh-desktop-build.log`).
This verifies target compilation/publication, not browser UI or physical devices.
The oscillator/filter changes below were added afterward and have Desktop evidence.

## Implemented

- `(dawSineOsc id frequency)` constructs `flow.sineOsc`. Each stereo input sample
  supplies that channel's frequency in Hz. Independent phase accumulators emit a
  unit sine before advancing. Frequency clamps to ±49% of sample rate; NaN means
  zero. Phase remains continuous across blocks. Reset/seek restarts phase at zero,
  matching the current DSP-reset contract rather than reconstructing prior phase.
- `(dawLowPass id input cutoff)` constructs a stereo one-pole processor. Each
  sample supplies cutoff in Hz. The pole is `exp(-2*pi*cutoff/sampleRate)` and state
  advances as `(1-pole)*input + pole*previous`. Cutoff clamps from 1 Hz to 49% of
  sample rate (at extremely low sample rates the lower bound equals the upper).
  NaN cutoff uses the lower bound; nonfinite audio input becomes zero so it cannot
  poison persistent state. Reset clears both channel states.
- The filter declares a **two-second truncated render tail**, accumulated through
  graph paths and subject to the existing 120-second graph-tail budget. This is
  an explicit finite render policy for an IIR, not a claim its response becomes
  mathematically zero after two seconds. Both new primitives have no bypass.
- Graph merging, JSON and executable Flow export support both primitives. Raw
  `flow.value` nodes accept public semantic units such as Hz in plugin metadata;
  their numbers are still broadcast explicitly. Unit checking remains strict for
  targets whose native device declares a specific unit. There is no general
  dimensional type inference between sample-stream connections.
- `examples/plugins/modulated-low-pass.flow` and its pinned `.flowplugin` compose
  a cutoff LFO from these primitives, multiplication and addition. Base cutoff,
  modulation depth and LFO rate are stable public Hz controls. The restricted
  isolated plugin worker builds the actual saved example successfully.

## Verification

All **359** affected backend/module tests passed, zero failures:
`/tmp/flow-oscillator-filter-regression.log`. Four new tests cover independent stereo
oscillator phase, block-size equivalence, reset reproducibility, the filter impulse
recurrence, tail metadata, reset, callback allocation, modulated signal graphs,
executable Flow export and the actual example package through the isolated worker.

The two module snapshots were deliberately updated for exactly two new public
signatures: native 691→693, reachable 685→687, `@flowDaw` 420→422. The same six
legacy unreachable signatures remain. `git diff --check` passed. Full core and
hardware gates were not repeated after adding these primitives.

## Next

Implement gate-driven envelopes and prepared per-voice graph instruments, then
required public Flow subtractive synth/sampler examples. Frequency modulation is
not oversampled/bandlimited by this sine primitive; do not promise alias-free
arbitrary modulation. This is not a complete instrument or resonant filter API.

Continue public automation/device instances/presets, asset capabilities, state/tail
transitions, MIDI input/recording and integrated workflows. Native JUI frontend and
historical callback-gap diagnosis remain deferred.
