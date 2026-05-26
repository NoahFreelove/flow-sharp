---
phase: quick-260524-qnf
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - bench/bench_function_calls.flow
  - bench/bench_collections.flow
  - bench/bench_var_lookup.flow
  - bench/bench_notestream.flow
  - bench/bench_parse.flow
  - bench/bench_parse_imports/m01.flow
  - bench/bench_parse_imports/m02.flow
  - bench/bench_parse_imports/m03.flow
  - bench/bench_parse_imports/m04.flow
  - bench/bench_parse_imports/m05.flow
  - bench/bench_overload.flow
  - bench/run.sh
  - bench/README.md
  - bench/baseline.txt
autonomous: true
requirements: []

must_haves:
  truths:
    - "Composer can run `bash bench/run.sh --label <name>` from repo root and get a results file plus stdout markdown table"
    - "Six `.flow` benchmark scripts under `bench/` each run cleanly on the current dev branch in Release mode without errors"
    - "No benchmark script invokes writeWav / play / loop / preview / loadWav / writeMidi / writeMusicXML / writeLilyPond / micBuffer / oscSend / renderSong file output — language cost only"
    - "Each script measures one of the six target hot paths: function calls, collection pipeline, var lookup, note stream compile, parse, overload resolution"
    - "Each script takes roughly 1-5 s wall-clock on a modern Linux .NET 10 box (tune iteration counts inline if needed)"
    - "run.sh runs each script N=5 times and reports mean + sample stddev for wall-clock seconds and max RSS MB"
    - "run.sh builds the project in Release mode once at the start, then invokes the CLI with `-c Release --no-build` so the JIT cost is paid once"
    - "Production code under flow-lang/ and flow-interpreter/ is untouched (zero changes to .cs / .csproj / .flow stdlib files); the only new top-level dir is bench/"
    - "After harness creation the composer captures pre-optimization baseline via `bash bench/run.sh --label baseline` and `bench/baseline.txt` is a stable filename copy of that results file"
  artifacts:
    - path: "bench/bench_function_calls.flow"
      provides: "Hot path: function dispatch + var lookup + parameter binding"
      min_lines: 10
    - path: "bench/bench_collections.flow"
      provides: "Hot path: range/map/reduce list-wrap allocations"
      min_lines: 10
    - path: "bench/bench_var_lookup.flow"
      provides: "Hot path: GetVariable throw/catch fast path on bare identifiers"
      min_lines: 10
    - path: "bench/bench_notestream.flow"
      provides: "Hot path: NoteStreamCompiler + MusicalContext lookups"
      min_lines: 10
    - path: "bench/bench_parse.flow"
      provides: "Hot path: lexer + parser on a long file with many imports + decls"
      min_lines: 50
    - path: "bench/bench_overload.flow"
      provides: "Hot path: OverloadResolver specificity scoring across numeric types"
      min_lines: 10
    - path: "bench/run.sh"
      provides: "Build-once-then-loop driver with /usr/bin/time -f '%e %M' x5 + mean/stddev table"
      min_lines: 60
    - path: "bench/README.md"
      provides: "Composer usage: baseline capture + post-bundle comparison + results location"
      min_lines: 10
    - path: "bench/baseline.txt"
      provides: "Stable-filename copy of pre-optimization run for downstream diffing"
  key_links:
    - from: "bench/run.sh"
      to: "flow-interpreter (Release build)"
      via: "dotnet build -c Release flow-sharp.sln, then dotnet run --project flow-interpreter -c Release --no-build -- <script>"
      pattern: "dotnet (build|run).*-c Release"
    - from: "bench/run.sh"
      to: "/usr/bin/time"
      via: "stderr capture of '%e %M' (wall seconds + max RSS KB) per run"
      pattern: "/usr/bin/time -f"
    - from: "bench/run.sh"
      to: "bench/results-<label>-<timestamp>.txt + bench/baseline.txt"
      via: "tee-equivalent markdown table emit and cp to stable filename"
      pattern: "results-.*\\.txt"
---

