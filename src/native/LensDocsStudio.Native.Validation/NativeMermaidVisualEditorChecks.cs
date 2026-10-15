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
    public static int RunMermaidVisualEditor(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        Directory.CreateDirectory(Path.GetFullPath(output));
        using var clipboard = NativeUiClipboard.Start();
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var display = new NativeTestDisplay(output);
        var result = MermaidVisualEditorCore(executable, output, highContrast);
        if (result == 0) display.Complete(); return result;
    }).GetAwaiter().GetResult();

    private static int MermaidVisualEditorCore(string executable, string output, bool highContrast)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        var root = Path.Combine(output, "fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var state = Path.Combine(root, "state"); var path = Path.Combine(root, "Visual authoring.md");
        const string block = "```mermaid\r\nflowchart LR\r\n    %% Preserve me\r\n    A[Write] -->|Next| B(Review)\r\n```\r\n";
        const string original = "# Native visual authoring\r\n\r\n" + block + "\r\nMiddle paragraph.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); var diskBefore = File.ReadAllBytes(path);
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
            var offset = original.LastIndexOf("```mermaid", StringComparison.Ordinal); Open(offset);
            Capture("existing-light"); Select("node_B"); Capture("node-properties");
            Find(visual!, "MermaidNodeLabel").AsTextBox().Text = "Review changed";
            Check(!Find(visual!, "MermaidApply").IsEnabled, "Unapplied inspector text cannot be silently lost through Apply");
            Click("MermaidUpdateNode"); AwaitSource(text => text.Contains("B(\"Review changed\")", StringComparison.Ordinal));
            Check(ReadEditorText(editor) == original, "Draft node editing leaves the owning Markdown untouched");
            Find(visual!, "MermaidNodeId").AsTextBox().Text = "Reviewed"; Click("MermaidUpdateNode");
            AwaitSource(text => text.Contains("Reviewed(\"Review changed\")", StringComparison.Ordinal));
            Click("MermaidInsertDecision"); AwaitSource(text => text.Contains("Node1{", StringComparison.Ordinal));
            Click("MermaidFit"); Find(visual!, "MermaidPort_Reviewed").AsButton().Invoke(); Find(visual!, "MermaidCanvas_node_Node1").Click();
            AwaitSource(text => text.Contains("Reviewed --> Node1", StringComparison.Ordinal));
            Find(visual!, "MermaidEdgeLabel").AsTextBox().Text = "Confirmed"; Click("MermaidUpdateEdge");
            AwaitSource(text => text.Contains("-->|Confirmed|", StringComparison.Ordinal)); Capture("edge-properties");
            var connected = Draft(); Click("MermaidDelete"); AwaitSource(text => !text.Contains("Confirmed", StringComparison.Ordinal));
            Click("MermaidUndo"); AwaitSource(text => text == connected); Click("MermaidRedo"); AwaitSource(text => !text.Contains("Confirmed", StringComparison.Ordinal));
            Click("MermaidUndo"); AwaitSource(text => text == connected);
            Check(new MermaidSourceParser().Parse(Draft()).Edges.Count == 2, "Real canvas connection, label, deletion, Undo and Redo produce the expected semantic graph");
            Click("MermaidInsertSubgraph"); AwaitSource(text => text.Contains("subgraph Group1", StringComparison.Ordinal)); Capture("subgraph-properties");
            Find(visual!, "MermaidGroupLabel").AsTextBox().Text = "Capture group"; Click("MermaidUpdateGroup");
            AwaitSource(text => text.Contains("subgraph Group1[Capture group]", StringComparison.Ordinal));
            Select("node_Node1"); Find(visual!, "MermaidNodeGroup").AsComboBox().Select("Group1"); Click("MermaidMoveNode");
            AwaitSource(text => new MermaidSourceParser().Parse(text).Node("Node1").SubgraphId == "Group1");
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
            if (!highContrast)
            {
                FocusWindow(main); Find(main, "ViewMenu").Click(); Find(main, "DarkTheme").AsMenuItem().Invoke();
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
            void OpenInsertion(bool editable = true)
            {
                FocusWindow(main!); Find(main!, "InsertMenu").Click(); Find(main!, "InsertFlowchart").AsMenuItem().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "Insert Flowchart did not open the native editor.");
                if (editable) Wait(() => Find(visual!, "MermaidApply").IsEnabled, "The new diagram did not become editable.");
            }

            void Open(int blockOffset)
            {
                FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + blockOffset)) is not null, "The mapped Edit visually action was not available.");
                var watch = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + blockOffset).AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor")))?.AsWindow()) is not null, "The native visual editor did not open.");
                Wait(() => Find(visual!, "MermaidApply").IsEnabled, "The visual editor did not become editable.");
                timings.Add(new { Operation = "Open visual editor", Milliseconds = watch.Elapsed.TotalMilliseconds });
            }
            void Click(string id) => Find(visual!, id).AsButton().Invoke();
            void Select(string id) { Find(visual!, "MermaidCanvas_" + id).Patterns.Toggle.Pattern.Toggle(); }
            string Draft() => ReadEditorText(Find(visual!, "MermaidDraftSource"));
            void AwaitSource(Func<string, bool> predicate) => Wait(() => predicate(Draft()), "The Mermaid draft did not reach its expected semantic/source state: " + (visual is null ? "closed" : Find(visual, "MermaidStatus").Name));
            void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("MermaidVisualEditor"))) is null, "The visual editor did not close."); visual = null; }
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
        { Passed = passed, Checks = checks, Timings = timings, HighContrastRequested = highContrast, Error = error,
            Limitations = "Disposable hosted Windows desktop UIA; physical monitor/DPI and screen-reader certification are not claimed." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
