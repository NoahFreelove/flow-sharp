---
phase: quick-260702-ijs
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs
autonomous: true
requirements: [TEMPO-PLAY-01]
must_haves:
  truths:
    - "`tempo N { (play | ... | ) }` renders the note stream at N BPM — doubling the tempo halves the rendered duration (frame count scales inversely with BPM)."
    - "`stream` (Sequence) honors the same active `tempo { }` block — the BPM is resolved on the originating thread BEFORE the Task.Run dispatch, so the background render uses the block tempo, not the Timeline default."
    - "With no active `tempo` block, `(play seq)` / `(stream seq)` fall back to `Timeline.GetBPM()` (which preserves the `setBPM` workaround and its default of 120 BPM) — never throws, no new advisory."
    - "Desktop and FlowTarget=Web builds both compile with 0 errors; the full .flow suite + xUnit suite stay green with zero new regressions."
  artifacts:
    - "flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs — new `RegisterContextDependent(registry, manager, context)` owning the `play(Sequence)` + `stream(Sequence)` registrations; `PlaySequence`/`StreamSequence` take a resolved `double bpm` param."
    - "flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs — CaptureMode regression proving the tempo block scales `(play seq)` duration."
  key_links:
    - "The registered `play`/`stream` lambda resolves `context.GetMusicalContext().Tempo ?? Timeline.GetBPM([]).As<double>()` — this is the seam that replaces the context-blind `Timeline.GetBPM()` read at PlaybackFunctions.cs:159."
    - "FlowEngine.cs calls `PlaybackFunctions.RegisterContextDependent(internalRegistry, _audioManager, _context)` AFTER `_context` exists (mirrors the direct `GranularFunctions.Register(internalRegistry, _context)` wiring) — the manager + context both reach the sequence overloads."
    - "BuiltInFunctions.RegisterSignaturesOnly also calls RegisterContextDependent(proxy, dummyAudio, dummyContext) so the LSP still enumerates the play(Sequence)/stream(Sequence) signatures."
---

<objective>
Fix `tempo { }` blocks being silently ignored by direct note-stream playback
(`(play | ... |)` and `(stream ...)`). Today `PlaySequence` reads the BPM from
`Timeline.GetBPM()` — a `[ThreadStatic]` field that defaults to 120 and is only ever
written by the `setBPM` builtin — so `tempo 120 { }` and `tempo 12000 { }` play at the
exact same speed. Song rendering already honors tempo (`SongRenderer.RenderSection` reads
`section.Context?.Tempo`), so this closes a "the silent block should just work" ergonomics
gap between the two render paths.

Approach A (chosen — see rationale below): make the two Sequence-consuming playback
overloads context-dependent so their registered lambdas can read the active
`MusicalContext.Tempo`, with a charitable fallback to `Timeline.GetBPM()` (which preserves
the `setBPM` escape hatch and the 120 default). The resolved BPM is computed on the calling
thread and threaded into `PlaySequence`/`StreamSequence` as a plain `double`, so `stream`'s
`Task.Run` render also uses the block tempo instead of the wrong thread's ThreadStatic.

Purpose: composers hear the tempo they wrote. A pure correctness + ergonomics fix reusing
the existing MusicalContext stack the rest of the language already respects.
Output: 3 modified C# files + 1 new xUnit regression test.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

# Root cause + contrast (read these ranges — do NOT re-investigate from scratch)
@flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
@flow-lang/StandardLibrary/Audio/Timeline.cs

# The registration + wiring seams the fix threads through
# - BuiltInFunctions.RegisterContextDependentFunctions (line ~1233) is the precedent
#   for context-dependent registration; RegisterSignaturesOnly (line ~142) is the LSP
#   signature-only path that must stay complete.
# - FlowEngine.cs line ~160 registers playback (no context yet); line ~168 registers
#   context-dependent fns; line ~178 GranularFunctions.Register is the direct-call
#   precedent to mirror.
@flow-lang/Core/FlowEngine.cs

# CaptureMode offline test precedent (mirror this exactly)
@flow-lang.Tests/Integration/Sweep0615/PlaySongErgonomicsTests.cs
</context>

