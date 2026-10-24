using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void RunDevelopmentHostShell(string executable, string sandbox, UIA3Automation automation, string revision, string tree)
    {
        var output = Path.Combine(sandbox, "ui", "shell"); Directory.CreateDirectory(output);
        var workspace = Path.Combine(sandbox, "workspace"); var sourcePath = Path.Combine(workspace, "acceptance-lf.md");
        var state = Path.Combine(sandbox, "state", "ui-shell"); Directory.CreateDirectory(state);
        WriteNew(Path.Combine(state, "preferences.json"), new { AutomaticUpdateChecks = false, Theme = "Light" });
        var source = File.ReadAllText(sourcePath); var checks = new List<string>(); var observations = new List<object>();
        WriteNew(Path.Combine(output, "before.json"), new { sourceSha256 = Hash(sourcePath), preferencesSha256 = Hash(Path.Combine(state, "preferences.json")), revision, tree });
        Application? app = null; Window? main = null; var stage = "launch";
        try
        {
            Launch();
            var editor = Find(main!, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == source, "Synthetic Markdown did not open exactly.");
            Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramActionPNG")) is not null, "Synthetic image/diagram preview was not ready.");
            if (main!.FindFirstDescendant(cf => cf.ByName("Synthetic orange square")) is null) throw new IOException("The actual relative image is missing from native preview.");
            Capture(main, "first-launch"); checks.Add("Explicit synthetic folder/file, Unicode, relative image and native diagram render in the actual process");
            stage = "quick-open"; QuickOpen("acceptance-crlf.md");
            if (ReadEditorText(Find(main, "MarkdownEditor")) != File.ReadAllText(Path.Combine(workspace, "acceptance-crlf.md"))) throw new IOException("CRLF file did not retain its exact source.");
            QuickOpen("relative.md"); QuickOpen("acceptance-lf.md"); editor = Find(main, "MarkdownEditor");
            checks.Add("Quick Open keyboard filtering/Enter opens CRLF and relative synthetic files with exact source");
            stage = "relative-link";
            var link = Find(main, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.FindText("Relative document", false, false) ?? throw new IOException("Relative document link is missing from preview.");
            link.ScrollIntoView(false);
            var linkBounds = link.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
            FocusWindow(main); Mouse.Click(new System.Drawing.Point((int)(linkBounds.Left + linkBounds.Width / 2), (int)(linkBounds.Top + linkBounds.Height / 2)));
            Wait(() => ReadEditorText(Find(main, "MarkdownEditor")) == File.ReadAllText(Path.Combine(workspace, "relative.md")), "Actual relative-link pointer did not open the synthetic target.");
            Capture(main, "relative-link"); QuickOpen("acceptance-lf.md"); editor = Find(main, "MarkdownEditor");
            checks.Add("Actual pointer activation of the rendered relative link opens its exact synthetic document");
            stage = "palette"; FocusWindow(main); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            var palette = Modal("CommandSearchWindow"); Capture(palette, "command-palette");
            Find(palette, "CommandSearchQuery").AsTextBox().Text = "Close workspace";
            Wait(() => Find(palette, "CommandSearchResults").AsListBox().Items.Any(item => item.Name.Contains("Close workspace", StringComparison.Ordinal)), "Palette did not expose its actual workspace command.");
            FocusWindow(palette); Keyboard.Type(VirtualKeyShort.ESCAPE); WaitModalClosed("CommandSearchWindow");
            checks.Add("Command Palette opens, filters its real command and cancels without changing the owner");
            stage = "editor-find"; FocusWindow(main); editor.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(main, "FindContext").Name == "Find in editor", "Ctrl+F did not target the editor.");
            Find(main, "FindText").AsTextBox().Text = "Café"; FocusWindow(main); Find(main, "FindText").Focus();
            Wait(() => Find(main, "FindText").Properties.HasKeyboardFocus.Value && Find(main, "FindText").AsTextBox().Text == "Café", "Editor Find input did not acquire keyboard focus with its exact query.");
            Capture(main, "editor-find-before-enter"); Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Café"), "Editor Find/Enter did not select exact Unicode text.");
            Capture(main, "editor-find"); Find(main, "CloseFind").AsButton().Invoke();
            stage = "preview-find";
            var preview = Find(main, "DocumentPreview");
            var range = preview.Patterns.Text.Pattern.DocumentRange.FindText("Only synthetic", false, false) ?? throw new IOException("Native preview text is missing.");
            var bounds = range.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
            FocusWindow(main); Mouse.Click(new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2)));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(main, "FindContext").Name == "Find in preview", "Ctrl+F did not target the focused preview.");
            Find(main, "FindText").AsTextBox().Text = "Café"; Find(main, "FindText").Focus();
            Wait(() => Find(main, "FindText").Properties.HasKeyboardFocus.Value && Find(main, "FindText").AsTextBox().Text == "Café", "Preview Find input did not acquire keyboard focus with its exact query.");
            Keyboard.Type(VirtualKeyShort.RETURN);
            Capture(main, "preview-find"); Find(main, "CloseFind").AsButton().Invoke();
            checks.Add("Editor and preview Ctrl+F retain separate contexts, Unicode input, Enter and close/focus behaviour");
            stage = "owner-edit";
            var action = main.FindAllDescendants().Single(element => (element.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("DiagramEditVisually", StringComparison.Ordinal));
            action.AsButton().Invoke(); var visual = Modal("MermaidVisualEditor");
            Wait(() => Find(visual, "MermaidStatus").Name.StartsWith("Ready", StringComparison.Ordinal), "Flowchart did not reach Ready.");
            Find(visual, "MermaidElements").AsListBox().Select(0);
            Find(visual, "MermaidNodeLabel").AsTextBox().Text = "Host Café😀🚀";
            Find(visual, "MermaidUpdateNode").AsButton().Invoke();
            Wait(() => Find(visual, "MermaidApply").IsEnabled, "Validated private edit did not permit Apply.");
            Capture(visual, "private-edit"); Find(visual, "MermaidApply").AsButton().Invoke(); WaitModalClosed("MermaidVisualEditor");
            Wait(() => ReadEditorText(editor).Contains("Host Café😀🚀", StringComparison.Ordinal), "Apply did not update the exact owner.");
            var edited = ReadEditorText(editor);
            if (File.ReadAllText(sourcePath) != source) throw new IOException("Private Apply wrote saved source before Save.");
            FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == source, "One-step owner Undo did not restore exact source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => ReadEditorText(editor) == edited, "One-step owner Redo did not restore the applied draft.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(sourcePath) == edited, "Ctrl+S did not persist exact Unicode source.");
            source = edited; checks.Add("Private property validation/Apply, one-step owner Undo/Redo and Ctrl+S preserve exact source ownership");
            stage = "isolated-library";
            foreach (var (command, title, name) in new[] { ("SaveDocumentTemplate", "Save document template", "Synthetic host template"), ("SaveMarkdownSnippet", "Save Markdown snippet", "Synthetic host snippet") })
            {
                FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Find(main, "FileMenu").AsMenuItem().Expand(); Find(main, command).AsMenuItem().Invoke();
                Window? prompt = null; Wait(() => (prompt = HostWindows(automation, app!.ProcessId).SingleOrDefault(window => window.Name == title)) is not null, "Native library name prompt did not open.");
                Find(prompt!, "WorkspacePathInput").AsTextBox().Text = name; Find(prompt!, "ConfirmWorkspacePath").AsButton().Invoke();
                Wait(() => Find(main, "Status").Name.Contains("Saved", StringComparison.Ordinal) && Find(main, "Status").Name.Contains(name, StringComparison.Ordinal), "Synthetic library item was not saved.");
            }
            var library = new LensDocsStudio.Core.State.LocalLibraryStore(Path.Combine(state, "content-library")).ReadAsync().GetAwaiter().GetResult();
            if (library.State.Active.Templates.Length != 1 || library.State.Active.Snippets.Length != 1 || ReadEditorText(editor) != source)
                throw new IOException("Actual template/snippet writes did not remain in the synthetic state library or changed the owner.");
            Capture(main, "isolated-library");
            checks.Add("Actual app template/snippet saves use only the explicit synthetic content-library state and retain source");
            stage = "cancel-stale-owner";
            OpenDraft(); Find(visual, "MermaidNodeLabel").AsTextBox().Text = "Cancelled private edit"; Find(visual, "MermaidUpdateNode").AsButton().Invoke();
            Wait(() => Find(visual, "MermaidApply").IsEnabled, "Private Cancel fixture did not validate.");
            Find(visual, "MermaidCancel").AsButton().Invoke(); DiscardPrivate(); WaitModalClosed("MermaidVisualEditor");
            if (ReadEditorText(editor) != source || File.ReadAllText(sourcePath) != source) throw new IOException("Cancel changed the exact owner or saved source.");
            OpenDraft(); Find(visual, "MermaidNodeLabel").AsTextBox().Text = "Private conflict"; Find(visual, "MermaidUpdateNode").AsButton().Invoke();
            Wait(() => Find(visual, "MermaidApply").IsEnabled, "Private conflict fixture did not validate.");
            var privateSource = ReadEditorText(Find(visual, "MermaidDraftSource")); var external = source + "\nExternal synthetic owner\n";
            FocusWindow(main); editor.Patterns.Value.Pattern.SetValue(external); FocusWindow(visual); Find(visual, "MermaidApply").AsButton().Invoke();
            Wait(() => Find(visual, "MermaidStatus").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale-owner replacement was not rejected.");
            if (ReadEditorText(Find(visual, "MermaidDraftSource")) != privateSource || ReadEditorText(editor) != external || File.ReadAllText(sourcePath) != source) throw new IOException("Stale rejection changed the private/external/saved source.");
            Capture(visual, "stale-owner"); Find(visual, "MermaidCancel").AsButton().Invoke(); DiscardPrivate(); WaitModalClosed("MermaidVisualEditor");
            editor.Patterns.Value.Pattern.SetValue(source); FocusWindow(main); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => ReadObservedText(sourcePath) == source, "Synthetic owner did not return to its saved baseline.");
            checks.Add("Dirty private Cancel and stale-owner Apply rejection retain exact private, external and saved source");
            stage = "panes";
            Drag("SidebarSplitter", 24); Drag("EditorSplitter", 24); Drag("OutlineSplitter", -20); Capture(main, "resized-panes");
            checks.Add("Actual pointer drags resize workspace/editor/outline panes");
            stage = "diagram-viewport"; Find(main, "DiagramActionZoom").AsButton().Invoke(); var diagram = Modal("DiagramWindow");
            Find(diagram, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(175);
            Wait(() => Find(diagram, "DiagramZoomLabel").Name == "175%", "Actual diagram zoom did not change.");
            Find(diagram, "DiagramFit").AsButton().Invoke(); Capture(diagram, "diagram-fit");
            var scale = HostWindowDpi(diagram.Properties.NativeWindowHandle.Value) / 96.0;
            diagram.Patterns.Transform.Pattern.Resize(560 * scale, 400 * scale);
            Find(diagram, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(400);
            var viewport = Find(diagram, "DiagramViewport"); var scroll = viewport.Patterns.Scroll.Pattern;
            Wait(() => scroll.HorizontallyScrollable.Value, "Enlarged native drawing did not expose horizontal pan.");
            var viewportBounds = viewport.BoundingRectangle; var panBefore = scroll.HorizontalScrollPercent.Value;
            FocusWindow(diagram); Mouse.DragHorizontally(new System.Drawing.Point((int)(viewportBounds.Left + viewportBounds.Width * .7), (int)(viewportBounds.Top + viewportBounds.Height / 2)), -80, MouseButton.Middle);
            Wait(() => scroll.HorizontalScrollPercent.Value > panBefore, "Actual middle-button pointer drag did not pan the native diagram.");
            Capture(diagram, "diagram-pan"); Find(diagram, "DiagramReset").AsButton().Invoke();
            FocusWindow(diagram); diagram.Close(); WaitModalClosed("DiagramWindow");
            checks.Add("Actual native diagram window accepts 175% zoom, Fit, middle-button pan and Reset at the existing scale");
            stage = "invalid-source"; QuickOpen("invalid.mmd");
            Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramError0")) is not null, "Invalid diagram did not show its source-preserving error.");
            if (ReadEditorText(Find(main, "MarkdownEditor")) != File.ReadAllText(Path.Combine(workspace, "invalid.mmd"))) throw new IOException("Invalid diagram source was rewritten.");
            Find(main, "MarkdownEditor").Focus(); Capture(main, "invalid-source"); QuickOpen("acceptance-lf.md"); editor = Find(main, "MarkdownEditor");
            checks.Add("Invalid standalone source remains exact and directly editable beside its native error");
            stage = "close-restart"; Capture(main, "before-restart"); FocusWindow(main); main.Close(); Wait(() => app!.HasExited, "Synthetic shell did not close normally."); app!.Dispose(); app = null;
            using var preferencesBefore = JsonDocument.Parse(File.ReadAllText(Path.Combine(state, "preferences.json")));
            var desktopBefore = preferencesBefore.RootElement.GetProperty("Desktop").GetRawText();
            Launch(); editor = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(editor) == source, "Saved source did not reopen after restart.");
            Capture(main!, "after-restart");
            using var preferencesAfter = JsonDocument.Parse(File.ReadAllText(Path.Combine(state, "preferences.json")));
            if (preferencesAfter.RootElement.GetProperty("Desktop").GetRawText() != desktopBefore) throw new IOException("Desktop pane preferences did not survive restart.");
            checks.Add("Normal close/restart reopens saved source and preserves desktop pane preferences");
            stage = "close-workspace"; Find(main!, "CloseWorkspace").AsButton().Invoke();
            Wait(() => ReadEditorText(editor) == source && Find(main!, "Status").Name.Contains("closed", StringComparison.OrdinalIgnoreCase), "Close workspace did not retain the open document.");
            Capture(main!, "closed-workspace"); checks.Add("Close workspace retains the open document and source");
            FocusWindow(main!); main!.Close(); Wait(() => app!.HasExited, "Final synthetic shell did not close normally.");
            WriteNew(Path.Combine(output, "completion.json"), new { status = "PASS", scope = "DEVELOPMENT_HOST_SHELL_UI", revision, tree, checks, observations,
                notRun = new[] { "Personal-host shell Open/Save dialogues: shared Windows recent-folder history cannot be isolated; no dialogue invoked", "Inspector drag persistence", "Physical screen reader, alternate DPI, OS High Contrast and installer" } });

            void OpenDraft()
            {
                main.FindAllDescendants().Single(element => (element.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("DiagramEditVisually", StringComparison.Ordinal)).AsButton().Invoke();
                visual = Modal("MermaidVisualEditor"); Wait(() => Find(visual, "MermaidStatus").Name.StartsWith("Ready", StringComparison.Ordinal), "Reopened Flowchart is not ready.");
                Find(visual, "MermaidElements").AsListBox().Select(0);
            }
            void DiscardPrivate()
            {
                AutomationElement? yes = null; Wait(() => (yes = HostWindows(automation, app!.ProcessId).Select(window => window.FindFirstDescendant(cf => cf.ByName("Yes"))).FirstOrDefault(element => element is not null)) is not null, "Synthetic private discard confirmation missing.");
                yes!.AsButton().Invoke();
            }
        }
        catch (Exception error)
        {
            try { if (main is not null) Capture(main, "failure"); } catch (Exception captureError) { File.WriteAllText(Path.Combine(output, "capture-error.txt"), captureError.ToString()); }
            WriteNew(Path.Combine(output, "completion.json"), new { status = "FAIL", scope = "DEVELOPMENT_HOST_SHELL_UI", revision, tree, stage, checks, observations, error = error.ToString(), processId = app?.ProcessId });
            throw;
        }
        finally { app?.Dispose(); }

        void Launch()
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = workspace };
            foreach (var arg in new[] { "--state-directory", state, workspace, sourcePath }) info.ArgumentList.Add(arg);
            app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Shell owner did not open within 25 seconds.");
        }
        Window Modal(string id) { Window? found = null; Wait(() => (found = HostWindowById(automation, app!.ProcessId, id)) is not null, "Native modal missing: " + id); return found!; }
        void WaitModalClosed(string id) => Wait(() => HostWindowById(automation, app!.ProcessId, id) is null, "Native modal did not close: " + id);
        void QuickOpen(string filename)
        {
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P); var picker = Modal("CommandSearchWindow");
            Find(picker, "CommandSearchQuery").AsTextBox().Text = filename;
            Wait(() => Find(picker, "CommandSearchResults").AsListBox().Items.Count(item => item.Name == filename) == 1, "Quick Open did not expose exactly one matching synthetic file identity.");
            Find(picker, "CommandSearchResults").AsListBox().Items.Single(item => item.Name == filename).Patterns.SelectionItem.Pattern.Select();
            FocusWindow(picker); Keyboard.Type(VirtualKeyShort.RETURN); WaitModalClosed("CommandSearchWindow");
            Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == File.ReadAllText(Path.Combine(workspace, filename)), "Quick Open selected different source.");
        }
        void Drag(string id, int pixels)
        {
            FocusWindow(main!); var bounds = Find(main!, id).BoundingRectangle;
            var point = new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
            Mouse.MoveTo(point); Mouse.Down(MouseButton.Left); Mouse.MoveTo(new System.Drawing.Point(point.X + pixels, point.Y)); Mouse.Up(MouseButton.Left);
            observations.Add(new { operation = "Pointer splitter drag", id, pixels, before = bounds.ToString(), after = Find(main!, id).BoundingRectangle.ToString() });
        }
        void Capture(Window window, string name)
        {
            FocusWindow(window); _ = DwmFlush(); window.CaptureToFile(Path.Combine(output, name + ".png"));
            WriteNew(Path.Combine(output, name + ".json"), new { revision, tree, processId = app!.ProcessId, hwnd = window.Properties.NativeWindowHandle.Value.ToInt64(), dpi = HostWindowDpi(window.Properties.NativeWindowHandle.Value), bounds = window.BoundingRectangle.ToString(),
                controls = window.FindAllDescendants().Select(element => new { id = element.Properties.AutomationId.ValueOrDefault, name = element.Properties.Name.ValueOrDefault, focused = element.Properties.HasKeyboardFocus.ValueOrDefault }).ToArray() });
        }
    }
}
