---
phase: 38-live-coding-2-0
plan: 02
subsystem: live-coding

# Dependency graph
requires:
  - phase: 38-live-coding-2-0
    provides: "Plan 38-01 LiveReloadManager rewrite (200ms debounce + ANSI status panel + 30s CancellationToken + per-block buffer dict skeleton)"
  - phase: 36-sequence-algebra-generative
    provides: "PrngRegistry FNV-1a stable-hash convention + ConcurrentDictionary-backed registry pattern + ResetAtRenderBoundary boundary-clear API"
  - phase: 32-full-scala-scl-tuning-loader
    provides: "TuningContextStatement parallel-AST pattern; ExecuteTuningContext PushFrame/PopFrame + try/finally scope discipline"
provides:
  - "LiveBlockStatement AST record + stable FNV-1a BlockId for per-block registry keying"
  - "`live <quantize> { ... }` block surface — Int + bar/bars suffix, NoteValue q/h/w/e/s, omitted-default-1bar"
  - "LiveBlockRegistry runtime — ConcurrentDictionary<int, LiveBlockRegistration> with Register/Snapshot/Clear API"
  - "D-v1.5-07 stderr advisory: `[live] entering live block at line N — opts OUT of two-run cmp-clean determinism` once per (line, process)"
affects: [38-03, 38-04, 38-07, 41-wasm-live-coding]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sister-AST pattern reused — LiveBlockStatement parallels TuningContextStatement (which parallels MusicalContextStatement) for keyword-block dispatch"
    - "FNV-1a 32-bit stable-hash convention extended from PrngRegistry's seed derivation to BlockId computation (same offset basis 2166136261 + prime 16777619)"
    - "Singleton-per-ExecutionContext registry shape from PrngRegistry replicated for LiveBlockRegistry"
    - "Charitable interpretation (D-v1.5-05) inside ResolveQuantizeBeats — unknown payload types fall back to 1 bar so the live session never dies mid-set (Pitfall #12)"

key-files:
  created:
    - flow-lang/Ast/Statements/LiveBlockStatement.cs
    - flow-lang/Runtime/LiveBlockRegistry.cs
    - flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs
    - flow-lang.Tests/Integration/Phase38/MultiLiveBlockTests.cs
    - flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs
  modified:
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/Runtime/ExecutionContext.cs

key-decisions:
  - "LiveBlockStatement is a sister AST node to MusicalContextStatement / TuningContextStatement (NOT a new MusicalContextType enum variant) — body carries IReadOnlyList<Statement> + a stable BlockId that the new MusicalContextStatement shape doesn't model"
  - "BlockId is FNV-1a 32-bit of (FileName + Line + Column) — same constants PrngRegistry uses so every stable-hash site in the engine shares one formula"
  - "Quantize parse forms collapse to a 1-bar default literal at the `live` keyword's location when omitted — preserves SourceLocation anchoring for diagnostics + BlockId stability"
  - "ResolveQuantizeBeats is intentionally charitable: unknown NoteValue suffix falls back to quarter notes; non-Int/non-String payload falls back to 1 bar's beats — keeps live coding flowing per Pitfall #12"
  - "LiveBlockRegistry backed by ConcurrentDictionary because Plan 38-01 LiveReloadManager runs renders on Task.Run while audio plays on the audio thread (two-actor read/write pattern)"

patterns-established:
  - "Pattern: keyword-block AST creation — add TokenType enum entry + lexer keyword switch entry + Parser dispatch arm + new sister AST record + Interpreter case branch + (if stateful) per-context registry; the LiveBlockStatement implementation now joins TuningContextStatement as a canonical worked example"
  - "Pattern: FNV-1a stable BlockId — when an AST node needs an identity that survives re-parses, compute FNV-1a of its SourceLocation using the PrngRegistry constants for cross-engine hash-formula uniformity"

requirements-completed: [LIVE-01]

# Metrics
duration: ~25min
completed: 2026-05-24
---

# Phase 38 Plan 02: `live <quantize> { ... }` Block Surface Summary

