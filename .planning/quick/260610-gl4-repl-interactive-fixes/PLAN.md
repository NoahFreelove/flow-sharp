---
quick_id: 260610-gl4
slug: repl-interactive-fixes
date: 2026-06-10
---

# Quick Task: REPL interactive-terminal defect fixes

Fix six interactive-REPL defects found in the first real-terminal smoke test (macOS).
Root causes traced via decompilation of PrettyPrompt 4.1.1 `PromptCallbacks` defaults.

## Findings + root cause + fix

1. **Completion dead on first prompt line, works after.** Default
   `ShouldOpenCompletionWindowAsync` only auto-opens after `(` / `.` / a letter
   following whitespace at caret==1. On a fresh first line the heuristic mis-fires.
   Tab DOES force-open (CompletionPane.OnKeyDown:139), so the symptom is the
   *auto*-open heuristic. Fix: override `ShouldOpenCompletionWindowAsync` so it
   opens charitably on identifier chars / `(` / `@` / inside `use "` strings, and
   reopens after a backspace (addresses findings 1 + 3).

2. **`use "` + Tab → no module suggestions, ever.** PrettyPrompt's default
   `GetSpanToReplaceByCompletionAsync` treats only `[A-Za-z0-9_]` as replaceable.
   After `use "@aud` the span-to-replace is `aud`; the module item ReplacementText
   `@audio` does not start with `aud`, so `FilteredView.Match` filters it out and the
   window shows nothing. The unit test passes because it calls `BuildItems` directly,
   bypassing PrettyPrompt's span/filter layer. Fix: override
   `GetSpanToReplaceByCompletionAsync` to extend the replaced span across the leading
   `@` (and the open `use "` quote) so the module path matches.

3. **Backspace after accepting completion doesn't reopen.** Same root as #1 — covered
   by the `ShouldOpenCompletionWindowAsync` override (open when the char left of caret
   is an identifier char, after a Backspace keypress).

4. **`:help createSineTone` → "no documentation entry".** `createSineTone` /
   `createSawTone` / `createSquareTone` / `createTriangleTone` genuinely absent from
   `BuiltInDocs.cs` (only `sine`/`saw`/`square`/`triangle` exist). Fix: add entries
   with the Hertz-first overload `(createSineTone 440Hz 1.0 0.5)` + duration-first
   forms + runnable Example, verified against BuiltInFunctions.cs + audio.flow.

5. **Ctrl+R does nothing.** Root cause is finding 6 — the mixed-format history file
   poisons PrettyPrompt's base64 history load, so reverse-search has nothing to search.

6. **History file mixes base64 (PrettyPrompt) + plaintext (our manual append) lines.**
   Two owners of persistence: PrettyPrompt auto-saves base64 on submit AND `Repl.cs:92`
   calls `_lineEditor.AppendHistory(input)` writing plaintext. Fix: make PrettyPrompt
   the SOLE owner — delete the manual append from the loop. Add a startup sanitizer
   that backs up a corrupted/mixed file and strips non-base64 lines before PrettyPrompt
   loads (charitable; never crash). Keep 10k-cap + 0600-mode. Rework AppendHistory/
   LoadHistory to the base64 format so the live file stays single-format; update the
   history tests accordingly.

## Gates
- dotnet build green
- dotnet test --filter Repl green (38 existing + new)
- PTY harness (macOS `script`) demonstrates findings 1, 2, 4 fixed + Ctrl+R prompt
- `echo '(print "hi")' | dotnet run` still prints hi (legacy fallback intact)
- `flow doc` exit 0 (BuiltInDocs touched)
