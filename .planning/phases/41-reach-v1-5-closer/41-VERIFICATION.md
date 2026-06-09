---
phase: 41-reach-v1-5-closer
verified: 2026-06-07T01:30:00Z
status: human_needed
score: 7/7 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Windows WASAPI audible stereo playback"
    expected: "Running `flow run examples/edm/pulse.flow` on Windows produces audible stereo output through the default WASAPI device (shared-mode). No crash, no silence."
    why_human: "WasapiBackend.IsAvailable() returns false on Linux; the code compiles and the probe-gate is verified, but only real Windows hardware can confirm audible output."
  - test: "macOS CoreAudio audible playback + <20 ms round-trip latency"
    expected: "A live-coding session (`flow watch`) on macOS produces audible output; round-trip latency feels <20 ms. If >20 ms, escalate to OwnAudioSharp 1.0.68 swap (D-18)."
    why_human: "CoreAudioBackend.IsAvailable() returns false on Linux. Audible verification and latency measurement require real macOS hardware."
  - test: "osx-x64 binary execution smoke"
    expected: "`flow-osx-x64-v1.5.0.tar.gz` .sha256 verifies OK; untar + `flow version` exits 0; `flow run examples/edm/pulse.flow` renders on an Intel Mac."
    why_human: "Cross-compiled from Linux; the resulting binary cannot be executed on this host."
  - test: "osx-arm64 binary execution smoke"
    expected: "`flow-osx-arm64-v1.5.0.tar.gz` .sha256 verifies OK; untar + `flow version` exits 0; a render runs on Apple Silicon."
    why_human: "Cross-compiled from Linux; aarch64 execution requires real Apple Silicon hardware."
  - test: "win-x64 binary execution smoke"
    expected: "`flow-win-x64-v1.5.0.zip` .sha256 verifies OK; unzip + `flow version` exits 0; a render runs on Windows."
    why_human: "Cross-compiled from Linux; Windows execution requires real Windows hardware."
  - test: "JetBrains Marketplace publish"
    expected: "`./gradlew signPlugin publishPlugin` succeeds with CERTIFICATE_CHAIN / PRIVATE_KEY / PRIVATE_KEY_PASSWORD / PUBLISH_TOKEN supplied as env vars; plugin appears on JetBrains Marketplace or direct-download fallback (docs/jetbrains/install.html) is confirmed."
    why_human: "Requires composer's JetBrains account + signing certificate. Secrets are env-var-only; none are committed. buildPlugin + verifyPlugin (Compatible vs IC-2024.2) are autonomously green; the upload is the human action."
  - test: "v1.5.0 GitHub Release cut"
    expected: "Composer verifies every .sha256, attaches the 5 binary archives + showcase WAV/MIDI to a GitHub Release tagged v1.5.0."
    why_human: "Outward-facing publish is a human gate per D-04. All artifacts are staged by scripts/publish.sh; the Release itself must not be cut autonomously."
---

# Phase 41: Reach + v1.5 Closer — Verification Report

