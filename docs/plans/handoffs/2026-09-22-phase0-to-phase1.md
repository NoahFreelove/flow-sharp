# Flow restructuring handoff — 2026-09-22

Phase 0 is complete in the verified working tree. Phase 1 has not started.
The latest implementation verification was on **2026-09-20**; writing this
handoff on September 22 did not rerun the suites.

## Read first

- [Roadmap](../2026-09-20-flow-restructuring-roadmap.md): section 2 defines the language contract; section 10 defines phase gates.
- [Progress ledger](../progress/flow-restructuring.md): current status at the top and chronological work below. Earlier open-gate notes are historical; the completion record supersedes them.
- [Phase 0 acceptance record](../../baselines/phase0/verification.md): results, skips, reproduction commands and evidence locations.
- [Compatibility decisions](../../decisions/2026-09-20-baseline-compatibility.md): intended vowel/MIDI behavior and the reviewed showcase fixture refresh.
- [Architecture inventory](../../baselines/phase0/architecture-inventory.md), [warning triage](../../baselines/phase0/warnings.md), and [performance baseline](../../baselines/phase0/README.md).

## Repository and commit state

Workspace: `/home/noah/Desktop/projects/flow-sharp`, branch `dev`.
Starting source revision: `1e85f6710b6a39c293b0e4361ca57364489c824d`.
Latest commit before this handoff: `dc325c6` (Phase 0 completion ledger).
Earlier ledger commits: `8135359`, `8c623c7`, `6ef3d91`.

**Phase 0 implementation remains uncommitted.** The commits above record
progress; cloning their HEAD alone does not reproduce the completed baseline.
This handoff and its ledger link are committed separately from implementation.
Inspect `git status --short` and `git diff` before staging anything. Do not reset
or clean the working tree to obtain a baseline.

Preserve these pre-existing user changes:

- Modified `docs/ARCHITECTURE.md` (the introductory roadmap link).
- Untracked `docs/plans/2026-09-20-flow-restructuring-roadmap.md`.
- Ignored local tests `tests/test_break_builtin.flow` and `tests/test_markov_corpus_array.flow`; neither was changed or added by this work.

The remaining pending work consists of SDK configuration, test fixes and tier
traits, four small language warning fixes, a MIDI comment correction, the
reviewed showcase WAV, testing documentation, and new files under
`.github/workflows/verify.yml`, `docs/baselines/`, `docs/decisions/`,
`scripts/BaselineProbe/`, `scripts/baseline/`, `scripts/ci/`, plus the two test
helpers `TerminalEnvironmentScope.cs` and `TestReportDirectory.cs`.
Review explicit paths before committing; avoid indiscriminate `git add .`.
The user asked that the progress ledger be committed and updated after work.

## What Phase 0 changed

| Area | Completed work |
| --- | --- |
| SDK | Corrected invalid `10.0.0` to `10.0.100`, with stable `latestFeature` roll-forward; verification selected 10.0.112. |
| Terminal tests | Tests control and restore `NO_COLOR` and `TERM`; hostile-environment cases pass. Existing collection serialization remains necessary for process-global state. |
| Test artifacts | Audit/bundle reports use unique temporary or configured artifact directories. Default verification detects tracked-file mutations without restoring them. |
| Behavior contracts | Tests now assert existing unknown-vowel `ah` fallback and warning dedup, and MIDI channel/hand/voice splitting with note/timing preservation. These were stale assertions, not runtime behavior changes. |
| Additional isolation | JACK tests reset diagnostic dedup and transport overrides. Showcase rendering uses a canonical logical source filename and isolated WAV/MIDI output. |
| CI | PR/push/dispatch workflow and local verifier split core, platform and long tests; fail on missing/empty results or tracked-file mutations. |
| Inventories | Recorded public APIs, static-field candidates, source/module/dependency/native/asset contracts, warnings, and Release time/allocation/memory measurements. |

No runtime extraction or Phase 1 implementation has begun. Global state,
executing analysis, worker lifetime, native dependency closure and repeated
rendering allocations remain future work.

## Verification and important caveats

