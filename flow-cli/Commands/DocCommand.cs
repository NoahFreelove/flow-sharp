using System.CommandLine;
using FlowCli.Doc;

namespace FlowCli.Commands;

// Phase 41 Plan 41-03 DOC-01/02 — `flow doc [--out dir] [--format html|md|both]
// [--source dir]`.
//
// Generates a browsable static reference from FlowLang.StandardLibrary.BuiltInDocs
// (~104 builtins) + the `///` doc-comments on harvested .flow procs (the 41-02
// ProcDeclaration.DocComment field), executing each `///` example in-process via
// the hermetic DocExampleRunner (DOC-02), and emitting HTML + Markdown under a
// normalized, traversal-confined `--out` (default docs/reference).
//
// Mirrors TestCommand's Build()/SetAction shape (CONTEXT D-06: a flow-cli verb,
// sibling to run/test/repl — not a new project).
//
// --out         output directory (default docs/reference); normalized + confined
//               under the working dir so a traversal-shaped arg cannot escape it
//               (T-41-03-V12).
// --format      html | md | both (default both).
// --source      a directory of .flow files to harvest /// doc-comments from
//               (TopDirectoryOnly, T-35-10 bounded). Repeatable. Defaults to the
//               current directory + the stdlib .flow corpus shipped beside the
//               binary so `flow doc` from a repo root just works.
internal static class DocCommand
{
    public static Command Build()
    {
        var outOpt = new Option<string?>("--out")
        {
            Description = "Output directory (default: docs/reference)",
        };
        var fmtOpt = new Option<string?>("--format")
        {
            Description = "Output format: html | md | both (default: both)",
        };
        var sourceOpt = new Option<string[]>("--source")
        {
            Description = "Directory of .flow files to harvest /// doc-comments from " +
                          "(repeatable; default: current dir + bundled stdlib)",
            AllowMultipleArgumentsPerToken = true,
        };

        var cmd = new Command("doc",
            "Generate browsable reference docs from /// comments + BuiltInDocs");
        cmd.Add(outOpt);
        cmd.Add(fmtOpt);
        cmd.Add(sourceOpt);

        cmd.SetAction(parseResult =>
        {
            var rawOut = parseResult.GetValue(outOpt);
            var format = DocGenerator.ParseFormat(parseResult.GetValue(fmtOpt));
            var userSources = parseResult.GetValue(sourceOpt) ?? Array.Empty<string>();

            var sources = ResolveSources(userSources);

            try
            {
                var gen = new DocGenerator();
                var result = gen.Generate(rawOut, format, sources);

                Console.WriteLine($"Generated {result.EntryCount} entries → {result.OutDir}");
                if (result.HtmlPath is not null)
                    Console.WriteLine($"  HTML:     {result.HtmlPath}");
                if (result.MarkdownPath is not null)
                    Console.WriteLine($"  Markdown: {result.MarkdownPath}");
                Console.WriteLine(
                    $"  Cache:    {result.Cache.Regenerated} regenerated, {result.Cache.Skipped} unchanged");
                if (result.ExampleFailureCount > 0)
                    Console.WriteLine($"  Examples: {result.ExampleFailureCount} [example failed]");

                // A failing /// example is a regression signal (DOC-02 — examples
                // double as tests) but does NOT fail the generation: the failure
                // is annotated in the output. Exit 0 so doc-gen stays a routine
                // build step; the annotation surfaces the regression in the docs.
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: flow doc failed: {ex.Message}");
                return 1;
            }
        });
        return cmd;
    }

    /// <summary>
    /// Build the harvest source list: any user-supplied --source dirs, else the
    /// current directory plus the stdlib .flow corpus shipped beside the binary.
    /// </summary>
    private static IEnumerable<string> ResolveSources(string[] userSources)
    {
        if (userSources.Length > 0)
            return userSources;

        var sources = new List<string> { Directory.GetCurrentDirectory() };
        // The bundled stdlib .flow files are copied beside the binary (the same
        // AppContext.BaseDirectory ModuleLoader resolves stdlib modules from).
        var stdlibDir = AppContext.BaseDirectory;
        if (Directory.Exists(stdlibDir))
            sources.Add(stdlibDir);
        return sources;
    }
}
