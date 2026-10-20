using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.Mindmaps;
using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunSourceEditor(string executable, string output, string family, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var observations = new List<object>();
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var path = Path.Combine(root, family + ".md"); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var starter = family switch { "Gantt" => GanttVisualDraft.Starter, "Mindmap" => MindmapVisualDraft.Starter, "Pie" => PieVisualDraft.Starter, _ => throw new ArgumentException("Unknown authoring family.") };
        var source = starter.ReplaceLineEndings("\r\n"); var block = "```mermaid\r\n" + source + "```\r\n";
        var original = "# Eight-family authoring\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        File.WriteAllText(path, original); Application? app = null; Window? main = null, visual = null;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active");
            Launch(path); var owner = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(owner) == original, "Fixture did not load exactly.");
            Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(family == "Gantt" ? 3 : 1); Capture("selected");
            Set("Label", "Edited Ω"); Click("Update"); Await(() => Draft().Contains("Edited Ω", StringComparison.Ordinal)); var edited = Draft();
            Click("Undo"); Await(() => Draft() == source); Click("Redo"); Await(() => Draft() == edited);
            // Actual keyboard input can remain incomplete without resetting the inspector or selection.
            var field = Find(visual!, family == "Pie" ? "PieValue" : family == "Gantt" ? "GanttDuration" : "MindmapLabel");
            field.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(family == "Mindmap" ? "Pending branch" : "1.");
            Wait(() => field.AsTextBox().Text == (family == "Mindmap" ? "Pending branch" : "1."), "Incomplete native input did not remain in its field.");
            if (family != "Mindmap") { Click("Update"); Wait(() => Find(visual!, family + "Status").Name.Contains("complete", StringComparison.OrdinalIgnoreCase) || Find(visual!, family + "Status").Name.Contains("whole-day", StringComparison.OrdinalIgnoreCase), "Actionable invalid numeric feedback was not exposed."); Check(Draft() == edited && field.AsTextBox().Text == "1.", "Rejected input retains pending text and exact validated draft"); }
            Click("Reset"); Wait(() => Find(visual!, family + "Apply").IsEnabled, "Reset did not restore the current validated properties.");
            Click(family == "Pie" ? "AddCategory" : family == "Gantt" ? "AddTask" : "AddChild"); Await(() => Draft() != edited); var added = Draft();
            Click("MoveUp");
            if (family == "Mindmap") Wait(() => !string.IsNullOrWhiteSpace(Find(visual!, family + "Status").Name), "Structural feedback missing.");
            else Await(() => Draft() != added);
            if (family == "Mindmap")
            {
                Click("AddSibling"); Await(() => Draft().Split("New branch", StringSplitOptions.None).Length == 3);
                Click("Indent"); Await(() => new MindmapVisualDraft(Draft()).Model.Nodes.Any(node => node.Depth >= 3)); Click("Outdent"); Await(() => Find(visual!, family + "Apply").IsEnabled);
            }
            else if (family == "Gantt") { Click("AddSection"); Await(() => Draft().Contains("section New section", StringComparison.Ordinal)); Click("AddTask"); Await(() => new GanttVisualDraft(Draft()).Model.Tasks.Count == 5); }
            Click("Delete"); Await(() => Find(visual!, family + "Apply").IsEnabled);
            var changed = Draft(); Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Private edits preserve owner source and saved bytes");
            foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
            {
                visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Click("Fit");
                Check(visual.BoundingRectangle.Contains(Find(visual, family + "Apply").BoundingRectangle), "Apply visible at " + width + "x" + height);
                Select(family == "Gantt" ? 3 : 1); var input = Find(visual, family + "Label"); input.Focus(); Wait(() => !input.IsOffscreen, "Inspector keyboard focus is off screen."); Capture("viewport-" + width + "x" + height);
            }
            if (!highContrast)
            {
                foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Wait(() => Find(visual!, family + "Apply").IsEnabled && visual!.Properties.ItemStatus.ValueOrDefault == theme, "Owned appearance did not settle."); Capture(theme.ToLowerInvariant()); }
            }
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("Apply"); WaitClosed(); Wait(() => ReadEditorText(owner) == expected, "Apply changed the wrong diagram or neighbouring bytes.");
            FocusWindow(main!); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(owner) == original, "Apply was not one owner Undo action.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(owner) == expected, "Owner Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Save failed.");
            Check(true, "Multiple identical fences retain ownership; Apply, one-step owner Undo/Redo and Save pass");
            main!.Close(); Wait(() => app!.HasExited, "Saved application did not close gracefully."); app.Dispose(); app = null;
            Launch(path); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Reopen starts from exact applied source");
            Select(family == "Gantt" ? 3 : 1); Set("Label", "Cancelled private edit"); Click("Update"); Await(() => Draft().Contains("Cancelled private edit", StringComparison.Ordinal)); Click("Cancel"); Discard(); WaitClosed(); Check(File.ReadAllText(path) == expected && ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Cancel retains owner and saved source");
            // A stale editor must retain its private draft and reject replacement of changed owner source.
            Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(family == "Gantt" ? 3 : 1); Set("Label", "Private conflict"); Click("Update"); Await(() => Draft().Contains("Private conflict", StringComparison.Ordinal)); var privateSource = Draft();
            var external = expected.Replace(changed, source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal); FocusWindow(main!); Find(main!, "MarkdownEditor").Patterns.Value.Pattern.SetValue(external); FocusWindow(visual!); Click("Apply");
            Wait(() => Find(visual!, family + "Status").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale-owner Apply was not rejected."); Check(Draft() == privateSource && ReadEditorText(Find(main!, "MarkdownEditor")) == external, "Stale-owner rejection preserves private and external source"); Capture("owner-conflict"); Click("Cancel"); Discard(); WaitClosed(); Stop();
            // Standalone documents use the same normal preview entry point and current source.
            var standalone = Path.Combine(root, family + ".mmd"); File.WriteAllText(standalone, source); Launch(standalone); Open(0); Check(Draft() == source, "Standalone Mermaid uses its native editor"); Click("Cancel"); WaitClosed(); Stop();
            Check(true, "Real UIA list selection, native field input, structural commands, private Undo/Redo, Apply/Cancel, owner Undo, Save/reopen and stale rejection exercised");
            File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", Family = family, SourceRevision = typeof(PieVisualEditorWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        { try { visual?.CaptureToFile(Path.Combine(output, "failure.png")); } catch { } File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Family = family, Checks = checks, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1; }
        finally { Stop(); }

        void Launch(string fixture)
        { var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var argument in new[] { "--state-directory", state, root, fixture }) info.ArgumentList.Add(argument); app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Native application did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800); }
        void Stop() { if (app is null) return; if (!app.HasExited) app.Kill(); app.Dispose(); app = null; main = visual = null; }
        void Open(int offset)
        {
            FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact diagram action missing."); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke();
            Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(family + "VisualEditor")))?.AsWindow()) is not null, "Native family editor did not open.");
            Wait(() => Find(visual!, family + "Apply").IsEnabled, "Canonical native draft did not become valid: " + Find(visual!, family + "Status").Name);
            Find(visual!, family + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("Fit");
        }
        void Select(int index) { var list = Find(visual!, family + "Elements").AsListBox(); list.Select(index); Wait(() => list.SelectedItem is not null && Find(visual!, family + "Label").IsEnabled, "Semantic selection did not reach inspector."); }
        string Draft() => ReadEditorText(Find(visual!, family + "SourceEditor"));
        void Set(string id, string value) => Find(visual!, family + id).AsTextBox().Text = value;
        void Click(string id) { Wait(() => Find(visual!, family + id).IsEnabled, "Command unavailable: " + family + id + "; " + Find(visual!, family + "Status").Name); Find(visual!, family + id).AsButton().Invoke(); }
        void Await(Func<bool> condition) => Wait(() => condition() && Find(visual!, family + "Apply").IsEnabled, "Expected draft did not validate: " + Find(visual!, family + "Status").Name);
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(family + "VisualEditor"))) is null, "Editor did not close."); visual = null; }
        void Discard() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "Discard confirmation missing."); automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); }
        void Capture(string name) { if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("Native editor remained busy before capture."); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Check(bool success, string message) { if (!success) throw new IOException(message); checks.Add(message); }
    }).GetAwaiter().GetResult();
}
