---
slug: playground-every-other-run-silence
status: resolved
trigger: |
  In the flowlang.dev playground (Phase 48 WASM runtime), pressing Run plays
  audio the first time, then SILENCE on the next run, then audio again — an
  alternating/intermittent silence on repeated runs of the SAME (working) snippet
  (reproduced on the "organ" hymn). Pre-existing (an open Phase 49 cross-browser-
  audible HUMAN-UAT gate); NOT caused by the static/examples refactor (the
  run/audio path was untouched). User verifies by EAR at a checkpoint.
created: 2026-06-26
updated: 2026-06-26
---

## Current Focus
<!-- OVERWRITE on each update -->

status: RESOLVED. Root cause confirmed in-browser; Candidate A applied + committed (0c3073e);
human by-ear acceptance PASSED (organ hymn audible on every run, 4-5× in a row).

reasoning_checkpoint:
  hypothesis: "Per-run engine recycle (NewEngineForRun) disposes the prior FlowEngine →
    WebAudioBackend.Dispose → JS closeContext → ctx.close() + `_audioContext = null`, which
    closes the tab-lifetime AudioContext MID-RUN; the alternate run's createAudioContext then
    returns a CLOSED context (or none) so source.start() is silent."
  confirming_evidence:
    - "playwright/?e2e=1 console timeline: on runs 2 & 4, [DBG closeContext] FIRED fires
      BETWEEN resumeAudio and the run's createAudioContext, and createAudioContext then logs
      state=closed. On runs 1,3,5 no closeContext fires and createAudioContext logs running."
    - "window.__flowAudioCtx.state alternates exactly: run1 running, run2 closed, run3 running,
      run4 closed, run5 running — a perfect every-other-run pattern matching the symptom."
    - "Run 1 is audible because NewEngineForRun has no previous engine to dispose (confirmed:
      no closeContext log before run 1's createAudioContext)."
  falsification_test: "If closeContext had NOT fired mid-run, or the context had stayed
    'running' on every run, the chain would be FALSIFIED. It fired on exactly the silent runs."
  fix_rationale: "Candidate A removes the per-run teardown: closeContext drains active sources
    but does NOT call ctx.close() and does NOT null _audioContext. The context is one-per-TAB
    (D-48-08) and must persist (resumed/suspended) for the tab's life. This addresses the root
    cause (the close→null→recreate-on-closed chain), not a symptom."
  blind_spots: "Headless gives every fresh context state=running on a real click (user
    activation); a real browser cold-start would be 'suspended' until gesture — but the FIX
    keeps ONE persistent resumed context, so the suspended-recreate path no longer occurs.
    By-ear audibility is the human gate (cannot self-verify sound)."
next_action: NONE — resolved. Human by-ear acceptance PASSED (user hard-reloaded the
  playground and confirmed the organ hymn "Abide With Me" plays audio on every run, 4-5× in
  a row; the every-other-run silence is gone). Fix committed 0c3073e on dev. Session archived.

## Evidence
<!-- APPEND only -->

- checked: playwright /playground?e2e=1, organ hymn, 5 consecutive Run clicks, BEFORE fix
  (served flow-runtime.js temporarily instrumented with [DBG] console.logs).
  found: window.__flowAudioCtx.state per run = [running, closed, running, closed, running].
    Console timeline: runs 2 & 4 show "[DBG closeContext] FIRED ctx.state=running" BETWEEN
    resumeAudio and that run's createAudioContext, and createAudioContext then logs
    state=closed; runs 1,3,5 show no closeContext and createAudioContext state=running.
  implication: ROOT CAUSE CONFIRMED IN-BROWSER (not falsified). The per-run engine recycle's
    closeContext closes the shared tab AudioContext mid-run → next run plays on a CLOSED
    context → silence. Exact every-other-run alternation reproduced. Run 1 audible because no
    prior engine exists to dispose.

- checked: Candidate A applied (closeContext drains _activeSources only; no ctx.close(); no
  _audioContext=null) to BOTH copies byte-identically; re-ran same 5-click playwright repro.
  found: window.__flowAudioCtx.state per run = [running, running, running, running, running]
    in BOTH the instrumented AFTER run AND the final un-instrumented FINAL-CLEAN run. closeContext
    still fires on recycle runs (2 & 4) but ctx.state stays running and createAudioContext returns
    the same persistent running context. No page errors; repeated same-snippet runs did not throw
    "Variable already declared" (NewEngineForRun reset intact).
  implication: Fix holds at the AudioContext-lifecycle level across >=4 consecutive runs.
    Audible confirmation is the human-by-ear gate (cannot self-verify sound headless).

