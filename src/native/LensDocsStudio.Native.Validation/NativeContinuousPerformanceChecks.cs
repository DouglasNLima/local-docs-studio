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
using LensDocsStudio.Native.Platform;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeContinuousPerformanceChecks
{
    public static void Run(string output, string? scenario = null)
    {
        Directory.CreateDirectory(output);
        var dispatcher = Dispatcher.CurrentDispatcher;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var fixtures = new[]
            {
                (Name: "list", Source: string.Join("\n", Enumerable.Range(1, 4000).Select(i => $"- **word{i}**")), Last: "word4000"),
                (Name: "table", Source: "| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"| Row{i} | Value{i} |")), Last: "Value1000"),
                (Name: "quote", Source: string.Join("\n>\n", Enumerable.Range(1, 1200).Select(i => $"> Quoted{i}")), Last: "Quoted1200"),
                (Name: "details", Source: "<details open>\n<summary>Large section</summary>\n\n" + string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Hidden{i}")) + "\n\n</details>", Last: "Hidden1200"),
                (Name: "paragraphs", Source: string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Paragraph{i}: **Evidence** &amp; review remains selectable.")), Last: "Paragraph1200")
            };
            if (scenario is "extended" or "large-4mb" or "mixed")
            {
                var large = "# Large editing fixture\n\n" + string.Concat(Enumerable.Range(0, 20000).Select(i =>
                    $"Paragraph {i:D5}: **portable Markdown** retains Unicode Café and [relative links](Section-0/Document-0.md). "
                    + "The current source must survive native insertion, layout and Undo without an implicit file write.\n\n"));
                var mixed = "# Mixed document\n\n[Deep target](#deep-target)\n\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"- **Mixed item{i}**"))
                    + "\n\n| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 300).Select(i => $"| Row{i} | Value{i} |"))
                    + "\n\n<details open>\n<summary>Outer evidence</summary>\n\n<details open>\n<summary>Nested evidence</summary>\n\n"
                    + string.Join("\n\n", Enumerable.Range(1, 300).Select(i => $"> **Quoted{i}** Café 😀 é"))
                    + "\n\n</details>\n\n</details>\n\n" + string.Join("\n\n", Enumerable.Repeat("![Repeated pixel](pixel.png)", 12))
                    + "\n\n```mermaid\nflowchart LR\nA[Source] --> B[Native preview]\n```\n\n## Deep target\n\nMixed final marker.";
                fixtures = [("large-4mb", large, "Paragraph 19999"), ("mixed", mixed, "Mixed final marker.")];
                if (scenario != "extended") fixtures = fixtures.Where(fixture => fixture.Name == scenario).ToArray();
                File.WriteAllBytes(Path.Combine(output, "pixel.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII="));
            }
            foreach (var fixture in fixtures)
            {
                var parseWatch = Stopwatch.StartNew();
                var parsed = new MarkdownService().Parse(fixture.Source);
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
                var watch = Stopwatch.StartNew(); var inputAt = -1d; var lastInput = 0d; var maximumGap = 0d;
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
                var result = task.GetAwaiter().GetResult(); var construction = watch.Elapsed.TotalMilliseconds;
                Input(); var layoutStart = watch.Elapsed.TotalMilliseconds; var layoutInput = -1d;
                var firstLayoutInput = dispatcher.BeginInvoke(DispatcherPriority.Input, () => { layoutInput = watch.Elapsed.TotalMilliseconds - layoutStart; Input(); });
                var at = watch.Elapsed.TotalMilliseconds; viewer.Document = result.Document; attach = watch.Elapsed.TotalMilliseconds - at;
                var beforeLayout = watch.Elapsed.TotalMilliseconds; Layout(); firstLayout = watch.Elapsed.TotalMilliseconds - beforeLayout;
                Pump(firstLayoutInput.Task);
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
                timer.Stop(); dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
                var completedAllocation = GC.GetTotalAllocatedBytes() - allocated;
                var layoutStages = new { viewer.MeasureMilliseconds, viewer.ArrangeMilliseconds, viewer.UpdateLayoutMilliseconds,
                    viewer.MeasureCalls, viewer.ArrangeCalls };
                var capture = new RenderTargetBitmap(640, 300, 96, 96, PixelFormats.Pbgra32); capture.Render(viewer);
                var raster = watch.Elapsed.TotalMilliseconds;
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
                using (var file = File.Create(Path.Combine(output, fixture.Name + "-initial.png"))) encoder.Save(file);
                var completeText = result.ReadText().Contains(fixture.Last, StringComparison.Ordinal);
                var last = result.SourceElements.MaxBy(item => item.Key).Value;
                viewer.Selection.Select(last.ContentStart, last.ContentEnd);
                var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, last.ContentStart); Pump(navigation); Layout();
                var rectangle = last.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var scrolling = (ScrollViewer)viewer.Template.FindName("PART_ContentHost", viewer);
                var targetVisible = !rectangle.IsEmpty && rectangle.Top >= 0 && rectangle.Bottom <= scrolling.ViewportHeight;
                var selectedTarget = viewer.Selection.Text.Contains(fixture.Last, StringComparison.Ordinal);
                using var process = Process.GetCurrentProcess();
                File.WriteAllText(Path.Combine(output, fixture.Name + ".json"), JsonSerializer.Serialize(new
                {
                    fixture.Name, ParsingMilliseconds = parsing, ImageAndDiagramPreparationMilliseconds = preparation,
                    SourceUtf8Bytes = System.Text.Encoding.UTF8.GetByteCount(fixture.Source),
                    ConstructionInputMilliseconds = inputAt, FirstLayoutInputMilliseconds = layoutInput,
                    MaximumInputGapMilliseconds = maximumGap, InputBudgetMilliseconds = 250,
                    InputAcceptance = inputAt >= 0 && inputAt < 250 && layoutInput >= 0 && layoutInput < 250 && maximumGap < 250 ? "PASS" : "FAIL",
                    ConstructionMilliseconds = construction, AttachmentMilliseconds = attach, FinalLayoutMilliseconds = firstLayout,
                    FirstContentLayoutMilliseconds = firstContent, CompletionMilliseconds = completion, FirstRasterMilliseconds = raster,
                    LayoutStages = layoutStages, AllocatedBytes = completedAllocation, process.PeakWorkingSet64,
                    GarbageCollections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(i => GC.CollectionCount(i) - collections[i]).ToArray(),
                    SourcePreserved = parsed.Source == fixture.Source, CompleteText = completeText, TargetVisible = targetVisible,
                    SelectedTarget = selectedTarget, Operations = operations,
                    Environment = new { OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(), Environment.ProcessorCount,
                        NativeAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(NativeMarkdownRenderer).Assembly.Location))),
                        Width = 640, Height = 300, Zoom = 100, Context = "Hidden HWND, framework-dependent Release validation, isolated process; no foreground interaction" }
                }, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(fixture.Name + ": " + (layoutInput < 250 && maximumGap < 250 ? "PASS" : "FAIL")
                    + " against 250 ms input budget; first layout " + layoutInput.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
                if (!completeText || !targetVisible || !selectedTarget || parsed.Source != fixture.Source || result.Warnings.Count != 0)
                    throw new IOException("Continuous preview lost content, navigation or prepared media.");
                void Layout()
                {
                    viewer.Measure(new Size(640, 300)); viewer.Arrange(new Rect(0, 0, 640, 300));
                    var update = Stopwatch.StartNew(); viewer.UpdateLayout(); viewer.UpdateLayoutMilliseconds += update.Elapsed.TotalMilliseconds;
                }
            }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
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
