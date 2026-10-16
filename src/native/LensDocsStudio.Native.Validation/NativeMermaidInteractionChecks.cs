using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunMermaidInteractions(string executable, string output, bool baseline, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var observations = new List<object>(); var started = DateTime.UtcNow;
        Process? observedProcess = null; Application? app = null; Window? main = null; Window? visual = null;
        try
        {
            foreach (var count in new[] { 20, 100, 300 })
            {
                var source = InteractionFixture(count);
                var fixture = Path.Combine(output, "drag-" + count + ".mmd"); File.WriteAllText(fixture, source);
                var launch = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, RedirectStandardError = true };
                launch.ArgumentList.Add("--state-directory"); launch.ArgumentList.Add(Path.Combine(output, "state-" + count)); launch.ArgumentList.Add(fixture);
                observedProcess = Process.Start(launch) ?? throw new IOException("Process launch failed."); app = Application.Attach(observedProcess.Id);
                main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Native window did not open.");
                main.Patterns.Transform.Pattern.Resize(1280, 800);
                Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "Preview Visual Edit action missing.");
                Find(main, "DiagramEditVisually0").AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "Visual Edit did not open.");
                Wait(() => Find(visual!, "MermaidDraftState").Name != "Validating...", "Canonical rendering did not finish.");
                var editable = Find(visual!, "MermaidApply").IsEnabled;
                visual!.CaptureToFile(Path.Combine(output, "before-" + count + ".png"));
                // The 300-node case can exceed the existing editable edge budget. Retain that boundary.
                if (!editable) { observations.Add(new { Nodes = count, Editable = false, Status = Find(visual, "MermaidStatus").Name }); }
                else
                {
                    Find(visual, "MermaidActualSize").AsButton().Invoke();
                    var samples = new List<double>();
                    for (var iteration = 0; iteration < 30; iteration++)
                    {
                        if (app.HasExited) throw new IOException("Application exited during node dragging: " + observedProcess!.ExitCode);
                        var node = Find(visual, "MermaidCanvas_node_N0"); var bounds = node.BoundingRectangle;
                        var start = new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                        var watch = Stopwatch.StartNew(); Mouse.MoveTo(start); Mouse.Down(MouseButton.Left);
                        try
                        {
                            for (var move = 0; move < 16; move++)
                                Mouse.MoveTo(new System.Drawing.Point(start.X + (move % 2 == 0 ? 45 : -20), start.Y + (move % 4) * 2));
                            Mouse.MoveTo(new System.Drawing.Point(start.X + 12, start.Y));
                            if (iteration % 5 == 0) Keyboard.Type(VirtualKeyShort.ESCAPE);
                        }
                        finally { Mouse.Up(MouseButton.Left); }
                        var draft = ReadEditorText(Find(visual, "MermaidDraftSource"));
                        if (draft != source) throw new IOException("Session dragging mutated the private source.");
                        if (!observedProcess!.Responding) throw new IOException("The native process stopped responding after pointer release.");
                        samples.Add(watch.Elapsed.TotalMilliseconds);
                        if (iteration % 3 == 2) Find(visual, "MermaidAutoLayout").AsButton().Invoke();
                    }
                    visual.CaptureToFile(Path.Combine(output, "after-" + count + ".png"));
                    observations.Add(new { Nodes = count, Editable = true, Drags = samples.Count, PointerMoves = samples.Count * 16,
                        RoundTripMilliseconds = samples, PrivateSourceExact = true, DiskSourceExact = File.ReadAllText(fixture) == source,
                        WorkingSet = observedProcess!.WorkingSet64, ProcessAlive = !app.HasExited });
                }
                Find(visual, "MermaidCancel").AsButton().Invoke();
                Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor"))) is null, "Cancel did not close the editor after stress.");
                app.Kill(); app.Dispose(); app = null; visual = null;
            }
            Record(true, null); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); } catch (Exception capture) { File.WriteAllText(Path.Combine(output, "capture-error.txt"), capture.ToString()); }
            if (observedProcess?.HasExited == true) File.WriteAllText(Path.Combine(output, "process-exit.txt"), observedProcess!.ExitCode + "\n" + observedProcess!.StandardError.ReadToEnd());
            Record(false, error.ToString()); Console.Error.WriteLine(error); return 1;
        }
        finally
        {
            Mouse.Up(MouseButton.Left);
            if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); }
            try
            {
                using var events = new EventLog("Application");
                File.WriteAllLines(Path.Combine(output, "windows-events.txt"), events.Entries.Cast<EventLogEntry>()
                    .Where(entry => entry.TimeGenerated.ToUniversalTime() >= started && entry.Source is ".NET Runtime" or "Application Error" or "Windows Error Reporting")
                    .Select(entry => entry.Source + "\n" + entry.Message));
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "event-log-unavailable.txt"), error.Message); }
        }
        void Record(bool passed, string? error) => File.WriteAllText(Path.Combine(output, "interactions.json"), JsonSerializer.Serialize(new
        { Passed = passed, Baseline = baseline, HighContrast = highContrast, Dpi = GetDpiForSystem(), Observations = observations, Error = error,
            Limitation = "Original private fixture unavailable; synthetic repeated pointer test. Round trips include input pacing and UIA transport, not per-frame latency." }, new JsonSerializerOptions { WriteIndented = true }));
    }).GetAwaiter().GetResult();

    private static string InteractionFixture(int count) => "flowchart TD\n    %% Synthetic stability fixture\n"
        + string.Join('\n', Enumerable.Range(0, count).Select(index => index % 5 == 1 ? $"N{index}{{Decision {index}}}" : $"N{index}[\"Node {index}<br/>Second line\"]"))
        + "\n" + string.Join('\n', Enumerable.Range(0, Math.Min(count - 1, 175)).Select(index => $"N{index} -->|Next {index}| N{index + 1}"))
        + "\nN0 --> N4\nN0 --> N7\nN4 --> N9\nN9 --> N2\nN7 --> N12\nN12 --> N4\n";
}
