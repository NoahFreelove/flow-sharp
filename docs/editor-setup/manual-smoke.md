# Manual Smoke Checklist — Flow LSP + VSCode Extension

This checklist covers the five manual-only verifications from
`.planning/phases/17-flow-language-server/17-VALIDATION.md` §Manual-Only
Verifications. It is the closing gate on Phase 17 and confirms that the
stack shipped by plans 17-01..17-07 renders correctly inside a real
VSCode session.

**When to run:** once after the VSCode extension is built locally (plan
17-08 Task 3, before phase closure) and again after the first release
tag push once the Marketplace and OpenVSX listings go live (deferred
items M-04 and M-05 below).

**How long it takes:** ~10–15 minutes once the prerequisite build is
done.

---

## Prerequisite build

The checklist assumes `vscode-extension/server/linux-x64/flow-lsp`
exists and passes `scripts/lsp-smoke.sh`. On a fresh clone:

```bash
cd /home/noah/Desktop/projects/flow-sharp

# 1. Confirm the full xUnit Phase17 suite is green.
dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"
# Expected: green. Record the Fact count (should be >= 55; currently 96 at plan 17-07).

# 2. Publish the self-contained flow-lsp binary + copy stdlib files beside it.
dotnet publish flow-lsp/flow-lsp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:_IsPublishing=true \
  -o vscode-extension/server/linux-x64

cp flow-lang/std.flow         vscode-extension/server/linux-x64/
cp flow-lang/collections.flow vscode-extension/server/linux-x64/
cp flow-lang/audio.flow       vscode-extension/server/linux-x64/
cp flow-lang/bars.flow        vscode-extension/server/linux-x64/
cp flow-lang/notation.flow    vscode-extension/server/linux-x64/
cp flow-lang/composition.flow vscode-extension/server/linux-x64/

# 3. Verify stdlib files landed (Pitfall 6 gate).
ls vscode-extension/server/linux-x64/*.flow
# Expected: exactly 6 files (std, collections, audio, bars, notation, composition).

# 4. Smoke-test the binary over stdio.
chmod +x scripts/lsp-smoke.sh
bash scripts/lsp-smoke.sh vscode-extension/server/linux-x64/flow-lsp
# Expected: "OK: flow-lsp smoke test passed".
# If the binary hangs (exits 3), that is plan 17-01's known debug-binary caveat;
# the self-contained Release binary shipped here is a different artifact and
# should handle initialize/shutdown/exit correctly.

# 5. Build the VSCode extension TypeScript.
cd vscode-extension
npm ci
npm run compile
# Expected: out/extension.js present, 0 TS errors.
```

---

## Manual verification — row 1/5: Syntax highlighting visually matches `flow-editor/` categories

**Decision:** D-04, D-05. **Why manual:** visual perception; automated
pixel-diff is fragile across themes.

1. In VSCode, `File → Open Folder → /home/noah/Desktop/projects/flow-sharp/vscode-extension`.
2. Press **F5** (or Run → Start Debugging). A second window opens —
   the **Extension Development Host** (EDH).
3. In the EDH window, open `tests/test_chords.flow` (or any other
   Flow file from `tests/` or `examples/`).
4. Compare colors against `flow-editor/Editor/FlowSyntaxHighlighter.cs`
   categories. Tick each:
   - [ ] Keywords (`proc`, `use`, `section`, `tempo`, `key`, `timesig`,
         `swing`, `dynamics`, `return`) — single consistent "keyword"
         color.
   - [ ] Type names (`Int`, `Float`, `String`, `Buffer`, `Note`,
         `Chord`, `Sequence`, `Song`) — "type" color (typically
         distinct from keywords).
   - [ ] Strings (`"literal"`, `"@audio"`) — "string" color.
   - [ ] Numbers (`120`, `3.14`, `0.5`) — "numeric" color.
   - [ ] Comments (`// comment`) — muted/gray.
   - [ ] Notes (`C4`, `Db5q`, `F#3`) — "note" color (often yellow in
         Catppuccin Mocha).
   - [ ] Chords (`Cmaj7`, `Dm`, `Bb7`, `Bdim`) — distinct from notes;
         should NOT color as notes or identifiers.
   - [ ] Roman numerals inside `key C { | I IV V7 | }` — colored as
         chords (semantic tokens override).
   - [ ] Note-stream delimiters (`|`) — "operator" or distinct color.
   - [ ] Operators (`->`, `=`, `+`, `*`, `/`) — "operator" color.
   - [ ] Booleans (`true`, `false`) — "constant" color.

**Expected result:** all categories present, coloring consistent across
the file, no unstyled regions.

**Report:** any category colored wrong (e.g., `Bb7` colored as note not
chord) with a pointer to the exact line/file.

---

## Manual verification — row 2/5: TM→semantic-tokens transition during server-spawn window

**Decision:** D-04. **Why manual:** perception/timing; there may be a
brief flicker as LSP semantic tokens overlay the TextMate paint.

1. In the EDH window, close the `.flow` file opened above.
2. Re-open it (or open a different `.flow` file).
3. Watch the **first 0–300 ms** after the file loads.

**Expected:** no visually jarring repaint. TextMate grammar paints
immediately (baseline coloring); semantic tokens may refine scope
assignments once the LSP server responds (~100–300 ms), producing at
most a subtle shift (e.g., a chord literal swapping from a generic
"identifier" scope to "entity.name.function").

