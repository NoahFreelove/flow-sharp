---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 05
status: complete
requirements: [SPEC-7]
self_check: PASSED
test_count_before: 961
test_count_after: 970
new_facts: 9
commits:
  - 9e48075 feat(28-05): voicePool block + steal-oldest VoiceAllocator.AllocateWithPool
  - ce7466f test(28-05): voicePool unit + stress facts — 9 facts; AsyncLocal isolation
key_files:
  created:
    - flow-lang.Tests/Unit/Phase28/VoicePoolTests.cs
    - flow-lang.Tests/Integration/Phase28/VoicePoolStressTests.cs
  modified:
    - flow-lang/Ast/Statements/MusicalContextStatement.cs (+ VoicePool enum)
    - flow-lang/Runtime/MusicalContext.cs (+ VoicePoolSize field, Clone)
    - flow-lang/Runtime/ExecutionContext.cs (+ VoicePoolSize in GetMusicalContext ??= chain)
    - flow-lang/Parsing/Parser.cs (+ voicePool dispatch + ParseMusicalContextStatement case)
    - flow-lang/Interpreter/Interpreter.cs (+ VoicePool case with 1..256 range check)
    - flow-lang/StandardLibrary/Audio/VoiceAllocator.cs (+ AllocateWithPool, AsyncLocal instrumentation)
    - flow-lang/StandardLibrary/Audio/SequenceRenderer.cs (+ RenderSequenceToVoicesWithPool)
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs (RenderSection routes through pool overload)
---

## Plan 05 — Voice-Pool with Steal-Oldest

### What shipped

Full SPEC-7 plumbing chain from `voicePool N { ... }` syntax through
context inheritance to deterministic steal-oldest allocation.

1. **Lexer/Parser/AST**: voicePool keyword (already shipped Plan 28-01) →
   `MusicalContextType.VoicePool` enum value → top-level dispatch in
   Parser.cs (only when followed by IntLiteral) → `ParseMusicalContextStatement`
   case parsing the integer N.

2. **Interpreter validation** (`Interpreter.cs:251-269`): range check 1..256
   with the locked composer-facing error message
   `"Voice pool size must be between 1 and 256, got N"` pointing at the
   statement location. In-range values set `musicalCtx.VoicePoolSize`.

3. **Context inheritance**: `MusicalContext.VoicePoolSize` (nullable int)
   added with the `??=` chain in `ExecutionContext.GetMusicalContext`
   alongside Tempo / TimeSignature / Gain. Clone copies the field so
   nested musical-context blocks inherit naturally.

4. **VoiceAllocator.AllocateWithPool** (`VoiceAllocator.cs:103-156`):
   - Validates 1..256, throws `ArgumentOutOfRangeException` otherwise
   - Sorts voices by onset (ThenBy original index — deterministic
     tiebreaker preserving Phase 18/25/27 two-run-cmp-clean)
   - Maintains active set bounded by `poolSize`; on overflow steals the
     entry with smallest onset+idx (oldest)
   - Truncates stolen voice in-place via `TruncateVoiceBuffer` (5 ms
     fade-out + zero remaining frames) so the SongRenderer mix sums
     them correctly
   - Returns the original voices list (mutated in-place) — no allocations
     for non-overflow cases (`if (voices.Count <= poolSize) return voices;`)

5. **SequenceRenderer.RenderSequenceToVoicesWithPool** (new overload at
   `SequenceRenderer.cs:85`) defaults pool to 32 when `voicePoolSize`
   is null (SPEC-7 locked default). Legacy `RenderSequenceToVoices`
   overloads with `maxVoices = 1024` and the loudest-N policy preserved
   for backward compat (direct callers — tests, REPL — work unchanged).

6. **SongRenderer.RenderSection** (`SongRenderer.cs:189-191`) routes
   per-section through the new pool overload, reading
   `section.Context?.VoicePoolSize`.

### Test instrumentation: AsyncLocal isolation

`VoiceAllocator.LastPoolSizeUsedForTests` is `AsyncLocal<int?>` (not a
plain static) so xUnit's parallel cross-class test execution doesn't
race the value. Plan 28-04's `MultiTrackMidiTests` and Plan 28-05's
`VoicePoolTests` both render songs concurrently — without AsyncLocal
the static's last-write-wins overwrites mid-test and produces flaky
failures. AsyncLocal scopes the value to each test's logical execution
flow.

### Truths verified by xUnit

**Unit (`VoicePoolTests`, 6 facts):**
- `VoicePool_ParsesAndApplies` — voicePool 16 → pool=16
- `VoicePool_DefaultsTo32` — no override → 32 (SPEC-7 locked default)
- `VoicePool_RejectsOutOfRange_Zero` — error message exact substring
- `VoicePool_RejectsOutOfRange_TooBig` — error message exact substring
- `VoicePool_AcceptsBoundary_One` — pool=1 boundary
- `VoicePool_AcceptsBoundary_TwoFiftySix` — pool=256 boundary

**Integration (`VoicePoolStressTests`, 3 facts):**
- `VoicePool_50OnsetsStealOldest` — 50 onset-0 voices @ pool=32 →
  voices[0..17] truncated (peak < 0.001), voices[18..49] audible
  (peak > 0.1). Specific index assertions, not just "approximately
  18 truncated".
- `VoicePool_DeterministicTwoRun` — two AllocateWithPool runs over
  byte-identical input produce byte-identical mutated buffers.
- `VoicePool_ExplicitOverride_8` — pool=8 over 50 voices → 42 truncated,
  8 audible.

### Test counts

- Phase 28 facts (xUnit): **40/40 GREEN** (4 + 17 + 55 + 5 + 9 across
  Plans 01..05)
- Phase 22 LegatoFacts: **8/8 GREEN** (Phase 22 transform unaffected)
- Full suite: **970/970 GREEN** (was 961 — +9 net new) — verified
  twice consecutively (no flake from AsyncLocal isolation)

### Self-Check: PASSED

Build clean, all targeted tests pass, full suite green, no architectural
deviations from PLAN.md. The AsyncLocal vs plain-static fix is a Rule-1
implementation detail — the public observable contract (set the static,
read the static) is unchanged from PLAN's specification; only the
storage backing was upgraded to handle parallel test execution safely.

### Deviations

1. **AsyncLocal instrumentation** (over plain static in PLAN). Required
   to tame xUnit cross-class parallel races; documented in the
   `LastPoolSizeUsedForTests` xmldoc and the test commit message.

2. **GetMusicalContext extension** added beyond PLAN's task list — needed
   to make voicePool-set-on-an-outer-frame visible to a section snapshot.
   Without this, the section's `musicalContext` from `_context.GetMusicalContext()`
   would have `VoicePoolSize == null` and the renderer would always see
   the default 32. Same pattern as Phase 22 / Phase 23 added their fields.

### Hand-off to dependent plans

- **Plan 28-06 (test infra)** can use `VoicePoolStressTests`'s
  `BuildSimultaneousOnsetVoices` helper as a reference for synthesizing
  AudioBuffers without going through the full song-rendering pipeline.
  RMS regression baselines for any fixture with >32 simultaneous voices
  may need regeneration (steal-oldest may now truncate where Phase 27
  loudest-N kept different voices).
- **Plan 28-07 (UAT)** can stress-test by writing a Flow script with
  `voicePool 8 { ... 50-note piano cluster ... }` and listening for the
  ducking artifact (oldest notes drop out as new ones arrive).
