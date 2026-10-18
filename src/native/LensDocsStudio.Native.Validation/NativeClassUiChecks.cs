using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using Mermaider.Models;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunClassEditor(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var path = Path.Combine(root, "Class.md"); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var source = ClassDiagramSourceParser.Starter.ReplaceLineEndings("\r\n"); var block = "```mermaid\r\n" + source + "```\r\n";
        var original = "# Synthetic software documentation\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); var initialSavedBytes = File.ReadAllBytes(path); var checks = new List<string>(); var observations = new List<object>(); Application? app = null; Window? main = null; Window? visual = null;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active during Class authoring");
            Launch(); var editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "Class fixture did not load exactly."); Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Capture("starter");
            CanvasClass("Document").Click(); Wait(() => Find(visual!, "ClassNodeId").AsTextBox().Text == "Document", "Pointer class-header selection did not reach its exact inspector."); Check(CanvasClass("Document").Patterns.SelectionItem.Pattern.IsSelected.Value, "Actual class-header pointer selection and native SelectionItem report the exact source identity");
            Click("ClassAddAttribute"); Set("ClassAttributeName", "Revision"); Set("ClassAttributeType", "int"); Choose("ClassVisibility", "Private"); Click("ClassUpdate");
            Await(model => model.Class("Document").Attributes.Any(item => item.Semantic.Name == "Revision" && item.Semantic.Type == "int" && item.Semantic.Visibility == ClassVisibility.Private)); Capture("attribute-inspector");
            Set("ClassAttributeName", "CurrentRevision"); Set("ClassAttributeType", "long"); Choose("ClassVisibility", "Protected"); Click("ClassUpdate"); Await(model => model.Class("Document").Attributes[^1].Semantic.Name == "CurrentRevision");
            SelectClass("Document"); CanvasMember("Document", false, 1).Click();
            Wait(() => Find(visual!, "ClassSelectionSummary").Name == "Document · attribute occurrence " + (MemberIndex("Document", false, 1) + 1) && Find(visual!, "ClassAttributeName").AsTextBox().Text == "CurrentRevision", "Actual attribute-row pointer selection did not match its exact source occurrence."); Check(true, "Actual attribute-row pointer selection retains source identity across compartment ordering");
            Click("ClassAddMethod"); Set("ClassAttributeName", "Save"); Set("ClassParameters", "document, options"); Set("ClassAttributeType", "bool"); Click("ClassUpdate"); Await(model => model.Class("Document").Methods.Any(item => item.Semantic.Name == "Save"));
            CanvasMember("Document", true, 0).Click();
            Wait(() => Find(visual!, "ClassAttributeName").AsTextBox().Text == "Open", "Actual member-row pointer selection did not select the original exact method occurrence."); Check(true, "Actual member-row pointer selection maps the original exact method occurrence");
            Set("ClassParameters", "path, options"); Set("ClassAttributeType", "MarkdownDocument"); Click("ClassUpdate"); Await(model => model.Class("Document").Methods[0].Semantic.Params == "path, options" && model.Class("Document").Methods[0].Semantic.Type == "MarkdownDocument"); Capture("method-inspector");
            Click("ClassAddClass"); Set("ClassNodeId", "DocumentService"); Click("ClassUpdate"); Await(model => model.Classes.Any(item => item.Id == "DocumentService"));
            Wait(() => !CanvasClass("DocumentService").IsOffscreen && Find(visual!, "ClassViewport").BoundingRectangle.IntersectsWith(CanvasClass("DocumentService").BoundingRectangle), "New canonical class was not brought into view."); Check(true, "Class insertion selects and brings the canonical header into view");
            Click("ClassAddRelationship"); Choose("ClassFrom", "Document"); Choose("ClassTo", "DocumentService"); Choose("ClassRelationshipType", "Inheritance"); Choose("ClassMarkerAt", "From"); Click("ClassUpdate");
            Await(model => model.Relationships.Count == 2 && model.Relationships[^1].Semantic.Type == ClassRelationType.Inheritance && model.Relationships[^1].Semantic.MarkerAt == ClassMarkerAt.From);
            SelectRelationship(0); Choose("ClassRelationshipType", "Composition"); Choose("ClassMarkerAt", "From"); Set("ClassLabel", "owns documentation"); Set("ClassFirstCardinality", "1"); Set("ClassSecondCardinality", "0..*"); Click("ClassUpdate");
            Await(model => model.Relationships[0].Semantic.Type == ClassRelationType.Composition && model.Relationships[0].Semantic.Label == "owns documentation"); Capture("relationship-inspector");
            SelectClass("Document"); visual!.FindAllDescendants().Single(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ClassCanvas_", StringComparison.Ordinal) && item.Name.StartsWith("Relationship 1 ·", StringComparison.Ordinal)).Click();
            Wait(() => Find(visual!, "ClassSelectionSummary").Name == "Relationship occurrence 1", "Actual relationship-label pointer selection did not retain exact occurrence identity."); Check(true, "Actual relationship-label pointer selection identifies the correct source occurrence");
            Click("ClassReconnectTo"); CanvasClass("DocumentService").Click(); Click("ClassUpdate"); Await(model => model.Relationships[0].Semantic.To == "DocumentService");
            Check(Model().Relationships[0].Semantic.ToCardinality == "0..*" && Model().Relationships[0].Semantic.MarkerAt == ClassMarkerAt.From, "Safe canvas endpoint reconnection preserves marker and endpoint cardinality ownership");
            var changed = Draft(); Click("ClassUndo"); Await(model => model.Relationships[0].Semantic.To == "MarkdownDocument"); Click("ClassRedo"); Await(_ => Draft() == changed);
            Click("ClassReverse"); Await(model => model.Relationships[0].Semantic.From == "DocumentService" && model.Relationships[0].Semantic.MarkerAt == ClassMarkerAt.To && model.Relationships[0].Semantic.FromCardinality == "0..*"); Click("ClassUndo"); Await(_ => Draft() == changed);
            Click("ClassFit"); var endpoint = Find(visual!, "ClassSecondEndpoint").BoundingRectangle; var timer = Stopwatch.StartNew();
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left);
            try { for (var step = 1; step <= 20; step++) Mouse.MoveTo(endpoint.Left + step, endpoint.Top + step); Keyboard.Type(VirtualKeyShort.ESCAPE); } finally { Mouse.Up(MouseButton.Left); }
            Check((Find(visual!, "ClassStatus").Properties.HelpText.ValueOrDefault ?? "").StartsWith("Class endpoint drag updates:", StringComparison.Ordinal), "Actual endpoint movement records pointer-only processing");
            Check(Draft() == changed && !app!.HasExited, "Pointer cancellation retains exact source/history and releases capture"); observations.Add(new { Operation = "Twenty actual endpoint pointer updates and cancellation", Milliseconds = timer.Elapsed.TotalMilliseconds });
            endpoint = Find(visual!, "ClassSecondEndpoint").BoundingRectangle; var emptyViewport = Find(visual!, "ClassViewport").BoundingRectangle;
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left); Mouse.MoveTo(emptyViewport.Right - 15, emptyViewport.Bottom - 15); Mouse.Up(MouseButton.Left);
            Wait(() => Find(visual!, "ClassStatus").Name.Contains("unchanged", StringComparison.OrdinalIgnoreCase), "Invalid Class endpoint drop feedback was not observed."); Check(Draft() == changed, "Actual invalid endpoint drop preserves exact private source/history");
            endpoint = Find(visual!, "ClassSecondEndpoint").BoundingRectangle; var destination = CanvasClass("MarkdownDocument").BoundingRectangle;
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left);
            try { Mouse.MoveTo(destination.Left + destination.Width / 2, destination.Top + Math.Min(8, destination.Height / 2)); } finally { Mouse.Up(MouseButton.Left); }
            Await(model => model.Relationships[0].Semantic.To == "MarkdownDocument"); Check(Model().Relationships[0].Semantic.ToCardinality == "0..*" && Model().Relationships[0].Semantic.MarkerAt == ClassMarkerAt.From, "Actual valid endpoint drag commits one exact edit and preserves cardinality/marker ownership"); Click("ClassUndo"); Await(_ => Draft() == changed);
            Click("ClassValidate"); Wait(() => Find(visual!, "ClassApply").IsEnabled, "Complete Class validation did not finish."); Check(ReadEditorText(editor) == original && File.ReadAllText(path) == original && initialSavedBytes.SequenceEqual(File.ReadAllBytes(path)), "Private Class authoring preserves owning document and saved bytes");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            {
                Resize(width, height); Click("ClassFit"); Capture("viewport-" + width + "x" + height); Check(visual!.BoundingRectangle.Contains(Find(visual, "ClassApply").BoundingRectangle), "Apply remains visible at " + width + "x" + height);
                SelectMember("Document", MemberIndex("Document", true, 0));
                foreach (var fieldId in new[] { "ClassAttributeType", "ClassAttributeName", "ClassParameters", "ClassVisibility", "ClassUpdate" })
                { var field = Find(visual!, fieldId); field.Focus(); Wait(() => !field.IsOffscreen && Find(visual!, "ClassInspectorViewport").BoundingRectangle.Contains(field.BoundingRectangle), "Class field clipped at " + width + "x" + height + ": " + fieldId); }
                Capture("method-viewport-" + width + "x" + height); SelectRelationship(0);
            }
            Resize(1280, 800); Capture("validated"); var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("ClassApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "Apply did not change only the second identical Class fence.");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == original, "Apply did not create exactly one owning document Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "Document Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Class document Save failed.");
            var savedBytes = File.ReadAllBytes(path);
            Check(true, "Real Open, Select, Add/Edit attribute, Add/Edit method, Add class, Inheritance, Composition, Reconnect, Validate, private Undo/Redo, Apply, document Undo/Redo and Save pass");
            FocusWindow(main!); main!.Close(); Wait(() => app!.HasExited, "The saved native Class document did not close gracefully."); Check(true, "Saved owning document closes gracefully before process restart");
            Stop(); Launch(); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Restart/reopen retains exact portable Class source and canonical semantics"); Capture("saved-reopened");
            SelectClass("Document"); Click("ClassAddAttribute"); Set("ClassAttributeName", "CancelledAttribute"); Click("ClassUpdate"); Await(model => model.Class("Document").Attributes.Any(item => item.Semantic.Name == "CancelledAttribute")); Click("ClassCancel"); Discard(); WaitClosed();
            Check(ReadEditorText(Find(main!, "MarkdownEditor")) == expected && File.ReadAllText(path) == expected && savedBytes.SequenceEqual(File.ReadAllBytes(path)), "Cancel of a separate changed draft preserves owning source and saved bytes");
            if (!highContrast) foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); SelectClass("Document"); Capture(theme.ToLowerInvariant()); Click("ClassCancel"); WaitClosed(); }
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "InsertClass").AsMenuItem().Invoke(); WaitVisual();
            Check(Draft() == ClassDiagramSourceParser.Starter, "Insert Class opens the exact validated portable starter"); Click("ClassApply"); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")).Contains("classDiagram", StringComparison.Ordinal), "Class insertion applies a portable Mermaid fence");
            RunAdditionalCases();
            File.WriteAllText(Path.Combine(output, "class-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "class-failure.txt"), error.ToString());
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(item => item.ControlType + ": " + item.AutomationId + " · " + item.Name + " · enabled=" + item.IsEnabled + " · " + item.Properties.HelpText.ValueOrDefault) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "class-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Checks = checks, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { Mouse.Up(MouseButton.Left); Stop(); }
        void Stop() { if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); app = null; main = null; visual = null; } }
        void Launch(string? fixturePath = null)
        {
            var fixtureState = fixturePath is null ? state : Path.Combine(root, "state-" + Path.GetFileNameWithoutExtension(fixturePath)); Directory.CreateDirectory(fixtureState);
            if (fixturePath is not null) File.WriteAllText(Path.Combine(fixtureState, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(fixtureState); info.ArgumentList.Add(root); info.ArgumentList.Add(fixturePath ?? path);
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native Class fixture did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800);
        }
        void WaitVisual()
        {
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("ClassVisualEditor")))?.AsWindow()) is not null, "The native Class editor did not open.");
            Wait(() => Find(visual!, "ClassApply").IsEnabled, "Canonical Class mapping did not become editable: " + Find(visual!, "ClassStatus").Name); Find(visual!, "ClassSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("ClassFit");
        }
        void Open(int offset) { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact Class preview action did not appear."); var timer = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); observations.Add(new { Operation = "Open Class editor including UIA transport", Milliseconds = timer.Elapsed.TotalMilliseconds }); }
        AutomationElement CanvasClass(string id) => visual!.FindAllDescendants().Single(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ClassCanvas_", StringComparison.Ordinal) && item.Name == "Class " + id);
        int MemberIndex(string id, bool method, int index) { var cls = Model().Class(id); return cls.Members.ToList().IndexOf((method ? cls.Methods : cls.Attributes)[index]); }
        AutomationElement CanvasMember(string id, bool method, int index) { var prefix = "  " + id + " · " + (method ? "method " : "attribute ") + (MemberIndex(id, method, index) + 1) + " ·"; return visual!.FindAllDescendants().Single(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ClassCanvas_", StringComparison.Ordinal) && item.Name.StartsWith(prefix, StringComparison.Ordinal)); }
        void SelectClass(string id) { CanvasClass(id).Patterns.SelectionItem.Pattern.Select(); Wait(() => Find(visual!, "ClassNodeId").AsTextBox().Text == id, "Class selection did not reach inspector."); }
        void SelectIndex(int index) { var list = Find(visual!, "ClassElements"); list.Focus(); Keyboard.Type(VirtualKeyShort.HOME); for (var i = 0; i < index; i++) Keyboard.Type(VirtualKeyShort.DOWN); }
        void SelectMember(string id, int row) { var index = 0; foreach (var cls in Model().Classes) { if (cls.Id == id) { index += row + 1; break; } index += 1 + cls.Members.Count; } SelectIndex(index); Wait(() => Find(visual!, "ClassSelectionSummary").Name.Contains("occurrence " + (row + 1), StringComparison.Ordinal), "Exact member occurrence selection failed."); }
        void SelectRelationship(int index) { SelectIndex(Model().Classes.Sum(item => 1 + item.Members.Count) + index); Wait(() => Find(visual!, "ClassSelectionSummary").Name == "Relationship occurrence " + (index + 1), "Exact relationship occurrence selection failed."); }
        string Draft() => ReadEditorText(Find(visual!, "ClassSource"));
        ClassDiagramModel Model() => new ClassDiagramSourceParser().Parse(Draft());
        void Await(Func<ClassDiagramModel, bool> predicate) => Wait(() => predicate(Model()) && Find(visual!, "ClassApply").IsEnabled, "Private Class draft did not reach expected state: " + Find(visual!, "ClassStatus").Name);
        void Set(string id, string value) => Find(visual!, id).AsTextBox().Text = value;
        void Click(string id) { Wait(() => Find(visual!, id).IsEnabled, "Class command is disabled: " + id + "; " + Find(visual!, id).Properties.HelpText.ValueOrDefault + "; " + Find(visual!, "ClassStatus").Name); Find(visual!, id).AsButton().Invoke(); }
        void Choose(string id, string item) { var combo = Find(visual!, id).AsComboBox(); combo.Select(item); if (combo.Patterns.ExpandCollapse.IsSupported) combo.Patterns.ExpandCollapse.Pattern.Collapse(); }
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("ClassVisualEditor"))) is null, "Class editor did not close."); visual = null; }
        void Resize(int width, int height) { visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && Math.Abs(visual.BoundingRectangle.Height - height) < 3 && FooterSettled(), "Class viewport did not settle."); }
        bool FooterSettled() { var window = visual!.BoundingRectangle; var footer = Find(visual, "ClassApply").BoundingRectangle; return window.Right - footer.Right is >= 4 and <= 32 && window.Bottom - footer.Bottom is >= 4 and <= 24; }
        void Capture(string name) { Mouse.MoveTo(visual!.BoundingRectangle.Left + 20, visual.BoundingRectangle.Top + 16); Wait(FooterSettled, "Class client layout incomplete before capture."); if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("Native Class app did not become idle before capture."); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Discard() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "Class discard confirmation did not appear."); automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void RunAdditionalCases()
        {
            Stop();
            foreach (var (name, raw) in new[] {
                ("unknown-protected", "classDiagram\nclass A {\n+string Name\n}\nunknown directive\n"),
                ("identical-occurrences", "classDiagram\nclass A {\n+string Name\n+string Name\n+Run(path) bool\n+Run(path) bool\n}\nA --> A : self\nA --> A : self\n"),
                ("empty-cardinalities", "classDiagram\nclass A\nA \"\" --> \"\" A : original\n"),
                ("reserved-note-target", "classDiagram\nclass A {\n+string Name\n}\nnote for B \"Future target\"\n"),
                ("empty-final", "classDiagram\nclass A\n") })
            {
                var fixture = Path.Combine(root, name + ".mmd"); File.WriteAllText(fixture, raw); var fixtureBytes = File.ReadAllBytes(fixture); Launch(fixture); Open(0);
                if (name == "unknown-protected")
                {
                    SelectClass("A"); Check(!Find(visual!, "ClassNodeId").IsEnabled && !Find(visual!, "ClassDelete").IsEnabled && !Find(visual!, "ClassUndo").IsEnabled, "Unknown global syntax protects native class structure without history");
                    SelectMember("A", 0); Check(!Find(visual!, "ClassAttributeName").IsEnabled && !Find(visual!, "ClassUpdate").IsEnabled, "Unknown dependencies cannot be overwritten through member properties");
                    Check(!string.IsNullOrWhiteSpace(Find(visual!, "ClassUpdate").Properties.HelpText.ValueOrDefault), "Protected commands expose accessible operation reasons"); Capture(name); Click("ClassCancel"); WaitClosed();
                }
                else if (name == "reserved-note-target")
                {
                    SelectClass("A"); Set("ClassNodeId", "B");
                    Wait(() => !Find(visual!, "ClassUpdate").IsEnabled, "An unresolved note target did not protect native class rename.");
                    Check(Draft() == raw && !Find(visual!, "ClassUndo").IsEnabled && Find(visual!, "ClassNodeId").AsTextBox().Text == "B"
                        && (Find(visual!, "ClassUpdate").Properties.HelpText.ValueOrDefault ?? "").Contains("unresolved note", StringComparison.Ordinal), "Unresolved note targets protect native identity changes while preserving source, history and pending properties");
                    Click("ClassResetProperties"); Wait(() => Find(visual!, "ClassNodeId").AsTextBox().Text == "A", "Reset did not restore protected Class properties.");
                    SelectIndex(2); Wait(() => Find(visual!, "ClassSelectionSummary").Name == "Note for B", "The unresolved note did not retain exact source inspection.");
                    Check(Find(visual!, "ClassDependencies").Name.Contains("Future target", StringComparison.Ordinal), "Read-only unresolved notes retain their exact target and text in the native inspector");
                    Click("ClassCancel"); WaitClosed();
                }
                else if (name == "empty-cardinalities")
                {
                    SelectRelationship(0); Set("ClassLabel", "changed"); Click("ClassUpdate"); Await(model => model.Relationships[0].Semantic.Label == "changed");
                    Check(Draft() == raw.Replace("original", "changed", StringComparison.Ordinal), "Unrelated native relationship properties preserve both quoted empty cardinalities"); Click("ClassCancel"); Discard(); WaitClosed();
                }
                else if (name == "empty-final")
                {
                    SelectClass("A"); Click("ClassDelete"); Await(model => model.Classes.Count == 0); Check(Find(visual!, "ClassAddClass").IsEnabled, "Deleting the last independent class retains an editable empty native draft");
                    Click("ClassUndo"); Await(_ => Draft() == raw); Click("ClassRedo"); Await(model => model.Classes.Count == 0);
                    Click("ClassAddClass"); Set("ClassNodeId", "B"); Click("ClassUpdate"); Await(model => model.Classes.Single().Id == "B"); Click("ClassCancel"); Discard(); WaitClosed();
                }
                else
                {
                    SelectMember("A", 1); Click("ClassMoveUp"); Await(_ => Draft() == raw);
                    Wait(() => Find(visual!, "ClassSelectionSummary").Name == "A · attribute occurrence 1", "Moving identical attributes lost the selected source occurrence."); Check(Find(visual!, "ClassUndo").IsEnabled, "Identical attribute movement creates private occurrence history without metadata in source");
                    Set("ClassAttributeName", "MovedName"); Click("ClassUpdate"); Await(model => model.Class("A").Attributes[0].Semantic.Name == "MovedName"); Check(Model().Class("A").Attributes[1].Semantic.Name == "Name", "Editing a moved identical attribute cannot change another occurrence");
                    Click("ClassUndo"); Await(_ => Draft() == raw); Click("ClassUndo"); Await(_ => Draft() == raw); Wait(() => Find(visual!, "ClassSelectionSummary").Name == "A · attribute occurrence 2", "Undo did not restore identical member identities.");
                    SelectRelationship(1); Set("ClassLabel", "second self occurrence"); Click("ClassUpdate"); Await(model => model.Relationships[1].Semantic.Label == "second self occurrence"); Check(Model().Relationships[0].Semantic.Label == "self", "Parallel self-relationship canvas/list identity remains exact"); Capture(name); Click("ClassCancel"); Discard(); WaitClosed();
                }
                Check(ReadEditorText(Find(main!, "MarkdownEditor")) == raw && File.ReadAllText(fixture) == raw && fixtureBytes.SequenceEqual(File.ReadAllBytes(fixture)), name + ": Cancel preserves exact standalone document and saved bytes"); Stop();
            }
            foreach (var scenario in new[] { "outside-change", "changed-owner", "closed-owner", "other-tab" })
            {
                var body = "classDiagram\r\nclass A {\r\n+string Name\r\n}\r\nA --> A : self\r\n";
                var raw = scenario == "outside-change" ? "# Outside changes\r\n\r\n```mermaid\r\n" + body + "```\r\n" : body;
                var fixture = Path.Combine(root, scenario + (scenario == "outside-change" ? ".md" : ".mmd")); File.WriteAllText(fixture, raw); Launch(fixture); Open(scenario == "outside-change" ? raw.IndexOf("```mermaid", StringComparison.Ordinal) : 0);
                SelectMember("A", 0); Set("ClassAttributeName", "PrivateName"); Click("ClassUpdate"); Await(model => model.Class("A").Attributes[0].Semantic.Name == "PrivateName"); var privateSource = Draft();
                FocusWindow(main!); var ownerEditor = Find(main!, "MarkdownEditor"); ownerEditor.Focus();
                if (scenario == "outside-change")
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type("%% Outside comment"); Keyboard.Type(VirtualKeyShort.RETURN);
                    Wait(() => ReadEditorText(ownerEditor) == "%% Outside comment\r\n" + raw, "Outside Class owner edit did not arrive."); FocusWindow(visual!); Click("ClassApply"); WaitClosed();
                    var targetBody = MermaidBlockSource.Find(new MarkdownService().Parse(raw)).Single().Body;
                    Check(ReadEditorText(ownerEditor) == "%% Outside comment\r\n" + raw[..targetBody.Start] + privateSource + raw[targetBody.End..], "Class Apply relocates its original container and preserves surrounding edits");
                }
                else if (scenario == "changed-owner")
                {
                    ownerEditor.Patterns.Value.Pattern.SetValue(raw.Replace("+string Name", "-int External", StringComparison.Ordinal)); var external = ReadEditorText(ownerEditor); FocusWindow(visual!); Click("ClassApply");
                    Wait(() => Find(visual!, "ClassStatus").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Concurrent owning source did not reject Class Apply."); Check(Draft() == privateSource && ReadEditorText(ownerEditor) == external, "Class conflict preserves private source and external document exactly"); Capture(scenario);
                }
                else if (scenario == "closed-owner")
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                    main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Single(item => item.Name.Contains("closed-owner.mmd", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select(); ownerEditor.Focus();
                    var count = main.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Length; Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
                    Wait(() => main.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Length < count, "Class owning tab did not close."); FocusWindow(visual!); Click("ClassApply");
                    Wait(() => Find(visual!, "ClassStatus").Name.Contains("owning document has been closed", StringComparison.Ordinal), "Closed owner did not reject Class Apply."); Check(Draft() == privateSource && File.ReadAllText(fixture) == raw, "Closed-owner rejection retains private source and saved bytes"); Capture(scenario);
                }
                else
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); var otherEditor = Find(main!, "MarkdownEditor"); otherEditor.Patterns.Value.Pattern.SetValue("Another local document"); FocusWindow(visual!); Click("ClassApply"); WaitClosed();
                    Check(ReadEditorText(otherEditor) == "Another local document", "Class Apply cannot redirect into the active unrelated tab");
                    main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Single(item => item.Name.Contains("other-tab.mmd", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select(); Wait(() => ReadEditorText(Find(main, "MarkdownEditor")) == privateSource, "Class Apply lost its original tab identity.");
                }
                if (visual is not null) { Click("ClassCancel"); Discard(); WaitClosed(); } Stop();
            }
        }
    }).GetAwaiter().GetResult();
}
