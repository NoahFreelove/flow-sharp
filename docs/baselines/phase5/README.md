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
