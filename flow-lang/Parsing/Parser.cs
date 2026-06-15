using FlowLang.Ast;
using FlowLang.Ast.Elements;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Parsing;

/// <summary>
/// Recursive descent parser for the Flow language.
/// </summary>
public partial class Parser
{
    private readonly List<Token> _tokens;
    private readonly ErrorReporter _errorReporter;
    private readonly PragmaSet _pragmaSet;
    private int _current = 0;
    // When true, disables the "identifier followed by literal = function call"
    // heuristic in ParsePrimary. Set while parsing arguments inside (func arg1 arg2).
    private bool _inFuncCallArgs = false;
    private bool _inLoop = false;
    // Phase 43 D-01 — set true after the first non-module non-comment statement
    // is appended to the Program's Statements list inside Parse(). A subsequent
    // `module <name>` keyword in ParseStatement reports a position-constraint
    // error instead of dispatching to ParseModuleDeclaration. Comments never
    // reach the flag-flip site because ParseStatement returns null for them.
    private bool _seenNonModuleNonCommentStatement = false;

    // Phase 41 (DOC-01, D-07): the text of the most-recent `///` doc-comment block
    // (from a TokenType.DocComment token) awaiting binding to the following proc.
    // Buffered in ParseStatement, consumed + cleared at ParseProcDeclaration entry,
    // and cleared (charitable orphan-drop, Pitfall 2) when any non-proc statement
    // dispatches. Null when no `///` is pending.
    private string? _pendingDocComment = null;

    // Bounds for syntax tree depth
    private int _parseDepth = 0;
    private const int MaxParseDepth = 500;

    public Parser(List<Token> tokens, ErrorReporter errorReporter, PragmaSet? pragmaSet = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _pragmaSet = pragmaSet ?? PragmaSet.Empty;
    }

    /// <summary>
    /// Parses the token stream into a Program AST.
    /// </summary>
    public Program Parse()
    {
        var statements = new List<Statement>();

        while (!IsAtEnd())
        {
            try
            {
                // Skip optional semicolons at statement level
                while (Match(TokenType.Semicolon))
                    ; // Consume extra semicolons

                if (IsAtEnd())
                    break;

                var stmt = ParseStatement();
                if (stmt != null)
                {
                    statements.Add(stmt);
                    // Phase 43 D-01 — flip the position-constraint flag after every
                    // non-module statement so any subsequent `module <name>` keyword
                    // reports the "must be first non-comment statement" error from
                    // ParseStatement. Comments are filtered out earlier (ParseStatement
                    // returns null on TokenType.Comment) and never reach this point.
                    if (stmt is not ModuleDeclarationStatement)
                        _seenNonModuleNonCommentStatement = true;
                }

                // Optionally consume trailing semicolon after statement
                Match(TokenType.Semicolon);
            }
            catch (ParseException ex)
            {
                _errorReporter.ReportError(ex.Message, CurrentToken.Location);
                Synchronize();
            }
        }

        return new Program(SourceLocation.Unknown, statements, _pragmaSet);
    }

