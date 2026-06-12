---
quick_id: 260611-x8l
slug: allow-note-comment-marker-mid-line-trail
status: complete
date: 2026-06-12
---

# Quick Task 260611-x8l — Summary

## What was done

`Note:` now works as a comment **trailing a statement** on the same line, not only at line
start — e.g. `(play x)   Note: this plays the tone`. (`Note:` at line start already worked;
the composer's gap was trailing/mid-line.)

`flow-lang/Lexing/SimpleLexer.cs`: the `Note:` arm in `SkipWhitespaceAndComments()` no longer
requires `IsStartOfLineContent()`; instead it excludes only the one real collision via a new
`IsTypeAnnotationColonPosition()` helper.

## The collision (caught + handled)

A naive "remove the line-start guard" broke the whole language: **`Note:` is core syntax — the
proc-parameter TYPE ANNOTATION** `Type: name` (`internal proc str (Note: value)`,
`proc bumpBeat (Beat: b)`, `(String: phoneme, Note: pitch, Double: duration)`). The stdlib is
saturated with it, so commenting those out made std.flow/audio.flow/bars.flow/notation.flow
fail to load → `print`/`dict`/`add`/... "not found" at engine init.

Disambiguation: a `Type:` annotation is the only place a type name is immediately followed by
`:`, and it **always sits right after `(` or `,`** in the parameter list. `IsTypeAnnotationColonPosition()`
scans back over spaces/tabs/`\r`: first non-whitespace char `(` or `,` → type annotation (not a
comment); a newline (line start) or any other char (trailing content) → comment.

Scope: only `Note:` (the explicit ask). `TODO:`/`FIXME:` keep their line-start guard — they
are not Flow type names, so no collision, but were left unchanged for tight scope.

## Verification

- Engine init clean, core builtins present: `(print (str (add 2 3)))` → `5`; `dict` works.
- Trailing comment: `(print "ok") Note: trailing` → `ok` (comment ignored).
- Line-start `Note:` still a comment; quoted `"Note: ..."` still a String.
- Type annotations intact: stdlib loads with no errors (it is full of `(Note:`/`, Note:`).
- `flow-lang.Tests`: lexer/parser/comment suites **277/277**; full suite **2428 passed**.
  The 4–5 failures are flaky/environmental Integration tests (Phase40 JACK server presence,
  Phase41 Showcase RMS pre-existing, Phase48 WASM) — the cluster shifts run-to-run and none
  are lexer/parser-related (confirmed pre-existing/flaky in quick task 260611-wp2 too).
- Proc/lambda-heavy `.flow` scripts smoke clean.

## Follow-on

Ships in the WASM bundle regen for the live playground (with the cutoff + createSineTone fixes).
