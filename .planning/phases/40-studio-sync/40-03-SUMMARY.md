---
phase: 40-studio-sync
plan: 03
subsystem: studio-sync
tags: [jack, transport, p-invoke, ableton-link-defer, license-gate, web-strip, determinism, phase-closer]
requires:
  - "flow-lang/StandardLibrary/Network/OscFunctions.cs (Register + gate template)"
  - "flow-lang/TypeSystem/SpecialTypes/ClockHandleType.cs (Plan 02 — handle-type precedent, specificity 153)"
  - "flow-lang/Runtime/MusicalContext.cs:43 (Tempo) + :180 (IsValidTempo)"
  - "flow-lang.Tests/Integration/Phase40/OfflineRenderDeterminismTests.cs (Plan 01 — LINK-02 gate reinforced)"
  - "libjack.so.0 (runtime native dep, present on dev box)"
provides:
  - "jackSync() best-effort JACK transport sync via @jack (JACK-01) — hand-rolled jack_transport_query P/Invoke, charitable absent-server no-op"
  - "JackHandleType (ref-identity, specificity 154) + JackHandleData + Value.JackHandle factory + ClockMode-free snapshot"
  - "@jack 3-site opt-in gate: ExecutionContext.JackEnabled + snapshot/restore + TestSnapshot"
  - "JackFunctions.TransportQueryOverride test seam (synthetic transport, no real JACK)"
  - "LinkDeferralTests — LINK-01 honest defer record (no GPL ref) + LINK-02 determinism reinforcement"
  - "JackTransportTests (absent no-op + drive-tempo seam + bad-tempo reject + non-JACK unaffected + gate error)"
  - "40-HUMAN-UAT.md (real synth / DAW master+slave / alignment / JACK / Link-deferred rows)"
  - "40-VERIFICATION.md (9-ID closure trace + D-40-NN trace + threat-mitigation table)"
affects:
  - "flow-lang.csproj (JackHandleType Compile-Remove + jack.flow None-Remove; JackSharp spiked + rejected — no PackageReference)"
  - "ExecutionContext / TestSnapshot (JackEnabled gate)"
  - "FlowEngine (JackFunctions.Register), ModuleLoader (@jack Web advisory)"
  - "TypeParser + Parser (JackHandle type-name)"
  - "flow-lang.Tests.csproj (Web strip-list — ClockMaster/ClockSlave/JackTransport/LinkDeferral)"
  - "ROADMAP / STATE / REQUIREMENTS / CLAUDE.md (Phase 40 closer sweep)"
tech-stack:
  added: []
  removed: ["JackSharp 0.4.0 (spiked under net10 — loads but exposes no transport API; rejected, hand-rolled P/Invoke instead)"]
  patterns: ["hand-rolled [DllImport(\"jack\")] jack_transport_query + jack_position_t ABI struct mirror", "charitable absent-server no-op (never throws)", "ref-identity handle (OscHandle model)", "#if !FLOW_WEB guard + Compile Remove", "license-gate honest defer (no GPL ref, structural test enforcement)"]
key-files:
  created:
    - flow-lang/StandardLibrary/Midi/JackFunctions.cs
    - flow-lang/StandardLibrary/Midi/JackHandleData.cs
    - flow-lang/TypeSystem/SpecialTypes/JackHandleType.cs
    - flow-lang/jack.flow
    - flow-lang.Tests/Integration/Phase40/JackTransportTests.cs
    - flow-lang.Tests/Integration/Phase40/LinkDeferralTests.cs
    - .planning/phases/40-studio-sync/40-HUMAN-UAT.md
    - .planning/phases/40-studio-sync/40-VERIFICATION.md
  modified:
    - flow-lang/flow-lang.csproj
    - flow-lang/Runtime/Value.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Runtime/ModuleLoader.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/Parsing/TypeParser.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs
    - flow-lang.Tests/flow-lang.Tests.csproj
    - .planning/ROADMAP.md
    - .planning/STATE.md
    - .planning/REQUIREMENTS.md
    - CLAUDE.md
