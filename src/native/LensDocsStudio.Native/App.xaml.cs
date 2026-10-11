using System.Windows;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var stateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensDocsStudio.Native");
        var args = e.Args.ToList();
        var stateOption = args.IndexOf("--state-directory");
        if (stateOption >= 0 && stateOption + 1 < args.Count)
        {
            stateRoot = Path.GetFullPath(args[stateOption + 1]);
            args.RemoveRange(stateOption, 2);
        }
        var session = new SessionStore(stateRoot);
        var dialogs = new DesktopDialogs();
        var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(stateRoot, "write-recovery")), session, new WorkspaceService(), dialogs);
        ThemeService.Apply(Resources, "Light");
        var window = new MainWindow(model, dialogs, args.ToArray());
        dialogs.Owner = window;
        MainWindow = window;
        window.Show();
    }
}

