using TcgImport.Core.Archidekt;

namespace TcgImport.Core.Tests;

public class DeckSearchFilterTests
{
    [Fact]
    public void Default_filter_sorts_by_recently_updated()
    {
        Assert.Equal("orderBy=-updatedAt&page=1&pageSize=30", new DeckSearchFilter().ToQueryString(1, 30));
    }

    [Fact]
    public void All_filters_map_to_archidekt_parameters()
    {
        var filter = new DeckSearchFilter
        {
            Name = " xavier ",
            OwnerUsername = "ISummonPotOfGreed",
            DeckFormat = 3,
            EdhBracket = 2,
            Colors = "gbu",
            ColorsInclude = true,
            CommanderName = "Esika, God of the Tree // The Prismatic Bridge",
            CardName = "Sol Ring",
            OrderBy = DeckSortOrder.Name,
        };

        Assert.Equal(
            "orderBy=name&page=2&pageSize=30&name=xavier&ownerUsername=ISummonPotOfGreed&deckFormat=3&edhBracket=2" +
            "&commanderName=Esika%2C%20God%20of%20the%20Tree%20%2F%2F%20The%20Prismatic%20Bridge&cardName=Sol%20Ring" +
            "&colors=U%2CB%2CG&colorsInclude=true",
            filter.ToQueryString(2, 30));
    }

    [Fact]
    public void Exact_colors_omit_the_include_flag()
    {
        var query = new DeckSearchFilter { Colors = "W" }.ToQueryString(1, 30);

        Assert.EndsWith("&colors=W", query);
    }

    [Fact]
    public void HasCriteria_ignores_sort_order()
    {
        Assert.False(new DeckSearchFilter { OrderBy = DeckSortOrder.MostViewed }.HasCriteria);
        Assert.True(new DeckSearchFilter { DeckFormat = 3 }.HasCriteria);
    }

    [Fact]
    public void Unknown_card_name_response_is_flagged()
    {
        var page = ArchidektJson.ParseSearch("""{ "count": -1, "next": null, "results": [] }""");

        Assert.True(page.UnknownCardName);
        Assert.Empty(page.Decks);
    }
}
