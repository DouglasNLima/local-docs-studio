using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunStatePerformance(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var measurements = new List<object>(); Application? app = null; Window? visual = null;
        try
        {
            foreach (var (states, transitions) in new[] { (10, 15), (30, 60), (100, 150) })
            {
                var root = Path.Combine(output, "fixture-" + states); Directory.CreateDirectory(root); var stateRoot = Path.Combine(root, "state"); Directory.CreateDirectory(stateRoot);
                File.WriteAllText(Path.Combine(stateRoot, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
                var source = "stateDiagram-v2\n" + string.Join('\n', Enumerable.Range(0, states).Select(i => "state \"State " + i + "\" as S" + i)) + "\n"
                    + string.Join('\n', Enumerable.Range(0, transitions).Select(i => "S" + (i % states) + " --> S" + ((i * 7 + 1) % states) + " : Review " + i)) + "\n";
                var path = Path.Combine(root, "State.mmd"); File.WriteAllText(path, source);
                var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var argument in new[] { "--state-directory", stateRoot, root, path }) info.ArgumentList.Add(argument);
                app = Application.Launch(info); var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The State performance fixture did not open.");
                Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "The State performance preview action did not appear.");
                using var process = Process.GetProcessById(app.ProcessId); process.Refresh(); var beforeMemory = process.WorkingSet64;
                var timer = Stopwatch.StartNew(); Find(main, "DiagramEditVisually0").AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("StateVisualEditor")))?.AsWindow()) is not null, "The State performance editor did not open.");
                Wait(() => Find(visual!, "StateApply").IsEnabled, "The State performance editor did not finish canonical mapping."); var openMs = timer.Elapsed.TotalMilliseconds;
                Find(visual!, "StateSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); timer.Restart();
                for (var i = 0; i < 20; i++) Find(visual!, "StateCanvas_state:S" + (i % states)).Patterns.SelectionItem.Pattern.Select(); var selectionMs = timer.Elapsed.TotalMilliseconds;
                if (ReadEditorText(Find(visual!, "StateSource")) != source) throw new IOException("Performance selection changed State source.");
                Find(visual!, "StateActualSize").AsButton().Invoke(); var viewport = Find(visual!, "StateViewport").BoundingRectangle;
                var target = visual!.FindAllDescendants().FirstOrDefault(item => item.AutomationId.StartsWith("StateCanvas_state:", StringComparison.Ordinal)
                    && !item.IsOffscreen && viewport.Contains(item.BoundingRectangle));
                string? drag = null;
                if (target is not null)
                {
                    var bounds = target.BoundingRectangle; var centre = new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2); Mouse.MoveTo(centre); Mouse.Down(MouseButton.Left);
                    try { for (var step = 1; step <= 20; step++) { Mouse.MoveTo(centre.X + step, centre.Y + step); Thread.Sleep(8); } } finally { Mouse.Up(MouseButton.Left); }
                    drag = Find(visual, "StateStatus").Properties.HelpText.ValueOrDefault;
                    if (string.IsNullOrWhiteSpace(drag)) throw new IOException("The visible State performance drag did not produce actual pointer updates.");
                }
                if (ReadEditorText(Find(visual!, "StateSource")) != source || File.ReadAllText(path) != source || app.HasExited || !process.Responding) throw new IOException("State performance interaction changed source or stopped responding.");
                process.Refresh(); measurements.Add(new { States = states, Transitions = transitions, EditorOpenIncludingUiaMilliseconds = openMs,
                    TwentyCanvasSelectionsIncludingUiaMilliseconds = selectionMs, WorkingSetBeforeEditor = beforeMemory, WorkingSetAfterInteractions = process.WorkingSet64,
                    ActualPointerDrag = drag, DragLimit = target is null ? "No complete state target in the actual-size viewport; no large-diagram pointer performance claim." : null, SourceExact = true, ProcessResponding = true });
                Find(visual!, "StateFit").AsButton().Invoke(); _ = DwmFlush(); visual.CaptureToFile(Path.Combine(output, "state-" + states + ".png"));
                app.Kill(); app.Dispose(); app = null; visual = null;
            }
            File.WriteAllText(Path.Combine(output, "state-performance-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Measurements = measurements, Scope = "Real hosted native process; timings include UIA transport and cold conversion. No rendering limit was increased." }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error) { try { visual?.CaptureToFile(Path.Combine(output, "failure.png")); } catch { } File.WriteAllText(Path.Combine(output, "state-performance-failure.txt"), error.ToString()); Console.Error.WriteLine(error); return 1; }
        finally { Mouse.Up(MouseButton.Left); if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); } }
    }).GetAwaiter().GetResult();
}