**Composer-facing `live <quantize> { body }` block + stable per-block FNV-1a BlockId + ConcurrentDictionary-backed LiveBlockRegistry — Plan 38-03's swap consumer now has the per-block hook it needs to wire voice-pool preservation, PRNG reseed, and stale-closure detection.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 3 (TDD: RED → GREEN for AST/lexer → GREEN for parser+interpreter+registry)
- **Files created:** 5 (2 production + 3 test)
- **Files modified:** 5 (lexer + parser + interpreter + token enum + execution context)

## Accomplishments

- `LiveBlockStatement` AST record (~107 LOC) with static `ComputeBlockId(SourceLocation)` helper using the PrngRegistry FNV-1a constants
- `live` keyword recognized by lexer + parser dispatch + interpreter execution branch covering all three quantize forms per D-38-02 (Int + bar/bars suffix, NoteValue q/h/w/e/s, omitted-default-1bar)
- `LiveBlockRegistry` runtime (~99 LOC) mirroring PrngRegistry shape so Plan 38-03's `LiveReloadManager.StagePendingBuffers` can address blocks by BlockId across re-renders
- D-v1.5-07 stderr advisory `[live] entering live block at line N — opts OUT of two-run cmp-clean determinism` deduped via `RenderingDiagnostics.WarnOnce` sentinel `live-determinism-optout:<line>`
- 7 new xUnit tests all GREEN; existing `tests/test_live_reload.flow` (no `live { }` block) byte-identical per D-38-01

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 failing tests** — `3a37a1d` (test) — 3 test files, RED phase confirmed via compile errors on `LiveBlockStatement` / `LiveBlockRegistry` / `TokenType.Live`
2. **Task 2: AST record + Lexer token + keyword** — `fc9edc0` (feat) — `LiveBlockStatement.cs` + `TokenType.Live` + lexer switch entry; flow-lang builds clean
3. **Task 3: Parser dispatch + ExecuteLiveBlock + LiveBlockRegistry + ExecutionContext property** — `155b5aa` (feat) — all 7 Task 1 tests GREEN; smoke `dotnet run --project flow-interpreter -- -e 'live 1bar { (print "in live") }'` prints "in live" and emits the D-v1.5-07 advisory to stderr

## Files Created/Modified

**Created:**
- `flow-lang/Ast/Statements/LiveBlockStatement.cs` (107 LOC) — record carrying Location + QuantizeValue + Body + BlockId + Span; static `ComputeBlockId(SourceLocation)` uses FNV-1a 32-bit (offset basis `2166136261u`, prime `16777619u`) over UTF-8 bytes of FileName + little-endian Line + little-endian Column. xmldoc cites D-v1.5-07, D-38-02, RESEARCH §A, T-38-AST.
- `flow-lang/Runtime/LiveBlockRegistry.cs` (99 LOC) — sealed `LiveBlockRegistry` class with `ConcurrentDictionary<int, LiveBlockRegistration>` and `Register` / `Snapshot` / `Clear`; sibling `LiveBlockRegistration` record (BlockId / Location / Body / QuantizeBeats).
- `flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs` (148 LOC) — 4 [Fact]s: single 1bar / quarter-note / omitted-default / deterministic BlockId across re-parses.
- `flow-lang.Tests/Integration/Phase38/MultiLiveBlockTests.cs` (105 LOC) — 2 [Fact]s: distinct BlockIds for two-blocks-one-file + both registered in `_context.LiveBlockRegistry`.
- `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` (78 LOC) — 1 [Fact]: D-v1.5-07 advisory fires exactly once per (line, process) across two FlowEngine.Execute calls in the same process.

