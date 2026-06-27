# Notation IO — Phase 39 chapters

Flow speaks the music-notation ecosystem's languages. Phase 39 adds 4 builtins
under `use "@notation-io"`:

- `(writeMusicXML song "file.musicxml")` — export to MusicXML 3.1 partwise
  (MuseScore reference consumer; opens in Sibelius / Dorico / Finale / LilyPond
  too).
- `(writeLilyPond song "file.ly")` — export to LilyPond 2.24+ source for
  engraving-quality printed scores.
- `(abc str)` — import ABC 2.1 subset + abc2midi extensions; returns
  `Section` (single-tune) or `Array[Section]` (multi-tune files with multiple
  `X:N` blocks).
- `(mml str)` — import PC-98-era MML common core; returns `Sequence`.

Distinct from the existing `@notation` module (musical-notation primitives like
note durations, rests, bar/sequence building — auto-loaded via `@std`).

## Reproduction prerequisites

- **`dotnet 10.x`** — required for all chapters.
- **`mscore` / `musescore4`** — optional. Needed if you want to render
  `to_musicxml.flow`'s output to PDF. The MusicXML round-trip CI gate
  (XML-02) charitable-skips when this binary is absent per D-39-08.
- **`lilypond` 2.24+** — optional. Needed if you want to compile
  `to_lilypond.flow`'s output to engraver PDF. Flow emits the `.ly` source
  correctly without it.

## Chapters

1. **`to_musicxml.flow`** — composer writes a 4-bar piano piece, exports it as
   MusicXML, opens in MuseScore (or any MusicXML-aware engraver).
2. **`to_lilypond.flow`** — same 4-bar piece, exports as LilyPond `.ly`,
   compiles to engraver PDF via `lilypond -dno-print-pages` if available.
3. **`from_abc.flow`** — composer imports a short ABC tune (4 bars, C major),
   demonstrates the `(abc str)` builtin returning a `Section`.
4. **`from_mml.flow`** — composer imports a short PC-98-style MML melody,
   demonstrates the `(mml str)` builtin returning a `Sequence`.

All four chapters are deterministic by construction (notation IO has no
PRNG); two consecutive runs of any chapter produce byte-identical output.

## Running

```bash
dotnet run --project flow-interpreter examples/notation/to_musicxml.flow
dotnet run --project flow-interpreter examples/notation/to_lilypond.flow
dotnet run --project flow-interpreter examples/notation/from_abc.flow
dotnet run --project flow-interpreter examples/notation/from_mml.flow
```

Each chapter also doubles as a regression test under `tests/test_notation_*_example.flow`
that runs via `flow test`.
