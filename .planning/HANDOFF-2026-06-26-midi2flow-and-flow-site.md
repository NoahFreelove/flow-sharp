# Handoff — 2026-06-26: MIDI→Flow translation quality + flowlang.dev playground

**Branch:** all work committed on `dev` (NOT deployed to CF Pages — see §Deployment).
**Trigger:** converting `~/Downloads/ragtime.mid` to Flow exposed a chain of audio/render
bugs; fixing them led into the playground. Two focus areas below.

---

## TL;DR

Converted a real ragtime MIDI to Flow and chased every "that doesn't sound right" to root
cause. Fixed three genuine engine bugs (chord+dynamic timing doubling; sampled-render
per-beat click; web playground every-other-run silence), added a `--no-dynamics`
converter flag, refactored the playground to load examples from `static/`, added a Ragtime
example, and re-published the WASM runtime so the browser playground actually runs the fixed
engine. 9 commits, 3 debug sessions (all resolved), 2 quick tasks.

---

## Part 1 — MIDI → Flow translation (`flow midi2flow`)

### How conversion works (unchanged this session)
`flow-cli midi2flow` → `flow-midi/Midi/MidiParser` → `flow-midi/Conversion/Quantizer`
(splits each track by channel, then hand-splits RH/LH at middle C, then first-fit voice
allocation → one `Sequence` per voice) → `flow-midi/Conversion/FlowGenerator` (round-trip
mode: explicit durations, `trackN_seq` naming, `section roundtrip`, per-bar sticky
dynamics). Command: `dotnet run --project flow-cli -- midi2flow IN.mid -o OUT.flow`.

### Bugs found + fixed (engine, not the converter)
The converter's OUTPUT was correct (every generated bar summed to the bar capacity). The
problems were in the **engine** that renders the generated `.flow`:

1. **Chord+dynamic bar-doubling ("drunk pianist") — FIXED `f038c1b`.**
   A note-stream bar containing BOTH a dynamic marking (`f`/`ff`/`mf`…) AND chords rendered
   at ~2× its notated length, so the chord-dense right hand drifted progressively behind the
   left. Root cause: `NoteStreamCompiler.InterpolateVelocities` rebuilt interpolated middle
   notes with the positional 12-arg `MusicalNoteData` ctor, silently dropping `IsChordTone`
   (+ 4 trailing fields). Chord tones then advanced the beat cursor in `BarType.ToTimeline`
   instead of sharing one onset. Fix: `notes[i] = notes[i].With(velocity: vel)` (preserve
   all fields). Regression test: `flow-lang.Tests/Integration/Debug2026/ChordDynamicBarDoublingTests.cs`.
   Session: `.planning/debug/resolved/chord-dynamic-bar-doubling.md`.

2. **Sampled-render per-beat "static"/click — FIXED `487f248`, `7d19477`.**
   Dense short sampled-piano notes produced a per-beat crackle. Root cause: the ADSR
   envelope faded each note to ~0 at its authored end, then the exponential release-tail
   loop restarted at full amplitude on the raw sample → a one-sample step at every note end,
   beat-aligned, stacking in dense passages. Fix: `SampledInstrumentRenderer` `baseRelease
   0.05 → 0.0` (envelope meets the tail continuously) + `EnvelopeProcessor` routes leftover
   to release only when `release > 0` (synth/SFZ/drum stay byte-identical). 5 sampled-
   instrument RMS/SHA baselines regenerated deterministically. Hard clicks 955 → 0.
   IMPORTANT FALSIFICATION: an "aliasing in the varispeed resampler" theory was investigated,
   built, and FALSIFIED (the fix was inaudible; the HF-roughness-by-pitch evidence was a
   metric artifact). Don't re-chase aliasing. Session:
   `.planning/debug/resolved/varispeed-aliasing-static.md`.

### New: `--no-dynamics` flag — `93354c6`
`flow midi2flow IN.mid -o OUT.flow --no-dynamics` suppresses the per-bar dynamic markings the
converter normally emits. Threaded `bool emitDynamics = true` through
`FlowGenerator.GenerateWithStats → WriteSequence → FormatBar → FormatElements`; default
output is byte-identical. Quick task: `.planning/quick/260626-e76-...`.

