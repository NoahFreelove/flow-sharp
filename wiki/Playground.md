# Playground

The Flow playground runs the **full interpreter in your browser** — no install, no backend. It lives at **[/playground](https://flow.noahfreelove.com/playground)**.

The same C# interpreter that powers the desktop CLI is compiled to WebAssembly (.NET 10 Mono-WASM) and shipped as a static bundle. When you press Run, your code is lexed, parsed, and evaluated entirely client-side, and any audio plays through the browser's Web Audio API.

## What You Get

- **Write and run Flow** in an in-browser editor with a console split for `print` output and advisories.
- **Hear it** — `(play ...)` routes to a real `AudioContext`. (Browsers require a user gesture before audio can start; press Run / the play control from a click.)
- **Download WAV and MIDI** — a script that calls `writeWav` / `writeMidi` surfaces the result as a downloadable file.
- **Example gallery** — a set of ready-to-run examples (12-bar blues, ragtime, DSP soundscapes, and more) load from the site's example manifest.
- **Structured errors** — parse / eval / runtime errors render as Rust-style diagnostic boxes with line, column, and a source snippet.

## Sharing

Two ways to share a sketch:

- **Share links (zero backend).** The playground compresses your code into a URL fragment (`#code=...`, fflate base64url, guarded against decompression bombs). The link carries the whole program — nothing is stored server-side. This is the default and works with no account.
- **Save to gist.** An optional GitHub OAuth path (scope `gist`, CSRF-protected, handled by a tiny Cloudflare Worker) saves your sketch as a real gist.

## Caveats

The browser build is ~85% of the language, but some things cannot run in a browser sandbox. Know these before you reach for them:

- **Sampled instruments are SILENT in the browser.** The piano, strings, flute, brass, sax, and bell synths depend on the University-of-Iowa sample bundle, which is not shipped to the web build. `renderSong song "piano"` renders **silence** for the sampled voices. Only the **synthesis** instruments make sound in the playground: `"sine"`, `"saw"`, `"square"`, `"triangle"`, `"organ"`, `"drums"`, and the wavetables `"warm"` / `"bright"` / `"buzz"`. Use those for anything you want to hear on the web.
- **`@sfz` is unavailable.** The SFZ sampler surface is stripped; `use "@sfz"` produces a charitable advisory.
- **`@osc`, `@midi`, `@jack` are unavailable.** Networking, realtime MIDI, and JACK all need native access that a browser tab can't have. `use` on any of them produces an advisory. See [OSC and MIDI](OSC-and-MIDI.md).
- **Microphone input (`micBuffer`) is unavailable** — the input functions are stripped on the web target.
- **`live { }` blocks are a parse-time error.** Live coding needs a filesystem watcher; it only exists on the desktop. See [Live Coding](Live-Coding.md).
- **`tts` does not work.** Text-to-speech shells out to `espeak-ng`, which isn't present in the sandbox. Formant `sing` (which is pure synthesis) does work.

Everything else — the whole core language, pattern matching, all music types, note streams and context blocks, the hand-rolled DSP (reverb, filters, granular, stretch, pitch-shift), the generative and improv stdlibs, notation-IO export, and MIDI *file* write — runs in the browser exactly as it does on the desktop.

## Determinism and Errors in the Browser

- Flow `print` output goes to the console pane; engine advisories (`[tuning]`, `[stretch]`, …) are shown separately.
- The same source produces byte-identical WAV/MIDI on repeated runs (the determinism contract holds in the browser too).
- There is no wall-clock cancel in the single-threaded WASM build — a runaway script hangs its own tab. Keep loops bounded.

## Running Flow Fully

For sampled instruments, SFZ, OSC/MIDI/JACK, mic input, and live coding, run the desktop build. See [Quick Start](Quick-Start.md) to install the `flow` CLI, and [CLI and Tooling](CLI-and-Tooling.md) for the full toolchain.

## See Also

- [Quick Start](Quick-Start.md) — Install the desktop build
- [CLI and Tooling](CLI-and-Tooling.md) — The `flow` CLI, REPL, and LSP
- [Audio and Synthesis](Audio-and-Synthesis.md) — Which synths are synthesis vs. sample-based
- [OSC and MIDI](OSC-and-MIDI.md) — Desktop-only realtime surfaces
- [Design Philosophy](Design-Philosophy.md) — The determinism contract and honest-scope shipping