**Phase Goal:** Ship the v1.5 "reach" surface — `flow doc` documentation generator (DOC-01/02), cross-platform self-contained binaries for 5 RIDs (BIN-01), Windows WASAPI (WASAPI-01) + macOS CoreAudio (COREAUDIO-01) audio backends, JetBrains Marketplace publish prep (JET-01), and a third-genre showcase piece (SHOWCASE-01) — while preserving offline-render determinism.
**Verified:** 2026-06-07T01:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `flow doc` verb exists in flow-cli, generates HTML + Markdown from `///` and BuiltInDocs, with content-hash cache | VERIFIED | `flow-cli/Commands/DocCommand.cs` registered in `CommandRegistry.cs` (14 subcommands). `flow-cli/Doc/` contains DocCollector, DocGenerator, HtmlEmitter, MarkdownEmitter, ContentHashCache. Smoke: `flow doc --out /tmp/flowdoc-smoke` → `index.html` + `reference.md` + `.flowdoc-cache.json`; second run reports `0 regenerated, 647 unchanged`. |
| 2 | DOC-02: `///` examples execute via FlowEngine; failures annotated `[example failed]` | VERIFIED | `DocExampleRunner.RunOne` spawns a per-example fresh `FlowEngine`, checks `ErrorReporter`, annotates failures inline. `DocExampleExecTests` 5/5 GREEN. CR-03 fix confirmed: Console redirect/restore on the calling thread (commit `27fc8cf`). |
| 3 | `WasapiBackend.cs` exists, implements `IAudioBackend`, Desktop-only, Web-stripped three ways, `IsAvailable()` returns false on Linux without crashing | VERIFIED | File exists at `flow-lang/Audio/WasapiBackend.cs`. `#if !FLOW_WEB` guard confirmed. `<Compile Remove>` in `flow-lang.csproj` under `FlowTarget=Web` confirmed. NAudio.Wasapi 2.3.0 Desktop-only `PackageReference` confirmed. `"NAudio"` in `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` (same commit). `WasapiBackendAvailabilityTests` LIVE + GREEN (26/26 total Phase41). Web build: 0 errors. |
| 4 | `CoreAudioBackend.cs` exists, compiles, `IsAvailable()` returns false on Linux | VERIFIED | File unmodified per D-18 (verify-not-build). `CoreAudioBackendAvailabilityTests` is LIVE and counted in the 26/26 Phase41 pass. Desktop build: 0 errors. |
| 5 | `scripts/publish.sh` produces 5-RID self-contained single-file archives with `.sha256` sidecars, no `PublishTrimmed=true` | VERIFIED | Script confirmed at `scripts/publish.sh`. 5 archives + 5 .sha256 sidecars produced per SUMMARY. `PublishTrimmed=false` on every RID (D-15). linux-x64 `flow version` smoke passes natively. WR-04 fix: patterns/generative/improv.flow added to STDLIB_FILES gate (commit `135a5da`). WR-05 fix: SIGINT/SIGTERM trap for in-flight archives (commit `2ebb334`). |
| 6 | `flow-jetbrains/build.gradle.kts` has signing/publishing/pluginVerification blocks with env-var-only secrets; `CHANGELOG.md` and `docs/jetbrains/install.html` exist; `until-build="253.*"` valid | VERIFIED | `build.gradle.kts`: all four secrets via `providers.environmentVariable(...)` only (no committed literals). `CHANGELOG.md` tracked (`.gitignore` allow-list fix committed). `docs/jetbrains/install.html` exists. Plugin version `1.5.0` in both gradle + plugin.xml. `until-build="253.*"` (not empty — Phase-31 defect fixed). `buildPlugin` BUILD SUCCESSFUL + `verifyPlugin` Compatible vs IC-2024.2 per SUMMARY. |
| 7 | `examples/edm/pulse.flow` exists, exercises all five Phase 41 headline primitives (match/euclidean/granular/live/midiOut), two-run cmp-clean on writeWav/writeMidi, RMS baseline committed + test GREEN | VERIFIED | File exists. Primitive checklist confirmed: `(match idx ...)` bassline selection (Phase 35), seeded `(euclidean 7 16 ... 1305)` + `(euclidean 4 16 ... 808)` (Phase 36), `(granular swell 60ms 18Hz 0.4)` (Phase 37), `live 1bar { }` + `midiOut` in commented demo section (clean render/demo split per Pitfall 5 / D-v1.5-07). `flow-lang.Tests/baselines/Phase41/showcase.wav` committed. `Phase41ShowcaseRmsTests` LIVE + GREEN (26/26). WAV SHA `a2c095c4…` + MIDI SHA `1ad0b7f9…` two-run cmp-clean per SUMMARY. |

