namespace TcgImport.App.ViewModels;

/// <summary>An entry in a filter drop-down.</summary>
public sealed record FilterOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
