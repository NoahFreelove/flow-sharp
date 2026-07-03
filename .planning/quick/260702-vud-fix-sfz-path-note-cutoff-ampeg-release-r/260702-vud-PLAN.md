---
phase: 260702-vud
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
  - flow-lang.Tests/Integration/Quick260702Vud/SfzAmpegReleaseTailTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs
autonomous: true
requirements: [VUD-01]
must_haves:
  truths:
    - "A sustained-articulation SFZ note (Normal/Legato/Tenuto/Accent/Sforzando) rendered from a patch declaring ampeg_release > 0 holds its envelope AT the sustain level through the authored note end — the envelope value at the last authored frame is >= ~0.9 of the sustain level (the 93%-fade cutoff bug is closed)."
    - "The rendered buffer for a sustained note is authoredFrames + releaseFrames long, where releaseFrames = clamp(region.AmpegRelease, 0, 10) * sampleRate; the ampeg_release tail RINGS PAST the authored end instead of being squeezed inside the note window."
    - "The tail is continuous at the authored boundary (sample magnitude just after the authored end ≈ just before — no step discontinuity) and decays monotonically (RMS of the last tail quarter is far below the RMS of the first tail quarter, reaching ~-60 dB / x0.001 by the tail end)."
    - "A patch with ampeg_release absent/0 produces a buffer of length authoredFrames with byte-identical output to the pre-change renderer (no tail — current behavior preserved)."
    - "Staccato and Marcato notes (effective sustain = 0) keep the current short/detached shape with NO tail appended, regardless of the patch's ampeg_release value; their buffer length stays authoredFrames."
    - "region.Volume and the amp_veltrack velocity gain (quick-260702-tpn) keep applying over the whole extended body; the SAMP-03 articulation multiplier and stereo pan operate over the extended buffer; both render paths (single-region hard-switch AND the RenderAndSumXfadeLayers velocity-crossfade path) use the extended length consistently."
    - "Two-run determinism holds (no RNG in this path); Desktop (default) build and FlowTarget=Web build both compile with 0 errors; dotnet test --filter FullyQualifiedName~Sfz is green."
  artifacts:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
    - flow-lang.Tests/Integration/Quick260702Vud/SfzAmpegReleaseTailTests.cs
  key_links:
    - "SfzRenderer.RenderInternal splits the old single `targetFrames` into `authoredFrames` (= durationSeconds*sampleRate) and `totalFrames` (= authoredFrames + tailFrames); tailFrames = IsSustainedArticulation(note.Articulation) && region.AmpegRelease > 0 ? (int)(Math.Clamp(region.AmpegRelease, 0, 10) * sampleRate) : 0."
    - "IsSustainedArticulation(a) = a != Articulation.Staccato && a != Articulation.Marcato (mirrors the sustain=0 gate inside SynthUtils.GenerateArticulationADSR)."
    - "totalFrames is passed to RenderRegionToMono / RenderAndSumXfadeLayers in place of the old targetFrames — AssembleBody already fills the whole target-length array (loop path continues into the tail region; non-loop path zero-pads), so the source content spans the extended length with no AssembleBody change."
    - "FinishMono gains an authoredFrames parameter alongside the existing (now-renamed) totalFrames; when totalFrames == authoredFrames it stays byte-identical to today (baseRelease = region.AmpegRelease>0 ? region.AmpegRelease : 0.05, no tail); when totalFrames > authoredFrames it generates the Phase 28 envelope over the AUTHORED window with baseRelease = 0 (holds sustain to the authored end per SampledInstrumentRenderer precedent) then multiplies [authoredFrames, totalFrames) by an exponential decay starting at level 1.0 (continuity) reaching x0.001 at the last tail frame."
    - "SAMP-03 multiplier in FinishMono is bounded to Math.Min(authoredFrames, fitted.Length) so the tail region is not reshaped by the articulation quartiles (mirrors the sweep-0614 fix in SampledInstrumentRenderer); byte-identical for the no-tail path where authoredFrames == fitted.Length."
