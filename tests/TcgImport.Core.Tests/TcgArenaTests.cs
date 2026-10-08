using System.Text;
using TcgImport.Core.Archidekt;
using TcgImport.Core.TcgArena;

namespace TcgImport.Core.Tests;

public class TcgArenaTests
{
    private static ArchidektDeck Deck(params DeckCard[] cards) =>
        new(new DeckSummary(1, "Test", "me", 3, DateTimeOffset.UnixEpoch, null, ""), cards);

    [Fact]
    public void Decklist_has_commander_main_and_sideboard_blocks()
    {
        var deck = Deck(
            new("Esika, God of the Tree // The Prismatic Bridge", 1, DeckSection.Commander),
            new("Sol Ring", 1, DeckSection.Main),
            new("Forest", 3, DeckSection.Main),
            new("Black Lotus", 1, DeckSection.Excluded),
            new("Duress", 2, DeckSection.Sideboard));

        var list = TcgArenaDecklist.FromDeck(deck);

        Assert.Equal(
            "Commander\n1 Esika, God of the Tree // The Prismatic Bridge\n\n1 Sol Ring\n3 Forest\n\nSideboard\n2 Duress",
            list.Text);
        Assert.Equal(1, list.CommanderCount);
        Assert.Equal(4, list.MainCount);
        Assert.Equal(2, list.SideboardCount);
        Assert.Equal(5, list.DeckTotal);
    }

    [Fact]
    public void Decklist_merges_printings_of_the_same_card()
    {
        var list = TcgArenaDecklist.FromDeck(Deck(
            new("Island", 4, DeckSection.Main),
            new("Island", 3, DeckSection.Main)));

        Assert.Equal("7 Island", list.Text);
    }

    [Fact]
    public void Decklist_without_commander_has_no_heading()
    {
        var list = TcgArenaDecklist.FromDeck(Deck(new DeckCard("Lightning Bolt", 4, DeckSection.Main)));

        Assert.Equal("4 Lightning Bolt", list.Text);
    }

    [Fact]
    public void Import_link_decodes_the_way_TCG_Arena_does()
    {
        const string decklist = "Commander\n1 Lim-Dûl the Necromancer\n\n1 Fire // Ice";

        var url = TcgArenaLink.ForImport("Xavier & friends", TcgArenaLink.DeckId(27052078), decklist);

        var uri = new Uri(url);
        Assert.Equal("https://tcg-arena.fr/import", uri.GetLeftPart(UriPartial.Path));
        var query = ParseQuery(uri.Query);
        Assert.Equal("Magic the Gathering", query["game"]);
        Assert.Equal("Xavier & friends", query["name"]);
        Assert.Equal("archidekt-27052078", query["id"]);

        // TCG Arena: atob(decodeURIComponent(searchParams.get("deck")))
        var base64 = Uri.UnescapeDataString(query["deck"]);
        Assert.Equal(decklist, Encoding.Latin1.GetString(Convert.FromBase64String(base64)));
    }

    // Mirrors URLSearchParams.get: split on & and =, then percent-decode once.
    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
}
