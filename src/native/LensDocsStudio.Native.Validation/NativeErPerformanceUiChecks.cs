using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunErPerformance(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null; using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var measurements = new List<object>(); Application? app = null; Window? visual = null;
        try
        {
            foreach (var (entities, relationships) in new[] { (10, 15), (30, 60), (100, 150) })
            {
                var root = Path.Combine(output, "fixture-" + entities); Directory.CreateDirectory(root); var stateRoot = Path.Combine(root, "state"); Directory.CreateDirectory(stateRoot);
                File.WriteAllText(Path.Combine(stateRoot, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
                var source = "erDiagram\n" + string.Concat(Enumerable.Range(0, entities).Select(i => " E" + i + " {\n string id PK\n string reference FK\n }\n"))
                    + string.Concat(Enumerable.Range(0, relationships).Select(i => " E" + (i % entities) + " ||--o{ E" + ((i * 7 + 1) % entities) + " : relation" + i + "\n"));
                var render = new DiagramService().Render(source);
                if (!render.Success) { measurements.Add(new { Entities = entities, Relationships = relationships, ResourceLimit = render.Error, NativeEditorOpened = false, LimitsUnchanged = true }); continue; }
                var path = Path.Combine(root, "ER.mmd"); File.WriteAllText(path, source);
                var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false }; foreach (var argument in new[] { "--state-directory", stateRoot, root, path }) info.ArgumentList.Add(argument);
                app = Application.Launch(info); var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The ER performance fixture did not open.");
                Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "The ER performance preview action did not appear.");
                using var process = Process.GetProcessById(app.ProcessId); process.Refresh(); var beforeMemory = process.WorkingSet64;
                var timer = Stopwatch.StartNew(); Find(main, "DiagramEditVisually0").AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("ErVisualEditor")))?.AsWindow()) is not null, "The ER performance editor did not open.");
                Wait(() => Find(visual!, "ErApply").IsEnabled, "The ER performance editor did not finish canonical mapping."); var openMs = timer.Elapsed.TotalMilliseconds;
                Find(visual!, "ErSourcePanel").Patterns.ExpandCollapse.Pattern.Expand(); timer.Restart();
                for (var i = 0; i < 20; i++) Find(visual!, "ErCanvas_entity:E" + (i % entities)).Patterns.SelectionItem.Pattern.Select(); var entityMs = timer.Elapsed.TotalMilliseconds;
                var targets = visual!.FindAllDescendants().Where(item => (item.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("ErCanvas_", StringComparison.Ordinal)).ToArray();
                var rows = targets.Where(item => item.Name.Contains(" · attribute ", StringComparison.Ordinal)).ToArray();
                var edges = targets.Where(item => item.Name.StartsWith("Relationship ", StringComparison.Ordinal)).ToArray();
                if (rows.Length != entities * 2 || edges.Length != relationships) throw new IOException("ER native performance targets do not match exact source occurrences.");
                timer.Restart(); for (var i = 0; i < 20; i++) rows[i % rows.Length].Patterns.SelectionItem.Pattern.Select(); var attributeMs = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); for (var i = 0; i < 20; i++) edges[i % edges.Length].Patterns.SelectionItem.Pattern.Select(); var relationshipMs = timer.Elapsed.TotalMilliseconds;
                Find(visual!, "ErFit").AsButton().Invoke(); var endpoint = Find(visual!, "ErSecondEndpoint"); var bounds = endpoint.BoundingRectangle; var viewport = Find(visual!, "ErViewport").BoundingRectangle; string? drag = null;
                if (!endpoint.IsOffscreen && viewport.Contains(bounds))
                {
                    Mouse.MoveTo(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2); Mouse.Down(MouseButton.Left);
                    try { for (var step = 1; step <= 20; step++) { Mouse.MoveTo(bounds.Left + step, bounds.Top + step); Thread.Sleep(8); } Keyboard.Type(VirtualKeyShort.ESCAPE); } finally { Mouse.Up(MouseButton.Left); }
                    drag = Find(visual!, "ErStatus").Properties.HelpText.ValueOrDefault;
                    if (string.IsNullOrWhiteSpace(drag)) throw new IOException("The visible ER endpoint performance drag did not produce actual updates.");
                }
                if (ReadEditorText(Find(visual!, "ErSource")) != source || File.ReadAllText(path) != source || app.HasExited || !process.Responding) throw new IOException("ER performance interaction changed source or stopped responding.");
                process.Refresh(); measurements.Add(new { Entities = entities, Relationships = relationships, EditorOpenIncludingUiaMilliseconds = openMs, TwentyEntitySelectionsMilliseconds = entityMs,
                    TwentyAttributeSelectionsMilliseconds = attributeMs, TwentyRelationshipSelectionsMilliseconds = relationshipMs, WorkingSetBeforeEditor = beforeMemory, WorkingSetAfterInteractions = process.WorkingSet64,
                    ActualEndpointDrag = drag, DragLimitation = drag is null ? "No complete endpoint target in the fitted viewport; no pointer timing claim for this size." : null, SourceExact = true, ProcessResponding = true });
                _ = DwmFlush(); visual.CaptureToFile(Path.Combine(output, "er-" + entities + ".png")); app.Kill(); app.Dispose(); app = null; visual = null;
            }
            File.WriteAllText(Path.Combine(output, "er-performance-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Measurements = measurements, Scope = "Real disposable native process; timings include UIA transport and cold conversion. Resource failures and pointer limitations remain explicit. No rendering limits changed." }, new JsonSerializerOptions { WriteIndented = true })); display.Complete(); return 0;
        }
        catch (Exception error) { try { visual?.CaptureToFile(Path.Combine(output, "failure.png")); } catch { } File.WriteAllText(Path.Combine(output, "er-performance-failure.txt"), error.ToString()); Console.Error.WriteLine(error); return 1; }
        finally { Mouse.Up(MouseButton.Left); if (app is not null) { try { app.Kill(); } catch { } app.Dispose(); } }
    }).GetAwaiter().GetResult();
}
