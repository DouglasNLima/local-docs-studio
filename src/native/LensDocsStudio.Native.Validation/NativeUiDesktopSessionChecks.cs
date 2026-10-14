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
        UserPreferences Preferences() => JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
        SessionState Session() => JsonSerializer.Deserialize<SessionState>(File.ReadAllText(Path.Combine(state, "session.json")))!;
        foreach (var id in new[] { "FollowCaretControl", "SyncScrollControl", "SyncSelectionControl" })
            Find(window, id).AsCheckBox().IsChecked = true;
        if (Preferences().Desktop?.ShowSidebar != false) Find(window, "ToggleSidebar").AsButton().Invoke();
        Wait(() => Preferences().Desktop is { ShowSidebar: false, FollowCaret: true, SyncScroll: true, SyncSelection: true },
            "The actual desktop session choices did not persist independently.");
        var active = Find(window, "DocumentTabs").FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .Single(tab => tab.Patterns.SelectionItem.Pattern.IsSelected.Value);
        active.RightClick();
        AutomationElement? pin = null;
        Wait(() => (pin = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("TabContextPin")))) is not null,
            "The active desktop session tab did not expose Pin.");
        pin!.AsMenuItem().Invoke();
        Wait(() => Session().Documents.Any(document => document.Id == Session().ActiveDocument && document.IsPinned),
            "The actual active desktop session pin did not persist.");
        if (ReadEditorText(editor) != text) throw new IOException("Preparing desktop preferences changed document text.");
        return new(Preferences(), Session());
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
                preferences = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
                var session = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(Path.Combine(state, "session.json")))!;
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
}
