namespace FlowCli.Doc;

// Phase 41 Plan 41-03 DOC-01 — the in-memory model the emitters render.
//
// One DocModel per documented surface entry: a built-in (sourced from
// FlowLang.StandardLibrary.BuiltInDocs.All) OR a composer/stdlib proc (sourced
// from a parsed ProcDeclaration's `///` DocComment — the 41-02 field).
//
// Per CONTEXT D-09 the exact shape is Claude's discretion; this record covers
// the five inputs the HTML/Markdown emitters need: name, a synthesized
// signature, an optional one-line summary, the per-parameter descriptions, the
// runnable example snippets, and (filled in by DocExampleRunner) the failure
// annotations for any example that did not execute cleanly.
//
// `Category` groups entries in the generated nav per the CLAUDE.md "Built-in
// Function Categories" listing; `Source` distinguishes a builtin entry from a
// harvested-proc entry so the content-hash cache + emitters can label origin.

public enum DocSource
{
    Builtin,
    Proc,
}

public sealed record DocParam(string Name, string Description);

public sealed record DocModel(
    string Name,
    string Signature,
    string? Summary,
    IReadOnlyList<DocParam> Params,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> ExampleFailures,
    string Category,
    DocSource Source)
{
    public bool HasFailures => ExampleFailures.Count > 0;

    /// <summary>
    /// Returns a copy with the given example-failure annotations attached.
    /// DocExampleRunner produces the failures, then re-stamps the model so the
    /// collected list and the run results stay a single immutable value.
    /// </summary>
    public DocModel WithFailures(IReadOnlyList<string> failures) =>
        this with { ExampleFailures = failures };
}
