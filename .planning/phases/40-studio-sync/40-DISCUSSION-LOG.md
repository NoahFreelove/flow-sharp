# Phase 40: Studio Sync - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-06
**Phase:** 40-studio-sync
**Areas discussed:** MIDI surface & mapping, Sync activation syntax, Scope/priority & Link, Verification approach

---

## MIDI surface (primary composer-facing surface)

| Option | Description | Selected |
|--------|-------------|----------|
| Both, high-level first | `(midiOut song "port")` primary + low-level `(midiNoteOn/midiCC/midiSysex …)` escape hatch for live/generative | ✓ |
| High-level only | Just `(midiOut song "port")`; no per-event builtins this phase | |
| Low-level only | Just event builtins; compose MIDI by hand | |

**User's choice:** Both, high-level first.
**Notes:** Matches "easy cases fast, flexible cases flexible." High-level for the common case (composer thinks in Songs), raw events for live coding / `@improv` / generative.

## Mapping (sequence → MIDI channel/program)

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse Phase 28 GM + override | `writeMidi` GM prefix-match verbatim (port sounds identical to .mid) + explicit per-sequence override | ✓ |
| Reuse Phase 28 GM, no override | Mirror writeMidi exactly, no per-sequence control | |
| Explicit assignment required | Composer must assign channels; no GM auto-routing | |

**User's choice:** Reuse Phase 28 GM + override.
**Notes:** Override mechanism left to planner; prefer a named-arg (D-36-11 universal named args).

## Activation (clock master/slave, Link, JACK enable)

| Option | Description | Selected |
|--------|-------------|----------|
| Opt-in builtins / toggles | `(clockMaster device)`/`(clockSlave "port")`, `(linkEnable)`, `(jackSync)` returning handles (mirror OscHandle) | ✓ |
| Musical-context blocks | `clockMaster { … }` etc., consistent with tempo/key/swing | |
| Mixed (builtins + optional block) | Both forms | |

**User's choice:** Opt-in builtins / toggles.
**Notes:** Sync is a stateful session mode (slave drives tempo; master⊕slave switch only at bar boundary), not body-scoped. Consistent with OSC opt-in + roadmap's literal `(jackSync)`.

## Module gate (dependency gating + granularity)

| Option | Description | Selected |
|--------|-------------|----------|
| `@midi` + separate `@link`/`@jack` | `use "@midi"` (RtMidi.Core) + separate modules so license-gated/Linux-only deps never force-load | ✓ |
| Single `@studio` module | One module pulls MIDI + clock + Link + JACK together | |
| Always-on, no module | All builtins available on Desktop without `use` | |

**User's choice:** `@midi` + separate `@link`/`@jack`.
**Notes:** Mirrors `@osc`/`@sfz`; Web `use` → charitable advisory (Phase 47 D-47-09). Isolates risky native deps.

## Scope & priority (must-ship vs defer)

| Option | Description | Selected |
|--------|-------------|----------|
| MIDI+clock core; Link/JACK best-effort | RtMidi out + clock master/slave non-negotiable; Link/JACK ship only if clean, else defer | ✓ |
| Ship all four fully | MIDI + clock + Link + JACK all green this phase | |
| MIDI out only; defer all sync | Real-time note/CC out only; defer clock + Link + JACK | |

**User's choice:** MIDI+clock core; Link/JACK best-effort.

## Link license posture

| Option | Description | Selected |
|--------|-------------|----------|
| Conservative: defer unless trivially clean | Brief check; any MIT-contamination ambiguity → defer LINK-01/02 to community/v1.6 | ✓ |
| Pursue via runtime P/Invoke, never bundle | Ship Link against user-installed `libabl_link`, accept dynamic-link GPL gray area | |
| Defer Link to v1.6 outright now | Skip license review entirely, defer immediately | |

**User's choice:** Conservative — defer unless trivially clean. Per D-v1.5-04.

## Verification approach

| Option | Description | Selected |
|--------|-------------|----------|
| Both: loopback CI + hardware HUMAN-UAT | Virtual-MIDI automated CI (charitable-skip) + documented real-gear HUMAN-UAT checklist | ✓ |
| Automated loopback only | Virtual MIDI assertions in CI, no hardware UAT | |
| Hardware HUMAN-UAT only | Manual checklist, minimal automated tests | |

**User's choice:** Both — loopback CI + hardware HUMAN-UAT. Mirrors Phase 49 honest machine/human split + Phase 39 charitable-skip gate.

---

## Claude's Discretion
- Exact override syntax for per-sequence channel/program mapping (prefer named-arg).
- Handle type names + specificity values for MidiDevice / clock / Link / JACK handles.
- Internal scheduling mechanism for `PlaybackStartTime + bufferOffset` MIDI alignment.
- Whether clock master needs explicit `(clockStop)` vs handle-based stop.
- Virtual-MIDI test mechanism (`snd-virmidi` vs RtMidi virtual ports vs loopback).

## Deferred Ideas
- WebMIDI as `WebMidiBackend` (v1.6, per Phase 47).
- CoreMIDI/WinMM backends (Phase 41, MIDI-RT-03).
- General MIDI-input builtin surface beyond clock slave.
- Ableton Link if license review defers it (community PR).
- JACK on macOS/Windows.
- MIDI 2.0 / MPE.
