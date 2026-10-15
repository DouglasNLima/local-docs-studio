using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int Run(string executable, string output, bool requireBundledRuntime = false, bool desktopUxOnly = false)
    {
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                int result;
                using (NativeUiClipboard.Start()) result = RunCore(executable, output, requireBundledRuntime, desktopUxOnly);
                completion.SetResult(result);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Native UI Automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }
    private static int RunCore(string executable, string output, bool requireBundledRuntime, bool desktopUxOnly)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var runRoot = Path.Combine(output, "ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        var source = Path.Combine(runRoot, "fixture.md");
        File.WriteAllText(source, "# Native UI fixture\n\nSearchable text.\n\n```mermaid\nflowchart LR\nA[Write] --> B[Review]\n```\n");
        var linkedDocument = Path.Combine(runRoot, "Second document.md");
        File.WriteAllText(linkedDocument, "# Second document\n\nLinked workspace content.\n\n## Details\n\nMore detail.\n");
        File.WriteAllText(Path.Combine(runRoot, "Completion.mmd"), "flo\n");
        var state = Path.Combine(runRoot, "state");
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var checks = new List<string>();
        var saveDialogueChecks = new List<object>();
        var performance = new List<object>();
        using var automation = new UIA3Automation();
        File.WriteAllText(Path.Combine(output, "native-ui-threading.json"), JsonSerializer.Serialize(new {
            AutomationApartment = Thread.CurrentThread.GetApartmentState().ToString(), ClipboardApartment = NativeUiClipboard.Apartment.ToString(),
            TransactionTimeoutMilliseconds = automation.TransactionTimeout.TotalMilliseconds, ConnectionTimeoutMilliseconds = automation.ConnectionTimeout.TotalMilliseconds }));
        Application? app = null;
        Process? launchedProcess = null;
        Window? mainWindow = null;
        try
        {
            var operationWatch = Stopwatch.StartNew();
            app = Launch(source);
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("Native window did not appear.");
            mainWindow = window;
            var editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Searchable", StringComparison.Ordinal), "Fixture text did not load.");
            Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "The initial native preview did not finish.");
            Wait(() => Find(window, "CaretStatus").Name == "Ln 1, Col 1 · UTF-8", "The initial active document did not expose its exact caret and encoding status.");
            checks.Add("Startup file opened through native controls");
            MeasureOperation("Initial process launch through fixture text and Mermaid preview readiness", operationWatch);
            CheckLoadedRuntime(launchedProcess!, executable, output, requireBundledRuntime);
            CheckQuickStart(window, editor, automation, app.ProcessId, output, source);
            checks.Add("Empty-draft quick start exposes native workspace/document/help actions, yields to typing and preserves existing source");
            checks.Add(requireBundledRuntime
                ? "Installed application loads the .NET runtime and WPF graphics from its own package directory"
                : "Actual application process exposes its loaded .NET runtime and WPF graphics modules");
            CheckDesktopControls(window, editor, automation, app.ProcessId, state, output, source);
            checks.Add("Modern desktop controls retain independent panels, Focus Mode, Full Screen, Quick Open, actual palette actions, source selection and Undo");
            CheckWorkspaceModernisation(window, editor, automation, app.ProcessId, state, output, source);
            CheckOutlineModernisation(window, editor, output, source);
            checks.Add("Actual tab pinning, dirty close cancellation, active survival and confirmed Windows folder recycling retain source and recovery");
            CheckSynchronisation(window, editor, state, output, source);
            checks.Add("Native Pages and Continuous selection synchronisation retains repeated occurrences, Unicode, focus, visible geometry, opt-out and source bytes");
            CheckScrollSynchronisation(window, editor, state, output, source);
            checks.Add("Native bidirectional semantic scroll, rapid input, independent caret following and tab cancellation preserve focus, caret and source");
            CheckMinimumWindow(window, editor, automation, app.ProcessId, output, source);
            checks.Add("Minimum-size native window hides idle Cancel and retains visible Find/Replace controls, working replacement/Close, exact Undo and unchanged saved source");
            CheckRefinement(window, editor, automation, app.ProcessId, state, output, source,
                (destination, title) => SaveThroughDialogue(destination, title, absolutePath: true));
            checks.Add("Native refinement retains measured header geometry/keyboard focus, file identities, filtered Settings drafts, closed-workspace context and guarded export history/actions");
            if (desktopUxOnly)
            {
                Wait(() => Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Native UI fixture", StringComparison.Ordinal), "The original diagram preview did not resume.");
                Find(window, "DiagramActionZoom").AsButton().Invoke();
                Window? drawing = null;
                Wait(() => (drawing = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("DiagramWindow")))?.AsWindow()) is not null, "The diagram viewport did not open.");
                CheckDiagramViewport(drawing!, output);
                CheckDiagramImageCopy(window, drawing!, editor, source, output, automation, app.ProcessId);
                checks.Add("Pointer-driven diagram pan, anchored zoom, Reset/Fit and viewport-independent clipboard pixels");
                FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                Wait(() => app.HasExited, "The desktop UX fixture did not close safely.");
                if (launchedProcess!.ExitCode != 0) throw new IOException("The desktop UX fixture failed during shutdown.");
                RecordResult(new { Passed = true, Scope = "Desktop UX controls and synchronisation", Checks = checks, FixtureRoot = runRoot });
                Console.WriteLine("Native desktop UX: " + checks.Count + " checks passed.");
                return 0;
            }
            CheckLargeEditorNavigation(window, editor, output, source);
            checks.Add("Long wrapped editor Find reveals its final selected target, preserves insertion Undo and saved bytes, and resumes the original preview");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            var find = Find(window, "FindText").AsTextBox();
            Wait(() => Find(window, "FindContext").Name == "Find in editor", "Ctrl+F did not choose editor context.");
            find.Text = "Searchable";
            Keyboard.Type(VirtualKeyShort.RETURN);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            operationWatch.Restart();
            Keyboard.Type("\nNative UI validation — Café");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Typing was captured by an inactive search control.");
            MeasureOperation("Windows keyboard text delivery through exact editor observation", operationWatch);
            checks.Add("Editor search closes and keyboard input reaches the editor");
            operationWatch.Restart(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(source).Contains("Native UI validation — Café", StringComparison.Ordinal), "Save did not persist Unicode text.");
            MeasureOperation("Save shortcut through observed UTF-8 file update", operationWatch);
            checks.Add("Ctrl+S persisted actual UTF-8 source");
            var preview = Find(window, "DocumentPreview");
            var previewText = preview.Patterns.Text.Pattern.DocumentRange.FindText("Searchable", false, false)
                ?? throw new IOException("The actual preview focus text is unavailable.");
            var previewTextBounds = previewText.GetBoundingRectangles().First(rectangle => rectangle.Width > 0 && rectangle.Height > 0);
            Mouse.Click(new System.Drawing.Point((int)(previewTextBounds.Left + previewTextBounds.Width / 2), (int)(previewTextBounds.Top + previewTextBounds.Height / 2)));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(window, "FindContext").Name == "Find in preview", "Ctrl+F did not choose preview context.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            checks.Add("Ctrl+F targets the focused preview");
            Find(window, "DiagramActionZoom").AsButton().Invoke();
            Window? diagramWindow = null;
            Wait(() => (diagramWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId)
                .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)).And(cf.ByAutomationId("DiagramWindow")))?.AsWindow()) is not null,
                "The native diagram zoom window did not open.");
            Find(diagramWindow!, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(175);
            Wait(() => Find(diagramWindow!, "DiagramZoomLabel").Name == "175%", "The native zoom did not change scale.");
            diagramWindow!.CaptureToFile(Path.Combine(output, "native-diagram.png"));
            CheckDiagramViewport(diagramWindow, output);
            checks.Add("Native diagram Space/middle-button pan, pointer-centred zoom and Fit/Reset");
            CheckDiagramImageCopy(window, diagramWindow, editor, source, output, automation, app.ProcessId);
            checks.Add("Mermaid opens in a native drawing window and zoom changes its scale");
            checks.Add("Keyboard diagram copying retains exact native pixels across zoom levels and after closing the drawing window without changing source");
            CheckDiagramThemes(window, editor, state, output, source);
            checks.Add("Diagram theme menus persist all six choices, render fixed palettes independently of application appearance and retain source and update consent");
            CheckDiagramFailureActions(window, editor, state, output, source);
            checks.Add("Failed diagrams retain exact source copy, keyboard editing from Preview layout, source position and complete Undo without saving or changing update consent");
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
            Wait(() => ReadObservedText(source).Contains("Second%20document.md", StringComparison.Ordinal), "The inserted link was not saved.");
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
                var preferences = JsonSerializer.Deserialize<LensDocsStudio.Core.State.UserPreferences>(ReadObservedText(Path.Combine(state, "preferences.json")))!;
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
            Wait(() => File.Exists(renamedDocument) && !File.Exists(linkedDocument) && ReadObservedText(source).Contains("Renamed%20document.md", StringComparison.Ordinal), "Renaming did not update the actual file and its incoming Markdown link.");
            if (!ReadObservedText(source).Contains("Unsaved linked-document changes must survive the move.", StringComparison.Ordinal)) throw new IOException("The linked document lost its unsaved changes during the move.");
            Find(window, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Renamed%20document.md", StringComparison.Ordinal), "The open linked document did not receive the planned link update.");
            checks.Add("Native rename review saves unsaved linked content, moves the file, updates links and refreshes open editor text");
            CheckCaseOnlyRename(window, editor, automation, app.ProcessId, source, renamedDocument);
            checks.Add("Case-only rename reviews real filename/link changes, refreshes open drafts and history, and restores original spelling without changing document bytes");
            CheckFolderMove(window, editor, automation, app.ProcessId, output, source);
            checks.Add("Native folder F2 review preserves unsaved edits, assets and empty folders, updates ordinary/wiki links and history, and supports case-only folder names in both directions");
            Find(window, "FileMenu").Click(); Find(window, "CreateWorkspaceFile").Patterns.Invoke.Pattern.Invoke();
            Window? createWindow = null;
            Wait(() => (createWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Create workspace file")))?.AsWindow()) is not null, "The workspace create-file dialogue did not open.");
            Find(createWindow!, "WorkspacePathInput").AsTextBox().Text = "created-from-ui.md";
            Find(createWindow!, "ConfirmWorkspacePath").AsButton().Invoke();
            var createdPath = Path.Combine(runRoot, "created-from-ui.md");
            Wait(() => File.Exists(createdPath) && editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The native create-file workflow did not create and open the file.");
            FocusWindow(window); editor.Click(); Keyboard.Type("Created through the native workspace.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(createdPath) == "Created through the native workspace.", "The new file did not save its actual edited content.");
            checks.Add("Create workspace file creates a real Markdown file and saves native editor content");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type("Unsaved content retained before deletion.");
            // A new file with no previous line break uses Windows' CRLF default.
            const string deletedDraft = "Created through the native workspace.\r\nUnsaved content retained before deletion.";
            Wait(() => ReadEditorText(editor) == deletedDraft,
                "The deletion fixture did not receive its exact unsaved source, including the requested line break.");
            Find(window, "FileMenu").Click(); Find(window, "DeleteDocument").Patterns.Invoke.Pattern.Invoke();
            Window? deleteWindow = null;
            Wait(() => (deleteWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Delete document")))?.AsWindow()) is not null, "The deletion confirmation did not open.");
            (deleteWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("OK"))) ?? throw new InvalidOperationException("The delete confirmation is unavailable.")).AsButton().Invoke();
            Wait(() => !File.Exists(createdPath) && Find(window, "Status").Name.StartsWith("Deleted created-from-ui.md", StringComparison.Ordinal), "The file was not removed through the Windows shell.");
            var snapshotStore = new LensDocsStudio.Core.State.DocumentSnapshotStore(state);
            var retainedSnapshots = snapshotStore.ListAsync().GetAwaiter().GetResult();
            var deletedSnapshots = retainedSnapshots.Entries.Select(entry => snapshotStore.ReadAsync(entry).GetAwaiter().GetResult())
                .Where(snapshot => snapshot.Document.Path == createdPath).ToArray();
            if (deletedSnapshots.Length != 2
                || !deletedSnapshots.Any(snapshot => snapshot.Document.Text == "Created through the native workspace.")
                || !deletedSnapshots.Any(snapshot => snapshot.Document.Text == deletedDraft))
                throw new IOException("Deletion did not retain separate exact saved and unsaved source snapshots for its document.");
            checks.Add("Deleting a document uses the Windows shell and retains saved and unsaved source snapshots");
            Find(window, "FileMenu").Click(); Find(window, "BrowseSnapshots").Patterns.Invoke.Pattern.Invoke();
            Window? snapshotsWindow = null;
            Wait(() => (snapshotsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Document snapshots")))?.AsWindow()) is not null, "The native snapshot browser did not open.");
            var retainedItem = Find(snapshotsWindow!, "SnapshotList").AsListBox().Items.First(i => i.Name.StartsWith("created-from-ui.md", StringComparison.Ordinal)
                && i.Name.Contains("unsaved editor text", StringComparison.Ordinal));
            retainedItem.Select();
            Wait(() => Find(snapshotsWindow!, "SnapshotSource").AsTextBox().Text.Contains("Unsaved content retained before deletion.", StringComparison.Ordinal) && Find(snapshotsWindow!, "RestoreSnapshot").IsEnabled, "The retained unsaved source did not appear.");
            Find(snapshotsWindow!, "RestoreSnapshot").AsButton().Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Unsaved content retained before deletion.", StringComparison.Ordinal), "The snapshot was not recovered into a new editor tab.");
            if (File.Exists(createdPath)) throw new IOException("Inspecting and restoring a snapshot unexpectedly recreated a workspace file.");
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            SaveThroughDialogue(createdPath);
            Wait(() => File.Exists(createdPath) && ReadObservedText(createdPath).Contains("Unsaved content retained before deletion.", StringComparison.Ordinal), "The recovered source did not save to its explicitly chosen destination.");
            checks.Add("Snapshot browser recovers unsaved text into a new draft and Save recreates only the chosen file");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            var sourceTab = window.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).And(cf.ByName("fixture.md"))) ?? throw new InvalidOperationException("The original document tab is unavailable.");
            sourceTab.Patterns.SelectionItem.Pattern.Select();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "The original tab did not activate after file creation.");
            FocusWindow(window);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "A new unsaved document did not open.");
            var previousClipboard = NativeUiClipboard.GetDataObject();
            uint ownedClipboardSequence = 0;
            try
            {
                var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(2, 2, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
                    new byte[] { 62, 136, 255, 255, 62, 136, 255, 255, 62, 136, 255, 255, 62, 136, 255, 255 }, 8);
                NativeUiClipboard.SetImage(bitmap);
                ownedClipboardSequence = GetClipboardSequenceNumber();
                editor.Click(); operationWatch.Restart(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("![pasted-image](assets/images/", StringComparison.Ordinal), "Clipboard image did not persist into the unsaved document.");
                MeasureOperation("Paste shortcut through persisted 2 by 2 image and source insertion", operationWatch);
            }
            finally { RestoreClipboard(previousClipboard, ownedClipboardSequence); }
            var imageDocument = Path.Combine(runRoot, "Image document.md");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            SaveThroughDialogue(imageDocument);
            Wait(() => File.Exists(imageDocument), "The document containing the pasted image was not saved.");
            var imageReference = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(ReadObservedText(imageDocument)).Single(l => l.IsImage).Reference;
            if (!imageReference.StartsWith("assets/images/", StringComparison.Ordinal)) throw new IOException("The saved image still relies on a private draft folder.");
            var imagePath = new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(imageDocument, imageReference);
            if (!File.Exists(imagePath) || new FileInfo(imagePath).Length < 50) throw new IOException("Saving the unsaved document did not persist its portable image.");
            checks.Add("Clipboard image in an unsaved document is finalised into a portable image when saved through the native dialogue");
            FocusWindow(window); Find(window, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Back did not return from the image document.");
            var imageTab = window.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).And(cf.ByName("Image document.md"))) ?? throw new InvalidOperationException("The image document tab is unavailable.");
            imageTab.Patterns.SelectionItem.Pattern.Select();
            previousClipboard = NativeUiClipboard.GetDataObject(); ownedClipboardSequence = 0;
            try
            {
                Find(window, "DocumentImage0").RightClick();
                AutomationElement? copyImage = null;
                Wait(() => (copyImage = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("CopyDocumentImage")))) is not null, "The native image context menu did not open.");
                copyImage!.Click();
                Wait(() => Find(window, "Status").Name.StartsWith("Image copied.", StringComparison.Ordinal), "The rendered image was not copied to the Windows clipboard.");
                ownedClipboardSequence = GetClipboardSequenceNumber();
                if (NativeUiClipboard.GetImage() is not { PixelWidth: 2, PixelHeight: 2 }) throw new IOException("The clipboard does not contain the rendered image pixels.");
                FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The destination image draft did not open.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("![pasted-image](assets/images/", StringComparison.Ordinal), "The copied document image was not pasted into the new draft.");
                var copiedDocument = Path.Combine(runRoot, "Copied image document.md");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); SaveThroughDialogue(copiedDocument);
                Wait(() => File.Exists(copiedDocument), "The copied image document was not saved.");
                var copiedReference = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(ReadObservedText(copiedDocument)).Single(l => l.IsImage).Reference;
                var copiedPath = new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(copiedDocument, copiedReference);
                if (copiedPath.Equals(imagePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(copiedPath) || !File.Exists(imagePath))
                    throw new IOException("Copying the image did not retain independent portable assets for both documents.");
                checks.Add("Rendered image context menu copies native pixels and paste/save creates an independent portable document image");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            }
            finally { RestoreClipboard(previousClipboard, ownedClipboardSequence); }
            // Exercise asset management against persisted image files and a still-unsaved open document.
            imageTab.Patterns.SelectionItem.Pattern.Select();
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type("\nUnsaved image document text must survive rename.");
            var unusedImage = Path.Combine(runRoot, "assets", "images", "unused-ui.png");
            File.Copy(imagePath, unusedImage);
            var unusedBytes = File.ReadAllBytes(unusedImage);
            Find(window, "FileMenu").Click(); Find(window, "BrowseAssets").Patterns.Invoke.Pattern.Invoke();
            Window? assetsWindow = null;
            Wait(() => (assetsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Workspace images")))?.AsWindow()) is not null, "The native image manager did not open.");
            Find(assetsWindow!, "AssetFilter").AsTextBox().Text = Path.GetFileName(imagePath);
            Wait(() => Find(assetsWindow!, "AssetList").AsListBox().Items.Length == 1, "Filtering did not select the workspace image.");
            if (Find(assetsWindow!, "RemoveUnusedAsset").IsEnabled || !Find(assetsWindow!, "AssetUsage").AsListBox().Items.Any(i => i.Name.Contains("unsaved changes", StringComparison.Ordinal)))
                throw new IOException("The asset manager did not protect the image referenced by an unsaved document.");
            Find(assetsWindow!, "RenameAsset").AsButton().Invoke();
            Window? saveImagesWindow = null;
            Wait(() => (saveImagesWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Save before image changes")))?.AsWindow()) is not null, "The image rename did not preserve unsaved document changes first.");
            saveImagesWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("OK")))!.AsButton().Invoke();
            Window? imagePathWindow = null;
            Wait(() => (imagePathWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Rename or move image")))?.AsWindow()) is not null, "The image path dialogue did not open.");
            Find(imagePathWindow!, "WorkspacePathInput").AsTextBox().Text = "assets/images/renamed-ui.png";
            Find(imagePathWindow!, "ConfirmWorkspacePath").AsButton().Invoke();
            Window? imageReviewWindow = null;
            Wait(() => (imageReviewWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Review image move")))?.AsWindow()) is not null, "The image reference review did not open.");
            if (Find(imageReviewWindow!, "AssetMoveDocuments").AsListBox().Items.Length != 1) throw new IOException("The image move did not identify the exact affected document.");
            Find(imageReviewWindow!, "ApplyAssetMove").AsButton().Invoke();
            var renamedImage = Path.Combine(runRoot, "assets", "images", "renamed-ui.png");
            Wait(() => File.Exists(renamedImage) && !File.Exists(imagePath) && ReadObservedText(imageDocument).Contains("assets/images/renamed-ui.png", StringComparison.Ordinal), "The image rename did not update its file and Markdown reference.");
            if (!ReadObservedText(imageDocument).Contains("Unsaved image document text must survive rename.", StringComparison.Ordinal)) throw new IOException("Renaming an image discarded an unsaved edit.");
            checks.Add("Native image manager shows unsaved references and reviewed rename preserves text while updating the real image and Markdown path");
            Wait(() => (assetsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Workspace images")))?.AsWindow()) is not null, "The image audit did not refresh after rename.");
            Find(assetsWindow!, "AssetFilter").AsTextBox().Text = "unused-ui.png";
            Wait(() => Find(assetsWindow!, "RemoveUnusedAsset").IsEnabled, "The unreferenced fixture image could not be selected for removal.");
            assetsWindow!.CaptureToFile(Path.Combine(output, "native-assets.png"));
            Find(assetsWindow!, "RemoveUnusedAsset").AsButton().Invoke();
            Window? removeImageWindow = null;
            Wait(() => (removeImageWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Remove unreferenced image")))?.AsWindow()) is not null, "The unused image confirmation did not open.");
            removeImageWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("OK")))!.AsButton().Invoke();
            Wait(() => !File.Exists(unusedImage), "The unused image was not removed through the Windows shell.");
            Wait(() => (assetsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Workspace images")))?.AsWindow()) is not null, "The image audit did not reopen after removal.");
            Find(assetsWindow!, "CloseAssets").AsButton().Invoke();
            Find(window, "FileMenu").Click(); Find(window, "BrowseSnapshots").Patterns.Invoke.Pattern.Invoke();
            Wait(() => (snapshotsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Document snapshots")))?.AsWindow()) is not null, "The snapshot browser did not open for image recovery.");
            Find(snapshotsWindow!, "SnapshotList").AsListBox().Items.First(i => i.Name.Contains("unused-ui.png", StringComparison.Ordinal)).Select();
            Wait(() => Find(snapshotsWindow!, "RestoreSnapshot").IsEnabled, "The retained image snapshot was unavailable.");
            Find(snapshotsWindow!, "RestoreSnapshot").AsButton().Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("![Recovered image]", StringComparison.Ordinal), "The removed image did not recover into a portable draft.");
            var recoveredImageDocument = Path.Combine(runRoot, "Recovered image.md");
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); SaveThroughDialogue(recoveredImageDocument);
            Wait(() => File.Exists(recoveredImageDocument), "The recovered image document was not saved.");
            var restoredReference = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(ReadObservedText(recoveredImageDocument)).Single(l => l.IsImage).Reference;
            var restoredImage = new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(recoveredImageDocument, restoredReference);
            if (!unusedBytes.SequenceEqual(File.ReadAllBytes(restoredImage)) || File.Exists(unusedImage)) throw new IOException("Image recovery did not preserve exact bytes at the explicitly chosen destination.");
            checks.Add("Unused image removal uses Windows recycling and the snapshot browser restores identical bytes into an explicitly saved portable document");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            sourceTab.Patterns.SelectionItem.Pattern.Select();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "The original document did not activate before session restoration.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type(VirtualKeyShort.RETURN);
            foreach (var line in new[] { "| Area | Status |", "|---|---|", "| Clipboard | Ready |" })
            { Keyboard.Type(line); Keyboard.Type(VirtualKeyShort.RETURN); }
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(source).Contains("| Clipboard | Ready |", StringComparison.Ordinal), "The clipboard table fixture did not save.");
            previousClipboard = NativeUiClipboard.GetDataObject(); ownedClipboardSequence = 0;
            try
            {
                Find(window, "EditMenu").Click(); Find(window, "CopyRenderedContent").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name.StartsWith("Rendered content copied as HTML", StringComparison.Ordinal), "Rich clipboard preparation did not finish.");
                ownedClipboardSequence = GetClipboardSequenceNumber();
                var clipboard = NativeUiClipboard.GetDataObject()!;
                var plain = clipboard.GetData(System.Windows.DataFormats.UnicodeText) as string ?? throw new IOException("The rich clipboard is missing Unicode text.");
                var rich = clipboard.GetData(System.Windows.DataFormats.Html) as string ?? throw new IOException("The rich clipboard is missing native HTML Format data.");
                var fragment = LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(rich);
                var clipboardHtml = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(fragment);
                File.WriteAllText(Path.Combine(output, "clipboard-fragment.html"), fragment);
                File.WriteAllText(Path.Combine(output, "clipboard-text.txt"), plain);
                if (!plain.Contains("Café", StringComparison.Ordinal)) throw new IOException("The Windows clipboard lost Unicode text.");
                if (!plain.Contains("Clipboard\tReady", StringComparison.Ordinal) || clipboardHtml.QuerySelectorAll("table").Length != 1)
                    throw new IOException("The Windows clipboard lost the fixture table or its text cells.");
                if (!clipboardHtml.QuerySelectorAll("img").Any(i => i.GetAttribute("src")?.StartsWith("data:image/png;base64,", StringComparison.Ordinal) == true))
                    throw new IOException("The Windows clipboard is missing embedded diagram pixels.");
                if (clipboardHtml.QuerySelectorAll("button,script").Length != 0 || plain.Contains("Copy source", StringComparison.Ordinal))
                    throw new IOException("The Windows clipboard leaked native preview controls.");
                checks.Add("Native rich copy publishes Windows HTML Format and Unicode text with real embedded diagram pixels, table cells and no preview controls");
                var originalMarkdown = ReadEditorText(editor);
                Find(window, "EditMenu").Click(); Find(window, "CopyMarkdownWithImages").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name.StartsWith("Markdown copied with embedded images.", StringComparison.Ordinal), "Markdown clipboard preparation did not finish.");
                ownedClipboardSequence = GetClipboardSequenceNumber();
                var sourceClipboard = NativeUiClipboard.GetDataObject()!;
                var sourcePlain = sourceClipboard.GetData(System.Windows.DataFormats.UnicodeText) as string ?? throw new IOException("Markdown copy has no Unicode source.");
                var sourceHtml = sourceClipboard.GetData(System.Windows.DataFormats.Html) as string ?? throw new IOException("Markdown copy has no image carrier.");
                var carrier = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(sourceHtml));
                var sourceImage = carrier.QuerySelectorAll("img").Single().GetAttribute("src")!;
                const string fixtureDiagram = "```mermaid\nflowchart LR\nA[Write] --> B[Review]\n```";
                if (!originalMarkdown.Contains(fixtureDiagram, StringComparison.Ordinal)
                    || sourcePlain.Replace("![Mermaid diagram](" + sourceImage + ")", fixtureDiagram, StringComparison.Ordinal) != originalMarkdown
                    || carrier.QuerySelectorAll("h1,h2,p,table,ul,ol,pre,code,strong,em,script").Length != 0)
                    throw new IOException("Markdown copying changed literal source or added semantic HTML.");
                FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The Markdown paste target is not empty.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Wait(() => Find(window, "Status").Name.StartsWith("Markdown pasted; embedded images saved locally.", StringComparison.Ordinal), "Markdown paste did not persist its image.");
                var sourcePastePath = Path.Combine(runRoot, "Markdown clipboard document.md");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); SaveThroughDialogue(sourcePastePath);
                Wait(() => File.Exists(sourcePastePath), "The Markdown clipboard document did not save.");
                var savedMarkdown = ReadObservedText(sourcePastePath);
                var sourcePasteLink = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(savedMarkdown).Single(l => l.IsImage);
                if (savedMarkdown.Replace(sourcePasteLink.Reference, sourceImage, StringComparison.Ordinal) != sourcePlain)
                    throw new IOException("Markdown paste changed source outside the saved image destination.");
                using (var sourcePixels = new MemoryStream(Convert.FromBase64String(sourceImage.Split(',', 2)[1])))
                using (var savedPixels = File.OpenRead(new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(sourcePastePath, sourcePasteLink.Reference)))
                {
                    var expectedPixels = LensDocsStudio.Native.Platform.NativeRasterService.Read(sourcePixels);
                    var actualPixels = LensDocsStudio.Native.Platform.NativeRasterService.Read(savedPixels);
                    if (expectedPixels.PixelWidth != actualPixels.PixelWidth || expectedPixels.PixelHeight != actualPixels.PixelHeight)
                        throw new IOException("Markdown paste changed the diagram dimensions.");
                    var stride = expectedPixels.PixelWidth * 4;
                    var expectedBytes = new byte[stride * expectedPixels.PixelHeight]; var actualBytes = new byte[expectedBytes.Length];
                    new System.Windows.Media.Imaging.FormatConvertedBitmap(expectedPixels, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(expectedBytes, stride, 0);
                    new System.Windows.Media.Imaging.FormatConvertedBitmap(actualPixels, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(actualBytes, stride, 0);
                    if (!expectedBytes.SequenceEqual(actualBytes)) throw new IOException("Markdown clipboard paste changed diagram pixels.");
                }
                checks.Add("Copy Markdown with images preserves literal source through Windows clipboard, native paste and Save as with identical portable diagram pixels");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
                sourceTab.Patterns.SelectionItem.Pattern.Select();
                var mixed = new System.Windows.DataObject();
                mixed.SetData(System.Windows.DataFormats.Html, rich, false); mixed.SetData(System.Windows.DataFormats.UnicodeText, plain, false);
                mixed.SetData(System.Windows.DataFormats.Bitmap, System.Windows.Media.Imaging.BitmapSource.Create(2, 2, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, new byte[16], 8));
                NativeUiClipboard.SetDataObject(mixed, true); ownedClipboardSequence = GetClipboardSequenceNumber();
                FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The rich paste target is not empty.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Wait(() => Find(window, "Status").Name.StartsWith("HTML pasted as Markdown.", StringComparison.Ordinal), "Formatted paste did not complete.");
                var pasted = ReadEditorText(editor);
                if (!pasted.Contains("Café", StringComparison.Ordinal) || !pasted.Contains("| Clipboard | Ready |", StringComparison.Ordinal) || !pasted.Contains("![Mermaid diagram]", StringComparison.Ordinal))
                    throw new IOException("Formatted paste lost text, table cells or diagram images when the clipboard also offered a bitmap.");
                var pastedPath = Path.Combine(runRoot, "Rich clipboard document.md");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); SaveThroughDialogue(pastedPath);
                Wait(() => File.Exists(pastedPath), "The formatted paste document did not save.");
                var pastedLinks = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(ReadObservedText(pastedPath)).Where(link => link.IsImage).ToArray();
                if (pastedLinks.Length != 1) throw new IOException("Formatted paste did not retain the single diagram image.");
                var pastedImage = new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(pastedPath, pastedLinks[0].Reference);
                using (var pixels = File.OpenRead(pastedImage))
                {
                    var decoded = LensDocsStudio.Native.Platform.NativeRasterService.Read(pixels);
                    if (decoded.PixelWidth <= 2 || decoded.PixelHeight <= 2) throw new IOException("Paste chose the fallback bitmap instead of the rich diagram image.");
                }
                NativeUiClipboard.SetText("Item,Value\nCSV,Portable"); ownedClipboardSequence = GetClipboardSequenceNumber();
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                Find(window, "EditMenu").Click(); Find(window, "PasteSpecialMenu").Patterns.ExpandCollapse.Pattern.Expand();
                Find(window, "PasteTable").Patterns.Invoke.Pattern.Invoke();
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("| CSV | Portable |", StringComparison.Ordinal), "Paste Special did not convert the CSV table.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
                Wait(() => ReadObservedText(pastedPath).Contains("| CSV | Portable |", StringComparison.Ordinal), "The pasted CSV table did not save.");
                checks.Add("Formatted Windows paste prefers HTML over a fallback bitmap, persists portable diagram pixels and exposes CSV table Paste Special");
                var beforeFailedPaste = ReadEditorText(editor);
                var invalidImage = new System.Windows.DataObject();
                invalidImage.SetData(System.Windows.DataFormats.Html, LensDocsStudio.Core.Exports.HtmlClipboardFormat.Encode("<p>Rejected image</p><img src='data:image/png;base64,AQID'>"), false);
                NativeUiClipboard.SetDataObject(invalidImage, true); ownedClipboardSequence = GetClipboardSequenceNumber();
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Window? pasteError = null;
                AutomationElement? acknowledge = null;
                var pasteErrorObservations = 0;
                Wait(() =>
                {
                    pasteErrorObservations++;
                    pasteError = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId)
                        .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)).And(cf.ByName("Lens Docs Studio")))?.AsWindow();
                    acknowledge = pasteError?.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)));
                    return acknowledge is { IsEnabled: true } && !acknowledge.IsOffscreen;
                }, "Invalid clipboard pixels did not report an accessible acknowledgement button.");
                File.WriteAllText(Path.Combine(output, "native-paste-error-acknowledgement.json"), JsonSerializer.Serialize(new
                {
                    ProcessId = pasteError!.Properties.ProcessId.Value, Title = pasteError.Name, WindowType = pasteError.ControlType.ToString(),
                    Button = acknowledge!.Name, acknowledge.IsEnabled, acknowledge.IsOffscreen, Bounds = acknowledge.BoundingRectangle,
                    Observations = pasteErrorObservations, Procedure = "Observe the error window and ready acknowledgement together within the original deadline; invoke once."
                }, new JsonSerializerOptions { WriteIndented = true }));
                acknowledge!.AsButton().Invoke();
                Wait(() => window.IsEnabled, "The paste error did not close.");
                if (editor.Patterns.Value.Pattern.Value.Value != beforeFailedPaste || ReadObservedText(pastedPath) != beforeFailedPaste)
                    throw new IOException("An invalid clipboard image discarded the editor selection or changed the saved document.");
                var literal = new System.Windows.DataObject(); literal.SetData(System.Windows.DataFormats.UnicodeText, "# Literal *source*", false);
                literal.SetData(System.Windows.DataFormats.Html, LensDocsStudio.Core.Exports.HtmlClipboardFormat.Encode("<p>Different rich text</p>"), false);
                NativeUiClipboard.SetDataObject(literal, true); ownedClipboardSequence = GetClipboardSequenceNumber();
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == "# Literal *source*", "Ctrl+Shift+V did not choose literal clipboard text.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeFailedPaste, "Undo did not restore the formatted paste document.");
                checks.Add("Invalid clipboard pixels preserve selected and saved source; Ctrl+Shift+V chooses literal text and one Undo restores the document");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                Find(window, "InsertMenu").Click(); Find(window, "CalloutMenu").Patterns.ExpandCollapse.Pattern.Expand();
                Find(window, "InsertNote").Patterns.Invoke.Pattern.Invoke();
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("> [!NOTE]", StringComparison.Ordinal), "The native callout menu did not insert a note.");
                var insertedCallout = ReadEditorText(editor);
                const string calloutBody = "Review evidence.";
                Keyboard.Type(calloutBody);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("> Review evidence.", StringComparison.Ordinal), "Callout typing did not replace only the selected body.");
                if (!editor.Patterns.Value.Pattern.Value.Value.StartsWith(beforeFailedPaste, StringComparison.Ordinal))
                    throw new IOException("Callout insertion changed the preceding document.");
                // Windows text input can create one undo group per character. First restore the exact
                // placeholder state, then require the formatting action itself to undo in one step.
                for (var typedUndo = 0; editor.Patterns.Value.Pattern.Value.Value != insertedCallout && typedUndo < calloutBody.Length + 1; typedUndo++)
                {
                    var beforeUndo = ReadEditorText(editor);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                    Wait(() => editor.Patterns.Value.Pattern.Value.Value != beforeUndo, "Undo did not change the typed callout body.");
                    if (!editor.Patterns.Value.Pattern.Value.Value.StartsWith(beforeFailedPaste + "\n\n> [!NOTE]\n> ", StringComparison.Ordinal))
                        throw new IOException("Undo of callout typing also changed its structural marker or preceding document.");
                }
                if (editor.Patterns.Value.Pattern.Value.Value != insertedCallout)
                    throw new IOException("Undo of callout typing did not restore the exact inserted placeholder.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeFailedPaste, "One Undo did not remove the complete callout insertion.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("> [!NOTE]", StringComparison.Ordinal), "Redo did not restore the inserted callout.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeFailedPaste, "Undo after Redo did not restore the saved document.");
                checks.Add("Native callout insertion retains surrounding text, selects its body for typing and supports complete Undo and Redo actions");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
                sourceTab.Patterns.SelectionItem.Pattern.Select();
            }
            finally { RestoreClipboard(previousClipboard, ownedClipboardSequence); }
            CheckInlineLabels(window, editor, automation, app.ProcessId, output, source);
            checks.Add("Native badge and keyboard-shortcut dialogues retain literal labels, validation and Cancel; status editing, preview text and horizontal rules preserve source with complete Undo and Redo");
            CheckTableEditor(window, editor, automation, app.ProcessId, output, source);
            checks.Add("Native table grid supports keyboard cell edits, Cancel, row and column changes, retained alignment, existing/new tables and complete Undo/Redo without changing saved source");
            var completionFile = Find(window, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem).And(cf.ByName("Completion.mmd")))
                ?? throw new IOException("The standalone Mermaid completion fixture is unavailable in the native tree.");
            FocusWindow(window); completionFile.Patterns.SelectionItem.Pattern.Select(); completionFile.Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == "flo\n", "The standalone Mermaid fixture did not open.");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type(VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidCompletion"))) is not null,
                "Ctrl+Space did not open the native completion window.");
            Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == "flowchart TD\n  A[Start] --> B[Finish]\n", "Enter did not accept the selected native completion.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == "flo\n", "One Undo did not restore the incomplete diagram.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.StartsWith("flowchart TD", StringComparison.Ordinal), "Redo did not restore the completed diagram.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == "flo\n", "Completion could not be undone after Redo.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type(VirtualKeyShort.END); Keyboard.Type("w");
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidCompletion"))) is not null,
                "Typing a Mermaid prefix did not offer native completion.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidCompletion"))) is null,
                "Escape did not dismiss native completion.");
            if (editor.Patterns.Value.Pattern.Value.Value != "flow\n") throw new IOException("Dismissing completion changed the typed source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == "flo\n", "Undo did not restore source after dismissed completion.");
            checks.Add("Native Mermaid completion supports Ctrl+Space, Enter, typing, Escape and one Undo/Redo per insertion");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME); Keyboard.Type(VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("MermaidCompletion"))) is not null,
                "The diagram could not be completed for site export.");
            Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(Path.Combine(runRoot, "Completion.mmd")).StartsWith("flowchart TD", StringComparison.Ordinal), "The completed diagram did not save before export.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W); sourceTab.Patterns.SelectionItem.Pattern.Select();
            Find(window, "ExportMenu").Click(); Find(window, "ExportDocsSite").Patterns.Invoke.Pattern.Invoke();
            Window? siteWindow = null;
            Wait(() => (siteWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Export Docs Site")))?.AsWindow()) is not null,
                "The native Docs Site options did not open.");
            Find(siteWindow!, "DocsSiteTitle").AsTextBox().Text = "Native UI documentation";
            Find(siteWindow!, "ConfirmDocsSite").Patterns.Invoke.Pattern.Invoke();
            var sitePath = Path.Combine(runRoot, "documentation-site.zip"); SaveThroughDialogue(sitePath, "Save Docs Site");
            Wait(() => File.Exists(sitePath) && ExportCompleted(window, sitePath), "The native Docs Site ZIP did not finish exporting with matching completion actions.");
            using (var siteZip = System.IO.Compression.ZipFile.OpenRead(sitePath))
            {
                var manifestEntry = siteZip.GetEntry("site-manifest.json") ?? throw new IOException("The exported site manifest is missing.");
                using var manifestStream = manifestEntry.Open(); using var manifest = JsonDocument.Parse(manifestStream);
                if (manifest.RootElement.GetProperty("title").GetString() != "Native UI documentation"
                    || manifest.RootElement.GetProperty("sourcePageCount").GetInt32() < 2
                    || manifest.RootElement.GetProperty("pages").EnumerateArray().Any(p => p.GetProperty("path").GetString()!.StartsWith("state/", StringComparison.OrdinalIgnoreCase))
                    || siteZip.GetEntry("index.html") is null || siteZip.GetEntry("assets/docs-site-data.js") is null)
                    throw new IOException("The native Docs Site lost options, portable assets or private-state isolation.");
            }
            checks.Add("Native Docs Site options and Save dialogue create a portable workspace ZIP without private state");
            // AvalonEdit's outer automation element exposes Value; keyboard input belongs to its text area.
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            var beforeProgress = ReadEditorText(editor);
            var progressWindow = OpenProgress(window);
            Find(progressWindow, "ProgressLabel").AsTextBox().Text = "Delivery & review";
            Find(progressWindow, "ProgressPercentage").AsTextBox().Text = "101";
            Find(progressWindow, "ApplyProgress").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(progressWindow, "ProgressValidation").Name.Contains("0 to 100", StringComparison.Ordinal), "The progress editor accepted an out-of-range percentage.");
            if (editor.Patterns.Value.Pattern.Value.Value != beforeProgress) throw new IOException("Invalid progress input changed the source.");
            Find(progressWindow, "ProgressPercentage").AsTextBox().Text = "65";
            Find(progressWindow, "ApplyProgress").Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("value=\"65\"", StringComparison.Ordinal), "The native progress editor did not insert its percentage.");
            var insertedProgress = ReadEditorText(editor);
            if (!insertedProgress.StartsWith(beforeProgress, StringComparison.Ordinal) || !insertedProgress.Contains("Delivery &amp; review", StringComparison.Ordinal))
                throw new IOException("Progress insertion lost preceding source or escaped label text.");
            progressWindow = OpenProgress(window);
            if (Find(progressWindow, "ProgressLabel").AsTextBox().Text != "Delivery & review" || Find(progressWindow, "ProgressPercentage").AsTextBox().Text != "65")
                throw new IOException("The native progress editor did not reopen the selected indicator.");
            Find(progressWindow, "ProgressPercentage").AsTextBox().Text = "100";
            Find(progressWindow, "ApplyProgress").Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("value=\"100\"", StringComparison.Ordinal), "The native progress update did not apply.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedProgress, "Progress update Undo did not restore the entire original indicator.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeProgress, "Progress insertion Undo did not restore the original source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedProgress, "Progress Redo changed the indicator.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            checks.Add("Native progress dialogue validates input, reopens an existing indicator and preserves complete Undo/Redo actions");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeProgress, "Progress source was not restored before bundle export.");
            var savedBeforeBundle = ReadObservedText(source);
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            const string bundleDraft = " Bundle-only unsaved text."; Keyboard.Type(bundleDraft);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.EndsWith(bundleDraft, StringComparison.Ordinal), "The bundle draft did not reach the editor.");
            var bundleSource = ReadEditorText(editor);
            Find(window, "ExportMenu").Click(); Find(window, "ExportMarkdownBundle").Patterns.Invoke.Pattern.Invoke();
            var bundlePath = Path.Combine(runRoot, "portable-documents.zip"); SaveThroughDialogue(bundlePath, "Export Markdown bundle");
            Wait(() => File.Exists(bundlePath) && ExportCompleted(window, bundlePath), "The native Markdown bundle did not finish with matching completion actions.");
            using (var archive = System.IO.Compression.ZipFile.OpenRead(bundlePath))
            {
                if (archive.Entries.Any(entry => entry.FullName.StartsWith("state/", StringComparison.OrdinalIgnoreCase))
                    || !archive.Entries.Any(entry => entry.FullName.StartsWith("assets/images/", StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("The Markdown bundle included private state or lost portable assets.");
                using var reader = new StreamReader((archive.GetEntry("fixture.md") ?? throw new IOException("The bundle source is missing.")).Open());
                if (reader.ReadToEnd() != bundleSource || ReadObservedText(source) != savedBeforeBundle)
                    throw new IOException("The Markdown bundle lost current edits or changed the saved source.");
            }
            FocusWindow(window); editor.Click();
            for (var typingUndo = 0; editor.Patterns.Value.Pattern.Value.Value != beforeProgress && typingUndo <= bundleDraft.Length; typingUndo++)
            {
                var previous = ReadEditorText(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value != previous, "Undo did not restore the bundle-only draft.");
                if (!editor.Patterns.Value.Pattern.Value.Value.StartsWith(beforeProgress, StringComparison.Ordinal)) throw new IOException("Undo changed the original document after bundle export.");
            }
            if (editor.Patterns.Value.Pattern.Value.Value != beforeProgress) throw new IOException("The bundle draft was not fully restored.");
            checks.Add("Native Markdown bundle exports unsaved source and portable assets without private state or source-file writes");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            var beforeAnchor = ReadEditorText(editor);
            var anchorWindow = OpenAnchor(window);
            Find(anchorWindow, "AnchorIdentifier").AsTextBox().Text = "invalid id";
            Find(anchorWindow, "ApplyAnchor").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(anchorWindow, "AnchorValidation").Name.Contains("128", StringComparison.Ordinal), "The native anchor dialogue accepted an invalid identifier.");
            if (editor.Patterns.Value.Pattern.Value.Value != beforeAnchor) throw new IOException("Invalid anchor input changed source.");
            Find(anchorWindow, "AnchorIdentifier").AsTextBox().Text = "native-anchor";
            Find(anchorWindow, "AnchorLabel").AsTextBox().Text = "Named native position";
            anchorWindow.CaptureToFile(Path.Combine(output, "native-anchor-dialogue.png"));
            Find(anchorWindow, "ApplyAnchor").Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("data-doc-anchor>Named native position</span>", StringComparison.Ordinal), "The anchor was not inserted.");
            var insertedAnchor = ReadEditorText(editor);
            anchorWindow = OpenAnchor(window);
            if (Find(anchorWindow, "AnchorIdentifier").AsTextBox().Text != "native-anchor" || !Find(anchorWindow, "AnchorIdentifier").Patterns.Value.Pattern.IsReadOnly.Value)
                throw new IOException("Editing an anchor did not preserve its identifier.");
            Find(anchorWindow, "AnchorLabel").AsTextBox().Text = "";
            Find(anchorWindow, "ApplyAnchor").Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("data-doc-anchor></span>", StringComparison.Ordinal), "The empty anchor edit did not apply.");
            var emptyAnchorSource = ReadEditorText(editor);
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_K);
            Window? anchorLinkWindow = null;
            Wait(() => (anchorLinkWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Insert document link")))?.AsWindow()) is not null,
                "The native anchor link picker did not open.");
            Find(anchorLinkWindow!, "DocumentLinkFilter").AsTextBox().Text = "fixture";
            Find(anchorLinkWindow!, "DocumentLinkResults").AsListBox().Select(0);
            Find(anchorLinkWindow!, "DocumentLinkSection").AsComboBox().Expand();
            Wait(() => Find(anchorLinkWindow!, "DocumentLinkSection").AsComboBox().Items.Any(i => i.Text == "Anchor: native-anchor"), "The link picker did not include the unsaved empty anchor.");
            Find(anchorLinkWindow!, "DocumentLinkSection").AsComboBox().Select("Anchor: native-anchor");
            Find(anchorLinkWindow!, "DocumentLinkLabel").AsTextBox().Text = "Visit native anchor";
            Find(anchorLinkWindow!, "InsertDocumentLink").Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.EndsWith("[Visit native anchor](fixture.md#native-anchor)", StringComparison.Ordinal), "The link picker lost the selected anchor reference.");
            AutomationElement? namedLink = null;
            Wait(() => (namedLink = Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetChildren()
                .FirstOrDefault(e => e.Properties.Name.ValueOrDefault == "Visit native anchor")) is not null, "The native anchor link was not rendered.");
            namedLink!.Patterns.Invoke.Pattern.Invoke();
            var anchorOffset = emptyAnchorSource.IndexOf("<span id=\"native-anchor\"", StringComparison.Ordinal);
            var anchorPrefix = emptyAnchorSource[..anchorOffset]; var anchorLine = anchorPrefix.Count(c => c == '\n') + 1;
            var anchorColumn = anchorOffset - anchorPrefix.LastIndexOf('\n');
            Wait(() => Find(window, "CaretStatus").Name == $"Ln {anchorLine}, Col {anchorColumn} · UTF-8", "The native link did not navigate to the empty anchor's exact source position.");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == emptyAnchorSource, "Undo did not remove the complete inserted anchor link.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedAnchor, "Anchor label editing did not form one Undo action.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeAnchor, "Anchor insertion did not restore the original source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedAnchor, "Anchor insertion Redo changed source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeAnchor, "Anchor checks did not restore the original source.");
            checks.Add("Native anchors validate identifiers, retain links when labels change, navigate to empty targets and preserve complete Undo/Redo");
            var contextOriginal = ReadEditorText(editor);
            var contextClipboard = NativeUiClipboard.GetDataObject(); uint contextClipboardSequence = 0;
            try
            {
                const string contextText = "Context action — Café";
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(contextText); contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextText, "The editor context menu did not paste into its selected text.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText("Clipboard replacement sentinel"); contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextCopy");
                Wait(() => NativeUiClipboard.GetText() == contextText, "The editor context Copy command lost selected Unicode text.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextCut"); contextClipboardSequence = GetClipboardSequenceNumber();
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Length == 0, "The editor context Cut command did not remove its selection.");
                EditorContext("EditorContextUndo");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextText, "Context Undo did not restore cut text.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); EditorContext("EditorContextBold");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == "**" + contextText + "**", "Context formatting did not retain the editor selection.");
                EditorContext("EditorContextUndo");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextText, "Context formatting did not form one Undo action.");
                EditorContext("EditorContextUndo");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Context paste Undo did not restore the original document.");
                checks.Add("Keyboard-opened editor context commands paste, copy, cut and format selected text with complete Undo");

                const string contextCode = "const nativeContext = 'Café 😀 🚀';\n  keepIndent();";
                const string contextMarkdown = "```js\n" + contextCode + "\n```";
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(contextMarkdown); contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextMarkdown, "The code context fixture did not paste.");
                Wait(() => Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("const nativeContext", StringComparison.Ordinal), "The code context fixture did not render.");
                var codeRange = Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange;
                var stringRange = codeRange.FindText("Café", false, false) ?? throw new IOException("The coloured string was not selectable.");
                var plainRange = codeRange.FindText("nativeContext", false, false) ?? throw new IOException("The plain identifier was not selectable.");
                if (stringRange.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor) is not int stringColour
                    || plainRange.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor) is not int plainColour || stringColour == plainColour)
                    throw new IOException("The native preview did not expose distinct syntax colours through UI Automation.");
                window.CaptureToFile(Path.Combine(output, "native-code-syntax.png"));
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    Find(window, "ViewMenu").Click(); Find(window, theme + "Theme").Patterns.Invoke.Pattern.Invoke();
                    var colour = ((System.Windows.Media.SolidColorBrush)LensDocsStudio.Native.Rendering.CodeSyntaxColours.For(LensDocsStudio.Core.Rendering.CodeTokenRole.String, theme == "Dark")).Color;
                    var expectedColour = colour.R << 16 | colour.G << 8 | colour.B;
                    int? observedColour = null;
                    try
                    {
                        Wait(() =>
                        {
                            observedColour = Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.FindText("Café", false, false)
                                ?.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor) as int?;
                            return observedColour == expectedColour;
                        }, "Changing the native theme did not redraw the code preview palette.");
                    }
                    finally
                    {
                        File.WriteAllText(Path.Combine(output, "native-theme-colour-" + theme.ToLowerInvariant() + ".json"),
                            JsonSerializer.Serialize(new { Theme = theme, Expected = expectedColour, Observed = observedColour }));
                    }
                    Wait(() => ReadObservedText(Path.Combine(state, "preferences.json")).Contains("\"" + theme + "\"", StringComparison.Ordinal), "The chosen theme was not persisted.");
                    if (editor.Patterns.Value.Pattern.Value.Value != contextMarkdown) throw new IOException("Changing the native theme altered editor source.");
                    window.CaptureToFile(Path.Combine(output, "native-theme-" + theme.ToLowerInvariant() + ".png"));
                }
                checks.Add("Native theme menus redraw preview colours, retain editor source and persist the chosen theme");
                OpenCodeContext("CopyCodeBlock").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name == "Code copied as plain text.", "The preview context command did not copy code.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                if (NativeUiClipboard.GetText() != contextCode || NativeUiClipboard.ContainsData(System.Windows.DataFormats.Html))
                    throw new IOException("Copy code lost source content or added rendered HTML to plain code.");
                OpenCodeContext("CopyCodeMarkdown").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name == "Code block Markdown source copied.", "The preview context command did not copy Markdown.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                if (NativeUiClipboard.GetText() != contextMarkdown) throw new IOException("Copy code Markdown lost its original fences or source line endings.");
                var codeSavedSource = ReadObservedText(source);
                Find(window, "EditMenu").Click(); Find(window, "CopyRenderedContent").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name.StartsWith("Rendered content copied as HTML", StringComparison.Ordinal), "Coloured code copying did not complete.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                var richCode = NativeUiClipboard.GetData(System.Windows.DataFormats.Html) as string ?? throw new IOException("Coloured code copying omitted Windows HTML Format.");
                var codeHtml = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(richCode));
                if (codeHtml.QuerySelector("pre code")?.TextContent != contextCode
                    || codeHtml.QuerySelector(".code-syntax-string")?.GetAttribute("style") != "color: #166534;")
                    throw new IOException("Actual Windows rich copying lost code text, indentation or syntax colours.");
                Find(window, "ExportMenu").Click(); Find(window, "ExportWord").Patterns.Invoke.Pattern.Invoke();
                var codeExport = Path.Combine(runRoot, "coloured-code.docx"); SaveThroughDialogue(codeExport, "Export DOCX");
                Wait(() => File.Exists(codeExport) && ExportCompleted(window, codeExport), "Coloured Word export did not complete.");
                using (var codePackage = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(codeExport, false))
                {
                    var paragraph = codePackage.MainDocumentPart!.Document!.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Single();
                    var stringRun = paragraph.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>().Single(run => run.InnerText.Contains("Café", StringComparison.Ordinal));
                    if (new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(codePackage).Any()
                        || NativeWordSyntaxChecks.ReadCode(paragraph) != contextCode || stringRun.RunProperties?.Color?.Val?.Value != "166534")
                        throw new IOException("The actual Word export lost editable code text, indentation or syntax colours.");
                }
                Find(window, "ExportMenu").Click(); Find(window, "ExportPdf").Patterns.Invoke.Pattern.Invoke();
                var codePdf = Path.Combine(runRoot, "coloured-code.pdf"); SaveThroughDialogue(codePdf, "Export PDF");
                Wait(() => File.Exists(codePdf) && ExportCompleted(window, codePdf), "Coloured PDF export did not complete.");
                using (var inspectedCode = UglyToad.PdfPig.PdfDocument.Open(codePdf))
                {
                    var selectable = string.Concat(inspectedCode.GetPages().SelectMany(page => page.Letters).Select(letter => letter.Value));
                    if (!selectable.Contains("Café😀🚀", StringComparison.Ordinal) || !selectable.Contains("keepIndent();", StringComparison.Ordinal)
                        || selectable.Contains('\0') || selectable.Contains('\uFFFD'))
                        throw new IOException("The actual PDF export lost selectable Unicode code.");
                }
                if (ReadEditorText(editor) != contextMarkdown || ReadObservedText(source) != codeSavedSource)
                    throw new IOException("Coloured code export changed editor or saved source.");
                checks.Add("Native rich copying and Word/PDF Export menus retain Unicode code while preserving editor and saved source");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Code context validation did not restore the original source.");
                checks.Add("Preview code exposes selectable syntax colours and context actions copy exact Unicode code and fenced Markdown through the Windows clipboard");
                const string decorationMarkdown = "# PDF decoration workflow\n\nBeforePlain ~~Removed Café😀~~ AfterPlain. ==Marked== remains selectable.\n\n[==MarkedLink==](https://example.org/decorated)";
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(decorationMarkdown); contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextPaste");
                Wait(() => ReadEditorText(editor) == decorationMarkdown
                    && Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("PDF decoration workflow", StringComparison.Ordinal),
                    "The decoration export fixture did not retain source and a current native preview.");
                window.CaptureToFile(Path.Combine(output, "native-pdf-decorations.png"));
                Find(window, "ExportMenu").Click(); Find(window, "ExportPdf").Patterns.Invoke.Pattern.Invoke();
                var decoratedPdf = Path.Combine(runRoot, "decorated-text.pdf"); SaveThroughDialogue(decoratedPdf, "Export PDF");
                Wait(() => File.Exists(decoratedPdf) && ExportCompleted(window, decoratedPdf), "Decorated PDF export did not complete.");
                using (var inspectedDecoration = UglyToad.PdfPig.PdfDocument.Open(decoratedPdf))
                {
                    var page = inspectedDecoration.GetPage(1); var words = page.GetWords().ToArray();
                    if (!NativePdfDecorationChecks.Struck(page, words.Single(word => word.Text == "Removed"))
                        || !NativePdfDecorationChecks.Highlighted(page, words.Single(word => word.Text == "Marked"))
                        || !string.Concat(page.Letters.Select(letter => letter.Value)).Contains("Café😀", StringComparison.Ordinal)
                        || !page.GetAnnotations().Select(annotation => annotation.Action).OfType<UglyToad.PdfPig.Actions.UriAction>()
                            .Any(action => action.Uri == "https://example.org/decorated"))
                        throw new IOException("The actual PDF export lost its visible decorations, selectable Unicode or working link.");
                }
                if (ReadEditorText(editor) != decorationMarkdown || ReadObservedText(source) != codeSavedSource)
                    throw new IOException("Decorated PDF export changed editor or saved source.");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == contextOriginal, "Decorated export damaged complete insertion Undo.");
                checks.Add("Native PDF Export retains actual strike-through/highlight geometry, selectable Unicode and hyperlinks while preserving source and complete Undo");
                const string tableMarkdown = "# Excluded introduction\n\n| Other | Table |\n|---|---|\n| Excluded | row |\n\n" +
                    "| Selected | Value |\n|---|---|\n| Native table Café | =1+1 |\n| Identifier | 000123 |\n\nExcluded conclusion.";
                var tableSavedSource = ReadObservedText(source);
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(tableMarkdown); contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == tableMarkdown, "The table context fixture did not paste.");
                Wait(() => Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Native table Café", StringComparison.Ordinal), "The table context fixture did not render.");
                OpenPreviewContext("Native table Café", "CopyPreviewTable").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name == "Table copied as formatted content and tab-separated text.", "The native table context command did not copy its table.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                var tableClipboardHtml = NativeUiClipboard.GetData(System.Windows.DataFormats.Html) as string ?? throw new IOException("Table copy did not publish Windows HTML Format.");
                var tableHtml = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(tableClipboardHtml));
                if (NativeUiClipboard.GetText() != "Selected\tValue\nNative table Café\t=1+1\nIdentifier\t000123"
                    || tableHtml.QuerySelectorAll("table").Length != 1 || !tableHtml.QuerySelector("table")!.HasAttribute("x:str")
                    || tableHtml.Body!.TextContent.Contains("Excluded", StringComparison.Ordinal)
                    || tableHtml.QuerySelectorAll("td,th").Any(cell => !(cell.GetAttribute("style") ?? "").Contains("mso-number-format", StringComparison.Ordinal)))
                    throw new IOException("Native table copy lost its selected-table boundary or literal-cell clipboard contract.");
                if (ReadObservedText(source) != tableSavedSource || editor.Patterns.Value.Pattern.Value.Value != tableMarkdown)
                    throw new IOException("Native table copying changed saved or edited source.");
                window.CaptureToFile(Path.Combine(output, "native-table-copy.png"));
                OpenPreviewContext("Native table Café", "ExportPreviewTableCsv").Patterns.Invoke.Pattern.Invoke();
                var csvPath = Path.Combine(runRoot, "fixture-table-2.csv");
                SaveThroughDialogue(csvPath, "Export table CSV", "fixture-table-2.csv");
                Wait(() => ExportCompleted(window, csvPath), "The native table CSV command did not finish.");
                if (!File.ReadAllBytes(csvPath).SequenceEqual(System.Text.Encoding.UTF8.GetBytes("Selected,Value\r\nNative table Café,=1+1\r\nIdentifier,000123"))
                    || ReadObservedText(source) != tableSavedSource || editor.Patterns.Value.Pattern.Value.Value != tableMarkdown)
                    throw new IOException("Native table CSV lost its selected cells, encoding or unchanged source.");
                checks.Add("Native table CSV context action and Save dialogue retain selected UTF-8 cells, table numbering and unchanged source");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Table context validation did not restore the original document.");
                checks.Add("Native table context copy isolates one table with literal-cell HTML and Unicode TSV without changing source");
                const string reviewSource = "# Review Café\n\n### Skipped\n\nColor and TODO remain.\n\n![](missing.png)\n\n[Missing](#absent)\n\n| A | B |\n|---|---|\n| One | Two | Extra |";
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(reviewSource); contextClipboardSequence = GetClipboardSequenceNumber(); EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == reviewSource, "The review fixture did not paste.");
                Find(window, "ViewMenu").Click(); Find(window, "ReviewDocument").Patterns.Invoke.Pattern.Invoke();
                Window? reviewWindow = null;
                Wait(() => (reviewWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Document review")))?.AsWindow()) is not null,
                    "The native document review did not open.");
                var reviewNotes = Find(reviewWindow!, "ReviewIssues").AsListBox();
                if (!Find(reviewWindow!, "ReviewResultStatus").Name.StartsWith("6 advisory notes.", StringComparison.Ordinal)
                    || !Find(reviewWindow!, "ReviewSummary").Name.Contains("2 headings", StringComparison.Ordinal))
                    throw new IOException("Native review did not retain the current document's metrics and six advisory notes.");
                FocusWindow(reviewWindow!); reviewNotes.Focus(); Keyboard.Type(VirtualKeyShort.END);
                Wait(() => reviewNotes.Items.Any(item => item.Name.Contains("Table row has 3 cells", StringComparison.Ordinal)), "Keyboard scrolling did not expose the table review note.");
                Keyboard.Type(VirtualKeyShort.HOME);
                Wait(() => reviewNotes.Items.Any(item => item.Name.Contains("TODO marker found", StringComparison.Ordinal)), "Keyboard scrolling did not return to the marker review note.");
                reviewNotes.Items.Single(item => item.Name.Contains("TODO marker found", StringComparison.Ordinal)).Select();
                reviewWindow!.CaptureToFile(Path.Combine(output, "native-document-review.png"));
                Find(reviewWindow!, "ReviewGoToSource").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name == "TODO marker found.", "Review did not navigate to the selected note.");
                EditorContext("EditorContextCopy");
                Wait(() => NativeUiClipboard.GetText() == "TODO", "Review navigation did not select the exact TODO marker.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                if (NativeUiClipboard.GetText() != "TODO" || editor.Patterns.Value.Pattern.Value.Value != reviewSource || ReadObservedText(source) != tableSavedSource)
                    throw new IOException("Review navigation lost the exact source selection or altered document content.");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Document review validation did not restore the original source.");
                checks.Add("Native document review uses current edits, shows scoped advisory notes and selects exact source without saving or changing text");
                var reviewTargetPath = Path.Combine(runRoot, "review-target.md"); File.WriteAllText(reviewTargetPath, "# Saved target");
                AutomationElement? reviewTargetItem = null;
                Wait(() => (reviewTargetItem = Find(window, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem).And(cf.ByName("review-target.md")))) is not null,
                    "The workspace review target did not appear in the native tree.");
                FocusWindow(window); reviewTargetItem!.Patterns.SelectionItem.Pattern.Select(); reviewTargetItem.Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == "# Saved target", "The workspace review target did not open.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText("# New target"); contextClipboardSequence = GetClipboardSequenceNumber(); EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == "# New target", "The unsaved target heading did not paste.");
                sourceTab.Patterns.SelectionItem.Pattern.Select();
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "The source tab did not return for workspace review.");
                const string workspaceReviewSource = "# Workspace review\n\n[New](review-target.md#new-target) [Old](review-target.md#saved-target)";
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(workspaceReviewSource); contextClipboardSequence = GetClipboardSequenceNumber(); EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == workspaceReviewSource, "The workspace review source did not paste.");
                Find(window, "ViewMenu").Click(); Find(window, "ReviewDocument").Patterns.Invoke.Pattern.Invoke(); reviewWindow = null;
                Wait(() => (reviewWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Document review")))?.AsWindow()) is not null,
                    "Workspace document review did not open.");
                if (!Find(reviewWindow!, "ReviewWorkspaceScope").Name.Contains("current open drafts", StringComparison.Ordinal)
                    || !Find(reviewWindow!, "ReviewResultStatus").Name.StartsWith("1 advisory note.", StringComparison.Ordinal))
                    throw new IOException("Workspace review did not use the unsaved target heading.");
                Find(reviewWindow!, "ReviewIssues").AsListBox().Items.Single(item => item.Name.Contains("linked section is missing", StringComparison.Ordinal)).Select();
                reviewWindow!.CaptureToFile(Path.Combine(output, "native-workspace-review.png"));
                Find(reviewWindow!, "ReviewGoToSource").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name == "The linked section is missing from its workspace document.", "Workspace review did not navigate to the selected link.");
                EditorContext("EditorContextCopy");
                Wait(() => NativeUiClipboard.GetText() == "[Old](review-target.md#saved-target)", "Workspace review did not select the exact stale link.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                if (ReadObservedText(reviewTargetPath) != "# Saved target" || ReadObservedText(source) != tableSavedSource || editor.Patterns.Value.Pattern.Value.Value != workspaceReviewSource)
                    throw new IOException("Workspace review changed saved or edited documents.");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Workspace review did not restore its source fixture.");
                reviewTargetItem = Find(window, "WorkspaceTree").FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem).And(cf.ByName("review-target.md")))
                    ?? throw new IOException("The workspace review target disappeared from the tree.");
                reviewTargetItem.Patterns.SelectionItem.Pattern.Select(); reviewTargetItem.Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == "# New target", "The target draft was lost during workspace review.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == "# Saved target", "Workspace review did not restore its target fixture.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W); sourceTab.Patterns.SelectionItem.Pattern.Select();
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Workspace review did not return to the original source tab.");
                checks.Add("Workspace review validates unsaved target headings and selects stale link source without writing either document");
                var figureReference = LensDocsStudio.Core.Parsing.MarkdownLinks.RelativeReference(source, renamedImage);
                var figureSource = "# Figure preview\n\n<figure data-image-figure>\n<img src=\"" + figureReference + "\" alt=\"Native figure pixels\">\n<figcaption>Figure 1 — Café &amp; evidence</figcaption>\n</figure>";
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(figureSource); contextClipboardSequence = GetClipboardSequenceNumber();
                EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == figureSource, "The figure source did not reach the native editor.");
                Wait(() => Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Figure 1 — Café & evidence", StringComparison.Ordinal),
                    "The native figure caption did not become selectable preview text.");
                Find(window, "EditMenu").Click(); Find(window, "CopyRenderedContent").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "Status").Name.StartsWith("Rendered content copied as HTML", StringComparison.Ordinal), "Figure clipboard preparation did not finish.");
                contextClipboardSequence = GetClipboardSequenceNumber();
                var figureClipboard = NativeUiClipboard.GetDataObject()!;
                var figureHtml = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(
                    figureClipboard.GetData(System.Windows.DataFormats.Html) as string ?? throw new IOException("Figure copying omitted native HTML.")));
                var figureElement = figureHtml.QuerySelector("figure") ?? throw new IOException("Figure copying omitted its semantic container.");
                var figureImageUri = figureElement.QuerySelector("img")?.GetAttribute("src") ?? "";
                if (figureElement.QuerySelector("figcaption")?.TextContent != "Figure 1 — Café & evidence"
                    || !figureImageUri.StartsWith("data:image/png;base64,", StringComparison.Ordinal)
                    || !(figureClipboard.GetData(System.Windows.DataFormats.UnicodeText) as string ?? "").Contains("[Native figure pixels]\nFigure 1 — Café & evidence", StringComparison.Ordinal))
                    throw new IOException("Figure copying lost its caption, image or Unicode text.");
                var figureBytes = Convert.FromBase64String(figureImageUri["data:image/png;base64,".Length..]);
                using (var figureStream = new MemoryStream(figureBytes))
                using (var expectedStream = File.OpenRead(renamedImage))
                {
                    var actualFigure = new System.Windows.Media.Imaging.FormatConvertedBitmap(LensDocsStudio.Native.Platform.NativeRasterService.Read(figureStream), System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    var expectedFigure = new System.Windows.Media.Imaging.FormatConvertedBitmap(LensDocsStudio.Native.Platform.NativeRasterService.Read(expectedStream), System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    var actualData = new byte[actualFigure.PixelWidth * actualFigure.PixelHeight * 4]; var expectedData = new byte[expectedFigure.PixelWidth * expectedFigure.PixelHeight * 4];
                    actualFigure.CopyPixels(actualData, actualFigure.PixelWidth * 4, 0); expectedFigure.CopyPixels(expectedData, expectedFigure.PixelWidth * 4, 0);
                    if (actualFigure.PixelWidth != expectedFigure.PixelWidth || actualFigure.PixelHeight != expectedFigure.PixelHeight || !actualData.SequenceEqual(expectedData))
                        throw new IOException("Native figure copying changed its pixels.");
                }
                if (ReadObservedText(source) != tableSavedSource || editor.Patterns.Value.Pattern.Value.Value != figureSource)
                    throw new IOException("Figure preview or copying changed saved or edited source.");
                window.CaptureToFile(Path.Combine(output, "native-image-figure.png"));
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Figure validation did not restore the original document.");
                checks.Add("Native image figures expose selectable captions and copy real pixels with semantic HTML and unchanged source");
                var figureEditOriginal = "# Figure editing\n\n![Original](<" + figureReference + "> \"Retained image title\")";
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(figureEditOriginal); contextClipboardSequence = GetClipboardSequenceNumber(); EditorContext("EditorContextPaste");
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == figureEditOriginal, "The standalone image fixture did not paste.");
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                Find(window, "InsertMenu").Click(); Find(window, "EditImageFigure").Patterns.Invoke.Pattern.Invoke();
                Window? figureWindow = null;
                Wait(() => (figureWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Image figure")))?.AsWindow()) is not null,
                    "The native image figure editor did not open for a standalone image.");
                if (Find(figureWindow!, "FigureAlternativeText").AsTextBox().Text != "Original" || Find(figureWindow!, "FigureCaption").AsTextBox().Text != "")
                    throw new IOException("The image figure dialogue lost the original description.");
                Find(figureWindow!, "FigureAlternativeText").AsTextBox().Text = "Edited image description";
                Find(figureWindow!, "FigureCaption").AsTextBox().Text = "Caption Café & evidence";
                figureWindow!.CaptureToFile(Path.Combine(output, "native-figure-dialogue.png"));
                Find(figureWindow!, "ApplyImageFigure").AsButton().Invoke();
                Wait(() => Find(window, "Status").Name == "Image figure updated." && new LensDocsStudio.Core.Parsing.MarkdownService().Parse(editor.Patterns.Value.Pattern.Value.Value)
                    .Syntax.OfType<LensDocsStudio.Core.Parsing.DocumentImageFigureBlock>().SingleOrDefault()?.Caption == "Caption Café & evidence",
                    "The figure dialogue did not apply the caption.");
                var convertedFigure = ReadEditorText(editor);
                Find(window, "InsertMenu").Click(); Find(window, "EditImageFigure").Patterns.Invoke.Pattern.Invoke();
                Wait(() => (figureWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Image figure")))?.AsWindow()) is not null,
                    "The existing figure did not reopen for editing.");
                if (Find(figureWindow!, "FigureAlternativeText").AsTextBox().Text != "Edited image description" || Find(figureWindow!, "FigureCaption").AsTextBox().Text != "Caption Café & evidence")
                    throw new IOException("The image figure editor did not restore its current values.");
                Find(figureWindow!, "FigureCaption").AsTextBox().Text = "Updated caption";
                Find(figureWindow!, "ApplyImageFigure").AsButton().Invoke();
                Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("<figcaption>Updated caption</figcaption>", StringComparison.Ordinal), "Caption editing did not replace the current figure.");
                var updatedFigureSource = ReadEditorText(editor);
                var editedFigure = new LensDocsStudio.Core.Parsing.MarkdownService().Parse(updatedFigureSource).Syntax.OfType<LensDocsStudio.Core.Parsing.DocumentImageFigureBlock>().Single();
                if (editedFigure.Image.Url != figureReference || editedFigure.Image.Title != "Retained image title" || ReadObservedText(source) != tableSavedSource)
                    throw new IOException("Figure caption editing changed its destination, title or saved source.");
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == convertedFigure, "Caption edit Undo did not restore the previous figure.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == updatedFigureSource, "Caption edit Redo did not restore the edited figure.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == convertedFigure, "Caption edit did not restore before conversion Undo.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == figureEditOriginal, "Figure conversion Undo lost the original Markdown image.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == contextOriginal, "Figure dialogue checks did not restore the original source.");
                checks.Add("Native image figure dialogue converts existing images, restores editable captions and retains destination/title through complete Undo/Redo");
            }
            finally
            {
                if (contextClipboardSequence != 0 && GetClipboardSequenceNumber() == contextClipboardSequence)
                { if (contextClipboard is null) NativeUiClipboard.Clear(); else NativeUiClipboard.SetDataObject(contextClipboard, true); }
            }
            var sourceBeforeLibraryImage = ReadEditorText(editor);
            var libraryImageReference = LensDocsStudio.Core.Parsing.MarkdownLinks.RelativeReference(source, renamedImage);
            var libraryImageMarkdown = "![Library portable image](" + libraryImageReference + ")";
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            var beforeLibraryClipboard = NativeUiClipboard.GetDataObject(); uint libraryClipboardSequence = 0;
            try
            {
                NativeUiClipboard.SetText("\n\n" + libraryImageMarkdown); libraryClipboardSequence = GetClipboardSequenceNumber();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage + "\n\n" + libraryImageMarkdown,
                    "The portable-image fixture did not reach the document before library capture.");
            }
            finally
            {
                if (libraryClipboardSequence != 0 && GetClipboardSequenceNumber() == libraryClipboardSequence)
                { if (beforeLibraryClipboard is null) NativeUiClipboard.Clear(); else NativeUiClipboard.SetDataObject(beforeLibraryClipboard, true); }
            }
            var templateSource = ReadEditorText(editor); var templateSavedSource = ReadObservedText(source);
            var templatePixels = File.ReadAllBytes(renamedImage); var templateBytes = NativeWordTemplateChecks.Create(templatePixels, attachedTemplate: true);
            var templatePath = Path.Combine(runRoot, "native-template.docx"); File.WriteAllBytes(templatePath, templateBytes);
            Find(window, "ExportMenu").Click(); Find(window, "SelectWordTemplate").Patterns.Invoke.Pattern.Invoke();
            OpenThroughDialogue(templatePath, "Select Word template");
            Wait(() => Find(window, "Status").Name.StartsWith("Word template selected: native-template.docx", StringComparison.Ordinal), "The native Word template did not validate and select.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportWord").Patterns.Invoke.Pattern.Invoke();
            var templateExport = Path.Combine(runRoot, "template-export.docx"); SaveThroughDialogue(templateExport, "Export DOCX");
            Wait(() => File.Exists(templateExport) && ExportCompleted(window, templateExport), "Word export with the selected native template did not complete.");
            NativeWordTemplateChecks.Check(File.ReadAllBytes(templateExport), templatePixels);
            Find(window, "ExportMenu").Click(); Find(window, "ClearWordTemplate").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(window, "Status").Name == "Default Word styles selected", "The native Word template did not clear.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportWord").Patterns.Invoke.Pattern.Invoke();
            var defaultExport = Path.Combine(runRoot, "default-export.docx"); SaveThroughDialogue(defaultExport, "Export DOCX");
            Wait(() => File.Exists(defaultExport) && ExportCompleted(window, defaultExport), "Default Word export did not complete after clearing the template.");
            using (var defaultPackage = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(defaultExport, false))
            {
                if (defaultPackage.MainDocumentPart!.HeaderParts.Any() || defaultPackage.MainDocumentPart.FooterParts.Any()
                    || defaultPackage.MainDocumentPart.StyleDefinitionsPart!.Styles!.Elements<DocumentFormat.OpenXml.Wordprocessing.Style>().Any(style => style.StyleId == "NativeHeading")
                    || new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(defaultPackage).Any())
                    throw new IOException("Clearing the native Word template retained its formatting or produced an invalid package.");
            }
            if (editor.Patterns.Value.Pattern.Value.Value != templateSource || ReadObservedText(source) != templateSavedSource || !File.ReadAllBytes(templatePath).SequenceEqual(templateBytes))
                throw new IOException("Template selection or Word export changed original source files.");
            checks.Add("Native Word template selection and export preserve heading styles, header/footer/logo and source files while excluding private template content; clearing restores default output");
            var libraryWindow = OpenTemplateLibrary(window);
            if (Find(libraryWindow, "UseLibraryTemplate").IsEnabled) throw new IOException("An empty Word template library enabled selection.");
            if (Find(libraryWindow, "WordTemplateEmptyState").IsOffscreen || !Find(libraryWindow, "ImportEmptyLibraryTemplate").IsEnabled)
                throw new IOException("The native empty template library has no visible primary import action.");
            libraryWindow.CaptureToFile(Path.Combine(output, "native-refinement-template-empty.png"));
            Find(libraryWindow, "ImportLibraryTemplate").AsButton().Invoke();
            OpenThroughDialogue(templatePath, "Import Word template to library");
            NameTemplate("Name Word template", "native-template", "Reusable Café");
            Wait(() => Find(libraryWindow, "WordTemplateStatus").Name.StartsWith("Imported Reusable Café.", StringComparison.Ordinal)
                && Find(libraryWindow, "UseLibraryTemplate").IsEnabled, "The native library did not import reusable template formatting.");
            Wait(() => Find(libraryWindow, "WordTemplateFormatting").Name.Contains("Headers: 1 · footers: 1", StringComparison.Ordinal)
                && Find(libraryWindow, "WordTemplateFormatting").Name.Contains("Embedded header/footer images: 1", StringComparison.Ordinal),
                "The imported template did not expose its retained formatting and embedded resources.");
            if (!Find(libraryWindow, "WordTemplateStatus").Name.Contains(LensDocsStudio.Core.Exports.DocxTemplateService.AttachedTemplateWarning, StringComparison.Ordinal))
                throw new IOException("The attached-template import did not show its safe omission warning.");
            libraryWindow.CaptureToFile(Path.Combine(output, "native-refinement-template-import-warning.png"));
            Find(libraryWindow, "RenameLibraryTemplate").AsButton().Invoke();
            NameTemplate("Rename Word template", "Reusable Café", "Renamed Café handbook");
            Wait(() => Find(libraryWindow, "WordTemplateStatus").Name == "Template renamed.", "The library rename did not complete.");
            Find(libraryWindow, "CopyLibraryTemplate").AsButton().Invoke();
            var libraryCopy = Path.Combine(runRoot, "template-library-copy.docx"); SaveThroughDialogue(libraryCopy, "Save Word template copy");
            Wait(() => File.Exists(libraryCopy) && Find(libraryWindow, "WordTemplateStatus").Name == "Saved reusable formatting to template-library-copy.docx.",
                "The native library did not save a reusable copy.");
            var copyContent = new LensDocsStudio.Core.Exports.ExportContent(new LensDocsStudio.Core.Parsing.MarkdownService().Parse("# Reusable template copy"),
                new Dictionary<int, LensDocsStudio.Core.Exports.ExportImage>(), new Dictionary<string, LensDocsStudio.Core.Exports.ExportImage>());
            NativeWordTemplateChecks.Check(new LensDocsStudio.Core.Exports.DocxExporter().Export(copyContent, File.ReadAllBytes(libraryCopy)), templatePixels);
            Find(libraryWindow, "ArchiveLibraryTemplate").AsButton().Invoke();
            Wait(() => Find(libraryWindow, "WordTemplateStatus").Name.StartsWith("Template archived.", StringComparison.Ordinal)
                && !Find(libraryWindow, "UseLibraryTemplate").IsEnabled, "Archiving did not remove the template from the active library.");
            Find(libraryWindow, "ShowArchivedTemplates").Patterns.Toggle.Pattern.Toggle();
            Wait(() => Find(libraryWindow, "ArchiveLibraryTemplate").Name == "Restore" && Find(libraryWindow, "ArchiveLibraryTemplate").IsEnabled,
                "The archived template did not become available for restoration.");
            if (Find(libraryWindow, "UseLibraryTemplate").IsEnabled) throw new IOException("An archived template was available for active selection.");
            Find(libraryWindow, "ArchiveLibraryTemplate").AsButton().Invoke();
            Wait(() => Find(libraryWindow, "WordTemplateStatus").Name == "Template restored to the active library.", "The archived template did not restore.");
            Find(libraryWindow, "ShowArchivedTemplates").Patterns.Toggle.Pattern.Toggle();
            Wait(() => Find(libraryWindow, "ArchiveLibraryTemplate").Name == "Archive" && Find(libraryWindow, "UseLibraryTemplate").IsEnabled,
                "The restored template did not return to the active library.");
            libraryWindow.CaptureToFile(Path.Combine(output, "native-template-library.png"));
            Find(libraryWindow, "UseLibraryTemplate").AsButton().Invoke();
            Wait(() => Find(window, "Status").Name == "Word template selected: Renamed Café handbook", "The stored library template was not selected.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportWord").Patterns.Invoke.Pattern.Invoke();
            var libraryExport = Path.Combine(runRoot, "library-export.docx"); SaveThroughDialogue(libraryExport, "Export DOCX");
            Wait(() => File.Exists(libraryExport) && ExportCompleted(window, libraryExport), "Word export did not use the library template.");
            NativeWordTemplateChecks.Check(File.ReadAllBytes(libraryExport), templatePixels);
            if (editor.Patterns.Value.Pattern.Value.Value != templateSource || ReadObservedText(source) != templateSavedSource || !File.ReadAllBytes(templatePath).SequenceEqual(templateBytes))
                throw new IOException("The Word template library changed source documents or the imported original.");
            Find(window, "FileMenu").Click(); Find(window, "SaveDocumentTemplate").Patterns.Invoke.Pattern.Invoke();
            NameLibraryEntry("Save document template", "Template name", "Native reusable source");
            Wait(() => Find(window, "Status").Name == "Saved template: Native reusable source", "The current native document did not save to the content library.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Find(window, "FileMenu").Click(); Find(window, "SaveMarkdownSnippet").Patterns.Invoke.Pattern.Invoke();
            NameLibraryEntry("Save Markdown snippet", "Snippet name", "Reusable selected source");
            Wait(() => Find(window, "Status").Name == "Saved snippet: Reusable selected source", "The selected Markdown did not save as a reusable snippet.");
            var contentLibrary = OpenContentLibrary(window);
            var browserLibraryBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-local-library.json"));
            var browserLibraryPath = Path.Combine(runRoot, "browser-library.json"); File.WriteAllBytes(browserLibraryPath, browserLibraryBytes);
            Find(contentLibrary, "ImportContentLibrary").AsButton().Invoke(); OpenThroughDialogue(browserLibraryPath, "Import content library");
            Wait(() => Find(contentLibrary, "ContentLibraryStatus").Name.StartsWith("Imported 3 entries;", StringComparison.Ordinal), "The browser library did not merge through the native import dialogue.");
            Find(contentLibrary, "ContentLibraryFilter").AsTextBox().Text = "Café handbook";
            Wait(() => Find(contentLibrary, "UseContentEntry").IsEnabled, "The imported template did not appear in the native library.");
            Find(contentLibrary, "RenameContentEntry").AsButton().Invoke(); NameLibraryEntry("Rename library entry", "Library name", "Migrated Café handbook");
            Wait(() => Find(contentLibrary, "ContentLibraryStatus").Name == "Library entry renamed.", "The imported content entry did not rename.");
            Find(contentLibrary, "ArchiveContentEntry").AsButton().Invoke();
            Wait(() => Find(contentLibrary, "ContentLibraryStatus").Name.StartsWith("Entry archived.", StringComparison.Ordinal), "The imported content entry did not archive.");
            Find(contentLibrary, "ArchivedContentLibrary").Patterns.Toggle.Pattern.Toggle();
            Wait(() => Find(contentLibrary, "ArchiveContentEntry").Name == "Restore" && Find(contentLibrary, "ArchiveContentEntry").IsEnabled,
                "The archived content entry was not available for restoration.");
            Find(contentLibrary, "ArchiveContentEntry").AsButton().Invoke();
            Wait(() => Find(contentLibrary, "ContentLibraryStatus").Name == "Entry restored to the active library.", "The archived content entry did not restore.");
            Find(contentLibrary, "ArchivedContentLibrary").Patterns.Toggle.Pattern.Toggle();
            Find(contentLibrary, "ContentLibraryFilter").AsTextBox().Text = "";
            Find(contentLibrary, "ExportContentLibrary").AsButton().Invoke();
            var contentExport = Path.Combine(runRoot, "exported-content-library.json"); SaveThroughDialogue(contentExport, "Export content library");
            Wait(() => File.Exists(contentExport) && Find(contentLibrary, "ContentLibraryStatus").Name.StartsWith("Exported all active", StringComparison.Ordinal),
                "The native content library did not export its active entries.");
            var exportedLibrary = LensDocsStudio.Core.State.LocalLibraryFormat.Read(File.ReadAllBytes(contentExport));
            if (exportedLibrary.Templates.Length != 2 || exportedLibrary.Snippets.Length != 2 || exportedLibrary.Profiles.Length != 1)
                throw new IOException($"The library export lost merged entries: {exportedLibrary.Templates.Length} templates, {exportedLibrary.Snippets.Length} snippets, {exportedLibrary.Profiles.Length} profiles.");
            var capturedTemplate = exportedLibrary.Templates.Single(item => item.Label == "Native reusable source").Content;
            if (!capturedTemplate.Contains("data:image/png;base64,", StringComparison.Ordinal)) throw new IOException("The library export lost the fixture's embedded image.");
            if (!capturedTemplate.Contains("```mermaid", StringComparison.Ordinal)) throw new IOException("The library export lost editable Mermaid.");
            if (exportedLibrary.Snippets.Single(item => item.Label == "Reusable selected source").Content != capturedTemplate)
                throw new IOException("Saving the selected source as a snippet did not retain the same portable content as its template.");
            Find(contentLibrary, "ContentLibraryFilter").AsTextBox().Text = "Native reusable source";
            Wait(() => Find(contentLibrary, "UseContentEntry").IsEnabled, "The saved native template was not selectable.");
            contentLibrary.CaptureToFile(Path.Combine(output, "native-content-library.png"));
            Find(contentLibrary, "UseContentEntry").AsButton().Invoke();
            Wait(() => Find(window, "Status").Name.StartsWith("Opened a new document from Native reusable source.", StringComparison.Ordinal), "The template did not create a new native document.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            var createdFromLibrary = Path.Combine(runRoot, "Library created.md"); SaveThroughDialogue(createdFromLibrary);
            Wait(() => File.Exists(createdFromLibrary), "The template-created document did not save.");
            var createdSource = ReadObservedText(createdFromLibrary);
            if (!createdSource.Contains("Native UI validation — Café", StringComparison.Ordinal) || !createdSource.Contains("```mermaid", StringComparison.Ordinal)
                || createdSource.Contains("data:image/", StringComparison.Ordinal)) throw new IOException("The template-created document lost editable source or failed to materialise images.");
            var createdImages = LensDocsStudio.Core.Parsing.MarkdownLinks.Read(createdSource).Where(link => link.IsImage).ToArray();
            if (createdImages.Length == 0 || createdImages.Any(link => !File.Exists(new LensDocsStudio.Core.Workspace.WorkspacePath(runRoot).ResolveLink(createdFromLibrary, link.Reference))))
                throw new IOException("The library-created document has missing portable image assets.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.LEFT);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == templateSource, "Template creation or saving lost the previous document navigation state.");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            contentLibrary = OpenContentLibrary(window); Find(contentLibrary, "ContentLibraryCategory").AsComboBox().Select("Markdown snippets");
            Find(contentLibrary, "ContentLibraryCategory").AsComboBox().Collapse();
            Find(contentLibrary, "ContentLibraryFilter").AsTextBox().Text = "Reusable selected source";
            Find(contentLibrary, "UseContentEntry").AsButton().Invoke();
            Wait(() => Find(window, "Status").Name == "Inserted snippet: Reusable selected source", "The saved snippet did not insert into its original document.");
            var withSnippet = ReadEditorText(editor);
            if (withSnippet == templateSource) throw new IOException("Snippet insertion did not change editor content.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == templateSource, "One Undo did not restore source before snippet insertion.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == withSnippet, "Snippet Redo did not restore the whole insertion.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == templateSource, "Snippet validation did not restore the original source.");
            var preferencesBeforeProfile = File.ReadAllBytes(Path.Combine(state, "preferences.json"));
            contentLibrary = OpenContentLibrary(window); Find(contentLibrary, "ContentLibraryCategory").AsComboBox().Select("Export profiles");
            Find(contentLibrary, "ContentLibraryCategory").AsComboBox().Collapse(); Find(contentLibrary, "UseContentEntry").AsButton().Invoke();
            Wait(() => Find(window, "Status").Name == "Applied export profile: Team documentation. Current session only.", "The imported export profile did not apply.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportDocsSite").Patterns.Invoke.Pattern.Invoke();
            Window? profileSite = null;
            Wait(() => (profileSite = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Export Docs Site")))?.AsWindow()) is not null,
                "The profiled Docs Site dialogue did not open.");
            if (Find(profileSite!, "DocsSiteTitle").AsTextBox().Text != "Café documentation"
                || profileSite!.FindFirstDescendant(cf => cf.ByName("Site description"))!.AsTextBox().Text != "A portable team handbook.")
                throw new IOException("The imported profile did not supply its Docs Site defaults.");
            FocusWindow(profileSite!); Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Export Docs Site"))) is null,
                "The profiled Docs Site dialogue did not cancel.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportMarkdownBundle").Patterns.Invoke.Pattern.Invoke();
            var profiledBundle = Path.Combine(runRoot, "profile-bundle.zip"); SaveThroughDialogue(profiledBundle, "Export Markdown bundle");
            Wait(() => File.Exists(profiledBundle) && ExportCompleted(window, profiledBundle)
                && Find(window, "Status").Name.EndsWith("Azure DevOps Mermaid formatting applied.", StringComparison.Ordinal),
                "The selected profile did not affect the actual Markdown bundle.");
            using (var archive = System.IO.Compression.ZipFile.OpenRead(profiledBundle))
            using (var entry = new StreamReader(archive.GetEntry("fixture.md")!.Open()))
                if (entry.ReadToEnd() != LensDocsStudio.Core.Exports.DevOpsMarkdownExporter.Convert(templateSource, "fixture.md"))
                    throw new IOException("The profiled bundle did not retain the exact expected current Markdown source.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportProfilesMenu").Click(); Find(window, "SaveExportProfile").Patterns.Invoke.Pattern.Invoke();
            NameLibraryEntry("Save export profile", "Profile name", "Native saved profile");
            Wait(() => Find(window, "Status").Name == "Saved export profile: Native saved profile", "The native export profile did not save to the library.");
            Find(window, "ExportMenu").Click(); Find(window, "ExportProfilesMenu").Click(); Find(window, "GenericExportProfile").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(window, "Status").Name == "Applied export profile: Generic documentation. Current session only.", "The generic export profile did not reset session overrides.");
            if (!File.ReadAllBytes(Path.Combine(state, "preferences.json")).SequenceEqual(preferencesBeforeProfile) || ReadObservedText(source) != templateSavedSource
                || editor.Patterns.Value.Pattern.Value.Value != templateSource || !File.ReadAllBytes(browserLibraryPath).SequenceEqual(browserLibraryBytes))
                throw new IOException("Library or profile actions changed persisted preferences, original JSON or source documents.");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "One Undo did not restore source before the library fixture image paste.");
            if (editor.Patterns.Value.Pattern.Value.Value != sourceBeforeLibraryImage || ReadObservedText(source) != templateSavedSource)
                throw new IOException("The library fixture did not restore the original editor and saved source before shutdown.");
            Window OpenCatalogue(bool snippets = false)
            {
                Find(window, snippets ? "InsertMenu" : "FileMenu").Click();
                Find(window, snippets ? "BrowseSnippetCatalogue" : "BrowseContentCatalogue").Patterns.Invoke.Pattern.Invoke();
                Window? catalogue = null;
                Wait(() => (catalogue = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId)
                    .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)).And(cf.ByName("Templates and snippets")))?.AsWindow()) is not null,
                    "The native content catalogue did not open.");
                return catalogue!;
            }
            var catalogueWindow = OpenCatalogue();
            Find(catalogueWindow, "CatalogueFilter").AsTextBox().Text = "Customer Release Notes";
            Find(catalogueWindow, "CatalogueField-version").AsTextBox().Text = "v9.7.0";
            Find(catalogueWindow, "CatalogueField-date").AsTextBox().Text = "2026-10-12";
            var releaseEntry = LensDocsStudio.Core.Content.BuiltInContentCatalogue.Entries.Single(entry => entry.Id == "release/customer");
            var generatedRelease = LensDocsStudio.Core.Content.BuiltInContentCatalogue.Generate(releaseEntry,
                new Dictionary<string, string> { ["version"] = "v9.7.0", ["date"] = "2026-10-12" });
            Wait(() => Find(catalogueWindow, "CataloguePreview").AsTextBox().Text == generatedRelease.Content,
                "The native generator did not update its source preview from edited fields.");
            catalogueWindow.CaptureToFile(Path.Combine(output, "native-content-catalogue.png"));
            Find(catalogueWindow, "UseCatalogueEntry").AsButton().Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == generatedRelease.Content, "The built-in generator did not create its exact editable source.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            var generatedReleasePath = Path.Combine(runRoot, "Generated release.md"); SaveThroughDialogue(generatedReleasePath);
            Wait(() => File.Exists(generatedReleasePath) && ReadObservedText(generatedReleasePath) == generatedRelease.Content,
                "The generated document did not save its editable source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.LEFT);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "Generator creation changed the previous document.");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            catalogueWindow = OpenCatalogue(true);
            Find(catalogueWindow, "CatalogueCategory").AsComboBox().Select("Requirements"); Find(catalogueWindow, "CatalogueCategory").AsComboBox().Collapse();
            Find(catalogueWindow, "CatalogueFilter").AsTextBox().Text = "Risks";
            Find(catalogueWindow, "UseCatalogueEntry").AsButton().Invoke();
            Wait(() => Find(window, "Status").Name == "Inserted snippet: Risks", "The built-in requirements snippet did not insert.");
            var withBuiltInSnippet = ReadEditorText(editor);
            if (!withBuiltInSnippet.Contains("| Risk | Likelihood | Impact | Mitigation |", StringComparison.Ordinal)) throw new IOException("The built-in snippet lost its editable table.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "Built-in snippet Undo lost the original document.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == withBuiltInSnippet, "Built-in snippet Redo lost its complete source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "The built-in snippet fixture did not restore original source.");
            catalogueWindow = OpenCatalogue();
            Find(catalogueWindow, "CatalogueCategory").AsComboBox().Select("Mermaid"); Find(catalogueWindow, "CatalogueCategory").AsComboBox().Collapse();
            Find(catalogueWindow, "CatalogueFilter").AsTextBox().Text = "Sequence";
            var generatedSequence = LensDocsStudio.Core.Content.BuiltInContentCatalogue.Generate(
                LensDocsStudio.Core.Content.BuiltInContentCatalogue.Entries.Single(entry => entry.Id == "mermaid/sequence"));
            Find(catalogueWindow, "UseCatalogueEntry").AsButton().Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == generatedSequence.Content, "The Mermaid template did not create standalone diagram source.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            var generatedDiagramPath = Path.Combine(runRoot, "Generated sequence.mmd"); SaveThroughDialogue(generatedDiagramPath);
            Wait(() => File.Exists(generatedDiagramPath) && ReadObservedText(generatedDiagramPath) == generatedSequence.Content,
                "The generated Mermaid document did not save its standalone diagram type.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.LEFT);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "Mermaid template creation changed the original document.");
            if (ReadObservedText(source) != templateSavedSource) throw new IOException("The built-in catalogue changed the original saved source.");
            checks.Add("Native catalogue filters templates/snippets, generates and saves field-edited Markdown and standalone Mermaid in new tabs, and inserts snippets with complete Undo/Redo while retaining original source");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            var detailsWindow = OpenDetails(window);
            Find(detailsWindow, "DetailsSummary").AsTextBox().Text = "Supporting evidence — Café";
            const string detailsBody = "## Hidden native heading\n\nRetained **bold** and [local guide](Second%20document.md).\n\n<details>\n<summary>Nested evidence</summary>\n\nNative-details-needle-042.\n</details>";
            Find(detailsWindow, "DetailsBody").AsTextBox().Text = detailsBody;
            detailsWindow.CaptureToFile(Path.Combine(output, "native-details-dialogue.png"));
            Find(detailsWindow, "ApplyDetails").AsButton().Invoke();
            var encodedDetailsSummary = System.Net.WebUtility.HtmlEncode("Supporting evidence — Café");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains(encodedDetailsSummary, StringComparison.Ordinal)
                && Find(window, "Status").Name == "Details section applied.", "The details dialogue did not insert its section.");
            var insertedDetails = ReadEditorText(editor);
            if (!insertedDetails.StartsWith(sourceBeforeLibraryImage, StringComparison.Ordinal)) throw new IOException("Details insertion lost preceding source.");
            var detailsStart = insertedDetails.IndexOf("<details data-details-block", sourceBeforeLibraryImage.Length, StringComparison.Ordinal);
            void WaitDetailsPreview(string summary)
                => Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DetailsToggle" + detailsStart))?.Name == summary,
                    "The native details preview did not finish rendering: " + summary);
            WaitDetailsPreview("Supporting evidence — Café");
            detailsWindow = OpenDetails(window);
            if (Find(detailsWindow, "DetailsSummary").AsTextBox().Text != "Supporting evidence — Café"
                || Find(detailsWindow, "DetailsBody").AsTextBox().Text.Replace("\r\n", "\n") != detailsBody)
                throw new IOException("The details dialogue did not restore the existing summary and Markdown body.");
            Find(detailsWindow, "DetailsSummary").AsTextBox().Text = "Updated supporting evidence";
            Find(detailsWindow, "ApplyDetails").AsButton().Invoke();
            var updatedDetails = insertedDetails.Replace(encodedDetailsSummary, "Updated supporting evidence", StringComparison.Ordinal);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == updatedDetails, "Editing a details summary changed its Markdown body or neighbouring source.");
            WaitDetailsPreview("Updated supporting evidence");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedDetails, "One Undo did not restore the complete details edit.");
            WaitDetailsPreview("Supporting evidence — Café");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == updatedDetails, "Details edit Redo did not restore the entire section.");
            WaitDetailsPreview("Updated supporting evidence");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedDetails, "Details edit Undo failed before preview checks.");
            WaitDetailsPreview("Supporting evidence — Café");
            var detailsToggle = Find(window, "DetailsToggle" + detailsStart);
            Wait(() => detailsToggle.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == FlaUI.Core.Definitions.ExpandCollapseState.Collapsed,
                "A new details section did not start collapsed.");
            if (detailsToggle.Name != "Supporting evidence — Café") throw new IOException("The native details expander lost its accessible summary.");
            var detailsHeader = detailsToggle.FindFirstDescendant(cf => cf.ByName("Supporting evidence — Café"))
                ?? throw new IOException("The native details header is unavailable to keyboard navigation.");
            FocusWindow(window); detailsHeader.Focus();
            Wait(() => detailsHeader.Properties.HasKeyboardFocus.Value, "The native details header did not acquire keyboard focus.");
            Keyboard.Type(VirtualKeyShort.SPACE);
            Wait(() => detailsToggle.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == FlaUI.Core.Definitions.ExpandCollapseState.Expanded,
                "Space did not expand the focused native details header.");
            Keyboard.Type(VirtualKeyShort.SPACE);
            Wait(() => detailsToggle.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == FlaUI.Core.Definitions.ExpandCollapseState.Collapsed,
                "Space did not collapse the focused native details header.");
            if (!detailsHeader.Properties.HasKeyboardFocus.Value || ReadEditorText(editor) != insertedDetails)
                throw new IOException("Toggling details with Space lost header focus or changed Markdown source.");
            File.WriteAllText(Path.Combine(output, "native-details-keyboard.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Summary = detailsToggle.Name, HeaderFocused = detailsHeader.Properties.HasKeyboardFocus.Value,
                KeyboardExpands = true, KeyboardCollapses = true, SourcePreserved = true
            }, new JsonSerializerOptions { WriteIndented = true }));
            detailsToggle.Patterns.ExpandCollapse.Pattern.Expand();
            try
            {
                Wait(() => Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Hidden native heading", StringComparison.Ordinal),
                    "Expanding details did not reveal its heading.");
            }
            catch
            {
                File.WriteAllText(Path.Combine(output, "native-details-automation.json"), JsonSerializer.Serialize(new
                {
                    Summary = Find(window, "DetailsToggle" + detailsStart).Name,
                    State = Find(window, "DetailsToggle" + detailsStart).Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.ToString(),
                    PreviewText = Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetText(-1),
                    EditorSource = editor.Patterns.Value.Pattern.Value.Value,
                    PreviewStatus = Find(window, "PreviewStatus").Name
                }, new JsonSerializerOptions { WriteIndented = true }));
                throw;
            }
            var nestedDetailsStart = insertedDetails.IndexOf("<details>", detailsStart, StringComparison.Ordinal);
            var nestedDetailsToggle = Find(window, "DetailsToggle" + nestedDetailsStart);
            if (nestedDetailsToggle.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != FlaUI.Core.Definitions.ExpandCollapseState.Collapsed)
                throw new IOException("Expanding the outer section changed its nested section state.");
            detailsToggle.Patterns.ExpandCollapse.Pattern.Collapse();
            preview = Find(window, "DocumentPreview"); preview.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(window, "FindContext").Name == "Find in preview", "Details search did not target the focused preview.");
            Find(window, "FindText").AsTextBox().Text = "Native-details-needle-042"; Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => Find(window, "DocumentPreview").Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Native-details-needle-042"),
                "Preview Find did not reveal and select text inside nested closed details.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait(() =>
            {
                var previewElement = Find(window, "DocumentPreview"); var viewport = previewElement.BoundingRectangle;
                return previewElement.Patterns.Text.Pattern.GetSelection().SelectMany(range => range.GetBoundingRectangles())
                    .Any(rectangle => rectangle.Width > 0 && rectangle.Height > 0 && rectangle.Top >= viewport.Top && rectangle.Bottom <= viewport.Bottom);
            }, "The selected hidden details match is outside the actual preview viewport.");
            if (Find(window, "DetailsToggle" + detailsStart).Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != FlaUI.Core.Definitions.ExpandCollapseState.Expanded
                || Find(window, "DetailsToggle" + nestedDetailsStart).Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != FlaUI.Core.Definitions.ExpandCollapseState.Expanded
                || editor.Patterns.Value.Pattern.Value.Value != insertedDetails)
                throw new IOException("Finding hidden details did not expand both ancestors without changing source.");
            window.CaptureToFile(Path.Combine(output, "native-details-preview.png"));
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "One Undo did not remove the complete inserted details section.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == insertedDetails, "Details insertion Redo changed source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == sourceBeforeLibraryImage, "The details fixture did not restore original source.");
            if (ReadObservedText(source) != templateSavedSource) throw new IOException("The details workflow changed saved source.");
            checks.Add("Native details dialogue restores and edits Markdown; keyboard and accessible nested expanders and preview Find reveal hidden text without source changes, with complete insertion/edit Undo and Redo");
            const string mathsFixture = "\n\n## Native maths UI\n\nNative formula $\\frac{x^2}{y_1}$ keeps its source.\n";
            var beforeMaths = ReadEditorText(editor);
            var mathsClipboard = NativeUiClipboard.GetDataObject(); uint mathsSequence = 0;
            try
            {
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                NativeUiClipboard.SetText(mathsFixture); mathsSequence = GetClipboardSequenceNumber();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
                Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeMaths + mathsFixture, "Literal maths fixture insertion changed source.");
                var mathsStart = (beforeMaths + mathsFixture).IndexOf("$\\frac{x^2}{y_1}$", beforeMaths.Length, StringComparison.Ordinal);
                var formulaImage = Find(window, "DocumentFormula" + mathsStart);
                if (formulaImage.Name != @"Formula: \frac{x^2}{y_1}") throw new IOException("The native formula lost its accessible LaTeX label.");
                var mathsHeading = Find(window, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.FindText("Native maths UI", false, false)
                    ?? throw new IOException("The formula heading did not appear in native preview text.");
                mathsHeading.ScrollIntoView(true);
                Wait(() => !formulaImage.IsOffscreen && formulaImage.BoundingRectangle.Width > 10, "The native formula did not acquire visible bounds.");
                var formulaBounds = formulaImage.BoundingRectangle;
                Mouse.Click(new System.Drawing.Point(formulaBounds.X + formulaBounds.Width / 2, formulaBounds.Y + formulaBounds.Height / 2), MouseButton.Right);
                ContextAction("CopyFormulaSource").Patterns.Invoke.Pattern.Invoke();
                Wait(() => NativeUiClipboard.GetText() == @"\frac{x^2}{y_1}", "Formula context copy changed the LaTeX source.");
                mathsSequence = GetClipboardSequenceNumber();
                Find(window, "EditMenu").Click(); Find(window, "CopyRenderedText").Patterns.Invoke.Pattern.Invoke();
                Wait(() => NativeUiClipboard.GetText().Contains(@"Native formula \frac{x^2}{y_1} keeps its source.", StringComparison.Ordinal),
                    "Copy rendered text lost the formula or surrounding prose.");
                mathsSequence = GetClipboardSequenceNumber();
                preview = Find(window, "DocumentPreview"); preview.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                Wait(() => Find(window, "FindContext").Name == "Find in preview", "Formula search used the wrong context.");
                Find(window, "FindText").AsTextBox().Text = @"\frac{x^2}{y_1}"; Keyboard.Type(VirtualKeyShort.RETURN);
                Wait(() => Find(window, "Status").Name.StartsWith("Matching formula selected", StringComparison.Ordinal), "Preview Find did not locate native LaTeX source.");
                Keyboard.Type(VirtualKeyShort.ESCAPE); window.CaptureToFile(Path.Combine(output, "native-maths-preview.png"));
                if (editor.Patterns.Value.Pattern.Value.Value != beforeMaths + mathsFixture || ReadObservedText(source) != templateSavedSource)
                    throw new IOException("Native formula preview, copy or search changed source.");
            }
            finally { RestoreClipboard(mathsClipboard, mathsSequence); }
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == beforeMaths, "One Undo did not restore the source before the maths fixture.");
            checks.Add("Native maths has accessible drawing bounds, exact LaTeX context/plain copy and focused preview search, while preserving source and complete insertion Undo");
            CheckWikiLinks(window, editor, automation, app.ProcessId, output, source, renamedDocument);
            checks.Add("Native workspace link completion inserts an unambiguous path; preview navigation and Back preserve drafts, missing targets explain the problem, and complete Undo restores saved/editor source");
            CheckHelpGuide(window, editor, automation, app.ProcessId, output, source);
            CheckAbout(window, editor, automation, app.ProcessId, output, source, executable);
            checks.Add("About displays the exact executable version and source revision without changing document state");
            CheckWorkspaceMap(window, editor, automation, app.ProcessId, output, source, renamedDocument);
            checks.Add("Native workspace map uses current drafts, copies a passive Markdown report, clears empty results and navigates exact source/document history; native diagrams and complete Undo preserve files");
            checks.Add("F1 opens the bundled native guide; focused topic search finds browser migration, clears empty results and closes without changing the document");
            CheckPreviewScheduling(window, editor, output, source);
            checks.Add("Editor-only mode pauses hidden rendering, explicit text copy uses current edits and Undo, and Find follows Editor/Preview layout without changing source");
            CheckPagination(window, editor, automation, app.ProcessId, state, output, source);
            checks.Add("Native pages and explicit Continuous find and visibly select the final 4,000-item target, preserve Split/Preview mode, source and Undo, and apply reading choices without consent changes");
            CheckSettings(window, editor, automation, app.ProcessId, state, output, source);
            CheckSettingsTransfer(window, editor, automation, app.ProcessId, state, output, source, OpenThroughDialogue,
                (path, title) => SaveThroughDialogue(path, title, absolutePath: true));
            checks.Add("Portable settings export/import uses native file pickers, stages browser choices for Apply/Cancel, preserves source and update consent, and restores the exported appearance");
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
            Wait(() => app.HasExited, "Native application did not close safely.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("Native application failed during shutdown: " + launchedProcess.ExitCode);
            app.Dispose(); launchedProcess.Dispose(); app = Launch(null);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("Restored window did not appear.");
            mainWindow = window;
            editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Session did not restore saved text.");
            checks.Add("Session restoration reopened the document");
            CheckRestoredSettings(window, automation, app.ProcessId);
            checks.Add("Native settings apply font, wrapping, indentation, preview zoom and outline choices, retain cancelled edits and survive restart without changing source or update consent");
            libraryWindow = OpenTemplateLibrary(window);
            Wait(() => Find(libraryWindow, "WordTemplateList").FindFirstDescendant(cf => cf.ByName("Renamed Café handbook")) is not null
                && Find(libraryWindow, "UseLibraryTemplate").IsEnabled, "The renamed and restored Word template did not survive application restart.");
            Find(libraryWindow, "DefaultLibraryTemplate").AsButton().Invoke();
            Wait(() => Find(window, "Status").Name == "Default Word styles selected", "The library did not restore default Word styles.");
            checks.Add("Native template library imports, renames, saves reusable copies, archives/restores, exports with retained formatting and survives restart without changing original files");
            contentLibrary = OpenContentLibrary(window);
            Find(contentLibrary, "ContentLibraryCategory").AsComboBox().Select("Export profiles"); Find(contentLibrary, "ContentLibraryCategory").AsComboBox().Collapse();
            Find(contentLibrary, "ContentLibraryFilter").AsTextBox().Text = "Native saved profile";
            Wait(() => Find(contentLibrary, "UseContentEntry").IsEnabled, "The saved native content library did not survive restart.");
            contentLibrary.Close();
            checks.Add("Native content library saves portable templates/snippets, merges browser JSON, renames/archives/restores, exports and survives restart; new tabs and snippet Undo preserve source while session profiles control actual bundle output");
            Find(window, "HelpMenu").Click();
            var updatesMenu = Find(window, "UpdatesMenu");
            Wait(() => updatesMenu.IsEnabled && !updatesMenu.Properties.IsOffscreen.Value, "The Updates menu command did not become visible.");
            File.WriteAllText(Path.Combine(output, "native-updates-command.json"), JsonSerializer.Serialize(new
            {
                updatesMenu.AutomationId, updatesMenu.Name, updatesMenu.IsEnabled,
                Bounds = updatesMenu.BoundingRectangle.ToString(), InvokeSupported = updatesMenu.Patterns.Invoke.IsSupported,
                Procedure = "Invoke the observed menu command once; wait for its window within the unchanged deadline."
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (!updatesMenu.Patterns.Invoke.IsSupported) throw new IOException("The Updates menu has no supported automation command.");
            // The retained failure left Help open: a popup-sensitive coordinate click never activated Updates.
            // Use the same supported command pattern as other menu actions; do not retry the action or extend Wait.
            updatesMenu.Patterns.Invoke.Pattern.Invoke();
            Window? updateWindow = null;
            Wait(() => (updateWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Lens Docs Studio updates")))?.AsWindow()) is not null, "The native updates window did not open.");
            var channel = Find(updateWindow!, "UpdateChannel").AsComboBox();
            channel.Select("Preview");
            channel.Collapse();
            Wait(() => ReadObservedText(Path.Combine(state, "preferences.json")).Contains("\"Preview\"", StringComparison.Ordinal), "Update channel preference was not persisted.");
            if (Find(updateWindow!, "DownloadUpdate").IsEnabled || Find(updateWindow!, "InstallUpdate").IsEnabled) throw new InvalidOperationException("Untrusted update actions were enabled.");
            checks.Add("Native update preferences persist and untrusted installation actions stay disabled");
            updateWindow!.CaptureToFile(Path.Combine(output, "native-updates.png"));
            updateWindow!.Close();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Lens Docs Studio updates"))) is null, "The updates window did not close.");
            var artefact = CheckArtifactWorkflow(window, editor, automation, app.ProcessId, runRoot, output,
                OpenThroughDialogue, (destination, title) => SaveThroughDialogue(destination, title, absolutePath: true));
            checks.Add("Native ZIP import opens its entry document; grouped evidence filters select a candidate and actual review/generic ZIP exports preserve unsaved source and supplied labels");
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
            Wait(() => app.HasExited, "The artefact workspace did not close safely.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("The artefact workspace failed during shutdown.");
            app.Dispose(); launchedProcess.Dispose(); app = Launch(null);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("The artefact workspace did not restore.");
            mainWindow = window; editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == artefact.Source, "The imported document did not restore its saved source.");
            CheckArtifactContextCleared(window, artefact.Root, artefact.Document, artefact.Source);
            checks.Add("Restart restores imported source but clears optional artefact metadata, reader and review-pack actions; no raw manifest is persisted");
            window.CaptureToFile(Path.Combine(output, "native-workspace.png"));
            CheckWordImport(window, editor, automation, app.ProcessId, runRoot, output, artefact.Document, OpenThroughDialogue);
            checks.Add("Native Word import cancellation preserves the previous document; completed import exposes every warning and creates unchanged-source, portable Markdown and image files");
            var workspaceContext = CheckWorkspaceContextSwitch(window, editor, automation, app.ProcessId, runRoot, state, output, OpenThroughDialogue);
            var desktopSession = PrepareDesktopSession(window, editor, automation, app.ProcessId, state);
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
            Wait(() => app.HasExited, "Restored application did not close.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("Restored application failed during shutdown: " + launchedProcess.ExitCode);
            app.Dispose(); launchedProcess.Dispose(); app = Launch(null);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The retained workspace did not restore.");
            mainWindow = window; editor = Find(window, "MarkdownEditor");
            CheckRetainedWorkspaceContent(window, editor, workspaceContext, output, "after-restart", workspaceContext.Draft);
            CheckDesktopSession(window, editor, automation, app.ProcessId, state, output, desktopSession);
            ObserveRetainedContextSaved(workspaceContext, output);
            checks.Add("Switching folders retains earlier drafts, native images and two-way links; saving and restarting preserves per-document workspace context and exact files");
            checks.Add("Actual process restart restores hidden panels, independent synchronisation choices, tab order, active identity and native pin actions without changing source");
            FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
            Wait(() => app.HasExited, "The retained workspace application did not close.");
            if (launchedProcess!.ExitCode != 0) throw new IOException("The retained workspace failed during shutdown.");
            RecordResult(new { Passed = true, Checks = checks, FixtureRoot = runRoot });
            Console.WriteLine("Native UI Automation: " + checks.Count + " checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            NativeUiFailureDiagnostics.Capture(launchedProcess, output);
            try { mainWindow?.CaptureToFile(Path.Combine(output, "native-ui-failure.png")); } catch { }
            try { File.WriteAllLines(Path.Combine(output, "native-ui-failure-controls.txt"), mainWindow!.FindAllDescendants().Select(e => $"{e.ControlType}: {e.AutomationId} — {e.Name}")); } catch { }
            try
            {
                var windows = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(app!.ProcessId));
                File.WriteAllLines(Path.Combine(output, "native-ui-failure-windows.txt"), windows.Select(e => $"{e.ControlType}: {e.AutomationId} — {e.Name} — {e.BoundingRectangle}"));
                for (var i = 0; i < windows.Length; i++)
                {
                    try { windows[i].CaptureToFile(Path.Combine(output, $"native-ui-failure-window-{i}.png")); } catch { }
                    try { File.WriteAllLines(Path.Combine(output, $"native-ui-failure-window-{i}.txt"), windows[i].FindAllDescendants().Select(e => $"{e.ControlType}: {e.AutomationId} — {e.Name}")); } catch { }
                }
            }
            catch { }
            RecordResult(new { Passed = false, Checks = checks, Error = ex.ToString(), FixtureRoot = runRoot });
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally { if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); } launchedProcess?.Dispose(); }

        void RecordResult(object result)
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(runRoot, "native-ui.json"), json);
            File.WriteAllText(Path.Combine(output, "native-ui.json"), json);
        }
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
        void MeasureOperation(string name, Stopwatch watch)
        {
            var elapsed = watch.Elapsed.TotalMilliseconds;
            launchedProcess!.Refresh();
            performance.Add(new { Operation = name, ElapsedMilliseconds = elapsed, ProcessId = launchedProcess.Id,
                WorkingSetBytes = launchedProcess.WorkingSet64, PrivateBytes = launchedProcess.PrivateMemorySize64,
                PeakWorkingSetBytes = launchedProcess.PeakWorkingSet64, ProcessCpuMilliseconds = launchedProcess.TotalProcessorTime.TotalMilliseconds });
            File.WriteAllText(Path.Combine(output, "native-ui-performance.json"), JsonSerializer.Serialize(new {
                Samples = performance, Environment.ProcessorCount,
                Limitations = "Observed Windows workflow latency includes input delivery, UIA and 100 ms polling. First launch is of this fixture process, not a cold operating-system cache. Memory belongs to the actual tested application; no forced garbage collection or universal timing threshold is used."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        AutomationElement ContextAction(string id)
        {
            AutomationElement? action = null;
            Wait(() => (action = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(id)))) is not null,
                "The native context action did not appear: " + id);
            return action!;
        }
        void EditorContext(string id)
        {
            Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            ContextAction(id).Patterns.Invoke.Pattern.Invoke();
        }
        AutomationElement OpenCodeContext(string id) => OpenPreviewContext("const nativeContext", id);
        AutomationElement OpenPreviewContext(string text, string id)
        {
            var previewWindow = app!.GetMainWindow(automation)!;
            var range = Find(previewWindow, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.FindText(text, false, false)
                ?? throw new IOException("The requested preview text range is unavailable.");
            range.ScrollIntoView(true);
            var bounds = range.GetBoundingRectangles().FirstOrDefault(r => r.Width > 0 && r.Height > 0);
            if (bounds.Width <= 0) throw new IOException("The requested preview text has no visible native bounds.");
            Mouse.Click(new System.Drawing.Point(bounds.X + Math.Max(1, bounds.Width / 3), bounds.Y + Math.Max(1, bounds.Height / 2)), MouseButton.Right);
            return ContextAction(id);
        }
        Window OpenAnchor(Window mainWindow)
        {
            Find(mainWindow, "InsertMenu").Click(); Find(mainWindow, "InsertAnchor").Patterns.Invoke.Pattern.Invoke();
            Window? anchorWindow = null;
            Wait(() => (anchorWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Document anchor")))?.AsWindow()) is not null,
                "The native anchor dialogue did not open.");
            return anchorWindow!;
        }
        Window OpenProgress(Window mainWindow)
        {
            Find(mainWindow, "InsertMenu").Click(); Find(mainWindow, "InsertProgress").Patterns.Invoke.Pattern.Invoke();
            Window? progressWindow = null;
            Wait(() => (progressWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Progress indicator")))?.AsWindow()) is not null,
                "The native progress dialogue did not open.");
            return progressWindow!;
        }
        Window OpenDetails(Window mainWindow)
        {
            Find(mainWindow, "InsertMenu").Click(); Find(mainWindow, "EditDetails").Patterns.Invoke.Pattern.Invoke();
            Window? detailsWindow = null;
            Wait(() => (detailsWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId)
                .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)).And(cf.ByName("Details section")))?.AsWindow()) is not null,
                "The native details dialogue did not open.");
            return detailsWindow!;
        }
        Window OpenTemplateLibrary(Window main)
        {
            Find(main, "ExportMenu").Click(); Find(main, "ManageWordTemplates").Patterns.Invoke.Pattern.Invoke();
            Window? library = null;
            Wait(() => (library = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Word template library")))?.AsWindow()) is not null,
                "The native Word template library did not open.");
            return library!;
        }
        Window OpenContentLibrary(Window main)
        {
            Find(main, "FileMenu").Click(); Find(main, "ManageContentLibrary").Patterns.Invoke.Pattern.Invoke();
            Window? library = null;
            Wait(() => (library = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Content library")))?.AsWindow()) is not null,
                "The native content library did not open.");
            return library!;
        }
        void NameLibraryEntry(string title, string label, string value)
        {
            Window? prompt = null;
            Wait(() => (prompt = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title)))?.AsWindow()) is not null,
                "The library naming dialogue did not open: " + title);
            var input = Find(prompt!, "WorkspacePathInput").AsTextBox();
            if (input.Name != label) throw new IOException("The library naming dialogue has an incorrect accessible label.");
            input.Text = value; Find(prompt!, "ConfirmWorkspacePath").AsButton().Invoke();
        }
        void NameTemplate(string title, string previous, string next)
        {
            Window? prompt = null;
            Wait(() => (prompt = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title)))?.AsWindow()) is not null,
                "The native template name dialogue did not open.");
            var input = Find(prompt!, "WorkspacePathInput").AsTextBox();
            if (input.Text != previous || input.Name != "Template name") throw new IOException("The template name dialogue lost its existing value or accessible label.");
            input.Text = next; Find(prompt!, "ConfirmWorkspacePath").AsButton().Invoke();
        }
        void OpenThroughDialogue(string path, string title)
        {
            Window? picker = null;
            Wait(() => (picker = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title)))?.AsWindow()) is not null,
                "The native file selection dialogue did not open.");
            FocusWindow(picker!);
            // Select the intended edit by its accessible label instead of a numeric child ID.
            var fileName = (picker!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit).And(cf.ByName("File name:")))
                ?? throw new IOException("The labelled Open filename field is unavailable.")).AsTextBox();
            try
            {
                fileName.Focus(); Wait(() => fileName.Properties.HasKeyboardFocus.Value, "The Open filename field did not acquire keyboard focus.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(path);
                Wait(() => fileName.Text == path, "The native Open dialogue did not accept the selected path.");
            }
            catch
            {
                try
                {
                    picker.CaptureToFile(Path.Combine(output, "native-open-dialogue-failure.png"));
                    File.WriteAllText(Path.Combine(output, "native-open-dialogue.json"), JsonSerializer.Serialize(new { Title = title, Expected = path,
                        Observed = fileName.Text, fileName.AutomationId, fileName.Name, KeyboardFocus = fileName.Properties.HasKeyboardFocus.Value }));
                }
                catch (Exception) { /* Preserve the original failure if diagnostic collection also fails. */ }
                throw;
            }
            // The filename combo's dropdown arrow is also named Open. It is not the confirmation command.
            var openButton = picker!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Open")))
                .Single(element => element.AutomationId != "DropDown").AsButton();
            File.WriteAllText(Path.Combine(output, "native-open-dialogue.json"), JsonSerializer.Serialize(new { Title = title, Expected = path,
                Observed = fileName.Text, ButtonId = openButton.AutomationId, ButtonBounds = openButton.BoundingRectangle.ToString() }));
            // Invoke the unambiguous confirmation command without relying on popup-sensitive coordinates.
            openButton.Invoke();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title))) is null,
                "The native Open dialogue did not close after its Open command.");
        }
        void SaveThroughDialogue(string destination, string title = "Save document", string? expectedFilename = null, bool absolutePath = false)
        {
            Window? saveWindow = null;
            Wait(() => (saveWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title)))?.AsWindow()) is not null, "The native Save dialogue did not open.");
            FocusWindow(saveWindow!);
            TextBox? fileName = null;
            var suggested = ""; var expected = absolutePath ? Path.GetFullPath(destination) : Path.GetFileName(destination);
            try
            {
                // The shell window can appear before its child controls. The address control
                // can share child ID 1001, so wait for the enabled, explicitly labelled input.
                Wait(() => (fileName = saveWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit).And(cf.ByName("File name:")))?.AsTextBox())?.IsEnabled == true,
                    "The labelled Save filename field did not become ready.");
                suggested = fileName!.Text;
                if (expectedFilename is not null)
                {
                    // Windows may hide the selected file type's extension in the filename field.
                    // Saving the untouched suggestion must still produce the exact expected path.
                    if (expected != expectedFilename || (suggested != expectedFilename && suggested != Path.GetFileNameWithoutExtension(expectedFilename)))
                        throw new IOException("The Save dialogue suggested an incorrect filename: " + suggested);
                }
                else
                {
                    fileName.Focus();
                    Wait(() => fileName.Properties.HasKeyboardFocus.Value, "The Save filename field did not acquire keyboard focus.");
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(expected);
                    Wait(() => fileName.Text == expected, "The native dialogue did not accept the typed filename.");
                }
            }
            catch
            {
                try { saveWindow!.CaptureToFile(Path.Combine(output, "native-save-dialogue-failure.png")); } catch (Exception) { }
                throw;
            }
            finally
            {
                try
                {
                    saveDialogueChecks.Add(new { Title = title, Suggested = suggested, Expected = expected, Observed = fileName?.Text, Name = fileName?.Name, AutomationId = fileName?.AutomationId, KeyboardFocus = fileName?.Properties.HasKeyboardFocus.Value });
                    File.WriteAllText(Path.Combine(output, "native-save-dialogues.json"), JsonSerializer.Serialize(saveDialogueChecks, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception) { /* Diagnostic capture must not mask the original failure. */ }
            }
            (saveWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Save"))) ?? throw new InvalidOperationException("The Save button is unavailable.")).AsButton().Click();
        }
    }

    private static AutomationElement Find(Window window, string id)
    {
        AutomationElement? element = null;
        Wait(() => (element = window.FindFirstDescendant(cf => cf.ByAutomationId(id))) is not null, "Missing native control: " + id);
        return element!;
    }
    private static string ReadObservedText(string path)
    {
        // Observe without denying the application's exclusive writer access. Exact content/status
        // assertions and existing deadlines decide readiness; an observation must not cause a save failure.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file, System.Text.Encoding.UTF8, true);
        return reader.ReadToEnd();
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
        try { if (previous is null) NativeUiClipboard.Clear(); else NativeUiClipboard.SetDataObject(previous, true); }
        catch (ExternalException) { }
    }
    internal static void Wait(Func<bool> condition, string failure, int seconds = 20)
    {
        var timer = Stopwatch.StartNew();
        Exception? lastObservationError = null;
        while (timer.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            // Retry a transient UIA observation within the original deadline, never the user action.
            try { if (condition()) return; }
            catch (Exception ex) when (ex is IOException or FlaUI.Core.Exceptions.ElementNotAvailableException
                || IsObservationTimeout(ex)) { lastObservationError = ex; }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException(failure, lastObservationError);
    }
    private static bool IsObservationTimeout(Exception error) => error is COMException && TimeoutCode(error.HResult)
        || error is System.ComponentModel.Win32Exception { NativeErrorCode: 1460 }
        || error is TimeoutException { InnerException: COMException timeout } && TimeoutCode(timeout.HResult)
        || error is FlaUI.Core.Exceptions.PatternNotSupportedException { InnerException: COMException inner } && TimeoutCode(inner.HResult);
    // Win32Exception can retain ERROR_TIMEOUT with a generic HRESULT. Retry only the
    // observation within Wait's existing deadline, without repeating an input action.
    private static bool TimeoutCode(int code) => code == unchecked((int)0x80131505) || code == unchecked((int)0x800705B4);
    private static string ReadEditorText(AutomationElement editor)
    {
        string? text = null;
        Wait(() => { text = editor.Patterns.Value.Pattern.Value.Value; return true; }, "The editor text provider remained unavailable.");
        return text!;
    }
}
