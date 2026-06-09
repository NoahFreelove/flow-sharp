---
phase: quick-260608-wcy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs
  - flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs
  - flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs
  - flow-lang.Tests/baselines/Phase41/showcase.wav
autonomous: true
requirements: [SOUND-DESIGN-BLEP-01]
must_haves:
  truths:
    - "Saw and square oscillators are band-limited via PolyBLEP — folded aliasing energy above Nyquist is gone on low notes"
    - "Sine and triangle oscillators render byte-identical to before (untouched)"
    - "Saw and square still clear the >=20% harmonic-richness floor (legit sub-Nyquist harmonics remain)"
    - "Two consecutive renders of the saw-based showcase are byte-identical (PolyBLEP is deterministic float math)"
    - "The full xUnit suite ends GREEN; the Phase46 saw/square byte guards are regenerated to the new band-limited contract and documented as expected"
    - "Both FlowTarget=Desktop and FlowTarget=Web builds stay green"
  artifacts:
    - path: "flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs"
      provides: "PolyBLEP-corrected SawSynthesizer + SquareSynthesizer render loops"
      contains: "PolyBlep"
    - path: "flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs"
      provides: "Regenerated saw/square byte-exact oracle mirroring the PolyBLEP math; sine/triangle oracles unchanged"
      contains: "PolyBlep"
    - path: "flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs"
      provides: "New saw + square >=20%-floor assertion proving band-limiting preserves harmonic richness"
      contains: "Saw"
    - path: "flow-lang.Tests/baselines/Phase41/showcase.wav"
      provides: "Re-rendered EDM showcase baseline (uses renderSong ... \"saw\") — benefits from the cleaner spectrum"
  key_links:
    - from: "SawSynthesizer.RenderNote"
      to: "PolyBLEP residual"
      via: "dt = frequency / sampleRate; correction at phase wrap"
      pattern: "PolyBlep"
    - from: "SquareSynthesizer.RenderNote"
      to: "PolyBLEP residual"
      via: "naiveSquare + blep(phase) - blep((phase+0.5)%1)"
      pattern: "PolyBlep"
    - from: "Phase41ShowcaseRmsTests"
      to: "examples/edm/pulse.flow"
      via: "renderSong song \"saw\" -> showcase.wav baseline"
      pattern: "renderSong"
---

