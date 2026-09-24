namespace FlowLang.TypeSystem;

/// <summary>
/// Hand-rolled rational arithmetic primitive (FRAC-01). Sibling of ArrayType.cs;
/// helper struct, NOT a FlowType — users never write `Fraction f = ...` in .flow
/// source. C# implementation detail consumed by MusicalNoteData.DurationFraction
/// (Phase 18 wiring) and Phase 19 tuplet duration math.
///
/// Constructor normalizes via GCD so 2/4 == 1/2 and 3/12 == 1/4 compare equal
/// via record-struct value-equality. Sign carried on numerator (D-USER-03).
/// Zero denominator throws DivideByZeroException eagerly.
///
/// Per RESEARCH Pitfall 4: int Num/Denom is sufficient for Phase 18+19 tuplet
/// ratios. Denominators stay single/double digits (3, 4, 12, max ~13 per
/// v1.3 D-05 TPQN cap). Switch to long is a single-line edit if needed.
///
/// Per D-USER-03: ToString always emits "Num/Denom" (no special-casing 1/1).
/// </summary>
public readonly record struct Fraction
{
    public int Num { get; }
    public int Denom { get; }

    public Fraction(int num, int denom)
    {
        if (denom == 0)
            throw new DivideByZeroException("Fraction denominator cannot be zero.");
        // Normalize sign onto numerator
        if (denom < 0) { num = -num; denom = -denom; }
        int g = Gcd(Math.Abs(num), denom);
        Num = num / g;
        Denom = denom / g;
    }

    /// <summary>
    /// Recursive Euclidean GCD. Mirrors flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs:117
    /// for stylistic consistency with the existing codebase idiom.
    /// </summary>
    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    public static Fraction operator +(Fraction l, Fraction r) =>
        new(l.Num * r.Denom + r.Num * l.Denom, l.Denom * r.Denom);

    public static Fraction operator *(Fraction l, Fraction r) =>
        new(l.Num * r.Num, l.Denom * r.Denom);

    public static bool operator <(Fraction l, Fraction r) =>
        l.Num * r.Denom < r.Num * l.Denom;

    public static bool operator >(Fraction l, Fraction r) => r < l;

    /// <summary>
    /// Always emits "Num/Denom" form per D-USER-03. Predictable and parseable;
    /// Phase 19 tuplet diagnostic prose can wrap it (e.g. "tuplet ratio 3/2").
    /// </summary>
    public override string ToString() => $"{Num}/{Denom}";
}