<design_decisions>
## Approach A chosen over Approach B — rationale

**Approach A (context-dependent play/stream): CHOSEN.** Reading the code confirmed it is
NOT disproportionately invasive — it reuses the exact `context.GetMusicalContext().Tempo`
seam the interpreter already uses (Interpreter.cs:316, SongRenderer.cs:459) and the
established "register a builtin directly against `_context` after it exists" wiring pattern
(FlowEngine.cs:178 `GranularFunctions.Register`). It fixes BOTH `play` and `stream`.

**Approach B (mirror tempo into Timeline from ExecuteMusicalContext): REJECTED.**
`Timeline.CurrentBPM` is `[ThreadStatic]` and `StreamSequence` renders on a `Task.Run`
thread, so B would silently fail to fix `stream`. It also leaves the two subsystems coupled
through a global mutable side-channel. Documented here so the executor does not revert to it.

## Registry semantics constraint (LOAD-BEARING)

`InternalFunctionRegistry.Register` APPENDS to a per-name list
(`_implementations[name].Add(...)`) — it does NOT override. So the `play(Sequence)` /
`stream(Sequence)` signatures must be registered EXACTLY ONCE. Therefore MOVE them out of
`Register(registry, manager)` into the new `RegisterContextDependent(...)` — do NOT
re-register (that would create duplicate identical signatures and an ambiguous resolve).

## Scope boundary (cross-check completed)

Only `play(Sequence)` and `stream(Sequence)` render a Sequence via the buggy path. `loop`
and `preview` are Buffer-only overloads (confirmed at PlaybackFunctions.cs:47-69) and are
OUT OF SCOPE. `MixVoicesToBuffer(voices, totalBeats, sampleRate, bpm)` already takes bpm as
a param — it is fed by `PlaySequence`'s local; the fix supplies the correct value.

## Test-surface decision (resolves the open question)

There is NO composer-observable offline `.flow` surface that shares the play/stream
tempo-resolution path: `play`/`stream` return Void; `renderSequenceToVoices` takes an
EXPLICIT bpm argument and never consults context (so it would not exercise the fix);
wrapping the stream in a Song uses the separate `SongRenderer` path. Per the diagnosis
fallback clause, the correct surface is an xUnit test in `flow-lang.Tests` driving the exact
`PlaySequence` path offline via CaptureMode (`FLOW_SUPPRESS_PLAYBACK=1` auto-enables it in
the test assembly — the `PlaySongErgonomicsTests` precedent reads
`engine.AudioManager.GetCapturedBuffer()`). No `.flow` script test is added because a
`.flow` script cannot observe the rendered duration of a Void-returning `play` call.
</design_decisions>

<tasks>

<task type="auto">
  <name>Task 1: Resolve play/stream Sequence tempo from the active MusicalContext</name>
  <files>flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs, flow-lang/StandardLibrary/BuiltInFunctions.cs, flow-lang/Core/FlowEngine.cs</files>
  <action>
Implement Approach A (per D-design above), reusing the `context.GetMusicalContext().Tempo`
seam so `tempo { }` blocks reach direct sequence playback.

