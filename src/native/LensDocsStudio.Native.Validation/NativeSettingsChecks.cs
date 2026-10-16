using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSettingsChecks
{
    public static void Run(string output)
    {
        var chosen = new WorkspaceSettings("Dark", "Preview", new("Consolas", 18, false, false, 2, true), new(125, DiagramTheme: "Forest"), false);
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(700, 750), new Size(560, 520) })
        {
            var saves = 0; var model = new SettingsViewModel(chosen, _ => { saves++; return Task.CompletedTask; });
            var window = new SettingsWindow(model, Path.Combine(output, "settings-recovery")); var content = (Grid)window.Content; window.Content = null;
            NameScope.SetNameScope(content, NameScope.GetNameScope(window)); content.DataContext = model;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native settings validation") { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface;
            void Layout() { surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout(); }
            Layout();
            var font = (ComboBox)window.FindName("EditorFont"); var fontSize = (ComboBox)window.FindName("EditorSize");
            var zoom = (Slider)window.FindName("Zoom");
            var diagramTheme = (ComboBox)window.FindName("DiagramTheme");
            if ((string)font.SelectedItem != "Consolas" || (int)fontSize.SelectedItem != 18 || zoom.Value != 125 || (string)diagramTheme.SelectedItem != "Forest")
                throw new IOException("Saved reading preferences did not populate native settings controls.");
            fontSize.SelectedItem = 20; zoom.Value = 150; diagramTheme.SelectedItem = "Neutral"; Layout();
            if (model.FontSize != 20 || model.PreviewZoom != 150 || model.DiagramTheme != "Neutral" || saves != 0) throw new IOException("Settings controls saved before Apply or lost their bindings.");
            var buttons = Descendants(content).OfType<Button>().ToArray();
            foreach (var button in buttons.Where(button => AutomationProperties.GetAutomationId(button).StartsWith("Settings", StringComparison.Ordinal)))
                if (button.ActualHeight < 20 || button.TranslatePoint(new(0, button.ActualHeight), surface).Y > size.Height)
                    throw new IOException("Settings actions were clipped at the validated window size.");
            foreach (var control in new FrameworkElement[] { font, fontSize, zoom, diagramTheme, (ComboBox)window.FindName("Theme"), (ComboBox)window.FindName("Layout"), (ComboBox)window.FindName("Indentation") })
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))) throw new IOException("A settings input has no accessible name.");
            foreach (var label in Descendants(content).OfType<Label>())
            {
                if (label.Foreground is not SolidColorBrush foreground || surface.Background is not SolidColorBrush background
                    || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5) throw new IOException("A settings label lost readable contrast.");
                if (label.Target is not FrameworkElement target || target.TranslatePoint(new(), content).Y < label.TranslatePoint(new(0, label.ActualHeight), content).Y - 0.5
                    || Math.Abs(target.TranslatePoint(new(), content).X - label.TranslatePoint(new(), content).X) > 1)
                    throw new IOException("A settings label overlaps its control or has a misaligned control edge: " + label.Content
                        + " label=" + new Rect(label.TranslatePoint(new(), content), label.RenderSize)
                        + " target=" + (label.Target is FrameworkElement input ? new Rect(input.TranslatePoint(new(), content), input.RenderSize).ToString() : "missing"));
            }
            var search = (TextBox)window.FindName("SettingsSearch");
            search.Text = "diagram"; Layout();
            if (!diagramTheme.IsVisible || font.IsVisible || model.FontSize != 20 || model.PreviewZoom != 150 || saves != 0)
                throw new IOException("Settings search lost staged values, failed to filter, or saved without Apply.");
            search.Text = "no-such-setting"; Layout();
            if (((TextBlock)window.FindName("NoSettingsMatches")).Visibility != Visibility.Visible) throw new IOException("Settings search has no empty-result guidance.");
            search.Text = "Preview"; Layout();
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-settings-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(file);
            search.Text = ""; Layout();
            model.ResetCommand.Execute(null); Layout();
            if (model.FontSize != 14 || model.PreviewZoom != 100 || model.Theme != "System" || model.DiagramTheme != "Auto" || saves != 0) throw new IOException("Restoring defaults persisted without Apply.");
            host.RootVisual = null; surface.Child = null; window.Close();
            if (saves != 0) throw new IOException("Closing settings persisted cancelled changes.");
        }

        WorkspaceSettings? applied = null; var notifications = 0;
        var successful = new SettingsViewModel(chosen, settings => { applied = settings; return Task.CompletedTask; });
        successful.Applied += () => notifications++; successful.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        if (applied != chosen || notifications != 1 || successful.IsSaving) throw new IOException("Settings Apply did not save the exact edited choices once.");
        var failed = new SettingsViewModel(chosen, _ => Task.FromException(new IOException("Fixture access failure")));
        failed.Applied += () => throw new IOException("Failed persistence closed the settings window.");
        failed.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        if (!failed.Status.Contains("Fixture access failure", StringComparison.Ordinal) || !failed.CanEdit || failed.FontSize != 18)
            throw new IOException("A failed settings save lost edits or left controls disabled.");

        var importSaves = 0;
        var importedModel = new SettingsViewModel(chosen, _ => { importSaves++; return Task.CompletedTask; });
        importedModel.Import(System.Text.Encoding.UTF8.GetBytes("""{"formatVersion":"lens-docs-studio-settings-1.0","theme":"Light","layout":"Editor","showOutline":true}"""));
        var selectedImport = chosen with { Theme = "Light", Layout = "Editor", ShowOutline = true };
        if (importedModel.Current != selectedImport || importSaves != 0 || !importedModel.Status.Contains("review", StringComparison.Ordinal))
            throw new IOException("Imported settings were not staged privately for review or replaced absent editor choices.");
        if (SettingsTransferFormat.Read(SettingsTransferFormat.Write(importedModel.Current), WorkspaceSettings.Default) != selectedImport || importSaves != 0)
            throw new IOException("Exporting selected settings changed saved preferences or lost selected choices.");
        try { importedModel.Import(System.Text.Encoding.UTF8.GetBytes("""{"formatVersion":"lens-docs-studio-settings-1.0","theme":"Unknown"}""")); throw new IOException("Invalid imported settings were accepted."); }
        catch (InvalidDataException) { }
        if (importedModel.Current != selectedImport || importSaves != 0) throw new IOException("A rejected import changed edited or saved settings.");
        importedModel.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        if (importSaves != 1) throw new IOException("Applying imported settings did not save exactly once.");

        const string source = "# Café settings\r\n\tOriginal tab\r\n";
        var editor = new TextEditor { Text = source, WordWrap = true, ShowLineNumbers = true };
        var document = editor.Document; document.Insert(document.TextLength, "Unsaved change"); editor.Select(2, 4);
        using (var host = new HwndSource(new HwndSourceParameters("Native editor settings validation") { Width = 600, Height = 300, WindowStyle = unchecked((int)0x80000000) }))
        {
            host.RootVisual = editor; editor.Measure(new Size(600, 300)); editor.Arrange(new Rect(0, 0, 600, 300)); editor.UpdateLayout();
            EditorPreferenceService.Apply(editor, chosen.Editor);
            if (editor.Document != document || editor.Text != source + "Unsaved change" || editor.SelectedText != "Café"
                || editor.FontSize != 18 || editor.FontFamily.Source != "Consolas" || editor.WordWrap || editor.ShowLineNumbers
                || editor.Options.IndentationSize != 2 || !editor.Options.ConvertTabsToSpaces)
                throw new IOException("Applying editor preferences changed source/selection or failed to configure actual native controls.");
            document.UndoStack.Undo(); if (editor.Text != source) throw new IOException("Settings changed the document undo history.");
            document.UndoStack.Redo(); if (editor.Text != source + "Unsaved change") throw new IOException("Settings changed the document redo history.");
        }
        File.WriteAllText(Path.Combine(output, "native-settings.json"), JsonSerializer.Serialize(new { Passed = true, Checks = new[] {
            "Native settings bind and fit minimum/normal sizes in both themes with accessible readable inputs",
            "Defaults and Cancel keep preferences private until Apply",
            "Apply saves once and failed saves retain editable choices",
            "Imported appearance choices remain private until Apply, exported choices round-trip, and invalid imports preserve current edits",
            "Actual editor font, wrap, line numbers and indentation change without altering source, selection or Undo/Redo" } }));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
