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
    private sealed record DesktopSessionExpectation(UserPreferences Preferences, SessionState Session);

    private static DesktopSessionExpectation PrepareDesktopSession(Window window, AutomationElement editor,
        UIA3Automation automation, int process, string state)
    {
        var text = ReadEditorText(editor);
        UserPreferences? preferences = null;
        SessionState? session = null;
        foreach (var id in new[] { "FollowCaretControl", "SyncScrollControl", "SyncSelectionControl" })
            Find(window, id).AsCheckBox().IsChecked = true;
        Wait(() => (preferences = ReadDesktopState<UserPreferences>(state, "preferences.json")) is not null,
            "The actual desktop preferences could not be observed.");
        if (preferences!.Desktop?.ShowSidebar != false) Find(window, "ToggleSidebar").AsButton().Invoke();
        Wait(() => (preferences = ReadDesktopState<UserPreferences>(state, "preferences.json")).Desktop is
            { ShowSidebar: false, FollowCaret: true, SyncScroll: true, SyncSelection: true },
            "The actual desktop session choices did not persist independently.");
        var active = Find(window, "DocumentTabs").FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .Single(tab => tab.Patterns.SelectionItem.Pattern.IsSelected.Value);
        active.RightClick();
        AutomationElement? pin = null;
        Wait(() => (pin = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("TabContextPin")))) is not null,
            "The active desktop session tab did not expose Pin.");
        pin!.AsMenuItem().Invoke();
        Wait(() =>
        {
            session = ReadDesktopState<SessionState>(state, "session.json");
            return session.Documents.Any(document => document.Id == session.ActiveDocument && document.IsPinned);
        },
            "The actual active desktop session pin did not persist.");
        if (ReadEditorText(editor) != text) throw new IOException("Preparing desktop preferences changed document text.");
        return new(preferences!, session!);
    }

    private static void CheckDesktopSession(Window window, AutomationElement editor, UIA3Automation automation,
        int process, string state, string output, DesktopSessionExpectation expected)
    {
        UserPreferences? preferences = null; var observations = new List<object>();
        // A hyperlink updates native editor text before its asynchronous session write
        // completes. Observe the exact persisted contract within the existing deadline;
        // never repeat the navigation action or accept a different final identity.
        try
        {
            Wait(() =>
            {
                preferences = ReadDesktopState<UserPreferences>(state, "preferences.json");
                var session = ReadDesktopState<SessionState>(state, "session.json");
                var matched = preferences.Desktop == expected.Preferences.Desktop && preferences.ShowOutline == expected.Preferences.ShowOutline
                    && preferences.Layout == expected.Preferences.Layout && preferences.Theme == expected.Preferences.Theme
                    && session.ActiveDocument == expected.Session.ActiveDocument
                    && session.Documents.Select(document => (document.Id, document.IsPinned)).SequenceEqual(expected.Session.Documents.Select(document => (document.Id, document.IsPinned)));
                observations.Add(new { Matched = matched, Active = session.ActiveDocument, ExpectedActive = expected.Session.ActiveDocument,
                    preferences.Desktop, preferences.ShowOutline, preferences.Layout, preferences.Theme,
                    Tabs = session.Documents.Select(document => new { document.Id, document.IsPinned }).ToArray() });
                return matched;
            }, "Restart lost desktop choices, tab order, pins or active document identity.");
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "native-desktop-restored-session-observations.json"), JsonSerializer.Serialize(new {
                DeadlineSeconds = 20, InputActionsRepeated = 0, Expected = expected.Preferences.Desktop, Observations = observations }));
        }
        if (window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceTree"))?.IsOffscreen == false)
            throw new IOException("Restart did not apply the persisted hidden workspace panel.");
        foreach (var id in new[] { "FollowCaretControl", "SyncScrollControl", "SyncSelectionControl" })
            if (Find(window, id).AsCheckBox().IsChecked != true)
                throw new IOException("Restart did not apply its independent synchronisation choice: " + id);
        var active = Find(window, "DocumentTabs").FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .Single(tab => tab.Patterns.SelectionItem.Pattern.IsSelected.Value);
        active.RightClick();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("TabContextUnpin"))) is not null,
            "The restored active tab did not apply its persisted pin in native controls.");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        var expectedText = expected.Session.Documents.Single(document => document.Id == expected.Session.ActiveDocument).Text;
        if (ReadEditorText(editor) != expectedText) throw new IOException("Desktop restoration changed the active source text.");
        window.CaptureToFile(Path.Combine(output, "native-desktop-restored-session.png"));
        File.WriteAllText(Path.Combine(output, "native-desktop-restored-session.json"), JsonSerializer.Serialize(new {
            Passed = true, SidebarHidden = true, IndependentSyncChoicesRestored = true, TabOrderPinsAndActiveRestored = true,
            preferences!.Desktop, preferences.ShowOutline, preferences.Layout, preferences.Theme, SourcePreserved = true }));
    }

    private static T ReadDesktopState<T>(string state, string name)
    {
        // Observe a single immutable file generation without preventing the app's
        // atomic replacement. Call only inside Wait: missing/locked generations
        // are transient observations, while malformed JSON still fails immediately.
        using var file = new FileStream(Path.Combine(state, name), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<T>(file) ?? throw new JsonException("The observed desktop state was null: " + name);
    }

    internal static void CheckDesktopStateObservation(string output)
    {
        var root = Path.Combine(output, "desktop-state-observation"); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "preferences.json");
        var expected = new UserPreferences(Desktop: new DesktopPreferences(ShowSidebar: false,
            FollowCaret: true, SyncScroll: true, SyncSelection: true));
        var original = JsonSerializer.SerializeToUtf8Bytes(expected); File.WriteAllBytes(path, original);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var lockedAttempts = 0;
        Wait(() =>
        {
            if (++lockedAttempts == 2) locked.Dispose();
            return ReadDesktopState<UserPreferences>(root, "preferences.json").Desktop == expected.Desktop;
        }, "Locked desktop state did not become readable.", 1);
        if (lockedAttempts != 2 || !File.ReadAllBytes(path).SequenceEqual(original))
            throw new IOException("Observing a locked generation changed bytes or accepted the wrong state.");

        var replacement = Path.Combine(root, "replacement.json"); File.Move(path, replacement);
        var missingAttempts = 0;
        Wait(() =>
        {
            if (++missingAttempts == 2) File.Move(replacement, path);
            return ReadDesktopState<UserPreferences>(root, "preferences.json").Desktop == expected.Desktop;
        }, "Missing desktop state did not become readable.", 1);
        if (missingAttempts != 2 || !File.ReadAllBytes(path).SequenceEqual(original))
            throw new IOException("Observing a missing generation changed bytes or accepted the wrong state.");

        var wrong = JsonSerializer.SerializeToUtf8Bytes(expected with { Desktop = expected.Desktop! with { ShowSidebar = true } });
        File.WriteAllBytes(path, wrong);
        using (var producer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            if (ReadObservedText(path) != System.Text.Encoding.UTF8.GetString(wrong))
                throw new IOException("The source-file observer failed to coexist with the application's writer sharing contract.");
        Exception? mismatch = null; var mismatchAttempts = 0;
        try
        {
            Wait(() => { mismatchAttempts++; return ReadDesktopState<UserPreferences>(root, "preferences.json").Desktop == expected.Desktop; },
                "Persistent wrong desktop choices", 1);
        }
        catch (InvalidOperationException error) { mismatch = error; }
        if (mismatch?.Message != "Persistent wrong desktop choices" || mismatchAttempts < 2 || !File.ReadAllBytes(path).SequenceEqual(wrong))
            throw new IOException("The observer accepted or repaired persistently wrong desktop choices.");

        File.WriteAllText(path, "{"); Exception? malformed = null; var malformedAttempts = 0;
        try { Wait(() => { malformedAttempts++; ReadDesktopState<UserPreferences>(root, "preferences.json"); return true; }, "Malformed state", 1); }
        catch (JsonException error) { malformed = error; }
        if (malformed is null || malformedAttempts != 1 || ReadObservedText(path) != "{")
            throw new IOException("Malformed desktop state was swallowed, retried or repaired.");
        File.WriteAllText(Path.Combine(output, "native-desktop-state-observation-regression.json"), JsonSerializer.Serialize(new {
            Passed = true, LockedGenerationRetried = true, MissingGenerationRetried = true,
            ExactStateAndBytesRetained = true, PersistentMismatchRejected = true, MalformedStateFailsImmediately = true,
            TextObserverAllowsProducerWrites = true,
            ObservationDeadlineSeconds = 1, ProductionDeadlineSeconds = 20, InputActionsExecuted = 0,
            lockedAttempts, missingAttempts, mismatchAttempts, malformedAttempts }));
    }
}
