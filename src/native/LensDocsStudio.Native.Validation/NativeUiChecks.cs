using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static class NativeUiChecks
{
    public static int Run(string executable, string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var runRoot = Path.Combine(output, "ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        var source = Path.Combine(runRoot, "fixture.md");
        File.WriteAllText(source, "# Native UI fixture\n\nSearchable text.\n\n```mermaid\nflowchart LR\nA[Write] --> B[Review]\n```\n");
        var linkedDocument = Path.Combine(runRoot, "Second document.md");
        File.WriteAllText(linkedDocument, "# Second document\n\nLinked workspace content.\n\n## Details\n\nMore detail.\n");
        var state = Path.Combine(runRoot, "state");
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var checks = new List<string>();
        using var automation = new UIA3Automation();
        Application? app = null;
        Process? launchedProcess = null;
        try
        {
            app = Launch(source);
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("Native window did not appear.");
            var editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Searchable", StringComparison.Ordinal), "Fixture text did not load.");
            Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "The initial native preview did not finish.");
            checks.Add("Startup file opened through native controls");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            var find = Find(window, "FindText").AsTextBox();
            Wait(() => Find(window, "FindContext").Name == "Find in editor", "Ctrl+F did not choose editor context.");
            find.Text = "Searchable";
            Keyboard.Type(VirtualKeyShort.RETURN);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type("\nNative UI validation — Café");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Typing was captured by an inactive search control.");
            checks.Add("Editor search closes and keyboard input reaches the editor");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => File.ReadAllText(source).Contains("Native UI validation — Café", StringComparison.Ordinal), "Save did not persist Unicode text.");
            checks.Add("Ctrl+S persisted actual UTF-8 source");
            var preview = Find(window, "DocumentPreview");
            preview.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(window, "FindContext").Name == "Find in preview", "Ctrl+F did not choose preview context.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            checks.Add("Ctrl+F targets the focused preview");
            Find(window, "DiagramActionZoom").Click();
            Window? diagramWindow = null;
            Wait(() => (diagramWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Mermaid diagram")))?.AsWindow()) is not null, "The native diagram zoom window did not open.");
            Find(diagramWindow!, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(175);
            Wait(() => Find(diagramWindow!, "DiagramZoomLabel").Name == "175%", "The native zoom did not change scale.");
            diagramWindow!.CaptureToFile(Path.Combine(output, "native-diagram.png"));
            diagramWindow.Close();
            checks.Add("Mermaid opens in a native drawing window and zoom changes its scale");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type(VirtualKeyShort.RETURN);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_K);
            Window? linkWindow = null;
            Wait(() => (linkWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Insert document link")))?.AsWindow()) is not null, "The native document-link picker did not open.");
            Find(linkWindow!, "DocumentLinkFilter").AsTextBox().Text = "Second";
            Find(linkWindow!, "DocumentLinkResults").AsListBox().Select(0);
            Find(linkWindow!, "InsertDocumentLink").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("[Second document](Second%20document.md)", StringComparison.Ordinal), "The inserted document link was not portable Markdown.");
            FocusWindow(window);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => File.ReadAllText(source).Contains("Second%20document.md", StringComparison.Ordinal), "The inserted link was not saved.");
            AutomationElement? documentLink = null;
            // WPF exposes FlowDocument hyperlinks through UIA TextPattern ranges.
            Wait(() => (documentLink = Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetChildren()
                .FirstOrDefault(e => e.Properties.Name.ValueOrDefault == "Second document")) is not null, "The rendered internal link did not appear through the native text pattern.");
            documentLink!.Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Linked workspace content", StringComparison.Ordinal), "The internal link did not open its workspace document.");
            checks.Add("Native document-link picker inserts portable Markdown and the link opens inside the workspace");
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.LEFT);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Back did not restore the previous document.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.RIGHT);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Linked workspace content", StringComparison.Ordinal), "Forward did not restore the linked document.");
            Find(window, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "The native Back button did not work.");
            checks.Add("Alt+Left, Alt+Right and native buttons navigate document history");
            Wait(() =>
            {
                var preferences = JsonSerializer.Deserialize<LensDocsStudio.Core.State.UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
                return preferences.RecentDocuments?.Contains(source) == true && preferences.RecentDocuments.Contains(linkedDocument) && preferences.RecentWorkspaces?.Contains(runRoot) == true;
            }, "Recent documents and workspaces were not persisted.");
            checks.Add("Recent document and workspace paths persist in isolated native preferences");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type("\nUnsaved linked-document changes must survive the move.");
            Find(window, "NavigateForward").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Linked workspace content", StringComparison.Ordinal), "The document selected for renaming did not open.");
            FocusWindow(window); Keyboard.Type(VirtualKeyShort.F2);
            Window? movePathWindow = null;
            Wait(() => (movePathWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Rename or move document")))?.AsWindow()) is not null, "The native rename command did not open.");
            Find(movePathWindow!, "WorkspacePathInput").AsTextBox().Text = "Renamed document.md";
            Find(movePathWindow!, "ConfirmWorkspacePath").AsButton().Invoke();
            Window? reviewMoveWindow = null;
            Wait(() => (reviewMoveWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Review document move")))?.AsWindow()) is not null, "The affected-link review did not open.");
            Find(reviewMoveWindow!, "ApplyDocumentMove").AsButton().Invoke();
            Window? unsavedLinksWindow = null;
            Wait(() => (unsavedLinksWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Unsaved linked documents")))?.AsWindow()) is not null, "The move did not request preservation of the unsaved linked document.");
            (unsavedLinksWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("OK"))) ?? throw new InvalidOperationException("The save-linked-documents confirmation is unavailable.")).AsButton().Invoke();
            Wait(() => (reviewMoveWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Review document move")))?.AsWindow()) is not null, "The move was not reviewed again after saving linked documents.");
            Find(reviewMoveWindow!, "ApplyDocumentMove").AsButton().Invoke();
            var renamedDocument = Path.Combine(runRoot, "Renamed document.md");
            Wait(() => File.Exists(renamedDocument) && !File.Exists(linkedDocument) && File.ReadAllText(source).Contains("Renamed%20document.md", StringComparison.Ordinal), "Renaming did not update the actual file and its incoming Markdown link.");
            if (!File.ReadAllText(source).Contains("Unsaved linked-document changes must survive the move.", StringComparison.Ordinal)) throw new IOException("The linked document lost its unsaved changes during the move.");
            Find(window, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Renamed%20document.md", StringComparison.Ordinal), "The open linked document did not receive the planned link update.");
            checks.Add("Native rename review saves unsaved linked content, moves the file, updates links and refreshes open editor text");
            Find(window, "FileMenu").Click(); Find(window, "CreateWorkspaceFile").Click();
            Window? createWindow = null;
            Wait(() => (createWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Create workspace file")))?.AsWindow()) is not null, "The workspace create-file dialogue did not open.");
            Find(createWindow!, "WorkspacePathInput").AsTextBox().Text = "created-from-ui.md";
            Find(createWindow!, "ConfirmWorkspacePath").AsButton().Invoke();
            var createdPath = Path.Combine(runRoot, "created-from-ui.md");
            Wait(() => File.Exists(createdPath) && editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The native create-file workflow did not create and open the file.");
            FocusWindow(window); editor.Click(); Keyboard.Type("Created through the native workspace.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => File.ReadAllText(createdPath) == "Created through the native workspace.", "The new file did not save its actual edited content.");
            checks.Add("Create workspace file creates a real Markdown file and saves native editor content");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type("\nUnsaved content retained before deletion.");
            Find(window, "FileMenu").Click(); Find(window, "DeleteDocument").Click();
            Window? deleteWindow = null;
            Wait(() => (deleteWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Delete document")))?.AsWindow()) is not null, "The deletion confirmation did not open.");
            (deleteWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("OK"))) ?? throw new InvalidOperationException("The delete confirmation is unavailable.")).AsButton().Invoke();
            Wait(() => !File.Exists(createdPath) && Find(window, "Status").Name.StartsWith("Deleted created-from-ui.md", StringComparison.Ordinal), "The file was not removed through the Windows shell.");
            var snapshotStore = new LensDocsStudio.Core.State.DocumentSnapshotStore(state);
            var retainedSnapshots = snapshotStore.ListAsync().GetAwaiter().GetResult();
            if (retainedSnapshots.Entries.Count != 2) throw new IOException("Deletion did not retain separate saved and unsaved source snapshots.");
            checks.Add("Deleting a document uses the Windows shell and retains saved and unsaved source snapshots");
            Find(window, "FileMenu").Click(); Find(window, "BrowseSnapshots").Click();
            Window? snapshotsWindow = null;
            Wait(() => (snapshotsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Document snapshots")))?.AsWindow()) is not null, "The native snapshot browser did not open.");
            var retainedItem = Find(snapshotsWindow!, "SnapshotList").AsListBox().Items.First(i => i.Name.Contains("unsaved editor text", StringComparison.Ordinal));
            retainedItem.Select();
            Wait(() => Find(snapshotsWindow!, "SnapshotSource").AsTextBox().Text.Contains("Unsaved content retained before deletion.", StringComparison.Ordinal) && Find(snapshotsWindow!, "RestoreSnapshot").IsEnabled, "The retained unsaved source did not appear.");
            Find(snapshotsWindow!, "RestoreSnapshot").AsButton().Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Unsaved content retained before deletion.", StringComparison.Ordinal), "The snapshot was not recovered into a new editor tab.");
            if (File.Exists(createdPath)) throw new IOException("Inspecting and restoring a snapshot unexpectedly recreated a workspace file.");
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            SaveThroughDialogue(createdPath);
            Wait(() => File.Exists(createdPath) && File.ReadAllText(createdPath).Contains("Unsaved content retained before deletion.", StringComparison.Ordinal), "The recovered source did not save to its explicitly chosen destination.");
            checks.Add("Snapshot browser recovers unsaved text into a new draft and Save recreates only the chosen file");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            var sourceTab = window.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).And(cf.ByName("fixture.md"))) ?? throw new InvalidOperationException("The original document tab is unavailable.");
            sourceTab.Patterns.SelectionItem.Pattern.Select();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "The original tab did not activate after file creation.");
            FocusWindow(window);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "A new unsaved document did not open.");
            var previousClipboard = System.Windows.Clipboard.GetDataObject();
            uint ownedClipboardSequence = 0;
            try
            {
                var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(2, 2, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
                    new byte[] { 62, 136, 255, 255, 62, 136, 255, 255, 62, 136, 255, 255, 62, 136, 255, 255 }, 8);
                System.Windows.Clipboard.SetImage(bitmap);
                ownedClipboardSequence = GetClipboardSequenceNumber();
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("![pasted-image](assets/images/", StringComparison.Ordinal), "Clipboard image did not persist into the unsaved document.");
            }
            finally { RestoreClipboard(previousClipboard, ownedClipboardSequence); }
            var imageDocument = Path.Combine(runRoot, "Image document.md");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            SaveThroughDialogue(imageDocument);
            Wait(() => File.Exists(imageDocument), "The document containing the pasted image was not saved.");
            var imageReference = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(File.ReadAllText(imageDocument)).Single(l => l.IsImage).Reference;
            if (!imageReference.StartsWith("assets/images/", StringComparison.Ordinal)) throw new IOException("The saved image still relies on a private draft folder.");
            var imagePath = new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(imageDocument, imageReference);
            if (!File.Exists(imagePath) || new FileInfo(imagePath).Length < 50) throw new IOException("Saving the unsaved document did not persist its portable image.");
            checks.Add("Clipboard image in an unsaved document is finalised into a portable image when saved through the native dialogue");
            FocusWindow(window); Find(window, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Back did not return from the image document.");
            var imageTab = window.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).And(cf.ByName("Image document.md"))) ?? throw new InvalidOperationException("The image document tab is unavailable.");
            imageTab.Patterns.SelectionItem.Pattern.Select();
            previousClipboard = System.Windows.Clipboard.GetDataObject(); ownedClipboardSequence = 0;
            try
            {
                Find(window, "DocumentImage0").RightClick();
                AutomationElement? copyImage = null;
                Wait(() => (copyImage = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("CopyDocumentImage")))) is not null, "The native image context menu did not open.");
                copyImage!.Click();
                Wait(() => Find(window, "Status").Name.StartsWith("Image copied.", StringComparison.Ordinal), "The rendered image was not copied to the Windows clipboard.");
                ownedClipboardSequence = GetClipboardSequenceNumber();
                if (System.Windows.Clipboard.GetImage() is not { PixelWidth: 2, PixelHeight: 2 }) throw new IOException("The clipboard does not contain the rendered image pixels.");
                FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The destination image draft did not open.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("![pasted-image](assets/images/", StringComparison.Ordinal), "The copied document image was not pasted into the new draft.");
                var copiedDocument = Path.Combine(runRoot, "Copied image document.md");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); SaveThroughDialogue(copiedDocument);
                Wait(() => File.Exists(copiedDocument), "The copied image document was not saved.");
                var copiedReference = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(File.ReadAllText(copiedDocument)).Single(l => l.IsImage).Reference;
                var copiedPath = new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(copiedDocument, copiedReference);
                if (copiedPath.Equals(imagePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(copiedPath) || !File.Exists(imagePath))
                    throw new IOException("Copying the image did not retain independent portable assets for both documents.");
                checks.Add("Rendered image context menu copies native pixels and paste/save creates an independent portable document image");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            }
            finally { RestoreClipboard(previousClipboard, ownedClipboardSequence); }
            sourceTab.Patterns.SelectionItem.Pattern.Select();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "The original document did not activate before session restoration.");
            window.Close();
            Wait(() => app.HasExited, "Native application did not close safely.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("Native application failed during shutdown: " + launchedProcess.ExitCode);
            app.Dispose(); launchedProcess.Dispose(); app = Launch(null);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("Restored window did not appear.");
            editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Session did not restore saved text.");
            checks.Add("Session restoration reopened the document");
            Find(window, "HelpMenu").Click();
            Find(window, "UpdatesMenu").Click();
            Window? updateWindow = null;
            Wait(() => (updateWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Lens Docs Studio updates")))?.AsWindow()) is not null, "The native updates window did not open.");
            var channel = Find(updateWindow!, "UpdateChannel").AsComboBox();
            channel.Select("Preview");
            channel.Collapse();
            Wait(() => File.ReadAllText(Path.Combine(state, "preferences.json")).Contains("\"Preview\"", StringComparison.Ordinal), "Update channel preference was not persisted.");
            if (Find(updateWindow!, "DownloadUpdate").IsEnabled || Find(updateWindow!, "InstallUpdate").IsEnabled) throw new InvalidOperationException("Untrusted update actions were enabled.");
            checks.Add("Native update preferences persist and untrusted installation actions stay disabled");
            updateWindow!.CaptureToFile(Path.Combine(output, "native-updates.png"));
            updateWindow!.Close();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Lens Docs Studio updates"))) is null, "The updates window did not close.");
            window.CaptureToFile(Path.Combine(output, "native-workspace.png"));
            window.Close();
            Wait(() => app.HasExited, "Restored application did not close.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("Restored application failed during shutdown: " + launchedProcess.ExitCode);
            File.WriteAllText(Path.Combine(output, "native-ui.json"), JsonSerializer.Serialize(new { Passed = true, Checks = checks, FixtureRoot = runRoot }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Native UI Automation: " + checks.Count + " checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            try { app?.GetMainWindow(automation, TimeSpan.FromSeconds(1))?.CaptureToFile(Path.Combine(output, "native-ui-failure.png")); } catch { }
            try { File.WriteAllLines(Path.Combine(output, "native-ui-failure-controls.txt"), app!.GetMainWindow(automation)!.FindAllDescendants().Select(e => $"{e.ControlType}: {e.AutomationId} — {e.Name}")); } catch { }
            File.WriteAllText(Path.Combine(output, "native-ui.json"), JsonSerializer.Serialize(new { Passed = false, Checks = checks, Error = ex.ToString(), FixtureRoot = runRoot }, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally { if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); } launchedProcess?.Dispose(); }

        Application Launch(string? path)
        {
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state);
            if (path is not null) { info.ArgumentList.Add(runRoot); info.ArgumentList.Add(path); }
            launchedProcess = Process.Start(info) ?? throw new IOException("Could not launch the native validation fixture.");
            launchedProcess.EnableRaisingEvents = true;
            // FlaUI refreshes and disposes its Process instance while locating a window.
            // Keep our separately owned started process for the real shutdown exit code.
            return Application.Attach(launchedProcess.Id);
        }
        void SaveThroughDialogue(string destination)
        {
            Window? saveWindow = null;
            Wait(() => (saveWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Save document")))?.AsWindow()) is not null, "The native Save dialogue did not open.");
            FocusWindow(saveWindow!);
            var fileName = Find(saveWindow!, "1001").AsTextBox();
            fileName.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(Path.GetFileName(destination));
            Wait(() => fileName.Text == Path.GetFileName(destination), "The native dialogue did not accept the typed filename.");
            (saveWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Save"))) ?? throw new InvalidOperationException("The Save button is unavailable.")).AsButton().Click();
        }
    }

    private static AutomationElement Find(Window window, string id)
    {
        AutomationElement? element = null;
        Wait(() => (element = window.FindFirstDescendant(cf => cf.ByAutomationId(id))) is not null, "Missing native control: " + id);
        return element!;
    }
    private static void FocusWindow(Window window)
    {
        window.SetForeground();
        Wait(() => { GetWindowThreadProcessId(GetForegroundWindow(), out var processId); return processId == window.Properties.ProcessId.Value; },
            "The native test window could not receive foreground focus. The interactive desktop is unavailable for keyboard validation.");
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    private static void RestoreClipboard(System.Windows.IDataObject? previous, uint ownedSequence)
    {
        // Do not replace a clipboard update made by the user or another application during validation.
        if (ownedSequence == 0 || GetClipboardSequenceNumber() != ownedSequence) return;
        try { if (previous is null) System.Windows.Clipboard.Clear(); else System.Windows.Clipboard.SetDataObject(previous, true); }
        catch (ExternalException) { }
    }
    private static void Wait(Func<bool> condition, string failure)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(20))
        {
            try { if (condition()) return; } catch (Exception ex) when (ex is IOException or FlaUI.Core.Exceptions.ElementNotAvailableException) { }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException(failure);
    }
}