In `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (`using FlowLang.Runtime;` is
already present):
  1. In `Register(InternalFunctionRegistry registry, AudioPlaybackManager manager)` REMOVE
     the `play(Sequence)` registration (the `playSeqSig` block) and the `stream(Sequence)`
     registration (the `streamSeqSig` block). Leave every Buffer/Song overload untouched.
  2. Add a new sibling method
     `public static void RegisterContextDependent(InternalFunctionRegistry registry, AudioPlaybackManager manager, FlowLang.Runtime.ExecutionContext context)`
     that registers those two signatures with tempo resolved from context. Each
     `registry.Register(` call MUST carry `ParameterNames: ["seq"]` (PlaybackFunctions.cs is
     under ParameterNamesCoverageTest — the invariant registerCount == paramNamesCount +
     varArgsCount must hold). The `play` lambda resolves the BPM then calls
     `PlaySequence(args, manager, bpm)`; the `stream` lambda resolves the BPM on the calling
     thread then calls `StreamSequence(args, manager, bpm)`. Resolution expression, used by
     both: the active tempo when present, else the Timeline fallback —
     `context.GetMusicalContext().Tempo ?? Timeline.GetBPM([]).As&lt;double&gt;()`. This
     preserves the `setBPM` workaround and the 120 default and never throws (charitable
     house style — no new advisory).
  3. Change `PlaySequence` to
     `private static Value PlaySequence(IReadOnlyList&lt;Value&gt; args, AudioPlaybackManager manager, double bpm)`:
     delete the `double bpm = Timeline.GetBPM([]).As&lt;double&gt;();` line (the current
     PlaybackFunctions.cs:159 root cause) and use the passed-in `bpm` for both
     `RenderSequenceToVoices(...)` and `MixVoicesToBuffer(...)`.
  4. Change `StreamSequence` to
     `private static Value StreamSequence(IReadOnlyList&lt;Value&gt; args, AudioPlaybackManager manager, double bpm)`:
     pass `bpm` into BOTH the Web synchronous fallback (`PlaySequence(args, manager, bpm)`)
     and the desktop `Task.Run(() => PlaySequence(args, manager, bpm))`. Because `bpm` is
     resolved in the registered lambda (the originating thread, where the MusicalContext
     stack is live) and captured by value, the background render uses the block tempo — the
     load-bearing reason Approach B was rejected.

In `flow-lang/Core/FlowEngine.cs`: after the `BuiltInFunctions.RegisterContextDependentFunctions(internalRegistry, _context);`
call (line ~168), add `Audio.PlaybackFunctions.RegisterContextDependent(internalRegistry, _audioManager, _context);`
(mirror the direct `GranularFunctions.Register(internalRegistry, _context)` wiring — `_audioManager`
and `_context` are both available fields here).

In `flow-lang/StandardLibrary/BuiltInFunctions.cs` `RegisterSignaturesOnly`: after the
`RegisterContextDependentFunctions(proxy, dummyContext);` call (line ~174, where
`dummyContext` and `dummyAudio` both exist) add
`Audio.PlaybackFunctions.RegisterContextDependent(proxy, dummyAudio, dummyContext);` so the
LSP still enumerates the play(Sequence)/stream(Sequence) signatures (keeps
BuiltInFunctionsTests' "play"/"stream" name assertions and the LSP surface complete).
  </action>
  <verify>
    <automated>dotnet build flow-lang/flow-lang.csproj 2>&amp;1 | tail -3 &amp;&amp; dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web 2>&amp;1 | tail -3</automated>
  </verify>
  <done>Both Desktop and FlowTarget=Web builds report 0 errors. `play(Sequence)` and `stream(Sequence)` are registered exactly once (in RegisterContextDependent). `PlaySequence` no longer reads `Timeline.GetBPM` directly; it and `StreamSequence` take a resolved `double bpm`. Every `registry.Register(` in PlaybackFunctions.cs still declares ParameterNames.</done>
</task>

<task type="auto">
  <name>Task 2: xUnit regression — tempo block scales play() rendered duration</name>
  <files>flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs</files>
  <action>
Create `flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs` (namespace
`FlowLang.Tests.Integration.Sweep0702`, `[Collection("FlowScripts")]`), mirroring the
CaptureMode pattern in `PlaySongErgonomicsTests.cs` (CaptureMode is auto-enabled in this
assembly via `FLOW_SUPPRESS_PLAYBACK=1`, so `(play seq)` routes its mix into the capture
buffer instead of PulseAudio).

Add a helper `RenderFrames(int bpm)` that: constructs a fresh `FlowEngine`; asserts
`engine.AudioManager.CaptureMode` is true; executes
`use "@audio"\ntempo {bpm} {{ (play | C4 D4 E4 F4 G4 A4 B4 C5 | ) }}\n`; reads
`engine.AudioManager.GetCapturedBuffer()` (assert non-null, Frames > 0) and returns
`buffer.Frames`. Use a fresh engine per call because `GetCapturedBuffer()` clears the buffer.

Tests:
  1. `PlaySequence_TempoBlock_ScalesRenderedDuration`: `frames120 = RenderFrames(120)`,
     `frames480 = RenderFrames(480)`; assert `(double)frames120 / frames480` is in
     [3.5, 4.5] (analytic ratio is 4.0 — same 8 quarter-note stream, 4x tempo → ~1/4 the
     frames). This is the assertion that fails RED before Task 1 (both would be ~176400
     because the Timeline default 120 ignores the block).
  2. `PlaySequence_TempoBlock_MapsToAnalyticFrameCount`: assert `RenderFrames(120)` is within
     a small tolerance of 176400 (8 beats x 60/120 s x 44100 Hz — the sine synth / 44.1 kHz
     mono path in PlaySequence), pinning the absolute BPM→duration mapping, not just the ratio.
  3. `PlaySequence_NoTempoBlock_DefaultsTo120`: render the same stream with NO tempo block
     (`use "@audio"\n(play | ... | )`) and assert its frame count equals `RenderFrames(120)`
     within tolerance — proves the charitable Timeline fallback (default 120) holds.

Add a class-doc comment recording the test-surface decision (why xUnit + CaptureMode and not
a `.flow` script — play/stream return Void and share no composer-observable offline duration
surface) and a note that `stream`'s Task.Run render shares the identical resolved-bpm value
(captured in the registered lambda before dispatch), so it is covered by construction; an
automated timing assertion on `stream` is intentionally omitted because the background
capture populates asynchronously (racy).
  </action>
  <verify>
    <automated>dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~TempoAffectsPlaybackTests" 2>&amp;1 | tail -15</automated>
  </verify>
  <done>All three facts pass. The ratio test proves `tempo 480 { }` renders the stream ~4x shorter than `tempo 120 { }`; the analytic and default-120 tests pin the absolute mapping and the charitable fallback.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

No new trust boundary is introduced. Input is composer-authored `.flow` source (already
trusted-input perimeter); the change only re-routes an internal tempo lookup from a
ThreadStatic global to the scoped MusicalContext stack. No new packages, no I/O, no network.

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-ijs-01 | Denial of Service | `tempo N { }` with huge N reaching MixVoicesToBuffer frame allocation | low | accept | `ExecuteMusicalContext` already rejects non-positive tempo via `MusicalContext.IsValidTempo`; a large-but-valid BPM produces a SHORTER buffer (fewer frames), so this fix cannot increase allocation vs. the pre-fix 120 default. No new guard needed. |
| T-ijs-02 | Tampering | none — no dependency/package install in this task | low | accept | Zero new NuGet packages; no `dotnet add package`. |
</threat_model>

<verification>
- `dotnet build flow-lang/flow-lang.csproj` and `... -p:FlowTarget=Web` both 0 errors.
- `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~TempoAffectsPlaybackTests"` all green.
- Regression guard: `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~ParameterNamesCoverageTest"` stays green (PlaybackFunctions.cs invariant preserved) and `... ~BuiltInFunctionsTests` stays green ("play"/"stream" still enumerable).
- Smoke: `dotnet run --project flow-interpreter -- -e 'use "@audio"
tempo 480 { (play | C4 D4 E4 F4 | ) }'` exits 0 (audio suppressed / backend-absent is fine — no throw).
</verification>

<success_criteria>
- `tempo N { (play seq) }` and `tempo N { (stream seq) }` render at N BPM; doubling N halves the rendered frame count.
- Absent a tempo block, playback falls back to `Timeline.GetBPM()` (default 120, `setBPM` still honored) with no exception and no new advisory.
- `play(Sequence)` / `stream(Sequence)` registered exactly once; LSP signature surface unchanged; ParameterNamesCoverageTest invariant intact.
- Desktop + Web builds green; full suite zero new regressions.
- NOTE (out of scope): the live flowlang.dev playground will not reflect this until `bash flow-site/scripts/sync-runtime.sh` regenerates the committed WASM bundle — a separate manual step, intentionally NOT part of this task.
</success_criteria>

<output>
Create `.planning/quick/260702-ijs-fix-tempo-block-being-ignored-by-play-st/260702-ijs-SUMMARY.md` when done.
</output>
