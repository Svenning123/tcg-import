namespace TcgImport.Core.Archidekt;

public sealed record ArchidektDeck(DeckSummary Summary, IReadOnlyList<DeckCard> Cards);

/// <param name="Name">Full oracle name; double-faced and split cards keep both halves ("Fire // Ice").</param>
public sealed record DeckCard(string Name, int Quantity, DeckSection Section);

public enum DeckSection
{
    Commander,
    Main,
    Sideboard,
    /// <summary>Cards in a category that Archidekt leaves out of the deck, such as Maybeboard.</summary>
    Excluded,
}
