using TcgImport.Core.Archidekt;
using TcgImport.Core.Printings;

namespace TcgImport.Core.TcgArena;

/// <summary>
/// A decklist in the plain-text format TCG Arena's importer parses: "{count} {card}" per line, with
/// "Commander" and "Sideboard" heading lines. A blank line ends a heading's section. Each card is either
/// a TCG Arena card id (that printing's art) or a card name (TCG Arena picks the art).
/// </summary>
public sealed record TcgArenaDecklist(
    string Text,
    int CommanderCount,
    int MainCount,
    int SideboardCount,
    IReadOnlyDictionary<PrintingOutcome, int> CardsByOutcome)
{
    /// <summary>The number TCG Arena shows as "Total cards" (it leaves the sideboard out).</summary>
    public int DeckTotal => CommanderCount + MainCount;

    public int Cards(PrintingOutcome outcome) => CardsByOutcome.GetValueOrDefault(outcome);

    /// <param name="chooser">Picks each card's printing. Without one, every card is sent by name.</param>
    public static TcgArenaDecklist FromDeck(ArchidektDeck deck, PrintingChooser? chooser = null)
    {
        var chosen = deck.Cards
            .Where(c => c.Section != DeckSection.Excluded)
            .Select(c => (Card: c, Choice: chooser?.Choose(c) ?? new ChosenPrinting(c.Name, false, PrintingOutcome.Kept)))
            .ToList();

        var commander = Lines(chosen, DeckSection.Commander);
        var main = Lines(chosen, DeckSection.Main);
        var sideboard = Lines(chosen, DeckSection.Sideboard);

        var blocks = new List<string>();
        if (commander.Count > 0) blocks.Add("Commander\n" + string.Join("\n", commander.Select(Format)));
        if (main.Count > 0) blocks.Add(string.Join("\n", main.Select(Format)));
        if (sideboard.Count > 0) blocks.Add("Sideboard\n" + string.Join("\n", sideboard.Select(Format)));

        return new TcgArenaDecklist(
            string.Join("\n\n", blocks),
            commander.Sum(l => l.Quantity),
            main.Sum(l => l.Quantity),
            sideboard.Sum(l => l.Quantity),
            chosen.GroupBy(c => c.Choice.Outcome).ToDictionary(g => g.Key, g => g.Sum(c => c.Card.Quantity)));
    }

    // Copies sent as the same card (printing id or name) become one line.
    private static List<(string Card, int Quantity)> Lines(List<(DeckCard Card, ChosenPrinting Choice)> chosen, DeckSection section) =>
        chosen
            .Where(c => c.Card.Section == section)
            .GroupBy(c => c.Choice.Card, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Sum(c => c.Card.Quantity)))
            .ToList();

    private static string Format((string Card, int Quantity) line) => $"{line.Quantity} {line.Card}";
}
