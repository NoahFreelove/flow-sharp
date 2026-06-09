---
phase: 49-flowlang-dev-site
plan: 05
subsystem: playground
tags: [sveltekit, svelte5-runes, monaco, monarch, wasm, webaudio, playwright, autoplay-gesture]

# Dependency graph
requires:
  - phase: 49-01
    provides: flow-site SvelteKit scaffold + committed Phase 48 AppBundle under static/wasm/ + playwright config (desktop/mobile/mobile-narrow projects) + playground/+page.svelte ssr=false stub + the 5 Wave-0 E2E stubs
  - phase: 49-02
    provides: skeuo components (Button, Panel, Toggle, LedIndicator) + surfaces (.surface-wood/.surface-paper/.surface-brushed-metal/.surface-felt) + design tokens + .sr-only
  - phase: 48
    provides: frozen flow-runtime.js (loadFlowRuntime → { run, play, stop, dispose, resumeAudio }) + RunResult/RunError contract (HANDOFF) + committed AppBundle
  - phase: 17
    provides: Flow TextMate grammar at vscode-extension/syntaxes/flow.tmLanguage.json — the source the Monarch tokenizer derives from
provides:
  - runtime.ts thin @vite-ignore wrapper over the frozen flow-runtime.js (never edits the runtime) + RunResult/RunError TS shapes
  - monaco/index.ts (editor.worker?worker self-host + createFlowEditor + flow-slate theme) + monaco/flow-monarch.ts (hand-written Flow Monarch tokenizer)
  - playground/state.svelte.ts (Svelte 5 runes PlaygroundState) + snippets.ts (Web-target-safe Quick-Start set) + download.ts (Blob/anchor offerDownload)
  - /playground three-column desktop / single-column mobile page with gesture-chained Run, stdout/stderr split, escaped Rust-style error boxes, conditional MIDI download, mobile read-only Monaco
  - 5 fleshed-out E2E specs (wasm-boot / playground-run / playground-audio / playground-export / playground-mobile)