decisions:
  - "JACK verdict (Open Q3): JackSharp 0.4.0 loads under net10 via net4x shim but exposes NO transport API → JACK-01 ships via hand-rolled jack_transport_query P/Invoke; JackSharp PackageReference removed (dead dep)"
  - "JackHandleType specificity 154 (above ClockHandle=153, MidiDevice=152, OscHandle=151) per D-40-03 discretion"
  - "@jack is a SEPARATE fine-grained opt-in from @midi (D-40-04) — JackEnabled gate parallel to MidiEnabled"
  - "Link deferred honestly = NOT registering a builtin + documenting + LinkDeferralTests structural assertion (no community stub needed — simpler honest option)"
  - "Closed a Plan-40-02 Web test-build gap: ClockMasterTests/ClockSlaveTests reference stripped Midi types but were not in the test-csproj Web strip-list"
metrics:
  tasks_completed: 3
  files_created: 8
  files_modified: 13
  phase40_tests: "32/32 green (25 prior + 5 JackTransport + 2 LinkDeferral)"
  duration: "~1 session"
  completed: "2026-06-07"
---

# Phase 40 Plan 03: JACK Transport Best-Effort + Ableton Link Deferral + Phase Closer Summary

Closes Phase 40: ships **JACK transport sync best-effort** (`jackSync` via a hand-rolled `jack_transport_query` P/Invoke after JackSharp 0.4.0 proved to have no transport API), records the **Ableton Link deferral honestly** (LINK-01 deferred per D-40-06 GPL — no implementation, structurally asserted) while shipping **LINK-02 determinism**, records the **MIDI-RT-03 deferral** to Phase 41, and authors the phase closer (`40-HUMAN-UAT.md` + `40-VERIFICATION.md` + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep). The honest shape: Phase 40 ships the MIDI+clock spine machine-proven; Link deferred; JACK best-effort; hardware behaviors pending HUMAN-UAT.

## JACK Verdict (Open Q3 — RESOLVED)

**JackSharp 0.4.0 was spiked under net10 and REJECTED for JACK-01. JACK-01 ships via a hand-rolled `[DllImport("jack")] jack_transport_query`.**

The Task-1 spike added `<PackageReference Include="JackSharp" Version="0.4.0" />` and built Desktop: it **restores + loads** via the net4x compat shim (NU1701, identical posture to the already-shipping Rug.Osc). BUT a reflective probe of the JackSharp assembly's exported types confirmed it exposes **NO transport surface** — no `jack_transport_query`, no tempo/BPM/BBT/position/bar member anywhere. JackSharp wraps audio/MIDI ports + connection control (`Client`/`Controller`/`Processor`/`Ports.*`/`Processing.*`), not transport. It therefore cannot satisfy JACK-01 (which needs transport state + BPM).

Per the plan's documented fallback (D-40-05 best-effort latitude), JACK-01 ships via a **minimal hand-rolled libjack P/Invoke**: `jack_client_open` / `jack_client_close` / `jack_transport_query` + a sequential-layout `jack_position_t` struct mirrored field-for-field from `jack/transport.h`. The `JackSharp` PackageReference was **removed** (a dead dependency since we don't use its API), but the `JackSharp` `AssemblyReferenceScanTests` forbidden-prefix entry (added by Plan 40-01) STAYS as a standing guard so a future `@jack` file cannot leak a JackSharp ref into the Web closure.

## What Shipped

