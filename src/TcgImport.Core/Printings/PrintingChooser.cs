using TcgImport.Core.Archidekt;
using TcgImport.Core.TcgArena;

namespace TcgImport.Core.Printings;

/// <summary>A set the user doesn't want card art from.</summary>
/// <param name="IncludeRelated">Also block the sets that belong to it, e.g. FIN's Commander decks, promos and tokens.</param>
public sealed record BlockedSet(string Code, bool IncludeRelated = true);

/// <param name="Card">What goes on the decklist line: a TCG Arena card id, or the card name.</param>
public sealed record ChosenPrinting(string Card, bool ByPrinting, PrintingOutcome Outcome);

public enum PrintingOutcome
{
    /// <summary>The printing chosen on Archidekt, or TCG Arena's own pick when sending by name.</summary>
    Kept,
    /// <summary>The printing was from a blocked set and another printing is used instead.</summary>
    Replaced,
    /// <summary>The printing is from a blocked set, but the card has no other printing in TCG Arena.</summary>
    OnlyBlockedPrintings,
    /// <summary>Card art was requested but TCG Arena doesn't have Archidekt's printing, so it goes by name.</summary>
    PrintingNotInTcgArena,
}

/// <summary>
/// Decides which printing of each card to send to TCG Arena: the one chosen on Archidekt (or TCG Arena's default),
/// unless it's from a blocked set. Then it uses the closest older printing, or else the closest newer one,
/// preferring paper sets over digital ones and regular sets over promos and collectible extras.
/// </summary>
public sealed class PrintingChooser
{
    // Gold-bordered, oversized and similar printings aren't great stand-ins for a normal card, and promo sets
    // span many years under one release date, so both rank below regular sets.
    private static readonly HashSet<string> ExtraSetTypes = new(StringComparer.OrdinalIgnoreCase) { "memorabilia", "token", "minigame", "promo" };

    private readonly TcgArenaCardIndex _index;
    private readonly SetCatalog _sets;
    private readonly bool _keepCardArt;
    private readonly HashSet<string> _blockedCodes;

    public PrintingChooser(TcgArenaCardIndex index, SetCatalog sets, bool keepCardArt, IEnumerable<BlockedSet> blocked)
    {
        _index = index;
        _sets = sets;
        _keepCardArt = keepCardArt;
        _blockedCodes = new(StringComparer.OrdinalIgnoreCase);
        foreach (var set in blocked)
        {
            _blockedCodes.Add(set.Code);
            if (set.IncludeRelated)
                foreach (var related in sets.RelatedSets(set.Code)) _blockedCodes.Add(related.Code);
        }
    }

    public bool IsBlocked(string setCode) => _blockedCodes.Contains(setCode);

    public ChosenPrinting Choose(DeckCard card)
    {
        var archidektPrinting = _keepCardArt ? _index.Printing(_index.CardIdFor(card.PrintingId)) : null;
        if (archidektPrinting is not null && !IsBlocked(archidektPrinting.Set))
            return new(archidektPrinting.CardId, ByPrinting: true, PrintingOutcome.Kept);

        var lostPrinting = _keepCardArt && archidektPrinting is null;
        var current = archidektPrinting ?? _index.DefaultPrintingOf(card.Name);
        if (current is null || !IsBlocked(current.Set))
        {
            // Sending by name: TCG Arena picks its default printing, which isn't blocked.
            return new(card.Name, ByPrinting: false, lostPrinting ? PrintingOutcome.PrintingNotInTcgArena : PrintingOutcome.Kept);
        }

        return Replacement(current) is { } replacement
            ? new(replacement.CardId, ByPrinting: true, PrintingOutcome.Replaced)
            : new(current.CardId, ByPrinting: true, PrintingOutcome.OnlyBlockedPrintings);
    }

    private TcgArenaPrinting? Replacement(TcgArenaPrinting blocked)
    {
        var blockedDate = _sets.Find(blocked.Set)?.ReleasedAt;
        return _index.PrintingsOf(blocked.Name)
            .Where(p => !IsBlocked(p.Set))
            .Select(p => (Printing: p, Set: _sets.Find(p.Set)))
            .OrderBy(c => c.Set?.Digital == true)                            // paper first
            .ThenBy(c => c.Set is not null && ExtraSetTypes.Contains(c.Set.SetType))
            .ThenBy(c => Direction(c.Set?.ReleasedAt, blockedDate))          // older, then newer, then unknown
            .ThenBy(c => Distance(c.Set?.ReleasedAt, blockedDate))           // closest first
            .Select(c => c.Printing)
            .FirstOrDefault();
    }

    private static int Direction(DateOnly? date, DateOnly? blockedDate) =>
        date is null || blockedDate is null ? 2 : date < blockedDate ? 0 : 1;

    private static int Distance(DateOnly? date, DateOnly? blockedDate) =>
        date is null || blockedDate is null ? int.MaxValue : Math.Abs(date.Value.DayNumber - blockedDate.Value.DayNumber);
}
