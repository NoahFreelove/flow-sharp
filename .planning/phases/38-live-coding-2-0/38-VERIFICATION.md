---
phase: 38-live-coding-2-0
verified: 2026-05-24T00:00:00Z
status: passed
score: 11/11 must-haves verified
overrides_applied: 3  # D-38-09 REPL-02 wording / D-38-10 REPL-04 alias / D-38-13 OSC-02 charitable inference — per D-v1.5-01 single-commit migration latitude
re_verification: true  # initial verify flagged 1 gap (OscHandle TypeParser); orchestrator applied 3-line fix; re-verified passing
gap_fix:
  - issue: "OscHandle missing from TypeParser + Parser type-name gate"
    fixed_in: ["flow-lang/Parsing/TypeParser.cs", "flow-lang/Parsing/Parser.cs"]
    fix_summary: "Added `TokenType.Identifier when token.Text == \"OscHandle\" => OscHandleType.Instance` arm to TypeParser.ParseType (line 217) and TypeParser.TryParseTypeFromIdentifier (line 348); added `or \"OscHandle\"` to Parser type-name gate (line 1954). Pattern mirrors Tuning/Sfz/MarkovModel/LsystemModel precedent. Build clean; tests/test_live_osc_controller.flow prints `PASS: OSC loopback round-trip`; 83/83 Phase 38 xUnit tests remain GREEN; pre-existing 34 unrelated test failures unchanged (no new regressions)."
gaps:
  - truth: "Composer writes `use \"@osc\"` and the module loads cleanly; `OscHandle h = (oscListen ...)` parses as a typed variable declaration"
    status: closed
    reason: "`OscHandle` is not registered in `flow-lang/Parsing/TypeParser.cs`. Every occurrence of `OscHandle` as a type annotation in Flow source — in `osc.flow` lines 36 and 43, in `tests/test_live_osc_controller.flow` line 23, and in `examples/live/osc_controller.flow` line 54 — produces a parse error: `Expected type name but got Identifier 'OscHandle'`. As a result `use \"@osc\"` fails with `Module contains structural syntax errors and cannot be imported`, and the OSC-01/02 composer surface is unreachable from Flow source even though the underlying C# OscFunctions implementation is correct. The 83 Phase38 xUnit tests pass because they call C# directly, bypassing the Flow language type parser. The FlowScriptTests regression suite detects the failure: `test_live_osc_controller.flow` is the sole new test failure introduced by Phase 38 (total failures rose from 34 pre-existing to 35)."
    artifacts:
      - path: "flow-lang/Parsing/TypeParser.cs"
        issue: "Missing `TokenType.Identifier when token.Text == \"OscHandle\" => OscHandleType.Instance` arm in both switch expressions (lines ~216 and ~344), matching the precedent for Tuning (line 206), Sfz (line 210), MarkovModel (line 213), LsystemModel (line 216)"
      - path: "flow-lang/osc.flow"
        issue: "Lines 36 and 43 use `OscHandle:` type annotation in `internal proc` declarations — parse error blocks `use \"@osc\"` entirely"
      - path: "tests/test_live_osc_controller.flow"
        issue: "Line 23: `OscHandle h = (oscListen ...)` — parse error; test fails in FlowScriptTests regression suite"
      - path: "examples/live/osc_controller.flow"
        issue: "Line 54: `OscHandle h = (oscListen ...)` — parse error; tutorial chapter fails to run"
    missing:
      - "Add `TokenType.Identifier when token.Text == \"OscHandle\" => OscHandleType.Instance` to `ParseTypeName` switch in TypeParser.cs (two switch expressions, lines ~200-219 and ~340-344), matching the Tuning/Sfz/MarkovModel/LsystemModel pattern"
      - "Add the using import for `OscHandleType` if not already present (`using FlowLang.TypeSystem.SpecialTypes;`)"
      - "Verify `dotnet run --project flow-interpreter -- -e 'use \"@osc\"; (print \"osc loaded\")'` exits 0 after the fix"
      - "Verify `FlowScriptTests.RunsToCompletion(test_live_osc_controller.flow)` goes green after the fix"
