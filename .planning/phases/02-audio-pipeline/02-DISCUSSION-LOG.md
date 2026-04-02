# Phase 2: Audio Pipeline - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-01
**Phase:** 02-audio-pipeline
**Areas discussed:** WAV loading API, Panning model, Sidechain API, Voice allocation
**Mode:** auto (all recommended defaults selected)

---

## WAV Loading API

| Option | Description | Selected |
|--------|-------------|----------|
| loadWav(String) -> Buffer | Pure function, returns buffer, matches writeWav | ✓ |
| loadSample(String) -> Buffer | Different name | |
| load(String) -> Buffer | Generic name | |

**User's choice:** [auto] loadWav (recommended — matches writeWav naming)
**Notes:** Functional return value, composable with ->

---

## Panning Model

| Option | Description | Selected |
|--------|-------------|----------|
| Constant-power + function + context block | Industry standard, both pan() and pan {} | ✓ |
| Linear panning, function only | Simpler but less natural | |
| Pan as property only | Set on voice, not composable | |

**User's choice:** [auto] Constant-power + function + context (recommended)
**Notes:** Also fixes the existing bug where Voice.Pan is unused in mixer

---

## Sidechain API

| Option | Description | Selected |
|--------|-------------|----------|
| sidechain(trigger, source, threshold, ratio) -> Buffer | Pure function | ✓ |
| compress(source, trigger, threshold, ratio) | Extend existing compress | |

**User's choice:** [auto] Separate sidechain function (recommended — clearer semantics)

---

## Voice Allocation

| Option | Description | Selected |
|--------|-------------|----------|
| Drop-quietest, 32 max, configurable | Musical priority, crossfade on steal | ✓ |
| Drop-oldest, 16 max | Simpler | |
| No limit | Trust user | |

**User's choice:** [auto] Drop-quietest + configurable (recommended)

---

## Claude's Discretion

- WAV header parsing internals
- Voice allocator data structure
- Pan context block implementation approach

## Deferred Ideas

None
