# Symphony Showcase + SFZ Tutorial

This directory ships two end-to-end uses of the Phase 33 SFZ orchestral
sampler:

- **`symphony.flow`** — the v1.4 headline piece. A ~60s ABA single-movement
  symphony for 5 VSCO Community CE 1.1.0 instruments. Documented below
  under [§ The Symphony](#the-symphony).
- **`sfz_smoke.flow`** — the original Phase 33 tutorial chapter. A 4-bar
  single-violin smoke fixture that introduces the surface (`use "@sfz"`,
  `(loadSfz #symbol)`, `renderSong song "sampler:NAME"`). Documented
  below under [§ Tutorial Chapter: sfz_smoke.flow](#tutorial-chapter-sfz_smokeflow).

Both files share the same one-time VSCO-CE install — see
[§ Setup](#setup). Composers new to the SFZ sampler should walk the
tutorial chapter first (`sfz_smoke.flow` is < 50 lines), then read the
symphony source as a working example of the same pipeline at full scope.

The companion showcase piece — an upbeat solo-piano ragtime — lives at
[`../ragtime/ragtime.flow`](../ragtime/ragtime.flow) and reuses the
same `sfz_root` configuration. See [§ See also](#see-also).

## The Symphony

**Working title:** *In Five Voices* — a play on both the 5-instrument
orchestration and the underlying Phase 28 `voicePool` voice-allocation
model.

**Duration:** ~62 seconds rendered at tempo 100 BPM.
**Form:** ABA single-movement.
**Key:** D minor.
**Time signature:** 4/4.

A pensive minimalist-orchestral piece in the Yann Tiersen / Ólafur
Arnalds / Max Richter mood bracket. Flute carries the A-theme;
sustained cello holds the bass throughout (with `humanizeGaussian` at
seed 42 for organic micro-timing); horn enters as a tenuto pad. A short
timpani marcato hit marks the A→B and B→A' section boundaries. In the
B section, horn takes the lead and the flute answers with a single
`{3:2 ...}q` triplet flourish (Phase 19 tuplet). The A' return brings
the violin in an octave above the original flute theme (via the
`(transpose ... 12)` transform) over a `{voice ...}{voice ...}` cello
polyphony block (Phase 28).

### Instrumentation

All five instruments resolve through the 19-entry GM dict from the
Phase 33 SFZ surface (see [§ Supported instruments](#supported-instruments)
below for the full dict).

| Symbol      | VSCO-CE patch        | Role                                                            |
|-------------|----------------------|-----------------------------------------------------------------|
| `#violin`   | `SViolinVib.sfz`     | Solo lead in A' — carries the A-theme transposed +12 semitones. |
| `#cello`    | `CelloEnsSusVib.sfz` | Sustained bass throughout; humanized in A; legato in B and A'.  |
| `#flute`    | `FluteSusVib.sfz`    | A-theme melody; B-section triplet ornament; A' harmony hold.    |
| `#horn`     | `FHornSus.sfz`       | Tenuto pad in A; melodic lead in B; full tenuto pad in A'.      |
| `#timpani`  | `Timpani.sfz`        | Single marcato hit on the A→B and B→A' transitions.             |

### Phase 34 features audibly demonstrated

Each criterion-#3 feature maps to a specific audible moment:

- **Musical-context blocks (`voicePool` / `tempo` / `timesig` / `key`)** —
  surfaced as the file-scope nesting at `symphony.flow:32-35`. The
  `voicePool 32` block makes the Phase 28 SPEC-7 locked default visible
  in composer-facing source.
- **Note streams (`| ... |`)** — every sequence in every section.
- **`transpose` transform** — violin's A' entrance: `(transpose | ... | 12)`
  lifts the flute A-theme an octave up.
- **`sampler:NAME` dispatch** — 5 calls (one per instrument): `renderSong
  piece "sampler:violin"` and so on. Each routes through the Phase 33
  `SfzRenderer`.
- **Phase 28 articulations** — all 5 tokens fire:
  - `>` (accent) on the flute A-theme downbeats and the violin A' lead.
  - `stacc` (staccato) on the B-section flute triplet flourish.
  - `ten` (tenuto) on the horn pad notes (A and A').
  - `leg` (legato) on the cello sustained bass in A and B.
  - `marc` (marcato) on the two timpani transition hits.
- **Voice block (`{voice ...}{voice ...}`)** — A' cello polyphony at
  `symphony.flow:88`: a sustained whole-note bass paired with an inner
  harmony half-note pair on the same instrument, mixed additively per
  Phase 28's voice-block contract.
- **Tuplet bracket (`{3:2 ...}q`)** — single Phase 19 triplet in the
  B-section flute ornament: `{3:2 D5 E5 F5}q`.
- **`humanizeGaussian`** — Phase 25 micro-timing applied to the A-section
  cello bass with a fixed integer seed (42) for two-run determinism.

### Expected output

- `examples/output/symphony.wav` — 44.1 kHz 16-bit stereo, ~10.5 MB,
  ~62 seconds.
- `examples/output/symphony.mid` — multi-track MIDI per CLAUDE.md
  § "Multi-track MIDI export". 5 instrument tracks (violin / cello /
  flute / horn / timpani) plus the conductor track (tempo + timesig
  meta-events). Sequence names route to GM programs via the prefix-match
  table in CLAUDE.md.

Both files are gitignored — they're produced locally by the composer
(and re-produced by anyone who installs VSCO-CE and renders the source).
The canonical MP3 + WAV ship as v1.4.0 GitHub Release assets.

### Mix notes

The composer-final post-UAT mix (read directly from the committed
source at `symphony.flow:118-146`):

| Stage                  | Setting                                  | Why                                                                          |
|------------------------|------------------------------------------|------------------------------------------------------------------------------|
| `volume rawViolin`     | `0.85` (linear)                          | Solo lead in A'; sits cleanly under the flute in other sections.             |
| `volume rawCello`      | `0.45` (linear)                          | Sustained bass bed — UAT iteration #2 dropped from 0.75 so it stops masking. |
| `volume rawFlute`      | `1.0`  (linear)                          | Lead instrument; UAT iteration #2 boosted from 0.85 to read as the melody.   |
| `volume rawHorn`       | `0.40` (linear)                          | Pad layer; UAT iteration #2 dropped from 0.65 to keep the lead intelligible. |
| `volume rawTimpani`    | `0.35` (linear)                          | Two single hits — `0.35` makes them present without clobbering the texture.  |
| Master reverb          | `(reverb 0.2 2.5s)`                      | UAT iteration #2 dropped wet from 0.30 so the 2.5s tail stops smearing.      |
| Master compress        | `(compress -12dB 4 100ms 200ms)`         | Soft 4:1 above -12 dB — glue, not a brick-wall limiter.                      |

Per-section reverb already fires inside `renderSong` (Phase 28 default);
the master reverb above sits on top.

### Reproduce locally

1. **Setup.** See [§ Setup](#setup) below for the one-time VSCO-CE
   install + `sfz_root` config — same setup the tutorial chapter uses.

2. **Render.** From the repo root:

   ```bash
   dotnet run --project flow-cli -c Release -- render examples/symphony/symphony.flow -o ignored.wav
   ```

   Notes:
   - **Use `flow-cli`, not `flow-interpreter`.** Only `flow-cli`'s
     `Program.cs` calls `FlowConfigLoader.LoadFromXdg()`, which is what
     makes `sfz_root` visible to the SFZ surface. The legacy
     `flow-interpreter` console app does not read the XDG config and
     will fail to resolve `(loadSfz #symbol)` against your VSCO-CE
     install.
   - The `-o` flag is **ignored at Phase 30** — the `(writeWav ...)`
     call inside the source is the real output path. The renderer
     writes to `examples/output/symphony.wav` and
     `examples/output/symphony.mid` relative to your current working
     directory.

   Once the `flow` binary is installed (via Phase 30's
   `scripts/install.sh`) the equivalent one-liner is:

   ```bash
   flow render examples/symphony/symphony.flow -o ignored.wav
   ```

3. **Listen.**

   ```bash
   aplay examples/output/symphony.wav             # Linux ALSA
   afplay examples/output/symphony.wav            # macOS
   flow play examples/symphony/symphony.flow      # PulseAudio via Phase 30 CLI
   ```

4. **Two-run determinism check.** Same inputs → same bytes. Two runs
   back-to-back must produce identical WAVs (Phase 28's two-run
   cmp-clean contract, preserved end-to-end through the real VSCO-CE
   library via the Phase 33 `SfzSampleCache`):

   ```bash
   cd /tmp && rm -f symphony.wav && \
     dotnet run --project ~/Desktop/projects/flow-sharp/flow-cli -c Release -- render \
     ~/Desktop/projects/flow-sharp/examples/symphony/symphony.flow -o ignored.wav && \
     cp symphony.wav /tmp/symphony_a.wav && rm -f symphony.wav && \
     dotnet run --project ~/Desktop/projects/flow-sharp/flow-cli -c Release -- render \
     ~/Desktop/projects/flow-sharp/examples/symphony/symphony.flow -o ignored.wav && \
     cp symphony.wav /tmp/symphony_b.wav && \
     cmp /tmp/symphony_a.wav /tmp/symphony_b.wav && echo "byte-identical"
   ```

5. **(Optional, composer-only) Encode to MP3 for sharing.** ffmpeg is a
   standard system tool; Flow has no MP3 dependency:

   ```bash
   ffmpeg -i examples/output/symphony.wav -c:a libmp3lame -b:a 192k -y flow-symphony-v1.4.0.mp3
   ```

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

## Tutorial Chapter: `sfz_smoke.flow`

`sfz_smoke.flow` is the Phase 33 tutorial chapter. It is the smallest
possible end-to-end use of the SFZ surface: one instrument
(`#violin`), 4 bars, one `renderSong "sampler:violin"` call, one
`writeWav`. Composers new to the SFZ surface should walk this file
before tackling the symphony source.

### Run

```bash
dotnet run --project flow-cli -c Release -- render examples/symphony/sfz_smoke.flow -o ignored.wav
```

The script writes `sfz_smoke.wav` to the current working directory. Play
with any system audio player (e.g. `aplay sfz_smoke.wav` on Linux,
`afplay sfz_smoke.wav` on macOS).

### What the tutorial demonstrates

- The full `use "@sfz"; Sfz violin = (loadSfz #violin); renderSong song "sampler:violin"; writeWav` pipeline.
- Symbol-keyed lookup against the 19-entry GM orchestral dict shipped in `flow-lang/sfz.flow`.
- Phase 28 articulation envelope applied on top of the SFZ-rendered notes.
- Coexistence with the existing Phase 29 bundled-sample path — `renderSong song "piano"` still works in the same script without any conflict.

### Supported instruments (19-symbol GM dict)

The dict in `flow-lang/sfz.flow` ships 15 verified VSCO-CE 1.1.0 paths
plus 4 known-missing placeholders. The full list:

**Strings:** `#violin`, `#viola`, `#cello`, `#contrabass`
**Woodwinds:** `#flute`, `#oboe`, `#clarinet`, `#bassoon`
**Brass:** `#trumpet`, `#horn`, `#trombone`, `#tuba`
**Keys + plucked + percussion:** `#piano`, `#harp`, `#timpani`
**Not bundled with VSCO-CE 1.1.0 (use absolute path):** `#choir`, `#guitar`, `#harpsichord`, `#celeste`

See [`.planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md`](../../.planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md) for the per-symbol verified VSCO-CE path each `loadSfz` call resolves to.

### Loading non-GM patches

For instruments outside the 19-symbol GM set or for custom SFZ patches,
use the absolute-path `loadSfz(String)` overload — it bypasses the dict
entirely:

```flow
Sfz custom = (loadSfz "/abs/path/to/MyPatch.sfz")
Buffer mix = (renderSong song "sampler:custom")
```

### Reference

- The `(loadSfz Symbol)` and `(loadSfz String)` builtins, the `Sfz` value type, and the `sampler:NAME` instrument-string dispatch are documented in the project [`CLAUDE.md`](../../CLAUDE.md) under "Music Types Quick Reference" and "Music-Specific Language Features".
- Phase 33 specification: [`.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md`](../../.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md)
- This example is the foundation for the v1.4 symphony showcase shipping in Phase 34.

## See also

- **[`../ragtime/README.md`](../ragtime/README.md)** — the v1.4 companion
  showcase: an upbeat F-major solo-piano ragtime piece via the same
  SFZ surface against VSCO-CE's `UprightPiano.sfz`. Together the
  symphony and the ragtime demonstrate Flow's genre-agnostic claim
  inside the v1.4 release (pensive D-minor orchestral vs. upbeat
  F-major solo piano).
