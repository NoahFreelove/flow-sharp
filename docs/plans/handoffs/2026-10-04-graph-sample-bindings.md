# Graph instrument sample bindings — 2026-10-04

P7-35 adds immutable assets and saved-content support for the sample graph kernel.
Public Flow construction/export remains the next required sampler step.

## Implemented

GraphSampleSet copies 1–256 unique integer slot bindings in 0–255, references
immutable nonempty PcmAsset values, and caps serialized slot payloads at 16 MiB.
Aliases count once per serialized slot for interchange budgeting. SineVoiceSettings
accepts GraphSamples alongside VoiceGraph; validation requires the exact set of
referenced sample slots (missing and unused bindings reject). Every prepared voice
receives the same immutable asset map/PCM references, with independent reader state.

GeneratedContentJson schema 4 stores graph instrument samples as slot/sample-rate/
little-endian float PCM records. Readers retain schemas 1–3; earlier versions cannot
carry graph sample bindings. Payload validation rejects wrong instrument roles,
missing/nonfinite/malformed PCM, duplicate/out-of-range slots and unmatched graph
slots. Existing generated-result 16 MiB aggregate inline-audio limits now include
graph sample sets. Arrangement preparation includes graph samples in the existing
aggregate decoded-asset budget, deduplicated by PCM object identity across tracks.

Public plugin numeric parameters cannot target sample resource slot IDs. Resource
selection needs an asset operation, not a numeric automation knob. Other supported
node parameters retain their existing metadata rules.

## Evidence

Six focused sample/instrument persistence tests passed (`/tmp/flow-sample-bindings.log`).
New coverage checks caller dictionary mutation cannot change bindings, identical
serialized reconstruction, sample-identical playback after decode, missing bindings,
invalid/duplicate slots and rejection of graph assets under old content versions.
The graph-instrument version rejection test was updated for current schema 4.

All **385** affected backend/module regressions passed with zero failures
(`/tmp/flow-sample-binding-regression.log`). `git diff --check` passed. Full core/
Web gates were not repeated for this slice.

## Next

Add Flow sample-node construction, a typed asset-map instrument constructor and
project asset access for executable export; retain direct graph export's explicit
failure until that wiring is complete. Then supply reusable sampler/drum examples
through the restricted worker. Continue MIDI input/basic recording and remaining
backend workflow work. Native JUI frontend and historical gap diagnosis remain
deferred; no device qualification is implied.
