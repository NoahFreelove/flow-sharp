---
status: partial
phase: 33-sfz-orchestral-sampler
source: [33-VERIFICATION.md]
started: 2026-05-16T05:03:48Z
updated: 2026-05-16T05:03:48Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. VSCO-CE 1.1.0 end-to-end playback — confirms must-have #2 (real orchestral library loads + plays correctly)

expected: `(loadSfz #violin)` resolves the verified relative path SViolinVib.sfz against `sfz_root`, returns a non-null Sfz value, and `renderSong song "sampler:violin"` writes a non-empty WAV with audible violin timbre (NOT silence and NOT a fallback advisory).

setup:
1. Download VSCO-CE 1.1.0 from https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0
2. Extract to `~/.flow/samples/VSCO-CE/`
3. Add to `~/.config/flow/config.toml`: `sfz_root = "/home/<you>/.flow/samples/VSCO-CE"`
4. Run: `dotnet run --project flow-interpreter examples/symphony/sfz_smoke.flow`
5. Play `sfz_smoke.wav` with: `aplay sfz_smoke.wav`

expected_outcome: A 4-bar single-violin ascending melody (C4 D4 E4 F4 G4h G4h C5w) audible as a sustained-vibrato violin timbre. NOT silence, NOT clicks, NOT a fallback advisory on stderr. Phase 28 articulation envelope shapes attack and release naturally; loop crossfade keeps held G4 and C5 notes clean across boundaries.

why_human: Must-have #2 requires a real VSCO-CE install at sfz_root. The repo intentionally does not bundle the 400 MB library (SPEC § Constraints — composer-supplied). The 33-VSCO-PATH-AUDIT.md verifies 15/19 GM paths against GitHub raw probes; load-path resolution code joins those relative paths with `FlowConfig.Active.SfzRoot`, but only a human with VSCO-CE installed can confirm end-to-end audio production. Synthetic smoke fixture (Phase33SfzSmokeTests) proves the parser+renderer+envelope pipeline is correct on test data; this UAT confirms the chain works with the blessed external library.

result: [pending]

## Summary

total: 1
passed: 0
issues: 0
pending: 1
skipped: 0
blocked: 0

## Gaps
