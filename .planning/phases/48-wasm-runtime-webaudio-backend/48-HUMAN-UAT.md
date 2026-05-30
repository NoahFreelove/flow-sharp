# Phase 48 — HUMAN-UAT: WASM Runtime Browser Smoke

**Status:** BLOCKED — runtime fails to boot in-browser (Chrome). See Row 1 + Composer Notes.
**Prerequisites:** Plan 48-04 shipped (flow-runtime.js + WasmEntry.cs + index.html in AppBundle).

## Setup

```bash
# 1. Publish the WASM bundle
dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release

# 2. Serve the publish output locally (Python 3.x available on most dev machines;
#    `python -m http.server` on macOS/Linux; the binary is `python3` on most systems)
#
#    NOTE (per 48-04-SUMMARY.md): the .NET 10 SDK Mono-WASM publish for this
#    project is FLAT — there is NO AppBundle/ subdir. dotnet.js lands at the
#    publish root; flow-runtime.js + index.html land under publish/wasm/. Serve
#    from the publish root so the runtime's `../dotnet.js` relative import resolves.
cd flow-lang/bin/Release/net10.0/browser-wasm/publish/
python3 -m http.server 8080

# 3. In another terminal/browser, visit:
http://localhost:8080/wasm/index.html
```

The textarea pre-fills with `(play (createSineTone 440Hz 1.0 0.5))` and the Run
button wires the D-48-09 user-gesture chain (`await resumeAudio()` THEN
`await run(source)` in the same click handler). If a future SDK reintroduces the
`_framework/` subdir, the serve path is unchanged; only `flow-runtime.js`'s
top-of-file import target would move (see 48-04-SUMMARY.md Deviations).

## Reproducible Steps (run per browser)

For each of the 3 browser rows below:

1. Open the browser DevTools console BEFORE navigating to the URL.
2. Visit `http://localhost:8080/wasm/index.html` (or the adjusted path).
3. Wait for the runtime to boot. Expect to see one of:
   - In DevTools console: "Flow runtime loaded" (if flow-runtime.js logs on boot — verify in Plan 48-04 implementation).
   - In the page: the textarea pre-filled with `(play (createSineTone 440Hz 1.0 0.5))` + a Run button.
