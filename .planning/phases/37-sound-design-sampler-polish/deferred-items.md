# Phase 37 — Deferred Items (Out-of-Scope Discoveries)

## Pre-existing test failures discovered during Plan 37-01 execution

Verified at git ref `818e539` (Task 2 commit, BEFORE any Plan 37-01 Task 3
changes) — `dotnet test --no-build` reports **34 failed / 1525 passed / 26
skipped** across the full solution. None are caused by Plan 37-01's work
(stash-pop verification: same set of failures with and without Task 3 WIP).

### Phase 28 (synth articulation)
- `FlowLang.Tests.Unit.Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` — multiple parameterized rows fail across `piano / brass / sax / flute / bell / strings` × `Accent / Tenuto / Legato / Sforzando`. Appears to be FFT-cosine-similarity threshold drift on the post-Phase-29 sampled-instrument path.

### Phase 30 (FlowMidi quantizer)
- `FlowMidi.Tests.Unit.Phase30.QuantizerRoundingTests.Two_Octave_Range_Does_Not_Split_RH_LH`
- `FlowMidi.Tests.Unit.Phase30.FlowGeneratorStructureTests.One_Sequence_Per_Track_Channel_No_RH_LH_Suffix`

These match the open `flow-midi inner-voice polyphony follow-up` memory entry — known issue tracked for a future polyphony-split plan.

### Phase 35 (match exhaustiveness)
- `FlowLang.Tests.Phase35.MatchExhaustivenessDefaultTests.NonExhaustiveDefaultWarnsAndReturnsVoid`
- `FlowLang.Tests.Phase35.MatchExhaustivenessDefaultTests.WarnDedupedPerMatchSpan`

### Scope boundary applied (Plan 37-01)
Per the executor's SCOPE BOUNDARY rule ("Only auto-fix issues DIRECTLY caused
by the current task's changes"), these are NOT fixed in this plan. They should
be triaged by a dedicated cleanup plan (or rolled into the Phase 37 closer
Plan 37-07's regression-sweep work — D-37 closer scope) before phase gate.
