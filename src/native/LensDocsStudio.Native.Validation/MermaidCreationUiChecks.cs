using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunMermaidCreation(string executable, string output, bool highContrast, bool dark) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var root = Path.Combine(output, "fixture"); Directory.CreateDirectory(root);
        var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(Theme: dark ? "Dark" : "Light", AutomaticUpdateChecks: false)));
        var path = Path.Combine(root, "creation.md"); const string original = "# Mermaid creation Ω😀\r\n\r\nKeep selected text intact.\r\n";
        File.WriteAllText(path, original); var checks = new List<string>(); var phases = new List<object>();
        Application? app = null; Window? main = null, picker = null, visual = null; var standaloneLaunch = 1;
        var captures = new List<object>();
        try
        {
            if (highContrast) Check(NativeTestHighContrast.IsEnabledNow, "Actual OS High Contrast enabled on disposable Windows");
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            foreach (var argument in new[] { "--state-directory", state, root, path }) info.ArgumentList.Add(argument);
            var timer = Stopwatch.StartNew(); app = Application.Launch(info);
            main = NativeExpansionLaunch.Prepare(app, automation, timer, path, output, 1, highContrast);
            var owner = Find(main, "MarkdownEditor");
            PrepareTextProvider(owner);

            // Search, clear, categories and keyboard never author source. Cancellation restores the exact selected text.
            owner.Focus(); owner.Patterns.Text.Pattern.DocumentRange.FindText("selected text", false, false).Select();
            Check(owner.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "selected text"), "Exact source selection exists before opening the picker");
            OpenPicker();
            Wait(() => Find(picker!, "MermaidPickerCreate").IsEnabled, "Initial native example did not become ready.");
            foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
            {
                ResizePicker(width, height); Capture(picker!, "catalogue-all-" + width + "x" + height);
            }
            CheckTabNavigation();
            var search = Find(picker!, "MermaidPickerSearch");
            search.AsTextBox().Text = "no matching xyz";
            Wait(() => Find(picker!, "MermaidPickerFamilies").AsListBox().Items.Length == 0 && !Find(picker!, "MermaidPickerCreate").IsEnabled, "Empty search did not disable Create.");
            Capture(picker!, "empty-search-disabled-create");
            Check(ReadEditorText(owner) == original, "Empty search leaves source unchanged");
            Find(picker!, "MermaidPickerClear").AsButton().Invoke();
            Wait(() => Find(picker!, "MermaidPickerFamilies").AsListBox().Items.Length == 24, "Clear did not restore 24 families.");
            var category = Find(picker!, "MermaidPickerCategory").AsComboBox(); category.Select("Structures and technical");
            Wait(() => Find(picker!, "MermaidPickerFamilies").AsListBox().Items.Length == 3, "Category membership differs.");
            Find(picker!, "MermaidPickerClear").AsButton().Invoke(); search.AsTextBox().Text = "ÉNERGY";
            Wait(() => Find(picker!, "MermaidPickerFamilies").AsListBox().Items.Length == 1, "Accent-insensitive alias search failed.");
            search.Focus(); Keyboard.Type(VirtualKeyShort.RETURN); app.WaitWhileBusy(TimeSpan.FromSeconds(5));
            Check(WindowNow("MermaidDiagramPicker") is not null, "Enter in search does not create a diagram");
            Find(picker!, "MermaidPickerClear").AsButton().Invoke(); FocusWindow(picker!); search.Focus(); Keyboard.Type(VirtualKeyShort.DOWN);
            var list = Find(picker!, "MermaidPickerFamilies").AsListBox(); Keyboard.Type(VirtualKeyShort.DOWN);
            Wait(() => list.SelectedItem?.Name == "Sequence", "Arrow navigation did not select Sequence.");
            Keyboard.Type(VirtualKeyShort.UP); Wait(() => list.SelectedItem?.Name == "Flowchart", "Arrow up did not select Flowchart.");
            Keyboard.Type(VirtualKeyShort.ESCAPE); WaitGone("MermaidDiagramPicker"); picker = null;
            Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Escape preserves owner and disk");
            Check(owner.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "selected text"), "Picker Cancel restores the original source selection");

            // Real pointer focus transfer must keep the captured nonempty source selection.
            Find(main, "MermaidCreation").Click(); picker = WaitWindow("MermaidDiagramPicker");
            var selectedPie = MermaidDiagramCatalogue.Get(MermaidDiagramFamily.Pie);
            Choose(selectedPie, selectedPie.Variants[0]); CreateVisual(selectedPie);
            Wait(() => Find(visual!, "PieApply").IsEnabled, "Selection insertion draft did not validate.");
            var selectedDraft = Draft(MermaidDiagramFamily.Pie);
            Find(visual!, "PieApply").AsButton().Invoke(); WaitGone("PieVisualEditor"); visual = null;
            var selectedEnd = original.IndexOf("selected text", StringComparison.Ordinal) + "selected text".Length;
            var afterSelection = original.Insert(selectedEnd, MermaidCreationTarget.FormatInsertion(selectedDraft, original, new(selectedEnd, 0)));
            Wait(() => ReadEditorText(owner) == afterSelection, "Toolbar pointer lost the captured selection or replaced selected text.");
            Check(File.ReadAllText(path) == original, "Insertion after selection does not save");
            FocusWindow(main); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(owner) == original, "Selection-preserving insertion was not one Undo.");

            foreach (var definition in MermaidDiagramCatalogue.All)
            foreach (var variant in definition.Variants)
            {
                owner.Patterns.Value.Pattern.SetValue(original); FocusWindow(main); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                OpenPicker(); Choose(definition, variant);
                foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
                {
                    ResizePicker(width, height);
                    var create = Find(picker, "MermaidPickerCreate");
                    Check(!create.IsOffscreen && picker.BoundingRectangle.Contains(create.BoundingRectangle), definition.Name + " footer visible at " + width + "×" + height);
                    Find(picker, "MermaidPickerSearch").Focus(); Keyboard.Type(VirtualKeyShort.TAB);
                    Check(picker.FindAllDescendants().Any(element => element.Properties.HasKeyboardFocus.ValueOrDefault), "Tab remains within the picker");
                    if (width == 1280 || definition.Family == MermaidDiagramFamily.Flowchart)
                        Capture(picker, definition.Id + "-" + variant.Id + "-" + width + "x" + height);
                }
                CreateVisual(definition, keyboard: true); var prefix = Prefix(definition.Family);
                var expectedStarter = variant.Source.ReplaceLineEndings("\r\n");
                Check(Draft(definition.Family) == expectedStarter, definition.Name + "/" + variant.Id + " opens the exact source in the correct existing editor");
                Check(ReadEditorText(owner) == original, "Create opens a private draft only");
                Find(visual!, prefix + "Cancel").AsButton().Invoke(); WaitGone(prefix + "VisualEditor"); visual = null;
                Check(ReadEditorText(owner) == original && File.ReadAllText(path) == original, "Visual Cancel preserves owner and disk");

                FocusWindow(main); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                OpenPicker(); Choose(definition, variant); CreateVisual(definition);
                if (definition.Family == MermaidDiagramFamily.Flowchart)
                {
                    Wait(() => Find(visual!, "MermaidInsertProcess").IsEnabled, "Flowchart palette not ready.");
                    Find(visual!, "MermaidInsertProcess").AsButton().Invoke();
                }
                Wait(() => Find(visual!, prefix + "Apply").IsEnabled, definition.Name + " Apply not ready.");
                var draft = Draft(definition.Family);
                Find(visual!, prefix + "Apply").AsButton().Invoke(); WaitGone(prefix + "VisualEditor"); visual = null;
                var expected = original + MermaidCreationTarget.FormatInsertion(draft, original, new(original.Length, 0));
                Wait(() => ReadEditorText(owner) == expected, "Apply did not insert exact " + definition.Name + " source at captured end.");
                Check(File.ReadAllText(path) == original, "Apply does not save");
                FocusWindow(main); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(owner) == original, "One Undo did not restore exact owner.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
                Wait(() => ReadEditorText(owner) == expected, "Redo did not restore exact insertion.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
                Wait(() => ReadObservedText(path) == expected, "Ordinary Save did not persist exact source.");
                Check(true, definition.Name + "/" + variant.Id + " Apply, one Undo, Redo and explicit Save");

                // Select the second of two identical source bodies. The toolbar must edit that occurrence.
                var block = MermaidBlockSource.Find(new MarkdownService().Parse(expected)).Single();
                var duplicate = expected + "\r\n" + expected[block.Container.Start..block.Container.End];
                owner.Patterns.Value.Pattern.SetValue(duplicate); owner.Focus();
                var range = owner.Patterns.Text.Pattern.DocumentRange.FindText(draft.TrimEnd('\r', '\n'), true, false);
                range.Select(); Find(main, "MermaidCreation").AsButton().Invoke(); visual = WaitWindow(prefix + "VisualEditor");
                Check(Draft(definition.Family).TrimEnd('\r', '\n') == draft.TrimEnd('\r', '\n'), "Contextual toolbar preserves second duplicate " + definition.Name + " source");
                Find(visual, prefix + "Cancel").AsButton().Invoke(); WaitGone(prefix + "VisualEditor"); visual = null;
                Check(ReadEditorText(owner) == duplicate, "Contextual Cancel leaves both occurrences unchanged");
                owner.Patterns.Value.Pattern.SetValue(original); FocusWindow(main); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
                Wait(() => ReadObservedText(path) == original, "Fixture reset Save failed.");
                phases.Add(new { Family = definition.Id, Variant = variant.Id, Editor = prefix + "VisualEditor", Status = "PASS" });
            }
            foreach (var (source, selection) in new[] { ("```csharp\ncode\n```\n", "code"), ("```mermaid\nC4Deployment\n```\n", "C4Deployment"), ("```mermaid\nflowchart TD\n", "flowchart"), ("```mermaid\nflowchart TD\n```\n", "```mermaid"), ("- List item\n    ```mermaid\n    flowchart TD\n    ```\n", "flowchart TD") })
            {
                owner.Patterns.Value.Pattern.SetValue(source); FocusWindow(main); owner.Focus();
                owner.Patterns.Text.Pattern.DocumentRange.FindText(selection, false, false).Select();
                Find(main, "MermaidCreation").AsButton().Invoke();
                Wait(() => Find(main, "Status").Name.Contains("source", StringComparison.OrdinalIgnoreCase), "Protected boundary has no actionable source feedback.");
                Check(ReadEditorText(owner) == source && WindowNow("MermaidDiagramPicker") is null, "Protected/ordinary/unclosed fence does not open a new starter");
            }
            // The established source command is still invokable from the same toolbar.
            owner.Patterns.Value.Pattern.SetValue(""); FocusWindow(main); owner.Focus();
            Find(main, "MermaidCreationActions").AsButton().Invoke();
            AutomationElement? raw = null;
            Wait(() => (raw = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("Formatmermaid")))) is not null, "Legacy Formatmermaid action missing.");
            raw!.AsMenuItem().Invoke(); Wait(() => ReadEditorText(owner).Contains("```mermaid", StringComparison.Ordinal), "Legacy raw code insertion changed.");
            Check(true, "Formatmermaid remains an accessible source insertion command");

            var pie = MermaidDiagramCatalogue.Get(MermaidDiagramFamily.Pie); var pieSource = pie.Variants[0].Source;
            foreach (var extension in new[] { ".mmd", ".mermaid" })
            {
                var fixture = Path.Combine(root, "standalone" + extension); File.WriteAllText(fixture, "");
                LaunchStandalone(fixture); owner = Find(main!, "MarkdownEditor");
                OpenPicker(); Choose(pie, pie.Variants[0], ""); CreateVisual(pie);
                Wait(() => Find(visual!, "PieApply").IsEnabled, "Standalone Pie did not validate.");
                Find(visual!, "PieApply").AsButton().Invoke(); WaitGone("PieVisualEditor"); visual = null;
                Wait(() => ReadEditorText(owner) == pieSource, "Standalone Apply inserted fences or changed source.");
                Check(File.ReadAllText(fixture) == "", "Standalone Apply does not save " + extension);
                FocusWindow(main!); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(owner) == "", "Standalone Apply is not one Undo.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
                Wait(() => ReadEditorText(owner) == pieSource, "Standalone Redo differs.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
                Wait(() => ReadObservedText(fixture) == pieSource, "Standalone Save differs.");
                LaunchStandalone(fixture); owner = Find(main!, "MarkdownEditor");
                Check(ReadEditorText(owner) == pieSource, "Saved standalone reopens exactly " + extension);
                Find(main!, "InsertMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main!, "InsertC4").AsMenuItem().Invoke();
                visual = WaitWindow("PieVisualEditor");
                Check(Draft(MermaidDiagramFamily.Pie) == pieSource, "Choosing C4 cannot replace a nonempty standalone Pie");
                Wait(() => Find(visual!, "PieApply").IsEnabled, "Existing standalone did not validate.");
                FocusWindow(main!); owner.Patterns.Value.Pattern.SetValue("external standalone source\n"); FocusWindow(visual!);
                Find(visual!, "PieApply").AsButton().Invoke();
                Wait(() => Find(visual!, "PieStatus").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Captured stale standalone owner was not rejected.");
                Check(Draft(MermaidDiagramFamily.Pie) == pieSource && ReadEditorText(owner) == "external standalone source\n" && File.ReadAllText(fixture) == pieSource,
                    "Stale-owner rejection retains private draft, external source and saved bytes");
                Find(visual!, "PieCancel").AsButton().Invoke(); WaitGone("PieVisualEditor"); visual = null;
                Find(main!, "MermaidCreation").AsButton().Invoke();
                Wait(() => Find(main!, "Status").Name.Contains("source-only", StringComparison.Ordinal), "Unsupported standalone did not offer source inspection.");
                Check(WindowNow("MermaidDiagramPicker") is null && ReadEditorText(owner) == "external standalone source\n", "Unsupported standalone is never replaced by a starter");
                owner.Patterns.Value.Pattern.SetValue(""); OpenPicker(); Find(picker!, "MermaidPickerCode").AsButton().Invoke(); WaitGone("MermaidDiagramPicker"); picker = null;
                Wait(() => ReadEditorText(owner) == "flowchart TD\n", "Standalone source alternative must stay raw Mermaid.");
                Check(true, "Standalone source alternative retains raw format " + extension);
            }
            Receipt("PASS", null); display.Complete(); return 0;
        }
        catch (Exception error)
        {
            try { (visual ?? picker ?? main)?.CaptureToFile(Path.Combine(output, "failure.png")); } catch { }
            Receipt("FAIL", error.ToString()); Console.Error.WriteLine(error); return 1;
        }
        finally { if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); } }

        Window? WindowNow(string id) => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app!.ProcessId).And(cf.ByAutomationId(id)))?.AsWindow();
        Window WaitWindow(string id) { Window? result = null; Wait(() => (result = WindowNow(id)) is not null, "Missing owned window " + id); return result!; }
        void WaitGone(string id) => Wait(() => WindowNow(id) is null, "Window did not close: " + id);
        void OpenPicker() { Find(main!, "MermaidCreation").AsButton().Invoke(); picker = WaitWindow("MermaidDiagramPicker"); }
        void PrepareTextProvider(AutomationElement editor)
        {
            // Pinned AvalonEdit's parent peer uses FromElement(TextArea), not CreatePeerForElement.
            // Observing its actual descendants materialises the child provider before querying Text.
            // Keep the real parent TextPattern and the existing action deadline; never infer selection.
            _ = editor.FindAllDescendants();
            Wait(() => editor.Patterns.Text.IsSupported, "The observed source editor has no TextPattern provider.");
            Check(editor.Patterns.Text.Pattern.DocumentRange.GetText(-1) == ReadEditorText(editor), "Observed text provider belongs to the exact source editor");
        }
        void Choose(MermaidDiagramDefinition definition, MermaidStarterVariant variant, string expectedOwner = original)
        {
            Find(picker!, "MermaidPickerSearch").AsTextBox().Text = definition.Name;
            var list = Find(picker!, "MermaidPickerFamilies").AsListBox();
            Wait(() => list.Items.Any(item => item.Name == definition.Name), "Searched family missing."); list.Select(definition.Name);
            Find(picker!, "MermaidPickerVariants").AsComboBox().Select(variant.Name);
            Wait(() => Find(picker!, "MermaidPickerCreate").IsEnabled, "Selected example unavailable: " + Find(picker!, "MermaidPickerStatus").Name);
            Check(ReadEditorText(Find(main!, "MarkdownEditor")) == expectedOwner, "Browsing does not change source");
        }
        void CreateVisual(MermaidDiagramDefinition definition, bool keyboard = false)
        {
            if (keyboard) { FocusWindow(picker!); Find(picker!, "MermaidPickerFamilies").AsListBox().SelectedItem!.Focus(); Keyboard.Type(VirtualKeyShort.RETURN); }
            else Find(picker!, "MermaidPickerCreate").AsButton().Invoke();
            WaitGone("MermaidDiagramPicker"); picker = null;
            visual = WaitWindow(Prefix(definition.Family) + "VisualEditor");
            Check(WindowNow("MermaidDiagramPicker") is null, "Exactly one editor replaces the picker");
        }
        void LaunchStandalone(string fixture)
        {
            if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); }
            var localState = Path.Combine(root, "standalone-state-" + ++standaloneLaunch); Directory.CreateDirectory(localState);
            File.WriteAllText(Path.Combine(localState, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(Theme: dark ? "Dark" : "Light", AutomaticUpdateChecks: false)));
            var start = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            foreach (var argument in new[] { "--state-directory", localState, root, fixture }) start.ArgumentList.Add(argument);
            var timer = Stopwatch.StartNew(); app = Application.Launch(start);
            main = NativeExpansionLaunch.Prepare(app, automation, timer, fixture, output, standaloneLaunch, highContrast); picker = visual = null;
        }
        string Draft(MermaidDiagramFamily family)
        {
            var prefix = Prefix(family); var panel = Find(visual!, prefix + "SourcePanel"); panel.Patterns.ExpandCollapse.Pattern.Expand();
            var id = family == MermaidDiagramFamily.Flowchart ? "MermaidDraftSource" : family is MermaidDiagramFamily.Sequence or MermaidDiagramFamily.State or MermaidDiagramFamily.Er or MermaidDiagramFamily.Class ? prefix + "Source" : prefix + "SourceEditor";
            return ReadEditorText(Find(visual!, id));
        }
        string Prefix(MermaidDiagramFamily family) => family switch { MermaidDiagramFamily.Flowchart => "Mermaid", MermaidDiagramFamily.Xy => "XYChart", _ => family.ToString() };
        void ResizePicker(int logicalWidth, int logicalHeight)
        {
            var dpi = CreationWindowDpi((nint)picker!.Properties.NativeWindowHandle.Value);
            if (dpi == 0) throw new IOException("The picker DPI could not be observed.");
            var width = logicalWidth * dpi / 96d; var height = logicalHeight * dpi / 96d;
            picker.Patterns.Transform.Pattern.Move(0, 0); picker.Patterns.Transform.Pattern.Resize(width, height);
            Wait(() => Math.Abs(picker.BoundingRectangle.Width - width) <= 1 && Math.Abs(picker.BoundingRectangle.Height - height) <= 1,
                "Actual picker dimensions differ from the requested logical size.");
        }
        void CheckTabNavigation()
        {
            var expected = new[] { "MermaidPickerSearch", "MermaidPickerClear", "MermaidPickerCategory", "MermaidPickerFamilies", "MermaidPickerVariants", "MermaidPickerSourcePanel", "MermaidPickerCancel", "MermaidPickerCode", "MermaidPickerCreate" };
            var walker = automation.TreeWalkerFactory.GetRawViewWalker();
            foreach (var backwards in new[] { false, true })
            {
                var visited = new HashSet<string>(); FocusWindow(picker!); Find(picker!, "MermaidPickerSearch").Focus();
                for (var step = 0; step < 32 && visited.Count < expected.Length; step++)
                {
                    var focused = automation.FocusedElement(); var inPicker = false;
                    for (var depth = 0; focused is not null && depth < 20; depth++, focused = walker.GetParent(focused))
                    {
                        var id = focused.Properties.AutomationId.ValueOrDefault;
                        if (id is not null && expected.Contains(id)) visited.Add(id);
                        if (id == "MermaidDiagramPicker") { inPicker = true; break; }
                    }
                    Check(inPicker, "Keyboard focus remains inside the owned picker");
                    if (backwards) Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
                    else Keyboard.Type(VirtualKeyShort.TAB);
                    app!.WaitWhileBusy(TimeSpan.FromSeconds(5));
                }
                Check(expected.All(visited.Contains), (backwards ? "Shift+Tab" : "Tab") + " reaches search, categories, families, variants, source and every footer action");
            }
        }
        void Capture(Window window, string name)
        {
            _ = DwmFlush(); window.CaptureToFile(Path.Combine(output, name + ".png"));
            captures.Add(new { File = name + ".png", Dpi = CreationWindowDpi((nint)window.Properties.NativeWindowHandle.Value), PhysicalBounds = window.BoundingRectangle });
        }
        void Check(bool success, string message) { if (!success) throw new IOException(message); checks.Add(message); }
        void Receipt(string status, string? error) => File.WriteAllText(Path.Combine(output, "creation-ui.json"), JsonSerializer.Serialize(new
        {
            Status = status, SourceRevision = typeof(MermaidDiagramPickerWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
            HighContrast = highContrast, Dark = dark, Checks = checks, Phases = phases, Captures = captures, Error = error
        }, new JsonSerializerOptions { WriteIndented = true }));
    }).GetAwaiter().GetResult();

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint CreationWindowDpi(nint handle);
}