<objective>
Build a re-runnable benchmark harness under `bench/` that measures Flow interpreter wall-clock and memory cost on six representative hot paths before/after each of six upcoming optimization bundles.

Purpose: Give the composer a one-command baseline (`bash bench/run.sh --label baseline`) and post-bundle delta (`bash bench/run.sh --label bundle-a`) so each optimization can be quantified independently. The harness measures language-side cost only (zero audio I/O), uses Release builds, and runs each script 5× to get mean + stddev signal.

Output:
- 6 `.flow` benchmark scripts (function calls, collections, var lookup, notestream, parse, overload) + 5 small import-target files for the parse bench
- 1 driver script `bench/run.sh` with --label flag, Release build, 5× repeat, mean+stddev markdown table
- 1 brief `bench/README.md`
- 1 captured `bench/baseline.txt` (pre-optimization run, stable filename)
- Zero changes to flow-lang/, flow-interpreter/, *.csproj, or `.flow` stdlib files
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md
@tests/test_range.flow
@tests/test_lambdas.flow
@tests/test_note_streams.flow

<interfaces>
Flow language constructs the bench scripts will use (all confirmed via tests/test_*.flow inspection):

- Comments: `Note: ...` (NOT `//` or `#`). Anything after `Note:` to end-of-line is ignored.
- Module imports: `use "@std"`, `use "@collections"`, `use "@audio"`, `use "@composition"`. Stdlib aliases are `@`-prefixed.
- Proc declaration: `proc name (Type: arg1, Type: arg2) { body }` — note the colon syntax (`Int: n` not `Int n`).
- Lambda: `fn Int n => (mul n 2)` ; multi-arg `fn Int a, Int b => (add a b)` ; zero-arg `fn => 42`.
- Arithmetic: ONLY prefix builtins — `(add x y)` `(sub x y)` `(mul x y)` `(div x y)` `(neg x)` `(idiv x y)`. NO infix `+ - * /`.
- Range / map / filter / reduce / each (all from `@collections` via `@std`):
    `(range 0 N)` → `Int[]` of [0, N)
    `(range 0 N step)` → `Int[]`
    `(map arr (fn Int n => ...))` → `Int[]`
    `(filter arr (fn Int n => (gt n 0)))`
    `(reduce arr 0 (fn Int acc, Int n => (add acc n)))` → `Int`
    `(each arr (fn Int n => (print (str n))))` → Void
  Note arg ORDER: lambda is LAST (`(map arr fn)`, `(reduce arr seed fn)`).
- Array length: `(len arr)` (NOT `length`).
- String concat: `(concat s1 s2)`.
- Stringify: `(str x)`.
- Print: `(print s)`.
- Variable typed decl: `Int x = 5`, `Int[] arr = (range 0 10)`, `Function f = fn ...`, `Sequence s = | C4 D4 |`.
- Note streams (require active timesig context): wrap in `timesig 4/4 { Sequence mel = | C4 D4 E4 F4 | }`.
- Section / Song: `section verse { Sequence s = | C4 D4 | (print (str s)) }` ; `Song g = [verse verse*2]`.
- No infix arithmetic, no `for`/`while` loops — iteration is via `(each)` / `(reduce)` / `(map)` over `(range ...)`.

How `dotnet run --project flow-interpreter` consumes flags:
- Flags BEFORE `--` are consumed by `dotnet run` itself (e.g. `-c Release`, `--no-build`).
- Flags AFTER `--` are passed to the Flow CLI (e.g. the script path).
- Correct form for bench harness: `dotnet run --project flow-interpreter -c Release --no-build -- bench/bench_X.flow`.

