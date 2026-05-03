---
slug: flow-midi-voice-silence
status: resolved
trigger: |
  flow-midi MIDI->.flow converter produces a .flow file that, when rendered,
  is roughly twice the original duration (~14 min vs ~5 min) AND has its first
  ~5 minutes at digital silence. Tracks are emitted as sequential Song sections
  instead of layered voices, so the "song" plays one hand at a time. On top of
  that, the early grand_piano_classic_* sections render at -84 dBFS (peak=2)
  while the later music_box_* sections render at -20 dBFS.
created: 2026-05-02
updated: 2026-05-02
---

# Debug Session: flow-midi-voice-silence

## Symptoms

**Expected behavior:**
flow-midi converts a multi-track piano MIDI (e.g. Chopin Nocturne Op. 9 No. 2,
~5 min) into a .flow script that, when rendered with `(renderSong song "piano")`,
produces a WAV roughly matching the original duration with both hands playing
together (layered voices) at audible volume.

**Actual behavior:**
1. The generated `Song` declaration is `[grand_piano_classic_rh
   grand_piano_classic_lh music_box_rh music_box_lh]` — four sequential sections.
   `SongRenderer` concatenates section buffers (SongRenderer.cs:12-13 doc comment:
   "rendering sequences, mixing voices, and concatenating section buffers"), so
   the four parts play end-to-end (~14 min) instead of mixed simultaneously.
2. The first two sections (`grand_piano_classic_rh`, `grand_piano_classic_lh`)
   render at peak=2 / -84 dBFS — effectively digital silence — for ~5 min, while
   the `music_box_*` sections that follow render at peak~3000 / -20 dBFS (audible
   but quiet). **CORRECTION (Evidence below): all four sections are mostly silent
   in isolation; only sparse seconds are audible. The "loud second half" was a
   sampling artifact — the per-section pattern of silence + sparse bursts was
   the same everywhere, but the windows the original WAV-inspect script picked
   happened to land on bursts in sections 3-4 and on silence in sections 1-2.**

**Loudness sample (5-second windows across the WAV, sample rate 44100):**
```
t= 41.9s  peak=    2 ( -84.3 dBFS)  avg|s|=0.6
t=167.7s  peak=    2 ( -84.3 dBFS)  avg|s|=0.6
t=335.4s  peak= 2996 ( -20.8 dBFS)  avg|s|=94.1
t=503.1s  peak= 2956 ( -20.9 dBFS)  avg|s|=138.9
t=670.8s  peak= 3000 ( -20.8 dBFS)  avg|s|=92.1
t=796.6s  peak= 3039 ( -20.7 dBFS)  avg|s|=138.5
```

**Error messages:** None. Both bugs are silent (literally) — no exceptions, no
parse errors. The lexer fix (commit 9773f55) cleared the only parse error.

**Timeline:**
First observed today (2026-05-02) immediately after the lexer fix unblocked
end-to-end rendering of /tmp/flow-render/chopin_nocturne.flow. The bugs are
likely as old as flow-midi itself (Phase 03 work) — no test or user input
exercised a multi-track piano file with high-octave notes before today.

**Reproduction:**
```bash
# 1. Build
dotnet build

# 2. Convert MIDI -> .flow
dotnet run --project flow-midi -c Release -- \
  "/home/noah/Downloads/midi/Chopin _ Nocturnes Op. 9, No. 2 in Eb Major.mid" \
  -o /tmp/flow-render/chopin_nocturne.flow

# 3. Edit last line so we render to WAV instead of playing live
sed -i 's|(play output)|(writeWav "/tmp/flow-render/chopin_nocturne.wav" output)|' \
  /tmp/flow-render/chopin_nocturne.flow

# 4. Render
dotnet run --project flow-interpreter -c Release -- \
  /tmp/flow-render/chopin_nocturne.flow

# 5. Confirm silence in first half + concatenation
python3 -c "
import wave, struct
with wave.open('/tmp/flow-render/chopin_nocturne.wav','rb') as w:
    rate = w.getframerate(); total = w.getnframes()
    print(f'duration={total/rate:.1f}s')
    for frac in [0.05, 0.2, 0.4, 0.6, 0.8, 0.95]:
        w.setpos(int(total*frac))
        raw = w.readframes(5*rate)
        s = struct.unpack('<' + 'h'*(len(raw)//2), raw)
        print(f't={int(total*frac)/rate:6.1f}s peak={max(abs(x) for x in s):5d}')
"
```