- checked: regression — code paths for (stop) and dispose (WebAudioBackend.cs:212/279).
  found: Stop() → FlowRuntimeInterop.StopAllSources() → JS stopAllSources (UNCHANGED by the fix);
    Dispose() → FlowRuntimeInterop.CloseContext() → JS closeContext (now drain-only). Existing
    Dispose() comment already states the context is GC'd by the browser when the C# handle drops.
  implication: (stop) unaffected; per-run/dispose teardown no longer closes the tab context
    (intended D-48-08 behavior). Offline writeWav/writeMidi don't use WebAudioBackend — untouched.

## Resolution
<!-- OVERWRITE as understanding evolves -->

root_cause: WebAudioBackend.Dispose() fires on EVERY per-run FlowEngine recycle
  (WasmEntry.NewEngineForRun); its JS closeContext called ctx.close() + nulled the module-global
  _audioContext, tearing down the one-per-tab (D-48-08) AudioContext mid-run. On alternate runs
  the script's (play) → createAudioContext returned the now-CLOSED context, so source.start()
  was silent. Run 1 escaped because there was no prior engine to dispose.
fix: Candidate A (JS-only, no WASM rebuild). closeContext now STOPS active sources (drains
  _activeSources) but does NOT call ctx.close() and does NOT null _audioContext — the tab-lifetime
  context persists (resumed/suspended) and is GC'd on tab close. Applied byte-identically to
  flow-site/static/wasm/flow-runtime.js (served + committed) and flow-lang/wasm/flow-runtime.js
  (canonical, survives a future sync-runtime.sh republish).
verification: playwright /playground?e2e=1 — AudioContext stays 'running' across 5 consecutive
  runs (before: running/closed/running/closed/running). (stop) + NewEngineForRun reset + offline
  determinism unaffected. Human by-ear acceptance PASSED 2026-06-26: user hard-reloaded the
  playground, ran the organ hymn 4-5× in a row, heard sound every time (no alternating silence).
committed: 0c3073e (dev) — both flow-runtime.js copies, byte-identical.
files_changed:
  - flow-site/static/wasm/flow-runtime.js (closeContext drain-only)
  - flow-lang/wasm/flow-runtime.js (closeContext drain-only, byte-identical)

follow_up (OUT OF SCOPE — optional v1.6 hardening, do NOT do now): Candidate B — decouple the
  AudioContext from the per-run engine in C# (route script (play) through the process-shared
  WasmEntry._sharedBackend whose context survives across runs, and/or make WebAudioBackend.Dispose
  NOT close the context on per-run engine recycle, only on real DisposeFromJs). Requires
  `dotnet publish -p:FlowTarget=Web` + `bash flow-site/scripts/sync-runtime.sh`. The canonical
  flow-lang/wasm/flow-runtime.js already carries the Candidate A fix, so a sync-runtime.sh
  republish will NOT regress it in the meantime.

# Debug Session: playground-every-other-run-silence

## Symptoms

**Expected:** Pressing Run plays the snippet's audio every time.

**Actual:** Run 1 audible; Run 2 silent; Run 3 audible (alternating / intermittent).
Reproduced by the user on the organ hymn (`abide-with-me`) — a snippet that DOES
produce sound on run 1. Affects the web playground generally.

**Errors:** None — audio just doesn't sound; no error box.

