---
phase: 260702-tpn
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
  - flow-lang.Tests/Integration/Quick260702Tpn/SfzAmpVeltrackTests.cs
  - flow-lang.Tests/Integration/Phase37/SfzVelocityCrossfadeRenderTests.cs
autonomous: true
requirements: [TPN-01]
must_haves:
  truths:
    - "A C4 note rendered through a default SFZ patch (amp_veltrack absent → treated as 100) at low velocity (pp) produces LOWER RMS than the same note at high velocity (ff) — dynamics are no longer flat/inverted."
    - "A VSCO-style two-layer patch (soft layer volume=+18dB / vel 0-62, loud layer volume=+6dB / vel 63-127) rendered at pp (vel 0.25) vs ff (vel 0.875) produces pp RMS < ff RMS (the diagnosis's inverted-dynamics bug is closed)."
    - "amp_veltrack=0 makes velocity irrelevant to amplitude (gain = 1.0 at every velocity); amp_veltrack=100 (default) makes gain = (vel/127)^2."
    - "The velocity gain is applied per-region so per-layer amp_veltrack differences are honored, and exactly once per rendered note body (single-region path AND each summed velocity-crossfade layer)."
    - "amp_veltrack no longer triggers the unrecognized-opcode advisory (it is on the whitelist)."
    - "Two-run determinism holds (velocity gain is a pure function of velocity — no RNG); Desktop (default) build and FlowTarget=Web build both compile with 0 errors; synth render paths are untouched."
  artifacts:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
    - flow-lang.Tests/Integration/Quick260702Tpn/SfzAmpVeltrackTests.cs
  key_links:
    - "SfzParser.KnownOpcodes gains 'amp_veltrack'; BuildRegion reads it via ReadDouble(default 100.0) — inherits the <global>/<group>/<region> cascade because ReadDouble reads the pre-merged region dict (global+group already copied in at <region> open)."
    - "SfzRegion gains AmpVeltrack (double, default 100.0) appended AFTER the existing Phase 37 optional params so all positional constructor callers stay valid."
    - "SfzRenderer.RenderRegionToMono gains an int vel param (both call sites — RenderInternal line ~279 and RenderAndSumXfadeLayers line ~306 — have vel in scope) and multiplies the fitted body by ComputeVelocityGain(region.AmpVeltrack, vel) alongside region.Volume."
    - "ComputeVelocityGain(ampVeltrack, vel) = (1-t) + t*(vel/127)^2 where t = clamp(ampVeltrack/100, 0, 1); exposed via ComputeVelocityGain_TestOnly mirroring ComputeXfadeGain_TestOnly."
---

<objective>
Implement the SFZ `amp_veltrack` velocity-amplitude curve in the SFZ render path so note velocity scales rendered output amplitude (the Sforzando/ARIA default that VSCO Community Edition is authored for). Today `SfzRenderer` uses velocity ONLY for region/layer selection and the xfin/xfout crossfade — velocity never scales amplitude — so VSCO's per-layer makeup gains (soft layers carry +18..+20 dB expecting the curve to attenuate) render pp LOUDER than ff and all dynamics flat-to-inverted on every SFZ instrument.

Purpose: Faithful musical dynamics. With the curve, vel 32 (pp) → gain (32/127)^2 ≈ 0.0635 (-23.9 dB) and vel 112 (ff) → ≈ 0.777 (-2.2 dB); combined with the makeup gains the net ordering becomes correct (pp quieter than ff).

