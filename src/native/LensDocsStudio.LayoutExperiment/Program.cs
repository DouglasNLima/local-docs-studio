using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    internal static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    internal static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--uia-client") return IndependentUiaClient.Run(args[1], int.Parse(args[2]), args[3]);
        var output = Path.GetFullPath(Option("--output") ?? "artifacts/layout-experiment/local"); Directory.CreateDirectory(output);
        if (args.Contains("--deterministic"))
        {
            try { DeterministicChecks.Run(output); return 0; }
            catch (Exception error) { Write(Path.Combine(output, "deterministic.json"), new { Status = "FAIL", Error = error.ToString() }); Console.Error.WriteLine(error); return 1; }
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new ExperimentWindow(); var exit = 0; var started = false;
        window.ContentRendered += async (_, _) =>
        {
            if (started) return; started = true;
            try
            {
                if (args.Contains("--lifecycle")) await LifecycleChecks.Run(window, output);
                else if (args.Contains("--view"))
                {
                    var source = Option("--source") is { } file ? File.ReadAllText(file) : ContinuousPreviewFixtures.Create(output, "list")[0].Source;
                    await window.Request(source); window.Surface.Focus(); return;
                }
                else await Measure(window, output, Option("--adapter") ?? "candidate", int.Parse(Option("--repetitions") ?? "1"), args.Contains("--edit"), args.Contains("--acceptance"), args.Contains("--uia"));
            }
            catch (Exception error) { Write(Path.Combine(output, "failure.json"), new { Status = "FAIL", Error = error.ToString() }); Console.Error.WriteLine(error); exit = 1; }
            finally { if (!args.Contains("--view")) { window.Close(); app.Shutdown(exit); } }
        };
        window.Closed += (_, _) => { if (args.Contains("--view")) app.Shutdown(exit); };
        app.Run(window); return exit;
        string? Option(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    }

    private static async Task Measure(ExperimentWindow window, string output, string adapter, int repetitions, bool edited, bool acceptance, bool uia)
    {
        if (adapter is not ("stock" or "candidate")) throw new ArgumentException("Choose stock or candidate.");
        var fixture = ContinuousPreviewFixtures.Create(output, "list")[0]; var samples = new List<object>();
        var retained = new List<WeakReference>(); var failed = false;
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            // Distant replacement preserves list shape and all original source bytes in
            // non-edited cases. Each edit is a genuinely different parsed document.
            var source = edited ? fixture.Source.Replace(fixture.Last, fixture.Last + $" revised{repetition + 1}", StringComparison.Ordinal) : fixture.Source;
            var sampleOutput = Path.Combine(output, "sample-" + repetition); Directory.CreateDirectory(sampleOutput);
            window.Content = window.Scroller; window.Surface.Install(null); window.Scroller.ScrollToTop(); window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var allocated = GC.GetTotalAllocatedBytes(); var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var watch = Stopwatch.StartNew(); var gaps = new List<double>(); var lastInput = 0d; var uiThread = Environment.CurrentManagedThreadId;
            void Input() { var now = watch.Elapsed.TotalMilliseconds; gaps.Add(now - lastInput); lastInput = now; }
            var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => Input(); timer.Start();
            var parsing = Stopwatch.StartNew();
            ListModel? model = null; ParsedDocument? parsed = null;
            if (adapter == "candidate") model = await ExperimentWindow.Worker(() => CompleteLayout.Parse(source, CancellationToken.None));
            else parsed = await Task.Run(() => new MarkdownService().Parse(source));
            var parsingMilliseconds = parsing.Elapsed.TotalMilliseconds;
            double boundary, complete, construction = 0, workerLayout = 0; int worker = uiThread;
            FlowDocumentScrollViewer? stock = null; PreviewResult? result = null; LayoutSnapshot? snapshot = null;
            if (adapter == "candidate")
            {
                boundary = watch.Elapsed.TotalMilliseconds;
                snapshot = await ExperimentWindow.Worker(() => CompleteLayout.Format(model!, 640, repetition + 1, "Light", CancellationToken.None));
                worker = snapshot.WorkerThread; workerLayout = snapshot.LayoutMilliseconds;
                window.Surface.Install(snapshot); window.UpdateLayout();
                await window.Dispatcher.InvokeAsync(Input, DispatcherPriority.Input);
                complete = watch.Elapsed.TotalMilliseconds - boundary;
            }
            else
            {
                var constructionClock = Stopwatch.StartNew();
                result = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed!, false,
                    new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
                construction = constructionClock.Elapsed.TotalMilliseconds;
                stock = new FlowDocumentScrollViewer { Width = 640, Height = 300, IsToolBarVisible = false, Background = Brushes.White };
                window.Content = stock; window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                boundary = watch.Elapsed.TotalMilliseconds;
                var delivery = window.Dispatcher.BeginInvoke(DispatcherPriority.Input, Input);
                stock.Document = result.Document;
                stock.Measure(new Size(640, 300)); stock.Arrange(new Rect(0, 0, 640, 300)); stock.UpdateLayout();
                await delivery.Task; complete = watch.Elapsed.TotalMilliseconds - boundary;
            }
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Input();
            var readiness = watch.Elapsed.TotalMilliseconds; timer.Stop(); var endThread = Environment.CurrentManagedThreadId;
            var bytes = GC.GetTotalAllocatedBytes() - allocated; var gc = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - collections[i]).ToArray();
            // From here onwards, independent correctness and capture are outside clocks.
            var oracle = adapter == "stock" ? CheckStock(stock!, result!, source) : CheckCandidate(window.Surface, snapshot!, source);
            Capture((FrameworkElement?)stock ?? window.Scroller, Path.Combine(sampleOutput, "initial.png"));
            WindowCapture.Save(window, Path.Combine(sampleOutput, "shown-window.png"));
            Write(Path.Combine(sampleOutput, "geometry.json"), oracle);
            if (uia && adapter == "candidate" && repetition == 0)
            {
                var executable = Environment.ProcessPath!;
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var arg in new[] { "--uia-client", output, Environment.ProcessId.ToString(), Hash(Encoding.UTF8.GetBytes(snapshot!.Model.Text)) }) start.ArgumentList.Add(arg);
                using var client = Process.Start(start)!;
                using var clientDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try { await client.WaitForExitAsync(clientDeadline.Token); }
                catch (OperationCanceledException) { client.Kill(true); throw new TimeoutException("Independent UIA client exceeded its external 60-second bound."); }
                if (client.ExitCode != 0)
                {
                    Write(Path.Combine(output, "provider-failure-state.json"), new { window.Surface.Anchor, window.Surface.Active, window.Surface.LastKey, window.Surface.KeyHistory,
                        window.Surface.IsKeyboardFocused, FocusedElement = System.Windows.Input.Keyboard.FocusedElement?.GetType().Name });
                    throw new IOException("Independent UIA client failed; see retained evidence.");
                }
            }
            using var process = Process.GetCurrentProcess();
            var observation = new
            {
                Adapter = adapter, Repetition = repetition, Edited = edited, Fixture = "list",
                Temperature = repetition == 0 ? "First fixture execution in fresh process; no prewarm" : "Repeated complete fresh construction, same process",
                FixtureSha256 = Hash(Encoding.UTF8.GetBytes(fixture.Source)), SourceSha256 = Hash(Encoding.UTF8.GetBytes(source)),
                HarnessSha256 = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)),
                SourceRevision = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_REVISION"),
                ProductSha256 = Hash(File.ReadAllBytes(typeof(NativeMarkdownRenderer).Assembly.Location)),
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, RuntimeAssembly = typeof(TextElement).Assembly.Location,
                OS = Environment.OSVersion.ToString(), ImageOS = Environment.GetEnvironmentVariable("ImageOS"), ImageVersion = Environment.GetEnvironmentVariable("ImageVersion"),
                CpuAvailable = Environment.ProcessorCount, Cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                Width = 640, Height = 300, Dpi = VisualTreeHelper.GetDpi(window).PixelsPerInchX, Zoom = 100, Appearance = "Light",
                ParsingMilliseconds = parsingMilliseconds, StockConstructionMilliseconds = construction,
                CompleteLayoutInputMilliseconds = complete, WorkerFormattingAndGeometryMilliseconds = workerLayout,
                StockOriginalFirstLayoutMilliseconds = adapter == "stock" ? complete : (double?)null,
                CandidateBeforeFirstRequiredLayoutThroughInstallInputMilliseconds = adapter == "candidate" ? complete : (double?)null,
                EndToEndReadinessMilliseconds = readiness, UiStartThread = uiThread, WorkerLayoutThread = worker, UiEndThread = endThread,
                CandidateStages = snapshot?.Stages,
                InputGapsMilliseconds = gaps, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(),
                DeferredRequiredLayout = 0, Correctness = "PASS", CompleteLayoutTiming = complete <= 250 ? "PASS" : "FAIL",
                AllocatedBytes = bytes, NaturalCollections = gc, ManagedBytes = GC.GetTotalMemory(false),
                process.WorkingSet64, process.PrivateMemorySize64, ForcedCollection = false
            };
            samples.Add(observation); Write(Path.Combine(sampleOutput, "sample.json"), observation);
            Console.WriteLine($"{adapter} repetition {repetition}: complete layout {complete:F2} ms; readiness {readiness:F2} ms");
            failed |= complete > 250 || gaps.DefaultIfEmpty().Max() >= 250;
            retained.Add(new WeakReference((object?)snapshot ?? result!.Document));
            if (stock is not null) stock.Document = null; window.Surface.Install(null); window.Content = window.Scroller;
        }
        Write(Path.Combine(output, "results.json"), new { Status = failed ? "FAIL" : "TIMING_ONLY_PASS", FullAcceptance = "INCOMPLETE_PENDING_PARITY", Samples = samples,
            Retention = new { Created = retained.Count, AliveAfterNaturalCollections = retained.Count(item => item.IsAlive), ForcedCollection = false } });
        if (acceptance) throw new IOException("Full prototype acceptance is incomplete until timing and all required parity/responsiveness gates pass.");
    }

    private static object CheckCandidate(LayoutSurface surface, LayoutSnapshot snapshot, string source)
    {
        var expected = string.Join("\r\n", Enumerable.Range(1, 4000).Select(i => source.Contains("word4000 revised", StringComparison.Ordinal) && i == 4000
            ? source[(source.LastIndexOf("word4000", StringComparison.Ordinal))..].TrimEnd('*') : "word" + i)) + "\r\n";
        if (snapshot.Model.Text != expected || snapshot.Model.Items.Length != 4000 || snapshot.Lines.Length < 4000 || snapshot.Model.Source != source)
            throw new IOException("Candidate complete logical text/list assertion failed.");
        foreach (var item in snapshot.Model.Items)
        foreach (var run in item.Runs)
            if (!run.Bold || source.Substring(run.SourceStart, run.Text.Length) != run.Text) throw new IOException("Actual style/source oracle failed.");
        var last = snapshot.Model.Items[^1]; surface.Select(last.Start, last.Start + last.Length);
        if (surface.SelectionSourceStart != source.LastIndexOf("word4000", StringComparison.Ordinal) || surface.SelectionSourceEnd != source.Length - 2)
            throw new IOException("Final item exact source range failed.");
        surface.Select(0, 0);
        return new { LogicalText = snapshot.Model.Text, SemanticLists = 1, Items = snapshot.Model.Items.Length, snapshot.Height,
            Lines = snapshot.Lines.Select(line => new { line.Start, line.End, line.Item, line.Top, line.Height, line.Baseline, Carets = line.Carets,
                Bounds = line.Clusters.SelectMany(cluster => cluster.Bounds).Select(bounds => new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height }) }) };
    }
    private static object CheckStock(FlowDocumentScrollViewer viewer, PreviewResult result, string source)
    {
        var list = result.Document.Blocks.OfType<System.Windows.Documents.List>().Single();
        if (list.ListItems.Count != 4000 || !result.ReadText().Contains("word4000", StringComparison.Ordinal)) throw new IOException("Stock complete list failed.");
        var scrolling = (ScrollViewer)viewer.Template.FindName("PART_ContentHost", viewer);
        viewer.Selection.Select(result.Document.ContentStart, result.Document.ContentEnd); viewer.Focus();
        System.Windows.Input.ApplicationCommands.Copy.Execute(null, viewer);
        var clipboardFormats = Clipboard.GetDataObject()?.GetFormats(false) ?? [];
        var items = list.ListItems.Select((item, index) =>
        {
            var paragraph = (Paragraph)item.Blocks.FirstBlock; var run = paragraph.Inlines.OfType<Run>().Single();
            var rectangle = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var last = run.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            if (run.FontWeight != FontWeights.Bold || !source.Contains(run.Text, StringComparison.Ordinal)) throw new IOException("Stock styled text failed.");
            return new { Item = index, Text = run.Text, IsEmpty = rectangle.IsEmpty, X = rectangle.X, Top = rectangle.Top, Height = rectangle.Height, EndX = last.X, paragraph.LineHeight,
                Margin = paragraph.Margin.ToString(), SourceStart = source.IndexOf(run.Text + "**", StringComparison.Ordinal) };
        }).ToArray();
        viewer.Selection.Select(result.Document.ContentStart, result.Document.ContentStart);
        return new { LogicalText = new TextRange(result.Document.ContentStart, result.Document.ContentEnd).Text,
            SemanticLists = 1, Items = items.Length, Height = scrolling.ExtentHeight, ClipboardFormats = clipboardFormats, Lines = items };
    }
    internal static void Capture(FrameworkElement target, string path)
    {
        var bitmap = new RenderTargetBitmap(640, 300, 96, 96, PixelFormats.Pbgra32);
        var backing = new DrawingVisual(); using (var context = backing.RenderOpen()) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 640, 300));
        bitmap.Render(backing); bitmap.Render(target);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
}
