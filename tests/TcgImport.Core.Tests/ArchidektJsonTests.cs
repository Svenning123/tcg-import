using TcgImport.Core.Archidekt;

namespace TcgImport.Core.Tests;

public class ArchidektJsonTests
{
    // Trimmed-down shape of https://archidekt.com/api/decks/{id}/
    private const string DeckJson = """
        {
          "id": 27052078, "name": "Xavier", "deckFormat": 3,
          "updatedAt": "2026-10-04T09:44:25.130913Z",
          "featured": "https://img/featured.webp", "customFeatured": "",
          "owner": { "username": "ISummonPotOfGreed" },
          "categories": [
            { "name": "Commander", "includedInDeck": true, "isPremier": true },
            { "name": "Ramp", "includedInDeck": true, "isPremier": false },
            { "name": "Maybeboard", "includedInDeck": false, "isPremier": false },
            { "name": "Sideboard", "includedInDeck": true, "isPremier": false }
          ],
          "cards": [
            { "quantity": 1, "categories": ["Commander"], "companion": false, "deletedAt": null,
              "card": { "oracleCard": { "name": "The Legend of Kyoshi // Avatar Kyoshi", "colorIdentity": ["Green"] } } },
            { "quantity": 1, "categories": ["Ramp", "Commander"], "companion": false, "deletedAt": null,
              "card": { "oracleCard": { "name": "Partner Commander", "colorIdentity": ["Blue"] } } },
            { "quantity": 3, "categories": ["Ramp"], "companion": false, "deletedAt": null,
              "card": { "oracleCard": { "name": "Forest", "colorIdentity": [] } } },
            { "quantity": 1, "categories": ["Maybeboard"], "companion": false, "deletedAt": null,
              "card": { "oracleCard": { "name": "Black Lotus", "colorIdentity": [] } } },
            { "quantity": 2, "categories": ["Sideboard"], "companion": false, "deletedAt": null,
              "card": { "oracleCard": { "name": "Duress", "colorIdentity": ["Black"] } } },
            { "quantity": 1, "categories": [], "companion": true, "deletedAt": null,
              "card": { "oracleCard": { "name": "Lurrus of the Dream-Den", "colorIdentity": ["White", "Black"] } } },
            { "quantity": 1, "categories": ["Ramp"], "companion": false, "deletedAt": "2026-10-01T00:00:00Z",
              "card": { "oracleCard": { "name": "Removed Card", "colorIdentity": ["Red"] } } },
            { "quantity": 1, "categories": null, "companion": false, "deletedAt": null,
              "card": { "oracleCard": { "name": "Uncategorised", "colorIdentity": [] } } }
          ]
        }
        """;

    [Fact]
    public void ParseDeck_assigns_sections_from_categories()
    {
        var deck = ArchidektJson.ParseDeck(DeckJson);

        var sections = deck.Cards.ToDictionary(c => c.Name, c => c.Section);
        Assert.Equal(DeckSection.Commander, sections["The Legend of Kyoshi // Avatar Kyoshi"]);
        Assert.Equal(DeckSection.Commander, sections["Partner Commander"]);
        Assert.Equal(DeckSection.Main, sections["Forest"]);
        Assert.Equal(DeckSection.Main, sections["Uncategorised"]);
        Assert.Equal(DeckSection.Excluded, sections["Black Lotus"]);
        Assert.Equal(DeckSection.Sideboard, sections["Duress"]);
        Assert.Equal(DeckSection.Sideboard, sections["Lurrus of the Dream-Den"]);
        Assert.DoesNotContain("Removed Card", sections.Keys);
    }

    [Fact]
    public void ParseDeck_builds_summary()
    {
        var summary = ArchidektJson.ParseDeck(DeckJson).Summary;

        Assert.Equal(27052078, summary.Id);
        Assert.Equal("Xavier", summary.Name);
        Assert.Equal("ISummonPotOfGreed", summary.OwnerUsername);
        Assert.Equal("Commander", summary.FormatName);
        Assert.Equal("https://img/featured.webp", summary.ImageUrl);
        // Only commander and main deck count toward colour identity.
        Assert.Equal("UG", summary.Colors);
    }

    [Fact]
    public void ParseSearch_skips_private_decks_and_reads_paging()
    {
        const string json = """
            {
              "count": 2, "next": "http://archidekt.com/api/decks/v3/?page=2",
              "results": [
                { "id": 1, "name": "Public", "deckFormat": 2, "updatedAt": "2026-10-01T00:00:00Z",
                  "featured": "f.webp", "customFeatured": "custom.webp", "private": false,
                  "owner": { "username": "me" }, "colors": { "W": 0, "U": 4, "B": 0, "R": 2, "G": 0 } },
                { "id": 2, "name": "Hidden", "deckFormat": 2, "updatedAt": "2026-10-01T00:00:00Z",
                  "private": true, "owner": { "username": "me" } }
              ]
            }
            """;

        var page = ArchidektJson.ParseSearch(json);

        var deck = Assert.Single(page.Decks);
        Assert.Equal("Public", deck.Name);
        Assert.Equal("custom.webp", deck.ImageUrl);
        Assert.Equal("UR", deck.Colors);
        Assert.Equal("Modern", deck.FormatName);
        Assert.True(page.HasMore);
    }

    [Fact]
    public void ParseSearch_swaps_archidekt_webp_art_for_scryfall_jpeg()
    {
        const string json = """
            { "count": 1, "next": null, "results": [
              { "id": 1, "name": "Xavier", "updatedAt": "2026-10-01T00:00:00Z", "private": false, "customFeatured": "",
                "featured": "https://card-images.archidekt.com/art/front/9/4/94a420c2-b1a8-4a98-a2a5-7f949d3081bc.webp" } ] }
            """;

        var deck = Assert.Single(ArchidektJson.ParseSearch(json).Decks);

        Assert.Equal("https://cards.scryfall.io/art_crop/front/9/4/94a420c2-b1a8-4a98-a2a5-7f949d3081bc.jpg", deck.ImageUrl);
    }

    [Theory]
    [InlineData("27052078", 27052078)]
    [InlineData(" 27052078 ", 27052078)]
    [InlineData("https://archidekt.com/decks/27052078/xavier", 27052078)]
    [InlineData("archidekt.com/decks/27052078", 27052078)]
    [InlineData("https://archidekt.com/api/decks/27052078/", 27052078)]
    public void TryParseDeckId_accepts_ids_and_links(string input, int expected)
    {
        Assert.True(ArchidektClient.TryParseDeckId(input, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xavier")]
    [InlineData("https://archidekt.com/search/decks?ownerUsername=ISummonPotOfGreed")]
    [InlineData("-5")]
    public void TryParseDeckId_rejects_search_text(string input)
    {
        Assert.False(ArchidektClient.TryParseDeckId(input, out _));
    }
}
