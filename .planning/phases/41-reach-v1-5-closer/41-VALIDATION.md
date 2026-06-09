---
phase: 41
slug: reach-v1-5-closer
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-07
---

# Phase 41 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `41-RESEARCH.md` §Validation Architecture + §Security Domain.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (C# unit/integration in `flow-lang.Tests/`) + the in-language Flow test framework (`flow test`, Phase 35) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); `tests/test_*.flow` (Flow scripts) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase41"` |
| **Full suite command** | `dotnet test` + `for t in tests/test_*.flow; do dotnet run --project flow-cli -- test "$t"; done` |
| **Estimated runtime** | ~30 s (quick subset) / several min (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase41"` (< 30 s)
- **After every plan wave:** Run `dotnet test` (full xUnit) + the Flow `test_*.flow` loop
- **Before `/gsd:verify-work`:** Full suite green + `bash scripts/test_two_run_determinism.sh` on the showcase + `dotnet build flow-lang -p:FlowTarget=Web` exit 0
- **Max feedback latency:** 30 s

---

## Per-Task Verification Map

> Task IDs are provisional (planner assigns final plan/wave). Requirement→test rows are the load-bearing contract.

| Req | Behavior | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|-----|----------|------------|-----------------|-----------|-------------------|-------------|--------|
| DOC-01 | `///` lexes as doc-comment; `//` + `/* */` unchanged | — | charitable lexer (errors accumulate) | unit | `dotnet test --filter "FullyQualifiedName~DocCommentLexTests"` | ❌ W0 | ⬜ pending |
| DOC-01 | `///` binds to following ProcDeclaration; orphan dropped charitably | — | N/A | unit | `dotnet test --filter "FullyQualifiedName~DocCommentBindTests"` | ❌ W0 | ⬜ pending |
| DOC-01 | `flow doc` emits HTML + Markdown with BuiltInDocs entries | T-41 path-traversal | normalize `--out`, default `docs/reference` | integration | `dotnet test --filter "FullyQualifiedName~FlowDocGenTests"` | ❌ W0 | ⬜ pending |
| DOC-01 | content-hash cache skips unchanged entries; edited entry regens | — | N/A | integration | `dotnet test --filter "FullyQualifiedName~DocCacheTests"` | ❌ W0 | ⬜ pending |
| DOC-02 | passing `///` example → no `[example failed]`; failing one → annotated | T-41 doc-example DoS | reuse hermetic `TestRunner` state reset | integration | `dotnet test --filter "FullyQualifiedName~DocExampleExecTests"` | ❌ W0 | ⬜ pending |
| BIN-01 | `publish.sh` produces 5 RID archives + `.sha256` | T-41 tampered binary | ship `.sha256` per artifact | smoke | `bash scripts/publish.sh && ls publish/flow-*-v1.5.0.*` | ❌ W0 (extend) | ⬜ pending |
| BIN-01 | linux-x64 + linux-arm64 binary runs `flow version` | — | N/A | smoke | `publish/flow-linux-x64/flow version` (arm64: qemu or skip-with-reason) | ❌ W0 | ⬜ pending |
| WASAPI-01 | `WasapiBackend` compiles Desktop; Web build stays green | T-41 web-drift | `#if !FLOW_WEB` + Compile-Remove | build | `dotnet build flow-lang -p:FlowTarget=Web` (exit 0) | gate exists | ⬜ pending |
| WASAPI-01 | NAudio not in Web closure | T-41 web-drift | forbidden-prefix `"NAudio"` | invariant | `dotnet test --filter "AssemblyReferenceScanTests"` | extend | ⬜ pending |
| WASAPI-01 | `IsAvailable()` false on Linux (no crash) | — | probe-gated | unit | `dotnet test --filter "WasapiBackendAvailabilityTests"` | ❌ W0 | ⬜ pending |
| WASAPI-01 | Windows audible playback | — | N/A | manual | HUMAN-UAT (D-05) | human | ⬜ pending |
| COREAUDIO-01 | `CoreAudioBackend.IsAvailable()` false on Linux; compiles clean | — | probe-gated | unit | `dotnet test --filter "CoreAudioBackendAvailabilityTests"` | ❌ W0 (confirm dup) | ⬜ pending |
| COREAUDIO-01 | macOS audible playback + <20 ms latency | — | N/A | manual | HUMAN-UAT (D-05) | human | ⬜ pending |
| JET-01 | `./gradlew buildPlugin` + `verifyPlugin` succeed | T-41 secret leak | env-var-only cert/token | integration | `cd flow-jetbrains && ./gradlew buildPlugin verifyPlugin` | ❌ W0 | ⬜ pending |
| JET-01 | Marketplace publish | T-41 secret leak | env-var token, never committed | manual | HUMAN-UAT (D-03) | human | ⬜ pending |
| SHOWCASE-01 | showcase WAV two-run cmp-clean | — | seeded PRNG | smoke | `bash scripts/test_two_run_determinism.sh examples/<genre>/<piece>.flow` | ❌ W0 | ⬜ pending |
| SHOWCASE-01 | showcase WAV RMS regression ±0.5 dB/100 ms | — | N/A | integration | `dotnet test --filter "FullyQualifiedName~Phase41ShowcaseRmsTests"` | ❌ W0 | ⬜ pending |
| SHOWCASE-01 | showcase MIDI offline render deterministic | — | seeded PRNG | smoke | render twice, cmp `.mid` SHA-256 | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/.../DocCommentLexTests.cs` — DOC-01 (`///` vs `//` vs `/* */`)
- [ ] `flow-lang.Tests/.../DocCommentBindTests.cs` — DOC-01 (binding + orphan-drop)
- [ ] `flow-lang.Tests/.../FlowDocGenTests.cs` — DOC-01 (HTML+MD emit)
- [ ] `flow-lang.Tests/.../DocCacheTests.cs` — DOC-01 (content-hash incremental)
- [ ] `flow-lang.Tests/.../DocExampleExecTests.cs` — DOC-02 (pass/fail annotation)
- [ ] `flow-lang.Tests/.../WasapiBackendAvailabilityTests.cs` — WASAPI-01 (Linux IsAvailable false, no crash)
- [ ] `flow-lang.Tests/.../Phase41ShowcaseRmsTests.cs` + baseline WAV under `flow-lang.Tests/baselines/Phase41/` — SHOWCASE-01 RMS regression
- [ ] Extend `scripts/publish.sh` for 5-RID + tar/zip + `.sha256` (BIN-01)
- [ ] Extend `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` with `"NAudio"` (WASAPI-01 Web-strip gate)
- [ ] `41-HUMAN-UAT.md` rows: Windows WASAPI audible, macOS CoreAudio audible+latency, osx-x64 exec, osx-arm64 exec, win-x64 exec, JetBrains Marketplace publish, GitHub Release cut

*(COREAUDIO-01 availability test may already exist — confirm before adding a duplicate.)*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Windows audible playback via WASAPI | WASAPI-01 | No Windows hardware on build host | Run `flow run examples/<genre>/<piece>.flow` on Windows; confirm audible stereo output |
| macOS audible playback + <20 ms latency | COREAUDIO-01 | No macOS hardware on build host | Run a live-coding session on macOS; if latency >20 ms, escalate to OwnAudioSharp swap (D-18) |
| osx-x64 / osx-arm64 / win-x64 binary execution | BIN-01 | Cross-compiled, not executable on Linux | Run `flow version` + a render on each platform |
| JetBrains Marketplace publish | JET-01 | Needs human's JetBrains account + signing cert | Follow `41-HUMAN-UAT.md` / deployment runbook; `./gradlew publishPlugin` with env-var token |
| v1.5.0 GitHub Release cut | BIN-01/SHOWCASE-01 | Outward-facing publish | Human verifies `.sha256`, attaches binaries + showcase audio, publishes Release |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
