# Audit Remediation Plan — subagent execution

**Source:** `.planning/CODEBASE-AUDIT-2026-06-09.md` (42 confirmed bugs, 10 web findings, 9 docs findings, 9 quality findings).
**Status:** PLANNED — not yet executed. Decision gate (§1) needs sign-off first.

## 0. Ground rules (apply to every agent)

- **Commit discipline:** one atomic commit per finding, message `fix(audit-0609): <§ref> <short title>`. No `Co-Authored-By` trailers. **Never push.**
- **Determinism:** every fix must preserve two-run cmp-clean. Fixes that legitimately change rendered bytes (see Baseline Ledger, §7) regenerate RMS baselines in the same commit with the reason in the message.
- **Tests first:** each agent writes/extends a regression test that fails before its fix and passes after (the audit's FIX lines name the pinning test where relevant). Targeted `dotnet test --filter` during work; full suite only at integration (Wave 4).
- **Isolation:** C# agents run in **git worktrees** (`isolation: 'worktree'`) because concurrent `dotnet build` in one checkout races on `obj/`. flow-site agents also worktree'd. I merge branches sequentially at Wave 4 — file ownership below is designed for zero-conflict merges.
- **Model policy (per Noah):** Sonnet by default. Opus only for the four packets flagged ⚠️ (semantic/AST/threading/level-math blast radius). Surgical one-liners done inline by the main loop (Fable), no agent overhead.
- **Scope guard:** agents fix ONLY their assigned findings. Anything adjacent they notice goes in their structured report, not the diff.
- **Each agent receives:** its audit section verbatim (incl. verifier corrections), file-ownership list, the ground rules, and the relevant CLAUDE.md excerpts (determinism conventions, charitable-interpretation philosophy).

## 1. Decision gate (need Noah's call before execution)

| # | Decision | Recommendation |
|---|----------|----------------|
| D1 | **Phase 38 REPL (§5.1):** wire `ReplLineEditor` into `Repl.Run`, or delete it + PrettyPrompt ref and re-document? | **Wire it.** The class is built and unit-tested; wiring + the line-0 Position fix is a contained medium task. |
| D2 | **LIVE-03 pipeline (§5.2):** full per-block wiring (large — the AST-visitor per-block render was never built), or minimal-honest subset now? | **Minimal now:** wire watch diagnostics (§5.8), PRNG-reseed-per-swap, and stale-closure gate into the whole-script path; amend CLAUDE.md/Phase 38 docs to say "whole-script 1-bar swap" honestly. Full per-block swaps → proper v1.6 phase. |
| D3 | **Debounce (§5 disputed, D-38-05 LOCK):** keep leading-edge + add missing synchronization, or flip to trailing-edge? | **Flip to trailing-edge** (restartable timer). The LOCK predates evidence that atomic-rename editors lose the final save; you're pre-traction. If you'd rather honor the LOCK: sync-only. |
| D4 | **Reverb tail (§3.8):** extending output past input length is a behavior change (longer buffers, RMS baselines shift). Ship it? | **Yes** — it's what the RT60 overload promises; regen baselines. |
| D5 | **Dynamic scoping (§2.5):** make user-proc call frames lookup boundaries (semantic change; could break scripts relying on caller-variable reads)? | **Fix write-through at minimum** (assignment must not cross a call boundary); decide read-through after the full `.flow` suite + examples run clean under the stricter mode. |
| D6 | **WASM republish:** integration wave needs `dotnet workload install wasm-tools` on this Mac + a multi-MB `static/wasm/` commit. OK? | Yes — required for §5.4/5.12/6.3 to actually reach the playground. Add `.gitattributes` in the same commit. |
| D7 | **GSD:** run this as direct subagent work (bypassing `/gsd:execute-phase`) per your direction, optionally logging it in STATE.md as a quick-task batch? | Bypass with a one-line STATE.md note at the end. |

## 2. Wave 1 — surgical inline fixes (main loop, no agents, ~30 min)

Tiny, unambiguous, single-file edits. One commit each, targeted test after each.

| Fix | § | Edit |
|-----|---|------|
| Compressor/Sidechain init | 3.1 | `-96f` → `0f` in `Compressor.cs:43` + `SidechainCompressor.cs:48` + regression test (RMS of first 100 ms ≈ steady-state) |
| Interpolated strings | 2.7 | `val.Data?.ToString()` → `val.ToString()` at `ExpressionEvaluator.cs:1080` (keep raw-string case) |
| euclidean DoS cap | 4.6 | add 1024-step guard to 3-arg registration (`BuiltInFunctions.cs:1638`), match the 4/6-arg style |
| WebAudio debug line | 3.9 | delete `WebAudioBackend.cs:177` stderr write (bundle refresh happens Wave 4) |
| Pidgin removal | 8.4 | drop PackageReference; add `Pidgin` to `ForbiddenTypeRefPrefixes`; fix CLAUDE.md mention |
| LSP module list | 5.16 | extend `StdlibSymbolIndex.ModuleNames` (+ derive-from-glob if trivial); fix VSIX workflow copy list |
| Audio-thread allocs | 8.6 | hoist scratch `float[]` in `CoreAudioBackend.cs:232` + `PulseAudioSimpleBackend.cs:180` |
| VU meter keys | 6.4 | `{#each vu as h (h)}` → index key in `+page.svelte:272` |
| _headers rule | 6.8 | add bare `/playground` COOP/COEP block |
| CLAUDE.md D-48-07 wording | 3.9 | "constant-power" → "identical samples" (one word) |

## 3. Wave 2 — parallel fix packets (worktrees; launch together)

File ownership is exclusive per agent. Estimated 10 agents.

| Agent | Model | Owns (exclusive) | Findings | Notes |
|-------|-------|------------------|----------|-------|
| **A. Transforms data-loss** ⚠️ | Opus | `Transforms/TransformFunctions.cs`, `MusicalNoteData`/`NoteType.cs` (`.With` extension only), new tests | 4.1 ParallelVoices, 4.2 12-of-17-arg ctor, 4.5 trill/tremolo, gap 10.3 cent-transpose | Highest composer value, delicate: extend `With(...)` w/ pitch slots, route ALL rebuild sites through it; recurse ParallelVoices via the HumanizeBar/CloneBar pattern. Pin: transpose voice-block ⇒ non-silent RMS; `[C4 E4 G4]q` stays 1 beat after transpose. |
| **B. DSP level math** ⚠️ | Opus | `DSP/PhaseVocoder.cs`, `DSP/Psola.cs`, `DSP/StretchEngine.cs`, Phase37 baselines | 3.2 COLA normalization, 3.4 PSOLA epoch grid | Verify: stretch(1.0±ε) level-continuous (<0.5 dB); factor-2 output level unchanged vs today (it was coincidentally correct — keep it correct); PSOLA factor-2 output has no pitch-rate AM. Regen Phase37 RMS baselines, two-run cmp-clean before/after. |
| **C. DSP hygiene** | Sonnet | `DSP/Reverb.cs`, `DSP/Filter.cs`, `Audio/FileIO.cs`, `SongRenderer.cs:350` region | 3.5 WAV sizes/padding, 3.6 denormal flush, 3.7 Q clamp+advisory, 3.8 tail extension (per D4) | 3.8 changes buffer lengths → baseline regen + per-voice path extension. Q clamp = charitable WarnOnce, ceiling 100. |
| **D. Core interpreter** ⚠️ | Opus | `Parsing/Parser.cs`, `Ast/Expressions/LiteralExpression.cs`, `Interpreter/*.cs`, `Runtime/ExecutionContext.cs`, `Runtime/ModuleLoader.cs`, `TypeSystem/FunctionSignature.cs` | 2.1 literal discriminator, 2.2 overload-cache invalidation, 2.3 `_returnValue` leaks (all 6 sites incl. REPL reset), 2.4 `~>` resolution, 2.6 import error delta, 2.8 PushFrame, 2.9 varargs | One agent because files overlap. 2.1 = AST record change (add `LiteralKind`), ripples through Parser emit sites — run the FULL `tests/*.flow` suite. 2.5 deliberately excluded (Wave 3, D5-gated). |
| **E. OSC/MIDI runtime** ⚠️(5.3 only) | Opus | `Network/OscFunctions.cs`, `Audio/MidiClock.cs`, `Midi/MidiFunctions.cs` | 5.3 handler thread-safety, 5.5 poll-loop returns, 5.6 midiOut handle leak, 5.10 timetag cancellation, 5.13 blob header | 5.3 design: queue handler invocations, drained at interpreter-safe points (or per-listener cloned context) — agent must propose, then implement; CC123 all-notes-off on midiOut close. |
| **F. Watch mode** | Sonnet | `flow-interpreter/LiveReloadManager.cs`, `LiveStatusPanel.cs` | 5.7 rate/channel deferral, 5.8 diagnostics out-param, 5.14 panel repaint/row math/stderr, debounce per D3 | Plus the D2-minimal subset if approved (PRNG reseed + stale-closure gate on whole-script path). |
| **G. CLI/scripts** | Sonnet | `scripts/install.sh`, `scripts/test_two_run_determinism.sh`, `flow-cli/Doc/*`, `flow-cli.csproj` | 7.1 artifact naming + RID detect + version stamps, 5.9 CWD resolution + eval quoting, 5.15 failure indexing | install.sh testable against locally-built `publish.sh` artifacts via `--local-tarball`. |
| **W1. Playground/OAuth** | Sonnet | `flow-site/src/lib/playground/*`, `src/lib/showcase/sources.ts` + `pieces.ts`, `src/routes/playground/+page.svelte` | 6.1 stash+resume, 6.2 append `(play mix)` to web variants (+ "rendered-to-file" advisory fallback), 6.7 activation gate, 6.10 test-flag gating | Playwright: mock OAuth redirect, assert editor survives; assert deep-link run reaches `play`. |
| **W2. Home/iOS-6** | Sonnet | `flow-site/src/routes/+page.svelte`, `src/lib/home/*` | 6.5 token scoping, 6.6 `#code=` deep links + delete CodeCard/examples.ts, 6.9 `<main>`/nav labels/aria-hidden/theme | Keep skeuomorphic visuals untouched — markup/CSS-var changes only. |
| **X. WASM runtime** | Sonnet | `Runtime/WasmEntry.cs`, `Audio/WebAudioBackend.cs` (Stop/locking), `Audio/FlowRuntimeInterop.cs`, `flow-lang/wasm/flow-runtime.js` | 5.4 in-memory MIDI hook → `RunResult.midi`, 5.11 stop-all (route engine backend to shared stop; JS `stopAllSources`), 5.12 strip JS debug logs + maxAbs scan | Code only — publish/sync deferred to Wave 4 so the bundle is rebuilt once. New Phase48 test: writeMidi script ⇒ non-null `midi` in JSON. |

## 4. Wave 3 — gated/sequential items (after Wave 2 merges)

| Item | Model | Scope |
|------|-------|-------|
| D1: wire `ReplLineEditor` (§5.1) | Opus | `Repl.cs` ReadCompleteInput → PrettyPrompt path, fix `ReplLineEditor.cs:228` Position bug, history append on submit; manual smoke + existing Repl tests. Sequential because Repl.cs interacts with D-packet's REPL `_returnValue` reset. |
| D5: scoping fix (§2.5) | Opus | Call-boundary frames in `StackFrame`/`ExecutionContext`; full `.flow` suite + all `examples/` must pass; document the rule in wiki/Language-Basics. |
| 8.1 test-skip conversion | Sonnet | Mechanical: guard-`return` → `Assert.Skip(...)` (or `PrereqFact` attribute family) across ~15+ sites. Late so it doesn't conflict with test files other agents touched. |
| 8.7 + 8.8 + 8.9 hygiene | Sonnet | `LibPulse` extraction; 9 file-scoped-namespace conversions + .editorconfig rule; reverb comment replacement. One mechanical agent. |

## 5. Wave 4 — integration (main loop)

1. Merge worktree branches into `dev` sequentially (ownership table ⇒ no conflicts expected; resolve if any).
2. Full verification: `dotnet build` (Desktop + `-p:FlowTarget=Web`), `dotnet test` full suite, `for t in tests/test_*.flow` run, `pnpm -C flow-site test` + `test:e2e`, two-run determinism harness (now works from repo root per §5.9 fix).
3. **WASM bundle refresh (D6):** `dotnet workload install wasm-tools` if absent → `dotnet publish -p:FlowTarget=Web -c Release` → `bash flow-site/scripts/sync-runtime.sh` → commit bundle + `flow-site/.gitattributes` (`static/wasm/_framework/** binary -diff`) (§6.3). Optional: tiny CI staleness check (flow-lang/ changed without static/wasm change ⇒ warn).
4. Baseline ledger review (§7): confirm every regenerated baseline maps to an intended fix.

## 6. Wave 5 — docs truth pass (3 parallel Sonnet agents, main checkout, disjoint files)

Written AFTER code reality settles (e.g. REPL row depends on D1).

| Agent | Files | Findings |
|-------|-------|----------|
| Docs-1 | `CLAUDE.md` | 7.2 (RtMidi → LibRtMidi rewrite ×4 passages, 9-project structure incl. flow-midi, real dep table), Phase 38 honesty amendment per D2, D-48-07 wording (if not Wave 1) |
| Docs-2 | `README.md`, `FEATURES.md` | 7.4 platform/REPL/PolyBLEP rows, 7.5 CLI 14-verb table, gap 10.x rows (transpose-Cent now Fully if A shipped; jux still Partial), install section vs fixed install.sh |
| Docs-3 | `wiki/` | 7.3 new pages: Live-Coding.md, OSC-and-MIDI.md (+ @jack section), micBuffer in Audio page; 7.6 Home.md banner + tooling bullet; Playback-and-Export.md platform claims; 7.7 MidiExport advisory text (1-line code edit, coordinate w/ nobody — file untouched elsewhere) |

## 7. Baseline-change ledger (expected legitimate byte changes)

| Fix | Affected baselines/tests |
|-----|--------------------------|
| 3.1 compressor | any RMS test exercising compress/sidechain |
| 3.2/3.4 stretch+pitchShift | Phase37 baselines (`stretch_pitchshift` fixtures); `examples/dsp/` outputs |
| 3.8 reverb tail | Phase15 reverbTime tests, any Song render using reverb |
| 4.1/4.2 transforms | only voice-block/chord/tuplet material — verify Phase41 `showcase.wav` UNCHANGED (pulse.flow uses match/euclidean/granular, not transforms-on-chords); if it shifts, stop and investigate |
| D3 debounce / 5.7 | none (timing behavior, not rendered bytes) |

Anything NOT on this ledger that changes bytes = regression; agent must stop and report.

## 8. Wave 6 — close-out

1. `/code-review`-style diff review agent over the full merged diff (Sonnet, read-only) — catches cross-packet interactions.
2. Update `.planning/CODEBASE-AUDIT-2026-06-09.md` findings with ✅/deferred status column; STATE.md one-liner (per D7).
3. Re-run the playground HUMAN-UAT-blocking checks locally (`pnpm dev` + manual OAuth-mocked save, showcase deep-link audio in one browser) — the human gates themselves stay yours.

## 9. Explicitly deferred (v1.6 candidates — not in this remediation)

- 8.2 ExecutionContext module-state refactor (touches everything; do as its own phase)
- 8.5 injectable tick source for clock tests
- LIVE-03 full per-block pipeline (per D2) — pairs naturally with the deferred debounce LOCK revisit if D3 = keep
- Gap items that are features, not fixes: renderSong `instruments=` routing (I1), effect automation (I2), midi2flow velocity/drums (10.2), writeMidi pitch-bend microtones (10.4), sparse named args (10.6), jux stereo (10.7) — candidates for a "v1.6 composer-trust" phase seeded from audit §10/§11

## 10. Execution mechanics (when approved)

- Wave 2 launches as ONE Workflow: `pipeline(PACKETS, fix-agent, verify-agent)` — each packet's fix agent (worktree) is followed by a read-only Sonnet verifier that re-runs the packet's tests in the worktree and checks the diff stayed in-scope before I merge.
- Structured agent return: `{commits: [...], findingsFixed: [...], testsAdded: [...], baselinesRegenerated: [...], outOfScopeNotes: [...], blocked: [...]}`.
- Rough scale: ~10 Wave-2 agents + ~10 verifiers + 4 Wave-3 + 3 docs + 1 review ≈ 28 agents. Wave 1 is me inline.
