using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace TcgImport.Core.TcgArena;

/// <summary>One printing of a card in TCG Arena's MTG data.</summary>
/// <param name="CardId">TCG Arena's id, which its importer accepts in place of a card name.</param>
/// <param name="Set">Scryfall set code, lower case.</param>
public sealed record TcgArenaPrinting(string CardId, string Name, string Set);

/// <summary>
/// TCG Arena's MTG printings, and how Scryfall printing ids (what Archidekt stores per card) map onto them.
/// The data has an entry per printing, either keyed "p-{scryfallId}" or an alias
/// "p-{scryfallId}" → { "aliasOf": "{tcgArenaId}" }.
/// </summary>
public sealed class TcgArenaCardIndex
{
    private readonly Dictionary<string, string> _cardIdByPrinting;
    private readonly Dictionary<string, TcgArenaPrinting> _printingByCardId;
    private readonly Dictionary<string, List<TcgArenaPrinting>> _printingsByName;

    /// <param name="printings">In TCG Arena's lookup order: the first printing of a name is the one it picks for that name.</param>
    private TcgArenaCardIndex(Dictionary<string, string> cardIdByPrinting, IEnumerable<TcgArenaPrinting> printings)
    {
        _cardIdByPrinting = cardIdByPrinting;
        _printingByCardId = new(StringComparer.OrdinalIgnoreCase);
        _printingsByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var printing in printings)
        {
            if (!_printingByCardId.TryAdd(printing.CardId, printing)) continue;
            if (!_printingsByName.TryGetValue(printing.Name, out var list)) _printingsByName[printing.Name] = list = [];
            list.Add(printing);
        }
    }

    public int Count => _cardIdByPrinting.Count;

    public string? CardIdFor(string? scryfallPrintingId) =>
        scryfallPrintingId is not null && _cardIdByPrinting.TryGetValue(scryfallPrintingId, out var id) ? id : null;

    public TcgArenaPrinting? Printing(string? cardId) =>
        cardId is not null && _printingByCardId.TryGetValue(cardId, out var printing) ? printing : null;

    /// <summary>All printings of a card, in TCG Arena's lookup order.</summary>
    public IReadOnlyList<TcgArenaPrinting> PrintingsOf(string cardName) =>
        _printingsByName.TryGetValue(cardName, out var list) ? list : [];

    /// <summary>The printing TCG Arena uses when a card is imported by name.</summary>
    public TcgArenaPrinting? DefaultPrintingOf(string cardName) => PrintingsOf(cardName).FirstOrDefault();

    /// <summary>Builds the index from TCG Arena's MTGCards.json (plain or gzip-compressed).</summary>
    public static async Task<TcgArenaCardIndex> ParseCardDataAsync(Stream stream, CancellationToken ct = default)
    {
        var json = await MaybeGunzip(stream);
        // Dictionary keeps the file's order, which is the order TCG Arena searches cards by name.
        var cards = await JsonSerializer.DeserializeAsync<Dictionary<string, CardStub>>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? [];

        var cardIdByPrinting = new Dictionary<string, string>(cards.Count, StringComparer.OrdinalIgnoreCase);
        var printings = new List<TcgArenaPrinting>(cards.Count);
        foreach (var (key, card) in cards)
        {
            if (key.StartsWith("p-", StringComparison.Ordinal))
                cardIdByPrinting[key[2..]] = card.AliasOf ?? key;

            // An alias stands where TCG Arena would look its target up by name.
            var target = card.AliasOf is { } alias && cards.TryGetValue(alias, out var aliased) ? (alias, aliased) : (key, card);
            if (target.Item2 is { IsToken: false } entry && EnglishName(entry.Name) is { Length: > 0 } name
                && entry.Set is { ValueKind: JsonValueKind.String } setValue && setValue.GetString() is { Length: > 0 } set)
                printings.Add(new TcgArenaPrinting(target.Item1, name, set));
        }
        return new TcgArenaCardIndex(cardIdByPrinting, printings);
    }

    /// <summary>Reads an index written by <see cref="Save"/>.</summary>
    public static TcgArenaCardIndex Load(string path)
    {
        using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        var cardIdByPrinting = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var printings = new List<TcgArenaPrinting>();
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts is ["A", var scryfallId, var cardId]) cardIdByPrinting[scryfallId] = cardId;
            else if (parts is ["P", var id, var set, var name]) printings.Add(new TcgArenaPrinting(id, name, set));
        }
        return new TcgArenaCardIndex(cardIdByPrinting, printings);
    }

    /// <summary>Writes a compact copy: "A\tscryfallId\tcardId" and "P\tcardId\tset\tname" lines, gzip-compressed.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var writer = new StreamWriter(new GZipStream(File.Create(temp), CompressionLevel.Fastest)))
        {
            foreach (var (scryfallId, cardId) in _cardIdByPrinting) writer.WriteLine($"A\t{scryfallId}\t{cardId}");
            foreach (var printing in _printingsByName.Values.SelectMany(list => list))
                writer.WriteLine($"P\t{printing.CardId}\t{printing.Set}\t{printing.Name}");
        }
        File.Move(temp, path, overwrite: true);
    }

    private static async Task<Stream> MaybeGunzip(Stream stream)
    {
        // The file is served gzip-compressed as-is (about 11 MB), so buffering it is cheap.
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        buffer.Position = 0;
        var bytes = buffer.GetBuffer();
        return buffer.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b
            ? new GZipStream(buffer, CompressionMode.Decompress)
            : buffer;
    }

    // Most entries have localized names ({ "en": ..., "fr": ... }); a few custom ones use a plain string.
    private static string? EnglishName(JsonElement? name) => name switch
    {
        { ValueKind: JsonValueKind.String } s => s.GetString(),
        { ValueKind: JsonValueKind.Object } o when o.TryGetProperty("en", out var en) && en.ValueKind == JsonValueKind.String => en.GetString(),
        _ => null,
    };

    private sealed class CardStub
    {
        public string? AliasOf { get; set; }
        public JsonElement? Name { get; set; }
        public JsonElement? Set { get; set; }
        public bool IsToken { get; set; }
    }
}