<objective>
Band-limit the `saw` and `square` oscillators in `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` with PolyBLEP to eliminate aliasing. The naive ramp (`2.0*phase-1.0`) and step (`phase<0.5?1:-1`) oscillators alias badly on low notes — audible as harsh "corruption" when bassy saws stack (confirmed via WAV analysis of `examples/edm/pulse.flow`: peak 34% FS, so NOT clipping — it's non-band-limited oscillator aliasing). This pulls forward the v1.6 "Sound Design 2.0" item D-37-09.

Purpose: A cleaner, professional oscillator spectrum is core to Flow's value (faithfully translate notation into correct, playable audio). EDM/synth genres lean hardest on saw + square — those are exactly the waveforms that alias worst.
Output: PolyBLEP saw + square in core flow-lang (present on Desktop AND Web targets); regenerated Phase46 byte guards; a new Phase29 harmonic-richness floor assertion for saw + square; a re-rendered Phase41 showcase baseline.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

# The four files this plan changes plus the two it must keep green:
@flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs
@flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs
@flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs
@flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs

<interfaces>
<!-- Contracts the executor needs — already verified against the codebase. -->

NoteSynthesizer.cs — the two loops to rewrite (verbatim current math):
- SawSynthesizer (lines 76-82):
    for i: t = i/(double)sampleRate; phase = (frequency * t) % 1.0;
           sample = (float)(amplitude * (2.0 * phase - 1.0));   // amplitude = 0.2 * note.Velocity
- SquareSynthesizer (lines 108-114):
    for i: t = i/(double)sampleRate; phase = (frequency * t) % 1.0;
           sample = (float)(amplitude * (phase < 0.5 ? 1.0 : -1.0));  // amplitude = 0.2 * note.Velocity

CRITICAL determinism comment (lines 73-75 / 105-107): the absolute-time `(frequency * t) % 1.0`
phase formula is the byte contract. KEEP IT. Derive the BLEP step width from `dt = frequency / sampleRate`.
Sine (lines 45-50) + Triangle (lines 141-147) loops stay verbatim — DO NOT TOUCH.

PolyBLEP residual (standard, e.g. Välimäki/Pekonen) — the function to add as a private static helper:
    // t in [0,1) is the current phase; dt is the per-sample phase increment (frequency/sampleRate).
    // Returns the residual to SUBTRACT from a discontinuity that jumps +1 (downward saw reset / square fall).
    static double PolyBlep(double t, double dt) {
        if (t < dt)            { t = t / dt;       return t + t - t * t - 1.0; }   // start of period
        else if (t > 1.0 - dt) { t = (t - 1.0)/dt; return t * t + t + t + 1.0; }   // end of period
        else                   return 0.0;
    }

Phase46 byte-guard oracle — ExpectedSaw()/ExpectedSquare() (lines 107-133) mirror the OLD math
element-for-element. They MUST be rewritten to mirror the NEW PolyBLEP math (same fixed inputs:
A4=440Hz, 44100, 1.0 beat, 120 bpm, velocity 0.63, 22050 samples). ExpectedSine()/ExpectedTriangle()
stay unchanged. The class doc header (lines 9-57) references the Plan 46-06 D-03 redirect rationale —
update the saw/square portions to note the Phase-quick band-limiting regeneration; leave sine/triangle wording.

Phase29 HarmonicRichnessRatio helper (flow-lang.Tests/Helpers/Phase29Fft.cs):
    double Phase29Fft.HarmonicRichnessRatio(AudioBuffer buffer, double fundamentalHz)
    // = Sum E(k*f0 for k in 2..8, below Nyquist) / E(f0). Skips partials >= Nyquist (won't count alias bins).
Reuse it directly on a saw/square render. A4 = 440 Hz, C4 = 261.63 Hz (12-TET).
The existing GAIN_THRESHOLD pattern compares to a pinned baseline; for saw/square use the RAW ratio
(no Phase28 baseline exists for these) and assert ratio >= a documented floor that proves rich harmonics survive.

Phase41 showcase baseline regeneration: examples/edm/pulse.flow does `(renderSong song "saw")`.
Render command: dotnet run --project flow-cli -- run examples/edm/pulse.flow  -> /tmp/pulse.wav
Then copy /tmp/pulse.wav over flow-lang.Tests/baselines/Phase41/showcase.wav.
Phase41ShowcaseRmsTests reads examples/edm/pulse.flow live and pins to that baseline (SPEC-8 +-0.5dB/100ms).
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: PolyBLEP-band-limit the saw + square oscillators (sine/triangle untouched)</name>
  <files>flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs</files>
  <behavior>
    - Add a `private static double PolyBlep(double t, double dt)` helper (the standard two-branch residual from the interfaces block) somewhere reachable by both Saw and Square synth classes (e.g. a small `internal static class BlepOscillator` in NoteSynthesizer.cs, or a private static on each class — choose one site, no duplication).
    - SawSynthesizer: keep `phase = (frequency * t) % 1.0` and `dt = frequency / (double)sampleRate`. Compute `naive = 2.0 * phase - 1.0`, then `value = naive - PolyBlep(phase, dt)` (the saw has ONE +1 discontinuity at the 0/1 wrap; subtract the residual). `sample = (float)(amplitude * value)`. amplitude stays `0.2 * note.Velocity`.
    - SquareSynthesizer: keep `phase = (frequency * t) % 1.0` and `dt = frequency / (double)sampleRate`. Compute `naive = phase < 0.5 ? 1.0 : -1.0`, then the standard band-limited square: `value = naive + PolyBlep(phase, dt) - PolyBlep((phase + 0.5) % 1.0, dt)` (a +BLEP at the 0->1 rising wrap and a -BLEP at the 0.5 falling edge). `sample = (float)(amplitude * value)`. amplitude stays `0.2 * note.Velocity`.
    - SineSynthesizer + TriangleSynthesizer render loops: UNCHANGED, byte-identical (sine is pure; triangle's 1/n^2 rolloff barely aliases and needs polyBLAMP, out of scope).
    - The absolute-time `(frequency * t) % 1.0` formula is preserved — only the per-sample value computation gains the BLEP correction. No RNG, no time-of-day, no incremental phase accumulator — PolyBLEP stays deterministic float math (two-run cmp-clean intact).
    - Update the D-03 FALLBACK comments on the saw/square loops to note the band-limiting change; leave the sine/triangle comments verbatim.
  </behavior>
  <action>Rewrite ONLY the SawSynthesizer and SquareSynthesizer `RenderNote` inner loops per the behavior block, adding the shared `PolyBlep` helper. Keep `dt = frequency / sampleRate` as the residual width. Saw subtracts one BLEP at the wrap; square is `naiveSquare + blep(phase) - blep((phase+0.5)%1)`. Do not touch SineSynthesizer or TriangleSynthesizer. Per D-37-09 (Sound Design 2.0, pulled forward). Keep amplitude scalars (0.2*velocity for both) exactly as-is so only the spectral shape changes, not gross level.</action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet build flow-lang -p:FlowTarget=Desktop 2>&1 | grep -qE "Build succeeded|0 Error|Build succeeded." && dotnet build flow-lang -p:FlowTarget=Web 2>&1 | grep -qE "Build succeeded|0 Error|Build succeeded." && echo BUILD_OK_BOTH_TARGETS</automated>
  </verify>
  <done>Both Desktop and Web builds of flow-lang compile clean (oscillators are core, present on both targets). SawSynthesizer + SquareSynthesizer route through `PolyBlep`; SineSynthesizer + TriangleSynthesizer loops are textually unchanged. The `(frequency * t) % 1.0` absolute-time phase formula is preserved in both modified synths.</done>
</task>

<task type="auto">
  <name>Task 2: Regenerate the Phase46 saw/square byte guards; add the Phase29 saw+square harmonic-richness floor</name>
  <files>flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs, flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs</files>
  <action>
Two independent test edits in one task (both are pure-test, no production change):

(a) NoteSynthesizerByteGuardTests.cs — rewrite `ExpectedSaw()` and `ExpectedSquare()` so the in-test oracle mirrors the NEW PolyBLEP arithmetic element-for-element (same fixed inputs: A4 440 Hz, 44100, 1.0 beat, 120 bpm, velocity 0.63 -> 22050 samples; saw amplitude 0.2*v, square amplitude 0.2*v). Replicate the exact `PolyBlep` residual + the saw `naive - blep(phase)` and square `naive + blep(phase) - blep((phase+0.5)%1)` expressions so the oracle is byte-identical to the production loop by construction (this is the intended regeneration — band-limiting LEGITIMATELY changes these bytes per the test's own documented FALLBACK note). Leave `ExpectedSine()` and `ExpectedTriangle()` UNCHANGED (sine/triangle untouched in Task 1). Update the saw/square portions of the class doc header to record that the band-limiting regeneration is expected and intentional; keep the sine/triangle wording. Do NOT loosen the exact bit-pattern compare — it stays element-wise BitConverter-exact.

(b) HarmonicRichnessTests.cs — add two new `[Fact]`s, `Saw_HarmonicRichness_ClearsFloor_AfterBandLimiting` and `Square_HarmonicRichness_ClearsFloor_AfterBandLimiting`. Each: render a single note via `SynthesizerFactory.Create("saw"|"square")` at A4 (or C4 — pick one and pin the fundamental: A4=440.0, C4=261.63) over the existing 2.0-beat/120-bpm/44100 convention, then call `Phase29Fft.HarmonicRichnessRatio(buf, fundamentalHz)` and assert the raw ratio is comfortably above the >=20% floor (assert `ratio >= 0.20` at minimum; band-limiting only removes folded energy ABOVE Nyquist, so legit sub-Nyquist harmonics keep saw/square far above 0.20 — the assertion MEASURES this, it does not assume it). Emit the measured ratio in the assertion message for the summary. These are NEW assertions (the existing Phase29 facts only cover drums/organ/wavetable); do not modify the existing facts.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet test --filter "FullyQualifiedName~NoteSynthesizerByteGuard" 2>&1 | grep -qE "Passed!|Failed: *0" && dotnet test --filter "FullyQualifiedName~HarmonicRichness" 2>&1 | grep -qE "Passed!|Failed: *0" && echo BYTEGUARD_AND_RICHNESS_GREEN</automated>
  </verify>
  <done>Phase46 byte guards pass against the new band-limited saw/square output (sine/triangle facts still green, unchanged). The two new Phase29 saw/square harmonic-richness facts pass with measured ratio >= 0.20 (printed in the assertion message). No existing Phase29 fact modified. No production code touched in this task.</done>
</task>

<task type="auto">
  <name>Task 3: Re-render the showcase baseline, re-pin Phase41, prove two-run determinism, full suite green</name>
  <files>flow-lang.Tests/baselines/Phase41/showcase.wav</files>
  <action>
Determinism + baseline regeneration + full-suite sweep:

1. Two-run cmp-clean proof: render `examples/edm/pulse.flow` TWICE and confirm byte-identical WAV (PolyBLEP is deterministic). Use the existing harness `bash scripts/test_two_run_determinism.sh examples/edm/pulse.flow` (it reads the script's writeWav target -> /tmp/pulse.wav, renders twice, compares SHA-256, exits 0 iff identical). If the harness's default render command does not resolve, fall back to two manual `dotnet run --project flow-cli -- run examples/edm/pulse.flow` invocations and `sha256sum /tmp/pulse.wav` after each.

2. Regenerate the Phase41 showcase baseline (it uses `(renderSong song "saw")` — it benefits directly from the cleaner spectrum, so its RMS WILL shift beyond +-0.5dB/100ms; this is EXPECTED): render once via `dotnet run --project flow-cli -- run examples/edm/pulse.flow` then copy `/tmp/pulse.wav` over `flow-lang.Tests/baselines/Phase41/showcase.wav` (overwrite). The re-pinned baseline is the new band-limited render.

3. Full xUnit sweep. Run `dotnet test` and confirm GREEN. EXPECTED baseline scope (verified during planning): ONLY the Phase41 showcase baseline exercises saw/square — Phase28 RMS baselines render `"sine"`, Phase37 render `"piano"`, Phase45 render `"brass"`/`"flute"` (none touch saw/square), so they should NOT shift. If, contrary to expectation, any OTHER RMS-windowed baseline (Phase28/37/45) exceeds +-0.5dB/100ms, regenerate that specific baseline WAV from the new render and DOCUMENT in the summary which baseline shifted and why (the cleaner saw/square spectrum is the only legitimate cause). Do NOT loosen any tolerance to force a pass. The starting state is 2283 passed / 0 failed / 14 skipped; the end state must be GREEN with the skip count unchanged (or document any delta).
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && bash scripts/test_two_run_determinism.sh examples/edm/pulse.flow && dotnet test --filter "FullyQualifiedName~Phase41Showcase" 2>&1 | grep -qE "Passed!|Failed: *0" && echo DETERMINISM_AND_SHOWCASE_GREEN</automated>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet test 2>&1 | tee /tmp/quick_wcy_fulltest.log | grep -qE "Failed: *0|Passed!" && echo FULL_SUITE_GREEN</automated>
  </verify>
  <done>`scripts/test_two_run_determinism.sh examples/edm/pulse.flow` exits 0 (byte-identical WAV across two renders — two-run cmp-clean preserved). `flow-lang.Tests/baselines/Phase41/showcase.wav` is the regenerated band-limited render and Phase41ShowcaseRmsTests passes against it. The full `dotnet test` suite ends GREEN with 0 failures; any baseline that shifted (expected: only Phase41) is regenerated + documented in the summary, no tolerance loosened.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (none new) | Pure offline DSP change inside flow-lang; no new input parsing, no network, no file-format surface, no new packages. PolyBLEP is added float math on an existing deterministic render path. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-wcy-01 | Tampering | NoteSynthesizer saw/square render bytes | mitigate | Phase46 byte-exact guard regenerated to the new contract pins every sample; sine/triangle oracles unchanged catch accidental cross-synth edits |
| T-wcy-02 | Denial of Service | non-determinism (would break two-run cmp-clean) | mitigate | PolyBLEP is pure deterministic float math (no RNG/clock/accumulator); Task 3 asserts byte-identical two renders via `test_two_run_determinism.sh` |
| T-wcy-03 | Information disclosure | n/a | accept | No data flows; offline DSP only |
| T-wcy-SC | Tampering | npm/pip/cargo installs | accept | Zero new packages — PolyBLEP is hand-rolled float math per the project's "hand-rolled DSP, no library" convention; no install tasks |
</threat_model>

<verification>
- `dotnet build flow-lang -p:FlowTarget=Desktop` AND `-p:FlowTarget=Web` both exit 0 (oscillators are core, on both targets).
- `dotnet test --filter "FullyQualifiedName~NoteSynthesizerByteGuard"` green — saw/square regenerated, sine/triangle still byte-exact.
- `dotnet test --filter "FullyQualifiedName~HarmonicRichness"` green — saw/square clear the >=20% floor (measured, printed).
- `bash scripts/test_two_run_determinism.sh examples/edm/pulse.flow` exits 0 — two-run cmp-clean.
- `dotnet test --filter "FullyQualifiedName~Phase41Showcase"` green against the re-pinned baseline.
- Full `dotnet test` ends GREEN (start: 2283 passed / 0 failed / 14 skipped). Any other shifted RMS baseline is regenerated + documented, never tolerance-loosened.
</verification>

<success_criteria>
- SawSynthesizer + SquareSynthesizer are band-limited via PolyBLEP; SineSynthesizer + TriangleSynthesizer unchanged.
- The `(frequency * t) % 1.0` absolute-time phase formula is preserved in the modified synths (byte-determinism contract).
- Two-run cmp-clean holds for the saw-based showcase (deterministic PolyBLEP).
- Phase46 byte guards regenerated for saw/square (documented as the expected band-limiting change), sine/triangle facts unchanged and green.
- Saw + square measured to clear the >=20% harmonic-richness floor post-band-limiting (new Phase29 facts).
- Phase41 `showcase.wav` re-pinned to the new band-limited render of `examples/edm/pulse.flow`.
- Full xUnit suite GREEN; both build targets green; zero new packages.
</success_criteria>

<output>
Create `.planning/quick/260608-wcy-band-limit-saw-square-oscillators-with-p/260608-wcy-SUMMARY.md` when done.
Document in the summary: (1) the PolyBLEP residual used + the saw (one-correction) vs square (two-correction) wiring; (2) the measured saw/square harmonic-richness ratios proving the >=20% floor holds; (3) the two-run determinism SHA; (4) WHICH baselines shifted (expected: only Phase41 showcase.wav) and the before/after — with the cleaner spectrum named as the cause; (5) confirmation sine/triangle bytes are unchanged.
</output>