**Score:** 7/7 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lang/Audio/WasapiBackend.cs` | Windows WASAPI IAudioBackend | VERIFIED | Exists; `#if !FLOW_WEB`; NAudio.Wasapi 2.3.0 pull→push bridge |
| `flow-lang/Audio/CoreAudioBackend.cs` | macOS CoreAudio IAudioBackend | VERIFIED | Exists (pre-Phase-41, unmodified per D-18); compiles; IsAvailable false on Linux |
| `flow-cli/Commands/DocCommand.cs` | `flow doc` CLI verb | VERIFIED | Exists; registered in CommandRegistry (14 subcommands) |
| `flow-cli/Doc/DocGenerator.cs` | End-to-end doc pipeline | VERIFIED | Exists; wires Collector → ExampleRunner → Cache → Emitters |
| `flow-cli/Doc/DocCollector.cs` | Content harvester | VERIFIED | Exists; reads BuiltInDocs.All + ProcDeclaration.DocComment |
| `flow-cli/Doc/DocExampleRunner.cs` | In-process example execution | VERIFIED | Exists; CR-03 fix applied (calling-thread redirect/restore) |
| `flow-cli/Doc/HtmlEmitter.cs` | Browsable static HTML | VERIFIED | Exists; no `<script>` tags; prefers-color-scheme |
| `flow-cli/Doc/MarkdownEmitter.cs` | Greppable Markdown reference | VERIFIED | Exists; WR-03 fix applied (EscFenced for code blocks) |
| `flow-cli/Doc/ContentHashCache.cs` | SHA256 incremental cache | VERIFIED | Exists; WR-02 fix applied (atomic tempfile + rename write) |
| `flow-lang/Ast/Statements/ProcDeclaration.cs` | `string? DocComment = null` field | VERIFIED | Field confirmed at line 87 |
| `flow-lang/Lexing/SimpleLexer.cs` | `///` before `//` arm | VERIFIED | `PendingDocComment` accessor + `TokenType.DocComment` emit confirmed |
| `flow-lang/StandardLibrary/BuiltInDocs.cs` | `public static All` accessor | VERIFIED | `IReadOnlyDictionary<string, Doc> All => _docs;` at line 209 |
| `scripts/publish.sh` | 5-RID self-contained publish | VERIFIED | Exists; SIGINT/SIGTERM trap + patterns/generative/improv.flow in STDLIB_FILES |
| `flow-jetbrains/build.gradle.kts` | Signing/publishing DSL | VERIFIED | Env-var-only secrets; `until-build="253.*"`; version 1.5.0 |
| `flow-jetbrains/CHANGELOG.md` | Keep-a-Changelog `[1.5.0]` | VERIFIED | Exists and tracked (`.gitignore` allow-list fix) |
| `docs/jetbrains/install.html` | Direct-download fallback | VERIFIED | Exists; self-contained static HTML |
| `examples/edm/pulse.flow` | ~60s EDM showcase | VERIFIED | Exists; all 5 primitives present; render/demo split |
| `flow-lang.Tests/baselines/Phase41/showcase.wav` | RMS regression baseline | VERIFIED | Exists; committed; test GREEN |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `flow-lang/Lexing/SimpleLexer.cs` | `flow-lang/Parsing/Parser.cs` | `TokenType.DocComment` token buffered in `_pendingDocComment` | WIRED | `_pendingDocComment` field at Parser.cs:42; DocComment token consumed at line 118; consumed + threaded at `ParseProcDeclaration` entry (lines 373-374). CR-01 fix: plain-comment arm clears buffer (commit `fd245a8`). |
| `flow-lang/Parsing/Parser.cs` | `flow-lang/Ast/Statements/ProcDeclaration.cs` | `DocComment: docComment` at record construction | WIRED | Line 462: `DocComment: docComment` passed to record constructor. |
| `flow-lang/StandardLibrary/BuiltInDocs.cs` | `flow-cli/Doc/DocCollector.cs` | `BuiltInDocs.All` read directly | WIRED | `foreach (var kvp in BuiltInDocs.All)` at DocCollector.cs:43. |
| `flow-cli/Doc/DocCollector.cs` | `flow-cli/Doc/DocGenerator.cs` | `Collect()` called in pipeline | WIRED | `collector.Collect(sources)` at DocGenerator.cs:95. |
| `flow-cli/Doc/DocGenerator.cs` | `flow-cli/Commands/DocCommand.cs` | `DocGenerator.Generate(...)` called in SetAction | WIRED | DocCommand.cs thin wrapper over `DocGenerator.Generate`. |
| `flow-cli/Commands/DocCommand.cs` | `flow-cli/Commands/CommandRegistry.cs` | `DocCommand.Build()` in subcommand list | WIRED | `DocCommand.Build()` at CommandRegistry.cs:36. |
| `flow-lang/Audio/WasapiBackend.cs` | `flow-lang/Audio/AudioPlaybackManager.cs` | `WasapiBackend.IsAvailable()` + `DetectBackend` Windows branch | WIRED | `WasapiBackend.IsAvailable()` called at AudioPlaybackManager.cs:87 + 183-184 inside `#if !FLOW_WEB` block. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `HtmlEmitter.cs` | `DocModel[] models` | `DocCollector.Collect()` reading `BuiltInDocs.All` (~104 entries) + harvested `ProcDeclaration.DocComment` | Yes — BuiltInDocs._docs populated at startup with real metadata | FLOWING |
| `examples/edm/pulse.flow` (writeWav) | `finalMix` (AudioBuffer) | `renderSong song "saw"` driven by `euclidean`/`granular`/`match` results | Yes — seeded, no hardcoded empty returns | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Phase41 test suite GREEN | `dotnet test flow-lang.Tests --filter "Category=Phase41" --no-build` | 26 passed / 0 failed / 0 skipped | PASS |
| Desktop build | `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop` | 0 errors | PASS |
| Web build (no NAudio in WASM) | `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` | 0 errors | PASS |
| AssemblyReferenceScanTests (NAudio forbidden) | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~AssemblyReferenceScanTests" --no-build` | 2 passed / 0 failed | PASS |
| WasapiBackend.IsAvailable false on Linux | Included in Phase41 26/26 | VERIFIED via test class (Category=Phase41 run) | PASS |

