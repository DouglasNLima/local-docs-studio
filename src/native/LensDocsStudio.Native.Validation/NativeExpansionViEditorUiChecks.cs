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
using LensDocsStudio.Core.PacketDiagrams;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Core.VennDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunExpansionViEditor(string executable, string output, string family, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var observations = new List<object>();
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var starter = family switch { "Packet" => PacketVisualDraft.Starter, "TreeView" => TreeViewVisualDraft.Starter, "Treemap" => TreemapVisualDraft.Starter, "Venn" => VennVisualDraft.Starter, _ => throw new ArgumentException("Unknown Expansion VI family.") };
        var source = starter.ReplaceLineEndings("\r\n"); var block = "```mermaid\r\n" + source + "```\r\n";
        var original = "# Bounded native authoring Ω\r\n\r\n" + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        var first = family switch { "Packet" => "Field 1 ·", "TreeView" => "Node 1 ·", "Treemap" => "Node 2 ·", _ => "Set 1 ·" };
        var path = Path.Combine(root, family + ".md"); File.WriteAllText(path, original); Application? app = null; Window? main = null, visual = null; var launchOrdinal = 0;
        try
        {
            if (highContrast) Check(System.Windows.SystemParameters.HighContrast, "Actual Windows High Contrast is active; UIA is not physical screen-reader certification");
            Launch(path); var owner = Find(main!, "MarkdownEditor"); Wait(() => ReadEditorText(owner) == original, "Fixture did not load exactly."); Open(original.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Capture("selected");
            using (var independent = new UIA3Automation())
            {
                var second = independent.FromHandle(visual!.Properties.NativeWindowHandle.Value).AsWindow();
                Check(Find(second, family + "Elements").AsListBox().SelectedItem?.Name == Find(visual!, family + "Elements").AsListBox().SelectedItem?.Name, "Independent native UIA connections agree on exact source occurrence selection");
            }
            Pointer(); Select(first); Set("Label", "Authored Ω"); Click("Update"); Await(() => Draft().Contains("Authored Ω", StringComparison.Ordinal)); var authored = Draft();
            Click("Undo"); Await(() => Draft() == source); Click("Redo"); Await(() => Draft() == authored);
            var field = Find(visual!, family + "Label"); field.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type("Bad\"quote"); Click("Update");
            Wait(() => Find(visual!, family + "Status").Name.Contains("quote", StringComparison.OrdinalIgnoreCase), "Actionable quote diagnostic missing.");
            Check(Draft() == authored && field.AsTextBox().Text == "Bad\"quote" && !Find(visual!, family + "Apply").IsEnabled, "Invalid native pending field remains visible, keeps last valid draft and disables Apply"); Click("Reset"); Await(() => Draft() == authored);
            var numeric = family is "Packet" ? "Identifier" : "Value";
            if (family != "TreeView")
            {
                Set(numeric, "1e"); Click("Update"); Wait(() => Find(visual!, family + "Status").Name.Contains(family == "Packet" ? "integer" : "decimal", StringComparison.OrdinalIgnoreCase), "Bounded numeric diagnostic missing.");
                Check(Draft() == authored && Find(visual!, family + numeric).AsTextBox().Text == "1e" && !Find(visual!, family + "Apply").IsEnabled, "Incomplete numeric metadata/range remains pending without owner mutation"); Click("Reset"); Await(() => Draft() == authored);
            }
            if (family == "Packet")
            {
                Set("Value", "16"); Click("Update"); Wait(() => Find(visual!, family + "Status").Name.Contains("relative", StringComparison.OrdinalIgnoreCase), "Downstream relative cursor impact was not protected.");
                Check(Draft() == authored, "Packet rejects changed downstream relative interpretation atomically"); Click("Reset"); Await(() => Draft() == authored);
            }
            else if (family == "TreeView")
            {
                Find(visual!, family + "Directory").AsComboBox().Select("file"); Click("Update"); Await(() => new TreeViewVisualDraft(Draft()).Model.Nodes[0].Directory == false);
                Click("Undo"); Await(() => Draft() == authored);
            }
            else if (family == "Treemap") { Set("Value", "3.75000"); Click("Update"); Await(() => Draft().Contains("3.75000", StringComparison.Ordinal)); Check(new TreemapVisualDraft(Draft()).Model.Nodes[0].ValueToken is null, "Treemap parent remains derived after native leaf-value edit"); }
            else
            {
                var beforeSize = new DiagramService().Render(Draft()).Svg; Set("Value", "3.75000"); Click("Update"); Await(() => Draft().Contains("3.75000", StringComparison.Ordinal)); Check(new DiagramService().Render(Draft()).Svg == beforeSize, "Venn stored size metadata changes exact source without changing fixed native geometry");
                Set("Identifier", "AuthorSet"); Click("Update"); Await(() => Draft().Contains("union AuthorSet,", StringComparison.Ordinal)); Check(!Draft().Contains("union A,", StringComparison.Ordinal), "Venn ID rename patches every proved native union reference atomically");
            }
            var beforeAdd = Draft(); Add(); Await(() => Draft() != beforeAdd); var added = Draft(); Click("MoveUp"); Await(() => Draft() != added); Click("Undo"); Await(() => Draft() == added); Click("Redo"); Await(() => Draft() != added); Click("Delete"); Await(() => Draft() == beforeAdd);
            var changed = Draft(); Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Private source, inspector and history preserve owner and disk before Apply");
            foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
            {
                visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Click("Fit"); Select(first); var input = Find(visual!, family + "Label"); input.Focus(); Wait(() => !input.IsOffscreen, "Focused inspector field is off screen.");
                Check(visual.BoundingRectangle.Contains(Find(visual, family + "Apply").BoundingRectangle) && visual.BoundingRectangle.Contains(Find(visual, family + "Cancel").BoundingRectangle), "Apply/Cancel and inspector remain reachable at " + width + "x" + height); Capture("viewport-" + width + "x" + height);
            }
            if (!highContrast) foreach (var theme in new[] { "Dark", "Light" }) { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Wait(() => Find(visual!, family + "Apply").IsEnabled && visual!.Properties.ItemStatus.ValueOrDefault == theme, "Owned Light/Dark appearance did not settle."); Capture(theme.ToLowerInvariant()); }
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..]; Click("Apply"); WaitClosed(); Wait(() => ReadEditorText(owner) == expected, "Apply changed the wrong fence or neighbouring source.");
            FocusWindow(main!); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(owner) == original, "Apply was not one owner Undo step."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(owner) == expected, "Owner Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Save failed.");
            Check(true, "Exact duplicate-fence ownership, one-step owner Undo/Redo and Save preserve every neighbouring byte"); main!.Close(); Wait(() => app!.HasExited, "Saved application did not close."); app!.Dispose(); app = null;
            Launch(path); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Restart/reopen retains exact authored source and numeric lexemes");
            Select(first); Set("Label", "Cancelled Ω"); Click("Update"); Await(() => Draft().Contains("Cancelled Ω", StringComparison.Ordinal)); Click("Cancel"); Discard(); WaitClosed(); Check(File.ReadAllText(path) == expected, "Cancel preserves authored saved bytes");
            Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Set("Label", "Private conflict Ω"); Click("Update"); Await(() => Draft().Contains("Private conflict Ω", StringComparison.Ordinal)); var privateSource = Draft();
            var external = expected.Replace(changed, source.ReplaceLineEndings("\n"), StringComparison.Ordinal); FocusWindow(main!); Find(main!, "MarkdownEditor").Patterns.Value.Pattern.SetValue(external); FocusWindow(visual!); Click("Apply"); Wait(() => Find(visual!, family + "Status").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale owner was not rejected."); Check(Draft() == privateSource && ReadEditorText(Find(main!, "MarkdownEditor")) == external, "Stale owner rejection preserves both exact sources"); Capture("owner-conflict"); Click("Cancel"); Discard(); WaitClosed(); Stop();
            var standalone = Path.Combine(root, family + ".mmd"); File.WriteAllText(standalone, source); Launch(standalone); Open(0); Check(Draft() == source, "Standalone Mermaid uses the ordinary native authoring route"); Click("Cancel"); WaitClosed();
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "Insert" + family).AsMenuItem().Invoke(); WaitVisual(); Check(Draft() == starter, "Insert opens the exact bounded native family starter"); Click("Apply"); WaitClosed(); var inserted = Find(main!, "MarkdownEditor"); Check(ReadEditorText(inserted).Contains(starter.TrimEnd('\r', '\n'), StringComparison.Ordinal), "Insert produces portable Mermaid source"); FocusWindow(main!); inserted.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(inserted) == "", "Insert was not one owner Undo step."); Stop();
            NavigationAndProtection(); Receipt("PASS", null); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(e => e.ControlType + ": " + e.AutomationId + " · " + e.Name + " · enabled=" + e.IsEnabled) ?? []); } catch { }
            Receipt("FAIL", error.ToString()); Console.Error.WriteLine(error); return 1;
        }
        finally { Stop(); }
        void Receipt(string status, string? error) => File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = status, Family = family, SourceRevision = typeof(PacketVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks, Observations = observations, Error = error }, new JsonSerializerOptions { WriteIndented = true }));
        void Launch(string fixture)
        {
            var timer = Stopwatch.StartNew(); var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var arg in new[] { "--state-directory", state, root, fixture }) info.ArgumentList.Add(arg); app = Application.Launch(info); main = visual = null;
            try { main = NativeExpansionLaunch.Prepare(app, automation, timer, fixture, output, ++launchOrdinal, highContrast); } finally { automation.TransactionTimeout = automation.ConnectionTimeout = TimeSpan.FromSeconds(3); }
        }
        void Stop() { if (app is null) return; if (!app.HasExited) app.Kill(); app.Dispose(); app = null; main = visual = null; }
        void Open(int offset) { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact diagram context action missing."); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); }
        void WaitVisual() { Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(family + "VisualEditor")))?.AsWindow()) is not null, "Bounded family editor missing."); Wait(() => Find(visual!, family + "Apply").IsEnabled, "Canonical draft invalid: " + Find(visual!, family + "Status").Name); Find(visual!, family + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("Fit"); }
        void Select(string name) { var list = Find(visual!, family + "Elements"); var row = list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).First(e => e.Name.StartsWith(name, StringComparison.Ordinal)); row.Patterns.SelectionItem.Pattern.Select(); Wait(() => list.AsListBox().SelectedItem?.Name.StartsWith(name, StringComparison.Ordinal) == true, "Exact occurrence list selection failed."); }
        string Draft() => ReadEditorText(Find(visual!, family + "SourceEditor"));
        void Set(string id, string value) => Find(visual!, family + id).AsTextBox().Text = value;
        void Click(string id) { Wait(() => Find(visual!, family + id).IsEnabled, "Command unavailable: " + family + id + "; " + Find(visual!, family + "Status").Name); Find(visual!, family + id).AsButton().Invoke(); }
        void Await(Func<bool> condition) => Wait(() => condition() && Find(visual!, family + "Apply").IsEnabled, "Expected draft did not validate: " + Find(visual!, family + "Status").Name);
        void WaitClosed() { Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(family + "VisualEditor"))) is null, "Editor did not close."); visual = null; }
        void Discard() { AutomationElement? yes = null; Wait(() => (yes = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Yes")))) is not null, "Discard confirmation missing."); yes!.AsButton().Invoke(); }
        void Capture(string name) { if (!app!.WaitWhileBusy(TimeSpan.FromSeconds(5))) throw new IOException("Editor remained busy before capture."); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Add()
        {
            Click(family == "Packet" ? "AddField" : family == "TreeView" ? "AddRoot" : family == "Treemap" ? "AddSection" : "AddSet"); Window? prompt = null;
            var title = family == "Packet" ? "Add Packet field" : family == "TreeView" ? "Add TreeView node" : family == "Treemap" ? "Add Treemap section" : "Add set";
            Wait(() => (prompt = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title)))?.AsWindow()) is not null, "Explicit addition prompt missing.");
            Find(prompt!, "WorkspacePathInput").AsTextBox().Text = family == "Packet" ? "64-71: \"Added Ω\"" : family == "Venn" ? "AddedSet" : "Added Ω"; Find(prompt!, "ConfirmWorkspacePath").AsButton().Invoke();
        }
        void Pointer()
        {
            Click("Fit"); var render = new DiagramService().Render(Draft()); var svgRoot = XDocument.Parse(render.Svg!).Root!;
            var shapes = family switch { "Packet" => SourceDeclarations.Groups(render.Svg!, "packet-field"), "TreeView" => SourceDeclarations.Groups(render.Svg!, "treeview-node"), "Treemap" => SourceDeclarations.Groups(render.Svg!, "treemap-tile"), _ => SourceDeclarations.Groups(render.Svg!, "venn-set") };
            var geometry = NativeSourceHit.ShapeGeometry(shapes[0], svgRoot)!; var bounds = geometry.Bounds; System.Windows.Point? point = null;
            var others = family == "Venn" ? shapes.Skip(1).Select(s => NativeSourceHit.ShapeGeometry(s, svgRoot)!).ToArray() : [];
            for (var x = 1; x < 20 && point is null; x++) for (var y = 1; y < 20 && point is null; y++) { var candidate = new System.Windows.Point(bounds.X + bounds.Width * x / 20, bounds.Y + bounds.Height * y / 20); if (geometry.FillContains(candidate) && others.All(other => !other.FillContains(candidate))) point = candidate; }
            Check(point is not null, "Actual native geometry has an unambiguous pointer interior"); var viewport = Find(visual!, family + "Viewport").BoundingRectangle; var scale = Find(visual!, family + "Zoom").Patterns.RangeValue.Pattern.Value.Value / 100;
            var sx = viewport.Left + point!.Value.X * scale; var sy = viewport.Top + point.Value.Y * scale; observations.Add(new { Operation = "Canonical pointer", Bounds = bounds.ToString(), Scale = scale, ScreenX = sx, ScreenY = sy }); FocusWindow(visual!); Mouse.MoveTo((int)Math.Round(sx), (int)Math.Round(sy)); Mouse.Click(MouseButton.Left);
            Wait(() => Find(visual!, family + "Label").AsTextBox().Text == (family == "Packet" ? "Source" : family == "TreeView" ? "project" : family == "Treemap" ? "Guides" : "Authors"), "Real pointer did not select the exact native source occurrence."); Check(true, "Real pointer selection uses actual native geometry, guarding overlapping Venn circles");
        }
        void NavigationAndProtection()
        {
            var many = family switch { "Packet" => "packet\n" + string.Concat(Enumerable.Range(0, 40).Select(i => i + ": \"Node " + i + "\"\n")), "TreeView" => "treeView\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "Node " + i + "\n")), "Treemap" => "treemap-beta\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "\"Node " + i + "\": " + (i == 39 ? 0 : 1) + "\n")), _ => "venn-beta\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "set n" + i + "[\"Node " + i + "\"]\n")) };
            var fixture = Path.Combine(root, "navigation.mmd"); File.WriteAllText(fixture, many); Launch(fixture); Open(0); var list = Find(visual!, family + "Elements").AsListBox(); list.Focus(); Keyboard.Type(VirtualKeyShort.END);
            Wait(() => list.SelectedItem is not null && !list.SelectedItem.IsOffscreen && Find(visual!, family + "Label").AsTextBox().Text == "Node 39", "Keyboard End did not reveal the exact off-screen/zero-area occurrence and inspector."); Click("Locate"); Check(!Find(visual!, family + "SourceEditor").IsOffscreen && Draft() == many, "Keyboard navigation and direct Locate preserve complete source with independent scrolling"); Capture("off-screen-navigation"); Click("Cancel"); WaitClosed(); Stop();
            var mixed = family switch { "Packet" => "packet\n0: \"A\"\n+0: \"Skipped\"\n", "TreeView" => "treeView\n├── A\n└── B\n", "Treemap" => "treemap-beta\nraw fallback\n \"A\": 1\n", _ => "venn-beta\nset A\nset A\nunion A,A\n" };
            fixture = Path.Combine(root, "protected.mmd"); File.WriteAllText(fixture, mixed); Launch(fixture); Open(0); Select(family == "Packet" ? "Field 1 ·" : family == "Venn" ? "Set 1 ·" : "Node 1 ·"); Click("Delete"); Wait(() => Find(visual!, family + "Status").Name.Contains("protect", StringComparison.OrdinalIgnoreCase), "Precise source-only structural protection missing.");
            Check(Draft() == mixed, "Protected skipped/fallback/box-prefix/duplicate source remains byte-exact"); Click("Locate"); Check(!Find(visual!, family + "SourceEditor").IsOffscreen && Draft() == mixed, "Protected source can be directly inspected after rejected operations"); Capture("protected-source"); Click("Cancel"); WaitClosed(); Stop();
        }
    }).GetAwaiter().GetResult();
}
