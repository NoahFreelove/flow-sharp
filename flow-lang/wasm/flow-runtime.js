// Phase 48 Plan 48-04 — Flow runtime ES module (D-48-12).
//
// This file is the JS-side companion of:
//   - flow-lang/Audio/FlowRuntimeInterop.cs (Plan 48-03 — 5 [JSImport] declarations
//     bound here via setModuleImports('flow-runtime', ...))
//   - flow-lang/Runtime/WasmEntry.cs       (Plan 48-04 — 4 [JSExport] static methods
//     reached here via getAssemblyExports)
//
// API surface frozen per D-48-13 (5 core exports + 1 convenience):
//   loadFlowRuntime() → Promise<{ run, play, stop, dispose, resumeAudio }>
//   runtime.run(source) → Promise<RunResult>      (D-48-14 shape)
//   runtime.play(wav, sampleRate?, channels?)     (Float32Array or float[])
//   runtime.stop()                                (revoke any active source)
//   runtime.dispose()                             (full teardown; idempotent)
//   runtime.resumeAudio()                         (D-48-09 user-gesture chain helper)
//
// RunResult shape (D-48-14):
//   { wav?: Float32Array, midi?: Uint8Array, stdout: string, stderr: string,
//     errors: { kind, message, line?, column?, sourceSnippet? }[], durationMs: number }
//   - stdout/stderr split per D-48-15.
//   - wav/midi are omitted from the JSON when absent (DefaultIgnoreCondition.WhenWritingNull).
//
// D-48-09 contract: this module does NOT call audioContext.resume() automatically.
// The playground (or the dev-smoke index.html) calls runtime.resumeAudio() from
// inside a user-gesture handler (the Run button's onclick) BEFORE runtime.run(...)
// so the autoplay policy is satisfied in the same call frame.
//
// D-48-10 cap is best-effort in-browser (AMENDED, debug session
// wasm-boot-no-app-bundle cycle 3): RunFromJs runs the Flow script SYNCHRONOUSLY
// on the single Mono-WASM main thread (the prior Task.Run+Wait(30s) shape
// deadlocked because the runtime has no worker thread to offload to). We
// deliberately add NO JS-side setTimeout "cap" here: a setTimeout callback cannot
// preempt a synchronous dotnet call (the JS event loop is blocked for the whole
// duration of RunFromJs), so a fake JS timeout would be non-functional. A runaway
// Flow script hangs its own tab exactly like any synchronous single-threaded JS —
// that is the accepted, honest tradeoff (the composer controls their own script).

// Publish-layout note (verified empirically against the generated AppBundle —
// see .planning/debug/wasm-boot-no-app-bundle.md):
// fix(48-06) made FlowTarget=Web emit a real bootable AppBundle. The layout is:
//   AppBundle/
//     flow-runtime.js        ← THIS file (copied here by WasmMainJSPath)
//     index.html             ← dev-smoke harness (copied by WasmMainHTMLPath)
//     package.json           ← { "type":"module" }
//     _framework/
//       dotnet.js            ← the loader entry point
//       dotnet.boot.js       ← boot manifest (mainAssemblyName: flow-lang.dll)
//       dotnet.native.wasm   ← Mono runtime
//       flow-lang.wasm       ← Webcil-encoded main assembly (NOT .dll in this layout)
//       System.*.wasm        ← Webcil-encoded framework assemblies
// flow-runtime.js sits at the AppBundle ROOT and dotnet.js sits under _framework/,
// so the relative import descends into ./_framework/dotnet.js. dotnet.create()
// then fetches dotnet.boot.js from that same _framework/ dir — which now exists,
// so the original "Failed to load config file dotnet.boot.js" 404 is resolved.
import { dotnet } from './_framework/dotnet.js';

let _runtime = null;
let _audioContext = null;
let _activeSources = new Set();

/**
 * Idempotent boot. Returns a cached runtime if already loaded.
 *
 * @returns {Promise<{
 *   run: (source: string) => Promise<RunResult>,
 *   play: (wav: Float32Array | number[], sampleRate?: number, channels?: number) => void,
 *   stop: () => void,
 *   dispose: () => void,
 *   resumeAudio: () => Promise<void>,
 * }>}
 */
