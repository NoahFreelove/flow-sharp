# Phase 33: VSCO-CE 1.1.0 Path Audit

**Audited:** 2026-05-15
**Source:** `https://github.com/sgossner/VSCO-2-CE/tree/SFZ` (canonical 1.1.0 SFZ release branch)
**Method:** GitHub trees API recursive listing of the `SFZ` branch (`https://api.github.com/repos/sgossner/VSCO-2-CE/git/trees/SFZ?recursive=1`) plus `raw.githubusercontent.com` content probes for representative `.sfz` files.
**Audit purpose:** Resolves Assumption A1 from `33-RESEARCH.md` — the 19-symbol GM-orchestral dict feeds `flow-lang/sfz.flow` (Plan 33-05). Verified-vs-TBD column drives the Plan 33-05 dict-build + the user-facing error path when `(loadSfz #symbol)` cannot resolve.

## Findings Summary

- **All `.sfz` patches live at the REPOSITORY ROOT** of the `SFZ` branch — not under nested `Strings/Violin/` directories as Assumption A1 originally hypothesised. The `Strings/`, `Brass/`, `Woodwinds/`, `Keys/`, `Percussion/`, `VSCO 1 Percussion/`, and `Miscellania Raw/` directories hold the **`.wav` sample files**; each top-level `.sfz` file declares `<control> default_path=<dir>\` to point at its sample folder.
- **Path canonicalisation:** every `.sfz` file's `<control> default_path=` uses **Windows-style backslashes** (e.g. `Strings\Solo Violin\Arco Vib\`). Plan 33-04's parser MUST accept backslashes and normalise to OS path separators (Linux primary per CLAUDE.md). This is implementation guidance that pairs with the `<control>` decision in `33-VSCO-CONTROL-DECISION.md` — the dict in Plan 33-05 only stores the top-level `.sfz` filename; the parser handles the path-into-samples cascade.
- **Articulation choice — locked to Sustain ("Sus") variants per SPEC-2's `loadSfz #violin → "violin sustain" semantic.** Where multiple sustain variants exist (e.g. `SViolinVib.sfz` vs `SViolinVib-Quiet.sfz`), the louder/default variant is canonical. Where solo + ensemble both exist (Strings only), the **solo** variant is canonical for `#violin`/`#viola`/`#cello`/`#contrabass` per single-instrument GM semantics; the `-Ens` variants stay accessible via the absolute-path `loadSfz "..."` overload.
- **4 of 19 GM symbols have NO VSCO-CE patch** (`#choir`, `#guitar`, `#harpsichord`, `#celeste`) — VSCO Community Edition ships brass / strings / woodwinds / keys (organ + piano) / percussion only. Plan 33-05 ships these 4 entries as TBD with a `Note: not in VSCO-CE 1.1.0` inline comment; `(loadSfz #choir)` errors with a clear message pointing the composer at the absolute-path overload.

**Phase 37 update (Plan 37-06 — 2026-05-23):** dict grew to **20 entries** with the addition of `#drums → GM-StylePerc.sfz` (DRUM-01 per D-37-13). The new entry brings the verified count to **16 of 20** (4 TBD rows unchanged). `#drums` is the first dict-symbol that drives `SfzData.IsPercussion = true` at SfzBuiltins load time (Plan 37-06 W7 LOCK) — SfzRenderer's `#auto` pitch-shift route (Plan 37-02 PitchShiftEngine) gates on the flag per D-37-14.

## Audit Table

