using System.Text;

namespace TcgImport.Core.TcgArena;

/// <summary>
/// Builds links to TCG Arena's import page: /import?game=..&amp;id=..&amp;name=..&amp;deck=..
/// The page shows the deck and saves it when the user clicks Import. Importing again with the
/// same id replaces the earlier copy.
/// </summary>
public static class TcgArenaLink
{
    public const string BaseUrl = "https://tcg-arena.fr";

    /// <summary>Must match the name of the MTG game mod added in TCG Arena.</summary>
    public const string MtgGameName = "Magic the Gathering";

    public static string DeckId(int archidektDeckId) => $"archidekt-{archidektDeckId}";

    public static string ForImport(string deckName, string deckId, string decklistText)
    {
        // TCG Arena decodes with atob(decodeURIComponent(param)), mirroring its own
        // encodeURIComponent(btoa(decklist)). btoa works on Latin-1, so encode the same way.
        var base64 = Convert.ToBase64String(Encoding.Latin1.GetBytes(decklistText));
        var deck = Uri.EscapeDataString(base64);

        return $"{BaseUrl}/import?" + string.Join("&",
            Param("game", MtgGameName),
            Param("name", deckName),
            Param("id", deckId),
            Param("deck", deck));
    }

    private static string Param(string key, string value) => $"{key}={Uri.EscapeDataString(value)}";
}
