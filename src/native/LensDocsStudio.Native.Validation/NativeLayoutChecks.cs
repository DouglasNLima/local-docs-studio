using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeLayoutChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try { Check(output); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static void Check(string output)
    {
        var source = string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Paragraph {i}: **Evidence** &amp; review remains selectable."));
        var parsed = new MarkdownService().Parse(source);
        var renderer = new NativeMarkdownRenderer(new NativeSvgService());
        var diagrams = new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var watch = Stopwatch.StartNew(); var inputMilliseconds = -1d;
        dispatcher.BeginInvoke(DispatcherPriority.Input, () => inputMilliseconds = watch.Elapsed.TotalMilliseconds);
        var pending = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { });
        if (pending.IsCompleted) throw new IOException("Large native layout did not yield to the dispatcher.");
        Pump(pending, dispatcher);
        var rendered = pending.GetAwaiter().GetResult(); var totalMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (inputMilliseconds < 0 || inputMilliseconds >= totalMilliseconds) throw new IOException("Input did not run during native layout preparation.");
        if (rendered.Document.Blocks.Count != 1200 || rendered.SourceElements.Count != 1200 || rendered.Warnings.Count != 0
            || !new TextRange(rendered.Document.ContentStart, rendered.Document.ContentEnd).Text.Contains("Paragraph 1200: Evidence & review remains selectable.", StringComparison.Ordinal))
            throw new IOException("Batched layout lost source mapping, paragraphs or decoded text.");
        using var cancellation = new CancellationTokenSource();
        dispatcher.BeginInvoke(DispatcherPriority.Input, () => cancellation.Cancel());
        var cancelled = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { }, cancellationToken: cancellation.Token);
        Pump(cancelled, dispatcher);
        try { cancelled.GetAwaiter().GetResult(); throw new IOException("New input did not cancel obsolete native layout."); }
        catch (OperationCanceledException) { }
        var beforeStart = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { }, cancellationToken: new(true));
        if (!beforeStart.IsCanceled) throw new IOException("Already-cancelled native layout allocated a completed result.");
        var oversized = new MarkdownService().Parse(string.Join("\n", Enumerable.Range(1, 4000).Select(index => $"- **word{index}**")));
        var oversizedWatch = Stopwatch.StartNew(); var oversizedInput = -1d;
        dispatcher.BeginInvoke(DispatcherPriority.Input, () => oversizedInput = oversizedWatch.Elapsed.TotalMilliseconds);
        var oversizedPending = renderer.RenderAsync(oversized, false, diagrams, _ => { }, (_, _) => { });
        Pump(oversizedPending, dispatcher);
        var oversizedResult = oversizedPending.GetAwaiter().GetResult(); var oversizedTotal = oversizedWatch.Elapsed.TotalMilliseconds;
        File.WriteAllText(Path.Combine(output, "native-large-block.json"), JsonSerializer.Serialize(new {
            SourceCharacters = oversized.Source.Length, InputMilliseconds = oversizedInput, ConstructionMilliseconds = oversizedTotal,
            Passed = oversizedInput >= 0 && oversizedInput < 250 && oversizedInput < oversizedTotal }, new JsonSerializerOptions { WriteIndented = true }));
        if (oversizedInput < 0 || oversizedInput >= 250 || oversizedInput >= oversizedTotal)
            throw new IOException("A single large list blocked dispatcher input for " + oversizedInput.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
        if (!oversizedResult.ReadText().Contains("word4000", StringComparison.Ordinal) || oversizedResult.SourceElements.Count < 4000 || oversizedResult.Document.Blocks.Count != 1)
            throw new IOException("Yielding inside a large list lost text or source mapping.");
        var nestedCases = new[]
        {
            (Name: "table", Source: "| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(index => $"| Row{index} | Value{index} |")), Last: "Value1000"),
            (Name: "quote", Source: string.Join("\n>\n", Enumerable.Range(1, 1200).Select(index => $"> Quoted{index}")), Last: "Quoted1200"),
            (Name: "details", Source: "<details>\n<summary>Large hidden section</summary>\n\n" + string.Join("\n\n", Enumerable.Range(1, 1200).Select(index => $"Hidden{index}")) + "\n\n</details>", Last: "Hidden1200"),
            (Name: "list", Source: oversized.Source, Last: "word4000")
        };
        var nestedEvidence = new List<object>();
        using var stallWatch = new PreviewStallWatch(output);
        foreach (var test in nestedCases)
        {
            var collectionsBefore = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
            var nested = new MarkdownService().Parse(test.Source); var nestedWatch = Stopwatch.StartNew();
            stallWatch.Begin(test.Name);
            using var input = new InputTrace(dispatcher, nestedWatch, stallWatch.Pulse);
            using var construction = new PreviewConstructionTrace(nestedWatch);
            Task<PreviewResult> task; double initialCall;
            try
            {
                task = renderer.RenderAsync(nested, false, diagrams, _ => { }, (_, _) => { });
                initialCall = nestedWatch.Elapsed.TotalMilliseconds; Pump(task, dispatcher);
            }
            finally { stallWatch.End(); }
            input.Dispose(); construction.Dispose(); var inputAt = input.Milliseconds;
            var result = task.GetAwaiter().GetResult(); var elapsed = nestedWatch.Elapsed.TotalMilliseconds;
            var constructionCpu = construction.ThreadCpuMilliseconds; var allocatedBytes = construction.AllocatedBytes;
            var completeText = result.ReadText().Contains(test.Last, StringComparison.Ordinal);
            var sourcePreserved = nested.Source == test.Source;
            var observation = JsonSerializer.Serialize(new
            {
                test.Name, InputMilliseconds = inputAt, InitialCallMilliseconds = initialCall, ConstructionMilliseconds = elapsed,
                MaximumInputGapMilliseconds = input.MaximumGapMilliseconds, ConstructionStages = construction.Stages,
                ConstructionThreadCpuMilliseconds = constructionCpu, ConstructionAllocatedBytes = allocatedBytes,
                OperationsBeforeInput = input.Operations,
                SourceCharacters = test.Source.Length, SourceElements = result.SourceElements.Count, CompleteText = completeText,
                SourcePreserved = sourcePreserved,
                GarbageCollections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(index => GC.CollectionCount(index) - collectionsBefore[index]).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
            // Retain the exact failed observation too; a combined exception cannot distinguish
            // runner scheduling/collection delays from incomplete rendering or source loss.
            File.WriteAllText(Path.Combine(output, "native-nested-" + test.Name + "-construction.json"), observation);
            if (inputAt < 0 || inputAt >= 250 || input.MaximumGapMilliseconds >= 250 || inputAt >= elapsed || !completeText
                || result.SourceElements.Count < 1000 || !sourcePreserved)
                throw new IOException("Nested " + test.Name + " rendering delayed input or lost complete content/source mapping: " + observation);
            using var stop = new CancellationTokenSource(); var cancelledAt = -1d; var cancelWatch = Stopwatch.StartNew();
            dispatcher.BeginInvoke(DispatcherPriority.Input, () => { cancelledAt = cancelWatch.Elapsed.TotalMilliseconds; stop.Cancel(); });
            var stale = renderer.RenderAsync(nested, false, diagrams, _ => { }, (_, _) => { }, cancellationToken: stop.Token); Pump(stale, dispatcher);
            try { stale.GetAwaiter().GetResult(); throw new IOException("Cancelled nested " + test.Name + " returned a stale preview."); }
            catch (OperationCanceledException) { }
            if (cancelledAt < 0 || cancelledAt >= 250) throw new IOException("Input cancellation was delayed by nested " + test.Name + " construction.");
            var viewport = InspectViewport(result, test.Last, output, test.Name);
            nestedEvidence.Add(new { test.Name, InputMilliseconds = inputAt, MaximumInputGapMilliseconds = input.MaximumGapMilliseconds,
                ConstructionMilliseconds = elapsed, CancelInputMilliseconds = cancelledAt,
                SourceCharacters = test.Source.Length, SourceElements = result.SourceElements.Count, Viewport = viewport });
        }
        stallWatch.Complete();
        File.WriteAllText(Path.Combine(output, "native-nested-layout.json"), JsonSerializer.Serialize(new { Passed = true,
            PassedScope = "Construction, cancellation, source integrity and final-target navigation; continuous first-layout input acceptance is reported separately for each viewport.",
            Cases = nestedEvidence }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "native-layout.json"), JsonSerializer.Serialize(new
        {
            Passed = true, SourceCharacters = source.Length, Blocks = rendered.Document.Blocks.Count,
            InputMilliseconds = inputMilliseconds, ConstructionMilliseconds = totalMilliseconds,
            Checks = new[] { "Input runs before a large native document finishes construction", "Batched rendering retains every paragraph, source span and decoded text",
                "New input cancels obsolete native construction", "Cancellation before construction returns no document",
                "Large lists, tables, quotes and closed details yield within the input budget, retain complete source mapping and cancel obsolete work",
                "Real hidden-HWND viewers lay out each large block and reveal its final target with selectable text" },
            Limitation = "Hidden-HWND viewport timings cover these fixtures at 100% zoom; they do not certify all pagination/scrolling, displays or individual oversized paragraph/code-block latency."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static object InspectViewport(PreviewResult result, string expected, string output, string name)
    {
        result.Document.Background = Brushes.White;
        var viewer = new FlowDocumentScrollViewer { Document = result.Document, IsToolBarVisible = false, Background = Brushes.White };
        using var host = new HwndSource(new HwndSourceParameters("Large native preview validation")
            { Width = 640, Height = 300, WindowStyle = unchecked((int)0x80000000) });
        var watch = Stopwatch.StartNew(); var inputAt = -1d;
        var input = viewer.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => inputAt = watch.Elapsed.TotalMilliseconds);
        host.RootVisual = viewer; var attach = watch.Elapsed.TotalMilliseconds;
        viewer.Measure(new Size(640, 300)); var measure = watch.Elapsed.TotalMilliseconds;
        viewer.Arrange(new Rect(0, 0, 640, 300)); var arrange = watch.Elapsed.TotalMilliseconds;
        viewer.UpdateLayout(); var initial = watch.Elapsed.TotalMilliseconds;
        // Observe first-layout responsiveness separately from the unchanged construction gate.
        // A known continuous-view layout stall remains visible even when batched construction passes.
        Pump(input.Task, viewer.Dispatcher); input.Task.GetAwaiter().GetResult();
        var last = result.SourceElements.MaxBy(item => item.Key).Value;
        viewer.Selection.Select(last.ContentStart, last.ContentEnd);
        watch.Restart(); var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, last.ContentStart); Pump(navigation, viewer.Dispatcher);
        navigation.GetAwaiter().GetResult(); Layout(); var elapsed = watch.Elapsed.TotalMilliseconds;
        var rectangle = last.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        var scrolling = (ScrollViewer)viewer.Template.FindName("PART_ContentHost", viewer);
        if (rectangle.IsEmpty || rectangle.Top < 0 || rectangle.Bottom > scrolling.ViewportHeight || !viewer.Selection.Text.Contains(expected, StringComparison.Ordinal))
            throw new IOException("Large " + name + " preview could not reveal and select its final source target.");
        var bitmap = new RenderTargetBitmap(640, 300, 96, 96, PixelFormats.Pbgra32); bitmap.Render(viewer);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(output, "native-large-" + name + ".png"))) encoder.Save(file);
        return new { InitialLayoutMilliseconds = initial, AttachMilliseconds = attach, MeasureMilliseconds = measure - attach,
            ArrangeMilliseconds = arrange - measure, UpdateLayoutMilliseconds = initial - arrange,
            FirstLayoutInputMilliseconds = inputAt, FirstLayoutInputBudgetMilliseconds = 250,
            FirstLayoutInputWithinBudget = inputAt >= 0 && inputAt < 250,
            FirstLayoutInputAcceptance = inputAt >= 0 && inputAt < 250 ? "PASS" : "FAIL",
            LastTargetMilliseconds = elapsed, scrolling.VerticalOffset, scrolling.ExtentHeight, scrolling.ViewportHeight };
        void Layout() { viewer.Measure(new Size(640, 300)); viewer.Arrange(new Rect(0, 0, 640, 300)); viewer.UpdateLayout(); }
    }
    private static void Pump(Task task, Dispatcher dispatcher)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("Native layout did not finish within the validation deadline.");
    }

    private sealed class InputTrace : IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly Stopwatch watch;
        private readonly DispatcherTimer timer;
        private readonly DispatcherOperation queued;
        private readonly Action? pulse;
        private readonly Dictionary<DispatcherOperation, double> started = [];
        private double lastInput;
        private bool disposed;
        public double Milliseconds { get; private set; } = -1;
        public double MaximumGapMilliseconds { get; private set; }
        public List<object> Operations { get; } = [];

        public InputTrace(Dispatcher dispatcher, Stopwatch watch, Action? pulse = null)
        {
            this.dispatcher = dispatcher; this.watch = watch; this.pulse = pulse;
            dispatcher.Hooks.OperationStarted += OnStarted;
            dispatcher.Hooks.OperationCompleted += OnCompleted;
            queued = dispatcher.BeginInvoke(DispatcherPriority.Input, ObserveInput);
            timer = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => ObserveInput(); timer.Start();
        }
        private void ObserveInput()
        {
            pulse?.Invoke();
            var elapsed = watch.Elapsed.TotalMilliseconds;
            MaximumGapMilliseconds = Math.Max(MaximumGapMilliseconds, elapsed - lastInput); lastInput = elapsed;
            if (Milliseconds < 0) Milliseconds = elapsed;
        }
        private void OnStarted(object? sender, DispatcherHookEventArgs args)
        {
            if (Milliseconds < 0) started[args.Operation] = watch.Elapsed.TotalMilliseconds;
        }
        private void OnCompleted(object? sender, DispatcherHookEventArgs args)
        {
            if (started.Remove(args.Operation, out var start))
                Operations.Add(new { Priority = args.Operation.Priority.ToString(), StartedMilliseconds = start,
                    DurationMilliseconds = watch.Elapsed.TotalMilliseconds - start });
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; timer.Stop(); queued.Abort();
            MaximumGapMilliseconds = Math.Max(MaximumGapMilliseconds, watch.Elapsed.TotalMilliseconds - lastInput);
            dispatcher.Hooks.OperationStarted -= OnStarted;
            dispatcher.Hooks.OperationCompleted -= OnCompleted;
        }
    }
}