Output: `amp_veltrack` added to the SFZ opcode whitelist + a new `SfzRegion.AmpVeltrack` field (default 100.0) flowing through the header cascade; a per-region velocity gain applied once per rendered note body in `SfzRenderer`; new tests pinning the curve math + loudness ordering; and a refresh of the one render test whose fixed-reference assertion is invalidated by the velocity-squared curve. No new Flow surface (amp_veltrack is an internal SFZ opcode, no composer-facing builtin). Changes stay inside `StandardLibrary/Audio/Sfz/` — Desktop-only, Web-stripped, no new guards.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md
@flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs
@flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
@flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
@flow-lang.Tests/Integration/Phase37/SfzVelocityCrossfadeRenderTests.cs
@flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Add amp_veltrack opcode + AmpVeltrack region field + per-region velocity gain in the SFZ renderer</name>
  <files>flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs, flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs, flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs</files>
  <behavior>
    - ComputeVelocityGain(ampVeltrack=100, vel=127) == 1.0
    - ComputeVelocityGain(ampVeltrack=100, vel=64) ≈ 0.2540 (= (64/127)^2, within 1e-3)
    - ComputeVelocityGain(ampVeltrack=0, vel) == 1.0 for every vel (velocity does not affect amplitude)
    - ComputeVelocityGain out-of-range clamp: ampVeltrack=150 behaves as 100; ampVeltrack=-50 behaves as 0 (t clamped to [0,1])
    - Parsing a region with no amp_veltrack yields AmpVeltrack == 100.0; parsing `amp_veltrack=0` yields 0.0; `amp_veltrack=50` yields 50.0
    - amp_veltrack does NOT emit the `[sfz] unrecognized opcode` advisory (whitelisted)
  </behavior>
  <action>
Add the `amp_veltrack` opcode and its render effect across three files.

SfzRegion.cs: append a new positional record parameter `double AmpVeltrack = 100.0` AFTER the existing Phase 37 optional params (`XfoutHiVel = -1`), so every existing positional constructor call in SfzParser stays valid. Document it in the class XML doc block matching the existing field-semantics style: SFZ velocity-amplitude tracking, [-100..100] in spec but this common-subset renderer clamps the effective track fraction to [0,1] charitably; default 100 (Sforzando/ARIA default) means gain = (vel/127)^2; 0 means velocity does not affect amplitude.

