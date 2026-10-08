using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TcgImport.App.Controls;

/// <summary>
/// A text box that shows suggestions from <see cref="SuggestionProvider"/> while the user types.
/// Up/Down pick a suggestion, Enter or a click accepts it, Escape closes the list.
/// </summary>
public partial class AutoCompleteTextBox : UserControl
{
    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(250);

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(AutoCompleteTextBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty SuggestionProviderProperty = DependencyProperty.Register(
        nameof(SuggestionProvider), typeof(Func<string, CancellationToken, Task<IReadOnlyList<string>>>), typeof(AutoCompleteTextBox));

    public static readonly DependencyProperty MinimumLengthProperty = DependencyProperty.Register(
        nameof(MinimumLength), typeof(int), typeof(AutoCompleteTextBox), new PropertyMetadata(2));

    private CancellationTokenSource? _pending;
    private bool _accepting;

    public AutoCompleteTextBox()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user accepts a suggestion.</summary>
    public event EventHandler<string>? SuggestionAccepted;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Func<string, CancellationToken, Task<IReadOnlyList<string>>>? SuggestionProvider
    {
        get => (Func<string, CancellationToken, Task<IReadOnlyList<string>>>?)GetValue(SuggestionProviderProperty);
        set => SetValue(SuggestionProviderProperty, value);
    }

    public int MinimumLength
    {
        get => (int)GetValue(MinimumLengthProperty);
        set => SetValue(MinimumLengthProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (AutoCompleteTextBox)d;
        var text = (string?)e.NewValue ?? "";
        if (box.Input.Text != text) box.Input.Text = text;
    }

    private async void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (Text != Input.Text) Text = Input.Text;

        // Only suggest while the user is typing, not when the text is set from code or by accepting a suggestion.
        _pending?.Cancel();
        if (_accepting || !Input.IsKeyboardFocusWithin || SuggestionProvider is not { } provider || Input.Text.Trim().Length < MinimumLength)
        {
            SuggestionPopup.IsOpen = false;
            return;
        }

        var pending = _pending = new CancellationTokenSource();
        try
        {
            await Task.Delay(TypingPause, pending.Token);
            var suggestions = await provider(Input.Text, pending.Token);
            if (pending.IsCancellationRequested) return;
            SuggestionList.ItemsSource = suggestions;
            SuggestionList.SelectedIndex = -1;
            SuggestionPopup.IsOpen = suggestions.Count > 0 && Input.IsKeyboardFocusWithin;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            // Suggestions are a convenience; typing the full name still works.
            SuggestionPopup.IsOpen = false;
        }
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!SuggestionPopup.IsOpen) return;
        var count = SuggestionList.Items.Count;
        switch (e.Key)
        {
            case Key.Down:
                SuggestionList.SelectedIndex = (SuggestionList.SelectedIndex + 1) % count;
                SuggestionList.ScrollIntoView(SuggestionList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                SuggestionList.SelectedIndex = SuggestionList.SelectedIndex <= 0 ? count - 1 : SuggestionList.SelectedIndex - 1;
                SuggestionList.ScrollIntoView(SuggestionList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter when SuggestionList.SelectedItem is string selected:
                Accept(selected);
                e.Handled = true;
                break;
            case Key.Escape:
                SuggestionPopup.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void SuggestionList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: string clicked }) Accept(clicked);
    }

    private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!SuggestionPopup.IsKeyboardFocusWithin) SuggestionPopup.IsOpen = false;
    }

    private void Accept(string suggestion)
    {
        _pending?.Cancel();
        SuggestionPopup.IsOpen = false;
        _accepting = true;
        try
        {
            Text = suggestion;
        }
        finally
        {
            _accepting = false;
        }
        Input.CaretIndex = Input.Text.Length;
        Input.Focus();
        SuggestionAccepted?.Invoke(this, suggestion);
    }
}
