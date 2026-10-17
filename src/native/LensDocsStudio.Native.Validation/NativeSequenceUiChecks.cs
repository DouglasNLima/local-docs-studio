using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Sequence;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunSequenceEditor(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var path = Path.Combine(root, "Sequence.md"); var state = Path.Combine(root, "state");
        Directory.CreateDirectory(state); File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var source = File.ReadAllText("tests/fixtures/native-sequence/local-document.mmd").ReplaceLineEndings("\r\n");
        var block = "```mermaid\r\n" + source + "```\r\n"; var original = "# Synthetic local workflow\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); var checks = new List<string>(); var observations = new List<object>();
        Application? app = null; Window? main = null; Window? visual = null;
        try
        {
            Launch(); var editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == original, "The Sequence fixture did not load exactly.");
            var offset = original.LastIndexOf("```mermaid", StringComparison.Ordinal); Open(offset);
            Check(Find(visual!, "SequenceApply").IsEnabled, "Correct Sequence family editor opens from the second identical diagram");
            Capture("starter");
            SelectParticipant("Studio"); Check(Find(visual!, "SequenceParticipantId").AsTextBox().Text == "Studio", "Participant ID inspection");
            Check(Find(visual!, "SequenceLabel").AsTextBox().Text == "Lens Docs Studio", "Participant alias inspection"); Capture("selected-participant");
            SelectMessage(0); Capture("selected-message"); var before = Draft();
            Find(visual!, "SequenceLabel").AsTextBox().Text = "Open local documentation";
            Check(!Find(visual!, "SequenceApply").IsEnabled, "Pending properties block Apply"); Click("SequenceUpdate");
            Await(text => text.Contains("Open local documentation", StringComparison.Ordinal));
            Check(Draft() == before.Replace("Open documentation", "Open local documentation", StringComparison.Ordinal), "Exact first-message label patch retains standalone activation and unrelated source");
            Click("SequenceAddMessage"); Capture("message-creation");
            Find(visual!, "SequenceSender").AsComboBox().Select("Author"); Find(visual!, "SequenceRecipient").AsComboBox().Select("Workspace");
            Find(visual!, "SequenceLabel").AsTextBox().Text = "Choose another local file";
            Find(visual!, "SequenceInsertion").AsComboBox().Select("At end"); Click("SequenceUpdate"); Await(text => new SequenceSourceParser().Parse(text).Messages.Count == 5);
            Check(ReadEditorText(editor) == original, "Participant/message authoring retains the immutable owning Markdown until Apply");
            SelectNote(); Find(visual!, "SequenceLabel").AsTextBox().Text = "All documentation remains local"; Click("SequenceUpdate"); Await(text => text.Contains("All documentation remains local", StringComparison.Ordinal)); Capture("selected-note");
            SelectMessage(3); Capture("activation-inspector"); Find(visual!, "SequenceMarker").AsComboBox().Select("None"); Click("SequenceUpdateActivation"); Await(text => !text.Contains("deactivate Studio", StringComparison.Ordinal));
            var changed = Draft(); Click("SequenceUndo"); Await(text => text.Contains("deactivate Studio", StringComparison.Ordinal)); Click("SequenceRedo"); Await(text => text == changed);
            Check(!changed.Contains("->>+", StringComparison.Ordinal) && changed.Contains("activate Studio", StringComparison.Ordinal), "Activation edits and private Undo/Redo retain standalone syntax");
            Click("SequenceValidate"); Wait(() => Find(visual!, "SequenceApply").IsEnabled, "Final Sequence validation did not complete.");
            foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
            {
                visual!.Patterns.Transform.Pattern.Resize(width, height); Click("SequenceFit"); Capture("viewport-" + width + "x" + height);
                var outer = visual.BoundingRectangle; var apply = Find(visual, "SequenceApply").BoundingRectangle;
                Check(outer.Contains(apply) && apply.Width > 0 && apply.Height > 0, "Apply remains visible at " + width + "x" + height);
            }
            visual!.Patterns.Transform.Pattern.Resize(1280, 800);
            Find(visual, "SequenceSourcePanel").Patterns.ExpandCollapse.Pattern.Collapse(); Capture("source-collapsed");
            Find(visual, "SequenceSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Capture("source-expanded");
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("SequenceApply"); WaitClosed(); Wait(() => ReadEditorText(editor) == expected, "Apply did not change only the second identical Sequence diagram.");
            Check(File.ReadAllText(path) == original, "Apply preserves ordinary unsaved-file behaviour");
            FocusWindow(main!); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == original, "One document Undo did not restore the original Sequence source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "Document Redo did not restore the Sequence source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Save did not persist the Sequence source.");
            Check(true, "Apply is one document Undo/Redo step and ordinary Save persists it");
            app!.Close(); app.Dispose(); app = null; Launch(); editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == expected, "Save/close/reopen lost Sequence or unrelated document content.");
            Open(offset); Check(Draft() == changed, "Save/close/reopen retains exact portable source and canonical editor ownership"); Capture("reopened");
            // Create pending properties, then explicitly discard them before Cancel.
            SelectMessage(0); Find(visual!, "SequenceLabel").AsTextBox().Text = "Discard this"; Click("SequenceResetProperties"); Click("SequenceCancel"); WaitClosed();
            Check(ReadEditorText(editor) == expected && ReadObservedText(path) == expected, "Cancel leaves owning document and disk unchanged");
            if (!highContrast)
            {
                FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "DarkTheme").AsMenuItem().Invoke();
                Open(offset); Capture("dark"); Click("SequenceCancel"); WaitClosed();
            }
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
            Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "InsertSequence").AsMenuItem().Invoke(); WaitVisual();
            Check(Draft().StartsWith("sequenceDiagram", StringComparison.Ordinal), "Insert Sequence visually creates the portable starter"); Capture("new-sequence");
            Click("SequenceApply"); WaitClosed(); Check(ReadEditorText(Find(main!, "MarkdownEditor")).Contains("```mermaid", StringComparison.Ordinal), "New Sequence Apply inserts a Markdown fence");
            app!.Kill(); app.Dispose(); app = null;
            File.WriteAllText(Path.Combine(output, "sequence-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "sequence-failure.txt"), error.ToString());
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(item => item.ControlType + ": " + item.AutomationId + " · " + item.Name) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "sequence-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Checks = checks, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); } }
        void Launch()
        {
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state); info.ArgumentList.Add(root); info.ArgumentList.Add(path);
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native Sequence fixture did not open."); main.Patterns.Transform.Pattern.Resize(1280, 840);
        }
        void WaitVisual()
        {
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("SequenceVisualEditor")))?.AsWindow()) is not null, "The native Sequence editor did not open.");
            Wait(() => Find(visual!, "SequenceApply").IsEnabled, "The canonical Sequence editor did not become editable: " + Find(visual!, "SequenceStatus").Name);
            Find(visual!, "SequenceSourcePanel").Patterns.ExpandCollapse.Pattern.Expand();
        }
        void Open(int offset)
        {
            FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "The exact Sequence preview action did not appear.");
            var timer = Stopwatch.StartNew(); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); observations.Add(new { Operation = "Open native Sequence editor including UIA", Milliseconds = timer.Elapsed.TotalMilliseconds });
        }
        void SelectParticipant(string id) { Find(visual!, "SequenceCanvas_participant:" + id).Patterns.Toggle.Pattern.Toggle(); Wait(() => Find(visual!, "SequenceParticipantId").AsTextBox().Text == id, "Canonical participant selection did not reach the inspector."); }
        void SelectMessage(int index)
        {
            var rows = Find(visual!, "SequenceInteractions").FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
            rows.Single(item => item.Name.StartsWith("Message " + (index + 1) + " ·", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            Wait(() => Find(visual!, "SequenceSelectionSummary").Name.StartsWith("Message", StringComparison.Ordinal), "Ordered message selection did not reach the inspector.");
        }
        void SelectNote() { Find(visual!, "SequenceInteractions").FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).Single(item => item.Name.StartsWith("Note ·", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select(); }
        string Draft() => ReadEditorText(Find(visual!, "SequenceSource"));
        void Await(Func<string, bool> predicate) => Wait(() => predicate(Draft()) && Find(visual!, "SequenceApply").IsEnabled, "The private Sequence draft did not reach the expected state: " + Find(visual!, "SequenceStatus").Name);
        void Click(string id) => Find(visual!, id).AsButton().Invoke();
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId("SequenceVisualEditor"))) is null, "The Sequence editor did not close."); visual = null; }
        void Capture(string name) { _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
    }).GetAwaiter().GetResult();
}