---

<objective>
Fix SFZ note cutoff: today `SfzRenderer.FinishMono` builds the note envelope via `GenerateArticulationADSR(..., baseRelease: region.AmpegRelease or 0.05, frames: authored-note-frames)`, and `EnvelopeProcessor.GenerateADSRCurve` allocates attack+decay+release INSIDE `totalFrames`, proportionally scaling all three down when they exceed it (QUICK-260504-v6j). With real libraries this squeezes the whole `ampeg_release` INTO the note window: VSCO CE OboeSusVib declares `ampeg_release=0.7`, so a 0.3s eighth note becomes ~93% release ramp and every SFZ note decays to zero before its slot ends — melodies sound detached/staccato/quiet (the Swan Lake oboe lead).

Real SFZ semantics: `ampeg_release` is what happens AFTER note-off — the tail rings PAST the note boundary. Phase 29's `SampledInstrumentRenderer` already implements exactly this pattern (baseRelease = 0.0 holds sustain to the authored end; a separate exponential tail restarts at the sustain level and rings out past the authored end). Mirror it in the SFZ path for sustained articulations.

Purpose: Faithful sustained playback for real SFZ orchestral libraries. A held sustained note holds its level through its notated end and rings out afterward, so overlapping tails mix correctly (the architectural facts confirm `SongRenderer` sums overlapping voice frames and truncates at section boundaries — longer note buffers cause no timing shift).

Output: `SfzRenderer` extends the rendered buffer to authoredFrames + releaseFrames for sustained articulations when `ampeg_release > 0`, holding sustain to the authored end and appending a continuous exponential release tail; staccato/marcato and ampeg_release-absent patches stay byte-identical. New tests pin the hold-at-end / tail-length / continuity behavior; one length-pinned articulation assertion is refreshed for the tail. Changes stay inside `StandardLibrary/Audio/Sfz/` — Desktop-only, Web-stripped, no new guards.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md
@flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
@flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs
@flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs
@flow-lang/StandardLibrary/Audio/SynthUtils.cs
@flow-lang.Tests/Integration/Quick260702Tpn/SfzAmpVeltrackTests.cs
@flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Extend SFZ render buffer with an ampeg_release tail for sustained articulations</name>
  <files>flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs</files>
  <behavior>
    - A sustained note (Articulation.Normal) rendered from a patch with ampeg_release=0.7 and a 0.3s note produces a buffer of length authoredFrames + (int)(0.7 * sampleRate); the envelope value at the last AUTHORED frame is >= 0.9 of the sustain level (does NOT fade to near-zero inside the window — anti-regression for the 93%-fade bug).
    - The same sustained note produces a tail that is continuous at the authored boundary (abs sample magnitude at authoredFrames ≈ abs sample magnitude at authoredFrames-1 within a small tolerance — no step) and decays: RMS of the last quarter of the tail << RMS of the first quarter of the tail.
    - A Staccato note from the SAME ampeg_release=0.7 patch produces a buffer of length authoredFrames (NO tail — sustain=0 gate) and stays short/detached.
    - A patch with ampeg_release absent (0) produces a buffer of length authoredFrames for a sustained note (no tail — current behavior).
    - region.Volume and the amp_veltrack velocity gain still scale the whole extended body; the velocity-crossfade summing path (>= 2 xfin/xfout layers) also produces the authoredFrames + tail length.
    - Two-run determinism: two renders of the same note+patch are byte-identical.
  </behavior>
  <action>
Refactor `RenderInternal` + `FinishMono` in SfzRenderer.cs to append an ampeg_release tail past the authored note end for sustained articulations, mirroring `SampledInstrumentRenderer` (Phase 29 debug session varispeed-aliasing-static). Read that class doc first — its baseRelease=0 + exponential-tail continuity rationale is the exact model.

