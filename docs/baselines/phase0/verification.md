# Phase 0 verification result

Phase 0 is complete in the verified working tree on 2026-09-20. This is a Linux
Desktop/Web baseline, with explicit environmental skips, not a hardware or
all-platform certification. The workflow is implemented and its commands were
validated locally; it has not been pushed or run on GitHub Actions here.

| Gate | Evidence |
| --- | --- |
| Current contracts and inventory | [Architecture inventory](architecture-inventory.md), public API/static-field/source/dependency/asset JSON snapshots |
| Vowel/MIDI contracts resolved | [Compatibility decisions](../../decisions/2026-09-20-baseline-compatibility.md); fallback/dedup and channel/note/timing assertions pass |
| SDK and terminal isolation | SDK selects 10.0.112; tests pass under `NO_COLOR=1 TERM=dumb`; per-test environment restoration |
| PR CI and separate test tiers | `.github/workflows/verify.yml`; core, platform and long tier commands all pass from a fresh isolated checkout |
| Warning triage | [Warning inventory](warnings.md); nullable-reference handling, override signature, duplicate import and dead local fixed; remaining warnings stay visible |
| Non-mutating default tests | Full unfiltered verifier found zero tracked-content changes; explicit report updates tested separately |
| Parse/evaluate/render baselines | [Measured results](README.md) and raw samples; no audio deadline claims |

## Test results

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Fresh checkout: core main suite | 2,715 | 0 | 14 |
| Fresh checkout: platform main suite | 13 | 0 | 5 |
| Fresh checkout: long main suite | 33 | 0 | 0 |
| Fresh checkout: MIDI | 21 | 0 | 0 |
| Original workspace: final unfiltered main suite | 2,763 | 0 | 19 |
| Original workspace: final unfiltered MIDI | 21 | 0 | 0 |

The tier sets have no overlap and cover every tracked test. The original
workspace additionally discovers two pre-existing ignored files:
`tests/test_break_builtin.flow` and `tests/test_markov_corpus_array.flow`.
They were neither removed nor added to this change; they explain the two-test
count difference. All final runs preserve tracked content without restoration.

Skipped cases: 13 Web-target-only tests in the Desktop runner, three real MIDI
loopback tests without librtmidi/VirMIDI prerequisites, two macOS CoreAudio tests
on Linux, and one MusicXML test without `mscore`. Exact names and reasons are
in [verification.json](verification.json). Web build/publish inspection still
runs in the platform tier; it does not assert actual browser/device playback.

The fresh checkout exposed two extra baseline defects beyond the initial
failure list: JACK diagnostic dedup leaked between tests, and the showcase
RMS fixture depended on the absolute checkout path. Both were fixed. The latter
required a documented one-time WAV refresh with the existing tolerance retained;
[the comparison](showcase-comparison.json) records hashes and the changed region.
The fixture now additionally requires byte-identical repeated rendering.

The verifier's own two Python tests exercise success and five failure scenarios:
build failure, test failure, missing TRX, zero executed tests, and tracked-file
mutation. Both test projects still run after one test project fails. These tests
pass and the core workflow runs them. CLI smoke:
`dotnet flow-cli/bin/Debug/net10.0/flow.dll run /tmp/flow-phase0/smoke.flow`
with source `(print (str (add 1 2)))` prints `3`.

## Reproduction and evidence

```sh
python3 -m unittest discover -s scripts/ci -p 'test_*.py'
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier core --artifacts /tmp/flow-ci-core
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier platform --artifacts /tmp/flow-ci-platform
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier long --artifacts /tmp/flow-ci-long
NO_COLOR=1 TERM=dumb FLOW_UPDATE_AUDIO_BASELINES=0 python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-ci-all
```

Installed `wasm-tools` is required for platform/all. Run one verification job
per checkout at a time. Artifact roots used for this acceptance run are
`/tmp/flow-phase0/{ci-core-fixed,ci-platform,ci-long-fixed,final-verified}`;
each contains build/test logs, TRX, and `verification.json`. The isolated
snapshot is `/tmp/flow-phase0/checkout`. Raw local evidence is not committed;
summary counts, skips, tier coverage, inventories, and measurement samples are.

Phase 1 is next. Known architectural issues (global state, executing analysis,
abandoned workers, native closure, repeated rendering copies) remain visible
migration work; a clean Phase 0 test baseline does not mean those are solved.