**Modified:**
- `flow-lang/Lexing/TokenType.cs` — adds `Live,` enum value adjacent to `Tuning,` with trailing comment `// Phase 38 (LIVE-01) — live <quantize> { ... } block (D-38-02)`.
- `flow-lang/Lexing/SimpleLexer.cs` — adds `"live" => TokenType.Live,` to the keyword switch adjacent to `tuning` / `voicePool` / `sustainPedal`.
- `flow-lang/Parsing/Parser.cs` — adds `if (Match(TokenType.Live)) return ParseLiveBlockStatement();` dispatch after the Tuning branch (~line 178); new `ParseLiveBlockStatement()` (~95 LOC) handles all three quantize forms.
- `flow-lang/Interpreter/Interpreter.cs` — adds `case LiveBlockStatement live: ExecuteLiveBlock(live); break;` adjacent to the existing musical-context / tuning-context cases; new `ExecuteLiveBlock(LiveBlockStatement)` (~50 LOC) + `ResolveQuantizeBeats(Value)` helper (~30 LOC) — emits advisory, resolves beats, registers a `LiveBlockRegistration`, executes the body once in a scope frame.
- `flow-lang/Runtime/ExecutionContext.cs` — adds `public LiveBlockRegistry LiveBlockRegistry { get; } = new();` property adjacent to `PrngRegistry`.

## FNV-1a Constants Used

Both `LiveBlockStatement.ComputeBlockId` and `PrngRegistry.ComputeDeterministicSeed` now share:
- Offset basis: `2166136261u`
- Prime: `16777619u`
- Byte order for integer fields: little-endian (LSB first), 4 bytes per int

`ComputeBlockId(SourceLocation)` hash inputs in order: UTF-8 bytes of `FileName` (null → empty), 4 little-endian bytes of `Line`, 4 little-endian bytes of `Column`.

## Test Pass Count

