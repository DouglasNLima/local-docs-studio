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
        File.WriteAllText(path, original); var checks = new List<string>(); var observations = new List<object>(); Application? app = null; Window? main = null; Window? visual = null;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active during ER authoring");
            Launch(); var editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "The ER fixture did not load exactly."); Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Capture("starter");
            SelectEntity("CUSTOMER"); var entityButton = Find(visual!, "ErCanvas_entity:CUSTOMER"); Check(entityButton.Patterns.SelectionItem.Pattern.IsSelected.Value, "Native entity SelectionItem exposes exact source identity");
            Find(visual!, "ErLabel").AsTextBox().Text = "Customers locally"; Check(!Find(visual!, "ErApply").IsEnabled, "Pending entity properties protect Apply"); Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Label == "Customers locally"); Capture("entity-inspector");
            SelectAttribute("CUSTOMER", 2); Check(Find(visual!, "ErAttributeName").AsTextBox().Text == "email", "Verified canvas row selects the correct source attribute occurrence");
            Find(visual!, "ErAttributeComment").AsTextBox().Text = "Contact email edited locally"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[2].Comment == "Contact email edited locally");
            Find(visual!, "ErPK").Patterns.Toggle.Pattern.Toggle(); Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[2].Keys.Contains(ErKeyType.PK)); Capture("attribute-inspector");
            Click("ErAddAttribute"); Find(visual!, "ErAttributeName").AsTextBox().Text = "created_on"; Find(visual!, "ErAttributeType").AsTextBox().Text = "datetime"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes.Count == 4);
            Click("ErMoveUp"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[2].Name == "created_on"); Click("ErUndo"); Await(text => new ErDiagramSourceParser().Parse(text).Entity("CUSTOMER").Attributes[3].Name == "created_on");
            SelectRelationship(0); Find(visual!, "ErLabel").AsTextBox().Text = "places locally"; Find(visual!, "ErSecondCardinality").AsComboBox().Select("One or more   |{"); Find(visual!, "ErRelationshipType").AsComboBox().Select("Non-identifying   .."); Click("ErUpdate");
            Await(text => { var relationship = new ErDiagramSourceParser().Parse(text).Relationships[0]; return relationship.Label == "places locally" && relationship.Cardinality2 == ErCardinality.Many && !relationship.Identifying; }); Capture("relationship-inspector");
            Click("ErAddEntity"); Find(visual!, "ErNodeId").AsTextBox().Text = "SHIPMENT"; Find(visual!, "ErLabel").AsTextBox().Text = "Shipment"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Entities.Any(item => item.Id == "SHIPMENT"));
            Click("ErAddRelationship"); Find(visual!, "ErFrom").AsComboBox().Select("ORDER · ORDER"); Find(visual!, "ErTo").AsComboBox().Select("SHIPMENT · Shipment"); Find(visual!, "ErLabel").AsTextBox().Text = "ships"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships.Count == 3);
            var changed = Draft(); Click("ErUndo"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships.Count == 2); Click("ErRedo"); Await(text => text == changed);
            Click("ErReverse"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships[^1].Entity1 == "SHIPMENT"); Click("ErUndo"); Await(text => text == changed);
            Click("ErReconnectTo"); Find(visual!, "ErCanvas_entity:ORDER_LINE").Click(); Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships[^1].Entity2 == "ORDER_LINE"); Click("ErUndo"); Await(text => text == changed);
            Click("ErFit"); var endpoint = Find(visual!, "ErSecondEndpoint").BoundingRectangle; var beforeDrag = Draft(); var allocated = Process.GetProcessById(app!.ProcessId).PrivateMemorySize64; var timer = Stopwatch.StartNew();
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left);
            try { for (var step = 1; step <= 20; step++) Mouse.MoveTo(endpoint.Left + step, endpoint.Top + step); Keyboard.Type(VirtualKeyShort.ESCAPE); } finally { Mouse.Up(MouseButton.Left); }
            Check(Draft() == beforeDrag && !app!.HasExited, "Real endpoint drag cancellation retains exact source/history and releases capture"); observations.Add(new { Operation = "Actual endpoint drag with twenty pointer updates and cancellation", Milliseconds = timer.Elapsed.TotalMilliseconds, PrivateBytesBefore = allocated, PrivateBytesAfter = Process.GetProcessById(app!.ProcessId).PrivateMemorySize64 });
            // Empty-space drop must retain source and history; a valid target is exercised through the inspector above.
            var empty = Find(visual!, "ErViewport").BoundingRectangle; endpoint = Find(visual!, "ErSecondEndpoint").BoundingRectangle;
            Mouse.MoveTo(endpoint.Left + endpoint.Width / 2, endpoint.Top + endpoint.Height / 2); Mouse.Down(MouseButton.Left); Mouse.MoveTo(empty.Right - 15, empty.Bottom - 15); Mouse.Up(MouseButton.Left);
            Wait(() => Find(visual!, "ErStatus").Name.Contains("unchanged", StringComparison.OrdinalIgnoreCase), "Invalid endpoint drop feedback missing."); Check(Draft() == changed, "Invalid endpoint drop leaves validated source unchanged");
            Click("ErValidate"); Wait(() => Find(visual!, "ErApply").IsEnabled, "ER validation did not finish.");
            Check(ReadEditorText(editor) == original && File.ReadAllText(path) == original, "Private ER operations retain owner and disk bytes before Apply");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            { ResizeForCapture(width, height); Click("ErFit"); Capture("viewport-" + width + "x" + height); Check(visual!.BoundingRectangle.Contains(Find(visual, "ErApply").BoundingRectangle), "ER Apply remains visible at " + width + "x" + height); }
            ResizeForCapture(1280, 800); Capture("validated");
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("ErApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "ER Apply did not change only the second identical diagram.");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == original, "ER Apply did not create one document Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "ER document Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "ER Save failed.");
            Check(true, "Real Open, Select entity/attribute/relationship, edit, cardinality, non-identifying, add, private Undo/Redo, Validate, Apply, document Undo/Redo and Save pass");
            Stop(); Launch(); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Restart/reopen lost ER source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Save/restart/reopen retains complete ER source and rendering"); SelectEntity("CUSTOMER"); SelectAttribute("CUSTOMER", 2); Capture("saved-reopened"); Click("ErCancel"); WaitClosed();
            if (!highContrast) foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); SelectEntity("CUSTOMER"); Capture(theme.ToLowerInvariant()); Click("ErCancel"); WaitClosed(); }
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").AsMenuItem().Click(); Find(main!, "InsertEr").AsMenuItem().Click(); WaitVisual(); Check(new ErDiagramSourceParser().Parse(Draft()).IsValid, "Insert ER opens a valid portable starter"); Click("ErApply"); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")).Contains("```mermaid", StringComparison.Ordinal), "Visual ER insertion applies a portable Markdown fence"); Stop();
            foreach (var fixture in new[] { "occurrences", "unknown-protected" })
            {
                var raw = File.ReadAllText("tests/fixtures/native-er/" + fixture + ".mmd"); var rawPath = Path.Combine(root, fixture + (fixture == "unknown-protected" ? ".mermaid" : ".mmd")); File.WriteAllText(rawPath, raw); Launch(rawPath); Open(0);
                if (fixture == "unknown-protected")
                {
                    SelectEntity("A"); Check(!Find(visual!, "ErNodeId").IsEnabled && !Find(visual!, "ErDelete").IsEnabled, "Unknown source disables ID and structural controls with explanations"); SelectAttribute("A", 0);
                    Check(!Find(visual!, "ErAttributeName").IsEnabled && !Find(visual!, "ErUpdate").IsEnabled && !Find(visual!, "ErUndo").IsEnabled, "Unknown attribute suffix cannot be overwritten or create history"); Capture("unknown-protected");
                    SelectEntity("B"); Find(visual!, "ErLabel").AsTextBox().Text = "Independent label"; Click("ErUpdate"); Await(text => text.Contains("Independent label", StringComparison.Ordinal)); Check(Draft().Contains("PK, FK", StringComparison.Ordinal), "Independent ER label edit preserves unknown comma tokens byte-exact"); Click("ErUndo"); Await(text => text == raw);
                }
                else
                {
                    SelectRelationship(4); Find(visual!, "ErLabel").AsTextBox().Text = "Second self occurrence"; Click("ErUpdate"); Await(text => new ErDiagramSourceParser().Parse(text).Relationships[4].Label == "Second self occurrence");
                    Check(new ErDiagramSourceParser().Parse(Draft()).Relationships[1].Label == "self", "Rendered self-loop reordering cannot redirect edits to another occurrence"); Capture("occurrences"); Click("ErCancel"); Discard(); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")) == raw && File.ReadAllText(rawPath) == raw, "Cancel preserves exact standalone owner and disk bytes");
                }
                if (visual is not null) { Click("ErCancel"); WaitClosed(); } Stop();
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
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native ER fixture did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800);
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
        { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "Private ER discard confirmation did not appear."); automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); }
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
