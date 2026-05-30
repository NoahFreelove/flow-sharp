# Phase 48 — HUMAN-UAT: WASM Runtime Browser Smoke

**Status:** Pending composer verification
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
| OS + version | (composer fills in) |
| Boot time (DevTools Network → DOMContentLoaded) | (composer fills in, target <3s) |
| Sine tone audible? | (yes/no) |
| stdout split working? | (yes/no) |
| errors[] structured? | (yes/no — inspect via `console.log(runtime.run(...))` in DevTools) |
| Autoplay policy: audio blocked before Run click? | (yes/no — should be YES; if NO, D-48-09 contract violated) |
| Composer sign-off | (initials + date OR "blocked: <reason>") |
| Gotchas observed | (free text) |

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

(Free-form notes block — composer documents anything observed that the structured rows don't cover. Browser-specific [JSImport] quirks, AudioContext.resume() behavior differences, sample rate negotiation, etc.)