human_verification:
  - test: "ANSI live status panel cross-terminal visual smoke"
    expected: "4-row panel renders in-place at top of terminal without flicker; row 2 omitted when zero live blocks; plain-line fallback when stdout is piped (NO_COLOR=1 / --no-color)"
    why_human: "Terminal emulator ANSI quirks (cursor-save/restore) cannot be verified headlessly; subjective 'no flicker' judgement"
  - test: "Ctrl+R history search interactive feel + :help fn REPL meta-command"
    expected: "PrettyPrompt 4.1.1 Ctrl+R reverse search surfaces history entries; ~/.config/flow/history persists across sessions with 0600 permissions; :help transpose renders bold+green header / dim signature / body / dim Example"
    why_human: "PrettyPrompt async raw-mode input loop and terminal key propagation cannot be verified headlessly"
  - test: "Real-microphone capture loopback"
    expected: "(micBuffer 5s) -> (writeWav /tmp/mic.wav) produces ~441 KB WAV with audible content; one-shot [audio-in] attenuation advisory fires; optional resample advisory fires on non-44.1 kHz devices"
    why_human: "PulseAudio PA_STREAM_RECORD requires a real PA daemon + real input device; CI uses fixture WAV via CaptureOverride test seam"
  - test: "OSC controller round-trip with a real surface (after TypeParser fix)"
    expected: "use @osc loads cleanly; OscHandle h = (oscListen ...) parses; sustained slider drags from TouchOSC/hardware controller arrive at <=200 Hz per D-38-14 sample-and-hold"
    why_human: "Real hardware OSC controller needed; edge cases (multi-arg bundles at high rates, address pattern variations) cannot be fully exercised in loopback"
  - test: "Live performance hot-edit during playback"
    expected: "Audio swap at next bar boundary with no click (64-sample crossfade); file-scope edit emits yellow advisory; stale-closure emits red advisory and keeps previous buffer; 30s timeout emits red advisory"
    why_human: "Subjective audio quality ('musically clean swap') requires a live performance context; wall-clock timing and audio dropout are not verifiable headlessly"
---

# Phase 38: Live Coding 2.0 Verification Report

**Phase Goal:** Modernized live-coding surface — composer wraps a section in `live 1bar { ... }`, edits the file mid-playback, and the new content hot-swaps at the next bar boundary without re-initializing voices or destroying playback state. REPL gets LSP-backed completion + `:help fn` inline help + multiline editing + history search + ASCII piano-roll preview. Audio input from mic/line-in composes with DSP pipeline. OSC server/client opens Flow to the network.

