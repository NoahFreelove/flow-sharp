---
phase: 30
slug: flow-cli-formal-install
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-10
---

# Phase 30 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from 30-RESEARCH.md `## Validation Architecture` (line 884+) and 30-SPEC.md `## Acceptance Criteria` (22 boxes).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + Microsoft.NET.Test.Sdk 17.13.0 (existing in flow-lang.Tests) |
| **New test project** | `flow-midi.Tests/` (created in Plan 30-06; mirrors flow-lang.Tests setup) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` + `flow-midi.Tests/flow-midi.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase30" --logger "console;verbosity=minimal"` |
| **Full suite command** | `dotnet test` (both projects) |
| **Estimated runtime** | ~30 seconds (round-trip ≤15s per SPEC + integration + smoke shells) |

---

## Sampling Rate

- **After every task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase30" --logger "console;verbosity=minimal"` (subset, fast)
- **After every plan wave:** `dotnet test` (full solution suite — must stay GREEN per Phase 18/25/27/28 backward-compat ACK contract)
- **Phase gate:** Full suite + `bash scripts/test-install.sh` exit 0 before `/gsd-verify-work`
- **Max feedback latency:** 30 seconds (Phase 30 subset); 90 seconds (full suite including Phase 28 RmsRegression facts)

---

## Per-Requirement Verification Map

| Req ID | Behavior | Plan | Wave | Test Type | Automated Command | File Exists | Status |
|--------|----------|------|------|-----------|-------------------|-------------|--------|
| REQ-1 | All 11 subcommands exit 0 on valid input | 30-01, 30-02, 30-09 | 0, 1, 5 | Integration | `dotnet test flow-lang.Tests --filter "Phase30.SubcommandSmoke"` | ❌ W0 | ⬜ pending |
| REQ-1 | Each subcommand `--help` exits 0 + prints usage | 30-02 | 1 | Smoke | `for cmd in run eval repl watch play render flow2midi midi2flow check version new; do dotnet run --project flow-cli -- $cmd --help; done` | ⚠️ semi-manual | ⬜ pending |
| REQ-2 | Publish produces self-contained binary, ≤120 MB | 30-04 | 3 | Build smoke | `bash scripts/test-publish.sh` | ❌ W0 | ⬜ pending |
| REQ-2 | `./flow run script.flow` works on clean Linux x64 (no .NET runtime) | 30-04 | 3 | Manual UAT | manual (no clean VM in CI) | n/a | ⬜ manual |
| REQ-3 | install.sh installs to ~/.local/share/flow without sudo | 30-05 | 4 | Integration | `bash scripts/test-install.sh` | ❌ W0 | ⬜ pending |
| REQ-3 | --system install requires sudo | 30-05 | 4 | Manual UAT (sudo behavior) | manual | n/a | ⬜ manual |
| REQ-3 | Re-run install.sh upgrades in place (idempotent) | 30-05 | 4 | Integration | `bash scripts/test-install.sh` (re-run path) | ❌ W0 | ⬜ pending |
| REQ-3 | install.sh writes default config.toml only if absent | 30-05 | 4 | Smoke | covered by `bash scripts/test-install.sh` | ❌ W0 | ⬜ pending |
| REQ-4 | config.toml values reflect in interpreter behavior (`default_tempo`) | 30-03 | 2 | Integration | `dotnet test flow-lang.Tests --filter "Phase30.FlowConfigPropagation"` | ❌ W0 | ⬜ pending |
| REQ-4 | All 4 optional keys propagate (`default_audio_device`, `default_tempo`, `default_timesig`, `stdlib_search_path`) | 30-03 | 2 | Integration | `dotnet test flow-lang.Tests --filter "Phase30.FlowConfigPropagation"` (Facts per key) | ❌ W0 | ⬜ pending |
| REQ-4 | Missing config.toml is silent fallback to defaults | 30-03 | 2 | Smoke | `rm -f ~/.config/flow/config.toml && dotnet run --project flow-cli -- version` exits 0, no stderr | ❌ W0 | ⬜ pending |
| REQ-4 | Malformed config.toml charitable: warning, fall back, no abort | 30-03 | 2 | Smoke | malformed-config smoke in 30-03 Task 2 verify block | ❌ W0 | ⬜ pending |
| REQ-5 | midi2flow emits parseable Flow source | 30-08, 30-09 | 3, 5 | Integration | `dotnet test flow-lang.Tests --filter "Phase30.Midi2FlowParseable"` | ❌ W0 | ⬜ pending |
| REQ-5 | Output structure: one Sequence per track, single section, single Song, no `(play output)` trailer | 30-08, 30-09 | 3, 5 | Unit | `dotnet test flow-midi.Tests --filter "FlowGenerator.RoundTripMode"` | ❌ W0 | ⬜ pending |
| REQ-6 | Round-trip note-count + pitch + duration parity, ±1 tick, 3 fixtures | 30-09 | 5 | Integration | `dotnet test flow-lang.Tests --filter "Phase30.Midi2FlowRoundTrip"` | ❌ W0 | ⬜ pending |
| REQ-6 (subtest) | Quarter notes preserved (Bug B Defect 1 regression) | 30-06, 30-07 | 1, 2 | Unit (synthetic fixture) | `dotnet test flow-midi.Tests --filter "Quantizer.QuarterNoteRhythm"` | ❌ W0 | ⬜ pending |
| REQ-6 (subtest) | Q+E+E pattern preserved (Bug B regression) | 30-06, 30-07 | 1, 2 | Unit (synthetic fixture) | `dotnet test flow-midi.Tests --filter "Quantizer.QuarterEighthEighthPattern"` | ❌ W0 | ⬜ pending |
| REQ-6 (subtest) | Chord notes preserved (Bug B regression) | 30-06, 30-07 | 1, 2 | Unit (synthetic fixture) | `dotnet test flow-midi.Tests --filter "Quantizer.ChordRoundTrip"` | ❌ W0 | ⬜ pending |
| REQ-6 (subtest) | Voice-block polyphony preserved (Phase 28 contract) | 30-09 | 5 | Integration | `dotnet test flow-lang.Tests --filter "Phase30.Midi2FlowRoundTrip" ~ two_voice_counterpoint` | ❌ W0 | ⬜ pending |
| REQ-7 | test-install.sh exits 0 | 30-05 | 4 | Bash smoke | `bash scripts/test-install.sh` | ❌ W0 | ⬜ pending |
| REQ-7 | flow render produces non-empty WAV (SPEC-7 acceptance line) | 30-05 | 4 | Bash smoke | `test -s "$TMP/test.wav"` in test-install.sh | ❌ W0 | ⬜ pending |
| REQ-7 | Total smoke runtime ≤ 60s | 30-05 | 4 | Bash smoke | `time bash scripts/test-install.sh` | ❌ W0 | ⬜ pending |
| REQ-8 | `dotnet run --project flow-interpreter` continues to work for the 4 Phase 27 fixtures (tutorial.flow, showcase.flow, h_alias.flow, microtonal_ji.flow) | all | all | Smoke | `for f in tutorial showcase h_alias microtonal_ji; do dotnet run --project flow-interpreter examples/$f.flow; done` | ⚠️ existing scripts may cover | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Per 30-RESEARCH.md `## Validation Architecture` (Wave 0 Gaps section, lines 919-929):

