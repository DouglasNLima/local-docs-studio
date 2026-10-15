using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckRefinement(Window window, AutomationElement editor, UIA3Automation automation, int process,
        string state, string output, string source, Action<string, string> save)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source); var root = Path.GetDirectoryName(source)!;
        var originalBounds = window.BoundingRectangle; var observations = new List<object>();
        var displayOutput = Path.Combine(output, "refinement-display"); Directory.CreateDirectory(displayOutput);
        using var display = new NativeTestDisplay(displayOutput);
        window.Patterns.Transform.Pattern.Move(0, 0);
        var markdown = Path.Combine(root, "Refinement Markdown.MD"); var diagram = Path.Combine(root, "Refinement diagram.mmd");
        var plain = Path.Combine(root, "Refinement notes.txt");
        File.WriteAllText(markdown, "# Markdown identity\n"); File.WriteAllText(diagram, "flowchart LR\nA --> B\n"); File.WriteAllText(plain, "Plain text identity\n");
        Tree(Path.GetFileName(markdown), "Markdown document"); Tree(Path.GetFileName(diagram), "Mermaid diagram"); Tree(Path.GetFileName(plain), "Plain-text document");
        var renamed = Path.ChangeExtension(diagram, ".MERMAID"); File.Move(diagram, renamed); Tree(Path.GetFileName(renamed), "Mermaid diagram");
        editor.Focus(); Wait(() => editor.Properties.HasKeyboardFocus.Value, "The editor did not receive focus before adaptive resizing.");
        var frameWidth = window.BoundingRectangle.Width - Find(window, "AdaptiveApplicationHeader").BoundingRectangle.Width;
        var lower = 860; var upper = 1280;
        if (CheckWidth(lower) || !CheckWidth(upper)) throw new IOException("The actual adaptive header did not provide both required row modes.");
        while (upper - lower > 1)
        {
            var middle = (lower + upper) / 2;
            if (CheckWidth(middle)) upper = middle; else lower = middle;
        }
        foreach (var width in new[] { lower, upper, upper + 1, lower, upper, 1280, 860, 1280 })
            if (CheckWidth(width, true) != (width >= upper)) throw new IOException("The native header did not retain its measured one-pixel transition in both directions.");
        window.CaptureToFile(Path.Combine(output, "native-refinement-file-icons.png"));
        var preferences = ReadDesktopState<UserPreferences>(state, "preferences.json");
        var settings = OpenSettingsWindow(window, automation, process);
        Find(settings, "SettingsSearch").AsTextBox().Text = "reading";
        Wait(() => !Find(settings, "SettingsReadingMode").IsOffscreen && settings.FindFirstDescendant(cf => cf.ByAutomationId("SettingsFont"))?.IsOffscreen != false,
            "Settings search did not isolate its existing reading-mode control.");
        Find(settings, "SettingsReadingMode").AsComboBox().Select("Pages");
        Find(settings, "SettingsSearch").AsTextBox().Text = "zoom";
        Find(settings, "SettingsZoom").Patterns.RangeValue.Pattern.SetValue(135);
        Find(settings, "SettingsSearch").AsTextBox().Text = "reading";
        if (Find(settings, "SettingsReadingMode").AsComboBox().SelectedItem?.Text != "Pages") throw new IOException("Filtering lost an unsaved settings choice.");
        settings.CaptureToFile(Path.Combine(output, "native-refinement-settings-search.png"));
        Find(settings, "SettingsCancel").AsButton().Invoke();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null, "Settings Cancel did not close the filtered dialogue.");
        if (WorkspaceSettings.From(ReadDesktopState<UserPreferences>(state, "preferences.json")) != WorkspaceSettings.From(preferences)) throw new IOException("Cancelling filtered settings saved staged edits.");
        var draft = original + "Refinement draft retained.";
        var previousClipboard = NativeUiClipboard.GetDataObject(); uint clipboardSequence = 0;
        try
        {
            NativeUiClipboard.SetText("Refinement draft retained."); clipboardSequence = GetClipboardSequenceNumber();
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            // A literal paste is one known Undo unit; simulated character typing intentionally has several units.
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == draft, "The workspace-close draft was not entered exactly.");
        }
        finally { RestoreClipboard(previousClipboard, clipboardSequence); }
        File.WriteAllText(Path.Combine(root, "Pending refinement.md"), "# Pending filesystem event\n");
        Find(window, "CloseWorkspace").AsButton().Invoke();
        Wait(() => ReadDesktopState<SessionState>(state, "session.json").Workspace is null
            && Find(window, "WorkspaceTree").FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Length == 0,
            "Workspace closure did not clear and persist the visible workspace.");
        var closed = ReadDesktopState<SessionState>(state, "session.json");
        if (ReadEditorText(editor) != draft || !closed.Documents.Any(document => document.Path == source && document.Text == draft && document.WorkspaceRoot == root))
            throw new IOException("Workspace closure lost an open draft or its retained document context.");
        window.CaptureToFile(Path.Combine(output, "native-refinement-workspace-closed.png"));
        OpenFolder(root); Tree(Path.GetFileName(markdown), "Markdown document");
        editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        Wait(() => ReadEditorText(editor) == original, "Workspace close/reopen changed the retained draft's Undo history.");
        Find(window, "ExportMenu").Click(); Find(window, "ExportHtml").AsMenuItem().Invoke();
        var exported = Path.Combine(root, "refinement-export.html"); save(exported, "Export HTML");
        Wait(() => File.Exists(exported) && ExportCompleted(window, exported), "Export feedback did not identify its actual successfully saved destination.");
        window.CaptureToFile(Path.Combine(output, "native-refinement-export-completed.png"));
        Find(window, "ExportMenu").Click(); Find(window, "ExportHtml").AsMenuItem().Invoke();
        Window? cancelledSave = null;
        Wait(() => (cancelledSave = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Export HTML")))?.AsWindow()) is not null,
            "The cancellation fixture did not open its native Save dialogue.");
        cancelledSave!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel")))!.AsButton().Invoke();
        Wait(() => Find(window, "Status").Name == "HTML export cancelled."
            && window.FindFirstDescendant(cf => cf.ByAutomationId("OpenExportedFile"))?.IsOffscreen != false,
            "A cancelled native Save dialogue retained an earlier success action.");
        Find(window, "ExportMenu").Click(); Find(window, "ExportHtml").AsMenuItem().Invoke();
        exported = Path.Combine(root, "refinement-export-2.html"); save(exported, "Export HTML");
        Wait(() => File.Exists(exported) && ExportCompleted(window, exported), "The second sequential export did not replace its completion destination.");
        Find(window, "ExportMenu").Click(); Find(window, "RecentExportsMenu").AsMenuItem().Expand();
        Wait(() =>
        {
            var recent = Find(window, "RecentExportsMenu").FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem));
            return recent.Any(item => item.Name.StartsWith("refinement-export.html", StringComparison.Ordinal))
                && recent.Any(item => item.Name.StartsWith("refinement-export-2.html", StringComparison.Ordinal));
        },
            "The completed output is absent from session-scoped recent exports.");
        window.CaptureToFile(Path.Combine(output, "native-refinement-recent-exports.png")); Keyboard.Type(VirtualKeyShort.ESCAPE); Keyboard.Type(VirtualKeyShort.ESCAPE);
        Find(window, "RevealExportedFile").AsButton().Invoke();
        Window? explorer = null;
        var shellObservations = new List<object>();
        try { Wait(() =>
        {
            shellObservations.Clear();
            foreach (var candidate in automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .Where(candidate => candidate.Properties.ProcessId.ValueOrDefault != process))
            {
                var items = candidate.FindAllDescendants(cf => cf.ByName("refinement-export-2.html").Or(cf.ByName("refinement-export-2")));
                foreach (var item in items)
                {
                    var selected = item.Patterns.SelectionItem.IsSupported && item.Patterns.SelectionItem.Pattern.IsSelected.Value;
                    shellObservations.Add(new { Window = candidate.Name, Item = item.Name, Type = item.ControlType.ToString(), Selected = selected });
                    if (selected) { explorer = candidate.AsWindow(); return true; }
                }
            }
            return explorer is not null;
        }, "Show in folder did not select the actual saved output in Windows Explorer."); }
        finally { File.WriteAllText(Path.Combine(output, "native-refinement-shell-selection.json"), JsonSerializer.Serialize(new { Status = Find(window, "Status").Name, Observations = shellObservations })); }
        explorer!.CaptureToFile(Path.Combine(output, "native-refinement-export-revealed.png")); explorer.Close(); FocusWindow(window);
        File.Delete(exported); Find(window, "OpenExportedFile").AsButton().Invoke();
        Wait(() => Find(window, "Status").Name.StartsWith("The exported file is unavailable or has changed", StringComparison.Ordinal), "A missing output did not fail gracefully before shell opening.");
        if (window.FindFirstDescendant(cf => cf.ByAutomationId("OpenExportedFile"))?.IsOffscreen == false) throw new IOException("A superseded export kept its success actions visible.");
        if (ReadEditorText(editor) != original || !File.ReadAllBytes(source).SequenceEqual(saved)) throw new IOException("Native refinements changed source or saved bytes.");
        window.Patterns.Transform.Pattern.Resize(originalBounds.Width, originalBounds.Height);
        window.Patterns.Transform.Pattern.Move(originalBounds.Left, originalBounds.Top); editor.Focus(); display.Complete();
        File.WriteAllText(Path.Combine(output, "native-refinement.json"), JsonSerializer.Serialize(new { Passed = true,
            Header = observations, AccessibleFileTypesAndExternalRename = true, SettingsSearchStagesAndCancels = true,
            LastTwoRowWidth = lower, FirstSingleRowWidth = upper,
            WorkspaceClosePreservesDraftRootsAndUndo = true, ExportDestinationAndHistory = true, ActualSavePickerCancellation = true,
            SequentialExportReplacement = true, ActualExplorerSelection = true, MissingExportRejectedBeforeShell = true }, new JsonSerializerOptions { WriteIndented = true }));

        bool CheckWidth(int width, bool keyboard = false)
        {
            window.Patterns.Transform.Pattern.Resize(width, width == 860 ? 540 : 800);
            bool single = false;
            System.Drawing.Rectangle menu = default, toolbar = default;
            Wait(() =>
            {
                var header = Find(window, "AdaptiveApplicationHeader");
                if (Math.Abs(window.BoundingRectangle.Width - width) >= 3
                    || Math.Abs(header.BoundingRectangle.Width - (window.BoundingRectangle.Width - frameWidth)) >= 2) return false;
                var mode = header.Properties.HelpText.ValueOrDefault;
                if (mode is not ("Menu and commands share one row" or "Commands are below the menu")) return false;
                single = mode == "Menu and commands share one row";
                menu = Find(window, "ApplicationMenu").BoundingRectangle; toolbar = Find(window, "ApplicationToolbar").BoundingRectangle;
                return single ? toolbar.Left >= menu.Right && toolbar.Top <= menu.Bottom : toolbar.Top >= menu.Bottom;
            }, "The adaptive header did not finish a non-overlapping layout at the actual requested width: " + width);
            if (!editor.Properties.HasKeyboardFocus.Value || ReadEditorText(editor) != original) throw new IOException("Header reflow lost typing focus or changed source.");
            if (keyboard)
            {
                FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                Wait(() => Find(window, "FileMenu").Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                    "Alt+F lost native menu access after reflow.");
                Keyboard.Type(VirtualKeyShort.ESCAPE); editor.Focus();
            }
            if (width is 860 or 1280) window.CaptureToFile(Path.Combine(output, "native-refinement-header-" + width + ".png"));
            observations.Add(new { Width = width, SingleRow = single, Menu = menu, Toolbar = toolbar, KeyboardMenu = keyboard, SourceAndFocusRetained = true });
            return single;
        }

        AutomationElement Tree(string name, string description)
        {
            AutomationElement? item = null;
            Wait(() => (item = Find(window, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem).And(cf.ByName(name))))?.Properties.HelpText.ValueOrDefault == description,
                "The monitored workspace did not expose its correct accessible file type: " + name);
            return item!;
        }
        void OpenFolder(string path)
        {
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_O);
            Window? picker = null;
            Wait(() => (picker = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Open documentation workspace")))?.AsWindow()) is not null,
                "The workspace reopen dialogue did not appear.");
            FocusWindow(picker!);
            var field = picker!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName("Folder:")))!.AsTextBox();
            field.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(path);
            Wait(() => field.Text == path, "The workspace reopen folder field lost its chosen path.");
            picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Select Folder"))).Single(element => element.AutomationId != "DropDown").AsButton().Invoke();
            Wait(() => ReadDesktopState<SessionState>(state, "session.json").Workspace == path || Find(window, "Status").Name == "Workspace opened", "The selected workspace did not reopen.");
        }
    }
}