**Timeline:** Pre-existing frozen Phase 48 runtime behavior (repeated-run audio was
an open Phase 49 HUMAN-UAT item). Surfaced now while testing examples on the dev
server (http://localhost:5179). Independent of the static/examples refactor.

**Reproduction:** `pnpm -C flow-site dev` (running on :5179) → /playground → load
"Abide With Me (hymn)" → Run (hear it) → wait for it to finish → Run again (silence)
→ Run again (sound). The playground's onRun already does
`await runtime.resumeAudio(); pg.run(...)` in one gesture frame (correct per D-48-09).

## Root Cause (CONFIRMED by code trace — verify in-browser before fixing)

Per-run engine recycle tears down the shared AudioContext:
- `WasmEntry.NewEngineForRun()` (flow-lang/Runtime/WasmEntry.cs:205) disposes the
  PREVIOUS `_sharedEngine` on EVERY `RunFromJs` (needed for the "Variable already
  declared" reset — fresh engine per run).
- `FlowEngine.Dispose()` (Core/FlowEngine.cs:532) → `_audioManager.Dispose()` →
  `AudioPlaybackManager.Dispose()` (Audio/AudioPlaybackManager.cs) → `_backend.Dispose()`
  → `WebAudioBackend.Dispose()` → JS `closeContext` (flow-runtime.js ~175) → `ctx.close()`
  + `_audioContext = null`.
- The JS `_audioContext` is a MODULE-GLOBAL singleton shared by every backend
  (CreateAudioContext: `if (!_audioContext) _audioContext = new AudioContext({sampleRate}); return _audioContext`).
- So: Run N's onRun resumes the shared context (gesture frame); then RunFromJs's
  NewEngineForRun disposes engine(N-1) → closeContext CLOSES that shared context;
  then engine(N)'s `(play)` → CreateAudioContext sees `_audioContext===null` → creates
  a NEW context MID-RUN (outside the gesture frame) → starts SUSPENDED → `source.start()`
  is silent. The async `closeContext` (`await ctx.close()` then null) racing the
  synchronous Play makes the exact parity timing-dependent → the alternating pattern.
- Run 1 is audible because NewEngineForRun has no previous engine to dispose
  (`if (_sharedEngine != null)`), so the gesture-resumed context survives.

CONTRAST WITH THE FALSIFIED-AGGRESSIVELY lesson from varispeed-aliasing-static:
this is a DIRECT lifecycle chain (dispose → close → null), not a metric inference —
but STILL confirm in-browser before committing (instrument the JS, see the close
fire mid-run).

## Goal

Audio plays on EVERY run in the playground (no alternating silence). Acceptance:
1. User confirms by ear: Run the organ hymn 4-5 times in a row — sound EVERY time.
   (HUMAN-VERIFY checkpoint — return to orchestrator; AskUserQuestion unavailable here.)
2. The fix does not regress: a script's own `(stop)` still stops audio; a fresh
   engine per run still fixes the "Variable already declared" double-run reset
   (don't break NewEngineForRun's reset semantics); the 30s-cap behavior unchanged.
3. Offline determinism untouched (writeWav/writeMidi paths don't use WebAudioBackend).
4. If feasible, a playwright assertion in `?e2e=1` mode (the AudioContext Proxy
   exposes `window.__flowAudioCtx`) that the context stays `running` (not closed)
   across N consecutive runs.

## Fix candidates (prefer least blast radius / no WASM rebuild)

- **Candidate A (JS-only, PREFERRED — no WASM recompile, dev server hot-serves it):**
  In flow-runtime.js make `closeContext` NOT close/null the tab-lifetime AudioContext
  — instead stop active sources (drain `_activeSources`) and LEAVE `_audioContext`
  alive (D-48-08 = one context per TAB, so it should persist for the tab's life and
  only be resumed/suspended, never torn down per-run). The context is GC'd on tab
  close. Edit BOTH copies: canonical `flow-lang/wasm/flow-runtime.js` AND the published
  `flow-site/static/wasm/flow-runtime.js` (keep them byte-identical). This avoids the
  per-run teardown entirely with no C# change. VERIFY it doesn't break `(stop)` (which
  should still stop sources — it calls stopAllSources, unaffected) or DisposeFromJs.
- **Candidate B (C#, more thorough, needs WASM rebuild + republish):** decouple the
  AudioContext from the per-run engine — route WASM script `(play)` through the
  process-shared `WasmEntry._sharedBackend` (whose context survives across runs),
  and/or make WebAudioBackend.Dispose NOT close the context on per-run engine recycle
  (only on real DisposeFromJs). Requires `dotnet publish -p:FlowTarget=Web` (wasm-tools)
  + `bash flow-site/scripts/sync-runtime.sh` to refresh `flow-site/static/wasm`.

Start with Candidate A; only escalate to B if A doesn't fully resolve it in-browser.

## Verification approach

- Instrument the CURRENT flow-runtime.js (temporary console.log in createAudioContext /
  closeContext / resumeAudio) and reproduce on the running dev server (:5179) — confirm
  closeContext fires mid-run and `_audioContext` is recreated suspended on the silent runs.
- After Candidate A: re-test repeated runs (by-ear via orchestrator→user; and/or
  playwright `?e2e=1` observing `window.__flowAudioCtx.state` stays 'running' across runs).
- Note: the dev server serves `flow-site/static/wasm/flow-runtime.js` directly — editing
  it (Candidate A) takes effect on browser reload with NO rebuild. The canonical
  `flow-lang/wasm/flow-runtime.js` must be edited to match so a future
  `sync-runtime.sh` republish doesn't regress it.

## Out of scope
- Web sampled-piano = silence (separate; ragtime snippet swapped to organ, committed d4322f0).
- The static/examples refactor (committed 21a4b25) — works; not the cause.
- Desktop audio backends (CoreAudio/PulseAudio) — unaffected.
</content>
