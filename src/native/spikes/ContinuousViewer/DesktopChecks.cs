using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using ContinuousViewer.Model;

namespace ContinuousViewer;

internal static class DesktopChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); HarnessSupport.Pixel(output);
        var dispatcher = Dispatcher.CurrentDispatcher;
        var task = Task.Run(() =>
        {
            var checks = new List<object>();
            void Check(string name, bool passed, object? detail = null)
            {
                checks.Add(new { name, passed, detail });
                File.WriteAllText(Path.Combine(output, "desktop.json"), JsonSerializer.Serialize(new
                { environment = "Genuine disposable GitHub-hosted Windows", physicalScreenReader = "NOT CERTIFIED", checks }, new JsonSerializerOptions { WriteIndented = true }));
                if (!passed) throw new IOException("Hosted interaction feasibility failed: " + name);
            }
            void Wait(Func<bool> predicate)
            {
                var watch = Stopwatch.StartNew();
                while (!predicate()) { if (watch.Elapsed.TotalSeconds > 20) throw new TimeoutException("Hosted native interaction exceeded 20 seconds."); Thread.Sleep(30); }
            }
            using var automation = new UIA3Automation();
            foreach (var fixtureName in new[] { "list", "rich", "large-4mb" })
            {
                var executable = Path.Combine(AppContext.BaseDirectory, "ContinuousViewer.exe");
                using var app = FlaUI.Core.Application.Launch(executable, $"--interactive {fixtureName} \"{output}\"");
                try
                {
                    var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The isolated spike window did not appear.");
                    var status = window.FindFirstDescendant(c => c.ByAutomationId("Readiness")) ?? throw new IOException("Readiness control missing.");
                    Wait(() => status.Name == "Complete logical document; viewport virtualised");
                    var viewer = window.FindFirstDescendant(c => c.ByAutomationId("ContinuousDocument")) ?? throw new IOException("Document control missing.");
                    var text = viewer.Patterns.Text.Pattern;
                    var expected = LogicalDocument.Build(new LensDocsStudio.Core.Parsing.MarkdownService().Parse(FixtureCatalogue.All.Single(f => f.Name == fixtureName).Source));
                    Check(fixtureName + ": real cross-process TextPattern reads offscreen logical content", text.DocumentRange.GetText(-1) == expected.Text);
                    var last = FixtureCatalogue.All.Single(f => f.Name == fixtureName).Last;
                    var range = text.DocumentRange.FindText(last, true, false);
                    Check(fixtureName + ": cross-process search discovers never-materialised content", range is not null && range.GetText(-1) == last);
                    window.Focus(); viewer.Click();
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.DOWN);
                    Wait(() => text.GetSelection().Single().GetText(-1).Length > 0);
                    Check(fixtureName + ": actual keyboard extends selection across native blocks", text.GetSelection().Single().GetText(-1) == expected.Text[..expected.Pieces[1].Start]);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.END);
                    Wait(() => text.GetSelection().Single().GetText(-1) == expected.Text);
                    Check(fixtureName + ": actual keyboard selects across the virtualised document", true);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
                    Wait(() => dispatcher.Invoke(() => Clipboard.ContainsText() && Clipboard.GetText() == expected.Text));
                    Check(fixtureName + ": actual Windows clipboard receives complete Unicode RTF HTML and Markdown", dispatcher.Invoke(() =>
                    {
                        var data = Clipboard.GetDataObject(); return data is not null && data.GetDataPresent(DataFormats.Rtf) && data.GetDataPresent(DataFormats.Html) && data.GetDataPresent("text/markdown")
                            && (string)data.GetData(DataFormats.UnicodeText)! == expected.Text;
                    }));
                    Keyboard.Type(VirtualKeyShort.NEXT);
                    Check(fixtureName + ": physical scrolling preserves selection", text.GetSelection().Single().GetText(-1) == expected.Text);
                    (window.FindFirstDescendant(c => c.ByAutomationId("NavigateDistant")) ?? throw new IOException("Navigation control missing.")).Patterns.Invoke.Pattern.Invoke();
                    var target = expected.AtSource(expected.Parsed.Source.LastIndexOf(last, StringComparison.Ordinal));
                    var targetSelection = expected.Text[target.Start..target.End];
                    // The previous whole-document selection also contains last; it is not navigation completion.
                    Wait(() => text.GetSelection().Single().GetText(-1) == targetSelection && text.GetSelection().Single().GetBoundingRectangles().Length > 0);
                    Check(fixtureName + ": actual distant target has on-screen automation bounds", text.GetSelection().Single().GetBoundingRectangles().Length > 0);
                    window.CaptureToFile(Path.Combine(output, fixtureName + "-distant.png"));
                    // A physical drag spans two visible native pieces. Selection is read through the document TextPattern.
                    viewer.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
                    var bounds = viewer.BoundingRectangle;
                    Mouse.MoveTo((int)bounds.Left + 50, (int)bounds.Top + 20); Mouse.Down();
                    Mouse.MoveTo((int)bounds.Left + 130, (int)bounds.Top + 110); Mouse.Up();
                    Wait(() => text.GetSelection().Single().GetText(-1).Length > 0);
                    Check(fixtureName + ": physical mouse drag selects across controls", text.GetSelection().Single().GetText(-1).Contains('\n'));
                    window.CaptureToFile(Path.Combine(output, fixtureName + "-drag.png"));
                }
                finally { if (!app.HasExited) app.Close(); }
            }
        });
        HarnessSupport.Pump(task, 60000);
    }
}
