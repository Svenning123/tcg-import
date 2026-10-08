using System.Text;
using System.Text.Json;
using TcgImport.Core.Archidekt;
using TcgImport.Core.Printings;
using TcgImport.Core.TcgArena;

namespace TcgImport.Core.Tests;

public class PrintingChooserTests
{
    private static readonly SetCatalog Sets = new(
    [
        new("lea", "Limited Edition Alpha", new DateOnly(1993, 8, 5), "core", null, false),
        new("m10", "Magic 2010", new DateOnly(2009, 7, 17), "core", null, false),
        new("cmr", "Commander Legends", new DateOnly(2020, 11, 20), "draft_innovation", null, false),
        new("30a", "30th Anniversary Edition", new DateOnly(2022, 11, 28), "memorabilia", null, false),
        new("fin", "Final Fantasy", new DateOnly(2025, 6, 13), "expansion", null, false),
        new("fic", "Final Fantasy Commander", new DateOnly(2025, 6, 13), "commander", "fin", false),
        new("tfic", "Final Fantasy Commander Tokens", new DateOnly(2025, 6, 13), "token", "fic", false),
        new("prm", "Magic Online Promos", new DateOnly(2002, 6, 24), "promo", null, true),
        new("eoe", "Edge of Eternities", new DateOnly(2025, 8, 1), "expansion", null, false),
    ]);

    [Fact]
    public async Task Keeps_the_archidekt_printing_when_not_blocked()
    {
        var chooser = await Chooser(keepArt: true, blocked: []);

        var choice = chooser.Choose(Card("Sol Ring", "uid-cmr"));

        Assert.Equal(new ChosenPrinting("ring-cmr", true, PrintingOutcome.Kept), choice);
    }

    [Fact]
    public async Task Blocked_printing_falls_back_to_the_closest_older_paper_printing()
    {
        var chooser = await Chooser(keepArt: true, blocked: [new BlockedSet("fin")]);

        // Older printings: cmr (closest), m10, lea, plus a digital one and a memorabilia one that rank lower.
        var choice = chooser.Choose(Card("Sol Ring", "uid-fin"));

        Assert.Equal(new ChosenPrinting("ring-cmr", true, PrintingOutcome.Replaced), choice);
    }

    [Fact]
    public async Task Falls_back_to_a_newer_printing_when_no_older_one_is_allowed()
    {
        var chooser = await Chooser(keepArt: true, blocked: [new BlockedSet("fin"), new BlockedSet("lea")]);

        var choice = chooser.Choose(Card("Lightning Bolt", "uid-bolt-fin"));

        Assert.Equal(new ChosenPrinting("bolt-eoe", true, PrintingOutcome.Replaced), choice);
    }

    [Fact]
    public async Task Related_sets_are_blocked_with_their_parent()
    {
        var chooser = await Chooser(keepArt: true, blocked: [new BlockedSet("fin")]);

        Assert.True(chooser.IsBlocked("fic"));
        Assert.True(chooser.IsBlocked("tfic"));
        Assert.Equal(PrintingOutcome.Replaced, chooser.Choose(Card("Sol Ring", "uid-fic")).Outcome);
    }

    [Fact]
    public async Task Related_sets_stay_allowed_when_asked()
    {
        var chooser = await Chooser(keepArt: true, blocked: [new BlockedSet("fin", IncludeRelated: false)]);

        Assert.False(chooser.IsBlocked("fic"));
        Assert.Equal(new ChosenPrinting("ring-fic", true, PrintingOutcome.Kept), chooser.Choose(Card("Sol Ring", "uid-fic")));
    }

    [Fact]
    public async Task Card_with_only_blocked_printings_keeps_its_printing()
    {
        var chooser = await Chooser(keepArt: true, blocked: [new BlockedSet("fin")]);

        var choice = chooser.Choose(Card("Cloud, Midgar Mercenary", "uid-cloud"));

        Assert.Equal(new ChosenPrinting("cloud-fin", true, PrintingOutcome.OnlyBlockedPrintings), choice);
    }

