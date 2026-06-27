using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.StandardLibrary;

namespace FlowCli.Doc;

// Phase 41 Plan 41-03 DOC-01 — collects DocModel entries from the two content
// sources locked by CONTEXT D-08:
//
//   (a) FlowLang.StandardLibrary.BuiltInDocs.All — the ~104 builtin metadata
//       entries (Summary + per-param descriptions). No examples (BuiltInDocs
//       carries none). Signature is synthesized from the param list.
//
//   (b) The `///` doc-comments on parsed proc declarations. The collector
//       lexes+parses each harvested .flow file (the SAME SimpleLexer + Parser
//       FlowEngine.Execute uses, but WITHOUT interpreting — we only walk the
//       AST), reads ProcDeclaration.DocComment (the 41-02 field), and extracts
//       the one-line summary + any fenced ``` example blocks from it.
//
// Charitable interpretation (D-07): a proc with no `///` still produces a
// signature-only entry (Summary == null), never an error. A .flow file that
// fails to lex/parse is skipped with its errors swallowed (the generator never
// crashes on malformed corpus — T-41-03-V5).
//
// Harvest is bounded exactly like TestCommand (T-35-10 precedent):
// Directory.GetFiles(dir, "*.flow", SearchOption.TopDirectoryOnly) — no
// recursion, name-pattern filtered — so a user-supplied source dir cannot walk
// the tree and expose arbitrary files.
public sealed class DocCollector
{
    /// <summary>
    /// Collect builtin entries plus proc entries harvested from <paramref name="flowSourceDirs"/>.
    /// Pass an empty list to collect builtins only (the zero-corpus charitable
    /// path still yields a full builtin reference).
    /// </summary>
    public DocModel[] Collect(IEnumerable<string>? flowSourceDirs = null)
    {
        var models = new List<DocModel>();

        // (a) Builtins from BuiltInDocs.All.
        foreach (var kvp in BuiltInDocs.All)
        {
            var name = kvp.Key;
            var doc = kvp.Value;
            var paramDocs = doc.Params
                .Select(p => new DocParam(p.Name, p.Description))
                .ToList();
            var signature = SynthesizeBuiltinSignature(name, paramDocs);
            models.Add(new DocModel(
                Name: name,
                Signature: signature,
                Summary: doc.Summary,
                Params: paramDocs,
                Examples: Array.Empty<string>(),
                ExampleFailures: Array.Empty<string>(),
                Category: ClassifyCategory(name),
                Source: DocSource.Builtin));
        }

        // (b) Procs from harvested .flow files.
        if (flowSourceDirs is not null)
        {
            // De-dup the proc keys so a proc that shadows a builtin name (or a
            // proc declared in two harvested dirs) does not double-list.
            var seenProcKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dir in flowSourceDirs)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, "*.flow", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    // Unreadable dir — charitable skip.
                    continue;
                }
                Array.Sort(files, StringComparer.Ordinal);

