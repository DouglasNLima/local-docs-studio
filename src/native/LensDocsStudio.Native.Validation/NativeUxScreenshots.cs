using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int CaptureUxScreenshots(string executable, string output, string revision, bool highContrast = false)
    {
        // The caller enforces the disposable-runner boundary before any process is launched.
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        var observations = new List<object>();
        try
        {
            using var contrast = highContrast ? new NativeTestHighContrast() : null;
            if (highContrast && !System.Windows.SystemParameters.HighContrast) throw new IOException("The hosted Windows high-contrast setting was not applied.");
            if (highContrast)
            {
                _ = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                var resources = new System.Windows.ResourceDictionary(); LensDocsStudio.Native.Platform.ThemeService.Apply(resources, "System");
                var foreground = (System.Windows.Media.SolidColorBrush)resources["PressedTextBrush"];
                var background = (System.Windows.Media.SolidColorBrush)resources["PressedBrush"];
                if (foreground.Color != System.Windows.SystemColors.HighlightTextColor || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5)
                    throw new IOException("Actual Windows high-contrast command press colours lost readable text and icon contrast.");
            }
            using var display = new NativeTestDisplay(output);
            using var automation = new UIA3Automation();
            foreach (var theme in highContrast ? new[] { "System" } : new[] { "Light", "Dark" })
            foreach (var size in new[] { (860, 540), (1280, 800), (1440, 900), (1920, 1080) })
            {
                var run = Path.Combine(output, "synthetic-" + Guid.NewGuid().ToString("N"));
                var workspace = Path.Combine(run, "Documentation"); Directory.CreateDirectory(workspace);
                var state = Path.Combine(run, "private-state"); Directory.CreateDirectory(state);
                File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: theme, AutomaticUpdateChecks: false)));
                var paths = Enumerable.Range(1, 9).Select(index => Path.Combine(workspace, $"Document {index} — working notes.md")).ToArray();
                foreach (var path in paths) File.WriteAllText(path, "# Documentation workspace\n\nLocal Markdown, Mermaid, and documentation studio.\n\n## Project notes\n\nWrite clear **documentation**, review the source and share portable files.\n\n### Next action\n\n- [ ] Review the documentation\n- [x] Preserve local assets\n\n```mermaid\nflowchart LR\nWrite --> Review --> Share\n```\n\n## Reference material\n\nMore context for the team.\n");
                var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
                info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state); info.ArgumentList.Add(workspace);
                foreach (var path in paths) info.ArgumentList.Add(path);
                using var process = Process.Start(info) ?? throw new IOException("The synthetic screenshot application did not start.");
                using var app = FlaUI.Core.Application.Attach(process.Id);
                try
                {
                    var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The screenshot window did not appear.");
                    Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "The screenshot preview did not finish.");
                    window.Patterns.Transform.Pattern.Resize(size.Item1, size.Item2);
                    Wait(() => Math.Abs(window.BoundingRectangle.Width - size.Item1) < 3 && Math.Abs(window.BoundingRectangle.Height - size.Item2) < 3,
                        "The requested screenshot dimensions were not applied.");
                    Wait(() => window.BoundingRectangle.Contains(Find(window, "Status").BoundingRectangle)
                        && window.BoundingRectangle.Contains(Find(window, "DocumentPreview").BoundingRectangle),
                        "Native screenshot content did not finish layout inside the resized window.");
                    if (revision != "c566144529c3c0e4393c1604544201b936e4a6a2")
                    {
                        var tabStrip = Find(window, "DocumentTabs");
                        Wait(() => tabStrip.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem))
                            .Where(tab => tab.Name == Path.GetFileName(paths[^1]))
                            .SelectMany(tab => tab.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))
                            .Any(button => button.Name == "Close document" && !button.IsOffscreen && tabStrip.BoundingRectangle.Contains(button.BoundingRectangle)),
                            "The active tab's close command did not remain visible after resizing its overflow strip.");
                    }
                    var screenshot = $"native-{(highContrast ? "high-contrast" : theme.ToLowerInvariant())}-{size.Item1}x{size.Item2}.png";
                    window.CaptureToFile(Path.Combine(output, screenshot));
                    if (paths.Any(path => !File.ReadAllText(path).StartsWith("# Documentation workspace\n", StringComparison.Ordinal)))
                        throw new IOException("Screenshot capture changed a synthetic document.");
                    observations.Add(new { Theme = theme, RequestedWidth = size.Item1, RequestedHeight = size.Item2,
                        Bounds = window.BoundingRectangle, DesktopBounds = automation.GetDesktop().BoundingRectangle,
                        Revision = revision, Screenshot = screenshot, HighContrast = highContrast, PhysicalDpiCertified = false, AccessibilityCertified = false });
                }
                finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(10000); } }
            }
            File.WriteAllText(Path.Combine(output, "screenshots.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.json"), JsonSerializer.Serialize(new { Error = error.ToString(), Observations = observations })); Console.Error.WriteLine(error); return 1; }
    }
}
