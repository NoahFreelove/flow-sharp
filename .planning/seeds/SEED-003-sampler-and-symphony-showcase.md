---
id: SEED-003
status: dormant
planted: 2026-05-04
planted_during: v1.3 Composer DX Tier B/C — Phase 25 closure / Phase 26 unblocked
trigger_when: Starting v1.4. Sampler tech lands mid-milestone; symphony showcase is the v1.4-closing capability.
scope: Large
---

# SEED-003: Multi-Sample Sampler + Short Symphony Showcase

## Why This Matters

Flow's existing synthesizers (piano, brass, sax, drums, strings, organ, bell)
are functional but unmistakably synthetic. To produce a clip that's *postable
on GitHub as a showcase* — i.e. that a stranger would listen to and take
seriously as an example of the language's capability — Flow needs sample-based
instruments backed by real recordings.

The user wants v1.4 to close with a **short symphony** rendered entirely from
Flow source code, accompanied by screenshots of the code that produced it. The
clip is the headline artifact of the milestone; the sampler tech is what
makes it possible.

Two threads, tightly coupled but separable:

1. **Sampler tech** — a multi-sample instrument subsystem capable of consuming
   real orchestral sample libraries (SFZ being the lowest-friction target)
   and dispatching the right sample for each note + velocity, with proper
   sustain looping for held notes.
2. **The symphony itself** — a curated piece written in Flow source,
   rendered through the new sampler, polished, screenshotted, and posted.
   Curation work, not engineering.

## When to Surface

**Trigger:** Starting v1.4.

The sampler tech should land **mid-milestone** so that other v1.4 features
can build on it (and so the user can iterate on the piece against working
instruments). The symphony showcase is **the v1.4-closing capability** — the
last phase before milestone close, after every other v1.4 feature has shipped
and stabilized.

This seed should be presented during `/gsd-new-milestone` for v1.4 regardless
of milestone theme — the user has explicitly committed to the showcase as the
v1.4 closer.

## Foundation Already in Place

Most of the WAV-loading and voice-allocation primitives ship today:

- **`loadWav` (varispeed)** — Phase 22 (v1.3) shipped sample loading +
  pitch-shift-via-resample. Single-sample-pitched playback already works.
- **Polyphonic voice allocation** — Phase 2 (v1.0) shipped voice pooling.
- **WAV format I/O** — `flow-lang/StandardLibrary/Audio/FileIO.cs` already
  reads/writes RIFF/WAV.
- **Synthesizer abstraction** — `flow-lang/StandardLibrary/Audio/Synthesizers/`
  has the registration pattern (Piano, Brass, Sax, Drums, Strings, Organ,
  Bell). A "Sampler" instrument slots into this layer.
- **Instrument selection in SongRenderer** —
  `flow-lang/StandardLibrary/Audio/SongRenderer.cs` already dispatches to
  the right synthesizer per voice; multi-sample dispatch is an extension of
  this, not a rewrite.

## Scope Estimate

**Large** — multi-phase milestone capstone. Rough shape:

### Sampler tech (mid-milestone, parallel-safe with other v1.4 work)

1. **SFZ subset parser.** SFZ is plain text with `<region>`, `<group>`,
   `<global>` blocks. The spec is huge but real libraries use a small
   subset: `sample`, `lokey`/`hikey`/`pitch_keycenter`, `lovel`/`hivel`,
   `loop_mode`/`loop_start`/`loop_end`, `ampeg_attack`/`ampeg_release`,
   `volume`, `pan`. Hand-rolled parser; no external dependency needed.
2. **Region matching engine.** Given a (note, velocity) pair, select the
   matching region(s) from a parsed SFZ instrument. This is a small
   indexable lookup once parsed.
3. **`Sampler` synthesizer.** New class in `StandardLibrary/Audio/
   Synthesizers/`, parallel to `PianoSynthesizer.cs`. Loads SFZ at
   instantiation, dispatches per-note to the right sample with in-zone
   pitch shift via resample (already implemented in `loadWav` Phase 22).
4. **Sustain loops.** Required for held orchestral notes (strings, brass,
   reeds). Read `loop_start`/`loop_end`/`loop_mode` from SFZ; loop the
   sample data between those points until note-off.
5. **Velocity layers.** Already falls out of region matching (multiple
   regions for the same pitch with different `lovel`/`hivel`). Crossfade
   (`xfin_lovel`/`xfin_hivel`) is a v2 polish item; hard-switching is
   fine for the showcase.
6. **Library management.** Decide where `.sfz` libraries live and how
   Flow finds them. Likely a stdlib search-path convention plus
   user-supplied paths via `loadSfz("path.sfz")`. **Decision pending.**
7. **Flow source surface.** How do composers reference a sampler
   instrument? Probably extend the existing instrument-name string
   (e.g. `"sampler:vsco-violin"`) or add a new `Sampler` value type.
   **Decision pending.**

### Symphony showcase (v1.4 closer — last phase)

