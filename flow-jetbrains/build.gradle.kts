// flow-jetbrains/build.gradle.kts
//
// Phase 31 Plan 08 — JetBrains plugin for the Flow language via LSP4IJ.
//
// Per CONTEXT D-09: LSP4IJ pinned to com.redhat.devtools.lsp4ij:0.19.3.
// Per CONTEXT D-10: this build configuration lands UNCONDITIONALLY; the
// produced .zip is the stretch artifact, but the scaffolding is the floor.
//
// JDK toolchain 21 (IntelliJ Platform 2024.2 ships with JBR 21; the platform
// verifier rejects sourceCompatibility lower than 21 for 2024.2+ targets).

plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "1.9.25"
    // Pinned at 2.2.0 (NOT 2.16.0): plugin >= 2.16.0 requires Gradle 9.0+, which
    // this wrapper does not ship. 2.2.0 prints an "outdated" informational warning
    // but executes correctly on Gradle 8.6. v1.5 follow-up: bump wrapper to Gradle
    // 9.x and pin plugin to the matching 2.16+ line. See 31-08-SUMMARY.md.
    id("org.jetbrains.intellij.platform") version "2.2.0"
}

group = "dev.flowlang"
version = "1.5.0"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // IntelliJ Platform baseline — matches LSP4IJ since-build 242.
        intellijIdeaCommunity("2024.2")

        // LSP4IJ — Red Hat's LSP bridge for the IntelliJ Platform.
        // D-09 pinned exact version (no `+` ranges, no `latest`).
        plugin("com.redhat.devtools.lsp4ij:0.19.3")
    }
}

kotlin {
    jvmToolchain(21)
    compilerOptions {
        jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_21)
    }
}

tasks.withType<JavaCompile> {
    options.release.set(21)
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            sinceBuild = "242"
            // Override the IntelliJ Platform Gradle Plugin's auto-computed
            // `untilBuild = "<sinceBuild-major>.*"`. Without this the plugin
            // would be gated at 242.* and rejected by IDEs from 243+ (e.g.
            // PyCharm 2025.3 / build 253).
            //
            // The upper bound is set to the WILDCARD `253.*`: LSP4IJ 0.19.3 is
            // API-stable across IntelliJ Platform 242..253, so the plugin is
            // declared compatible through the 253 branch. When a newer platform
            // ships, bump this ceiling (and re-pin LSP4IJ if it breaks).
            //
            // Phase 41 JET-01 Rule-1 fix: the Phase 31 `provider { "" }` emitted
            // an EMPTY `until-build=""`, which the IntelliJ Plugin Verifier
            // rejects ("attribute with only a branch number () is not valid; use
            // a wildcard like '.*'"). A concrete `253.*` wildcard is the
            // verifier-valid form (plugin 2.2.0 has no `untilBuild.unset()`).
            untilBuild = "253.*"
        }
    }

    // ── JET-01 / Phase 41 D-03 — Marketplace publish staging ──────────────
    //
    // pluginVerification runs AUTONOMOUSLY (`./gradlew verifyPlugin`): the
    // IntelliJ Plugin Verifier checks binary/API compatibility against the
    // recommended IntelliJ Platform IDEs for the declared since/until range.
    //
    // signing + publishing are the HUMAN gate (41-HUMAN-UAT.md row 6). Their
    // secrets come from `providers.environmentVariable(...)` ONLY — never a
    // literal, never committed (threat T-41-06-IDISCLOSE / V14 / V6). The
    // autonomous build possesses no real cert or token, so `signPlugin` /
    // `publishPlugin` are NOT run here; the composer supplies the four env
    // vars at publish time:
    //   CERTIFICATE_CHAIN / PRIVATE_KEY / PRIVATE_KEY_PASSWORD / PUBLISH_TOKEN
    pluginVerification {
        ides {
            recommended()
        }
    }

    signing {
        certificateChain = providers.environmentVariable("CERTIFICATE_CHAIN")
        privateKey = providers.environmentVariable("PRIVATE_KEY")
        password = providers.environmentVariable("PRIVATE_KEY_PASSWORD")
    }

    publishing {
        token = providers.environmentVariable("PUBLISH_TOKEN")
        // channels = listOf("default")  // optional — default = stable Marketplace channel
    }
}