### Probe Execution

Step 7c: No `probe-*.sh` scripts declared in Phase 41 plans. SKIPPED (no phase-declared probes).

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| DOC-01 | 41-02, 41-03 | `///` doc-comment grammar + `flow doc` verb (HTML+Markdown, content-hash cache) | SATISFIED | SimpleLexer `///` arm, ProcDeclaration.DocComment, BuiltInDocs.All, DocCommand + DocGenerator pipeline; 12/12 DOC tests GREEN |
| DOC-02 | 41-03 | `///` examples execute via FlowEngine; `[example failed]` annotation | SATISFIED | DocExampleRunner in-process execution; 5/5 DocExampleExecTests GREEN |
| WASAPI-01 | 41-04 | Windows WASAPI IAudioBackend via NAudio.Wasapi 2.3.0 | SATISFIED (code half) | WasapiBackend.cs exists, wired, Web-stripped, NAudio forbidden-gate in place. Audible Windows verification is HUMAN-UAT row 1. |
| COREAUDIO-01 | 41-04 | macOS CoreAudio IAudioBackend (hand-rolled, verify-not-build per D-18) | SATISFIED (code half) | CoreAudioBackend.cs present, compiles, probe returns false on Linux. Audible macOS verification is HUMAN-UAT row 2. |
| BIN-01 | 41-05 | 5-RID self-contained binaries + .sha256 | SATISFIED (linux-x64 smoke proven; osx/win exec is HUMAN-UAT) | scripts/publish.sh produces all 5 archives; linux-x64 `flow version` smoke passes; no trimming; SIGINT trap + stdlib gate fixed. |
| JET-01 | 41-06 | JetBrains Marketplace publish prep | SATISFIED (artifacts + buildPlugin/verifyPlugin; upload is HUMAN-UAT) | build.gradle.kts signing/publishing DSL (env-var-only), CHANGELOG.md, install.html, until-build="253.*" fix; buildPlugin green + verifyPlugin Compatible vs IC-2024.2. |
| SHOWCASE-01 | 41-07 | Third-genre showcase (~60s EDM) exercising all 5 Phase 35-40 headline primitives | SATISFIED | examples/edm/pulse.flow; 5 primitives confirmed; two-run cmp-clean; RMS baseline + 26/26 GREEN |

All 7 required IDs accounted for.

**Note — MIDI-RT-03 (macOS CoreMIDI + Windows WinMM):** REQUIREMENTS.md traceability row records this as "Deferred → Phase 41" from Phase 40. It is NOT in the Phase 41 phase-goal requirement IDs (WASAPI-01/COREAUDIO-01/BIN-01/DOC-01/DOC-02/JET-01/SHOWCASE-01) per 41-CONTEXT.md scope and the verifier brief. The cross-platform binary work (BIN-01) delivers the `IMidiBackend` abstraction already in place; CoreMIDI/WinMM backend implementations remain open. This is an acknowledged carry-forward tracked in the traceability table, not a Phase 41 gap.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `flow-lang/Lexing/SimpleLexer.cs` | 1222, 1347-1348 | Strings containing "FIXME:" / "TODO:" | ℹ️ Info | These are Flow language comments describing what the lexer parses (e.g., `FIXME:` as a doc-comment orphan example), not code debt markers. Not a blocker. |

