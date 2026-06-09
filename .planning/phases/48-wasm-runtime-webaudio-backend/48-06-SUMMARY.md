---
phase: 48-wasm-runtime-webaudio-backend
plan: 06
subsystem: human-uat-browser-smoke
tags: [wasm, webaudio, human-uat, browser-smoke, autoplay, in-phase-repair, firefox]
requirements: [REQ-WEBAUDIO-04, REQ-WASM-API-03]
dependency-graph:
  requires:
    - "Plan 48-04 (flow-runtime.js ES module + WasmEntry.cs JSExport + index.html dev harness — the surface the human smoke exercises)"
    - "Plan 48-05 (two-run determinism + bundle-size pins — the in-process contract the browser smoke validates the human-observable half of)"
  provides:
    - "48-HUMAN-UAT.md — 3-row browser smoke (Chrome/Firefox/Safari) with composer sign-offs: Firefox PASS, Chrome boot-fixed/audio-deferred, Safari skipped"
    - "9 fix(48-06) commits that turned the FlowTarget=Web library into a bootable, audible in-browser runtime (root-cause repair surfaced by the human smoke)"
  affects:
    - "Plan 48-07 closer reads 48-HUMAN-UAT.md outcomes for the VERIFICATION Known-Caveats + routes the Chrome/Safari follow-up to the v1.6 backlog"
    - "Phase 49 SvelteKit playground inherits a runtime proven to boot + play audio in Firefox; Chrome/Safari re-smoke is a documented follow-up"
tech-stack:
  added: []
  patterns:
    - "Synchronous execution on single-threaded Mono-WASM — Task.Run + Wait() deadlocks the only thread; both Flow eval and WebAudioBackend.Play must run inline on the JS-invoked call (commits a8c1911, 805269c)"
    - "FlowTarget=Web library must emit its own bootable AppBundle — a library publish skips app-bundle generation by default, so no dotnet.boot.js was produced (root cause; commit 08140bb + 35dd537 gate)"
    - "Source-generated JSON for RunResult — reflection-based System.Text.Json throws JsonSerializerIsReflectionDisabled under TrimMode=full; a JsonSerializerContext restores trim-safe (de)serialization (commit 5b80c01)"
    - "stdlib .flow files embedded as resources into the WASM so the builtin surface loads in-browser with no filesystem (commit 5ccc10e)"
    - "AudioContext created + resumed inside resumeAudio() within the user-gesture frame so playback is actually audible under the browser autoplay policy (commit a5ae19f; D-48-09)"
    - "Charitable Web-target skip of stripped-impl stdlib procs (micBuffer/loadSfz) so a Web build doesn't trip on Desktop-only builtins (commit b46589c)"
key-files:
  created:
    - ".planning/phases/48-wasm-runtime-webaudio-backend/48-HUMAN-UAT.md (scaffold landed 2026-05-30 commit d808743; filled with composer sign-offs 2026-06-05)"
  modified:
    - "flow-lang/flow-lang.csproj (WASM app-bundle emit + publish-phase gate + embedded stdlib resources)"
    - "flow-lang/Runtime/WasmEntry.cs (synchronous Flow execution; source-gen JSON context)"
    - "flow-lang/Audio/WebAudioBackend.cs (synchronous Play; create+resume AudioContext in resumeAudio)"
    - "flow-lang/wasm/index.html (use \"@audio\" default script; verified AppBundle serve-path header)"
    - "flow-lang/wasm/flow-runtime.js (resumeAudio gesture chain)"
decisions:
  - "APPROVED-WITH-FOLLOWUP per Closure Conditions. Firefox PASS (composer heard the 440 Hz tone, autoplay-correct) satisfies the 'Row signed off as pass OR documented gotcha non-blocking' bar and proves the runtime end-to-end. Chrome and Safari are documented non-blocking follow-ups, not blockers."
  - "Original 2026-05-30 Chrome BLOCKER (dotnet.boot.js 404) ROOT-CAUSED + FIXED, not worked around: a FlowTarget=Web *library* publish skipped app-bundle generation entirely, so dotnet.js had no boot manifest. Commit 08140bb makes the Web build emit a bootable AppBundle; 35dd537 gates it to the publish phase. Re-smoke verified: curl /_framework/dotnet.boot.js → HTTP 200."
  - "Single-threaded WASM forced two sync rewrites: Flow eval (a8c1911) and WebAudioBackend.Play (805269c) both deadlocked under the Phase 47-era Task.Run+Wait pattern. Running inline on the JS-invoked call is the correct Mono-WASM idiom and is the reason the tone is now audible."
  - "Chrome audio re-smoke DEFERRED to v1.6 follow-up (Plan 48-07 logs it to MILESTONES.md). The boot blocker that originally failed Chrome is fixed and HTTP-verified; only the human ear-check is outstanding, and Firefox already provides that evidence on the same engine path. Non-blocking."
  - "Safari SKIPPED — no macOS available on a Linux-only dev machine; routed to Phase 49 / v1.6 per Closure Conditions."
  - "Setup block in 48-HUMAN-UAT.md left as the historical pre-fix layout with an explicit correction note pointing at the verified post-fix serve path (browser-wasm/AppBundle/ + /index.html) in flow-lang/wasm/index.html and .planning/debug/wasm-boot-no-app-bundle.md."
