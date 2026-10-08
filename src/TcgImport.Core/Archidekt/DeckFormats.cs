namespace TcgImport.Core.Archidekt;

/// <summary>Archidekt's numeric deck formats and Commander brackets, as defined in archidekt.com's own scripts.</summary>
public static class DeckFormats
{
    public static IReadOnlyDictionary<int, string> Names { get; } = new Dictionary<int, string>
    {
        [1] = "Standard",
        [2] = "Modern",
        [3] = "Commander",
        [4] = "Legacy",
        [5] = "Vintage",
        [6] = "Pauper",
        [7] = "Custom",
        [8] = "Frontier",
        [9] = "Future Standard",
        [10] = "Penny Dreadful",
        [11] = "1v1 Commander",
        [12] = "Duel Commander",
        [13] = "Standard Brawl",
        [14] = "Oathbreaker",
        [15] = "Pioneer",
        [16] = "Historic",
        [17] = "Pauper EDH",
        [18] = "Alchemy",
        [19] = "Pioneer (Prev. Explorer)",
        [20] = "Brawl",
        [21] = "Gladiator",
        [22] = "Premodern",
        [23] = "PreDH",
        [24] = "Timeless",
        [25] = "Canadian Highlander",
        [26] = "Competitive Brawl",
        [27] = "Tiny Leaders Reborn",
    };

    public static IReadOnlyDictionary<int, string> Brackets { get; } = new Dictionary<int, string>
    {
        [1] = "Exhibition (1)",
        [2] = "Core (2)",
        [3] = "Upgraded (3)",
        [4] = "Optimized (4)",
        [5] = "cEDH (5)",
    };

    public static string Name(int? code) =>
        code is null ? "No format"
        : Names.TryGetValue(code.Value, out var name) ? name
        : $"Format {code}";
}