### "Dreamy/washy" piano — NOT a bug, a render knob
The converted piano sounded washy because `play`/`writeWav` use the sampled piano's **1.5 s
default release tail** (`SampledInstrumentRenderer.DefaultReleaseSec`), so short notes ring
~0.8 s and overlap. For crisp, true-to-the-MIDI piano, render with a short release:
`Buffer b = (renderSong s "piano" 0.4s)` then `(play b)`. (0.4 s was the chosen value.)

### Known limitations / follow-ups (NOT done)
- The converter splits one piano part into ~6 voices (RH/LH × first-fit). Faithful but
  produces many `trackN_seq` sequences; an excerpt needs ALL voices to sound right (see §Part 2).
- Sampled instruments don't sound on the Web target (see §Part 2) — converter output that
  uses `"piano"` is silent in the browser playground.
- Staccato/Marcato `sustain=0` tail seam is a separate, pre-existing articulation-tail
  decision (recorded in the knowledge base; not in the ragtime).
- `flow midi2flow` keeps only the FIRST tempo when a MIDI has tempo changes (ragtime.mid had 15).

### Demo artifacts (in `~/Downloads/`)
`ragtime.flow` (full convert, sampled piano), `ragtime_piano.flow` (crisp 0.4 s release),
`ragtime_organ.flow`, `ragtime_sine.flow`, plus rendered WAVs. (These are scratch/demo, not in the repo.)

---

## Part 2 — flowlang.dev site (`flow-site/`)

> `flow-site/` is greenfield SvelteKit 2 / Svelte 5 / TS / pnpm — repo-root C# conventions
> do NOT apply. The playground runs Flow IN-BROWSER via the committed Phase 48 WASM runtime.

### Examples now load from `static/examples/` — `21a4b25`
Refactored the playground from a hardcoded `SNIPPETS` array (`src/lib/playground/snippets.ts`)
to a manifest-driven dynamic load:
- `static/examples/manifest.json` — ordered `[{id,label,blurb,file}]`.
- `static/examples/<id>.flow` — one source per example (served at `/examples/...`).
- `snippets.ts` is now an async loader (`loadManifest` / `loadSnippetSource`, plus
  `DEFAULT_SNIPPET_ID` / `BLANK_SOURCE`). `state.svelte.ts` `loadSnippet` is async;
  `+page.svelte` fetches the manifest in `onMount` and the default source feeds
  `initialValue = pendingGistSource ?? arrival.source ?? defaultSource`.
- Home hero snippets are SEPARATE (`src/routes/+page.svelte`, guarded by
  `home-deeplinks.test.ts`) — untouched.
- **To add an example:** drop a `.flow` in `static/examples/` + a manifest entry. NB:
  `.gitignore` ignores `*.flow` repo-wide, so `git add -f` the example files.
- New vitest: `src/lib/playground/snippets.test.ts`. Quick task:
  `.planning/quick/260626-n7r-...`.

### Ragtime example added — `21a4b25` (+ `d4322f0`)
A 24-bar, six-voice excerpt of the converted ragtime. **Must use a synth voice on web**
(see below) — committed as `(play (renderSong s "organ"))` in `d4322f0` after the sampled
piano was found silent in the browser.

### GOTCHA: sampled instruments are SILENT on the Web target
The U-Iowa sample bundle is stripped on `FlowTarget=Web` (Phase 47), and
`PianoSynthesizer.RenderNote` returns `CreateSilence` when samples are absent (it does NOT
fall back to synthesis). So `renderSong "piano"` (and brass/sax/strings/flute/bell) = silence
in the playground. Only pure-synth voices sound on web: **sine, saw, square, triangle, organ,
bell, wavetable**. Playground examples must use one of these.

