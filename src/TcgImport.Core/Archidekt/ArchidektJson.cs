using System.Text.Json;

namespace TcgImport.Core.Archidekt;

/// <summary>Parses Archidekt's undocumented API responses into our models.</summary>
public static class ArchidektJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private const string ColorOrder = "WUBRG";

    private static readonly Dictionary<string, char> ColorLetters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["White"] = 'W', ["Blue"] = 'U', ["Black"] = 'B', ["Red"] = 'R', ["Green"] = 'G',
    };

    public static DeckSearchPage ParseSearch(string json)
    {
        var response = JsonSerializer.Deserialize<SearchResponse>(json, Options)
            ?? throw new FormatException("Empty search response from Archidekt.");

        // Private decks only show up when signed in as their owner.
        var decks = response.Results
            .Select(d => new DeckSummary(
                d.Id,
                d.Name,
                d.Owner?.Username ?? "",
                d.DeckFormat,
                d.UpdatedAt,
                ImageUrl(d.CustomFeatured, d.Featured),
                ColorsFromCounts(d.Colors),
                d.Private))
            .ToList();

        // Archidekt answers count -1 when a commander or card filter doesn't name an existing card.
        return response.Count < 0
            ? new DeckSearchPage([], 0, HasMore: false, UnknownCardName: true)
            : new DeckSearchPage(decks, response.Count, HasMore: response.Next is not null);
    }

    /// <summary>
    /// Distinct card names from /api/cards/v2/. Archidekt also matches translated names, so names containing
    /// the search text come first.
    /// </summary>
    public static IReadOnlyList<string> ParseCardNames(string json, string searchText, bool legendaryFirst)
    {
        var response = JsonSerializer.Deserialize<CardSearchResponse>(json, Options)
            ?? throw new FormatException("Empty card search response from Archidekt.");
        var query = searchText.Trim();

        return response.Results
            .Select(c => c.OracleCard)
            .Where(o => o is { Name.Length: > 0 })
            .DistinctBy(o => o!.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(o => legendaryFirst && (o!.SuperTypes?.Contains("Legendary", StringComparer.OrdinalIgnoreCase) ?? false))
            .ThenByDescending(o => o!.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(o => o!.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(o => o!.Name)
            .ToList();
    }

    public static ArchidektDeck ParseDeck(string json)
    {
        var deck = JsonSerializer.Deserialize<DeckResponse>(json, Options)
            ?? throw new FormatException("Empty deck response from Archidekt.");

        var categories = deck.Categories
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var cards = deck.Cards
            .Where(c => c.DeletedAt is null && c.Quantity > 0 && c.Card?.OracleCard?.Name is { Length: > 0 })
            .Select(c => new DeckCard(c.Card!.OracleCard!.Name, c.Quantity, SectionOf(c, categories), c.Card.Uid))
            .ToList();

        var colorIdentity = deck.Cards
            .Where(c => c.DeletedAt is null && SectionOf(c, categories) is DeckSection.Commander or DeckSection.Main)
            .SelectMany(c => c.Card?.OracleCard?.ColorIdentity ?? []);

        var summary = new DeckSummary(
            deck.Id,
            deck.Name,
            deck.Owner?.Username ?? "",
            deck.DeckFormat,
            deck.UpdatedAt,
            ImageUrl(deck.CustomFeatured, deck.Featured),
            ColorsFromNames(colorIdentity),
            deck.Private);

        return new ArchidektDeck(summary, cards);
    }

    private static DeckSection SectionOf(CardEntryDto card, Dictionary<string, CategoryDto> categories)
    {
        var names = card.Categories ?? [];
        if (names.Any(n => categories.TryGetValue(n, out var c) && c.IsPremier))
            return DeckSection.Commander;

        // Archidekt treats the first category as the card's primary one.
        var primary = names.FirstOrDefault();
        if (card.Companion || string.Equals(primary, "Sideboard", StringComparison.OrdinalIgnoreCase))
            return DeckSection.Sideboard;
        if (primary is not null && categories.TryGetValue(primary, out var category) && !category.IncludedInDeck)
            return DeckSection.Excluded;
        return DeckSection.Main;
    }

    private const string ArchidektArtPrefix = "https://card-images.archidekt.com/art/front/";
    private const string ScryfallArtPrefix = "https://cards.scryfall.io/art_crop/front/";

    private static string? ImageUrl(string? custom, string? featured)
    {
        var url = !string.IsNullOrWhiteSpace(custom) ? custom
            : !string.IsNullOrWhiteSpace(featured) ? featured
            : null;

        // Archidekt serves card art as WebP, which WPF can't decode without an extra codec.
        // Its paths mirror Scryfall's, so use Scryfall's JPEG art crop of the same card instead.
        if (url is not null && url.StartsWith(ArchidektArtPrefix, StringComparison.OrdinalIgnoreCase)
            && url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return ScryfallArtPrefix + url[ArchidektArtPrefix.Length..^".webp".Length] + ".jpg";
        }
        return url;
    }

    private static string ColorsFromCounts(Dictionary<string, int>? counts) =>
        counts is null ? "" : new string(ColorOrder.Where(c => counts.GetValueOrDefault(c.ToString()) > 0).ToArray());

    private static string ColorsFromNames(IEnumerable<string> names)
    {
        var letters = names.Select(n => ColorLetters.GetValueOrDefault(n)).ToHashSet();
        return new string(ColorOrder.Where(letters.Contains).ToArray());
    }

    private sealed class SearchResponse
    {
        public int Count { get; set; }
        public string? Next { get; set; }
        public List<DeckListItemDto> Results { get; set; } = [];
    }

    private sealed class DeckListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? DeckFormat { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? Featured { get; set; }
        public string? CustomFeatured { get; set; }
        public bool Private { get; set; }
        public OwnerDto? Owner { get; set; }
        public Dictionary<string, int>? Colors { get; set; }
    }

    private sealed class DeckResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? DeckFormat { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? Featured { get; set; }
        public string? CustomFeatured { get; set; }
        public bool Private { get; set; }
        public OwnerDto? Owner { get; set; }
        public List<CategoryDto> Categories { get; set; } = [];
        public List<CardEntryDto> Cards { get; set; } = [];
    }

    private sealed class OwnerDto
    {
        public string Username { get; set; } = "";
    }

    private sealed class CategoryDto
    {
        public string Name { get; set; } = "";
        public bool IncludedInDeck { get; set; }
        public bool IsPremier { get; set; }
    }

    private sealed class CardEntryDto
    {
        public int Quantity { get; set; }
        public List<string>? Categories { get; set; }
        public bool Companion { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public CardDto? Card { get; set; }
    }

    private sealed class CardDto
    {
        /// <summary>Scryfall id of the chosen printing.</summary>
        public string? Uid { get; set; }
        public OracleCardDto? OracleCard { get; set; }
    }

    private sealed class CardSearchResponse
    {
        public List<CardDto> Results { get; set; } = [];
    }

    private sealed class OracleCardDto
    {
        public string Name { get; set; } = "";
        public List<string>? SuperTypes { get; set; }
        public List<string>? ColorIdentity { get; set; }
    }
}

public sealed record DeckSearchPage(IReadOnlyList<DeckSummary> Decks, int TotalCount, bool HasMore, bool UnknownCardName = false);
