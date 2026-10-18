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
        File.WriteAllText(path, original); var checks = new List<string>(); var observations = new List<object>(); Application? app = null; Window? main = null; Window? visual = null;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active during Class authoring");
            Launch(); var editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "Class fixture did not load exactly."); Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Capture("starter");
            SelectClass("Document"); Check(CanvasClass("Document").Patterns.SelectionItem.Pattern.IsSelected.Value, "Native class SelectionItem reports the selected source identity");
            Click("ClassAddAttribute"); Set("ClassAttributeName", "Revision"); Set("ClassAttributeType", "int"); Find(visual!, "ClassVisibility").AsComboBox().Select("Private"); Click("ClassUpdate");
            Await(model => model.Class("Document").Attributes.Any(item => item.Semantic.Name == "Revision" && item.Semantic.Type == "int" && item.Semantic.Visibility == ClassVisibility.Private)); Capture("attribute-inspector");
            Set("ClassAttributeName", "CurrentRevision"); Set("ClassAttributeType", "long"); Find(visual!, "ClassVisibility").AsComboBox().Select("Protected"); Click("ClassUpdate"); Await(model => model.Class("Document").Attributes[^1].Semantic.Name == "CurrentRevision");
            Click("ClassAddMethod"); Set("ClassAttributeName", "Save"); Set("ClassParameters", "document, options"); Set("ClassAttributeType", "bool"); Click("ClassUpdate"); Await(model => model.Class("Document").Methods.Any(item => item.Semantic.Name == "Save"));
            SelectMember("Document", 1); Check(Find(visual!, "ClassAttributeName").AsTextBox().Text == "Open", "Canvas/list member navigation selects the original exact method occurrence");
            Set("ClassParameters", "path, options"); Set("ClassAttributeType", "MarkdownDocument"); Click("ClassUpdate"); Await(model => model.Class("Document").Methods[0].Semantic.Params == "path, options" && model.Class("Document").Methods[0].Semantic.Type == "MarkdownDocument"); Capture("method-inspector");
            Click("ClassAddClass"); Set("ClassNodeId", "DocumentService"); Click("ClassUpdate"); Await(model => model.Classes.Any(item => item.Id == "DocumentService"));
            Wait(() => !CanvasClass("DocumentService").IsOffscreen && Find(visual!, "ClassViewport").BoundingRectangle.IntersectsWith(CanvasClass("DocumentService").BoundingRectangle), "New canonical class was not brought into view."); Check(true, "Class insertion selects and brings the canonical header into view");
            Click("ClassAddRelationship"); Find(visual!, "ClassFrom").AsComboBox().Select("Document"); Find(visual!, "ClassTo").AsComboBox().Select("DocumentService"); Find(visual!, "ClassRelationshipType").AsComboBox().Select("Inheritance"); Find(visual!, "ClassMarkerAt").AsComboBox().Select("From"); Click("ClassUpdate");
            Await(model => model.Relationships.Count == 2 && model.Relationships[^1].Semantic.Type == ClassRelationType.Inheritance && model.Relationships[^1].Semantic.MarkerAt == ClassMarkerAt.From);
            SelectRelationship(0); Find(visual!, "ClassRelationshipType").AsComboBox().Select("Composition"); Find(visual!, "ClassMarkerAt").AsComboBox().Select("From"); Set("ClassLabel", "owns documentation"); Set("ClassFirstCardinality", "1"); Set("ClassSecondCardinality", "0..*"); Click("ClassUpdate");
            Await(model => model.Relationships[0].Semantic.Type == ClassRelationType.Composition && model.Relationships[0].Semantic.Label == "owns documentation"); Capture("relationship-inspector");
            Click("ClassReconnectTo"); CanvasClass("DocumentService").Click(); Click("ClassUpdate"); Await(model => model.Relationships[0].Semantic.To == "DocumentService");
            Check(Model().Relationships[0].Semantic.ToCardinality == "0..*" && Model().Relationships[0].Semantic.MarkerAt == ClassMarkerAt.From, "Safe canvas endpoint reconnection preserves marker and endpoint cardinality ownership");
            var changed = Draft(); Click("ClassUndo"); Await(model => model.Relationships[0].Semantic.To == "MarkdownDocument"); Click("ClassRedo"); Await(_ => Draft() == changed);
            Click("ClassReverse"); Await(model => model.Relationships[0].Semantic.From == "DocumentService" && model.Relationships[0].Semantic.MarkerAt == ClassMarkerAt.To && model.Relationships[0].Semantic.FromCardinality == "0..*"); Click("ClassUndo"); Await(_ => Draft() == changed);
            Click("ClassFit"); var endpoint = Find(visual!, "ClassSecondEndpoint").BoundingRectangle; var timer = Stopwatch.StartNew();
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left);
            try { for (var step = 1; step <= 20; step++) Mouse.MoveTo(endpoint.Left + step, endpoint.Top + step); Keyboard.Type(VirtualKeyShort.ESCAPE); } finally { Mouse.Up(MouseButton.Left); }
            Check((Find(visual!, "ClassStatus").Properties.HelpText.ValueOrDefault ?? "").StartsWith("Class endpoint drag updates:", StringComparison.Ordinal), "Actual endpoint movement records pointer-only processing");
            Check(Draft() == changed && !app!.HasExited, "Pointer cancellation retains exact source/history and releases capture"); observations.Add(new { Operation = "Twenty actual endpoint pointer updates and cancellation", Milliseconds = timer.Elapsed.TotalMilliseconds });
            Click("ClassValidate"); Wait(() => Find(visual!, "ClassApply").IsEnabled, "Complete Class validation did not finish."); Check(ReadEditorText(editor) == original && File.ReadAllText(path) == original, "Private Class authoring preserves owning document and saved bytes");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            {
                Resize(width, height); Click("ClassFit"); Capture("viewport-" + width + "x" + height); Check(visual!.BoundingRectangle.Contains(Find(visual, "ClassApply").BoundingRectangle), "Apply remains visible at " + width + "x" + height);
                SelectMember("Document", 1);
                foreach (var fieldId in new[] { "ClassAttributeType", "ClassAttributeName", "ClassParameters", "ClassVisibility", "ClassUpdate" })
                { var field = Find(visual!, fieldId); field.Focus(); Wait(() => !field.IsOffscreen && Find(visual!, "ClassInspectorViewport").BoundingRectangle.Contains(field.BoundingRectangle), "Class field clipped at " + width + "x" + height + ": " + fieldId); }
                Capture("method-viewport-" + width + "x" + height); SelectRelationship(0);
            }
            Resize(1280, 800); Capture("validated"); var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("ClassApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "Apply did not change only the second identical Class fence.");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == original, "Apply did not create exactly one owning document Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "Document Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Class document Save failed.");
            Check(true, "Real Open, Select, Add/Edit attribute, Add/Edit method, Add class, Inheritance, Composition, Reconnect, Validate, private Undo/Redo, Apply, document Undo/Redo and Save pass");
            Stop(); Launch(); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Restart/reopen retains exact portable Class source and canonical semantics"); Capture("saved-reopened");
            SelectClass("Document"); Click("ClassAddAttribute"); Set("ClassAttributeName", "CancelledAttribute"); Click("ClassUpdate"); Await(model => model.Class("Document").Attributes.Any(item => item.Semantic.Name == "CancelledAttribute")); Click("ClassCancel"); Discard(); WaitClosed();
            Check(ReadEditorText(Find(main!, "MarkdownEditor")) == expected && File.ReadAllText(path) == expected, "Cancel of a separate changed draft preserves owning source and saved bytes");
            if (!highContrast) foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); SelectClass("Document"); Capture(theme.ToLowerInvariant()); Click("ClassCancel"); WaitClosed(); }
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "InsertClass").AsMenuItem().Invoke(); WaitVisual();
            Check(Draft() == ClassDiagramSourceParser.Starter, "Insert Class opens the exact validated portable starter"); Click("ClassApply"); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")).Contains("classDiagram", StringComparison.Ordinal), "Class insertion applies a portable Mermaid fence");
            File.WriteAllText(Path.Combine(output, "class-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "class-failure.txt"), error.ToString());
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(item => item.ControlType + ": " + item.AutomationId + " · " + item.Name) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "class-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Checks = checks, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { Mouse.Up(MouseButton.Left); Stop(); }
        void Stop() { if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); app = null; main = null; visual = null; } }
        void Launch()
        {
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state); info.ArgumentList.Add(root); info.ArgumentList.Add(path);
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native Class fixture did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800);
        }
        void WaitVisual()
        {
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("ClassVisualEditor")))?.AsWindow()) is not null, "The native Class editor did not open.");
            Wait(() => Find(visual!, "ClassApply").IsEnabled, "Canonical Class mapping did not become editable: " + Find(visual!, "ClassStatus").Name); Find(visual!, "ClassSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("ClassFit");
        }
        void Open(int offset) { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact Class preview action did not appear."); var timer = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); observations.Add(new { Operation = "Open Class editor including UIA transport", Milliseconds = timer.Elapsed.TotalMilliseconds }); }
        AutomationElement CanvasClass(string id) => visual!.FindAllDescendants().Single(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ClassCanvas_", StringComparison.Ordinal) && item.Name == "Class " + id);
        void SelectClass(string id) { CanvasClass(id).Patterns.SelectionItem.Pattern.Select(); Wait(() => Find(visual!, "ClassNodeId").AsTextBox().Text == id, "Class selection did not reach inspector."); }
        void SelectIndex(int index) { var list = Find(visual!, "ClassElements"); list.Focus(); Keyboard.Type(VirtualKeyShort.HOME); for (var i = 0; i < index; i++) Keyboard.Type(VirtualKeyShort.DOWN); }
        void SelectMember(string id, int row) { var index = 0; foreach (var cls in Model().Classes) { if (cls.Id == id) { index += row + 1; break; } index += 1 + cls.Members.Count; } SelectIndex(index); Wait(() => Find(visual!, "ClassSelectionSummary").Name.Contains("occurrence " + (row + 1), StringComparison.Ordinal), "Exact member occurrence selection failed."); }
        void SelectRelationship(int index) { SelectIndex(Model().Classes.Sum(item => 1 + item.Members.Count) + index); Wait(() => Find(visual!, "ClassSelectionSummary").Name == "Relationship occurrence " + (index + 1), "Exact relationship occurrence selection failed."); }
        string Draft() => ReadEditorText(Find(visual!, "ClassSource"));
        ClassDiagramModel Model() => new ClassDiagramSourceParser().Parse(Draft());
        void Await(Func<ClassDiagramModel, bool> predicate) => Wait(() => predicate(Model()) && Find(visual!, "ClassApply").IsEnabled, "Private Class draft did not reach expected state: " + Find(visual!, "ClassStatus").Name);
        void Set(string id, string value) => Find(visual!, id).AsTextBox().Text = value;
        void Click(string id) => Find(visual!, id).AsButton().Invoke();
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("ClassVisualEditor"))) is null, "Class editor did not close."); visual = null; }
        void Resize(int width, int height) { visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && Math.Abs(visual.BoundingRectangle.Height - height) < 3 && FooterSettled(), "Class viewport did not settle."); }
        bool FooterSettled() { var window = visual!.BoundingRectangle; var footer = Find(visual, "ClassApply").BoundingRectangle; return window.Right - footer.Right is >= 4 and <= 32 && window.Bottom - footer.Bottom is >= 4 and <= 24; }
        void Capture(string name) { Wait(FooterSettled, "Class client layout incomplete before capture."); if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("Native Class app did not become idle before capture."); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Discard() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "Class discard confirmation did not appear."); automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
    }).GetAwaiter().GetResult();
}
