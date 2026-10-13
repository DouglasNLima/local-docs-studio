using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckWordImport(Window owner, AutomationElement editor, UIA3Automation automation, int process,
        string runRoot, string output, string originalDocument, Action<string, string> open)
    {
        var fixture = NativeWordImportFixtureChecks.Run(runRoot);
        var originalText = ReadEditorText(editor); var originalBytes = File.ReadAllBytes(originalDocument);
        var parent = Path.Combine(runRoot, "word-imports"); Directory.CreateDirectory(parent);

        // Cancelling the destination picker must not start conversion or change the workspace.
        OpenImport(); var cancelledPicker = FolderPicker();
        (cancelledPicker.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel")))
            ?? throw new IOException("The import destination Cancel command is unavailable.")).AsButton().Invoke();
        Wait(() => CurrentFolderPicker() is null, "Cancelling the Word import destination did not close its dialogue.");
        if (Directory.EnumerateFileSystemEntries(parent).Any() || ReadEditorText(editor) != originalText
            || !File.ReadAllBytes(originalDocument).SequenceEqual(originalBytes))
            throw new IOException("Cancelling Word import changed the previous document or created output.");

        OpenImport(); var picker = FolderPicker();
        FocusWindow(picker);
        var folder = (picker.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName("Folder:")))
            ?? throw new IOException("The Word import destination field is unavailable.")).AsTextBox();
        folder.Focus(); Wait(() => folder.Properties.HasKeyboardFocus.Value, "The import destination did not acquire keyboard focus.");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(parent);
        Wait(() => folder.Text == parent, "The import destination did not accept the chosen folder.");
        picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Select Folder")))
            .Single(element => element.AutomationId != "DropDown").AsButton().Invoke();
        Wait(() => CurrentFolderPicker() is null, "The Word import destination dialogue did not close.");
        Wait(() => Find(owner, "Status").Name.StartsWith("Imported into a new portable workspace", StringComparison.Ordinal),
            "Word import did not complete through the native application.");
        var root = Directory.GetDirectories(parent, "native-word-import-fixture-import-*").Single();
        var document = Path.Combine(root, "document.md"); var markdown = File.ReadAllText(document);
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == markdown, "The imported saved Markdown did not open in the editor.");
        Wait(() => Find(owner, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal),
            "The imported Word document did not render.");
        Wait(() => owner.FindFirstDescendant(cf => cf.ByControlType(ControlType.Image).And(cf.ByName("Portable pixel"))) is { } image
            && image.BoundingRectangle.Width > 0 && image.BoundingRectangle.Height > 0,
            "The imported Word image is missing from the actual native preview.");
        NativeWordImportFixtureChecks.CheckMarkdown(markdown);
        var status = Find(owner, "Status");
        var message = status.Name;
        var help = status.Properties.HelpText.Value;
        if (NativeWordImportFixtureChecks.ExpectedWarnings.Any(warning => !message.Contains(warning, StringComparison.Ordinal)
            || !help.Contains(warning, StringComparison.Ordinal)))
            throw new IOException("The native import status or accessible tooltip omitted a conversion warning.");
        if (Directory.EnumerateDirectories(parent, ".lens-import-*").Any()
            || !File.ReadAllBytes(fixture.Path).SequenceEqual(fixture.Bytes)
            || !File.ReadAllBytes(originalDocument).SequenceEqual(originalBytes))
            throw new IOException("Word import retained staging data or changed an original document.");
        var imageReference = MarkdownLinks.Read(markdown).Single(link => link.IsImage).Reference;
        var imagePath = new WorkspacePath(root).ResolveLink(document, imageReference);
        if (!File.ReadAllBytes(imagePath).SequenceEqual(fixture.Image))
            throw new IOException("Word import did not persist the original embedded image.");

        var copy = Path.Combine(runRoot, "copied-word-workspace"); Directory.CreateDirectory(copy);
        foreach (var source in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            WorkspacePath.RejectReparsePoints(source);
            var destination = Path.Combine(copy, Path.GetRelativePath(root, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, false);
        }
        var copiedDocument = Path.Combine(copy, "document.md");
        if (File.ReadAllText(copiedDocument) != markdown
            || !File.ReadAllBytes(new WorkspacePath(copy).ResolveLink(copiedDocument, imageReference)).SequenceEqual(fixture.Image))
            throw new IOException("The imported image or source stopped resolving after copying the workspace.");
        owner.CaptureToFile(Path.Combine(output, "native-word-import.png"));
        File.WriteAllText(Path.Combine(output, "native-ui-word-import.json"), JsonSerializer.Serialize(new
        {
            Passed = true, CancelPreservedSource = true, OriginalDocxUnchanged = true, PreviousDocumentUnchanged = true,
            MarkdownMatchesSavedFile = true, Warnings = NativeWordImportFixtureChecks.ExpectedWarnings,
            Status = message, AccessibleTooltip = help, ExactImage = true, NativePreviewImage = true, CopiedWorkspacePortable = true,
            OriginalSha256 = Convert.ToHexString(SHA256.HashData(fixture.Bytes)), ImageSha256 = Convert.ToHexString(SHA256.HashData(fixture.Image))
        }, new JsonSerializerOptions { WriteIndented = true }));

        void OpenImport()
        {
            Find(owner, "FileMenu").Click(); Find(owner, "ImportDocument").Patterns.Invoke.Pattern.Invoke();
            open(fixture.Path, "Import document");
        }
        Window? CurrentFolderPicker() => automation.GetDesktop()
            .FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Open documentation workspace")))?.AsWindow();
        Window FolderPicker()
        {
            Window? found = null;
            Wait(() => (found = CurrentFolderPicker()) is not null, "The Word import destination dialogue did not open.");
            return found!;
        }
    }
}
