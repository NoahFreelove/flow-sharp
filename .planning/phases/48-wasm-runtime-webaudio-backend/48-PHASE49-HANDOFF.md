# Phase 48 → Phase 49 Hand-off: `flow-runtime.js` Consumption Contract

**From:** Phase 48 (WASM Runtime + WebAudioBackend) — SHIPPED 2026-06-05
**To:** Phase 49 (flowlang.dev SvelteKit site)
**Purpose:** Lets the Phase 49 planner / executor consume `flow-runtime.js` from the
playground tab WITHOUT re-deriving the contract from C# / JS source. Read this BEFORE
writing any playground code. The API surface is frozen per D-48-13 — treat it as a
published contract.

---

## 1. Where to find the bundle

| Artifact | Path | Role |
|----------|------|------|
| ES module (source) | `flow-lang/wasm/flow-runtime.js` | The hand-written runtime API. This is the file Phase 49 imports. |
| JSExport boundary (source) | `flow-lang/Runtime/WasmEntry.cs` | C# side: 4 `[JSExport]` methods + `RunResult` / `RunError` POCOs. DO NOT consume directly — go through `flow-runtime.js`. |
| JSImport boundary (source) | `flow-lang/Audio/FlowRuntimeInterop.cs` | C# side: 5 `[JSImport(..., "flow-runtime")]` audio bindings, wired JS-side by `setModuleImports('flow-runtime', {...})`. |
| Dev-smoke harness | `flow-lang/wasm/index.html` | Reference implementation of the D-48-09 gesture chain. NOT shipped to flowlang.dev — Phase 49 builds the real UI. |
| Published AppBundle | `flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/` | The runnable bundle (`dotnet publish -p:FlowTarget=Web -c Release`). |

**Published AppBundle layout (verified on disk, post-boot-fix):**

```
AppBundle/
  flow-runtime.js        ← THIS is the file you import
  index.html             ← dev harness (do not ship)
  package.json           ← { "type": "module" }
  _framework/
    dotnet.js            ← Mono-WASM loader (flow-runtime.js imports ./_framework/dotnet.js)
    dotnet.boot.js       ← boot manifest (mainAssemblyName: flow-lang.dll) — serves HTTP 200
    dotnet.native.wasm   ← Mono runtime
    flow-lang.wasm       ← Webcil-encoded main assembly (NOTE: .wasm, not .dll, in this layout)
    System.*.wasm        ← Webcil-encoded framework assemblies
```

`flow-runtime.js` sits at the AppBundle ROOT; `dotnet.js` sits under `_framework/`. The
module's top-level `import { dotnet } from './_framework/dotnet.js'` descends into that dir,
and `dotnet.create()` then fetches `dotnet.boot.js` from the same `_framework/`. **Preserve
this relative layout when copying into SvelteKit `static/`.**

---

## 2. How to integrate into SvelteKit

1. **Build step:** copy the published `AppBundle/` into SvelteKit `static/wasm/` (e.g. a
   `flow-site/scripts/sync-runtime.sh` that runs `dotnet publish -p:FlowTarget=Web -c Release`
   then `cp -r .../AppBundle/* static/wasm/`). Keep the `flow-runtime.js`-at-root +
   `_framework/`-sibling layout intact.
2. **Dynamic import on the playground tab only (D-49-02):**
   ```js
   const { loadFlowRuntime } = await import('/wasm/flow-runtime.js');
   const runtime = await loadFlowRuntime();   // idempotent — caches after first boot
   ```
   Defer-loading on `/playground` keeps marketing + docs pages snappy — the ~3 MB WASM
   download never fires on Home / Docs / Showcase.
3. **Boot errors vs. run errors are distinct.** `loadFlowRuntime()` throws
   `Error('Flow runtime boot failed: ...')` if the Mono-WASM runtime cannot boot — surface
   this in a top-level error pane. Per-script errors come back inside `RunResult.errors[]`
   (see §4), NOT as thrown exceptions.

---

## 3. Serving headers (COOP/COEP — D-48-02 v1.6 preview)

Phase 48 v1 ships single-threaded WASM and needs NO special headers. The v1.6 AudioWorklet +
SharedArrayBuffer streaming stretch (D-48-02) WILL need cross-origin isolation. Cloudflare
Pages sets these natively via a `_headers` file (chosen over GitHub Pages for exactly this
reason — D-49-04). Phase 49 MAY wire them preemptively at deploy time (zero cost if the v1.6
streaming path is never built; preserves the option):

```
# flow-site/static/_headers  (Cloudflare Pages)
/playground/*
  Cross-Origin-Opener-Policy: same-origin
  Cross-Origin-Embedder-Policy: require-corp
```

