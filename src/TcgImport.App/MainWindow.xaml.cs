using System.Windows;
using System.Windows.Input;
using TcgImport.App.ViewModels;

namespace TcgImport.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // PasswordBox can't be data-bound, so the window hands the password to the view model and clears it.
    private async void SignIn_Click(object sender, RoutedEventArgs e) => await SignInAsync();

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SignInAsync();
    }

    private async Task SignInAsync()
    {
        var password = PasswordInput.Password;
        PasswordInput.Clear();
        await ((MainViewModel)DataContext).SignInAsync(password);
    }
}
