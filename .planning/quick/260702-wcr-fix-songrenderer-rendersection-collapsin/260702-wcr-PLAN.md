---
phase: quick-260702-wcr
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs
  - flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs
autonomous: true
requirements: [QUICK-WCR-01]
must_haves:
  truths:
    - "A Song section whose sequences are entirely rests renders as SILENCE of the section's notated length (not zero frames)"
    - "In a multi-section Song, note content after an all-rest section starts at exactly the rest section's notated duration"
    - "A genuinely-empty section (no sequences / zero-length) still renders zero-length (unchanged)"
    - "The silent-section length scales with tempo — the resolved section bpm is honored"
    - "Songs with NO all-rest sections render byte-identical to before (no regression)"
  artifacts:
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
    - flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs
  key_links:
    - "RenderSection(SectionData, Func<string,INoteSynthesizer>) guard at SongRenderer.cs:548 — the single funnel for RenderSong / sampler:NAME / RenderSongAuto"
    - "maxBeats derives from max(SequenceData.TotalBeats), which is bar-capacity based (SequenceType.cs BarLengthBeats) so all-rest bars report full length"
---

<objective>
Fix `SongRenderer.RenderSection` collapsing an all-rest section to zero frames, which shifts every later section of a Song early.

Rests produce no `Voice` objects, so an all-rest section reaches the end-of-method guard with `allVoices.Count == 0` but `maxBeats > 0` (bars are capacity-based, so `SequenceData.TotalBeats` still reports the notated bar-grid length). The current guard returns a ZERO-length buffer in that case; `AppendBuffers` then concatenates nothing and subsequent sections start early. Verified repro (organ synth): a Song `[tacet melody]` where `tacet` is 2 bars of rests rendered byte-for-byte identical to `[melody]` alone (both 423404-byte WAVs) — the rest section contributed nothing.

Purpose: silence is the only musically-correct reading of a rest section (charitable-interpretation alignment; matches note-stream bar-grid semantics where bars occupy capacity regardless of content). This is the standard multi-instrument orchestral pattern — any instrument tacet for whole sections triggers it.

Output: a one-branch fix in the single core `RenderSection` overload + a focused xUnit regression suite.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
</execution_context>

<context>
@.planning/STATE.md
@flow-lang/StandardLibrary/Audio/SongRenderer.cs
@flow-lang/TypeSystem/SpecialTypes/SequenceType.cs
@flow-lang.Tests/Integration/Quick260702Vud/SfzAmpegReleaseTailTests.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Return silent notated-length buffer for all-rest sections in RenderSection</name>
  <files>flow-lang/StandardLibrary/Audio/SongRenderer.cs</files>
  <action>
In the single core overload `RenderSection(SectionData section, Func<string,INoteSynthesizer> synthForSequence)`, replace the terminal guard at line 548-549:

    if (allVoices.Count == 0 || maxBeats <= 0)
        return new AudioBuffer(0, StereoChannels, DefaultSampleRate);

Split it into two cases so an all-rest section (no voices, but real notated length) renders as SILENCE of its notated length instead of collapsing:

  - When `maxBeats <= 0` (genuinely empty section — no sequences, or only zero-length sequences): KEEP the existing zero-length return `new AudioBuffer(0, StereoChannels, DefaultSampleRate)`. This preserves current behavior.
  - Otherwise, when `allVoices.Count == 0` but `maxBeats > 0` (all-rest section): return a SILENT stereo buffer of the notated length. Compute frames with the SAME formula `MixVoicesToStereoBuffer` uses (SongRenderer.cs:560-561), using the already-resolved section `bpm` local (line 459) so tempo context is honored:

        int totalFrames = (int)(maxBeats * (60.0 / bpm) * DefaultSampleRate);
        return new AudioBuffer(totalFrames, StereoChannels, DefaultSampleRate);

    A freshly-constructed `AudioBuffer(frames, channels, sampleRate)` is zero-filled (silent) — that is exactly the invariant `MixVoicesToStereoBuffer` already relies on when it additively mixes into a fresh buffer, so a zero-voice mix and this silent buffer are frame-count-identical.

When `allVoices.Count > 0`, control still falls through to the existing `return MixVoicesToStereoBuffer(allVoices, bpm, DefaultSampleRate, maxBeats);` — leave that line untouched.

`maxBeats` source is already confirmed correct: it is `max(sequence.TotalBeats)` over the section's sequences (loop at lines 522-523), and `SequenceData.TotalBeats` is bar-capacity based (`SequenceType.cs` `BarLengthBeats` uses `TimeSignature.BarCapacityQuarters`), so an all-rest bar contributes its full length. No deviation needed. If — contrary to this — an all-rest sequence were found to report `TotalBeats == 0`, STOP and report; do not silently derive length another way.

This is the ONLY edit site: RenderSong (single-synth), sampler:NAME (RenderSongWithSfz), and RenderSongAuto all funnel through this one overload.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp &amp;&amp; dotnet build flow-lang/flow-lang.csproj 2>&amp;1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>flow-lang builds clean (Desktop). The guard now returns a notated-length silent stereo buffer when `allVoices.Count == 0 &amp;&amp; maxBeats > 0`, and only the `maxBeats <= 0` case returns zero-length. The `bpm` local is used for the frame count.</done>
</task>

<task type="auto">
  <name>Task 2: Add regression tests pinning all-rest section length, onset alignment, tempo scaling, and the empty-section zero-length invariant</name>
  <files>flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs</files>
  <action>
Create `flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs`, namespace `FlowLang.Tests.Integration.Quick260702Wcr`, `[Collection("FlowScripts")]`. Follow the recent-quick conventions in the referenced `Quick260702Vud/SfzAmpegReleaseTailTests.cs` (xUnit `[Fact]`, a `Dispose` that calls `RenderingDiagnostics.ResetForTesting()`).

