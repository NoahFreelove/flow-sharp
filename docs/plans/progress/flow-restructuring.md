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
| P0-02 Non-mutating test outputs | ready | Inventory generated tracked outputs before moving them. |

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

Resolution of the three behavior-contract failures, non-mutating default tests, PR CI, dependency/API inventory, warning triage, and representative performance measurements remain before Phase 0 can be declared complete. Hardware/audio listening validation is not claimed.


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

Next ready slice: P0-02, redirect generated audit/bundle output to artifacts
and require an explicit baseline-update mode. Then resolve the vowel/MIDI
contracts and implement PR CI. No architecture extraction has begun.
