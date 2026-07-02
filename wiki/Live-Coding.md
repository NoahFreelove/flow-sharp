# Live Coding

Flow can hot-swap a running script while it plays. You edit the file, save, and the new version crossfades in at a bar boundary without stopping the audio. This is driven by `flow watch` and the `live { }` block.

> **Honest scope (read this first).** The `live <quantize> { }` block parses and records its quantize value, but `flow watch` currently performs **whole-script** swaps at 1-bar boundaries regardless of the declared quantize. Every `live` block in a file swaps together — the per-block independent quantize timelines are wired for **v1.6**. The voice-state preservation and stale-closure gates described below are fully implemented, but they too operate on the whole-script swap path. Where a feature is v1.6-only it is called out inline.

Live coding is **Desktop-only**. It requires a `FileSystemWatcher`, so it is stripped on the Web target — a `live { }` block in the browser playground is a parse-time error.

## `flow watch`

Point `flow watch` at a script and it renders once, starts playing, and then re-renders whenever the file changes on disk:

```bash
flow watch path/to/script.flow
```

- **Bar-quantized swap.** A re-render is applied at the next bar boundary, not immediately, so playback stays in time.
- **64-sample equal-power crossfade.** The old and new buffers are blended over 64 samples at the swap point — no click.
- **Failed renders keep the previous version.** If your edit has a parse or eval error, the last good version keeps playing and the error is surfaced in the `[live]` advisory row. You never get a silence-on-error gap.
- **200 ms trailing-edge debounce.** Editors often fire several save events in a burst; Flow waits 200 ms of quiet before rendering, so only the final save of a burst triggers a re-render.
- **30 s evaluation cap.** A single render is capped at 30 seconds of wall-clock time so a runaway script can't wedge the session.

## The `live { }` Block

Wrap the code you want to hot-swap in a `live <quantize> { }` block. The quantize value is one of `1bar` (also `2bar`, `4bar`, …), a note value (`q`, `h`), or omitted (defaults to `1bar`):

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            live 1bar {
                section main {
                    Sequence melody = | C4q D4q E4q F4q |
                }
                Song song = [main]
                Buffer result = (renderSong song "piano")
                (play result)
            }
        }
    }
}
```

Run it in watch mode, edit the `Sequence melody = | ... |` line, and save. The new melody swaps in at the next bar.

```bash
flow watch hello_live.flow
```

Without `flow watch`, a `live { }` block runs exactly once as ordinary code — the block markers are inert. You can commit a script with `live` blocks and still `flow run` it normally.

### Quantize Values Today

You can write different quantize values on different blocks, and they parse correctly:

```flow
live 1bar {  Note: drums — intended to swap every bar
    section drums { Sequence kick = | C2q _ C2q _ | }
    Song song = [drums]
    (play (renderSong song "drums"))
}

live 2bar {  Note: pad — intended to swap every 2 bars
    section pad { Sequence chord_pad = | C3w | F3w | }
    Song song = [pad]
    (play (renderSong song "strings"))
}
```

**Today both blocks swap together on the whole-script path at the 1-bar boundary.** The independent 1-bar / 2-bar timelines shown above are the intended v1.6 behavior, not the current one. The block still records its declared quantize for when that wiring lands.

## The Live Status Panel

In a real terminal, `flow watch` draws a four-row ANSI status panel that updates at ~2 Hz off the audio thread:

| Row | Shows |
|-----|-------|
| 1 | Tempo / TimeSig / current Bar |
| 2 | Active `live` blocks and their last-swap info |
| 3 | Voices N/M with a per-instrument breakdown |
| 4 | Sticky advisory (auto-clears after 8 s) |

The panel falls back to plain lines when output is redirected, or when `NO_COLOR` / `--no-color` is set / `TERM=dumb`. Real parse and eval diagnostics (with line numbers) surface in the row-4 advisory on every failed reload.

## State Preservation Across Reload

When a swap happens, Flow diffs the old and new voice sets by name so held notes and envelopes don't retrigger:

- Every rendered voice is tagged `{sequenceName}:{ordinal}` at allocation.
- `VoiceAllocator.DiffByVoiceName` classifies each voice as **Preserved** (name survives — its `OffsetBeats` is copied forward so the envelope keeps going), **Dropped** (name gone — a fade-out is applied), or **Added** (new name — cold start).

The net effect: renaming or re-arranging voices across an edit doesn't click, and voices whose identity survives keep playing seamlessly. (This runs on the whole-script swap path; per-live-block granularity is v1.6.)

## Stale-Closure Detection

If an edit removes a binding that a surviving lambda still captures, applying the new version would blow up mid-set. Flow's `LambdaCaptureAuditor` walks the new AST before the swap and, if it finds a lambda referencing a removed binding, keeps the previous version and emits:

```
[live] stale closure: references removed binding '<name>' at line N — keeping previous version
```

## File-Scope Edits

Editing outside every `live { }` block (for example changing the `tempo 120` header) does **not** hot-swap. Live mode treats setup as frozen for the duration of a set — the session never dies mid-performance. Flow emits a yellow advisory and waits for you to restart:

```
[live] file-scope edit detected outside live blocks at line N — restart 'flow watch' to apply
```

There is no auto-restart by design.

## Determinism Trade-Off

Entering a `live { }` block **opts out of the two-run byte-identical determinism contract**. On every entry Flow emits a one-shot stderr advisory (deduplicated per line):

```
[live] entering live block at line N — opts OUT of two-run cmp-clean determinism
```

This is intentional: live coding is about editing mid-set, and the determinism contract would only get in the way. **Offline render paths stay deterministic** — `writeWav` and `writeMidi` produce byte-identical output regardless of live state. Only the interactive `play` / `loop` / `preview` path opts out.

See [Design Philosophy](Design-Philosophy.md) for the full determinism story.

## See Also

- [Quick Start](Quick-Start.md) — Installing and running Flow, watch-mode basics
- [CLI and Tooling](CLI-and-Tooling.md) — Every `flow` verb, the REPL, the LSP
- [OSC and MIDI](OSC-and-MIDI.md) — Driving hardware and other apps live
- [Playback and Export](Playback-and-Export.md) — `play`, `stream`, deterministic offline export
- [Design Philosophy](Design-Philosophy.md) — Charitable interpretation and the determinism contract
