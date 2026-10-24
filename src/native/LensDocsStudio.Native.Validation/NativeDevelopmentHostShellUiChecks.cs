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
            stage = "palette"; FocusWindow(main); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            var palette = Modal("CommandSearchWindow"); Capture(palette, "command-palette");
            Find(palette, "CommandSearchQuery").AsTextBox().Text = "Close workspace";
            Wait(() => Find(palette, "CommandSearchResults").AsListBox().Items.Any(item => item.Name.Contains("Close workspace", StringComparison.Ordinal)), "Palette did not expose its actual workspace command.");
            FocusWindow(palette); Keyboard.Type(VirtualKeyShort.ESCAPE); WaitModalClosed("CommandSearchWindow");
            checks.Add("Command Palette opens, filters its real command and cancels without changing the owner");
            stage = "editor-find"; FocusWindow(main); editor.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(main, "FindContext").Name == "Find in editor", "Ctrl+F did not target the editor.");
            Find(main, "FindText").AsTextBox().Text = "Café"; FocusWindow(main); Find(main, "FindText").Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Café"), "Editor Find/Enter did not select exact Unicode text.");
            Capture(main, "editor-find"); Find(main, "CloseFind").AsButton().Invoke();
            stage = "preview-find";
            var preview = Find(main, "DocumentPreview");
            var range = preview.Patterns.Text.Pattern.DocumentRange.FindText("Only synthetic", false, false) ?? throw new IOException("Native preview text is missing.");
            var bounds = range.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
            FocusWindow(main); Mouse.Click(new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2)));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(main, "FindContext").Name == "Find in preview", "Ctrl+F did not target the focused preview.");
            Find(main, "FindText").AsTextBox().Text = "Café"; Find(main, "FindText").Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
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
            stage = "panes";
            Drag("SidebarSplitter", 24); Drag("EditorSplitter", 24); Drag("OutlineSplitter", -20); Capture(main, "resized-panes");
            checks.Add("Actual pointer drags resize workspace/editor/outline panes");
            stage = "diagram-viewport"; Find(main, "DiagramActionZoom").AsButton().Invoke(); var diagram = Modal("DiagramWindow");
            Find(diagram, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(175);
            Wait(() => Find(diagram, "DiagramZoomLabel").Name == "175%", "Actual diagram zoom did not change.");
            Find(diagram, "DiagramFit").AsButton().Invoke(); Capture(diagram, "diagram-fit");
            FocusWindow(diagram); diagram.Close(); WaitModalClosed("DiagramWindow");
            checks.Add("Actual native diagram window accepts 175% zoom and Fit");
            stage = "exports";
            foreach (var (id, extension, title) in new[] { ("ExportWord", "docx", "Export DOCX"), ("ExportPdf", "pdf", "Export PDF"), ("ExportHtml", "html", "Export HTML") })
            {
                Find(main, "ExportMenu").AsMenuItem().Expand(); Find(main, id).AsMenuItem().Invoke();
                Save(Path.Combine(sandbox, "exports", "host." + extension), title);
            }
            Find(main, "DiagramActionPNG").AsButton().Invoke(); Save(Path.Combine(sandbox, "exports", "host-diagram.png"), null);
            Find(main, "ExportMenu").AsMenuItem().Expand(); Find(main, "ExportDocsSite").AsMenuItem().Invoke();
            var site = ModalByName("Export Docs Site"); Find(site, "DocsSiteTitle").AsTextBox().Text = "Synthetic host documentation";
            Find(site, "ConfirmDocsSite").AsButton().Invoke(); Save(Path.Combine(sandbox, "exports", "host-site.zip"), "Save Docs Site");
            checks.Add("Actual native UI exports DOCX/PDF/HTML/PNG/Docs Site into explicit fresh sandbox destinations");
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
                notRun = new[] { "Actual relative-link pointer activation", "Inspector drag persistence and diagram pan", "Stale-owner/invalid-source UI in this additional shell run; see separately labelled hosted suites", "Physical screen reader, alternate DPI, OS High Contrast and installer" } });
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
        Window Modal(string id) { Window? found = null; Wait(() => (found = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(id)))?.AsWindow()) is not null, "Native modal missing: " + id); return found!; }
        Window ModalByName(string name) { Window? found = null; Wait(() => (found = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(name)))?.AsWindow()) is not null, "Native modal missing: " + name); return found!; }
        void WaitModalClosed(string id) => Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(id))) is null, "Native modal did not close: " + id);
        void QuickOpen(string filename)
        {
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P); var picker = Modal("CommandSearchWindow");
            Find(picker, "CommandSearchQuery").AsTextBox().Text = filename;
            Wait(() => Find(picker, "CommandSearchResults").AsListBox().Items.Length == 1, "Quick Open did not filter the exact synthetic filename.");
            Find(picker, "CommandSearchResults").AsListBox().Select(0); FocusWindow(picker); Keyboard.Type(VirtualKeyShort.RETURN); WaitModalClosed("CommandSearchWindow");
            Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == File.ReadAllText(Path.Combine(workspace, filename)), "Quick Open selected different source.");
        }
        void Drag(string id, int pixels)
        {
            FocusWindow(main!); var bounds = Find(main!, id).BoundingRectangle;
            var point = new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
            Mouse.MoveTo(point); Mouse.Down(MouseButton.Left); Mouse.MoveTo(new System.Drawing.Point(point.X + pixels, point.Y)); Mouse.Up(MouseButton.Left);
            observations.Add(new { operation = "Pointer splitter drag", id, pixels, before = bounds.ToString(), after = Find(main!, id).BoundingRectangle.ToString() });
        }
        void Save(string destination, string? title)
        {
            if (!destination.StartsWith(Path.Combine(sandbox, "exports") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || File.Exists(destination)) throw new IOException("Only a fresh sandbox export destination is allowed.");
            Window? dialogue = null;
            Wait(() => (dialogue = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(title is null ? cf.ByClassName("#32770") : cf.ByName(title)))?.AsWindow()) is not null, "Actual Save dialogue did not open.");
            TextBox? field = null;
            Wait(() => (field = dialogue!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit).And(cf.ByName("File name:")))?.AsTextBox())?.IsEnabled == true, "Labelled export filename field is missing.");
            field!.Text = destination;
            if (field.Text != destination) throw new IOException("Export destination was not accepted exactly.");
            // A shell picker can list unrelated recent folders. Retain only its
            // exact labelled synthetic destination, never its private navigation tree.
            observations.Add(new { operation = "Save dialogue destination", file = Path.GetFileName(destination), hwnd = dialogue!.Properties.NativeWindowHandle.Value.ToInt64(), field = field.Name, exactDestination = field.Text == destination });
            dialogue!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Save")))!.AsButton().Invoke();
            Wait(() => File.Exists(destination) && new FileInfo(destination).Length > 0, "Export did not produce the chosen file.");
            observations.Add(new { operation = "Real UI export", file = Path.GetFileName(destination), bytes = new FileInfo(destination).Length, sha256 = Hash(destination) });
        }
        void Capture(Window window, string name)
        {
            FocusWindow(window); _ = DwmFlush(); window.CaptureToFile(Path.Combine(output, name + ".png"));
            WriteNew(Path.Combine(output, name + ".json"), new { revision, tree, processId = app!.ProcessId, hwnd = window.Properties.NativeWindowHandle.Value.ToInt64(), dpi = HostWindowDpi(window.Properties.NativeWindowHandle.Value), bounds = window.BoundingRectangle.ToString(),
                controls = window.FindAllDescendants().Select(element => new { id = element.Properties.AutomationId.ValueOrDefault, name = element.Properties.Name.ValueOrDefault, focused = element.Properties.HasKeyboardFocus.ValueOrDefault }).ToArray() });
        }
    }
}
