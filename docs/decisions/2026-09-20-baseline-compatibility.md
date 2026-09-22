# Baseline compatibility decisions

Status: accepted for the restructuring baseline, 2026-09-20.

The Phase 0 baseline preserves intentional current runtime behavior. These
choices resolve older tests before changing them; they introduce no new language
or converter semantics.

## Unknown vocal phonemes

`FormantData.GetFormants` returns the neutral `ah` formants for an unknown
phoneme and emits an advisory once per phoneme through `RenderingDiagnostics`.
Canonical `ah`, `ee`, `eh`, `oh`, `oo` values remain unchanged. This follows the
intentional charitable change recorded in `.planning/quick/260701-vx4-*` and
`.planning/STATE.md`, and the implementation's remarks. The old exact-exception
test predates that change. Replace it with fallback and per-phoneme dedup checks;
do not reintroduce a render-stopping exception. Diagnostic dedup is currently
process-global, a known Phase 2 migration target.

## MIDI import tracks and voices

Preserve current `Quantizer` behavior: split source tracks by channel, split
melodic material at middle C (MIDI 60 belongs to the right hand), then allocate
non-overlapping voices across the whole track. Keep drum-channel material in
its separate unsplit track. The output may contain multiple sequences per
source track/channel. Preserve notes, timing, velocity, channel identity, and
stable voice identity rather than promising one emitted sequence per input track.

Evidence: `.planning/HANDOFF-2026-06-26-midi2flow-and-flow-site.md`, section
"How conversion works", and `Quantizer.EmitTrackAsVoices` /
`AllocateVoicesTrackWide`. The earlier Phase 30 no-splitting assertions and
comment describe a superseded contract. Update those tests to verify both
emitted structure and note/timing preservation, not only a changed count.

A future option to retain source-track grouping is reasonable but is not
required for this baseline. It must address overlapping voices explicitly and
must not silently replace the current default during architectural extraction.

## Portable showcase audio fixture

Fresh-checkout validation exposed a separate fixture dependency: the showcase
uses an unseeded `granular` call whose `PrngRegistry` seed includes the source
file name. The old RMS test supplied the absolute checkout path. The same
production code therefore passed in the original checkout and failed in
`/tmp/flow-phase0/checkout` (2.90 dB deviation in the 200–300 ms window).

Use the fixed logical name `examples/edm/pulse.flow` when executing this fixture.
This changes no production seed derivation or example source. Refresh only the
showcase WAV, retain the 0.5 dB tolerance, and also require byte-identical repeated
renders. WAV and MIDI test outputs both use unique temporary paths.
Missing showcase baselines now fail; deliberate refreshes require
`FLOW_UPDATE_AUDIO_BASELINES=1` with the showcase test filter.

The reviewed baseline comparison preserved 2,763,521 frames, stereo layout,
44.1 kHz rate and 16-bit PCM. Differences end at frame 154,366 (3.50036 seconds),
consistent with the granular riser/reverb; the remaining roughly 59 seconds
are byte-identical. This is a test source-identity normalization, not an
unexplained renderer baseline replacement.

- Previous SHA-256: `01312cd5c12b324ba52935bf81e4150bad6c009a5f30b4de55cdf79541e43e77`.
- Canonical-name SHA-256: `f19e650bb359b245dbc954c36ad45d9481b8ddbeda9c552970e9289dcef4a079`.
