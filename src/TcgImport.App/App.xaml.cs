using System.Net.Http;
using System.Windows;
using TcgImport.App.ViewModels;
using TcgImport.Core.Archidekt;
using TcgImport.Core.Storage;

namespace TcgImport.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TcgImport/0.1 (+https://github.com/TobiasSaugbjerg/tcg-import)");

        var viewModel = new MainViewModel(new ArchidektClient(http), new AppStateStore(AppStateStore.DefaultPath));
        new MainWindow { DataContext = viewModel }.Show();
    }
}
