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
    private static void CheckWorkspaceModernisation(Window window, AutomationElement editor, UIA3Automation automation, int process,
        string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var root = Path.GetDirectoryName(source)!;
        var second = Path.Combine(root, "Tab B.md"); var third = Path.Combine(root, "Tab C.md");
        File.WriteAllText(second, "# Tab B\n"); File.WriteAllText(third, "# Tab C\n");
        TabAction(Path.GetFileName(source), "Pin");
        Wait(() => JsonSerializer.Deserialize<SessionState>(File.ReadAllText(Path.Combine(state, "session.json")))!.Documents.Any(document => document.Path == source && document.IsPinned),
            "The actual pinned tab did not persist.");
        FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        if (ReadEditorText(editor) != original) throw new IOException("Normal close removed the pinned active document.");
        Open("Tab B", "# Tab B"); Open("Tab C", "# Tab C");
        FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
        Wait(() => ReadEditorText(editor).StartsWith("# Tab B", StringComparison.Ordinal), "Ctrl+Shift+Tab did not activate the preceding source document.");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.TAB);
        Wait(() => ReadEditorText(editor).StartsWith("# Tab C", StringComparison.Ordinal), "Ctrl+Tab did not restore the next source document.");
        TabAction("Tab B.md", "Close tabs to the right");
        Wait(() => ReadEditorText(editor).StartsWith("# Tab B", StringComparison.Ordinal) && Tab("Tab C.md", false) is null,
            "Close to the right did not activate its surviving document.");
        if (!File.Exists(third)) throw new IOException("Closing a tab deleted its source file.");
        editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type("Dirty tab text.");
        Wait(() => ReadEditorText(editor).Contains("Dirty tab text.", StringComparison.Ordinal), "The dirty tab fixture did not receive its source edit.");
        TabAction(Path.GetFileName(source), "Close others"); var prompt = Dialogue("Unsaved document");
        Button(prompt, "Cancel").AsButton().Invoke();
        Wait(() => Tab("Tab B.md", false) is not null, "Cancelling close others discarded a dirty tab.");
        FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        prompt = Dialogue("Unsaved document"); Button(prompt, "No").AsButton().Invoke();
        Wait(() => ReadEditorText(editor) == original && Tab("Tab B.md", false) is null, "Closing the dirty synthetic tab did not retain its surviving pinned document.");
        TabAction(Path.GetFileName(source), "Unpin");

        var folder = Path.Combine(root, "Recycle candidate"); Directory.CreateDirectory(Path.Combine(folder, "empty"));
        var document = Path.Combine(folder, "Recycle note.md"); var asset = Path.Combine(folder, "asset.bin");
        const string savedNote = "# Recycle note\n"; const string dirtyMarker = "Retained unsaved folder text.";
        byte[] assetBytes = [0, 42, 128, 255]; File.WriteAllText(document, savedNote); File.WriteAllBytes(asset, assetBytes);
        Tree("Recycle candidate").AsTreeItem().Expand(); Tree("Recycle note.md").AsTreeItem().DoubleClick();
        Wait(() => ReadEditorText(editor) == savedNote, "The folder's actual source document did not open.");
        editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type(dirtyMarker);
        Wait(() => ReadEditorText(editor).Contains(dirtyMarker, StringComparison.Ordinal), "The folder's unsaved text did not reach its editor.");
        FolderDelete(); prompt = Dialogue("Delete workspace folder"); Button(prompt, "Cancel").AsButton().Invoke();
        Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("CancelOperation"))?.IsOffscreen != false, "Cancelled folder deletion did not finish its operation.");
        if (!Directory.Exists(folder) || File.ReadAllText(document) != savedNote || !File.ReadAllBytes(asset).SequenceEqual(assetBytes)
            || !ReadEditorText(editor).Contains(dirtyMarker, StringComparison.Ordinal))
            throw new IOException("Cancelling folder deletion lost source, assets or an unsaved draft.");
        FolderDelete(); prompt = Dialogue("Delete workspace folder"); prompt.CaptureToFile(Path.Combine(output, "native-folder-delete-confirmation.png"));
        Button(prompt, "OK").AsButton().Invoke();
        Wait(() => !Directory.Exists(folder) && Find(window, "Status").Name.StartsWith("Folder recycled.", StringComparison.Ordinal), "Confirmed folder deletion did not recycle the actual complete folder.");
        var snapshots = new DocumentSnapshotStore(state);
        var retained = snapshots.ListAsync().GetAwaiter().GetResult().Entries.Select(entry => snapshots.ReadAsync(entry).GetAwaiter().GetResult()).ToArray();
        if (!retained.Any(snapshot => snapshot.Document.Text.Contains(dirtyMarker, StringComparison.Ordinal))
            || !retained.Any(snapshot => snapshot.Document.Text == savedNote))
            throw new IOException("Folder deletion did not retain both saved source and its unsaved open text.");
        if (!File.ReadAllBytes(source).SequenceEqual(saved) || File.ReadAllText(second) != "# Tab B\n" || !File.Exists(third))
            throw new IOException("Workspace or tab actions changed unrelated saved documents.");
        Wait(() => ReadEditorText(editor) == original, "Folder deletion did not restore the surviving active document.");
        File.WriteAllText(Path.Combine(output, "native-workspace-modernisation.json"), JsonSerializer.Serialize(new {
            Passed = true, PinsPersisted = true, KeyboardTabSwitching = true, CloseRightSurvival = true, CloseOthersCancel = true, CloseIsNotDelete = true,
            FolderDeleteCancelPreservesEverything = true, ActualWindowsRecycle = true, SavedAndUnsavedSnapshots = true, UnrelatedBytesPreserved = true }));

        AutomationElement? Tab(string title, bool required = true)
        {
            var result = Find(window, "DocumentTabs").FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                .FirstOrDefault(tab => tab.Name.StartsWith(title, StringComparison.Ordinal));
            if (required && result is null) throw new IOException("The actual document tab is missing: " + title);
            return result;
        }
        void TabAction(string title, string action)
        {
            Tab(title)!.RightClick();
            AutomationElement? command = null;
            Wait(() => (command = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("TabContext" + action.Replace(" ", ""))))) is not null,
                "The native tab context action did not open: " + action);
            command!.AsMenuItem().Invoke();
        }
        void Open(string query, string expected)
        {
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
            var picker = Dialogue("Quick Open"); Find(picker, "CommandSearchQuery").AsTextBox().Text = query + ".md";
            // Fuzzy matching legitimately finds 'B' within 'Tab C'. The exact filename
            // must rank first and be the keyboard-confirmed result.
            Wait(() => Find(picker, "CommandSearchResults").AsListBox().SelectedItem?.Name == query + ".md", "Quick Open did not rank the exact tab filename first.");
            Keyboard.Type(VirtualKeyShort.RETURN); Wait(() => ReadEditorText(editor).StartsWith(expected, StringComparison.Ordinal), "The tab fixture did not open.");
        }
        AutomationElement Tree(string name)
        {
            AutomationElement? result = null;
            Wait(() => (result = Find(window, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem).And(cf.ByName(name)))) is not null,
                "The monitored workspace did not expose its synthetic item: " + name);
            return result!;
        }
        void FolderDelete()
        {
            // A parent TreeItem's UIA bounds include all expanded children. Click its
            // actual header text so the input targets the reviewed folder itself.
            var header = Tree("Recycle candidate").FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName("Recycle candidate")))
                ?? throw new IOException("The actual folder header has no accessible text target.");
            header.RightClick(); AutomationElement? action = null;
            Wait(() => (action = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("WorkspaceContextDelete")))) is not null,
                "The actual folder's delete context action is unavailable.");
            action!.AsMenuItem().Invoke();
            var confirmation = Dialogue("Delete workspace folder");
            if (!confirmation.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Any(text => text.Name.StartsWith("Delete folder Recycle candidate?", StringComparison.Ordinal)))
                throw new IOException("Folder deletion confirmation targets a different item from its actual clicked header.");
        }
        Window Dialogue(string title)
        {
            Window? result = null;
            Wait(() => (result = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByControlType(ControlType.Window)).And(cf.ByName(title)))?.AsWindow()) is not null,
                "The expected native dialogue did not appear: " + title);
            return result!;
        }
        AutomationElement Button(Window owner, string name) => owner.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(name)))
            ?? throw new IOException("The native dialogue button is unavailable: " + name);
    }
}