### Task 1 — JACK best-effort + jackSync builtin (commit `5918762`)
- **JackSharp net10 spike + verdict** (above): hand-rolled `jack_transport_query` chosen.
- `JackFunctions.cs` (`#if !FLOW_WEB`): `jackSync()` queries JACK transport via the P/Invoke, drives `MusicalContext.Tempo` + bar/beat **only when the BBT-valid bit is set AND the BPM passes `IsValidTempo`** (T-40-01 — out-of-range rejected, not written). **Charitable absent-server (JACK-01 / T-40-04):** no server / `libjack.so.0` absent / any native surprise → `WarnOnce("jack-absent", ...)` + dead handle, **NEVER throws**; the `QueryTransport` body catches `DllNotFoundException` + every `Exception` and degrades to "no server present". `TransportQueryOverride` test seam injects a synthetic snapshot so CI exercises both branches with no real JACK.
- `JackHandleType` (ref-identity sealed singleton, specificity **154**), `JackHandleData` (`required`-init snapshot: `ServerPresent` + `Tempo?` + `Bar?` + `Beat?`), `Value.JackHandle` factory — all `#if !FLOW_WEB` + Compile-Removed on Web.
- **@jack 3-site opt-in gate (D-40-04, parallel to @midi):** `ExecutionContext.JackEnabled` (+ snapshot :10e + restore :10e + `TestSnapshot.JackEnabled`); `FlowEngine` `JackFunctions.Register(...)` in the `#if !FLOW_WEB` block; `ModuleLoader.IsStrippedOnWeb |= @jack`. `jack.flow` module (`module jack`, `internal proc jackSync ()`, trailing `(__enableJackModule)`). `JackHandle` type-name in `TypeParser` (both arms) + `Parser.IsTypeKeyword`.
- Web-strip csproj: `<Compile Remove="TypeSystem\SpecialTypes\JackHandleType.cs" />` + `<None Remove="jack.flow" />` (JackFunctions/JackHandleData covered by the `StandardLibrary\Midi\**\*.cs` wildcard) + the Desktop `<None Update="jack.flow">` copy.
- `JackTransportTests` (5 Facts): `JackAbsentServerNoOp` (charitable no-op, never throws, Tempo untouched — JACK-01 headline), `JackPresentServerDrivesTempo_ViaSeam`, `JackInvalidTransportTempo_Rejected` (T-40-01), `NonJackWorkflowUnaffected`, `JackSyncWithoutModuleImport_GatedError`.

### Task 2 — Ableton Link deferral record + LINK-02 + MIDI-RT-03 (commit `9729532`)
- **LINK-01 deferred honestly per D-40-06** (GPLv2+, HIGH threat T-40-02): NO Link implementation ships — no `@link` module, no `linkEnable`/`linkDisable`, no `libabl_link` reference. Chose the simpler honest option (NOT registering + documenting, no community stub).
- `LinkDeferralTests` (2 Facts): `LinkDeferral_NoGplReference` (asserts the flow-lang assembly graph carries no `abl_link`/`ableton`/`link`/`libabl` referenced-assembly + no `AbletonLink`/`AblLink`/`LinkEnable` type + no `link.flow` on disk) + `OfflineRenderIgnoresSync_LinkDeferred` (LINK-02 reinforcement — offline render byte-identical across runs; with Link shipping nothing, the strong form holds).
- **MIDI-RT-03 (CoreMIDI/WinMM) recorded as deferred to Phase 41** — the same `IMidiBackend` abstraction covers it later; no Phase 40 work (VERIFICATION note).

### Task 3 — Phase closer (commit — this metadata commit)
- `40-HUMAN-UAT.md`: 7 rows (real synth note-on / low-level events / DAW master / DAW slave / MIDI-audio alignment / JACK / Link-deferred) modeling the Phase 48/49 HUMAN-UAT format, honest machine-vs-human split (D-40-07), with the `librtmidi-dev` + `snd-virmidi` + `jackd` setup notes.
- `40-VERIFICATION.md`: per-REQ closure trace for ALL 9 IDs (MIDI-RT-01/02/04 + CLOCK-01/02 + LINK-02 closed; MIDI-RT-03 + LINK-01 deferred-with-rationale; JACK-01 closed best-effort), each mapped to its test + status + D-40-NN trace + honest caveats + threat-mitigation confirmation table.
- Tracking sweep: ROADMAP (Phase 40 checklist line → done; 40-03 plan checkbox; "sample-accurate" → "best-effort ms-aligned" correction in success criteria #2; LINK-01 defer + LINK-02 closed in criterion #4); STATE (Current Position → execution-complete-pending-HUMAN-UAT; Phase 40 highlights entry; progress 90→93 plans); REQUIREMENTS (status table + per-REQ rows: JACK-01 + LINK-02 → complete, MIDI-RT-03 + LINK-01 → deferred); CLAUDE.md (new "Studio Sync (Phase 40)" section documenting @midi/@jack surface + Link defer + best-effort JACK + librtmidi.so prerequisite + Web-strip).

## Verification

| Gate | Result |
|------|--------|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | exit 0 |
| `dotnet build flow-lang -p:FlowTarget=Web` | exit 0 (JackHandleType + JackFunctions/JackHandleData + jack.flow stripped) |
| `dotnet test --filter JackTransport` | 5/5 green (absent no-op + drive-tempo seam + bad-tempo reject + non-JACK unaffected + gate error) |
| `dotnet test --filter "LinkDeferral|OfflineRender"` | 4/4 green (LINK-01 defer record + LINK-02 determinism) |
| `dotnet test --filter Phase40` | 32/32 green (25 prior + 5 JackTransport + 2 LinkDeferral) |
| `dotnet test -p:FlowTarget=Web --filter AssemblyReferenceScan` | 2/2 green (RtMidi.Core + JackSharp absent from Web dll — T-40-02/03) |
| 40-HUMAN-UAT.md + 40-VERIFICATION.md exist + cover all 9 IDs | ✓ (grep MIDI-RT-01 + LINK-01 pass) |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - test infra] JackTransportTests cross-class Console-redirection race**
- **Found during:** Task 1, when running the full Phase40 suite (passed in isolation, flaked 2 Facts in the full suite — `JackAbsentServerNoOp` + `NonJackWorkflowUnaffected` saw empty stdout).
- **Issue:** `JackTransportTests` drives a `FlowEngineRunner` that redirects process-wide `Console.Out`/`Error`; running it parallel to the WASM/console tests raced the redirect (same root cause Plan 40-01 documented for `VirtualMidiTests`).
- **Fix:** `[Collection(WasmEntryConsoleCollection.Name)]` on `JackTransportTests` (LinkDeferralTests already carried it). Phase40 suite 32/32 green.
- **Commit:** `4d47631`.

