# Phase 4 interim evidence — 2026-09-24

Implementation through `39eb9bc`; Phase 4 remains in progress.

- `python3 scripts/ci/verify.py --tier core --artifacts /tmp/flow-phase4-core`:
  Desktop solution build passes; 2,871 main tests and 21 MIDI tests pass, 14 main
  tests skipped, zero failures, zero tracked-content changes. See
  [core-verification.json](core-verification.json).
- `bash scripts/lsp-smoke.sh flow-lsp/bin/Debug/net10.0/flow-lsp`: boot, framed
  initialize response and clean exit 0; 349 stdout bytes.
- Dependency classification refresh includes `FlowLang.Analysis`; targeted ratchet
  test passes and forbidden dependency edges remain empty.

This is a working-checkout core gate, not a fresh-clone all-tier or browser gate.
The checkout still contains two ignored local Flow fixtures, as recorded in the
Phase 3 evidence. No runtime evaluation semantics were changed in these slices.
Static binding/type/overload analysis and browser integration remain pending.
