package dev.flowlang.jetbrains

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.project.Project
import com.redhat.devtools.lsp4ij.LanguageServerFactory
import com.redhat.devtools.lsp4ij.server.OSProcessStreamConnectionProvider
import com.redhat.devtools.lsp4ij.server.StreamConnectionProvider

/**
 * Spawns the Flow language server for an IntelliJ Platform project.
 *
 * Wires LSP4IJ to the `flow lsp` subcommand (added by Phase 31 Plan 01's
 * [LspCommand](../../../../../../../flow-cli/Commands/LspCommand.cs)), which
 * delegates to `flow-lsp/Program.cs` for the OmniSharp `LanguageServer.From(...)`
 * stdio wiring.
 *
 * Binary discoverability per Phase 31 RESEARCH Pitfall 7:
 *  - Primary: `flow` on PATH (provided by Phase 30's `flow install`).
 *  - Fallback: `FLOW_LSP_PATH` environment variable, when `flow` is not on PATH
 *    (set before launching IntelliJ; see flow-jetbrains/README.md).
 *
 * The Phase 31 SPEC-7 stretch bar is "builds + opens .flow with completions" —
 * this factory is the LSP4IJ entry point that drives that demo. Per CONTEXT
 * D-10 this scaffolding lands UNCONDITIONALLY at phase closure even if the
 * stretch demo defers to v1.5.
 */
class FlowLanguageServerFactory : LanguageServerFactory {
    override fun createConnectionProvider(project: Project): StreamConnectionProvider {
        val flowPath = System.getenv("FLOW_LSP_PATH") ?: "flow"
        val cmd = GeneralCommandLine(flowPath, "lsp")
        return object : OSProcessStreamConnectionProvider() {
            init {
                commandLine = cmd
            }
        }
    }
}