**Record:**
- [ ] "not noticeable" — ship as is.
- [ ] "subtle / acceptable" — ship as is.
- [ ] "jarring" — file follow-up; may need a theme-color tweak or a TM
      grammar refinement.

---

## Manual verification — row 3/5: Extension activates on `.flow` file, shows status

**Decision:** D-13. **Why manual:** integration with the VSCode host
process.

1. In the EDH window, with a `.flow` file open:
2. Open the Output panel (`View → Output`).
3. From the output dropdown, select **`Flow LSP Trace`** (may also be
   labeled "Flow Language" or similar).
4. Observe log lines from the server startup sequence.
5. Alternatively, open the status bar and look for a Flow indicator.

**Extra sanity checks while here:**
- [ ] **D-06 diagnostics.** Type `proc (` at the top of the file. Within
      ~200 ms a red squiggle should appear. Remove the corruption — the
      squiggle clears.
- [ ] **D-07 completion.** In a proc body, type `pri` → completion list
      should include `print` with its signature in the detail row.
      Then type `use "@` → expect 6 stdlib paths (`@std`, `@audio`,
      `@collections`, `@bars`, `@notation`, `@composition`) and
      **not** built-in function names or user procs.
- [ ] **D-08 hover.** Hover `print` in the code → expect a markdown
      tooltip with signature + doc summary from `BuiltInDocs`.
- [ ] **D-09 go-to-definition.** Ctrl+Click (or F12) on a
      `use "@audio"` import → jumps to `audio.flow`. On a user-declared
      proc name → jumps to its declaration.
- [ ] **D-10 signature help.** Type `transpose(seq, ` → a signature
      tooltip appears with "active parameter 1" indication.
- [ ] **D-11 note-stream completion.** Inside `key Cmajor { | ` press
      `Ctrl+Space` → expect `I`, `IV`, `V7`, etc. (roman numerals
      resolved through HarmonyFunctions). Inside a `| ... |` stream
      with no enclosing `key` → expect note letters / chord literals /
      durations (`q`, `h`, `w`, `e`, `s`) / rests (`_`). **Neither**
      context should offer user proc names, `tempo`, `proc`, or other
      top-level keywords.
- [ ] **Snippet expansion.** Type `tempo` + Tab → expands to
      `tempo ${1:120} { $0 }` with the cursor at placeholder 1.

**Expected:** all sanity checks green; diagnostics + completion +
hover + go-to-def + signature help all functional.

**Report:**
- `smoke: clear` — all ticks green.
- `smoke: partial - <description>` — some checks failed; describe
  which and against which decision ID.
- `smoke: blocked - <description>` — extension failed to activate or
  core feature broken.

---

## Manual verification — row 4/5: Per-platform binary works on a non-dev OS

**Decision:** D-14. **Why manual:** CI smoke-boots each binary but
visual rendering needs human eyes on macOS/Windows.

**Status: DEFERRED to first release tag push.**

After the first `git push origin v0.1.0` (or whatever tag) completes
and both Marketplace + OpenVSX listings land, install the platform-
appropriate VSIX on one non-Linux machine (macOS or Windows VM) and
repeat rows 1–3 of this checklist.

Track this as a blocking HUMAN-UAT item against the first release, not
against Phase 17 closure. Plan 17-08 cannot execute this because the
VSIXs for macOS and Windows are not yet published.

---

## Manual verification — row 5/5: Marketplace + OpenVSX publish succeeds on tag push

**Decision:** D-15. **Why manual:** end-to-end publish is destructive /
semi-idempotent; dry-runs are the automated surrogate but the real-run
has to happen once.

**Status: DEFERRED to first release tag push.**

On the first tag, watch the
`.github/workflows/publish-extension.yml` run complete (both
`build-server` and `publish` jobs green), then verify:

- The extension appears at
  `https://marketplace.visualstudio.com/items?itemName=<publisher>.flow-language`
  with all 4 per-platform VSIX entries.
- The extension appears at
  `https://open-vsx.org/extension/<publisher>/flow-language`
  with matching platforms.

Track this as a blocking HUMAN-UAT item against the first release, not
against Phase 17 closure. The runbook at
`.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md`
Step 4 walks through this verification.

---

## Summary

| # | Item                                              | Status    |
|---|---------------------------------------------------|-----------|
| 1 | Syntax highlighting visually matches flow-editor  | pending   |
| 2 | TM → semantic-tokens transition acceptable        | pending   |
| 3 | Extension activates + core LSP features work      | pending   |
| 4 | Per-platform binary on non-dev OS                 | deferred to first release tag |
| 5 | Marketplace + OpenVSX publish succeeds on tag     | deferred to first release tag |

Items 1–3 are required for Phase 17 closure. Items 4 and 5 are
explicitly deferred to the first release tag push and are tracked as
HUMAN-UAT items against that milestone.

## Cross-references

- Validation source: `.planning/phases/17-flow-language-server/17-VALIDATION.md`
  §Manual-Only Verifications
- Marketplace runbook:
  `.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md`
- Plan doc: `.planning/phases/17-flow-language-server/17-08-PLAN.md` Task 3
- CI workflow:
  `.github/workflows/publish-extension.yml`
