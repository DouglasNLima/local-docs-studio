using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckFolderMove(Window owner, AutomationElement editor, UIA3Automation automation, int process, string output, string originalSource)
    {
        var root = Path.GetDirectoryName(originalSource)!; var before = ReadEditorText(editor); var originalSaved = File.ReadAllText(originalSource);
        var folder = Path.Combine(root, "folder-move"); var destination = Path.Combine(root, "renamed-folder");
        Directory.CreateDirectory(Path.Combine(folder, "empty"));
        var guide = Path.Combine(folder, "folder-guide.md"); var home = Path.Combine(root, "folder-move-home.md");
        const string guideText = "# Folder guide\n\n[Outside](../fixture.md)\n\n[Asset](asset.bin)";
        const string homeText = "# Folder home\n\n[Guide](folder-move/folder-guide.md) [[folder-move/folder-guide|Wiki guide]]";
        File.WriteAllText(guide, guideText); File.WriteAllText(home, homeText); byte[] asset = [0, 128, 255, 1]; File.WriteAllBytes(Path.Combine(folder, "asset.bin"), asset);
        OpenTreeFile("folder-move-home.md", homeText); OpenTreeFile("folder-guide.md", guideText);
        FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type("\nUnsaved folder edit.");
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == guideText + "\nUnsaved folder edit.", "The folder fixture did not retain its unsaved edit.");
        var selectedFolder = TreeItem("folder-move"); selectedFolder.Select(); selectedFolder.Focus(); Keyboard.Type(VirtualKeyShort.F2);
        Window? pathWindow = null;
        Wait(() => (pathWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Rename or move folder")))?.AsWindow()) is not null,
            "F2 on a workspace folder did not open the native folder destination dialogue.");
        Find(pathWindow!, "WorkspacePathInput").AsTextBox().Text = "renamed-folder"; Find(pathWindow!, "ConfirmWorkspacePath").Patterns.Invoke.Pattern.Invoke();
        Window? save = null;
        Wait(() => (save = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Save before folder move")))?.AsWindow()) is not null,
            "Folder move did not request preservation of its unsaved source.");
        (save!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("OK")))
            ?? throw new IOException("The folder-save confirmation is unavailable.")).Patterns.Invoke.Pattern.Invoke();
        Window? review = null;
        Wait(() => (review = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Review folder move")))?.AsWindow()) is not null,
            "The native folder move review did not open.");
        if (!Find(review!, "FolderMoveSummary").Name.Contains("Files: 2", StringComparison.Ordinal)
            || Find(review!, "FolderMoveDocuments").AsListBox().Items.Length != 2) throw new IOException("Folder review lost actual asset counts or affected documents.");
        review!.CaptureToFile(Path.Combine(output, "native-folder-move-review.png"));
        Find(review, "ApplyFolderMove").Patterns.Invoke.Pattern.Invoke();
        Wait(() => Find(owner, "Status").Name.StartsWith("Moved folder renamed-folder", StringComparison.Ordinal)
            && !Directory.Exists(folder) && Directory.Exists(destination), "The reviewed folder did not move as a complete native operation.");
        if (File.ReadAllText(Path.Combine(destination, "folder-guide.md")) != guideText + "\nUnsaved folder edit."
            || !File.ReadAllBytes(Path.Combine(destination, "asset.bin")).SequenceEqual(asset) || !Directory.Exists(Path.Combine(destination, "empty")))
            throw new IOException("The folder move lost saved edits, binary assets or empty folders.");
        Find(owner, "NavigateBack").Click();
        var expectedHome = homeText.Replace("folder-move/folder-guide", "renamed-folder/folder-guide", StringComparison.Ordinal)
            .Replace("[[renamed-folder/folder-guide|", "[[renamed-folder/folder-guide.md|", StringComparison.Ordinal);
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == expectedHome && File.ReadAllText(home) == expectedHome, "Folder move did not update incoming ordinary/wiki links and the existing open tab.");
        Find(owner, "NavigateForward").Click();
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == guideText + "\nUnsaved folder edit.", "Forward history lost the relocated folder document.");
        OpenTreeFile(Path.GetFileName(originalSource), before);
        if (File.ReadAllText(originalSource) != originalSaved) throw new IOException("Folder movement changed an unrelated source document.");
        TreeItem TreeItem(string name) => Find(owner, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem).And(cf.ByName(name)))?.AsTreeItem()
            ?? throw new IOException("The expected native tree entry is unavailable: " + name);
        void OpenTreeFile(string name, string expected)
        {
            TreeItem? item = null;
            Wait(() => (item = Find(owner, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem).And(cf.ByName(name)))?.AsTreeItem()) is not null,
                "Folder monitoring did not reveal the file: " + name);
            FocusWindow(owner); item!.Select(); item.Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == expected, "The native tree did not open the expected document: " + name);
        }
    }
}
