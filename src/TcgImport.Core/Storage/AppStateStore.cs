using System.Text.Json;
using TcgImport.Core.Archidekt;
using TcgImport.Core.Printings;

namespace TcgImport.Core.Storage;

public sealed class AppState
{
    public List<DeckSummary> Favorites { get; set; } = [];


    /// <summary>Browser executable to open TCG Arena with. Empty means the Windows default browser.</summary>
    public string? BrowserPath { get; set; }

    /// <summary>Send each card's Archidekt printing so TCG Arena shows the same art.</summary>
    public bool KeepCardArt { get; set; } = true;

    /// <summary>Sets whose printings are swapped for another printing when sending to TCG Arena.</summary>
    public List<BlockedSet> BlockedSets { get; set; } = [];

    /// <summary>The Archidekt sign-in, encrypted by the app for the current Windows user. Never the password.</summary>
    public string? ProtectedArchidektSession { get; set; }
}

/// <summary>Keeps <see cref="AppState"/> in a JSON file (by default %APPDATA%\TcgImport\state.json).</summary>
public sealed class AppStateStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TcgImport", "state.json");

    public string FilePath { get; } = filePath;

    public AppState Load()
    {
        if (!File.Exists(FilePath)) return new AppState();
        try
        {
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Options) ?? new AppState();
        }
        catch (JsonException)
        {
            // Keep the unreadable file for inspection instead of silently overwriting the user's favorites.
            File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