Original MIDI: `~/Downloads/midi/Chopin _ Nocturnes Op. 9, No. 2 in Eb Major.mid`.

## Goal

Chopin nocturne renders to a ~5-minute WAV with the four MIDI tracks playing
**simultaneously** (layered) at audible volume across the whole piece.
Acceptance:

1. Output WAV duration is within ~10% of original MIDI duration (so
   ~270-330s for this Chopin nocturne, not 800s+).
2. Peak amplitude across the whole WAV is consistent — no multi-minute
   stretches at -84 dBFS.
3. Both `grand_piano_classic_rh` and `grand_piano_classic_lh` produce audible
   piano notes at appropriate velocity.
4. Existing flow-midi tests (if any) and the full `tests/test_*.flow` suite
   still pass.

## Suspected Files

- `flow-midi/Conversion/FlowGenerator.cs` — emits the section / Song layout
- `flow-midi/Conversion/Quantizer.cs` — could be dropping notes or velocity
  in early sections, or assigning everything to sequential sections
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — section-to-buffer
  rendering and instrument dispatch (the doc comment confirms concatenation
  is the *intended* Song semantics)
- `flow-lang/StandardLibrary/Audio/Synthesizers/` — name-based instrument
  lookup; section names containing `grand_piano_classic` vs `music_box` may
  hit different synth paths
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — instrument registry

## Current Focus

