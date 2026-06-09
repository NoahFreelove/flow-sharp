namespace FlowLang.Lexing;

/// <summary>
/// Types of tokens in the Flow language.
/// </summary>
public enum TokenType
{
    // Keywords
    Proc,
    EndProc,
    Return,
    Use,
    Note,
    Internal,
    Lazy,
    Fn,
    Timesig,
    Tempo,
    Swing,
    Key,
    Section,
    Dynamics,
    Rit,
    Accel,
    Pan,
    Gain,
    ReverbTime,
    VoicePool,          // Phase 28 (SPEC-7) — voicePool N { ... } musical-context block
    SustainPedal,       // sustainPedal { ... } musical-context block — extends note durations
    Tuning,             // Phase 32 (SPEC-2) — tuning <expr> { ... } musical-context block (D-13)
    Module,             // Phase 43 (D-03) — module <name> top-of-file declaration
    Live,               // Phase 38 (LIVE-01) — live <quantize> { ... } block (D-38-02)
    Match,              // Phase 35 Plan 35-05 (LANG-01) — (match scrutinee | pat => body | ...)
    When,               // Phase 35 Plan 35-05 (LANG-01) — guard clause: `| n when (...) => ...`
    Pickup,
    For,
    While,
    Break,
    Continue,
    In,
    As,                 // Phase 35 Plan 35-07 (LANG-03) — `-> CALL as NAME` chain naming
    Progression,

    // Type keywords
    Void,
    Int,
    Float,
    Long,
    Double,
    String,
    Bool,
    Number,
    Buf,  // Special type for audio buffers (used in examples)

    // Literals
    IntLiteral,
    FloatLiteral,
    StringLiteral,
    BoolLiteral,
    NoteLiteral,        // A+, C--, etc.
    SemitoneLiteral,    // +1st, -5st
    CentLiteral,        // +50c, -25c (microtones)
    TimeLiteral,        // 100ms, 2.5s
    DecibelLiteral,     // -3dB, +6dB
    HertzLiteral,       // 800Hz, 1.5kHz (Phase 26.2 ERG-04)
    ChordLiteral,       // Cmaj7, Dm, Gsus4
    SymbolLiteral,      // #foo (Phase 26.1 SYM-01) — the leading '#' is a token boundary; lexeme is the body without '#'
    BeatLiteral,        // 0.5b, 2b, +1b, -2b (Phase 45 D-06/D-07) — eval-time pragma multiplier in ExpressionEvaluator.EvaluateBeatLiteral
    InterpolatedStringStart,   // $"
    InterpolatedStringEnd,     // " (closing an interpolated string)
    InterpolatedStringText,    // Text segments between { } in interpolated strings

    // Operators
    Arrow,              // ->
    TildeArrow,         // ~> (Phase 26.1 TUP-10 — tuple-unpack flow operator)
    FatArrow,           // =>
    Dot,                // .
    At,                 // @
    Assign,             // =
    Colon,              // :
    Plus,               // +
    Minus,              // -
    Star,               // *
    Slash,              // /
    LessThan,           // <
    GreaterThan,        // >
    LessLess,           // << (Phase 26.1 TUP-09 — tuple-literal opening; emitted only at expression-start positions)
    GreaterGreater,     // >> (Phase 26.1 TUP-09 — tuple-literal closing; PeekNext-equality gate also protects note-stream `>` accent)

    // Delimiters
    LParen,             // (
    RParen,             // )
    LBracket,           // [
    RBracket,           // ]
    LBrace,             // {
    RBrace,             // }
    Pipe,               // | (note stream bar delimiter)
    Underscore,         // _ (rest in note stream)
    Tilde,              // ~ (tie between notes)
    Comma,              // ,
    Semicolon,          // ;
    Ellipsis,           // ...

    // Other
    Identifier,
    Comment,
    DocComment,         // Phase 41 (DOC-01) — `/// summary` captured for the following proc (D-07 additive grammar)
    Eof
}
