# Phase 38: Live Coding 2.0 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-23
**Phase:** 38-live-coding-2-0
**Areas discussed:** `live` block scope + watch mode posture, REPL surface (`?fn` + piano-roll + multiline), Live block timeout + stale-closure recovery UX, OSC type tags (explicit vs charitable)

---

## `live` Block Scope + Watch Mode Posture

### Q1: When a composer runs `flow watch file.flow` on a script with NO `live { }` block, what should happen?

| Option | Description | Selected |
|--------|-------------|----------|
| Whole-script becomes hot-swappable (existing behavior) | Drop-in. Modernized watch keeps current `LiveReloadManager` semantics (whole-script re-render at bar boundary). `live { }` becomes optional precision tool for finer-grained quantize. Zero migration cost. | ✓ |
| Opt-in only — no `live` block = no hot-swap | `flow watch` without `live { }` becomes 'restart-on-save'. Cleaner separation but composer's existing .flow files all break. | |
| Opt-in but `flow watch` autowraps | Watch mode implicitly wraps script body in `live 1bar { }`. Consistent language model but pays SFZ-load cost on every reload. | |

**User's choice:** Whole-script hot-swappable (drop-in). Existing behavior preserved.
**Notes:** Migration burden zero — composer keeps existing .flow files unchanged. `live { }` opts into finer-grained quantize control.

### Q2: If a file contains multiple `live` blocks with different quantize values, how should they interact?

| Option | Description | Selected |
|--------|-------------|----------|
| Each block swaps independently at its own quantize | Per-block pending-buffer + bar counter. Max expressiveness (mix fast drums + slow pad). | ✓ |
| Only one `live` block per file — parser error if 2+ | Simplifies state model. Lower expressivity. | |
| Multiple allowed but synchronized to GCD quantize | Predictable global rhythm but defeats per-block quantize purpose. | |

**User's choice:** Each block independent.
**Notes:** Status panel will list each active block per D-38-08.

### Q3: When a `live` block content changes and hot-swaps, what re-evaluates: just the block body, or the whole file?

| Option | Description | Selected |
|--------|-------------|----------|
| Whole file re-evaluates, then live block body swaps | Simple model. File-scope setup re-runs every save (SFZ load cached). | |
| Only the live block body re-evaluates | File-scope bindings frozen at first run. Composer restarts `flow watch` for setup changes. Performance-lock mental model. | ✓ |
| Whole file re-evals + `setup { }` freeze block | Adds new language construct — scope creep. | |

**User's choice:** Only live block body re-evaluates (file-scope frozen when `live { }` exists).
**Notes:** Coherent with Q1 — no `live { }` = whole-script hot-swap; with `live { }` = performance lock on setup.

### Q4: Given file-scope is frozen when `live { }` exists, what happens if composer edits code OUTSIDE any `live { }` block during a session?

| Option | Description | Selected |
|--------|-------------|----------|
| Silent no-op | Cleanest UX but composer doesn't know edits aren't applying. | |
| One-shot stderr advisory per file-scope edit | `[live] file-scope edit detected outside live blocks at line N — restart `flow watch` to apply.` Dedup per (file, line). | ✓ |
| Auto-restart | Kills playback mid-set. Violates Pitfall #12 lock. | |

**User's choice:** One-shot stderr advisory.
**Notes:** Matches Flow's existing stderr-advisory pattern (`[tuning]`/`[abc]`/`[mml]`).

---

## REPL Surface — `?fn` + piano-roll + multiline

### Q1: How should `?fn` inline help be triggered in the REPL?

| Option | Description | Selected |
|--------|-------------|----------|
| Bare `?transpose` at line start — special parse | Matches roadmap wording `?fn`. Cheapest typing. | |
| `:help transpose` meta-command | Extends existing `:quit`/`:help`/`:clear`/`:stop` family. Consistent grammar. | ✓ |
| Both — `?transpose` shorthand AND `:help transpose` | Both forms ship; small parse-rule cost. | |

**User's choice:** `:help fn` meta-command (overrides REQUIREMENTS.md REPL-02 wording).
**Notes:** Consistency with existing meta-commands wins over wording-literal. REQUIREMENTS.md gets updated at Plan 38-07 closer per D-v1.5-01 latitude.

### Q2: Piano-roll on `(inspect seq)` vs existing `(visualize seq)` builtin?

