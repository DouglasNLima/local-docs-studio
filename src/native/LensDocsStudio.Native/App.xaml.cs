using System.Windows;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class App : Application
{
    private NativeInstanceService? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppContext.SetSwitch("Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop", true);
        if (e.Args is ["--apply-native-update", var ticket])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { await NativeUpdateLauncher.ApplyAsync(ticket); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { MessageBox.Show("The native update could not proceed safely.\n\n" + ex.Message, "Lens Docs Studio update"); }
            Shutdown(); return;
        }
        var stateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensDocsStudio.Native");
        var args = e.Args.ToList();
        var stateOption = args.IndexOf("--state-directory");
        if (stateOption >= 0 && stateOption + 1 < args.Count)
        {
            stateRoot = Path.GetFullPath(args[stateOption + 1]);
            args.RemoveRange(stateOption, 2);
        }
        instance = new NativeInstanceService(stateRoot);
        if (!instance.IsPrimary)
        {
            try { await instance.ForwardAsync(args.ToArray()); }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { MessageBox.Show("The existing native workspace could not be activated. Try again after it has finished starting.\n\n" + ex.Message, "Lens Docs Studio"); }
            Shutdown(); return;
        }
        var session = new SessionStore(stateRoot);
        var dialogs = new DesktopDialogs();
        var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(stateRoot, "write-recovery")), session, new WorkspaceService(), dialogs);
        var initialTheme = "System";
        try { initialTheme = (await session.ReadAsync<UserPreferences>("preferences.json", validate: WorkspaceSettings.ArePreferencesValid))?.Theme ?? "System"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* The normal session recovery reports unreadable preferences. */ }
        ThemeService.Apply(Resources, initialTheme);
        var window = new MainWindow(model, dialogs, args.ToArray());
        dialogs.Owner = window;
        MainWindow = window;
        window.Show();
        _ = instance.ListenAsync(paths => Dispatcher.InvokeAsync(async () =>
        {
            window.Activate();
            await model.RunAsync(async () =>
            {
                foreach (var path in paths)
                {
                    if (Directory.Exists(path)) await model.SetWorkspaceAsync(path);
                    else if (File.Exists(path) && WorkspaceService.IsDocument(path)) await model.OpenPathAsync(path);
                }
            });
        }).Task.Unwrap());
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}

