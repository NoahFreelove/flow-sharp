# SFZ Orchestral Sampler — Tutorial

A composer-facing tutorial for Phase 33's SFZ orchestral sampler.

Flow ships built-in synthesizers (piano, brass, sax, drums, etc.) and the
Phase 29 bundled-sample tonal instruments. Phase 33 layers an **opt-in**
SFZ-format sampler on top so composers can load richer external libraries —
multi-velocity, multi-articulation, full chromatic coverage — without
swapping the runtime. The blessed library for the v1.4 milestone is
[VSCO Community CE 1.1.0](https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0)
(CC-BY 4.0, ~400 MB).

## Setup

1. **Download VSCO Community CE 1.1.0:**
   <https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0>

2. **Extract** the release to a directory you'll keep — e.g.
   `~/.flow/samples/VSCO-CE/`. The directory should contain top-level
   `.sfz` files (`SViolinVib.sfz`, `FluteSusVib.sfz`, ...) alongside
   per-instrument sample directories.

3. **Configure `sfz_root`** in `~/.config/flow/config.toml`:

   ```toml
   sfz_root = "/home/<you>/.flow/samples/VSCO-CE"
   ```

   (Add the line if the file already exists; create the file otherwise.
   The other Phase 30 keys — `install_path`, `default_tempo`,
   `default_audio_device`, `stdlib_search_path` — are independent and
   continue to work unchanged.)

## Run

```bash
dotnet run --project flow-interpreter examples/symphony/sfz_smoke.flow
```

The script writes `sfz_smoke.wav` to the current working directory. Play
with any system audio player (e.g. `aplay sfz_smoke.wav` on Linux,
`afplay sfz_smoke.wav` on macOS).

## What the tutorial demonstrates

- The full `use "@sfz"; Sfz violin = (loadSfz #violin); renderSong song "sampler:violin"; writeWav` pipeline.
- Symbol-keyed lookup against the 19-entry GM orchestral dict shipped in `flow-lang/sfz.flow`.
- Phase 28 articulation envelope applied on top of the SFZ-rendered notes.
- Coexistence with the existing Phase 29 bundled-sample path — `renderSong song "piano"` still works in the same script without any conflict.

## Supported instruments (19-symbol GM dict)

The dict in `flow-lang/sfz.flow` ships 15 verified VSCO-CE 1.1.0 paths
plus 4 known-missing placeholders. The full list:

**Strings:** `#violin`, `#viola`, `#cello`, `#contrabass`
**Woodwinds:** `#flute`, `#oboe`, `#clarinet`, `#bassoon`
**Brass:** `#trumpet`, `#horn`, `#trombone`, `#tuba`
**Keys + plucked + percussion:** `#piano`, `#harp`, `#timpani`
**Not bundled with VSCO-CE 1.1.0 (use absolute path):** `#choir`, `#guitar`, `#harpsichord`, `#celeste`

See [`.planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md`](../../.planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md) for the per-symbol verified VSCO-CE path each `loadSfz` call resolves to.

## Loading non-GM patches

For instruments outside the 19-symbol GM set or for custom SFZ patches,
use the absolute-path `loadSfz(String)` overload — it bypasses the dict
entirely:

```flow
Sfz custom = (loadSfz "/abs/path/to/MyPatch.sfz")
Buffer mix = (renderSong song "sampler:custom")
```

## Reference

- The `(loadSfz Symbol)` and `(loadSfz String)` builtins, the `Sfz` value type, and the `sampler:NAME` instrument-string dispatch are documented in the project [`CLAUDE.md`](../../CLAUDE.md) under "Music Types Quick Reference" and "Music-Specific Language Features".
- Phase 33 specification: [`.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md`](../../.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md)
- This example is the foundation for the v1.4 symphony showcase shipping in Phase 34.
