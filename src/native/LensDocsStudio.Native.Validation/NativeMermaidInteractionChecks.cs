using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Mermaid;

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
                    var samples = new List<double>(); var updateMetrics = new List<string>();
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
                        updateMetrics.Add(Find(visual, "MermaidStatus").Properties.HelpText.ValueOrDefault ?? "");
                        if (iteration % 3 == 2) Find(visual, "MermaidAutoLayout").AsButton().Invoke();
                    }
                    visual.CaptureToFile(Path.Combine(output, "after-" + count + ".png"));
                    observations.Add(new { Nodes = count, Editable = true, Drags = samples.Count, PointerMoves = samples.Count * 16,
                        RoundTripMilliseconds = samples, UpdateMetrics = updateMetrics, PrivateSourceExact = true, DiskSourceExact = File.ReadAllText(fixture) == source,
                        WorkingSet = observedProcess!.WorkingSet64, ProcessAlive = !app.HasExited });
                }
                if (!baseline && count == 20 && editable) CandidateScenarios(source);
                Find(visual, "MermaidCancel").AsButton().Invoke();
                Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor"))) is null, "Cancel did not close the editor after stress.");
                app.Kill(); app.Dispose(); app = null; visual = null;
            }
            Record(true, null); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); } catch (Exception capture) { File.WriteAllText(Path.Combine(output, "capture-error.txt"), capture.ToString()); }
            Record(false, error.ToString());
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
        void CandidateScenarios(string original)
        {
            var checks = new List<string>(); var placements = new List<object>();
            var editor = Find(main!, "MarkdownEditor");
            string Draft() => ReadEditorText(Find(visual!, "MermaidDraftSource"));
            void Click(string id) => Find(visual!, id).AsButton().Invoke();
            void Ready(Func<string, bool> expected) => Wait(() => expected(Draft()) && Find(visual!, "MermaidApply").IsEnabled, "Candidate draft/render did not settle: " + Find(visual!, "MermaidStatus").Name);
            System.Drawing.Point Centre(string id)
            { var box = Find(visual!, id).BoundingRectangle; return new(box.Left + box.Width / 2, box.Top + box.Height / 2); }
            void Check(bool valid, string text) { if (!valid) throw new IOException(text); checks.Add(text); }
            void Capture(string name) { _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
            void Drag(System.Drawing.Point from, System.Drawing.Point to, bool cancel = false, string? capture = null)
            {
                Mouse.MoveTo(from); Mouse.Down(MouseButton.Left);
                try
                {
                    for (var step = 1; step <= 8; step++) Mouse.MoveTo(new(from.X + (to.X - from.X) * step / 8, from.Y + (to.Y - from.Y) * step / 8));
                    if (capture is not null) Capture(capture);
                    if (cancel) Keyboard.Type(VirtualKeyShort.ESCAPE);
                }
                finally { Mouse.Up(MouseButton.Left); }
            }
            var scroll = Find(visual!, "MermaidViewport").Patterns.Scroll.Pattern;
            foreach (var zoom in new[] { 50, 100, 150, 200 })
            {
                Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.SetValue(zoom);
                var viewport = Find(visual!, "MermaidViewport").BoundingRectangle;
                // Exercise real middle-button PAN before measuring the drop target.
                Mouse.DragHorizontally(new(viewport.Left + viewport.Width / 2, viewport.Top + viewport.Height / 2), -35, MouseButton.Middle);
                var point = new System.Drawing.Point(viewport.Left + viewport.Width / 2, viewport.Top + viewport.Height / 2);
                var horizontal = scroll.HorizontalScrollPercent.Value; var vertical = scroll.VerticalScrollPercent.Value;
                Drag(Centre("MermaidInsertProcess"), point, capture: "palette-preview-" + zoom);
                Ready(text => new MermaidSourceParser().Parse(text).Nodes.Count == 21);
                var centre = Centre("MermaidCanvas_node_Node1"); var distance = Math.Sqrt(Math.Pow(centre.X - point.X, 2) + Math.Pow(centre.Y - point.Y, 2));
                Check(distance <= 6, "Drop centre is within six physical pixels at measured 96 DPI, zoom " + zoom + "; actual error " + distance);
                Check(Find(visual!, "MermaidSelectionSummary").Name.StartsWith("Node1", StringComparison.Ordinal), "Inserted node is selected with its inspector at " + zoom);
                Check(ReadEditorText(editor) == original, "Palette insertion leaves owning source unchanged before Apply at " + zoom);
                placements.Add(new { Zoom = zoom, Target = point, Actual = centre, DistancePixels = distance, HorizontalBefore = horizontal, HorizontalAfter = scroll.HorizontalScrollPercent.Value, VerticalBefore = vertical, VerticalAfter = scroll.VerticalScrollPercent.Value });
                Capture("drop-" + zoom); Click("MermaidUndo"); Ready(text => text == original);
            }
            Click("MermaidActualSize"); Click("MermaidReset");
            var view = Find(visual!, "MermaidViewport").BoundingRectangle;
            var expectedCentres = new Dictionary<string, System.Drawing.Point>();
            foreach (var (palette, x, y) in new[] { ("Process", 25, 25), ("Decision", 55, 45), ("Circle", 75, 75) })
            {
                var id = "Node" + (expectedCentres.Count + 1); var point = new System.Drawing.Point(view.Left + view.Width * x / 100, view.Top + view.Height * y / 100);
                var count = 21 + expectedCentres.Count;
                Drag(Centre("MermaidInsert" + palette), point); Ready(text => new MermaidSourceParser().Parse(text).Nodes.Count == count);
                expectedCentres[id] = point;
                foreach (var (node, expected) in expectedCentres)
                    Check(Math.Abs(Centre("MermaidCanvas_node_" + node).X - expected.X) <= 6 && Math.Abs(Centre("MermaidCanvas_node_" + node).Y - expected.Y) <= 6, "Earlier insertion centre remains stable: " + node);
            }
            Capture("multiple-session-positions");
            Find(visual!, "MermaidCanvas_node_Node1").Click(); Find(visual!, "MermaidNodeLabel").AsTextBox().Text = "Session centre retained"; Click("MermaidUpdateNode");
            Ready(text => text.Contains("Session centre retained", StringComparison.Ordinal));
            foreach (var (node, expected) in expectedCentres) Check(Math.Abs(Centre("MermaidCanvas_node_" + node).X - expected.X) <= 6 && Math.Abs(Centre("MermaidCanvas_node_" + node).Y - expected.Y) <= 6, "Label rerender retains session centre: " + node);
            var beforeReconnect = Draft();
            if (!Find(visual!, "MermaidSelectionSummary").Name.Contains("Next 0", StringComparison.Ordinal)) Find(visual!, "MermaidCanvas_edge-0").Patterns.Toggle.Pattern.Toggle();
            Wait(() => visual!.FindFirstDescendant(cf => cf.ByAutomationId("MermaidDestinationEndpoint")) is not null, "Selected connection has no endpoint handles.");
            Capture("connection-endpoints");
            Drag(Centre("MermaidDestinationEndpoint"), Centre("MermaidCanvas_node_Node3"), capture: "reconnecting-destination");
            Ready(text => new MermaidSourceParser().Parse(text).Edges[0].Target.Id == "Node3");
            Check(new MermaidSourceParser().Parse(Draft()).Edges[0].Label == "Next 0", "Destination reconnection preserves the exact original label");
            Capture("reconnection-result"); var connected = Draft();
            Click("MermaidUndo"); Ready(text => text == beforeReconnect); Click("MermaidRedo"); Ready(text => text == connected);
            Click("MermaidUndo"); Ready(text => text == beforeReconnect);
            if (!Find(visual!, "MermaidSelectionSummary").Name.Contains("Next 0", StringComparison.Ordinal)) Find(visual!, "MermaidCanvas_edge-0").Patterns.Toggle.Pattern.Toggle();
            Drag(Centre("MermaidSourceEndpoint"), Centre("MermaidCanvas_node_Node2"));
            Ready(text => new MermaidSourceParser().Parse(text).Edges[0].Source.Id == "Node2");
            Check(true, "Source endpoint uses the exact selected edge and validates Mermaid");
            Click("MermaidUndo"); Ready(text => text == beforeReconnect);
            if (!Find(visual!, "MermaidSelectionSummary").Name.Contains("Next 0", StringComparison.Ordinal)) Find(visual!, "MermaidCanvas_edge-0").Patterns.Toggle.Pattern.Toggle();
            Drag(Centre("MermaidDestinationEndpoint"), new(view.Right - 8, view.Top + 8));
            Check(Draft() == beforeReconnect, "Invalid endpoint drop leaves exact source unchanged");
            // Repeated movement and Escape after a semantic edit must still release capture.
            var beforeCancel = Centre("MermaidCanvas_node_Node1");
            Drag(beforeCancel, new(beforeCancel.X + 40, beforeCancel.Y + 20), true);
            Check(Centre("MermaidCanvas_node_Node1") == beforeCancel && Draft() == beforeReconnect, "Escape restores node position and source after reconnection");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            {
                visual!.Patterns.Transform.Pattern.Resize(width, height); Capture("candidate-" + width + "x" + height);
                var apply = Find(visual, "MermaidApply").BoundingRectangle;
                Check(visual.BoundingRectangle.Contains(apply), "Apply remains visible at " + width + "x" + height);
            }
            Click("MermaidAutoLayout"); Capture("auto-layout-reset");
            Check(Draft() == beforeReconnect, "Auto layout resets only temporary positions");
            Check(ReadEditorText(editor) == original, "All private interactions preserve the original document before Apply");
            var applied = Draft(); Click("MermaidApply");
            Wait(() => ReadEditorText(editor) == applied, "Apply did not write the validated source to the owning editor.");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == original, "One document Undo did not restore the entire original diagram.");
            Check(true, "Apply and one document Undo preserve the original portable source exactly");
            observations.Add(new { CandidateChecks = checks, Placements = placements });
            // Reopen so the caller also verifies ordinary Cancel after the completed transaction.
            Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "Preview action missing after document Undo.");
            Find(main!, "DiagramEditVisually0").AsButton().Invoke();
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "Editor did not reopen after Undo.");
            Wait(() => Find(visual!, "MermaidApply").IsEnabled, "Reopened diagram did not finish rendering.");
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
