using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeArrangementRemediationChecks
{
    public static void Run(string output)
    {
        if (Directory.Exists(output)) throw new IOException("Use a new remediation evidence directory.");
        Directory.CreateDirectory(output);
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        ThemeService.Apply(Application.Current!.Resources, "Light");
        var checks = new List<string>(); var passed = false;
        try
        {
            foreach (var family in new[] { "ER", "Flowchart" })
            {
                var source = family == "ER" ? "erDiagram\n A ||--o{ B : owns\n" : "flowchart TD\n A --> B\n";
                Window editor = family == "ER" ? new ErVisualEditorWindow(source, new(), (_, _) => Task.FromResult<string?>(null))
                    : new MermaidVisualEditorWindow(source, new(), (_, _) => Task.FromResult<string?>(null));
                if (editor is ErVisualEditorWindow er) er.ArrangeDiagram = (_, _) => { };
                if (editor is MermaidVisualEditorWindow flow) flow.ArrangeDiagram = (_, _) => { };
                editor.Show(); Until(() => ((Button)editor.FindName("ArrangeButton")).IsEnabled);
                var input = (ICSharpCode.AvalonEdit.TextEditor)editor.FindName("SourceEditor");
                bool Owns() => editor is ErVisualEditorWindow e ? e.OwnsArrangementSource(source) : ((MermaidVisualEditorWindow)editor).OwnsArrangementSource(source);
                Check(Owns(), "Mapped editor owns its source: " + family); input.Text += "\n";
                Check(!Owns(), "A pending source draft invalidates an already open arrangement owner: " + family); input.Text = source;
                var consent = false; var prompts = 0;
                var window = new DiagramArrangementWindow(source, new(), () => null, exportRecovery: Path.Combine(output, "recovery"),
                    confirmDiscard: _ => { prompts++; return consent; }) { Owner = editor };
                window.Show(); Ready(window); var session = (ArrangementSession)Field(window, "session")!;
                Await((Task)Call(window, "ApplyState", session.Current.State.TogglePin("A"))!);
                editor.Close();
                Check(prompts == 1 && editor.IsVisible && window.IsVisible && session.IsDirty, "Owner close can retain the dirty arrangement: " + family);
                var closed = editor is ErVisualEditorWindow erOwner ? erOwner.CloseForOwner() : ((MermaidVisualEditorWindow)editor).CloseForOwner();
                Check(!closed && window.IsVisible && session.Current.State.Positions["A"].Pinned, "Application close arbitration reports a declined arrangement close: " + family);
                consent = true; editor.Close();
                Check(!editor.IsVisible && !window.IsVisible && prompts == 3, "Explicit discard closes the owned arrangement and editor: " + family);
            }
            var dense = new System.Text.StringBuilder("flowchart TD\n"); var random = new Random(2);
            for (var i = 0; i < 14; i++) dense.Append($" N{i}[Node {i}]\n");
            for (var i = 0; i < 36; i++) dense.Append($" N{random.Next(14)} --> N{random.Next(14)}\n");
            var candidateWindow = new DiagramArrangementWindow(dense.ToString(), new(), () => null, confirmDiscard: _ => true);
            candidateWindow.Show(); Ready(candidateWindow); Call(candidateWindow, "GenerateCandidates", candidateWindow, new RoutedEventArgs()); Ready(candidateWindow);
            var choices = (ComboBox)candidateWindow.FindName("CandidateChoices"); Check(choices.Items.Count > 0, "Dense public input has a safe candidate for the stale-state regression");
            var stale = choices.Items[0]; var candidateSession = (ArrangementSession)Field(candidateWindow, "session")!;
            Check(choices.Items.Count > 1, "Two independent candidates exercise rapid selection arbitration");
            var unchanged = candidateSession.Current;
            choices.SelectedIndex = 0;
            Check(!choices.IsEnabled, "The candidate selector is disabled while its preview is prepared");
            choices.SelectedIndex = 1; Ready(candidateWindow);
            Call(candidateWindow, "AcceptCandidate", candidateWindow, new RoutedEventArgs()); Ready(candidateWindow);
            Check(ReferenceEquals(unchanged, candidateSession.Current), "A programmatic selection race cannot accept a different candidate from the selected item");
            Call(candidateWindow, "CancelCandidate", candidateWindow, new RoutedEventArgs()); Ready(candidateWindow);
            Await((Task)Call(candidateWindow, "ApplyState", candidateSession.Current.State.TogglePin("N0"))!);
            var accepted = candidateSession.Current;
            Check(choices.Items.Count == 0 && candidateSession.Current.State.Positions["N0"].Pinned, "Pinning invalidates candidates from the prior generation");
            choices.ItemsSource = new[] { stale }; choices.SelectedIndex = 0; Ready(candidateWindow);
            Check(ReferenceEquals(accepted, candidateSession.Current) && Field(candidateWindow, "proposed") is null && choices.Items.Count == 0,
                "A stale candidate cannot remove a newer pin or replace the accepted layout");
            candidateWindow.Close();

            const string styled = "flowchart TD\n A[Readable] --> B[Finish]\n style A fill:#ffffff\n";
            var engine = new ArrangementEngine(new DiagramService().Render(styled, true));
            var snapshot = engine.Arrange(engine.Canonical.State.Move("A", 400, 400));
            var svg = new NativeSvgService(); var inspection = DiagramInspection.CreateArranged(snapshot, svg)!;
            var prepared = svg.Prepare(snapshot.Render.Svg!); var xml = XDocument.Parse(prepared);
            Check(xml.Descendants().Any(x => x.Name.LocalName == "text" && x.Value == "Readable" && (string?)x.Attribute("fill") == "#202124"), "Prepared arranged SVG preserves the visible contrasting authored node label");
            Check(xml.Descendants().Any(x => (string?)x.Attribute("class") == "native-background") && !xml.Descendants().Any(x => x.Name.LocalName == "image"), "Prepared arranged SVG has a full vector background and no raster substitution");
            Check(NativeSvgService.ToExportPng(svg.Render(prepared)).SequenceEqual(NativeSvgService.ToExportPng(inspection.Drawing)), "Prepared arranged SVG and accepted native PNG have identical pixels");
            File.WriteAllText(Path.Combine(output, "prepared-arranged.svg"), prepared);
            passed = true;
        }
        finally { File.WriteAllText(Path.Combine(output, "remediation.json"), JsonSerializer.Serialize(new { Status = passed ? "PASS" : "FAIL", Scope = "Shown owned WPF windows; controlled discard responses, no OS pointer injection", Checks = checks }, new JsonSerializerOptions { WriteIndented = true })); }
        void Check(bool value, string description) { if (!value) throw new IOException(description); checks.Add(description); }
    }
    private static object? Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
    private static object? Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void Ready(DiagramArrangementWindow window) => Until(() => Field(window, "session") is ArrangementSession && !(bool)Field(window, "busy")!);
    private static void Await(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> complete)
    {
        var clock = Stopwatch.StartNew();
        while (!complete())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Remediation check exceeded its bounded wait.");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false); Dispatcher.PushFrame(frame); Thread.Sleep(10);
        }
    }
}