Note: `require-corp` makes every cross-origin subresource on `/playground` need CORP/CORS
headers (Monaco CDN assets, fonts, etc.). If that complicates the v1 playground, leave the
headers OFF for v1.5 and add them only when the v1.6 streaming path lands — the offline-render
runtime does not require them.

---

## 4. API contract

TypeScript-like signatures (documentation only — there is no enforced `.ts` file; the source
is `flow-lang/wasm/flow-runtime.js` with JSDoc `@typedef`s):

```ts
export async function loadFlowRuntime(): Promise<Runtime>   // idempotent; caches the runtime

interface Runtime {
  run(source: string): Promise<RunResult>          // execute Flow source
  play(wav: Float32Array | number[],               // push raw PCM (rarely needed directly —
       sampleRate?: number /* =44100 */,           //   (play ...) in Flow source already routes
       channels?: number  /* =2 */): void          //   here via WebAudioBackend)
  stop(): void                                     // revoke any active source node (idempotent)
  dispose(): void                                  // tear down backend + engine (idempotent)
  resumeAudio(): Promise<void>                     // D-48-09 — call from a user-gesture frame
}

interface RunResult {
  wav?: Float32Array     // present only if absent fields are omitted; reserved (see note)
  midi?: Uint8Array      // present if the Flow source emitted MIDI (D-48-18)
  stdout: string         // captured `print` output (D-48-15)
  stderr: string         // captured advisory `[X] ...` output (D-48-15)
  errors: RunError[]     // structured parse/eval/runtime errors (D-48-14)
  durationMs: number     // wall-clock ms — NOT byte-identical across runs; exclude from any cmp
}

interface RunError {
  kind: "parse" | "eval" | "runtime" | "cancel" | "platform-not-supported"
  message: string        // human-readable; NO .NET stack traces leak (T-48-15)
  line?: number          // 1-based, when known
  column?: number        // 1-based, when known
  sourceSnippet?: string // quoted source line for Rust-style diagnostic boxes
}
```

**Contract notes:**

- `run()` returns a JSON object (parsed from the JSON string `WasmEntry.RunFromJs` returns).
  JSON keys are camelCase (`stdout`, `durationMs`); absent `wav` / `midi` are OMITTED, not
  `null` — test with `if ('midi' in result)` or `result.midi != null`.
- `wav` is reserved in the contract but is currently `null` from `run()` — in-browser audio
  goes out through the live `WebAudioBackend` when the Flow source calls `(play ...)`, not by
  returning a buffer to JS. Phase 49 does NOT need to wire `wav` playback manually for the
  common case; the tone plays itself. (A future `runtime.exportWav()` helper is a v1.6
  option for download — see §9.)
- `run()` is declared `async` to preserve the Promise-returning surface, but in single-threaded
  WASM the Flow script executes SYNCHRONOUSLY (blocks the JS event loop for its duration). A
  runaway script hangs the tab — the D-48-10 30s cap is best-effort, non-preemptive in-browser
  (see §5 / 48-VERIFICATION.md Caveat 1). The `"cancel"` error kind stays DEFINED but is not
  raised in-browser.

---

## 5. User-gesture chain requirement (D-48-09) — MANDATORY

Browser autoplay policy blocks `AudioContext.resume()` outside a user gesture. The runtime
NEVER calls `resume()` itself — that is the playground's responsibility.

- The Run button's `onclick` handler MUST call BOTH `await runtime.resumeAudio()` AND
  `await runtime.run(source)` in the SAME async function frame:
  ```js
  runButton.onclick = async () => {
    await runtime.resumeAudio();   // creates + resumes the AudioContext in the gesture frame
    const result = await runtime.run(editor.getValue());
    renderConsole(result);         // stdout / stderr / errors
  };
  ```
- DO NOT call `resumeAudio()` on page load or outside a click/keypress handler — it will be a
  silent no-op and the tone will not play (this exact bug — resuming a not-yet-created context
  — was the final boot-fix cycle; see 48-HUMAN-UAT.md).
- The "Play in playground" deep-link from Home (D-49-08) auto-clicks Run on arrival, which
  counts as the gesture — keep that auto-click inside the same handler that calls
  `resumeAudio()`.
- `resumeAudio()` is idempotent and cheap — calling it on every Run is fine and recommended.

**Verified:** Firefox composer UAT 2026-06-05 confirmed audio is silent until the Run gesture,
then audible — D-48-09 satisfied.

---

## 6. Bundle size + first-paint cost

- **First-paint cost: ~3.07 MB compressed Brotli** (canonical Plan 48-05 measurement;
  10.99 MB uncompressed). The post-boot-fix Webcil AppBundle re-measures even smaller at
  ~1.63 MB Brotli / 5.38 MB uncompressed (`48-BUNDLE-SIZE.md`). Either way, well under the
  5 MB "comfortable first-paint" threshold. Includes the Mono-WASM runtime + `flow-lang.wasm`
  + embedded stdlib + DryWetMidi.
