# Public plugin automation playback — 2026-10-04

P7-33 completes the initial playback path for persisted public parameter curves,
removing P7-32's temporary preparation rejection.

## Implemented

ProjectCompiler resolves public output-binding/parameter IDs through pinned plugin
metadata, validates authored typed values and expands declared targets into concrete
graph automation. Structural controls reject; discrete public parameters require
step curves. Missing bindings/plugins/parameters reject. Expansion is capped at
256 public target lanes and 100,000 points; prepared graph limits additionally
apply to the combined public/internal lanes. Duplicate target ownership rejects
during graph preparation.

Master effect targets enter existing master graph automation. Instrument lanes
are lowered once per routed track with the same tempo-aware musical conversion,
then attached to each independent voice graph. A voice starts/resets its graph
clock at the absolute project frame while DSP state still resets normally. Thus a
late note, stolen voice or held-note seek reads the current project curve instead
of restarting automation from note age. Envelope/oscillator state remains local
to the note. No interpreter or per-sample allocations were introduced.

Prepared automation owns its targets, so live previews reject through the existing
validation path. Public committed-value commands also reject lanes owning that
public parameter ID. Step and linear playback use exact authored curves rather
than adding live-control smoothing. Stored instance values supply the initial value
before the first automation point. Pause freezes the project clock; seek/loop reset
uses the absolute destination frame under existing transport policy.

## Evidence

Nineteen focused voice/plugin tests passed (`/tmp/flow-public-automation-playback.log`).
New voice coverage checks a late onset at frame 50 receives 0.5 from a 0→1 curve
spanning frames 0→100, held seek at 75 receives 0.75, automated-target live rejection,
and zero allocations. The real instrument plugin integration test now adds a
public step curve, verifies exact half-level rendered audio at its project boundary,
rejects conflicting preview/commit, and verifies sample-identical save/reopen output.
Existing tempo-aware curve and transport tests remain in the regression suite.
`git diff --check` passed.

All **381** affected backend/module tests passed, zero failures
(`/tmp/flow-public-automation-playback-regression.log`). Full core/Web gates
were not repeated for this slice.

## Next

Broaden public automation coverage to multi-target effects, tempo changes and full
Flow export/audio round trips. Continue sampler graph primitives/examples, device
instances/presets, MIDI input/basic recording and integrated workflow readiness.
Native JUI frontend and historical callback-gap diagnosis remain deferred. Physical
callback qualification is still separate from these deterministic playback tests.