| Option | Description | Selected |
|--------|-------------|----------|
| Extend `visualize` — add articulation glyphs + bar tick marks | `(inspect seq)` becomes alias. Charitable to existing scripts. | ✓ |
| Ship `inspect` as richer overload — `visualize` stays terse | Two builtins, two use cases. | |
| Replace `visualize` with `inspect` (deprecate) | Single canonical surface but existing examples get noisy. | |

**User's choice:** Extend `visualize`; `(inspect seq)` is a builtin-level alias.
**Notes:** Adds articulation glyphs (Accent `>`, Staccato `.`, Marcato `^`, Tenuto `_`, Sforzando `!`, Legato `~`) per Phase 28 enum mapping.

### Q3: REPL-03 line editor implementation approach for Ctrl+R history + multi-line?

| Option | Description | Selected |
|--------|-------------|----------|
| Hand-roll TUI line editor on `Console.ReadKey()` | No new dep. ~400-600 LOC. Tracks Flow's minimal-deps principle. | |
| Pull in `ReadLine.NET`-style lightweight readline | ~10 LOC integration. New NuGet dep with license + maintenance check. | ✓ |
| Defer Ctrl+R + multi-line UX to v1.6 — ship history file only | Trims heaviest sub-item. Scope cut. | |

**User's choice:** `ReadLine.NET`-style library (specific pick at plan-start per researcher gate).
**Notes:** Adds 3rd new dep in v1.5 (after Rug.Osc Phase 38 + RtMidi.Core Phase 40 + WASAPI/OwnAudioSharp Phase 41). License + maintenance + .NET 10 compat MANDATORY at plan-start; hand-roll fallback reserved if gate fails.

---

## Live Block Timeout + Stale-Closure Recovery UX

### Q1: 30s wall-clock evaluation cap fires — what does composer see and hear?

| Option | Description | Selected |
|--------|-------------|----------|
| Revert silently — keep previous buffer + stderr advisory | Charitable. Matches "never die mid-set" Pitfall #12 lock. | ✓ |
| Mute + advisory — silence the offending live block until next save | Audibly honest but possibly jarring. | |
| Throw — halt playback entirely, exit `flow watch` | Hard fail. Unusable in live set. | |

**User's choice:** Revert silently + dedup'd stderr advisory.
**Notes:** Advisory: `[live] evaluation timed out at 30s — keeping previous version`. Dedup per (error_kind, line).

### Q2: Stale-closure detection (closure references removed binding) — what happens?

| Option | Description | Selected |
|--------|-------------|----------|
| Revert silently — same UX as 30s timeout | Consistent recovery model across all failure modes. | ✓ |
| Substitute Void for missing binding + advisory + continue | Honest about composer intent; audible result may be jarring. | |
| Mute the offending live block + advisory | Different from timeout — composer can tell which failure mode hit. | |

**User's choice:** Revert silently (same UX as timeout).
**Notes:** Advisory: `[live] stale closure: references removed binding '{name}' at line N — keeping previous version`. Uniform recovery UX across timeout / stale-closure / parse error / runtime error.

### Q3: ANSI live status panel content — what shows in the modernized watch panel?

| Option | Description | Selected |
|--------|-------------|----------|
| Current tempo + time signature + bar number | Essential rhythm context. | ✓ |
| Active live block list + per-block quantize + last-swap-bar | Critical for multi-block coordination. | ✓ |
| Voice count + active synthesizer/instrument names | Useful for "why does this sound thin" diagnosis. | ✓ |
| Last advisory / error line (auto-cleared after N seconds) | Sticky single-line for recent advisories. | ✓ |

**User's choice:** All four rows.
**Notes:** Multi-line ANSI panel at top of terminal, redrawn in place via cursor moves. Plain-line fallback when stdout is not a TTY. Researcher decides exact ANSI sequences + redraw cadence (~10 Hz default suggested); auto-clear interval ~8s default at researcher's discretion.

---

## OSC Type Tags — Explicit vs Charitable

### Q1: How should arg → OSC type tag mapping work in `(oscSend ...)`?

| Option | Description | Selected |
|--------|-------------|----------|
| Charitable smallest-tag-that-fits inference | Map by Flow type. Matches D-v1.5-05 charitable posture. Escape hatch for explicit override. | ✓ |
| Strict per-arg — require composer to spell the tag | Matches OSC spec literal-mindedness + REQUIREMENTS wording. Violates Flow's charitable principle. | |
| Hybrid — inference default, opt-in strict via `(oscSendStrict ...)` | Best of both. Two builtins. | |

