using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using Mermaider.Models;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunErEditor(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var path = Path.Combine(root, "ER.md"); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var source = File.ReadAllText("tests/fixtures/native-er/database-documentation.mmd").ReplaceLineEndings("\r\n"); if (!source.EndsWith("\r\n", StringComparison.Ordinal)) source += "\r\n";
        var block = "```mermaid\r\n" + source + "```\r\n"; var original = "# Synthetic database documentation\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); var checks = new List<string>(); var observations = new List<object>(); Application? app = null; Window? main = null; Window? visual = null; var launchOrdinal = 0;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active during ER authoring");
            Launch(); var editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "The ER fixture did not load exactly."); Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Capture("starter");
            SelectEntity("CUSTOMER"); var entityButton = Find(visual!, "ErCanvas_entity:CUSTOMER"); Check(entityButton.Patterns.SelectionItem.Pattern.IsSelected.Value, "Native entity SelectionItem exposes exact source identity");
            var beforeIdRename = Draft(); Find(visual!, "ErNodeId").AsTextBox().Text = "CLIENT"; Click("ErUpdate");
            Await(text => new ErDiagramSourceParser().Parse(text).Entities.Any(entity => entity.Id == "CLIENT" && entity.Label == "CUSTOMER"));
            Check(new ErDiagramSourceParser().Parse(Draft()).Relationships.All(relationship => relationship.Entity1 != "CUSTOMER" && relationship.Entity2 != "CUSTOMER"), "ID-only rename retains the displayed label and updates all native relationship references");
            Click("ErUndo"); Await(text => text == beforeIdRename); SelectEntity("CUSTOMER");
            Find(visual!, "ErLabel").AsTextBox().Text = "Customers locally"; Check(!Find(visual!, "ErApply").IsEnabled, "Pending entity properties protect Apply"); Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Label == "Customers locally"); Capture("entity-inspector");
            SelectAttribute("CUSTOMER", 2); Check(Find(visual!, "ErAttributeName").AsTextBox().Text == "email", "Verified canvas row selects the correct source attribute occurrence");
            Find(visual!, "ErAttributeComment").AsTextBox().Text = "Contact email edited locally"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[2].Comment == "Contact email edited locally");
            Find(visual!, "ErPK").Patterns.Toggle.Pattern.Toggle(); Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[2].Keys.Contains(ErKeyType.PK)); Capture("attribute-inspector");
            Click("ErAddAttribute"); Find(visual!, "ErAttributeName").AsTextBox().Text = "created_on"; Find(visual!, "ErAttributeType").AsTextBox().Text = "datetime"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes.Count == 4);
            Click("ErMoveUp"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[2].Name == "created_on"); Click("ErUndo"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[3].Name == "created_on");
            SelectRelationship(0); Find(visual!, "ErLabel").AsTextBox().Text = "places locally"; Find(visual!, "ErSecondCardinality").AsComboBox().Select("One or more   |{"); Find(visual!, "ErRelationshipType").AsComboBox().Select("Non-identifying   .."); Click("ErUpdate");
            Await(text => { var relationship = new ErDiagramSourceParser().Parse(text).Relationships[0]; return relationship.Label == "places locally" && relationship.Cardinality2 == ErCardinality.Many && !relationship.Identifying; }); Capture("relationship-inspector");
            Click("ErAddEntity"); Find(visual!, "ErNodeId").AsTextBox().Text = "SHIPMENT"; Find(visual!, "ErLabel").AsTextBox().Text = "Shipment"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entities.Any(item => item.Id == "SHIPMENT"));
            Wait(() => !Find(visual!, "ErCanvas_entity:SHIPMENT").IsOffscreen && Find(visual!, "ErViewport").BoundingRectangle.IntersectsWith(Find(visual!, "ErCanvas_entity:SHIPMENT").BoundingRectangle), "The new palette entity was not brought into the zoomed viewport.");
            Check(true, "New palette entities remain visible through the native zoom and scroll transform");
            Click("ErAddRelationship"); Find(visual!, "ErFrom").AsComboBox().Select("ORDER · ORDER"); Find(visual!, "ErTo").AsComboBox().Select("SHIPMENT · Shipment"); Find(visual!, "ErLabel").AsTextBox().Text = "ships"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships.Count == 3);
            var changed = Draft(); Click("ErUndo"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships.Count == 2); Click("ErRedo"); Await(text => text == changed);
            Click("ErReverse"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships[^1].Entity1 == "SHIPMENT"); Click("ErUndo"); Await(text => text == changed);
            Click("ErReconnectTo"); Find(visual!, "ErCanvas_entity:ORDER_LINE").Click(); Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships[^1].Entity2 == "ORDER_LINE"); Click("ErUndo"); Await(text => text == changed);
            Click("ErFit"); var endpoint = Find(visual!, "ErSecondEndpoint").BoundingRectangle; var beforeDrag = Draft(); var allocated = Process.GetProcessById(app!.ProcessId).PrivateMemorySize64; var timer = Stopwatch.StartNew();
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left);
            try { for (var step = 1; step <= 20; step++) Mouse.MoveTo(endpoint.Left + step, endpoint.Top + step); Keyboard.Type(VirtualKeyShort.ESCAPE); } finally { Mouse.Up(MouseButton.Left); }
            Check((Find(visual!, "ErStatus").Properties.HelpText.ValueOrDefault ?? "").StartsWith("ER endpoint drag updates:", StringComparison.Ordinal), "Endpoint dragging produces measured pointer updates before cancellation");
            Check(Draft() == beforeDrag && !app!.HasExited, "Real endpoint drag cancellation retains exact source/history and releases capture"); observations.Add(new { Operation = "Actual endpoint drag with twenty pointer updates and cancellation", Milliseconds = timer.Elapsed.TotalMilliseconds, PrivateBytesBefore = allocated, PrivateBytesAfter = Process.GetProcessById(app!.ProcessId).PrivateMemorySize64 });
            // Empty-space drop must retain source and history; a valid target is exercised through the inspector above.
            var empty = Find(visual!, "ErViewport").BoundingRectangle; endpoint = Find(visual!, "ErSecondEndpoint").BoundingRectangle;
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left); Mouse.MoveTo(empty.Right - 15, empty.Bottom - 15); Mouse.Up(MouseButton.Left);
            Wait(() => Find(visual!, "ErStatus").Name.Contains("unchanged", StringComparison.OrdinalIgnoreCase), "Invalid endpoint drop feedback missing."); Check(Draft() == changed, "Invalid endpoint drop leaves validated source unchanged");
            endpoint = Find(visual!, "ErSecondEndpoint").BoundingRectangle; var validTarget = Find(visual!, "ErCanvas_entity:ORDER_LINE").BoundingRectangle;
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left);
            try { Mouse.MoveTo(validTarget.Left + validTarget.Width / 2, validTarget.Top + Math.Min(8, validTarget.Height / 2)); } finally { Mouse.Up(MouseButton.Left); }
            Await(text => new ErDiagramSourceParser().Parse(text).Relationships[^1].Entity2 == "ORDER_LINE");
            Check(new ErDiagramSourceParser().Parse(Draft()).Relationships[^1].Cardinality2 == ErCardinality.ZeroMany, "Actual endpoint drop keeps the second endpoint cardinality"); Click("ErUndo"); Await(text => text == changed);
            Click("ErValidate"); Wait(() => Find(visual!, "ErApply").IsEnabled, "ER validation did not finish.");
            Check(ReadEditorText(editor) == original && File.ReadAllText(path) == original, "Private ER operations retain owner and disk bytes before Apply");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            {
                ResizeForCapture(width, height); Click("ErFit"); Capture("viewport-" + width + "x" + height); Check(visual!.BoundingRectangle.Contains(Find(visual, "ErApply").BoundingRectangle), "ER Apply remains visible at " + width + "x" + height);
                SelectAttribute("CUSTOMER", 2);
                foreach (var fieldId in new[] { "ErAttributeType", "ErAttributeName", "ErPK", "ErFK", "ErUK", "ErAttributeComment", "ErUpdate" })
                {
                    var field = Find(visual!, fieldId); field.Focus();
                    Wait(() => !field.IsOffscreen && Find(visual!, fieldId == "ErUpdate" ? "ErInspectorPanel" : "ErInspectorViewport").BoundingRectangle.Contains(field.BoundingRectangle) && !Find(visual!, "ErSelectionSummary").IsOffscreen, "ER attribute field is clipped at " + width + "x" + height + ": " + fieldId);
                }
                Find(visual!, "ErAttributeComment").Focus();
                Capture("attribute-viewport-" + width + "x" + height); Check(true, "All attribute fields and key markers remain keyboard accessible at " + width + "x" + height); SelectRelationship(2);
            }
            ResizeForCapture(1280, 800); Capture("validated");
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("ErApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "ER Apply did not change only the second identical diagram.");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == original, "ER Apply did not create one document Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "ER document Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "ER Save failed.");
            Check(true, "Real Open, Select entity/attribute/relationship, edit, cardinality, non-identifying, add, private Undo/Redo, Validate, Apply, document Undo/Redo and Save pass");
            Stop(); Launch(); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Restart/reopen lost ER source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Save/restart/reopen retains complete ER source and rendering"); SelectEntity("CUSTOMER"); SelectAttribute("CUSTOMER", 2); Capture("saved-reopened"); Click("ErCancel"); WaitClosed();
            if (!highContrast) foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); SelectEntity("CUSTOMER"); Capture(theme.ToLowerInvariant()); Click("ErCancel"); WaitClosed(); }
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "InsertEr").AsMenuItem().Invoke(); WaitVisual(); Check(new ErDiagramSourceParser().Parse(Draft()).IsValid, "Insert ER opens a valid portable starter"); Click("ErApply"); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")).Contains("```mermaid", StringComparison.Ordinal), "Visual ER insertion applies a portable Markdown fence"); Stop();
            foreach (var fixture in new[] { "occurrences", "unknown-protected", "empty-final", "identical-attributes" })
            {
                var raw = File.ReadAllText("tests/fixtures/native-er/" + fixture + ".mmd"); var rawPath = Path.Combine(root, fixture + (fixture == "unknown-protected" ? ".mermaid" : ".mmd")); File.WriteAllText(rawPath, raw); Launch(rawPath); Open(0);
                if (fixture == "empty-final")
                {
                    SelectEntity("A"); Click("ErDelete"); Await(text => new ErDiagramSourceParser().Parse(text).Entities.Count == 0);
                    Check(Find(visual!, "ErAddEntity").IsEnabled, "Deleting the last independent entity leaves an editable empty native draft");
                    Click("ErUndo"); Await(text => text == raw); Click("ErRedo"); Await(text => new ErDiagramSourceParser().Parse(text).Entities.Count == 0);
                    Click("ErAddEntity"); Find(visual!, "ErNodeId").AsTextBox().Text = "B"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entities.Single().Id == "B");
                    Check(true, "Empty native ER draft supports new entity creation and exact private Undo/Redo"); Click("ErCancel"); Discard(); WaitClosed();
                }
                else if (fixture == "identical-attributes")
                {
                    SelectAttribute("A", 1); Click("ErMoveUp"); Await(text => text == raw);
                    Wait(() => Find(visual!, "ErSelectionSummary").Name == "A · attribute 1", "Moving identical rows did not retain the selected occurrence.");
                    Check(Find(visual!, "ErUndo").IsEnabled, "Byte-identical row movement has one private history step without changing portable source");
                    Find(visual!, "ErAttributeComment").AsTextBox().Text = "Moved second occurrence"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("A").Attributes[0].Comment == "Moved second occurrence");
                    Check(new ErDiagramSourceParser().Parse(Draft()).Entity("A").Attributes[1].Comment is null, "The next native edit follows the moved identical occurrence"); var edited = Draft();
                    Click("ErUndo"); Await(text => text == raw); Click("ErUndo"); Await(text => text == raw);
                    Wait(() => Find(visual!, "ErSelectionSummary").Name == "A · attribute 2", "Private Undo did not restore identical row identities.");
                    Click("ErRedo"); Await(text => text == raw); Click("ErRedo"); Await(text => text == edited); Capture("identical-attribute-identities");
                    Click("ErCancel"); Discard(); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")) == raw, "Private identical-row movement and editing leave the owner unchanged on Cancel");
                }
                else if (fixture == "unknown-protected")
                {
                    SelectEntity("A"); Check(!Find(visual!, "ErNodeId").IsEnabled && !Find(visual!, "ErDelete").IsEnabled, "Unknown source disables ID and structural controls with explanations"); SelectAttribute("A", 0);
                    Check(!Find(visual!, "ErAttributeName").IsEnabled && !Find(visual!, "ErUpdate").IsEnabled && !Find(visual!, "ErUndo").IsEnabled, "Unknown attribute suffix cannot be overwritten or create history"); Capture("unknown-protected");
                    SelectEntity("B"); Find(visual!, "ErLabel").AsTextBox().Text = "Independent label"; Click("ErUpdate"); Await(text => text.Contains("Independent label", StringComparison.Ordinal)); Check(Draft().Contains("PK, FK", StringComparison.Ordinal), "Independent ER label edit preserves unknown comma tokens byte-exact"); Click("ErUndo"); Await(text => text == raw);
                }
                else
                {
                    var repeatedRow = visual!.FindAllDescendants().Single(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ErCanvas_", StringComparison.Ordinal) && item.Name == "  A · attribute 2 · string id"); repeatedRow.Click();
                    Wait(() => Find(visual!, "ErSelectionSummary").Name == "A · attribute 2", "The repeated attribute canvas hit selected the wrong occurrence.");
                    Find(visual!, "ErAttributeComment").AsTextBox().Text = "Second exact row edited"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("A").Attributes[1].Comment == "Second exact row edited");
                    Check(new ErDiagramSourceParser().Parse(Draft()).Entity("A").Attributes[0].Comment == "First occurrence", "Repeated attribute canvas hits retain exact private row identities"); Click("ErUndo"); Await(text => text == raw);
                    visual!.FindAllDescendants().Single(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ErCanvas_", StringComparison.Ordinal) && item.Name == "Relationship 5 · A → A · self").Patterns.SelectionItem.Pattern.Select();
                    Find(visual!, "ErLabel").AsTextBox().Text = "Second self occurrence"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships[4].Label == "Second self occurrence");
                    Check(new ErDiagramSourceParser().Parse(Draft()).Relationships[1].Label == "self", "Rendered self-loop reordering cannot redirect edits to another occurrence"); Capture("occurrences"); Click("ErCancel"); Discard(); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")) == raw && File.ReadAllText(rawPath) == raw, "Cancel preserves exact standalone owner and disk bytes");
                }
                if (visual is not null) { Click("ErCancel"); WaitClosed(); } Stop();
            }
            foreach (var scenario in new[] { "outside-change", "changed-owner", "closed-owner", "other-tab" })
            {
                var body = "erDiagram\r\n A[\"Draft\"] {\r\n string id PK\r\n }\r\n A ||--o{ A : Check\r\n";
                var raw = scenario == "outside-change" ? "# Outside changes\r\n\r\n```mermaid\r\n" + body + "```\r\n" : body;
                var scenarioPath = Path.Combine(root, scenario + (scenario == "outside-change" ? ".md" : ".mmd")); File.WriteAllText(scenarioPath, raw); Launch(scenarioPath); Open(scenario == "outside-change" ? raw.IndexOf("```mermaid", StringComparison.Ordinal) : 0); SelectEntity("A");
                Find(visual!, "ErLabel").AsTextBox().Text = "Private change"; Click("ErUpdate"); Await(text => text.Contains("Private change", StringComparison.Ordinal)); var privateSource = Draft();
                FocusWindow(main!); var ownerEditor = Find(main!, "MarkdownEditor"); ownerEditor.Focus();
                if (scenario == "outside-change")
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type("%% Outside comment"); Keyboard.Type(VirtualKeyShort.RETURN);
                    Wait(() => ReadEditorText(ownerEditor) == "%% Outside comment\r\n" + raw, "The Er owner outside edit did not arrive."); FocusWindow(visual!); Click("ErApply"); WaitClosed();
                    var targetBody = MermaidBlockSource.Find(new MarkdownService().Parse(raw)).Single().Body;
                    Check(ReadEditorText(ownerEditor) == "%% Outside comment\r\n" + raw[..targetBody.Start] + privateSource + raw[targetBody.End..], "Er Apply relocates the owning Markdown container while preserving surrounding edits");
                }
                else if (scenario == "changed-owner")
                {
                    ownerEditor.Patterns.Value.Pattern.SetValue(raw.Replace("Draft\"", "External\"", StringComparison.Ordinal)); var outside = ReadEditorText(ownerEditor); FocusWindow(visual!); Click("ErApply");
                    Wait(() => Find(visual!, "ErStatus").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Concurrent owner edits were not rejected."); Check(Draft() == privateSource && ReadEditorText(ownerEditor) == outside, "Er owner conflicts retain private and external source exactly"); Capture(scenario);
                }
                else if (scenario == "closed-owner")
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                    main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Single(item => item.Name.Contains("closed-owner.mmd", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select(); ownerEditor.Focus();
                    var count = main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Length; Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
                    Wait(() => main.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Length < count, "The Er owner tab did not close."); FocusWindow(visual!); Click("ErApply");
                    Wait(() => Find(visual!, "ErStatus").Name.Contains("owning document has been closed", StringComparison.Ordinal), "Closed Er owner did not reject Apply."); Check(Draft() == privateSource && File.ReadAllText(scenarioPath) == raw, "Closed-owner rejection preserves private Er source and disk bytes"); Capture(scenario);
                }
                else
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); var otherEditor = Find(main!, "MarkdownEditor"); otherEditor.Patterns.Value.Pattern.SetValue("Another local document");
                    FocusWindow(visual!); Click("ErApply"); WaitClosed(); Check(ReadEditorText(otherEditor) == "Another local document", "Er Apply cannot redirect to the currently active other tab");
                    main!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)).Single(item => item.Name.Contains("other-tab.mmd", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
                    Wait(() => ReadEditorText(Find(main, "MarkdownEditor")) == privateSource, "Er Apply did not retain its owning tab identity."); Check(true, "Er Apply updates the original owner while another tab is active");
                }
                if (visual is not null) { Click("ErCancel"); Discard(); WaitClosed(); } Stop();
            }
            File.WriteAllText(Path.Combine(output, "er-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "er-failure.txt"), error.ToString());
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(item => item.ControlType + ": " + item.AutomationId + " · " + item.Name) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "er-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Checks = checks, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { Mouse.Up(MouseButton.Left); Stop(); }
        void Stop() { if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); app = null; main = null; visual = null; } }
        void Launch(string? fixturePath = null)
        {
            var fixtureState = fixturePath is null ? state : Path.Combine(root, "state-" + Path.GetFileNameWithoutExtension(fixturePath)); Directory.CreateDirectory(fixtureState);
            if (fixturePath is not null) File.WriteAllText(Path.Combine(fixtureState, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(fixtureState); info.ArgumentList.Add(root); info.ArgumentList.Add(fixturePath ?? path);
            var timer = Stopwatch.StartNew(); app = Application.Launch(info); main = visual = null;
            try { main = NativeExpansionLaunch.Prepare(app, automation, timer, fixturePath ?? path, output, ++launchOrdinal, highContrast); }
            finally { automation.TransactionTimeout = automation.ConnectionTimeout = TimeSpan.FromSeconds(3); }
        }
        void WaitVisual()
        {
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("ErVisualEditor")))?.AsWindow()) is not null, "The native ER editor did not open.");
            Wait(() => Find(visual!, "ErApply").IsEnabled, "Canonical ER mapping did not become editable: " + Find(visual!, "ErStatus").Name); Find(visual!, "ErSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("ErFit");
        }
        void Open(int offset)
        { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "The exact ER preview action did not appear."); var timer = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); observations.Add(new { Operation = "Open ER editor including UIA transport", Milliseconds = timer.Elapsed.TotalMilliseconds }); }
        void SelectEntity(string id)
        { Find(visual!, "ErCanvas_entity:" + id).Patterns.SelectionItem.Pattern.Select(); Wait(() => Find(visual!, "ErNodeId").AsTextBox().Text == id, "Entity selection did not reach the inspector."); }
        void SelectRelationship(int index)
        {
            var list = Find(visual!, "ErElements"); var model = new ErDiagramSourceParser().Parse(Draft());
            // WPF virtualises off-screen tree rows. Native keyboard navigation
            // realises the exact source-order occurrence without guessing a label.
            list.Focus(); Keyboard.Type(VirtualKeyShort.HOME);
            for (var step = 0; step < model.Entities.Count + model.Entities.Sum(item => item.Attributes.Count) + index; step++) Keyboard.Type(VirtualKeyShort.DOWN);
            Wait(() => list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).Any(item => item.Name.StartsWith("Relationship " + (index + 1) + " ·", StringComparison.Ordinal) && item.Patterns.SelectionItem.Pattern.IsSelected.Value), "Keyboard selection did not realise the exact transition occurrence.");
            Wait(() => Find(visual!, "ErSelectionSummary").Name.StartsWith("Relationship", StringComparison.Ordinal), "Relationship occurrence selection failed.");
        }
        string Draft() => ReadEditorText(Find(visual!, "ErSource"));
        void Await(Func<string, bool> predicate) => Wait(() => predicate(Draft()) && Find(visual!, "ErApply").IsEnabled, "The private ER draft did not reach the expected state: " + Find(visual!, "ErStatus").Name);
        void Click(string id) => Find(visual!, id).AsButton().Invoke();
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("ErVisualEditor"))) is null, "The ER editor did not close."); visual = null; }
        void ResizeForCapture(int width, int height)
        {
            visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height);
            Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && Math.Abs(visual.BoundingRectangle.Height - height) < 3 && FooterSettled(), "Requested ER window and client layout did not settle.");
            var window = visual.BoundingRectangle; var footer = Find(visual, "ErApply").BoundingRectangle;
            observations.Add(new { Operation = "Settled ER client layout", Width = width, Height = height, FooterRightInset = window.Right - footer.Right, FooterBottomInset = window.Bottom - footer.Bottom });
        }
        bool FooterSettled()
        {
            var window = visual!.BoundingRectangle; var footer = Find(visual, "ErApply").BoundingRectangle;
            return window.Right - footer.Right is >= 4 and <= 32 && window.Bottom - footer.Bottom is >= 4 and <= 24;
        }
        void Capture(string name)
        {
            // Window chrome can resize before WPF arranges its client. Observe actual
            // layout and the input queue before flushing the compositor, without sleeps.
            Wait(FooterSettled, "ER client layout is incomplete before capture.");
            if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("The native ER application did not become idle before capture.");
            _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png"));
        }
        void Discard()
        { AutomationElement? yes = null; Wait(() => (yes = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))) is not null, "Private ER discard confirmation did not appear."); yes!.AsButton().Invoke(); }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void SelectAttribute(string entityId, int row)
        {
            var list = Find(visual!, "ErElements"); var parsed = new ErDiagramSourceParser().Parse(Draft());
            var index = 0; foreach (var entity in parsed.Entities) { if (entity.Id == entityId) { index += row + 1; break; } index += 1 + entity.Attributes.Count; }
            list.Focus(); Keyboard.Type(VirtualKeyShort.HOME); for (var i = 0; i < index; i++) Keyboard.Type(VirtualKeyShort.DOWN);
            Wait(() => Find(visual!, "ErSelectionSummary").Name == entityId + " · attribute " + (row + 1), "Exact attribute row selection failed.");
        }
    }).GetAwaiter().GetResult();
}
