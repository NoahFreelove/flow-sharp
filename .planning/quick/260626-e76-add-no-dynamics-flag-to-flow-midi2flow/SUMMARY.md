---
quick_id: 260626-e76
slug: add-no-dynamics-flag-to-flow-midi2flow
status: complete
date: 2026-06-26
---

# Quick Task Summary: Add `--no-dynamics` flag to `flow midi2flow`

## One-liner

`flow midi2flow --no-dynamics` suppresses the ppp..fff dynamic markings the
converter normally stamps for velocity fidelity, via an optional
`bool emitDynamics = true` threaded through the FlowGenerator call chain — the
default path stays byte-identical for every existing caller.

## Files Changed

- **flow-midi/Conversion/FlowGenerator.cs** — added optional `bool emitDynamics = true`
  (appended LAST) to `Generate`, `GenerateWithStats`, `WriteSequence`, `FormatBar`,
  `FormatElements`; threaded through the call chain. In `FormatElements` the
  `VelocityToDynamic` + sticky-dynamic emission in the `NoteElement` and
  `ChordElement` arms is now guarded behind `if (emitDynamics)`. When false, no
  dynamic token is computed or pushed and `stickyDynamic` is never touched; all
  other tokens (note names, durations, dots, ties, chords, rests) are unchanged.
- **flow-cli/Commands/Midi2FlowCommand.cs** — added `--no-dynamics` bool `Option`
  (default false, description "Omit dynamic markings (ppp..fff) from the generated
  source") and passed `emitDynamics: !noDynamics` into the existing
  `FlowGenerator.GenerateWithStats(midi, qr, input.Name, roundTrip: true)` call.
- **.planning/STATE.md** — appended Quick Tasks Completed row.

## Verification Results

### Build
`dotnet build flow-cli` (pulls flow-midi) -> Build succeeded, 0 errors (only
pre-existing NU1701 / VSTHRD002 warnings).

### Default still emits dynamics (byte-identical contract)
`midi2flow ~/Downloads/ragtime.mid -o /tmp/rt_dyn.flow` -> converted; output
contains the dynamic ladder tokens (mf, f, ...) exactly as before. Because
`emitDynamics=true` runs the original statements verbatim (the `if (emitDynamics)`
guard is a no-op when true), the default output is structurally identical to
pre-change output.

### Flag suppresses dynamics
`midi2flow ~/Downloads/ragtime.mid -o /tmp/rt_nodyn.flow --no-dynamics` -> converted;
zero standalone dynamic tokens. `check /tmp/rt_nodyn.flow` -> OK.
`check /tmp/rt_dyn.flow` -> OK.

### Diff shows ONLY removed dynamics (critical correctness check)
Raw `diff` is large only because removing tokens shifts col-100 line-wrap points.
Normalizing whitespace (erasing wrap differences) and stripping standalone dynamic
tokens from the default:

    perl -0777 -pe 's/\s+/ /g' rt_dyn.flow | perl -pe 's/\b(ppp|pp|p|mp|mf|f|ff|fff)\b //g' > rt_dyn.norm
    perl -0777 -pe 's/\s+/ /g' rt_nodyn.flow > rt_nodyn.norm
    diff rt_dyn.norm rt_nodyn.norm

Result: IDENTICAL (exit 0) — the only difference between default and
`--no-dynamics` output is the removed dynamic tokens. No note/duration/structure
change.

### Tests
`dotnet test flow-midi.Tests` -> 19/21 pass. The 2 failures
(`FlowGeneratorStructureTests.One_Sequence_Per_Track_Channel_No_RH_LH_Suffix`,
`QuantizerRoundingTests.Two_Octave_Range_Does_Not_Split_RH_LH`) are PRE-EXISTING —
they fail identically on clean HEAD (verified by stashing the working changes and
re-running). They concern RH/LH track splitting, not dynamics, and are out of scope.

## Deviations from Plan

None — plan executed exactly as written.

## Out-of-Scope / Deferred

- 2 pre-existing flow-midi.Tests failures (RH/LH splitting). Not introduced by this
  change; left untouched.

## Self-Check: PASSED
