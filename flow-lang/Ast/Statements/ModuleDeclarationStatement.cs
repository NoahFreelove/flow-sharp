using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Phase 43 D-01 / D-03 — a <c>module &lt;name&gt;</c> top-of-file declaration.
/// Names a <c>.flow</c> file for qualified-access lookups (e.g. <c>math.sin</c>
/// from another file that has imported this one).
///
/// Per CONTEXT D-01, the declaration MUST be the first non-comment statement of
/// the file; line comments (<c>// ...</c>) preceding the declaration are stripped
/// at <see cref="FlowLang.Parsing.Parser.ParseStatement"/> entry and do NOT count
/// as non-comment statements. Files WITHOUT a <c>module</c> declaration continue
/// to parse and execute as before — their procs still export into the unqualified
/// namespace via the existing <c>use "@x"</c> path (back-compat invariant).
///
/// Per CONTEXT D-03, <c>module</c> is the reserved keyword choice (familiar from
/// Haskell / OCaml / F# / Rust). No alternative spellings (<c>mod</c> /
/// <c>namespace</c> / <c>pkg</c>) — Flow stays Anglophone-musical-DSL-style.
///
/// Per CONTEXT D-02, qualified access (<c>math.sin(0.5)</c>) reuses the existing
/// <see cref="FlowLang.Ast.Expressions.MemberAccessExpression"/> AST node. No new
/// expression surface is required; runtime dispatch checks the module registry
/// first inside <see cref="FlowLang.Interpreter.ExpressionEvaluator"/> and falls
/// through to the existing instance-member path on miss (Wave 2 work).
///
/// <see cref="Name"/> follows the standard Flow identifier rule
/// <c>[a-zA-Z_][a-zA-Z0-9_]*</c>; invalid name tokens (numeric literals, etc.)
/// produce a parse error at the keyword's source location.
///
/// Phase 43 Plan 43-01 lands the SYNTACTIC surface only — the registry, runtime
/// dispatch, and stdlib migration ship in subsequent plans (Wave 2+).
/// </summary>
public record ModuleDeclarationStatement(
    SourceLocation Location,
    string Name,
    Span? Span = null) : Statement(Location);