    private Statement? ParseStatement()
    {
        _parseDepth++;
        if (_parseDepth > MaxParseDepth)
            throw new ParseException($"Maximum nested parse depth of {MaxParseDepth} reached.");

        try
        {
            // Skip comments
            if (Match(TokenType.Comment))
                return null;

            // Phase 41 (DOC-01, D-07): buffer a `///` doc-comment so the NEXT
            // statement-parse can bind it. The token is out-of-band (returns null
            // like a plain comment) — only ParseProcDeclaration consumes the buffer.
            // Contiguous `///` lines already arrive as ONE DocComment token from the
            // lexer; a later DocComment token simply overwrites the buffer (last
            // block wins). We do NOT flip `_seenNonModuleNonCommentStatement` here
            // (doc-comments, like plain comments, are not "real" statements).
            if (Check(TokenType.DocComment))
            {
                _pendingDocComment = Advance().Text;
                return null;
            }

            // Phase 41 (DOC-01, D-07, Pitfall 2): charitable orphan-drop. If a `///`
            // is pending but the upcoming statement is NOT a proc declaration, the
            // doc-comment has nothing to bind to — drop it silently (never an error).
            // proc / `internal proc` consume the buffer in ParseProcDeclaration; any
            // other dispatch path below would leak it to a later proc, so clear now.
            if (_pendingDocComment != null
                && !Check(TokenType.Proc) && !Check(TokenType.Internal))
            {
                _pendingDocComment = null;
            }

            // Phase 43 D-01 — `module <name>` top-of-file declaration.
            // Must be the first non-comment statement of the file; mid-file
            // `module` keywords are reported as parse errors at the keyword's
            // source location (the flag is flipped in Parse() after each
            // non-module, non-comment statement is appended to the Program).
            if (Check(TokenType.Module))
            {
                if (_seenNonModuleNonCommentStatement)
                {
                    var badLoc = CurrentToken.Location;
                    _errorReporter.ReportError(
                        "module declaration must be the first non-comment statement of the file",
                        badLoc);
                    // Consume the `module` keyword + (optional) name token so the
                    // parser advances past the bad declaration and keeps reporting
                    // useful errors on subsequent statements rather than looping
                    // on the same position-constraint error.
                    Advance();
                    if (Check(TokenType.Identifier))
                        Advance();
                    return null;
                }
                Advance(); // consume `module`
                return ParseModuleDeclaration();
            }

            if (Match(TokenType.Proc))
                return ParseProcDeclaration(false);

            if (Match(TokenType.Internal))
            {
                Expect(TokenType.Proc, "Expected 'proc' after 'internal'");
                return ParseProcDeclaration(true);
            }

            if (Match(TokenType.Return))
                return ParseReturnStatement();

            if (Match(TokenType.Use))
                return ParseImportStatement();

            // Musical context blocks
            if (Match(TokenType.Timesig))
                return ParseMusicalContextStatement(MusicalContextType.Timesig);
            if (Match(TokenType.Tempo))
                return ParseMusicalContextStatement(MusicalContextType.Tempo);
            if (Match(TokenType.Swing))
                return ParseMusicalContextStatement(MusicalContextType.Swing);
            if (Match(TokenType.Key))
                return ParseMusicalContextStatement(MusicalContextType.Key);
            if (Match(TokenType.Dynamics))
                return ParseMusicalContextStatement(MusicalContextType.Dynamics);
            if (Match(TokenType.Rit))
                return ParseMusicalContextStatement(MusicalContextType.Rit);
            if (Match(TokenType.Accel))
                return ParseMusicalContextStatement(MusicalContextType.Accel);
            // Only parse `pan` as a context block when followed by a numeric literal or sign
            // (e.g., `pan 0.5 { ... }` or `pan -1.0 { ... }`), not when used as a variable name.
            if (Check(TokenType.Pan) && _current + 1 < _tokens.Count
                && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
                    or TokenType.Minus or TokenType.Plus))
            {
                Advance(); // consume `pan`
                return ParseMusicalContextStatement(MusicalContextType.Pan);
            }
            // Only parse `gain` as a context block when followed by a numeric literal or sign
            // (e.g., `gain 0.5 { ... }`), not when used as a function name.
            if (Check(TokenType.Gain) && _current + 1 < _tokens.Count
                && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
                    or TokenType.Minus or TokenType.Plus))
            {
                Advance(); // consume `gain`
                return ParseMusicalContextStatement(MusicalContextType.Gain);
            }
            // Only parse `reverbTime` as a context block when followed by a numeric literal or sign
            // (e.g., `reverbTime 2.5 { ... }`). Per D-03, negative rejection happens INSIDE the case body
            // so the error points at the '-' rather than at '{'.
            if (Check(TokenType.ReverbTime) && _current + 1 < _tokens.Count
                && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
                    or TokenType.Minus or TokenType.Plus))
            {
                Advance(); // consume `reverbTime`
                return ParseMusicalContextStatement(MusicalContextType.ReverbTime);
            }
            // Phase 28 SPEC-7: voicePool N { ... } context block. Integer N only (no float).
            // Range validation (1..256) happens at the interpreter so the error points
            // at the offending value, not at the '{'.
            if (Check(TokenType.VoicePool) && _current + 1 < _tokens.Count
                && _tokens[_current + 1].Type is TokenType.IntLiteral)
            {
                Advance(); // consume `voicePool`
                return ParseMusicalContextStatement(MusicalContextType.VoicePool);
            }
            // sustainPedal { ... } — no value, just the block. Notes inside this block
            // render with extended duration so they ring like a piano with the sustain
            // pedal held down. Nests with other context blocks.
            if (Match(TokenType.SustainPedal))
            {
                return ParseMusicalContextStatement(MusicalContextType.SustainPedal);
            }

            // Phase 32 D-13: `tuning <expr> { ... }` musical-context block. Unlike the
            // other musical-context keywords, `tuning` is FULLY RESERVED per CONTEXT
            // Claude's Discretion (SPEC line 139 pre-public lean — not added to the
            // keyword-as-proc-name allowlist at line 247). The three D-15 expression
            // forms (identifier, inline call, string-literal sugar) all dispatch to
            // ParseTuningContextStatement.
            if (Match(TokenType.Tuning))
                return ParseTuningContextStatement();

            // Phase 38 D-38-02 LIVE-01: `live <quantize> { ... }` block. Quantize
            // accepts Int + optional `bar`/`bars` suffix, a NoteValue identifier
            // (`q`/`h`/`w`/`e`/`s`), or is omitted entirely (defaults to 1bar).
            // BlockId is FNV-1a of the keyword's SourceLocation so the runtime
            // LiveBlockRegistry slot survives re-renders per D-38-02 independent
            // multi-block swap.
            if (Match(TokenType.Live))
            {
                // Phase 47 D-47-09: live blocks require FileSystemWatcher (browser-
                // unavailable). Parse-time error rather than runtime advisory because
                // `live { ... }` is block syntax, not a builtin invocation — composer
                // needs a Rust-style diagnostic pointing at the source line.
                // FlowEngine.SupportsLiveBlocks is false ONLY when FLOW_WEB was defined
                // at compile time; on Desktop the property is a compile-time `true`
                // and this branch is dead code (Roslyn constant-fold).
                //
                // LiveBlockStatement.cs + LiveBlockRegistry.cs STAY in the Web build
                // per Plan 47-01 strip-list — the AST types remain referenceable
                // (Interpreter.cs:133 case-dispatch + ExecutionContext.cs:292 property)
                // but the parse-time throw prevents instances from ever being
                // constructed under Web target.
                if (!Core.FlowEngine.SupportsLiveBlocks)
                {
                    var liveTok = PreviousToken;
                    throw new ParseException(
                        $"`live` block requires Desktop target — line {liveTok.Location.Line}. " +
                        $"Build with FlowTarget=Desktop or run with `flow run script.flow` locally.");
                }
                return ParseLiveBlockStatement();
            }

            // Section declaration: section name { ... }
            if (Match(TokenType.Section))
                return ParseSectionDeclaration();

            // Loop constructs
            if (Match(TokenType.For))
                return ParseForStatement();
            if (Match(TokenType.While))
                return ParseWhileStatement();
            if (Match(TokenType.Break))
            {
                if (!_inLoop)
                    throw new ParseException("'break' can only be used inside a loop");
                var breakLoc = PreviousToken.Location;
                return new BreakStatement(breakLoc, Span: Span.At(breakLoc));
            }
            if (Match(TokenType.Continue))
            {
                if (!_inLoop)
                    throw new ParseException("'continue' can only be used inside a loop");
                var contLoc = PreviousToken.Location;
                return new ContinueStatement(contLoc, Span: Span.At(contLoc));
            }

            // Phase 26.1 TUP-09: Tuple destructuring assignment statement
            // `<<Type? name, Type? name, ...>> = expr`. Must come BEFORE the IsTypeKeyword
            // check because `<<` is not a type-keyword token but is the only
            // statement-start position where LessLess can occur.
            //
            // sweep-0614: a statement can ALSO begin with a tuple LITERAL —
            // `<<3, 4>> ~> add` (the headline `~>` tuple-unpack form) or a bare
            // `<<1, 2>>`. Disambiguate by scanning to the matching `>>` and only
            // committing to the destructure grammar when the token immediately
            // AFTER it is `=`. Otherwise fall through to the expression-statement
            // path so ParsePrimary builds a TupleLiteralExpression.
            if (Check(TokenType.LessLess) && IsTupleDestructureTarget())
            {
                return ParseTupleDestructureStatement();
            }

            // Check for variable declaration: Type identifier =
            if (IsTypeKeyword(CurrentToken.Type))
            {
                return ParseVariableDeclaration();
            }

            // Check for assignment (identifier followed by =)
            if (Check(TokenType.Identifier))
            {
                // Look ahead to distinguish assignment from expression
                int savedPos = _current;
                Advance(); // Skip identifier

                if (Check(TokenType.Assign))
                {
                    // It's an assignment: reset and parse
                    _current = savedPos;
                    return ParseAssignment();
                }

                // Not assignment - reset and parse as expression
                _current = savedPos;
            }

            // Phase 26 D-15: detect stray arithmetic operators at statement-start.
            // After Phase 26 there are no infix operators, so a leading +/-/*// at
            // statement boundary indicates legacy infix that was abandoned mid-rewrite.
            // ParseUnaryShorthand would otherwise silently absorb the '+' (D-03) or
            // emit (neg IDENT) for `-x`, which masks `1 + 2` style legacy code as
            // a no-op success. Generic 'unexpected token' is the contract per D-15.
            if (Check(TokenType.Star) || Check(TokenType.Slash))
            {
                throw new ParseException(
                    $"Unexpected token '{CurrentToken.Text}' at {CurrentToken.Location} — Phase 26 removed infix arithmetic; use prefix builtins (add)/(sub)/(mul)/(div).");
            }
            // For Plus/Minus, only error when not followed by an identifier (D-01 shorthand)
            // and not followed by a digit (already handled by lexer). The remaining cases
            // are stray operators between value-producing tokens.
            if ((Check(TokenType.Plus) || Check(TokenType.Minus))
                && _current + 1 < _tokens.Count
                && _tokens[_current + 1].Type != TokenType.Identifier)
            {
                throw new ParseException(
                    $"Unexpected token '{CurrentToken.Text}' at {CurrentToken.Location} — Phase 26 removed infix arithmetic; use prefix builtins (add)/(sub)/(mul)/(div).");
            }

            // Expression statement
            var expr = ParseExpression();
            return new ExpressionStatement(expr.Location, expr, Span: new Span(expr.Location, PreviousToken.Location));
        }
        finally
        {
            _parseDepth--;
        }
    }

    private ProcDeclaration ParseProcDeclaration(bool isInternal)
    {
        var location = PreviousToken.Location;

        // Phase 41 (DOC-01, D-07): consume the pending `///` doc-comment (if any)
        // and clear the buffer IMMEDIATELY — before parsing the body — so a `///`
        // inside the body (or a following proc) can never re-read this one
        // (Pitfall 2). Null when this proc has no `///` (charitable signature-only).
        string? docComment = _pendingDocComment;
        _pendingDocComment = null;
        // Allow musical context keywords (like 'pan') as procedure names.
        // break-control (0615): also allow the loop-control keyword tokens so the
        // `internal proc break ()` / `internal proc continue ()` surface declarations
        // in std.flow parse — these bind the prefix-only `(break)` / `(continue)`
        // builtins (registered in BuiltInFunctions.RegisterIterationGuard) into the
        // global frame. The keyword tokens carry their literal text ("break"/"continue").
        string name;
        if (Check(TokenType.Identifier) || Check(TokenType.Pan) || Check(TokenType.Gain)
            || Check(TokenType.Tempo) || Check(TokenType.Swing) || Check(TokenType.Key)
            || Check(TokenType.Timesig) || Check(TokenType.Break) || Check(TokenType.Continue))
        {
            name = Advance().Text;
        }
        else
        {
            name = Expect(TokenType.Identifier, "Expected procedure name").Text;
        }

        var parameters = new List<Parameter>();
        Expect(TokenType.LParen, "Expected '(' after procedure name");

        // Parse parameters: Ints: name (plural varargs) or Type...: name (ellipsis varargs) or Type: name
        while (!Check(TokenType.RParen) && !IsAtEnd())
        {
            var (paramType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
            _current = nextIndex;

            // Check for ellipsis varargs (...) after type (alternative syntax)
            if (Check(TokenType.Ellipsis))
            {
                Advance();
                isVarArgs = true;
            }

            Expect(TokenType.Colon, "Expected ':' after parameter type");
            var paramName = ExpectParameterName().Text;

            parameters.Add(new Parameter(paramName, paramType, isVarArgs));

            if (!Check(TokenType.RParen))
                Expect(TokenType.Comma, "Expected ',' between parameters");
        }

        Expect(TokenType.RParen, "Expected ')' after parameters");

        // Parse body statements until "end proc" or "end"
        var body = new List<Statement>();

        if (!isInternal)
        {
            while (!Check(TokenType.EndProc) && !Check(TokenType.Eof))
            {
                // Skip optional semicolons at statement level
                while (Match(TokenType.Semicolon))
                    ; // Consume extra semicolons

                if (Check(TokenType.EndProc) || Check(TokenType.Eof))
                    break;

                var stmt = ParseStatement();
                if (stmt != null)
                    body.Add(stmt);

                // Optionally consume trailing semicolon after statement
                Match(TokenType.Semicolon);
            }

            Expect(TokenType.EndProc, "Expected 'end' after procedure body");

            // Optionally consume "proc" after "end"
            if (Check(TokenType.Proc))
                Advance();
        }

        // Phase 44 Plan 44-02 D-02 / D-03 — capture the declaring file's
        // `enable strict;` bit onto every ProcDeclaration at parse time.
        // Mirrors the Phase 35 LANG-04 `CapturedPragmas: _pragmaSet` threading
        // at line 1794 (MatchExpression). Plan 44-02 threads only the boolean
        // `.Has("strict")` evaluation (smaller surface than capturing the full
        // PragmaSet — no nullable handling at the Interpreter read site).
        return new ProcDeclaration(
            location, name, parameters, body, isInternal,
            Span: new Span(location, PreviousToken.Location),
            IsStrict: _pragmaSet?.Has("strict") ?? false,
            // Phase 45 Plan 45-06 D-04 — capture the declaring file's
            // `enable beat-true-to-sig;` bit onto every ProcDeclaration at parse
            // time, mirroring IsStrict above. The Interpreter pushes/pops this
            // around the proc body so a (beat N) call inside the proc reads the
            // DECLARING file's pragma bit, not the caller's (Pitfall 3 / cross-file).
            IsBeatTrueToSig: _pragmaSet?.Has("beat-true-to-sig") ?? false,
            // Phase 41 Plan 41-02 DOC-01 / D-07 — the `///` doc-comment captured
            // at entry (above), or null for a charitable signature-only entry.
            DocComment: docComment);
    }

    private VariableDeclaration ParseVariableDeclaration()
    {
        var (varType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
        _current = nextIndex;

        // If plural form was used (e.g., "Ints"), treat it as array type in variable declarations
        if (isVarArgs)
        {
            varType = new ArrayType(varType);
        }

        var name = Expect(TokenType.Identifier, "Expected variable name").Text;
        var location = PreviousToken.Location;

        Expression value;

        // Check if there's an initializer
        if (Match(TokenType.Assign))
        {
            // Special case: Song type with [section1 section2*N ...] arrangement syntax
            if (varType is SongType && Check(TokenType.LBracket))
            {
                value = ParseSongExpression();
            }
            else
            {
                value = ParseExpression();
            }
        }
        else
        {
            // No initializer - create default value based on type
            value = CreateDefaultValueExpression(varType, location);
        }

        return new VariableDeclaration(value.Location, varType, name, value, Span: new Span(location, PreviousToken.Location));
    }

    /// <summary>
    /// sweep-0614: non-consuming lookahead that decides whether a statement-start
    /// <c>&lt;&lt;</c> opens a tuple-destructure target (<c>&lt;&lt;...&gt;&gt; = expr</c>)
    /// or a tuple LITERAL used as an expression (e.g. <c>&lt;&lt;3, 4&gt;&gt; ~&gt; add</c>
    /// or a bare <c>&lt;&lt;1, 2&gt;&gt;</c>). Scans from the current <c>&lt;&lt;</c> to its
    /// matching <c>&gt;&gt;</c> (depth-counting nested tuple literals) and returns true only
    /// when the token immediately after the matching <c>&gt;&gt;</c> is <c>=</c>.
    /// </summary>
    private bool IsTupleDestructureTarget()
    {
        // _current points at the opening LessLess.
        int depth = 0;
        for (int i = _current; i < _tokens.Count; i++)
        {
            var t = _tokens[i].Type;
            if (t == TokenType.LessLess)
            {
                depth++;
            }
            else if (t == TokenType.GreaterGreater)
            {
                depth--;
                if (depth == 0)
                {
                    // Token immediately after the matching `>>`.
                    int next = i + 1;
                    return next < _tokens.Count && _tokens[next].Type == TokenType.Assign;
                }
            }
            else if (t == TokenType.Eof)
            {
                break;
            }
        }
        // Unbalanced `<<` — let ParseTupleDestructureStatement produce the
        // existing diagnostic rather than silently swallowing it.
        return true;
    }

    /// <summary>
    /// Phase 26.1 TUP-09: parses <c>&lt;&lt;Type? name, Type? name, ...&gt;&gt; = expr</c>
    /// destructuring assignment. Each slot supports an optional type annotation followed
    /// by an identifier name (CONTEXT § Specifics block 2 — composers can use bare names
    /// when the RHS type is known).
    /// </summary>
    private TupleDestructureStatement ParseTupleDestructureStatement()
    {
        Expect(TokenType.LessLess, "Expected '<<' to start destructure pattern");
        var location = PreviousToken.Location;
        var patterns = new List<TupleDestructurePattern>();
        while (!Check(TokenType.GreaterGreater) && !IsAtEnd())
        {
            FlowType? slotType = null;
            // Optional per-slot type annotation. IsTypeKeyword honors the same allowlist
            // as ParseVariableDeclaration, so any annotation accepted there works here.
            if (IsTypeKeyword(CurrentToken.Type))
            {
                var (parsedType, nextIdx, _) = TypeParser.ParseType(_tokens, _current);
                slotType = parsedType;
                _current = nextIdx;
            }
            if (CurrentToken.Type != TokenType.Identifier)
                throw new ParseException(
                    $"Expected identifier in destructure pattern at {CurrentToken.Location}");
            var name = Advance().Text;
            patterns.Add(new TupleDestructurePattern(slotType, name));
            if (!Check(TokenType.GreaterGreater) && Check(TokenType.Comma))
                Advance();
        }
        Expect(TokenType.GreaterGreater, "Expected '>>' after destructure pattern");
        Expect(TokenType.Assign, "Expected '=' after destructure pattern");
        var value = ParseExpression();
        return new TupleDestructureStatement(location, patterns, value, Span: new Span(location, PreviousToken.Location));
    }

    private Expression CreateDefaultValueExpression(FlowType type, SourceLocation location)
    {
        object defaultValue = type switch
        {
            IntType => 0,
            FloatType => 0.0,
            LongType => 0L,
            DoubleType => 0.0,
            StringType => "",
            BoolType => false,
            NumberType => System.Numerics.BigInteger.Zero,
            VoidType => null!,
            ArrayType => null!, // Will be handled specially
            _ => null! // For special types like Buffer, Note, etc.
        };

        // Handle array types - create empty array expression via list() call
        if (type is ArrayType arrayType)
        {
            // Create a call to list() with no arguments. Synthetic — zero-width Span.
            return new FunctionCallExpression(location, "list", new List<Expression>(), Span: Span.At(location));
        }

        // For null default values (Buffer, custom types), use null literal
        if (defaultValue == null)
        {
            // Return a special marker that will evaluate to a null/void value
            // We'll use 0 as a placeholder and handle conversion at runtime.
            // Synthetic literal — zero-width Span.
            return new LiteralExpression(location, 0, Span: Span.At(location));
        }

        return new LiteralExpression(location, defaultValue, Span: Span.At(location));
    }

    private AssignmentStatement ParseAssignment()
    {
        var name = Expect(TokenType.Identifier, "Expected variable name").Text;
        var location = PreviousToken.Location;

        Expect(TokenType.Assign, "Expected '=' in assignment");

        var value = ParseExpression();

        return new AssignmentStatement(location, name, value, Span: new Span(location, PreviousToken.Location));
    }

    private ReturnStatement ParseReturnStatement()
    {
        var location = PreviousToken.Location;
        var value = ParseExpression();
        return new ReturnStatement(location, value, Span: new Span(location, PreviousToken.Location));
    }

    private SongExpression ParseSongExpression()
    {
        var location = CurrentToken.Location;
        Expect(TokenType.LBracket, "Expected '[' for song arrangement");

        var sections = new List<SongSectionReference>();
        var elements = new List<SongElement>();

        while (!Check(TokenType.RBracket) && !IsAtEnd())
        {
            var nameToken = Expect(TokenType.Identifier, "Expected section name in song arrangement");
            var sectionName = nameToken.Text;
            var elemLoc = nameToken.Location;

            // Phase 36 Plan 36-10 (D-36-13): parameterized call shape
            // `verse(arg1, arg2, name=value, ...)`. Mirrors the function-call
            // arg-list logic from Phase 36 Plan 36-02 — supports positionals
            // followed by named args; positional-after-named is rejected.
            List<Expression>? positionalArgs = null;
            Dictionary<string, Expression>? namedArgs = null;
            bool isCall = false;
            if (Match(TokenType.LParen))
            {
                isCall = true;
                positionalArgs = new List<Expression>();
                bool sawNamedArg = false;
                while (!Check(TokenType.RParen) && !IsAtEnd())
                {
                    if (Check(TokenType.Identifier)
                        && _current + 1 < _tokens.Count
                        && _tokens[_current + 1].Type == TokenType.Assign)
                    {
                        var argNameTok = Advance();
                        Advance(); // consume `=`
                        var valueExpr = ParseExpression();
                        namedArgs ??= new Dictionary<string, Expression>();
                        if (namedArgs.ContainsKey(argNameTok.Text))
                        {
                            _errorReporter.ReportError(
                                $"duplicate named argument '{argNameTok.Text}' in call to section '{sectionName}'",
                                argNameTok.Location);
                        }
                        else
                        {
                            namedArgs[argNameTok.Text] = valueExpr;
                        }
                        sawNamedArg = true;
                    }
                    else
                    {
                        if (sawNamedArg)
                        {
                            _errorReporter.ReportError(
                                $"positional argument after named argument is not allowed (in call to section '{sectionName}')",
                                CurrentToken.Location);
                        }
                        positionalArgs.Add(ParseExpression());
                    }
                    // Allow comma between args (optional)
                    if (!Check(TokenType.RParen)) Match(TokenType.Comma);
                }
                Expect(TokenType.RParen, "Expected ')' after section call arguments");
            }

            int repeatCount = 1;

            // Check for repeat: name*N or name(args)*N (D-36-14)
            if (Match(TokenType.Star))
            {
                var countToken = Expect(TokenType.IntLiteral, "Expected repeat count after '*'");
                repeatCount = (int)countToken.Value!;
            }

            sections.Add(new SongSectionReference(sectionName, repeatCount));

            if (isCall)
            {
                elements.Add(new SectionCallElement(
                    elemLoc,
                    sectionName,
                    positionalArgs!,
                    NamedArgs: namedArgs,
                    RepeatCount: repeatCount,
                    Span: new Span(elemLoc, PreviousToken.Location)));
            }
            else
            {
                elements.Add(new BareSectionElement(
                    elemLoc,
                    sectionName,
                    RepeatCount: repeatCount,
                    Span: nameToken.EffectiveSpan));
            }
        }

        Expect(TokenType.RBracket, "Expected ']' after song arrangement");

        return new SongExpression(
            location,
            sections,
            Span: new Span(location, PreviousToken.Location),
            Elements: elements);
    }

    private SectionDeclaration ParseSectionDeclaration()
    {
        var location = PreviousToken.Location;
        var name = Expect(TokenType.Identifier, "Expected section name").Text;

        // Phase 36 Plan 36-10 (D-36-13..17 / SECT-01) — optional parameter
        // list: `section verse(Pattern, Pattern, ...) { ... }`. Each parameter
        // is a Phase 35 pattern (LiteralPattern / BindingPattern / ConstructorPattern /
        // GuardPattern / etc.) optionally followed by `= Expression` for the
        // default value (D-36-15).
        //
        // Backward-compat: when no LParen follows the name, both Parameters
        // and DefaultValues stay null and the section behaves exactly like
        // the pre-Phase-36 zero-arg form.
        IReadOnlyList<Pattern>? parameters = null;
        IReadOnlyList<Expression?>? defaultValues = null;
        if (Match(TokenType.LParen))
        {
            var paramList = new List<Pattern>();
            var defaultList = new List<Expression?>();
            while (!Check(TokenType.RParen) && !IsAtEnd())
            {
                var pattern = ParseSectionParameterPattern();
                Expression? defaultExpr = null;
                if (Match(TokenType.Assign))
                    defaultExpr = ParseExpression();
                paramList.Add(pattern);
                defaultList.Add(defaultExpr);
                if (!Check(TokenType.RParen))
                {
                    if (!Match(TokenType.Comma))
                    {
                        // Allow space-separated for S-expression-friendly
                        // surface but emit nothing on a clean Comma. If we
                        // see something else AND we're not at RParen, bail.
                        if (!Check(TokenType.RParen))
                            Expect(TokenType.Comma, "Expected ',' between section parameters");
                    }
                }
            }
            Expect(TokenType.RParen, "Expected ')' after section parameters");
            parameters = paramList;
            defaultValues = defaultList;
        }

        Expect(TokenType.LBrace, "Expected '{' after section name");

        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ;

            if (Check(TokenType.RBrace) || IsAtEnd())
                break;

            var stmt = ParseStatement();
            if (stmt != null)
                body.Add(stmt);

            Match(TokenType.Semicolon);
        }

        Expect(TokenType.RBrace, "Expected '}' after section body");

        return new SectionDeclaration(
            location,
            name,
            body,
            Span: new Span(location, PreviousToken.Location),
            Parameters: parameters,
            DefaultValues: defaultValues);
    }

    private ImportStatement ParseImportStatement()
    {
        var location = PreviousToken.Location;
        var path = Expect(TokenType.StringLiteral, "Expected string literal for import path");
        return new ImportStatement(location, (string)path.Value!, Span: new Span(location, PreviousToken.Location));
    }

    /// <summary>
    /// Phase 43 D-01 / D-03 — parses the <c>module &lt;name&gt;</c> top-of-file
    /// declaration. Dispatched by <see cref="ParseStatement"/> when the current
    /// token is <see cref="TokenType.Module"/> AND the position-constraint flag
    /// (<see cref="_seenNonModuleNonCommentStatement"/>) is false. The
    /// <c>module</c> keyword has already been consumed by the caller, so
    /// <see cref="PreviousToken"/>.Location points at the keyword itself.
    /// Module names follow the standard identifier rule
    /// <c>[a-zA-Z_][a-zA-Z0-9_]*</c>; non-identifier tokens (numeric literals,
    /// string literals, etc.) raise a parse error.
    /// </summary>
    private ModuleDeclarationStatement ParseModuleDeclaration()
    {
        var location = PreviousToken.Location;
        var nameTok = Expect(TokenType.Identifier, "Expected module name after 'module' keyword");
        return new ModuleDeclarationStatement(
            location,
            nameTok.Text,
            Span: new Span(location, PreviousToken.Location));
    }

    private MusicalContextStatement ParseMusicalContextStatement(MusicalContextType contextType)
    {
        var location = PreviousToken.Location;
        Expression value;
        Expression? value2 = null;

        switch (contextType)
        {
            case MusicalContextType.Timesig:
                // Parse numerator / denominator (e.g., 4/4, 3/4, 7/8), OR the
                // common-time shorthand `C` which lowers to 4/4.
                if (Check(TokenType.Identifier) && CurrentToken.Text == "C")
                {
                    var cLoc = CurrentToken.Location;
                    Advance(); // consume `C`
                    value = new LiteralExpression(cLoc, 4, Span: Span.At(cLoc));
                    value2 = new LiteralExpression(cLoc, 4, Span: Span.At(cLoc));
                    break;
                }
                {
                    var numLoc = CurrentToken.Location;
                    value = new LiteralExpression(numLoc,
                        (int)Expect(TokenType.IntLiteral, "Expected integer numerator for time signature (or 'C' for common time)").Value!,
                        Span: Span.At(numLoc));
                    Expect(TokenType.Slash, "Expected '/' separator in time signature (e.g., timesig 4/4)");
                    var denLoc = CurrentToken.Location;
                    value2 = new LiteralExpression(denLoc,
                        (int)Expect(TokenType.IntLiteral, "Expected integer denominator for time signature").Value!,
                        Span: Span.At(denLoc));
                }
                break;

            case MusicalContextType.Tempo:
            {
                int tempoSign = 1;
                var tempoLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) tempoSign = -1;
                else if (Match(TokenType.Plus)) tempoSign = 1;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(tempoLoc, tempoSign * (int)Advance().Value!, Span: Span.At(tempoLoc));
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(tempoLoc, tempoSign * (double)Advance().Value!, Span: Span.At(tempoLoc));
                else
                    throw new ParseException($"Expected numeric tempo value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.Swing:
            {
                int swingSign = 1;
                var swingLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) swingSign = -1;
                else if (Match(TokenType.Plus)) swingSign = 1;
                if (Check(TokenType.IntLiteral))
                {
                    var intToken = Advance();
                    int intVal = swingSign * (int)intToken.Value!;
                    if (Check(TokenType.Identifier) && CurrentToken.Text == "%")
                    {
                        Advance();
                        value = new LiteralExpression(swingLoc, intVal / 100.0, Span: Span.At(swingLoc));
                    }
                    else
                        value = new LiteralExpression(swingLoc, (double)intVal, Span: Span.At(swingLoc));
                }
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(swingLoc, swingSign * (double)Advance().Value!, Span: Span.At(swingLoc));
                else
                    throw new ParseException($"Expected swing value (percentage or float), got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.Key:
                // Accept identifier like Cmajor, Aminor, etc.
                var keyToken = Expect(TokenType.Identifier, "Expected key name (e.g., Cmajor, Aminor)");
                value = new LiteralExpression(keyToken.Location, keyToken.Text, Span: Span.At(keyToken.Location));
                break;

            case MusicalContextType.Dynamics:
            {
                var dynToken = Expect(TokenType.Identifier, "Expected dynamic level (pp, p, mp, mf, f, ff, fff, ppp)");
                var velocity = TryParseDynamicMarking(dynToken.Text);
                if (!velocity.HasValue)
                {
                    _errorReporter.ReportError(
                        $"Unknown dynamic marking '{dynToken.Text}'. Use: ppp, pp, p, mp, mf, f, ff, fff",
                        dynToken.Location);
                    value = new LiteralExpression(dynToken.Location, 0.63, Span: Span.At(dynToken.Location));
                }
                else
                {
                    value = new LiteralExpression(dynToken.Location, velocity.Value, Span: Span.At(dynToken.Location));
                }
                break;
            }

            case MusicalContextType.Rit:
            case MusicalContextType.Accel:
            {
                var tempoLoc = CurrentToken.Location;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(tempoLoc, (int)Advance().Value!, Span: Span.At(tempoLoc));
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(tempoLoc, (double)Advance().Value!, Span: Span.At(tempoLoc));
                else
                    throw new ParseException($"Expected target tempo for {contextType}, got {CurrentToken.Type}");
                break;
            }

            case MusicalContextType.Pan:
            {
                int panSign = 1;
                var panLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) panSign = -1;
                else if (Match(TokenType.Plus)) panSign = 1;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(panLoc, panSign * (double)(int)Advance().Value!, Span: Span.At(panLoc));
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(panLoc, panSign * (double)Advance().Value!, Span: Span.At(panLoc));
                else
                    throw new ParseException($"Expected numeric pan value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.Gain:
            {
                int gainSign = 1;
                var gainLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) gainSign = -1;
                else if (Match(TokenType.Plus)) gainSign = 1;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(gainLoc, gainSign * (double)(int)Advance().Value!, Span: Span.At(gainLoc));
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(gainLoc, gainSign * (double)Advance().Value!, Span: Span.At(gainLoc));
                else
                    throw new ParseException($"Expected numeric gain value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.ReverbTime:
            {
                var rtLoc = CurrentToken.Location;
                if (Match(TokenType.Minus))
                    throw new ParseException(
                        $"reverbTime cannot be negative (RT60 is a time in seconds); got '-' at {rtLoc}");
                if (Match(TokenType.Plus)) { /* silent sign noise, accept */ }
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(rtLoc, (double)(int)Advance().Value!, Span: Span.At(rtLoc));
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(rtLoc, (double)Advance().Value!, Span: Span.At(rtLoc));
                else
                    throw new ParseException(
                        $"Expected numeric reverbTime value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.VoicePool:
            {
                // Phase 28 SPEC-7: voicePool N { ... } — integer literal only.
                // Range validation (1..256) is done at the interpreter so the error
                // message points at the offending integer, not at the '{'.
                var poolLoc = CurrentToken.Location;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(poolLoc, (int)Advance().Value!, Span: Span.At(poolLoc));
                else
                    throw new ParseException(
                        $"Expected integer voice pool size (1..256), got {CurrentToken.Type} '{CurrentToken.Text}' at {poolLoc}");
                break;
            }

            case MusicalContextType.SustainPedal:
                // No value — sustainPedal { ... } takes no argument. Synthesize a
                // placeholder so the existing MusicalContextStatement shape holds.
                value = new LiteralExpression(location, 1, Span: Span.At(location));
                break;

            default:
                throw new ParseException($"Unknown musical context type: {contextType}");
        }

        // Expect body block
        Expect(TokenType.LBrace, "Expected '{' to open musical context block");

        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ; // skip semicolons

            if (Check(TokenType.RBrace) || IsAtEnd())
                break;

            var stmt = ParseStatement();
            if (stmt != null)
                body.Add(stmt);

            Match(TokenType.Semicolon);
        }

        Expect(TokenType.RBrace, "Expected '}' to close musical context block");

        return new MusicalContextStatement(location, contextType, value, value2, body, Span: new Span(location, PreviousToken.Location));
    }

    /// <summary>
    /// Phase 32 Plan 32-06 D-13/D-15 — parses a <c>tuning &lt;expr&gt; { ... }</c>
    /// musical-context block into a <see cref="TuningContextStatement"/>. Called
    /// AFTER the <see cref="TokenType.Tuning"/> keyword token has been consumed
    /// by the dispatch at <c>ParseStatement</c>; the keyword's location lives in
    /// <see cref="PreviousToken"/>.
    ///
    /// Three composer surfaces (D-15):
    /// <list type="number">
    ///   <item>identifier: <c>tuning partch { }</c> — <see cref="ParseExpression"/>
    ///   produces a <see cref="VariableExpression"/>.</item>
    ///   <item>inline call: <c>tuning (loadScala "x.scl") { }</c> —
    ///   <see cref="ParseExpression"/> produces a <see cref="FunctionCallExpression"/>.</item>
    ///   <item>string-literal sugar: <c>tuning "x.scl" { }</c> — desugared HERE
    ///   at parse time into a synthetic <see cref="FunctionCallExpression"/> for
    ///   <c>loadScala</c>. T-32-AST mitigation: the synthetic call's
    ///   <see cref="SourceLocation"/> is the <c>tuning</c> keyword's line (NOT
    ///   <see cref="SourceLocation.Unknown"/> nor a synthetic frame), so runtime
    ///   errors during the desugared call surface at the composer's typed
    ///   <c>tuning "x.scl"</c> line.</item>
    /// </list>
    /// </summary>
    private TuningContextStatement ParseTuningContextStatement()
    {
        // The `tuning` keyword has already been consumed by the dispatch in
        // ParseStatement (via Match). PreviousToken is the `tuning` keyword.
        var tuningLocation = PreviousToken.Location;

        Expression tuningExpr;

        if (Check(TokenType.StringLiteral))
        {
            // D-15 string-literal sugar: `tuning "x.scl" { }` desugars at parse
            // time to a FunctionCallExpression for loadScala. The synthetic call
            // node's SourceLocation MUST be the `tuning` keyword's location so
            // runtime errors surface at the user's source line (T-32-AST).
            var litToken = Advance();
            string sclPath = (string)litToken.Value!;
            var literalArg = new LiteralExpression(litToken.Location, sclPath, Span: Span.At(litToken.Location));
            tuningExpr = new FunctionCallExpression(
                tuningLocation,
                "loadScala",
                new List<Expression> { literalArg },
                Span: new Span(tuningLocation, litToken.Location));
        }
        else
        {
            // Identifier or inline-call form. ParseExpression handles both:
            //   `partch`                  -> VariableExpression
            //   `(loadScala "x.scl")`     -> FunctionCallExpression
            tuningExpr = ParseExpression();
        }

        // Expect body block.
        Expect(TokenType.LBrace, "Expected '{' to open tuning context block");

        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ; // skip semicolons

            if (Check(TokenType.RBrace) || IsAtEnd())
                break;

            var stmt = ParseStatement();
            if (stmt != null)
                body.Add(stmt);

            Match(TokenType.Semicolon);
        }

        Expect(TokenType.RBrace, "Expected '}' to close tuning context block");

        return new TuningContextStatement(tuningLocation, tuningExpr, body, Span: new Span(tuningLocation, PreviousToken.Location));
    }

    /// <summary>
    /// Phase 38 Plan 38-02 LIVE-01 — parses a <c>live &lt;quantize&gt; { ... }</c>
    /// block per RESEARCH §A lines 414-457. The <c>live</c> keyword has already
    /// been consumed by <see cref="ParseStatement"/>; <see cref="PreviousToken"/>
    /// carries its location.
    ///
    /// <para>
    /// Quantize forms (D-38-02):
    /// <list type="bullet">
    ///   <item><c>live 1bar { }</c> / <c>live 2bars { }</c> — Int literal with
    ///   optional <c>bar</c>/<c>bars</c> identifier suffix. The integer becomes
    ///   the literal payload; the suffix is consumed when present and ignored
    ///   in the AST shape (the runtime interprets a bare Int as bars).</item>
    ///   <item><c>live q { }</c> — NoteValue identifier (<c>q</c>/<c>h</c>/<c>w</c>/<c>e</c>/<c>s</c>).
    ///   Captured as a String LiteralExpression so the interpreter can map it
    ///   to its beat fraction via <see cref="FlowLang.TypeSystem.SpecialTypes.NoteValueType"/>.</item>
    ///   <item><c>live { }</c> — quantize omitted. Parser synthesizes a
    ///   <c>LiteralExpression(location, 1)</c> as the 1-bar default per the
    ///   plan's must-haves; the interpreter resolves to 1 × beats-per-bar.</item>
    /// </list>
    /// </para>
    /// </summary>
    private LiveBlockStatement ParseLiveBlockStatement()
    {
        // The `live` keyword has already been consumed by the dispatch in
        // ParseStatement (via Match). PreviousToken is the `live` keyword.
        var location = PreviousToken.Location;

        Expression quantizeValue;

        if (Check(TokenType.LBrace))
        {
            // Omitted — synthesize the 1-bar default at the `live` keyword's
            // location so error reporting + BlockId stability anchor on the
            // composer's source line.
            quantizeValue = new LiteralExpression(location, 1, Span: Span.At(location));
        }
        else if (Check(TokenType.IntLiteral))
        {
            // Int + optional `bar`/`bars` suffix.
            var intLoc = CurrentToken.Location;
            int intVal = (int)Advance().Value!;
            quantizeValue = new LiteralExpression(intLoc, intVal, Span: Span.At(intLoc));
            // Consume optional "bar" / "bars" suffix when present.
            if (Check(TokenType.Identifier)
                && (CurrentToken.Text == "bar" || CurrentToken.Text == "bars"))
            {
                Advance();
            }
        }
        else if (Check(TokenType.Identifier)
            && (CurrentToken.Text == "q" || CurrentToken.Text == "h"
                || CurrentToken.Text == "w" || CurrentToken.Text == "e"
                || CurrentToken.Text == "s"))
        {
            // NoteValue identifier (q/h/w/e/s). Capture as a String literal
            // so the interpreter routes through NoteValueType.Parse for the
            // beats-per-unit resolution.
            var nvToken = Advance();
            quantizeValue = new LiteralExpression(nvToken.Location, nvToken.Text, Span: Span.At(nvToken.Location));
        }
        else
        {
            throw new ParseException(
                $"Expected quantize unit (Int + 'bar'/'bars', or NoteValue q/h/w/e/s, or '{{' for 1-bar default), got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
        }

        Expect(TokenType.LBrace, "Expected '{' to open live block body");

        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ; // skip semicolons

            if (Check(TokenType.RBrace) || IsAtEnd())
                break;

            var stmt = ParseStatement();
            if (stmt != null)
                body.Add(stmt);

            Match(TokenType.Semicolon);
        }

        Expect(TokenType.RBrace, "Expected '}' to close live block body");

        int blockId = LiveBlockStatement.ComputeBlockId(location);

        return new LiveBlockStatement(
            location,
            quantizeValue,
            body,
            blockId,
            Span: new Span(location, PreviousToken.Location));
    }

    private ForStatement ParseForStatement()
    {
        var location = PreviousToken.Location;
        var (elementType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
        _current = nextIndex;
        if (isVarArgs)
            elementType = new ArrayType(elementType);
        var varName = Expect(TokenType.Identifier, "Expected variable name in for loop").Text;
        Expect(TokenType.In, "Expected 'in' after variable name in for loop");
        var collection = ParseExpression();
        Expect(TokenType.LBrace, "Expected '{' to begin for loop body");

        var savedInLoop = _inLoop;
        _inLoop = true;
        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ;
            if (Check(TokenType.RBrace) || IsAtEnd()) break;
            var stmt = ParseStatement();
            if (stmt != null) body.Add(stmt);
            Match(TokenType.Semicolon);
        }
        _inLoop = savedInLoop;

        Expect(TokenType.RBrace, "Expected '}' to close for loop body");
        return new ForStatement(location, elementType, varName, collection, body, Span: new Span(location, PreviousToken.Location));
    }

    private WhileStatement ParseWhileStatement()
    {
        var location = PreviousToken.Location;
        var condition = ParseExpression();
        Expect(TokenType.LBrace, "Expected '{' to begin while loop body");

        var savedInLoop = _inLoop;
        _inLoop = true;
        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ;
            if (Check(TokenType.RBrace) || IsAtEnd()) break;
            var stmt = ParseStatement();
            if (stmt != null) body.Add(stmt);
            Match(TokenType.Semicolon);
        }
        _inLoop = savedInLoop;

        Expect(TokenType.RBrace, "Expected '}' to close while loop body");
        return new WhileStatement(location, condition, body, Span: new Span(location, PreviousToken.Location));
    }

    private Expression ParseExpression()
    {
        _parseDepth++;
        if (_parseDepth > MaxParseDepth)
            throw new ParseException($"Maximum nested parse depth of {MaxParseDepth} reached.");

        try
        {
            return ParseFlowExpression();
        }
        finally
        {
            _parseDepth--;
        }
    }

    // Phase 26: arithmetic is now prefix-only; ParseFlowExpression dispatches directly
    // to ParseUnaryShorthand (the post-Phase-26 successor of ParseAdditive/ParseUnary).
    private Expression ParseFlowExpression()
    {
        var left = ParseUnaryShorthand();

        // Phase 26.1 TUP-10: also match TildeArrow `~>`. Unlike `->` (which does
        // a parse-time transform when RHS is a recognizable call shape), `~>`
        // ALWAYS emits TupleUnpackFlowExpression because tuple arity is unknown
        // at parse time (RESEARCH Q5 / Pitfall 2). The evaluator does the unpack
        // at runtime when the tuple's IReadOnlyList<Value> is in hand.
        while (true)
        {
            bool isTildeArrow;
            if (Match(TokenType.Arrow)) { isTildeArrow = false; }
            else if (Match(TokenType.TildeArrow)) { isTildeArrow = true; }
            else break;

            var location = PreviousToken.Location;
            var right = ParseUnaryShorthand();

            if (isTildeArrow)
            {
                // Always defer to runtime — arity unknown at parse time (RESEARCH Q5)
                left = new TupleUnpackFlowExpression(location, left, right, Span: new Span(left.Location, PreviousToken.Location));
                continue;
            }

            // Existing -> behavior unchanged below.
            // Transform right side if it's an identifier or function call
            // x -> func becomes func(x)
            // x -> func(arg) becomes func(x, arg)
            // x -> func (expr) becomes func(x, expr) (parenthesized args in flow context)
            if (right is VariableExpression varExpr)
            {
                // Collect a single parenthesized argument after the function name in flow context
                // This supports: x -> concat (expr) -> print
                // Only collect one parenthesized expression to avoid consuming the next statement
                // (e.g., `arr -> each (lambda)\n("next stmt")` should not treat the second line as an arg)
                var args = new List<Expression> { left };
                if (!IsAtEnd() && Check(TokenType.LParen)
                    && CurrentToken.Location.Line == varExpr.Location.Line)
                {
                    args.Add(ParseUnaryShorthand());
                }
                right = new FunctionCallExpression(right.Location, varExpr.Name, args, Span: new Span(right.Location, PreviousToken.Location));
            }
            else if (right is FunctionCallExpression funcCall)
            {
                // Prepend left to function arguments
                var newArgs = new List<Expression> { left };
                newArgs.AddRange(funcCall.Arguments);
                right = funcCall with { Arguments = newArgs };
            }
            else
            {
                // Otherwise just wrap in flow expression — also threads `as NAME` if present
                // (RESEARCH §Pattern 3; OQ5 supported form is `EXPR -> CALL as NAME` so
                // branch 3 + `as` is an edge case; classic pipe semantics still apply).
                var intermediateNameElse = TryConsumeAsClause();
                left = new FlowExpression(location, left, right, IntermediateName: intermediateNameElse, Span: new Span(left.Location, PreviousToken.Location));
                continue;
            }

            // Phase 35 Plan 35-07 (LANG-03): peek for `as NAME` after the prepend-transform
            // produced the constructed FunctionCallExpression. When present, wrap the
            // constructed call in a FlowExpression carrying IntermediateName — the evaluator
            // path with IntermediateName != null evaluates Right ONLY (the constructed call
            // already contains the prepended Left in its args) and declares the binding in
            // the CURRENT frame per Pitfall 7. RESEARCH OQ5 (RESOLVED 2026-05-18):
            // right-associative with `->`; only the `EXPR -> CALL as NAME -> ...` form ships.
            var intermediateName = TryConsumeAsClause();
            if (intermediateName != null)
            {
                left = new FlowExpression(location, left, right, IntermediateName: intermediateName, Span: new Span(left.Location, PreviousToken.Location));
            }
            else
            {
                left = right;
            }
        }

        return left;
    }

    // Phase 26 (D-01 + D-03): replaces deleted ParseUnary/ParseAdditive/ParseMultiplicative.
    // Arithmetic is now prefix-only via (add)/(sub)/(mul)/(div)/(neg)/(idiv) builtins.
    // The remaining "unary" responsibilities are:
    //   D-03: silently strip leading '+' at expression-start (no node emitted)
    //   D-01: leading '-' followed by an identifier lowers to (neg IDENT)
    private Expression ParseUnaryShorthand()
    {
        // D-03: silently strip '+' at expression-start (no node emitted)
        if (Match(TokenType.Plus))
        {
            // No-op; just continue
        }
        // D-01: '-' followed by identifier → (neg IDENT)
        if (Check(TokenType.Minus) && _current + 1 < _tokens.Count
            && _tokens[_current + 1].Type == TokenType.Identifier)
        {
            var loc = CurrentToken.Location;
            Advance(); // consume '-'
            var name = Advance().Text;
            return new FunctionCallExpression(loc, "neg",
                new List<Expression> { new VariableExpression(loc, name, Span: Span.At(loc)) },
                Span: new Span(loc, PreviousToken.Location));
        }
        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Match(TokenType.At))
            {
                // Array indexing: arr@index (supports unary minus for negative indices)
                var index = ParseUnaryShorthand();
                expr = new ArrayIndexExpression(expr.Location, expr, index, Span: new Span(expr.Location, PreviousToken.Location));
            }
            else if (Match(TokenType.Dot))
            {
                // Member access: obj.member
                var memberName = Expect(TokenType.Identifier, "Expected member name after '.'").Text;
                expr = new MemberAccessExpression(expr.Location, expr, memberName, Span: new Span(expr.Location, PreviousToken.Location));
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expression ParsePrimary()
    {
        // Lazy expression
        if (Match(TokenType.Lazy))
        {
            var location = PreviousToken.Location;
            Expect(TokenType.LParen, "Expected '(' after 'lazy'");
            var innerExpr = ParseExpression();
            Expect(TokenType.RParen, "Expected ')' after lazy expression");
            return new LazyExpression(location, innerExpr, Span: new Span(location, PreviousToken.Location));
        }

        // Literals
        // Phase 26: IntLiteral may carry an int, long, or BigInteger payload depending
        // on whether the literal overflowed Int32. Pass the boxed Value through —
        // EvaluateLiteral dispatches on the underlying CLR type.
        // Phase 35 LANG-04: literals derive their Span from the consumed token's
        // EffectiveSpan (which the lexer populated via the Wave 1 sweep).
        if (Match(TokenType.IntLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);

        if (Match(TokenType.FloatLiteral))
            return new LiteralExpression(PreviousToken.Location, (double)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);

        if (Match(TokenType.StringLiteral))
            return new LiteralExpression(PreviousToken.Location, (string)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);

        if (Match(TokenType.InterpolatedStringStart))
            return ParseInterpolatedString();

        if (Match(TokenType.BoolLiteral))
            return new LiteralExpression(PreviousToken.Location, (bool)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);

        // Audit §2.1: music-literal tokens carry their RAW TEXT as the LiteralExpression
        // payload and are resolved to a music Value by TryParseSpecialLiteral at eval time.
        // They MUST be tagged IsMusicLiteral: true so the evaluator distinguishes them from
        // ordinary quoted StringLiteral tokens (which carry a genuine string and must stay
        // a String — `"10s"` is not Second(10), `"a"` is not Note A4).
        if (Match(TokenType.NoteLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan, IsMusicLiteral: true);

        if (Match(TokenType.SemitoneLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan, IsMusicLiteral: true);

        if (Match(TokenType.CentLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan, IsMusicLiteral: true);

        if (Match(TokenType.TimeLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan, IsMusicLiteral: true);

        if (Match(TokenType.DecibelLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan, IsMusicLiteral: true);

        // Phase 26.2 ERG-04 — HertzLiteral routes to LiteralExpression with raw text;
        // ExpressionEvaluator.TryParseSpecialLiteral resolves "800Hz" / "1.5kHz" to Value.Hertz(canonical-Hz double).
        if (Match(TokenType.HertzLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan, IsMusicLiteral: true);

        // Phase 45 D-09: BeatLiteral diverges from the flat LiteralExpression(text) routing
        // used by Cent/Time/Decibel/Hertz — cast Token.Value to double directly so the raw
        // payload survives to eval time (ExpressionEvaluator.EvaluateBeatLiteral) where the
        // pragma multiplier formula reads it against the active timesig (REQ-BEAT-AST-02).
        if (Match(TokenType.BeatLiteral))
        {
            double rawValue = (double)PreviousToken.Value!;
            return new BeatLiteralExpression(PreviousToken.Location, rawValue,
                                             Span: PreviousToken.EffectiveSpan);
        }

        if (Match(TokenType.ChordLiteral))
            return new ChordLiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

        if (Match(TokenType.SymbolLiteral))
            return new SymbolLiteralExpression(PreviousToken.Location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);

        // Lambda expression: fn Type name, Type name => body
        if (Match(TokenType.Fn))
        {
            return ParseLambdaExpression();
        }

        // Progression expression: progression | I IV V | or progression voices 4 | I IV V |
        if (Match(TokenType.Progression))
        {
            return ParseProgressionExpression();
        }

        // Pickup note stream: pickup | C4 D4 |
        if (Match(TokenType.Pickup))
        {
            Expect(TokenType.Pipe, "Expected '|' after 'pickup'");
            return ParseNoteStream(isPickup: true);
        }

        // Note stream expression: | C4 D4 E4 F4 |
        if (Match(TokenType.Pipe))
        {
            return ParseNoteStream();
        }

        // Array literal [elem1, elem2, ...]
        if (Match(TokenType.LBracket))
        {
            var location = PreviousToken.Location;
            var elements = new List<Expression>();

            while (!Check(TokenType.RBracket) && !IsAtEnd())
            {
                elements.Add(ParseExpression());
                if (!Check(TokenType.RBracket))
                {
                    // Support both comma-separated and space-separated array elements
                    if (Check(TokenType.Comma))
                        Advance(); // consume optional comma
                }
            }

            Expect(TokenType.RBracket, "Expected ']' after array literal");
            return new ArrayLiteralExpression(location, elements, Span: new Span(location, PreviousToken.Location));
        }

        // Phase 26.1 TUP-09: tuple literal <<elem1, elem2, ...>>. Empty <<>> and singleton
        // <<x>> are valid arities (CONTEXT § Specifics block 2 — `<<>>` empty + `<<x>>` singleton).
        if (Match(TokenType.LessLess))
        {
            var location = PreviousToken.Location;
            var elements = new List<Expression>();

            while (!Check(TokenType.GreaterGreater) && !IsAtEnd())
            {
                elements.Add(ParseExpression());
                if (!Check(TokenType.GreaterGreater) && Check(TokenType.Comma))
                    Advance();
            }

            Expect(TokenType.GreaterGreater, "Expected '>>' after tuple literal");
            return new TupleLiteralExpression(location, elements, Span: new Span(location, PreviousToken.Location));
        }

        // Parenthesized expression or function call
        if (Match(TokenType.LParen))
        {
            var location = PreviousToken.Location;

            // Phase 35 Plan 35-05 (LANG-01): (match scrutinee | pat => body | ... | _ => default)
            // The `(match` open paren is the disambiguator vs. note-stream `|` per RESEARCH §D.2 +
            // Pitfall 2. Detected BEFORE the generic identifier-call branch below so the `match`
            // keyword is never mistaken for a function name. The `match` keyword has its own
            // TokenType.Match (Plan 35-05 Task 2 lexer addition).
            if (Check(TokenType.Match))
            {
                Advance(); // consume `match`
                return ParseMatch(location);
            }

            // Phase 43 Plan 43-03 D-02 — qualified function call: (mod.fn args)
            // Recognized as a function call when we see LParen Ident Dot Ident inside
            // a parenthesized form. Emits a FunctionCallExpression with Name="mod.fn"
            // (dotted string); EvaluateFunctionCall detects the dot at runtime and
            // routes through ExecutionContext.ModuleRegistry.TryGetProc per D-02.
            // Pitfall 2: only the parenthesized-call form (LParen IDENT Dot IDENT)
            // produces a qualified-call FunctionCallExpression — bare `chord.root`
            // (NO surrounding LParen / RParen) continues to parse as
            // MemberAccessExpression and dispatch via the existing instance-member
            // path. The 4-token lookahead ensures we don't perturb `(chord.root)`
            // value-reference forms either: the trailing token must be either an
            // argument-start or RParen, which means the disambiguator picks the
            // qualified-call branch ONLY when the user wrote something callable.
            if (Check(TokenType.Identifier)
                && _current + 1 < _tokens.Count
                && _tokens[_current + 1].Type == TokenType.Dot
                && _current + 2 < _tokens.Count
                && _tokens[_current + 2].Type == TokenType.Identifier
                && _current + 3 < _tokens.Count
                && _tokens[_current + 3].Type != TokenType.Dot   // chained `.` is NOT a call
                && _tokens[_current + 3].Type != TokenType.At)
            {
                var modTok = Advance(); // module identifier
                Advance();              // '.'
                var fnTok = Advance();  // proc identifier
                var qualifiedName = $"{modTok.Text}.{fnTok.Text}";
                var args = new List<Expression>();

                var savedFlag = _inFuncCallArgs;
                _inFuncCallArgs = true;
                while (!Check(TokenType.RParen) && !IsAtEnd())
                {
                    args.Add(ParseExpression());
                }
                _inFuncCallArgs = savedFlag;

                Expect(TokenType.RParen, "Expected ')' after qualified function arguments");
                return new FunctionCallExpression(
                    location,
                    qualifiedName,
                    args,
                    Span: new Span(location, PreviousToken.Location));
            }

            // Check if this is a function call like (func arg1 arg2)
            // But NOT if the identifier is followed by -> (that's a parenthesized flow expression)
            //
            // break-control (0615): TokenType.Break / TokenType.Continue are recognized
            // as call names here so the prefix-only `(break)` / `(continue)` builtins
            // parse (they lex as keyword tokens, same situation as Pan/Gain). The bare
            // `break` / `continue` STATEMENT form is handled earlier at statement-start
            // (ParseStatement) and never reaches ParsePrimary — these two forms coexist.
            // The keyword tokens carry their literal text ("break"/"continue") so
            // `Advance().Text` resolves the builtin name correctly.
            if ((Check(TokenType.Identifier) || Check(TokenType.Pan) || Check(TokenType.Gain)
                 || Check(TokenType.Break) || Check(TokenType.Continue)) && _current + 1 < _tokens.Count
                && _tokens[_current + 1].Type != TokenType.Arrow
                && _tokens[_current + 1].Type != TokenType.Dot
                && _tokens[_current + 1].Type != TokenType.At)
            {
                var name = Advance().Text;
                var args = new List<Expression>();

                // Phase 36 Plan 36-02 (D-36-11): universal named-argument syntax.
                // 2-token peek inside the arg-list loop — when current is Identifier
                // and next is Assign, parse as `name=expr` named arg. Positional args
                // MUST precede all named args (same rule as Python/C#); sawNamedArg
                // is the flip-on flag that converts a subsequent positional into a
                // diagnostic. TokenType.Assign is already in TryLexSignedNumber's
                // expression-start set (SimpleLexer.cs:468) so `arg=-5` lexes the
                // negative as a single signed IntLiteral — no special-casing here.
                Dictionary<string, Expression>? namedArgs = null;
                bool sawNamedArg = false;

                // Inside (func ...) args, disable the "identifier literal = function call"
                // heuristic so that (add n 1) parses as add(n, 1), not add(n(1)).
                var savedFlag = _inFuncCallArgs;
                _inFuncCallArgs = true;
                while (!Check(TokenType.RParen) && !IsAtEnd())
                {
                    // 2-token peek for named-arg form `Identifier = Expression`.
                    if (Check(TokenType.Identifier)
                        && _current + 1 < _tokens.Count
                        && _tokens[_current + 1].Type == TokenType.Assign)
                    {
                        var argNameTok = Advance(); // Identifier
                        Advance();                  // Assign
                        var argName = argNameTok.Text;
                        var argLoc = argNameTok.Location;
                        var valueExpr = ParseExpression();
                        namedArgs ??= new Dictionary<string, Expression>();
                        if (namedArgs.ContainsKey(argName))
                        {
                            _errorReporter.ReportError(
                                $"duplicate named argument '{argName}' in call to '{name}'",
                                argLoc);
                        }
                        else
                        {
                            namedArgs[argName] = valueExpr;
                        }
                        sawNamedArg = true;
                    }
                    else
                    {
                        if (sawNamedArg)
                        {
                            _errorReporter.ReportError(
                                $"positional argument after named argument is not allowed (in call to '{name}')",
                                CurrentToken.Location);
                        }
                        args.Add(ParseExpression());
                    }
                }
                _inFuncCallArgs = savedFlag;

                Expect(TokenType.RParen, "Expected ')' after function arguments");
                return new FunctionCallExpression(
                    location,
                    name,
                    args,
                    Span: new Span(location, PreviousToken.Location),
                    NamedArgs: namedArgs);
            }

            // Regular parenthesized expression
            var expr = ParseExpression();
            Expect(TokenType.RParen, "Expected ')' after expression");
            return expr;
        }

        // Variable or function call (also allow music context keywords as identifiers)
        if (Match(TokenType.Identifier) || Match(TokenType.Tempo) || Match(TokenType.Swing)
            || Match(TokenType.Key) || Match(TokenType.Timesig) || Match(TokenType.Pan)
            || Match(TokenType.Gain))
        {
            var name = PreviousToken.Text;
            var location = PreviousToken.Location;

            // Look ahead: if next token starts a simple argument, this is a function call
            // Note: We only support simple arguments (literals, identifiers) for optional parens
            // For complex arguments (parenthesized expressions), use explicit syntax: (func (expr))
            // Disabled inside (func ...) args to prevent (add n 1) from becoming add(n(1))
            if (!_inFuncCallArgs && IsArgumentStart(CurrentToken.Type)
                && CurrentToken.Location.Line == location.Line)
            {
                var args = new List<Expression>();

                // Parse simple arguments until we hit a terminator or non-argument token
                while (!IsAtEnd() && IsArgumentStart(CurrentToken.Type)
                       && CurrentToken.Location.Line == location.Line)
                {
                    args.Add(ParseUnaryShorthand()); // Parse argument expression
                }

                return new FunctionCallExpression(location, name, args, Span: new Span(location, PreviousToken.Location));
            }

            // No arguments - it's a variable reference. Phase 35 LANG-04 Wave 2a:
            // use the identifier token's full Span (start + end-of-identifier)
            // so the diagnostic renderer can size the caret line to the
            // identifier width when the variable is unknown. PreviousToken is the
            // identifier we just consumed via Match() above; its EffectiveSpan
            // is populated by the lexer (Plan 35-01 LexerSpan sweep).
            return new VariableExpression(location, name, Span: PreviousToken.EffectiveSpan);
        }

        throw new ParseException($"Unexpected token {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    }

    private Expression ParseInterpolatedString()
    {
        var location = PreviousToken.Location;
        var parts = new List<Expression>();

        while (!Check(TokenType.InterpolatedStringEnd) && !IsAtEnd())
        {
            if (Match(TokenType.InterpolatedStringText))
            {
                parts.Add(new LiteralExpression(PreviousToken.Location, (string)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan));
            }
            else
            {
                // Parse an expression (the tokens between { and } were already lexed inline)
                parts.Add(ParseExpression());
            }
        }

        Expect(TokenType.InterpolatedStringEnd, "Expected closing '\"' for interpolated string");
        return new InterpolatedStringExpression(location, parts, Span: new Span(location, PreviousToken.Location));
    }

    private Expression ParseLambdaExpression()
    {
        var location = PreviousToken.Location;
        var parameters = new List<LambdaParameter>();

        // Parse parameters: Type name, Type name => body
        // The fat arrow terminates the parameter list
        while (!Check(TokenType.FatArrow) && !IsAtEnd())
        {
            var (paramType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
            _current = nextIndex;

            var paramName = ExpectParameterName("Expected parameter name in lambda").Text;
            parameters.Add(new LambdaParameter(paramName, paramType));

            if (!Check(TokenType.FatArrow))
                Expect(TokenType.Comma, "Expected ',' between lambda parameters");
        }

        Expect(TokenType.FatArrow, "Expected '=>' in lambda expression");

        List<Statement> body;
        if (Check(TokenType.LParen) && IsMultiStatementLambdaBody())
        {
            // Multi-statement lambda body: ( stmt1 stmt2 ... )
            Advance(); // consume '('
            body = new List<Statement>();
            while (!Check(TokenType.RParen) && !IsAtEnd())
            {
                while (Match(TokenType.Semicolon)) ; // skip semicolons
                if (Check(TokenType.RParen)) break;
                var stmt = ParseStatement();
                if (stmt != null) body.Add(stmt);
                Match(TokenType.Semicolon);
            }
            Expect(TokenType.RParen, "Expected ')' after lambda body");
        }
        else
        {
            // Single-expression body (existing behavior)
            var expr = ParseExpression();
            body = new List<Statement> { new ExpressionStatement(expr.Location, expr, Span: new Span(expr.Location, PreviousToken.Location)) };
        }
        return new LambdaExpression(location, parameters, body, Span: new Span(location, PreviousToken.Location));
    }

    /// <summary>
    /// sweep-0614: structural lookahead deciding whether a lambda body that begins
    /// with <c>(</c> is a multi-statement block <c>( stmt1 stmt2 ... )</c> or a single
    /// parenthesized expression. Called with CurrentToken pointing at the body's opening
    /// <c>(</c>; non-consuming. Replaces the old type-keyword-only heuristic, which only
    /// recognized declaration-first blocks and mis-parsed expression-first bodies such as
    /// <c>fn Int x =&gt; ((print "side") x)</c>.
    ///
    /// Rules (in order):
    ///   1. A depth-1 <c>;</c> separator anywhere inside the outer parens ⇒ multi-statement
    ///      (covers both declaration-first and expression-first semicolon-separated bodies).
    ///   2. First inner token is a type keyword ⇒ multi-statement (declaration block —
    ///      preserves the prior behavior even with no semicolon, e.g. <c>(Int y = 5)</c>).
    ///   3. First inner token is an identifier / call-head keyword ⇒ single function-call
    ///      expression (prefix-only Flow has no bare-identifier statement, so an
    ///      identifier-first body with no semicolon is unambiguously one call).
    ///   4. Otherwise ⇒ count top-level units inside the outer parens; 2+ ⇒ multi-statement,
    ///      exactly 1 ⇒ single parenthesized expression (e.g. <c>((add 1 2))</c>).
    /// </summary>
    private bool IsMultiStatementLambdaBody()
    {
        // CurrentToken is the opening '('. Find its matching ')' tracking nesting.
        int open = _current;
        int depth = 0;
        int close = -1;
        bool hasTopLevelSemicolon = false;
        for (int i = open; i < _tokens.Count; i++)
        {
            var t = _tokens[i].Type;
            if (t == TokenType.LParen || t == TokenType.LBracket || t == TokenType.LessLess)
            {
                depth++;
            }
            else if (t == TokenType.RParen || t == TokenType.RBracket || t == TokenType.GreaterGreater)
            {
                depth--;
                if (depth == 0)
                {
                    close = i;
                    break;
                }
            }
            else if (t == TokenType.Semicolon && depth == 1)
            {
                hasTopLevelSemicolon = true;
            }
            else if (t == TokenType.Eof)
            {
                break;
            }
        }

        // Unbalanced — let the single-expression path produce the existing diagnostic.
        if (close < 0) return false;

        // Rule 1: a top-level statement separator forces a block.
        if (hasTopLevelSemicolon) return true;

        int firstInner = open + 1;
        if (firstInner >= close) return false; // empty `()` — treat as single expr.

        var firstType = _tokens[firstInner].Type;

        // Rule 2: declaration-first block (keeps prior behavior).
        if (IsTypeKeyword(firstType)) return true;

        // Rule 3: identifier / call-head keyword ⇒ single call expression.
        if (firstType is TokenType.Identifier or TokenType.Tempo or TokenType.Swing
            or TokenType.Key or TokenType.Timesig or TokenType.Pan or TokenType.Gain)
        {
            return false;
        }

        // Rule 4: count top-level units inside the outer parens.
        int units = 0;
        int j = firstInner;
        while (j < close)
        {
            var tt = _tokens[j].Type;
            if (tt == TokenType.LParen || tt == TokenType.LBracket || tt == TokenType.LessLess)
            {
                // Skip the balanced group.
                int d = 0;
                while (j < close)
                {
                    var gt = _tokens[j].Type;
                    if (gt == TokenType.LParen || gt == TokenType.LBracket || gt == TokenType.LessLess) d++;
                    else if (gt == TokenType.RParen || gt == TokenType.RBracket || gt == TokenType.GreaterGreater)
                    {
                        d--;
                        if (d == 0) { j++; break; }
                    }
                    j++;
                }
            }
            else
            {
                // Single standalone token unit.
                j++;
            }
            units++;
            if (units >= 2) return true;
        }
        return units >= 2;
    }

    /// <summary>
    /// Parses a progression expression: progression [voices N] | I IV V |
    /// The 'progression' keyword has already been consumed.
    /// </summary>
    private Expression ParseProgressionExpression()
    {
        var location = PreviousToken.Location;

        // Optional: voices N modifier
        int? voiceCount = null;
        if (Check(TokenType.Identifier) && CurrentToken.Text == "voices")
        {
            Advance(); // consume "voices"
            var countToken = Expect(TokenType.IntLiteral, "Expected integer after 'voices'");
            voiceCount = (int)countToken.Value!;
        }

        Expect(TokenType.Pipe, "Expected '|' after 'progression' keyword");

        var chords = new List<ProgressionElement>();

        while (!Check(TokenType.Pipe) && !IsAtEnd())
        {
            var elemLocation = CurrentToken.Location;

            // Roman numerals are lexed as Identifier tokens
            if (!Check(TokenType.Identifier))
            {
                _errorReporter.ReportError(
                    $"Expected roman numeral in progression, got '{CurrentToken.Text}'",
                    CurrentToken.Location);
                Advance(); // skip bad token
                continue;
            }

            var numeralToken = Advance();
            string numeral = numeralToken.Text;

            // Validate it looks like a roman numeral
            if (!ScaleDatabase.IsRomanNumeral(numeral))
            {
                _errorReporter.ReportError(
                    $"'{numeral}' is not a valid roman numeral chord symbol",
                    numeralToken.Location);
            }

            // Optional :N bar count suffix
            int barCount = 1;
            if (Match(TokenType.Colon))
            {
                var countToken = Expect(TokenType.IntLiteral, "Expected integer after ':' in progression element");
                barCount = (int)countToken.Value!;
                if (barCount < 1)
                {
                    _errorReporter.ReportError(
                        "Bar count must be at least 1",
                        countToken.Location);
                    barCount = 1;
                }
            }

            chords.Add(new ProgressionElement(elemLocation, numeral, barCount));
        }

        Expect(TokenType.Pipe, "Expected closing '|' in progression");

        if (chords.Count == 0)
        {
            _errorReporter.ReportError("Progression must contain at least one chord", location);
        }

        return new ProgressionExpression(location, chords, voiceCount, Span: new Span(location, PreviousToken.Location));
    }

    /// <summary>
    /// Phase 35 Plan 35-05 (LANG-01) — parses the body of a
    /// <c>(match scrutinee | pat1 => body1 | pat2 => body2 | _ => default)</c>
    /// expression. The caller (<see cref="ParsePrimary"/>) has already consumed
    /// the opening <c>(</c> and the <c>match</c> keyword token; the
    /// <paramref name="openParenLocation"/> argument carries the open-paren
    /// SourceLocation so the resulting MatchExpression can pin a Span from
    /// the open paren through the closing paren.
    ///
    /// <para>
    /// Note-stream `|` disambiguation per Phase 35 RESEARCH §D.2 + Pitfall 2:
    /// inside this method, every <c>|</c> we see introduces an arm — it never
    /// delegates to <see cref="ParseNoteStream"/>. The disambiguator is the
    /// <c>(match</c> open paren in the caller, which establishes that we're
    /// in arm-parsing mode. <see cref="ParseNoteStream"/> only fires from a
    /// primary-expression-start position (see ParsePrimary's `Match(TokenType.Pipe)`
    /// branch), which an arm-delimiter `|` is NOT.
    /// </para>
    /// </summary>
    private Expression ParseMatch(SourceLocation openParenLocation)
    {
        var scrutinee = ParseExpression();

        var arms = new List<MatchArm>();
        while (Check(TokenType.Pipe) && !IsAtEnd())
        {
            var pipeToken = Advance(); // consume `|`
            var armLocation = pipeToken.Location;

            var pattern = ParsePattern();
            Expect(TokenType.FatArrow, "Expected '=>' after pattern in match arm");
            var body = ParseExpression();

            arms.Add(new MatchArm(
                pattern,
                body,
                Span: new Span(armLocation, PreviousToken.Location)));
        }

        Expect(TokenType.RParen, "Expected ')' to close match expression");

        return new MatchExpression(
            openParenLocation,
            scrutinee,
            arms,
            Span: new Span(openParenLocation, PreviousToken.Location),
            // Phase 35 Plan 35-06 (LANG-02 / D-v1.5-05): thread this parse
            // session's PragmaSet onto the MatchExpression so the evaluator's
            // non-exhaustive policy is driven by the file the MATCH was
            // PARSED IN (not the file that's currently evaluating). Pitfall 4
            // / Phase 21 D-06 — each imported file gets its own PragmaSet.
            CapturedPragmas: _pragmaSet);
    }

    /// <summary>
    /// Phase 35 Plan 35-05 (LANG-01) — parses a single Pattern node sitting
    /// between a match arm's leading <c>|</c> and its <c>=&gt;</c>. Recognized
    /// surface forms:
    ///
    /// <list type="bullet">
    ///   <item><description><c>_</c> — <see cref="WildcardPattern"/></description></item>
    ///   <item><description>Int / Float / String / Bool / Note literals —
    ///   <see cref="LiteralPattern"/></description></item>
    ///   <item><description>Bare identifier — <see cref="BindingPattern"/>.
    ///   If immediately followed by <c>when (...)</c> the binding is wrapped
    ///   in a <see cref="GuardPattern"/>.</description></item>
    ///   <item><description>Chord literal token (Cmaj7 / Dm) —
    ///   <see cref="ConstructorPattern"/> with <c>IsChordLiteral=true</c>.
    ///   Plan 35-06's PatternMatcher.MatchConstructor reads the flag and
    ///   dispatches to <c>ChordParser</c>; Plan 35-05's runtime falls through
    ///   to silent Void.</description></item>
    /// </list>
    ///
    /// All other token shapes produce a parser-error and a best-effort
    /// <see cref="WildcardPattern"/> recovery so the surrounding match keeps
    /// parsing — mirrors the rest of Parser.cs's recovery posture.
    /// </summary>
    /// <summary>
    /// Phase 36 Plan 36-10 (D-36-17 SECT-01) — parses a single Pattern node
    /// in section-parameter position. Recognized surface forms:
    ///
    /// <list type="bullet">
    ///   <item><description><c>Type name</c> — typed BindingPattern (the
    ///   common case: <c>Note root</c>, <c>Int repeats</c>).</description></item>
    ///   <item><description><c>&lt;&lt;Type name, Type name&gt;&gt;</c> —
    ///   ConstructorPattern with tuple-destructure flag set; binds each
    ///   inner slot as a typed BindingPattern.</description></item>
    ///   <item><description>Guard clause <c>pattern when (expr)</c> wraps the
    ///   inner pattern in a GuardPattern (D-36-17).</description></item>
    ///   <item><description>Otherwise falls through to the existing
    ///   <see cref="ParsePattern"/> entry point — match-arm patterns
    ///   (LiteralPattern, ConstructorPattern with music-aware extractors,
    ///   BindingPattern, etc.) all work transparently.</description></item>
    /// </list>
    /// </summary>
    private Pattern ParseSectionParameterPattern()
    {
        var location = CurrentToken.Location;

        // Tuple-destructure pattern: `<<Type name, Type name, ...>>`
        if (Match(TokenType.LessLess))
        {
            var subPatterns = new List<Pattern>();
            while (!Check(TokenType.GreaterGreater) && !IsAtEnd())
            {
                var subPattern = ParseSectionParameterPattern();
                subPatterns.Add(subPattern);
                if (!Check(TokenType.GreaterGreater))
                {
                    if (!Match(TokenType.Comma))
                    {
                        if (!Check(TokenType.GreaterGreater))
                            Expect(TokenType.Comma, "Expected ',' in tuple destructure pattern");
                    }
                }
            }
            Expect(TokenType.GreaterGreater, "Expected '>>' after tuple destructure pattern");
            var tuplePattern = new ConstructorPattern(
                location,
                "Tuple",
                subPatterns,
                Span: new Span(location, PreviousToken.Location));

            // Guard clause on tuple
            if (Match(TokenType.When))
            {
                var guardExpr = ParseExpression();
                return new GuardPattern(location, tuplePattern, guardExpr,
                    Span: new Span(location, PreviousToken.Location));
            }
            return tuplePattern;
        }

        // Typed binding: `Type identifier`
        if (IsTypeKeyword(CurrentToken.Type))
        {
            var (slotType, nextIdx, _) = Parsing.TypeParser.ParseType(_tokens, _current);
            _current = nextIdx;
            if (CurrentToken.Type == TokenType.Identifier)
            {
                var nameTok = Advance();
                var binding = new BindingPattern(
                    location,
                    nameTok.Text,
                    Span: new Span(location, PreviousToken.Location),
                    TypeAnnotation: slotType);

                if (Match(TokenType.When))
                {
                    var guardExpr = ParseExpression();
                    return new GuardPattern(location, binding, guardExpr,
                        Span: new Span(location, PreviousToken.Location));
                }
                return binding;
            }
            // If no identifier follows, fall through to the general parser
            // (rare and likely a syntax error — let ParsePattern report it).
            _current--;  // best-effort recover (rare)
        }

        // Fall through to the match-arm pattern grammar
        return ParsePattern();
    }

    private Pattern ParsePattern()
    {
        var startToken = CurrentToken;
        var location = startToken.Location;

        Pattern inner;

        if (Match(TokenType.Underscore))
        {
            inner = new WildcardPattern(location, Span: PreviousToken.EffectiveSpan);
        }
        else if (Match(TokenType.IntLiteral))
        {
            inner = new LiteralPattern(location, PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);
        }
        else if (Match(TokenType.FloatLiteral))
        {
            inner = new LiteralPattern(location, (double)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);
        }
        else if (Match(TokenType.StringLiteral))
        {
            inner = new LiteralPattern(location, (string)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);
        }
        else if (Match(TokenType.BoolLiteral))
        {
            inner = new LiteralPattern(location, (bool)PreviousToken.Value!, Span: PreviousToken.EffectiveSpan);
        }
        else if (Match(TokenType.NoteLiteral))
        {
            // Note literals store the raw text payload — matches Parser.cs's
            // existing LiteralExpression treatment so PatternMatcher's
            // Value.Equals path lines up with the scrutinee's stored string.
            inner = new LiteralPattern(location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);
        }
        else if (Match(TokenType.ChordLiteral))
        {
            // Plan 35-06 consumes IsChordLiteral=true to route through
            // ChordParser.Parse for chord-quality extraction.
            inner = new ConstructorPattern(
                location,
                PreviousToken.Text,
                new List<Pattern>(),
                Span: PreviousToken.EffectiveSpan)
            {
                IsChordLiteral = true,
            };
        }
        else if (Match(TokenType.SymbolLiteral))
        {
            // Phase 35 Plan 35-06 (LANG-02) — `#staccato` / `#legato` /
            // `#accent` etc. in pattern position become a
            // ConstructorPattern with IsArticulationSymbol=true. The lexer
            // already stripped the leading `#`, so PreviousToken.Text holds
            // just the symbol body. PatternMatcher.MatchArticulation maps
            // the body string to an <see cref="Articulation"/> enum value
            // and compares against the scrutinee's note articulation.
            //
            // sweep-0614: a `#symbol` pattern can match TWO kinds of scrutinee
            // and the parser can't know which the scrutinee will be:
            //   - a Symbol value (`#kick`, `#jazz`, even `#staccato`) → symbol
            //     equality on the interned name;
            //   - a MusicalNote scrutinee whose articulation is named by the
            //     symbol (`#staccato`, `#legato`, …) → articulation extractor.
            // Previously ALL symbols set ONLY IsArticulationSymbol, so a general
            // symbol (and any Symbol-typed scrutinee) silently fell through to
            // `| _ =>`. Now EVERY symbol literal sets IsSymbolLiteral; an
            // articulation-keyword name ADDITIONALLY sets IsArticulationSymbol.
            // PatternMatcher dispatches on the runtime scrutinee type.
            var symbolBody = PreviousToken.Text;
            inner = new ConstructorPattern(
                location,
                symbolBody,
                new List<Pattern>(),
                Span: PreviousToken.EffectiveSpan)
            {
                IsSymbolLiteral = true,
                IsArticulationSymbol = IsArticulationName(symbolBody),
            };
        }
        else if (Match(TokenType.Identifier))
        {
            // Phase 35 Plan 35-06 (LANG-02) — a roman-numeral identifier
            // (I / ii / V7 / vi / etc.) in pattern position becomes a
            // ConstructorPattern with IsRomanNumeral=true. The decision is
            // made at parse time using ScaleDatabase.IsRomanNumeral; the
            // ACTUAL resolution against the active key context happens at
            // match time inside PatternMatcher.MatchRomanNumeral, since
            // the key musical-context is not known until evaluation. When
            // the identifier is NOT a roman numeral, fall back to the
            // BindingPattern semantics from Plan 35-05.
            if (ScaleDatabase.IsRomanNumeral(PreviousToken.Text))
            {
                inner = new ConstructorPattern(
                    location,
                    PreviousToken.Text,
                    new List<Pattern>(),
                    Span: PreviousToken.EffectiveSpan)
                {
                    IsRomanNumeral = true,
                };
            }
            else
            {
                // Bare identifier captures the scrutinee as a binding.
                inner = new BindingPattern(location, PreviousToken.Text, Span: PreviousToken.EffectiveSpan);
            }
        }
        else
        {
            _errorReporter.ReportError(
                $"Unexpected token '{CurrentToken.Text}' in match pattern; expected literal, identifier, '_', chord, or #symbol",
                CurrentToken.Location);
            // Consume the offending token to avoid an infinite loop in the arm.
            if (!IsAtEnd()) Advance();
            return new WildcardPattern(location, Span: Span.At(location));
        }

        // Guard clause: `pattern when (...)` — the guard's GuardExpression runs
        // in the extended scope produced by `inner`'s bindings (so a sibling
        // BindingPattern's name is visible to the guard predicate).
        if (Match(TokenType.When))
        {
            var guardExpr = ParseExpression();
            return new GuardPattern(
                location,
                inner,
                guardExpr,
                Span: new Span(location, PreviousToken.Location));
        }

        return inner;
    }

    // Helper methods

    // sweep-0614: lowercase set of Articulation enum member names, computed once.
    // Used in pattern position to decide whether a `#symbol` is an articulation
    // keyword (→ IsArticulationSymbol) or a general symbol literal
    // (→ IsSymbolLiteral / Symbol-equality match).
    private static readonly HashSet<string> _articulationNames =
        new(Enum.GetNames<Articulation>().Select(n => n.ToLowerInvariant()));

    private static bool IsArticulationName(string symbolBody)
        => _articulationNames.Contains(symbolBody.ToLowerInvariant());

    private bool IsTypeKeyword(TokenType type)
    {
        if (type is TokenType.Void or TokenType.Int or TokenType.Float
            or TokenType.Long or TokenType.Double or TokenType.String
            or TokenType.Bool or TokenType.Number or TokenType.Buf)
        {
            return true;
        }

        // Function type: (Type, Type => Type)
        if (type == TokenType.LParen && TypeParser.LooksLikeFunctionType(_tokens, _current))
        {
            return true;
        }

        // Check for special types and plural forms (array types)
        if (type == TokenType.Identifier)
        {
            var text = CurrentToken.Text;

            // Special types
            if (text is "Buffer" or "Note" or "Bar" or "Semitone" or "Cent"
                or "Millisecond" or "Second" or "Decibel" or "Hertz" or "Lazy"
                or "MusicalNote" or "Function" or "Chord" or "Section" or "Song"
                or "OscillatorState" or "Envelope" or "Beat" or "Voice"
                or "Track" or "NoteValue" or "TimeSignature" or "Sequence"
                or "Symbol" or "Tuple"  // Phase 26.1 TUP-09 — `Tuple<<T1, T2>>` annotation gate
                or "Dict"  // Phase 26.1 DICT-01 — `Dict<K, V>` annotation gate
                or "Tuning"  // Phase 32 Plan 32-04 — `Tuning t = (loadScala ...)` annotation gate
                or "Sfz"  // Phase 33 Plan 33-05 — `Sfz v = (loadSfz #...)` annotation gate
                or "MarkovModel"  // Phase 36 Plan 36-06 — `MarkovModel m = (markovTrain ...)` annotation gate
                or "LsystemModel"  // Phase 36 Plan 36-07 — `LsystemModel m = (lsystemModel ...)` annotation gate
                or "OscHandle"  // Phase 38 Plan 38-06 — `OscHandle h = (oscListen ...)` annotation gate
                or "MidiDevice"  // Phase 40 Plan 40-01 — `MidiDevice dev = (openMidiOutput ...)` annotation gate (string-only check; safe on Web — @midi import rejected before any decl parses)
                or "ClockHandle"  // Phase 40 Plan 40-02 — `ClockHandle h = (clockMaster ...)` annotation gate + the `clockStop` midi.flow decl (string-only check; safe on Web)
                or "JackHandle")  // Phase 40 Plan 40-03 — `JackHandle h = (jackSync)` annotation gate + the jackSync jack.flow decl (string-only check; safe on Web)
                return true;

            // Plural forms (array types like Ints, Strings, etc.)
            if (text.EndsWith("s"))
            {
                var singular = text.Substring(0, text.Length - 1);
                if (singular is "Void" or "Int" or "Float" or "Long" or "Double"
                    or "String" or "Bool" or "Number" or "Buf" or "Buffer"
                    or "Note" or "Bar" or "Semitone" or "Cent" or "Millisecond" or "Second" or "Decibel"
                    or "Hertz"
                    or "MusicalNote" or "Function" or "Chord" or "Section" or "Song"
                    or "OscillatorState" or "Envelope" or "Beat" or "Voice"
                    or "Track" or "NoteValue" or "TimeSignature" or "Sequence"
                    or "Symbol")
                    return true;
            }
        }

        return false;
    }

    private bool IsArgumentStart(TokenType type)
    {
        // For optional parentheses syntax: identifiers and literals can be arguments
        // Note: LParen is intentionally excluded here despite being an unambiguous
        // expression start. Including it would cause identifiers followed by parenthesized
        // expressions to be misinterpreted as function calls (e.g., `xs (fn ...)` inside
        // `(each xs (fn ...))`). Use explicit function call syntax instead: (func (expr))
        return type is TokenType.IntLiteral
            or TokenType.FloatLiteral
            or TokenType.StringLiteral
            or TokenType.BoolLiteral
            or TokenType.NoteLiteral
            or TokenType.SemitoneLiteral
            or TokenType.CentLiteral
            or TokenType.TimeLiteral
            or TokenType.DecibelLiteral
            or TokenType.HertzLiteral
            or TokenType.BeatLiteral  // Phase 45 D-09 / REQ-BEAT-AST-03 — Nb at expression-start + as arg
            or TokenType.ChordLiteral
            or TokenType.SymbolLiteral
            or TokenType.InterpolatedStringStart
            or TokenType.Identifier;
    }

    /// <summary>
    /// Phase 35 Plan 35-07 (LANG-03) — consumes an optional `as Identifier`
    /// clause appearing after a flow-chain step's RHS. Returns the identifier
    /// text when present, null otherwise. Per RESEARCH OQ5 (RESOLVED 2026-05-18):
    /// `as` must be followed by an Identifier; emits a parse error and returns
    /// null when the next token is anything else (caller's chain continues
    /// without binding for graceful recovery).
    /// </summary>
    private string? TryConsumeAsClause()
    {
        if (!Match(TokenType.As)) return null;
        if (!Check(TokenType.Identifier))
        {
            _errorReporter.ReportError(
                $"Expected identifier after `as` in flow chain, got {CurrentToken.Type} '{CurrentToken.Text}'",
                CurrentToken.Location);
            return null;
        }
        return Advance().Text;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return CurrentToken.Type == type;
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return PreviousToken;
    }

    private Token Expect(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw new ParseException($"{message}. Got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    }

    private Token ExpectParameterName(string errorMessage = "Expected parameter name")
    {
        if (Check(TokenType.Identifier) || Check(TokenType.Tempo) || Check(TokenType.Swing)
            || Check(TokenType.Key) || Check(TokenType.Timesig) || Check(TokenType.Pan)
            || Check(TokenType.Gain))
            return Advance();
        return Expect(TokenType.Identifier, errorMessage);
    }

    private bool IsAtEnd() => CurrentToken.Type == TokenType.Eof;

    private Token CurrentToken => _current < _tokens.Count ? _tokens[_current] : _tokens[^1];
    private Token PreviousToken => _tokens[_current - 1];

    private void Synchronize()
    {
        Advance();

        while (!IsAtEnd())
        {
            if (PreviousToken.Type == TokenType.EndProc) return;

            // Also sync on semicolons
            if (PreviousToken.Type == TokenType.Semicolon) return;

            if (CurrentToken.Type is TokenType.Proc or TokenType.Return
                or TokenType.Use or TokenType.Internal
                or TokenType.Timesig or TokenType.Tempo
                or TokenType.Swing or TokenType.Key
                or TokenType.Dynamics or TokenType.Rit or TokenType.Accel
                or TokenType.Pan or TokenType.Section)
            {
                return;
            }

            Advance();
        }
    }
}
