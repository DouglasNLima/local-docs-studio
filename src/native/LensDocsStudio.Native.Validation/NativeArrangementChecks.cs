using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeArrangementChecks
{
    public static void Run(string output, string root, bool highContrast = false)
    {
        if (Directory.Exists(output)) throw new IOException("Use a fresh arrangement evidence directory; previous results must be retained.");
        Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var checks = new List<string>(); var measurements = new List<object>(); var passed = false;
        try
        {
            VerifyEntryReadiness(Check);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "tests/fixtures/native-complex-er"), "*.mmd"))
            {
                var source = File.ReadAllText(file); var engine = new ArrangementEngine(new DiagramService().Render(source));
                var watch = Stopwatch.StartNew(); var allocated = GC.GetTotalAllocatedBytes();
                var candidates = engine.Candidates(engine.Canonical.State); var elapsed = watch.Elapsed.TotalMilliseconds;
                File.WriteAllText(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + "-Canonical.svg"), engine.Canonical.Render.Svg);
                File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + "-Canonical.png"), NativeSvgService.ToExportPng(DiagramInspection.CreateArranged(engine.Canonical, new())!.Drawing));
                Check(candidates.Count > 0, "Public ER fixture has a real safe candidate: " + Path.GetFileName(file));
                foreach (var candidate in candidates)
                {
                    var inspection = DiagramInspection.CreateArranged(candidate, new());
                    Check(inspection?.Entities.Count == candidate.State.Positions.Count && inspection.Connections.Count == candidate.Render.ErLayout!.Relationships.Count, "Every arranged ER occurrence is inspectable");
                    Check(candidate.Render.Source == source && candidate.Quality.BodyIntersections == 0 && candidate.Quality.LabelBodyOverlaps == 0, "Arranged source and hard geometry constraints hold");
                    var restored = new ArrangementEngine(new DiagramService().Render(source)).Arrange(candidate.State);
                    Check(restored.Render.Svg == candidate.Render.Svg, "Saved candidate positions and routing variant reproduce exact SVG");
                    var reopened = DiagramInspection.CreateArranged(restored, new())!;
                    Check(inspection!.Connections.Select(c => c.Identity).SequenceEqual(reopened.Connections.Select(c => c.Identity)), "Connection occurrence identities survive reopen");
                    var name = Path.GetFileNameWithoutExtension(file) + "-" + candidate.State.Strategy;
                    File.WriteAllText(Path.Combine(output, name + ".svg"), candidate.Render.Svg);
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), NativeSvgService.ToExportPng(inspection.Drawing));
                    measurements.Add(new { Fixture = Path.GetFileName(file), candidate.State.Strategy, candidate.State.LayoutVariant, Before = engine.Canonical.Quality, After = candidate.Quality,
                        CandidateMilliseconds = elapsed, AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, WorkingSetBytes = Environment.WorkingSet });
                }
            }
            // Real candidate selection/acceptance in a shown window, separately from
            // pure engine tests and the hosted pointer/keyboard acceptance process.
            ThemeService.Apply(Application.Current!.Resources, "Light");
            var dense = new System.Text.StringBuilder("flowchart TD\n"); var random = new Random(2);
            for (var i = 0; i < 14; i++) dense.Append($" N{i}[Node {i}]\n");
            for (var i = 0; i < 36; i++) dense.Append($" N{random.Next(14)} --> N{random.Next(14)}\n");
            var denseSource = dense.ToString(); var denseWorkspace = Path.Combine(output, "Dense-Flowchart"); Directory.CreateDirectory(denseWorkspace);
            File.WriteAllText(Path.Combine(denseWorkspace, "diagram.mmd"), denseSource);
            ArrangementDocumentContext DenseContext() => new(denseWorkspace, "diagram.mmd", denseSource, true, 0);
            var denseWindow = new DiagramArrangementWindow(denseSource, new(), DenseContext, ThemeService.IsDark("Light")); denseWindow.Show();
            Until(() => Field(denseWindow, "session") is ArrangementSession && !(bool)Field(denseWindow, "busy")!);
            var denseSession = (ArrangementSession)Field(denseWindow, "session")!;
            Capture(denseWindow, Path.Combine(denseWorkspace, "canonical-window.png"));
            Call(denseWindow, "GenerateCandidates", denseWindow, new RoutedEventArgs()); Call(denseWindow, "Revert", denseWindow, new RoutedEventArgs());
            Until(() => !(bool)Field(denseWindow, "busy")!);
            Check(!denseSession.IsDirty && Field(denseWindow, "candidateSearch") is null, "Cancelling candidate search retains the accepted source-owned layout");
            Call(denseWindow, "GenerateCandidates", denseWindow, new RoutedEventArgs()); Until(() => !(bool)Field(denseWindow, "busy")!);
            var denseChoices = (ComboBox)denseWindow.FindName("CandidateChoices"); Check(denseChoices.Items.Count == 2, "Dense Flowchart exposes two real measured strategies in the native window");
            denseChoices.SelectedIndex = 0; Until(() => !(bool)Field(denseWindow, "busy")!);
            Check(!denseSession.IsDirty && Field(denseWindow, "proposed") is ArrangementSnapshot, "Previewing an automatic layout cannot replace the accepted layout");
            Capture(denseWindow, Path.Combine(denseWorkspace, "candidate-window.png"));
            Call(denseWindow, "AcceptCandidate", denseWindow, new RoutedEventArgs()); Until(() => !(bool)Field(denseWindow, "busy")!);
            Check(denseSession.IsDirty && denseSession.Current.State.Strategy != ArrangementStrategy.Canonical, "Explicit acceptance installs a distinct verified native candidate");
            Call(denseWindow, "SaveLayout", denseWindow, new RoutedEventArgs()); Until(() => !(bool)Field(denseWindow, "busy")!);
            var denseSvg = denseSession.Current.Render.Svg; Check(!denseSession.IsDirty, "Accepted candidate saves independently of source"); denseWindow.Close();
            var denseReopen = new DiagramArrangementWindow(denseSource, new(), DenseContext, ThemeService.IsDark("Light")); denseReopen.Show();
            Until(() => Field(denseReopen, "session") is ArrangementSession && !(bool)Field(denseReopen, "busy")!);
            Check(((ArrangementSession)Field(denseReopen, "session")!).Current.Render.Svg == denseSvg, "Native reopen restores the accepted automatic routes exactly"); denseReopen.Close();

            foreach (var theme in highContrast ? new[] { "System" } : new[] { "Light", "Dark" })
            foreach (var family in new[] { "ER", "Flowchart" })
            {
                ThemeService.Apply(Application.Current!.Resources, theme);
                var source = family == "ER" ? "erDiagram\n A ||--o{ B : owns\n A ||--o{ B : reviews\n B ||--o{ B : revisits\n"
                    : "flowchart TD\n A[Start] -->|next| B{Decision}\n A -->|review| B\n B -->|retry| B\n";
                var workspace = Path.Combine(output, theme + "-" + family); Directory.CreateDirectory(workspace);
                var path = Path.Combine(workspace, "diagram.mmd"); File.WriteAllText(path, source);
                var changed = false;
                ArrangementDocumentContext Context() => changed ? throw new IOException("The owning diagram changed.") : new(workspace, "diagram.mmd", source, true, 0);
                var window = new DiagramArrangementWindow(source, new(), Context, ThemeService.IsDark(theme));
                window.Show(); Until(() => Field(window, "session") is ArrangementSession && !(bool)Field(window, "busy")!);
                var session = (ArrangementSession)Field(window, "session")!; var engine = (ArrangementEngine)Field(window, "engine")!;
                Check(((RadioButton)window.FindName("NavigateMode")).IsChecked == true, "Navigate is the default mode");
                Check(!Directory.Exists(Path.Combine(workspace, ".lens-docs")), "Opening a diagram creates no sidecar");
                Capture(window, Path.Combine(workspace, "canonical-window.png"));
                Call(window, "GenerateCandidates", window, new RoutedEventArgs()); Until(() => !(bool)Field(window, "busy")!);
                var choices = (ComboBox)window.FindName("CandidateChoices");
                if (choices.Items.Count > 0)
                {
                    choices.SelectedIndex = 0; Until(() => !(bool)Field(window, "busy")!);
                    Check(session.Current.State.SameAs(engine.Canonical.State), "Candidate preview cannot replace the accepted arrangement");
                    Capture(window, Path.Combine(workspace, "candidate-window.png"));
                    Call(window, "AcceptCandidate", window, new RoutedEventArgs()); Until(() => !(bool)Field(window, "busy")!);
                    Check(!session.Current.State.SameAs(engine.Canonical.State), "Explicit candidate acceptance changes real positions");
                    Call(window, "UndoLayout", window, new RoutedEventArgs()); Until(() => !(bool)Field(window, "busy")!);
                }
                var state = new ArrangementState(session.Current.State.Positions.Select(p => new KeyValuePair<string, ArrangementPosition>(p.Key, p.Key == "A" ? new(100, 100, true) : new(620, 420))), ArrangementStrategy.Manual);
                Await((Task)Call(window, "ApplyState", state)!);
                Check(session.Current.State.Positions["A"].Pinned && session.IsDirty, "Manual move and pin belong to layout history");
                Check(((TextBlock)window.FindName("StateLabel")).Text.Contains("Unsaved arrangement"), "Unsaved state is accessible");
                var inspection = (DiagramInspection)Field(window, "inspection")!;
                Check(inspection.Connections.Count == 3 && inspection.Connections.Select(c => c.Identity).Distinct().Count() == 3, "Parallel and self connections have distinct current hit identities");
                Call(window, "Fit", window, new RoutedEventArgs());
                Capture(window, Path.Combine(workspace, "arranged-window.png"));
                File.WriteAllText(Path.Combine(workspace, "arranged.svg"), session.Current.Render.Svg);
                File.WriteAllBytes(Path.Combine(workspace, "arranged.png"), NativeSvgService.ToExportPng(inspection.Drawing));
                Call(window, "SaveLayout", window, new RoutedEventArgs()); Until(() => !(bool)Field(window, "busy")!);
                Check(!session.IsDirty && File.Exists(Path.Combine(workspace, ArrangementStore.RelativePath)), "Explicit Save layout writes a portable sidecar");
                Check(File.ReadAllText(path) == source, "Arrange and Save preserve the exact source file");
                if (!highContrast)
                {
                    foreach (var appearance in new[] { theme == "Light" ? "Dark" : "Light", theme })
                    {
                        ThemeService.Apply(Application.Current.Resources, appearance); window.ApplyTheme(appearance);
                        Until(() => !(bool)Field(window, "busy")!);
                        Check(session.Current.State.SameAs(state) && !session.IsDirty, "Appearance changes preserve the saved arrangement and pins");
                    }
                }
                Call(window, "UndoLayout", window, new RoutedEventArgs()); Until(() => !(bool)Field(window, "busy")!);
                Check(session.Current.State.SameAs(engine.Canonical.State), "Layout Undo restores canonical positions independently");
                Call(window, "RedoLayout", window, new RoutedEventArgs()); Until(() => !(bool)Field(window, "busy")!);
                Check(session.Current.State.Positions["A"].Pinned, "Layout Redo restores pins");
                var expectedSvg = session.Current.Render.Svg; changed = true;
                try { Await((Task)Call(window, "ApplyState", state.Move("B", 700, 500))!); throw new IOException("Stale owner was accepted."); }
                catch (IOException error) when (error.Message == "The owning diagram changed.") { Check(session.Current.Render.Svg == expectedSvg, "Stale ownership rejects completed work"); }
                changed = false; window.Close();
                var reopened = new DiagramArrangementWindow(source, new(), Context, ThemeService.IsDark(theme)); reopened.Show();
                Until(() => Field(reopened, "session") is ArrangementSession && !(bool)Field(reopened, "busy")!);
                var restored = (ArrangementSession)Field(reopened, "session")!;
                Check(restored.Current.Render.Svg == expectedSvg && restored.Current.State.Positions["A"].Pinned, "Actual native reopen restores the same arranged snapshot and pins");
                Check(AutomationProperties.GetAutomationId((FrameworkElement)reopened.FindName("Viewport")) == "ArrangementViewport", "Viewport has a stable UIA identity");
                reopened.Close();
            }
            passed = true;
        }
        finally { File.WriteAllText(Path.Combine(output, "arrangement.json"), JsonSerializer.Serialize(new { Status = passed ? "PASS" : "FAIL", Provenance = "Public synthetic fixtures and shown owned WPF windows; no desktop input or private sources", HighContrast = highContrast, Checks = checks, Measurements = measurements }, new JsonSerializerOptions { WriteIndented = true })); }
        void Check(bool condition, string description) { if (!condition) throw new IOException(description); checks.Add(description); }
    }
    private static void VerifyEntryReadiness(Action<bool, string> check)
    {
        ThemeService.Apply(Application.Current.Resources, "Light");
        foreach (var family in new[] { "ER", "Flowchart" })
        {
            var source = family == "ER" ? "erDiagram\n A ||--o{ B : owns\n" : "flowchart TD\n A --> B\n";
            Window editor = family == "ER" ? new ErVisualEditorWindow(source, new(), (_, _) => Task.FromResult<string?>(null))
                : new MermaidVisualEditorWindow(source, new(), (_, _) => Task.FromResult<string?>(null));
            var called = false; Action<Window, string> arrange = (_, actualSource) => called = actualSource == source;
            if (editor is ErVisualEditorWindow er) er.ArrangeDiagram = arrange;
            if (editor is MermaidVisualEditorWindow flow) flow.ArrangeDiagram = arrange;
            var button = (Button)editor.FindName("ArrangeButton");
            check(!button.IsEnabled, "Arrange entry is disabled before canonical mapping: " + family);
            editor.Show(); Until(() => button.IsEnabled);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(called, "Ready Arrange entry routes the exact owning source: " + family);
            var input = (ICSharpCode.AvalonEdit.TextEditor)editor.FindName("SourceEditor"); input.Text = source + "\n";
            check(!button.IsEnabled, "Pending source disables Arrange entry: " + family);
            input.Text = source; editor.Close();
        }
    }
    private static object? Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    private static object? Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
    private static void Await(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> complete)
    {
        var watch = Stopwatch.StartNew();
        while (!complete())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Arrangement acceptance timed out.");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false); Dispatcher.PushFrame(frame); Thread.Sleep(10);
        }
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
