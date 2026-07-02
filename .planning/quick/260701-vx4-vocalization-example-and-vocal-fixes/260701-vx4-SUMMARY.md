---
quick_id: 260701-vx4
title: Add a vocalization playground example + two charitable-interpretation vocal-stdlib fixes
date: 2026-07-01
status: complete
commit: da00212
related_commits: [44da382]
---

# Quick Task 260701-vx4 — Summary

## What shipped

Three deliverables — a flowlang.dev playground example plus two charitable-interpretation
fixes to the vocal stdlib.

### 1. Playground example — "Choir of Circuits" (`vocal-choir`)

A ~14 s robotic vocal piece that shows off `sing`, Flow's **pure formant synthesizer**
(sawtooth buzz → Csound tenor vowel-formant bandpass filters). No samples, so it is fully
audible in the browser WASM playground.

- `flow-site/static/examples/vocal-choir.flow` (new) — the piece.
- `flow-site/static/examples/manifest.json` — appended a 10th entry (`vocal-choir`,
  "Choir of circuits (vocal synthesis)") as the last array element; blurb honestly notes
  the retro-robotic vocoder voice.

**Composition (what it demonstrates):**
1. **Vowel choir** — six sustained chords, each three `sing` voices `mix`ed together
   ("oo"/"oh"/"eh"/"ah" vowel colours over a C – Am – F – G – Am – C progression),
   `appendBuffers`'d into a 12 s line, reverbed and level-tamed.
2. **CV-syllable lead** — a two-phrase "na-na-ta / ta-na-ta-na-sa" line built with
   `appendBuffers` per syllable (onsets n/t/s), delayed 2 s and reverbed.
3. **Synth pad** — a warm triangle-oscillator drone on the C–G root (`createTriangleTone`,
   pure synthesis so it sounds on the web), lowpassed and faded.
4. **Mixdown** — each layer built in mono then `pan`ned into the stereo field (so every
   cross-layer `mix` has matching channel counts), glued with a master reverb + gain.

Tours `sing`, `mix`, `appendBuffers`, `reverb`, `pan`, `lowpass`, `volume`, `gain`,
`fadeIn`/`fadeOut`, `createSilenceMono`, `createTriangleTone`.

### 2. Charitable phoneme fallback (`FormantData.GetFormants`)

An unrecognized phoneme previously **threw** `ArgumentException` (violated the house
charitable-interpretation philosophy — a stray token halted the whole render). It now
degrades to the neutral `"ah"` vowel with a one-shot stderr advisory:

```
[vocal] unknown phoneme 'xy' — using 'ah' (valid: ah, ee, eh, oh, oo; onsets s/t/n)
```

Placed at `GetFormants` so it covers every caller — the direct vowel path, the
consonant-vowel unmapped-remainder path, and the whole-string fallback. Valid phonemes are
**byte-identical** (the found-branch is untouched). Uses the existing
`RenderingDiagnostics.WarnOnce` channel (per-phoneme sentinel), the same one-shot mechanism
as the `[tuning]` / `[module]` advisories.

### 3. tts Web-target guard (`TtsHook.RunTts`)

`tts` shells out via `Process.Start`, which hard-crashes on the Web target with a raw
`PlatformNotSupportedException` (the browser sandbox has no subprocess API, and Vocalization
is NOT Web-stripped — `tts` registers unconditionally). Wrapped the process path in
`#if !FLOW_WEB`; the `#if FLOW_WEB` branch emits a one-shot advisory and returns a 0.5 s
silent mono buffer:

```
[tts] external TTS unavailable on Web target — returning silence. Use sing for browser vocals.
```

`sing` (pure formant synthesis) remains the browser vocal path.

## Runtime-version safety (why the example works on the CURRENT bundle)

The committed WASM bundle (`flow-site/static/wasm/`, synced 2026-06-26) **predates** today's
resolver fix (commits 7962c61 / aa25f7a). The example was written to behave identically on
old and new runtimes:

- RAW DOUBLE SECONDS durations — `(sing "ah" C4 2.0)`, never `500ms`.
- Hz-first / Double-duration tone generators — `(createTriangleTone 131Hz 12.0 0.10)`
  (the shape proven by the committed `sine-440.flow`), never the new `…Hz …s` Second form.
- `createSilenceMono`, never the new `silence` alias.
- No bound `(applyEnvelope …)` (returned Void on the old runtime).
- No sampled instruments (silent on web) and no `tts` (crashes on web).

> **The flow-lang changes (phoneme fallback + tts guard) reach the LIVE playground only after
> a future `bash flow-site/scripts/sync-runtime.sh` bundle regen.** The example itself works on
> the current committed bundle because it avoids all new-runtime-only call shapes.

## Verification

- **Both builds green** on current HEAD: `dotnet build flow-lang/flow-lang.csproj` and
  `-p:FlowTarget=Web` — 0 errors each (only the pre-existing Rug.Osc NU1701 warning).
- `tests/test_vocalization.flow` — PASS (all valid phonemes, no advisory fires; the CI
  `FlowScriptData` PASS-sentinels are unaffected → no sentinel update needed).
- `tests/test_type_ergonomics.flow` — PASS.
- Unknown-phoneme eval `(sing "xy" C4 0.5)` — no longer throws; advisory on stderr, exit 0,
  22050-frame buffer.
- Example render (`writeWav` variant): exit 0, no errors / clipping warnings / unexpected
  advisories; **14.03 s stereo, peak 0.494 FS (−6.1 dBFS), RMS −24.2 dBFS** (comparable to
  windy-field's −23.4); render wall-time a small fraction of the 30 s WASM cap.
- Real example file with `(play master)` — exits 0, clean.
- `manifest.json` — valid JSON, 10 entries, `vocal-choir` last.

## Commits

| Scope | Commit | Notes |
|-------|--------|-------|
| flow-lang charitable fixes (`FormantData.cs` + `TtsHook.cs`) | **44da382** | Folded by the orchestrator into the concurrent vqz commit due to a shared-index collision (see below); the message credits vx4 (`… + charitable vocal fixes [260701-vqz+vx4]`). |
| Example + manifest (`vocal-choir.flow` + `manifest.json`) | **da00212** | Committed with explicit paths (`git commit --only -- …`) to stay race-safe. |
| GSD planning artifacts | this commit | PLAN.md + SUMMARY.md + STATE.md row. |

## Notes / deviations

- **Shared-index race (documented hazard).** While I had `FormantData.cs` + `TtsHook.cs`
  staged, a concurrent agent finishing the 260701-vqz work committed against the SAME working
  tree; the shared git index swept my two staged files into that agent's commit. The
  orchestrator re-credited the commit as `44da382`. Net effect: my flow-lang code is committed
  and correct (verified in HEAD + both builds green), but it did **not** get its own atomic
  commit — it rides `44da382`. I did NOT rewrite the shared history (live concurrent agent =
  high risk of clobbering work). Remaining commits used `git commit --only -- <paths>` to
  avoid recurrence.
- The new `.flow` was **force-added** (`git add -f`): the repo-root `.gitignore` has a blanket
  `*.flow` rule, so every `flow-site/static/examples/*.flow` is tracked only via force-add
  (matches the other 9 examples).
- No new NuGet packages; no runtime/bundle change committed.
