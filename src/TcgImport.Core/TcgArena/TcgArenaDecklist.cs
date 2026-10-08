using TcgImport.Core.Archidekt;

namespace TcgImport.Core.TcgArena;

/// <summary>
/// A decklist in the plain-text format TCG Arena's importer parses: "{count} {card name}" per line,
/// with "Commander" and "Sideboard" heading lines. A blank line ends a heading's section.
/// </summary>
public sealed record TcgArenaDecklist(string Text, int CommanderCount, int MainCount, int SideboardCount)
{
    /// <summary>The number TCG Arena shows as "Total cards" (it leaves the sideboard out).</summary>
    public int DeckTotal => CommanderCount + MainCount;

    public static TcgArenaDecklist FromDeck(ArchidektDeck deck)
    {
        var commander = Lines(deck, DeckSection.Commander);
        var main = Lines(deck, DeckSection.Main);
        var sideboard = Lines(deck, DeckSection.Sideboard);

        var blocks = new List<string>();
        if (commander.Count > 0) blocks.Add("Commander\n" + string.Join("\n", commander.Select(Format)));
        if (main.Count > 0) blocks.Add(string.Join("\n", main.Select(Format)));
        if (sideboard.Count > 0) blocks.Add("Sideboard\n" + string.Join("\n", sideboard.Select(Format)));

        return new TcgArenaDecklist(
            string.Join("\n\n", blocks),
            commander.Sum(l => l.Quantity),
            main.Sum(l => l.Quantity),
            sideboard.Sum(l => l.Quantity));
    }

    // Different printings of the same card are separate entries on Archidekt; TCG Arena only needs the name.
    private static List<(string Name, int Quantity)> Lines(ArchidektDeck deck, DeckSection section) =>
        deck.Cards
            .Where(c => c.Section == section)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.First().Name, g.Sum(c => c.Quantity)))
            .ToList();

    private static string Format((string Name, int Quantity) line) => $"{line.Quantity} {line.Name}";
}