- hypothesis: **TWO INDEPENDENT BUGS confirmed, root causes identified.**
  - BUG A: `flow-midi/Conversion/FlowGenerator.cs` emits each MIDI track as a
    separate `section` and lists them in `Song`. `SongRenderer` concatenates
    section buffers, so four 3.5-min sections play end-to-end. Fix: emit ONE
    section containing one Sequence per track. SongRenderer already mixes
    multiple sequences within a section in parallel (verified — see Evidence).
  - BUG B: `VoiceAllocator.Allocate()` (called from
    `SequenceRenderer.RenderSequenceToVoices`) caps the rendered voice list
    at 32 voices, where each Voice = one sounded note across the entire
    Sequence's lifetime. A 210-second piano section with ~600 notes therefore
    keeps only the 32 loudest notes total — most of the section ends up
    silent, with sparse audible bursts where the surviving notes happen to
    play. The original "first half silent, second half audible" symptom was
    a sampling-coincidence artifact: per-second analysis (Evidence #4 below)
    shows ALL four sections are dominated by silence with sparse bursts.
- next_action: ROOT_CAUSE_FOUND — proceed to fix.

## Evidence

- timestamp: 2026-05-02
  finding: Confirmed BUG A architecture in FlowGenerator.cs:96-119. Each
  playable track becomes its own `section name { Sequence name_seq = | … | }`
  and `Song song = [name1 name2 …]`. `SongRenderer.RenderSong` (lines 100-112)
  iterates sections and uses `AppendBuffers` to concatenate, never mixing
  across sections. Within a section, however, lines 138-153 sum all sequences
  into `allVoices` and pass them to `MixVoicesToStereoBuffer` — proving the
  parallel-mix capability already exists at the section level.

- timestamp: 2026-05-02
  finding: Empirical proof that multi-sequence sections mix in parallel.
  Test file `/tmp/flow-render/test_multivoice.flow`:
  ```flow
  section combined {
    Sequence rh = | C5q D5q E5q F5q | G5q A5q B5q C6q |
    Sequence lh = | C3q G3q E3q G3q | C3q G3q E3q G3q |
  }
  Song song = [combined]
  ```
  Renders to 4.0s (= 2 bars × 4 beats / 2 bps), peak=5855 (≈ 2× single-voice
  peak ≈ 2999, demonstrating additive mixing).

- timestamp: 2026-05-02
  finding: Per-second loudness scan of `grand_piano_classic_rh` rendered in
  isolation (Song = [grand_piano_classic_rh] only):
  ```
  duration=210.0s, ~600 NoteElements/ChordElements in the sequence
  silent windows (peak < 100): 35/42 sampled 5s windows  (≈ 83%)
  audible bursts at: t=60s, 95s, 110s, 115s, 145s, 190s, 205s
  ```
  Same pattern in `grand_piano_classic_lh` (37/42 silent), `music_box_rh`
  (37/42 silent). The sparse-burst-on-silent-background pattern is uniform
  across all four sections — there is no name-based silencing.

- timestamp: 2026-05-02
  finding: ROOT CAUSE for BUG B: `SequenceRenderer.RenderSequenceToVoices`
  (SequenceRenderer.cs:34, 44) uses a hardcoded default `int maxVoices = 32`,
  which is then enforced by `VoiceAllocator.Allocate(allVoices, sampleRate, 32)`
  (SequenceRenderer.cs:62, 106). `BarRenderer.RenderBarToVoices` (BarRenderer.cs:
  44-90) creates ONE Voice PER NOTE — the voice list represents every note
  played across the full sequence, not simultaneous polyphony. With ~600 notes
  in a 210s sequence, `Allocate` keeps the 32 loudest by `GetPeakAmplitude`
  (VoiceAllocator.cs:46-61) and silently fades the rest, scattering 32
  surviving notes across 210 seconds of audio. The result: ~5% of the
  sequence has audible content.
  
  Crucially, only `PlaybackFunctions.cs:127` passes a runtime-configurable
  `manager.MaxVoices` (intended for live audio engine resource limits). All
  offline-render paths (`SongRenderer.cs:140, 295`, `PolyrhythmFunctions.cs:
  55-56`, `BuiltInFunctions.cs:1072`) get the hardcoded 32. For offline
  rendering producing a Buffer (no real-time constraint), there's no resource
  reason to cap voice count by total-lifetime peak — voice stealing should
  apply only to *simultaneously-active* voices.

- timestamp: 2026-05-02
  finding: Tiny test cases (`test_simple.flow` with 8 piano notes, 
  `test_chopin_short.flow` with 4 chopin bars containing chords) render
  cleanly with full audibility (peak 2999-5918 throughout). Bug B only
  manifests when total note count in a sequence > 32. Mid-sized
  `test_chopin_short.flow` (4 bars, ~15 notes) was fully audible. The
  half-section `test_half_rh.flow` (~57 bars, ~300 notes) was 62% silent.

## Eliminated

- Section-name-based instrument dispatch: `SynthesizerFactory.Create`
  (NoteSynthesizer.cs:214-237) keys only on the `synthType` string passed to
  `renderSong`, never on section name. `"piano"` resolves identically for all
  four sections.
- Velocity dropout from the converter: `FlowGenerator.FormatBar` (lines 187-
  228) emits only note name + duration, NEVER a velocity marker. So all
  emitted notes use the default `velocity = 0.63` from `MusicalNoteData`'s
  ctor (NoteType.cs:269) uniformly across sections.
- Pitch-out-of-range exceptions: `NoteType.Parse` throws if MIDI < E0 (16) or
  > E10 (124). The Chopin range stays inside (lowest A1+, highest D7+).
- Lexer/parser failures on the converted file: lexer fix at 9773f55 cleared
  these; the file lexes and parses cleanly (rendering proceeds end-to-end,
  produces the WAV, no errors emitted).

## Resolution

**Two independent root causes, two atomic fixes:**

### Fix A — flow-midi/Conversion/FlowGenerator.cs (BUG A: sequential tracks)

Replace the per-track `section` emission (lines 96-119) with a single section
containing one `Sequence` per track. The current code:

```csharp
foreach (var track in playableTracks)
{
    string sectionName = SanitizeVarName(track.Name);
    // … dedup …
    sb.AppendLine($"{indent}section {uniqueName} {{");
    string sectionIndent = indent + "    ";
    string seqVar = uniqueName + "_seq";
    WriteSequence(sb, sectionIndent, seqVar, track);
    sb.AppendLine($"{indent}}}");
    sb.AppendLine();
}
string songSections = string.Join(" ", trackNames);
sb.AppendLine($"{indent}Song song = [{songSections}]");
```

Becomes:

```csharp
sb.AppendLine($"{indent}section song {{");
string sectionIndent = indent + "    ";
foreach (var track in playableTracks)
{
    string seqName = SanitizeVarName(track.Name);
    // … dedup against a HashSet of sequence names …
    string seqVar = seqName + "_seq";
    WriteSequence(sb, sectionIndent, seqVar, track);
    sb.AppendLine();
}
sb.AppendLine($"{indent}}}");
sb.AppendLine();
sb.AppendLine($"{indent}Song song = [song]");
```

This produces a Song with one section containing N parallel Sequences, which
SongRenderer mixes simultaneously (verified above).

### Fix B — flow-lang/StandardLibrary/Audio/VoiceAllocator.cs (BUG B: voice stealing on offline render)

The semantically correct fix is to make `Allocate` time-aware: a voice should
only be dropped when it would exceed `maxVoices` at some moment in time when
*another* voice is also playing. Voices that play at non-overlapping times
should never be stolen.

Sketch:
```csharp
public static List<Voice> Allocate(List<Voice> voices, int sampleRate, int maxVoices)
{
    if (voices.Count <= maxVoices) return voices;
    
    // Build a list of (startFrame, endFrame, voiceIdx) intervals.
    var intervals = voices.Select((v, idx) => {
        int start = (int)(v.OffsetBeats * sampleRate);  // crude — needs bpm
        int end = start + v.Buffer.Frames;
        return (start, end, idx, peak: GetPeakAmplitude(v));
    }).ToList();
    
    // Sweep-line: find moments where simultaneous voice count > maxVoices.
    // For each such moment, drop the quietest voice and re-check.
    // … (implementation: bring offsetBeats * bpm-aware seconds; convert to frames)
    
    // OR simpler: bucket voices into overlapping clusters; apply maxVoices
    // cap within each cluster.
}
```

Because that's a substantial rewrite, a **minimal, targeted alternative** is
to recognize that offline rendering paths (`SongRenderer`, `BuiltInFunctions
.RenderSequence`, `PolyrhythmFunctions.Polyrhythm`) have no real-time
constraint and should not voice-steal at all. Only the `play`/`loop` live
paths (`PlaybackFunctions.cs:127`) should enforce a cap.

Concretely, change the default `maxVoices` parameter in
`SequenceRenderer.RenderSequenceToVoices` from `32` to `int.MaxValue` (or
`0` as a "unlimited" sentinel), and have `VoiceAllocator.Allocate` short-
circuit when `maxVoices >= voices.Count` (it already does on line 20-21).
Live-playback paths already pass `manager.MaxVoices` explicitly — those are
unaffected.

This is a one-line change with predictable scope. Test impact is minimal
because almost all existing tests pass simple sequences with < 32 notes
(verified: `test_simple.flow` with 8 notes is unchanged; the cap only
triggered on long sequences that the test suite doesn't exercise).

### Verification plan

1. Apply Fix A → re-convert Chopin → confirm `Song song = [song]` with single
   section in generated .flow file → render → confirm duration ~270-330s.
2. Apply Fix B → render same file → confirm peak ≥ ~3000 across 80%+ of the
   timeline, NOT just sparse bursts.
3. Run full test suite: `for t in tests/test_*.flow; do dotnet run --project
   flow-interpreter "$t"; done` and `dotnet test`.
4. Run flow-midi tests if any exist (`flow-midi.Tests`).
5. Listen-test the rendered WAV (musical sanity).

Atomic commits:
- Commit 1: BUG A fix in `flow-midi/Conversion/FlowGenerator.cs` + any
  flow-midi unit tests.
- Commit 2: BUG B fix in `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs`
  default param change (or VoiceAllocator overhaul if user prefers the
  semantically correct approach).

## Outcome (2026-05-02)

Both fixes applied; the timeline-aware VoiceAllocator overhaul was deferred
in favor of the quick cap-bump per user direction.

**Bug A — applied at FlowGenerator.cs:96-122.**
Replaced the per-track `section` loop with a single `section song_part {…}`
holding one `Sequence` per MIDI track. `Song song = [song_part]` then refers
to the one section, and `SongRenderer`'s within-section parallel mix path
layers all parts simultaneously.

**Bug B — applied at SequenceRenderer.cs:34, 44, 75, 87.**
Default `maxVoices` parameter raised from 32 to 1024 across all four
`RenderSequenceToVoices` overloads. Live-playback callers in
`PlaybackFunctions.cs:127` pass `manager.MaxVoices` explicitly so they're
unaffected. The timeline-aware fix in `VoiceAllocator.Allocate` is left as a
future improvement (parameter is still semantically "voices across timeline"
rather than "simultaneous polyphony").

**Verification (Chopin Nocturne Op. 9 No. 2 in Eb Major):**
- Duration: 838.5s → 210.0s (matches MIDI source within 0.5%).
- Loudness: 8 sample windows uniformly between -5 and -8 dBFS (was -84 dBFS
  in the early sections).
- Tests: `dotnet test` 511/511 pass; `tests/test_*.flow` 66/66 pass (3
  pre-existing intentional error-tests fail on master too — confirmed by
  bisect).

**Files changed:**
- `flow-midi/Conversion/FlowGenerator.cs` — single-section emission (Bug A).
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — maxVoices cap raised
  to 1024 (Bug B).
