# Selected WAV export — 2026-10-04

## Pre-change core checkpoint

`/tmp/flow-backend-authoring-core/verification.json`: Desktop solution build passed;
3494 main tests and 21 MIDI tests passed (**3515 total passed**, 14 skipped, zero
failures), no tracked content mutations. This checkpoint includes tuning, embedded
styles and formant generation, and predates range export below.

## Implemented

`ProjectExportRange` captures a nonempty half-open range of output frames. Its
FromQuarters helper uses the complete tempo map and rounds boundaries consistently;
sub-frame selections collapsing to zero are rejected. The frame range is interpreted
at the selected export sample rate, not at a source asset's sample rate.

`ProjectBounceFile.ExportRangeAsync` retains captured snapshot/asset validation,
temporary-file publication, opt-in overwrite and cancellation behavior. It rejects
selections extending beyond prepared playback before creating temporary output.
The returned Frames counts written frames. Full ExportAsync remains unchanged.

`PlaybackWaveWriter.WriteRange` continuously renders from zero, discards preroll,
and writes only [start,end). It does not seek past delay/filter/envelope history.
Progress reports all processed frames including preroll, with end as total. This
keeps memory bounded to blocks but makes a late selection cost its preroll time.

End semantics are explicit: an exact crop of full project playback, hard-cut at
the selected end. Effects originating before the start remain audible. Select
through the prepared tail region to retain tails; this does not synthesize extra
post-selection tails or omit later-arrangement events when extending a selection.
Full bounce continues to include all prepared tails. No document or live transport
mutation is performed; export selection is a host IO option, not musical processing.

## Validation

**10 passed**, zero failures/skips, `/tmp/flow-range-export.log`:
range and full bounce, WAV writer, render preferences and combined lifecycle.
An unaligned selection matches the exact full-mix sample slice with delay and
automation. Cancellation during preroll preserves an existing destination and
cleans temporary output. Out-of-bounds range also preserves it. Quarter conversion
crosses a tempo change; invalid/collapsed selections fail.

## Continue

Remaining MVP findings from `2026-10-04-authoring-and-mvp-audit.md`: audio clip
gain/fades with shared Flow semantics; metronome/count-in; explicit note clipboard
and grouped gesture verification. Then reconcile old roadmap/audit status, provide
the explicit captured-sample authoring example and run final browser/WASM/core
qualification as appropriate. Native frontend and historical callback-gap work
remain deferred; hardware/latency qualification remains separately open.
