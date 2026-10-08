using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcgImport.Core.Printings;
using TcgImport.Core.Storage;
using TcgImport.Core.TcgArena;

namespace TcgImport.App.ViewModels;

/// <summary>A row in the blocked sets list.</summary>
public sealed record BlockedSetViewModel(BlockedSet Set, string Title, string Detail);

/// <summary>The Printings tab: whether to keep Archidekt's card art, and which sets' art to avoid.</summary>
public sealed partial class PrintingsViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly AppStateStore _store;
    private readonly TcgArenaCardIndexStore _cardIndex;
    private readonly SetCatalogStore _setCatalog;
    private readonly Action<string> _setStatus;
    private SetCatalog? _sets;

    public PrintingsViewModel(AppState state, AppStateStore store, TcgArenaCardIndexStore cardIndex,
        SetCatalogStore setCatalog, Action<string> setStatus)
    {
        _state = state;
        _store = store;
        _cardIndex = cardIndex;
        _setCatalog = setCatalog;
        _setStatus = setStatus;
        _keepCardArt = state.KeepCardArt;
        ShowBlockedSets();
        SetSuggestions = (text, _) => Task.FromResult<IReadOnlyList<string>>(
            _sets?.Search(text).Select(SetLabel).ToList() ?? []);
    }

    /// <summary>Send Archidekt's chosen printings so TCG Arena shows the same card art.</summary>
    [ObservableProperty] private bool _keepCardArt;

    /// <summary>Set code or name typed in the "block a set" box.</summary>
    [ObservableProperty] private string _newSetText = "";

    [ObservableProperty] private bool _includeRelatedSets = true;

    public ObservableCollection<BlockedSetViewModel> BlockedSets { get; } = [];

    public Func<string, CancellationToken, Task<IReadOnlyList<string>>> SetSuggestions { get; }

    partial void OnKeepCardArtChanged(bool value)
    {
        _state.KeepCardArt = value;
        _store.Save(_state);
    }

    /// <summary>Loads the set list (for names, dates and suggestions); works offline from a cached copy.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            _sets = await _setCatalog.GetAsync();
            ShowBlockedSets();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            // Without the set list, blocked sets still show by code; it's loaded again when sending.
        }
    }

    [RelayCommand]
    private async Task AddBlockedSetAsync()
    {
        // Suggestions read "FIN · Final Fantasy (2025)"; the code is the part before the dot.
        var code = NewSetText.Split('·')[0].Trim().ToLowerInvariant();
        if (code.Length == 0) return;

        await InitializeAsync();
        if (_sets is not null && _sets.Find(code) is null)
        {
            var matches = _sets.Search(code);
            if (matches.Count == 0)
            {
                _setStatus($"No set called \"{NewSetText.Trim()}\". Type a set code like FIN, or part of a set name.");
                return;
            }
            code = matches[0].Code;
        }

        _state.BlockedSets.RemoveAll(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
        _state.BlockedSets.Add(new BlockedSet(code, IncludeRelatedSets));
        _store.Save(_state);
        ShowBlockedSets();
        NewSetText = "";
        _setStatus($"Blocked {code.ToUpperInvariant()}. Re-send a deck to apply it.");
    }

    [RelayCommand]
    private void RemoveBlockedSet(BlockedSetViewModel row)
    {
        _state.BlockedSets.RemoveAll(s => string.Equals(s.Code, row.Set.Code, StringComparison.OrdinalIgnoreCase));
        _store.Save(_state);
        ShowBlockedSets();
        _setStatus($"Unblocked {row.Set.Code.ToUpperInvariant()}.");
    }

    /// <summary>
    /// The chooser for a send, or null when every card can simply go by name.
    /// Downloads TCG Arena's card data and the set list the first time.
    /// </summary>
    public async Task<(PrintingChooser? Chooser, string Problem)> CreateChooserAsync()
    {
        if (!KeepCardArt && _state.BlockedSets.Count == 0) return (null, "");
        if (_cardIndex.NeedsDownload)
            _setStatus("Downloading TCG Arena's card list to match printings (first time only, about 11 MB)…");
        try
        {
            var index = await _cardIndex.GetAsync();
            _sets = await _setCatalog.GetAsync();
            return (new PrintingChooser(index, _sets, KeepCardArt, _state.BlockedSets), "");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            return (null, " Couldn't load the card data for printings, so TCG Arena picked the card art this time.");
        }
    }

    /// <summary>What happened to card art in a send, for the status line.</summary>
    public static string Summary(TcgArenaDecklist decklist)
    {
        var notes = new List<string>();
        if (decklist.Cards(PrintingOutcome.Replaced) is > 0 and var replaced)
            notes.Add($"{Cards(replaced)} moved off blocked sets");
        if (decklist.Cards(PrintingOutcome.OnlyBlockedPrintings) is > 0 and var stuck)
            notes.Add($"{Cards(stuck)} only exist in blocked sets");
        if (decklist.Cards(PrintingOutcome.PrintingNotInTcgArena) is > 0 and var missing)
            notes.Add($"{Cards(missing)} use TCG Arena's default art because it doesn't have that printing");
        return notes.Count == 0 ? "" : " Printings: " + string.Join("; ", notes) + ".";
    }

    private static string Cards(int count) => count == 1 ? "1 card" : $"{count} cards";

    private void ShowBlockedSets()
    {
        BlockedSets.Clear();
        foreach (var blocked in _state.BlockedSets)
        {
            var set = _sets?.Find(blocked.Code);
            var title = set is null ? blocked.Code.ToUpperInvariant() : SetLabel(set);
            var related = blocked.IncludeRelated && _sets is not null ? _sets.RelatedSets(blocked.Code) : [];
            var detail = !blocked.IncludeRelated ? "Only this set"
                : related.Count == 0 ? "No related sets"
                : $"Also blocks {related.Count} related set{(related.Count == 1 ? "" : "s")}: " +
                  string.Join(", ", related.Select(r => r.Code.ToUpperInvariant()));
            BlockedSets.Add(new BlockedSetViewModel(blocked, title, detail));
        }
    }

    private static string SetLabel(MtgSet set) =>
        $"{set.Code.ToUpperInvariant()} · {set.Name}" + (set.ReleasedAt is { } date ? $" ({date.Year})" : "");
}
