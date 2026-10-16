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
            foreach (var count in baseline ? new[] { 20, 100, 300, 24 } : new[] { 100, 300, 24, 20 })
            {
                var source = InteractionFixture(count);
                var fixture = Path.Combine(output, "drag-" + count + ".mmd"); File.WriteAllText(fixture, source);
                var launch = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, RedirectStandardError = true };
                launch.Environment["LENS_NATIVE_MERMAID_DIAGNOSTICS"] = "1";
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
                // Keep renderer protection fail-closed; it must not become an editing permission.
                if (!editable)
                {
                    observations.Add(new { Nodes = count, Editable = false, Status = Find(visual, "MermaidStatus").Name });
                    if (!baseline) throw new IOException("The supported synthetic stress fixture did not become editable: " + count);
                }
                else
                {
                    Find(visual, "MermaidActualSize").AsButton().Invoke();
                    var samples = new List<double>(); var updateMetrics = new List<string>(); var draggedIds = new HashSet<string>();
                    for (var iteration = 0; iteration < 30; iteration++)
                    {
                        if (app.HasExited) throw new IOException("Application exited during node dragging: " + observedProcess!.ExitCode);
                        var draggedId = iteration % 2 == 0 ? "N0" : "N1";
                        var node = Find(visual, "MermaidCanvas_node_" + draggedId); var bounds = node.BoundingRectangle;
                        var viewportBounds = Find(visual, "MermaidViewport").BoundingRectangle;
                        if (!viewportBounds.Contains(new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)))
                        { draggedId = "N0"; node = Find(visual, "MermaidCanvas_node_N0"); bounds = node.BoundingRectangle; }
                        draggedIds.Add(draggedId);
                        var start = new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                        var watch = Stopwatch.StartNew(); Mouse.MoveTo(start); Mouse.Down(MouseButton.Left);
                        try
                        {
                            for (var move = 0; move < 16; move++)
                                MovePointer(new System.Drawing.Point(start.X + (move % 2 == 0 ? 45 : -20), start.Y + (move % 4) * 2));
                            MovePointer(new System.Drawing.Point(start.X + 12, start.Y));
                            if (iteration == 1) visual!.CaptureToFile(Path.Combine(output, "drag-active-" + count + ".png"));
                            if (iteration % 5 == 0) Keyboard.Type(VirtualKeyShort.ESCAPE);
                        }
                        finally { Mouse.Up(MouseButton.Left); }
                        var draft = ReadEditorText(Find(visual, "MermaidDraftSource"));
                        if (draft != source) throw new IOException("Session dragging mutated the private source.");
                        if (!observedProcess!.Responding) throw new IOException("The native process stopped responding after pointer release.");
                        samples.Add(watch.Elapsed.TotalMilliseconds);
                        updateMetrics.Add(Find(visual, "MermaidStatus").Properties.HelpText.ValueOrDefault ?? "");
                        Console.WriteLine($"Nodes {count}: drag {iteration + 1}/30, {watch.Elapsed.TotalMilliseconds:F1} ms round trip");
                        if (iteration % 3 == 2) Find(visual, "MermaidAutoLayout").AsButton().Invoke();
                    }
                    visual.CaptureToFile(Path.Combine(output, "after-" + count + ".png"));
                    observations.Add(new { Nodes = count, Editable = true, Drags = samples.Count, PointerMoves = samples.Count * 17,
                        DraggedNodeIds = draggedIds, RoundTripMilliseconds = samples, UpdateMetrics = updateMetrics, PrivateSourceExact = true, DiskSourceExact = File.ReadAllText(fixture) == source,
                        WorkingSet = observedProcess!.WorkingSet64, ProcessAlive = !app.HasExited });
                }
                Record(false, "Validation in progress; completion is recorded only after all checks pass.");
                if (!baseline && count == 20 && editable) CandidateScenarios(source);
                Find(visual, "MermaidCancel").AsButton().Invoke();
                Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor"))) is null, "Cancel did not close the editor after stress.");
                app.Kill(); app.Dispose(); app = null; visual = null;
            }
            if (!baseline) CheckWorkspaceViewportOwnership(executable, output);
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
            observations.Add(new { CandidateChecks = checks, Placements = placements });
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
                    for (var step = 1; step <= 8; step++) MovePointer(new(from.X + (to.X - from.X) * step / 8, from.Y + (to.Y - from.Y) * step / 8));
                    if (capture is not null) Capture(capture);
                    if (cancel) Keyboard.Type(VirtualKeyShort.ESCAPE);
                }
                finally { Mouse.Up(MouseButton.Left); }
            }
            void ChangeTheme(string name)
            {
                FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand();
                Find(main!, name + "Theme").AsMenuItem().Invoke(); FocusWindow(visual!);
                Ready(text => text.Contains("Session centre retained", StringComparison.Ordinal));
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
                var nodeBefore = Centre("MermaidCanvas_node_N0"); Drag(nodeBefore, new(nodeBefore.X + 24, nodeBefore.Y));
                Check(Math.Abs(Centre("MermaidCanvas_node_N0").X - nodeBefore.X - 24) <= 3 && Draft() == original,
                    "Existing-node dragging follows the actual pointer without changing source at zoom " + zoom);
                Click("MermaidAutoLayout");
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
            var selectedRows = Find(visual!, "MermaidElements").FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
            selectedRows.Single(row => row.Name.StartsWith("Node1 ·", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            selectedRows.Single(row => row.Name.StartsWith("Node2 ·", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.AddToSelection();
            var nodeOne = Centre("MermaidCanvas_node_Node1"); var nodeTwo = Centre("MermaidCanvas_node_Node2");
            Drag(nodeOne, new(nodeOne.X + 36, nodeOne.Y + 12));
            foreach (var id in new[] { "Node1", "Node2" }) expectedCentres[id] = new(expectedCentres[id].X + 36, expectedCentres[id].Y + 12);
            Check(Math.Abs(Centre("MermaidCanvas_node_Node1").X - nodeOne.X - 36) <= 2 && Math.Abs(Centre("MermaidCanvas_node_Node2").X - nodeTwo.X - 36) <= 2, "Actual multi-node dragging moves both selected nodes together");
            Capture("multi-node-drag");
            selectedRows.Single(row => row.Name.StartsWith("Node1 ·", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            var beforeRename = Draft(); var renameCentre = Centre("MermaidCanvas_node_Node1");
            Find(visual!, "MermaidNodeId").AsTextBox().Text = "Positioned"; Click("MermaidUpdateNode");
            Ready(text => new MermaidSourceParser().Parse(text).Nodes.Any(node => node.Id == "Positioned"));
            Check(Centre("MermaidCanvas_node_Positioned") == renameCentre, "Safe node renaming transfers its session centre");
            var renamed = Draft(); Click("MermaidUndo"); Ready(text => text == beforeRename);
            Check(Centre("MermaidCanvas_node_Node1") == renameCentre, "Private Undo restores the previous node identity and session centre");
            Click("MermaidRedo"); Ready(text => text == renamed); Click("MermaidUndo"); Ready(text => text == beforeRename);
            Check(Centre("MermaidCanvas_node_Node1") == renameCentre, "Private Redo and Undo retain the manual centre");
            if (!highContrast)
            {
                Find(visual!, "MermaidElements").Focus(); Keyboard.Type(VirtualKeyShort.F2);
                Find(visual!, "MermaidInlineLabel").AsTextBox().Text = "Retain this pending label";
                Check(Draft() == beforeRename, "Typing an inline label retains the exact private source until confirmation");
                FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "DarkTheme").AsMenuItem().Invoke(); FocusWindow(visual!);
                Check(Find(visual!, "MermaidInlineLabel").AsTextBox().Text == "Retain this pending label" && Draft() == beforeRename, "Theme changes retain uncommitted inline label text");
                Find(visual!, "MermaidInlineLabel").Focus(); Keyboard.Type(VirtualKeyShort.ESCAPE);
                Ready(text => text == beforeRename); Capture("dark-session-layout");
                foreach (var (node, expected) in expectedCentres) Check(Math.Abs(Centre("MermaidCanvas_node_" + node).X - expected.X) <= 6 && Math.Abs(Centre("MermaidCanvas_node_" + node).Y - expected.Y) <= 6, "Theme rerender retains session centre: " + node);
                ChangeTheme("Light");
            }
            var beforeDeactivation = Centre("MermaidCanvas_node_Node1");
            Mouse.MoveTo(beforeDeactivation); Mouse.Down(MouseButton.Left);
            try { MovePointer(new(beforeDeactivation.X + 45, beforeDeactivation.Y + 25)); FocusWindow(main!); }
            finally { Mouse.Up(MouseButton.Left); }
            FocusWindow(visual!);
            Check(Centre("MermaidCanvas_node_Node1") == beforeDeactivation, "Window deactivation cancels movement and releases capture");
            var beforeInterruptedUndo = Draft(); var positionBeforeUndo = Centre("MermaidCanvas_node_Node1");
            Mouse.MoveTo(positionBeforeUndo); Mouse.Down(MouseButton.Left);
            try { MovePointer(new(positionBeforeUndo.X + 28, positionBeforeUndo.Y + 16)); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); }
            finally { Mouse.Up(MouseButton.Left); }
            Ready(text => text != beforeInterruptedUndo);
            Find(visual!, "MermaidElements").Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Ready(text => text == beforeInterruptedUndo);
            Check(Centre("MermaidCanvas_node_Node1") == positionBeforeUndo, "Undo during an active drag cancels capture before changing history; Redo restores the saved position");
            var beforeRepeatedSource = Draft(); var repeatedCentre = Centre("MermaidCanvas_node_Node1");
            Find(visual!, "MermaidCanvas_node_Node1").Click();
            Find(visual!, "MermaidNodeLabel").AsTextBox().Text = "Transient label"; Click("MermaidUpdateNode");
            Ready(text => text.Contains("Transient label", StringComparison.Ordinal));
            Find(visual!, "MermaidDraftSource").Patterns.Value.Pattern.SetValue(beforeRepeatedSource); Click("MermaidValidateSource");
            Ready(text => text == beforeRepeatedSource);
            Click("MermaidUndo"); Click("MermaidUndo"); Ready(text => text == beforeRepeatedSource);
            Check(Centre("MermaidCanvas_node_Node1") == repeatedCentre, "Rapid Undo through identical source restores its own session layout and private element identities");
            var beforeReconnect = Draft();
            if (!Find(visual!, "MermaidSelectionSummary").Name.Contains("Next 0", StringComparison.Ordinal)) Find(visual!, "MermaidCanvas_edge-0").Patterns.Toggle.Pattern.Toggle();
            Wait(() => visual!.FindFirstDescendant(cf => cf.ByAutomationId("MermaidDestinationEndpoint")) is not null, "Selected connection has no endpoint handles.");
            Capture("connection-endpoints");
            Drag(Centre("MermaidDestinationEndpoint"), Centre("MermaidCanvas_node_Node3"), capture: "reconnecting-destination");
            Ready(text => new MermaidSourceParser().Parse(text).Edges.Single(edge => edge.Label == "Next 0").Target.Id == "Node3");
            Check(new MermaidSourceParser().Parse(Draft()).Edges.Single(edge => edge.Label == "Next 0").Type == FlowConnectionType.Directed, "Destination reconnection preserves the exact original label");
            Capture("reconnection-result"); var connected = Draft();
            Click("MermaidUndo"); Ready(text => text == beforeReconnect); Click("MermaidRedo"); Ready(text => text == connected);
            Click("MermaidUndo"); Ready(text => text == beforeReconnect);
            if (!Find(visual!, "MermaidSelectionSummary").Name.Contains("Next 0", StringComparison.Ordinal)) Find(visual!, "MermaidCanvas_edge-0").Patterns.Toggle.Pattern.Toggle();
            Drag(Centre("MermaidSourceEndpoint"), Centre("MermaidCanvas_node_Node2"));
            Ready(text => new MermaidSourceParser().Parse(text).Edges.Single(edge => edge.Label == "Next 0").Source.Id == "Node2");
            Check(true, "Source endpoint uses the exact selected edge and validates Mermaid");
            Click("MermaidUndo"); Ready(text => text == beforeReconnect);
            if (!Find(visual!, "MermaidSelectionSummary").Name.Contains("Next 0", StringComparison.Ordinal)) Find(visual!, "MermaidCanvas_edge-0").Patterns.Toggle.Pattern.Toggle();
            Drag(Centre("MermaidDestinationEndpoint"), new(view.Right - 8, view.Top + 8));
            Check(Draft() == beforeReconnect, "Invalid endpoint drop leaves exact source unchanged");
            // Repeated movement and Escape after a semantic edit must still release capture.
            var beforeCancel = Centre("MermaidCanvas_node_Node1");
            Drag(beforeCancel, new(beforeCancel.X + 40, beforeCancel.Y + 20), true);
            Check(Centre("MermaidCanvas_node_Node1") == beforeCancel && Draft() == beforeReconnect, "Escape restores node position and source after reconnection");
            var beforeGroup = Draft(); var groupPoint = new System.Drawing.Point(view.Left + view.Width / 4, view.Top + view.Height * 4 / 5);
            Drag(Centre("MermaidInsertSubgraph"), groupPoint); Ready(text => new MermaidSourceParser().Parse(text).Subgraphs.Count == 1);
            var groupCentre = Centre("MermaidCanvas_group_Group1");
            Check(Math.Abs(groupCentre.X - groupPoint.X) <= 6 && Math.Abs(groupCentre.Y - groupPoint.Y) <= 6, "Palette subgraph appears at its actual drop centre");
            Drag(Centre("MermaidInsertProcess"), groupCentre);
            Ready(text => new MermaidSourceParser().Parse(text).Nodes.Any(node => node.Id == "Node4" && node.SubgraphId == "Group1"));
            Check(true, "Dropping into the explicitly selected subgraph uses verified membership");
            Find(visual!, "MermaidCanvas_node_N0").Click(); Find(visual!, "MermaidNodeGroup").Focus();
            Find(visual!, "MermaidNodeGroup").AsComboBox().Select("Group1"); Click("MermaidMoveNode");
            Ready(text => new MermaidSourceParser().Parse(text).Node("N0").SubgraphId == "Group1");
            Check(Find(visual!, "MermaidCanvas_group_Group1").BoundingRectangle.Contains(Find(visual!, "MermaidCanvas_node_N0").BoundingRectangle), "Unpositioned members inherit the manually placed subgraph's translation");
            Capture("positioned-subgraph-membership");
            Click("MermaidUndo"); Ready(text => new MermaidSourceParser().Parse(text).Node("N0").SubgraphId is null);
            Click("MermaidUndo"); Ready(text => !new MermaidSourceParser().Parse(text).Nodes.Any(node => node.Id == "Node4"));
            Click("MermaidUndo"); Ready(text => text == beforeGroup);
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
            var obsoletePreviewAction = Find(main!, "DiagramEditVisually0").FrameworkAutomationElement.RuntimeId.Value;
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == original, "One document Undo did not restore the entire original diagram.");
            Check(true, "Apply and one document Undo preserve the original portable source exactly");
            // Reopen so the caller also verifies ordinary Cancel after the completed transaction.
            Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is { } currentAction
                && !obsoletePreviewAction.SequenceEqual(currentAction.FrameworkAutomationElement.RuntimeId.Value), "The current preview action was not replaced after document Undo.");
            Find(main!, "DiagramEditVisually0").AsButton().Invoke();
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "Editor did not reopen after Undo.");
            Wait(() => Find(visual!, "MermaidApply").IsEnabled, "Reopened diagram did not finish rendering.");
        }
        void Record(bool passed, string? error) => File.WriteAllText(Path.Combine(output, "interactions.json"), JsonSerializer.Serialize(new
        { Passed = passed, Baseline = baseline, HighContrast = highContrast, Dpi = GetDpiForSystem(), Observations = observations, Error = error,
            Limitation = "Original private fixture unavailable; synthetic repeated pointer test. Round trips include input pacing and UIA transport, not per-frame latency." }, new JsonSerializerOptions { WriteIndented = true }));
    }).GetAwaiter().GetResult();

    private static void MovePointer(System.Drawing.Point point) { Mouse.Position = point; Thread.Sleep(16); }

    private static string InteractionFixture(int count) => count == 24 ? NestedInteractionFixture() : "flowchart TD\n    %% Synthetic stability fixture\n"
        + string.Join('\n', Enumerable.Range(0, count).Select(index => index % 5 == 1 ? $"N{index}{{Decision {index}}}" : $"N{index}[\"Node {index}<br/>Second line\"]"))
        + "\n" + string.Join('\n', Enumerable.Range(0, Math.Min(count - 1, 175)).Select(index => $"N{index} -->|Next {index}| N{index + 1}"))
        + "\nN0 --> N4\nN0 --> N7\nN4 --> N9\nN9 --> N2\nN7 --> N12\nN12 --> N4\n";
    private static string NestedInteractionFixture() => "flowchart TD\nsubgraph Outer[Outer workflow]\nsubgraph Inner[Inner review]\n"
        + "N0[\"Long review label with detailed synthetic context<br/>Second line with further context\"]\nN1{Decision}\nend\nN2[Shared]\nN3[Review]\nend\n"
        + string.Join('\n', Enumerable.Range(4, 20).Select(index => $"N{index}[Node {index}]"))
        + "\n" + string.Join('\n', Enumerable.Range(0, 23).Select(index => $"N{index} -->|Next| N{index + 1}"))
        + "\nN0 --> N4\nN0 --> N6\nN0 --> N8\nN0 --> N10\nN0 --> N12\nN10 --> N2\nN12 --> N3\n";
}