/// <summary>Downloads TCG Arena's MTG card data when it changes and keeps a small local index of it.</summary>
public sealed class TcgArenaCardIndexStore(HttpClient http, string cacheDirectory)
{
    public const string CardDataUrl = "https://valcur.github.io/TCG-Arena-MTG/MTGCards.json";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    // Bump the file name when the index format changes, so old caches are rebuilt.
    private string IndexPath => Path.Combine(cacheDirectory, "tcgarena-mtg-index-v2.gz");
    private string ETagPath => Path.Combine(cacheDirectory, "tcgarena-mtg-index-v2.etag");

    private TcgArenaCardIndex? _index;

    /// <summary>True when the next <see cref="GetAsync"/> must download the card data (about 11 MB).</summary>
    public bool NeedsDownload => _index is null && !File.Exists(IndexPath);

    public async Task<TcgArenaCardIndex> GetAsync(CancellationToken ct = default)
    {
        var cached = File.Exists(IndexPath);
        var fresh = cached && DateTime.UtcNow - File.GetLastWriteTimeUtc(IndexPath) < CheckInterval;
        if (fresh) return _index ??= TcgArenaCardIndex.Load(IndexPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, CardDataUrl);
        if (cached && File.Exists(ETagPath))
            request.Headers.TryAddWithoutValidation("If-None-Match", await File.ReadAllTextAsync(ETagPath, ct));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException) when (cached)
        {
            // Offline: an older index is better than none.
            return _index ??= TcgArenaCardIndex.Load(IndexPath);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified && cached)
            {
                File.SetLastWriteTimeUtc(IndexPath, DateTime.UtcNow);
                return _index ??= TcgArenaCardIndex.Load(IndexPath);
            }
            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync(ct);
            _index = await TcgArenaCardIndex.ParseCardDataAsync(body, ct);
            _index.Save(IndexPath);
            if (response.Headers.ETag is { } etag) await File.WriteAllTextAsync(ETagPath, etag.ToString(), ct);
            return _index;
        }
    }
}
