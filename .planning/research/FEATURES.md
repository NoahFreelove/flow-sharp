# Feature Research — v1.2 Stability & Composer DX

**Domain:** Music-production DSL / live-coding language (brownfield, extending Flow)
**Researched:** 2026-04-18
**Confidence:** HIGH for composer mental-models; MEDIUM for exact behavioral defaults

## Scope

This document covers the five Tier A composer-DX features identified in `CODEBASE-AUDIT-2026-04-18.md` Section 5, researched against how music-coding environments (SuperCollider, TidalCycles, Sonic Pi, Strudel, LilyPond, notation/DAW software) implement equivalent primitives. Bug-fix scope (C1–C7) is covered separately in PITFALLS.md — this file exists to inform requirements writing for the new features only.

The older v1.1 version of this file (math stdlib, `//` comments, etc.) was replaced because those features are now shipped; v1.2 research supersedes it.

---

## Feature Landscape

### Table Stakes (Composers expect these in any modern music-coding environment)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Sequence slicing** `slice(seq, start, end)` | TidalCycles has `slice` + `bite` + `chunk`; Strudel has `slice` + `chunk`; SuperCollider `Pseq` has an `offset` arg; Sonic Pi live-loops rely on `ring.take`/`drop`. Composers expect to pull phrases out of a longer sequence to reuse, transform, or loop. | LOW | Existing `take`/`drop` collections ops give the template. Unit must match the seq's own unit (bars or note-indices — pick one and document). |
| **Note-name aliases for B/H (German)** | LilyPond ships `english.ly`, `deutsch.ly`, `nederlands.ly`, `espanol.ly`, `svenska.ly` etc. Users with a classical/European background reach for `H` naturally. Missing it makes the language feel Anglocentric. | LOW | Lexer change + test. `H` → `B4` (natural), `B` stays `B4` flat only if `deutsch` mode is opt-in — otherwise `B` stays English (Bb would be ambiguous). **Recommend: additive only — `H` accepted as alias for `B`; do NOT redefine `B` as `Bb`.** |
| **Enharmonic helpers** (`Db` ↔ `C#`, `enharmonic(note)`) | Standard notation software (Finale, Sibelius, MuseScore, Dorico) all provide respell-enharmonic. Users writing in a flat-heavy key (Db major, Ab minor) want `Db E F Ab` not `C# E F G#`. | LOW | `Db`, `Eb`, `Gb`, `Ab`, `Bb` already parse today. Missing: an `enharmonic(note)` function that returns the alternate spelling, plus a policy for roman-numeral / scale output spelling in flat keys (currently `ScaleDatabase.cs:33-42` is brittle here — audit item). |
| **Per-voice/per-section reverb** | Every DAW provides per-channel reverb send levels. SuperCollider has `PbindFx` + orbits, Strudel has `.room()` + `.roomsize()` per-pattern, TidalCycles uses orbit-per-pattern. Composers hear reverb as a *voice attribute*, not a global render pass. | LOW | Mirrors shipped `gain`/`pan` context pattern. Add `ReverbTime` field to `MusicalContext`, validation, and Reverb.cs integration to pick up the per-voice value. |
| **MIDI velocity reflects dynamics** | MuseScore's "Single Note Dynamics" feature, Dorico's CC11/velocity mapping, and every notation→MIDI exporter handle this. Without it, a crescendo-decorated phrase exports as a flat-velocity MIDI file and sounds wrong in external DAWs. | LOW-MED | Dynamics envelope already computed for audio rendering (crescendo/decrescendo/swell). Need to sample it at note-onset time during MIDI export and write to the `NoteOnEvent.velocity` byte (`MidiExport.cs:191-192`). |
| **Swing on euclidean patterns** | TidalCycles `swingBy`, Ableton's Rotating Rhythm Generator `Swing` knob, Strudel `.swing()`, Sonic Pi `:swing` opt. Euclidean + swing is the de facto modern beat-design combo. | LOW | `MusicalContext.Swing` already exists and is validated [0.0, 1.0]. Extend `euclidean()` to accept an optional swing arg, or have it consult ambient context. **Recommend: both — explicit arg overrides context.** |

