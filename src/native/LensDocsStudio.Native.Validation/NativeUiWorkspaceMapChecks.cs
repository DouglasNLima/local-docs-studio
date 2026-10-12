using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckWorkspaceMap(Window owner, AutomationElement editor, UIA3Automation automation, int process, string output, string source, string target)
    {
        var before = ReadEditorText(editor); var saved = File.ReadAllText(source); var targetSaved = File.ReadAllText(target);
        var previous = NativeUiClipboard.GetDataObject(); uint owned = 0;
        const string missing = "[Map missing](missing-map-target.md)";
        var fixture = "\n\n## Native map draft\n\n[[" + Path.GetFileName(target) + "|Map target]] " + missing;
        try
        {
            NativeUiClipboard.SetText(fixture); owned = GetClipboardSequenceNumber();
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before + fixture, "The native map fixture did not reach the current draft.");
            var map = OpenMap(); var query = Find(map, "MapQuery").AsTextBox();
            Find(map, "MapDocuments").Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => query.Properties.HasKeyboardFocus.Value, "Ctrl+F inside the map did not focus its document search.");
            query.Text = Path.GetFileName(source);
            Wait(() => Find(map, "MapDocuments").AsListBox().Items.Length == 1
                && Find(map, "MapOutgoing").AsListBox().Items.Any(item => item.Name.Contains("missing-map-target.md", StringComparison.Ordinal)),
                "The map did not include outgoing links from the unsaved draft.");
            Find(map, "MapCopyReport").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(map, "MapStatus").Name.StartsWith("Workspace map report copied", StringComparison.Ordinal), "The native map did not copy its Markdown report.");
            owned = GetClipboardSequenceNumber();
            var report = NativeUiClipboard.GetText();
            if (!report.StartsWith("# Documentation map", StringComparison.Ordinal) || !report.Contains("missing-map-target.md", StringComparison.Ordinal)
                || !report.Contains("```mermaid", StringComparison.Ordinal)) throw new IOException("The copied workspace report lost its current links or diagram source.");
            map.CaptureToFile(Path.Combine(output, "native-workspace-map.png"));
            query.Text = "empty-map-724915";
            Wait(() => Find(map, "MapDocuments").AsListBox().Items.Length == 0 && !Find(map, "MapOpenDocument").IsEnabled && !Find(map, "MapGoToSource").IsEnabled,
                "Empty map search retained an openable document or stale source link.");
            query.Text = Path.GetFileName(source);
            Wait(() => Find(map, "MapOutgoing").AsListBox().Items.Any(item => item.Name.Contains("missing-map-target.md", StringComparison.Ordinal)), "Map search did not restore its source links.");
            Find(map, "MapOutgoing").AsListBox().Items.Single(item => item.Name.Contains("missing-map-target.md", StringComparison.Ordinal)).Select();
            Find(map, "MapGoToSource").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(owner, "Status").Name == "Selected the original link source.", "Map navigation did not select the original link source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
            Wait(() => NativeUiClipboard.GetText() == missing, "Map source navigation did not retain the exact Markdown selection."); owned = GetClipboardSequenceNumber();
            map = OpenMap(); Find(map, "MapQuery").AsTextBox().Text = Path.GetFileName(target);
            Wait(() => Find(map, "MapDocuments").AsListBox().Items.Length == 1, "The map could not find its existing target document.");
            Find(map, "MapOpenDocument").Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == targetSaved, "The map did not open the selected workspace document.");
            Find(owner, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before + fixture, "Map document navigation lost the unsaved source when going Back.");
            map = OpenMap(); Find(map, "MapQuery").AsTextBox().Text = Path.GetFileName(source);
            Wait(() => Find(map, "MapDocuments").AsListBox().Items.Length == 1, "The map could not select the current document for its diagram.");
            Find(map, "MapSelectedDiagram").Patterns.Invoke.Pattern.Invoke();
            Window? diagram = null;
            Wait(() => (diagram = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("WorkspaceMapDiagram")))?.AsWindow()) is not null,
                "The selected workspace diagram did not open as a native drawing window.");
            Wait(() => diagram!.FindFirstDescendant(cf => cf.ByName("Mermaid diagram drawing"))?.BoundingRectangle.Width > 10,
                "The workspace diagram has no native drawing bounds.");
            diagram!.CaptureToFile(Path.Combine(output, "native-workspace-map-diagram.png"));
            FocusWindow(diagram); Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("WorkspaceMapDiagram"))) is null,
                "Escape did not close the native workspace diagram.");
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before, "Map navigation, copying or diagrams changed the original insertion Undo.");
            if (File.ReadAllText(source) != saved || File.ReadAllText(target) != targetSaved) throw new IOException("Read-only workspace map actions changed saved source.");
        }
        finally { RestoreClipboard(previous, owned); }
        Window OpenMap()
        {
            FocusWindow(owner); Find(owner, "ViewMenu").Click(); Find(owner, "OpenWorkspaceMap").Patterns.Invoke.Pattern.Invoke();
            Window? map = null;
            Wait(() => (map = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Workspace map")))?.AsWindow()) is not null,
                "The native workspace map did not open.");
            return map!;
        }
    }
}