    [Fact]
    public async Task Without_card_art_cards_go_by_name_unless_tcg_arenas_default_is_blocked()
    {
        var chooser = await Chooser(keepArt: false, blocked: [new BlockedSet("fin")]);

        // TCG Arena's default for Sol Ring is its first printing in the data (fin), so it must be replaced.
        Assert.Equal(new ChosenPrinting("ring-cmr", true, PrintingOutcome.Replaced), chooser.Choose(Card("Sol Ring", "uid-m10")));
        // Lightning Bolt's default (lea) isn't blocked, so TCG Arena may pick it by name.
        Assert.Equal(new ChosenPrinting("Lightning Bolt", false, PrintingOutcome.Kept), chooser.Choose(Card("Lightning Bolt", "uid-bolt-fin")));
    }

    [Fact]
    public async Task Unknown_printing_goes_by_name()
    {
        var chooser = await Chooser(keepArt: true, blocked: []);

        Assert.Equal(new ChosenPrinting("Sol Ring", false, PrintingOutcome.PrintingNotInTcgArena), chooser.Choose(Card("Sol Ring", "uid-missing")));
    }

    [Fact]
    public async Task Decklist_counts_outcomes()
    {
        var chooser = await Chooser(keepArt: true, blocked: [new BlockedSet("fin")]);
        var deck = new ArchidektDeck(new DeckSummary(1, "Test", "me", 3, DateTimeOffset.UnixEpoch, null, ""),
        [
            new("Cloud, Midgar Mercenary", 1, DeckSection.Commander, "uid-cloud"),
            new("Sol Ring", 1, DeckSection.Main, "uid-fin"),
            new("Sol Ring", 1, DeckSection.Main, "uid-cmr"),
            new("Lightning Bolt", 2, DeckSection.Main, "uid-missing"),
            new("Black Lotus", 1, DeckSection.Excluded, "uid-lotus"),
        ]);

        var list = TcgArenaDecklist.FromDeck(deck, chooser);

        Assert.Equal("Commander\n1 cloud-fin\n\n2 ring-cmr\n2 Lightning Bolt", list.Text);
        Assert.Equal(1, list.Cards(PrintingOutcome.Replaced));
        Assert.Equal(1, list.Cards(PrintingOutcome.OnlyBlockedPrintings));
        Assert.Equal(2, list.Cards(PrintingOutcome.PrintingNotInTcgArena));
        Assert.Equal(5, list.DeckTotal);
    }

    private static DeckCard Card(string name, string printingId) => new(name, 1, DeckSection.Main, printingId);

    private static async Task<PrintingChooser> Chooser(bool keepArt, IEnumerable<BlockedSet> blocked)
    {
        // Order matters: the first printing of a name is TCG Arena's default for it.
        var printings = new (string Id, string Name, string Set)[]
        {
            ("ring-fin", "Sol Ring", "fin"),
            ("ring-fic", "Sol Ring", "fic"),
            ("ring-prm", "Sol Ring", "prm"),
            ("ring-30a", "Sol Ring", "30a"),
            ("ring-lea", "Sol Ring", "lea"),
            ("ring-m10", "Sol Ring", "m10"),
            ("ring-cmr", "Sol Ring", "cmr"),
            ("bolt-lea", "Lightning Bolt", "lea"),
            ("bolt-fin", "Lightning Bolt", "fin"),
            ("bolt-eoe", "Lightning Bolt", "eoe"),
            ("cloud-fin", "Cloud, Midgar Mercenary", "fin"),
        };
        var data = new Dictionary<string, object>();
        foreach (var (id, name, set) in printings) data[id] = new { id, name = new { en = name }, set };
        // Archidekt printing ids: uid-{set} for Sol Ring, uid-bolt-fin, uid-cloud.
        foreach (var set in new[] { "fin", "fic", "lea", "m10", "cmr" }) data[$"p-uid-{set}"] = new { aliasOf = $"ring-{set}" };
        data["p-uid-bolt-fin"] = new { aliasOf = "bolt-fin" };
        data["p-uid-cloud"] = new { aliasOf = "cloud-fin" };

        var index = await TcgArenaCardIndex.ParseCardDataAsync(new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
        return new PrintingChooser(index, Sets, keepArt, blocked);
    }
}
