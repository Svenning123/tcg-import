using System.Net;
using System.Text.RegularExpressions;

namespace TcgImport.Core.Archidekt;

/// <summary>Reads public and unlisted decks from Archidekt's (unofficial) JSON API.</summary>
public sealed partial class ArchidektClient(HttpClient http)
{
    private const string BaseUrl = "https://archidekt.com/api/decks/";
    public const int PageSize = 30;

    public async Task<DeckSearchPage> SearchAsync(DeckSearchFilter filter, int page, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"{BaseUrl}v3/?{filter.ToQueryString(page, PageSize)}", ct);
        // Archidekt answers 404 for a page past the end of the results.
        if (response.StatusCode == HttpStatusCode.NotFound && page > 1)
            return new DeckSearchPage([], 0, HasMore: false);
        response.EnsureSuccessStatusCode();
        return ArchidektJson.ParseSearch(await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<ArchidektDeck> GetDeckAsync(int deckId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"{BaseUrl}{deckId}/", ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new DeckUnavailableException(deckId);
        response.EnsureSuccessStatusCode();
        return ArchidektJson.ParseDeck(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Accepts a bare deck id or any archidekt.com/decks/{id}/... link.</summary>
    public static bool TryParseDeckId(string? input, out int deckId)
    {
        deckId = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim();
        if (int.TryParse(text, out deckId)) return deckId > 0;

        var match = DeckUrlRegex().Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out deckId);
    }

    [GeneratedRegex(@"archidekt\.com/(?:api/)?decks/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeckUrlRegex();
}

public sealed class DeckUnavailableException(int deckId)
    : Exception($"Archidekt deck {deckId} doesn't exist or is private. Only public and unlisted decks can be imported.")
{
    public int DeckId { get; } = deckId;
}
