# Flow restructuring progress

Roadmap: [Flow restructuring and focused DAW](../2026-09-20-flow-restructuring-roadmap.md).

## Current milestone

Phase 0 is in progress. Its clean-baseline gate has not passed.

Starting revision: `1e85f6710b6a39c293b0e4361ca57364489c824d`.
Pre-existing changes: `docs/ARCHITECTURE.md` and the untracked roadmap; preserved.
Owner: primary implementation agent. No delegated file ownership.

| Task | Status | Scope / dependencies |
| --- | --- | --- |
| P0-01a SDK and terminal isolation | verified | `global.json`, terminal test classes, terminal environment helper. No runtime semantic changes. |
| P0-01b Fresh baseline | verified | Main and MIDI test suites; logs and TRX under `/tmp/flow-restructuring-baseline/`. |
| P0-01c CI and disputed behavior contracts | ready | Depends on baseline classification; unknown-vowel and MIDI splitting decisions remain open. |
| P0-02 Non-mutating test outputs | verified | Audit and bundle reports use isolated artifacts; explicit update mode verified; full solution run preserved tracked contents. |

## Decisions

- Start with SDK selection and terminal test reliability before runtime extraction.
- Use .NET SDK `10.0.100` with `latestFeature` roll-forward and prereleases disabled: accept installed stable .NET 10 feature bands, avoid silently switching major versions.
- Terminal tests own `NO_COLOR` and `TERM` for each test and restore prior values. Existing suite-wide collection serialization remains required for process-global state.
- Preserve unknown-vowel and MIDI runtime behavior pending explicit contract resolution; do not suppress failing tests.

## Verification

Host: Ubuntu 26.04 x64; installed SDK `10.0.112`, runtime `10.0.12`.
The initial `dotnet --info` reported `10.0.0` as an invalid SDK version.

Before-change baseline (inherited `NO_COLOR=1`, `TERM=xterm-256color`):

```sh
dotnet test flow-lang.Tests/flow-lang.Tests.csproj --logger 'trx;LogFileName=language-before.trx' --results-directory /tmp/flow-restructuring-baseline
```

Log: `/tmp/flow-restructuring-baseline/language-before.log`.
Result: **2,753 passed, 9 failed, 19 skipped**, 2,781 total. Local `/tmp` artifacts are not committed.

## Remaining gates

Resolution of the three behavior-contract failures, PR CI, dependency/API inventory, warning triage, and representative performance measurements remain before Phase 0 can be declared complete. Hardware/audio listening validation is not claimed.


## Completed slice — 2026-09-20

P0-01a is verified in the working tree: `global.json`,
`flow-lang.Tests/Helpers/TerminalEnvironmentScope.cs`,
`Integration/Audit0609/LiveStatusPanelTests.cs`, and
`Integration/Phase38/{AnsiPanelRenderTests,PanelTtyFallbackTests}.cs`
(the integration paths are relative to `flow-lang.Tests`).
The ledger was initially committed as `6ef3d91`; implementation files remain
uncommitted for review. No runtime behavior changed. Phase 0 remains incomplete.

SDK verification: `dotnet --version` selects `10.0.112` with the corrected
configuration. `git diff --check` passes.

Terminal validation commands:

```sh
NO_COLOR=1 TERM=dumb dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter 'FullyQualifiedName~AnsiPanelRenderTests|FullyQualifiedName~LiveStatusPanelTests|FullyQualifiedName~PanelTtyFallbackTests' --logger 'trx;LogFileName=terminal-hostile.trx' --results-directory /tmp/flow-restructuring-baseline
env -u NO_COLOR -u TERM dotnet test flow-lang.Tests/flow-lang.Tests.csproj --no-build --filter 'FullyQualifiedName~AnsiPanelRenderTests|FullyQualifiedName~LiveStatusPanelTests|FullyQualifiedName~PanelTtyFallbackTests' --logger 'trx;LogFileName=terminal-unset.trx' --results-directory /tmp/flow-restructuring-baseline
dotnet test flow-midi.Tests/flow-midi.Tests.csproj --logger 'trx;LogFileName=midi-baseline.trx' --results-directory /tmp/flow-restructuring-baseline
```

Both terminal runs: **13 passed, 0 failed, 0 skipped**, including the eight
previously failing cases and the new `TERM=dumb` case. The new ESC assertion
uses ordinal comparison because culture-sensitive comparison can ignore ESC.
The entire main suite was not rerun after these test-only changes; no claim of
a post-change full-suite pass is made.

MIDI baseline: **19 passed, 2 failed, 0 skipped**, 21 total.
Logs share each TRX filename stem with a `.log` suffix in the artifact directory.

### Failure and skip classification

- Eight main-suite failures: four `AnsiPanelRenderTests` and four
  `LiveStatusPanelTests` depended on the inherited terminal environment.
  All now pass under both tested environments.
- `FormantDataTests.GetFormants_UnknownVowel_ThrowsArgumentException`:
  test expects an exception; `FormantData.GetFormants` deliberately falls back
  to `ah` with an advisory. Preserve current runtime behavior until the contract
  decision is recorded and the conflicting test is reconciled.