- **Defer-load on the playground tab only (D-49-02).** Marketing + docs pages do NOT trigger
  this download. Phase 49's static code blocks render as syntax-highlighted text with an
  "Open in playground" CTA — the runtime boots only when the playground tab mounts.
- Enable Brotli on the Cloudflare Pages serving config (default for static assets) so the
  ~3 MB compressed figure is what actually crosses the wire.

---

## 7. Known browser gotchas (from 48-HUMAN-UAT.md)

| Browser | Status | Gotcha / note for Phase 49 |
|---------|--------|----------------------------|
| Firefox 121+ (Linux) | PASS | Clean boot + audible 440 Hz tone; autoplay-correct. Load-bearing proof the runtime works. |
| Chrome 120+ (Linux) | DEFERRED | Original `dotnet.boot.js` 404 boot blocker is FIXED + HTTP-verified; only the human audio ear-check is outstanding. Phase 49 should re-smoke audio in Chrome/Chromium early (it shares the engine path Firefox proved). |
| Safari 17+ (macOS) | SKIPPED | No macOS on the Linux-only dev machine. Phase 49 / v1.6 should verify Safari — historically the strictest autoplay policy; the D-48-09 gesture chain should satisfy it, but confirm. |

Both deferrals are logged in `.planning/MILESTONES.md` v1.6 backlog.

---

## 8. What Phase 49 must NOT change

- **DO NOT modify `flow-runtime.js`.** It is the canonical, frozen surface (D-48-13). If
  SvelteKit ergonomics need a thin adapter, WRAP it in a `flow-site/src/lib/runtime.ts`
  module — do not edit the runtime file.
- **DO NOT modify `WasmEntry.cs`.** Phase 49 consumes via the JSON-parsed `RunResult` only.
  The 4 `[JSExport]` names + `RunResult` / `RunError` shape are pinned (JS + xUnit tests parse
  them directly).
- **DO NOT add new `[JSImport]` / `[JSExport]` names.** Any new boundary method is a future
  v1.6 WASM-runtime phase change to `WasmEntry.cs` / `FlowRuntimeInterop.cs`, not a Phase 49
  edit.
- **DO NOT break the AppBundle relative layout** (`flow-runtime.js` at root, `_framework/`
  sibling). The `./_framework/dotnet.js` import + `dotnet.boot.js` fetch depend on it.

---

## 9. MIDI + WAV export download mechanism (D-48-18)

`runtime.run(source)` returns `RunResult.midi` as a `Uint8Array` when the Flow source called
`writeMidi` (DryWetMidi is WASM-compatible — D-48-17 confirmed, ships on Web). Phase 49 wires
the download UI; no backend change needed:

```js
const result = await runtime.run(source);
if (result.midi) {
  const blob = new Blob([result.midi], { type: 'audio/midi' });
  const url = URL.createObjectURL(blob);
  const a = Object.assign(document.createElement('a'), { href: url, download: 'flow.mid' });
  a.click();
  URL.revokeObjectURL(url);
}
```

**WAV export:** parallel mechanism. `RunResult.wav` is `Float32Array` (reserved; currently
`null` from `run()` — audio plays live via WebAudioBackend in v1). To offer a WAV download,
Phase 49 either (a) wraps a Float32Array in a ~40-LOC hand-rolled WAVE header and triggers the
same Blob/anchor download, OR (b) defers to a v1.6 `runtime.exportWav()` helper added to
`flow-runtime.js`. Notation exports (`@notation-io` MusicXML / LilyPond / ABC / MML) are
strings — same Blob-download mechanism with the appropriate MIME type (D-48-19).

---

## 10. Quick-start snippet for the Phase 49 playground

```js
// /playground tab mount (SvelteKit onMount, playground-only)
const { loadFlowRuntime } = await import('/wasm/flow-runtime.js');
let runtime;
try {
  runtime = await loadFlowRuntime();
} catch (e) {
  showBootError(e.message);   // top-level pane (distinct from per-run errors)
}

// Run button — single gesture frame (D-48-09)
runButton.onclick = async () => {
  await runtime.resumeAudio();                 // MUST be inside the click frame
  const result = await runtime.run(editor.getValue());
  stdoutPane.textContent = result.stdout;
  stderrPane.textContent = result.stderr;      // dimmed/italic per D-48-15
  renderDiagnostics(result.errors);            // Rust-style boxes per D-48-14
  if (result.midi) offerMidiDownload(result.midi);  // §9
  // (play ...) in the Flow source already produced the tone via WebAudioBackend
};
```

Default playground starter script (matches the dev harness): `use "@audio"` then
`(play (createSineTone 440Hz 1.0 0.5))` → audible 440 Hz tone on Run.
