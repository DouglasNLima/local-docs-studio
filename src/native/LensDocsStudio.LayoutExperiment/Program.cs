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
        if (args.FirstOrDefault() == "--uia-client") return IndependentUiaClient.Run(args[1], int.Parse(args[2]), args[3], args[4], args[5]);
        if (args.FirstOrDefault() == "--studio-client") return StudioChecks.Client(args[1], int.Parse(args[2]), args[3]);
        if (args.FirstOrDefault() == "--media-client") return MediaChecks.Client(args[1], int.Parse(args[2]), long.Parse(args[3]), args[4]);
        if (args.FirstOrDefault() == "--range-client") return RangeOracleChecks.Client(args[1], int.Parse(args[2]), args[3], args[4]);
        if (args.FirstOrDefault() == "--link-client") return LinkChecks.Client(args[1], long.Parse(args[2]), args[3], args[4], args[5]);
        if (args.FirstOrDefault() == "--image-clipboard-client") return ImageClipboardChecks.Client(args[1], int.Parse(args[2]), args[3], args[4]);
        if (args.FirstOrDefault() == "--stock-serialisation-oracle")
        {
            try { _ = IndependentUiaClient.StockOracle(args[1]); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        var output = Path.GetFullPath(Option("--output") ?? "artifacts/layout-experiment/local"); Directory.CreateDirectory(output);
        Write(Path.Combine(output, "execution-identity.json"), new { Source = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_REVISION"), SourceDirty = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_DIRTY"),
            CompilationConfiguration = Environment.GetEnvironmentVariable("LENS_LAYOUT_COMPILATION") ?? "IL",
            HarnessSha256 = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)), Arguments = args, Process = Environment.ProcessId, UiThread = Environment.CurrentManagedThreadId,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, WpfAssembly = typeof(TextElement).Assembly.Location, CpuAvailable = Environment.ProcessorCount, Os = Environment.OSVersion.ToString() });
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
                if (args.Contains("--image-clipboard-checks")) await ImageClipboardChecks.Run(window, output);
                else if (args.Contains("--link-checks")) await LinkChecks.Run(window, output);
                else if (args.Contains("--visual-checks")) await VisualChecks.Run(window, output);
                else if (args.Contains("--range-checks")) await RangeOracleChecks.Run(window, output);
                else if (args.Contains("--media-controls")) await MediaControlChecks.Run(window, output);
                else if (args.Contains("--media-checks") || args.Contains("--media-legacy-directory"))
                { window.LegacyMediaLocationForChecks = args.Contains("--media-legacy-directory"); await MediaChecks.Run(window, output); }
                else if (args.Contains("--studio-checks")) await StudioChecks.Run(window, output);
                else if (args.Contains("--parity") || args.Contains("--parity-all")) await TargetedChecks.Run(window, output, args.Contains("--parity-all"), Option("--scenario"));
                else if (args.Contains("--appearance-worker-theme-control")) { window.WorkerThemeOwnershipForChecks = true; await AppearanceChecks.Run(window, output); }
                else if (args.Contains("--appearance-controls")) await AppearanceControlChecks.Run(window, output);
                else if (args.Contains("--appearance")) await AppearanceChecks.Run(window, output);
                else if (args.Contains("--studio"))
                {
                    var path = Option("--source"); var source = path is null ? ContinuousPreviewFixtures.Create(output, Option("--scenario") ?? "mixed")[0].Source : File.ReadAllText(path);
                    _ = new DiagnosticStudio(window, source, path is null ? Path.Combine(output, "diagnostic.md") : Path.GetFullPath(path)); return;
                }
                else if (args.Contains("--clipboard-oracle")) await ClipboardChecks.ProbeStock(window, output);
                else if (args.Contains("--lifecycle")) await LifecycleChecks.Run(window, output);
                else if (args.Contains("--view"))
                {
                    var source = Option("--source") is { } file ? File.ReadAllText(file) : ContinuousPreviewFixtures.Create(output, "list")[0].Source;
                    await window.Request(source); window.Surface.Focus(); return;
                }
                else
                {
                    var scenario = Option("--scenario") ?? "list";
                    var names = scenario == "all" ? ContinuousPreviewFixtures.Create(output, "all").Select(fixture => fixture.Name) : [scenario];
                    foreach (var name in names)
                    {
                        var destination = scenario == "all" ? Path.Combine(output, name) : output; Directory.CreateDirectory(destination);
                        try { await Measure(window, destination, Option("--adapter") ?? "candidate", int.Parse(Option("--repetitions") ?? "1"), args.Contains("--edit"), args.Contains("--acceptance"), args.Contains("--uia"), name, args.Contains("--acceptance-v2") || args.Contains("--contract-v2")); }
                        catch (Exception error) when (scenario == "all") { Write(Path.Combine(destination, "fixture-failure.json"), new { Status = "FAIL", Error = error.ToString() }); exit = 1; }

                    }
                    if (args.Contains("--acceptance-v2")) throw new IOException("v2 full acceptance remains incomplete pending the aggregate functional/appearance/lifecycle gate.");
                }
            }
            catch (Exception error) { Write(Path.Combine(output, "failure.json"), new { Status = "FAIL", Error = error.ToString() }); Console.Error.WriteLine(error); exit = 1; }
            finally { if (!args.Contains("--view") && !args.Contains("--studio")) { window.Close(); app.Shutdown(exit); } }
        };
        window.Closed += (_, _) => { if (args.Contains("--view") || args.Contains("--studio")) app.Shutdown(exit); };
        app.Run(window); return exit;
        string? Option(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    }

    private static int fixtureOrdinal;
    private static async Task Measure(ExperimentWindow window, string output, string adapter, int repetitions, bool edited, bool acceptance, bool uia, string scenario, bool acceptanceV2)
    {
        if (ExperimentAppearance.ActualHighContrast) throw new IOException("Controlled Light measurements require the actual OS normal appearance before either adapter starts.");
        if (adapter is not ("stock" or "candidate")) throw new ArgumentException("Choose stock or candidate.");
        var fixture = FixtureCatalog.Create(output, scenario)[0]; var samples = new List<object>();
        var ordinal = fixtureOrdinal++;
        var retained = new List<WeakReference>(); var failed = false;
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            // Original distant-source policy, including the appended heading/paragraph and edit zero.
            var source = edited ? fixture.Source.Replace(fixture.Last, fixture.Last + $" revised{repetition + 1}", StringComparison.Ordinal) + $"\n\n## Fresh revision {repetition + 1}\n\nChanged distant content Café 😀.\n" : fixture.Source;
            var sampleOutput = Path.Combine(output, "sample-" + repetition); Directory.CreateDirectory(sampleOutput);
            var sourceIo = Stopwatch.StartNew(); var sourcePath = Path.Combine(output, "fixture.md"); File.WriteAllText(sourcePath, source); window.SourcePath = sourcePath; window.WorkspaceRoot = output; // Source I/O is explicitly outside readiness.
            var sourceIoMilliseconds = sourceIo.Elapsed.TotalMilliseconds; var hostSetup = Stopwatch.StartNew();
            window.Content = window.Scroller; window.Surface.Install(null); window.Scroller.ScrollToTop(); window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var hostSetupMilliseconds = hostSetup.Elapsed.TotalMilliseconds; var readinessStartTimestamp = Stopwatch.GetTimestamp();
            var allocated = GC.GetTotalAllocatedBytes(); var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var watch = Stopwatch.StartNew(); var gaps = new List<double>(); var lastInput = 0d; var uiThread = Environment.CurrentManagedThreadId;
            var inputTimeline = new List<object>();
            void Input() { var now = watch.Elapsed.TotalMilliseconds; gaps.Add(now - lastInput); inputTimeline.Add(new { Timestamp = Stopwatch.GetTimestamp(), GapMilliseconds = now - lastInput }); lastInput = now; }
            var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => Input(); timer.Start();
            var parsing = Stopwatch.StartNew();
            ListModel? model = null; ParsedDocument? parsed = null; int preparationThread = 0;
            if (adapter == "candidate")
            {
                var theme = await ThemeGeometry.Prepare("Light", CancellationToken.None);
                model = await ExperimentWindow.Worker(() => { preparationThread = Environment.CurrentManagedThreadId; return CompleteLayout.Parse(source, CancellationToken.None, sourcePath, output, preparedTheme: theme); });
            }
            else parsed = await Task.Run(() => { preparationThread = Environment.CurrentManagedThreadId; return new MarkdownService().Parse(source); });
            var parsingMilliseconds = parsing.Elapsed.TotalMilliseconds;
            double boundary, complete, construction = 0, workerLayout = 0; int worker = uiThread;
            FlowDocumentScrollViewer? stock = null; PreviewResult? result = null; LayoutSnapshot? snapshot = null;
            if (adapter == "candidate")
            {
                boundary = 0; // Model preparation can contain required Mermaid layout; include it transparently.
                snapshot = await ExperimentWindow.Worker(() => CompleteLayout.Format(model!, 640, repetition + 1, "Light", CancellationToken.None));
                worker = snapshot.WorkerThread; workerLayout = snapshot.LayoutMilliseconds;
                window.InstallMeasured(snapshot); window.UpdateLayout();
                await window.Dispatcher.InvokeAsync(Input, DispatcherPriority.Input);
                complete = watch.Elapsed.TotalMilliseconds - boundary;
            }
            else
            {
                var prepared = await ExperimentWindow.Worker(() => StockPreparation.Create(parsed!, sourcePath, output));
                var constructionClock = Stopwatch.StartNew();
                result = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed!, false,
                    prepared.Diagrams, _ => { }, (_, _) => { }, preparedDrawings: prepared.Drawings, preparedImages: prepared.Images, codeSyntax: prepared.Syntax);
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
            var readiness = watch.Elapsed.TotalMilliseconds; timer.Stop(); var timingGaps = gaps.ToArray(); var endThread = Environment.CurrentManagedThreadId;
            var bytes = GC.GetTotalAllocatedBytes() - allocated; var gc = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - collections[i]).ToArray();
            Write(Path.Combine(sampleOutput, "timing-boundary.json"), new
            {
                Contract = acceptanceV2 ? AcceptanceV2.Contract : "historical-v1", Fixture = fixture.Name, Adapter = adapter, Repetition = repetition, Edited = edited,
                RunGroup = Environment.GetEnvironmentVariable("LENS_LAYOUT_RUN_GROUP"),
                SourceRevision = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_REVISION"), SourceDirty = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_DIRTY"),
                CompilationConfiguration = Environment.GetEnvironmentVariable("LENS_LAYOUT_COMPILATION") ?? "IL",
                HarnessSha256 = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)), FixtureSha256 = Hash(Encoding.UTF8.GetBytes(fixture.Source)), SourceSha256 = Hash(Encoding.UTF8.GetBytes(source)),
                CompleteLayoutInputMilliseconds = complete, EndToEndReadinessMilliseconds = readiness, ReadinessStartTimestamp = readinessStartTimestamp, SourceIoMilliseconds = sourceIoMilliseconds, EmptyHostSetupMilliseconds = hostSetupMilliseconds,
                InputGapsMilliseconds = timingGaps, UiStartThread = uiThread, ThemePreparationThread = model?.Theme.Thread, PreparationThread = preparationThread, WorkerLayoutThread = worker, UiEndThread = endThread, CandidateStages = snapshot?.Stages,
                AllocatedBytes = bytes, NaturalCollections = gc, Correctness = "NOT YET EVALUATED; later sample/correctness receipt is authoritative"
            });
            // From here onwards, independent correctness and capture are outside clocks.
            var originalList = scenario == "list" && !edited;
            var oracle = originalList ? adapter == "stock" ? CheckStock(stock!, result!, source) : CheckCandidate(window.Surface, snapshot!, source)
                : SemanticChecks.Check(adapter, fixture.Name, source, stock, result, snapshot);
            Capture((FrameworkElement?)stock ?? window.Scroller, Path.Combine(sampleOutput, "initial.png"));
            WindowCapture.Save(window, Path.Combine(sampleOutput, "shown-window.png"));
            Write(Path.Combine(sampleOutput, "geometry.json"), oracle);
            var interactionGaps = new List<double>(); var interactionAllocated = GC.GetTotalAllocatedBytes(); var interactionCollections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            if (uia && adapter == "candidate" && repetition == 0)
            {
                interactionAllocated = GC.GetTotalAllocatedBytes(); interactionCollections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                gaps.Clear(); lastInput = watch.Elapsed.TotalMilliseconds; timer.Start();
                using var acknowledgement = new InputAcknowledgement(window);
                var executable = Environment.ProcessPath!;
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var arg in new[] { "--uia-client", output, Environment.ProcessId.ToString(), Hash(Encoding.UTF8.GetBytes(snapshot!.Model.Text[..LayoutAutomationPeer.TextEnd(snapshot)])), scenario, acknowledgement.Name }) start.ArgumentList.Add(arg);
                using var client = (await Task.Run(() => Process.Start(start)))!;
                using var clientDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try { await client.WaitForExitAsync(clientDeadline.Token); }
                catch (OperationCanceledException) { client.Kill(true); throw new TimeoutException("Independent UIA client exceeded its external 60-second bound."); }
                finally { Write(Path.Combine(output, "input-acknowledgements.json"), acknowledgement.Receipts); }
                if (client.ExitCode != 0)
                {
                    Write(Path.Combine(output, "interaction-heartbeat-failure.json"), new { TimingInputGaps = timingGaps, InteractionInputGaps = gaps, InteractionObserved = uia && adapter == "candidate", InputTimeline = inputTimeline, InteractionStages = window.Surface.InteractionStages, InteractionAllocatedBytes = GC.GetTotalAllocatedBytes() - interactionAllocated, InteractionNaturalCollections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - interactionCollections[i]).ToArray(), CompleteLayout = complete, PreparationPresentation = readiness });
                    Write(Path.Combine(output, "provider-failure-state.json"), new { window.Surface.Anchor, window.Surface.Active, window.Surface.LastKey, window.Surface.KeyHistory, window.Surface.MouseHistory,
                        window.Surface.IsKeyboardFocused, FocusedElement = System.Windows.Input.Keyboard.FocusedElement?.GetType().Name });
                    throw new IOException("Independent UIA client failed; see retained evidence.");
                }
            }
            timer.Stop(); if (uia && adapter == "candidate") interactionGaps.AddRange(gaps);
            using var process = Process.GetCurrentProcess();
            var observation = new
            {
                Adapter = adapter, Repetition = repetition, Edited = edited, Fixture = fixture.Name, Contract = acceptanceV2 ? AcceptanceV2.Contract : "historical-v1",
                ProcessId = Environment.ProcessId, FixtureOrdinalInProcess = ordinal,
                RunGroup = Environment.GetEnvironmentVariable("LENS_LAYOUT_RUN_GROUP"),
                Temperature = repetition > 0 ? "Repeated complete fresh construction, same process" : ordinal == 0 ? "First fixture execution in fresh process; no prewarm" : "First construction for this fixture after earlier fixtures in the same process",
                FixtureSha256 = Hash(Encoding.UTF8.GetBytes(fixture.Source)), SourceSha256 = Hash(Encoding.UTF8.GetBytes(source)),
                HarnessSha256 = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)),
                CompilationConfiguration = Environment.GetEnvironmentVariable("LENS_LAYOUT_COMPILATION") ?? "IL",
                SourceRevision = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_REVISION"), SourceDirty = Environment.GetEnvironmentVariable("LENS_LAYOUT_SOURCE_DIRTY"),
                ProductSha256 = Hash(File.ReadAllBytes(typeof(NativeMarkdownRenderer).Assembly.Location)),
                HarnessInformationalVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Program).Assembly)?.InformationalVersion,
                WpfFrameworkSha256 = Hash(File.ReadAllBytes(typeof(TextElement).Assembly.Location)),
                WpfFormattingAssembly = typeof(System.Windows.Media.TextFormatting.TextFormatter).Assembly.Location,
                WpfFormattingSha256 = Hash(File.ReadAllBytes(typeof(System.Windows.Media.TextFormatting.TextFormatter).Assembly.Location)),
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, RuntimeAssembly = typeof(TextElement).Assembly.Location,
                HostMachine = Environment.MachineName, OS = Environment.OSVersion.ToString(), ImageOS = Environment.GetEnvironmentVariable("ImageOS"), ImageVersion = Environment.GetEnvironmentVariable("ImageVersion"),
                CpuAvailable = Environment.ProcessorCount, Cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                Width = 640, Height = 300, Dpi = VisualTreeHelper.GetDpi(window).PixelsPerInchX, Zoom = 100, Appearance = "Light", ActualOsHighContrast = ExperimentAppearance.ActualHighContrast,
                ReadinessStartTimestamp = readinessStartTimestamp, SourceIoMilliseconds = sourceIoMilliseconds, EmptyHostSetupMilliseconds = hostSetupMilliseconds, ParsingMilliseconds = parsingMilliseconds, StockConstructionMilliseconds = construction,
                CompleteLayoutInputMilliseconds = complete, WorkerFormattingAndGeometryMilliseconds = workerLayout,
                StockOriginalFirstLayoutMilliseconds = adapter == "stock" ? complete : (double?)null,
                CandidateBeforeFirstRequiredLayoutThroughInstallInputMilliseconds = adapter == "candidate" ? complete : (double?)null,
                EndToEndReadinessMilliseconds = readiness, UiStartThread = uiThread, ThemePreparationThread = model?.Theme.Thread, PreparationThread = preparationThread, WorkerLayoutThread = worker, UiEndThread = endThread,
                CandidateStages = snapshot?.Stages,
                InputGapsMilliseconds = timingGaps, MaximumInputGapMilliseconds = timingGaps.DefaultIfEmpty().Max(),
                InteractionObserved = uia && adapter == "candidate", InputTimeline = inputTimeline, InteractionStages = window.Surface.InteractionStages, InteractionInputGapsMilliseconds = interactionGaps, MaximumInteractionInputGapMilliseconds = interactionGaps.DefaultIfEmpty().Max(),
                V2 = AcceptanceV2.Rows(complete, readiness, timingGaps.Concat(interactionGaps).DefaultIfEmpty().Max(), false, false),
                DeferredRequiredLayout = 0, ListLogicalStyleAndSourceChecks = "PASS", FullParity = "INCOMPLETE", CompleteLayoutTiming = complete <= (acceptanceV2 ? 1000 : 250) ? "PASS" : "FAIL", Future250StretchOnly = complete <= 250 ? "MET" : "NOT_MET",
                AllocatedBytes = bytes, NaturalCollections = gc, InteractionAllocatedBytes = uia ? GC.GetTotalAllocatedBytes() - interactionAllocated : (long?)null, InteractionNaturalCollections = uia ? Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - interactionCollections[i]).ToArray() : null, ManagedBytes = GC.GetTotalMemory(false),
                process.WorkingSet64, process.PrivateMemorySize64, ForcedCollection = false
            };
            samples.Add(observation); Write(Path.Combine(sampleOutput, "sample.json"), observation);
            Console.WriteLine($"{adapter} repetition {repetition}: complete layout {complete:F2} ms; readiness {readiness:F2} ms");
            failed |= acceptanceV2 ? complete > 1000 || readiness > 1500 || timingGaps.Concat(interactionGaps).DefaultIfEmpty().Max() > 100 : complete > 250 || timingGaps.DefaultIfEmpty().Max() >= 250;
            retained.Add(new WeakReference((object?)snapshot ?? result!.Document));
            if (stock is not null) stock.Document = null; window.Surface.Install(null); window.Content = window.Scroller;
        }
        Write(Path.Combine(output, "results.json"), new { Status = failed ? "FAIL" : "TIMING_ONLY_PASS", FullAcceptance = "INCOMPLETE_PENDING_PARITY", Samples = samples,
            Retention = new { Created = retained.Count, AliveAfterNaturalCollections = retained.Count(item => item.IsAlive), ForcedCollection = false } });
        if (acceptanceV2) { Write(Path.Combine(output, "acceptance-v2.json"), new { Contract = AcceptanceV2.Contract, Timing = failed ? "FAIL" : "PASS", FullAcceptance = "INCOMPLETE", Missing = "Full independent parity and actual OS appearance/lifecycle certification" });
            }
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
