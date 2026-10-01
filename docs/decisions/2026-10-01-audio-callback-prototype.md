# Linux callback prototype and preliminary isolation evidence

Status: prototype choice recorded; managed/native strategy remains undecided.
Date: 2026-10-01.

Use a separate `Flow.Platform.Linux` assembly for an opt-in PortAudio v19 callback
adapter. The installed `libportaudio.so.2` exposes the callback path needed for
measurement; existing PulseAudio Simple playback is blocking/full-buffer oriented.
Keep Flow.Audio free of native dependencies and leave compatibility playback and
the frozen browser API unchanged. No native dependency is bundled or new NuGet
introduced. Packaging and broader device portability are later work.

Bindings follow the [PortAudio v19 API](https://portaudio.com/docs/v19-doxydocs/portaudio_8h.html)
and [callback restrictions](https://portaudio.com/docs/v19-doxydocs/writing_a_callback.html).
Open/start/close/enumeration happen on a serialized control thread. The callback
only renders into native stereo memory and updates preallocated telemetry. A
GCHandle roots its delegate until native close returns; explicit disposal is
required. Exceptions produce silence and abort without crossing the C boundary.

Four 20-second runs on the declared reference machine found no reported underflows
or managed-body deadline misses. At 48 kHz/256 frames, moving Flow/asset/forced-GC
load into a child process reduced the observed steady maximum body time from
2.526 to 0.459 ms and maximum entry gap from 9.919 to 5.753 ms. This is preliminary
support for process-isolated evaluation, not proof of real-time safety or causality
across machines. Runs are single samples, not a controlled repeated study.

The 128-frame isolated run had a 1.883 ms startup callback, exceeding 70% of the
2.667 ms block budget once. Steady maximum was 0.140 ms, but entry gaps reached
5.681 ms; callback batching/scheduling requires investigation. Preserve startup
results rather than declaring the warm profile a complete pass. Native output
latency was reported as zero and is treated as unavailable. Muted output does not
certify audible correctness, hardware latency or absence of every downstream glitch.

Before broadly porting DSP: run the 30-minute reference workload, investigate
startup/GC/scheduling and device-loss behavior, and compare an isolation/native
alternative as needed. UI stress is deferred with the desktop shell. No general
processor/plugin lifecycle or final managed/native decision is implied.

Evidence: `docs/baselines/phase6/callback-*.json` and the Phase 6 baseline README.

## Sustained evidence update

The owner-requested 30-minute isolated-load run completed at 48 kHz/256 frames:
337,508 callbacks, worst managed body 2.085725 ms (39.11% of the deadline), zero
body misses/headroom overruns, reported underflows, body allocations, parent GC
collections or lost timing samples. This strengthens the case for managed audio
with isolated evaluation on this configuration. It does not settle the final
strategy: entry gaps reached 15.686382 ms, reported latency is unavailable, and
UI/listening/device-recovery evidence remains open. Investigate native callback
cadence/buffering and 128-frame startup before broad DSP migration. Do not label
the entire Phase 6 gate complete on the strength of this body-time result.

See `callback-isolated-256-30min.json` and `callback-stress-assessment.json` in the
Phase 6 baseline directory. Frequent mid-score seeks mean this run does not
establish sustained loop-end/EOF behavior; those still have unit-test coverage.
