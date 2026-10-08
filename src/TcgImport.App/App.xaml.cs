using System.IO;
using System.Net.Http;
using System.Windows;
using TcgImport.App.ViewModels;
using TcgImport.Core.Archidekt;
using TcgImport.Core.Printings;
using TcgImport.Core.Storage;
using TcgImport.Core.TcgArena;

namespace TcgImport.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TcgImport/0.1 (+https://github.com/TobiasSaugbjerg/tcg-import)");
        var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TcgImport");

        var viewModel = new MainViewModel(
            new ArchidektClient(http),
            new TcgArenaCardIndexStore(http, cacheDir),
            new SetCatalogStore(http, cacheDir),
            new AppStateStore(AppStateStore.DefaultPath));
        new MainWindow { DataContext = viewModel }.Show();
        await viewModel.InitializeAsync();
    }
}
