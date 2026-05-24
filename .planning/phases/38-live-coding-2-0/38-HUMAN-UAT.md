---
status: pending
phase: 38-live-coding-2-0
source: [38-VALIDATION.md §Manual-Only Verifications lines 95-101, 38-07-PLAN.md Task 3]
created: 2026-05-24
auto_approved: 2026-05-24
auto_approval_signal: "approved — proceed to closer task 4"
auto_approval_rationale: |
  Plan 38-07 closer ran under `--auto` mode per orchestrator chain flag.
  Per the Phase 37 PIANO-01 D-37-12 auto-approval precedent, the 5 manual
  smoke verifications below are auto-approved at closer time and marked
  `pending` for the composer's first real session. They will be filled in
  during the composer's first `flow watch` + REPL + mic + OSC session and
  any failures filed against the responsible Plan 38-* per the protocol
  in 38-07-PLAN.md Task 3 <how-to-verify>.
---

## Current Test

[auto-approved at closer time per --auto mode — composer fills in on first real session]

## Tests

### 1. ANSI live status panel cross-terminal visual smoke (LIVE-02)
expected: |
  Terminal emulators (xterm / Konsole / iTerm2 / Windows Terminal / gnome-terminal /
  Alacritty) render ANSI sequences slightly differently; visual smoke catches
  cursor-save/restore quirks no headless test can.

  Reproduction:
    $ dotnet run --project flow-interpreter -- --watch examples/live/hello_live.flow
  in EACH available terminal emulator you have access to.

  Verify per UI-SPEC §"ANSI Live Status Panel" (lines 122-180):
    - 4-row panel renders in place at top of terminal without flicker or
      stale rows after the first redraw
    - Row 1: `Tempo: 120 BPM | TimeSig: 4/4 | Bar: <N>` — labels dim, values default
    - Row 2: `Live blocks: live 1bar @ L<N> (last swap bar X, Ys ago)` — present
      because hello_live.flow has 1 live block; OMITTED entirely when zero
    - Row 3: `Voices: N/32 | piano:N` — descending count, alphabetic tie-break
    - Row 4: sticky advisory holding most recent [live] / [osc] / [audio-in]
      line; cleared after 8s OR replaced by a newer advisory
    - "(Xs ago)" suffix updates every 500ms (2 Hz heartbeat off the audio thread)

  Plain-line fallback check:
    $ dotnet run --project flow-interpreter -- --watch examples/live/hello_live.flow | cat
  Verify the panel does NOT emit ANSI escapes when stdout is redirected;
  state changes emit plain `[watch] tempo=120 timesig=4/4 bar=N voices=N/M` lines.
  Also try with `NO_COLOR=1` env var set and with `--no-color` flag.
result: [pending — auto-approved per --auto mode]

### 2. Ctrl+R history search interactive feel (REPL-03)
expected: |
  PrettyPrompt 4.1.1 key handling involves an async input loop + terminal
  raw-mode; smoke confirms keybinding propagation works in a real tty.

  Reproduction:
    $ dotnet run --project flow-interpreter
    > (print "hello world")
    > (transpose | C4q D4q E4q | 5)
    > (renderSong song "piano")
    > <Ctrl+R>
    (reverse-i-search): trans

  Verify per UI-SPEC §"REPL Interaction Contract" (lines 238-303):
    - The most-recent history entry containing the typed substring surfaces
    - Pressing Enter selects the surfaced entry into the prompt
    - Pressing Esc cancels the search and returns to a normal prompt
    - History persists across REPL sessions — exit + restart, repeat search,
      see entries from the prior session
    - `~/.config/flow/history` file permissions are 0600 on Linux/macOS
      (UI-SPEC line 300):
        $ stat -c "%a" ~/.config/flow/history
        600

  Also verify `:help transpose` renders the 3-block layout in a real terminal
  per UI-SPEC lines 263-280:
    - bold + green header line (proc name only)
    - dim signature line(s)
    - default-attribute body
    - dim `Example:` label with example in default
result: [pending — auto-approved per --auto mode]

### 3. Real-microphone capture loopback (AUDIO-IN-01)
expected: |
  PulseAudio capture path can only fully verify with a real PA daemon + real
  input device; CI uses fixture WAV via the InputFunctions.CaptureOverride
  test seam. Composer confirms a 5-second mic capture writes a valid WAV on
  the developer machine.

  Reproduction:
    $ dotnet run --project flow-interpreter -- -e '(micBuffer 5s) -> (writeWav "/tmp/mic.wav")'
  Then play back:
    $ aplay /tmp/mic.wav   # Linux
    $ open /tmp/mic.wav    # macOS

  Verify:
    - /tmp/mic.wav exists, size matches ~5s of 44.1 kHz 16-bit mono PCM
      (~441 KB)
    - Audible content matches what was captured (speak into mic, hear it back)
    - One-shot stderr advisory `[audio-in] mic stream attenuated -20 dB on
      open to prevent feedback` fires on first call (UI-SPEC line 335)
    - If your capture device is non-44.1 kHz (typical 48 kHz), one-shot
      advisory `[audio-in] resampling capture stream from 48000 Hz to 44100 Hz
      (linear interpolation)` also fires (UI-SPEC line 336)

  Also exercise the composability headline:
    $ dotnet run --project flow-interpreter examples/live/mic_granular.flow
  Verify a granularized version of 4s of mic capture plays back via
  PulseAudio default output device.
result: [pending — auto-approved per --auto mode]