**Verified:** 2026-05-24
**Status:** GAPS FOUND — 1 blocker: `OscHandle` missing from TypeParser makes `use "@osc"` fail with parse error
**Re-verification:** No — initial verification (auditing the Plan 38-07 closer draft)

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `live <quantize> { ... }` block parses (including 1bar / q / omitted-default forms), produces LiveBlockStatement AST, and emits D-v1.5-07 stderr advisory once per (line, process) | VERIFIED | `flow-lang/Ast/Statements/LiveBlockStatement.cs` 107 LOC + `flow-lang/Lexing/TokenType.cs` Live token + `flow-lang/Parsing/Parser.cs` ParseLiveBlockStatement (~95 LOC) + `flow-lang/Interpreter/Interpreter.cs` ExecuteLiveBlock + `flow-lang/Runtime/LiveBlockRegistry.cs` 99 LOC ConcurrentDictionary. Spot-check: `test_live_hello.flow` and `test_live_multi_block.flow` run clean printing PASS and the D-v1.5-07 advisory. |
| 2 | `flow watch` debounces at 200ms; rapid back-to-back saves coalesce; 30s CancellationToken wrap with Task.Run + Wait(TimeSpan) in place; 4-row ANSI live status panel with TTY-fallback plain-line mode | VERIFIED | `flow-interpreter/LiveStatusPanel.cs` 429 LOC; `flow-interpreter/LiveReloadManager.cs` `DebounceMs = 200` at line 74; `RenderTimeout = TimeSpan.FromSeconds(30)` at line 82; `_panel.PublishAdvisory` at 9 call sites. All 9 WatchDebounceTests + AnsiPanelRenderTests + PanelTtyFallbackTests GREEN. |
| 3 | Voice-pool state preserved (Voice.Name key) when name survives reload; PRNG reseeded at swap boundary; stale-closure detection via LambdaCaptureAuditor | VERIFIED | `flow-lang/StandardLibrary/Audio/Voice.cs` `Name { get; init; }` + `CopyStateFrom(prev)`. `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` `DiffByVoiceName`. `flow-lang/Interpreter/LambdaCaptureAuditor.cs`. `LiveReloadManager.StagePendingBuffers` calls `PrngRegistry.ResetAtRenderBoundary()` at line 651. All 12 VoicePoolNameDiff + StaleClosureDetection + PrngReseedAtSwap + TimeoutRevert tests GREEN. |
| 4 | LSP-backed Tab completion in REPL via static `CompletionHandler.BuildItems()` in-process | VERIFIED | `flow-interpreter/ReplLineEditor.cs` 341 LOC calls `FlowLsp.Handlers.CompletionHandler.BuildItems()` at line 230. `flow-interpreter/flow-interpreter.csproj` has `flow-lsp` ProjectReference + PrettyPrompt 4.1.1. ReplCompletionTests GREEN. |
| 5 | `:help fn` meta-command in REPL — bold+green header / dim signature / body / dim Example; unknown identifier → yellow advisory | VERIFIED | `flow-interpreter/Repl.cs` HandleCommand `:help <name>` arm lines 205-212 + ShowHelpForName lines 235-291. Reads from BuiltInDocs.TryGet per Phase 31. ReplHelpMetaCommandTests GREEN. |
| 6 | Multi-line editing + Ctrl+R history search + `~/.config/flow/history` 0600 mode | VERIFIED | `flow-interpreter/ReplLineEditor.cs` PrettyPrompt 4.1.1 wrapper; `persistentHistoryFilepath` ctor param; `DefaultHistoryFilePath()` at line 64; 10k rotation + ApplyUnixPermissions. ReplMultiLineTests + ReplHistorySearchTests GREEN. |
| 7 | `(inspect seq)` / `(visualize seq)` alias pair — ASCII piano-roll with articulation glyphs and bar tick marks | VERIFIED | `flow-lang/StandardLibrary/VisualizationFunctions.cs` sig3 `inspect` alias at line 36; articulation glyph switch at line 149; Legato gap-fill pass at line 179. VisualizeArticulationGlyphTests + InspectAliasTests + GlyphCollisionTests GREEN. |
| 8 | `(micBuffer duration)` reads from PulseAudio PA_STREAM_RECORD; -20 dB attenuation on open; linear resample to 44.1 kHz; composes with DSP pipeline | VERIFIED | `flow-lang/Audio/PulseAudioCaptureBackend.cs` 272 LOC, `PA_STREAM_RECORD = 2` at line 240, `pa_simple_read` at line 268. `flow-lang/StandardLibrary/Audio/InputFunctions.cs` 244 LOC, -20 dB scalar at line 66, ResampleLinear at line 218. Spot-check: `test_live_mic_granular.flow` prints PASS. 13 PulseAudioCaptureBackendTests + MicBufferAttenuationTests + MicBufferResampleTests GREEN. |
| 9 | `use "@osc"` loads the OSC module; `(oscSend ...)` and `(oscListen ...)` builtins activate; OscHandle reference-identity type works in Flow source | FAILED | `OscHandle` is absent from `flow-lang/Parsing/TypeParser.cs`. `use "@osc"` fails: `Module '/…/osc.flow' contains structural syntax errors and cannot be imported` — because `osc.flow` lines 36 and 43 use `OscHandle:` type annotations in `internal proc` declarations. `OscHandle h = (oscListen ...)` in `test_live_osc_controller.flow:23` and `examples/live/osc_controller.flow:54` produce: `Unexpected token Assign '='`. The 19 OscTypeTagInference/RateLimit/Loopback/Bundle/BundleDepthCap xUnit tests pass because they call C# OscFunctions directly, not through Flow source. FlowScriptTests regression adds 1 new failure: `test_live_osc_controller.flow`. |
| 10 | OSC-01 rate-limit ≤200 Hz per path, bundle support depth-cap 8, OscHandle lifecycle (oscStop cancels listener) | VERIFIED (C# layer only — see gap #9) | `flow-lang/StandardLibrary/Network/OscFunctions.cs` 598 LOC; `RateLimitWindowMs = 5` at line 70; `_lastFireTimeMs` ConcurrentDictionary at line 72; OscBundleDepthCapTests GREEN; oscStop CancellationTokenSource. All 19 OSC xUnit tests GREEN. The C# implementation is correct; only the Flow-source type-annotation path is broken. |
| 11 | OSC-02 charitable smallest-tag-that-fits type-tag inference: Int→,i / Long→,h / Float→,f / Double→,d / String→,s / Bool→,T,F / Buffer→,b | VERIFIED (C# layer only — see gap #9) | `flow-lang/StandardLibrary/Network/OscFunctions.cs` `InferOscArgs` at line 259; all type-tag mappings documented at lines 243-248. 6 OscTypeTagInferenceTests GREEN. Callable from C# but unreachable from Flow source until gap #9 is fixed. |

**Score:** 10/11 truths verified (OSC-01/02 truth #9 blocked by TypeParser gap)

Note: Truths #10 and #11 count the C# layer as VERIFIED since the underlying implementation is correct and well-tested; the gap is exclusively in the type-parser wiring that connects Flow-source type annotations to OscHandleType. All 83 Phase38 xUnit tests remain GREEN.

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lang/Ast/Statements/LiveBlockStatement.cs` | LiveBlockStatement record + ComputeBlockId FNV-1a | VERIFIED | 107 LOC; FNV-1a at lines 72-106 |
| `flow-lang/Runtime/LiveBlockRegistry.cs` | ConcurrentDictionary + Register/Snapshot/Clear + LiveBlockRegistration record | VERIFIED | 99 LOC; all APIs present |
| `flow-interpreter/LiveStatusPanel.cs` | ANSI 4-row panel + TTY fallback + PublishAdvisory API | VERIFIED | 429 LOC; IDisposable heartbeat Timer; forceTtyMode test seam |
| `flow-interpreter/LiveReloadManager.cs` | 200ms debounce + LiveStatusPanel + 30s timeout + per-block Dict | VERIFIED | 903 LOC; DebounceMs=200 at line 74; RenderTimeout=30s at line 82; _panel at line 116 |
| `flow-interpreter/ReplLineEditor.cs` | PrettyPrompt 4.1.1 wrapper + CompletionHandler.BuildItems() + history at ~/.config/flow/history | VERIFIED | 341 LOC; BuildItems call at line 230; DefaultHistoryFilePath at line 64 |
| `flow-interpreter/Repl.cs` (extended) | :help fn meta-command + ShowHelpForName | VERIFIED | HandleCommand lines 205-212; ShowHelpForName lines 235-291 |
| `flow-lang/StandardLibrary/VisualizationFunctions.cs` (extended) | (inspect seq) alias + articulation glyphs + bar tick marks | VERIFIED | inspect alias at line 36; glyph switch at line 149 |
| `flow-lang/Audio/PulseAudioCaptureBackend.cs` | PA_STREAM_RECORD=2 + pa_simple_read P/Invoke | VERIFIED | 272 LOC; constants at lines 235-240; pa_simple_read at line 268 |
| `flow-lang/StandardLibrary/Audio/InputFunctions.cs` | (micBuffer Second/Double) + -20dB attenuation + ResampleLinear | VERIFIED | 244 LOC; -20dB at line 66; ResampleLinear at line 218 |
| `flow-lang/StandardLibrary/Network/OscFunctions.cs` | 5 builtins + InferOscArgs + rate-limit gate + bundle support | VERIFIED (C# layer) | 598 LOC; all 5 builtins registered; InferOscArgs at line 259 |
| `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` | Reference-identity FlowType for OscHandle | VERIFIED | Sealed singleton, specificity 151 |
| `flow-lang/osc.flow` | @osc stdlib module that activates OSC surface | STUB/BROKEN | File exists (56 LOC) but fails to parse because `OscHandle` is not in TypeParser — lines 36 and 43 produce parse errors |
| `flow-lang.Tests/Integration/Phase38/` (25 test files) | All Phase38 xUnit tests GREEN | VERIFIED | 83 facts GREEN per `dotnet test --filter FullyQualifiedName~Phase38` |
| `examples/live/` (5 chapters) | 4 .flow + 1 .md tutorial chapters | PARTIALLY VERIFIED | hello_live.flow, multi_block.flow, mic_granular.flow run clean; repl_session.md ships; osc_controller.flow fails at runtime (OscHandle type annotation parse error, line 54) |
| `tests/test_live_*.flow` (4 files) | Paired regression tests with PASS sentinels | PARTIALLY VERIFIED | test_live_hello.flow PASS, test_live_multi_block.flow PASS, test_live_mic_granular.flow PASS; test_live_osc_controller.flow FAILS (OscHandle parse error at line 23; FlowScriptTests regression confirms) |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `flow-lang/Lexing/SimpleLexer.cs` | `flow-lang/Ast/Statements/LiveBlockStatement.cs` | `"live" => TokenType.Live` at line 897 | VERIFIED | Keyword recognized |
| `flow-lang/Parsing/Parser.cs` | `LiveBlockStatement` | `ParseLiveBlockStatement()` at line 959; dispatch at line 179 | VERIFIED | grep returns 2 hits |
| `flow-lang/Interpreter/Interpreter.cs` | `LiveBlockRegistry.Register` | `ExecuteLiveBlock` at line 463; dispatch at line 124 | VERIFIED | Registry wired |
| `flow-lang/Runtime/ExecutionContext.cs` | `LiveBlockRegistry` | `public LiveBlockRegistry LiveBlockRegistry { get; }` at line 156 | VERIFIED | Property present |
| `flow-interpreter/LiveReloadManager.cs` | `LiveStatusPanel` | `_panel = new LiveStatusPanel(...)` at line 174; 9 PublishAdvisory call sites | VERIFIED | Wired |
| `flow-interpreter/LiveReloadManager.cs` | `PrngRegistry.ResetAtRenderBoundary()` | `StagePendingBuffers` at line 595; reset call at line 651 | VERIFIED | PRNG reseed at swap boundary |
| `flow-interpreter/ReplLineEditor.cs` | `CompletionHandler.BuildItems()` | `FlowLsp.Handlers.CompletionHandler.BuildItems()` at line 230 | VERIFIED | In-process LSP call |
| `flow-interpreter/flow-interpreter.csproj` | `flow-lsp` (ProjectReference) + PrettyPrompt 4.1.1 | Lines 7 and 11 of csproj | VERIFIED | Both present |
| `flow-lang/Parsing/TypeParser.cs` | `OscHandleType` | MISSING — no `TokenType.Identifier when token.Text == "OscHandle"` arm | FAILED (BLOCKER) | All other reference-identity types (Tuning/Sfz/MarkovModel/LsystemModel) have this arm; OscHandle does not |
| `flow-lang/osc.flow` | `OscFunctions` activation | `(__enableOscModule)` trailing call at line 56 | BLOCKED | Module fails to parse before reaching __enableOscModule due to OscHandle type annotation errors at lines 36 and 43 |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|-------------------|--------|
| `LiveStatusPanel.cs` | `tempo`, `timesig`, `bar`, `blocks` | `PublishState(...)` called from LiveReloadManager at swap + heartbeat | Yes — reads from FlowEngine's MusicalContext | FLOWING |
| `InputFunctions.cs` | `rawSamples` | `PulseAudioCaptureBackend.Capture(duration)` → real PA read | Yes — real PA_STREAM_RECORD capture (fixture override in CI) | FLOWING |
| `OscFunctions.cs` InferOscArgs | OSC wire bytes | `Value.Type` switch over actual composer-passed args | Yes — real type dispatch | FLOWING (C# only) |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| `live 1bar { ... }` block executes + emits D-v1.5-07 advisory | `dotnet run --project flow-interpreter tests/test_live_hello.flow` | `PASS: live hello smoke ran` + advisory on stderr | PASS |
| Multi-block independent registration | `dotnet run --project flow-interpreter tests/test_live_multi_block.flow` | `PASS: multi-block live registration` + 2 advisory lines | PASS |
| micBuffer composability | `dotnet run --project flow-interpreter tests/test_live_mic_granular.flow` | `PASS: mic + granular pipeline composes` | PASS |
| `use "@osc"` module loads | `dotnet run --project flow-interpreter -e 'use "@osc"'` | `Module contains structural syntax errors` — FAILS | FAIL |
| `OscHandle h = (oscListen ...)` parses | `dotnet run --project flow-interpreter tests/test_live_osc_controller.flow` | `Unexpected token Assign '='` — FAILS | FAIL |
| All Phase38 xUnit tests | `dotnet test flow-lang.Tests --filter FullyQualifiedName~Phase38` | `Passed: 83, Failed: 0` | PASS |
| Total test suite regression | `dotnet test flow-lang.Tests` | `Failed: 35, Passed: 1720` — 34 pre-existing + 1 NEW (test_live_osc_controller.flow) | FAIL (1 new) |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| LIVE-01 | 38-02 | `live <quantize> { ... }` block — auto-loops; hot-swaps; D-v1.5-07 advisory | VERIFIED | LiveBlockStatement.cs + Parser + Interpreter ExecuteLiveBlock; tests pass |
| LIVE-02 | 38-01, 38-03 | Modernized watch mode — ANSI panel + 200ms debounce + 30s cap | VERIFIED | LiveStatusPanel.cs 429 LOC; DebounceMs=200; RenderTimeout=30s; 9 xUnit tests |
| LIVE-03 | 38-03 | Voice-pool state preserved; PRNG reseeded; stale-closure detection | VERIFIED | Voice.Name + DiffByVoiceName + LambdaCaptureAuditor + PrngRegistry.ResetAtRenderBoundary; 12 xUnit tests |
| REPL-01 | 38-04 | LSP-backed Tab completion via in-process CompletionHandler.BuildItems() | VERIFIED | ReplLineEditor.cs + flow-lsp ProjectReference + PrettyPrompt 4.1.1 |
| REPL-02 | 38-04 | `:help fn` meta-command (D-38-09 override of `?fn`) | VERIFIED | Repl.cs ShowHelpForName + BuiltInDocs.TryGet; REQUIREMENTS.md updated |
| REPL-03 | 38-04 | Multi-line editing + Ctrl+R history + ~/.config/flow/history 0600 | VERIFIED | ReplLineEditor.cs persistentHistoryFilepath + ApplyUnixPermissions |
| REPL-04 | 38-04 | `(inspect seq)` / `(visualize seq)` alias pair + articulation glyphs (D-38-10 override) | VERIFIED | VisualizationFunctions.cs inspect alias + glyph switch; REQUIREMENTS.md updated |
| AUDIO-IN-01 | 38-05 | `(micBuffer duration)` via PA_STREAM_RECORD; -20 dB attenuation | VERIFIED | PulseAudioCaptureBackend.cs + InputFunctions.cs; 13 xUnit tests |
| AUDIO-IN-02 | 38-05 | Captured Buffer composes with DSP; 44.1 kHz linear resample | VERIFIED | ResampleLinear in InputFunctions.cs; test_audio_in_pipeline.flow PASS |
| OSC-01 | 38-06 | `(oscListen ...)` rate-limited 200 Hz/path; OscHandle lifecycle | BLOCKED | C# implementation correct (19 xUnit tests pass); unreachable from Flow source — osc.flow fails to parse because OscHandle not in TypeParser |
| OSC-02 | 38-06 | `(oscSend ...)` charitable smallest-tag-that-fits inference (D-38-13 override) | BLOCKED | C# InferOscArgs correct; unreachable from Flow source for same reason; REQUIREMENTS.md updated |

---

### Three REQUIREMENTS.md Wording Overrides (Verified Applied)

| REQ | Override | Verification |
|-----|---------|-------------|
| REPL-02 | `?fn` → `:help fn` per D-38-09 | `grep ":help fn" .planning/REQUIREMENTS.md` returns match at line 98 |
| REPL-04 | solo `(inspect seq)` → `(inspect seq)` / `(visualize seq)` alias pair per D-38-10 | `grep "inspect seq.*visualize seq.*alias" .planning/REQUIREMENTS.md` returns match at line 100 |
| OSC-02 | strict-tag-by-arg → charitable smallest-tag-that-fits per D-38-13 | `grep "charitable smallest-tag" .planning/REQUIREMENTS.md` returns match at line 110 |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `flow-lang/Parsing/TypeParser.cs` | ~200-219, ~340-344 | Missing `OscHandle` arm in both type-name switch expressions — structural omission, not a debt marker | BLOCKER | Causes `use "@osc"` parse failure; `OscHandle` type annotation unusable in Flow source |
| `flow-lang/osc.flow` | 36, 43 | `OscHandle:` type annotation in `internal proc` declarations — parse error produced because TypeParser lacks OscHandle | BLOCKER (consequence of above) | Stdlib module fails to import entirely |

No TBD / FIXME / XXX / unreferenced debt markers found in Phase38-modified files.

---

### Gaps Summary

**Root cause:** `OscHandle` was implemented as a full `FlowType` subclass (`flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs`) following the Phase 32/33/36 pattern for reference-identity types. Every previous reference-identity type (Tuning, Sfz, MarkovModel, LsystemModel) that requires a type-annotation in Flow source also has a corresponding arm in `TypeParser.cs` — OscHandle is the only one that does not. The consequence cascades through:

1. `osc.flow` fails to parse (2 OscHandle annotations at lines 36 and 43)
2. `use "@osc"` fails with "Module contains structural syntax errors"
3. `OscHandle h = (oscListen ...)` fails with "Unexpected token Assign"
4. `test_live_osc_controller.flow` produces a new regression failure (total: 35 vs 34 pre-existing)
5. `examples/live/osc_controller.flow` is a broken tutorial chapter

**Fix scope:** 3-line change in `flow-lang/Parsing/TypeParser.cs` — add one arm to each of the two switch expressions, matching the Tuning/Sfz/MarkovModel/LsystemModel pattern. No other files require changes.

**All other Phase 38 surfaces ship correctly.** The 83 Phase38 xUnit tests pass (they test the C# layer directly). LIVE-01/02/03, REPL-01/02/03/04, AUDIO-IN-01/02, and the C# implementations of OSC-01/02 are all verified. The gap is exclusively the type-parser wiring for the OscHandle type annotation in Flow source code.

---

### Human Verification Required

#### 1. ANSI Live Status Panel Cross-Terminal Smoke

**Test:** Run `dotnet run --project flow-interpreter -- --watch examples/live/hello_live.flow` in each available terminal emulator (xterm / Konsole / iTerm2 / gnome-terminal / Alacritty). Also pipe through `cat` to test plain-line fallback.
**Expected:** 4-row panel renders in-place without cursor flicker; row 2 present (1 live block); plain-line `[watch] tempo=N timesig=N/N bar=N voices=N/M` under pipe mode.
**Why human:** Terminal emulator ANSI cursor-save/restore quirks cannot be verified headlessly; subjective "no flicker" judgement required.

#### 2. Ctrl+R History Search + :help fn Interactive Feel

**Test:** Run the REPL, enter several commands, then Ctrl+R to search history; exit and re-enter to verify persistence. Also run `:help transpose`.
**Expected:** Ctrl+R reverse search surfaces entries; history persists across sessions; `~/.config/flow/history` has 0600 permissions; `:help transpose` renders bold+green header / dim signature / body / dim Example.
**Why human:** PrettyPrompt async raw-mode input loop requires a real tty; permission check requires running on Linux/macOS.

#### 3. Real-Microphone Capture Loopback

**Test:** `dotnet run --project flow-interpreter -- -e '(micBuffer 5s) -> (writeWav "/tmp/mic.wav")'` then play back via aplay.
**Expected:** `/tmp/mic.wav` exists (~441 KB); audible content matches what was captured; one-shot `[audio-in] mic stream attenuated -20 dB on open` advisory fires.
**Why human:** PulseAudio PA_STREAM_RECORD requires real PA daemon + real input device; CI uses fixture WAV.

#### 4. OSC Controller Round-Trip with Real Surface (after TypeParser fix)

**Test:** After the TypeParser gap is closed, run `dotnet run --project flow-interpreter tests/test_live_osc_controller.flow` to confirm it prints `fader=0.5` and `PASS: OSC loopback round-trip`. Then exercise with a real TouchOSC/hardware controller.
**Expected:** `use "@osc"` loads cleanly; sustained slider drags arrive at ≤200 Hz; OSC stop tears down cleanly.
**Why human:** Real hardware OSC controller needed for rate-limit and bundle edge cases; test_live_osc_controller.flow currently blocked by gap #9.

#### 5. Live Performance Hot-Edit During Playback

**Test:** Run `dotnet run --project flow-interpreter -- --watch examples/live/multi_block.flow`; edit live block body mid-playback and save; test file-scope edit advisory; test stale-closure advisory; test 30s timeout.
**Expected:** Audio swap at next bar boundary (no click); file-scope edit → yellow advisory, no auto-restart; stale closure → red advisory, previous buffer kept; timeout → red advisory, previous buffer kept.
**Why human:** Subjective "musically clean swap" judgement; wall-clock timing (30s timeout) requires real time; audio dropout not verifiable headlessly.

---

## Pre-existing Test Failures (Out of Scope)

34 failures carry across from before Phase 38 (confirmed by SUMMARY cross-references):
- Phase28 PerSynthArticulationTests — 26 FFT cosine differentiable failures
- Phase28 RagtimeFixtureTests — 2 RMS regression baseline failures
- Phase29 ArticulationOnSampleTests — 6 audible-content-ratio bounds failures
- Phase35 MatchExhaustivenessDefaultTests — 2 exhaustiveness warning failures

**1 NEW failure introduced by Phase 38:** `FlowScriptTests.RunsToCompletion(test_live_osc_controller.flow)` — direct consequence of the OscHandle TypeParser gap documented above.

---

_Verified: 2026-05-24_
_Verifier: Claude (gsd-verifier) — auditing Plan 38-07 closer draft_