8. **Pick a sample library.** Free options: VSCO Community, Versilian
   Studios Community Orchestra, Sonatina Symphonic Orchestra. Need to
   verify license compatibility for redistribution / linking.
9. **Write the piece.** A short symphony — likely 30-90 seconds, 3-6
   instruments, demonstrating: musical context blocks, note streams,
   transforms, the sampler, and ideally features added in earlier v1.4
   phases.
10. **Polish + render.** Final mix, normalize, render to high-quality
    WAV.
11. **Screenshots + posting.** Code screenshots of the source paired
    with the audio. README updates pointing at the showcase.

## Decisions Pending

Lock these in spec-phase, not now:

- **Format choice.** SFZ (text, simple, free libraries) vs SoundFont
  `.sf2` (binary, broader compatibility, heavier parser) vs custom
  Flow-native format. Recommend SFZ.
- **Pitch-shift technique within zones.** Linear interpolation, sinc,
  or pitch-shift libraries? Linear is what `loadWav` already does and
  is fine for ±2-3 semitones; beyond that artifacts compound, which
  is *why* you want multi-sample mapping in the first place.
- **Library distribution.** Does Flow bundle a default sample library
  (size: ~hundreds of MB to GB), or always require user download?
  Bundling makes the showcase reproducible from a git clone; not
  bundling keeps the repo small. Likely: ship a tiny "demo SFZ" with
  the repo, point at downloadable libraries for serious work.
- **License/hosting for the showcase audio.** Does the rendered .wav
  ship in the repo? In a release tag? In a separate assets repo?
  GitHub repo size matters here.
- **Sampler API surface.** New `Sampler` value type vs extending the
  instrument-name string vs SFZ-as-data-loaded-via-`use`. Affects how
  natural the syntax feels.
- **Showcase piece itself.** Genre / structure / length / instrument
  count — these are creative decisions for the user, not engineering
  decisions, but the engineering needs to know roughly what the piece
  asks of the sampler.

## Sequencing within v1.4

User-stated sequencing:
- **Sampler tech: mid-milestone**, lands so subsequent features and
  the showcase can use it.
- **Other v1.4 features: in between.** User has signaled additional
  features will be inserted before the showcase phase.
- **Symphony showcase: very last phase of v1.4.** Closes the milestone.
  This implies a hard ordering constraint — the showcase phase cannot
  be planned until every prior v1.4 feature is locked, since the piece
  may demonstrate them.

## Public-ness Implication

A public showcase clip is the moment Flow stops being pre-public.
Per `project_pre_public_no_legacy_burden.md`, breaking changes can land
in one commit today; once the clip is posted and people start cloning to
reproduce it, the API surface the clip demonstrates becomes effectively
frozen.

**Implication for sequencing:** prefix-only arithmetic (Phase 26) and
symbols/tuples/dicts (Phase 26.1) **must ship before this seed activates**.
v1.3 already plans this — Phase 27 is the v1.3 tutorial/showcase refresh.
The v1.4 showcase clip should use the v1.3-stabilized syntax direction,
not the legacy infix one. This is structural, not aesthetic — posting a
clip whose code uses syntax we plan to remove would be a self-inflicted
legacy burden right at the moment of going public.

## Breadcrumbs

Existing code likely to be touched:

- `flow-lang/StandardLibrary/Audio/FileIO.cs` — existing WAV reader;
  the Sampler reuses it for loading individual sample files
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` —
  reference pattern for a new `SamplerSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — instrument
  dispatch lookup; new sampler instruments register here
- `flow-lang/StandardLibrary/Audio/` — natural home for a new
  `Sampler/` subdirectory containing SFZ parser, region matcher, and
  sample cache
- `flow-lang/Runtime/Value.cs` — if a new `Sampler` value type lands
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — registration of
  new builtins (`loadSfz`, etc.)
- (Phase 22 work) — the varispeed `loadWav` implementation already
  in place is the resampling primitive the Sampler reuses

Repo files that don't exist yet but should:
- `flow-lang/StandardLibrary/Audio/Sampler/SfzParser.cs`
- `flow-lang/StandardLibrary/Audio/Sampler/SfzInstrument.cs`
- `flow-lang/StandardLibrary/Audio/Sampler/RegionMatcher.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/SamplerSynthesizer.cs`
- `examples/symphony/` (the showcase piece + assets)
- `examples/symphony/README.md` (how to reproduce the clip)

## Notes

- Captured 2026-05-04 during v1.3 close-out planning, after the user
  expressed wanting Flow ready for "sorta-production" with a postable
  GitHub clip.
- Combined seed (sampler + showcase) rather than two separate seeds:
  the showcase doesn't make sense without the sampler, and the
  sampler's quality bar is set by what the showcase demands.
- v1.3 must close cleanly first — Phases 26 / 26.1 / 27 are still
  pending. Do not pre-empt v1.3 for this work.
- The user explicitly noted other v1.4 features will be inserted
  before the showcase phase. This seed should be the closer, not the
  whole milestone.