### Every-other-run silence — FIXED `0c3073e`, `648c15d` (JS-only)
Pressing Run alternated audible/silent. Root cause: `WasmEntry.NewEngineForRun` disposes the
prior `FlowEngine` per run → `WebAudioBackend.Dispose` → JS `closeContext` did `ctx.close()`
+ nulled the one-per-tab AudioContext (D-48-08) MID-RUN, so the alternate run's `(play)` ran
against a closed context. Fix (JS-only, confirmed in-browser via playwright `?e2e=1`
observing `window.__flowAudioCtx.state`): `closeContext` now drains active sources but never
closes/nulls the tab-lifetime context. Edited BOTH `flow-site/static/wasm/flow-runtime.js`
(served) and `flow-lang/wasm/flow-runtime.js` (canonical, so a republish won't regress it).
Session: `.planning/debug/resolved/playground-every-other-run-silence.md`.

### GOTCHA: the committed WASM runtime does NOT auto-update — `ea0d3da`
`flow-site/static/wasm/` is the playground's engine, committed verbatim (CF Pages builds
pure-Node). A flow-lang C# fix does NOT reach the playground until you re-publish:
```
bash flow-site/scripts/sync-runtime.sh   # = dotnet publish -p:FlowTarget=Web, then copy AppBundle → static/wasm/
```
(needs the `wasm-tools` workload). This bit us: after fixing the timing bug on desktop, the
playground ragtime STILL drifted because it ran the stale pre-fix runtime. The republish
(`ea0d3da`) rebuilt `flow-lang.wasm` (now carries `f038c1b` + `487f248`); the canonical
`flow-runtime.js` (with the closeContext fix) was preserved byte-identically. JS-only fixes
reach web by editing the two `flow-runtime.js` copies; C# fixes require the full republish.

### Verifying the playground locally
`pnpm -C flow-site dev` (port chosen at launch; was 5179) → hard-reload (Ctrl+Shift+R) to
bypass the cached `.wasm`. Pre-existing `pnpm -C flow-site check` (svelte-check) noise:
vitest globals aren't in tsconfig `types`, so all `*.test.ts` fail type-check though they
RUN green under vitest — a one-line tsconfig `types` add would green it (not done).

---

## Commit ledger (all on `dev`, 2026-06-26)

| Commit | What |
|---|---|
| `f038c1b` | fix(note-stream): preserve IsChordTone in velocity interpolation (timing) |
| `93354c6` | feat(midi2flow): add --no-dynamics flag |
| `487f248` | fix(sampled-render): envelope ends at sustain — kills per-beat click |
| `7d19477` | docs: resolve varispeed-aliasing-static + knowledge base |
| `21a4b25` | refactor(flow-site): examples from static/examples + Ragtime snippet |
| `d4322f0` | fix(flow-site): ragtime snippet → organ (sampled piano silent on web) |
| `0c3073e` | fix(wasm-runtime): keep tab AudioContext alive across runs |
| `648c15d` | docs(debug): resolve + archive every-other-run-silence |
| `ea0d3da` | chore(flow-site): republish WASM runtime AppBundle |

(`a299dd2` favicon change also landed on `dev` — not part of this session's work.)

---

## Deployment status
NOT deployed. All commits are on `dev`. flowlang.dev (CF Pages, prod branch `main`,
direct-upload) will not have any of this until a deploy. The flow-site dev server started
this session was stopped.

## Knowledge base / memory
- `.planning/debug/knowledge-base.md` updated with two bug classes: (1) per-bar
  `MusicalNoteData` reconstruction must use `.With(...)`, not the positional ctor; (2) WASM
  per-run engine recycle must not tear down tab-lifetime singletons (AudioContext).
- External memory added: `project_flow_site_playground_runtime` (web sampled-instrument
  silence; static/examples loading; the republish requirement). `project_sampled_piano_short_sustain`
  updated with the crisp-release recipe.

## Suggested next steps
1. Deploy flow-site to CF Pages if the playground changes should go live.
2. (Optional) Fix the `svelte-check` tsconfig `types` noise.
3. (Optional, v1.6) Candidate-B C# decoupling of the AudioContext from the per-run engine
   (more robust than the JS-only closeContext fix); converter per-track polyphony / tempo-map
   improvements.
</content>
