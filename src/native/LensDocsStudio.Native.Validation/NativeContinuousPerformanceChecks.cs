using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
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
using LensDocsStudio.Native.Platform;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeContinuousPerformanceChecks
{
    public static void Run(string output, string? scenario = null, bool acceptance = false, int repetitions = 1, bool edited = false, bool checkAnchors = false, bool segmentedPrototype = false)
    {
        Directory.CreateDirectory(output);
        var dispatcher = Dispatcher.CurrentDispatcher;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var fixtures = ContinuousPreviewFixtures.Create(output, scenario);
            if (checkAnchors) fixtures = [.. fixtures,
                ("anchor-footnotes", "# Reference\n\nReference [^early].\n\n[^early]: Early note.\n\n# Body\n\n" + string.Join("\n\n", Enumerable.Range(0, 150).Select(i => "Body paragraph " + i)) + "\n\n# Last\n\nLast marker.", "Last marker."),
                ("anchor-wrapped-table", "# Wrapped rows\n\n| Long | Short |\n|---|---|\n| " + string.Join(" ", Enumerable.Repeat("wrapped **Café 😀** content", 160)) + " | small |\n| second | row |\n\n# Last anchor\n\nLast marker.", "Last marker."),
                ("anchor-nested-list", "# Nested\n\n42. First\n    - Inner **bold**\n    - " + string.Join(" ", Enumerable.Repeat("wrapped inner", 300)) + "\n43. Next\n\n# Details container\n\n<details>\n<summary>Closed</summary>\n\nHidden.\n\n</details>\n\n# Last\n\nLast marker.", "Last marker.")];
            if (repetitions is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(repetitions));
            var samples = new List<object>(); var passed = true;
            var documents = new List<WeakReference>();
            for (var repetition = 0; repetition < repetitions; repetition++)
            foreach (var fixture in fixtures)
            {
                // Fresh sources exercise recomputation; their original fixture is retained.
                var source = edited ? fixture.Source.Replace(fixture.Last, fixture.Last + $" revised{repetition + 1}", StringComparison.Ordinal)
                    + $"\n\n## Fresh revision {repetition + 1}\n\nChanged distant content Café 😀.\n" : fixture.Source;
                var sampleOutput = repetitions == 1 ? output : Path.Combine(output, "repeat-" + repetition);
                Directory.CreateDirectory(sampleOutput);
                var endToEnd = Stopwatch.StartNew();
                var allAllocation = GC.GetTotalAllocatedBytes();
                var parseWatch = Stopwatch.StartNew();
                var parsed = new MarkdownService().Parse(source);
                var parsing = parseWatch.Elapsed.TotalMilliseconds;
                var preparationWatch = Stopwatch.StartNew();
                var diagrams = parsed.Syntax.Descendants<FencedCodeBlock>().Where(code => code.Info is "mermaid" or "mmd")
                    .ToDictionary(code => code, code => new DiagramService().Render(code.Lines.ToString()));
                var media = StaWorker.RunAsync(() => (Drawings: diagrams.Where(pair => pair.Value.Success)
                    .ToDictionary(pair => pair.Key, pair => new NativeSvgService().Render(pair.Value.Svg!)),
                    Images: new PreviewImageService().Prepare(parsed, Path.Combine(output, "synthetic.md"), output))).GetAwaiter().GetResult();
                var preparation = preparationWatch.Elapsed.TotalMilliseconds;
                var viewer = new StageViewer { IsToolBarVisible = false, Background = Brushes.White };
                using var host = new HwndSource(new HwndSourceParameters("Continuous performance validation")
                    { Width = 640, Height = 300, WindowStyle = unchecked((int)0x80000000) });
                host.RootVisual = viewer;
                Layout(); Pump(dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
                var allocated = GC.GetTotalAllocatedBytes();
                var collections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
                viewer.Reset();
                var viewportChanges = new List<object>();
                viewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) =>
                {
                    if (viewportChanges.Count < 32) viewportChanges.Add(new { e.ViewportWidth, e.ViewportHeight, e.ExtentHeight, e.ViewportWidthChange });
                }));
                var watch = Stopwatch.StartNew(); var inputAt = -1d; var lastInput = 0d; var maximumGap = 0d;
                using var constructionTrace = new PreviewConstructionTrace(watch);
                var operations = new List<object>(); var starts = new Dictionary<DispatcherOperation, double>();
                void Input()
                {
                    var now = watch.Elapsed.TotalMilliseconds;
                    maximumGap = Math.Max(maximumGap, now - lastInput); lastInput = now;
                    if (inputAt < 0) inputAt = now;
                }
                void Started(object? sender, DispatcherHookEventArgs e) => starts[e.Operation] = watch.Elapsed.TotalMilliseconds;
                void Completed(object? sender, DispatcherHookEventArgs e)
                {
                    if (starts.Remove(e.Operation, out var at) && operations.Count < 512)
                        operations.Add(new { Priority = e.Operation.Priority.ToString(), Milliseconds = watch.Elapsed.TotalMilliseconds - at });
                }
                dispatcher.Hooks.OperationStarted += Started; dispatcher.Hooks.OperationCompleted += Completed;
                var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (_, _) => Input(); timer.Start();
                var attach = 0d; var firstLayout = 0d; var firstContent = -1d;
                viewer.LayoutUpdated += (_, _) => { if (firstContent < 0 && viewer.Document?.Blocks.Count > 0) firstContent = watch.Elapsed.TotalMilliseconds; };
                dispatcher.BeginInvoke(DispatcherPriority.Input, Input);
                var task = new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false,
                    diagrams, _ => { }, (_, _) => { }, preparedDrawings: media.Drawings, preparedImages: media.Images);
                try { Pump(task); }
                catch (Exception error)
                {
                    timer.Stop(); dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
                    File.WriteAllText(Path.Combine(output, fixture.Name + "-failure.json"), JsonSerializer.Serialize(new
                    {
                        fixture.Name, Status = "FAIL", Stage = "Construction", Error = error.GetType().Name,
                        ConstructionInputMilliseconds = inputAt, MaximumInputGapMilliseconds = maximumGap,
                        InputBudgetMilliseconds = 250, ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
                        AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, SourcePreserved = parsed.Source == fixture.Source,
                        Completion = "NOT COMPLETED", Navigation = "NOT TESTED"
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    throw;
                }
                var result = task.GetAwaiter().GetResult();
                if (segmentedPrototype) SegmentNativeLists(result.Document);
                var construction = watch.Elapsed.TotalMilliseconds;
                var constructionAllocation = constructionTrace.AllocatedBytes; var constructionCpu = constructionTrace.ThreadCpuMilliseconds;
                constructionTrace.Dispose();
                Input(); var layoutStart = watch.Elapsed.TotalMilliseconds; var layoutInput = -1d;
                var firstLayoutInput = dispatcher.BeginInvoke(DispatcherPriority.Input, () => { layoutInput = watch.Elapsed.TotalMilliseconds - layoutStart; Input(); });
                ContinuousHarnessEvents.Log.Stage("harness/first-layout-start");
                var at = watch.Elapsed.TotalMilliseconds; viewer.Document = result.Document; attach = watch.Elapsed.TotalMilliseconds - at;
                var beforeLayout = watch.Elapsed.TotalMilliseconds; Layout(); firstLayout = watch.Elapsed.TotalMilliseconds - beforeLayout;
                Pump(firstLayoutInput.Task);
                ContinuousHarnessEvents.Log.Stage("harness/first-layout-end");
                if (fixture.Name == "large-4mb")
                    File.WriteAllText(Path.Combine(output, fixture.Name + "-first-layout.json"), JsonSerializer.Serialize(new
                    {
                        fixture.Name, ConstructionMilliseconds = construction, FirstLayoutInputMilliseconds = layoutInput,
                        MaximumInputGapMilliseconds = maximumGap, InputBudgetMilliseconds = 250,
                        InputAcceptance = layoutInput >= 0 && layoutInput < 250 && maximumGap < 250 ? "PASS" : "FAIL",
                        FirstContentLayoutMilliseconds = firstContent, SourceUtf8Bytes = System.Text.Encoding.UTF8.GetByteCount(fixture.Source),
                        SourcePreserved = parsed.Source == fixture.Source, CompleteText = result.ReadText().Contains(fixture.Last, StringComparison.Ordinal),
                        SourceElements = result.SourceElements.Count, FullLayout = "PENDING", Navigation = "NOT TESTED"
                    }, new JsonSerializerOptions { WriteIndented = true }));
                try { Pump(dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task); }
                catch (Exception error)
                {
                    timer.Stop(); dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
                    File.WriteAllText(Path.Combine(output, fixture.Name + "-failure.json"), JsonSerializer.Serialize(new
                    {
                        fixture.Name, Status = "FAIL", Stage = "Waiting for background layout / dispatcher idle", Error = error.GetType().Name,
                        ConstructionMilliseconds = construction, FirstLayoutInputMilliseconds = layoutInput,
                        MaximumInputGapMilliseconds = maximumGap, InputBudgetMilliseconds = 250,
                        ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds, AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated,
                        SourcePreserved = parsed.Source == fixture.Source, FullLayout = "NOT SETTLED", Navigation = "NOT TESTED"
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    throw;
                }
                Input();
                var completion = watch.Elapsed.TotalMilliseconds;
                var readiness = endToEnd.Elapsed.TotalMilliseconds;
                var readinessAllocation = GC.GetTotalAllocatedBytes() - allAllocation;
                timer.Stop(); dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
                var completedAllocation = GC.GetTotalAllocatedBytes() - allocated;
                var layoutStages = new { viewer.MeasureMilliseconds, viewer.ArrangeMilliseconds, viewer.UpdateLayoutMilliseconds,
                    viewer.MeasureCalls, viewer.ArrangeCalls };
                ContinuousHarnessEvents.Log.Stage("harness/correctness-start");
                var mapProbe = Stopwatch.StartNew(); var visibleAnchor = result.TextMap.VisibleSource(300); mapProbe.Stop();
                var visibleAnchorMilliseconds = mapProbe.Elapsed.TotalMilliseconds;
                var capture = new RenderTargetBitmap(640, 300, 96, 96, PixelFormats.Pbgra32);
                var backing = new DrawingVisual();
                using (var context = backing.RenderOpen()) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 640, 300));
                capture.Render(backing); capture.Render(viewer);
                var raster = watch.Elapsed.TotalMilliseconds;
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
                using (var file = File.Create(Path.Combine(sampleOutput, fixture.Name + "-initial.png"))) encoder.Save(file);
                if (checkAnchors && result.TextMap.VisibleSource(300) != ViewportAnchorReference.VisibleSource(result.TextMap, 300))
                    throw new IOException("The initial viewport, including closed details, differs from the exhaustive anchor oracle.");
                var completeText = result.ReadText().Contains(fixture.Last, StringComparison.Ordinal);
                var last = result.SourceElements.MaxBy(item => item.Key).Value;
                viewer.Selection.Select(last.ContentStart, last.ContentEnd);
                var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, last.ContentStart); Pump(navigation); Layout();
                var rectangle = last.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var scrolling = (ScrollViewer)viewer.Template.FindName("PART_ContentHost", viewer);
                var targetVisible = !rectangle.IsEmpty && rectangle.Top >= 0 && rectangle.Bottom <= scrolling.ViewportHeight;
                var selectedTarget = viewer.Selection.Text.Contains(edited ? "Changed distant content" : fixture.Last, StringComparison.Ordinal);
                using var process = Process.GetCurrentProcess();
                var anchorChecks = new List<object>();
                if (checkAnchors)
                {
                    foreach (var fraction in new[] { 0d, .5, 1d })
                    {
                        scrolling.ScrollToVerticalOffset(scrolling.ScrollableHeight * fraction); Layout();
                        var scan = Stopwatch.StartNew(); var expectedAnchor = ViewportAnchorReference.VisibleSource(result.TextMap, 300); scan.Stop();
                        var indexed = Stopwatch.StartNew(); var actualAnchor = result.TextMap.VisibleSource(300); indexed.Stop();
                        anchorChecks.Add(new { Fraction = fraction, ExpectedSourceOffset = expectedAnchor, ActualSourceOffset = actualAnchor,
                            ExhaustiveMilliseconds = scan.Elapsed.TotalMilliseconds, IndexedMilliseconds = indexed.Elapsed.TotalMilliseconds });
                        if (actualAnchor != expectedAnchor) throw new IOException("Indexed viewport anchor differs from the retained exhaustive native geometry oracle: " + fixture.Name);
                    }
                }
                var correct = completeText && targetVisible && selectedTarget && parsed.Source == source && result.Warnings.Count == 0;
                var withinBudget = inputAt >= 0 && inputAt < 250 && layoutInput >= 0 && layoutInput <= 250 && maximumGap < 250;
                passed &= correct && withinBudget;
                var observation = new
                {
                    SegmentedNativePrototype = segmentedPrototype, PrototypeChunkSize = segmentedPrototype ? 64 : 0,
                    WarningCount = result.Warnings.Count,
                    fixture.Name, ParsingMilliseconds = parsing, ImageAndDiagramPreparationMilliseconds = preparation,
                    Repetition = repetition, Temperature = repetition == 0 ? "First fixture execution in this process (no prewarm)" : "Repeated fresh construction in the same process",
                    Edited = edited, SourceUtf8Bytes = Encoding.UTF8.GetByteCount(source),
                    FixtureSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fixture.Source))),
                    SourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
                    HarnessSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NativeContinuousPerformanceChecks).Assembly.Location))),
                    SourceRevision = Environment.GetEnvironmentVariable("LENS_CONTINUOUS_SOURCE_REVISION"),
                    ConstructionInputMilliseconds = inputAt, FirstLayoutInputMilliseconds = layoutInput,
                    MaximumInputGapMilliseconds = maximumGap, InputBudgetMilliseconds = 250,
                    InputAcceptance = inputAt >= 0 && inputAt < 250 && layoutInput >= 0 && layoutInput < 250 && maximumGap < 250 ? "PASS" : "FAIL",
                    FirstLayoutAcceptance = layoutInput >= 0 && layoutInput <= 250 ? "PASS" : "FAIL", Correctness = correct ? "PASS" : "FAIL",
                    ConstructionMilliseconds = construction, AttachmentMilliseconds = attach, FinalLayoutMilliseconds = firstLayout,
                    ConstructionStages = constructionTrace.Stages, ConstructionUIThreadAllocatedBytes = constructionAllocation,
                    ConstructionUIThreadCpuMilliseconds = constructionCpu,
                    FirstContentLayoutMilliseconds = firstContent, CompletionMilliseconds = completion, FirstRasterMilliseconds = raster,
                    EndToEndReadinessMilliseconds = readiness, EndToEndAllocatedBytes = readinessAllocation,
                    DeferredRequiredContent = false, ViewportChanges = viewportChanges,
                    LayoutStages = layoutStages, AllocatedBytes = completedAllocation, process.PeakWorkingSet64,
                    ManagedBytesWithoutCollection = GC.GetTotalMemory(false), process.WorkingSet64, process.PrivateMemorySize64,
                    GarbageCollections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(i => GC.CollectionCount(i) - collections[i]).ToArray(),
                    SourcePreserved = parsed.Source == source, CompleteText = completeText, TargetVisible = targetVisible,
                    LogicalTextSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.ReadText()))),
                    SourceElementCount = result.SourceElements.Count,
                    Structure = Structure(result.Document.Blocks),
                    SelectedTarget = selectedTarget, Operations = operations,
                    AnchorChecks = anchorChecks,
                    ViewportAnchorProbe = new { Milliseconds = visibleAnchorMilliseconds, SourceOffset = visibleAnchor, Boundary = "Outside readiness and first-layout clocks" },
                    Environment = new { OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(), Environment.ProcessorCount,
                        NativeAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(NativeMarkdownRenderer).Assembly.Location))),
                        Dpi = VisualTreeHelper.GetDpi(viewer).PixelsPerInchX, Appearance = SystemParameters.HighContrast ? "High Contrast" : "Light",
                        Width = 640, Height = 300, Zoom = 100, Context = "Hidden HWND, framework-dependent Release validation, isolated process; no foreground interaction" }
                };
                File.WriteAllText(Path.Combine(sampleOutput, fixture.Name + ".json"), JsonSerializer.Serialize(observation, new JsonSerializerOptions { WriteIndented = true }));
                samples.Add(new { fixture.Name, Repetition = repetition, FirstLayoutMilliseconds = layoutInput, FirstLayoutAcceptance = observation.FirstLayoutAcceptance, observation.InputAcceptance, observation.Correctness });
                Console.WriteLine(fixture.Name + ": " + (layoutInput < 250 && maximumGap < 250 ? "PASS" : "FAIL")
                    + " against 250 ms input budget; first layout " + layoutInput.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
                if (!correct)
                    throw new IOException("Continuous preview lost content, navigation or prepared media.");
                documents.Add(new WeakReference(result.Document));
                viewer.Document = null; host.RootVisual = null;
                void Layout()
                {
                    viewer.Measure(new Size(640, 300)); viewer.Arrange(new Rect(0, 0, 640, 300));
                    var update = Stopwatch.StartNew(); viewer.UpdateLayout(); viewer.UpdateLayoutMilliseconds += update.Elapsed.TotalMilliseconds;
                }
            }
            File.WriteAllText(Path.Combine(output, "acceptance.json"), JsonSerializer.Serialize(new
            {
                Status = segmentedPrototype ? "EXPERIMENT_ONLY" : passed ? "PASS" : "FAIL",
                AcceptanceEligible = !segmentedPrototype, MeasuredBudgetResult = passed ? "PASS" : "FAIL", Enforced = acceptance, ThresholdMilliseconds = 250,
                Boundary = "Input callback queued before attachment, synchronous Measure/Arrange/UpdateLayout and dispatcher delivery; unchanged original first-layout boundary",
                ReadinessBoundary = "Before parsing through full construction, attachment, initial layout and ApplicationIdle; no required content deferred",
                Samples = samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(output, "lifecycle-observation.json"), JsonSerializer.Serialize(new
            {
                DocumentsCreated = documents.Count, AliveAfterNaturalCollections = documents.Count(document => document.IsAlive),
                ManagedBytes = GC.GetTotalMemory(false), ForcedCollections = false,
                Limitation = "Natural-collection retention observation only; JIT liveness and collection timing prevent a zero-retention/leak claim. Every viewer is detached after correctness verification."
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (acceptance && !passed) throw new IOException("Continuous 250 ms acceptance FAILED; see acceptance.json and every raw sample.");
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    // Experimental native-list segmentation only. Never used by the application.
    // It changes logical list grouping and is not a production semantics acceptance.
    private static void SegmentNativeLists(FlowDocument document)
    {
        foreach (var list in document.Blocks.OfType<System.Windows.Documents.List>().ToArray())
        {
            var items = list.ListItems.ToArray(); if (items.Length <= 64) continue;
            var count = 0;
            while (count < items.Length)
            {
                var segment = new System.Windows.Documents.List { MarkerStyle = list.MarkerStyle, StartIndex = list.StartIndex + count,
                    Padding = list.Padding, Margin = new Thickness(list.Margin.Left, count == 0 ? list.Margin.Top : 0,
                        list.Margin.Right, count + 64 >= items.Length ? list.Margin.Bottom : 0) };
                document.Blocks.InsertBefore(list, segment);
                foreach (var item in items.Skip(count).Take(64)) { list.ListItems.Remove(item); segment.ListItems.Add(item); }
                count += 64;
            }
            document.Blocks.Remove(list);
        }
    }

    private static object Structure(BlockCollection blocks)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var canonical = new StringBuilder(); var pendingText = new StringBuilder(); string? pendingStyle = null;
        void Flush()
        {
            if (pendingStyle is null) return;
            canonical.Append(pendingStyle.Length).Append(':').Append(pendingStyle).Append(pendingText.Length).Append(':').Append(pendingText);
            pendingStyle = null; pendingText.Clear();
        }
        void Count(TextElement element)
        {
            var name = element.GetType().Name; counts[name] = counts.GetValueOrDefault(name) + 1;
            if (element is Run run)
            {
                var style = string.Join('|', run.FontFamily.Source, run.FontSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    run.FontWeight, run.FontStyle, run.FontStretch, run.BaselineAlignment, run.Foreground?.ToString(), run.Background?.ToString(),
                    string.Join(',', run.TextDecorations.Select(decoration => decoration.Location)));
                if (pendingStyle != style) Flush();
                pendingStyle = style; pendingText.Append(run.Text); return;
            }
            Flush(); canonical.Append('<').Append(name).Append('>');
            switch (element)
            {
                case Section section: foreach (var block in section.Blocks) Count(block); break;
                case System.Windows.Documents.List list: foreach (var item in list.ListItems) Count(item); break;
                case ListItem item: foreach (var block in item.Blocks) Count(block); break;
                case Table table: foreach (var group in table.RowGroups) foreach (var row in group.Rows) foreach (var cell in row.Cells) { Count(cell); } break;
                case TableCell cell: foreach (var block in cell.Blocks) Count(block); break;
                case Paragraph paragraph: foreach (var inline in paragraph.Inlines) Count(inline); break;
                case Span span: foreach (var inline in span.Inlines) Count(inline); break;
            }
        }
        foreach (var block in blocks) Count(block);
        Flush();
        return new
        {
            ElementCounts = counts,
            // Coalescing plain runs intentionally changes representation. The gate
            // still compares every other element count and actual styled text.
            NonRunCounts = counts.Where(pair => pair.Key != "Run").ToDictionary(),
            StyledTextSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
        };
    }

    private sealed class StageViewer : FlowDocumentScrollViewer
    {
        public double MeasureMilliseconds, ArrangeMilliseconds, UpdateLayoutMilliseconds;
        public int MeasureCalls, ArrangeCalls;
        public void Reset() { MeasureMilliseconds = ArrangeMilliseconds = UpdateLayoutMilliseconds = 0; MeasureCalls = ArrangeCalls = 0; }
        protected override Size MeasureOverride(Size available)
        {
            var watch = Stopwatch.StartNew();
            try { return base.MeasureOverride(available); }
            finally { MeasureCalls++; MeasureMilliseconds += watch.Elapsed.TotalMilliseconds; }
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var watch = Stopwatch.StartNew();
            try { return base.ArrangeOverride(finalSize); }
            finally { ArrangeCalls++; ArrangeMilliseconds += watch.Elapsed.TotalMilliseconds; }
        }
    }

    private static void Pump(Task task)
    {
        if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; }
        var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(60) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("Continuous preview exceeded the bounded validation deadline.");
        task.GetAwaiter().GetResult();
    }
}
