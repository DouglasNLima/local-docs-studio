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
                WriteUxContrastDiagnostic(output, executable, revision, resources, foreground, background);
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
                Directory.CreateDirectory(Path.Combine(workspace, "00 - Guides"));
                File.WriteAllText(Path.Combine(workspace, "01 - Markdown.md"), "# Markdown document\n");
                File.WriteAllText(Path.Combine(workspace, "02 - Mermaid.mmd"), "flowchart LR\nA --> B\n");
                File.WriteAllText(Path.Combine(workspace, "03 - Notes.txt"), "Plain text document\n");
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
                    window.Patterns.Transform.Pattern.Move(0, 0);
                    FocusWindow(window);
                    FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(size.Item1 / 2, size.Item2 / 2));
                    Wait(() => automation.GetDesktop().BoundingRectangle.Contains(window.BoundingRectangle)
                        && window.BoundingRectangle.Contains(Find(window, "Status").BoundingRectangle)
                        && !display.TaskbarOverlaps(Find(window, "Status").BoundingRectangle)
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
                    var overflowCommands = window.FindAllDescendants(cf => cf.ByName("More commands")).Select(command => new {
                        command.IsOffscreen, Bounds = command.BoundingRectangle }).ToArray();
                    window.CaptureToFile(Path.Combine(output, screenshot));
                    if (paths.Any(path => !File.ReadAllText(path).StartsWith("# Documentation workspace\n", StringComparison.Ordinal)))
                        throw new IOException("Screenshot capture changed a synthetic document.");
                    observations.Add(new { Theme = theme, RequestedWidth = size.Item1, RequestedHeight = size.Item2,
                        Bounds = window.BoundingRectangle, DesktopBounds = automation.GetDesktop().BoundingRectangle,
                        Revision = revision, Screenshot = screenshot, HighContrast = highContrast, OverflowCommands = overflowCommands,
                        PhysicalDpiCertified = false, AccessibilityCertified = false });
                }
                catch
                {
                    try
                    {
                    var failed = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                    failed?.CaptureToFile(Path.Combine(output, "native-screenshot-failure.png"));
                    if (failed is not null)
                    {
                        var strip = Find(failed, "DocumentTabs");
                        File.WriteAllText(Path.Combine(output, "native-screenshot-failure-tabs.json"), JsonSerializer.Serialize(new {
                            Strip = strip.BoundingRectangle,
                            Tabs = strip.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem))
                                .Select(tab => new { tab.Name, tab.IsOffscreen, Bounds = tab.BoundingRectangle,
                                    Buttons = tab.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                                        .Select(button => new { button.Name, button.IsOffscreen, Bounds = button.BoundingRectangle }).ToArray() }).ToArray()
                        }));
                    }
                    }
                    catch (Exception diagnostic) { File.WriteAllText(Path.Combine(output, "native-screenshot-failure-capture.txt"), diagnostic.ToString()); }
                    throw;
                }
                finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(10000); } }
            }
            display.Complete();
            File.WriteAllText(Path.Combine(output, "screenshots.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.json"), JsonSerializer.Serialize(new { Error = error.ToString(), Observations = observations })); Console.Error.WriteLine(error); return 1; }
    }
    // Read only, after the original resource evaluation: do not prime WPF's colour
    // cache before Apply, pump the dispatcher, reapply a theme or repair a value.
    // This describes the validation process; the capture child has not started.
    private static void WriteUxContrastDiagnostic(string output, string executable, string revision,
        System.Windows.ResourceDictionary resources, System.Windows.Media.SolidColorBrush foreground,
        System.Windows.Media.SolidColorBrush background)
    {
        object Colour(System.Windows.Media.Color colour) => new { argb = colour.ToString(), colour.A, colour.R, colour.G, colour.B };
        System.Windows.Media.Color OsColour(int index)
        {
            var value = UxGetSysColor(index);
            return System.Windows.Media.Color.FromRgb((byte)value, (byte)(value >> 8), (byte)(value >> 16));
        }
        object Brush(System.Windows.Media.SolidColorBrush brush)
        {
            var binding = System.Windows.Data.BindingOperations.GetBinding(brush, System.Windows.Media.SolidColorBrush.ColorProperty);
            var requested = binding?.Source?.GetType().GetProperty("Colour")?.GetValue(binding.Source);
            return new { effective = Colour(brush.Color), requested = requested is System.Windows.Media.Color colour ? Colour(colour) : null,
                bindingStatus = System.Windows.Data.BindingOperations.GetBindingExpression(brush, System.Windows.Media.SolidColorBrush.ColorProperty)?.Status.ToString(), brush.IsFrozen };
        }
        object Binary(string path) => new { path = Path.GetFullPath(path), sha256 = Hash(path),
            version = FileVersionInfo.GetVersionInfo(path).ProductVersion };
        var systemForeground = System.Windows.SystemColors.HighlightTextColor;
        var systemBackground = System.Windows.SystemColors.HighlightColor;
        var osForeground = OsColour(14); // COLOR_HIGHLIGHTTEXT
        var osBackground = OsColour(13); // COLOR_HIGHLIGHT
        var report = new
        {
            atUtc = DateTimeOffset.UtcNow, scope = "VALIDATION_PROCESS_PRE_ASSERTION; capture application not yet launched",
            revisionArgument = revision, githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"), attempt = Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT"),
            runnerImage = Environment.GetEnvironmentVariable("ImageOS"), imageVersion = Environment.GetEnvironmentVariable("ImageVersion"),
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, osVersion = Environment.OSVersion.VersionString,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processId = Environment.ProcessId, sessionId = Process.GetCurrentProcess().SessionId,
            harness = Binary(typeof(NativeUiChecks).Assembly.Location), themeAssembly = Binary(typeof(LensDocsStudio.Native.Platform.ThemeService).Assembly.Location),
            wpf = Binary(typeof(System.Windows.Application).Assembly.Location), targetExecutable = Binary(executable),
            targetAssembly = Binary(Path.ChangeExtension(executable, ".dll")),
            requestedTheme = "System", wpfHighContrast = System.Windows.SystemParameters.HighContrast, osHighContrast = NativeTestHighContrast.IsEnabledNow,
            themeDictionaries = resources.MergedDictionaries.SelectMany(dictionary => dictionary.MergedDictionaries).Select(dictionary => dictionary.Source?.ToString()).ToArray(),
            pressedText = Brush(foreground), pressedBackground = Brush(background),
            wpfHighlightText = Colour(systemForeground), wpfHighlight = Colour(systemBackground),
            win32HighlightText = Colour(osForeground), win32Highlight = Colour(osBackground),
            pressedContrast = NativeSyntaxChecks.Contrast(foreground.Color, background.Color),
            wpfContrast = NativeSyntaxChecks.Contrast(systemForeground, systemBackground),
            win32Contrast = NativeSyntaxChecks.Contrast(osForeground, osBackground),
            foregroundEqualsWpfHighlightText = foreground.Color == systemForeground, requiredContrast = 4.5,
            screenshotInputs = new { themes = new[] { "System" }, sizes = new[] { "860x540", "1280x800", "1440x900", "1920x1080" },
                source = "Unchanged synthetic Documentation workspace fixture in NativeUxScreenshots.cs", capturesStarted = false }
        };
        File.WriteAllText(Path.Combine(output, "high-contrast-pre-assertion.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetSysColor")]
    private static extern uint UxGetSysColor(int index);
}
