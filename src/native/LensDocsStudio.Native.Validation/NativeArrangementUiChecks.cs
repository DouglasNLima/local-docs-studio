using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunArrangementInput(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>();
        foreach (var family in new[] { "ER", "Flowchart" })
        {
            var folder = Path.Combine(output, family); Directory.CreateDirectory(folder); var stateRoot = Path.Combine(folder, "state"); Directory.CreateDirectory(stateRoot);
            File.WriteAllText(Path.Combine(stateRoot, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false, Theme: "Light")));
            var source = family == "ER" ? "erDiagram\n A ||--o{ B : owns\n" : "flowchart TD\n A[Start] --> B[Finish]\n";
            var path = Path.Combine(folder, "diagram.mmd"); File.WriteAllText(path, source); var bytes = File.ReadAllBytes(path);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var argument in new[] { "--state-directory", stateRoot, path }) start.ArgumentList.Add(argument);
            using var app = Application.Launch(start); Window? main = null; Window? arrangement = null;
            try
            {
                main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The arrangement test app did not open."); FocusWindow(main);
                Wait(() => ReadEditorText(Find(main, "MarkdownEditor")) == source && Find(main, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "The source-owned preview did not become ready.");
                var visual = main.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)).First(button => button.AutomationId.StartsWith("DiagramEditVisually", StringComparison.Ordinal)); visual.AsButton().Invoke();
                Window? editor = null;
                Wait(() => (editor = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId(family == "ER" ? "ErVisualEditor" : "MermaidVisualEditor")))?.AsWindow()) is not null, "The owning editor did not open.");
                Wait(() => Find(editor!, family == "ER" ? "ErArrange" : "MermaidArrange").IsEnabled, "The Arrange entry was not enabled.");
                Find(editor!, family == "ER" ? "ErArrange" : "MermaidArrange").AsButton().Invoke();
                Wait(() => (arrangement = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("DiagramArrangementWindow")))?.AsWindow()) is not null, "The arrangement window did not open.");
                Ready(); FocusWindow(arrangement!);
                Check(Find(arrangement!, "ArrangementNavigate").Patterns.SelectionItem.Pattern.IsSelected.Value, "Navigate is selected by default: " + family);
                Find(arrangement!, "ArrangementArrange").Patterns.SelectionItem.Pattern.Select();
                var engine = new ArrangementEngine(new DiagramService().Render(source, ThemeService.IsDark("Light")));
                var canonical = StaWorker.RunAsync(() => DiagramInspection.CreateArranged(engine.Canonical, new())!, default).GetAwaiter().GetResult();
                var node = canonical.Entities.Single(item => item.Identity.EndsWith(":A", StringComparison.Ordinal)).Shape.Bounds;
                var image = Find(arrangement!, "ArrangementDrawing").BoundingRectangle;
                var point = new System.Drawing.Point((int)(image.Left + (node.X + node.Width / 2) * image.Width / canonical.Drawing.Width), (int)(image.Top + (node.Y + node.Height / 2) * image.Height / canonical.Drawing.Height));
                Mouse.MoveTo(point); Mouse.Down(MouseButton.Left);
                try { Mouse.MoveTo(point.X + 70, point.Y + 15); } finally { Mouse.Up(MouseButton.Left); }
                Wait(() => Find(arrangement!, "ArrangementStatus").Name.StartsWith("Arrangement accepted", StringComparison.Ordinal), "Pointer movement did not produce a verified arrangement."); Ready();
                Find(arrangement!, "ArrangementPin").AsButton().Invoke(); Ready(); Save();
                var saved = ReadState(); Check(saved.Positions["A"].Pinned && saved.Positions["A"].X != engine.Canonical.State.Positions["A"].X, "Actual pointer movement and pin persist: " + family);
                var movedInspection = StaWorker.RunAsync(() => DiagramInspection.CreateArranged(engine.Arrange(saved), new())!, default).GetAwaiter().GetResult();
                var movedNode = movedInspection.Entities.Single(item => item.Identity.EndsWith(":A", StringComparison.Ordinal)).Shape.Bounds;
                image = Find(arrangement!, "ArrangementDrawing").BoundingRectangle;
                var movedPoint = new System.Drawing.Point((int)(image.Left + (movedNode.X + movedNode.Width / 2) * image.Width / movedInspection.Drawing.Width),
                    (int)(image.Top + (movedNode.Y + movedNode.Height / 2) * image.Height / movedInspection.Drawing.Height));
                Mouse.MoveTo(movedPoint); Mouse.Down(MouseButton.Left);
                try { Mouse.MoveTo(movedPoint.X + 30, movedPoint.Y + 20); Keyboard.Type(VirtualKeyShort.ESCAPE); } finally { Mouse.Up(MouseButton.Left); }
                Ready(); Save(); Check(ReadState().Positions["A"] == saved.Positions["A"], "Escape cancels an actual in-progress drag");
                Mouse.MoveTo(movedPoint); Mouse.Down(MouseButton.Left);
                try { Mouse.MoveTo(movedPoint.X + 30, movedPoint.Y + 20); FocusWindow(editor!); } finally { Mouse.Up(MouseButton.Left); }
                FocusWindow(arrangement!); Ready(); Save(); Check(ReadState().Positions["A"] == saved.Positions["A"], "Deactivation and lost capture cancel an actual drag");
                Find(arrangement!, "ArrangementViewport").Focus(); Keyboard.Type(VirtualKeyShort.DOWN);
                Wait(() => Find(arrangement!, "ArrangementState").Name.StartsWith("Unsaved arrangement", StringComparison.Ordinal), "Keyboard positioning did not mark the arrangement dirty."); Ready(); Save();
                var keyboard = ReadState(); Check(keyboard.Positions["A"].Y == saved.Positions["A"].Y + 10, "Arrow-key positioning uses drawing units: " + family);
                Find(arrangement!, "ArrangementUndo").AsButton().Invoke(); Ready(); Save(); Check(ReadState().Positions["A"] == saved.Positions["A"], "UI layout Undo restores the moved pinned position");
                Find(arrangement!, "ArrangementRedo").AsButton().Invoke(); Ready(); Save(); Check(ReadState().Positions["A"] == keyboard.Positions["A"], "UI layout Redo restores keyboard movement");
                foreach (var format in new[] { "SVG", "PNG" })
                {
                    Find(arrangement!, format == "SVG" ? "ArrangementExportSvg" : "ArrangementExportPng").AsButton().Invoke();
                    Window? dialogue = null;
                    Wait(() => (dialogue = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Export arranged " + format)))?.AsWindow()) is not null, "The explicit arranged export dialogue did not open.");
                    TextBox? filename = null;
                    Wait(() => (filename = dialogue!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit).And(cf.ByName("File name:")))?.AsTextBox())?.IsEnabled == true, "The export filename field was not ready.");
                    FocusWindow(dialogue!); filename!.Focus();
                    Wait(() => filename.Properties.HasKeyboardFocus.Value, "The export filename field did not acquire focus.");
                    var destination = Path.Combine(folder, "arranged." + format.ToLowerInvariant());
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(destination);
                    Wait(() => filename.Text == destination, "The export dialogue did not accept the typed destination.");
                    File.WriteAllText(Path.Combine(folder, "dialogue-" + format + ".json"), JsonSerializer.Serialize(new { Format = format, Expected = destination, Observed = filename.Text }));
                    dialogue!.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Save"))).Single(button => button.AutomationId != "DropDown").AsButton().Click();
                    Wait(() => File.Exists(Path.Combine(folder, "arranged." + format.ToLowerInvariant())), "The arranged export was not written."); Ready();
                }
                var expected = engine.Arrange(keyboard);
                Check(File.ReadAllText(Path.Combine(folder, "arranged.svg")) == expected.Render.Svg, "Actual arranged SVG export matches the saved visible snapshot");
                var expectedPng = StaWorker.RunAsync(() => NativeSvgService.ToExportPng(DiagramInspection.CreateArranged(expected, new())!.Drawing), default).GetAwaiter().GetResult();
                Check(File.ReadAllBytes(Path.Combine(folder, "arranged.png")).SequenceEqual(expectedPng), "Actual arranged PNG export matches the same verified snapshot");
                arrangement!.CaptureToFile(Path.Combine(folder, "arranged-input.png"));
                Check(ReadEditorText(Find(main, "MarkdownEditor")) == source && File.ReadAllBytes(path).SequenceEqual(bytes), "Actual arrange, pin, keyboard, Undo/Redo and exports preserve source bytes");
                arrangement!.Close(); arrangement = null;
                Find(editor!, family == "ER" ? "ErArrange" : "MermaidArrange").AsButton().Invoke();
                Wait(() => (arrangement = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("DiagramArrangementWindow")))?.AsWindow()) is not null, "Reopen failed.");
                Wait(() => Find(arrangement!, "ArrangementStatus").Name.StartsWith("Saved workspace arrangement restored", StringComparison.Ordinal), "Native re-open did not load the workspace sidecar.");
                Check(ReadState().Positions["A"] == keyboard.Positions["A"], "Real window re-open recovers the pinned arrangement");
                arrangement!.Close(); arrangement = null; editor!.Close();
                void Ready() => Wait(() => Find(arrangement!, "ArrangementSave").IsEnabled && !Find(arrangement!, "ArrangementState").Name.Contains("Working", StringComparison.Ordinal), "Arrangement work did not finish.");
                void Save() { Find(arrangement!, "ArrangementSave").AsButton().Invoke(); Wait(() => Find(arrangement!, "ArrangementStatus").Name.StartsWith("Layout saved", StringComparison.Ordinal), "Save layout did not complete."); Ready(); }
                ArrangementState ReadState()
                {
                    var record = new ArrangementStore(new(folder)).ReadAsync().GetAwaiter().GetResult().File.Layouts.Single();
                    return new(record.Positions, record.Strategy, record.LayoutVariant);
                }
            }
            catch (Exception error)
            {
                main?.CaptureToFile(Path.Combine(folder, "failure-main.png")); arrangement?.CaptureToFile(Path.Combine(folder, "failure-arrangement.png"));
                File.WriteAllText(Path.Combine(output, "arrangement-input.json"), JsonSerializer.Serialize(new { Passed = false, Checks = checks, Error = error.ToString() })); Console.Error.WriteLine(error); return 1;
            }
            finally { if (!app.HasExited) app.Kill(); }
        }
        File.WriteAllText(Path.Combine(output, "arrangement-input.json"), JsonSerializer.Serialize(new { Passed = true, HighContrast = highContrast, Checks = checks }, new JsonSerializerOptions { WriteIndented = true })); return 0;
        void Check(bool condition, string description) { if (!condition) throw new IOException(description); checks.Add(description); }
    }).GetAwaiter().GetResult();
}
