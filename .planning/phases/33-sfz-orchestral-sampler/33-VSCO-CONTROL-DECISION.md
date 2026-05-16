# Phase 33: VSCO-CE 1.1.0 `<control>` / `default_path=` Decision

**Decided:** 2026-05-15
**Resolves:** RESEARCH Open Question Q3 + Assumption A7
**Source:** `https://github.com/sgossner/VSCO-2-CE/tree/SFZ` (canonical 1.1.0 SFZ release branch)

## Methodology

Probed 15 representative `.sfz` files spanning **all six** VSCO-CE instrument categories (Brass, Strings, Woodwinds, Keys, Percussion, plus solo + ensemble variants) by fetching the raw GitHub content via `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/<filename>`. Probe set:

1. **Strings** — `SViolinVib.sfz`, `ViolaEnsSusVib.sfz`, `CelloEnsSusVib.sfz`, `ContrabassSusVB.sfz`, `Harp.sfz`
2. **Brass** — `TrumpetSus.sfz`, `FHornSus.sfz`, `TromboneSus.sfz`, `TubaSus.sfz`
3. **Woodwinds** — `FluteSusVib.sfz`, `OboeSusVib.sfz`, `ClarinetSus.sfz`, `BassoonSus.sfz`
4. **Keys** — `UprightPiano.sfz`
5. **Percussion** — `Timpani.sfz`

Each probe inspected the first ~40 lines of the file looking for `<control>` headers and `default_path=` opcodes.

## Findings

**100% of probed files (15/15) declare `<control>` + `default_path=` as their first non-comment header.**

Representative excerpts (verbatim, headers-only):

```sfz
// SViolinVib.sfz
<control>
default_path=Strings\Solo Violin\Arco Vib\

<global>
ampeg_attack=0.001
ampeg_release=0.8
...
```

```sfz
// UprightPiano.sfz
<control>
default_path=Keys\Upright Piano\

<global>
...
```

```sfz
// Timpani.sfz
<control>
default_path=Percussion\Timpani\

<global>
...
```

```sfz
// FluteSusVib.sfz
<control>
default_path=Woodwinds\Flute\susvib\

<global>
...
```

```sfz
// TromboneSus.sfz
<control>
default_path=Brass\Tenor Trombone\sus\

<global>
...
```

Pattern observations:

1. **`default_path=` always uses Windows-style backslashes** (e.g. `Strings\Solo Violin\Arco Vib\`). The parser MUST normalise to OS path separators (Linux primary per CLAUDE.md). Trailing backslash is consistent — every observed entry ends with `\`.
2. **`<control>` is always FIRST** before `<global>` / `<group>` / `<region>`. No probed file omits it.
3. **`<control>` blocks appear to declare ONLY `default_path=`** in VSCO-CE 1.1.0 (no other control opcodes observed in the probe set), but Plan 33-04 should still treat `<control>` as a fourth header type capable of holding additional opcodes for forward-compat with other SFZ libraries the composer may load via the absolute-path overload.
4. **Without `<control>` parsing, EVERY VSCO-CE `sample=` resolves to a non-existent file** because every region writes `sample=LLVln_ArcoVib_A3_p.wav` (filename only) expecting the `default_path=` cascade to provide the directory.

## Decision

### **FOUND**

Plan 33-04 MUST extend its opcode whitelist to **14 entries** and parse `<control>` as a fourth header type. Specifically:

1. **Whitelist becomes 14 opcodes** — add `default_path` alongside the existing 13 (`sample`, `lokey`, `hikey`, `pitch_keycenter`, `lovel`, `hivel`, `loop_mode`, `loop_start`, `loop_end`, `ampeg_attack`, `ampeg_release`, `volume`, `pan`).
2. **`<control>` parses as a fourth header type** (alongside `<global>`, `<group>`, `<region>`).
3. **`default_path=` cascades into every region's `sample=` path resolution at parse time** — when a `<region>` declares `sample=foo.wav`, the parser computes the absolute sample path as `Path.Combine(sfz_file_dir, default_path_normalised, "foo.wav")`. Backslash-to-OS-separator normalisation happens before the join.
4. **Path normalisation rule** — replace `\` with `Path.DirectorySeparatorChar` on the value of `default_path=` before joining. Trailing separator is harmless (`Path.Combine` handles it); preserve as-is.
5. **Cascade scope** — `<control>` is file-scope (one per file at the top); subsequent `<global>` / `<group>` / `<region>` blocks all inherit. If a future SFZ library declares multiple `<control>` blocks (uncommon but spec-permissive), Plan 33-04 may take the first or last; Phase 33 leaves this unspecified beyond "VSCO-CE never does it".
6. **SPEC-3's "13 listed opcodes" wording is conditionally relaxed** to 14 to include `default_path`. Update Plan 33-04's spec acceptance accordingly: "all 14 known opcodes appear in the parsed structure" replaces "all 13".
7. **Region-level `sample=` resolution now has TWO absolute-path roots:**
   - If `default_path` is set (typical for VSCO-CE), `Path.Combine(sfz_file_dir, default_path_normalised, sample_value)`.
   - If `default_path` is unset, `Path.Combine(sfz_file_dir, sample_value)` — the backwards-compatible plain-relative resolution per SFZ spec.
8. **Smoke fixture (Plan 33-01 Task 2) does NOT declare `<control>`** — the synthetic `smoke.sfz` exercises the no-`<control>` codepath, and verified VSCO patches exercise the `<control>` codepath in user setups. Plan 33-04 must include unit tests for both.

### Why FOUND, not NOT FOUND

A 15/15 probe rate across all six VSCO-CE instrument categories is conclusive evidence that `<control> default_path=` is the dominant pattern in VSCO-CE 1.1.0. Skipping `<control>` would cause **every** `(loadSfz #symbol)` call against a real VSCO install to hit `FileNotFoundError` on the first region's `sample=` resolution — invalidating SPEC-2's "with sfz_root configured, `(loadSfz #violin)` returns a non-null Sfz value" acceptance criterion and pre-emptively breaking Phase 34's symphony showcase. The 1-opcode + 1-header parser extension is small (~20 LOC est.) and entirely additive.

## Citations

- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/SViolinVib.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/ViolaEnsSusVib.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/CelloEnsSusVib.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/ContrabassSusVB.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/Harp.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/TrumpetSus.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/FHornSus.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/TromboneSus.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/TubaSus.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/FluteSusVib.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/OboeSusVib.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/ClarinetSus.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/BassoonSus.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/UprightPiano.sfz`
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/Timpani.sfz`
- Tree listing: `https://api.github.com/repos/sgossner/VSCO-2-CE/git/trees/SFZ?recursive=1` (commit-pinned SHA `6dd651d55dde97fd4028699be9d4481f26917891` at audit time)
