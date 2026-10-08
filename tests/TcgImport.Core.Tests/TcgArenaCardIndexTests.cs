using System.IO.Compression;
using System.Text;
using TcgImport.Core.TcgArena;

namespace TcgImport.Core.Tests;

public sealed class TcgArenaCardIndexTests : IDisposable
{
    // Shape of TCG Arena's MTGCards.json: printings keyed "p-{scryfallId}", some aliasing another entry.
    public const string CardData = """
        {
          "6ad8011d-3471-4369-9d68-b264cc027487": { "id": "6ad8011d-3471-4369-9d68-b264cc027487", "name": { "en": "Sol Ring" }, "set": "frc" },
          "p-8ee443cc-e17a-493b-9c93-1f9e141a30e4": { "id": "p-8ee443cc-e17a-493b-9c93-1f9e141a30e4", "aliasOf": "6ad8011d-3471-4369-9d68-b264cc027487" },
          "p-56355ff3-2232-4a11-b868-aec9a50b9ee5": { "id": "p-56355ff3-2232-4a11-b868-aec9a50b9ee5", "name": { "en": "Bayou" }, "set": "3ed" },
          "p-treasure": { "id": "p-treasure", "name": { "en": "Treasure" }, "set": "tfin", "isToken": true }
        }
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "TcgImportTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Maps_printings_through_aliases_and_direct_entries()
    {
        var index = await Parse(Encoding.UTF8.GetBytes(CardData));

        Assert.Equal("6ad8011d-3471-4369-9d68-b264cc027487", index.CardIdFor("8ee443cc-e17a-493b-9c93-1f9e141a30e4"));
        Assert.Equal("p-56355ff3-2232-4a11-b868-aec9a50b9ee5", index.CardIdFor("56355ff3-2232-4a11-b868-aec9a50b9ee5"));
        Assert.Null(index.CardIdFor("00000000-0000-0000-0000-000000000000"));
        Assert.Null(index.CardIdFor(null));
    }

    [Fact]
    public async Task Knows_each_printings_set_and_skips_tokens()
    {
        var index = await Parse(Encoding.UTF8.GetBytes(CardData));

        Assert.Equal(new TcgArenaPrinting("p-56355ff3-2232-4a11-b868-aec9a50b9ee5", "Bayou", "3ed"),
            index.Printing("p-56355ff3-2232-4a11-b868-aec9a50b9ee5"));
        Assert.Single(index.PrintingsOf("Sol Ring"));
        Assert.Equal("frc", index.DefaultPrintingOf("Sol Ring")?.Set);
        Assert.Empty(index.PrintingsOf("Treasure"));
    }

    [Fact]
    public async Task Reads_gzip_compressed_card_data()
    {
        using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(CardData));

        var index = await Parse(compressed.ToArray());

        Assert.Equal(3, index.Count);
    }

    [Fact]
    public async Task Saved_index_loads_back()
    {
        var index = await Parse(Encoding.UTF8.GetBytes(CardData));
        var path = Path.Combine(_dir, "index.gz");

        index.Save(path);
        var loaded = TcgArenaCardIndex.Load(path);

        Assert.Equal(index.Count, loaded.Count);
        Assert.Equal("6ad8011d-3471-4369-9d68-b264cc027487", loaded.CardIdFor("8ee443cc-e17a-493b-9c93-1f9e141a30e4"));
        Assert.Equal(index.PrintingsOf("Bayou"), loaded.PrintingsOf("Bayou"));
    }

    private static Task<TcgArenaCardIndex> Parse(byte[] bytes) =>
        TcgArenaCardIndex.ParseCardDataAsync(new MemoryStream(bytes));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