How `/usr/bin/time -f "%e %M"` works:
- `%e` = wall-clock seconds (float, e.g. `1.42`).
- `%M` = max resident-set size in KB (integer, e.g. `185432`).
- Output goes to STDERR by default; STDOUT carries the Flow program's prints.
- To capture cleanly: `result=$(/usr/bin/time -f "%e %M" dotnet run ... 2>&1 1>/dev/null)` — redirects program stdout to /dev/null, captures time's stderr.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create the six benchmark .flow scripts under bench/</name>
  <files>
    bench/bench_function_calls.flow,
    bench/bench_collections.flow,
    bench/bench_var_lookup.flow,
    bench/bench_notestream.flow,
    bench/bench_parse.flow,
    bench/bench_parse_imports/m01.flow,
    bench/bench_parse_imports/m02.flow,
    bench/bench_parse_imports/m03.flow,
    bench/bench_parse_imports/m04.flow,
    bench/bench_parse_imports/m05.flow,
    bench/bench_overload.flow
  </files>
  <action>
    Create the `bench/` directory and write six `.flow` benchmark scripts plus five small import-target files for the parse bench. Use ONLY existing language constructs verified in <interfaces> above — no new syntax, no audio I/O builtins, no filesystem writes.

    Per-script requirements:

    (a) `bench/bench_function_calls.flow` — exercise function dispatch + var lookup + parameter binding.
      - Imports: `use "@std"`.
      - Declare a user proc, e.g. `proc work (Int: a, Int: b) { (add a b) }` (implicit return of the (add ...) expression).
      - Drive a tight loop via `(reduce (range 0 N) 0 (fn Int acc, Int i => (work acc i)))` with N tuned to roughly 1–5 s on .NET 10 (start at N=200000; if first dry-run is < 0.5 s bump to 500000, if > 8 s drop to 100000).
      - Print only the final reduced value plus a `PASSED` sentinel for sanity (no per-iteration prints — print I/O dominates measurement).
      - End with `(print "bench_function_calls: PASSED")`.

    (b) `bench/bench_collections.flow` — exercise map/filter/reduce list-wrap allocations.
      - Imports: `use "@std"` (which transitively brings `@collections`).
      - Pipeline at N=10000: `Int[] xs = (range 0 N)`, then `Int[] doubled = (map xs (fn Int n => (mul n 2)))`, then `Int sum = (reduce doubled 0 (fn Int acc, Int n => (add acc n)))`.
      - Also include one filter pass: `Int[] evens = (filter xs (fn Int n => (equals 0 (sub n (mul (div n 2) 2)))))` to exercise the filter list-wrap path.
      - Drive the whole pipeline R times via an outer `(each (range 0 R) (fn Int i => ...))` where R~20; tune R so total runtime is ~1-5 s.
      - Print only the final `sum` value + `(print "bench_collections: PASSED")` at the end. Move per-iteration work into a proc so each loop iter calls it once (no in-flight prints).

    (c) `bench/bench_var_lookup.flow` — exercise GetVariable throw/catch fast path on bare identifiers.
      - Imports: `use "@std"`.
      - Bind ~20 named Int vars at top of file (a, b, c, ..., t) with small literal values.
      - Define a proc that references ALL of those bound vars in arithmetic combinations (forces lookup of each), e.g. `proc touchAll () { (add a (add b (add c ... t))) }`.
      - Call that proc N=300000 times via `(reduce (range 0 N) 0 (fn Int acc, Int i => (add acc (touchAll))))` (tune N for ~1-5 s).
      - Final `(print "bench_var_lookup: PASSED")`.

    (d) `bench/bench_notestream.flow` — exercise NoteStreamCompiler + MusicalContext lookups.
      - Imports: `use "@std"`.
      - Wrap in `tempo 120 { timesig 4/4 { ... } }` block so notestream compile has context.
      - Inside the block, define a proc `compileBar` whose body is exactly `Sequence s = | C4 D4 E4 F4 G4 A4 B4 C5 D5 E5 F5 G5 A5 B5 C6 D6 |` followed by `(len s)` to force materialization. (16-note stream is moderate; the literal must appear at parse time inside the proc body so each call re-compiles via NoteStreamCompiler.)
      - Drive it R=5000 times via `(reduce (range 0 R) 0 (fn Int acc, Int i => (add acc (compileBar))))`. Tune R for ~1-5 s.
      - Final `(print "bench_notestream: PASSED")`.

    (e) `bench/bench_parse.flow` — exercise lexer + parser on a long file with many imports + many decls.
      - At top: `use "@std"`, `use "@collections"`, `use "@audio"`, `use "@composition"`.
      - Then 5 lines: `use "./bench_parse_imports/m01.flow"` through `m05.flow` (relative-path module load, exercises ModuleLoader). These files MUST exist (see below).
      - Then ~150 simple statements: alternating `Int aNNN = NNN`, `proc pNNN (Int: x) { (add x NNN) }`, `Int rNNN = (pNNN NNN)`, `(print (str rNNN))` style declarations. Goal is ≥ 100 declarations so the parse dominates execution. Keep each statement simple (no nested calls) so the parser walk is the bulk of the cost; execution is incidental.
      - Final `(print "bench_parse: PASSED")`.
      - Also create the 5 import-target files `bench/bench_parse_imports/m01.flow` ... `m05.flow`. Each is ~30 lines of plain `Int kNN = NN` declarations + one `proc` + a single `(print "m0X loaded")`. They must parse cleanly when imported (composer test: `dotnet run --project flow-interpreter -- bench/bench_parse_imports/m01.flow` succeeds standalone).
      - Tune total length so wall-clock is ~1-5 s; if the parse alone is < 0.5 s, expand the decl block to 300 lines.

    (f) `bench/bench_overload.flow` — exercise OverloadResolver specificity scoring across numeric types.
      - Imports: `use "@std"`.
      - Declare Int + Long + Float + Double vars at top: `Int i = 7`, `Long l = 7L` (if Long literal supported — verify by reading flow-lang/Lexing/SimpleLexer.cs for `L` suffix; if not supported, use `Long l = (toLong 7)` or omit Long and rotate Int/Float/Double only).
      - Define a proc that calls `(add)` repeatedly across mixed numeric types, e.g. `(add (add i f) (add d (add i f)))` — each `(add)` forces OverloadResolver to score Int+Float, Float+Double, etc.
      - Loop the proc N=200000 times via reduce. Tune N for ~1-5 s.
      - Final `(print "bench_overload: PASSED")`.

    SCRIPT-WIDE RULES:
    - NEVER call: `writeWav`, `play`, `loop`, `preview`, `loadWav`, `writeMidi`, `writeMusicXML`, `writeLilyPond`, `micBuffer`, `oscSend`, `oscListen`, `renderSong`, `renderSequence`, `renderSequences`. We measure language cost, not I/O.
    - NEVER write multi-line `(print ...)` chains in hot loops — print dominates. Hot loops should print ONCE at the end with the final reduced value + a `PASSED` sentinel.
    - Use only `Note: ...` for comments (NOT `//` or `#`).
    - All numeric loops drive iteration via `(reduce (range 0 N) seed fn)` or `(each (range 0 N) fn)` — there is no `for`/`while` in Flow.
    - Verify each script runs end-to-end before moving on: `dotnet run --project flow-interpreter -c Release -- bench/bench_X.flow` exits 0 and prints `bench_X: PASSED`. If a script errors, FIX the bench script (do NOT modify the language).
    - If a script runs faster than ~0.5 s or slower than ~8 s, retune the iteration count inline (the goal is 1-5 s for clean signal-to-noise). Document the final tuned N as a `Note:` comment at the top of the file.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet build -c Release flow-sharp.sln 2>&1 | tail -5 &amp;&amp; for s in bench/bench_function_calls.flow bench/bench_collections.flow bench/bench_var_lookup.flow bench/bench_notestream.flow bench/bench_parse.flow bench/bench_overload.flow; do echo "=== $s ===" &amp;&amp; dotnet run --project flow-interpreter -c Release --no-build -- "$s" 2>&amp;1 | tail -3 || echo "FAILED: $s"; done</automated>
  </verify>
  <done>All six bench/bench_*.flow scripts exist; each exits 0 in Release mode and prints its `bench_X: PASSED` sentinel; five bench/bench_parse_imports/m0N.flow files exist and parse cleanly when standalone; no production code was modified (verify via `git status -- flow-lang/ flow-interpreter/` shows no diffs).</done>
