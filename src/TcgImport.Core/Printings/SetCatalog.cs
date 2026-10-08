using System.Text.Json;
using System.Text.Json.Serialization;

namespace TcgImport.Core.Printings;

/// <param name="Code">Scryfall set code, lower case ("fin"). Archidekt and TCG Arena use the same codes.</param>
/// <param name="ParentCode">Set this one belongs to, e.g. "fic" (Final Fantasy Commander) → "fin".</param>
public sealed record MtgSet(string Code, string Name, DateOnly? ReleasedAt, string SetType, string? ParentCode, bool Digital);

/// <summary>All Magic sets, from Scryfall's /sets API.</summary>
public sealed class SetCatalog
{
    private readonly Dictionary<string, MtgSet> _byCode;
    private readonly ILookup<string, MtgSet> _byParent;

    public SetCatalog(IEnumerable<MtgSet> sets)
    {
        _byCode = sets.DistinctBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        _byParent = _byCode.Values.Where(s => s.ParentCode is not null)
            .ToLookup(s => s.ParentCode!, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<MtgSet> All => _byCode.Values;

    public MtgSet? Find(string? code) => code is not null && _byCode.TryGetValue(code, out var set) ? set : null;

    /// <summary>Sets that belong to <paramref name="code"/>, directly or through another child (e.g. "tfic" → "fic" → "fin").</summary>
    public IReadOnlyList<MtgSet> RelatedSets(string code)
    {
        var found = new List<MtgSet>();
        var queue = new Queue<string>([code]);
        while (queue.Count > 0)
        {
            foreach (var child in _byParent[queue.Dequeue()])
            {
                if (found.Contains(child) || string.Equals(child.Code, code, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(child);
                queue.Enqueue(child.Code);
            }
        }
        return found;
    }

    /// <summary>Sets whose code starts with, or whose name contains, the text. Codes first, then newest first.</summary>
    public IReadOnlyList<MtgSet> Search(string text, int max = 12)
    {
        var query = text.Trim();
        if (query.Length == 0) return [];
        return _byCode.Values
            .Where(s => s.Code.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                        || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => string.Equals(s.Code, query, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(s => s.Code.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(s => s.ReleasedAt)
            .Take(max)
            .ToList();
    }

    public static SetCatalog Parse(string scryfallSetsJson)
    {
        var response = JsonSerializer.Deserialize<SetsResponse>(scryfallSetsJson)
            ?? throw new FormatException("Empty set list from Scryfall.");
        return new SetCatalog(response.Data.Select(s => new MtgSet(
            s.Code,
            s.Name,
            DateOnly.TryParse(s.ReleasedAt, out var released) ? released : null,
            s.SetType ?? "",
            s.ParentSetCode,
            s.Digital)));
    }

    private sealed class SetsResponse
    {
        [JsonPropertyName("data")] public List<SetDto> Data { get; set; } = [];
    }

    private sealed class SetDto
    {
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("released_at")] public string? ReleasedAt { get; set; }
        [JsonPropertyName("set_type")] public string? SetType { get; set; }
        [JsonPropertyName("parent_set_code")] public string? ParentSetCode { get; set; }
        [JsonPropertyName("digital")] public bool Digital { get; set; }
    }
}

/// <summary>Downloads Scryfall's set list and caches it for a week.</summary>
public sealed class SetCatalogStore(HttpClient http, string cacheDirectory)
{
    public const string SetsUrl = "https://api.scryfall.com/sets";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    private string CachePath => Path.Combine(cacheDirectory, "scryfall-sets.json");
    private SetCatalog? _catalog;

    public async Task<SetCatalog> GetAsync(CancellationToken ct = default)
    {
        if (_catalog is not null) return _catalog;

        var cached = File.Exists(CachePath);
        if (cached && DateTime.UtcNow - File.GetLastWriteTimeUtc(CachePath) < MaxAge)
            return _catalog = SetCatalog.Parse(await File.ReadAllTextAsync(CachePath, ct));

        try
        {
            // Scryfall asks API clients to send an Accept header.
            using var request = new HttpRequestMessage(HttpMethod.Get, SetsUrl);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            _catalog = SetCatalog.Parse(json);
            Directory.CreateDirectory(cacheDirectory);
            await File.WriteAllTextAsync(CachePath, json, ct);
            return _catalog;
        }
        catch (HttpRequestException) when (cached)
        {
            return _catalog = SetCatalog.Parse(await File.ReadAllTextAsync(CachePath, ct));
        }
    }
}
