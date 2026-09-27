namespace Flow.Music.IO;

/// <summary>Flow key names → SMF key signature (sharps positive, flats negative; minor = 1).</summary>
public static class KeySignatures
{
    public static IReadOnlyDictionary<string, (sbyte sharpsFlats, byte minor)> Map { get; } =
        new Dictionary<string, (sbyte, byte)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cmajor"] = (0, 0), ["Gmajor"] = (1, 0), ["Dmajor"] = (2, 0), ["Amajor"] = (3, 0),
            ["Emajor"] = (4, 0), ["Bmajor"] = (5, 0), ["Fsharpmajor"] = (6, 0), ["Csharpmajor"] = (7, 0),
            ["Fmajor"] = (-1, 0), ["Bbmajor"] = (-2, 0), ["Ebmajor"] = (-3, 0), ["Abmajor"] = (-4, 0),
            ["Dbmajor"] = (-5, 0), ["Gbmajor"] = (-6, 0),
            ["Aminor"] = (0, 1), ["Eminor"] = (1, 1), ["Bminor"] = (2, 1), ["Fsharpminor"] = (3, 1),
            ["Csharpminor"] = (4, 1), ["Gsharpminor"] = (5, 1), ["Dsharpminor"] = (6, 1), ["Asharpminor"] = (7, 1),
            ["Dminor"] = (-1, 1), ["Gminor"] = (-2, 1), ["Cminor"] = (-3, 1), ["Fminor"] = (-4, 1),
            ["Bbminor"] = (-5, 1), ["Ebminor"] = (-6, 1),
            // Enharmonic spellings accepted by Flow key names.
            ["Dsharpmajor"] = (-3, 0), ["Gsharpmajor"] = (-4, 0), ["Asharpmajor"] = (-2, 0),
            ["Dbminor"] = (-5, 1), ["Gbminor"] = (-6, 1), ["Abminor"] = (-4, 1),
        };
}