### Differentiators (What Flow does that stands out)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **`loopEdit(...)`** — surgical phrase replacement | None of the surveyed environments have a single-call "replace bars N-M in this sequence with that phrase" primitive. Tidal users do it via `mask` + `stitch`, SuperCollider via `Pseq` concatenation. A first-class `loopEdit(seq, startBar, endBar, replacement)` is a composer-workflow win that matches how DAW users think ("select region → paste"). | LOW-MED | Implement as `concat(slice(seq, 0, start), replacement, slice(seq, end, length(seq)))`. I.e., derived from `slice`. |
| **Humanize on euclidean** (timing jitter) | Ableton's RRG has swing but not per-hit humanize; SuperCollider requires hand-rolling `Pwhite` on `\timingOffset`. Adding `humanize: 0..1` to `euclidean()` in one call is a clear DX improvement. | LOW | Multiply a small random offset by beat-duration; bounds: `±humanize * 0.05 * beat` is a reasonable starting range. Should be deterministic if a seed is supplied (matches existing `(?? ...)` pattern). |
| **`reverbTime` as a context block** (not a plugin send) | DAWs need aux-bus wiring; code-based tools usually require plugging together UGens. A pure declarative `reverbTime 2.5 { | C4 D4 E4 |}` is idiomatic Flow. Mental model: "notes in this block decay for 2.5s". | LOW | Natural fit given the shipped context-block machinery. |
| **MIDI-velocity envelope from Flow dynamics** | Closes the export-parity loop: audio and MIDI produce the same musical result. MuseScore/Dorico struggle with this; Flow can do it cleanly because dynamics are already first-class sequence-level state. | LOW-MED | Must handle overlapping dynamics (crescendo → swell → decrescendo in same bar) — pick last-writer-wins or multiplicative. Recommend: **sample the envelope at note onset, map to 1-127 with a floor ≥ 8** (avoid the whisper-quiet-still-triggers issue flagged in audit §3 note about `MidiExport.cs:195`). |

