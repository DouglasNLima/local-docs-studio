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
    private sealed record WorkspaceContextFixture(string First, string Second, string Source, string Target,
        string Image, byte[] Pixels, string Original, string Draft, string TargetText, string State);

    private static WorkspaceContextFixture CheckWorkspaceContextSwitch(Window owner, AutomationElement editor,
        UIA3Automation automation, int process, string runRoot, string state, string output, Action<string, string> open)
    {
        var root = Path.Combine(runRoot, "retained-workspaces"); Directory.CreateDirectory(root);
        var word = NativeWordImportFixtureChecks.Run(root);
        var first = Path.Combine(root, "first"); var second = Path.Combine(root, "second");
        foreach (var directory in new[] { Path.Combine(first, "docs"), Path.Combine(first, "other"), Path.Combine(first, "assets"), second })
            Directory.CreateDirectory(directory);
        var source = Path.Combine(first, "docs", "retained.md"); var target = Path.Combine(first, "other", "target.md");
        var image = Path.Combine(first, "assets", "pixel.png");
        const string original = "![Retained pixel](../assets/pixel.png)\n\n[Other retained document](../other/target.md)\n\n# Retained Café\n";
        const string targetText = "![Retained pixel](../assets/pixel.png)\n\n[Return to retained source](../docs/retained.md)\n";
        // Keyboard.Type supplies the phrase; it does not synthesise Enter from LF.
        // The source already ends with a line break, so this remains a separate paragraph.
        const string addition = "Draft Ω remains portable.";
        File.WriteAllText(source, original); File.WriteAllText(target, targetText); File.WriteAllBytes(image, word.Image);
        File.WriteAllText(Path.Combine(second, "index.md"), "# Other workspace\n");
        var fixture = new WorkspaceContextFixture(first, second, source, target, image, word.Image, original, original + addition, targetText, state);
        OpenFolder(first);
        FocusWindow(owner); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O); open(source, "Open");
        Wait(() => ReadEditorText(editor) == original, "The retained workspace fixture did not open.");
        editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type(addition);
        Wait(() => ReadEditorText(editor) == fixture.Draft, "The retained workspace draft was not entered exactly.");
        OpenFolder(second);
        CheckRetainedWorkspaceContent(owner, editor, fixture, output, "after-switch", original);
        FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        Wait(() => ReadObservedText(source) == fixture.Draft && RetainedContextSaved(fixture),
            "Saving the earlier tab lost its source, root or linked document context.");
        CheckRetainedWorkspaceContent(owner, editor, fixture, output, "after-save", fixture.Draft);
        return fixture;

        void OpenFolder(string path)
        {
            FocusWindow(owner); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_O);
            Window? picker = null;
            Wait(() => (picker = CurrentPicker()) is not null, "The retained workspace folder dialogue did not open.");
            FocusWindow(picker!);
            var field = (picker!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName("Folder:")))
                ?? throw new IOException("The workspace folder field is unavailable.")).AsTextBox();
            field.Focus(); Wait(() => field.Properties.HasKeyboardFocus.Value, "The workspace folder field did not receive focus.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(path);
            Wait(() => field.Text == path, "The workspace folder field did not retain the chosen path.");
            picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Select Folder")))
                .Single(element => element.AutomationId != "DropDown").AsButton().Invoke();
            Wait(() => CurrentPicker() is null && Find(owner, "Status").Name == "Workspace opened", "The selected workspace did not open.");
        }
        Window? CurrentPicker() => automation.GetDesktop()
            .FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Open documentation workspace")))?.AsWindow();
    }

    private static void CheckRetainedWorkspaceContent(Window owner, AutomationElement editor, WorkspaceContextFixture fixture,
        string output, string phase, string expectedSaved)
    {
        Wait(() => ReadEditorText(editor) == fixture.Draft, "The earlier tab did not retain its exact text: " + phase);
        Wait(() => Find(owner, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal),
            "The retained workspace preview did not finish after restoring its editor: " + phase);
        CheckImage(); Follow("Other retained document", fixture.TargetText);
        CheckImage(); Follow("Return to retained source", fixture.Draft);
        if (ReadObservedText(fixture.Source) != expectedSaved || ReadObservedText(fixture.Target) != fixture.TargetText
            || !File.ReadAllBytes(fixture.Image).SequenceEqual(fixture.Pixels))
            throw new IOException("Workspace navigation changed source or image bytes: " + phase);
        owner.CaptureToFile(Path.Combine(output, "native-workspace-context-" + phase + ".png"));
        File.WriteAllText(Path.Combine(output, "native-ui-workspace-context-" + phase + ".json"), JsonSerializer.Serialize(new
        { Passed = true, Phase = phase, ExactEditorText = true, OriginalSavedText = expectedSaved == fixture.Original,
            ExpectedSavedText = true, NativeImageVisible = true, LinkedImageVisible = true, TwoWayLinks = true, ImageBytesRetained = true,
            Source = fixture.Source, ExpectedRoot = fixture.First, ActiveWorkspace = fixture.Second }, new JsonSerializerOptions { WriteIndented = true }));

        AutomationElement[] Children()
        {
            var text = Find(owner, "DocumentPreview").Patterns.Text;
            if (!text.IsSupported) return [];
            try { return text.Pattern.DocumentRange.GetChildren(); }
            // Restored editor text can precede the asynchronous FlowDocument and
            // its Text provider. Keep observing within the existing Wait deadline.
            catch (FlaUI.Core.Exceptions.PatternNotSupportedException) { return []; }
        }
        void CheckImage()
        {
            // Image is the first block. Earlier tests retain Pages mode and editor
            // synchronisation may have moved to the trailing draft paragraph.
            for (var page = 0; page < 8; page++)
            {
                var previous = owner.FindFirstDescendant(cf => cf.ByAutomationId("PreviewPreviousPage"));
                if (previous?.IsEnabled != true) break;
                var label = Find(owner, "PreviewPageNumber").Name;
                previous.Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(owner, "PreviewPageNumber").Name != label, "The retained preview did not return to its first page.");
            }
            Wait(() => Children().FirstOrDefault(item => item.ControlType == ControlType.Image && item.Name == "Retained pixel")
                is { IsOffscreen: false } image && image.BoundingRectangle.Width > 0 && image.BoundingRectangle.Height > 0,
                "The retained image is missing from the native preview: " + phase);
        }
        void Follow(string label, string expected)
        {
            AutomationElement? link = null;
            Wait(() => (link = Children().FirstOrDefault(item => item.ControlType == ControlType.Hyperlink && item.Name == label)) is not null,
                "A retained workspace link is missing: " + label);
            link!.Patterns.Invoke.Pattern.Invoke();
            Wait(() => ReadEditorText(editor) == expected, "A retained workspace link did not open its target: " + label);
        }
    }

    private static bool RetainedContextSaved(WorkspaceContextFixture fixture, List<object>? observations = null)
    {
        try
        {
            using var file = new FileStream(Path.Combine(fixture.State, "session.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var session = JsonSerializer.Deserialize<SessionState>(file);
            var matches = session?.Workspace == fixture.Second && new[] { fixture.Source, fixture.Target }.All(path =>
                session.Documents.SingleOrDefault(document => document.Path == path)?.WorkspaceRoot == fixture.First);
            observations?.Add(new { Readable = true, Matches = matches, Workspace = session?.Workspace,
                Roots = new[] { fixture.Source, fixture.Target }.Select(path => new { Path = path,
                    Root = session?.Documents.SingleOrDefault(document => document.Path == path)?.WorkspaceRoot }).ToArray() });
            return matches;
        }
        catch (IOException error)
        { observations?.Add(new { Readable = false, Matches = false, Error = error.GetType().Name, error.HResult }); return false; }
    }

    private static void ObserveRetainedContextSaved(WorkspaceContextFixture fixture, string output)
    {
        var observations = new List<object>(); var passed = false;
        try { Wait(() => RetainedContextSaved(fixture, observations), "Restart did not persist the exact per-document workspace roots."); passed = true; }
        finally
        {
            File.WriteAllText(Path.Combine(output, "native-workspace-context-session-observations.json"), JsonSerializer.Serialize(new
            { Passed = passed, DeadlineSeconds = 20, InputActionsExecuted = 0, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    internal static void CheckWorkspaceContextObservation(string output)
    {
        var root = Path.Combine(output, "workspace-context-observation"); Directory.CreateDirectory(root);
        var fixture = new WorkspaceContextFixture(Path.Combine(root, "first"), Path.Combine(root, "second"),
            Path.Combine(root, "first", "source.md"), Path.Combine(root, "first", "target.md"), "unused.png", [], "", "", "", root);
        var state = new SessionState(1, fixture.Second, null, [
            new RecoveredDocument(Guid.NewGuid(), fixture.Source, "", null, false, 0, 0, WorkspaceRoot: fixture.First),
            new RecoveredDocument(Guid.NewGuid(), fixture.Target, "", null, false, 0, 0, WorkspaceRoot: fixture.First)]);
        var path = Path.Combine(root, "session.json"); var original = JsonSerializer.SerializeToUtf8Bytes(state); File.WriteAllBytes(path, original);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var observations = new List<object>();
        if (RetainedContextSaved(fixture, observations)) throw new IOException("An exclusive state read lock did not reproduce the one-shot false result.");
        var attempts = 0;
        Wait(() => { if (++attempts == 2) locked.Dispose(); return RetainedContextSaved(fixture, observations); }, "Valid roots did not become readable after lock release.", 1);
        if (attempts != 2 || !File.ReadAllBytes(path).SequenceEqual(original)) throw new IOException("Observing transient state changed bytes or repeated an input action.");
        var wrong = state with { Documents = state.Documents.Select(document => document with { WorkspaceRoot = fixture.Second }).ToArray() };
        var wrongBytes = JsonSerializer.SerializeToUtf8Bytes(wrong); File.WriteAllBytes(path, wrongBytes);
        Exception? failure = null; var mismatches = 0;
        try { Wait(() => { mismatches++; return RetainedContextSaved(fixture); }, "Persistent wrong workspace roots", 1); }
        catch (InvalidOperationException error) { failure = error; }
        if (failure?.Message != "Persistent wrong workspace roots" || mismatches < 2 || !File.ReadAllBytes(path).SequenceEqual(wrongBytes))
            throw new IOException("Persistent mismatched roots were accepted or repaired by the observer.");
        File.WriteAllText(Path.Combine(output, "native-workspace-context-observation-regression.json"), JsonSerializer.Serialize(new
        { Passed = true, TransientReadFailureReproduced = true, RetainedBytes = true, PersistentMismatchRejected = true,
            ObservationDeadlineSeconds = 1, ProductionDeadlineSeconds = 20, InputActionsExecuted = 0, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
