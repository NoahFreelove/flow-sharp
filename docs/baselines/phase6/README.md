# Phase 6 verification — prepared playback

Date: 2026-10-01. This verifies the initial backend slice, not the full Phase 6 gate.

- Focused music-model tests: 67 passed.
- Desktop solution build and all-tier tests: 3,079 main + 21 MIDI passed,
  19 prerequisite skips, zero failures and zero tracked-content mutations.
- Generated Web bundle: fresh sessions, located parse errors and music arithmetic
  pass through the unchanged JavaScript adapter.
- Legacy sine bit parity includes tuning, ties, pedal, parallel voices and stealing.
  Prepared read/seek/reset and boundary crossings allocate zero bytes after warmup.
- Device timing, underruns, managed/native decision, full DSP and UI remain open.

Reproduce:

```sh
MSBUILDDISABLENODEREUSE=1 python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-phase6-prepared
node scripts/ci/wasm-session-smoke.mjs "$PWD/flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/flow-runtime.js" /tmp/flow-phase6-wasm.json
```

Summaries are adjacent JSON files; raw logs/TRX are in the temporary artifact path.
Local allocation tests do not establish hard real-time safety or immunity to GC.

## Frame transport slice

2026-10-01: `PreparedSineTransport` adds single-owner play/pause/stop/seek/loop.
Fourteen new cases preserve reference samples and require zero warmed allocation.
The all-tier gate passed **3,093 main + 21 MIDI**, with 19 prerequisite skips,
zero failures and zero tracked-file mutations. Web adapter smoke also passed.
The adjacent `transport-all-verification.json` and `transport-wasm-verification.json`
record the results; raw logs/TRX are at `/tmp/flow-phase6-transport/`.

Reproduce with the commands above using `--artifacts /tmp/flow-phase6-transport`
and `/tmp/flow-phase6-transport-wasm.json` for the smoke output. This slice does
not establish device deadlines or cross-thread transport safety.
