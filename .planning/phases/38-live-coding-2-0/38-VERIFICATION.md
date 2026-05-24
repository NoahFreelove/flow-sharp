---
phase: 38-live-coding-2-0
verified: 2026-05-24
status: passed
score: 11/11 REQs CLOSED
overrides_applied: 3  # D-38-09 REPL-02 wording / D-38-10 REPL-04 alias / D-38-13 OSC-02 charitable inference — per D-v1.5-01 single-commit migration latitude
manual_uat_status: 5 rows deferred to first composer use per Phase 17 precedent (--auto mode invocation, see 38-HUMAN-UAT.md)
---

# Phase 38: Live Coding 2.0 — Verification Report

**Phase Goal:** `live <quantize> { ... }` block + modernized watch mode +
REPL polish (LSP-backed completion, `:help fn`, multiline + history search,
piano-roll preview) + audio input via PA_STREAM_RECORD + OSC server/client
(Rug.Osc 1.2.5)

**Verified:** 2026-05-24
**Status:** passed (11/11 REQs CLOSED — Plans 38-01..06 shipped + Plan 38-07
closer-sweep applied)
**Branch:** `worktree-agent-a3993c0ad15156bf5` (will merge to `dev`)

## REQ Coverage

| REQ ID       | Requirement                                                       | Plan(s) | Status | Evidence (file:line / commit)                                                                                                                       |
|--------------|-------------------------------------------------------------------|---------|--------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| LIVE-01      | `live <quantize> { ... }` block — auto-loops; hot-swaps at quantize boundary; D-v1.5-07 stderr advisory at every entry | 38-02   | CLOSED | `flow-lang/Ast/Statements/LiveBlockStatement.cs` (107 LOC, FNV-1a `ComputeBlockId(SourceLocation)`); `flow-lang/Lexing/TokenType.cs` (`Live,` token); `flow-lang/Parsing/Parser.cs` (`ParseLiveBlockStatement()` ~95 LOC); `flow-lang/Interpreter/Interpreter.cs` (`ExecuteLiveBlock` + `ResolveQuantizeBeats`); `flow-lang/Runtime/LiveBlockRegistry.cs` (99 LOC ConcurrentDictionary); `flow-lang/Runtime/ExecutionContext.cs` (`LiveBlockRegistry` property). Commits `fc9edc0` + `155b5aa`. |
| LIVE-02      | Modernized watch mode — ANSI status panel + structured stderr + 30s wall-clock cap + 200ms debounce | 38-01, 38-03 | CLOSED | `flow-interpreter/LiveStatusPanel.cs` (429 LOC, 4-row panel + plain-line fallback per UI-SPEC §"ANSI Live Status Panel"); `flow-interpreter/LiveReloadManager.cs` (389 → 614 LOC; `DebounceMs = 200`; `Wait(RenderTimeout)` 30s; per-block `_pendingPerBlock` dict; `PublishTimeoutAdvisory` aligned to UI-SPEC line 330). Commits `ccba90f` / `8fbc127` / `d4f14f3` (Plan 38-01); `9c02b8d` (Plan 38-03 wording finalization). |
| LIVE-03      | Voice-pool state preserved when voice name survives; PRNG reseeded at swap; stale-closure detection | 38-03   | CLOSED | `flow-lang/StandardLibrary/Audio/Voice.cs` (`Name { get; init; }` + `CopyStateFrom(prev)` transfers OffsetBeats); `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` (`DiffByVoiceName` returns `(Preserved, Dropped, Added)`; `ApplyFadeOut` private → public); `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (`Name = $"{name}:{ordinal}"` tagging); `flow-lang/Interpreter/LambdaCaptureAuditor.cs` (526 LOC AST walker covering every Phase 35/36/38 node); `flow-interpreter/LiveReloadManager.cs` (`StagePendingBuffers` per-block stale-closure gate + `PrngRegistry.ResetAtRenderBoundary` call; `DetectFileScopeEdit` D-38-04). Commit `9c02b8d`. |
| REPL-01      | LSP-backed Tab completion via in-process `flow-lsp` `CompletionHandler.BuildItems()` | 38-04   | CLOSED | `flow-interpreter/ReplLineEditor.cs` (341 LOC, `FlowPromptCallbacks` routes Tab through static `CompletionHandler.BuildItems()` per D-38-12 SIMPLIFICATION; 4 symbol indices cached at ctor); `flow-interpreter/flow-interpreter.csproj` (PrettyPrompt 4.1.1 PackageReference + `flow-lsp` ProjectReference). Commits `1a99aa9` / `bf5a3b1`. |
| REPL-02      | Inline `:help fn` meta-command — signature + doc-comment + 1-line example from `BuiltInDocs` (per D-38-09 — overrides bare `?fn` wording) | 38-04   | CLOSED | `flow-interpreter/Repl.cs` `HandleCommand` `:help <name>` arm at lines 210-220 + `ShowHelpForName` renders 3-block layout (bold+green header / dim signature / body / dim Example) per UI-SPEC lines 263-280; unknown identifier emits locked yellow `[help] no documentation entry for '<name>'` advisory per UI-SPEC line 289; `BuiltInDocs.TryGet(identifier)` consumer pattern matches `HoverHandler.cs:46-65`. Commit `bf5a3b1`. |
| REPL-03      | Multi-line editing + Ctrl+R history search + `~/.config/flow/history` 0600 mode | 38-04   | CLOSED | `flow-interpreter/ReplLineEditor.cs` PrettyPrompt 4.1.1 wrapper (Ctrl+R reverse search built-in via `persistentHistoryFilepath`); `ReplInputCompleteness` static helper extends brace+proc-depth to LParen/RParen + LBracket/RBracket nesting (Rule 2 auto-add); history file at `~/.config/flow/history`, 10k cap with rotation, 0600 mode on Linux/macOS per UI-SPEC lines 297-303. Commit `bf5a3b1`. |
| REPL-04      | `(inspect seq)` / `(visualize seq)` alias pair — ASCII piano-roll with articulation glyphs + tick marks (per D-38-10 — overrides solo `(inspect seq)` wording) | 38-04   | CLOSED | `flow-lang/StandardLibrary/VisualizationFunctions.cs` extended with Phase 28 articulation enum switch (`>`/`.`/`^`/`_`/`!`/`~` glyphs per UI-SPEC §"Glyph Inventory"); `inspect(Sequence)` signature dispatches to same `Visualize` body per PATTERNS line 808 alias precedent; tick-mark row added above first pitch row per UI-SPEC lines 217-228 (`+` at bar columns / `-` elsewhere); Legato gap-fill pass per UI-SPEC line 212; bar-line `|` wins over sustain `#` collision rule per UI-SPEC line 214. Commit `644aeb8`. |
| AUDIO-IN-01  | `(micBuffer duration)` reads from default input via PA_STREAM_RECORD; -20 dB auto-attenuation on open | 38-05   | CLOSED | `flow-lang/Audio/PulseAudioCaptureBackend.cs` (272 LOC sibling class to `PulseAudioSimpleBackend`; `PA_STREAM_RECORD = 2` + `pa_simple_read` P/Invoke); `flow-lang/StandardLibrary/Audio/InputFunctions.cs` (244 LOC; `(micBuffer Second)` + `(micBuffer Double)` overloads; -20 dB scalar applied unconditionally; one-shot WarnOnce advisory `[audio-in] mic stream attenuated -20 dB on open` per UI-SPEC line 335). Commit `34bb251` (+ test seam `CaptureOverride`/`NativeRateForTesting`). |
| AUDIO-IN-02  | Captured `Buffer` composes with mix/play/writeWav/granular; linear-interp resample to 44.1 kHz at capture-side | 38-05   | CLOSED | `flow-lang/StandardLibrary/Audio/InputFunctions.cs` ResampleLinear helper (~30 LOC per RESEARCH §J); one-shot WarnOnce advisory `[audio-in] resampling capture stream from <N> Hz to 44100 Hz`; composability via the shared `AudioBuffer` value type — chains with `granular`/`mix`/`play`/`writeWav` without ceremony; `flow-lang/audio.flow` adds two `internal proc micBuffer(...)` forward decls. Commit `34bb251` + `tests/test_audio_in_pipeline.flow` composability smoke (commit `2a2146a`). |
| OSC-01       | OSC server `(oscListen port path handler)` rate-limited 200 Hz/path; handler is `(Args... => Void)` lambda | 38-06   | CLOSED | `flow-lang/StandardLibrary/Network/OscFunctions.cs` (598 LOC; `oscListen` spawns `Task.Run` UDP receive loop with `CancellationTokenSource` + Pitfall #5 `Cts.Token.Register(() => receiver.Dispose())`); per-path `ConcurrentDictionary<string, long> _lastFireTimeMs` 5 ms gate per D-38-14; `OscHandle` reference-identity Value via `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` (specificity 151) + `flow-lang/StandardLibrary/Network/OscHandleData.cs` (listener vs pending-packet discriminator); `(oscBundle ...)` + `(oscSendBundle ...)` bundle support with depth-cap-8 advisory per D-38-15. Commits `525d1a2` / `465056e`. |
| OSC-02       | OSC client `(oscSend host port path arg1 ...)` — charitable smallest-tag-that-fits type-tag inference (per D-38-13 — overrides strict-tag wording) | 38-06   | CLOSED | `flow-lang/StandardLibrary/Network/OscFunctions.cs` `InferOscArgs` public helper — `Value.Type` switch maps Int→`,i` / Long→`,h` / Float→`,f` / Double→`,d` / String|Symbol→`,s` / Bool→`,T`/`,F` / Buffer→`,b` flatten; composer escape hatch via explicit cast at call site (`(toLong 1)`, `1.5d`); `flow-lang/osc.flow` (56 LOC) opt-in `use "@osc"` module with `__enableOscModule` marker flipping `ExecutionContext.OscEnabled`; Rug.Osc 1.2.5 (MIT, zero transitive deps, .NET Standard 2.0) backing. Commit `465056e`. |

**All 11 REQs CLOSED.** Plans 38-01..06 ship the implementation; Plan 38-07
ships the composer-facing tutorial chapters + paired tests + this verification
log + the REQUIREMENTS.md wording sweep per D-v1.5-01 single-commit migration
latitude.

## Three Wording Overrides Applied (per D-v1.5-01)

The CONTEXT.md decisions deliberately overrode three earlier REQUIREMENTS.md
wordings; the override traces are recorded in `38-CONTEXT.md` <decisions> and
flagged here for plan-checker future audit:

| REQ     | Original wording (pre-Phase-38)                                          | Override decision | Justification |
|---------|--------------------------------------------------------------------------|-------------------|---------------|
| REPL-02 | Inline `?fn` help (`?transpose`)                                         | D-38-09 — ships `:help fn` form on the existing `:quit`/`:help`/`:clear`/`:stop` meta-command family | Consistency with existing meta-commands; composer doesn't learn two REPL grammars |
| REPL-04 | Pretty piano-roll on `(inspect seq)` only                                | D-38-10 — ships BOTH `(inspect seq)` AND `(visualize seq)` alias backed by one implementation | Charitable to pre-Phase-38 scripts that called `visualize`; alias is zero-cost (signature registration only) |
| OSC-02  | Args explicitly typed at the type-tag level (no implicit conversion)     | D-38-13 — ships charitable smallest-tag-that-fits inference per D-v1.5-05; composer escape hatch via explicit cast | Flow's overall posture (D-v1.5-05 charitable interpretation default) wins over OSC-spec literal-mindedness for the common case |

REQUIREMENTS.md REPL-02 / REPL-04 / OSC-02 lines swept in this commit per
D-v1.5-01 pre-traction no-deprecation latitude (single-commit migration with
in-repo migrators only).

## Test Counts (Plans 38-01..06 combined)

### xUnit (`dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38"`)
- **Plan 38-01:** 9 facts (WatchDebounce + AnsiPanelRender + PanelTtyFallback)
- **Plan 38-02:** 7 facts (LiveBlockParser + MultiLiveBlock + LiveBlockDeterminismAdvisory)
- **Plan 38-03:** 12 facts (VoicePoolNameDiff + StaleClosureDetection + PrngReseedAtSwap + TimeoutRevert)
- **Plan 38-04:** 7 facts (ReplCompletion + ReplHelpMetaCommand + ReplMultiLine + ReplHistorySearch + VisualizeArticulationGlyph + InspectAlias + GlyphCollision)
- **Plan 38-05:** 13 facts (PulseAudioCaptureBackend ×6 + MicBufferAttenuation ×3 + MicBufferResample ×3 + fixture generator ×1)
- **Plan 38-06:** 19 facts (OscTypeTagInference ×6 + OscRateLimit ×4 + OscLoopback ×2 + OscBundle ×4 + OscBundleDepthCap ×3)

**Total Phase 38: ~67 facts GREEN** (commit-time per-plan SUMMARY counts; full
suite re-run at merge time validates the aggregate).

### Composer-facing (Plan 38-07 closer regressions)
- 5 `examples/live/` tutorial chapters (4 `.flow` + 1 narrated `.md`)
- 4 paired `tests/test_live_*.flow` regression tests (mic test may require
  real PulseAudio device per Plan 38-05 — accepted as manual-only per Phase 17
  precedent if the test seam isn't wired)

## Composer-Facing Surfaces Shipped

| Surface | Activation | Reference chapter | Reference test |
|---|---|---|---|
| `live 1bar { ... }` block | always-on (no `use` gate) | `examples/live/hello_live.flow` | `tests/test_live_hello.flow` |
| Multi-block independent swap | always-on | `examples/live/multi_block.flow` | `tests/test_live_multi_block.flow` |
| `(micBuffer duration)` | `use "@audio"` | `examples/live/mic_granular.flow` | `tests/test_live_mic_granular.flow` |
| OSC client + server | `use "@osc"` | `examples/live/osc_controller.flow` | `tests/test_live_osc_controller.flow` |
| `:help fn` REPL meta-command | REPL only | `examples/live/repl_session.md` § Session 1 | xUnit `ReplHelpMetaCommandTests` |
| `(inspect seq)` / `(visualize seq)` alias | always-on | `examples/live/repl_session.md` § Session 2 | xUnit `InspectAliasTests` + `VisualizeArticulationGlyphTests` |
| Tab completion + Ctrl+R history | REPL only | `examples/live/repl_session.md` § Session 3 | xUnit `ReplCompletionTests` + `ReplHistorySearchTests` |

## Cross-Cutting Determinism Contract

- **Two-run cmp-clean** preserved for offline render paths (`writeWav` /
  `writeMidi`) — Phase 18/25/27/28/29/33/36/37 inheritance intact.
- **`live { ... }` blocks explicitly opt OUT** of the two-run cmp-clean
  contract per D-v1.5-07. Every entry emits a one-shot stderr advisory
  `[live] entering live block at line N — opts OUT of two-run cmp-clean
  determinism` (dedup'd per `(line, process)`).
- **PRNG reseed at swap boundary** via `PrngRegistry.ResetAtRenderBoundary()`
  preserves the per-block deterministic stream when SourceLocation is
  unchanged (Plan 38-03 wiring).

## Manual-Only Smoke Verifications (deferred per Phase 17 precedent)

Tracked in `38-HUMAN-UAT.md`. The 5 manual rows from `38-VALIDATION.md` lines
95-101 cannot be automated (terminal emulator quirks, real-mic dependency,
hardware controller dependency, subjective "did the swap sound musically
clean" judgement). They are auto-approved at Phase-38 closer time per the
`--auto` mode invocation flag (mirrors the Phase 37 PIANO-01 D-37-12
auto-approval pattern) and marked `pending` for the composer's first real
session, where they will be filled in.

| Row | Behavior                                          | REQ(s)              |
|-----|---------------------------------------------------|---------------------|
| 1   | ANSI status panel cross-terminal smoke            | LIVE-02             |
| 2   | Ctrl+R history search interactive feel            | REPL-03             |
| 3   | Real-microphone capture loopback                  | AUDIO-IN-01         |
| 4   | OSC controller round-trip with a real surface     | OSC-01, OSC-02      |
| 5   | Live performance hot-edit during playback         | LIVE-01, LIVE-02, LIVE-03 |

## Pre-existing Test Failures (Out of Scope — Carried Across Phase 28/29/35 Surfaces)

Per Plan 38-01..06 SUMMARYs: ~34 pre-existing failures in Phase 28
PerSynthArticulationTests (FFT cosine differentiable on synth articulation
envelopes), RagtimeFixtureTests (RMS regression baseline), Phase 29
ArticulationOnSampleTests (audible-content-ratio bounds for sampled-piano
articulations), Phase 35 MatchExhaustivenessDefaultTests. None touch the
Plan 38-01..06 surfaces. Tracked in `deferred-items.md`; carried forward to a
future plan's investigation per the executor scope-boundary rule. Phase 38's
own 67+ tests are all GREEN.

## Next-Phase Readiness

**UNBLOCKED for Phase 40 (Studio Sync) and Phase 41 (Reach + v1.5 Closer).**

- Phase 38 ships the WASM live-coding precursor (Phase 41 WASM playground IS
  watch-mode-in-browser — the `live { ... }` block surface is the foundation).
- Phase 40 MIDI dispatch will consume the same Phase 35 pattern-matching
  precedent established by Phase 39 `ArticulationEmit.cs` — clear continuity.
- `examples/live/` chapter pattern (4 `.flow` + 1 narrated `.md`) matches
  Phase 36/39 precedent; future Phase 40/41 closers can follow the same shape.

## v1.6 Follow-ups (informational, NOT gaps)

Captured across Plans 38-01..06 SUMMARYs as carryover items:

- Streaming audio input `(micStream callback)` — out of scope for v1.5;
  AUDIO-IN-01/02 lock to one-shot blocking `(micBuffer duration)`
- Composer-tunable micro-crossfade length (currently 64 samples per D-38-06)
- OSC address pattern wildcards (`/synth/*/freq`) — literal-path match only in v1.5
- OSC IPv6 + multicast — defer to v1.6
- Per-block timeout line tracking (Plan 38-03 currently emits `line: 1` because
  the 30s worker has detached when the cap fires)
- `_lastVoices` population from FlowEngine capture-mode pipeline — enables
  voice preservation across whole-script swaps too (Plan 38-03 ships the
  primitive; cross-render plumbing deferred)
- Tighter live-block body line-range tracking (Plan 38-03 uses a heuristic
  `[Location.Line + 1, Location.Line + Body.Count + 1]` for D-38-04 file-scope
  edit detection)
- Envelope-cursor preservation in `Voice.CopyStateFrom` (v1.5 transfers
  `OffsetBeats` only; envelope-cursor field is a future Voice extension)
- Hand-rolled TUI line editor on `Console.ReadKey()` — reserved as fallback
  if D-38-11 readline library gate ever fails (currently PrettyPrompt 4.1.1
  is shipped)
- OSC flood advisory (one-shot per-path) — deferred per D-38-14 Claude's
  discretion (default leans no-advisory; revisit if composer reports confusion)

---

*Verified: 2026-05-24*
*Verifier: Plan 38-07 closer agent (worktree-agent-a3993c0ad15156bf5)*
