namespace TcgImport.Core.Archidekt;

/// <summary>
/// Filters for Archidekt's deck search (/api/decks/v3/), matching the options on archidekt.com/search/decks.
/// Commander and card names must be exact, full card names ("Esika, God of the Tree // The Prismatic Bridge").
/// </summary>
public sealed record DeckSearchFilter
{
    public string? Name { get; init; }
    public string? OwnerUsername { get; init; }
    public int? DeckFormat { get; init; }
    public int? EdhBracket { get; init; }

    /// <summary>Colour letters in any order, e.g. "UBG".</summary>
    public string? Colors { get; init; }

    /// <summary>False: deck colours must match exactly. True: deck must include these colours.</summary>
    public bool ColorsInclude { get; init; }

    public string? CommanderName { get; init; }
    public string? CardName { get; init; }
    public DeckSortOrder OrderBy { get; init; } = DeckSortOrder.RecentlyUpdated;

    public bool HasCriteria =>
        !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(OwnerUsername) ||
        DeckFormat is not null || EdhBracket is not null || !string.IsNullOrWhiteSpace(Colors) ||
        !string.IsNullOrWhiteSpace(CommanderName) || !string.IsNullOrWhiteSpace(CardName);

    public string ToQueryString(int page, int pageSize)
    {
        var query = new List<(string, string)>
        {
            ("orderBy", OrderByValue(OrderBy)),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
        };
        AddIfSet(query, "name", Name);
        AddIfSet(query, "ownerUsername", OwnerUsername);
        AddIfSet(query, "deckFormat", DeckFormat?.ToString());
        AddIfSet(query, "edhBracket", EdhBracket?.ToString());
        AddIfSet(query, "commanderName", CommanderName);
        AddIfSet(query, "cardName", CardName);

        var colors = ColorLetters(Colors);
        if (colors.Length > 0)
        {
            query.Add(("colors", string.Join(",", colors.ToCharArray())));
            if (ColorsInclude) query.Add(("colorsInclude", "true"));
        }

        return string.Join("&", query.Select(p => $"{p.Item1}={Uri.EscapeDataString(p.Item2)}"));
    }

    private static void AddIfSet(List<(string, string)> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) query.Add((key, value.Trim()));
    }

    private static string ColorLetters(string? colors) =>
        colors is null ? "" : new string("WUBRG".Where(c => colors.Contains(c, StringComparison.OrdinalIgnoreCase)).ToArray());

    private static string OrderByValue(DeckSortOrder order) => order switch
    {
        DeckSortOrder.RecentlyCreated => "-createdAt",
        DeckSortOrder.Name => "name",
        DeckSortOrder.MostViewed => "-viewCount",
        DeckSortOrder.Largest => "-size",
        _ => "-updatedAt",
    };
}

public enum DeckSortOrder
{
    RecentlyUpdated,
    RecentlyCreated,
    Name,
    MostViewed,
    Largest,
}