- 7 new Phase 38 Plan 38-02 tests: ALL PASS
- Full Phase 38 test surface (16 tests across Plan 38-01's 9 + our 7): ALL PASS
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38" --no-build` → `Passed: 16, Failed: 0`
- Smoke `dotnet run --project flow-interpreter -- -e 'live 1bar { (print "in live") }'` → exits 0, prints "in live" + advisory
- Smoke `dotnet run --project flow-interpreter tests/test_live_reload.flow` (no `live { }` block) → exits 0, byte-identical to pre-change behavior per D-38-01

## Decisions Made

- **Sister AST node, not a MusicalContextType enum variant:** the new shape carries Body (IReadOnlyList<Statement>) + BlockId — the existing MusicalContextStatement carries scalar Value/Value2 primitives, so a sister record is the smaller blast radius. Matches the TuningContextStatement precedent from Phase 32.
- **FNV-1a constants borrowed from PrngRegistry:** keeps every stable-hash site in the engine on one formula. Same offset basis + prime + byte ordering as `PrngRegistry.ComputeDeterministicSeed` (Phase 36 Plan 36-01 line 172).
- **Quantize String-payload encoding for NoteValue:** the parser captures `q`/`h`/`w`/`e`/`s` as a String literal (token text), not a NoteValueType data wrapper. Simpler than threading NoteValue construction through the parser; ResolveQuantizeBeats maps the suffix at registration time.
- **Charitable ResolveQuantizeBeats:** unknown String payload falls back to quarter, unknown payload type falls back to 1 bar — preserves the "live session never dies mid-set" lock from Pitfall #12.

## Deviations from Plan

**Total deviations: 1 minor — auto-fixed.**

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test file used wrong `ErrorReporter` API surface**
- **Found during:** Task 3 — test build failed with `error CS1061: 'ErrorReporter' does not contain a definition for 'GetErrors'`.
- **Issue:** `LiveBlockParserTests.ParseToProgram` helper referenced `errorReporter.GetErrors().FirstOrDefault()` — the actual API exposes `Errors` as an `IReadOnlyList<FlowError>` property, not a method.
- **Fix:** Changed both call sites to `errorReporter.Errors.FirstOrDefault()`.
- **Files modified:** `flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs`
- **Verification:** Test project builds clean, 4 LiveBlockParserTests [Fact]s all GREEN.
- **Committed in:** `155b5aa` (Task 3 — bundled with the production-symbol commit because the test file's fix is the bridge that makes the tests usable).

## Issues Encountered

**Orchestrator passed a corrupted base SHA at agent startup.** The full SHA `a8891cca12dc9ce7c0aa6e72d1f24fbc25b1f200` did not parse — the actual base commit is `a8891cc80170c097c0dde1b2aa421f2f3f616ac7`. The short prefix `a8891cc` is shared, so the worktree HEAD assertion (`worktree-agent-*` namespace check) passed, and I was able to recover by resolving the short prefix and resetting onto the correct full SHA. This is a low-stakes orchestrator-side issue but worth flagging — if the prefix had also conflicted, the worktree would have failed to acquire the planned base. **Mitigation:** the worktree branch namespace check + git's short-prefix resolution path were sufficient for recovery.

**Working-tree corruption during a base-commit regression sanity check.** While confirming that 32 pre-existing failures in Phase 28/29/35 (PerSynthArticulation FFT cosine, RagtimeFixture RMS, MatchExhaustiveness, ArticulationOnSample) were not introduced by Plan 38-02, I ran `git checkout -q a8891cc... -- flow-lang flow-lang.Tests` to inspect the base. This overwrote the working tree with the BASE's files (the commit `155b5aa` was intact, but the working tree no longer matched HEAD). Restored via `git checkout HEAD -- flow-lang flow-lang.Tests`; rebuilt and re-verified all 7 Phase 38 tests still GREEN. **Lesson:** for "is this pre-existing?" sanity checks, prefer a fresh worktree or a `git diff` against the base — never `git checkout <other-ref> -- <paths>` which silently mutates the working tree.

**Pre-existing test failures NOT caused by Plan 38-02 (informational):**
- `Phase28.PerSynthArticulationTests` (26 parameterized cases) — FFT cosine differentiation assertions on synth articulation envelope shapes
- `Phase28.RagtimeFixtureTests` (2) — RMS regression baselines for ragtime piano
- `Phase29.ArticulationOnSampleTests` (6 parameterized cases) — audible-content-ratio bounds for sampled piano articulations
- `Phase35.MatchExhaustivenessDefaultTests` (2) — match-expression exhaustiveness default-arm warnings

None of these touch the parser dispatch / lexer keyword switch / AST records / interpreter statement-dispatch surface that Plan 38-02 modifies. They predate our work (confirmed by the existing baseline state of those tests). Deferring to future plans / phases per the scope-boundary rule.

## User Setup Required

None — no external service configuration needed.

## Next Phase Readiness

**Plan 38-03 (LIVE-03 state preservation) is unblocked.** The per-block FNV-1a `BlockId` + `LiveBlockRegistry` snapshot API are exactly the hooks the swap consumer needs:

1. `LiveReloadManager.StagePendingBuffers` will read `engine.Context.LiveBlockRegistry.Snapshot()` to diff old vs. new live blocks by BlockId
2. Per-block voice-pool preservation hangs off the BlockId-keyed dict — voices whose Name survives the new buffer inherit prior state; voices not in the new list release
3. PRNG reseed at swap boundary: `engine.Context.PrngRegistry.ResetAtRenderBoundary()` already exists from Phase 36 Plan 36-01; Plan 38-03 just calls it at each swap
4. Stale-closure detection (`LambdaCaptureAuditor`) walks the new block's Body (already an `IReadOnlyList<Statement>` field on `LiveBlockRegistration`) — no AST surface changes required

The D-38-01 whole-script swap path (no `live { }` block) continues to work unchanged — additive parser change preserves the legacy `LiveReloadManager` behavior byte-identical.

## Self-Check: PASSED

All claimed files exist:
- `flow-lang/Ast/Statements/LiveBlockStatement.cs` ✓
- `flow-lang/Runtime/LiveBlockRegistry.cs` ✓
- `flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs` ✓
- `flow-lang.Tests/Integration/Phase38/MultiLiveBlockTests.cs` ✓
- `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` ✓

All claimed commits exist in git log:
- `3a37a1d` test(38-02): add Wave 0 failing tests for live block surface ✓
- `fc9edc0` feat(38-02): add LiveBlockStatement AST + TokenType.Live + lexer keyword ✓
- `155b5aa` feat(38-02): wire live block parser + interpreter + LiveBlockRegistry ✓

---
*Phase: 38-live-coding-2-0*
*Completed: 2026-05-24*
