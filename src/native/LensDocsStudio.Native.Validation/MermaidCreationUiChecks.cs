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
        Application? app = null; Window? main = null, picker = null, visual = null;
        try
        {
            if (highContrast) Check(NativeTestHighContrast.IsEnabledNow, "Actual OS High Contrast enabled on disposable Windows");
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            foreach (var argument in new[] { "--state-directory", state, root, path }) info.ArgumentList.Add(argument);
            var timer = Stopwatch.StartNew(); app = Application.Launch(info);
            main = NativeExpansionLaunch.Prepare(app, automation, timer, path, output, 1, highContrast);
            var owner = Find(main, "MarkdownEditor");

            // Search, clear, categories and keyboard never author source. Cancellation restores the exact selected text.
            owner.Focus(); owner.Patterns.Text.Pattern.DocumentRange.FindText("selected text", false, false).Select();
            OpenPicker();
            var search = Find(picker!, "MermaidPickerSearch");
            search.AsTextBox().Text = "no matching xyz";
            Wait(() => Find(picker!, "MermaidPickerFamilies").AsListBox().Items.Length == 0 && !Find(picker!, "MermaidPickerCreate").IsEnabled, "Empty search did not disable Create.");
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

            foreach (var definition in MermaidDiagramCatalogue.All)
            foreach (var variant in definition.Variants)
            {
                owner.Patterns.Value.Pattern.SetValue(original); FocusWindow(main); owner.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                OpenPicker(); Choose(definition, variant);
                foreach (var (width, height) in new[] { (880, 600), (1280, 800) })
                {
                    picker!.Patterns.Transform.Pattern.Move(0, 0); picker.Patterns.Transform.Pattern.Resize(width, height);
                    var create = Find(picker, "MermaidPickerCreate");
                    Check(!create.IsOffscreen && picker.BoundingRectangle.Contains(create.BoundingRectangle), definition.Name + " footer visible at " + width + "×" + height);
                    Find(picker, "MermaidPickerSearch").Focus(); Keyboard.Type(VirtualKeyShort.TAB);
                    Check(picker.FindAllDescendants().Any(element => element.Properties.HasKeyboardFocus.ValueOrDefault), "Tab remains within the picker");
                    if (width == 1280 || definition.Family == MermaidDiagramFamily.Flowchart)
                        Capture(picker, definition.Id + "-" + variant.Id + "-" + width + "x" + height);
                }
                CreateVisual(definition); var prefix = Prefix(definition.Family);
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
            foreach (var (source, selection) in new[] { ("```csharp\ncode\n```\n", "code"), ("```mermaid\nC4Deployment\n```\n", "C4Deployment"), ("```mermaid\nflowchart TD\n", "flowchart"), ("```mermaid\nflowchart TD\n```\n", "```mermaid") })
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
        void Choose(MermaidDiagramDefinition definition, MermaidStarterVariant variant)
        {
            Find(picker!, "MermaidPickerSearch").AsTextBox().Text = definition.Name;
            var list = Find(picker!, "MermaidPickerFamilies").AsListBox();
            Wait(() => list.Items.Any(item => item.Name == definition.Name), "Searched family missing."); list.Select(definition.Name);
            Find(picker!, "MermaidPickerVariants").AsComboBox().Select(variant.Name);
            Wait(() => Find(picker!, "MermaidPickerCreate").IsEnabled, "Selected example unavailable: " + Find(picker!, "MermaidPickerStatus").Name);
            Check(ReadEditorText(Find(main!, "MarkdownEditor")) == original, "Browsing does not change source");
        }
        void CreateVisual(MermaidDiagramDefinition definition)
        {
            Find(picker!, "MermaidPickerCreate").AsButton().Invoke(); WaitGone("MermaidDiagramPicker"); picker = null;
            visual = WaitWindow(Prefix(definition.Family) + "VisualEditor");
            Check(WindowNow("MermaidDiagramPicker") is null, "Exactly one editor replaces the picker");
        }
        string Draft(MermaidDiagramFamily family)
        {
            var prefix = Prefix(family); var panel = Find(visual!, prefix + "SourcePanel"); panel.Patterns.ExpandCollapse.Pattern.Expand();
            var id = family == MermaidDiagramFamily.Flowchart ? "MermaidDraftSource" : family is MermaidDiagramFamily.Sequence or MermaidDiagramFamily.State or MermaidDiagramFamily.Er or MermaidDiagramFamily.Class ? prefix + "Source" : prefix + "SourceEditor";
            return ReadEditorText(Find(visual!, id));
        }
        string Prefix(MermaidDiagramFamily family) => family switch { MermaidDiagramFamily.Flowchart => "Mermaid", MermaidDiagramFamily.Xy => "XYChart", _ => family.ToString() };
        void Capture(Window window, string name) { _ = DwmFlush(); window.CaptureToFile(Path.Combine(output, name + ".png")); }
        void Check(bool success, string message) { if (!success) throw new IOException(message); checks.Add(message); }
        void Receipt(string status, string? error) => File.WriteAllText(Path.Combine(output, "creation-ui.json"), JsonSerializer.Serialize(new
        {
            Status = status, SourceRevision = typeof(MermaidDiagramPickerWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
            HighContrast = highContrast, Dark = dark, Checks = checks, Phases = phases, Error = error
        }, new JsonSerializerOptions { WriteIndented = true }));
    }).GetAwaiter().GetResult();
}