**2. [Rule 3 - blocking] Plan-40-02 Web test-build gap (ClockMaster/ClockSlave Compile-Remove)**
- **Found during:** Task 1, running `AssemblyReferenceScan` under `-p:FlowTarget=Web` (Task 1 acceptance criterion) — the test project failed to compile because `ClockMasterTests`/`ClockSlaveTests` reference `FlowLang.StandardLibrary.Midi` (stripped on Web) but were never added to the test-csproj Web strip-list at Plan 40-02.
- **Fix:** added `ClockMasterTests.cs` + `ClockSlaveTests.cs` (Plan-40-02 gap) + `JackTransportTests.cs` + `LinkDeferralTests.cs` (this plan) to the `'$(FlowTarget)' == 'Web'` `<Compile Remove>` group. Web test build green; AssemblyReferenceScan runnable.
- **Commit:** `5918762`.

**3. [Plan deviation — JACK implementation path] hand-rolled P/Invoke instead of JackSharp**
- This is the plan's own documented fallback (Task 1 action + D-40-05), not a silent deviation. The JackSharp net10 spike (Open Q3) confirmed JackSharp 0.4.0 loads but has no transport API, so per the fallback JACK-01 ships via a hand-rolled `jack_transport_query`. The `JackSharp` PackageReference was removed (dead dep); the forbidden-prefix guard stays. Recorded in the SUMMARY + 40-VERIFICATION.md as the verdict.

## Honest Scope Notes

- **JACK is best-effort (D-40-05) with an unverified ABI struct against a live server.** `jack_position_t` is mirrored field-for-field from `jack/transport.h` and exercised by the `TransportQueryOverride` seam in CI, but not yet against a running JACK timebase master (HUMAN-UAT Row 6). The absent-server no-op — the load-bearing "JACK absence never affects non-JACK workflows" guarantee — IS machine-proven.
- **MIDI-RT-04 alignment is best-effort ms, NOT sample-accurate** (carried from Plan 01; ROADMAP success-criterion wording corrected this plan).
- **Link deferred (GPL) — nothing ships.** Structural enforcement = no `libabl_link` ref anywhere (`LinkDeferralTests` + `AssemblyReferenceScanTests`).
- **`librtmidi.so` + a JACK server are native runtime prerequisites absent on this dev box**, so real-ALSA / real-JACK end-to-end paths charitable-skip in CI; the in-process seams prove the byte/timing/transport logic.

## Self-Check: PASSED
- All 8 created files exist on disk (verified below).
- All 3 task/fix commits (`5918762`, `9729532`, `4d47631`) exist in git history.
- Phase40 suite 32/32 green; Desktop + Web builds exit 0; AssemblyReferenceScan (Web) green; 40-HUMAN-UAT.md + 40-VERIFICATION.md exist + cover all 9 requirement IDs.
