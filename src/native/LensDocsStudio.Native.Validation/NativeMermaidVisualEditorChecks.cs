using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
    public static int RunMermaidVisualEditor(string executable, string output, bool highContrast, bool polish = false) => Task.Run(() =>
    {
        Directory.CreateDirectory(Path.GetFullPath(output));
        using var clipboard = NativeUiClipboard.Start();
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var display = new NativeTestDisplay(output);
        var result = MermaidVisualEditorCore(executable, output, highContrast, polish);
        if (result == 0) display.Complete(); return result;
    }).GetAwaiter().GetResult();

    private static int MermaidVisualEditorCore(string executable, string output, bool highContrast, bool polish)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        var root = Path.Combine(output, "fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var state = Path.Combine(root, "state"); var path = Path.Combine(root, "Visual authoring.md");
        const string block = "```mermaid\r\nflowchart LR\r\n    %% Preserve me\r\n    A[Write] -->|Next| B(Review)\r\n```\r\n";
        const string original = "# Native visual authoring\r\n\r\n" + block + "\r\nMiddle paragraph.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); var diskBefore = File.ReadAllBytes(path);
        var firstFrames = 0;
        var checks = new List<string>(); var timings = new List<object>();
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        Application? app = null; Window? main = null; Window? visual = null;
        var clipboardBefore = NativeUiClipboard.GetDataObject(); uint clipboardOwned = 0;
        try
        {
            checks.AddRange(NativeMermaidDocumentChecks.Run());
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state); info.ArgumentList.Add(root); info.ArgumentList.Add(path);
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native application window did not appear.");
            var editor = Find(main, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "The visual authoring fixture did not load exactly.");
            main.Patterns.Transform.Pattern.Resize(1280, 840);
            var offset = original.LastIndexOf("```mermaid", StringComparison.Ordinal); Open(offset);
            Click("MermaidActualSize");
            Wait(() => Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.Value.Value == 100, "Actual size did not settle before comparison capture.");
            _ = DwmFlush(); Capture("existing-light");
            if (polish)
            {
                var source = Draft();
                var fromBounds = Find(visual!, "MermaidCanvas_node_A").BoundingRectangle;
                var toBounds = Find(visual!, "MermaidCanvas_node_B").BoundingRectangle;
                Mouse.Click(new System.Drawing.Point(toBounds.Left - 13, fromBounds.Top + fromBounds.Height / 2 + 4));
                Wait(() => Find(visual!, "MermaidSelectionSummary").Name.Contains("Next", StringComparison.Ordinal), "The broad connection pointer target did not select its actual inspector.");
                Check(Draft() == source, "A pointer offset from the thin connection line selects its inspector without changing source");
                var rows = Find(visual!, "MermaidElements").FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
                rows[0].Patterns.SelectionItem.Pattern.Select(); rows[1].Patterns.SelectionItem.Pattern.AddToSelection();
                Check(Find(visual!, "MermaidElements").Patterns.Selection.Pattern.Selection.Value.Length == 2, "Real list multi-selection exposes both selected nodes");
                Capture("multi-selection"); Click("MermaidDuplicate");
                AwaitSource(text => new MermaidSourceParser().Parse(text).Nodes.Count == 4 && new MermaidSourceParser().Parse(text).Edges.Count == 2);
                var duplicate = Draft(); Click("MermaidUndo"); AwaitSource(text => text == source);
                Click("MermaidRedo"); AwaitSource(text => text == duplicate); Click("MermaidUndo"); AwaitSource(text => text == source);
                Check(Draft() == source, "Multi-node duplication includes the internal connection and private Undo restores exact source");
                Click("MermaidCopySource");
                Wait(() => NativeUiClipboard.GetText() == source, "Source-header copy did not place the exact draft on the actual clipboard.");
                Check(true, "Source-header copy emits the exact draft"); clipboardOwned = GetClipboardSequenceNumber();
                var toggle = Find(visual!, "MermaidSourceToggle");
                var beforeHeight = Find(visual!, "MermaidViewport").BoundingRectangle.Height;
                toggle.Patterns.Toggle.Pattern.Toggle();
                Wait(() => Find(visual!, "MermaidViewport").BoundingRectangle.Height > beforeHeight + 80, "Collapsing source did not reclaim canvas space.");
                Capture("collapsed-source");
                toggle.Patterns.Toggle.Pattern.Toggle();
                Wait(() => Find(visual!, "MermaidViewport").BoundingRectangle.Height < beforeHeight + 5, "Expanding source did not restore the workspace split.");
                Check(Draft() == source, "Source collapse and expansion preserve the exact draft");
                var zoomValue = Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.Value.Value;
                Click("MermaidZoomIn"); Check(Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.Value.Value > zoomValue, "Zoom in changes the native viewport scale");
                Click("MermaidZoomOut"); Check(Math.Abs(Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.Value.Value - zoomValue) < .01, "Zoom out reverses the zoom-in scale without source changes");
                Check(Draft() == source, "View commands leave Mermaid source byte-for-byte unchanged");
                Check(Find(visual!, "MermaidElements").Patterns.Selection.Pattern.Selection.Value.Length == 2, "Source collapse and view commands retain the original multi-selection");
                foreach (var selected in Find(visual!, "MermaidElements").Patterns.Selection.Pattern.Selection.Value)
                    selected.Patterns.SelectionItem.Pattern.RemoveFromSelection();
                Wait(() => Find(visual!, "MermaidElements").Patterns.Selection.Pattern.Selection.Value.Length == 0, "The multi-selection fixture did not clear before the single-node scenario.");
            }
            Select("node_B");
            Wait(() => Find(visual!, "MermaidSelectionSummary").Name.StartsWith("B", StringComparison.Ordinal), "The selected-node inspector did not finish its first meaningful layout.");
            Capture("node-properties");
            if (polish)
            {
                var shapeSource = Draft(); Find(visual!, "MermaidNodeShape").AsComboBox().Select("Decision"); Click("MermaidUpdateNode");
                AwaitSource(text => new MermaidSourceParser().Parse(text).Node("B").Shape == FlowNodeShape.Decision);
                Click("MermaidUndo"); AwaitSource(text => text == shapeSource);
                Check(new MermaidSourceParser().Parse(Draft()).Node("B").Shape == FlowNodeShape.Rounded, "Shape inspector changes actual Mermaid geometry and Undo restores exact source");
            }
            Find(visual!, "MermaidNodeLabel").AsTextBox().Text = "Review changed";
            if (polish)
            {
                Capture("pending-properties");
                Select("node_A");
                Check(Find(visual!, "MermaidNodeLabel").AsTextBox().Text == "Review changed" && Find(visual!, "MermaidSelectionSummary").Name.Contains("B"), "Selection changes retain unapplied property text and selection");
                if (!highContrast)
                {
                    var pendingSource = Draft(); var zoomBefore = Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.Value.Value;
                    ChangeApplicationTheme("Dark"); FocusWindow(visual!); Capture("pending-properties-dark");
                    Check(Find(visual!, "MermaidNodeLabel").AsTextBox().Text == "Review changed" && Draft() == pendingSource && Find(visual!, "MermaidZoom").Patterns.RangeValue.Pattern.Value.Value == zoomBefore,
                        "Live theme updates retain pending inspector text, draft, selection and zoom");
                    ChangeApplicationTheme("Light"); FocusWindow(visual!);
                }
            }
            Check(!Find(visual!, "MermaidApply").IsEnabled, "Unapplied inspector text cannot be silently lost through Apply");
            Check(!Find(visual!, "MermaidValidateSource").IsEnabled, "Source validation cannot discard pending inspector text");
            Click("MermaidUpdateNode"); AwaitSource(text => text.Contains("B(\"Review changed\")", StringComparison.Ordinal));
            Check(ReadEditorText(editor) == original, "Draft node editing leaves the owning Markdown untouched");
            Find(visual!, "MermaidNodeId").AsTextBox().Text = "Reviewed"; Click("MermaidUpdateNode");
            AwaitSource(text => text.Contains("Reviewed(\"Review changed\")", StringComparison.Ordinal));
            Click("MermaidInsertDecision"); AwaitSource(text => text.Contains("Node1{", StringComparison.Ordinal));
            Click("MermaidFit"); Find(visual!, "MermaidPort_Reviewed").AsButton().Invoke(); if (polish) Capture("creating-connection"); Find(visual!, "MermaidCanvas_node_Node1").Click();
            AwaitSource(text => text.Contains("Reviewed --> Node1", StringComparison.Ordinal));
            Find(visual!, "MermaidEdgeLabel").AsTextBox().Text = "Confirmed"; Click("MermaidUpdateEdge");
            AwaitSource(text => text.Contains("-->|Confirmed|", StringComparison.Ordinal)); Capture("edge-properties");
            if (polish)
            {
                Click("MermaidReverseEdge");
                AwaitSource(text => new MermaidSourceParser().Parse(text).Edges.Any(edge => edge.Source.Id == "Node1" && edge.Target.Id == "Reviewed" && edge.Label == "Confirmed"));
                Click("MermaidReverseEdge");
                AwaitSource(text => new MermaidSourceParser().Parse(text).Edges.Any(edge => edge.Source.Id == "Reviewed" && edge.Target.Id == "Node1" && edge.Label == "Confirmed"));
                Check(true, "Connection inspector reverses actual endpoints and retains the label");
            }
            var connected = Draft(); Click("MermaidDelete"); AwaitSource(text => !text.Contains("Confirmed", StringComparison.Ordinal));
            Click("MermaidUndo"); AwaitSource(text => text == connected); Click("MermaidRedo"); AwaitSource(text => !text.Contains("Confirmed", StringComparison.Ordinal));
            Click("MermaidUndo"); AwaitSource(text => text == connected);
            Check(new MermaidSourceParser().Parse(Draft()).Edges.Count == 2, "Real canvas connection, label, deletion, Undo and Redo produce the expected semantic graph");
            Click("MermaidInsertSubgraph"); AwaitSource(text => text.Contains("subgraph Group1", StringComparison.Ordinal)); Capture("subgraph-properties");
            Find(visual!, "MermaidGroupLabel").AsTextBox().Text = "Capture group"; Click("MermaidUpdateGroup");
            AwaitSource(text => text.Contains("subgraph Group1[Capture group]", StringComparison.Ordinal));
            Select("node_Node1"); Find(visual!, "MermaidNodeGroup").AsComboBox().Select("Group1"); Click("MermaidMoveNode");
            AwaitSource(text => new MermaidSourceParser().Parse(text).Node("Node1").SubgraphId == "Group1");
            if (polish)
            {
                var safe = Draft(); Find(visual!, "MermaidFillColour").AsTextBox().Text = "#GGGGGG"; Click("MermaidApplyStyle");
                Wait(() => Find(visual!, "MermaidResetProperties").IsEnabled, "Rejected colour validation did not return to an editable inspector.");
                Check(!Find(visual!, "MermaidApply").IsEnabled, "Invalid pending colours cannot enable document Apply");
                Check(Draft() == safe, "Invalid hexadecimal colour keeps exact source and pending inspector protection");
                Click("MermaidResetProperties"); Check(Draft() == safe && Find(visual!, "MermaidApply").IsEnabled, "Explicit property reset restores the validated draft");
                var fill = Find(visual!, "MermaidFillColour").AsTextBox(); fill.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type("#11223344");
                Wait(() => fill.Text == "#11223344", "Keyboard colour input was truncated instead of retaining the complete invalid value.");
                Check(true, "Keyboard colour input retains an invalid long value instead of truncating it to an accepted prefix");
                Click("MermaidApplyStyle"); Wait(() => Find(visual!, "MermaidResetProperties").IsEnabled, "Long colour validation did not return to the inspector.");
                Check(Draft() == safe && !Find(visual!, "MermaidApply").IsEnabled, "Invalid long colour is rejected without source mutation");
                Click("MermaidResetProperties");
            }
            Find(visual!, "MermaidFillColour").AsTextBox().Text = "#E8F3FA"; Click("MermaidApplyStyle");
            AwaitSource(text => text.Contains("style Node1 fill:#E8F3FA", StringComparison.Ordinal));
            var valid = Draft(); var sourceEditor = Find(visual!, "MermaidDraftSource");
            sourceEditor.Patterns.Value.Pattern.SetValue("flowchart TD\nA -->"); Click("MermaidValidateSource");
            Wait(() => Find(visual!, "MermaidStatus").Name.Contains("retained", StringComparison.Ordinal), "Invalid source did not retain its diagnostic and draft.");
            Check(Draft() == "flowchart TD\nA -->" && !Find(visual!, "MermaidApply").IsEnabled, "Invalid direct source is retained and cannot overwrite the document"); Capture("invalid-source");
            sourceEditor.Patterns.Value.Pattern.SetValue(valid); Click("MermaidValidateSource"); AwaitSource(text => text == valid && Find(visual!, "MermaidApply").IsEnabled);
            var zoom = Find(visual!, "MermaidZoom"); zoom.Patterns.RangeValue.Pattern.SetValue(400);
            var viewport = Find(visual!, "MermaidViewport"); var scroll = viewport.Patterns.Scroll.Pattern;
            Wait(() => scroll.HorizontallyScrollable.Value, "The native canvas did not expose its actual enlarged scroll extent.");
            var bounds = viewport.BoundingRectangle; var before = scroll.HorizontalScrollPercent.Value;
            Mouse.DragHorizontally(new(bounds.Left + bounds.Width * 2 / 3, bounds.Top + bounds.Height / 2), -100, MouseButton.Middle);
            Wait(() => scroll.HorizontalScrollPercent.Value > before, "Middle-button PAN did not move the actual canvas.");
            Click("MermaidFit");
            Wait(() => zoom.Patterns.RangeValue.Pattern.Value.Value < 400 && !scroll.HorizontallyScrollable.Value && !scroll.VerticallyScrollable.Value,
                "Fit did not contain the actual canvas after PAN."); Capture("source-and-canvas");
            if (!highContrast)
            {
                var themeDraft = Draft(); ChangeApplicationTheme("Dark");
                FocusWindow(visual!); Capture("live-dark-theme");
                Check(Draft() == themeDraft && ReadEditorText(editor) == original, "Live theme changes retain private and owning source");
                ChangeApplicationTheme("Light"); FocusWindow(visual!);
            }
            var second = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1];
            var expected = original[..second.Body.Start] + valid + original[second.Body.End..];
            Click("MermaidApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "Apply did not replace only the second identical block.");
            Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(diskBefore), "Apply retains ordinary unsaved-file behaviour");
            FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == original, "One AvalonEdit Undo did not restore the complete original Markdown.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => ReadEditorText(editor) == expected, "One AvalonEdit Redo did not restore the applied source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(path) == expected, "Ordinary Save did not persist the applied Mermaid.");
            Check(new DiagramService().Render(valid).Success, "Applied portable source renders through the established diagram/export service");
            Open(offset); Click("MermaidInsertProcess"); AwaitSource(text => text != valid); Click("MermaidCancel"); WaitClosed();
            Check(ReadEditorText(editor) == expected && ReadObservedText(path) == expected, "Cancel discards the draft without touching source or disk");
            Open(offset); Select("node_Reviewed");
            Find(visual!, "MermaidNodeLabel").AsTextBox().Text = "Relocated"; Click("MermaidUpdateNode"); AwaitSource(text => text.Contains("Relocated", StringComparison.Ordinal)); var relocatedDraft = Draft();
            FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type("Outside change"); Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => ReadEditorText(editor) == "Outside change\r\n\r\n" + expected, "The independent surrounding edit did not arrive.");
            FocusWindow(visual!); Click("MermaidApply"); WaitClosed();
            var shifted = "Outside change\r\n\r\n" + expected[..second.Body.Start] + relocatedDraft + expected[(second.Body.Start + valid.Length)..];
            Wait(() => ReadEditorText(editor) == shifted, "A safe edit-history relocation did not preserve the correct identical occurrence.");
            Check(ReadObservedText(path) == expected, "Surrounding edits and visual Apply remain in memory until Save");
            var shiftedOffset = shifted.LastIndexOf("```mermaid", StringComparison.Ordinal); Open(shiftedOffset); Click("MermaidInsertProcess"); var conflictedDraft = Draft();
            FocusWindow(main); editor.Patterns.Value.Pattern.SetValue(shifted.Replace("Relocated", "Externally changed", StringComparison.Ordinal)); var outside = ReadEditorText(editor);
            FocusWindow(visual!); Click("MermaidApply");
            Wait(() => Find(visual!, "MermaidStatus").Name.Contains("changed while", StringComparison.Ordinal), "Changed diagram source was not rejected."); Capture("source-conflict");
            Check(Draft() == conflictedDraft && ReadEditorText(editor) == outside, "Conflicting Apply preserves both the external source and private draft");
            Click("MermaidCancel"); WaitClosed();
            main.CaptureToFile(Path.Combine(output, "mermaid-main-preview.png"));
            File.WriteAllText(Path.Combine(output, "mermaid-before.md"), original); File.WriteAllText(Path.Combine(output, "mermaid-after.md"), expected);
            if (polish)
            {
                Open(shiftedOffset); Select("node_Reviewed");
                foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
                {
                    visual!.Patterns.Transform.Pattern.Move(0, 0);
                    visual.Patterns.Transform.Pattern.Resize(width, height);
                    Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && Math.Abs(visual.BoundingRectangle.Height - height) < 3, "Requested Mermaid editor dimensions did not settle.");
                    foreach (var id in new[] { "MermaidApply", "MermaidCancel", "MermaidViewport", "MermaidElements", "MermaidSelectionSummary" })
                        Check(visual.BoundingRectangle.Contains(Find(visual, id).BoundingRectangle), id + " remains contained at " + width + "x" + height);
                    Capture("responsive-" + width + "x" + height);
                    if (width == 880)
                    {
                        var beforePreview = Draft();
                        (visual.FindFirstDescendant(cf => cf.ByName("More commands")) ?? throw new IOException("The narrow native toolbar did not expose its overflow control.")).Patterns.Toggle.Pattern.Toggle();
                        FlaUI.Core.AutomationElements.AutomationElement? previewCommand = null;
                        Wait(() => (previewCommand = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidRenderedPreview")))) is { IsOffscreen: false }, "Native overflow did not reveal Rendered preview.");
                        Check(automation.GetDesktop().BoundingRectangle.Contains(previewCommand!.BoundingRectangle), "Native overflow command is completely contained by the disposable desktop");
                        automation.GetDesktop().CaptureToFile(Path.Combine(output, "mermaid-toolbar-overflow.png")); previewCommand.AsButton().Invoke();
                        Window? diagram = null;
                        Wait(() => (diagram = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("DiagramWindow")))?.AsWindow()) is not null, "Overflow preview did not open the native rendered diagram.");
                        diagram!.CaptureToFile(Path.Combine(output, "mermaid-overflow-rendered-preview.png")); diagram.Close();
                        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("DiagramWindow"))) is null, "Native rendered preview did not close.");
                        Check(Draft() == beforePreview, "Narrow toolbar overflow opens the existing native rendered preview without changing source");
                        FocusWindow(visual);
                    }
                }
                visual!.Patterns.Transform.Pattern.Resize(1280, 840); Click("MermaidCancel"); WaitClosed();
            }
            if (!highContrast)
            {
                ChangeApplicationTheme("Dark");
                Open(shiftedOffset); Select("node_Reviewed"); Capture("dark");
                visual!.Patterns.Transform.Pattern.Resize(880, 600);
                Wait(() => visual.BoundingRectangle.Width < 890 && visual.BoundingRectangle.Contains(Find(visual, "MermaidApply").BoundingRectangle), "Narrow-window Apply was clipped.");
                Capture("narrow"); visual.Patterns.Transform.Pattern.Resize(1440, 900);
                Wait(() => visual.BoundingRectangle.Width > 1400 && automation.GetDesktop().BoundingRectangle.Contains(visual.BoundingRectangle), "The wide editor was clipped by the hosted desktop.");
                Click("MermaidFit"); Capture("wide"); Click("MermaidCancel"); WaitClosed();
            }
            LaunchDocument("Standalone.mmd", "flowchart TD\r\nA[Input] --> B[Output]\r\n");
            Open(0); Select("node_A");
            var rawBefore = ReadEditorText(editor); Find(visual!, "MermaidNodeLabel").AsTextBox().Text = "Standalone changed"; Click("MermaidUpdateNode");
            AwaitSource(text => text.Contains("Standalone changed", StringComparison.Ordinal));
            Find(visual!, "MermaidElements").Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
            Wait(() => NativeUiClipboard.GetText().Contains("Standalone changed", StringComparison.Ordinal), "Selected-node copy did not emit actual portable Mermaid source."); clipboardOwned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
            AwaitSource(text => new MermaidSourceParser().Parse(text).Nodes.Count == 3);
            Check(ReadEditorText(editor) == rawBefore, "Native clipboard paste modifies only the private Mermaid draft");
            var rawAfter = Draft(); Capture("standalone");
            Click("MermaidApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == rawAfter, "Standalone visual Apply did not update raw source.");
            Check(!rawAfter.Contains("```", StringComparison.Ordinal), "Real standalone UIA Apply never inserts a synthetic fence");
            FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == rawBefore, "Standalone UIA Undo did not restore its complete source.");

            foreach (var extension in new[] { ".md", ".mermaid" })
            {
                LaunchDocument("Empty" + extension, ""); OpenInsertion(); Capture("empty-new" + extension.Replace('.', '-'));
                Click("MermaidInsertProcess"); AwaitSource(text => text.Contains("Node1[", StringComparison.Ordinal));
                Find(visual!, "MermaidElements").Focus(); Keyboard.Type(VirtualKeyShort.F2);
                var inline = Find(visual!, "MermaidInlineLabel").AsTextBox(); inline.Text = "Cancelled label"; Keyboard.Type(VirtualKeyShort.ESCAPE);
                Check(!Draft().Contains("Cancelled label", StringComparison.Ordinal), "Escape cancels a real inline label edit in " + extension);
                Find(visual!, "MermaidElements").Focus(); Keyboard.Type(VirtualKeyShort.F2); Find(visual!, "MermaidInlineLabel").AsTextBox().Text = "Keyboard label";
                Keyboard.Type(VirtualKeyShort.RETURN); AwaitSource(text => text.Contains("Node1[\"Keyboard label\"]", StringComparison.Ordinal)); Capture("inline-label-result" + extension.Replace('.', '-'));
                var paletteDecision = Find(visual!, "MermaidInsertDecision"); var dragOrigin = paletteDecision.BoundingRectangle;
                Mouse.DragHorizontally(new(dragOrigin.Left + dragOrigin.Width / 2, dragOrigin.Top + dragOrigin.Height / 2), 500, MouseButton.Left);
                AwaitSource(text => new MermaidSourceParser().Parse(text).Nodes.Count == 2);
                Check(new MermaidSourceParser().Parse(Draft()).Node("Node2").Shape == FlowNodeShape.Decision, "Native palette drag-and-drop adds the intended semantic shape in " + extension);
                Capture("palette-drop" + extension.Replace('.', '-'));
                var beforeDrag = Draft(); var draggedNode = Find(visual!, "MermaidCanvas_node_Node2").BoundingRectangle;
                Mouse.DragHorizontally(new(draggedNode.Left + draggedNode.Width / 2, draggedNode.Top + draggedNode.Height / 2), 55, MouseButton.Left);
                Wait(() => Find(visual!, "MermaidCanvas_node_Node2").BoundingRectangle.Left > draggedNode.Left + 30, "Native element dragging did not move its real canvas control.");
                Check(Draft() == beforeDrag, "Node dragging keeps portable source byte-for-byte unchanged in " + extension);
                Click("MermaidDuplicate"); AwaitSource(text => new MermaidSourceParser().Parse(text).Nodes.Count == 3);
                Click("MermaidUndo"); AwaitSource(text => new MermaidSourceParser().Parse(text).Nodes.Count == 2);
                Click("MermaidRedo"); AwaitSource(text => new MermaidSourceParser().Parse(text).Nodes.Count == 3);
                var inserted = Draft(); Click("MermaidApply"); WaitClosed();
                Wait(() => ReadEditorText(editor).Contains("Keyboard label", StringComparison.Ordinal), "New flowchart insertion did not update the document.");
                Check(extension == ".md" ? ReadEditorText(editor).StartsWith("```mermaid", StringComparison.Ordinal) : ReadEditorText(editor) == inserted,
                    "New flowchart uses the correct portable Markdown/raw representation for " + extension);
                FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == "", "New flowchart insertion was not one coherent document Undo.");
            }
            const string protectedSource = "flowchart TD\nA[Keep] --> B[Protect] --> C[Source]\n";
            LaunchDocument("Protected.mmd", protectedSource); OpenInsertion(false); Capture("protected-source");
            Check(!Find(visual!, "MermaidApply").IsEnabled && !Find(visual!, "MermaidInsertProcess").IsEnabled && Draft() == protectedSource,
                "Unsupported chained source opens for protected inspection without destructive visual commands");
            Click("MermaidCancel"); WaitClosed(); Check(ReadEditorText(editor) == protectedSource, "Protected inspection leaves the owning source exact");
            if (polish)
            {
                foreach (var count in new[] { 20, 100, 300 })
                {
                    var graph = "flowchart TD\n" + string.Join('\n', Enumerable.Range(0, count).Select(index => $"N{index}[Node {index}]"))
                        + "\n" + string.Join('\n', Enumerable.Range(0, Math.Min(count - 1, 199)).Select(index => $"N{index} --> N{index + 1}"));
                    LaunchDocument("Canvas-" + count + ".mmd", graph);
                    var watch = Stopwatch.StartNew(); OpenInsertion(); Click("MermaidFit");
                    timings.Add(new { Operation = "Native canvas " + count + " nodes", Milliseconds = watch.Elapsed.TotalMilliseconds });
                    Check(Draft() == graph && Find(visual!, "MermaidElements").Patterns.Selection.IsSupported, "Native " + count + "-node canvas retains exact source and accessible list");
                    Capture("canvas-" + count); Click("MermaidCancel"); WaitClosed();
                }
                var complex = File.ReadAllText("tests/fixtures/native-mermaid-visual/complex.mmd");
                LaunchDocument("Complex.mmd", complex); OpenInsertion(); Click("MermaidFit"); Capture("complex");
                Check(Draft() == complex, "Complex nested and styled diagram is preserved through the native canvas");
                Click("MermaidCancel"); WaitClosed();
            }
            Record(true, null); return 0;

            void LaunchDocument(string name, string text)
            {
                if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); }
                var fixturePath = Path.Combine(root, name); File.WriteAllText(fixturePath, text);
                var launch = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
                launch.ArgumentList.Add("--state-directory"); launch.ArgumentList.Add(Path.Combine(root, "state-" + name.Replace('.', '-')));
                launch.ArgumentList.Add(root); launch.ArgumentList.Add(fixturePath);
                app = Application.Launch(launch); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The additional Mermaid fixture did not start.");
                editor = Find(main, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == text, "The additional fixture source did not load exactly.");
            }
            void ChangeApplicationTheme(string theme)
            {
                FocusWindow(main!);
                // An owned editor may cover the owner's menu; activate its actual UIA pattern.
                Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand();
                Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId(theme + "Theme")) is not null, "The application theme menu did not expand.");
                Find(main!, theme + "Theme").AsMenuItem().Invoke();
                Wait(() => ReadObservedText(Path.Combine(state, "preferences.json")).Contains("\"" + theme + "\"", StringComparison.Ordinal), "The application theme was not applied and persisted.");
            }
            void OpenInsertion(bool editable = true)
            {
                FocusWindow(main!); Find(main!, "InsertMenu").Click(); Find(main!, "InsertFlowchart").AsMenuItem().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "Insert Flowchart did not open the native editor.");
                if (polish) Capture("first-frame-insertion-" + ++firstFrames);
                if (editable) Wait(() => Find(visual!, "MermaidApply").IsEnabled, "The new diagram did not become editable.");
                if (polish) CaptureMeaningful("first-meaningful-insertion-" + firstFrames);
            }

            void Open(int blockOffset)
            {
                FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + blockOffset)) is not null, "The mapped Edit visually action was not available.");
                var watch = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + blockOffset).AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "The native visual editor did not open.");
                if (polish) Capture("first-frame-existing-" + ++firstFrames);
                Wait(() => Find(visual!, "MermaidApply").IsEnabled, "The visual editor did not become editable.");
                if (polish) CaptureMeaningful("first-meaningful-existing-" + firstFrames);
                timings.Add(new { Operation = "Open visual editor", Milliseconds = watch.Elapsed.TotalMilliseconds });
            }
            void Click(string id) => Find(visual!, id).AsButton().Invoke();
            void Select(string id) { Find(visual!, "MermaidCanvas_" + id).Patterns.Toggle.Pattern.Toggle(); }
            string Draft() => ReadEditorText(Find(visual!, "MermaidDraftSource"));
            void AwaitSource(Func<string, bool> predicate) => Wait(() => predicate(Draft()), "The Mermaid draft did not reach its expected semantic/source state: " + (visual is null ? "closed" : Find(visual, "MermaidStatus").Name));
            void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor"))) is null, "The visual editor did not close."); visual = null; }
            void CaptureMeaningful(string name)
            {
                Wait(() => Find(visual!, "MermaidDraftSource").BoundingRectangle.Height > 0 && Draft().Length > 0, "The source panel did not finish its initial native layout.");
                // Observe the compositor after real layout; never delay the application to mask painting.
                _ = DwmFlush(); Capture(name);
            }
            void Capture(string name) { visual!.CaptureToFile(Path.Combine(output, "mermaid-" + name + ".png")); }
            void Check(bool condition, string description) { if (!condition) throw new IOException(description); checks.Add(description); }
        }
        catch (Exception error)
        {
            try { visual?.CaptureToFile(Path.Combine(output, "mermaid-failure.png")); main?.CaptureToFile(Path.Combine(output, "mermaid-main-failure.png"));
                File.WriteAllLines(Path.Combine(output, "mermaid-failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(element => element.ControlType + ": " + element.AutomationId + " · " + element.Name) ?? []); } catch { }
            Record(false, error.ToString()); Console.Error.WriteLine(error); return 1;
        }
        finally { RestoreClipboard(clipboardBefore, clipboardOwned); if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); } }
        void Record(bool passed, string? error) => File.WriteAllText(Path.Combine(output, "mermaid-ui.json"), JsonSerializer.Serialize(new
        { Passed = passed, Checks = checks, Timings = timings, HighContrastRequested = highContrast, Dpi = GetDpiForSystem(), Error = error,
            Limitations = "Disposable hosted Windows desktop UIA; physical monitor/DPI and screen-reader certification are not claimed." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