metrics:
  completed: 2026-06-05
  tasks: 2
  files_created: 1
  files_modified: 6
  files_deleted: 0
  fix_commits: 9
  human_uat_rows_total: 3
  human_uat_rows_pass: 1
  human_uat_rows_deferred: 1
  human_uat_rows_skipped: 1
  bundle_size_compressed_brotli: "3.07 MB (per 48-BUNDLE-SIZE.md / Plan 48-05; unchanged)"
  web_publish_status: "exit 0 (fresh publish 2026-06-05)"
  boot_manifest_http: "200 (dotnet.boot.js; was 404 on 2026-05-30)"
---

# Phase 48 Plan 06: HUMAN-UAT Browser Smoke — Summary

## What this plan delivered

Plan 48-06 was the one `autonomous: false` plan in Phase 48 — a human checkpoint where the
composer validates the WASM runtime end-to-end in real browsers (only a human ear + a browser
session can confirm audio actually comes out and the autoplay policy is honored).

The scaffold (`48-HUMAN-UAT.md`, 3 browser rows + reproducible steps + closure conditions)
landed 2026-05-30. The **first browser smoke immediately surfaced a BLOCKING boot failure** in
Chrome: `dotnet.boot.js` 404. Per the plan's Closure Conditions this routed to in-phase repair
(`/gsd:debug`) rather than a deferral — it was a runtime/build-config defect, not a UX nit.

## In-phase repair (9 commits)

Root cause: a `FlowTarget=Web` **library** publish never runs app-bundle generation, so
`dotnet.js` shipped with no boot manifest. The repair chain:

| Commit | Fix |
|--------|-----|
| `08140bb` | Emit a bootable WASM AppBundle for the `FlowTarget=Web` library (root cause) |
| `35dd537` | Gate the app-bundle to the publish phase (`WasmBuildOnlyAfterPublish`) |
| `5b80c01` | Source-gen JSON for the WASM `RunResult` (trim-safe under `TrimMode=full`) |
| `5ccc10e` | Embed stdlib `.flow` into the WASM so the builtin surface loads in-browser |
| `a8c1911` | Run Flow synchronously in WASM — `Task.Run`+`Wait` deadlocks single-threaded Mono |
| `805269c` | Run `WebAudioBackend.Play` synchronously — same single-thread deadlock |
| `b46586c`* | Charitably skip stripped-impl stdlib procs (`micBuffer`/`loadSfz`) on Web |
| `941ef0a` | Prepend `use "@audio"` to the dev-harness default script |
| `a5ae19f` | Create + resume the `AudioContext` in `resumeAudio()` so playback is audible |

(*commit `b46589c`)

## Re-smoke outcome (2026-06-05)

Fresh `dotnet publish … -p:FlowTarget=Web -c Release` → served `browser-wasm/AppBundle/` on
`localhost:8080`. Boot manifest now resolves (`curl /_framework/dotnet.boot.js` → **HTTP 200**).

- **Row 2 — Firefox (Linux): PASS.** Composer clicked Run and heard the audible 440 Hz tone;
  no sound before the gesture (autoplay-correct, D-48-09 satisfied). This is the load-bearing
  evidence that .NET-in-WASM → browser `AudioContext` audio works — Phase 48's single biggest
  feasibility risk, cleared.
- **Row 1 — Chrome (Linux): DEFERRED.** The original boot blocker is fixed + HTTP-verified, but
  the composer tested Firefox only this session, so Chrome audio is not human-confirmed. Logged
  to the v1.6 backlog by Plan 48-07. Non-blocking.
- **Row 3 — Safari: SKIPPED.** No macOS available; routed to Phase 49 / v1.6.

## Verdict

**APPROVED-WITH-FOLLOWUP.** Firefox PASS satisfies the Closure Conditions and proves the runtime.
Chrome audio re-smoke + Safari smoke are documented non-blocking follow-ups for the v1.6 backlog.
Plan 48-07 (closer) consumes this file next.

## Self-Check: PASSED
- [x] 48-HUMAN-UAT.md filled with composer sign-offs (Firefox PASS / Chrome deferred / Safari skipped)
- [x] Status flipped from BLOCKED to APPROVED-WITH-FOLLOWUP
- [x] In-phase repair commits recorded with SHAs
- [x] Outstanding follow-ups identified for Plan 48-07 → v1.6 backlog routing
- [x] No production code modified by this close-out (UAT doc + summary only)