1. Add a private static helper `IsSustainedArticulation(Articulation a)` returning `a != Articulation.Staccato && a != Articulation.Marcato`. Those two are the only articulations that SynthUtils.GenerateArticulationADSR forces to sustain=0; every other articulation holds a non-zero sustain and therefore gets a release tail.

2. In `RenderInternal` (currently computes `int targetFrames = (int)(durationSeconds * sampleRate)`): rename that quantity to `authoredFrames`. Compute the tail:
   - `int tailFrames = (IsSustainedArticulation(note.Articulation) && region.AmpegRelease > 0.0) ? (int)(Math.Clamp(region.AmpegRelease, 0.0, 10.0) * sampleRate) : 0;`
   - Note: the region is only known AFTER region selection (line ~211). Compute authoredFrames early for the `<= 0` guard (unchanged), and compute `totalFrames = authoredFrames + tailFrames` AFTER `region` is resolved but BEFORE the RenderRegionToMono / RenderAndSumXfadeLayers calls. For the xfade summing path, all summed layers share the picked `region`'s tail decision (the picked region drives the tail; xfade layers only differ in velocity gain) — compute the tail from the picked `region` once and thread the SAME `totalFrames` to RenderAndSumXfadeLayers.
   - Clamp is charitable per CLAUDE.md: a malformed ampeg_release above 10s clamps to 10s (matches the SampledInstrumentRenderer [0.05, 10.0] release band spirit). ampeg_release <= 0 → tailFrames 0 → no tail.

3. Thread `totalFrames` where the old `targetFrames` was passed to `RenderRegionToMono(...)`, `RenderAndSumXfadeLayers(...)`, and the initial `<= 0` guard uses `authoredFrames`. `AssembleBody` needs NO change — it already allocates `new float[targetFrames]` and fills the whole array (loop path continues into the tail region; NoLoop/OneShot path straight-copies then zero-pads), so passing the larger totalFrames fills source content across the extended length automatically. The velocity/volume scale in RenderRegionToMono continues to apply over `fitted.Length` (= totalFrames) — correct, the whole extended body is scaled once.

4. Change `FinishMono` to accept BOTH the authored window and the total length. New signature: `FinishMono(float[] fitted, MusicalNoteData note, SfzRegion region, int authoredFrames, int totalFrames, int sampleRate, double voicePan)`. Inside:
   - `bool hasTail = totalFrames > authoredFrames;`
   - Generate the Phase 28 envelope over the AUTHORED window only: pass `frames: authoredFrames` and `baseRelease: hasTail ? 0.0 : (region.AmpegRelease > 0 ? region.AmpegRelease : 0.05)`. When hasTail is false this is byte-identical to today (frames == authoredFrames == fitted.Length, baseRelease unchanged). When hasTail is true, baseRelease=0 holds the sustain level through the authored end so the envelope ends at the sustain level (1.0), meeting the tail continuously.
   - Apply the envelope over the authored window: iterate `for (int i = 0; i < authoredFrames && i < fitted.Length; i++) fitted[i] *= envelope[i];` (the envelope array is authoredFrames long; the tail region past authoredFrames is left un-enveloped here and shaped by the tail loop below). For the no-tail path this equals the current `SynthUtils.ApplyEnvelope(fitted, envelope)` behavior because authoredFrames == fitted.Length.
   - SAMP-03 multiplier: bound the loop to `int multFrames = Math.Min(authoredFrames, fitted.Length);` and sample the quartiles against `authoredFrames` (NOT fitted.Length) — mirrors the sweep-0614 fix in SampledInstrumentRenderer so the tail is not reshaped by the articulation A/D/S/R quartiles. Byte-identical for the no-tail path.
   - Exponential release tail: when hasTail, after the envelope + SAMP-03, apply over `[authoredFrames, fitted.Length)`:
     `double level = 1.0; double decayPerFrame = Math.Pow(0.001, 1.0 / tailFrames); for (int i = authoredFrames; i < fitted.Length; i++) { fitted[i] = (float)(fitted[i] * level); level *= decayPerFrame; }`
     level starts at 1.0 (continuity with the held sustain=1.0 envelope — no step) and reaches x0.001 (~-60 dB) at the final tail frame. Pass `tailFrames` (or `totalFrames - authoredFrames`) into FinishMono so this loop knows the decay base. No RNG — deterministic.
   - Keep the Phase 37 MIX-02 stereo pan step over the full `fitted` (whole extended buffer) unchanged.

