using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.TimelineDiagrams;
using LensDocsStudio.Core.JourneyDiagrams;
using LensDocsStudio.Core.QuadrantDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunExpansionEditor(string executable, string output, string family, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var observations = new List<object>();
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var path = Path.Combine(root, family + ".md"); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var starter = family switch { "Timeline" => TimelineVisualDraft.Starter, "Journey" => JourneyVisualDraft.Starter, "Quadrant" => QuadrantVisualDraft.Starter, _ => throw new ArgumentException("Unknown expansion family.") };
        var source = starter.ReplaceLineEndings("\r\n"); var block = "```mermaid\r\n" + source + "```\r\n";
        var original = "# Eleven-family authoring\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        var first = family == "Quadrant" ? 7 : 3;
        File.WriteAllText(path, original); Application? app = null; Window? main = null, visual = null;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active");
            Launch(path); var owner = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(owner) == original, "Fixture did not load exactly.");
            Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Capture("selected");
            using (var independent = new UIA3Automation())
            {
                var second = independent.FromHandle(visual!.Properties.NativeWindowHandle.Value).AsWindow();
                Check(Find(second, family + "Elements").AsListBox().SelectedItem is not null && Find(second, family + "Label").AsTextBox().Text == Find(visual!, family + "Label").AsTextBox().Text,
                    "Independent UIA connections agree on occurrence selection and persistent inspector");
            }
            // Compute the pointer from actual ordinary SVG geometry and the current native viewport.
            Select(family == "Quadrant" ? 8 : family == "Timeline" ? 8 : 6); Click("Fit");
            var rendered = new DiagramService().Render(Draft()); var svgRoot = XDocument.Parse(rendered.Svg!).Root!;
            var group = SourceDeclarations.Groups(rendered.Svg!, family == "Quadrant" ? "quadrant-point" : family == "Timeline" ? "timeline-period" : "journey-task")[0];
            var bounds = NativeSourceHit.ShapeBounds(group, svgRoot)!.Value;
            var viewport = Find(visual!, family + "Viewport").BoundingRectangle; var scale = Find(visual!, family + "Zoom").Patterns.RangeValue.Pattern.Value.Value / 100;
            var sx = viewport.Left + (bounds.X + bounds.Width / 2) * scale; var sy = viewport.Top + (bounds.Y + bounds.Height / 2) * scale;
            observations.Add(new { Operation = "Canonical pointer", Bounds = bounds.ToString(), Viewport = viewport.ToString(), Scale = scale, ScreenX = sx, ScreenY = sy });
            FocusWindow(visual!); Mouse.MoveTo((int)Math.Round(sx), (int)Math.Round(sy)); Mouse.Click(MouseButton.Left);
            Wait(() => Find(visual!, family + "Label").AsTextBox().Text == (family == "Quadrant" ? "Guides" : family == "Timeline" ? "First review" : "Write guide"), "Canonical pointer did not select the first source occurrence.");
            Check(true, "Real canonical pointer selection resolves occurrence geometry through the ordinary native adapter");
            Set("Label", "Edited Ω"); Click("Update"); Await(() => Draft().Contains("Edited Ω", StringComparison.Ordinal)); var edited = Draft();
            Click("Undo"); Await(() => Draft() == source); Click("Redo"); Await(() => Draft() == edited);
            var fieldId = family == "Timeline" ? "Label" : family == "Journey" ? "Score" : "X";
            var field = Find(visual!, family + fieldId); var invalid = family == "Timeline" ? "Invalid: event" : "1.";
            field.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(invalid);
            Wait(() => field.AsTextBox().Text == invalid, "Incomplete native input did not remain in its field."); Click("Update");
            Wait(() => Find(visual!, family + "Status").Name.Contains(family == "Timeline" ? "separator" : "complete", StringComparison.OrdinalIgnoreCase), "Precise pending-input feedback is missing.");
            Check(Draft() == edited && field.AsTextBox().Text == invalid && !Find(visual!, family + "Apply").IsEnabled, "Invalid pending input preserves the last valid source and cannot Apply"); Click("Reset"); Await(() => Draft() == edited);
            if (family == "Quadrant") { Set("X", "0.125000"); Set("Y", "0.875000"); Click("Update"); Await(() => Draft().Contains("[0.125000, 0.875000]", StringComparison.Ordinal)); }
            else if (family == "Journey") { Set("Score", "5"); Click("Update"); Await(() => new JourneyVisualDraft(Draft()).Model.Tasks[0].Score == "5"); Click("AddActor"); Await(() => Draft().Contains("New actor", StringComparison.Ordinal)); Set("Label", "Reviewer: Ω"); Click("Update"); Await(() => Draft().Contains("Reviewer: Ω", StringComparison.Ordinal)); Click("MoveUp"); Await(() => Find(visual!, family + "Label").AsTextBox().Text == "Reviewer: Ω"); Click("Delete"); Await(() => !Draft().Contains("Reviewer: Ω", StringComparison.Ordinal)); Select(first); }
            else { Click("AddEvent"); Await(() => Draft().Contains("New event", StringComparison.Ordinal)); Set("Label", "New continuation Ω"); Click("Update"); Await(() => Draft().Contains("New continuation Ω", StringComparison.Ordinal)); Click("MoveUp"); Await(() => Find(visual!, family + "Label").AsTextBox().Text == "New continuation Ω"); Click("Delete"); Await(() => !Draft().Contains("New continuation Ω", StringComparison.Ordinal)); Select(first); }
            var beforeAdd = Draft(); Click(family == "Quadrant" ? "AddPoint" : family == "Journey" ? "AddTask" : "AddPeriod"); Await(() => Draft() != beforeAdd); var added = Draft();
            Click("MoveUp"); Await(() => Draft() != added); Click("Undo"); Await(() => Draft() == added); Click("Redo"); Await(() => Draft() != added); Click("Delete"); Await(() => Draft() == beforeAdd);
            if (family != "Quadrant")
            {
                Select(first); var combo = Find(visual!, family + "Destination").AsComboBox(); combo.Patterns.ExpandCollapse.Pattern.Expand(); combo.Select("Unsectioned"); combo.Patterns.ExpandCollapse.Pattern.Collapse(); Click("MoveToSection"); Await(() => Draft() != beforeAdd);
                Click("Undo"); Await(() => Draft() == beforeAdd); Click("AddSection"); Await(() => Draft().Contains("section New section", StringComparison.Ordinal)); var sectionAdded = Draft();
                Click("MoveUp"); Await(() => Draft() != sectionAdded); Click("Undo"); Await(() => Draft() == sectionAdded); Click("Delete"); Await(() => Draft() == beforeAdd);
            }
            var changed = Draft(); Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Private edits preserve the owner and saved bytes");
            foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
            {
                visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Click("Fit"); Select(first);
                var input = Find(visual!, family + "Label"); input.Focus(); Wait(() => !input.IsOffscreen, "Inspector keyboard focus is off screen.");
                Check(visual.BoundingRectangle.Contains(Find(visual, family + "Apply").BoundingRectangle), "Apply and focused field are reachable at " + width + "x" + height); Capture("viewport-" + width + "x" + height);
            }
            if (!highContrast)
                foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Wait(() => Find(visual!, family + "Apply").IsEnabled && visual!.Properties.ItemStatus.ValueOrDefault == theme, "Owned appearance did not settle."); Capture(theme.ToLowerInvariant()); }
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("Apply"); WaitClosed(); Wait(() => ReadEditorText(owner) == expected, "Apply changed the wrong diagram or neighbouring bytes.");
            FocusWindow(main!); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(owner) == original, "Apply was not one owner Undo action.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(owner) == expected, "Owner Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Save failed.");
            Check(true, "Second identical fence Apply, one-step owner Undo/Redo and Save preserve neighbouring source"); main!.Close(); Wait(() => app!.HasExited, "Saved application did not close."); app!.Dispose(); app = null;
            Launch(path); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Reopen uses exact applied source");
            Select(first); Set("Label", "Cancelled private edit"); Click("Update"); Await(() => Draft().Contains("Cancelled private edit", StringComparison.Ordinal)); Click("Cancel"); Discard(); WaitClosed(); Check(File.ReadAllText(path) == expected && ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Cancel retains owner source");
            Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Set("Label", "Private conflict"); Click("Update"); Await(() => Draft().Contains("Private conflict", StringComparison.Ordinal)); var privateSource = Draft();
            var external = expected.Replace(changed, source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal); FocusWindow(main!); Find(main!, "MarkdownEditor").Patterns.Value.Pattern.SetValue(external); FocusWindow(visual!); Click("Apply");
            Wait(() => Find(visual!, family + "Status").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale owner was not rejected."); Check(Draft() == privateSource && ReadEditorText(Find(main!, "MarkdownEditor")) == external, "Stale-owner rejection retains both sources"); Capture("owner-conflict"); Click("Cancel"); Discard(); WaitClosed(); Stop();
            var standalone = Path.Combine(root, family + ".mmd"); File.WriteAllText(standalone, source); Launch(standalone); Open(0); Check(Draft() == source, "Standalone Mermaid uses the same normal editor route"); Click("Cancel"); WaitClosed();
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "Insert" + family).AsMenuItem().Invoke(); WaitVisual(); Check(Draft() == starter, "Normal Insert menu uses the family starter"); Click("Apply"); WaitClosed();
            var inserted = Find(main!, "MarkdownEditor"); Check(ReadEditorText(inserted).Contains(starter.TrimEnd('\r', '\n'), StringComparison.Ordinal), "Insertion produces portable Mermaid"); FocusWindow(main!); inserted.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(inserted) == "", "Insertion was not one Undo action."); Stop();
            ProtectedAndNavigation();
            File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", Family = family, SourceRevision = typeof(QuadrantVisualEditorWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error) { try { visual?.CaptureToFile(Path.Combine(output, "failure.png")); } catch { } File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Family = family, Checks = checks, Observations = observations, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1; }
        finally { Stop(); }
        void Launch(string fixture) { var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var argument in new[] { "--state-directory", state, root, fixture }) info.ArgumentList.Add(argument); app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Native application did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800); }
        void Stop() { if (app is null) return; if (!app.HasExited) app.Kill(); app.Dispose(); app = null; main = visual = null; }
        void Open(int offset) { var watch = Stopwatch.StartNew(); FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact diagram action missing."); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); using var process = Process.GetProcessById(app!.ProcessId); observations.Add(new { Operation = "Open canonical editor and UIA", Milliseconds = watch.Elapsed.TotalMilliseconds, WorkingSetBytes = process.WorkingSet64, SourceCharacters = Draft().Length }); }
        void WaitVisual() { Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(family + "VisualEditor")))?.AsWindow()) is not null, "Family editor missing."); Wait(() => Find(visual!, family + "Apply").IsEnabled, "Canonical draft invalid: " + Find(visual!, family + "Status").Name); Find(visual!, family + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("Fit"); }
        void Select(int index) { var list = Find(visual!, family + "Elements").AsListBox(); list.Select(index); Wait(() => list.SelectedItem is not null && Find(visual!, family + "Label").IsEnabled, "Semantic inspector selection failed."); }
        string Draft() => ReadEditorText(Find(visual!, family + "SourceEditor"));
        void Set(string id, string value) => Find(visual!, family + id).AsTextBox().Text = value;
        void Click(string id) { Wait(() => Find(visual!, family + id).IsEnabled, "Command unavailable: " + family + id + "; " + Find(visual!, family + "Status").Name); Find(visual!, family + id).AsButton().Invoke(); }
        void Await(Func<bool> condition) => Wait(() => condition() && Find(visual!, family + "Apply").IsEnabled, "Expected draft did not validate: " + Find(visual!, family + "Status").Name);
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(family + "VisualEditor"))) is null, "Editor did not close."); visual = null; }
        void Discard() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes"))) is not null, "Discard confirmation missing."); automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))!.AsButton().Invoke(); }
        void Capture(string name) { if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("Editor remained busy before capture."); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Check(bool success, string message) { if (!success) throw new IOException(message); checks.Add(message); }
        void ProtectedAndNavigation()
        {
            var mixed = family switch { "Timeline" => "timeline\n Same : One : Two\n ignored bytes\n  : Three\n Same : Four\n", "Journey" => "journey\n Same: 9: Me, Me\n ignored bytes\n Same: 3:\n", _ => "quadrantChart\n Same: [9, -2]\n ignored bytes\n Same: [.5, .50]\n" };
            var fixture = Path.Combine(root, "protected.mmd"); File.WriteAllText(fixture, mixed); Launch(fixture); Open(0); Select(family == "Quadrant" ? 7 : 2);
            Check(Find(visual!, family + "Capabilities").Name.Contains(family == "Timeline" ? "continuation" : "Raw", StringComparison.OrdinalIgnoreCase), "Protection/raw-value explanation is visible");
            Click("MoveDown"); Wait(() => Find(visual!, family + "Status").Name.Contains("protect", StringComparison.OrdinalIgnoreCase), "Mixed-source move protection missing."); Check(Draft() == mixed, "Rejected structural move retains unknown source and raw values");
            Set("Label", "Safe local Ω"); Click("Update"); Await(() => Draft().Contains("Safe local Ω", StringComparison.Ordinal)); Check(Draft().Contains("ignored bytes", StringComparison.Ordinal) && Draft().Contains(family == "Timeline" ? ": Three" : family == "Journey" ? "9: Me, Me" : "[9, -2]", StringComparison.Ordinal), "Safe local editing retains continuation/actor/raw-coordinate source"); Capture("protected-local-edit"); Click("Cancel"); Discard(); WaitClosed(); Stop();
            var many = family switch { "Timeline" => "timeline\n" + string.Concat(Enumerable.Range(0, 40).Select(i => " Period " + i + " : Event " + i + "\n")), "Journey" => "journey\n" + string.Concat(Enumerable.Range(0, 40).Select(i => " Task " + i + ": 3\n")), _ => "quadrantChart\n" + string.Concat(Enumerable.Range(0, 40).Select(i => " Point " + i + ": [.5, .5]\n")) };
            fixture = Path.Combine(root, "navigation.mmd"); File.WriteAllText(fixture, many); Launch(fixture); Open(0); var list = Find(visual!, family + "Elements").AsListBox(); list.Focus(); Keyboard.Type(VirtualKeyShort.END);
            Wait(() => list.SelectedItem is not null && Find(visual!, family + "Label").AsTextBox().Text.EndsWith("39", StringComparison.Ordinal), "End did not reach off-screen full-name occurrence."); Check(!list.SelectedItem!.IsOffscreen, "Off-screen keyboard navigation reveals the selected occurrence"); Capture("off-screen-navigation"); Click("Cancel"); WaitClosed(); Stop();
        }
    }).GetAwaiter().GetResult();
}
