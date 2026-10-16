using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class WorkspaceMapWindow : Window
{
    private readonly WorkspaceMapViewModel model;
    public (string Path, int Start, int Length)? Navigation { get; private set; }
    public bool DiagramRequested { get; private set; }
    public string? DiagramPath { get; private set; }
    public WorkspaceMapWindow(WorkspaceMapViewModel model)
    {
        this.model = model; InitializeComponent(); DataContext = model; WindowStateService.FitInitialWindow(this);
        model.OpenRequested += (path, start, length) => { Navigation = (path, start, length); DialogResult = true; };
        model.DiagramRequested += path => { DiagramRequested = true; DiagramPath = path; DialogResult = true; };
        Loaded += (_, _) => Query.Focus();
    }
    private void LinkSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox selected || selected.SelectedItem is not WorkspaceMapLink link) return;
        if (selected == Incoming) Outgoing.SelectedItem = null; else Incoming.SelectedItem = null;
        model.SelectedLink = link;
    }
    private void MapKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Query.Focus(); Query.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }
    private async void CopyReport(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        button.IsEnabled = false;
        try
        {
            var report = await Task.Run(() => WorkspaceMapService.Markdown(model.Map));
            if (!IsVisible) return;
            Clipboard.SetText(report, TextDataFormat.UnicodeText); model.Status = "Workspace map report copied as Markdown. Source documents are unchanged.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { model.Status = "The map report could not be copied: " + ex.Message; }
        finally { button.IsEnabled = true; }
    }
}