SfzParser.cs: add `"amp_veltrack"` to the `KnownOpcodes` HashSet (update the count wording in the surrounding doc comment from 22 to 23). In `BuildRegion`, read it with `double ampVeltrack = ReadDouble(region, "amp_veltrack", 100.0, patchDescription);` placed alongside the other ReadDouble calls (near volumeDb/panSfz). It inherits the global/group/region cascade automatically because ReadDouble reads the already-merged `region` dict (global+group are copied into regionOpcodes at `<region>` open). Pass `ampVeltrack` into the `new SfzRegion(...)` constructor as the final argument (after `xfoutHiVel`). Do NOT clamp in the parser — store the raw declared value on the record so the renderer owns the charitable clamp + advisory (matches the diagnosis's renderer-side formula).

SfzRenderer.cs: add a pure helper mirroring the ComputeXfadeGain / ComputeXfadeGain_TestOnly pattern:
  - `private static double ComputeVelocityGain(double ampVeltrack, int midiVelocity)` computing `t = Math.Clamp(ampVeltrack / 100.0, 0.0, 1.0); normVel = midiVelocity / 127.0; return (1.0 - t) + t * normVel * normVel;`
  - `public static double ComputeVelocityGain_TestOnly(double ampVeltrack, int midiVelocity) => ComputeVelocityGain(ampVeltrack, midiVelocity);`
Thread velocity into `RenderRegionToMono`: add an `int vel` parameter. Update its two call sites — the single-region path in RenderInternal (has local `vel`) and the per-layer call inside RenderAndSumXfadeLayers (has `vel` param). Inside RenderRegionToMono, where `region.Volume` is applied to `fitted`, fold in the velocity gain so it is applied exactly once per rendered body: compute `double velGain = ComputeVelocityGain(region.AmpVeltrack, vel);` and multiply the body by `(float)(region.Volume * velGain)` in a single pass (keep the existing `!= 1.0` short-circuit correct against the combined scale). Because RenderRegionToMono runs per-layer in the xfade summing path, per-layer amp_veltrack differences are honored. Add a one-shot charitable advisory when the declared value is out of the [0,100] common-subset range: `if (region.AmpVeltrack < 0.0 || region.AmpVeltrack > 100.0) RenderingDiagnostics.WarnOnce($"sfz:ampveltrack:{patch.Description}", $"[sfz] amp_veltrack={region.AmpVeltrack} out of [0,100] in '{patch.Description}' — clamping");` — keyed per patch so iterative reloads do not flood stderr. Keep the existing pan / envelope / articulation-multiplier stages in FinishMono unchanged.

Do NOT touch synth render paths, the round-robin picker, or ComputeXfadeGain. This is purely additive amplitude shaping in the SFZ sample path.
  </action>
  <verify>
    <automated>dotnet build flow-lang/flow-lang.csproj 2>&1 | tail -3 && dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web 2>&1 | tail -3</automated>
  </verify>
  <done>Both Desktop and Web builds compile with 0 errors; amp_veltrack is whitelisted and read into SfzRegion.AmpVeltrack (default 100.0); ComputeVelocityGain applies (1-t)+t*(vel/127)^2 with t clamped to [0,1]; the velocity gain is multiplied into the fitted body alongside region.Volume in RenderRegionToMono at both call sites.</done>
</task>

<task type="auto">
  <name>Task 2: Add amp_veltrack tests (curve math + parser default + loudness ordering) and refresh the velocity-relative render assertions</name>
  <files>flow-lang.Tests/Integration/Quick260702Tpn/SfzAmpVeltrackTests.cs, flow-lang.Tests/Integration/Phase37/SfzVelocityCrossfadeRenderTests.cs</files>
  <action>
Create `flow-lang.Tests/Integration/Quick260702Tpn/SfzAmpVeltrackTests.cs` (namespace `FlowLang.Tests.Integration.Quick260702Tpn`, `[Collection("FlowScripts")]`, `IDisposable` with `RenderingDiagnostics.ResetForTesting()` + `FlowConfig.Reset()` in ctor/Dispose — copy the harness shape from SfzVelocityCrossfadeRenderTests, including its `FindRepoRoot()` + `RmsDb` helpers and the `SfzSampleCache.SetRaw_TestOnly` + `FileIO.LoadWavInternal(...sfz-smoke/C4_sine.wav)` cache-building pattern). Add these facts:

1. Curve-math pins via `SfzRenderer.ComputeVelocityGain_TestOnly`: assert (100,127)==1.0; (100,64) within 1e-3 of 0.2540; (0,32)==1.0 and (0,127)==1.0; (150,64) equals (100,64) and (-50,64) equals (0,64) (clamp behavior). Use `Assert.Equal(expected, actual, 4)` for the double comparisons.

2. Parser-default pin: `SfzParser.Parse(...)` a minimal inline one-region .sfz WITHOUT amp_veltrack and assert `patch.Regions[0].AmpVeltrack == 100.0`; parse a second inline region with `amp_veltrack=0` and assert `== 0.0`; parse `amp_veltrack=50` and assert `== 50.0`.

3. Loudness-ordering (the diagnosis's headline): build an inline two-region .sfz string (both `sample=C4_sine.wav`, `lokey=0 hikey=127 pitch_keycenter=60`; soft region `lovel=0 hivel=62 volume=18`; loud region `lovel=63 hivel=127 volume=6`), `SfzParser.Parse` it, build a cache that SetRaw_TestOnly's the shared C4_sine.wav buffer against each region's SamplePath, render a C4 note at velocity 0.25 (pp → vel 32, hits soft) and at velocity 0.875 (ff → vel 111, hits loud), and assert `RmsDb(ppBuf) < RmsDb(ffBuf)`. Add a comment recording the expected magnitudes from the diagnosis (pp net ≈ -16 dB via 0.0635 curve × +18 dB makeup; ff net ≈ +3.7 dB via 0.764 curve × +6 dB makeup). Render a C4 `MusicalNoteData` exactly as RenderAtVelocity does in SfzVelocityCrossfadeRenderTests (Articulation.Normal, durationBeats 1.0, bpm 120.0, sampleRate 44100).

Refresh `SfzVelocityCrossfadeRenderTests.VelocityCrossfade_AcrossBand_NoDropout_NeverBelowReference`: the fixture has no amp_veltrack (→ effective track 100), so every render now also carries `(vel/127)^2`, and the reference is taken at vel=50 while the swept notes are vel 60..80 — the fixed `refDb + 3.5` ceiling no longer holds. Preserve the test's INTENT (no dropout, crossfade sum sits at/above the single-layer level at the SAME velocity) by making the reference per-velocity: for each swept `vel`, compute `expectedSingleDb = refDb + 40.0 * Math.Log10(vel / 50.0)` (this is `refDb + 20*log10(velGain(vel)/velGain(50))` with velGain=(vel/127)^2, which reduces to `40*log10(vel/50)`), then assert `db >= expectedSingleDb - 0.5` (never a dropout / never below the same-velocity single layer) and `db <= expectedSingleDb + 3.5` (coherent √2 ceiling for the identical-source fixture). Keep the "never silent" (`!double.IsNegativeInfinity`) assertion unchanged. Update the inline comment to explain the velocity-squared curve is now folded in. Leave `VelocityCrossfade_BandLowEdge_IsNotSilent` unchanged (it only asserts non-silence). Do NOT change any other SFZ test — verify SfzSmokeTests (`rms > 0.01` floor, renders at default velocity 0.63 → gain ≈ 0.40, still far above the -40 dBFS floor), SfzArticulationTests (hash-distinctness at a uniform velocity → unaffected), and the pan tests (relative L/R ratios preserved under a uniform scalar) all stay green by running the filter below.
  </action>
  <verify>
    <automated>dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz" 2>&1 | tail -15</automated>
  </verify>
  <done>New SfzAmpVeltrackTests facts pass (curve math, parser default, pp-quieter-than-ff loudness ordering); the refreshed SfzVelocityCrossfadeRenderTests passes against the per-velocity reference; the full `~Sfz` filter is green with zero regressions (SfzSmokeTests / SfzArticulationTests / SfzPan* / SfzHardSwitchRegression / SfzDeterminism all still pass).</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| .sfz file → parser | Composer/library-supplied opcode text; amp_veltrack value is untrusted numeric input |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-tpn-01 | Tampering | amp_veltrack opcode value (out-of-range / malformed) | low | mitigate | ReadDouble charitable fallback to 100.0 on parse failure; renderer clamps effective track fraction to [0,1] with a one-shot WarnOnce — never throws, never produces NaN/Inf gain |
| T-tpn-02 | Denial of Service | per-note render hot path | low | accept | ComputeVelocityGain is O(1) arithmetic; WarnOnce dedups per patch so a pathological .sfz cannot flood stderr on repeated notes |
| T-tpn-SC | Tampering | package installs | low | accept | No new NuGet/package installs in this task — pure in-repo C# edits, no legitimacy gate needed |
</threat_model>

<verification>
- `dotnet build flow-lang/flow-lang.csproj` (Desktop) and `-p:FlowTarget=Web` both 0 errors.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz"` green (includes the baseline-adjacent SfzSmoke / SfzArticulation / SfzDeterminism / SfzPan* render tests).
- Determinism: velocity gain is a pure function of velocity — no RNG introduced; SfzDeterminismTests two-run byte-identical facts remain green.
</verification>

<success_criteria>
- amp_veltrack parsed (whitelist + AmpVeltrack field default 100.0, header cascade honored) and applied per-region as `(1-t)+t*(vel/127)^2` amplitude gain, once per rendered body, at both single-region and per-xfade-layer sites.
- pp renders quieter than ff on a VSCO-style makeup-gain patch (inverted-dynamics bug closed); amp_veltrack=0 disables the curve; default (absent) behaves as track=100.
- Desktop + Web builds clean; SFZ test filter green with the one velocity-relative render test refreshed; synth paths byte-untouched.
</success_criteria>

<output>
Create `.planning/quick/260702-tpn-implement-sfz-amp-veltrack-velocity-ampl/260702-tpn-SUMMARY.md` when done
</output>
