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

    // Operators
    Arrow,              // ->
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

    // Delimiters
    LParen,             // (
    RParen,             // )
    LBracket,           // [
    RBracket,           // ]
    LBrace,             // {
    RBrace,             // }
    Comma,              // ,
    Semicolon,          // ;
    Ellipsis,           // ...

    // Other
    Identifier,
    Comment,
    Eof
}