Capture rendered buffers WITHOUT WAV round-trip (WAV write applies seeded dither, so "all samples exactly zero" would fail). Two proven capture paths — use whichever fits each fact:
  - Flow source path: `using var runner = new FlowEngineRunner();` then `runner.RunSource(src)`; assert `ErrorCount == 0`; read a rendered buffer back via `runner.GetVariable("rendered").As<AudioBuffer>()` (GetVariable + As&lt;T&gt; pattern is used in `flow-lang.Tests/Unit/QuickFixes/SustainPedalContextInheritanceFacts.cs`). Use the `organ` synth (full sustain, no sample-length cap — audible energy is unambiguous). Prelude: `use "@std"` / `use "@audio"` / `use "@notation"`.
  - Direct-construction path (for the empty-section fact): build `new SectionData("empty", new Dictionary&lt;string, SequenceData&gt;(), null)`, a registry `Dictionary&lt;string, SectionData&gt;`, a `SongData(new List&lt;SongSectionRef&gt; { new("empty") }, registry)`, then `SongRenderer.RenderSong(new List&lt;Value&gt; { Value.Song(song), Value.String("organ") }).As&lt;AudioBuffer&gt;()`.

Beat/frame math for assertions (44100 Hz, stereo): frames = round(totalBeats × (60/bpm) × 44100). An all-rest stream `| _ _ _ _ | _ _ _ _ |` is 2 bars × 4 quarter-beats = 8 beats in 4/4.

Facts to write:

1. `AllRestSection_ThenNoteSection_NoteContentStartsAtRestDuration`
   Render two songs under `tempo 100`: `Song withRest = [tacet melody]` and `Song noRest = [melody]`, where `tacet` wraps an all-rest sequence `| _ _ _ _ | _ _ _ _ |` (8 beats) and `melody` is `| C4q D4q E4q F4q |`. Capture both buffers.
   - Assert `withRest.Frames == noRest.Frames + restFrames` where `restFrames = (int)(8.0 * (60.0/100.0) * 44100)` (allow ±2 for rounding).
   - Assert the lead of `withRest` is silent: peak absolute sample over the first `restFrames` frames (both channels) is below a tiny epsilon (e.g. 1e-6).
   - Assert audible energy exists after the rest: peak absolute sample over frames `[restFrames, withRest.Frames)` is well above that epsilon (organ note energy).
   This is the frame-accurate proof that later sections no longer start early.

2. `AllRestSection_Alone_IsSilentBufferOfNotatedLength`
   Render `Song s = [tacet]` (the same 8-beat all-rest section) under `tempo 100`. Capture the buffer.
   - Assert `buf.Frames == (int)(8.0 * (60.0/100.0) * 44100)` (±2) — NOT zero.
   - Assert every sample is exactly 0.0 (both channels) — a fresh zero-filled silent buffer.

3. `EmptySection_NoSequences_StaysZeroLength`
   Use the direct-construction path to build a section with an empty `Sequences` dictionary; render via `SongRenderer.RenderSong`. Assert `buf.Frames == 0`. This pins the preserved `maxBeats <= 0` behavior.

4. `AllRestSection_HalfTempo_DoublesFrames`
   Render the same 8-beat all-rest section under `tempo 60` and under `tempo 120`. Assert `framesAt60 == framesAt120 * 2` (±2). Proves the resolved section bpm drives the silent length.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp &amp;&amp; dotnet test flow-lang.Tests --filter "FullyQualifiedName~Quick260702Wcr" 2>&amp;1 | grep -E "Passed!|Failed!|error|Passed:|Failed:" | head</automated>
  </verify>
  <done>All four facts in `AllRestSectionLengthTests` pass: all-rest section renders silence of notated length, note content after it starts at the correct frame, empty section stays zero-length, and the silent length scales with tempo.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

No new trust boundary is introduced. This is a pure internal audio-buffer sizing fix inside the offline render pipeline — no external input parsing, no new package, no network/IO surface, no filesystem path handling.

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-wcr-01 | Denial of Service | `RenderSection` silent-buffer allocation sized by `maxBeats` | low | accept | `maxBeats` is bounded by the composer's own authored bar count (same bound that already sizes `MixVoicesToStereoBuffer`); no new unbounded allocation path — a huge silent buffer was already allocatable via a huge note section. No amplification. |
</threat_model>

<verification>
- `dotnet build flow-lang/flow-lang.csproj` — Desktop build clean.
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` — Web build clean (the edit is plain arithmetic, no `#if` guards, no stripped API).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Quick260702Wcr"` — the 4 new facts pass.
- Regression / byte-identity guard for songs WITHOUT all-rest sections: `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz|FullyQualifiedName~ReverbTimeRender|FullyQualifiedName~SustainPedal"` — these multi-section render suites funnel through `RenderSection` and must stay green (they contain no all-rest sections, so their rendered output is unchanged).
- Expectation to state: rendered output changes ONLY for songs containing all-rest sections (previously collapsed). Songs without them are byte-identical — the `allVoices.Count > 0` fall-through and the `maxBeats <= 0` branch are both untouched.
</verification>

<success_criteria>
- All-rest section renders a silent stereo buffer of its notated length (tempo-scaled), not zero frames.
- In `[tacet melody]`, note audio begins at exactly the rest section's notated frame offset; total length equals the sum of both sections.
- Genuinely-empty section (no sequences) still returns zero-length.
- Desktop AND Web builds green; new tests pass; at least one existing multi-section render suite still passes (no regression for rest-free songs).
</success_criteria>

<output>
Create `.planning/quick/260702-wcr-fix-songrenderer-rendersection-collapsin/260702-wcr-SUMMARY.md` when done.
</output>
