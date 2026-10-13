using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ContinuousViewer.Model;

namespace ContinuousViewer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            if (args.FirstOrDefault() == "--validate") { ValidationHarness.Run(Path.GetFullPath(args.ElementAtOrDefault(1) ?? "acceptance/continuous/validation")); return 0; }
            if (args.FirstOrDefault() == "--desktop") { RequireHostedWindows(); DesktopChecks.Run(Path.GetFullPath(args[1])); return 0; }
            if (args.FirstOrDefault() == "--stress") { StressChecks.Run(Path.GetFullPath(args[1])); return 0; }
            if (args.FirstOrDefault() == "--exports") { ExportChecks.Run(Path.GetFullPath(args[1])); return 0; }
            if (args.FirstOrDefault() == "--cluster") { ClusterChecks.Run(Path.GetFullPath(args[1])); return 0; }
            if (args.FirstOrDefault() == "--measure")
            {
                MeasurementHarness.Run(args[1], args[2], Path.GetFullPath(args[3])); return 0;
            }
            if (args.FirstOrDefault() == "--interactive")
            {
                // Agent desktop permission for earlier product builds does not authorise this new executable.
                RequireHostedWindows();
                var application = new Application(); var viewer = new VirtualDocumentView(); var loader = new DocumentLoader();
                var fixture = FixtureCatalogue.All.Single(f => f.Name == (args.ElementAtOrDefault(1) ?? "rich"));
                var root = args.ElementAtOrDefault(2);
                var dock = new DockPanel(); var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
                var end = new Button { Content = "Distant target", Padding = new Thickness(8, 3, 8, 3) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(end, "NavigateDistant");
                end.Click += async (_, _) => await viewer.NavigateAsync(fixture.Source.LastIndexOf(fixture.Last, StringComparison.Ordinal), true);
                toolbar.Children.Add(end); DockPanel.SetDock(toolbar, Dock.Top); dock.Children.Add(toolbar); dock.Children.Add(viewer);
                System.Windows.Automation.AutomationProperties.SetAutomationId(viewer, "ContinuousDocument");
                var status = new TextBlock { Text = "Logical document loading", Margin = new Thickness(8) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(status, "Readiness"); toolbar.Children.Add(status);
                var window = new Window { Title = "Lens Docs Studio — Continuous viewer feasibility", Width = 640, Height = 380, Content = dock };
                window.Loaded += async (_, _) => { if (await loader.LoadAsync(viewer, fixture.Source, root is null ? null : Path.Combine(root, "synthetic.md"), root)) status.Text = "Complete logical document; viewport virtualised"; };
                window.Closed += (_, _) => { loader.Dispose(); viewer.Dispose(); };
                application.Run(window); return 0;
            }
            Console.WriteLine("Experimental harness: --validate <output> | --measure <baseline|prototype> <fixture> <output> | --interactive <fixture> (disposable hosted Windows only)");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void RequireHostedWindows()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Interactive spike execution requires genuine disposable GitHub-hosted Windows. Local validation uses hidden HWNDs only.");
    }
}
