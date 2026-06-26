---
quick_id: 260626-e76
slug: add-no-dynamics-flag-to-flow-midi2flow
status: planned
---

# Quick Task: Add `--no-dynamics` flag to `flow midi2flow`

## Goal

Give `flow midi2flow` a `--no-dynamics` flag that suppresses the dynamic
markings (ppp/pp/p/mp/mf/f/ff/fff) the converter normally stamps on every bar
for velocity fidelity. Composer escape-hatch requested alongside the
just-fixed chord-dynamic-bar-doubling bug. Default behavior (dynamics ON) must
stay byte-identical for every existing caller.

## Tasks

1. **flow-midi/Conversion/FlowGenerator.cs** — thread a new optional
   `bool emitDynamics = true` parameter (added at the END of each signature,
   defaulted true) through the call chain:
   - `GenerateWithStats(...)` and the `Generate(...)` convenience overload
   - `WriteSequence(...)`
   - `FormatBar(...)`
   - `FormatElements(List<IBarElement> elements, bool useAutoFit, bool emitDynamics)`
   In `FormatElements`, in the `NoteElement` and `ChordElement` switch arms,
   guard the dynamic-token emission (`VelocityToDynamic` + the
   `parts.Add(dyn)` / sticky-dynamic update) behind `if (emitDynamics)`. When
   `emitDynamics` is false, do NOT compute or push any dynamic token and do
   NOT touch `stickyDynamic`. Every other emitted token (note names,
   durations, dots, ties, chords, rests) stays exactly as today.

2. **flow-cli/Commands/Midi2FlowCommand.cs** — add a
   `--no-dynamics` bool `Option` (default false) with a clear description
   ("Omit dynamic markings (ppp..fff) from the generated source"). Pass
   `emitDynamics: !noDynamics` into the existing
   `FlowGenerator.GenerateWithStats(midi, qr, input.Name, roundTrip: true)`
   call (named arg at the end).

## Constraints

- Default (dynamics ON) output must be byte-identical to current output for
  all existing callers (flow-midi/Program.cs, tests) — they don't pass the new
  arg.
- Do NOT touch the renderer, NoteStreamCompiler, or any audio path (that was a
  separate, already-committed fix).
- Do NOT modify ~/Downloads/ragtime.flow.

## Verification

- `dotnet build` clean (flow-cli + flow-midi).
- Default still emits dynamics:
  `dotnet run --project flow-cli -- midi2flow ~/Downloads/ragtime.mid -o /tmp/rt_dyn.flow`
  → grep finds dynamic tokens (e.g. standalone ` f `, `mf`, `ff`).
- Flag suppresses them:
  `dotnet run --project flow-cli -- midi2flow ~/Downloads/ragtime.mid -o /tmp/rt_nodyn.flow --no-dynamics`
  → zero standalone dynamic tokens; `dotnet run --project flow-cli -- check /tmp/rt_nodyn.flow` → OK.
- `diff /tmp/rt_dyn.flow /tmp/rt_nodyn.flow` shows ONLY removed dynamic tokens
  (no note/duration/structure changes).
- flow-midi.Tests still pass (default path unchanged): `dotnet test flow-midi.Tests`.

## Commit

Single atomic commit on `dev`:
`feat(midi2flow): add --no-dynamics flag to suppress dynamic markings`
