using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.LayoutExperiment;

internal static class MediaControlChecks
{
    public static async Task Run(ExperimentWindow experiment, string output)
    {
        experiment.Hide();
        // The controls are separate fresh processes when invoked by the focused script.
        // This route exercises the existing MainWindow button and ExportDiagram handler,
        // with the normal updater/start-up/session loader replaced by isolated test setup.
        var stockOutput = Path.Combine(output, "stock"); Directory.CreateDirectory(stockOutput);
        var fixture = LensDocsStudio.Native.Validation.ContinuousPreviewFixtures.Create(stockOutput, "mixed")[0];
        var path = Path.Combine(stockOutput, "fixture.md"); File.WriteAllText(path, fixture.Source);
        foreach (var name in new[] { "CommandIcons", "DesktopStyles", "FormStyles" })
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/LensDocsStudio.Native;component/Resources/" + name + ".xaml", UriKind.Relative) });
        ThemeService.Apply(Application.Current.Resources, "Light");
        var dialogs = new DesktopDialogs(); var state = Path.Combine(stockOutput, "isolated-state");
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        await model.OpenPathAsync(path, stockOutput);
        await model.UpdatePreferencesAsync(current => current with { Preview = (current.Preview ?? new()) with { ReadingMode = "Continuous" } });
        var stock = new LensDocsStudio.Native.MainWindow(model, dialogs, []); dialogs.Owner = stock;
        var type = typeof(LensDocsStudio.Native.MainWindow);
        var loaded = type.GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        stock.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), stock);
        void Invoke(string name, params object[] args) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(stock, args);
        Invoke("ApplyTheme"); Invoke("ApplyReadingPreferences"); Invoke("BindActiveDocument"); Invoke("SetLayout", "Preview"); stock.Show();
        await (Task)type.GetMethod("UpdatePreviewAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(stock, [false])!;
        var preview = (DocumentPreview)stock.FindName("Preview");
        var firstMedia = preview.Document.Blocks.OfType<System.Windows.Documents.BlockUIContainer>().FirstOrDefault();
        if (firstMedia is null) throw new IOException("The stock control has no real media block.");
        firstMedia.BringIntoView(); stock.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        try { await MediaChecks.Exercise(stock, stockOutput, "stock"); }
        finally { stock.Close(); }
        if (File.ReadAllText(path) != fixture.Source) throw new IOException("The stock control changed the source.");

        var controlOutput = Path.Combine(output, "native-dialogue-only"); Directory.CreateDirectory(controlOutput);
        var control = new Window { Title = "EXPERIMENT ONLY — Native dialogue control", Width = 360, Height = 180 };
        var panel = new StackPanel(); control.Content = panel;
        var svg = new DiagramService().Render("flowchart LR\n A[Native control] --> B[Complete]").Svg!;
        foreach (var format in new[] { "SVG", "PNG" })
        {
            var button = new Button { Content = format, Margin = new Thickness(8) }; panel.Children.Add(button);
            button.Click += async (_, _) =>
            {
                var dialogue = new Microsoft.Win32.SaveFileDialog { FileName = "diagram." + format.ToLowerInvariant(), Filter = format + " diagram|*." + format.ToLowerInvariant() };
                if (dialogue.ShowDialog(control) != true) return;
                var bytes = await ExperimentWindow.Worker(() => format == "SVG" ? System.Text.Encoding.UTF8.GetBytes(new NativeSvgService().Prepare(svg)) : NativeSvgService.ToPng(new NativeSvgService().Render(svg)));
                await File.WriteAllBytesAsync(dialogue.FileName, bytes);
            };
        }
        control.Show();
        try { await MediaChecks.Exercise(control, controlOutput, "native-dialogue-only"); }
        finally { control.Close(); }
    }
}