| Symbol         | VSCO-CE Relative Path                        | Confidence | Source                                                                                          |
|----------------|----------------------------------------------|------------|-------------------------------------------------------------------------------------------------|
| `#violin`      | `SViolinVib.sfz`                             | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/SViolinVib.sfz` (probed; `<control> default_path=Strings\Solo Violin\Arco Vib\`)             |
| `#viola`       | `ViolaEnsSusVib.sfz`                         | verified*  | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/ViolaEnsSusVib.sfz` (probed) — VSCO-CE has no solo viola; ensemble is canonical for `#viola` |
| `#cello`       | `CelloEnsSusVib.sfz`                         | verified*  | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/CelloEnsSusVib.sfz` (probed) — VSCO-CE has no solo cello; ensemble is canonical for `#cello` |
| `#contrabass`  | `ContrabassSusVB.sfz`                        | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/ContrabassSusVB.sfz` (probed; `<control> default_path=Strings\Solo Contrabass\SusVib\`)      |
| `#flute`       | `FluteSusVib.sfz`                            | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/FluteSusVib.sfz` (probed)                                                                    |
| `#oboe`        | `OboeSusVib.sfz`                             | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/OboeSusVib.sfz` (probed)                                                                     |
| `#clarinet`    | `ClarinetSus.sfz`                            | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/ClarinetSus.sfz` (probed)                                                                    |
| `#bassoon`     | `BassoonSus.sfz`                             | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/BassoonSus.sfz` (probed)                                                                     |
| `#trumpet`     | `TrumpetSus.sfz`                             | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/TrumpetSus.sfz` (probed)                                                                     |
| `#horn`        | `FHornSus.sfz`                               | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/FHornSus.sfz` (probed) — F Horn is the canonical orchestral horn                             |
| `#trombone`    | `TromboneSus.sfz`                            | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/TromboneSus.sfz` (probed)                                                                    |
| `#tuba`        | `TubaSus.sfz`                                | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/TubaSus.sfz` (probed)                                                                        |
| `#piano`       | `UprightPiano.sfz`                           | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/UprightPiano.sfz` (probed; `<control> default_path=Keys\Upright Piano\`)                     |
| `#harp`        | `Harp.sfz`                                   | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/Harp.sfz` (probed)                                                                           |
| `#timpani`     | `Timpani.sfz`                                | verified   | `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/Timpani.sfz` (probed)                                                                        |
| `#choir`       | TBD — not in VSCO-CE 1.1.0                   | TBD        | needs composer download (no choir SFZ ships in VSCO Community Edition)                                                                                 |
| `#guitar`      | TBD — not in VSCO-CE 1.1.0                   | TBD        | needs composer download (no guitar SFZ ships in VSCO Community Edition)                                                                                |
| `#harpsichord` | TBD — not in VSCO-CE 1.1.0                   | TBD        | needs composer download (no harpsichord SFZ ships in VSCO Community Edition)                                                                           |
| `#celeste`     | TBD — not in VSCO-CE 1.1.0                   | TBD        | needs composer download (no celeste SFZ ships in VSCO Community Edition)                                                                               |
| `#drums`       | `GM-StylePerc.sfz`                           | verified   | Plan 37-06 (2026-05-23) — DRUM-01 via Phase 33 SFZ surface; W7 LOCK — `SfzData.IsPercussion = true` set at SfzBuiltins load time, drives `#auto` pitch-shift route per D-37-14 |

`*` = ensemble-canonical because VSCO-CE has no solo patch for the instrument; semantics still match SPEC-2's "violin/viola/cello" symphony intent.

## Fallback Behaviour for Unverified Rows

For the 4 TBD rows (`#choir`, `#guitar`, `#harpsichord`, `#celeste`), Plan 33-05 should:

1. Ship the symbol with an empty-string or `null` relative-path entry in the dict.
2. Wire the `(loadSfz #symbol)` codepath so that an empty/null entry produces an `UnknownInstrumentSymbolError` variant whose message includes:
   - The symbol name
   - The text "not bundled with VSCO Community Edition"
   - A pointer to the absolute-path overload: `(loadSfz "/path/to/your/symbol.sfz")`
3. Register the test-harness advisory so unit tests catch any accidental typo'd path attempting to populate the TBD slot.

For the 15 verified rows, Plan 33-05's dict entries are the canonical truth — no further verification needed.

## Other Verified .sfz Patches in VSCO-CE 1.1.0

These are bundled in VSCO-CE but NOT in the locked 19-symbol GM dict. Composers can access them via the absolute-path `loadSfz` overload. Listed for awareness only — no Plan 33-05 entry needed.

- Articulation alternates: `*Stac.sfz`, `*Spic.sfz`, `*Pizz.sfz`, `*Trem.sfz`, `*-KS.sfz` (keyswitched) for most strings + woodwinds.
- Brass mutes: `TrumpetHarmonMuteSus.sfz`, `TrumpetStraightMuteSus.sfz`, `FHornMute.sfz`.
- Quiet variants: `*-Quiet.sfz` for ensemble strings + contrabass.
- Untuned percussion: `GM-StylePerc.sfz`, `Glockenspiel.sfz`, `Marimba.sfz`, `Xylophone.sfz`, `TubularBells.sfz`, `TimpaniRolls.sfz`.
- Alternate piano: `VSUpright1.sfz`.

## Notes for Plan 33-05 (Dict Builder)

- The dict in `flow-lang/sfz.flow` is keyed by `Symbol` and produces a relative path joined to `sfz_root` (Phase 30 config key). Dict entries are top-level filenames — the `<control> default_path=` cascade resolves the actual sample path inside the parser.
- 4 entries are TBD; document per the fallback behaviour section above.
- 1 entry has a Vib variant (`SViolinVib.sfz`) that pairs with the Phase 28 articulation envelope cleanly — vibrato + envelope shaping compose correctly per SPEC-8.
- Path normalisation: backslash → OS separator handled in Plan 33-04's parser, NOT in the Flow dict.
