# Phase 41: Reach + v1.5 Closer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-07
**Phase:** 41-Reach + v1.5 Closer
**Mode:** `--auto` (Claude auto-selected recommended defaults; no interactive prompts)
**Areas discussed:** Execution split, `flow doc` output & `///` grammar, Third-genre choice, Cross-platform binary strategy, Win/Mac audio backends

---

## Execution split — autonomous vs human-gated

| Option | Description | Selected |
|--------|-------------|----------|
| Run everything, fake the cross-platform gates | Produce binaries + claim audio works without hardware | |
| Run Linux-completable + code-write cross-platform; flag human gates honestly | Autonomous DOC/SHOWCASE/linux-binaries + write WASAPI/CoreAudio code; flag osx/win verify, JetBrains publish, GitHub Release as human | ✓ |
| Defer the entire phase to human | Wait for cross-platform machines before any work | |

**Auto-selection:** `[auto] Execution split — Selected: "Run Linux-completable + code-write; flag human gates" (recommended default — matches feedback_autonomous_phase_execution).`
**Notes:** "Flag, don't fake." Human gates → `41-HUMAN-UAT.md`, mirroring `40-HUMAN-UAT.md` / `49-HUMAN-UAT.md`.

---

## `flow doc` output & `///` grammar

| Option | Description | Selected |
|--------|-------------|----------|
| HTML + Markdown, `///` additive lexer grammar, examples-as-tests, BuiltInDocs source | `flow-cli` verb; static HTML at `docs/reference/`; examples run via TEST-01 | ✓ |
| HTML only, no example execution | Simpler, but DOC-02 unmet and examples can rot | |
| Markdown only | Greppable but not "browsable reference site" per DOC-01 | |

**Auto-selection:** `[auto] flow doc — Selected: "HTML + Markdown + /// additive grammar + examples-as-tests" (recommended default — satisfies DOC-01 + DOC-02; reuses BuiltInDocs.cs + Phase 35 test framework).`
**Notes:** Charitable — missing `///` → signature-only entry, never an error. Content-hash incremental cache. Static HTML only for v1.5 (search/interactive deferred).

---

## Third-genre choice (SHOWCASE-01)

| Option | Description | Selected |
|--------|-------------|----------|
| EDM | Strongest fit for required feature checklist (granular/time-stretch, generative, four-on-the-floor patterns, sidechain, live block, MIDI); max contrast vs symphony+ragtime | ✓ (recommended default — composer confirms) |
| Death metal | Boldest genre-agnostic proof; tremolo patterns, blast beats, aggressive synthesis | |
| Jazz | Leans on @improv jam + swing + Markov, but overlaps existing markov_jazz.flow | |

**Auto-selection:** `[auto] Third-genre — Selected: "EDM" (recommended default — best feature-checklist fit + max contrast). FLAGGED composer-overridable: this is a creative choice, one .flow file, trivially swappable.`
**Notes:** Genuine composer decision — surfaced prominently for confirmation before execution per `feedback_ergonomics_priority` + `project_genre_agnostic`.

---

## Cross-platform binary strategy (BIN-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Build all 5 RIDs from Linux (cross-compile), verify linux only, no trim, stage Release | All 5 self-contained binaries autonomously; osx/win execution + Release = human | ✓ |
| Build + trim all RIDs | Smaller binaries, but trimming breaks reflection-heavy registry | |
| Only build linux, defer rest | Leaves Mac/Win users unable to run Flow — fails BIN-01 intent | |

**Auto-selection:** `[auto] Binaries — Selected: "Build all 5 RIDs from Linux, no trim, verify linux, stage Release" (recommended default).`
**Notes:** No `PublishTrimmed` (reflection-heavy `InternalFunctionRegistry`). Naming `flow-<rid>-v1.5.0.tar.gz`/`.zip` + `.sha256`. GitHub Release cut = human (outward-facing publish gate).

---

## Win/Mac audio backends (WASAPI-01 / COREAUDIO-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Write WasapiBackend (NAudio.Wasapi); keep existing hand-rolled CoreAudio; human smoke-test | Both backends compile-clean + probe-gated; OwnAudioSharp conditional | ✓ |
| Rewrite macOS on OwnAudioSharp now | Speculative — existing CoreAudio already written, unverified | |
| Hand-roll WASAPI P/Invoke | Reinvents NAudio.Wasapi; more risk, no benefit | |

**Auto-selection:** `[auto] Audio backends — Selected: "WasapiBackend via NAudio.Wasapi + keep hand-rolled CoreAudio; OwnAudioSharp deferred to failed-smoke-test" (recommended default).`
**Notes:** NAudio.Wasapi PackageReference must be Desktop-only (Web forbidden-prefix gate). Shared-mode default, exclusive-mode config opt-in. Real-hardware verification = human gate.

---

## Claude's Discretion

- `flow doc` HTML template/styling (functional reference site, not a design contract).
- Doc-comment AST attachment internals + content-hash cache key.
- Showcase piece's specific generative/DSP knobs (composer owns musical content; Claude owns feature-checklist scaffolding).

## Deferred Ideas

- OwnAudioSharp macOS swap — only on a failed >20 ms latency smoke-test.
- Binary trimming + source-gen `InternalFunctionRegistry` — v1.6 (also unblocks NativeAOT-LLVM WASM).
- `flow doc` search/interactive features — v1.6.
- Phase 49 live deploy/OAuth/cross-browser + Phase 40 hardware UAT — sibling phases' human gates, required for milestone close, tracked in their own HUMAN-UAT files.