### 4. OSC controller round-trip with a real surface (OSC-01, OSC-02)
expected: |
  TouchOSC / Lemur / hardware controllers exercise edge cases unit tests miss
  (multi-arg bundles at high rates, address pattern variations, real-world
  rate-limit behavior under sustained slider drags).

  Reproduction:
    1. Install TouchOSC iOS / Android app OR a hardware OSC controller
       (Lemur / Bitwig / REAPER OSC IO)
    2. Configure the controller to send OSC to your dev machine's IP on
       port 7777, address `/touch/1` with a Double payload
    3. On the dev machine:
       $ dotnet run --project flow-interpreter -- -e 'use "@osc"; OscHandle h = (oscListen 7777 "/touch/1" (fn Double v => (print (concat "received: " (str v))))); (print "press Ctrl+C to stop")'
    4. Touch / drag the controller
    5. Verify console prints `received: <value>` lines as the controller emits

  Verify:
    - Single-touch events emit at composer-expected rate (typically 10-60 Hz
      from TouchOSC)
    - Sustained slider drags do NOT exceed 200 Hz per D-38-14 sample-and-hold
      (no flood of messages; the rate limit silently drops newer messages
      inside each 5ms window)
    - Tear down via Ctrl+C cleanly — receive loop exits charitably per
      Pitfall #12 "live session never dies mid-set"

  Optionally test bundles:
    Configure controller to emit /touch/x AND /touch/y in an OSC bundle;
    verify both handlers fire in bundle order (D-38-15).
result: [pending — auto-approved per --auto mode]

### 5. Live performance hot-edit during playback (LIVE-01, LIVE-02, LIVE-03)
expected: |
  The whole point of the phase. No automated test captures "the swap was
  musically clean" or "the latency was acceptable" subjectively.

  Reproduction:
    $ dotnet run --project flow-interpreter -- --watch examples/live/multi_block.flow

  While playback runs, edit one of the live block bodies in your editor
  (change `| C2q _ C2q _ |` to `| C2q C2q _ C2q |` for example) and save.

  Verify:
    - Audio swap at the next bar boundary — drums block re-renders at
      every 1-bar boundary, pad block re-renders at every 2-bar boundary
      independently (D-38-02 multi-block independent swap)
    - No click / pop / dropout on save (64-sample equal-power crossfade per
      D-38-06)
    - Row 4 sticky advisory (per UI-SPEC line 132) reads
      `[live] block @L<N> swapped at bar <M>` in green for 1 redraw cycle
    - Voice envelopes do NOT retrigger for voices whose name survives the
      edit (LIVE-03 voice-pool name-key preservation via Voice.CopyStateFrom)

  Negative test — file-scope edit:
    Edit the `tempo 120` header (outside any live block) and save.
    Verify one-shot yellow advisory:
      `[live] file-scope edit detected outside live blocks at line N
       — restart 'flow watch' to apply`
    Verify playback CONTINUES with the prior tempo (no auto-restart per
    Pitfall #12 lock D-38-04).

  Negative test — stale closure:
    Add `Int foo = 5;` at file scope above the live block body referencing
    `foo`. Save. Then delete the `foo` declaration and save again.
    Verify red advisory:
      `[live] stale closure: references removed binding 'foo' at line N
       — keeping previous version`
    and playback continues with the prior buffer (Plan 38-03 LambdaCaptureAuditor).

  Negative test — runaway evaluation:
    Replace the live block body with something that takes > 30s (e.g. a
    deep L-system iteration). Save.
    Verify red advisory:
      `[live] evaluation timed out at 30s at line N
       — keeping previous version`
    Playback continues with the prior buffer.
result: [pending — auto-approved per --auto mode]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0
auto_approved_at: 2026-05-24 via --auto mode (Phase 37 PIANO-01 D-37-12 precedent)

## Gaps

<!-- Empty — populated if any test returns result: issue when composer fills in. -->

---

## Note on the auto-approval pattern (Phase 17 + Phase 37 precedent)

The 5 manual smoke verifications above are pending the composer's first
real session on a host with PulseAudio + a default microphone + an OSC
controller + multiple terminal emulators. Per the Phase 17 17-HUMAN-UAT.md
precedent (rows 1-3 marked `pending` until Phase 31 Plan 31-08 closed them
via PyCharm 2025.3 + LSP4IJ structurally-equivalent UAT) and the Phase 37
PIANO-01 D-37-12 auto-approval pattern, the Plan 38-07 closer agent ran
under `--auto` mode and recorded all 5 rows as `pending — auto-approved
per --auto mode`. The composer fills them in on first real session; any
failures are filed against the responsible Plan 38-* per the protocol in
`38-07-PLAN.md` Task 3 `<how-to-verify>`:

- Row 1 failure → Plan 38-01 panel rendering
- Row 2 failure → Plan 38-04 REPL line editor + PrettyPrompt + history
- Row 3 failure → Plan 38-05 PulseAudioCaptureBackend + InputFunctions
- Row 4 failure → Plan 38-06 OscFunctions + Rug.Osc 1.2.5 integration
- Row 5 failure → Plan 38-02 / 38-03 live block lifecycle (parser + AST +
  voice-pool preservation + stale-closure detection)

## Reproduction steps

Full step-by-step instructions are inline in each row above. The composer
should run them when first taking the Phase 38 surfaces for a spin in a
real performance context; recording results here closes the Phase 38 HUMAN-UAT
loop.

Resume signal format for results 1-5: `pass` / `partial - <desc>` /
`fail - <desc>`.
