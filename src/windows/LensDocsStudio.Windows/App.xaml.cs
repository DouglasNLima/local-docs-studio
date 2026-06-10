using LensDocsStudio.Windows.Services;
using LensDocsStudio.Windows.Smoke;
using Microsoft.UI.Xaml;

namespace LensDocsStudio.Windows;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var launchArguments = Environment.GetCommandLineArgs().Skip(1).ToList();
        if (launchArguments.Count == 0 && !string.IsNullOrWhiteSpace(args.Arguments))
        {
            launchArguments = SmokeOptions.SplitArguments(args.Arguments);
        }

        var smokeOptions = SmokeOptions.Parse(launchArguments);
        var startupFilePath = StartupFileArguments.GetStartupFilePath(launchArguments);
        window = new MainWindow(smokeOptions, startupFilePath);
        window.Activate();
    }
}
