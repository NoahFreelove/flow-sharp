---
id: SEED-001
status: dormant
planted: 2026-05-02
planted_during: v1.3 Composer DX Tier B/C — Phase 22 (in progress)
trigger_when: Starting a new milestone after v1.3 closes, or any milestone focused on developer experience / editor tooling
scope: Large
---

# SEED-001: v1.4 LSP enhancements + JetBrains plugin (stretch)

## Why This Matters

Flow shipped a Language Server + VSCode extension in v1.2 (Phase 17), but the
extension is not yet on the VS Code marketplace and the README currently
overstates editor support. Beyond publishing, the LSP itself has gaps that
hurt both target audiences:

- **Music-production audience** wants contextual suggestions that understand
  the active musical context (key, tempo, timesig), the imported stdlib
  modules (`@audio`, `@harmony`, `@notation`), and which feature flags /
  pragmas are enabled.
- **Functional-language audience** (Flow is Lisp/Haskell-inspired) expects
  the table-stakes features they get in other functional editors — proper
  function-application coloring, varargs visibility in signature help,
  multi-form comment recognition, and useful diagnostics that go beyond
  "function not found".

Concretely missing today:
- Suggestions don't filter by what the current file actually `use`d
- Suggestions don't reflect feature-flag / pragma state (e.g. H-as-B alias)
- Function calls aren't visually distinguished from identifiers
- Varargs functions don't surface their variadic shape in completions/hovers
- Only `//` is recognized as a comment — common doc styles like
  `Note: ...` / `TODO: ...` / `;` (Lisp-style) aren't highlighted
- Diagnostics are mostly errors; warnings (e.g. unused import, out-of-key
  note when scale linting lands, unreachable section) are sparse or missing

Stretch goal: a JetBrains plugin so users on Rider/IntelliJ/PyCharm get the
same Flow experience. The LSP work above is a prerequisite — once the
language server is solid, wrapping it for IntelliJ Platform via the LSP4IJ
or built-in LSP support is mechanical.

## When to Surface

**Trigger:** Starting a new milestone after v1.3 closes, or any milestone
focused on developer experience / editor tooling.

This seed should be presented during `/gsd-new-milestone` when the milestone
scope matches any of these conditions:
- Milestone explicitly scoped to "v1.4" or to LSP / editor / tooling work
- Milestone goals mention "developer experience", "DX", "IDE", "editor",
  "completion", "diagnostics", or "JetBrains"
- Milestone follows v1.3 close (next-in-line slot)

## Scope Estimate

**Large** — likely a full milestone of its own. Rough phase shape:

1. **Diagnostics & warnings expansion** — unused imports, type-narrowed
   warnings, scale-lint hookup, structured severity model in `flow-lsp`
2. **Context-aware completion** — module-import filtering, pragma/feature-flag
   awareness, musical-context-aware suggestions (already partial for roman
   numerals; extend to chord/scale completion in key blocks)
3. **Varargs in signature help & hover** — surface variadic params in
   `OverloadResolver` output through `LspMappings.cs`
4. **Grammar enhancements** — recognize `;` Lisp-style comments,
   `Note:` / `TODO:` / `FIXME:` lead-in comment forms, distinct scopes for
   function-call vs identifier vs builtin
5. **VS Code marketplace + OpenVSX publish** — finish v1.2 Phase 17 rows 4-5
   that were deferred to first release tag
6. **(Stretch) JetBrains plugin** — wrap `flow-lsp` via LSP4IJ; ship to
   JetBrains Marketplace

## Breadcrumbs

Existing code likely to be touched:

- `flow-lsp/Program.cs` — LSP server entrypoint
- `flow-lsp/DocumentManager.cs` — document state, parse cache
- `flow-lsp/ParseSession.cs` — incremental parsing for diagnostics
- `flow-lsp/LspMappings.cs` — Flow ↔ LSP type translation (completion,
  hover, signature help)
- `vscode-extension/src/extension.ts` — language client wiring
- `vscode-extension/syntaxes/flow.tmLanguage.json` — TextMate grammar
  (function-call coloring, comment forms live here)
- `vscode-extension/language-configuration.json` — comment definitions
- `flow-lang/Lexing/SimpleLexer.cs` — tokenizer (single-line `//` comments
  added v1.1 Phase 7; extend for `;` and lead-in forms)
- `flow-lang/TypeSystem/OverloadResolver.cs` — varargs scoring source
- `flow-lang/Runtime/MusicalContext.cs` — context state for context-aware
  completion
- `flow-lang/Runtime/ModuleLoader.cs` — import graph for filtering
  suggestions by what's `use`d

Related decisions in PROJECT.md:
- "LSP project references flow-lang directly" (v1.2 Key Decisions)
- "Per-platform self-contained VSIX with bundled stdlib" (v1.2 Key Decisions)

Open from v1.2 Phase 17:
- HUMAN-UAT rows 1-3 (manual smoke deferred to first release tag)
- Rows 4-5 (non-dev OS + Marketplace/OpenVSX publish verification) deferred
  to first release tag — likely converges with this milestone's publish step

## Notes

- Captured 2026-05-02 during a session where the README was identified as
  overstating LSP availability (not on VS Code marketplace yet) and the
  user observed that `Note: ...` is not currently treated as a comment.
- Local dev workflow is now wired: `vscode-extension/.vscode/launch.json`
  (extension dev host), `vscode-extension/server/linux-x64` symlinked to
  `flow-lsp/bin/Debug/net10.0/` for live LSP testing.
- v1.3 must close cleanly first — Phase 22 is in flight, Phases 23-27
  still pending. Do not pre-empt v1.3 for this work.
- JetBrains plugin is explicitly stretch. The LSP-quality work is the
  table-stakes part of the milestone; the IntelliJ wrapper can defer to
  v1.5 if scope is too large.