- [ ] `flow-midi.Tests/` — entirely new xUnit project (mirrors flow-lang.Tests setup) — **Plan 30-06**
- [ ] `flow-midi.Tests/flow-midi.Tests.csproj` — xunit.v3 + ProjectReference to flow-midi — **Plan 30-06**
- [ ] `flow-midi.Tests/Quantizer/QuantizerTests.cs` — synthetic-fixture tests for Bug B regression — **Plan 30-06**
- [ ] `flow-midi.Tests/Conversion/FlowGeneratorTests.cs` — output-shape tests — **Plan 30-08**
- [ ] `flow-lang.Tests/Integration/Phase30/` directory — **Plans 30-03, 30-09**
- [ ] `flow-lang.Tests/Integration/Phase30/Midi2FlowRoundTripTests.cs` — REQ-6 acceptance test — **Plan 30-09**
- [ ] `flow-lang.Tests/Integration/Phase30/SubcommandSmokeTests.cs` — REQ-1 acceptance — **Plan 30-02** (optional; subcommand handler dotnet-run smoke covers in interim)
- [ ] `flow-lang.Tests/Integration/Phase30/FlowConfigPropagationTests.cs` — REQ-4 acceptance — **Plan 30-03**
- [ ] `flow-lang.Tests/Fixtures/midi/{ragtime_q_ee,two_voice_counterpoint,drum_loop}.mid` + source `.flow` + LICENSE + README — **Plan 30-09 Task 2**
- [ ] `scripts/test-publish.sh` — smoke test that `dotnet publish` succeeds and `du -sh` ≤120MB — **Plan 30-04**
- [ ] `scripts/test-install.sh` — REQ-7 smoke (publish + install + flow version + flow check + flow render + non-empty WAV assertion) — **Plan 30-05**

*Wave 0 is distributed across Plans 30-04, 30-05, 30-06, 30-09 — each plan's first task seeds its own test infrastructure before adding behavior.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `flow play examples/showcase.flow` audio output | SPEC-1, REQ-2 | PulseAudio absent in CI; sound output is subjective | Run on dev box with PulseAudio: `flow play examples/showcase.flow`; confirm audio plays without distortion |
| Self-contained binary on clean Linux x64 (no .NET runtime) | REQ-2 | No clean VM in CI | Spin up a Docker container `ubuntu:24.04` (no .NET), copy publish dir, run `./flow version`; confirm exit 0 |
| `--system` install (sudo path) | REQ-3 | CI containers don't have a real sudo; permissions semantics differ | On dev box: `sudo bash scripts/install.sh --system`; confirm `/usr/local/bin/flow version` works for any user |
| `flow new my-piece` scaffold renders without error | SPEC-1 (REQ-1) | The scaffold needs the just-installed binary in PATH | `flow new /tmp/test-piece && cd /tmp/test-piece && flow render *.flow -o /tmp/test.wav`; confirm exit 0 + non-empty WAV |
| DAW round-trip smoke for 3 fixtures | REQ-6 | Bit-level equality is insufficient — listen for correctness | Load each `roundtrip.mid` in LMMS / Reaper; visual diff against source `.mid`; confirm note positions match |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (test projects + directories + fixtures created)
- [ ] No watch-mode flags in automated verify blocks
- [ ] Feedback latency < 30s for Phase 30 subset
- [ ] `nyquist_compliant: true` set in frontmatter
- [ ] REQ-1..REQ-8 all have at least one automated verification row
- [ ] Round-trip suite runtime ≤ 15s (SPEC test-runtime-budget)
- [ ] Smoke runtime ≤ 60s (SPEC test-runtime-budget)

**Approval:** pending — to be marked approved after manual UAT in `30-VERIFICATION.md` (created in Plan 30-09 Task 4).
