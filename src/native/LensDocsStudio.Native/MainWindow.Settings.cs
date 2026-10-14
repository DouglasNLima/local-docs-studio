using System.Windows;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        var settings = WorkspaceSettings.From(model.Preferences) with { Theme = model.Theme, Layout = model.Layout };
        var editorFocus = Editor.IsKeyboardFocusWithin; var previewFocus = Preview.IsKeyboardFocusWithin;
        new SettingsWindow(new SettingsViewModel(settings, async updated =>
        {
            var themeChanged = model.Theme != updated.Theme;
            var diagramThemeChanged = WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme != updated.Preview.DiagramTheme;
            await model.UpdatePreferencesAsync(updated.ApplyTo);
            model.Theme = updated.Theme; SetLayout(updated.Layout); ApplyReadingPreferences();
            if (themeChanged) ApplyTheme();
            if (themeChanged || diagramThemeChanged) QueuePreview();
            model.Status = "Settings saved.";
        }), Path.Combine(model.StateDirectory, "export-recovery")) { Owner = this }.ShowDialog();
        if (editorFocus && Editor.IsVisible) Editor.Focus(); else if (previewFocus && Preview.IsVisible) Preview.Focus();
    }

    private void ApplyReadingPreferences()
    {
        var settings = WorkspaceSettings.From(model.Preferences);
        EditorPreferenceService.Apply(Editor, settings.Editor);
        Preview.MinZoom = 50; Preview.MaxZoom = 200; Preview.Zoom = settings.Preview.Zoom;
        Preview.ApplyReadingMode(settings.Preview.ReadingMode);
        updatingSyncControls = true;
        try { ReadingModeControl.ItemsSource = WorkspaceSettings.ReadingModes; ReadingModeControl.SelectedItem = settings.Preview.ReadingMode; }
        finally { updatingSyncControls = false; }
        RefreshSyncControls();
        RefreshDiagramThemeChecks();
        ApplyPanels();
    }
}
