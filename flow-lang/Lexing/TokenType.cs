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
    Tuning,             // Phase 32 (SPEC-2) — tuning <expr> { ... } musical-context block (D-13)
    Pickup,
    For,
    While,
    Break,
    Continue,
    In,
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
    Eof
}
