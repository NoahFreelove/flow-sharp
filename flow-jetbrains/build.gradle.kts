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
version = "0.1.0"

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
            // Disable the IntelliJ Platform Gradle Plugin's auto-computed
            // `untilBuild = "<major>.*"`. Without this override the plugin
            // is gated at 242.* and is rejected by IDEs from 243+ (e.g.
            // PyCharm 2025.3 / build 253). LSP4IJ 0.19.3 is API-stable on
            // IntelliJ Platform 242..253; if a newer platform breaks LSP4IJ
            // itself, bump the LSP4IJ pin in dependencies above.
            untilBuild = provider { "" }
        }
    }
}
