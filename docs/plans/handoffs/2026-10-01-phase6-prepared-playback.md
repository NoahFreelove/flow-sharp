# Phase 6 — prepared playback foundation, 2026-10-01

Phases 0–5 are complete. Phase 6 backend work is in progress; finish the audio
backend and Flow plugins before UI/DAW work, per owner direction. Use the roadmap,
progress ledger and `docs/TESTING.md` directly. Commit verified slices; do not push.

## Delivered

`Flow.Audio.PreparedSinePlayback.Prepare(snapshot, options, budgets, cancellation)`
prepares note metadata before publication, with global note/placement limits.
Repeated section objects share metadata, repeats remain compact, no PCM is cached.
`Read(Span<float>)` writes interleaved stereo into host-owned memory, advances a
frame cursor and zero-fills past EOF. `Seek(frame)` and `Reset()` need no preroll
for dry sine; they work inside held/stolen notes and across repeats/tempo changes.

Valid reads/seeks/resets allocate no managed memory. The host must serialize all
cursor operations; there are no internal locks, device calls or interpreter work.
Managed metadata owns no external resources and has no disposal operation.
Sample rate and block limit are fixed at preparation; reprepare for a new rate.

The offline renderer and prepared playback share voice preparation and sample
math, preserving legacy bits. Offline rendering retains its one-section-at-a-time
memory policy. Prepared playback scans source-order voices in the current section
per block; this is bounded by configured notes but not yet tuned for dense scores.

## Verification

67/67 focused music-model cases. Full all-tier gate: 3,079 main + 21 MIDI passed,
19 prerequisite skips, zero failures or tracked-file mutations. Generated Web
bundle passes the unchanged JS adapter smoke. `docs/baselines/phase6/` contains
summaries; raw logs/TRX are at `/tmp/flow-phase6-prepared/`.

## Next ready slice

Add a single-owner transport over the prepared source: play/pause/stop, seek,
loop ranges and exact boundary/EOF behavior. Keep its frame clock independent of
UI timers. Then introduce bounded host commands and graph publication/retirement,
and a callback-capable Linux device prototype with deadline measurements under
background Flow/GC load. Decide managed/native strategy before broad DSP porting.

Still open: general processor lifecycle, parameters, smoothing, sampler/effects,
real-time device evidence and the Phase 7 declarative plugin graph/authoring API.
No audible hardware validation or real-time deadline guarantee is claimed here.
The desktop shell remains deferred. The owner's action-owned Undo/Redo interface
is specified in roadmap §9.3 for the later UI-independent project model.

Full verification must run serially with `MSBUILDDISABLENODEREUSE=1`; do not edit
tracked files while the verifier hashes them. Restore the Desktop target before
Desktop tests if a Web publish changed restore assets. Preserve frozen browser
JavaScript and do not incidentally update the website bundle.
