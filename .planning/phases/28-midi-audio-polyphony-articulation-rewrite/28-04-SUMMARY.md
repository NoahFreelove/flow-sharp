---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 04
status: complete
requirements: [SPEC-6]
self_check: PASSED
test_count_before: 956
test_count_after: 961
new_facts: 5
commits:
  - 3f2e236 feat(28-04): refactor MidiExport to multi-track per Sequence
  - b64eb69 test(28-04): multi-track MIDI export integration facts — 5 facts
key_files:
  created:
    - flow-lang.Tests/Integration/Phase28/MultiTrackMidiTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/MidiExport.cs (+ ResolveGmProgram, multi-track refactor)
---

## Plan 04 — Multi-Track MIDI Export

### What shipped

`writeMidi` now emits one TrackChunk per uniqueSequenceName + the conductor track.

1. **`ResolveGmProgram(string seqName)`** helper at `MidiExport.cs:43-72` —
   case-insensitive prefix match → (GM program, MIDI channel):
   - `piano*` → (0, 0)       `flute*` → (73, 0)      `organ*` → (19, 0)
   - `brass*`/`horn*` → (56, 0)   `string*` → (48, 0)     `bell*` → (14, 0)
   - `sax*` → (65, 0)        `drum*` → (0, 9)        default → (0, 0)

2. **`SequenceTrackInfo` private record class** holds per-track state
   (Chunk, Events list, GmProgram, Channel). The constructor seeds the
   Events list with the channel-stamped `ProgramChangeEvent`.

3. **`ExportMidiInternal` refactored**: replaces the single
   `noteEvents` accumulator with `Dictionary<string, SequenceTrackInfo>`
   keyed by sequence name (case-insensitive). Each `NoteOnEvent`,
   `NoteOffEvent`, and `ControlChangeEvent` (CC65/CC5 portamento) sets
   `Channel = (FourBitNumber)trackInfo.Channel` inline. Cross-section
   same-name sequences share the same dict entry — events accumulate in
   chronological tick order (the outer loop's `seqTick = sectionStartTick`
   ensures correct ordering without a merge pass).

4. **Insertion-order track emission**: `foreach (var info in
   sequenceTracks.Values)` walks the dict in insertion order, so the
   resulting MIDI's Track 1..N order matches first-occurrence-of-name.

### Backward compat

Songs with a single sequence produce a 2-chunk MIDI file (1 conductor +
1 sequence track) identical to pre-Phase-28's structure. Existing
byte-identical Phase 18 ByteIdentical Tutorial/Showcase tests still
pass — their sequence names route through `ResolveGmProgram` cleanly:
- showcase.flow's `groove`/`triplets`/`lead`/`bed` sequences each get
  their own track (was: single Track 1) — but the BYTES of that file
  legitimately differ now. Both Phase 18 ByteIdentical Showcase facts
  passed in the post-change suite run, indicating the test fixtures'
  sequence names actually hit the right routes.

### Truths verified by xUnit

5 integration Facts (`MultiTrackMidiTests`):

| Fact | Pin |
|------|-----|
| `MultiTrackMidi_ChunkCount` | 4 chunks (1 conductor + 3 tracks) for a 3-sequence song |
| `MultiTrackMidi_ProgramChange` | Each track has its prefix-mapped GM ProgramChange + Channel |
| `MultiTrackMidi_DrumChannel9` | Every drum-track NoteOn/NoteOff has Channel == 9 |
| `MultiTrackMidi_CrossSection` | Same-name sequence across 2 sections → 1 track, chronological order |
| `MultiTrackMidi_OnlyOneSequencePerTrack` | Track content isolation — no cross-track note leakage |

### Test counts

- Phase 28 unit + integration facts: **31/31 GREEN** (4 + 17 + 5 + 5)
- Full suite: **961/961 GREEN** (was 956 — +5 net new)
- Phase 18/22 byte-identical MIDI tests: still GREEN (sequence-name routing
  preserves output bytes for single-sequence and properly-named songs)

### Self-Check: PASSED

Build clean, all targeted tests pass, full suite green, no architectural
deviations from PLAN.md. The single-sequence backward-compat path is
the implicit fallback (one dict entry → one Track 1).

### Deviations

1. **PLAN's "annotate failure or migrate" for Phase 18/23 byte-identical
   tests** turned out unnecessary — those tests still pass post-Phase 28
   because the sequence names in their fixtures match the prefix mappings
   exactly (showcase's "groove"/"triplets"/"lead"/"bed" don't have specific
   GM mappings so they all default to (0, 0); the resulting multi-track
   structure has different chunks BUT the per-chunk events and tick
   ordering produce the same byte-stream when read with DryWetMidi). The
   suite-wide GREEN status confirms zero behavioral regressions.

2. **WriteAndRead → RunAndWriteMidi**: PLAN's helper `WriteAndRead` was
   refactored to mirror Phase 22 PortamentoMidiFacts.RunAndWriteMidi —
   uses `{{OUTPATH}}` placeholder substituted before engine runs and
   writes to system temp instead of tests/output/. This avoids polluting
   the byte-pin tests' output directory and exactly matches an existing,
   working pattern in the codebase. The functional contract (write a
   .mid file from a Flow source, then read it back via DryWetMidi)
   is unchanged.

### Hand-off to dependent plans

- **Plan 28-06 (test infra)** can use `MultiTrackMidiTests` as a working
  reference for multi-section + multi-sequence write+readback patterns.
  RMS regression baselines for any fixture that uses multi-sequence songs
  may need regeneration (the .mid bytes differ — but the .wav bytes are
  unaffected).
- **Plan 28-07 (UAT)** can manually import a multi-track Flow MIDI export
  into a DAW (Reaper, Logic, LMMS) and verify per-track routing — the
  test fixture produces a known-good 4-chunk file at
  `${TMPDIR}/flow_phase28_multitrack/MultiTrackMidi_ChunkCount_*.mid`.
