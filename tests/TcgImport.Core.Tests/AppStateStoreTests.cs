using TcgImport.Core.Archidekt;
using TcgImport.Core.Printings;
using TcgImport.Core.Storage;

namespace TcgImport.Core.Tests;

public sealed class AppStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "TcgImportTests", Guid.NewGuid().ToString("N"));
    private string StatePath => Path.Combine(_dir, "state.json");

    [Fact]
    public void Missing_file_loads_empty_state()
    {
        var state = new AppStateStore(StatePath).Load();

        Assert.Empty(state.Favorites);
        Assert.Empty(state.BlockedSets);
    }

    [Fact]
    public void Saved_state_round_trips()
    {
        var store = new AppStateStore(StatePath);
        var favorite = new DeckSummary(27052078, "Xavier", "ISummonPotOfGreed", 3,
            DateTimeOffset.Parse("2026-10-04T09:44:25Z"), "https://img/x.webp", "UG");

        store.Save(new AppState { Favorites = [favorite], BlockedSets = [new("fin", IncludeRelated: false)] });
        var loaded = store.Load();

        Assert.Equal(favorite, Assert.Single(loaded.Favorites));
        Assert.Equal(new BlockedSet("fin", IncludeRelated: false), Assert.Single(loaded.BlockedSets));
    }

    [Fact]
    public void Corrupt_file_is_kept_aside_and_loads_empty_state()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(StatePath, "{ not json");

        var state = new AppStateStore(StatePath).Load();

        Assert.Empty(state.Favorites);
        Assert.True(File.Exists(StatePath + ".corrupt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