### Anti-Features (Seem useful, explicitly exclude)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Slice by sub-beat time (e.g., `slice(seq, 2.35, 3.7)` in beats)** | "More flexible" than whole-bar slicing. | Opens a can of worms for partial-note handling: what happens when a slice cuts mid-note? Requires new tied-note-split logic. Not worth it for v1.2. | `slice` takes **integer bar indices** (or sequence-step indices — pick one, document, enforce). Sub-beat editing can wait for v2 if users ask. |
| **Full MIDI CC11 (expression) emission alongside velocity** | More faithful to how orchestral sample libraries read dynamics. | CC11 semantics vary wildly by instrument — sending CC11 to a piano preset does nothing; sending it to strings can conflict with user's existing automation. Doubles export complexity. | v1.2 = velocity only. A "MIDI expression mode" flag can be added later if users request CC11. |
| **German mode that remaps `B` to `Bb`** | "Authentic German notation." | Destructive and silent-breaking: every existing `B4` in every user's `.flow` file suddenly means a different pitch. | **Additive only** — `H` = `B natural`, `B` stays English. German users who want full remap can write `Bb` explicitly or request an opt-in pragma in a later version. |
| **Auto-detect "this looks like German notation" and switch modes** | "Just works." | Heuristics-based type-system switching is always wrong. | Explicit is better. Document `H` as an alias, recommend users pick a convention per file. |
| **`reverbTime` as a live-modulatable UGen parameter** | "More powerful." | Flow's existing `reverb()` processor is offline-batch; live modulation requires a streaming DSP graph, not in scope for v1.2. | `reverbTime` is block-scoped (like `gain`, `pan`) — captured once at render time. Automation within the block can wait. |
| **Velocity-from-dynamics applied to the audio renderer too (retroactively)** | "Consistency." | Audio renderer already applies the dynamics envelope directly to sample amplitudes — that IS the velocity equivalent in audio. MIDI export is the only place the envelope currently gets lost. | Keep the feature scoped: MIDI export only. Audio path unchanged. |
| **Humanize that changes pitch (± cents jitter)** | "Even more human-sounding." | Pitch humanization overlaps with `C4+50c` cent-offset syntax already shipped, and with future microtonal work (Tier B #12). Muddies the feature. | v1.2 humanize = timing only. Velocity-humanize is a candidate follow-up if users ask. |
| **`enharmonic(seq)` auto-rewrite the whole sequence in spelling consistent with current key** | "Smart notation." | Requires a real spelling engine — this is a hard music-theory problem (Bach vs. Debussy pick different enharmonic spellings). | v1.2 `enharmonic(note)` = single-note swap (`C#` → `Db`). Whole-sequence rewrite is v2+ notation-polish work. |

---

## Feature Dependencies

```
slice(seq, start, end)
    └──enables──> loopEdit(seq, start, end, replacement)

MIDI velocity from dynamics
    └──depends on──> (existing) crescendo/decrescendo/swell transforms
    └──depends on──> (existing) MidiExport pipeline
    └──independent of──> slice / reverbTime / euclidean swing
    └──interacts with──> minor-bug MidiExport.cs:195 (velocity floor of 1)

reverbTime context block
    └──depends on──> (existing) MusicalContext push/pop machinery
    └──depends on──> (existing) Reverb.cs DSP implementation
    └──prerequisite fix──> C1 (ExecuteMusicalContext stack leak) — otherwise any
                           validation error inside reverbTime leaks the frame
    └──independent of──> all other v1.2 features

euclidean swing/humanize
    └──depends on──> (existing) MusicalContext.Swing field (already shipped)
    └──depends on──> (existing) euclidean() Bjorklund impl
    └──independent of──> MIDI velocity / reverbTime / slice

note-name aliases (H) and enharmonic()
    └──depends on──> SimpleLexer note-vs-identifier lookahead (audit §2 note:
                     lexer bug at 543-564 is a prerequisite fix, same file touched)
    └──recommended ordering──> fix that lexer bug FIRST, then add alias in same patch
    └──independent of──> all other v1.2 features
```

### Dependency Notes

- **`loopEdit` is a thin wrapper over `slice`:** do both in the same requirement/phase; `slice` is the primitive, `loopEdit` is the ergonomics layer.
- **MIDI velocity from dynamics is independent** of the other DX features and can ship first if the team wants visible progress. It also fixes the "MIDI export loses musicality" complaint which is a frequent real-user pain-point in equivalent tools (MuseScore forum threads confirm this).
- **`reverbTime` lands cleanly** atop shipped context-block machinery (`gain`, `pan`, `swing`) — but the whole context-block system is hosting critical bug C1 (stack-leak on validation error). Fix C1 before (or in the same PR as) adding `reverbTime`, otherwise a bad `reverbTime -1 { ... }` will permanently corrupt the context stack for the rest of the program.
- **Euclidean swing/humanize is trivially small** if `MusicalContext.Swing` is reused; humanize needs a small RNG (existing `(?? ...)` seeding convention should be followed).
- **Note-name aliases share a file with the audit-flagged lexer bug** (`SimpleLexer.cs:543-564`). The aliases are additive *only* if that bug is fixed first — otherwise `H4w` might tokenize wrong.
- **All five features are otherwise independent** of one another except the `slice` → `loopEdit` relationship. They can ship in any order.

---

## MVP Definition for v1.2

### Launch With (Required — minimum viable scope per feature)

**These define "v1.2 Tier A is done":**

- [ ] **`slice(Sequence, Int, Int) → Sequence`** — integer bar indices (0-based, exclusive end). Returns a new Sequence containing bars `[start, end)`. Errors (not silent-clamps) on out-of-range indices.
- [ ] **`loopEdit(Sequence, Int, Int, Sequence) → Sequence`** — integer bar indices. Returns a new Sequence with bars `[start, end)` replaced by the replacement sequence. Length of replacement may differ from `end - start`.
- [ ] **`H` as a lexer-level alias for `B`** in note literals and note streams. Documented as "optional German notation." `B` remains English (natural = B, flat = `Bb`). Aliasing is purely token-level; downstream Value/MusicalNoteData stores the canonical English form.
- [ ] **`enharmonic(Note) → Note`** — returns the enharmonic spelling (`C#` → `Db`, `D#` → `Eb`, `F#` → `Gb`, `G#` → `Ab`, `A#` → `Bb`, and the reverse). Naturals (`C`, `D`, `E`, `F`, `G`, `A`, `B`) are passthrough (unchanged). Double-sharps/flats not in scope.
- [ ] **`reverbTime Double { ... }` context block** — value is decay time in seconds (RT60 convention, consistent with industry standard). Validated `> 0`. Pushes onto the MusicalContext stack; block-scoped; inherits to nested scopes; picked up by the Reverb DSP at render time for voices rendered within the block.
- [ ] **MIDI velocity reflects active dynamics envelope** — for each note exported to MIDI, sample the dynamics envelope (crescendo/decrescendo/swell) at the note's onset time and map the resulting 0.0–1.0 value to MIDI velocity 8–127 (floor of 8 prevents whisper-silent triggers; ceiling of 127 is the MIDI spec maximum). Must be default-on for existing export calls (no new flag required) — but document it loudly as a behavior change in v1.2 release notes.
- [ ] **`euclidean(hits, steps, note, swing?, humanize?)` overload** — two new optional parameters. `swing` in [0.0, 1.0] (reusing `MusicalContext.IsValidSwing`); delays odd-indexed hits by `swing * beatDuration / 2` (matches TidalCycles `swingBy` semantics). `humanize` in [0.0, 1.0]; applies `±humanize * 0.05 * beatDuration` random timing offset per hit. Both default to 0.0 when absent (preserves current behavior — backward compatible).

### Add After Validation (defer within v1.2 if scope pressures arise)

- [ ] **`slice` by beat indices (not just bars)** — add `sliceBeats(seq, startBeat, endBeat)` if users ask. Integer beats only, still avoids mid-note splits.
- [ ] **`loopEdit` with auto-transition blending** (crossfade between spliced regions) — useful for audio buffers but not for symbolic sequences; revisit if users request.
- [ ] **`enharmonic` spelling-aware (picks flats in flat keys)** — currently only a 1-to-1 swap; upgrade to context-aware once Harmony `ScaleDatabase.cs:33-42` is cleaned up (audit §2).
- [ ] **`reverbTime` with separate `earlyReflections` / `damping` knobs** — right now `Reverb.cs` has these as constants; exposing them is a follow-up.
- [ ] **Humanize velocity jitter** (in addition to timing) — small extension.
- [ ] **Seeded humanize** (`humanizeSeed` parameter) — matches the `(??)` determinism convention.

### Future Consideration (v2+ — explicitly NOT in v1.2)

- [ ] **`bite` / `chunk` functions** (TidalCycles-style pattern subdivision + rotation) — separate feature family.
- [ ] **Full multilingual note-name mode** (LilyPond-style `\language "deutsch"` directive) — redesign if users ask.
- [ ] **MIDI CC11 expression export** alongside velocity — only if orchestral-sample-library users complain.
- [ ] **Per-voice reverb type** (plate vs. hall vs. spring presets) — beyond scoped reverb time.
- [ ] **`enharmonic` whole-sequence re-spelling engine** — real music-theory problem.
- [ ] **Groove templates / swing curves** (non-linear swing, MPC-style) — beyond a single scalar swing parameter.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Rationale |
|---------|------------|---------------------|----------|-----------|
| MIDI velocity from dynamics | HIGH | LOW | P1 | Closes a broken export loop; affects every user doing MIDI export. |
| Euclidean swing/humanize | HIGH | LOW | P1 | Modern beat-design ergonomics; reuses shipped swing/seed infrastructure. |
| `slice` + `loopEdit` | HIGH | LOW | P1 | Fills an obvious composition-workflow gap; enables phrase editing. |
| `reverbTime` context | MEDIUM | LOW | P1 | Consistent with shipped context-block pattern; satisfies "per-voice reverb" expectation. Blocked on C1 fix. |
| `H` alias + `enharmonic()` | MEDIUM | LOW | P2 | Pedagogical / international-user win; pure additive. Can ship late in v1.2 without blocking others. |

All five Tier A features are P1 or P2 — none should be cut from v1.2. None rises to P3 because the whole bundle was already pre-filtered via the audit's impact × fit ÷ effort ranking.

---

## Cross-Environment Feature Comparison

| Feature | SuperCollider | TidalCycles | Sonic Pi | Strudel | Notation software | Flow (v1.2 target) |
|---------|---------------|-------------|----------|---------|-------------------|---------------------|
| **Slice sequence** | `Pseq` with `offset`; `Pchain` composition | `slice`, `chop`, `bite`, `chunk`, `ur` | `ring.take` / `ring.drop`; `live_loop` re-slicing | `slice`, `chunk`, `chunkBack`, stepwise | Region select/paste in DAW | `slice(seq, start, end)`, `loopEdit(...)` |
| **German/enharmonic names** | No built-in | No (sample names) | No (symbols) | No | LilyPond `\language "deutsch"`; Finale/Sibelius spelling menus | `H` alias (additive); `enharmonic(note)` function |
| **Per-voice reverb** | `PbindFx`, orbit assignment | Orbit `d1`/`d2` → separate reverbs in SuperDirt; `room`, `size` | FX `with_fx :reverb do…end` block | `.room(x).size(y)` per-pattern | Aux bus + send level | `reverbTime { ... }` context block |
| **Dynamics → MIDI velocity** | Manual (`\amp`, `\velocity` patterns) | Manual (`# gain`, `# velocity`) | Manual (`amp:` arg, `sleep`-based swells) | Manual (`.gain()`, `.velocity()`) | MuseScore single-note dynamics (recent); Dorico CC11+velocity; generally imperfect | Automatic — sample dynamics envelope at note onset |
| **Euclidean + swing** | `Pwrand` + `Pkey`; hand-rolled Bjorklund | `euclid`, `euclidInv`, `euclidFull`; `swingBy` | `spread` function; `:swing` opt on some samplers | `euclid(...)`, `.swing()` time modifier | N/A (MIDI sequencers only) | `euclidean(h, s, n, swing, humanize)` |
| **Humanize (timing)** | Hand-rolled with `Pwhite` on `\timingOffset` | Via `nudge` or manual offset pattern | `rand` on `sleep` arg | No direct; hand-roll with `.late(rand(...))` | Per-DAW: Ableton Humanize, Logic Humanize, Reaper Humanize Notes | `humanize` parameter on `euclidean()` |

---

## Expected User Mental Models

What a user will *guess* the feature does before reading docs. Requirements should honor these.

### `slice(seq, start, end)`
- **Mental model:** "Give me bars 2 through 5 of this sequence." (Array-slice intuition from every programming language.)
- **Expected:** 0-based start, exclusive end (Python/JS convention).
- **Surprise to avoid:** Don't make indices 1-based (confuses programmers) or inclusive-inclusive (confuses both).

### `loopEdit(seq, start, end, replacement)`
- **Mental model:** "Splice in this new phrase over bars 3–5." (Copy-paste region DAW intuition.)
- **Expected:** Replacement can be longer or shorter than the replaced region; total length adjusts.
- **Surprise to avoid:** Don't force replacement to match length — that's just assignment, not editing.

### `H` alias
- **Mental model:** "I'm a German/Nordic classical musician; H is B natural, always has been since Bach." (Every source confirms this is the single universal interpretation in those notation traditions.)
- **Expected:** Writing `H4q` behaves identically to `B4q`.
- **Surprise to avoid:** Don't redefine `B` — that breaks every existing Flow script.

### `enharmonic(note)`
- **Mental model:** "Respell this note." (Notation-software Respell-Enharmonic menu item.)
- **Expected:** `C#4` → `Db4`, and vice versa. Naturals pass through.
- **Surprise to avoid:** Don't change the pitch (only the spelling). Don't fail on naturals (return them unchanged).

### `reverbTime 2.5 { | C4 D4 E4 | }`
- **Mental model:** "These notes ring out for 2.5 seconds after they stop." (DAW reverb-decay knob intuition.)
- **Expected:** Value in seconds (RT60 convention). Larger = longer tail.
- **Surprise to avoid:** Don't make the unit "milliseconds" or a normalized 0-1 — composers think in seconds.

### MIDI velocity from dynamics
- **Mental model:** "If I write `crescendo (| C4 D4 E4 F4 |)`, the MIDI file should have notes getting louder, same as the audio does." (Notation-software export intuition.)
- **Expected:** The MIDI velocity contour matches what they hear.
- **Surprise to avoid:** Don't require a separate flag (`writeMidi(..., preserveDynamics=true)`) — it should just work, with a release-note callout for the behavior change.

### `euclidean(3, 8, C4, swing=0.6, humanize=0.3)`
- **Mental model:** "Tresillo pattern, with MPC-style swing and a bit of human timing wobble." (Ableton Groove Pool / MPC intuition.)
- **Expected:** `swing=0.5` is straight (no swing — matches `MusicalContext.Swing` shipped convention). `humanize=0` is perfectly quantized; `humanize=1` is maximum wobble (still musical, not chaos).
- **Surprise to avoid:** Don't make swing=0 be "extreme swing" and swing=1 be "no swing" — the shipped convention already uses 0.5=straight.

---

## Integration Notes (for Requirements phase)

Pointers into the existing codebase that requirements can reference:

| Feature | Primary touch-points | Supporting references |
|---------|----------------------|-----------------------|
| `slice` / `loopEdit` | `StandardLibrary/BuiltInFunctions.cs` (register), `flow-lang/audio.flow` (optional convenience wrapper) | Existing `take`/`drop`/`concat` templates; `Sequence` type in `TypeSystem/SpecialTypes/` |
| `H` alias | `Lexing/SimpleLexer.cs` (~543-564 — also has the note-vs-identifier lookahead audit bug; fix together) | `NoteType.Parse` in `TypeSystem/SpecialTypes/NoteType.cs` |
| `enharmonic()` function | `StandardLibrary/BuiltInFunctions.cs` or new `StandardLibrary/Harmony/EnharmonicFunctions.cs` | `PitchConversion.cs`; ScaleDatabase for key-aware v2 follow-up |
| `reverbTime` context | `Runtime/MusicalContext.cs` (add field), `Interpreter/Interpreter.cs` (`ExecuteMusicalContext` — but **block on C1 fix first** per audit), `Parsing/Parser.cs` (recognize keyword), `Audio/DSP/Reverb.cs` (read from context) | Existing `Gain`/`Pan`/`Swing` validator methods are the template |
| MIDI velocity from dynamics | `StandardLibrary/Audio/MidiExport.cs:191-192` (velocity byte), `StandardLibrary/Audio/SequenceRenderer.cs` (dynamics envelope sampling path) | Existing dynamics transforms in `StandardLibrary/Transforms/` |
| Euclidean swing/humanize | `StandardLibrary/BuiltInFunctions.cs:1011-1060` (existing `euclidean` registration) | `MusicalContext.Swing` field + `IsValidSwing`; existing RNG conventions from `(? ...)`/`(?? ...)` |

---

## Open Questions for Requirements Phase

These are implementation details that spec'ing should nail down; research has surfaced but not resolved them:

1. **`slice` unit: bars or note-indices?** Bars feel more musical; indices are more predictable. Recommend: **bars**, consistent with how Sequence is rendered bar-by-bar. Confirm with user before coding.
2. **Does `H` require a pragma (`use "@deutsch"`) or is it always available?** Recommend: **always available**, documented as an alias. If someone genuinely doesn't want it, they simply don't write `H`.
3. **Velocity floor value for MIDI export:** Recommend **8** (per audit §3 feedback — 1 is too quiet, 0 is note-off). Nail in spec.
4. **`reverbTime` default when block absent:** Recommend **don't change current reverb behavior** — block is opt-in; absence = existing Reverb.cs defaults apply. Do NOT apply a global default reverb-time if someone hasn't asked for one.
5. **Humanize determinism:** Recommend **seeded** via an optional `seed:` param, matching `(?? ...)` convention. Without seed, timing varies per run (like DAW "Humanize" buttons).
6. **Swing curve shape on euclidean:** Recommend **linear delay of odd-indexed hits** (matches TidalCycles `swingBy`). A curved / MPC-style swing is v2+.

---

## Sources

**Ecosystem research (HIGH → MEDIUM confidence):**

- [TidalCycles — slice reference](https://userbase.tidalcycles.org/slice.html) — confirms `slice n pat $ sound s` semantics (MEDIUM — userbase wiki)
- [TidalCycles — sampling reference](https://tidalcycles.org/docs/reference/sampling/) — official (HIGH)
- [Strudel — Pattern effects](https://strudel.cc/workshop/pattern-effects/) — `slice`, `chop`, chunk (HIGH, current)
- [Strudel — Audio effects `room`/`size`/`dry`](https://strudel.cc/learn/effects/) — reverb per-pattern model (HIGH)
- [SuperCollider Pseq docs](https://doc.sccode.org/Classes/Pseq.html) — offset param (HIGH)
- [SuperCollider Pdef docs](https://doc.sccode.org/Classes/Pdef.html) — live pattern replacement semantics (HIGH)
- [SuperCollider PbindFx documentation](https://pustota.basislager.org/_/sc-help/Help/Classes/PbindFx.html) — per-pattern effects incl. reverb (MEDIUM — third-party mirror)
- [Sonic Pi — sample slicing walkthrough](https://www.raspberrypi.org/magpi/sonic-pi-sample-slicing/) — idiomatic live-loop slicing (MEDIUM)
- [Tidal Cycles — week 3 slice & splice lesson](https://club.tidalcycles.org/t/week-3-lesson-3-slice-and-splice/519) — community walkthrough (MEDIUM)
- [Tidal Cycles — swingBy generalizing swing](https://club.tidalcycles.org/t/generalizing-swing-and-rotating-uneven-rhythms-by-mapping-integers-from-a-latent-space-to-time/4991) — swing semantics (MEDIUM)
- [Ableton CV Tools — Rotating Rhythm Generator](https://www.ableton.com/en/blog/geometric-sequencing/) — Euclid + swing in a mainstream DAW (HIGH, official)
- [LilyPond — note names in other languages](https://lilypond.org/doc/v2.25/Documentation/notation/note-names-in-other-languages) — confirms German `deutsch.ly` + `H` convention (HIGH)
- [Wikipedia — B (musical note)](https://en.wikipedia.org/wiki/B_(musical_note)) — historical origin of H/B split (HIGH)
- [Tonalsoft — German H nomenclature](http://www.tonalsoft.com/enc/g/german-h.aspx) — theory reference (MEDIUM)
- [Tunable — German notation H/B + -is/-es system](https://tunableapp.com/notations/german/) — practical reference (MEDIUM)
- [MuseScore forum — MIDI export of crescendo dynamics](https://musescore.org/en/node/322137) — confirms existing notation software struggles with this exact problem (HIGH — canonical source for the pain point)
- [MuseScore — automatic dynamics from MIDI file volume](https://new.musescore.org/en/node/373933) — motivates the feature (MEDIUM)
- [MuseScore — single note dynamics implementation notes](https://musescore.org/en/node/280281) — CC11 vs. velocity tradeoffs (HIGH)
- [Dorico forums — CC11 vs velocity interpretation](https://forums.steinberg.net/t/interpretation-of-cc11-expression-velocity-midi/977375) — industry-standard discussion (HIGH)
- [Splice — advanced MIDI velocity techniques](https://splice.com/blog/advanced-midi-velocity-techniques/) — humanize technique context (MEDIUM)
- [Black Ghost Audio — humanizing productions](https://www.blackghostaudio.com/blog/5-ways-to-humanize-your-productions) — 3-5% timing/velocity starting guidance (MEDIUM)
- [MIDI Drum Files — humanizing MIDI](https://mididrumfiles.com/2024/10/humanize-it-making-it-feel-real/) — ±10 ticks as subtle-life threshold (MEDIUM)
- [Rational Acoustics — RT60 reverberation time](https://support.rationalacoustics.com/support/solutions/articles/150000190451-reverberation-time-spilling-the-t-on-rt60) — confirms RT60 as industry-standard "reverb time" unit (HIGH)

**Codebase-internal references (HIGH — verified in-repo at this session):**

- `flow-lang/Runtime/MusicalContext.cs:35-106` — existing Swing/Gain/Pan/Velocity fields, validators, Clone()
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1011-1060` — existing `euclidean` registration (Bjorklund)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:191-199` — existing note-on velocity byte construction
- `.planning/CODEBASE-AUDIT-2026-04-18.md` §5 Tier A — source of the v1.2 feature bundle
- `.planning/PROJECT.md` — Core Value ("faithfully translate musical notation into correct, playable audio") is the evaluation criterion for every feature above; `H` / dynamics→velocity / reverbTime all *directly* advance this.

---

*Feature research for: Flow v1.2 Stability & Composer DX milestone*
*Researched: 2026-04-18*
