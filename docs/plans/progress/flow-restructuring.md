# Flow restructuring progress

Roadmap: [Flow restructuring and focused DAW](../2026-09-20-flow-restructuring-roadmap.md).

## Current milestone

Phase 0 is in progress. Its clean-baseline gate has not passed.

Starting revision: `1e85f6710b6a39c293b0e4361ca57364489c824d`.
Pre-existing changes: `docs/ARCHITECTURE.md` and the untracked roadmap; preserved.
Owner: primary implementation agent. No delegated file ownership.

| Task | Status | Scope / dependencies |
| --- | --- | --- |
| P0-01a SDK and terminal isolation | running | `global.json`, terminal test classes, terminal environment helper. No runtime semantic changes. |
| P0-01b Fresh baseline | running | Main and MIDI test suites; logs and TRX under `/tmp/flow-restructuring-baseline/`. |
| P0-01c CI and disputed behavior contracts | ready | Depends on baseline classification; unknown-vowel and MIDI splitting decisions remain open. |
| P0-02 Non-mutating test outputs | ready | Inventory generated tracked outputs before moving them. |

## Decisions

- Start with SDK selection and terminal test reliability before runtime extraction.
- Use .NET SDK `10.0.100` with `latestFeature` roll-forward and prereleases disabled: accept installed stable .NET 10 feature bands, avoid silently switching major versions.
- Terminal tests own `NO_COLOR` and `TERM` for each test and restore prior values. Existing suite-wide collection serialization remains required for process-global state.
- Preserve unknown-vowel and MIDI runtime behavior pending explicit contract resolution; do not suppress failing tests.

## Verification

Host: Ubuntu 26.04 x64; installed SDK `10.0.112`, runtime `10.0.12`.
The initial `dotnet --info` reported `10.0.0` as an invalid SDK version.

Running before-change baseline:

```sh
dotnet test flow-lang.Tests/flow-lang.Tests.csproj --logger 'trx;LogFileName=language-before.trx' --results-directory /tmp/flow-restructuring-baseline
```

Log: `/tmp/flow-restructuring-baseline/language-before.log`.
Results and final verification will be recorded after completion. Local `/tmp` artifacts are not committed.

## Remaining gates

Fresh failure/skip classification, verified terminal isolation, MIDI baseline, non-mutating default tests, PR CI, dependency/API inventory, warning triage, and representative performance measurements remain before Phase 0 can be declared complete. Hardware/audio listening validation is not claimed.
