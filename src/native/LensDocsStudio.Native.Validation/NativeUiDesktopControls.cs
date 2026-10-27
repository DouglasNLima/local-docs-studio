using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckDesktopControls(Window window, AutomationElement editor, UIA3Automation automation, int process, string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var bounds = window.BoundingRectangle;
        var checks = new List<string>();
        string preferences = Path.Combine(state, "preferences.json");
        UserPreferences ReadPreferences() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(preferences))!;
        bool Visible(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id)) is { IsOffscreen: false };

        Find(window, "ToggleSidebar").AsButton().Invoke();
        Wait(() => !Visible("WorkspaceTree") && ReadPreferences().Desktop?.ShowSidebar == false, "Workspace did not collapse and persist.");
        if (!Visible("DocumentPreview")) throw new IOException("Hiding workspace also hid the preview.");
        Find(window, "ToggleOutline").AsButton().Invoke();
        Wait(() => !ReadPreferences().ShowOutline, "Outline visibility did not persist independently.");
        Find(window, "ToggleSidebar").AsButton().Invoke();
        Wait(() => Visible("WorkspaceTree") && ReadPreferences().Desktop?.ShowSidebar == true, "Workspace did not reopen.");
        if (ReadPreferences().ShowOutline) throw new IOException("Reopening workspace also reopened the outline.");
        Find(window, "ToggleOutline").AsButton().Invoke();
        Wait(() => ReadPreferences().ShowOutline, "Outline did not reopen.");
        checks.Add("Independent panel visibility persists without replacing source or preview");

        var sidebarWidth = ReadPreferences().Desktop!.SidebarWidth; var outlineWidth = ReadPreferences().Desktop!.OutlineWidth;
        Find(window, "ToggleOutline").AsButton().Invoke(); Wait(() => !ReadPreferences().ShowOutline, "Independent resize did not hide the outline.");
        ResizePanel("Resize workspace", 40);
        Wait(() => ReadPreferences().Desktop!.SidebarWidth > sidebarWidth + 25, "Pointer resizing did not persist the workspace width.");
        var resizedSidebar = ReadPreferences().Desktop!.SidebarWidth;
        Find(window, "ToggleSidebar").AsButton().Invoke(); Find(window, "ToggleSidebar").AsButton().Invoke();
        Wait(() => Visible("WorkspaceTree") && Math.Abs(ReadPreferences().Desktop!.SidebarWidth - resizedSidebar) < 2,
            "Reopening the workspace lost its resized width.");
        ResizePanel("Resize workspace", -40);
        Find(window, "ToggleOutline").AsButton().Invoke(); Wait(() => ReadPreferences().ShowOutline, "Independent resize did not restore the outline.");
        Find(window, "ToggleSidebar").AsButton().Invoke(); Wait(() => ReadPreferences().Desktop?.ShowSidebar == false, "Independent resize did not hide the workspace.");
        ResizePanel("Resize outline", -40);
        Wait(() => ReadPreferences().Desktop!.OutlineWidth > outlineWidth + 25, "Pointer resizing did not persist the independent outline width.");
        var resizedOutline = ReadPreferences().Desktop!.OutlineWidth;
        Find(window, "ToggleOutline").AsButton().Invoke(); Find(window, "ToggleOutline").AsButton().Invoke();
        Wait(() => ReadPreferences().ShowOutline && Math.Abs(ReadPreferences().Desktop!.OutlineWidth - resizedOutline) < 2,
            "Reopening the outline lost its resized width.");
        ResizePanel("Resize outline", 40);
        Find(window, "ToggleSidebar").AsButton().Invoke(); Wait(() => ReadPreferences().Desktop?.ShowSidebar == true, "Independent resize did not restore the workspace.");
        checks.Add("Pointer-driven independent panel widths persist through hide/reopen without changing source");

        var beforeFocus = ReadPreferences();
        FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_Z);
        Wait(() => !Visible("WorkspaceTree") && !Visible("ToggleSidebar"), "Focus Mode did not hide secondary chrome.");
        if (ReadPreferences().Desktop != beforeFocus.Desktop || ReadPreferences().ShowOutline != beforeFocus.ShowOutline)
            throw new IOException("Focus Mode overwrote permanent panel preferences.");
        window.CaptureToFile(Path.Combine(output, "native-focus-mode.png"));
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait(() => Visible("WorkspaceTree") && Visible("ToggleSidebar"), "Escape did not restore Focus Mode panels.");
        checks.Add("Focus Mode restores panel preferences and leaves the current layout intact");

        FocusWindow(window); Keyboard.Type(VirtualKeyShort.F11);
        Wait(() => window.BoundingRectangle.Height > bounds.Height + 5, "Full Screen did not reclaim the native window chrome.");
        window.CaptureToFile(Path.Combine(output, "native-full-screen.png"));
        editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        Wait(() => Visible("FindContext"), "Find did not open within Full Screen.");
        Find(window, "ViewMenu").Click(); Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("OpenWorkspaceMap"))?.IsOffscreen != false, "Escape did not close the menu in Full Screen.");
        if (!Visible("FindContext")) throw new IOException("Menu Escape dismissed Find before its own command surface.");
        if (window.BoundingRectangle.Height <= bounds.Height + 5) throw new IOException("Menu Escape exited Full Screen before closing its own menu.");
        Keyboard.Type(VirtualKeyShort.ESCAPE); Wait(() => !Visible("FindContext"), "Find Escape did not dismiss its own controls in Full Screen.");
        if (window.BoundingRectangle.Height <= bounds.Height + 5) throw new IOException("Find Escape exited Full Screen before closing its own controls.");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait(() => Math.Abs(window.BoundingRectangle.Width - bounds.Width) < 3 && Math.Abs(window.BoundingRectangle.Height - bounds.Height) < 3,
            "Full Screen did not restore the original window dimensions.");
        checks.Add("F11 and Escape restore the original native window dimensions");

        FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
        var picker = SearchWindow();
        Find(picker, "CommandSearchQuery").AsTextBox().Text = "Second";
        Wait(() => Find(picker, "CommandSearchResults").AsListBox().Items.Length == 1, "Quick Open did not filter workspace filenames.");
        picker.CaptureToFile(Path.Combine(output, "native-quick-open.png"));
        Keyboard.Type(VirtualKeyShort.RETURN);
        Wait(() => ReadEditorText(editor).StartsWith("# Second document", StringComparison.Ordinal), "Quick Open did not activate its selected document.");
        FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        Wait(() => ReadEditorText(editor) == original, "Closing Quick Open's clean tab did not restore the surviving active document.");
        checks.Add("Quick Open opens the filtered workspace document and active-tab survival retains source");

        FocusWindow(window); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        picker = SearchWindow(); Find(picker, "CommandSearchQuery").AsTextBox().Text = "Toggle outline";
        Wait(() => Find(picker, "CommandSearchResults").AsListBox().Items.Length == 1, "Command Palette did not filter the actual panel action.");
        picker.CaptureToFile(Path.Combine(output, "native-command-palette.png"));
        Keyboard.Type(VirtualKeyShort.RETURN); Wait(() => !ReadPreferences().ShowOutline, "Command Palette did not execute the outline toggle.");
        Find(window, "ToggleOutline").AsButton().Invoke(); Wait(() => ReadPreferences().ShowOutline, "Outline did not restore after the palette action.");
        checks.Add("Command Palette reuses an actual enabled native action");

        var tree = Find(window, "WorkspaceTree");
        var selectedFile = tree.FindFirstDescendant(cf => cf.ByName(Path.GetFileName(source)).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem)))
            ?? throw new IOException("The active workspace document is missing from the tree.");
        var targetPath = Path.Combine(Path.GetDirectoryName(source)!, "Second document.md");
        var targetFile = tree.FindFirstDescendant(cf => cf.ByName("Second document.md").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem)))
            ?? throw new IOException("The context-menu target is missing from the tree.");
        selectedFile.Click(); targetFile.RightClick();
        AutomationElement? copyPath = null;
        Wait(() => (copyPath = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("WorkspaceContextCopyabsolutepath")))) is not null,
            "Right-click did not expose the workspace path action.");
        copyPath!.AsMenuItem().Invoke();
        Wait(() => NativeUiClipboard.GetDataObject()?.GetData(System.Windows.DataFormats.UnicodeText) as string == targetPath,
            "Workspace context action copied a previously selected path instead of the right-clicked file.");
        if (ReadEditorText(editor) != original) throw new IOException("Copying a workspace path activated or modified another document.");
        checks.Add("Workspace right-click actions use the clicked item rather than an earlier selection");

        FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        Wait(() => Find(window, "FindContext").Name == "Find in editor", "Toolbar fixture search did not target the editor.");
        Find(window, "FindText").AsTextBox().Text = "Searchable"; Find(window, "FindNext").AsButton().Invoke();
        Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Searchable"), "Toolbar fixture did not select its exact source word.");
        Wait(() => Find(window, "FindCounter").Name == "1 of 1", "Editor Find counter did not reflect its exact source occurrence.");
        Find(window, "FindPrevious").AsButton().Invoke();
        Wait(() => Find(window, "FindCounter").Name == "1 of 1" && editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Searchable"), "Previous did not wrap a single editor occurrence.");
        if (!Find(window, "PreviewZoomLabel").Name.Contains("percent", StringComparison.Ordinal)) throw new IOException("Preview zoom lacks its accessible percentage.");
        Find(window, "CloseFind").AsButton().Invoke();
        Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Searchable"), "Closing Find discarded the toolbar fixture's source selection.");
        Find(window, "FormatBold").AsButton().Invoke();
        Wait(() => ReadEditorText(editor) == original.Replace("Searchable", "**Searchable**", StringComparison.Ordinal), "Formatting toolbar did not wrap the active source selection.");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        Wait(() => ReadEditorText(editor) == original, "Toolbar formatting did not retain one exact Undo.");
        checks.Add("Formatting toolbar edits the active selection and groups one exact Undo");
        if (!File.ReadAllBytes(source).SequenceEqual(saved) || ReadEditorText(editor) != original)
            throw new IOException("Desktop controls changed saved or unsaved document content.");
        window.CaptureToFile(Path.Combine(output, "native-desktop-controls.png"));
        File.WriteAllText(Path.Combine(output, "native-desktop-controls.json"), JsonSerializer.Serialize(new { Passed = true, Checks = checks }));

        Window SearchWindow()
        {
            Window? result = null;
            Wait(() => (result = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("CommandSearchWindow")))?.AsWindow()) is not null,
                "The native searchable command window did not appear.");
            return result!;
        }
        void ResizePanel(string name, int delta)
        {
            var divider = window.FindFirstDescendant(cf => cf.ByName(name)) ?? throw new IOException("The actual panel divider is unavailable: " + name);
            var rectangle = divider.BoundingRectangle;
            Mouse.DragHorizontally(new System.Drawing.Point((int)(rectangle.Left + rectangle.Width / 2), (int)(rectangle.Top + rectangle.Height / 2)), delta, MouseButton.Left);
        }
    }
}