5. Update all three FinishMono call sites in RenderInternal (the two return paths — xfade-summed at line ~270 and single-region at line ~284) to pass `authoredFrames, totalFrames`.

Do NOT place any fenced code inside the written file's comments that a test negative-greps; ordinary explanatory comments are fine. Keep the change inside StandardLibrary/Audio/Sfz/ — no new #if FLOW_WEB guards (already Web-stripped).
  </action>
  <verify>
    <automated>dotnet build flow-lang/flow-lang.csproj 2>&1 | tail -5 && dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web 2>&1 | tail -5</automated>
  </verify>
  <done>Desktop + Web builds compile with 0 errors. RenderInternal computes authoredFrames + totalFrames with a sustained-and-ampeg_release>0 tail gate; FinishMono holds sustain to the authored end (baseRelease=0) and appends a continuous exponential tail; staccato/marcato and ampeg_release-absent paths stay byte-identical.</done>
</task>

<task type="auto">
  <name>Task 2: Pin the tail behavior with new tests and refresh the one length-invalidated articulation assertion</name>
  <files>flow-lang.Tests/Integration/Quick260702Vud/SfzAmpegReleaseTailTests.cs, flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs</files>
  <action>
Add a new xUnit test class `SfzAmpegReleaseTailTests` following the SfzAmpVeltrackTests fixture conventions verbatim (namespace `FlowLang.Tests.Integration.Quick260702Vud`, `[Collection("FlowScripts")]`, `IDisposable` with `RenderingDiagnostics.ResetForTesting()` + `FlowConfig.Reset()` in ctor and Dispose, `FindRepoRoot()` helper, `RmsDb`/RMS helpers, and the inline-SFZ pattern: `SfzParser.Parse(sfzText, path, name)` → `SfzSampleCache` + `cache.SetRaw_TestOnly(patch, region.SamplePath, buf)` → `new SfzRenderer(cache)` → `renderer.Render(note, 44100, durationBeats, bpm, patch)`). Load the committed `flow-lang.Tests/fixtures/sfz-smoke/C4_sine.wav` via `FileIO.LoadWavInternal(wavPath)` (same as SfzAmpVeltrackTests). Build `MusicalNoteData` directly with the ctor `(noteName:'C', octave:4, alteration:0, durationValue:4, isRest:false, velocity:0.8, articulation:...)`.

Pin these facts (use an inline patch declaring `ampeg_release=0.7` so tailFrames is a large, easily-measured fraction; render a short note, e.g. durationBeats=0.5 at bpm=120 → 0.25s authored, so the 0.7s tail dominates):

1. `SustainedNote_HoldsLevelAtAuthoredEnd`: render Articulation.Normal. Compute authoredFrames = (int)(durationBeats*60/bpm*44100). Assert the max abs sample magnitude across a small window just before authoredFrames (e.g. frames [authoredFrames-200, authoredFrames-1]) is >= 0.9 * the max abs magnitude in a mid-note window (e.g. around authoredFrames/2). This is the anti-regression for the 93%-fade bug — the note must NOT have decayed to near-zero at its authored end. (Because the patch is stereo after pan, read max abs across both channels per frame; a centered patch keeps both channels equal.)