affects: [49-06 (Share/Save buttons + #code= encode.ts fills the fragment decode this page reads guardedly), 49-08 (HUMAN-UAT — audible audio + MIDI-bytes once the Phase 48 in-memory hook lands)]

# Tech tracking
tech-stack:
  added: []   # monaco-editor 0.55.1 was already in package.json from the 49-01 scaffold; no new packages
  patterns:
    - "Svelte 5 runes class state in a .svelte.ts module (PlaygroundState) consumed by the page; NEVER name the local instance `state` — it shadows the $state rune and the compiler emits store_get($state) → SSR 500"
    - "Monaco + WASM are dynamic-imported INSIDE onMount only (Pitfall 1); ssr=false on the route; the AppBundle stays opaque (@vite-ignore) and self-loads its own ./_framework/dotnet.js"
    - "Autoplay gesture chain: Run onclick awaits resumeAudio() THEN run() back-to-back in one async frame (HANDOFF §5); AudioContext wrapped via a Proxy construct-trap so a test hook can read .state headless"
    - "RunResult strings rendered with Svelte curly-expr auto-escape (never raw HTML) — attacker-controllable #code= source, Security V5"
    - "Monaco editor.worker?worker self-host + worker.format='es' (Firefox module worker); optimizeDeps include monaco-editor / exclude editor.worker"

key-files:
  created:
    - flow-site/src/lib/runtime.ts
    - flow-site/src/lib/monaco/index.ts
    - flow-site/src/lib/monaco/flow-monarch.ts
    - flow-site/src/lib/playground/state.svelte.ts
    - flow-site/src/lib/playground/snippets.ts
    - flow-site/src/lib/playground/download.ts
  modified:
    - flow-site/vite.config.ts
    - flow-site/src/routes/playground/+page.svelte
    - flow-site/tests/wasm-boot.spec.ts
    - flow-site/tests/playground-run.spec.ts
    - flow-site/tests/playground-audio.spec.ts
    - flow-site/tests/playground-export.spec.ts
    - flow-site/tests/playground-mobile.spec.ts

decisions:
  - "Renamed the page's local PlaygroundState instance `state` → `pg` (Rule 1 bug): a variable named `state` shadows the $state rune; the Svelte compiler resolved `$state(...)` as store auto-subscription of `state` (store_get($$store_subs, '$state', state)), crashing SSR with `store.subscribe is not a function`."
  - "Snippets use `(play | ... |)` for note streams and `(writeMidi path Song)` for export — `renderSequence` does not resolve and `(play Song)` has no overload on the Web target; corrected to the actual builtin surface."
  - "MIDI download wired forward-compatibly: the SHIPPED Phase 48 WasmEntry.cs hardcodes `Midi = null` (the in-memory writeMidi capture hook is reserved, HANDOFF §9 describes the INTENDED contract). The button renders only `{#if pg.hasMidi}` so it lights up the moment a future runtime populates `midi`; we did NOT edit the frozen runtime (HANDOFF §8). Recorded as a 49-08 UAT item."
  - "Audio E2E asserts AudioContext `.state === 'running'` after the gesture (resume succeeded) — audibility is headless-unverifiable and is a 49-08 HUMAN-UAT item."

metrics:
  duration: ~50min
  tasks: 3
  files_created: 6
  files_modified: 7
  commits: 3
  completed: 2026-06-05
---

# Phase 49 Plan 05: WASM Playground Summary

Built the interactive playground tab: the Phase 48 WASM runtime lazy-boots in `onMount`, Monaco mounts client-only with hand-written Flow Monarch highlighting, Svelte 5 runes drive editor/console/run state, the Run button chains `resumeAudio()` + `run()` in one user-gesture frame, the console splits stdout/stderr and renders escaped Rust-style error boxes, a conditional MIDI download button is wired, and Monaco degrades to read-only single-column on mobile down to 320px. End-to-end verified in headless chromium: the .NET-in-WASM runtime boots, `(print ...)` produces stdout, and the AudioContext reaches `running` after the gesture.

## What shipped

**Task 1 — runtime wrapper + Monaco + Monarch (`9ef9fbe`)**
- `runtime.ts` — thin `bootRuntime()` that `await import(/* @vite-ignore */ '/wasm/flow-runtime.js')` then `loadFlowRuntime()`; re-exports `RunResult`/`RunError`/`FlowRuntime` TS shapes for editor typing. Never touches the frozen runtime (HANDOFF §8). A single `@ts-expect-error` covers the runtime-only static-asset import (no `.d.ts` ships for it).
- `monaco/index.ts` — `editor.worker?worker` self-host + `self.MonacoEnvironment = { getWorker }` (base editor worker only — single custom language); `createFlowEditor(container, opts)` registers the `flow` language + Monarch tokenizer + a `flow-slate` theme on `--color-slate`, JetBrains Mono, line numbers, `automaticLayout: true`.
- `monaco/flow-monarch.ts` — hand-written `IMonarchLanguage` derived from the Phase 17 TextMate scopes: keywords (incl. the reserved musical-context block words), types (the FlowType surface), chord literals BEFORE note literals (so `Bb7`/`Cmaj7` tokenize as chords), music-numeric suffixes (`Hz/kHz/dB/ms/s/st/c/b`), symbols `#foo`, `-> ~> => @`, note-stream `|` + tuple `<< >>`, comments, strings.
- `vite.config.ts` — `optimizeDeps.include monaco-editor` / `exclude editor.worker` + `worker.format: 'es'`.

**Task 2 — runes state + snippets + download (`0ce8101`)**
- `state.svelte.ts` — `PlaygroundState` with `$state` (editorValue/bootError/runStatus/stdout/stderr/errors/midi/lastDurationMs/lastRunAt/activeSnippetId) + `$derived` (hasRun/hasMidi); `run(runtime, source)` splits the `RunResult` into fields and drops the never-raised `cancel` kind (D-48-10); `stop`/`newBlank`/`loadSnippet`/`downloadMidi`. The autoplay gesture lives in the page, not here.
- `snippets.ts` — 6 Web-target-safe Quick-Start snippets (sine-440 default, print-to-console, note-stream melody, chord progression, Song→MIDI, print-arithmetic); no sampler/OSC/mic/live (stripped on Web).
- `download.ts` — `offerDownload(bytes, name, mime)` (Blob + anchor + `revokeObjectURL`) + `offerMidiDownload`; normalizes a `Uint8Array` into a plain `ArrayBuffer` for strict `BlobPart` typing.

**Task 3 — page + gesture Run + console + mobile + E2E (`46828b4`)**
- `playground/+page.svelte` — three-column grid (30/50/20) collapsing to single column <768px. `onMount` lazy-boots the runtime (catch → boot-error pane with the UI-SPEC copy), mounts Monaco, reads `#code=` guardedly (49-06 hardens). Run onclick = `await runtime.resumeAudio(); const p = pg.run(...);` — single gesture frame. Console: stdout pane (ink) + stderr sub-region (ink-muted, "Advisories") + escaped Rust-style error boxes with `--color-danger` rail. LedIndicator + Stop + conditional Download MIDI. Brushed-metal status bar (runtime · bundle · last-run). Mobile: `readOnly` Monaco + banner; `data-readonly` mirror for the E2E.
- 5 E2E specs replace the Wave-0 stubs (15 runs across desktop/mobile/mobile-narrow, all green).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Local `state` variable shadowed the `$state` rune → SSR 500**
- **Found during:** Task 3 (first `/playground` load returned HTTP 500: `store.subscribe is not a function`).
- **Issue:** The page declared `const state = new PlaygroundState()`. The Svelte 5 compiler tokenized the unrelated `$state(...)` rune calls as **store auto-subscription of the `state` variable** — the compiled SSR output was `let ready = store_get($$store_subs, "$state", state)(false)`, which throws because `PlaygroundState` is not a store. This 500'd the route (CSR never started).
- **Fix:** Renamed the instance `state` → `pg` throughout the page (protecting `$state`, `audioState`, `runStatus`, the `LedIndicator state=` prop name, and the import path). Root-caused by SSR-rendering the built component in isolation to capture the exact `store_get` line.
- **Files modified:** `flow-site/src/routes/playground/+page.svelte` — **Commit:** `46828b4`

**2. [Rule 1 - Bug] Snippets used non-resolving Flow builtins**
- **Found during:** Task 3 (snippet validation probe — `renderSequence not found`, `play(Song)` no overload, `writeMidi(String, Sequence)` no overload).
- **Issue:** The plan-body snippet sketches used `(play (renderSequence seq))` and `(writeMidi path (renderSequence seq))`; `renderSequence` is not exposed and `play`/`writeMidi` don't accept those shapes on the Web target.
- **Fix:** Note streams play directly via `(play | ... |)`; MIDI export goes through a `Song` (`section` → `Song s = [...]` → `(writeMidi path s)`). All 6 snippets now run with zero Flow errors (verified against the live runtime).
- **Files modified:** `flow-site/src/lib/playground/snippets.ts` — **Commit:** `46828b4`

**3. [Rule 3 - Blocking] TS module-resolution + strict-BlobPart compile errors**
- **Issue:** (a) `import('/wasm/flow-runtime.js')` has no `.d.ts` → svelte-check `Cannot find module`. (b) `new Blob([uint8])` failed strict lib.dom typing (`ArrayBufferLike` vs `ArrayBuffer`, SharedArrayBuffer mismatch, TS 6).
- **Fix:** (a) `@ts-expect-error` on the runtime-only static-asset import (typed via `FlowRuntimeModule`). (b) copy the bytes into a fresh `ArrayBuffer` before the Blob.
- **Files modified:** `flow-site/src/lib/runtime.ts`, `flow-site/src/lib/playground/download.ts` — **Commits:** `9ef9fbe`, `0ce8101`

## Known Stubs

- **MIDI download button (forward-compatible, NOT goal-blocking).** The button renders only `{#if pg.hasMidi}`. The SHIPPED Phase 48 `WasmEntry.cs` hardcodes `Midi = null` (the in-memory `writeMidi` capture hook is reserved — HANDOFF §9 documents the INTENDED contract, not a current behavior). Per HANDOFF §8 the runtime is frozen and must NOT be edited, so the playground wires the download mechanism (verified to fire a real Blob download) and the conditional button, which light up automatically once a future WASM-runtime phase populates `RunResult.midi`. Resolution path: 49-08 HUMAN-UAT confirms, or a v1.6 WASM-runtime phase wires the hook. The `playground-export.spec.ts` asserts the mechanism + the correct no-MIDI button absence.
- **Share / Save to gist buttons** render disabled (label-only) — wired in Plan 49-06 (gist OAuth + `#code=` encode). The page already reads the `#code=` fragment guardedly so 49-06's encode.ts plugs in.

## New Packages

None. `monaco-editor` 0.55.1 was already a `dependencies` entry from the 49-01 scaffold; no install ran, so no slopsquat/legitimacy gate applied. Zero lockfile changes.

## Threat Flags

None beyond the plan's `<threat_model>`. The three `mitigate`/`transfer` dispositions are all honored:
- **T-49-05-XSS (mitigate):** all `RunResult` strings (stdout/stderr/`errors[]` message/snippet) render via Svelte curly-expr auto-escape — no `{@html}` anywhere in the page (grep-verified); shared `#code=` source loads into Monaco as text.
- **T-49-05-FRAG (transfer):** `readFragmentSource()` defensively decodes + size-caps (100 KB) the `#code=` fragment and tolerates its absence; full hardening is 49-06's encode.ts.
- **T-49-05-BOOT (mitigate):** boot failure surfaces the friendly UI-SPEC copy in a top-level pane; `RunError.message` is the runtime-sanitized string (no .NET stack traces — T-48-15).
- **T-49-05-DoS (accept):** documented; the 30s cap is best-effort/non-preemptive single-threaded WASM — a runaway script hangs only its own tab.

## Verification

- `pnpm -C flow-site build` → exit 0 (no Monaco SSR crash; AppBundle copied to output; `_headers` lands at output root).
- `pnpm -C flow-site exec playwright test tests/wasm-boot.spec.ts tests/playground-run.spec.ts tests/playground-audio.spec.ts tests/playground-export.spec.ts tests/playground-mobile.spec.ts` → **15 passed** (5 specs × desktop/mobile/mobile-narrow; system-chromium fallback).
  - wasm-boot: runtime ready, no boot-error pane, three-column scaffold visible.
  - playground-run: `(print ...)` → stdout pane contains `hello flow` + `3`; empty state before first run; no error boxes.
  - playground-audio: AudioContext `.state === 'running'` after the Run gesture (resume succeeded).
  - playground-export: Blob/anchor MIDI download fires (`flow.mid`); button absent on a no-MIDI run.
  - playground-mobile: read-only banner + `data-readonly="true"` Monaco + single-column stack + **no horizontal overflow at 375px AND 320px** + Run still resumes audio.
- `pnpm -C flow-site check` (svelte-check) → 0 errors (2 pre-existing Wave-0 warnings: design-page unused CSS + node types).
- Gesture-chain grep (`resumeAudio()…run(`), no-`{@html}`, `readOnly` present, no `test.skip` — all pass.
- Direct runtime probe: the .NET-in-WASM engine boots in headless chromium and executes Flow; all 6 snippets run with zero Flow errors.

## Deferred to 49-08 HUMAN-UAT

- **Audible output** across Firefox / Chrome / Safari (headless asserts only AudioContext `running`).
- **Real `RunResult.midi` bytes** + the MIDI download button appearing from an actual `writeMidi` run (pending the Phase 48 in-memory capture hook; the UI + mechanism are wired and tested).

## Self-Check: PASSED

All 6 created lib files + the SUMMARY exist on disk; all 3 task commits (`9ef9fbe`, `0ce8101`, `46828b4`) present in git history.
