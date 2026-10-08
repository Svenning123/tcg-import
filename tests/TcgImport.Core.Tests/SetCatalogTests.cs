using TcgImport.Core.Archidekt;
using TcgImport.Core.Printings;

namespace TcgImport.Core.Tests;

public class SetCatalogTests
{
    // Trimmed-down https://api.scryfall.com/sets
    private const string SetsJson = """
        { "object": "list", "data": [
          { "code": "fin", "name": "Final Fantasy", "released_at": "2025-06-13", "set_type": "expansion", "digital": false },
          { "code": "fic", "name": "Final Fantasy Commander", "released_at": "2025-06-13", "set_type": "commander", "parent_set_code": "fin", "digital": false },
          { "code": "tfic", "name": "Final Fantasy Commander Tokens", "released_at": "2025-06-13", "set_type": "token", "parent_set_code": "fic", "digital": false },
          { "code": "fca", "name": "Final Fantasy: Through the Ages", "released_at": "2025-06-13", "set_type": "masterpiece", "parent_set_code": "fin", "digital": false },
          { "code": "3ed", "name": "Revised Edition", "released_at": "1994-04-11", "set_type": "core", "digital": false },
          { "code": "prm", "name": "Magic Online Promos", "released_at": "2002-06-24", "set_type": "promo", "digital": true }
        ] }
        """;

    [Fact]
    public void Parses_sets()
    {
        var set = SetCatalog.Parse(SetsJson).Find("FIC");

        Assert.Equal(new MtgSet("fic", "Final Fantasy Commander", new DateOnly(2025, 6, 13), "commander", "fin", false), set);
    }

    [Fact]
    public void Related_sets_include_grandchildren()
    {
        var related = SetCatalog.Parse(SetsJson).RelatedSets("fin").Select(s => s.Code).Order();

        Assert.Equal(["fca", "fic", "tfic"], related);
    }

    [Fact]
    public void Search_matches_codes_first_then_names()
    {
        var catalog = SetCatalog.Parse(SetsJson);

        Assert.Equal("fin", catalog.Search("fin")[0].Code);
        Assert.Equal(["fca", "fic", "fin", "tfic"], catalog.Search("final").Select(s => s.Code).Order());
        Assert.Equal("3ed", Assert.Single(catalog.Search("revised")).Code);
    }

    [Fact]
    public void Card_name_search_puts_matching_and_legendary_names_first()
    {
        const string json = """
            { "count": 4, "results": [
              { "oracleCard": { "name": "Giant Tortoise", "superTypes": [] } },
              { "oracleCard": { "name": "Testament of Faith", "superTypes": [] } },
              { "oracleCard": { "name": "Tesak, Judith's Hellhound", "superTypes": ["Legendary"] } },
              { "oracleCard": { "name": "Testament of Faith", "superTypes": [] } }
            ] }
            """;

        // "Giant Tortoise" matched on a translated name; neither it nor Tesak contains "test", so they keep Archidekt's order.
        Assert.Equal(["Testament of Faith", "Giant Tortoise", "Tesak, Judith's Hellhound"],
            ArchidektJson.ParseCardNames(json, "test", legendaryFirst: false));
        Assert.Equal("Tesak, Judith's Hellhound", ArchidektJson.ParseCardNames(json, "tes", legendaryFirst: true)[0]);
    }
}
