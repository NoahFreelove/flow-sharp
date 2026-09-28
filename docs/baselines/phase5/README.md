# Phase 5 — initial slices, 2026-09-24

Phase 5 remains in progress. `48259fa` introduces detached evaluated score data;
`9fdb8ef` removes quadratic song-buffer assembly; `5017ab3` makes the render
cancellation fixture independent of finite-render speed. Documentation at `b3834d9`
records the new boundary and outstanding native snapshot render/export work.

## Verification

Final all-tier verification at `b3834d9`: **3,022 main + 21 MIDI tests passed**,
**19 skips**, **0 failures**, **0 tracked-content mutations**. Evidence is recorded in
[all-verification.json](all-verification.json). It includes the Desktop solution
build, corpus/audio compatibility checks, dependency gates and Web publish tests.
The run uses `MSBUILDDISABLENODEREUSE=1`: without it, idle build workers retained
output pipes in the existing Web-publish test helpers. No JavaScript adapter or
website bundle changes were made. The generated trimmed Web bundle also boots
under Node through the unchanged adapter: repeated fresh sessions, a located parse
error and music arithmetic (`150ms`) pass. See
[wasm-verification.json](wasm-verification.json). The existing missing Mono symbol
file warning did not affect execution.

The preceding run had one failure: the old cancellation fixture assumed that a
400-repeat render must take more than 300 ms. Linear assembly made that assumption
false. The revised fixture continuously renders small buffers until cancellation;
separate unit tests assert cancellation between bounded assembly chunks. All 17
cancellation/model tests passed before the final all-tier run. Earlier failure
logs remain under `/tmp/flow-phase5-slices-all`.

[allocation.json](allocation.json) records the deterministic allocation comparison:
128 repeats of 2,048 stereo frames, old prefix-copy algorithm versus linear assembly.
Both outputs match exactly. This isolates assembly allocation, not whole-render
peak memory or wall time. The test requires at least an eightfold reduction.

## Reproduce and limits

```sh
MSBUILDDISABLENODEREUSE=1 python3 scripts/ci/verify.py --tier all \
  --artifacts /tmp/flow-phase5-slices-final
dotnet test flow-lang.Tests/flow-lang.Tests.csproj \
  --filter FullyQualifiedName~MusicModel \
  --logger 'trx;LogFileName=model.trx' --results-directory /tmp/flow-phase5-model
node scripts/ci/wasm-session-smoke.mjs \
  "$PWD/flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/flow-runtime.js" \
  /tmp/flow-phase5-wasm.json
```

Run suites serially and do not edit tracked files during verification. Counts are
from the working checkout, including two ignored local Flow fixtures; no fresh-clone
gate is claimed. Hardware listening and remote CI were not performed. Full logs
and TRX files are in `/tmp/flow-phase5-slices-final`.

Snapshots preserve authored musical data but are not yet renderer/export inputs.
Compiler IDs are structural, score timing excludes audio tails, and the output
buffer is still contiguous. Shared transforms, explicit render contexts/options,
streaming/progress and native-host render/export remain open. See the
[boundary decision](../../decisions/2026-09-24-composition-snapshot-boundary.md).

## Native streaming renderer — `a6d720a`

The new `Flow.Audio` artifact references only `Flow.Music.Model` and the BCL.
It renders dry sine snapshots into borrowed stereo blocks with explicit options,
cancellation, delivered-frame progress and a per-section note budget. It shares
note-duration policy with the legacy bar renderer. Ten native-render tests cover
bit-exact legacy sine parity across tuplets, parallel voices, negative onsets,
ties/rests, overlap, sustain, pool stealing, tuning and section tempos; they also
cover Flow/native parity, block-size independence, repeat allocation, failures,
progress/cancellation and concurrent jobs. The broader targeted selection passed
156 tests before the full gate.

The native host constructs a score with repeats and two tempos and exports a valid
294,000-frame stereo PCM16 WAV at 44,100 Hz. Its loaded assembly list contains
neither language nor compatibility runtime; see [native-host.json](native-host.json).
The host-owned encoder saturates without dither and does not replace legacy WAV
export. The WAV header was independently decoded and the PCM checksum recorded.

Reproduce the native proof:

```sh
dotnet run --project scripts/MusicHost -- /tmp/flow-native-host.wav
MSBUILDDISABLENODEREUSE=1 python3 scripts/ci/verify.py --tier all \
  --artifacts /tmp/flow-phase5-native-all
```

The renderer retains note metadata for a section and reuses output-block storage
across repeats. It does not allocate PCM for the whole song or individual notes.
Nonzero reverb is explicitly rejected. Full instrument/effect routing, snapshot
MIDI export, shared editing transforms and legacy ambient-service migration remain
open; this evidence does **not** close Phase 5. No hardware playback, fresh-clone
or remote CI gate is claimed for this slice.

Final all-tier verification at `a6d720a`: **3,032 main + 21 MIDI tests passed**,
**19 skips**, **0 failures**, **0 tracked-content mutations**. See
[native-all-verification.json](native-all-verification.json). Generated Web
output also passes the Node smoke through the unchanged JavaScript adapter:
fresh repeated sessions, located parse errors and `150ms` music arithmetic. See
[native-wasm-verification.json](native-wasm-verification.json). The existing
missing Mono symbol-file warning did not affect execution.

## Snapshot MIDI export — `3a69b3a`, `c419c14`

`Flow.Music.IO` references only `Flow.Music.Model`, DryWetMidi and the BCL. It
writes snapshots as Standard MIDI Files and owns the GM routing/key tables that
legacy MIDI, MusicXML, LilyPond and `midiOut` now delegate to. Nine tests cover
the artifact boundary, byte-identical output versus legacy `writeMidi` for three
Flow corpora (including TPQN elevation to 3,360), native readback of tracks,
routing, tempo/meter/key maps and controllers, rounding/clamping, and failures
before any byte is written. Intentional score-timing divergences from legacy are
characterized in the tests and listed in the boundary decision. An independent
review found no legacy regression; its note-off rounding finding was fixed
test-first in `c419c14`, together with meter and 28-bit delta validation.

The native host writes the same WAV plus a 155-byte format-1 SMF (480 TPQN, two
tracks, stable SHA-256 across runs) with no language or compatibility assembly
loaded; Flow's `midi2flow` reads it back. See [midi-native-host.json](midi-native-host.json).

Final all-tier verification at `c419c14`: **3,041 main + 21 MIDI tests passed**,
**19 skips**, **0 failures**, **0 tracked-content mutations**
([midi-all-verification.json](midi-all-verification.json)). The regenerated Web
bundle includes `Flow.Music.IO.wasm` and passes the unchanged JavaScript adapter
smoke ([midi-wasm-verification.json](midi-wasm-verification.json)). Legacy
`writeMidi` output is unchanged; rerouting it through snapshots, instrument/effect
rendering and shared editing transforms remain open. Phase 5 is not closed.

## `writeMidi` through snapshots — `ac16d0f`, `3772d92`

By owner decision, harpsichord routes to GM 6 and Flow `writeMidi` writes
through the snapshot exporter. Integer-tick corpora keep their previous bytes
(SHA-256 recorded before the old walk was removed). All-tier verification at
`3772d92`: **3,045 main + 21 MIDI passed**, **19 skips**, **0 failures**, **0
tracked-content mutations** ([writemidi-all-verification.json](writemidi-all-verification.json));
Web smoke passes ([writemidi-wasm-verification.json](writemidi-wasm-verification.json)).
