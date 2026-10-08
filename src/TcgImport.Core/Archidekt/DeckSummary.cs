using System.Text.Json.Serialization;

namespace TcgImport.Core.Archidekt;

/// <summary>What we know about a deck without loading its card list. Also what we persist for favorites.</summary>
public sealed record DeckSummary(
    int Id,
    string Name,
    string OwnerUsername,
    int? DeckFormat,
    DateTimeOffset UpdatedAt,
    string? ImageUrl,
    string Colors,
    bool IsPrivate = false)
{
    [JsonIgnore] public string FormatName => DeckFormats.Name(DeckFormat);
    [JsonIgnore] public string ArchidektUrl => $"https://archidekt.com/decks/{Id}";
}
