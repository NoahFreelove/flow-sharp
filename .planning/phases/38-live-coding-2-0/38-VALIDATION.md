---
phase: 38
slug: live-coding-2-0
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-23
approved: 2026-05-23
---

# Phase 38 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `38-RESEARCH.md` § Validation Architecture (27 tests mapped 1:1 against 11 REQs).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing `flow-lang.Tests/` C# project) + `.flow` script smoke tests under `tests/test_*.flow` (existing pattern, executed via `dotnet run --project flow-interpreter tests/file.flow`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); none required for `.flow` smoke tests |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38"` |
| **Full suite command** | `dotnet test flow-lang.Tests && for t in tests/test_live_*.flow tests/test_repl_*.flow tests/test_audio_in_*.flow tests/test_osc_*.flow tests/test_visualize_*.flow; do dotnet run --project flow-interpreter "$t" || exit 1; done` |
| **Estimated runtime** | ~60 seconds (xUnit ~30s + smoke ~30s) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38.{plan_id_underscored}"` (per-plan filter — keeps quick feedback under 15s)
- **After every plan wave:** Run quick command (above) for all completed Plan-IDs in the wave
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds (per-plan xUnit slice)

---

## Per-Task Verification Map

Pulled from `38-RESEARCH.md` § Validation Architecture. Plan/Task IDs finalized at plan-time; this table is a forward declaration the planner refines.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 38-01-* | 38-01 | 1 | LIVE-02 | T-38-21 (debounce) | 200ms debounce coalesces rapid saves | unit | `dotnet test --filter "Phase38.WatchDebounceTests"` | ❌ W0 | ⬜ pending |
| 38-01-* | 38-01 | 1 | LIVE-02 | — | ANSI panel renders 4 rows, redraws ≤10Hz | unit | `dotnet test --filter "Phase38.AnsiPanelRenderTests"` | ❌ W0 | ⬜ pending |
| 38-01-* | 38-01 | 1 | LIVE-02 | — | Plain-line fallback when `Console.IsOutputRedirected` | unit | `dotnet test --filter "Phase38.PanelTtyFallbackTests"` | ❌ W0 | ⬜ pending |
| 38-02-* | 38-02 | 2 | LIVE-01 | — | `live 1bar { ... }` parses into LiveBlockStatement AST | unit | `dotnet test --filter "Phase38.LiveBlockParserTests"` | ❌ W0 | ⬜ pending |
| 38-02-* | 38-02 | 2 | LIVE-01 | — | Multiple `live` blocks tracked independently per quantize | unit | `dotnet test --filter "Phase38.MultiLiveBlockTests"` | ❌ W0 | ⬜ pending |
| 38-02-* | 38-02 | 2 | LIVE-01, D-v1.5-07 | — | Stderr advisory emitted on every `live` block entry | unit | `dotnet test --filter "Phase38.LiveBlockDeterminismAdvisoryTests"` | ❌ W0 | ⬜ pending |
| 38-03-* | 38-03 | 3 | LIVE-03 | T-38-12 (live state) | Voice-pool state preserved when voice name survives | unit | `dotnet test --filter "Phase38.VoicePoolNameDiffTests"` | ❌ W0 | ⬜ pending |
| 38-03-* | 38-03 | 3 | LIVE-03 | T-38-12 | Stale-closure detection emits advisory + reverts | unit | `dotnet test --filter "Phase38.StaleClosureDetectionTests"` | ❌ W0 | ⬜ pending |
| 38-03-* | 38-03 | 3 | LIVE-03 | — | PrngRegistry.ResetAtRenderBoundary called at swap | unit | `dotnet test --filter "Phase38.PrngReseedAtSwapTests"` | ❌ W0 | ⬜ pending |
| 38-03-* | 38-03 | 3 | LIVE-01, LIVE-02 | T-38-12 | 30s CancellationToken cap → revert + advisory | unit | `dotnet test --filter "Phase38.TimeoutRevertTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-01 | T-38-13 (partial parse) | Tab completion uses in-process CompletionHandler.BuildItems() | unit | `dotnet test --filter "Phase38.ReplCompletionTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-02 | — | `:help transpose` prints signature + doc + example | unit | `dotnet test --filter "Phase38.ReplHelpMetaCommandTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-03 | — | Multi-line paren-balanced continuation works | unit | `dotnet test --filter "Phase38.ReplMultiLineTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-03 | — | Ctrl+R history search returns matches from `~/.config/flow/history` | unit | `dotnet test --filter "Phase38.ReplHistorySearchTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-04 | — | `(visualize seq)` renders articulation glyphs at note onsets | unit | `dotnet test --filter "Phase38.VisualizeArticulationGlyphTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-04 | — | `(inspect seq)` is a working alias of `(visualize seq)` | unit | `dotnet test --filter "Phase38.InspectAliasTests"` | ❌ W0 | ⬜ pending |
| 38-04-* | 38-04 | 2 | REPL-04 | — | Glyph collision rules resolved correctly (bar line wins) | unit | `dotnet test --filter "Phase38.GlyphCollisionTests"` | ❌ W0 | ⬜ pending |
| 38-05-* | 38-05 | 2 | AUDIO-IN-01 | T-38-24 (mic feedback) | -20 dB auto-attenuation applied on `(micBuffer)` open | unit | `dotnet test --filter "Phase38.MicBufferAttenuationTests"` | ❌ W0 | ⬜ pending |
| 38-05-* | 38-05 | 2 | AUDIO-IN-02 | — | Linear interp resample to 44.1kHz preserves duration ±1 sample | unit | `dotnet test --filter "Phase38.MicBufferResampleTests"` | ❌ W0 | ⬜ pending |
| 38-05-* | 38-05 | 2 | AUDIO-IN-02 | — | `(micBuffer)` composes with `(granular)` / `(mix)` / `(writeWav)` | smoke | `dotnet run --project flow-interpreter tests/test_audio_in_pipeline.flow` | ❌ W0 | ⬜ pending |
| 38-06-* | 38-06 | 3 | OSC-02 | — | Charitable type-tag inference: Int→,i Long→,h Float→,f Double→,d String→,s Bool→,T/,F | unit | `dotnet test --filter "Phase38.OscTypeTagInferenceTests"` | ❌ W0 | ⬜ pending |
| 38-06-* | 38-06 | 3 | OSC-01 | T-38-10 (OSC flood) | Rate-limit gate: 200Hz/path drop-newest sample-and-hold | unit | `dotnet test --filter "Phase38.OscRateLimitTests"` | ❌ W0 | ⬜ pending |
| 38-06-* | 38-06 | 3 | OSC-01, OSC-02 | — | UDP loopback round-trip (127.0.0.1:ephemeral) preserves payload | unit | `dotnet test --filter "Phase38.OscLoopbackTests"` | ❌ W0 | ⬜ pending |
| 38-06-* | 38-06 | 3 | OSC-01, OSC-02 | — | Bundle support both directions, timetag honored on receive | unit | `dotnet test --filter "Phase38.OscBundleTests"` | ❌ W0 | ⬜ pending |
| 38-06-* | 38-06 | 3 | OSC-01 | — | Bundle nesting depth >8 → clamp + stderr advisory | unit | `dotnet test --filter "Phase38.OscBundleDepthCapTests"` | ❌ W0 | ⬜ pending |
| 38-07-* | 38-07 | 4 | all | — | All 5 `examples/live/*.flow` chapters execute cleanly | smoke | `for f in examples/live/*.flow; do dotnet run --project flow-interpreter "$f" || exit 1; done` | ❌ W0 | ⬜ pending |
| 38-07-* | 38-07 | 4 | REPL-02, REPL-04, OSC-02 | — | REQUIREMENTS.md wording overrides applied per D-38-09/10/13 | manual | `grep -E ":help fn|charitable type-tag|inspect.*visualize alias" .planning/REQUIREMENTS.md` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Phase38/` directory — 23 new xUnit test files (see Per-Task Verification Map for filter names)
- [ ] `flow-lang.Tests/Phase38/TestFixtures/mic_fixture.wav` — synthetic capture-path fixture (1s sine 440Hz @ 48kHz, used by `MicBufferResampleTests` + `MicBufferAttenuationTests` without requiring real mic hardware)
- [ ] `flow-lang.Tests/Phase38/TestFixtures/historyfile.example` — `~/.config/flow/history`-format fixture for `ReplHistorySearchTests`
- [ ] `tests/test_audio_in_pipeline.flow` — `.flow` smoke for `(micBuffer) -> (granular) -> writeWav` composition
- [ ] `tests/test_live_*.flow` (5 chapter files per CONTEXT.md `examples/live/` list — also serve as regression tests per the Phase 36/39 chapter-as-test pattern):
  - `tests/test_live_hello.flow` — minimal `live 1bar { }` smoke
  - `tests/test_live_multi_block.flow` — multi-block independent swap
  - `tests/test_live_mic_granular.flow` — AUDIO-IN + DSP composition
  - `tests/test_live_osc_controller.flow` — OSC round-trip smoke
  - `tests/test_live_repl_session.md` — narrated REPL transcript (manual-only verification)
- [ ] NuGet packages installed: `Rug.Osc 1.2.5`, `PrettyPrompt 4.1.1` (or hand-rolled fallback per D-38-11 gate)
- [ ] `flow-lsp` ProjectReference added to `flow-interpreter/flow-interpreter.csproj` for in-process `CompletionHandler.BuildItems()` access

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| ANSI status panel cross-terminal visual smoke | LIVE-02 | Terminal emulators (xterm / Konsole / iTerm2 / Windows Terminal) render ANSI sequences slightly differently; visual smoke catches cursor-save/restore quirks no headless test can | `dotnet run --project flow-interpreter -- watch examples/live/hello_live.flow` in each available terminal; verify 4-row panel redraws cleanly without flicker or stale rows |
| Ctrl+R history search interactive feel | REPL-03 | PrettyPrompt key handling involves async input loop + terminal raw-mode; smoke confirms keybinding propagation works in real tty | `dotnet run --project flow-interpreter` (interactive REPL), type a few commands, press Ctrl+R, type substring, verify match appears |
| Real-microphone capture loopback | AUDIO-IN-01 | PulseAudio capture path can only fully verify with real PA daemon + real device; CI uses fixture WAV. Composer confirms a 5-second mic capture writes a valid WAV on developer machine | `dotnet run --project flow-interpreter -e '(micBuffer 5s) -> (writeWav "mic.wav")'` then open `mic.wav` in any player; verify audible content |
| OSC controller round-trip with a real surface | OSC-01, OSC-02 | TouchOSC / Lemur / hardware controllers exercise edge cases unit tests miss (multi-arg bundles at high rates, address pattern variations) | Open TouchOSC iOS app, point at developer machine on port 7777, run `(oscListen 7777 "/touch/1" (fn ... => (print "received"))` in REPL; touch the controller; verify console output |
| Live performance feel — composer hot-edits during playback | LIVE-01, LIVE-02, LIVE-03 | The whole point of the phase. No automated test captures "the swap was musically clean" or "the latency was acceptable" subjectively | `dotnet run --project flow-interpreter -- watch examples/live/multi_block.flow`; edit a `live` block body, save, listen for clean swap at next bar boundary; verify last-advisory row in panel reads correctly |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s (per-plan xUnit slice)
- [ ] `nyquist_compliant: true` set in frontmatter (planner flips after Wave 0 confirmed in plan-checker)

**Approval:** pending (planner sets to approved YYYY-MM-DD after wave-0 confirmation)
