# Session handoff — 2026-06-09/10 audit + remediation

For the next Claude session (or human) picking this up. Context: composer asked for a
multi-agent codebase audit, then approved a 3-wave multi-agent fix run, then macOS
HUMAN-UAT smoke testing which surfaced more bugs (some fixed, some in flight).

## Git state

- `origin/dev` is at `94df2ed` (iOS-6 home redesign). NOTHING has been pushed.
- Local `dev` outgoing commits:
  - `8fee14b` — THE SQUASH: the entire audit remediation (~70 findings) as one commit
    with `Co-Authored-By: Claude Fable 5` (composer-requested trailer; note the global
    CLAUDE.md normally FORBIDS trailers — this was an explicit one-time instruction).
  - `36c91de` — CoreAudio drain fix (audit §3.3) — play() now blocks until audio finishes.
  - `d24d72d` — hello-world fix: `(createSineTone 440Hz 1.0 0.5)` had no frequency-first
    overload; 440Hz coerced into the DURATION slot → 440 s of inaudible 1 Hz. Hertz-first
    overloads added to all four tone procs in audio.flow + the C# builtin.
  - (+ pending: REPL interactive-fix commits from an Opus agent, see "In flight")
- The composer may want the post-squash commits squashed in too — ASK before pushing
  or re-squashing. Pushing requires explicit permission (global rule).
- `audit0609/*` branches retained for per-fix forensic history (the squash collapsed dev
  history). Safe to delete once the composer is done with them.

## What happened (full detail in these files)

- `.planning/CODEBASE-AUDIT-2026-06-09.md` — 56-agent audit, adversarially verified,
  ~70 findings with file:line + verifier corrections. §12 has the original sequencing.
- `.planning/CODEBASE-AUDIT-2026-06-09-FIXPLAN.md` — the wave plan + 7 approved
  decisions (D1-D7: REPL wiring yes, LIVE-03 minimal-honest, trailing-edge debounce
  overriding D-38-05 LOCK, reverb tail yes, lexical scoping both read+write blocking,
  WASM republish yes, GSD bypassed).
- Verification state at squash: dotnet test 0 failed / 2403 passed / 21 skipped (first
  ever 0-failure run on macOS); 134 .flow scripts; flow-site vitest 133/133; Playwright
  e2e 276/276 (first green since the iOS-6 redesign); Desktop+Web builds; two-run
  determinism byte-stable.

## In flight RIGHT NOW

An Opus agent (working in the MAIN checkout, committing as `fix(repl): ...`) is fixing
the composer's interactive smoke-test findings:
1. Completion dead on first prompt line (works after first Enter).
2. `use "` + Tab → no module suggestions live (unit test passes; live path differs).
3. Backspace after accepting a completion doesn't reopen suggestions.
4. `:help createSineTone` → "no documentation entry" (BuiltInDocs gaps — agent adding
   audio entries incl. the NEW Hertz-first signature).
5. Ctrl+R inert — likely caused by:
6. `~/.config/flow/history` mixed base64+plaintext (PrettyPrompt persists base64 AND our
   wiring manually appended plaintext → corrupt). Fix = single persistence owner +
   charitable migration. NOTE: the composer's real history file IS currently corrupted;
   after the fix, it should be auto-migrated (or tell them to delete it).
Agent verifies via PTY trick: `printf '...' | script -q /dev/null dotnet run --project
flow-interpreter` (gives the child a real TTY; \t=Tab, \x12=Ctrl+R).

**After the agent lands:** review commits, run `dotnet test --filter Repl`, then ask the
composer to re-test interactively: first-line completion, `use "` Tab, backspace-reopen,
`:help createSineTone`, Ctrl+R, fresh history file format, and one more listen to
`(play (createSineTone 440Hz 1.0 0.5))` (should be a clean 1 s 440 Hz beep, full tail).

## Remaining open items (composer/human)

- Pre-existing HUMAN-UAT gates: Phase 40 hardware MIDI (`40-HUMAN-UAT.md`), Phase 41
  binaries/Release (`41-HUMAN-UAT.md`), Phase 49 deploy/OAuth/cross-browser
  (`49-HUMAN-UAT.md`). The v1.5.0 GitHub Release cut is a human gate.
- REPL interactive retest (above).
- Decide push timing + whether to fold post-squash commits into one.

## v1.6 backlog seeded by the audit (not regressions — deliberately deferred)

Per-block live pipeline (D-38-02 wiring); jux L/R stereo; renderSong per-part
`instruments=` routing; effect automation/sweeps; midi2flow velocity/drums/tempo;
writeMidi pitch-bend microtones; sparse named args (OverloadResolver relaxation);
ExecutionContext module-state refactor (§8.2); section-final-bar reverb tail;
oscBundle has no public packet constructor (error text references nonexistent
`oscSendMessage`); no hex int literals (midiSysex takes Buffer). Audit §10/§11 has
the full feature-gap/idea lists (incl. 4 headline ideas).

## Environment / process quirks the next session MUST know

- **Agent worktrees fork from `origin/dev`, NOT local dev.** Until the squash is pushed,
  any `isolation: worktree` agent gets pre-audit code. Either work in the main checkout
  (serialize!) or push first. This caused every Wave-2/3 merge conflict this session.
- **flow-site pnpm install on this Mac needs `SHARP_IGNORE_GLOBAL_LIBVIPS=1`** (Homebrew
  libvips makes sharp try a source build that fails; the env var forces the prebuilt).
- `dotnet workload install wasm-tools` IS installed (needed for FlowTarget=Web publish +
  flow-site/scripts/sync-runtime.sh). Don't run builds DURING a workload install.
- Self-regenerating files that dirty the tree after test runs — restore, don't commit:
  `.planning/phases/42-*/42-AUDIT-data/*.txt`, `.planning/phases/48-*/48-BUNDLE-SIZE.md`,
  `flow-site/static/textures/*.avif` (pnpm build re-encodes). Phase37 fixture WAVs are
  now gitignored.
- Full-suite runs are contention-sensitive: DryWetMidiWasmPublishTests shells
  `dotnet publish` — never run other dotnet commands concurrently with the full suite
  (3 WASM tests fail under contention, pass solo).
- Shell discipline: piping dotnet/test output through `tail`/`grep` MASKS exit codes and
  truncates evidence — capture to a file, grep the file (this bit us twice).
- PTY testing for interactive console apps: `script -q /dev/null <cmd>` on macOS.
- `pkill -f flow-interpreter` if eval/play processes pile up (a pre-fix 440 s hello-world
  left zombies holding AudioQueues).