**User's choice:** Charitable inference (overrides REQUIREMENTS.md OSC-02 wording).
**Notes:** Int → `,i`, Long → `,h`, Float → `,f`, Double → `,d`, String → `,s`, Bool → `,T`/`,F`, Buffer → `,b`. Composer escape hatch via explicit cast or `types=",hd"` named arg (researcher picks exact form). REQUIREMENTS.md OSC-02 wording updated at Plan 38-07 per D-v1.5-01 latitude.

### Q2: Rate-limit overflow behavior (200 Hz/path cap exceeded)?

| Option | Description | Selected |
|--------|-------------|----------|
| Drop newest — oldest in-window message wins | Sample-and-hold semantics. Simplest. | ✓ |
| Coalesce — keep newest, drop intermediates | "Latest control value" semantics — better for continuous controllers. | |
| Drop with one-shot advisory — `[osc] flood at /fader/1 dropping N msg/s` | Same as drop-newest + composer gets stderr ping. | |

**User's choice:** Drop newest (oldest in-window wins, sample-and-hold).
**Notes:** Per-path `_lastFireTime` timestamp gate. Composer-side smoothing recommended for jitter-sensitive use cases. No per-drop advisory (would spam at flood).

### Q3: OSC bundle handling (incoming + outgoing)?

| Option | Description | Selected |
|--------|-------------|----------|
| Server auto-unpacks bundles; no client bundle surface | Minimal API. Covers ~95% of needs. | |
| Full bundle support — `(oscBundle msg1 msg2 ...)` builtin both ways | Larger API but expressive. Timetag honored. | ✓ |
| Bundles dropped — unsupported in v1.5 | Slim. Incompatibility risk with bundle-only senders. | |

**User's choice:** Full bundle support both directions, timetag honored on receive.
**Notes:** Server: auto-unpack incoming bundles, dispatch in order, honor timetag for future-scheduled messages. Client: `(oscBundle ...)` constructor + `(oscSendBundle host port bundle [timetag])`. Bundle nesting depth capped at 8 (T-38 DoS guard, mirroring Phase 36 T-36-17 / Phase 39 D-39-19 patterns). Rug.Osc's native `OscBundle` type leveraged.

---

## Claude's Discretion

Areas deferred to researcher / planner with no composer override:
- Exact ANSI escape sequence cadence for the 4-row status panel (10 Hz redraw vs event-triggered, color palette, exact TTY-detection fallback).
- Specific readline library pick (`ReadLine.NET` vs `PrettyPrompt` vs equivalent) — license + maintenance + .NET 10 compat gate at plan-start.
- Exact name and shape of the OSC type-tag escape hatch (`types=` named arg vs `(oscSendTyped)` separate builtin vs `(asOscFloat)` per-arg wrapper).
- Auto-clear interval for sticky advisory row in ANSI panel (proposed 8s).
- LSP-in-process sharing between REPL and live `flow watch` (single instance vs per-process).
- Exact backing builtin name for `(inspect seq)` / `(visualize seq)` alias pair.
- 200Hz overflow advisory shape (one-shot per path per process vs no advisory at all).
- PulseAudio capture default device name + whether `-20dB` auto-attenuation applies when no playback is active.
- Plan breakdown — suggested 7 plans (modernized watch / live block / state preservation / REPL polish / audio input / OSC / closer).

## Deferred Ideas

- Streaming audio input `(micStream callback)` — defer to v1.6.
- `setup { }` block (sibling to `live { }`) — rejected as scope creep.
- Composer-tunable micro-crossfade length — defer to v1.6 if click-artifacts surface.
- OSC address pattern wildcards (`/synth/*/freq`), IPv6, multicast — all v1.6.
- OSC server-side authentication / TLS — out of OSC 1.0 spec, defer indefinitely.
- Hand-rolled TUI line editor — only ships if D-38-11 readline library gate fails.
- Auto-restart on file-scope edit (as flag) — defer to v1.6 if composer demand surfaces.
- REPL syntax highlighting — not in REPL-01..04 scope; possible side-benefit if D-38-11 picks PrettyPrompt.
- Composer pause/resume hotkey for `flow watch` — not in LIVE-* scope; composer uses Ctrl+C.
