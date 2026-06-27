# Changelog

All notable changes to the **Flow Language** JetBrains plugin are documented in
this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the plugin versioning tracks the Flow language milestones.

## [1.5.0] — 2026-06-08

Initial JetBrains Marketplace release, shipped with the Flow **v1.5** milestone
(*Stage, Studio, Web*). The plugin scaffolding was first authored in Phase 31
(v1.4) and is published for the first time here (Phase 41, JET-01).

### Added

- **LSP4IJ language-server bridge.** Editor support for `.flow` files is provided
  by wrapping the `flow lsp` language server through
  [LSP4IJ](https://github.com/redhat-developer/lsp4ij) 0.19.3 — completions,
  hovers, signature help, and diagnostics from the same server that powers the
  VSCode extension (Phase 17). The server is registered via
  `FlowLanguageServerFactory` and routed to all `*.flow` files.
- **Marketplace publish configuration.** `build.gradle.kts` gains the IntelliJ
  Platform 2.x `signing`, `publishing`, and `pluginVerification` blocks. All
  signing/publishing secrets are read from environment variables only
  (`CERTIFICATE_CHAIN`, `PRIVATE_KEY`, `PRIVATE_KEY_PASSWORD`, `PUBLISH_TOKEN`)
  and are never committed to version control.
- **Direct-download fallback.** `docs/jetbrains/install.html` documents manual
  installation from a downloaded plugin `.zip` for use while Marketplace review
  is pending.

### Compatibility

- IntelliJ Platform IDEs **build 242 and later** (IntelliJ IDEA Community
  2024.2+, plus PyCharm / WebStorm / GoLand / etc. on the same platform).
  `since-build = "242"`; the upper bound is left open so newer platform builds
  are not gated out.

### Requirements

- The `flow` binary on `PATH` (install via Phase 30's `flow install`, which
  drops `flow` into `~/.local/bin/`), **or** the `FLOW_LSP_PATH` environment
  variable pointing at the `flow` binary as a fallback. The plugin invokes
  `flow lsp` to start the language server.
- For building from source: **JDK 21** and the bundled Gradle 8.6 wrapper. (The
  installed plugin uses IntelliJ's bundled JBR at runtime.)

[1.5.0]: https://github.com/noah-freelove/flow-sharp/releases/tag/v1.5.0
