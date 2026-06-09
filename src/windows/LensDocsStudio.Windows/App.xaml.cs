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
        var smokeOptions = !string.IsNullOrWhiteSpace(args.Arguments)
            ? SmokeOptions.Parse(args.Arguments)
            : SmokeOptions.Parse(Environment.GetCommandLineArgs().Skip(1));
        window = new MainWindow(smokeOptions);
        window.Activate();
    }
}
