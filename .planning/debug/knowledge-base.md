# GSD Debug Knowledge Base

Resolved debug sessions. Used by `gsd-debugger` to surface known-pattern hypotheses at the start of new investigations.

---

## chord-dynamic-bar-doubling — dynamic+chord note-stream bar renders at ~2x duration
- **Date:** 2026-06-25
- **Error patterns:** note stream, dynamic marking, chord, IsChordTone, InterpolateVelocities, MusicalNoteData reconstruction, bar doubling, beat cursor, ToTimeline, velocity interpolation, drunk pianist drift, midi2flow
- **Root cause:** `NoteStreamCompiler.InterpolateVelocities` rebuilt each interpolated middle note with the 12-arg `MusicalNoteData` positional constructor, silently resetting the 5 trailing fields (IsChordTone, DurationFraction, OnsetOffset, DurationOverlap, PortamentoMs) to defaults. Dropping IsChordTone made interpolated chord tones advance the bar's beat cursor in `BarType.ToTimeline` instead of sharing the lead onset, so any bar mixing a dynamic (≥2 distinct velocities → interpolation runs) with chords overran (doubled).
- **Fix:** Rebuild via `notes[i].With(velocity: vel)` instead of the positional ctor — overrides only velocity and passes every other field through by null-coalesce, preserving IsChordTone. One edit covers both call sites (main note-stream path + per-voice CompileVoiceBlock).
- **Files changed:** flow-lang/Runtime/NoteStreamCompiler.cs, flow-lang.Tests/Integration/Debug2026/ChordDynamicBarDoublingTests.cs
- **Bug class / lesson:** Any per-bar `MusicalNoteData` reconstruction MUST use `MusicalNoteData.With(...)` rather than the positional constructor, to avoid silently dropping IsChordTone / DurationFraction / OnsetOffset / DurationOverlap / PortamentoMs. The 2026-06-09 transform audit fixed this drop-on-reconstruct pattern elsewhere (NoteType.cs With() docs) but missed `InterpolateVelocities`. When auditing for this pattern, grep for `new MusicalNoteData(` outside the With() builder itself.
---
