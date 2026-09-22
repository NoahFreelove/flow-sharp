# Phase 0 restructuring baseline

Captured 2026-09-20 on Ubuntu 26.04 x64, .NET SDK 10.0.112 / runtime 10.0.12,
Intel Core i7-11700K (8 cores, 16 logical processors). Release configuration.
The source inventory records hashes of the measured production source; the
starting Git revision is `8135359` with Phase 0 working-tree changes. No user
style packs were present under `~/.config/flow/styles` on the reference machine.

## Reproduce

```sh
dotnet run --project scripts/BaselineProbe -c Release -- /tmp/flow-baseline
python3 scripts/baseline/inventory.py --output /tmp/flow-baseline/source-inventory.json
```

The probe references the existing compatibility host and reflects the five
production assemblies. It does not select a future language-only profile.
Run it alone: builds, tests, and audio workloads change the measurements.
Keep raw output outside the repository; copying it here is an explicit,
reviewed baseline update. There are no CI performance thresholds yet.

- `measurements.json`: all raw timing, process-wide managed allocation, managed
  heap, working-set samples, exact Flow workloads, and render PCM SHA-256s.
- `public-api.json`: declared public members of all 402 visible types in the
  five production assemblies. This is an inventory, not a claim that every
  current public type should remain a supported API.
- `static-fields.json`: 789 non-constant static fields, including private,
  readonly-reference, auto-property, and compiler-generated fields. Metadata
  is inspected without reading values or triggering their static initializers.
  This is a candidate list, not 789 confirmed mutable-state defects.
- `source-inventory.json`: source hashes; 10 projects including the new probe;
  conditional project/package/resource declarations; restored dependency
  closure; 18 module/style sources with import/procedure declarations;
  native interop call sites; asset sizes and SHA-256s. Assets total 3,859,887
  bytes across 38 files (samples, licenses, shipped style files).

## Observed measurements

One warmup, five samples, median below; first engine has one sample and no
warmup. Explicit collections precede each sample. The first engine measurement
runs after metadata reflection has loaded assemblies: it includes engine
initialization/disposal, not cold process launch. Allocation is a process-wide
managed delta, including minor harness overhead. Heap/working-set samples are
observational; peak working set is cumulative across this process and must not
be interpreted as an isolated per-workload peak.

| Workload | Median time (ms) | Median allocated (MiB) |
| --- | ---: | ---: |
| First engine create/dispose | 100.959 | 1.902 |
| Warm engine create/dispose | 2.881 | 1.816 |
| Lex + parse 1,000 declarations | 5.403 | 4.484 |
| Evaluate pre-parsed map/reduce over 10,000 integers | 60.776 | 16.467 |
| Render 1 section, 2 seconds of sine audio | 1.520 | 1.013 |
| Render 8 repeated sections, 16 seconds | 16.560 | 31.643 |
| Render 32 repeated sections, 64 seconds | 133.068 | 387.027 |

Evaluation excludes parsing and engine initialization and checks result
99,990,000. Rendering excludes composition evaluation, uses 44.1 kHz stereo
float PCM, and checks nonempty finite output. Final 32-section PCM is about
21.5 MiB; process peak working set reached about 398.7 MiB. The allocation
increase is consistent with the existing repeated-prefix buffer copying in
`SongRenderer`; it is a baseline for Phase 5, not a newly introduced failure.

These are offline measurements on one machine with ordinary OS scheduling and
tiered JIT. They establish neither callback deadlines nor audible-device
correctness. Phase 6 still needs real-time and hardware measurements. The
existing `bench/run.sh` remains useful for end-to-end interpreter timings;
its historical results are not substituted for the measurements above.