                foreach (var file in files)
                {
                    foreach (var proc in ParseProcs(file))
                    {
                        var sig = SynthesizeProcSignature(proc);
                        var key = sig + "␟" + Path.GetFileName(file);
                        if (!seenProcKeys.Add(key))
                            continue;

                        var (summary, examples) = ParseDocComment(proc.DocComment);
                        models.Add(new DocModel(
                            Name: proc.Name,
                            Signature: sig,
                            Summary: summary,
                            Params: proc.Parameters
                                .Select(p => new DocParam(p.Name, ""))
                                .ToList(),
                            Examples: examples,
                            ExampleFailures: Array.Empty<string>(),
                            Category: ClassifyCategory(proc.Name),
                            Source: DocSource.Proc));
                    }
                }
            }
        }

        // Deterministic ordering: category, then name, then source (builtin
        // before proc). Stable diffable output is a D-09 requirement.
        models.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Category, b.Category);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Name, b.Name);
            if (c != 0) return c;
            return a.Source.CompareTo(b.Source);
        });
        return models.ToArray();
    }

    /// <summary>
    /// Lex + parse a .flow file and return its top-level ProcDeclarations.
    /// Reuses the exact lex/parse path of FlowEngine.Execute but stops before
    /// interpretation — we only need the AST. Errors are swallowed (charitable).
    /// </summary>
    private static IReadOnlyList<ProcDeclaration> ParseProcs(string file)
    {
        string source;
        try
        {
            source = File.ReadAllText(file);
        }
        catch
        {
            return Array.Empty<ProcDeclaration>();
        }

        var errors = new ErrorReporter();
        try
        {
            var (pragmaSet, transformed) = PragmaScanner.Scan(source, file, errors);
            if (errors.HasErrors) return Array.Empty<ProcDeclaration>();

            var lexer = new SimpleLexer(transformed, errors, file, pragmaSet);
            var tokens = lexer.Tokenize();
            if (errors.HasErrors) return Array.Empty<ProcDeclaration>();

            var parser = new Parser(tokens, errors, pragmaSet);
            var program = parser.Parse();
            if (errors.HasErrors) return Array.Empty<ProcDeclaration>();

            return program.Statements
                .OfType<ProcDeclaration>()
                .ToList();
        }
        catch
        {
            // Any lexer/parser exception on malformed corpus → charitable skip.
            return Array.Empty<ProcDeclaration>();
        }
    }

    /// <summary>
    /// Split a captured `///` doc-comment into a one-line summary and a list of
    /// fenced example blocks. Convention: the first non-fence text line(s) up to
    /// the first ``` fence form the summary; each ```-delimited block is one
    /// example. A doc-comment with no fences is summary-only.
    /// </summary>
    public static (string? Summary, IReadOnlyList<string> Examples) ParseDocComment(string? docComment)
    {
        if (string.IsNullOrWhiteSpace(docComment))
            return (null, Array.Empty<string>());

        var lines = docComment.Replace("\r\n", "\n").Split('\n');
        var summaryLines = new List<string>();
        var examples = new List<string>();
        var current = new List<string>();
        bool inFence = false;

        foreach (var raw in lines)
        {
            var line = raw;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (!inFence)
                {
                    inFence = true;
                    current.Clear();
                }
                else
                {
                    inFence = false;
                    var body = string.Join("\n", current).Trim('\n');
                    if (body.Length > 0)
                        examples.Add(body);
                    current.Clear();
                }
                continue;
            }

            if (inFence)
            {
                current.Add(line);
            }
            else if (examples.Count == 0)
            {
                // Pre-fence prose is the summary.
                summaryLines.Add(line.Trim());
            }
        }

        // Unterminated fence — treat the captured body as an example anyway
        // (charitable: a composer's open ``` shouldn't drop their example).
        if (inFence && current.Count > 0)
        {
            var body = string.Join("\n", current).Trim('\n');
            if (body.Length > 0)
                examples.Add(body);
        }

        var summary = string.Join(" ", summaryLines.Where(s => s.Length > 0)).Trim();
        return (summary.Length == 0 ? null : summary, examples);
    }

    private static string SynthesizeBuiltinSignature(string name, IReadOnlyList<DocParam> paramDocs)
    {
        if (paramDocs.Count == 0)
            return $"({name})";
        var ps = string.Join(" ", paramDocs.Select(p => p.Name));
        return $"({name} {ps})";
    }

    private static string SynthesizeProcSignature(ProcDeclaration proc)
    {
        if (proc.Parameters.Count == 0)
            return $"({proc.Name})";
        var ps = string.Join(" ", proc.Parameters.Select(p =>
        {
            var typeName = p.Type?.Name ?? "Void";
            var star = p.IsVarArgs ? "..." : "";
            return $"{typeName}{star}: {p.Name}";
        }));
        return $"({proc.Name} {ps})";
    }

    /// <summary>
    /// Map a function name to a CLAUDE.md "Built-in Function Categories" bucket.
    /// Prefix/keyword heuristic — the categories are comment-delimited in
    /// BuiltInDocs.cs (not retrievable at runtime), so this self-contained
    /// classifier mirrors that grouping. Unknown names fall to "Other".
    /// </summary>
    public static string ClassifyCategory(string name)
    {
        // Order matters — first match wins.
        if (Is(name, "print", "input", "str", "read", "write")) return "I/O";
        if (Is(name, "add", "sub", "mul", "div", "neg", "idiv", "mod", "abs", "min", "max", "pow", "sqrt", "round", "floor", "ceil")) return "Arithmetic";
        if (Is(name, "and", "or", "not", "if", "equals", "eq", "gt", "lt", "gte", "lte", "compare")) return "Logic & Comparison";
        if (Is(name, "list", "head", "tail", "last", "init", "empty", "reverse", "take", "drop",
                "append", "prepend", "concat", "contains", "map", "filter", "reduce", "each",
                "length", "len", "range", "zip", "slice", "fold", "sort")) return "Collections";
        if (Is(name, "dict", "get", "getOr", "set", "remove", "has", "keys", "values", "size", "merge")) return "Dictionaries";
        if (Is(name, "random", "choose", "euclidean", "markov", "lsystem", "cellular", "life",
                "lorenz", "logistic", "quantizeToScale", "jam", "registerStyle", "listStyles")) return "Generative";
        if (Is(name, "buffer", "silence", "noise", "adsr", "applyEnvelope") ||
            name.StartsWith("create", StringComparison.Ordinal)) return "Audio core";
        if (Is(name, "reverb", "lowpass", "highpass", "bandpass", "compress", "sidechain",
                "delay", "gain", "volume", "granular", "stretch", "pitchShift", "pan")) return "Audio effects";
        if (Is(name, "play", "loop", "preview", "stop", "audioDevices", "setAudioDevice",
                "isAudioAvailable", "micBuffer")) return "Playback & Input";
        if (Is(name, "chordNotes", "chordRoot", "chordQuality", "arpeggio", "scaleNotes",
                "resolveNumeral")) return "Harmony";
        if (Is(name, "transpose", "invert", "retrograde", "augment", "diminish", "up", "down",
                "repeat", "legato")) return "Transforms";
        if (Is(name, "musicalNote", "rest", "bar", "sequence", "renderSequence", "renderSequences",
                "renderSong", "getSections", "sectionSequences", "writeWav", "writeMidi",
                "writeMusicXML", "writeLilyPond", "abc", "mml")) return "Notation & Rendering";
        if (Is(name, "midiPorts", "openMidiOutput", "midiOut", "midiNoteOn", "midiNoteOff",
                "midiCC", "midiSysex", "clockMaster", "clockSlave", "clockStop",
                "oscSend", "oscListen", "oscStop", "oscBundle", "oscSendBundle",
                "jackSync") || name.StartsWith("midi", StringComparison.Ordinal)) return "MIDI & Network";
        if (Is(name, "inspect", "visualize")) return "Visualization";
        return "Other";
    }

    private static bool Is(string name, params string[] candidates) =>
        candidates.Contains(name, StringComparer.Ordinal);
}
