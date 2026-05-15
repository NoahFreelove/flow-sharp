# flow-jetbrains

JetBrains IDE plugin for the [Flow](https://github.com/noah-freelove/flow-sharp)
language, wrapping the `flow-lsp` language server via [LSP4IJ](https://github.com/redhat-developer/lsp4ij).

**Status:** Phase 31 (v1.4) — Stretch deliverable. The scaffolding lands
unconditionally; whether the built plugin `.zip` ships with the v1.4 release
tag is decided by the Phase 31 manual UAT (see
`.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-VERIFICATION.md`
after closure).

## What This Is

A thin IntelliJ Platform plugin that:

1. Registers the `.flow` file extension as a Flow-typed file.
2. Declares `flow-lsp` as the LSP server for Flow files via LSP4IJ.
3. Spawns the LSP server by invoking `flow lsp` on the user's PATH (the
   `flow lsp` subcommand was added in Phase 31 Plan 01; it delegates to
   `flow-lsp/Program.cs`).

Once the plugin is installed and a `.flow` file is opened, LSP4IJ wires the
editor to the running `flow-lsp` process — completions, hovers, signature
help, and diagnostics from the same server that powers VSCode (Phase 17).

## Requirements

- **IntelliJ Community 2024.2 or later** (build 242+) — LSP4IJ 0.19.3 baseline.
  Older IntelliJ versions will refuse to load the plugin with an "incompatible
  build" error. Download: https://www.jetbrains.com/idea/download/
- **JDK 21** on PATH (for building the plugin from source; the plugin itself
  uses IntelliJ's bundled JBR at runtime).
- **`flow` binary on PATH.** Run `flow install` (Phase 30) to drop `flow` into
  `~/.local/bin/`. The plugin invokes `flow lsp` to start the server.

## Build

```bash
cd flow-jetbrains
./gradlew buildPlugin
```

On first invocation the Gradle wrapper downloads Gradle 8.6 (~150 MB) and the
IntelliJ Platform 2024.2 SDK plus LSP4IJ 0.19.3 (~500 MB total); subsequent
builds reuse the cached artifacts in `~/.gradle/`.

The built plugin lands at:

```
flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip
```

## Install

1. Open IntelliJ IDEA Community 2024.2 or later.
2. **Settings → Plugins → ⚙ (gear icon) → Install Plugin from Disk…**
3. Select `flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip`.
4. Restart IntelliJ when prompted.
5. Open a `.flow` file. LSP4IJ should auto-start the language server
   (check the status bar for an "LSP" indicator).

## Configuration

### `FLOW_LSP_PATH` environment variable (fallback)

If the `flow` binary is **not** on PATH, set `FLOW_LSP_PATH` to its absolute
location before launching IntelliJ:

```bash
export FLOW_LSP_PATH=/absolute/path/to/flow
idea .
```

The factory class (`FlowLanguageServerFactory.kt`) reads `FLOW_LSP_PATH`
first and falls back to `flow` on PATH if unset. This is the
[RESEARCH Pitfall 7](../.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-RESEARCH.md)
documented fallback for binary-discoverability.

## Phase 31 Status Note (CONTEXT D-10)

Per the Phase 31 CONTEXT decision D-10, the scaffolding (this directory,
the Gradle build files, `plugin.xml`, and `FlowLanguageServerFactory.kt`)
lands **unconditionally** at phase closure. Three possible outcomes are
recorded in `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-VERIFICATION.md`:

- **stretch met** — `gradlew buildPlugin` succeeds AND manual UAT confirms
  completions appear in a real IntelliJ install. Plugin `.zip` is attached
  to the v1.4 release tag.
- **stretch met partial: …** — completions appear but one specific feature
  regressed (e.g. Unicode ellipsis encoding). Recorded for v1.5 follow-up.
- **stretch deferred: …** — build or UAT failed; scaffolding stays for v1.5
  to pick up.

## Known Caveat: `gradle-wrapper.jar`

The conventional Gradle wrapper ships a small `gradle/wrapper/gradle-wrapper.jar`
that the `gradlew` script uses to bootstrap. This repository ships the wrapper
**without** the `.jar` for two reasons:

1. JAR files in source control are noisy on diffs.
2. Generating it requires a host with Gradle already installed
   (`gradle wrapper --gradle-version 8.6`), which Phase 31's build environment
   does not have.

To bootstrap the wrapper after cloning, install Gradle 8.6+ (via
[SDKMAN!](https://sdkman.io/) — `sdk install gradle 8.6` — or your platform's
package manager) and run inside this directory:

```bash
gradle wrapper --gradle-version 8.6
```

This generates `gradle/wrapper/gradle-wrapper.jar` and rewrites `gradlew` /
`gradlew.bat` from Gradle's current templates. After that one-time bootstrap,
the wrapper self-hosts — Gradle does not need to remain on PATH.

If a CI environment has Gradle 8.6 available, `./gradlew buildPlugin` works
out of the box (Gradle resolves the missing wrapper jar from the configured
distribution URL).

## Layout

```
flow-jetbrains/
├── build.gradle.kts                    Gradle build (LSP4IJ 0.19.3 + IntelliJ 2024.2)
├── settings.gradle.kts                 Project name
├── gradle.properties                   JVM args + Kotlin code style
├── .gitignore                          Excludes build/, .gradle/, .idea/, *.iml
├── README.md                           This file
├── gradle/wrapper/
│   └── gradle-wrapper.properties       Wrapper config (Gradle 8.6)
├── gradlew                             POSIX wrapper script (chmod +x)
├── gradlew.bat                         Windows wrapper script
└── src/main/
    ├── resources/META-INF/
    │   └── plugin.xml                  Plugin descriptor (LSP4IJ extensions)
    └── kotlin/dev/flowlang/jetbrains/
        └── FlowLanguageServerFactory.kt  LSP4IJ factory spawning `flow lsp`
```

## References

- LSP4IJ project: https://github.com/redhat-developer/lsp4ij
- LSP4IJ DeveloperGuide: https://github.com/redhat-developer/lsp4ij/blob/main/docs/DeveloperGuide.md
- IntelliJ Platform Gradle Plugin 2.x: https://plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin.html
- Phase 31 plan: `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-08-PLAN.md`
- Phase 31 LSP4IJ research notes: `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-RESEARCH.md`