- `FlowGeneratorStructureTests.One_Sequence_Per_Track_Channel_No_RH_LH_Suffix`:
  expects two sequences, receives four.
- `QuantizerRoundingTests.Two_Octave_Range_Does_Not_Split_RH_LH`:
  expects one track, receives right/left-hand tracks. Both MIDI failures come
  from current melodic hand/voice splitting in `Quantizer`; whether to preserve
  splitting by default or expose an option remains a compatibility decision.
- Main-suite skips: 13 Web-only tests on Desktop, 3 real MIDI loopback tests
  lacking librtmidi/VirMIDI prerequisites, 2 macOS-only CoreAudio tests, and
  1 MusicXML round-trip test lacking `mscore`. No failures were suppressed.

### Test side effects and warnings

The default main suite changed seven previously clean tracked reports:
`.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/` files
`advisory-sites.txt`, `all-clamps.txt`, `charitable-sites.txt`, `flow-call-sites.txt`,
`flow-proc-decls.txt`, `summary.txt`, and
`.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md`.
Generated versions were preserved under `/tmp/flow-restructuring-baseline/generated/`
and only those test-generated changes were restored. User changes remain intact.
The non-mutating-test gate therefore still fails; restoration is not a fix.

Build warnings include Rug.Osc .NET Framework compatibility (`NU1701`),
nullability (`CS8765`, `CS8604`, `CS8602`), duplicate import (`CS0105`), unused
members/locals (`CS0414`, `CS0219`), and test analyzer warnings about blocking,
cancellation, and unused theory arguments. Logs retain details; no blanket
warning suppression or warnings-as-errors policy was introduced.

At the end of the first slice, P0-02 was next. Its completion is recorded below.
No architecture extraction has begun.


## Completed slice P0-02 — 2026-09-20

Starting revision: `8c623c7`, with the previous slice's implementation still
in the working tree. Owner: primary implementation agent. No delegates.

Verified working-tree changes:

- `flow-lang.Tests/Helpers/TestReportDirectory.cs`: unique retained directories
  under the system temporary `flow-test-reports/` directory by default;
  `FLOW_TEST_ARTIFACTS_DIR` selects another root. Only `FLOW_UPDATE_REPORTS=1`
  directs reports to their historical paths, taking precedence over that root.
- `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs`: passes
  the scripts' existing `--out-dir` argument using `ProcessStartInfo.ArgumentList`,
  reads newly generated artifacts, and checks historical file contents and
  membership recursively in normal mode.
- `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs`: writes the
  bundle report through the same output policy and documents explicit updates
  in newly generated report text.
- `docs/TESTING.md`: artifact collection and explicit historical-update commands.

Standalone audit script defaults are unchanged; direct callers can already
select `--out-dir`. No production runtime changes or historical report refreshes
are included. Implementation changes remain uncommitted; only the ledger is
committed as requested.

### Verification

Artifacts: `/tmp/flow-restructuring-reports/` (local, not committed).

```sh
FLOW_TEST_ARTIFACTS_DIR='/tmp/flow-restructuring-reports/artifacts with spaces' dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter 'FullyQualifiedName~ClampGrepConsistencyTests|FullyQualifiedName~BundleSizeBudgetTests' --logger 'trx;LogFileName=reports.trx' --results-directory /tmp/flow-restructuring-reports
dotnet test flow-sharp.sln --logger trx --results-directory /tmp/flow-restructuring-reports/full
FLOW_UPDATE_REPORTS=1 dotnet test flow-lang.Tests/flow-lang.Tests.csproj --no-build --filter 'FullyQualifiedName~InventoryFiles_LandInSelectedDirectory|FullyQualifiedName~BundleSizeReport_WrittenToDisk' --logger 'trx;LogFileName=update.trx' --results-directory /tmp/flow-restructuring-reports
```

- Focused filter: **12 passed**, including the matching strict-clamp tests.
  Artifact paths containing spaces worked. Hashes of all existing tracked files
  were unchanged after the run (`tracked-before.json`).
- Full default solution run: main **2,762 passed, 1 failed, 19 skipped**;
  MIDI **19 passed, 2 failed, 0 skipped**. Remaining failures are exactly the
  previously classified vowel/MIDI contract disagreements. This also verifies
  the prior terminal fixes in the full suite.
- Full-run before/after SHA-256 comparison of every existing tracked file found
  **zero content changes**, without restoration (`full-before.json`). Default
  temporary report generation was exercised because neither report setting
  was supplied to this run. The non-mutating tracked-content gate passes on
  this Linux configuration; this is not a claim that all tests pass.
- Explicit update mode: **2 passed**; all eight intended historical outputs
  were written (including the empty `input-clamps.txt`). The validation wrapper
  saved results under `explicit-update-output/` and restored exact pre-run
  contents in a `finally` block. The wrapper is retained as `verify-update.py`.
- `git diff --check` passes. `reports.log`, `full.log`, and `update.log` retain
  build/test evidence; corresponding TRX files are in the same artifact tree.

Next ready work: settle the documented vowel/MIDI contracts and add PR CI
without suppressing failures. Dependency/API inventory, warning triage, and
performance measurements are still required for the complete Phase 0 gate.