No `TBD`, `FIXME`, or `XXX` debt markers found in Phase 41 modified production files. All 9 code-review issues (CR-01..03 + WR-01..06) have dedicated `fix(41):` commits and are verified present in the codebase.

### Human Verification Required

#### 1. Windows WASAPI Audible Playback

**Test:** On a Windows machine, download `flow-win-x64-v1.5.0.zip`, verify .sha256, unzip, run `flow run examples/edm/pulse.flow`.
**Expected:** Audible stereo output through the default WASAPI device (shared-mode). No crash, no silence.
**Why human:** WasapiBackend.IsAvailable() returns false on Linux. Code compiles and probe-gate is verified. Only real Windows hardware confirms audible output.

#### 2. macOS CoreAudio Audible Playback + Latency

**Test:** On a macOS machine, download the osx binary, start a live-coding session (`flow watch examples/edm/pulse.flow`); confirm audible output AND that round-trip latency feels <20 ms.
**Expected:** Audible output; <20 ms latency. If latency >20 ms, escalate to OwnAudioSharp 1.0.68 swap (D-18) — RESEARCH has scoped it; do NOT swap speculatively.
**Why human:** CoreAudioBackend.cs has never been verified on real Mac hardware. latency cannot be measured in a headless Linux environment.

#### 3. osx-x64 Binary Execution Smoke

**Test:** On an Intel Mac, verify `flow-osx-x64-v1.5.0.tar.gz` .sha256, untar, run `flow version` + `flow run examples/edm/pulse.flow`.
**Expected:** `flow version` exits 0; render completes without errors.
**Why human:** Cross-compiled from Linux; cannot be executed on this host.

#### 4. osx-arm64 Binary Execution Smoke

**Test:** On Apple Silicon, verify `flow-osx-arm64-v1.5.0.tar.gz` .sha256, untar, run `flow version` + a render.
**Expected:** `flow version` exits 0; render completes without errors.
**Why human:** Cross-compiled from Linux; Apple Silicon not available here.

#### 5. win-x64 Binary Execution Smoke

**Test:** On Windows, verify `flow-win-x64-v1.5.0.zip` .sha256, unzip, run `flow version` + a render.
**Expected:** `flow version` exits 0; render completes.
**Why human:** Cross-compiled from Linux; Windows execution not available here.

#### 6. JetBrains Marketplace Publish

**Test:** From `flow-jetbrains/`, run `./gradlew signPlugin publishPlugin` with env vars `CERTIFICATE_CHAIN` / `PRIVATE_KEY` / `PRIVATE_KEY_PASSWORD` / `PUBLISH_TOKEN` (composer's account only — never committed). Confirm plugin appears on Marketplace or direct-download fallback (`docs/jetbrains/install.html`) is used.
**Expected:** Plugin published; `flow-jetbrains-1.5.0.zip` visible on Marketplace.
**Why human:** Requires composer's JetBrains account + signing certificate. `buildPlugin` + `verifyPlugin` (Compatible vs IC-2024.2) are autonomously green; the upload is the human action per D-03.

#### 7. v1.5.0 GitHub Release Cut

**Test:** Composer verifies every `.sha256`, attaches the 5 binary archives + showcase WAV/MIDI, and pushes a GitHub Release tagged `v1.5.0`.
**Expected:** Release live on GitHub with all artifacts attached and checksums verified.
**Why human:** Outward-facing publish is a human gate per D-04. All artifacts staged; Release must not be cut autonomously.

---

### Gaps Summary

No automated gaps found. All 7 required must-haves are VERIFIED in the codebase. The phase goal is achieved in code. The remaining work is exclusively the 7 human-gated items above (real-hardware audio verification, binary execution smoke on macOS/Windows, JetBrains Marketplace upload, and the v1.5.0 GitHub Release cut) — all of which are explicitly documented in `41-HUMAN-UAT.md` per D-02/D-03/D-04/D-05 and the `feedback_autonomous_phase_execution` principle.

---

_Verified: 2026-06-07T01:30:00Z_
_Verifier: Claude (gsd-verifier)_
