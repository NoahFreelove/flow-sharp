---
phase: 46
slug: codebase-bloat-removal
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-30
---

# Phase 46 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> **Pure removal/redirect phase — the bar is "prove nothing changed," not "prove a new behavior."**

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`flow-lang.Tests`) + `.flow` script harness (`tests/test_*.flow`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (no extra config) |
| **Quick run command** | `dotnet build` + targeted `dotnet test --filter` for the touched area |
| **Full suite command** | `dotnet test` && `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` |
| **Estimated runtime** | ~120–240 seconds (build + xUnit + ~70 .flow scripts) |

---

## Sampling Rate

- **After every task commit:** `dotnet build` (must compile) + targeted `dotnet test --filter` for the affected test class
- **After every plan wave:** Full `dotnet test` + Phase 28 RMS baselines
- **Before `/gsd:verify-work`:** Full suite green + every `tests/test_*.flow` exits clean + two-run cmp-clean determinism holds
- **Max feedback latency:** ~30 seconds (incremental build + filtered test)

---

## Per-Task Verification Map

| Target | Removal/Keep | Test Type | Automated Command | Proof of Non-Regression |
|--------|--------------|-----------|-------------------|-------------------------|
| D-02 TimelineMap | remove | unit | `dotnet test --filter SongRenderer\|BarRenderer\|SequenceRenderer` | Build green + Song/Section render path tests unchanged; zero external callers confirmed |
| D-03 NoteSynthesizer redirect | redirect→SynthUtils | byte-exact | NEW `Assert.Equal(float[])` guard captured pre-redirect + two-run cmp-clean + Phase 28 RMS | **Highest risk** — incremental-phase vs per-sample recompute may diverge in IEEE-754. Fallback: redirect only `BeatsToSeconds`+`CreateSilence`, keep oscillator loops inline if exact-byte Fact confirms divergence |
| D-04 Fixtures merge | verify-only (already done Phase 44) | fs assertion | `test ! -d flow-lang.Tests/Fixtures && test -d flow-lang.Tests/fixtures` + full `dotnet test` | Already merged in `e0d7274`; task confirms no capital-F path strings remain in `flow-lang.Tests` |
| D-05 audio.flow internal decls | remove (2 decls @224,227) | script | `for t in tests/test_*audio*.flow tests/test_*tone*.flow; do dotnet run ...; done` | Composer stereo proc wrappers (audio.flow ~352-411) untouched → tone scripts byte-identical |
| D-06 exportWav alias | remove + migrate callers | script | run all migrated `tests/test_*.flow` callers; `dotnet test --filter FlowScript\|WriteWav` | 7 callers ported to `writeWav` (arg-order swap); alias-assertion in `test_writewav.flow` rewritten |
| D-07 test.flow legacy half | remove + port consumer | script | `dotnet run --project flow-interpreter tests/test_test_library.flow` | Ported to `@test` (`assert`/`assertEq`/`assertWithinDb`); FAIL-cases inverted via `(assert (not …))` |
| D-08 ClampSamples shims | inline | unit/build | `dotnet build` + `dotnet test --filter Playback\|PulseAudio` | Direct `AudioUtils.ClampSamples()` calls; same namespace, no behavior change |
| D-09 diagnostics .txt | **KEEP** (condition failed) | n/a | `dotnet test --filter DiagnosticRendererGolden` | Golden tests `File.ReadAllText` the .txt files (`:39`,`:77`,`:116`) → removal would break 2 Facts; KEEP per D-09 escape clause |
| D-12 Progression DSL | keep + invest | unit + script | NEW progression unit test (mirror `EuclideanSwingTests` + `FlowEngineRunner.GetVariable`) + extended `examples/showcase.flow` | Adds missing coverage; showcase demo stays byte-identical (non-rendered demo recommended) |
| D-16 legacy doc notes | keep-treatment | build | `dotnet build` | Comment-only notes on Track/Timeline + bars.flow; no deprecation warnings, no stderr advisories |

*Status legend: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] **D-03 exact-byte guard** — capture NoteSynthesizer oscillator output as `float[]` from the PRE-redirect build into a new `Assert.Equal` Fact, so the redirect can be proven bit-identical (or the divergence detected → trigger inline-retention fallback). This MUST exist before the D-03 redirect task runs.

*Otherwise: existing xUnit + `.flow` script + Phase 28 RMS infrastructure covers all phase verification.*

---

## Manual-Only Verifications

| Behavior | Why Manual | Test Instructions |
|----------|------------|-------------------|
| Two-run cmp-clean determinism | Requires two full renders + byte compare across a represervative `.flow` corpus | Render a fixed `.flow` script twice; `cmp` the two WAVs — must be byte-identical |

*All other phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All removal tasks have an automated build/test/script verify
- [ ] D-03 byte-exact guard exists in Wave 0 before the redirect
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
