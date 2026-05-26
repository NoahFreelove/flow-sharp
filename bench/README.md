# Flow interpreter benchmark harness

Re-runnable harness measuring wall-clock + max RSS across six representative
interpreter hot paths. Language-side cost only — no audio I/O, no filesystem
writes, no `renderSong`/`writeWav`/`writeMidi` calls.

## Quick start

```bash
# Capture pre-optimization baseline (run from anywhere)
bash bench/run.sh --label baseline
cp bench/results-baseline-<timestamp>.txt bench/baseline.txt

# After an optimization bundle ships, re-run with a new label
bash bench/run.sh --label bundle-a
diff bench/baseline.txt bench/results-bundle-a-<timestamp>.txt
```

The script echoes the absolute path of the freshly-written results file on
its last stdout line, so the composer can pipe it into `cp` / `diff` without
hunting for the timestamp.

## What's measured

| Script                    | Hot path                                       |
| ------------------------- | ---------------------------------------------- |
| bench_function_calls.flow | function dispatch + var lookup + param binding |
| bench_collections.flow    | range / map / filter / reduce list-wrap allocs |
| bench_var_lookup.flow     | GetVariable resolution on bare identifiers     |
| bench_notestream.flow     | NoteStreamCompiler + MusicalContext lookup     |
| bench_parse.flow          | SimpleLexer + Parser on long file + imports    |
| bench_overload.flow       | OverloadResolver specificity scoring           |

Each script runs **5x** in Release mode. Reported values are **mean +
sample stddev** for wall-clock seconds (`%e` from `/usr/bin/time`) and max
resident-set MB (`%M` from `/usr/bin/time`, converted KB → MB).

## Tuned iteration counts

Tuned once per script so each takes roughly 1–5 s wall-clock on a modern
Linux .NET 10 box. The exact `N` / `R` lives at the top of each script as
a `Note:` comment with a brief tuning history. To retune for a different
machine, edit the literal inline and re-run `bash bench/run.sh --label
recap` to confirm the new value lands inside 1–5 s.

## Constraints

- **Linux only.** Uses GNU `/usr/bin/time -f` (BSD/macOS `time` does not
  accept `-f`). The harness bails out cleanly if `/usr/bin/time` is missing.
- **Release build only.** Debug-mode JIT skews measurements badly; `run.sh`
  always passes `-c Release` and uses `--no-build` after the initial build
  so JIT cost is paid once.
- **No production code modifications.** The harness lives entirely under
  `bench/`. `flow-lang/` and `flow-interpreter/` and all `.csproj` / `.sln`
  files are untouched.
- **No audio I/O.** None of the bench scripts call `writeWav`, `play`,
  `loop`, `preview`, `loadWav`, `writeMidi`, `writeMusicXML`, `writeLilyPond`,
  `micBuffer`, `oscSend`, `oscListen`, `renderSong`, `renderSequence`, or
  `renderSequences`. The composer can verify with:

  ```bash
  grep -E 'writeWav|writeMidi|loadWav|micBuffer|oscSend|renderSong|writeMusicXML|writeLilyPond|play |loop |preview ' bench/bench_*.flow \
      | grep -v '^[[:space:]]*Note:'
  ```

  An empty result confirms the no-audio-I/O invariant.

## Results files

Per-run output lands at `bench/results-<label>-<YYYYMMDD-HHMMSS>.txt`. These
per-run files accumulate over time and are intentionally gitignored
(`bench/results-*.txt` is re-ignored after the bench/ allow-list in the
top-level `.gitignore`).

The stable baseline lives at `bench/baseline.txt` and IS tracked — it's a
verbatim copy of the first `--label baseline` run and is the diff anchor
for every future `--label bundle-X` comparison.

## Adding a new bench

1. Create `bench/bench_<name>.flow` using only the language constructs in
   `CLAUDE.md` (note `proc name (Type: arg) ... end proc` — the proc body is
   delimited by `... end proc`, NOT `{ }`; `tempo`/`timesig`/`key` blocks
   DO use `{ }`).
2. Drive iteration via `(reduce (range 0 N) seed fn)` — there is no
   `for`/`while` in Flow.
3. Print exactly once at the end with a `bench_<name>: PASSED` sentinel.
4. Tune `N` inline so the script lands in 1–5 s.
5. `bash bench/run.sh --label sanity` will pick the new file up
   automatically (the driver globs `bench_*.flow` at the top level).