2. `SustainedNote_BufferLength_IsAuthoredPlusRelease`: assert `buf.Frames == authoredFrames + (int)(0.7 * 44100)` (allow +/-2 frames for int truncation). For a Staccato render from the same patch, assert `buf.Frames == authoredFrames` (+/-2) — no tail.

3. `Tail_IsContinuousAndDecays`: assert continuity — abs sample at frame `authoredFrames` is within a small absolute tolerance of the abs sample at `authoredFrames-1` (no step discontinuity; use the mono-downmixed magnitude or per-channel). Assert decay — RMS of the last quarter of the tail region `[authoredFrames + 3*tailFrames/4, buf.Frames)` is far below (e.g. < 0.2x) the RMS of the first quarter `[authoredFrames, authoredFrames + tailFrames/4)`.

4. `AmpegReleaseAbsent_NoTail`: an inline patch with NO ampeg_release, sustained note → `buf.Frames == authoredFrames` (+/-2).

5. `TwoRuns_ByteIdentical`: render the ampeg_release=0.7 sustained note twice; assert the two `buf.Data` arrays are element-wise equal (determinism).

Then refresh SfzArticulationTests.cs: the `SixArticulations_AudibleDuration_WithinTolerance` fact uses the smoke.sfz fixture which declares `ampeg_release=0.05`, so sustained articulations (Tenuto/Legato/Accent/Sforzando) now ring ~0.05s past the authored end and the Tenuto upper-bound assertion `tenutoAudible <= (int)(authoredFrames * 1.05)` will fail (audible frames now extend into the tail). Update ONLY that upper bound to account for the release tail: change it to `tenutoAudible <= (int)((authoredFrames + (int)(0.05 * SampleRate)) * 1.05)` (or equivalently add the smoke fixture's releaseFrames to the ceiling), and update the assertion message to mention the ampeg_release tail. Leave the `>= 0.95` lower bound and the staccato/marcato/legato/accent/sforzando ratio assertions unchanged — they use tenutoAudible as a relative reference and remain valid (staccato/marcato get more margin; the sustained ratios stay ~1.0 since all sustained articulations share the same tail). Survey the rest of the Sfz test suite for any OTHER assertion that pins rendered length to exactly the authored duration and update it the same way; the buf.Frames >= N smoke/loop assertions are `>=` and survive unchanged, and the RMS-relative pan/velocity/round-robin tests survive unchanged.
  </action>
  <verify>
    <automated>dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz" 2>&1 | tail -15</automated>
  </verify>
  <done>New SfzAmpegReleaseTailTests pins hold-at-authored-end, authoredFrames+release length, staccato no-tail, absent-release no-tail, tail continuity+decay, and two-run byte-identity. SfzArticulationTests Tenuto upper bound refreshed for the 0.05s smoke tail. `dotnet test --filter FullyQualifiedName~Sfz` is green with zero new failures.</done>
</task>

</tasks>

<verification>
- `dotnet build` (Desktop default) compiles with 0 errors.
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` compiles with 0 errors (change is inside the already-Web-stripped Sfz directory; no new guards).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz"` is green (new tail tests pass; refreshed articulation assertion passes; no new regressions).
- Two-run determinism: the same note+patch renders byte-identically (pinned by TwoRuns_ByteIdentical; no RNG in this path).
</verification>

<success_criteria>
Sustained-articulation SFZ notes hold their envelope at the sustain level through the authored note end and ring out via an exponential ampeg_release tail appended PAST the authored boundary (buffer length = authoredFrames + releaseFrames), continuous at the seam and decaying to ~-60 dB. Staccato/marcato and ampeg_release-absent patches are byte-identical to the pre-change renderer. region.Volume + amp_veltrack + SAMP-03 + stereo pan all still apply correctly over the extended body across both render paths. Desktop + Web builds green; Sfz test filter green; determinism preserved.
</success_criteria>

<output>
Create `.planning/quick/260702-vud-fix-sfz-path-note-cutoff-ampeg-release-r/260702-vud-SUMMARY.md` when done.
</output>