4. Click the Run button. Expect:
   - An audible 440 Hz tone for ~1 second at 50% volume.
   - The `stdout` pane displays no output (sine tone doesn't print anything).
   - The `errors` pane displays no entries.
   - In DevTools console (Network tab): a single `dotnet.js` load + a few `_framework/*` loads; no 404s.
5. Modify the textarea to `(print "hello flow")` and click Run again. Expect:
   - No audio.
   - The `stdout` pane shows `hello flow`.
   - No errors.
6. Modify the textarea to `(print (add 1 2))` and click Run. Expect: stdout shows `3`.
7. Try a deliberate parse error: `(print` (unclosed paren). Expect:
   - The `errors` pane shows at least one entry with `kind: "parse"`.
   - No crash; the page is still responsive.

## Per-Browser Rows

### Row 1: Chrome 120+ (Linux/macOS/Windows)

| Field | Value |
|-------|-------|
| Browser version | (composer fills in) |
| OS + version | Linux |
| Boot time (DevTools Network → DOMContentLoaded) | n/a — runtime never booted |
| Sine tone audible? | NO — runtime boot failed |
| stdout split working? | n/a |
| errors[] structured? | n/a |
| Autoplay policy: audio blocked before Run click? | n/a |
| Composer sign-off | **BLOCKED 2026-05-30** |
| Gotchas observed | `Flow runtime boot failed: Failed to load config file dotnet.boot.js — TypeError: error loading dynamically imported module: http://localhost:8080/dotnet.boot.js`. dotnet.js loaded (the `../dotnet.js` import resolved) but the loader then 404'd on the boot manifest at the served root. → Boot-manifest path mismatch; routed to Plan 48-06.1 repair. |

### Row 2: Firefox 121+ (Linux/macOS/Windows)

| Field | Value |
|-------|-------|
| Browser version | (composer fills in) |
| OS + version | (composer fills in) |
| Boot time | (composer fills in) |
| Sine tone audible? | (yes/no) |
| stdout split working? | (yes/no) |
| errors[] structured? | (yes/no) |
| Autoplay policy: audio blocked before Run click? | (yes/no) |
| Composer sign-off | (initials + date OR "blocked: <reason>") |
| Gotchas observed | (free text) |

### Row 3: Safari 17+ (macOS only, optional if composer is on Linux)

| Field | Value |
|-------|-------|
| Browser version | (composer fills in) |
| OS + version | (composer fills in) |
| Boot time | (composer fills in) |
| Sine tone audible? | (yes/no) |
| stdout split working? | (yes/no) |
| errors[] structured? | (yes/no) |
| Autoplay policy: audio blocked before Run click? | (yes/no) |
| Composer sign-off | (initials + date OR "skipped: no macOS available") |
| Gotchas observed | (free text) |

## Closure Conditions

Phase 48 HUMAN-UAT passes if AND ONLY IF:
- Row 1 (Chrome) signed off as pass OR documented gotcha non-blocking.
- Row 2 (Firefox) signed off as pass OR documented gotcha non-blocking.
- Row 3 (Safari) signed off as pass OR explicitly skipped with reason "no macOS available — defer to Phase 49 / v1.6".

If any row fails with a blocking issue:
- Plan 48-07 (closer) reads this file, routes the issue to either:
  - In-phase repair: open Plan 48-06.1 to fix.
  - v1.6 deferral: log the gotcha in MILESTONES.md v1.6 backlog.
  - Phase 49 hand-off: if the issue is browser UX-shaped (NOT a runtime defect).

## Composer Notes

**2026-05-30 — BLOCKING boot failure (Chrome, Linux).** First browser smoke surfaced:

```
Flow runtime boot failed: Failed to load config file dotnet.boot.js
TypeError: error loading dynamically imported module: http://localhost:8080/dotnet.boot.js
```

**Diagnosis (in progress):** `flow-runtime.js` imports `../dotnet.js`, which resolved
successfully from `http://localhost:8080/wasm/index.html` → `http://localhost:8080/dotnet.js`.
The .NET 10 Mono-WASM loader then tried to fetch its boot manifest `dotnet.boot.js`
relative to the served root (`http://localhost:8080/dotnet.boot.js`) and got a 404.

**CONFIRMED root cause (on-disk publish inspected 2026-05-30):** `dotnet.boot.js`
(and `blazor.boot.json`) **do not exist anywhere** in the publish output. The
`…/browser-wasm/publish/` directory contains the raw build/AOT intermediate set,
NOT a runnable web bundle:

- Publish root has `dotnet.js` + `dotnet.native.wasm` + `dotnet.runtime.js` **AND**
  build intermediates that a real bundle never ships: `driver.c`, `corebindings.c`,
  `libmonosgen-2.0.a`, `emcc-link.rsp`, `*.h`, `wasm-props.json`.
- **No `dotnet.boot.js`, no `_framework/`, no `AppBundle/`/`wwwroot/`.**
- `flow-runtime.js`'s `import '../dotnet.js'` resolved fine (dotnet.js IS at root);
  `dotnet.create()` then fetched its boot manifest at root → 404.

**Why:** `flow-lang` is a **library** project. `dotnet publish` of a library with
`RuntimeIdentifier=browser-wasm` emits the runtime + native intermediates but never
runs the app-bundle generation step (`WasmGenerateAppBundle` / boot-manifest write),
because a library has no WASM app head/entry. So `dotnet.js` has nothing to boot.

**Why this escaped earlier plans:** 48-04 presence-checked files on disk + confirmed
`publish` exits 0, but never *booted* the runtime in a browser. 48-05's determinism /
bundle-size tests call `WasmEntry.RunFromJs` **in-process on Desktop**, never through
the browser boot path — so the missing manifest was invisible to the xUnit suite.

**Fix direction (for /gsd:debug):** make `FlowTarget=Web` produce a bootable WASM app
bundle — e.g. emit `dotnet.boot.js` via the app-bundle target (a wasm app head /
`OutputType`+`WasmGenerateAppBundle` handling), then reconcile `flow-runtime.js`'s
`../dotnet.js` import + `wasm/` placement + `index.html` against the new bundle layout.
Verify by **republish → serve → boot in a real browser** (the xUnit suite cannot
catch this class of defect — add a browser/boot smoke or at least a
"boot-manifest-exists" publish-output assertion).

**Routing (per Closure Conditions):** runtime/build-config defect, NOT a browser-UX
nit → in-phase repair before Plan 48-07 closer. Handed to `/gsd:debug` (2026-05-30).
Firefox/Safari rows deferred until the boot fix lands and Chrome re-smoke passes.
