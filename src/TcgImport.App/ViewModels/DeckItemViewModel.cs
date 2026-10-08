using CommunityToolkit.Mvvm.ComponentModel;
using TcgImport.Core.Archidekt;

namespace TcgImport.App.ViewModels;

/// <summary>One deck row. The same instance is shown in both the search results and favorites.</summary>
public sealed partial class DeckItemViewModel(DeckSummary summary) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(Details), nameof(ImageUrl))]
    private DeckSummary _summary = summary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteLabel))]
    private bool _isFavorite;

    /// <summary>Result of the last send, e.g. "Sent 14:02 · 100 cards".</summary>
    [ObservableProperty]
    private string? _lastResult;

    public int Id => Summary.Id;
    public string Name => Summary.Name;
    public string? ImageUrl => Summary.ImageUrl;
    public string FavoriteLabel => IsFavorite ? "★ Favorite" : "☆ Favorite";

    public string Details => string.Join("  ·  ", new[]
    {
        Summary.FormatName,
        Summary.Colors.Length > 0 ? Summary.Colors : null,
        string.IsNullOrEmpty(Summary.OwnerUsername) ? null : "by " + Summary.OwnerUsername,
        "updated " + Summary.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd"),
    }.Where(s => s is not null));
}
