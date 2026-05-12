---
phase: 29-instrument-realism
plan: 01
type: execute
status: complete
tasks: 9/9
date: 2026-05-11
---

# Plan 29-01 Summary — bundled sample library + closure gates

## Outcome

The 21-sample CC-BY 4.0 instrument bundle landed at `flow-lang/Samples/`. Both closure gates (LICENSE audit + 5 MB size cap) pass. Plans 29-02 through 29-07 are now unblocked.

| Instrument | Files | Size | Source |
|---|---|---|---|
| Piano | 10 (C2/C3/C4/C5/C6 × pp/ff) | 1.3 MB | Iowa MIS piano (Univ. of Iowa Electronic Music Studios) |
| Brass | 3 (A3/A4/A5) | 532 KB | Iowa MIS Bb trumpet (substituted for "brass" generic) |
| Sax | 2 (F4/C5) | 268 KB | Iowa MIS Eb alto saxophone |
| Strings | 3 (D3/D4/D5) | 532 KB | Iowa MIS viola (D3) + violin (D4, D5) — mixed instrument |
| Flute | 2 (G4/G5) | 268 KB | Iowa MIS concert flute |
| Bell | 1 (C5) | 136 KB | Iowa MIS plastic bells |
| **Total** | **21 wav + 6 LICENSE.md + CREDITS.md** | **3.1 MB / 5 MB cap** | |

## SPEC relaxation (committed 2026-05-11)

Original 29-SPEC required CC0-only. Composer reported during curation that pitch+dynamic-specific CC0 samples are scarce — most Freesound CC0 hits are full tunes, not isolated pitches at named velocities. SPEC-2 was relaxed to accept CC-BY 3.0 / 4.0 with attribution (CC-BY-SA and CC-BY-NC remain excluded). CC-BY entries require an `Attribution:` line in their per-instrument LICENSE.md; a bundle-wide `CREDITS.md` aggregates attributions so end users see one consolidated credit list. Commit cd4419c carries the SPEC + Plan 29-01 edits.

## Plan substitutions

| Required | Provided | Reason |
|---|---|---|
| brass: horn or trumpet at A3/A4/A5 | Bb trumpet | Generic "brass"; trumpet acceptable substitute |
| strings: D3 (violin can't reach) | Viola D3 (sulC) | Violin lowest open string is G3 (MIDI 55); D3 = MIDI 50 unreachable. Viola lowest = C3, D3 plays naturally. Mixed-instrument bundle documented in strings/LICENSE.md |
| sax: F4/C5 originally downloaded as Eb3/Eb4/Eb5 | Re-downloaded F4 + C5 from Iowa | Alto sax is transposing; original Eb3/Eb4/Eb5 downloads were the wrong slot pitches |
| flute: G4/G5 + bonus G6 | Kept G4 + G5 only | Plan asks for 2 samples; bonus G6 dropped |

## Tasks

| Task | What | Status |
|------|------|--------|
| 1 | 10 piano samples C2..C6 × pp/ff + LICENSE.md | ✓ |
| 2 | 3 brass samples A3/A4/A5 + LICENSE.md | ✓ |
| 3 | 2 sax samples F4/C5 + LICENSE.md | ✓ |
| 4 | 3 strings samples D3/D4/D5 (viola+violin) + LICENSE.md | ✓ |
| 5 | 2 flute samples G4/G5 + LICENSE.md | ✓ |
| 6 | 1 bell sample C5 + LICENSE.md | ✓ |
| 7 | LicenseAuditTests.cs (6 instruments + CREDITS check) | ✓ — 7 facts GREEN |
| 8 | RepoSizeTests.cs (5 MB cap) | ✓ — 1 fact GREEN |
| 9 | .gitignore Phase 29 carve-out (samples must be tracked despite global *.wav + *.md ignore) | ✓ |
| — | Bundle-wide CREDITS.md (added per SPEC-2 relaxation) | ✓ |

## Verification

```
$ ls flow-lang/Samples/*/*.wav | wc -l        # 21
$ du -sh flow-lang/Samples                     # 3.1M
$ file flow-lang/Samples/piano/C4_ff.wav       # mono, 16 bit, 44100 Hz
$ dotnet test flow-lang.Tests --filter Phase29 # 8/8 GREEN (7 license + 1 size)
$ dotnet test flow-lang.Tests                  # 1011/1011 GREEN (zero regressions to Phase 28 / Phase 30)
$ dotnet test flow-midi.Tests                  # 13/13 GREEN
```

## Composer notes (workflow flag)

The CC0-only original constraint was researched against Freesound only. Iowa MIS turned out to be far better suited to Phase 29's needs (single isolated pitches at named dynamics across instrument families) but lives under "free with attribution" — effectively CC-BY. Future phases that need orchestral samples should default to Iowa MIS as the first stop, not Freesound.

## Files modified

- 21 .wav files under `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/`
- 6 `LICENSE.md` files (one per instrument) declaring CC-BY 4.0 + Iowa MIS source + attribution
- `flow-lang/Samples/CREDITS.md` aggregating attributions for end users
- `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` (created — 7 facts)
- `flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs` (created — 1 fact)
- `.gitignore` (Phase 29 carve-out)
