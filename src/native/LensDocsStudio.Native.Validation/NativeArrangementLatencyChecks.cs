using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

/// <summary>Real pointer input on a disposable hosted desktop; never enabled by a product preference.</summary>
internal static class NativeArrangementLatencyChecks
{
    private const int SamplesPerDrag = 120;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void Run(string output, string root)
    {
        RequireHostedDesktop();
        if (Directory.Exists(output)) throw new IOException("Use a fresh pointer acceptance directory; previous observations must be retained.");
        Directory.CreateDirectory(output); Initialise();
        var cases = new List<object>(); var passed = false;
        try
        {
            foreach (var (name, source) in Workloads(root))
            {
                var folder = System.IO.Path.Combine(output, name); Directory.CreateDirectory(folder);
                var sourcePath = System.IO.Path.Combine(folder, "diagram.mmd"); File.WriteAllText(sourcePath, source);
                var sourceBytes = File.ReadAllBytes(sourcePath);
                using var observation = new Observation(source, folder);
                try
                {
                    observation.Window.Show(); Until(() => observation.Ready); observation.Dpi();
                    var task = Task.Run(() => Exercise(observation)); Await(task, TimeSpan.FromMinutes(4));
                    if (!File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes)) throw new IOException("Actual pointer acceptance changed source bytes.");
                    cases.Add(new { Case = name, Status = "PASS", Measurements = task.Result, Phases = observation.Phases.ToArray(), Dpi = observation.DpiReceipt });
                }
                catch (Exception error)
                {
                    cases.Add(new { Case = name, Status = "FAIL", ErrorType = error.GetType().Name, Error = error.Message,
                        PartialSteps = observation.Steps.ToArray(), PartialFrames = observation.Frames.ToArray(), Phases = observation.Phases.ToArray(), Dpi = observation.DpiReceipt });
                    throw;
                }
                finally { File.WriteAllText(System.IO.Path.Combine(folder, "pointer.json"), JsonSerializer.Serialize(cases[^1], Json)); }
            }
            passed = true;
        }
        finally
        {
            File.WriteAllText(System.IO.Path.Combine(output, "pointer-acceptance.json"), JsonSerializer.Serialize(new
            {
                Status = passed ? "PASS" : "FAIL", SamplesRequestedPerWorkload = SamplesPerDrag,
                Method = "Actual SendInput events target a shown production WPF arrangement window. Input-to-WPF timestamps require a matching new actual pointer-arrival event before a CompositionTarget.Rendering opportunity. Separately labelled pixel observations follow DwmFlush and one bounded screen BitBlt capture per probe; they are conservative observed upper bounds including capture overhead, not photon/scanout timestamps. Each held-button drag has 120 frame-observation-paced samples and a separate 200-event producer-paced stream. No synthetic handler calls substitute for pointer input. Per-move allocations include opt-in diagnostic instrumentation.",
                ObservationBudget = "Each complete workload has a four-minute harness budget including screen capture, 120 acknowledged probes, 200 continuous inputs and drop/candidate/cancellation checks. Individual feedback waits remain 25 seconds. These are harness completion bounds, not product latency acceptance thresholds.",
                PhysicalDpiBoundary = "Only GetDpiForWindow/VisualTreeHelper DPI reported by the current desktop is physical coverage. No registry/display setting is changed. 125/150/200 percent and monitor transitions are not inferred from WPF scaling.",
                NumericalAcceptance = "No new latency threshold is imposed. Raw samples, counts and percentiles are observations; bounded harness waits detect missing feedback rather than redefine the product performance gate.",
                Cases = cases
            }, Json));
        }
    }

    // This mode owns a visible window but injects no desktop input. An approved external
    // Computer Use session can drive it, including with confidential local source copies.
    public static void Observe(string sourcePath, string output)
    {
        if (Directory.Exists(output)) throw new IOException("Use a fresh observer directory.");
        Directory.CreateDirectory(output); Initialise();
        var source = File.ReadAllText(sourcePath); var root = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(sourcePath))!;
        var originalBytes = File.ReadAllBytes(sourcePath);
        using var observation = new Observation(source, root, System.IO.Path.GetFileName(sourcePath));
        observation.Window.Title = "Arrangement acceptance observer";
        observation.Window.Show(); Until(() => observation.Ready); observation.Dpi();
        Console.WriteLine("Owned arrangement observer ready. No automatic pointer input is enabled.");
        var done = false; observation.Window.Closed += (_, _) => done = true;
        try { Until(() => done, TimeSpan.FromMinutes(15)); }
        finally { File.WriteAllText(System.IO.Path.Combine(output, "observer.json"), JsonSerializer.Serialize(new
        {
            Status = done ? "OBSERVED" : "INCOMPLETE", InputInjection = false,
            Scope = "Input-event arrival to WPF presentation opportunities in an owned window. External injection time and physical scanout latency are unavailable. No source, identifiers, source hashes or source paths are recorded.",
            Dpi = observation.DpiReceipt, Phases = observation.Phases.ToArray(), Frames = observation.Frames.ToArray(),
            SourcePreserved = File.ReadAllBytes(sourcePath).SequenceEqual(originalBytes)
        }, Json)); }
    }

    private static object Exercise(Observation observation)
    {
        var window = observation.Window; var handle = observation.Handle;
        if (!SetForegroundWindow(handle)) Thread.Sleep(100);
        if (GetForegroundWindow() != handle) throw new IOException("The owned arrangement window did not acquire the foreground.");
        var idleQueue = new ConcurrentQueue<double>();
        using (var idlePulse = new Timer(_ => { var sent = Now(); window.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => idleQueue.Enqueue(Now() - sent)); }, null, 0, 16))
            Thread.Sleep(2000);
        observation.Steps.Enqueue(new { Stage = "idle-dispatcher-baseline", Samples = Statistics(idleQueue) });
        var layout = Ui(() =>
        {
            observation.Arrange.IsChecked = true; observation.Zoom.Value = 100; window.UpdateLayout();
            var inspection = observation.Inspection;
            var node = inspection.Entities.OrderBy(n => n.Shape.Bounds.Width * n.Shape.Bounds.Height).First();
            var bounds = node.Shape.Bounds;
            observation.Viewport.ScrollToHorizontalOffset(Math.Max(0, bounds.X - 80));
            observation.Viewport.ScrollToVerticalOffset(Math.Max(0, bounds.Y - 70)); window.UpdateLayout();
            return (node.Identity, Bounds: bounds, Centre: observation.Drawing.PointToScreen(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)),
                Corner: observation.GhostCorner(bounds), Dpi: VisualTreeHelper.GetDpi(window));
        });
        var centre = Round(layout.Centre); var original = Ui(() => observation.Session.Current.State);
        var trajectory = Ui(() =>
        {
            var bounds = new Rect(observation.Viewport.PointToScreen(new(0, 0)), observation.Viewport.PointToScreen(new(observation.Viewport.ActualWidth, observation.Viewport.ActualHeight)));
            bounds.Intersect(new Rect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79)));
            var x = centre.X + 192 < bounds.Right - 3 ? 1 : centre.X - 192 > bounds.Left + 3 ? -1 : 0;
            var y = centre.Y + 60 < bounds.Bottom - 3 ? 1 : centre.Y - 60 > bounds.Top + 3 ? -1 : 0;
            if (x == 0 || y == 0 || !bounds.Contains(new Point(centre.X, centre.Y))) throw new IOException("The owned viewport cannot contain the bounded pointer sampling trajectory.");
            return (X: x, Y: y);
        });
        var id = layout.Identity[(layout.Identity.IndexOf(':') + 1)..];
        var planningEngine = Ui(() => (ArrangementEngine)Field(window, "engine")!);
        // Find a feasible target before any button is held. This is not timed as input or drop work.
        var planned = PlanSafeDrop(planningEngine, id, original, layout.Dpi);
        var moveStart = Now(); Move(centre); Thread.Sleep(60);
        var down = Now(); Button(true);
        try
        {
            WaitWorker(() => Ui(() => Field(window, "pointer") is Point), "Real mouse down was not accepted.");
            var deliveredDown = Ui(() => observation.LastButtonDown) ?? throw new IOException("No actual mouse-down coordinates were observed.");
            var firstResponse = observation.NextFrame(() => Ui(() => Field(window, "selected") as string) == layout.Identity, down);
            observation.Steps.Enqueue(new { Stage = "mouse-down", InputToWpfFrameMilliseconds = firstResponse, RequestedPhysicalPoint = centre, Delivered = deliveredDown });
            // Explicitly exercise sub-threshold movement, then the actual threshold transition.
            Move(new(centre.X + 1, centre.Y)); Thread.Sleep(30);
            if (Ui(() => observation.Ghost.Visibility == Visibility.Visible)) throw new IOException("A one-physical-pixel move unexpectedly activated the drag ghost.");
            var frameSamples = new List<PointerSample>(); var frameTimes = new List<double>();
            var cpu = Process.GetCurrentProcess().TotalProcessorTime; var allocated = GC.GetTotalAllocatedBytes(true);
            for (var i = 0; i < SamplesPerDrag; i++)
            {
                // Spacing exceeds twice the admitted one-pixel input rounding radius.
                var target = new PixelPoint(centre.X + trajectory.X * (35 + (i % 24) * 4), centre.Y + trajectory.Y * (15 + ((i / 24) % 2) * 25));
                var expected = new Point(layout.Corner.X + target.X - centre.X, layout.Corner.Y + target.Y - centre.Y);
                var probes = Ui(() => observation.Probes(expected));
                var beforeStarted = Now(); var before = ReadPixels(probes.Points); var beforeCapture = Now() - beforeStarted; var started = Now();
                var request = observation.Request(expected, target, started); Move(target);
                var frame = request.GetAwaiter().GetResult();
                var visible = ObservePixels(probes, before, started);
                frameSamples.Add(new(i, frame.InputToFrameMilliseconds, visible.InputToObservedPixelsMilliseconds, frame.GhostMatchesExpected,
                    beforeCapture, visible.ScreenCaptureMilliseconds, visible.CompositorFlushMilliseconds, visible.CaptureCount,
                    frame.InjectedMilliseconds, frame.InputArrivalMilliseconds, frame.FrameTimestampMilliseconds, frame.InputSequence,
                    frame.Target, frame.ObservedPointer));
                observation.Steps.Enqueue(frameSamples[^1]);
                frameTimes.Add(frame.FrameTimestampMilliseconds);
            }
            // A second stream is paced by the producer, not by presentation acknowledgement.
            // WPF may coalesce samples; report the number actually observed and presented.
            var burstStart = Now(); var finalInjected = 0d;
            for (var i = 0; i < 200; i++)
            {
                var target = new PixelPoint(centre.X + trajectory.X * (32 + (i % 40) * 4), centre.Y + trajectory.Y * (12 + (i / 40) * 5));
                var injected = Now(); observation.InjectedMoves[target] = injected; finalInjected = injected; Move(target); Thread.Sleep(16);
            }
            StreamFrame[] burst = []; StreamArrival[] arrivals = []; var finalAcknowledged = false; var observationEnd = 0d;
            try
            {
                WaitWorker(() => observation.StreamArrivals.Any(f => f.InjectedMilliseconds == finalInjected)
                    && observation.StreamFrames.Any(f => f.InjectedMilliseconds == finalInjected), "The final producer-paced input did not reach both actual arrival and WPF presentation within the bounded observation wait.");
                finalAcknowledged = true;
            }
            finally
            {
                observationEnd = Now();
                burst = observation.StreamFrames.Where(f => f.InjectedMilliseconds >= burstStart).ToArray();
                arrivals = observation.StreamArrivals.Where(f => f.InjectedMilliseconds >= burstStart).ToArray();
                observation.InjectedMoves.Clear();
                observation.Steps.Enqueue(new { Stage = "producer-paced-stream", InjectedSamples = 200, ObservedInputEvents = arrivals.Length,
                    DistinctArrivedInjections = arrivals.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    DistinctPresentedInjections = burst.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    UnobservedInjectedSamples = 200 - arrivals.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    UnpresentedInjectedSamples = 200 - burst.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    FinalInjectionMilliseconds = finalInjected, ObservationEndMilliseconds = observationEnd,
                    FinalInjectionAcknowledged = finalAcknowledged, TailRightCensored = !finalAcknowledged,
                    PresentedSamples = burst.Length, InputToWpfFrame = Statistics(burst.Select(f => f.FrameMilliseconds - f.InjectedMilliseconds)), Samples = burst });
            }
            var dragCpu = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds;
            var dragAllocated = GC.GetTotalAllocatedBytes(true) - allocated;
            var phasesWhileDragging = observation.Phases.Where(p => p.StartedMilliseconds >= down).ToArray();
            var expensivePhases = phasesWhileDragging.Where(p => p.Name.StartsWith("renderer.", StringComparison.Ordinal) || p.Name.StartsWith("svg.", StringComparison.Ordinal)
                || p.Name is "arrangement.routing" or "arrangement.native-preparation" or "arrangement.svg-generation" or "arrangement.geometry-validation").ToArray();
            var expensivePhaseCounts = expensivePhases.GroupBy(p => p.Name).ToDictionary(group => group.Key, group => group.Count());
            observation.Steps.Enqueue(new { Stage = "drag-expensive-work", ExpensivePhaseCount = expensivePhases.Length, CountsByPhase = expensivePhaseCounts });
            if (expensivePhases.Length != 0) throw new IOException("Continuous ghost dragging invoked expensive rendering, geometry validation or conversion.");
            var requestedDrop = new PixelPoint(centre.X + planned.X, centre.Y + planned.Y);
            Move(requestedDrop); Thread.Sleep(40);
            var dropStarted = Now(); Button(false);
            WaitWorker(() => Ui(() => observation.Ready && !observation.Session.Current.State.SameAs(original)), "The real drop did not produce an accepted arrangement.");
            var deliveredUp = Ui(() => observation.LastButtonUp) ?? throw new IOException("No actual mouse-up coordinates were observed.");
            if (deliveredUp.TimestampMilliseconds < dropStarted) throw new IOException("The drop receipt refers to an earlier mouse-up event.");
            var deliveredDelta = new PixelPoint(deliveredUp.Point.X - deliveredDown.Point.X, deliveredUp.Point.Y - deliveredDown.Point.Y);
            var expectedPosition = new Point(Math.Max(32, original.Positions[id].X + deliveredDelta.X / layout.Dpi.DpiScaleX),
                Math.Max(32, original.Positions[id].Y + deliveredDelta.Y / layout.Dpi.DpiScaleY));
            var dropFrame = observation.NextFrame(() => Ui(() => observation.Ready), dropStarted);
            var dropVisible = FlushMilliseconds(dropStarted);
            observation.Steps.Enqueue(new { Stage = "drop", ReleaseStartedMilliseconds = dropStarted,
                RequestedDown = centre, RequestedUp = requestedDrop, RequestedDelta = planned,
                DeliveredDown = deliveredDown, DeliveredUp = deliveredUp, DeliveredDelta = deliveredDelta, ExpectedLayoutPosition = expectedPosition,
                ReleaseToWpfFrameMilliseconds = dropFrame, ReleaseToCompositorFlushMilliseconds = dropVisible });
            var after = Ui(() => observation.Session.Current.State);
            var changed = after.Positions[id];
            if (Math.Abs(changed.X - expectedPosition.X) > .01 || Math.Abs(changed.Y - expectedPosition.Y) > .01)
                throw new IOException("Actual physical pointer movement was not converted to the expected layout units.");
            var cancellation = CancellationChecks(observation, id);
            var automatic = AutomaticChecks(observation);
            var closure = CloseDuringDrag(observation);
            var pointerPhases = phasesWhileDragging.Where(p => p.Name == "arrangement.pointer.move").ToArray();
            return new
            {
                ActualOsDpi = observation.DpiReceipt, RequestedSamples = SamplesPerDrag,
                IdleDispatcherInputQueueDelay = Statistics(idleQueue),
                DownToFirstWpfResponseMilliseconds = firstResponse,
                Threshold = new { BelowThresholdPhysicalPixels = 1, NoGhost = true, FirstActivatedFrameMilliseconds = frameSamples[0].InputToWpfFrameMilliseconds },
                InputToWpfFrame = Statistics(frameSamples.Select(s => s.InputToWpfFrameMilliseconds)),
                InputToObservedPixels = Statistics(frameSamples.Where(s => s.InputToObservedPixelsMilliseconds.HasValue).Select(s => s.InputToObservedPixelsMilliseconds!.Value)),
                PixelObservedSamples = frameSamples.Count(s => s.InputToObservedPixelsMilliseconds.HasValue),
                AcknowledgementPacedPresentationIntervals = Statistics(frameTimes.Zip(frameTimes.Skip(1), (a, b) => b - a)),
                IntervalBoundary = "Acknowledgement-paced intervals include input production and pixel observation; they are not individual WPF frame execution duration.",
                WpfFrameExecutionDuration = "NOT MEASURED: CompositionTarget.Rendering exposes a presentation opportunity, not complete render-thread execution or scanout.",
                BeforeInputScreenCaptureMilliseconds = Statistics(frameSamples.Select(s => s.BeforeInputScreenCaptureMilliseconds)),
                AfterInputScreenCaptureMilliseconds = Statistics(frameSamples.Select(s => s.AfterInputScreenCaptureMilliseconds)),
                PixelProbeCompositorFlushMilliseconds = Statistics(frameSamples.Select(s => s.PixelProbeCompositorFlushMilliseconds)),
                PointerHandler = Statistics(pointerPhases.Select(p => p.Milliseconds)),
                PointerHandlerAllocatedBytes = Statistics(pointerPhases.Where(p => p.AllocatedBytes.HasValue).Select(p => (double)p.AllocatedBytes!.Value)),
                ProducerPacedStream = new { InjectedSamples = 200, RequestedIntervalMilliseconds = 16, ObservedInputEvents = arrivals.Length, PresentedSamples = burst.Length,
                    DistinctArrivedInjections = arrivals.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    DistinctPresentedInjections = burst.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    UnobservedInjectedSamples = 200 - arrivals.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    UnpresentedInjectedSamples = 200 - burst.Select(f => f.InjectedMilliseconds).Distinct().Count(),
                    FinalInjectionMilliseconds = finalInjected, ObservationEndMilliseconds = observationEnd,
                    FinalInjectionAcknowledged = finalAcknowledged, TailRightCensored = !finalAcknowledged,
                    CoalescingBoundary = "Input and WPF presentation may coalesce moves. Observation waits for the final injected sample's arrival and frame with the existing bounded wait; missing intermediate samples are not classified as zero latency or proven coalescing.",
                    InputToArrival = Statistics(arrivals.Select(f => f.ArrivalMilliseconds - f.InjectedMilliseconds)),
                    InputToWpfFrame = Statistics(burst.Select(f => f.FrameMilliseconds - f.InjectedMilliseconds)), Samples = burst },
                DragCpuMilliseconds = dragCpu, DragCumulativeAllocatedBytes = dragAllocated, ProcessWorkingSetBytes = Environment.WorkingSet,
                PerMoveExpensiveRenderingCalls = expensivePhases.Length, ExpensivePhaseCounts = expensivePhaseCounts, Samples = frameSamples,
                Drop = new { ReleaseStartedMilliseconds = dropStarted,
                    RequestedDown = centre, RequestedUp = requestedDrop, RequestedDelta = planned,
                    DeliveredDown = deliveredDown, DeliveredUp = deliveredUp, DeliveredDelta = deliveredDelta,
                    ExpectedLayoutPosition = expectedPosition, AcceptedLayoutPosition = changed,
                    ReleaseToRoutingStartMilliseconds = observation.Phases.Where(p => p.Name == "arrangement.routing" && p.StartedMilliseconds >= dropStarted).Select(p => (double?)(p.StartedMilliseconds - dropStarted)).FirstOrDefault(),
                    ReleaseToWpfFrameMilliseconds = dropFrame, ReleaseToCompositorFlushMilliseconds = dropVisible,
                    VisibilityBoundary = "Accepted state reached a WPF presentation opportunity and compositor flush; final diagram pixels were not independently verified by this timing probe.",
                    Phases = observation.Phases.Where(p => p.StartedMilliseconds >= dropStarted && p.StartedMilliseconds <= dropStarted + dropVisible).ToArray(), SourcePreserved = true },
                Cancellation = cancellation, Automatic = automatic, Closure = closure
            };
        }
        finally { Button(false); }
        T Ui<T>(Func<T> action) => window.Dispatcher.Invoke(action);
    }

    private static PixelPoint PlanSafeDrop(ArrangementEngine engine, string id, ArrangementState original, DpiScale dpi)
    {
        foreach (var delta in new[] { new PixelPoint(12, 8), new(0, 12), new(12, 0), new(40, 15), new(-12, 8) })
        {
            var p = original.Positions[id];
            try { _ = engine.Arrange(original.Move(id, Math.Max(32, p.X + delta.X / dpi.DpiScaleX), Math.Max(32, p.Y + delta.Y / dpi.DpiScaleY))); return delta; }
            catch (InvalidDataException) { }
        }
        throw new IOException("No safe small drop was found for this pointer workload; the previous layout remains accepted.");
    }

    private static object CancellationChecks(Observation observation, string id)
    {
        var window = observation.Window; var before = Ui(() => observation.Session.Current.State);
        Ui(() => { observation.Zoom.Value = 200; window.UpdateLayout(); return true; });
        PixelPoint Start()
        {
            return Ui(() =>
            {
                var bounds = observation.Inspection.Entities.Single(n => n.Identity.EndsWith(":" + id, StringComparison.Ordinal)).Shape.Bounds;
                observation.Viewport.ScrollToHorizontalOffset(Math.Max(0, bounds.X * 2 - 60));
                observation.Viewport.ScrollToVerticalOffset(Math.Max(0, bounds.Y * 2 - 50)); window.UpdateLayout();
                return Round(observation.Drawing.PointToScreen(new(bounds.X + Math.Min(30, bounds.Width / 2), bounds.Y + Math.Min(20, bounds.Height / 2))));
            });
        }
        var point = Start(); Move(point); Button(true); Move(new(point.X + 30, point.Y + 20));
        WaitWorker(() => Ui(() => observation.Ghost.Visibility == Visibility.Visible), "The cancellation ghost did not appear at 200% zoom.");
        var escape = Now(); Key(0x1B); Button(false);
        WaitWorker(() => Ui(() => observation.Ghost.Visibility != Visibility.Visible && !observation.Viewport.IsMouseCaptured), "Escape did not cancel captured dragging.");
        var escapeMs = Now() - escape;
        if (!Ui(() => observation.Session.Current.State.SameAs(before))) throw new IOException("Escape changed the accepted layout.");
        point = Start(); Move(point); Button(true); Move(new(point.X + 30, point.Y + 20));
        WaitWorker(() => Ui(() => observation.Ghost.Visibility == Visibility.Visible), "Capture-loss ghost did not appear.");
        var capture = Now(); Ui(() => { observation.Viewport.ReleaseMouseCapture(); return true; }); Button(false);
        WaitWorker(() => Ui(() => observation.Ghost.Visibility != Visibility.Visible), "Capture loss did not cancel the drag.");
        var captureMs = Now() - capture;
        if (!Ui(() => observation.Session.Current.State.SameAs(before))) throw new IOException("Capture loss changed the accepted layout.");
        point = Start(); Move(point); Button(true); Move(new(point.X + 30, point.Y + 20));
        WaitWorker(() => Ui(() => observation.Ghost.Visibility == Visibility.Visible), "Deactivation ghost did not appear.");
        var deactivation = Now();
        Ui(() => { var other = new Window { Title = "Owned cancellation sentinel", Width = 220, Height = 100 }; other.Show(); other.Activate(); other.Close(); window.Activate(); return true; }); Button(false);
        WaitWorker(() => Ui(() => observation.Ghost.Visibility != Visibility.Visible), "Deactivation did not cancel the drag.");
        if (!Ui(() => observation.Session.Current.State.SameAs(before))) throw new IOException("Deactivation changed the accepted layout.");
        return new { ZoomPercentage = 200, EscapeMilliseconds = escapeMs, CaptureLossMilliseconds = captureMs, DeactivationMilliseconds = Now() - deactivation, AcceptedLayoutPreserved = true };
        T Ui<T>(Func<T> action) => window.Dispatcher.Invoke(action);
    }

    private static object AutomaticChecks(Observation observation)
    {
        var window = observation.Window; var before = Ui(() => observation.Session.Current.State);
        var probes = new ConcurrentQueue<double>();
        using var pulse = new Timer(_ => { var start = Now(); window.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => probes.Enqueue(Now() - start)); }, null, 0, 16);
        var cancelledSearchStart = Now(); Click("ArrangementCandidates");
        WaitWorker(() => Ui(() => Field(window, "candidateSearch") is not null) || Completed(cancelledSearchStart), "Candidate generation did not start.");
        var cancellationRequested = Ui(() => Field(window, "candidateSearch") is not null);
        var cancelStart = Now(); if (cancellationRequested) Key(0x1B);
        WaitWorker(() => Ui(() => observation.Ready), "Candidate cancellation did not finish.");
        var cancellation = Now() - cancelStart;
        var cancellationStatus = Ui(() => ((TextBlock)window.FindName("Status")).Text);
        var cancellationChoices = Ui(() => ((ComboBox)window.FindName("CandidateChoices")).Items.Count);
        var cancellationOutcome = Classify(cancellationStatus, cancellationChoices);
        observation.Steps.Enqueue(new { Stage = "automatic-cancellation", Outcome = cancellationOutcome, RawUiStatus = cancellationStatus,
            CancellationRequested = cancellationRequested, CandidateChoicesCount = cancellationChoices, Milliseconds = cancellation });
        if (cancellationOutcome == "Failure or unclassified UI outcome") throw new IOException("The automatic cancellation attempt reported a failure or unclassified UI outcome: " + cancellationStatus);
        if (!Ui(() => observation.Session.Current.State.SameAs(before))) throw new IOException("Cancelled automatic search changed the accepted layout.");
        var searchStart = Now(); Click("ArrangementCandidates");
        WaitWorker(() => Completed(searchStart) && Ui(() => observation.Ready), "Candidate generation did not finish.");
        var searchMs = Now() - searchStart;
        var count = Ui(() => ((ComboBox)window.FindName("CandidateChoices")).Items.Count);
        var searchStatus = Ui(() => ((TextBlock)window.FindName("Status")).Text);
        var outcome = Classify(searchStatus, count);
        var freshCandidates = outcome == "Feasible alternatives";
        if (!Ui(() => observation.Session.Current.State.SameAs(before))) throw new IOException("Automatic search changed the accepted layout before explicit acceptance.");
        observation.Steps.Enqueue(new { Stage = "automatic-search", Outcome = outcome, RawUiStatus = searchStatus,
            CandidateChoicesCount = count, FreshCandidateCount = freshCandidates ? count : 0, SearchWallMilliseconds = searchMs, AcceptedLayoutPreserved = true });
        if (outcome == "Failure or unclassified UI outcome") throw new IOException("Automatic candidate search reported a failure or unclassified UI outcome: " + searchStatus);
        double? acceptMs = null;
        if (freshCandidates)
        {
            Click("ArrangementCandidateChoices"); Key(0x28); Key(0x0D);
            WaitWorker(() => Ui(() => observation.Ready && Field(window, "proposed") is ArrangementSnapshot), "Actual candidate selection did not present a candidate.");
            var acceptStart = Now(); Click("ArrangementAccept");
            WaitWorker(() => Ui(() => observation.Ready && Field(window, "proposed") is null), "Candidate acceptance did not finish.");
            _ = observation.NextFrame(() => Ui(() => observation.Ready), acceptStart); acceptMs = FlushMilliseconds(acceptStart);
        }
        return new { CandidateCount = freshCandidates ? count : 0, CandidateChoicesCount = count, Outcome = outcome, RawUiStatus = searchStatus, SearchWallMilliseconds = searchMs,
            SearchOutcomeBoundary = "The UI reports user cancellation and resource deadline through the same status. The receipt preserves that ambiguity; zero choices alone never establish a completed no-improvement search. Choices retained from earlier searches are not counted as fresh alternatives.",
            SearchPreservedAcceptedLayout = true,
            CancellationRequested = cancellationRequested, CancellationOutcome = cancellationOutcome, CancellationRawUiStatus = cancellationStatus,
            CancellationMilliseconds = cancellationRequested ? (double?)cancellation : null, AcceptanceToCompositorFlushMilliseconds = acceptMs,
            AcceptanceVisibilityBoundary = "Accepted state reached a WPF presentation opportunity and compositor flush; final diagram pixels were not independently verified by this timing probe.",
            DispatcherInputQueueDelay = Statistics(probes), CancellationPreservedAcceptedLayout = true };
        bool Completed(double start) => observation.Phases.Any(p => p.Name == "arrangement.candidates" && p.StartedMilliseconds >= start);
        static string Classify(string status, int choices) => status.StartsWith("No safe improvement was found within the bounded search.", StringComparison.Ordinal)
            ? "Completed search without a safe objective improvement"
            : status.StartsWith("Arrangement work was cancelled or reached its bounded deadline.", StringComparison.Ordinal)
                ? "Cancelled or resource deadline reached"
                : choices > 0 && status.StartsWith("Current:", StringComparison.Ordinal)
                    ? "Feasible alternatives"
                    : "Failure or unclassified UI outcome";
        T Ui<T>(Func<T> action) => window.Dispatcher.Invoke(action);
        void Click(string automationId)
        {
            var point = Ui(() =>
            {
                var control = Descendants(window).OfType<FrameworkElement>().Single(e => System.Windows.Automation.AutomationProperties.GetAutomationId(e) == automationId);
                return Round(control.PointToScreen(new(control.ActualWidth / 2, control.ActualHeight / 2)));
            });
            Move(point); Button(true); Button(false);
        }
    }

    private static object CloseDuringDrag(Observation observation)
    {
        var window = observation.Window;
        var save = Ui(() =>
        {
            var control = Descendants(window).OfType<FrameworkElement>().Single(e => System.Windows.Automation.AutomationProperties.GetAutomationId(e) == "ArrangementSave");
            return Round(control.PointToScreen(new(control.ActualWidth / 2, control.ActualHeight / 2)));
        });
        Move(save); Button(true); Button(false);
        WaitWorker(() => Ui(() => observation.Ready && !observation.Session.IsDirty && ((TextBlock)window.FindName("Status")).Text.StartsWith("Layout saved", StringComparison.Ordinal)), "The clean close fixture was not saved.");
        var point = Ui(() =>
        {
            var bounds = observation.Inspection.Entities.OrderBy(n => n.Shape.Bounds.Width * n.Shape.Bounds.Height).First().Shape.Bounds;
            observation.Zoom.Value = 200; window.UpdateLayout();
            observation.Viewport.ScrollToHorizontalOffset(Math.Max(0, bounds.X * 2 - 60));
            observation.Viewport.ScrollToVerticalOffset(Math.Max(0, bounds.Y * 2 - 50)); window.UpdateLayout();
            return Round(observation.Drawing.PointToScreen(new(bounds.X + Math.Min(25, bounds.Width / 2), bounds.Y + Math.Min(15, bounds.Height / 2))));
        });
        Move(point); Button(true); Move(new(point.X + 35, point.Y + 25));
        WaitWorker(() => Ui(() => observation.Ghost.Visibility == Visibility.Visible), "The closure drag ghost did not appear.");
        var started = Now();
        Send(new() { Type = 1, Union = new() { Keyboard = new() { Key = 0x12 } } }); Key(0x73);
        Send(new() { Type = 1, Union = new() { Keyboard = new() { Key = 0x12, Flags = 2 } } }); Button(false);
        WaitWorker(() => Ui(() => (bool)Field(window, "closed")!), "Actual Alt+F4 did not close the clean arrangement during drag.");
        if (Ui(() => observation.Viewport.IsMouseCaptured || observation.Ghost.Visibility == Visibility.Visible)) throw new IOException("Closure retained the drag ghost or mouse capture.");
        return new { ActualKeyboardClose = true, ZoomPercentage = 200, Milliseconds = Now() - started, CaptureReleased = true, PendingDragDiscarded = true, PreviouslyAcceptedLayoutSaved = true };
        T Ui<T>(Func<T> action) => window.Dispatcher.Invoke(action);
    }

    internal static IEnumerable<(string Name, string Source)> Workloads(string root)
    {
        yield return ("small-er", "erDiagram\n A ||--o{ B : owns\n");
        yield return ("small-flowchart", "flowchart TD\n A[Start] --> B[Finish]\n");
        yield return ("dense-er", File.ReadAllText(System.IO.Path.Combine(root, "tests/fixtures/native-complex-er/complete-detailed.mmd")));
        var source = new StringBuilder("flowchart TD\n"); var random = new Random(2);
        for (var i = 0; i < 14; i++) source.Append($" N{i}[Node {i}]\n");
        for (var i = 0; i < 36; i++) source.Append($" N{random.Next(14)} --> N{random.Next(14)}\n");
        yield return ("dense-flowchart", source.ToString());
    }

    internal sealed class Observation : IDisposable
    {
        public DiagramArrangementWindow Window { get; }
        public readonly ConcurrentQueue<Phase> Phases = new();
        public readonly ConcurrentQueue<object> Frames = new();
        public readonly ConcurrentQueue<object> Steps = new();
        public readonly ConcurrentDictionary<PixelPoint, double> InjectedMoves = new();
        public readonly ConcurrentQueue<StreamFrame> StreamFrames = new();
        public readonly ConcurrentQueue<StreamArrival> StreamArrivals = new();
        private readonly ActivityListener listener;
        private readonly ConcurrentQueue<TaskCompletionSource<double>> frameWaiters = new();
        private PendingFrame? pending;
        private bool disposed;
        private int observedFrameCount;
        private double lastInput;
        private string lastInputKind = "None";
        private long inputSequence;
        private long lastRecordedInputSequence;
        private PixelPoint lastMoveScreen;
        private double lastInjectedMove;
        private double lastPresentedInjection;
        public ButtonReceipt? LastButtonDown { get; private set; }
        public ButtonReceipt? LastButtonUp { get; private set; }
        public object? DpiReceipt { get; private set; }
        public Observation(string source, string root, string file = "diagram.mmd")
        {
            Window = new(source, new(), () => new(root, file, source, true, 0));
            listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == "LensDocsStudio.Mermaid", Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = a =>
                {
                    if (Phases.Count >= 30_000) return;
                    var allocation = a.TagObjects.FirstOrDefault(t => t.Key == "allocated.current-thread.bytes").Value;
                    Phases.Enqueue(new(a.OperationName, Now() - a.Duration.TotalMilliseconds, a.Duration.TotalMilliseconds, allocation is long n ? n : null));
                }
            };
            ActivitySource.AddActivityListener(listener); CompositionTarget.Rendering += Frame;
            Window.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, e) =>
            {
                lastInput = Now(); lastInputKind = "Down"; Interlocked.Increment(ref inputSequence);
                LastButtonDown = new(lastInput, inputSequence, Round(Window.PointToScreen(e.GetPosition(Window))));
            }), true);
            Window.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler((_, e) =>
            {
                lastInput = Now(); lastInputKind = "Move";
                var screen = Round(Window.PointToScreen(e.GetPosition(Window)));
                lastMoveScreen = screen; Interlocked.Increment(ref inputSequence);
                if (InjectedMoves.IsEmpty) return;
                if (!InjectedMoves.TryGetValue(screen, out var injected))
                {
                    // Absolute SendInput normalisation can round by one physical pixel.
                    for (var dx = -1; dx <= 1; dx++) for (var dy = -1; dy <= 1; dy++)
                        if (InjectedMoves.TryGetValue(new(screen.X + dx, screen.Y + dy), out var nearby)) injected = Math.Max(injected, nearby);
                }
                if (injected > 0)
                { lastInjectedMove = injected; StreamArrivals.Enqueue(new(injected, lastInput)); }
            }), true);
            Window.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((_, e) =>
            {
                lastInput = Now(); lastInputKind = "Up"; Interlocked.Increment(ref inputSequence);
                LastButtonUp = new(lastInput, inputSequence, Round(Window.PointToScreen(e.GetPosition(Window))));
            }), true);
        }
        public IntPtr Handle => Window.Dispatcher.Invoke(() => new WindowInteropHelper(Window).Handle);
        public ScrollViewer Viewport => (ScrollViewer)Window.FindName("Viewport");
        public Image Drawing => (Image)Window.FindName("Drawing");
        public Rectangle Ghost => (Rectangle)Window.FindName("DragGhost");
        public Slider Zoom => (Slider)Window.FindName("Zoom");
        public RadioButton Arrange => (RadioButton)Window.FindName("ArrangeMode");
        public ArrangementSession Session => (ArrangementSession)Field(Window, "session")!;
        public DiagramInspection Inspection => (DiagramInspection)Field(Window, "inspection")!;
        public bool Ready => Field(Window, "session") is ArrangementSession && !(bool)Field(Window, "busy")!;
        public Point GhostCorner(Rect bounds) => Drawing.PointToScreen(new(bounds.X, bounds.Y));
        public object Dpi()
        {
            var dpi = VisualTreeHelper.GetDpi(Window); var handle = new WindowInteropHelper(Window).Handle;
            return DpiReceipt = new { Win32Dpi = GetDpiForWindow(handle), WpfDpiX = dpi.PixelsPerInchX, WpfDpiY = dpi.PixelsPerInchY,
                Awareness = GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(handle)), MonitorCount = GetSystemMetrics(80), PhysicalTransitionsExercised = false };
        }
        public Task<FrameSample> Request(Point expected, PixelPoint target, double injected)
        {
            var completion = new TaskCompletionSource<FrameSample>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref pending, new(expected, target, injected, Interlocked.Read(ref inputSequence), completion));
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
        }
        public double NextFrame(Func<bool> condition, double started)
        {
            WaitWorker(condition, "The expected state did not reach a presentation opportunity.");
            var waiter = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously); frameWaiters.Enqueue(waiter);
            Window.Dispatcher.BeginInvoke(DispatcherPriority.Render, () => Window.InvalidateVisual());
            return waiter.Task.WaitAsync(TimeSpan.FromSeconds(25)).GetAwaiter().GetResult() - started;
        }
        private void Frame(object? sender, EventArgs e)
        {
            var now = Now();
            while (frameWaiters.TryDequeue(out var waiter)) waiter.TrySetResult(now);
            if (lastRecordedInputSequence != inputSequence && (Ghost.Visibility == Visibility.Visible || Field(Window, "pointer") is Point)
                && Interlocked.Increment(ref observedFrameCount) <= 5000)
            {
                lastRecordedInputSequence = inputSequence;
                Frames.Enqueue(new { InputSequence = inputSequence, InputArrivalMilliseconds = lastInput, TimestampMilliseconds = now,
                    GhostVisible = Ghost.Visibility == Visibility.Visible, InputKind = lastInputKind, InputEventArrivalToFirstWpfFrameMilliseconds = now - lastInput });
            }
            if (Ghost.Visibility != Visibility.Visible) return;
            if (lastInjectedMove != 0 && lastPresentedInjection != lastInjectedMove)
            { lastPresentedInjection = lastInjectedMove; StreamFrames.Enqueue(new(lastInjectedMove, lastInput, now)); }
            var point = Ghost.PointToScreen(new(0, 0));
            var request = Volatile.Read(ref pending);
            if (request is null || inputSequence <= request.InputSequenceBeforeInjection || lastInputKind != "Move" || lastInput < request.Injected
                || Math.Abs(lastMoveScreen.X - request.Target.X) > 1 || Math.Abs(lastMoveScreen.Y - request.Target.Y) > 1
                || Math.Abs(point.X - request.Expected.X) > 2 || Math.Abs(point.Y - request.Expected.Y) > 2) return;
            Volatile.Write(ref pending, null); request.Completion.TrySetResult(new(now - request.Injected, now, true,
                request.Injected, lastInput, inputSequence, request.Target, lastMoveScreen));
        }
        public PixelProbes Probes(Point corner)
        {
            var scale = VisualTreeHelper.GetDpi(Window).DpiScaleX * Zoom.Value / 100;
            var bounds = Inspection.Entities.First(e => e.Identity == (Field(Window, "selected") as string)).Shape.Bounds;
            var w = bounds.Width * scale; var h = bounds.Height * scale; var inset = Math.Max(1, scale);
            var points = new List<PixelPoint>();
            foreach (var fraction in new[] { .13, .29, .53, .71, .89 })
            {
                foreach (var offset in new[] { 0d, 1d })
                {
                    points.Add(Round(new(corner.X + w * fraction, corner.Y + inset + offset)));
                    points.Add(Round(new(corner.X + inset + offset, corner.Y + h * fraction)));
                }
            }
            var colour = ((SolidColorBrush)Ghost.Stroke).Color;
            return new(points.ToArray(), (uint)(colour.R | colour.G << 8 | colour.B << 16));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            CompositionTarget.Rendering -= Frame; listener.Dispose();
            if (Window.IsLoaded) { if (Field(Window, "session") is ArrangementSession session) session.MarkSaved(); Window.Close(); }
        }
    }

    internal sealed record Phase(string Name, double StartedMilliseconds, double Milliseconds, long? AllocatedBytes);
    internal sealed record StreamFrame(double InjectedMilliseconds, double ArrivalMilliseconds, double FrameMilliseconds);
    internal sealed record StreamArrival(double InjectedMilliseconds, double ArrivalMilliseconds);
    internal sealed record ButtonReceipt(double TimestampMilliseconds, long InputSequence, PixelPoint Point);
    private sealed record PendingFrame(Point Expected, PixelPoint Target, double Injected, long InputSequenceBeforeInjection, TaskCompletionSource<FrameSample> Completion);
    internal sealed record FrameSample(double InputToFrameMilliseconds, double FrameTimestampMilliseconds, bool GhostMatchesExpected,
        double InjectedMilliseconds, double InputArrivalMilliseconds, long InputSequence, PixelPoint Target, PixelPoint ObservedPointer);
    private sealed record PointerSample(int Ordinal, double InputToWpfFrameMilliseconds, double? InputToObservedPixelsMilliseconds, bool GhostMatchesExpected,
        double BeforeInputScreenCaptureMilliseconds, double AfterInputScreenCaptureMilliseconds, double PixelProbeCompositorFlushMilliseconds, int PixelProbeCaptures,
        double InjectedMilliseconds, double InputArrivalMilliseconds, double FrameTimestampMilliseconds, long InputSequence, PixelPoint Target, PixelPoint ObservedPointer);
    private sealed record PixelObservation(double? InputToObservedPixelsMilliseconds, double ScreenCaptureMilliseconds, double CompositorFlushMilliseconds, int CaptureCount);
    internal sealed record PixelProbes(PixelPoint[] Points, uint Colour);
    internal readonly record struct PixelPoint(int X, int Y);
    private static PixelPoint Round(Point point) => new((int)Math.Round(point.X), (int)Math.Round(point.Y));
    internal static double Now() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;
    internal static object Statistics(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        double? Percentile(double p) => sorted.Length == 0 ? null : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)];
        return new { Count = sorted.Length, Median = Percentile(.5), P95 = Percentile(.95), P99 = Percentile(.99), Maximum = Percentile(1), PercentilesAreSparse = sorted.Length < 100 };
    }
    private static uint[] ReadPixels(PixelPoint[] points)
    {
        var left = points.Min(p => p.X); var top = points.Min(p => p.Y);
        var width = checked(points.Max(p => p.X) - left + 1); var height = checked(points.Max(p => p.Y) - top + 1);
        if (width > 8192 || height > 8192 || (long)width * height > 4_000_000) throw new IOException("The screen pixel probe exceeded its bounded capture area.");
        var screen = GetDC(IntPtr.Zero); if (screen == IntPtr.Zero) throw new IOException("The owned desktop did not supply a screen capture context.");
        var memory = IntPtr.Zero; var bitmap = IntPtr.Zero; var previous = IntPtr.Zero;
        try
        {
            memory = CreateCompatibleDC(screen); bitmap = CreateCompatibleBitmap(screen, width, height);
            if (memory == IntPtr.Zero || bitmap == IntPtr.Zero) throw new IOException("The owned desktop could not prepare its bounded pixel probe.");
            previous = SelectObject(memory, bitmap);
            if (!BitBlt(memory, 0, 0, width, height, screen, left, top, 0x00CC0020)) throw new IOException("The owned desktop screen capture failed.");
            // One screen capture avoids a potentially blocking desktop round-trip per pixel.
            return points.Select(p => GetPixel(memory, p.X - left, p.Y - top)).ToArray();
        }
        finally
        {
            if (previous != IntPtr.Zero) _ = SelectObject(memory, previous);
            if (bitmap != IntPtr.Zero) _ = DeleteObject(bitmap);
            if (memory != IntPtr.Zero) _ = DeleteDC(memory);
            _ = ReleaseDC(IntPtr.Zero, screen);
        }
    }
    private static bool NearColour(uint a, uint b) => a != uint.MaxValue && Math.Abs((int)(a & 255) - (int)(b & 255)) < 36
        && Math.Abs((int)((a >> 8) & 255) - (int)((b >> 8) & 255)) < 36 && Math.Abs((int)((a >> 16) & 255) - (int)((b >> 16) & 255)) < 36;
    private static PixelObservation ObservePixels(PixelProbes probes, uint[] before, double started)
    {
        var captures = 0d; var flushes = 0d;
        for (var frame = 0; frame < 4; frame++)
        {
            var flushStart = Now(); _ = DwmFlush(); flushes += Now() - flushStart;
            var captureStart = Now(); var after = ReadPixels(probes.Points); captures += Now() - captureStart;
            if (after.Where((value, i) => !NearColour(before[i], probes.Colour) && NearColour(value, probes.Colour)).Any()) return new(Now() - started, captures, flushes, frame + 1);
        }
        return new(null, captures, flushes, 4); // Never invent a visible-pixel sample from a WPF callback alone.
    }
    private static double FlushMilliseconds(double started) { _ = DwmFlush(); return Now() - started; }
    internal static object? Field(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    internal static void Initialise()
    {
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        ThemeService.Apply(Application.Current!.Resources, "Light");
    }
    internal static void Await(Task task, TimeSpan? timeout = null) { Until(() => task.IsCompleted, timeout); task.GetAwaiter().GetResult(); }
    internal static void Until(Func<bool> condition, TimeSpan? timeout = null)
    {
        if (condition()) return;
        var clock = Stopwatch.StartNew(); var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (condition() || clock.Elapsed > (timeout ?? TimeSpan.FromSeconds(45))) frame.Continue = false; };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        if (!condition()) throw new TimeoutException("Arrangement observation exceeded its bounded wait.");
    }
    private static void WaitWorker(Func<bool> condition, string error)
    {
        var watch = Stopwatch.StartNew(); while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException(error); Thread.Sleep(2); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void RequireHostedDesktop()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows" || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new IOException("Automatic pointer acceptance requires a disposable GitHub-hosted Windows desktop.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Union; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    private static void Send(Input input) { if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new IOException("The owned desktop rejected actual input."); }
    private static void Move(PixelPoint point)
    {
        var left = GetSystemMetrics(76); var top = GetSystemMetrics(77); var width = GetSystemMetrics(78); var height = GetSystemMetrics(79);
        Send(new() { Union = new() { Mouse = new() { X = (int)Math.Round((point.X - left) * 65535d / Math.Max(1, width - 1)), Y = (int)Math.Round((point.Y - top) * 65535d / Math.Max(1, height - 1)), Flags = 0xC001 } } });
    }
    private static void Button(bool down) => Send(new() { Union = new() { Mouse = new() { Flags = down ? 2u : 4u } } });
    private static void Key(ushort key) { Send(new() { Type = 1, Union = new() { Keyboard = new() { Key = key } } }); Send(new() { Type = 1, Union = new() { Keyboard = new() { Key = key, Flags = 2 } } }); }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr handle);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr handle);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr handle, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
