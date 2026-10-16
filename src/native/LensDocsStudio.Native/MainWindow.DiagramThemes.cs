using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private string CurrentDiagramTheme => DiagramThemes.ResolveName(WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme,
        ThemeService.IsDark(model.Theme));

    private async void ChangeDiagramTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string choice } || !DiagramThemes.Choices.Contains(choice)) return;
        try
        {
            await model.UpdatePreferencesAsync(current => current with { Preview = (current.Preview ?? new()) with { DiagramTheme = choice } });
            QueuePreview(); model.Status = "Diagram theme: " + choice;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { model.Status = "Diagram theme could not be saved: " + error.Message; }
        finally { RefreshDiagramThemeChecks(); }
    }

    private void RefreshDiagramThemeChecks()
    {
        var selected = WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme;
        foreach (var item in DiagramThemeMenu.Items.OfType<MenuItem>()) item.IsChecked = Equals(item.Tag, selected);
    }
}
