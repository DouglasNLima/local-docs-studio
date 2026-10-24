using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.XyCharts;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunChartEditor(string executable, string output, string family, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var observations = new List<object>(); var prefix = family == "Xy" ? "XYChart" : family;
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var starter = family switch { "Xy" => XyVisualDraft.Starter, "Radar" => RadarVisualDraft.Starter, "Sankey" => SankeyVisualDraft.Starter, _ => throw new ArgumentException("Unknown chart family.") };
        var source = starter.ReplaceLineEndings("\r\n"); var block = "```mermaid\r\n" + source + "```\r\n";
        var original = "# Fourteen-family authoring Ω\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        var path = Path.Combine(root, family + ".md"); File.WriteAllText(path, original); Application? app = null; Window? main = null, visual = null;
        var first = family == "Xy" ? "Series 1 · bar" : family == "Radar" ? "Curve 1 ·" : "Link 1 ·";
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active; UIA is not physical screen-reader certification");
            Launch(path); var owner = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(owner) == original, "Fixture did not load exactly.");
            Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Capture("selected");
            using (var independent = new UIA3Automation())
            {
                var second = independent.FromHandle(visual!.Properties.NativeWindowHandle.Value).AsWindow();
                Check(Find(second, prefix + "Elements").AsListBox().SelectedItem?.Name == Find(visual!, prefix + "Elements").AsListBox().SelectedItem?.Name,
                    "Independent native UIA connections agree on source occurrence selection");
            }
            Pointer();
            Select(first); Set("Label", "Authored Ω"); Click("Update"); Await(() => Draft().Contains("Authored Ω", StringComparison.Ordinal)); var authored = Draft();
            Click("Undo"); Await(() => Draft() == source); Click("Redo"); Await(() => Draft() == authored);
            Select(family == "Xy" ? "Series 1 · value 1" : family == "Radar" ? "Curve 1 · axis 1" : "Link 1 ·");
            var beforeInvalid = Draft(); var field = Find(visual!, prefix + "Value"); field.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type("1e");
            Wait(() => field.AsTextBox().Text == "1e", "Incomplete numeric text was not retained."); Click("Update");
            Wait(() => Find(visual!, prefix + "Status").Name.Contains("finite", StringComparison.OrdinalIgnoreCase), "Actionable numeric feedback missing.");
            Check(Draft() == beforeInvalid && field.AsTextBox().Text == "1e" && !Find(visual!, prefix + "Apply").IsEnabled, "Invalid pending input retains last valid draft, remains visible and disables Apply"); Click("Reset"); Await(() => Draft() == beforeInvalid);
            Set("Value", "3.75000"); Click("Update"); Await(() => Draft().Contains("3.75000", StringComparison.Ordinal));
            var beforeAdd = Draft(); Click(family == "Xy" ? "AddBar" : family == "Radar" ? "AddAxis" : "AddLink"); Await(() => Draft() != beforeAdd); var added = Draft();
            Check(Find(visual!, prefix + "Capabilities").Name.Length > 20, "Persistent inspector exposes operation and protection context");
            Click("MoveUp"); Await(() => Draft() != added); Click("Undo"); Await(() => Draft() == added); Click("Redo"); Await(() => Draft() != added); Click("Delete"); Await(() => Draft() == beforeAdd);
            var changed = Draft(); Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Private source, inspector and history never mutate owner or disk before Apply");
            foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
            {
                visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Click("Fit"); Select(first);
                var input = Find(visual!, prefix + "Label"); input.Focus(); Wait(() => !input.IsOffscreen, "Focused inspector input is off screen.");
                Check(visual.BoundingRectangle.Contains(Find(visual, prefix + "Apply").BoundingRectangle), "Apply and inspector remain reachable at " + width + "x" + height); Capture("viewport-" + width + "x" + height);
            }
            if (!highContrast)
                foreach (var theme in new[] { "Dark", "Light" })
                { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Wait(() => Find(visual!, prefix + "Apply").IsEnabled && visual!.Properties.ItemStatus.ValueOrDefault == theme, "Owned Light/Dark appearance did not settle."); Capture(theme.ToLowerInvariant()); }
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("Apply"); WaitClosed(); Wait(() => ReadEditorText(owner) == expected, "Apply changed the wrong fence or neighbouring bytes.");
            FocusWindow(main!); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(owner) == original, "Apply was not one owner Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(owner) == expected, "Owner Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Save failed.");
            Check(true, "Exact second identical fence Apply, one-step owner Undo/Redo and Save preserve every neighbouring byte"); main!.Close(); Wait(() => app!.HasExited, "Saved application did not close."); app!.Dispose(); app = null;
            Launch(path); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Reopened chart retains exact authored source and numeric precision");
            Select(first); Set("Label", "Cancelled private Ω"); Click("Update"); Await(() => Draft().Contains("Cancelled private Ω", StringComparison.Ordinal)); Click("Cancel"); Discard(); WaitClosed();
            Check(File.ReadAllText(path) == expected && ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Cancel preserves exact saved and owner source");
            Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Set("Label", "Private conflict Ω"); Click("Update"); Await(() => Draft().Contains("Private conflict Ω", StringComparison.Ordinal)); var privateSource = Draft();
            var external = expected.Replace(changed, source.ReplaceLineEndings("\n"), StringComparison.Ordinal); FocusWindow(main!); Find(main!, "MarkdownEditor").Patterns.Value.Pattern.SetValue(external); FocusWindow(visual!); Click("Apply");
            Wait(() => Find(visual!, prefix + "Status").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale owner edit was not rejected."); Check(Draft() == privateSource && ReadEditorText(Find(main!, "MarkdownEditor")) == external, "Stale Apply preserves both private and changed owner source"); Capture("owner-conflict"); Click("Cancel"); Discard(); WaitClosed(); Stop();
            var standalone = Path.Combine(root, family + ".mmd"); File.WriteAllText(standalone, source); Launch(standalone); Open(0); Check(Draft() == source, "Standalone Mermaid uses the ordinary visual authoring route"); Click("Cancel"); WaitClosed();
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "Insert" + family).AsMenuItem().Invoke(); WaitVisual(); Check(Draft() == starter, "Insert uses the correct full-family editor and portable starter"); Click("Apply"); WaitClosed();
            var inserted = Find(main!, "MarkdownEditor"); Check(ReadEditorText(inserted).Contains(starter.TrimEnd('\r', '\n'), StringComparison.Ordinal), "Insert produces portable Mermaid source"); FocusWindow(main!); inserted.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(inserted) == "", "Insert was not one owner Undo step."); Stop();
            NavigationAndProtection();
            File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", Family = family, SourceRevision = typeof(XyVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(e => e.ControlType + ": " + e.AutomationId + " · " + e.Name + " · enabled=" + e.IsEnabled) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Family = family, Checks = checks, Observations = observations, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { Stop(); }
        void Launch(string fixture) { var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var arg in new[] { "--state-directory", state, root, fixture }) info.ArgumentList.Add(arg); app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Native application did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800); }
        void Stop() { if (app is null) return; if (!app.HasExited) app.Kill(); app.Dispose(); app = null; main = visual = null; }
        void Open(int offset) { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact diagram context action missing."); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); }
        void WaitVisual() { Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(prefix + "VisualEditor")))?.AsWindow()) is not null, "Chart editor missing."); Wait(() => Find(visual!, prefix + "Apply").IsEnabled, "Canonical draft invalid: " + Find(visual!, prefix + "Status").Name); Find(visual!, prefix + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("Fit"); }
        void Select(string name)
        {
            var list = Find(visual!, prefix + "Elements"); var row = list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).First(e => e.Name.StartsWith(name, StringComparison.Ordinal));
            row.Patterns.SelectionItem.Pattern.Select(); Wait(() => list.AsListBox().SelectedItem?.Name.StartsWith(name, StringComparison.Ordinal) == true, "Occurrence list selection failed.");
        }
        string Draft() => ReadEditorText(Find(visual!, prefix + "SourceEditor"));
        void Set(string id, string value) => Find(visual!, prefix + id).AsTextBox().Text = value;
        void Click(string id) { Wait(() => Find(visual!, prefix + id).IsEnabled, "Command unavailable: " + prefix + id + "; " + Find(visual!, prefix + "Status").Name); Find(visual!, prefix + id).AsButton().Invoke(); }
        void Await(Func<bool> condition) => Wait(() => condition() && Find(visual!, prefix + "Apply").IsEnabled, "Expected draft did not validate: " + Find(visual!, prefix + "Status").Name);
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(prefix + "VisualEditor"))) is null, "Editor did not close."); visual = null; }
        void Discard() { AutomationElement? yes = null; Wait(() => (yes = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))) is not null, "Discard confirmation missing."); yes!.AsButton().Invoke(); }
        void Capture(string name) { if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("Editor remained busy before capture."); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Pointer()
        {
            Click("Fit"); var render = new DiagramService().Render(Draft()); var svgRoot = XDocument.Parse(render.Svg!).Root!;
            XElement shape = family switch { "Xy" => SourceDeclarations.Groups(render.Svg!, "xy-bars")[0].Elements().First(), "Radar" => SourceDeclarations.Groups(render.Svg!, "radar-curve")[0].Elements().First(e => e.Name.LocalName == "circle"), _ => SourceDeclarations.Groups(render.Svg!, "sankey-link")[0] };
            var geometry = NativeSourceHit.ShapeGeometry(shape, svgRoot)!; var bounds = geometry.Bounds; System.Windows.Point? point = null;
            for (var x = 1; x < 20 && point is null; x++) for (var y = 1; y < 20 && point is null; y++) { var candidate = new System.Windows.Point(bounds.X + bounds.Width * x / 20, bounds.Y + bounds.Height * y / 20); if (geometry.FillContains(candidate)) point = candidate; }
            Check(point is not null, "Actual canonical pointer shape has a visible interior"); var viewport = Find(visual!, prefix + "Viewport").BoundingRectangle; var scale = Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value / 100;
            var sx = viewport.Left + point!.Value.X * scale; var sy = viewport.Top + point.Value.Y * scale; observations.Add(new { Operation = "Canonical pointer", Bounds = bounds.ToString(), Scale = scale, ScreenX = sx, ScreenY = sy });
            FocusWindow(visual!); Mouse.MoveTo((int)Math.Round(sx), (int)Math.Round(sy)); Mouse.Click(MouseButton.Left);
            Wait(() => Find(visual!, prefix + "Value").AsTextBox().Text == (family == "Xy" ? "-5" : family == "Radar" ? "2" : "3.000"), "Real pointer did not select the canonical source value/ribbon occurrence.");
            Check(true, "Real pointer selection follows actual native geometry and exact source occurrence");
        }
        void NavigationAndProtection()
        {
            var many = family switch {
                "Xy" => "xychart\n x-axis [A,B,C]\n" + string.Concat(Enumerable.Range(0, 35).Select(i => " bar \"Series " + i + "\" [1,2,3]\n")),
                "Radar" => "radar-beta\n axis A,B,C,D\n" + string.Concat(Enumerable.Range(0, 35).Select(i => " curve c" + i + "{1,2,3,4}\n")),
                _ => "sankey-beta\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "A" + i + ",N" + i + ",1\n")) };
            var fixture = Path.Combine(root, "navigation.mmd"); File.WriteAllText(fixture, many); Launch(fixture); Open(0); var list = Find(visual!, prefix + "Elements").AsListBox(); list.Focus(); Keyboard.Type(VirtualKeyShort.END);
            Wait(() => list.SelectedItem is not null && !list.SelectedItem.IsOffscreen && (family == "Sankey" ? Find(visual!, prefix + "Label").AsTextBox().Text == "N39" : Find(visual!, prefix + "Value").AsTextBox().Text == (family == "Xy" ? "3" : "4")), "Keyboard End did not reveal the off-screen occurrence and persistent inspector.");
            Click("Locate"); Check(!Find(visual!, prefix + "SourceEditor").IsOffscreen && Draft() == many, "Off-screen list navigation and Locate preserve source with adjustable source and inspector scrolling"); Capture("off-screen-navigation"); Click("Cancel"); WaitClosed(); Stop();
            var mixed = family switch { "Xy" => "xychart\n x-axis [A,B,C]\n bar [1,invalid,3]\n line [1,2,3]\n", "Radar" => "radar-beta\n axis A,B,C\n curve c{1,invalid,3}\n", _ => "sankey-beta\n A,B,0\n A,B,2.000\n" };
            fixture = Path.Combine(root, "protected.mmd"); File.WriteAllText(fixture, mixed); Launch(fixture); Open(0);
            if (family == "Sankey") { Select("Rename node across proved endpoints · B"); Set("Label", "Global"); Click("Update"); }
            else Click(family == "Xy" ? "AddCategory" : "AddAxis");
            Wait(() => Find(visual!, prefix + "Status").Name.Contains(family == "Sankey" ? "protect" : family == "Xy" ? "aligned" : "exact", StringComparison.OrdinalIgnoreCase), "Precise malformed/ignored source protection is missing.");
            Check(Draft() == mixed, "Coerced/dropped/ignored historical rows are retained exactly; protected operations cannot silently repair source"); Capture("protected-source");
            if (family == "Sankey") Click("Reset"); Click("Cancel"); WaitClosed(); Stop();
        }
    }).GetAwaiter().GetResult();
}
