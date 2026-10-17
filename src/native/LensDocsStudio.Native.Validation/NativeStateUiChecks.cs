using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.StateDiagrams;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunStateEditor(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var path = Path.Combine(root, "State.md"); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var source = File.ReadAllText("tests/fixtures/native-state/document-review.mmd").ReplaceLineEndings("\r\n"); if (!source.EndsWith("\r\n", StringComparison.Ordinal)) source += "\r\n";
        var block = "```mermaid\r\n" + source + "```\r\n"; var original = "# Synthetic documentation review\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); var checks = new List<string>(); var observations = new List<object>(); Application? app = null; Window? main = null; Window? visual = null;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active during State authoring");
            Launch(); var editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "The State Markdown fixture did not load exactly."); Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Capture("starter");
            var draftButton = Find(visual!, "StateCanvas_state:Draft"); draftButton.Patterns.SelectionItem.Pattern.Select(); Wait(() => Find(visual!, "StateNodeId").AsTextBox().Text == "Draft", "Canvas State selection failed.");
            Check(draftButton.Patterns.SelectionItem.Pattern.IsSelected.Value, "State canvas exposes native SelectionItem and exact state identity");
            Check(Find(visual!, "StateDelete").Name.StartsWith("Delete", StringComparison.Ordinal) && Find(visual!, "StateReverse").Name.StartsWith("Reverse", StringComparison.Ordinal), "State toolbar action names remain meaningful independently of capability explanations");
            Find(visual!, "StateLabel").AsTextBox().Text = "Local draft"; Check(!Find(visual!, "StateApply").IsEnabled, "Pending State properties protect Apply"); Click("StateUpdate"); Await(text => new StateDiagramSourceParser().Parse(text).Node("Draft").Label == "Local draft"); Capture("state-inspector");
            SelectTransition(2); Find(visual!, "StateLabel").AsTextBox().Text = "Please revise locally"; Click("StateUpdate"); Await(text => new StateDiagramSourceParser().Parse(text).Transitions[2].Label == "Please revise locally");
            var pointerTarget = visual!.FindFirstDescendant(cf => cf.ByName("Transition 3: Review to Draft. Please revise locally")) ?? throw new IOException("The exact canonical transition target is missing.");
            Click("StateFit"); pointerTarget.Click(); Wait(() => Find(visual!, "StateSelectionSummary").Name == "Transition Review → Draft", "Precise native transition pointer selection failed."); Capture("transition-inspector");
            Click("StateAddState"); Find(visual!, "StateNodeId").AsTextBox().Text = "Archived"; Find(visual!, "StateLabel").AsTextBox().Text = "Archived locally"; Click("StateUpdate"); Await(text => text.Contains("as Archived", StringComparison.Ordinal));
            Click("StateAddTransition"); Find(visual!, "StateFrom").AsComboBox().Select("Review · Review"); Find(visual!, "StateTo").AsComboBox().Select("Archived · Archived locally"); Find(visual!, "StateLabel").AsTextBox().Text = "Archive"; Click("StateUpdate"); Await(text => text.Contains("Review --> Archived : Archive", StringComparison.Ordinal));
            Click("StateReconnectFrom"); Find(visual!, "StateCanvas_state:Approved").Click(); Click("StateUpdate"); Await(text => text.Contains("Approved --> Archived : Archive", StringComparison.Ordinal));
            Click("StateReconnectTo"); Find(visual!, "StateCanvas_state:Published").Click(); Click("StateUpdate"); Await(text => text.Contains("Approved --> Published : Archive", StringComparison.Ordinal));
            Click("StateUndo"); Await(text => text.Contains("Approved --> Archived : Archive", StringComparison.Ordinal));
            Click("StateReverse"); Await(text => text.Contains("Archived --> Approved : Archive", StringComparison.Ordinal)); Click("StateUndo"); Await(text => text.Contains("Approved --> Archived : Archive", StringComparison.Ordinal));
            Click("StateAddTransition"); Find(visual!, "StateFrom").AsComboBox().Select("Archived · Archived locally"); Find(visual!, "StateTo").AsComboBox().Select("Archived · Archived locally"); Find(visual!, "StateLabel").AsTextBox().Text = "Check again"; Click("StateUpdate"); Await(text => text.Contains("Archived --> Archived : Check again", StringComparison.Ordinal));
            var changed = Draft(); Click("StateUndo"); Await(text => !text.Contains("Archived --> Archived", StringComparison.Ordinal)); Click("StateRedo"); Await(text => text == changed);
            Click("StateAddInitial"); Find(visual!, "StateTo").AsComboBox().Select("Archived · Archived locally"); Click("StateUpdate"); Await(text => text.Contains("[*] --> Archived", StringComparison.Ordinal)); Click("StateUndo"); Await(text => text == changed);
            Click("StateAddFinal"); Find(visual!, "StateFrom").AsComboBox().Select("Archived · Archived locally"); Click("StateUpdate"); Await(text => text.Contains("Archived --> [*]", StringComparison.Ordinal)); Click("StateUndo"); Await(text => text == changed);
            Check(true, "Source and destination canvas reconnection, reverse and scoped initial/final palette connections validate and undo safely");
            Check(ReadEditorText(editor) == original && File.ReadAllText(path) == original, "State editing and private Undo/Redo leave owner/disk source exact before Apply");
            SelectState("Draft"); Click("StateFit");
            var dragMetrics = new List<string>(); var beforeDrag = Draft();
            for (var i = 0; i < 20; i++)
            {
                var bounds = Find(visual!, "StateCanvas_state:Draft").BoundingRectangle; var centre = new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                Mouse.MoveTo(centre); Mouse.Down(MouseButton.Left);
                try
                {
                    for (var step = 1; step <= 12; step++) { Mouse.MoveTo(centre.X + step * 2, centre.Y + step); Thread.Sleep(8); }
                    if (i == 1 && !highContrast)
                    {
                        using (var liveContrast = new NativeTestHighContrast())
                        { Wait(() => visual!.Properties.ItemStatus.ValueOrDefault == "High Contrast" && Find(visual!, "StateApply").IsEnabled, "Live High Contrast did not complete a safe State re-render during pointer capture."); Capture("live-high-contrast"); Check(Draft() == beforeDrag, "High Contrast and canonical re-render during dragging preserve private State source"); }
                        Wait(() => visual!.Properties.ItemStatus.ValueOrDefault != "High Contrast" && Find(visual!, "StateApply").IsEnabled, "State presentation did not restore after live High Contrast.");
                    }
                    if (i % 5 == 0) Keyboard.Type(VirtualKeyShort.ESCAPE);
                }
                finally { Mouse.Up(MouseButton.Left); }
                Check(Draft() == beforeDrag && !app!.HasExited, "Session drag " + (i + 1) + " preserves source and process stability"); dragMetrics.Add(Find(visual!, "StateStatus").Properties.HelpText.ValueOrDefault ?? "");
                if (i % 3 == 2) Click("StateAutoLayout");
            }
            observations.Add(new { Operation = "Twenty native State drags with 240 pointer moves, cancellation and Auto layout", Metrics = dragMetrics });
            Capture("session-drag"); Click("StateAutoLayout");
            Find(visual!, "StateSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("StateValidate"); Wait(() => Find(visual!, "StateApply").IsEnabled, "State validation did not finish.");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            { visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && Math.Abs(visual.BoundingRectangle.Height - height) < 3, "Requested State window dimensions did not settle."); Click("StateFit"); Capture("viewport-" + width + "x" + height); Check(visual.BoundingRectangle.Contains(Find(visual, "StateApply").BoundingRectangle), "State Apply remains visible at " + width + "x" + height); }
            visual!.Patterns.Transform.Pattern.Resize(1280, 800); Capture("validated");
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("StateApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "State Apply did not change only the second identical diagram.");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == original, "State Apply did not produce one document Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "State document Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "State Save failed.");
            Check(true, "Real native Open, Select, Edit state/transition, Add, Connect, self-transition, Validate, Apply, document Undo/Redo and Save pass");
            Stop(); Launch(); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Restart/reopen lost State source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Save/restart/reopen preserves complete State source and canonical rendering"); Capture("saved-reopened"); Click("StateCancel"); WaitClosed();
            if (!highContrast)
            {
                foreach (var theme in new[] { "Dark", "Light" })
                { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); SelectState("Draft"); Capture(theme.ToLowerInvariant()); Click("StateCancel"); WaitClosed(); }
            }
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").AsMenuItem().Click(); Find(main!, "InsertState").AsMenuItem().Click(); WaitVisual(); Check(new StateDiagramSourceParser().Parse(Draft()).IsValid, "Insert State opens a valid supported starter"); Click("StateApply"); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")).Contains("```mermaid", StringComparison.Ordinal), "Visual State insertion applies a portable Markdown fence");
            Stop();
            foreach (var fixture in new[] { "document-review", "advanced-protected", "unknown-protected" })
            {
                var raw = File.ReadAllText("tests/fixtures/native-state/" + fixture + ".mmd"); var rawPath = Path.Combine(root, fixture + (fixture == "unknown-protected" ? ".mermaid" : ".mmd")); File.WriteAllText(rawPath, raw); Launch(rawPath); Open(0);
                if (fixture == "advanced-protected")
                {
                    SelectState("Reviewer"); Check(!Find(visual!, "StateNodeId").IsEnabled && !Find(visual!, "StateDelete").IsEnabled, "Composite/notes/styles protect State ID and structural operations with accessible explanations");
                    Find(visual!, "StateLabel").AsTextBox().Text = "Independent local reviewer"; Click("StateUpdate"); Await(text => text.Contains("Independent local reviewer", StringComparison.Ordinal));
                    Check(Draft().Contains("note right of Reviewer : Synthetic local review", StringComparison.Ordinal), "Independent supported label edit retains protected note bytes"); Capture("advanced-protected");
                    var independentDraft = Draft(); SelectTransition(7); Find(visual!, "StateLabel").AsTextBox().Text = "Second identical occurrence"; Click("StateUpdate"); Await(text => new StateDiagramSourceParser().Parse(text).Transitions[7].Label == "Second identical occurrence");
                    Check(new StateDiagramSourceParser().Parse(Draft()).Transitions[6].Label == "Choose", "Repeated State transitions retain exact independent identities in the real native editor"); Click("StateUndo"); Await(text => text == independentDraft); Click("StateUndo"); Await(text => text == raw);
                }
                else if (fixture == "unknown-protected")
                {
                    SelectState("A"); Check(!Find(visual!, "StateAddState").IsEnabled, "Unknown State dependencies protect palette operations");
                    Find(visual!, "StateLabel").AsTextBox().Text = "Rejected unknown dependency";
                    Check(!Find(visual!, "StateUpdate").IsEnabled && !string.IsNullOrWhiteSpace(Find(visual!, "StateUpdate").Properties.HelpText.ValueOrDefault), "Unknown State edits are disabled with accessible dependency diagnostics");
                    Check(Draft() == raw && !Find(visual!, "StateUndo").IsEnabled && ReadEditorText(Find(main!, "MarkdownEditor")) == raw, "Rejected unknown State edits leave source, history and owning .mermaid document unchanged"); Capture("unknown-protected"); Click("StateResetProperties");
                }
                else
                {
                    SelectState("Draft"); Find(visual!, "StateLabel").AsTextBox().Text = "Private cancellation"; Click("StateUpdate"); Await(text => text.Contains("Private cancellation", StringComparison.Ordinal)); Click("StateCancel");
                    Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "State Cancel did not request private draft discard.");
                    automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")) == raw && File.ReadAllText(rawPath) == raw, "Source-preserving State Cancel retains exact owner and disk source");
                }
                if (visual is not null) { Click("StateCancel"); WaitClosed(); } Stop();
            }
            foreach (var scenario in new[] { "outside-change", "changed-owner", "closed-owner", "other-tab" })
            {
                var body = "stateDiagram-v2\r\nstate \"Draft\" as Draft\r\nDraft --> Draft : Check\r\n";
                var raw = scenario == "outside-change" ? "# Outside changes\r\n\r\n```mermaid\r\n" + body + "```\r\n" : body;
                var scenarioPath = Path.Combine(root, scenario + (scenario == "outside-change" ? ".md" : ".mmd")); File.WriteAllText(scenarioPath, raw); Launch(scenarioPath); Open(scenario == "outside-change" ? raw.IndexOf("```mermaid", StringComparison.Ordinal) : 0); SelectState("Draft");
                Find(visual!, "StateLabel").AsTextBox().Text = "Private change"; Click("StateUpdate"); Await(text => text.Contains("Private change", StringComparison.Ordinal)); var privateSource = Draft();
                FocusWindow(main!); var ownerEditor = Find(main!, "MarkdownEditor"); ownerEditor.Focus();
                if (scenario == "outside-change")
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type("%% Outside comment"); Keyboard.Type(VirtualKeyShort.RETURN);
                    Wait(() => ReadEditorText(ownerEditor) == "%% Outside comment\r\n" + raw, "The State owner outside edit did not arrive."); FocusWindow(visual!); Click("StateApply"); WaitClosed();
                    var targetBody = MermaidBlockSource.Find(new MarkdownService().Parse(raw)).Single().Body;
                    Check(ReadEditorText(ownerEditor) == "%% Outside comment\r\n" + raw[..targetBody.Start] + privateSource + raw[targetBody.End..], "State Apply relocates the owning Markdown container while preserving surrounding edits");
                }
                else if (scenario == "changed-owner")
                {
                    ownerEditor.Patterns.Value.Pattern.SetValue(raw.Replace("Draft\"", "External\"", StringComparison.Ordinal)); var outside = ReadEditorText(ownerEditor); FocusWindow(visual!); Click("StateApply");
                    Wait(() => Find(visual!, "StateStatus").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Concurrent owner edits were not rejected."); Check(Draft() == privateSource && ReadEditorText(ownerEditor) == outside, "State owner conflicts retain private and external source exactly"); Capture(scenario);
                }
                else if (scenario == "closed-owner")
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                    main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Single(item => item.Name.Contains("closed-owner.mmd", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select(); ownerEditor.Focus();
                    var count = main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Length; Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
                    Wait(() => main.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Length < count, "The State owner tab did not close."); FocusWindow(visual!); Click("StateApply");
                    Wait(() => Find(visual!, "StateStatus").Name.Contains("owning document has been closed", StringComparison.Ordinal), "Closed State owner did not reject Apply."); Check(Draft() == privateSource && File.ReadAllText(scenarioPath) == raw, "Closed-owner rejection preserves private State source and disk bytes"); Capture(scenario);
                }
                else
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); var otherEditor = Find(main!, "MarkdownEditor"); otherEditor.Patterns.Value.Pattern.SetValue("Another local document");
                    FocusWindow(visual!); Click("StateApply"); WaitClosed(); Check(ReadEditorText(otherEditor) == "Another local document", "State Apply cannot redirect to the currently active other tab");
                    main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Single(item => item.Name.Contains("other-tab.mmd", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
                    Wait(() => ReadEditorText(Find(main, "MarkdownEditor")) == privateSource, "State Apply did not retain its owning tab identity."); Check(true, "State Apply updates the original owner while another tab is active");
                }
                if (visual is not null) { Click("StateCancel"); Discard(); WaitClosed(); } Stop();
            }
            File.WriteAllText(Path.Combine(output, "state-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "state-failure.txt"), error.ToString());
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(item => item.ControlType + ": " + item.AutomationId + " · " + item.Name) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "state-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Checks = checks, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { Mouse.Up(MouseButton.Left); Stop(); }
        void Stop() { if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); app = null; main = null; visual = null; } }
        void Launch(string? fixturePath = null)
        {
            var fixtureState = fixturePath is null ? state : Path.Combine(root, "state-" + Path.GetFileNameWithoutExtension(fixturePath)); Directory.CreateDirectory(fixtureState);
            if (fixturePath is not null) File.WriteAllText(Path.Combine(fixtureState, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(fixtureState); info.ArgumentList.Add(root); info.ArgumentList.Add(fixturePath ?? path);
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native State fixture did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800);
        }
        void WaitVisual()
        {
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("StateVisualEditor")))?.AsWindow()) is not null, "The native State editor did not open.");
            Wait(() => Find(visual!, "StateApply").IsEnabled, "Canonical State mapping did not become editable: " + Find(visual!, "StateStatus").Name); Find(visual!, "StateSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("StateFit");
        }
        void Open(int offset)
        { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "The exact State preview action did not appear."); var timer = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); observations.Add(new { Operation = "Open State editor including UIA transport", Milliseconds = timer.Elapsed.TotalMilliseconds }); }
        void SelectState(string id)
        { Find(visual!, "StateCanvas_state:" + id).Patterns.SelectionItem.Pattern.Select(); Wait(() => Find(visual!, "StateNodeId").AsTextBox().Text == id, "State selection did not reach the inspector."); }
        void SelectTransition(int index)
        {
            var list = Find(visual!, "StateElements"); var model = new StateDiagramSourceParser().Parse(Draft());
            // WPF virtualises off-screen tree rows. Native keyboard navigation
            // realises the exact source-order occurrence without guessing a label.
            list.Focus(); Keyboard.Type(VirtualKeyShort.HOME);
            for (var step = 0; step < model.Nodes.Count + model.Groups.Count + model.PseudoNodes.Count + index; step++) Keyboard.Type(VirtualKeyShort.DOWN);
            Wait(() => list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).Any(item => item.Name.StartsWith("Transition " + (index + 1) + " ·", StringComparison.Ordinal) && item.Patterns.SelectionItem.Pattern.IsSelected.Value), "Keyboard selection did not realise the exact transition occurrence.");
            Wait(() => Find(visual!, "StateSelectionSummary").Name.StartsWith("Transition", StringComparison.Ordinal), "Transition occurrence selection failed.");
        }
        string Draft() => ReadEditorText(Find(visual!, "StateSource"));
        void Await(Func<string, bool> predicate) => Wait(() => predicate(Draft()) && Find(visual!, "StateApply").IsEnabled, "The private State draft did not reach the expected state: " + Find(visual!, "StateStatus").Name);
        void Click(string id) => Find(visual!, id).AsButton().Invoke();
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("StateVisualEditor"))) is null, "The State editor did not close."); visual = null; }
        void Capture(string name) { _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Discard()
        { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "Private State discard confirmation did not appear."); automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
    }).GetAwaiter().GetResult();
}
