using System.Reflection;
using System.Windows;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record StockWindowForChecks(LensDocsStudio.Native.MainWindow Window, WorkspaceViewModel Model)
{
    public static async Task<StockWindowForChecks> Open(string path, string root, string state)
    {
        foreach (var name in new[] { "CommandIcons", "DesktopStyles", "FormStyles" })
        {
            var uri = new Uri("/LensDocsStudio.Native;component/Resources/" + name + ".xaml", UriKind.Relative);
            if (!Application.Current.Resources.MergedDictionaries.Any(dictionary => dictionary.Source == uri))
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
        }
        ThemeService.Apply(Application.Current.Resources, "Light");
        var dialogs = new DesktopDialogs();
        var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        await model.OpenPathAsync(path, root);
        await model.UpdatePreferencesAsync(current => current with { Preview = (current.Preview ?? new()) with { ReadingMode = "Continuous" } });
        var window = new LensDocsStudio.Native.MainWindow(model, dialogs, []); dialogs.Owner = window;
        var type = typeof(LensDocsStudio.Native.MainWindow);
        var loaded = type.GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        foreach (var name in new[] { "ApplyTheme", "ApplyReadingPreferences", "BindActiveDocument" }) type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        type.GetMethod("SetLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, ["Preview"]); window.Show();
        await (Task)type.GetMethod("UpdatePreviewAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [false])!;
        return new(window, model);
    }
    public async Task Close()
    {
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Window.Closed += (_, _) => closed.TrySetResult(true); Window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5)); Model.Dispose();
    }
}
