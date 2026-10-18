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
                windows.Add(OpenClose(family, -1, host, hostButton, service, observations));
                Drain();
                // Diagnostic collection measures retained windows. Never called by production code.
                GC.Collect(); GC.WaitForPendingFinalizers(); Drain(); GC.Collect(); Drain();
                var retained = windows.Count(item => item.IsAlive);
                observations.Add(new { Family = family, Operation = "Controlled diagnostic collection after six smaller and one larger open/close cycle", RetainedWindows = retained, RetainedIdentities = windows.Select((item, index) => item.IsAlive ? "family index " + (index / 7) + ", case " + (index % 7) : null).Where(item => item is not null).ToArray(), ManagedBytes = GC.GetTotalMemory(false), WorkingSet = Environment.WorkingSet });
                if (retained != 0) throw new IOException(family + ": a closed editor remains retained after dispatcher drain and diagnostic collection.");
            }
            windows.AddRange(ConcurrentWindows(host, hostButton, service, observations));
            Drain(); GC.Collect(); GC.WaitForPendingFinalizers(); Drain(); GC.Collect(); Drain();
            if (windows.Any(item => item.IsAlive)) throw new IOException("Concurrent editor windows remained retained after their cleanup completed.");
            File.WriteAllText(Path.Combine(output, "suite-lifecycle.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrast = highContrast,
                Dpi = new { X = VisualTreeHelper.GetDpi(host).PixelsPerInchX, Y = VisualTreeHelper.GetDpi(host).PixelsPerInchY },
                WindowCases = windows.Count,
                Method = "Six smaller and one larger real WPF window cycle per family plus five simultaneous family windows, shared renderer service, exact source, native selection updates without UIA, Input-priority heartbeat, rapid theme cancellation and close while conversion capacity is held. Controlled GC occurs only in the diagnostic harness. Retained-window assertions are separate from working-set observations. The larger Flowchart retains its existing protected/partial capabilities; a useful canonical drawing, rather than an enabled authoring palette, defines presentation readiness.", Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "suite-lifecycle-failure.json"), JsonSerializer.Serialize(new { Error = error.ToString(), Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
            // Diagnostic-only hosted tooling captures roots before this failed process
            // exits. It never changes collection boundaries or the zero-retention gate.
            var dumpTool = Environment.GetEnvironmentVariable("LENS_SUITE_DUMP_TOOL");
            if (!string.IsNullOrEmpty(dumpTool) && File.Exists(dumpTool))
            {
                var start = new ProcessStartInfo(dumpTool) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { "collect", "--process-id", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), "--type", "Heap", "--output", Path.GetFullPath(Path.Combine(output, "suite-retention.dmp")) }) start.ArgumentList.Add(argument);
                using var dump = Process.Start(start)!; var stdout = dump.StandardOutput.ReadToEndAsync(); var stderr = dump.StandardError.ReadToEndAsync();
                if (!dump.WaitForExit(60000)) dump.Kill();
                File.WriteAllText(Path.Combine(output, "suite-dump-collection.txt"), stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
            }
            throw;
        }
        finally { host.Close(); recorder.Write(Path.Combine(output, "suite-lifecycle-phases.json")); SynchronizationContext.SetSynchronizationContext(previous); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenClose(string family, int cycle, Window owner, Button ownerFocus, DiagramService service, List<object> observations)
    {
        var source = NativeSuitePerformanceChecks.Source(family, cycle < 0) + (cycle == 2 ? "%% queued lifecycle fixture\n" : "");
        ThemeService.Apply(Application.Current.Resources, "Light");
        using var firstPresentationPhase = MermaidDiagnostics.Measure("editor.first-presentation." + family);
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var lastBeat = Stopwatch.GetTimestamp(); var maximumGap = 0d;
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        heartbeat.Tick += Beat; heartbeat.Start();
        Window window = Create(family, source, service);
        window.Owner = owner; var weak = new WeakReference(window); window.Show();
        bool Ready() => IsReady(window);
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
            if (cycle == 0)
            {
                heartbeat.Stop(); CheckKeyboardRollback(window, family, list, observations); CheckPointerZoom(window, family, observations); lastBeat = Stopwatch.GetTimestamp(); heartbeat.Start();
            }
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
            observations.Add(new { Family = family, Cycle = cycle, Scenario = cycle < 0 ? "Larger" : "Smaller", FirstUsefulPresentationIncludingNativeDispatcherMs = firstUsefulMs,
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
    private static Window Create(string family, string source, DiagramService service) => family switch
    {
        "Flowchart" => new MermaidVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
        "Sequence" => new SequenceVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
        "State" => new StateVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
        "ER" => new ErVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null)),
        _ => new ClassVisualEditorWindow(source, service, static (_, _) => Task.FromResult<string?>(null))
    };
    private static void CheckKeyboardRollback(Window window, string family, ListBox list, List<object> observations)
    {
        list.SelectedIndex = 0; Drain(); var original = list.SelectedItem; var conversions = ConversionCount();
        var sourceEditor = (ICSharpCode.AvalonEdit.TextEditor)window.FindName("SourceEditor"); var source = sourceEditor.Text;
        var fieldName = family switch { "Flowchart" => "NodeLabel", "Sequence" => "ParticipantId", _ => "NodeId" };
        var field = (TextBox)window.FindName(fieldName); var before = field.Text; field.Text += "Pending";
        var pending = window.GetType().GetProperty("PropertiesPending", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (!(bool)pending.GetValue(window)!) throw new IOException("Native keyboard fixture did not create pending properties.");
        var delivered = false;
        System.Windows.Input.KeyEventHandler key = (_, args) => { if (args.Key == System.Windows.Input.Key.Down) delivered = true; };
        list.AddHandler(System.Windows.Input.Keyboard.PreviewKeyDownEvent, key, true);
        try
        {
            list.Focus(); FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
            PumpUntil(() => delivered && ReferenceEquals(list.SelectedItem, original), "Native blocked keyboard selection did not restore its exact occurrence.");
            Drain();
            if (field.Text != before + "Pending" || !(bool)pending.GetValue(window)!) throw new IOException("Native blocked keyboard selection lost pending values.");
            ((Button)window.FindName(family == "Flowchart" ? "ResetPropertiesButton" : "ResetButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Drain();
            if (field.Text != before || (bool)pending.GetValue(window)! || !ReferenceEquals(list.SelectedItem, original)) throw new IOException("Native Reset changed the selected occurrence or failed to restore values.");
            if (list.ItemContainerGenerator.ContainerFromItem(original) is not ListBoxItem entry) throw new IOException("Native selected navigation entry was not realised.");
            var bounds = entry.TransformToAncestor(list).TransformBounds(new Rect(entry.RenderSize));
            if (!entry.IsVisible || bounds.Top < -1 || bounds.Bottom > list.ActualHeight + 1) throw new IOException("Native selected navigation entry remained outside its viewport: " + bounds);
            if (ConversionCount() != conversions) throw new IOException("Native keyboard selection repeated SVG conversion.");
            if (sourceEditor.Text != source) throw new IOException("Native keyboard rollback changed private source.");
            observations.Add(new { Family = family, Operation = "Native blocked keyboard selection and Reset retain the exact visible occurrence", PendingValuesPreserved = true, SourceExact = true, SelectionConversions = 0 });
        }
        finally { list.RemoveHandler(System.Windows.Input.Keyboard.PreviewKeyDownEvent, key); }
    }
    private static bool IsReady(Window window)
    {
        if (!window.IsEnabled) return false;
        if (window is not MermaidVisualEditorWindow) return window.FindName("ApplyButton") is UIElement { IsEnabled: true };
        return window.GetType().GetField("canonicalDrawing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) is DrawingImage
            && window.GetType().GetField("renderingCanvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) is false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ConcurrentWindows(Window owner, Button ownerFocus, DiagramService service, List<object> observations)
    {
        var families = new[] { "Flowchart", "Sequence", "State", "ER", "Class" }; var opened = new List<Window>(); var originals = new List<string>();
        var timer = Stopwatch.StartNew(); ThemeService.Apply(Application.Current.Resources, "Light");
        try
        {
            foreach (var family in families)
            {
                var source = NativeSuitePerformanceChecks.Source(family, false) + "%% simultaneous editor fixture\n";
                var window = Create(family, source, service); window.Owner = owner; opened.Add(window); originals.Add(source); window.Show();
            }
            PumpUntil(() => opened.All(IsReady), "The five simultaneous editors did not all become editable.");
            var openMs = timer.Elapsed.TotalMilliseconds; opened[2].Close();
            if (opened[2].IsVisible || opened.Where((_, index) => index != 2).Any(window => !window.IsVisible)) throw new IOException("Closing one modeless editor affected another family window.");
            ThemeService.Apply(Application.Current.Resources, "Dark");
            foreach (var window in opened.Where(window => window.IsVisible)) window.GetType().GetMethod("ApplyTheme")!.Invoke(window, ["Dark"]);
            PumpUntil(() => opened.Where(window => window.IsVisible).All(IsReady), "Concurrent theme rendering did not finish.");
            for (var index = 0; index < opened.Count; index++)
            {
                var editor = (ICSharpCode.AvalonEdit.TextEditor)opened[index].FindName("SourceEditor");
                if (editor.Text != originals[index]) throw new IOException("Concurrent rendering changed a different editor's source.");
            }
            var weak = opened.Select(window => new WeakReference(window)).ToArray();
            foreach (var window in opened.Where(window => window.IsVisible)) window.Close();
            var cleanup = opened.Select(window => ((TaskCompletionSource)window.GetType().GetField("closedCleanup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Task).ToArray();
            PumpUntil(() => cleanup.All(task => task.IsCompleted), "Concurrent native cleanup did not finish.");
            foreach (var task in cleanup) task.GetAwaiter().GetResult();
            owner.Activate(); ownerFocus.Focus(); Drain();
            observations.Add(new { Operation = "Five modeless family windows open together, independent close, concurrent theme change and completed cleanup", OpenMs = openMs,
                SourceExact = true, IndependentWindows = true, WorkingSet = Environment.WorkingSet }); return weak;
        }
        finally { foreach (var window in opened.Where(window => window.IsVisible)) window.Close(); }
    }
    private static long ConversionCount() => (long)typeof(NativeSvgService).GetField("canonicalConversions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private static void CheckPointerZoom(Window window, string family, List<object> observations)
    {
        var viewport = (ScrollViewer)window.FindName("Viewport"); var zoom = (Slider)window.FindName("Zoom");
        var content = (FrameworkElement)viewport.Content; var originalZoom = zoom.Value;
        var originalHorizontal = viewport.HorizontalOffset; var originalVertical = viewport.VerticalOffset;
        try
        {
            zoom.Value = 200; Drain(); viewport.ScrollToHorizontalOffset((viewport.ExtentWidth - viewport.ViewportWidth) / 2);
            viewport.ScrollToVerticalOffset((viewport.ExtentHeight - viewport.ViewportHeight) / 2); Drain();
            var horizontal = viewport.ScrollableWidth > 20; var vertical = viewport.ScrollableHeight > 20;
            if (!horizontal && !vertical) throw new IOException("The pointer zoom fixture has no scrollable axis.");
            var pointer = new Point(viewport.ViewportWidth * .4, viewport.ViewportHeight * .4);
            var anchor = viewport.TranslatePoint(pointer, content); var screen = viewport.PointToScreen(pointer); var conversions = ConversionCount();
            window.Activate(); viewport.Focus(); FlaUI.Core.Input.Mouse.MoveTo((int)screen.X, (int)screen.Y);
            using (FlaUI.Core.Input.Keyboard.Pressing(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL))
            {
                FlaUI.Core.Input.Mouse.Scroll(1);
                PumpUntil(() => zoom.Value > 200, "Native Ctrl+wheel did not reach the editor zoom handler.");
            }
            Drain(); var after = content.TranslatePoint(anchor, viewport);
            if (horizontal && Math.Abs(after.X - pointer.X) > 3 || vertical && Math.Abs(after.Y - pointer.Y) > 3)
                throw new IOException(family + ": Ctrl+wheel moved the pointer's canonical anchor: " + (after - pointer));
            if (ConversionCount() != conversions) throw new IOException("Pointer zoom repeated native SVG conversion.");
            observations.Add(new { Family = family, Operation = "Actual Ctrl+wheel preserves its canonical pointer anchor without SVG conversion",
                HorizontalChecked = horizontal, VerticalChecked = vertical, HorizontalErrorDip = after.X - pointer.X, VerticalErrorDip = after.Y - pointer.Y });
        }
        finally { zoom.Value = originalZoom; viewport.ScrollToHorizontalOffset(originalHorizontal); viewport.ScrollToVerticalOffset(originalVertical); Drain(); }
    }
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