| Accepted run (2026-09-20) | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Fresh isolated checkout: core main | 2,715 | 0 | 14 |
| Fresh isolated checkout: platform main | 13 | 0 | 5 |
| Fresh isolated checkout: long main | 33 | 0 | 0 |
| Fresh isolated checkout: MIDI | 21 | 0 | 0 |
| Original workspace: unfiltered main | 2,763 | 0 | 19 |
| Original workspace: MIDI | 21 | 0 | 0 |

The isolated checkout included the candidate working-tree implementation.
The tiers cover all tracked tests without overlap. The two ignored local Flow
tests explain the original workspace's two extra passes. The 19 skips comprise
13 Web-only cases in the Desktop runner, three real MIDI prerequisite skips,
two macOS CoreAudio cases on Linux, and one missing-`mscore` MusicXML case.
Web build/publish still runs in the platform tier.

All accepted runs left tracked content unchanged. The verifier's two Python
tests passed, including five failure scenarios; CLI arithmetic printed `3`;
`git diff --check` passed. GitHub Actions has not run remotely, and actual
hardware/audible playback has not been verified.

- Run **one build/test/publish verification job per checkout**. Do not edit tracked files while the verifier is running: it hashes their contents before and after, including an initially dirty tree.
- Platform/all requires `wasm-tools`. Reproduction commands are in the acceptance record. Keep suites serialized because tests share console and other global state.
- Normal reports go to `/tmp/flow-test-reports` or `FLOW_TEST_ARTIFACTS_DIR`. Only `FLOW_UPDATE_REPORTS=1` opts into historical report paths. Standalone audit scripts retain their historical defaults.
- Only explicitly reviewed fixture updates should use `FLOW_UPDATE_AUDIO_BASELINES=1`. The showcase refresh fixed checkout-path-dependent granular random seeding by using `examples/edm/pulse.flow` as the logical filename. Production randomness was unchanged. Preserve the refreshed WAV and the repeated-render byte-equality assertion; see [comparison evidence](../../baselines/phase0/showcase-comparison.json).
- Performance results measure offline work, not audio callback deadlines. First-engine construction was measured after reflection, not as a cold process start. The static-field inventory includes readonly references and compiler-generated candidates.
- Raw logs/TRX live under `/tmp/flow-phase0/{ci-core-fixed,ci-platform,ci-long-fixed,final-verified}`; the isolated snapshot is `/tmp/flow-phase0/checkout`. These are ephemeral. Durable summaries and raw inventory/measurement JSON are under `docs/baselines/phase0/`, currently pending the implementation commit.

## Next work: Phase 1

The next implementation request should start with roadmap section 2 and the
Phase 1 gate. The current request is only to write this handoff.

1. Turn characteristic language programs into executable contracts with expected results/diagnostics, rationale and compatibility status. Include feature interactions: optional parentheses, implicit returns, `->`/`~>`, lexical captures, overloads/named arguments/varargs, units, and generous/strict behavior.
2. Add non-musical tutorial examples covering collections, dictionary aggregation, tuple pipelines, recursion/trees, cached lazy failures, pattern dispatch and a two-module utility.
3. Record short decisions for module/profile compatibility, stable grammar with optional music semantics, exported signature ownership, dependency direction, session lifetime, and public analysis/evaluation APIs.
4. Prototype a minimal host against current code and document the remaining direct music/native/sample dependencies. A language-only artifact is a later extraction gate.
5. Verify the contracts and runnable CLI, update and commit the progress ledger, and report exactly what is committed versus still pending.

Avoid starting Phase 2 extraction before the Phase 1 contracts and seams are
specific enough to test. UI framework selection and language redesign are
outside this phase.

### Suggested resume prompt

> Continue the Flow restructuring roadmap with Phase 1. Read
> `docs/plans/handoffs/2026-09-22-phase0-to-phase1.md` and the progress ledger
> first. Preserve the verified but uncommitted Phase 0 implementation and the
> pre-existing user changes. Implement Phase 1's executable contracts,
> non-musical examples, architecture decisions and minimal-host prototype;
> verify its completion gate and update and commit the progress ledger.
