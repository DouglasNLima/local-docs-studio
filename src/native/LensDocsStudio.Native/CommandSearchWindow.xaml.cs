using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public sealed record DesktopAction(string Title, string Detail, Action Execute, bool IsEnabled = true);

public partial class CommandSearchWindow : Window
{
    private readonly IReadOnlyList<DesktopAction> actions;
    public DesktopAction? Chosen { get; private set; }
    public CommandSearchWindow(string title, IReadOnlyList<DesktopAction> actions)
    {
        this.actions = actions; InitializeComponent(); Title = SearchTitle.Text = title;
        WindowStateService.FitInitialWindow(this); Refresh(); Loaded += (_, _) => Query.Focus();
    }
    private void QueryChanged(object sender, TextChangedEventArgs e) { if (Results is not null) Refresh(); }
    private void Refresh()
    {
        var terms = Query.Text.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var matches = actions.Where(action => terms.All(term => Matches(action.Title + " " + action.Detail, term)))
            .OrderBy(action => Query.Text.Length > 0 && action.Title.StartsWith(Query.Text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ToArray();
        Results.ItemsSource = matches; Results.SelectedItem = matches.FirstOrDefault(action => action.IsEnabled);
        ResultStatus.Text = matches.Length == 0 ? "No matching results." : $"{matches.Length} results · Up/Down to choose · Enter to confirm · Escape to close";
    }
    private static bool Matches(string value, string term)
    {
        if (value.Contains(term, StringComparison.CurrentCultureIgnoreCase)) return true;
        var next = 0;
        foreach (var character in value)
            if (char.ToUpperInvariant(character) == char.ToUpperInvariant(term[next]) && ++next == term.Length) return true;
        return false;
    }
    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; return; }
        if (e.Key == Key.Enter) { Confirm(sender, e); e.Handled = true; return; }
        if (e.Key is not (Key.Up or Key.Down)) return;
        var direction = e.Key == Key.Down ? 1 : -1;
        var index = Results.SelectedIndex;
        while ((index += direction) >= 0 && index < Results.Items.Count)
            if (((DesktopAction)Results.Items[index]).IsEnabled) { Results.SelectedIndex = index; Results.ScrollIntoView(Results.SelectedItem); break; }
        e.Handled = true;
    }
    private void Confirm(object sender, RoutedEventArgs e)
    { if (Results.SelectedItem is DesktopAction { IsEnabled: true } action) { Chosen = action; DialogResult = true; } }
}
