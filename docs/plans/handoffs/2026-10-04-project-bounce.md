# Captured project bounce — 2026-10-04

`ProjectBounceFile.ExportAsync` adds the first complete project-to-WAV backend
path. It captures an immutable project snapshot, resolves and verifies external
assets with the existing project resolver, prepares a fresh `ProjectCompiler`
playback, and streams its output through `PlaybackWaveWriter`. It neither evaluates
saved Flow source nor touches a live playback cursor/device or document history.
Instruments, routing, automation, effect chains and prepared tails therefore follow
the same implementation as playback.

Output is stereo IEEE float32 RIFF/WAVE with a fact chunk. Float values above unity
are retained; there is no implicit normalization/clipping. Nonfinite samples fail.
The writer uses bounded block storage rather than retaining a whole-song PCM buffer.
It validates size before writing a header and rejects exports beyond the 32-bit
RIFF size limit; RF64 and integer PCM/dither options are not implemented. Full-project
rendering begins at frame zero; selected ranges are not yet exposed.

The host writes a unique temporary file beside the destination, flushes to disk,
checks cancellation and publishes with a final rename. Overwrite is opt-in. Failure
or cancellation removes owned temporary output without replacing an existing file.
Missing referenced clip media fails instead of silently publishing an incomplete
mix; other playback diagnostics are returned. Unused unresolved assets can remain
diagnostic-only. Normalized project asset paths are protected from direct overwrite.
The directory must already exist. No project or global settings are changed.

The caller owns and awaits the returned task and cancellation token. Optional frame
progress executes on the export worker; a UI must marshal updates to its owner.
This API does not create a hidden background scheduler or serialize multiple calls.
Preparatory asset decoding still obeys existing memory limits; streaming export does
not add streaming sample import. The snapshot stays fixed even if the document is
edited while export is running.

## Verification and next work

Initial writer/bounce focused suite: **5 passed**, `/tmp/flow-project-bounce-tests.log`.
Final affected backend regression: **491 passed**, zero failures/skips,
`/tmp/flow-project-bounce-regression.log`. `git diff --check` passed.
Tests compare exported PCM with actual project
playback, including gain automation and delay tails, and cover exact float roundtrip,
partial final blocks, RIFF bounds, nonfinite data, missing media, overwrite refusal,
mid-render cancellation and cleanup. No device qualification was performed.

Continue with stems: define their processing point and common timeline explicitly.
Arbitrary nonlinear shared/master graphs mean independently processed stems need
not sum to the full mix. Do not promise additive reconstruction for those graphs.
Exporting a batch needs bounded work, cancellation and coherent publication of the
completed set. Then finish host autosave/recovery and the backend readiness audit.
The overall goal remains active; native JUI and historical callback-gap diagnosis
remain deferred.
