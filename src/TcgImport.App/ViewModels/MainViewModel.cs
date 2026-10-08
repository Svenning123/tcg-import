using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcgImport.App.Services;
using TcgImport.Core.Archidekt;
using TcgImport.Core.Storage;
using TcgImport.Core.TcgArena;

namespace TcgImport.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int SearchTab = 0;

    private readonly ArchidektClient _archidekt;
    private readonly AppStateStore _store;
    private readonly AppState _state;
    private readonly Dictionary<int, DeckItemViewModel> _items = [];

    // The search that "Load more" continues.
    private DeckSearchFilter _searchFilter = new();
    private int _searchPage;

    public MainViewModel(ArchidektClient archidekt, AppStateStore store)
    {
        _archidekt = archidekt;
        _store = store;
        _state = store.Load();
        _ownerUsername = _state.OwnerUsername ?? "";
        ClearFilters();

        foreach (var summary in _state.Favorites)
        {
            var item = Item(summary);
            item.IsFavorite = true;
            Favorites.Add(item);
        }

        _status = Favorites.Count > 0
            ? "Pick a favorite to send, or search Archidekt."
            : "Search by deck name and/or Archidekt username, or paste an Archidekt deck link.";
    }

    public ObservableCollection<DeckItemViewModel> Results { get; } = [];
    public ObservableCollection<DeckItemViewModel> Favorites { get; } = [];

    /// <summary>Deck name, deck id, or an archidekt.com/decks/... link.</summary>
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string _ownerUsername;
    [ObservableProperty] private string _status;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canLoadMore;
    [ObservableProperty] private int _selectedTab;

    // Filters, mirroring the advanced options on archidekt.com/search/decks.
    public IReadOnlyList<FilterOption<int?>> FormatOptions { get; } =
        [new("Any format", null), .. DeckFormats.Names.OrderBy(f => f.Value).Select(f => new FilterOption<int?>(f.Value, f.Key))];
    public IReadOnlyList<FilterOption<int?>> BracketOptions { get; } =
        [new("Any bracket", null), .. DeckFormats.Brackets.Select(b => new FilterOption<int?>(b.Value, b.Key))];
    public IReadOnlyList<FilterOption<bool>> ColorMatchOptions { get; } =
        [new("Exactly these colours", false), new("Including these colours", true)];
    public IReadOnlyList<FilterOption<DeckSortOrder>> SortOptions { get; } =
    [
        new("Recently updated", DeckSortOrder.RecentlyUpdated),
        new("Recently created", DeckSortOrder.RecentlyCreated),
        new("Name", DeckSortOrder.Name),
        new("Most viewed", DeckSortOrder.MostViewed),
        new("Largest", DeckSortOrder.Largest),
    ];

    [ObservableProperty] private FilterOption<int?>? _selectedFormat;
    [ObservableProperty] private FilterOption<int?>? _selectedBracket;
    [ObservableProperty] private FilterOption<bool>? _selectedColorMatch;
    [ObservableProperty] private FilterOption<DeckSortOrder>? _selectedSort;
    [ObservableProperty] private bool _white;
    [ObservableProperty] private bool _blue;
    [ObservableProperty] private bool _black;
    [ObservableProperty] private bool _red;
    [ObservableProperty] private bool _green;
    [ObservableProperty] private string _commanderName = "";
    [ObservableProperty] private string _cardName = "";

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedFormat = FormatOptions[0];
        SelectedBracket = BracketOptions[0];
        SelectedColorMatch = ColorMatchOptions[0];
        SelectedSort = SortOptions[0];
        White = Blue = Black = Red = Green = false;
        CommanderName = CardName = "";
    }

    private DeckSearchFilter CurrentFilter() => new()
    {
        Name = Query,
        OwnerUsername = OwnerUsername,
        DeckFormat = SelectedFormat?.Value,
        EdhBracket = SelectedBracket?.Value,
        Colors = (White ? "W" : "") + (Blue ? "U" : "") + (Black ? "B" : "") + (Red ? "R" : "") + (Green ? "G" : ""),
        ColorsInclude = SelectedColorMatch?.Value ?? false,
        CommanderName = CommanderName,
        CardName = CardName,
        OrderBy = SelectedSort?.Value ?? DeckSortOrder.RecentlyUpdated,
    };

    [RelayCommand]
    private Task SearchAsync() => RunAsync(async () =>
    {
        SelectedTab = SearchTab;

        if (ArchidektClient.TryParseDeckId(Query, out var deckId))
        {
            var deck = await _archidekt.GetDeckAsync(deckId);
            ShowResults([deck.Summary], hasMore: false, append: false);
            Status = $"Found \"{deck.Summary.Name}\".";
            return;
        }

        var filter = CurrentFilter();
        if (!filter.HasCriteria)
        {
            Status = "Enter a deck name, an Archidekt username, a deck link, or pick a filter.";
            return;
        }

        _searchFilter = filter;
        _searchPage = 1;
        var page = await _archidekt.SearchAsync(_searchFilter, _searchPage);
        ShowResults(page.Decks, page.HasMore, append: false);
        Status = page.UnknownCardName
            ? "Archidekt doesn't know that commander or card. Use the full, exact card name, e.g. \"Sol Ring\"."
            : page.Decks.Count == 0 ? "No public or unlisted decks found."
            : ShowingText();

        if (_state.OwnerUsername != OwnerUsername.Trim())
        {
            _state.OwnerUsername = OwnerUsername.Trim();
            _store.Save(_state);
        }
    });

    [RelayCommand]
    private Task LoadMoreAsync() => RunAsync(async () =>
    {
        var page = await _archidekt.SearchAsync(_searchFilter, _searchPage + 1);
        _searchPage++;
        ShowResults(page.Decks, page.HasMore, append: true);
        Status = ShowingText();
    });

    [RelayCommand]
    private Task SendAsync(DeckItemViewModel item) => RunAsync(async () =>
    {
        Status = $"Loading \"{item.Name}\" from Archidekt…";
        var deck = await _archidekt.GetDeckAsync(item.Id);
        item.Summary = deck.Summary;
        if (item.IsFavorite) SaveFavorites();

        var decklist = TcgArenaDecklist.FromDeck(deck);
        if (decklist.DeckTotal == 0)
        {
            Status = $"\"{deck.Summary.Name}\" has no cards in its main deck, so there's nothing to send.";
            return;
        }

        var url = TcgArenaLink.ForImport(deck.Summary.Name, TcgArenaLink.DeckId(deck.Summary.Id), decklist.Text);
        var browser = BrowserLauncher.Open(url, _state.BrowserPath);

        item.LastResult = $"Sent {DateTime.Now:HH:mm}  ·  {decklist.DeckTotal} cards";
        var sideboard = decklist.SideboardCount > 0 ? $" (+{decklist.SideboardCount} in the sideboard)" : "";
        Status = $"Opened \"{deck.Summary.Name}\" in {browser}. Click Import there. " +
                 $"TCG Arena should show Total cards: {decklist.DeckTotal}{sideboard}; a lower number means some cards weren't recognised.";
    });

    [RelayCommand]
    private void ToggleFavorite(DeckItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        if (item.IsFavorite) Favorites.Insert(0, item);
        else Favorites.Remove(item);
        SaveFavorites();
        Status = item.IsFavorite ? $"Added \"{item.Name}\" to favorites." : $"Removed \"{item.Name}\" from favorites.";
    }

    [RelayCommand]
    private void OpenOnArchidekt(DeckItemViewModel item) =>
        BrowserLauncher.Open(item.Summary.ArchidektUrl, _state.BrowserPath);

    private string ShowingText() => Results.Count == 1 ? "Showing 1 deck." : $"Showing {Results.Count} decks.";

    private void ShowResults(IEnumerable<DeckSummary> decks, bool hasMore, bool append)
    {
        if (!append) Results.Clear();
        foreach (var summary in decks)
        {
            var item = Item(summary);
            if (!Results.Contains(item)) Results.Add(item);
        }
        CanLoadMore = hasMore;
    }

    /// <summary>Reuses one view model per deck so favorite state stays in sync between the two lists.</summary>
    private DeckItemViewModel Item(DeckSummary summary)
    {
        if (_items.TryGetValue(summary.Id, out var item))
        {
            item.Summary = summary;
            return item;
        }
        return _items[summary.Id] = new DeckItemViewModel(summary);
    }

    private void SaveFavorites()
    {
        _state.Favorites = Favorites.Select(f => f.Summary).ToList();
        _store.Save(_state);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (DeckUnavailableException ex)
        {
            Status = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            Status = $"Couldn't reach Archidekt: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            Status = "Archidekt didn't answer in time. Try again.";
        }
        catch (Exception ex)
        {
            Status = $"Something went wrong: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