</task>

<task type="auto">
  <name>Task 2: Write bench/run.sh driver + bench/README.md, then capture baseline.txt</name>
  <files>bench/run.sh, bench/README.md, bench/baseline.txt</files>
  <action>
    Create the harness driver and docs, then run it once to capture the pre-optimization baseline.

    (a) `bench/run.sh` — bash driver. Required shape:

      ```
      #!/usr/bin/env bash
      set -euo pipefail
      ```

      then:
      - Parse `--label <name>` arg with a default of `current`. Reject unknown args with a usage message.
      - Resolve the repo root: `REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." &amp;&amp; pwd)"` so the script works regardless of cwd.
      - Build once at start: `dotnet build -c Release "$REPO_ROOT/flow-sharp.sln"` redirecting stdout to /dev/null but propagating exit code on failure. After this, every CLI run uses `--no-build` to avoid re-evaluating MSBuild on every iteration.
      - Build a sorted list of all `bench_*.flow` files in `$REPO_ROOT/bench/` (NOT recursive — exclude `bench_parse_imports/`).
      - For each script, run N=5 iterations. Each iteration: `/usr/bin/time -f "%e %M" dotnet run --project "$REPO_ROOT/flow-interpreter" -c Release --no-build -- "$script" >/dev/null 2> "$tmpdir/timing"` then read the timing line (the LAST line of stderr in the timing file — `/usr/bin/time` prints to stderr after the program exits) and parse `%e %M` via `read elapsed rss < <(tail -n 1 "$tmpdir/timing")`. Accumulate the 5 (elapsed, rss) pairs.
      - Compute mean and sample stddev (use bc -l or awk; awk is fine and avoids the bc dep). Stddev formula: `sqrt(sum((x-mean)^2) / (n-1))`. RSS reported in MB = KB/1024 (rounded to 1 decimal).
      - Emit a markdown table to STDOUT and to the results file. Columns: `| Script | Mean (s) | Stddev (s) | Mean RSS (MB) | Stddev RSS (MB) |`. Include a heading line `# Flow benchmark run — label: $LABEL — $(date -Iseconds)` and a build-info footer line with `dotnet --version` and `git rev-parse --short HEAD` (charitable: if `git` fails or not in a repo, emit `unknown`).
      - Results file path: `$REPO_ROOT/bench/results-$LABEL-$(date +%Y%m%d-%H%M%S).txt`. Echo the absolute path of the written file at the end on its own line so the composer can grab it.
      - Use a `trap` to clean up the tmpdir on exit.
      - If `/usr/bin/time` is missing (BSD time on macOS won't accept `-f`), bail out with a clear error message — this harness targets Linux .NET 10. (The CLAUDE.md states .NET 10 Linux is the dev box.)

    (b) `bench/README.md` — concise usage:

      ```
      # Flow interpreter benchmark harness

      Re-runnable harness measuring wall-clock + max RSS across six representative
      hot paths. Language-side cost only — no audio I/O, no filesystem writes.

      ## Quick start

          # Capture pre-optimization baseline (run from anywhere)
          bash bench/run.sh --label baseline
          cp bench/results-baseline-<timestamp>.txt bench/baseline.txt

          # After an optimization bundle ships, re-run with a new label
          bash bench/run.sh --label bundle-a
          diff bench/baseline.txt bench/results-bundle-a-<timestamp>.txt

      ## What's measured

      | Script | Hot path |
      |--------|----------|
      | bench_function_calls.flow | function dispatch + var lookup + param binding |
      | bench_collections.flow    | range/map/filter/reduce list-wrap allocations |
      | bench_var_lookup.flow     | GetVariable throw/catch fast path |
      | bench_notestream.flow     | NoteStreamCompiler + MusicalContext lookup |
      | bench_parse.flow          | lexer + parser on long file + many imports |
      | bench_overload.flow       | OverloadResolver specificity scoring |

      Each script runs 5x in Release mode. Reported values are mean + sample stddev.

      ## Constraints

      - Linux + .NET 10 (uses GNU `/usr/bin/time -f`)
      - Release build only (Debug skews measurements badly)
      - Zero modifications to production code
      ```

    (c) Capture baseline. After (a) and (b) land and `bash bench/run.sh --label baseline` succeeds:
      - `cp` the freshly written `bench/results-baseline-<timestamp>.txt` to `bench/baseline.txt` so downstream diffs against optimization bundles use a stable filename.
      - The script itself does NOT auto-copy — that's a one-shot manual `cp` after the first run, so re-running with `--label baseline` later doesn't silently clobber the historical baseline. (The composer keeps both: `bench/baseline.txt` stable + `bench/results-baseline-<ts>.txt` historical.)

    PERMISSION NOTES:
    - `chmod +x bench/run.sh` after creation so `./bench/run.sh` works, but the README documents `bash bench/run.sh` form to avoid permission ambiguity across systems.
    - Do NOT `git add` results files defensively — leave that to the composer. (Out of scope per quick mode; this task ships the harness + one baseline capture.)
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp &amp;&amp; bash -n bench/run.sh &amp;&amp; chmod +x bench/run.sh &amp;&amp; bash bench/run.sh --label baseline &amp;&amp; ls -la bench/baseline.txt bench/results-baseline-*.txt &amp;&amp; head -20 bench/baseline.txt</automated>
  </verify>
  <done>bench/run.sh exists, is executable, parses cleanly with `bash -n`, and successfully runs `--label baseline` end-to-end producing a results file. bench/baseline.txt is a verbatim copy of that results file. bench/README.md exists and describes the workflow. The baseline file contains a markdown table with one row per bench_*.flow script, each row showing mean/stddev for elapsed and RSS. Final `git status` shows ONLY new files under bench/ — zero changes to flow-lang/, flow-interpreter/, *.csproj.</done>
</task>

</tasks>

<verification>
After both tasks complete, sanity-check the harness invariants:

1. `git status --short -- flow-lang/ flow-interpreter/ '*.csproj' '*.sln'` shows NO modifications (production code untouched). Note: `flow-sharp.sln.DotSettings.user` and other unrelated pre-existing modifications shown in initial git status are pre-existing and NOT introduced by this plan.
2. `ls bench/` shows: 6 `bench_*.flow` scripts + `bench_parse_imports/` dir (with 5 files) + `run.sh` + `README.md` + `baseline.txt` + at least one `results-baseline-*.txt`.
3. `cat bench/baseline.txt` shows a markdown table with 6 rows (one per bench script) and non-zero mean values.
4. `grep -E "writeWav|writeMidi|loadWav|micBuffer|oscSend|renderSong|writeMusicXML|writeLilyPond|play |loop |preview " bench/bench_*.flow | grep -v '^[[:space:]]*Note:'` returns NO matches (no audio I/O in benches).
5. `bash bench/run.sh --label sanity` succeeds a second time, producing a fresh `results-sanity-*.txt` without overwriting `baseline.txt`.
</verification>

<success_criteria>
- All 6 bench scripts exit 0 in Release mode and print their `PASSED` sentinel
- bench/run.sh runs end-to-end with --label, builds once, runs each script 5×, emits markdown table with mean + sample stddev for elapsed seconds AND max RSS MB
- bench/baseline.txt is a stable-filename copy of the first --label baseline run
- Zero changes to production source (flow-lang/, flow-interpreter/, *.csproj, *.sln, flow-lang/*.flow stdlib)
- Composer can re-run `bash bench/run.sh --label bundle-X` after each upcoming optimization bundle and diff the results file against bench/baseline.txt to quantify deltas
</success_criteria>

<output>
Create `.planning/quick/260524-qnf-set-up-flow-interpreter-benchmark-harnes/260524-qnf-SUMMARY.md` when done summarizing:
- The 6 bench scripts created + their tuned iteration counts
- The baseline numbers (elapsed mean + RSS mean per script) so future bundle diffs have a memory anchor
- Any composer-observable quirks (e.g. "first run includes JIT cost — second --label baseline run would show steadier numbers" — only mention if actually observed)
</output>
