using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

/// <summary>Real WPF windows, native dispatcher input and controlled diagnostic GC.
/// This runs only on the disposable hosted desktop; no UIA transport enters these timings.</summary>
internal static class NativeSuiteLifecycleChecks
{
    public static void Run(string output, bool highContrast)
    {
        Directory.CreateDirectory(output); using var recorder = new MermaidPhaseRecorder(); using var contrast = highContrast ? new NativeTestHighContrast() : null;
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(application.Resources, "Light");
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var hostButton = new Button { Content = "Disposable lifecycle owner" };
        var host = new Window { Title = "Mermaid suite lifecycle fixture", Width = 1024, Height = 768, Content = hostButton };
        var windows = new List<WeakReference>(); var observations = new List<object>(); var service = new DiagramService();
        host.Show();
        try
        {
            foreach (var family in new[] { "Flowchart", "Sequence", "State", "ER", "Class" })
            {
                for (var cycle = 0; cycle < 6; cycle++) windows.Add(OpenClose(family, cycle, host, hostButton, service, observations));
                Drain();
                // Diagnostic collection measures retained windows. Never called by production code.
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Drain();
                var retained = windows.Count(item => item.IsAlive);
                observations.Add(new { Family = family, Operation = "Controlled diagnostic collection after six open/close cycles", RetainedWindows = retained, RetainedIdentities = windows.Select((item, index) => item.IsAlive ? "family index " + (index / 6) + ", cycle " + (index % 6) : null).Where(item => item is not null).ToArray(), ManagedBytes = GC.GetTotalMemory(false), WorkingSet = Environment.WorkingSet });
                if (retained != 0) throw new IOException(family + ": a closed editor remains retained after dispatcher drain and diagnostic collection.");
            }
            File.WriteAllText(Path.Combine(output, "suite-lifecycle.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrast = highContrast,
                Method = "Six real WPF window cycles per family, shared renderer service, exact source, native selection updates without UIA, Input-priority heartbeat, rapid theme cancellation and close while conversion capacity is held. Controlled GC occurs only in the diagnostic harness. Retained-window assertions are separate from working-set observations.", Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "suite-lifecycle-failure.json"), JsonSerializer.Serialize(new { Error = error.ToString(), Observations = observations }, new JsonSerializerOptions { WriteIndented = true })); throw;
        }
        finally { host.Close(); recorder.Write(Path.Combine(output, "suite-lifecycle-phases.json")); SynchronizationContext.SetSynchronizationContext(previous); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenClose(string family, int cycle, Window owner, Button ownerFocus, DiagramService service, List<object> observations)
    {
        var source = NativeSuitePerformanceChecks.Source(family, false) + (cycle == 2 ? "%% queued lifecycle fixture\n" : "");
        ThemeService.Apply(Application.Current.Resources, "Light");
        using var firstPresentationPhase = MermaidDiagnostics.Measure("editor.first-presentation." + family);
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var lastBeat = Stopwatch.GetTimestamp(); var maximumGap = 0d;
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        heartbeat.Tick += Beat; heartbeat.Start();
        Window window = family switch
        {
            "Flowchart" => new MermaidVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
            "Sequence" => new SequenceVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
            "State" => new StateVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
            "ER" => new ErVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
            _ => new ClassVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null))
        };
        window.Owner = owner; var weak = new WeakReference(window); window.Show();
        bool Ready() => window.FindName(family == "Flowchart" ? "Palette" : "ApplyButton") is UIElement { IsEnabled: true } && window.IsEnabled;
        try
        {
            PumpUntil(Ready, "The native suite lifecycle did not reach an editable presentation."); Drain();
            var firstUsefulMs = timer.Elapsed.TotalMilliseconds; firstPresentationPhase?.Dispose();
            var list = (ListBox)window.FindName(family == "Sequence" ? "Participants" : "Elements");
            var conversions = ConversionCount(); var selectedAllocated = GC.GetAllocatedBytesForCurrentThread(); timer.Restart();
            for (var selection = 0; selection < 20; selection++)
            { using var interaction = selection == 0 ? MermaidDiagnostics.Measure("editor.first-interaction." + family) : null; list.SelectedIndex = selection % Math.Min(3, list.Items.Count); }
            var selectionMs = timer.Elapsed.TotalMilliseconds; var selectionBytes = GC.GetAllocatedBytesForCurrentThread() - selectedAllocated;
            if (ConversionCount() != conversions) throw new IOException(family + ": ordinary selection repeated native SVG conversion.");
            var sourceEditor = (ICSharpCode.AvalonEdit.TextEditor)window.FindName("SourceEditor");
            if (sourceEditor.Text != source) throw new IOException(family + ": native selection changed exact source.");
            if (cycle == 1)
            {
                ApplyTheme("Dark"); ApplyTheme("Light"); ApplyTheme("Dark"); PumpUntil(Ready, "Latest theme rendering did not complete after cancellation.");
                if (sourceEditor.Text != source) throw new IOException("Theme cancellation changed private source.");
                heartbeat.Stop();
                var currentDrawing = family switch
                {
                    "Flowchart" => (DrawingImage)window.GetType().GetField("canonicalDrawing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!,
                    "Sequence" => (DrawingImage)((Image)window.FindName("CanonicalImage")).Source,
                    "State" => ((NativeStatePresentation)window.GetType().GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Canonical.Drawing,
                    "ER" => ((NativeErPresentation)window.GetType().GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Drawing,
                    _ => ((NativeClassPresentation)window.GetType().GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Drawing
                };
                var expectedRender = service.Render(source, ThemeService.IsDark("Dark"));
                var expectedDrawing = new NativeSvgService().Render(expectedRender.Svg!);
                if (!NativeSvgService.ToPng(currentDrawing, .25).SequenceEqual(NativeSvgService.ToPng(expectedDrawing, .25)))
                    throw new IOException(family + ": an obsolete theme result overrode the latest canonical presentation.");
                lastBeat = Stopwatch.GetTimestamp(); heartbeat.Start();
            }
            if (cycle == 2)
            {
                var gate = (SemaphoreSlim)typeof(NativeSvgService).GetField("canonicalConversion", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                gate.Wait();
                try
                {
                    ApplyTheme("Dark");
                    PumpUntil(() => (int)typeof(NativeSvgService).GetField("canonicalWaiting", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)! > 0, "The close-during-conversion fixture never reached the occupied conversion queue.");
                    window.Close();
                }
                finally { gate.Release(); }
            }
            else window.Close();
            if (window.IsVisible) throw new IOException(family + ": unchanged private editor refused closure.");
            var cleanup = (TaskCompletionSource)window.GetType().GetField("closedCleanup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var cleanupTimer = Stopwatch.StartNew(); PumpUntil(() => cleanup.Task.IsCompleted, "Native editor cleanup did not finish."); cleanup.Task.GetAwaiter().GetResult(); var cleanupMs = cleanupTimer.Elapsed.TotalMilliseconds;
            owner.Activate(); ownerFocus.Focus(); Drain();
            observations.Add(new { Family = family, Cycle = cycle, FirstUsefulPresentationIncludingNativeDispatcherMs = firstUsefulMs,
                TwentyNativeSelectionUpdatesMs = selectionMs, SelectionUiThreadAllocatedBytes = selectionBytes,
                MaximumInputHeartbeatGapMs = maximumGap, UiThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
                WorkingSet = Environment.WorkingSet, CleanupCompletionMs = cleanupMs, SourceExact = true, SelectionConversions = 0, RapidThemeCancellation = cycle == 1, ClosedDuringQueuedConversion = cycle == 2 });
            return weak;
        }
        finally { heartbeat.Stop(); heartbeat.Tick -= Beat; if (window.IsVisible) window.Close(); }
        void Beat(object? sender, EventArgs args) { var current = Stopwatch.GetTimestamp(); maximumGap = Math.Max(maximumGap, Stopwatch.GetElapsedTime(lastBeat, current).TotalMilliseconds); lastBeat = current; }
        void ApplyTheme(string theme)
        {
            ThemeService.Apply(Application.Current.Resources, theme);
            window.GetType().GetMethod("ApplyTheme")!.Invoke(window, [theme]);
        }
    }
    private static long ConversionCount() => (long)typeof(NativeSvgService).GetField("canonicalConversions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private static void PumpUntil(Func<bool> condition, string failure)
    {
        var frame = new DispatcherFrame(); var watch = Stopwatch.StartNew(); var timeout = false;
        var poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        poll.Tick += Check; poll.Start();
        try { Dispatcher.PushFrame(frame); } finally { poll.Stop(); poll.Tick -= Check; }
        if (timeout) throw new IOException(failure);
        void Check(object? sender, EventArgs args) { if (condition()) frame.Continue = false; else if (watch.Elapsed > TimeSpan.FromSeconds(20)) { timeout = true; frame.Continue = false; } }
    }
    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
