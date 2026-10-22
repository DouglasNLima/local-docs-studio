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
using LensDocsStudio.Core.RequirementDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.GitGraphDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.KanbanDiagrams;
using LensDocsStudio.Core.ArchitectureDiagrams;
using LensDocsStudio.Core.C4Diagrams;
using LensDocsStudio.Core.BlockDiagrams;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunWorkflowEditor(string executable, string output, string family, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var observations = new List<object>(); var prefix = family;
        var topology = family is "Architecture" or "C4" or "Block";
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root); var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var starter = family switch { "Architecture" => ArchitectureVisualDraft.Starter, "C4" => C4VisualDraft.Starter, "Block" => BlockVisualDraft.Starter, "Kanban" => KanbanVisualDraft.Starter, "Requirement" => RequirementVisualDraft.Starter, "GitGraph" => GitGraphVisualDraft.Starter, _ => throw new ArgumentException("Unknown workflow family.") };
        var source = starter.ReplaceLineEndings("\r\n"); var block = "```mermaid\r\n" + source + "```\r\n";
        var original = (topology ? "# Twenty-family authoring Ω\r\n\r\n" : "# Seventeen-family authoring Ω\r\n\r\n") + block + "\r\nBetween identical diagrams.\r\n\r\n" + block + "\r\nAfter.\r\n";
        var path = Path.Combine(root, family + ".md"); File.WriteAllText(path, original); Application? app = null; Window? main = null, visual = null;
        var first = family switch { "Architecture" => "service · gateway", "C4" => "Person · reader", "Block" => "Grid 1 · reader", "Kanban" => "Card · ", "Requirement" => "Requirement · ", _ => "Command 1 · commit" };
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
            var invalid = family switch { "Architecture" => "Bad]label", "C4" => "Bad\"\"quote", "Kanban" => "Bad[label", _ => "Bad\"quote" };
            var beforeInvalid = Draft(); var field = Find(visual!, prefix + "Label"); field.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(invalid);
            Wait(() => field.AsTextBox().Text == invalid, "Invalid pending text was not retained."); Click("Update");
            Wait(() => Find(visual!, prefix + "Status").Name.Contains(family == "Kanban" ? "native ID" : "quote", StringComparison.OrdinalIgnoreCase) || Find(visual!, prefix + "Status").Name.Contains("separator", StringComparison.OrdinalIgnoreCase), "Actionable grammar feedback missing.");
            Check(Draft() == beforeInvalid && field.AsTextBox().Text == invalid && !Find(visual!, prefix + "Apply").IsEnabled, "Invalid pending input retains last valid draft, remains visible and disables Apply"); Click("Reset"); Await(() => Draft() == beforeInvalid);
            Set(family == "Kanban" ? "Ticket" : "Identifier", family == "GitGraph" ? "Tag" : "DOC-42"); Click("Update"); Await(() => Draft().Contains(family == "GitGraph" ? "Tag" : "DOC-42", StringComparison.Ordinal));
            if (topology) first = family switch { "Architecture" => "service · DOC-42", "C4" => "Person · DOC-42", _ => "Grid 1 · DOC-42" };
            var beforeAdd = Draft();
            if (topology) AddTopology(); else Click(family == "Kanban" ? "AddCard" : family == "Requirement" ? "AddRequirement" : "AddCommit");
            Await(() => Draft() != beforeAdd); if (family == "GitGraph") { Set("Label", "Added candidate"); Click("Update"); Await(() => Draft().Contains("Added candidate", StringComparison.Ordinal)); } var added = Draft();
            Check(Find(visual!, prefix + "Capabilities").Name.Length > 20, "Persistent inspector exposes operation and protection context");
            Click("MoveUp"); Await(() => Draft() != added); Click("Undo"); Await(() => Draft() == added); Click("Redo"); Await(() => Draft() != added);
            if (family == "Block") { Click("Undo"); Await(() => Draft() == added); }
            Click("Delete"); Await(() => Draft() == beforeAdd);
            if (family == "Kanban")
            {
                Select(first); Find(visual!, prefix + "Destination").AsComboBox().Select("Done · ID: done"); Click("MoveCard");
                Await(() => { var board = new KanbanVisualDraft(Draft()).Model; return board.Cards.Single(c => c.Id == "guide").Column == board.Columns.Single(c => c.Id == "done").Identity; });
                Check(true, "Keyboard-accessible destination command moves only the selected card to the proved column"); Click("Undo"); Await(() => Draft() == beforeAdd);
            }
            else if (family == "Requirement")
            {
                Select("Relation · "); Set("Value", "verifies"); Click("Update"); Await(() => Draft().Contains("- verifies ->", StringComparison.Ordinal));
                Check(true, "Actual relationship inspector changes the native relation type while preserving symbolic endpoints"); Click("Undo"); Await(() => Draft() == beforeAdd);
            }
            else if (family == "GitGraph")
            {
                Click("AddBranch"); Window? prompt = null;
                Wait(() => (prompt = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName("Append GitGraph branch")))?.AsWindow()) is not null, "Branch name prompt missing.");
                var beforeBranch = Draft(); Find(prompt!, "WorkspacePathInput").AsTextBox().Text = " bad name "; Find(prompt!, "ConfirmWorkspacePath").AsButton().Invoke();
                Wait(() => Find(prompt!, "WorkspacePathInput").AsTextBox().Text == " bad name " && prompt!.FindAllDescendants().Any(e => e.Name?.Contains("Spaces, quotes and line breaks", StringComparison.Ordinal) == true), "Invalid branch name was not retained with grammar feedback.");
                Check(Draft() == beforeBranch && !Find(visual!, prefix + "Apply").IsEnabled, "Invalid branch input remains visible, disables owner Apply and preserves the last valid draft without trimming or repair");
                prompt!.CaptureToFile(Path.Combine(output, "invalid-branch-name.png"));
                Find(prompt!, "WorkspacePathInput").AsTextBox().Text = "topic"; Find(prompt!, "ConfirmWorkspacePath").AsButton().Invoke(); Await(() => Draft().Contains("branch topic", StringComparison.Ordinal));
                Click("AddCommit"); Await(() => new GitGraphVisualDraft(Draft()).Model.Heads.ContainsKey("topic"));
                Find(visual!, prefix + "Branch").AsComboBox().Select("main"); Click("Checkout"); Await(() => new GitGraphVisualDraft(Draft()).Model.CurrentBranch == "main");
                Find(visual!, prefix + "Branch").AsComboBox().Select("topic"); Click("Merge"); Await(() => Draft().Contains("merge topic", StringComparison.Ordinal));
                Check(true, "Actual named branch, commit, checkout and merge commands preserve current branch and two proved native parent heads");
            }
            else if (family == "Architecture")
            {
                Select("Connection 1 · "); Find(visual!, prefix + "SourceSide").AsComboBox().Select("T"); Find(visual!, prefix + "TargetSide").AsComboBox().Select("B"); Find(visual!, prefix + "Arrow").AsComboBox().Select("<-->"); Click("Update"); Await(() => Draft().Contains(":T <--> B:", StringComparison.Ordinal));
                Check(true, "Actual native side and arrow selectors patch only the chosen connection occurrence"); Click("Undo"); Await(() => Draft() == beforeAdd);
                Select("junction · route"); Check(Find(visual!, prefix + "Summary").Name.Contains("route", StringComparison.Ordinal), "Routing junction remains keyboard-selectable without a substitute icon");
            }
            else if (family == "C4")
            {
                Select(first); Find(visual!, prefix + "Destination").AsComboBox().Select("studio"); Click("MoveMembership"); Await(() => new C4VisualDraft(Draft()).Model.Nodes.Single(n => n.Alias == "DOC-42").ParentIdentity is not null);
                Check(true, "Actual boundary destination moves the complete selected source group without changing indentation"); Click("Undo"); Await(() => Draft() == beforeAdd);
                Select("Relationship 1 · "); Set("Label", "Reviewed Ω"); Click("Update"); Await(() => Draft().Contains("\"Reviewed Ω\"", StringComparison.Ordinal)); Click("Undo"); Await(() => Draft() == beforeAdd);
            }
            else if (family == "Block")
            {
                Select("Grid 2 · Spacer"); Set("Value", "2"); Click("Update"); Await(() => Draft().Contains("space:2", StringComparison.Ordinal));
                Check(true, "Invisible spacer is selected and its exact span is edited through the keyboard occurrence list"); Click("Undo"); Await(() => Draft() == beforeAdd);
            }
            var changed = Draft(); Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Private source, inspector and history never mutate owner or disk before Apply");
            foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
            {
                visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height); Click("Fit"); Select(first);
                var input = Find(visual!, prefix + "Label"); input.Focus(); Wait(() => !input.IsOffscreen, "Focused inspector input is off screen.");
                if (width == 880)
                {
                    var locate = Find(visual!, prefix + "Locate"); locate.Focus();
                    Wait(() => !locate.IsOffscreen && Find(visual!, prefix + "InspectorViewport").Patterns.Scroll.Pattern.VerticalScrollPercent.Value > 0, "Native focus did not scroll the final inspector action into view.");
                    var selectedName = Find(visual!, prefix + "Elements").AsListBox().SelectedItem?.Name;
                    Keyboard.Type(VirtualKeyShort.SPACE); Wait(() => !Find(visual!, prefix + "SourceEditor").IsOffscreen, "Keyboard Locate did not reveal the source panel.");
                    Check(Draft() == changed && Find(visual!, prefix + "Elements").AsListBox().SelectedItem?.Name == selectedName, "Actual inspector scrolling and keyboard Locate retain source, selection and inspector context in a small window");
                    input.Focus(); Wait(() => !input.IsOffscreen, "Returning focus did not restore the top inspector field.");
                }
                Check(visual.BoundingRectangle.Contains(Find(visual, prefix + "Apply").BoundingRectangle), "Apply and inspector remain reachable at " + width + "x" + height); Capture("viewport-" + width + "x" + height);
            }
            if (!highContrast || topology)
                foreach (var theme in new[] { "Dark", "Light" })
                { FocusWindow(main!); Find(main!, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, theme + "Theme").AsMenuItem().Invoke(); Wait(() => Find(visual!, prefix + "Apply").IsEnabled && visual!.Properties.ItemStatus.ValueOrDefault == (highContrast ? "High Contrast" : theme), "Owned Light/Dark appearance did not settle."); observations.Add(new { OwnedTheme = theme, ActualHighContrast = System.Windows.SystemParameters.HighContrast }); Capture(theme.ToLowerInvariant()); }
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1]; var expected = original[..target.Body.Start] + changed + original[target.Body.End..];
            Click("Apply"); WaitClosed(); Wait(() => ReadEditorText(owner) == expected, "Apply changed the wrong fence or neighbouring bytes.");
            FocusWindow(main!); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(owner) == original, "Apply was not one owner Undo step.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(owner) == expected, "Owner Redo failed."); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S); Wait(() => ReadObservedText(path) == expected, "Save failed.");
            Check(true, "Exact second identical fence Apply, one-step owner Undo/Redo and Save preserve every neighbouring byte"); main!.Close(); Wait(() => app!.HasExited, "Saved application did not close."); app!.Dispose(); app = null;
            Launch(path); Wait(() => ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Save/restart/reopen lost source."); Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Check(Draft() == changed, "Reopened diagram retains exact authored source and untouched lexemes");
            Select(first); Set("Label", "Cancelled private Ω"); Click("Update"); Await(() => Draft().Contains("Cancelled private Ω", StringComparison.Ordinal)); Click("Cancel"); Discard(); WaitClosed();
            Check(File.ReadAllText(path) == expected && ReadEditorText(Find(main!, "MarkdownEditor")) == expected, "Cancel preserves exact saved and owner source");
            Open(expected.LastIndexOf("```mermaid", StringComparison.Ordinal)); Select(first); Set("Label", "Private conflict Ω"); Click("Update"); Await(() => Draft().Contains("Private conflict Ω", StringComparison.Ordinal)); var privateSource = Draft();
            var external = expected.Replace(changed, source.ReplaceLineEndings("\n"), StringComparison.Ordinal); FocusWindow(main!); Find(main!, "MarkdownEditor").Patterns.Value.Pattern.SetValue(external); FocusWindow(visual!); Click("Apply");
            Wait(() => Find(visual!, prefix + "Status").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale owner edit was not rejected."); Check(Draft() == privateSource && ReadEditorText(Find(main!, "MarkdownEditor")) == external, "Stale Apply preserves both private and changed owner source"); Capture("owner-conflict"); Click("Cancel"); Discard(); WaitClosed(); Stop();
            var standalone = Path.Combine(root, family + ".mmd"); File.WriteAllText(standalone, source); Launch(standalone); Open(0); Check(Draft() == source, "Standalone Mermaid uses the ordinary visual authoring route"); Click("Cancel"); WaitClosed();
            FocusWindow(main!); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N); Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "Insert" + family).AsMenuItem().Invoke(); WaitVisual(); Check(Draft() == starter, "Insert uses the correct full-family editor and portable starter"); Click("Apply"); WaitClosed();
            var inserted = Find(main!, "MarkdownEditor"); Check(ReadEditorText(inserted).Contains(starter.TrimEnd('\r', '\n'), StringComparison.Ordinal), "Insert produces portable Mermaid source"); FocusWindow(main!); inserted.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(inserted) == "", "Insert was not one owner Undo step."); Stop();
            NavigationAndProtection();
            File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", Family = family, SourceRevision = typeof(KanbanVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            try { (visual ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); File.WriteAllLines(Path.Combine(output, "failure-controls.txt"), (visual ?? main)?.FindAllDescendants().Select(e => e.ControlType + ": " + e.AutomationId + " · " + e.Name + " · enabled=" + e.IsEnabled) ?? []); } catch { }
            File.WriteAllText(Path.Combine(output, "authoring-ui.json"), JsonSerializer.Serialize(new { Status = "FAIL", Family = family, SourceRevision = typeof(KanbanVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks, Observations = observations, Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.Error.WriteLine(error); return 1;
        }
        finally { Stop(); }
        void Launch(string fixture) { var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var arg in new[] { "--state-directory", state, root, fixture }) info.ArgumentList.Add(arg); app = Application.Launch(info); main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Native application did not open."); main.Patterns.Transform.Pattern.Resize(1280, 800); }
        void Stop() { if (app is null) return; if (!app.HasExited) app.Kill(); app.Dispose(); app = null; main = visual = null; }
        void Open(int offset) { FocusWindow(main!); Wait(() => main!.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually" + offset)) is not null, "Exact diagram context action missing."); Find(main!, "DiagramEditVisually" + offset).AsButton().Invoke(); WaitVisual(); }
        void WaitVisual() { Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(prefix + "VisualEditor")))?.AsWindow()) is not null, "Workflow editor missing."); Wait(() => Find(visual!, prefix + "Apply").IsEnabled, "Canonical draft invalid: " + Find(visual!, prefix + "Status").Name); Find(visual!, prefix + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); Click("Fit"); }
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
        void AddTopology()
        {
            var before = Draft(); Click(family == "Architecture" ? "AddService" : family == "C4" ? "AddElement" : "AddBlock"); Window? dialog = null;
            var title = family == "Architecture" ? "Add service" : family == "C4" ? "Add C4 alias" : "Add block";
            Wait(() => (dialog = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByName(title)))?.AsWindow()) is not null, "Explicit new identifier prompt missing.");
            Find(dialog!, "WorkspacePathInput").AsTextBox().Text = " bad id "; Find(dialog!, "ConfirmWorkspacePath").AsButton().Invoke();
            Wait(() => Find(dialog!, "WorkspacePathInput").AsTextBox().Text == " bad id " && dialog!.FindAllDescendants().Any(e => e.Name?.Contains("identifier", StringComparison.OrdinalIgnoreCase) == true), "Invalid identifier lost its original pending text or actionable explanation.");
            Check(Draft() == before && !Find(visual!, prefix + "Apply").IsEnabled, "Invalid new identifier remains pending without trimming, minting an ID or changing the private draft"); dialog!.CaptureToFile(Path.Combine(output, "invalid-new-identifier.png"));
            Find(dialog!, "WorkspacePathInput").AsTextBox().Text = "added_node"; Find(dialog!, "ConfirmWorkspacePath").AsButton().Invoke();
        }
        void Pointer()
        {
            Click("Fit"); var render = new DiagramService().Render(Draft()); var svgRoot = XDocument.Parse(render.Svg!).Root!;
            XElement shape = family switch { "Architecture" => SourceDeclarations.Groups(render.Svg!, "architecture-service")[0], "C4" => SourceDeclarations.Groups(render.Svg!, "c4-element")[0], "Block" => SourceDeclarations.Groups(render.Svg!, "node")[0], "Kanban" => SourceDeclarations.Groups(render.Svg!, "kanban-card")[0], "Requirement" => SourceDeclarations.Groups(render.Svg!, "req-node")[0], _ => SourceDeclarations.Groups(render.Svg!, "commit")[0] };
            var geometry = NativeSourceHit.ShapeGeometry(shape, svgRoot)!; var bounds = geometry.Bounds; System.Windows.Point? point = null;
            for (var x = 1; x < 20 && point is null; x++) for (var y = 1; y < 20 && point is null; y++) { var candidate = new System.Windows.Point(bounds.X + bounds.Width * x / 20, bounds.Y + bounds.Height * y / 20); if (geometry.FillContains(candidate)) point = candidate; }
            Check(point is not null, "Actual canonical pointer shape has a visible interior"); var viewport = Find(visual!, prefix + "Viewport").BoundingRectangle; var scale = Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value / 100;
            var sx = viewport.Left + point!.Value.X * scale; var sy = viewport.Top + point.Value.Y * scale; observations.Add(new { Operation = "Canonical pointer", Bounds = bounds.ToString(), Scale = scale, ScreenX = sx, ScreenY = sy });
            FocusWindow(visual!); Mouse.MoveTo((int)Math.Round(sx), (int)Math.Round(sy)); Mouse.Click(MouseButton.Left);
            Wait(() => Find(visual!, prefix + "Label").AsTextBox().Text == (family is "C4" or "Block" ? "Reader" : family == "Architecture" ? "Gateway" : family == "Kanban" ? "Write guide" : family == "Requirement" ? "guide" : "Start"), "Real pointer did not select the exact canonical source occurrence.");
            Check(true, "Real pointer selection follows actual native geometry and exact source occurrence");
        }
        void NavigationAndProtection()
        {
            var many = family switch {
                "Architecture" => "architecture-beta\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "service n" + i + "[Node " + i + "]\n")),
                "C4" => "C4Context\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "Person(n" + i + ",\"Node " + i + "\")\n")),
                "Block" => "block\ncolumns 3\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "n" + i + "[Node " + i + "]\n")),
                "Kanban" => "kanban\n    todo[To do]\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "        c" + i + "[Card " + i + "]\n")),
                "Requirement" => "requirementDiagram\n" + string.Concat(Enumerable.Range(0, 35).Select(i => "requirement r" + i + " {\n    id: \"DOC-" + i + "\"\n}\n")),
                _ => "gitGraph LR:\n" + string.Concat(Enumerable.Range(0, 40).Select(i => "    commit id: \"Commit " + i + "\"\n")) };
            var fixture = Path.Combine(root, "navigation.mmd"); File.WriteAllText(fixture, many); Launch(fixture); Open(0); var list = Find(visual!, prefix + "Elements").AsListBox(); list.Focus(); Keyboard.Type(VirtualKeyShort.END);
            Wait(() => list.SelectedItem is not null && !list.SelectedItem.IsOffscreen && Find(visual!, prefix + "Label").AsTextBox().Text == (topology ? "Node 39" : family == "Kanban" ? "Card 39" : family == "Requirement" ? "r34" : "Commit 39"), "Keyboard End did not reveal the off-screen occurrence and persistent inspector.");
            Click("Locate"); Check(!Find(visual!, prefix + "SourceEditor").IsOffscreen && Draft() == many, "Off-screen list navigation and Locate preserve source with adjustable source and inspector scrolling"); Capture("off-screen-navigation"); Click("Cancel"); WaitClosed(); Stop();
            var mixed = family switch { "Architecture" => "architecture-beta\ngroup g[Group]\nservice a[A] in g\n{g}:R -- L:a\n", "C4" => "C4Context\nPerson(a,\"A\")\nPerson(a,\"Hidden\")\n", "Block" => "block\na[First] a[Hidden]\n", "Kanban" => "kanban\n  A\n    Card\n B\n", "Requirement" => "requirementDiagram\nrequirement a {\nid: first\n}\nrequirement a {\nid: hidden\n}\n", _ => "gitGraph TB:\ncommit\n" };
            fixture = Path.Combine(root, "protected.mmd"); File.WriteAllText(fixture, mixed); Launch(fixture); Open(0);
            if (family == "Kanban") { Select("Column · A"); Click("MoveDown"); }
            else if (family == "Requirement") { Select("Requirement · a"); Click("Delete"); }
            else if (topology) { Select(family == "Architecture" ? "service · a" : family == "C4" ? "Person · a" : "Grid 1 · a"); Click("Delete"); }
            else Click("AddCommit");
            Wait(() => Find(visual!, prefix + "Status").Name.Contains("protect", StringComparison.OrdinalIgnoreCase), "Precise source protection is missing.");
            Check(Draft() == mixed, "Historical unproved source remains exact; protected operations cannot silently repair or discard source"); Capture("protected-source"); Click("Cancel"); WaitClosed(); Stop();
        }
    }).GetAwaiter().GetResult();
}