export async function loadFlowRuntime() {
    if (_runtime) return _runtime;

    let setModuleImports;
    let getAssemblyExports;
    let getConfig;
    try {
        // dotnet.create() loads the Mono-WASM runtime + main assembly.
        // The relative './_framework/dotnet.js' import at the top of this file
        // is resolved by the browser against the AppBundle root that loaded the
        // module; dotnet.create() in turn fetches dotnet.boot.js from the same
        // _framework/ directory. See header comment for the verified layout.
        ({ setModuleImports, getAssemblyExports, getConfig } = await dotnet.create());
    } catch (err) {
        // Bubble a clear, actionable error so the playground sees boot failures
        // distinct from per-run script errors. Phase 49's UI surfaces this in
        // a top-level error pane (vs. the per-run diagnostic stream).
        throw new Error('Flow runtime boot failed: ' + (err && err.message ? err.message : String(err)));
    }

    // ------------------------------------------------------------------
    // setModuleImports — wire the JS side of FlowRuntimeInterop's 5
    // [JSImport(..., "flow-runtime")] declarations. The module-name
    // string "flow-runtime" MUST match the second [JSImport] arg in
    // flow-lang/Audio/FlowRuntimeInterop.cs.
    // ------------------------------------------------------------------
    setModuleImports('flow-runtime', {

        // [JSImport("createAudioContext", "flow-runtime")]
        // D-48-08: one AudioContext per browser tab. Cached after first call.
        createAudioContext: (sampleRate) => {
            if (!_audioContext) {
                _audioContext = new AudioContext({ sampleRate });
            }
            return _audioContext;
        },

        // [JSImport("playStereoFloat32", "flow-runtime")]
        // SYSLIB1072 contract from Plan 48-03: the C# side marshals the
        // Float32 PCM samples as raw bytes (Span<byte>). We reinterpret
        // the same ArrayBuffer here as Float32Array — zero-copy across
        // the boundary per RESEARCH §5.
        //
        // D-48-07: samples is ALWAYS interleaved stereo (channels=2)
        // because WebAudioBackend.PromoteToStereo runs before marshal.
        // We de-interleave into the AudioBuffer's per-channel layout.
        playStereoFloat32: (ctx, samplesAsBytes, channels, sampleRate) => {
            // The [JSMarshalAs<JSType.MemoryView>] Span<byte> arrives as
            // a typed Uint8Array view backed by the same underlying memory
            // as the C# Span. Reinterpret as Float32 (4 bytes per sample).
            const samples = new Float32Array(
                samplesAsBytes.buffer,
                samplesAsBytes.byteOffset,
                samplesAsBytes.byteLength / 4,
            );

            const frames = (samples.length / channels) | 0;
            if (frames <= 0) return null;

            const buffer = ctx.createBuffer(channels, frames, sampleRate);
            for (let ch = 0; ch < channels; ch++) {
                const channelData = buffer.getChannelData(ch);
                // de-interleave: input is L/R/L/R...; AudioBuffer wants
                // per-channel arrays (L-only / R-only).
                for (let i = 0; i < frames; i++) {
                    channelData[i] = samples[i * channels + ch];
                }
            }

            const source = ctx.createBufferSource();
            source.buffer = buffer;
            source.connect(ctx.destination);
            // Defensive (cycle 8): if the context is still suspended — e.g. a caller
            // that did not run the resumeAudio() gesture chain — kick it. A source
            // started on a suspended context plays once the context resumes, so this
            // recovers audio instead of silently dropping it (charitable-by-default).
            if (ctx.state === 'suspended') { ctx.resume().catch(() => { /* ignore */ }); }
            source.start();

            _activeSources.add(source);
            source.onended = () => _activeSources.delete(source);

            return source;
        },

        // [JSImport("stopSource", "flow-runtime")]
        // Idempotent — already-stopped exception is swallowed charitably.
        stopSource: (source) => {
            if (!source) return;
            try { source.stop(); } catch (e) { /* already stopped — fine */ }
            _activeSources.delete(source);
        },

        // [JSImport("closeContext", "flow-runtime")]
        // Stops every tracked source then closes the AudioContext.
        // The await on ctx.close() is honored by the JS engine; the C#
        // side fires-and-forgets (return type is void).
        closeContext: async (ctx) => {
            for (const src of _activeSources) {
                try { src.stop(); } catch (e) { /* ignore */ }
            }
            _activeSources.clear();
            if (ctx) {
                try { await ctx.close(); } catch (e) { /* ignore */ }
            }
            _audioContext = null;
        },

        // [JSImport("resumeContext", "flow-runtime")]
        // D-48-09 escape hatch — the C# side NEVER calls this from
        // WebAudioBackend.Play. Wired here so the runtime API can
        // expose it via the resumeAudio() convenience method.
        resumeContext: async (ctx) => {
            if (ctx) {
                try { await ctx.resume(); } catch (e) { /* ignore */ }
            }
        },
    });

    // ------------------------------------------------------------------
    // getAssemblyExports — reach the [JSExport]-decorated WasmEntry
    // static methods on the C# side.
    // ------------------------------------------------------------------
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);

    // ------------------------------------------------------------------
    // The 5-core + 1-convenience runtime surface (D-48-13). Each runtime
    // method dispatches directly via the full `exports.FlowLang.Runtime.WasmEntry.*`
    // path — the verbose form is intentional: it documents the boundary
    // each call crosses and surfaces the WasmEntry static class as the
    // single source of truth for the JS-callable surface.
    // ------------------------------------------------------------------
    _runtime = {

        /**
         * Execute a Flow source string. Returns the structured RunResult.
         *
         * exports.FlowLang.Runtime.WasmEntry.RunFromJs returns a JSON STRING
         * (not an object) per D-48-14 — we parse it here so the caller sees
         * a plain JS object.
         *
         * NOTE (D-48-10 amendment, debug session wasm-boot-no-app-bundle cycle 3):
         * RunFromJs executes the Flow script SYNCHRONOUSLY on the single Mono-WASM
         * main thread and returns the JSON string directly. This `run` is kept
         * `async` only to preserve the D-48-13 Promise-returning API surface
         * (Phase 49 may make execution genuinely async via a worker). The 30s cap
         * is best-effort in-browser — there is NO preemptive timeout, because no JS
         * setTimeout can interrupt a synchronous dotnet call (the event loop is
         * blocked for the whole call). A runaway script hangs its own tab.
         *
         * @param {string} source - Flow source code.
         * @returns {Promise<RunResult>}
         */
        run: async (source) => {
            const json = exports.FlowLang.Runtime.WasmEntry.RunFromJs(String(source ?? ''));
            try {
                return JSON.parse(json);
            } catch (err) {
                return {
                    stdout: '',
                    stderr: '',
                    errors: [{
                        kind: 'runtime',
                        message: 'flow-runtime.js: RunResult JSON parse failed — ' + err.message,
                        line: null,
                        column: null,
                        sourceSnippet: null,
                    }],
                    durationMs: 0,
                };
            }
        },

        /**
         * Push Float32 PCM into the shared backend.
         *
         * SYSLIB1072 boundary contract: C# side accepts byte[]. We
         * reinterpret the Float32Array's underlying buffer as Uint8Array
         * — same memory, zero-copy.
         *
         * @param {Float32Array | number[]} wav - Interleaved Float32 PCM.
         * @param {number} [sampleRate=44100]
         * @param {number} [channels=2]
         */
        play: (wav, sampleRate = 44100, channels = 2) => {
            if (!wav || wav.length === 0) return;
            let bytes;
            if (wav instanceof Float32Array) {
                bytes = new Uint8Array(wav.buffer, wav.byteOffset, wav.byteLength);
            } else {
                // Plain number[] — copy into a fresh Float32Array then view as bytes.
                const f32 = new Float32Array(wav);
                bytes = new Uint8Array(f32.buffer, f32.byteOffset, f32.byteLength);
            }
            exports.FlowLang.Runtime.WasmEntry.PlayFromJs(bytes, sampleRate, channels);
        },

        /** Revoke any active source node. Idempotent. */
        stop: () => {
            exports.FlowLang.Runtime.WasmEntry.StopFromJs();
        },

        /** Tear down the backend + engine; clear all state. Idempotent. */
        dispose: () => {
            exports.FlowLang.Runtime.WasmEntry.DisposeFromJs();
        },

        /**
         * D-48-09 convenience: call from inside a user-gesture handler
         * (e.g. a button's onclick) BEFORE runtime.run(...) to satisfy
         * the browser autoplay policy.
         *
         * @returns {Promise<void>}
         */
        resumeAudio: async () => {
            // Create the shared AudioContext NOW — inside the user-gesture frame —
            // if it does not exist yet, THEN resume it. WebAudioBackend.Play creates
            // the context lazily DURING run(), which is too late: resuming a
            // not-yet-created context was a silent no-op, so the context that Play
            // later created started suspended and source.start() produced no sound
            // (debug session wasm-boot-no-app-bundle cycle 8). Creating + resuming
            // here means the C# createAudioContext import returns THIS already-running
            // context (it caches on `if (!_audioContext)`), so playback is audible.
            if (!_audioContext) {
                _audioContext = new AudioContext();
            }
            try { await _audioContext.resume(); } catch (e) { /* ignore */ }
        },
    };

    return _runtime;
}

/**
 * @typedef {Object} RunError
 * @property {('parse'|'eval'|'runtime'|'cancel'|'platform-not-supported')} kind
 * @property {string} message
 * @property {number|null} [line]
 * @property {number|null} [column]
 * @property {string|null} [sourceSnippet]
 */

/**
 * @typedef {Object} RunResult
 * @property {Float32Array} [wav]    - Rendered Float32 PCM (D-48-14; absent when no buffer was emitted).
 * @property {Uint8Array}   [midi]   - Encoded SMF bytes (D-48-18; absent when no MIDI emitted).
 * @property {string}       stdout   - Captured Console.Out (D-48-15).
 * @property {string}       stderr   - Captured Console.Error (D-48-15).
 * @property {RunError[]}   errors   - Structured run errors (D-48-14).
 * @property {number}       durationMs - Wall-clock duration in milliseconds.
 */
