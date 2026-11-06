using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Validation;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = true };
    [STAThread]
    private static int Main(string[] args)
    {
        // No local desktop input, fake hosted flags, environment prewarming or HC polling.
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||
            Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows")
        { Console.Error.WriteLine("Foreground UX measurements require a disposable GitHub-hosted Windows desktop."); return 2; }
        if (args.Length != 6) { Console.Error.WriteLine("Arguments: fixtures output fixture warm-repetitions appearance product-revision"); return 2; }
        var output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output)) throw new IOException("Evidence directories must be fresh.");
        Directory.CreateDirectory(output);
        NativeTestHighContrast? contrast = null;
        try
        {
            var fixtures = Path.GetFullPath(args[0]); var fixture = JsonSerializer.Deserialize<FixtureManifest>(File.ReadAllText(Path.Combine(fixtures, "manifest.json")), Json)!.Fixtures.Single(f => f.Id == args[2]);
            var bytes = File.ReadAllBytes(Path.Combine(fixtures, fixture.File));
            if (Hash(bytes) != fixture.Sha256.ToUpperInvariant()) throw new IOException("Fixture identity mismatch.");
            var source = new UTF8Encoding(false, true).GetString(bytes);
            var iterations = int.Parse(args[3]); if (iterations is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(iterations));
            var highContrast = args[4] == "high-contrast";
            if (args[4] is not ("normal" or "high-contrast")) throw new ArgumentException("Unknown appearance.");
            var product = typeof(MainWindow).Assembly;
            if (!product.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Contains(args[5], StringComparison.OrdinalIgnoreCase))
                throw new IOException("Loaded product assembly does not identify the prescribed source revision.");
            contrast = highContrast ? new NativeTestHighContrast() : null;
            var osContrast = NativeTestHighContrast.IsEnabledNow;
            if (osContrast != highContrast) throw new IOException("Actual OS appearance differs from the requested stratum.");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
            ThemeService.Apply(app.Resources, "System");
            // Preserve the first observed palette. A failing palette does not stop separate timing collection.
            var foreground = ((SolidColorBrush)app.Resources["PressedTextBrush"]).Color;
            var background = ((SolidColorBrush)app.Resources["PressedBrush"]).Color;
            var ratio = Contrast(foreground, background);
            Write(output, "appearance.json", new { highContrast, osContrast, wpfHighContrast = SystemParameters.HighContrast,
                foreground = foreground.ToString(), background = background.ToString(), ratio, required = 4.5,
                win32Highlight = Desktop.GetSysColor(13), win32Text = Desktop.GetSysColor(14),
                wpfHighlight = SystemColors.HighlightColor.ToString(), wpfText = SystemColors.HighlightTextColor.ToString(),
                verdict = highContrast ? (ratio >= 4.5 && foreground == SystemColors.HighlightTextColor ? "PASS" : "FAIL") : "NOT RUN",
                scope = "FIRST PROBE HOST PALETTE; distinct from the unchanged historical pre-assertion test" });
            var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
            File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: "System", Layout: "Editor", AutomaticUpdateChecks: false,
                Preview: new(ReadingMode: "Continuous"), ShowOutline: false)));
            var path = Path.Combine(output, fixture.File); File.WriteAllBytes(path, bytes);
            File.Copy(Path.Combine(fixtures, "pixel.png"), Path.Combine(output, "pixel.png"));
            var other = Path.Combine(output, "other.md"); File.WriteAllText(other, "# Other document\n\nOtherSource\n", new UTF8Encoding(false));
            var dialogs = new DesktopDialogs();
            var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
            var area = SystemParameters.WorkArea;
            var window = new MainWindow(model, dialogs, [other, path]) { Width = Math.Min(1280, area.Width), Height = Math.Min(800, area.Height),
                WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top };
            Write(output, "identity.json", new { schema = 1, productRevision = args[5], fixture = fixture.Id, fixture.Sha256, highContrast, iterations,
                harness = Identity(typeof(Program).Assembly), product = Identity(product), core = Identity(typeof(SessionStore).Assembly), wpf = Identity(typeof(Window).Assembly),
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, os = Environment.OSVersion.VersionString,
                processors = Environment.ProcessorCount, processId = Environment.ProcessId, stopwatchFrequency = Stopwatch.Frequency,
                sourceBytes = bytes.Length, sourceCharacters = source.Length, lineEnding = fixture.LineEnding,
                capabilities = new { slash = typeof(MainWindow).GetField("slashCommands", BindingFlags.NonPublic | BindingFlags.Instance) is not null,
                    comparison = typeof(MainWindow).GetField("comparisonSlots", BindingFlags.NonPublic | BindingFlags.Instance) is not null,
                    attachmentMarker = typeof(MainWindow).GetField("diagnosticRequest", BindingFlags.NonPublic | BindingFlags.Instance) is not null },
                image = Environment.GetEnvironmentVariable("ImageVersion"), run = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
                protocol = "foreground SendInput to semantic state plus captured desktop response; software observation upper bound, not physical scan-out" });
            var probe = new Probe(window, model, source, fixture, path, output, iterations);
            var started = Stopwatch.GetTimestamp(); window.Show();
            _ = Task.Run(async () =>
            {
                var code = 0;
                try { await probe.Run(started); }
                catch (Exception error) { code = 1; Write(output, "failure.json", new { verdict = "FAIL", error = error.ToString() }); await probe.FailureSnapshot(); Console.Error.WriteLine(error); }
                finally
                {
                    probe.Dispose();
                    try { contrast?.Dispose(); } catch (Exception error) { code = 1; Write(output, "restore-failure.json", new { error = error.ToString() }); }
                    // Disposable files only. Avoid dirty-document dialogues masking terminal evidence.
                    Environment.Exit(code);
                }
            });
            app.Run(); return 1;
        }
        catch (Exception error) { Write(output, "failure.json", new { verdict = "FAIL", error = error.ToString() }); contrast?.Dispose(); return 1; }
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static void Write(string output, string file, object value) => File.WriteAllText(Path.Combine(output, file), JsonSerializer.Serialize(value, Json));
    private static object Identity(Assembly assembly) => new { sha256 = Hash(File.ReadAllBytes(assembly.Location)), name = assembly.GetName().Name,
        version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion };
    private static double Contrast(Color a, Color b)
    {
        double L(Color c) { double C(byte v) { var s = v / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); } return .2126 * C(c.R) + .7152 * C(c.G) + .0722 * C(c.B); }
        var x = L(a); var y = L(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
    internal sealed record FixtureManifest(Fixture[] Fixtures);
    internal sealed record Fixture(string Id, string File, string Sha256, string LineEnding, string[] ExpectedTokens, int ListItems, int TableRows, int Diagrams, int Images);
}

internal sealed class Probe : IDisposable
{
    private readonly MainWindow window;
    private readonly WorkspaceViewModel model;
    private readonly string source, path, output;
    private readonly Program.Fixture fixture;
    private readonly int iterations;
    private readonly TextEditor editor;
    private readonly DocumentPreview preview;
    private readonly StreamWriter journal;
    private readonly StageListener stages = new();
    private readonly List<object> dispatcherIntervals = [];
    private readonly Dictionary<DispatcherOperation, long> operations = [];
    private readonly CancellationTokenSource stopped = new();
    private Rect windowBounds;
    private IntPtr windowHandle;
    private int sampleCount, pngCount;
    private double heartbeatMaximum;
    private bool intervalOverflow;
    private long lastActionStart;
    private Task? heartbeat;
    private string currentAction = "startup";
    private const int ActionSeconds = 30;

    internal Probe(MainWindow window, WorkspaceViewModel model, string source, Program.Fixture fixture, string path, string output, int iterations)
    {
        this.window = window; this.model = model; this.source = source; this.fixture = fixture; this.path = path; this.output = output; this.iterations = iterations;
        editor = (TextEditor)window.FindName("Editor"); preview = (DocumentPreview)window.FindName("Preview");
        journal = new StreamWriter(Path.Combine(output, "samples.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
        window.Dispatcher.Hooks.OperationStarted += OperationStarted;
        window.Dispatcher.Hooks.OperationCompleted += OperationCompleted;
    }
    private async Task<T> Ui<T>(Func<T> action) => await window.Dispatcher.InvokeAsync(action, DispatcherPriority.Input).Task.WaitAsync(TimeSpan.FromSeconds(ActionSeconds));
    private async Task Ui(Action action) => await Ui(() => { action(); return true; });
    private async Task Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        do { if (await Ui(condition)) return; await Task.Delay(8); } while (clock.Elapsed.TotalSeconds < ActionSeconds);
        throw new TimeoutException("Required semantic response was not observed within 30 seconds.");
    }
    private T? Field<T>(string name) => (T?)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        if (root is Visual or System.Windows.Media.Media3D.Visual3D)
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Tree(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private FrameworkElement Control(string id) => Tree(window).OfType<FrameworkElement>().First(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == id);
    private bool FindQueryReady(string query, int matches) => ((TextBox)window.FindName("FindText")).Text == query &&
        (window.FindName("FindCounter") is not TextBlock counter || counter.Text.Contains($"of {matches}", StringComparison.Ordinal));
    private static Rect Bounds(FrameworkElement element)
    {
        var start = element.PointToScreen(new Point(0, 0)); var end = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new Rect(start, end);
    }
    private async Task PointAt(FrameworkElement element)
    {
        // A model layout flag can precede arranged input geometry. Set up the
        // next action against the settled visible control, without forcing layout.
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task.WaitAsync(TimeSpan.FromSeconds(ActionSeconds));
        await Until(() => element.IsVisible && element.IsMeasureValid && element.IsArrangeValid && element.ActualWidth > 0 && element.ActualHeight > 0);
        Desktop.Move(await Ui(() => { var b = Rect.Intersect(Bounds(element), Desktop.ClientBounds(windowHandle));
            if (b.IsEmpty) throw new IOException("Input target has no visible client geometry.");
            return new Point(b.X + b.Width / 2, b.Y + b.Height / 2); }));
        // SetCursorPos returning does not establish that WPF has processed the
        // native pointer move. Require the actual target's mouse-over state.
        await Until(() => element.IsMouseOver);
    }
    private async Task FocusEditor()
    {
        currentAction = "setup-focus-editor";
        await PointAt(editor.TextArea.TextView); Desktop.Click(); await Until(() => editor.IsKeyboardFocusWithin);
    }
    private async Task ClickControl(string id) { await PointAt(await Ui(() => Control(id))); Desktop.Click(); }
    private void Record(object record) => journal.WriteLine(JsonSerializer.Serialize(record, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    private async Task<double> Measure(string action, string temperature, int iteration, Action input, Func<bool> response, bool requirePixelChange = true, long? origin = null)
    {
        currentAction = action;
        windowBounds = Desktop.ClientBounds(windowHandle);
        var before = Desktop.Capture(windowBounds);
        var cpu = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(false); var gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var started = origin ?? Stopwatch.GetTimestamp();
        lastActionStart = started;
        input();
        Desktop.Frame after;
        long semantic;
        var clock = Stopwatch.StartNew();
        do
        {
            await Until(response); semantic = Stopwatch.GetTimestamp();
            // Let the normal WPF render queue run. No UpdateLayout, synthetic event or hidden HWND.
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task.WaitAsync(TimeSpan.FromSeconds(ActionSeconds));
            windowBounds = Desktop.ClientBounds(windowHandle);
            after = Desktop.Capture(windowBounds);
            if (!requirePixelChange || after.Sha256 != before.Sha256) break;
            await Task.Delay(8);
        } while (clock.Elapsed.TotalSeconds < ActionSeconds);
        var ended = Stopwatch.GetTimestamp();
        if (requirePixelChange && after.Sha256 == before.Sha256) throw new IOException($"{action}: semantic change had no observed screen response.");
        if (!await Ui(response)) throw new IOException($"{action}: semantic result did not persist through screen capture.");
        var process = Process.GetCurrentProcess(); process.Refresh();
        var elapsed = Stopwatch.GetElapsedTime(started, ended).TotalMilliseconds;
        Record(new { kind = "sample", action, temperature, iteration, status = "OBSERVED", elapsedMs = elapsed,
            startTick = started, semanticTick = semantic, endTick = ended, semanticMs = Stopwatch.GetElapsedTime(started, semantic).TotalMilliseconds,
            screenSha256 = after.Sha256, screenWidth = after.Width, screenHeight = after.Height, changedPixels = after.Sha256 != before.Sha256, foregroundVerified = true,
            cpuMs = process.TotalProcessorTime.TotalMilliseconds - cpu, allocations = GC.GetTotalAllocatedBytes(false) - allocated,
            gc = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gc[i]).ToArray(), privateBytes = process.PrivateMemorySize64,
            workingSet = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false), observerIncluded = true });
        sampleCount++;
        if (iteration == 0 && pngCount++ < 40) Desktop.Save(after, Path.Combine(output, $"{sampleCount:D4}-{action}.png"));
        return elapsed;
    }
    internal async Task Run(long startup)
    {
        await Until(() => window.IsLoaded && !model.IsBusy && model.ActiveDocument?.EditorDocument.Text == source && editor.IsVisible);
        await Ui(() => { window.Activate(); Desktop.SetForegroundWindow(new WindowInteropHelper(window).Handle); });
        Desktop.OwnsForeground(); windowHandle = await Ui(() => new WindowInteropHelper(window).Handle); windowBounds = Desktop.ClientBounds(windowHandle);
        Record(new { kind = "startup", elapsedMs = Stopwatch.GetElapsedTime(startup).TotalMilliseconds, scope = "Show to exact buffer; screen verification follows", status = "OBSERVED" });
        Record(new { kind = "environment", dpi = await Ui(() => VisualTreeHelper.GetDpi(window).PixelsPerInchX), width = windowBounds.Width, height = windowBounds.Height,
            readingMode = await Ui(() => preview.ViewingMode.ToString()), zoom = await Ui(() => preview.Zoom) });
        heartbeat = Heartbeat();
        await FocusEditor(); Desktop.Key(0x24, 0x11); await Until(() => editor.CaretOffset == 0);
        // First render remains the first construction of this document in a fresh process.
        await PointAt(await Ui(() => Control("LayoutSplit")));
        await Measure("first-usable-preview", "cold-render", 0, Desktop.Click, Usable);
        var renderingStart = lastActionStart;
        await Complete(renderingStart, "cold-render");
        for (var i = 0; i < iterations; i++) await Interactions(i);
        await FeatureInteractions();
        await RenderingInput();
        await Cancellation();
        if (Program.Hash(File.ReadAllBytes(path)) != fixture.Sha256.ToUpperInvariant()) throw new IOException("Measured interactions changed the fixture file.");
        if (!await Ui(() => model.Documents.Single(d => d.FilePath == path).EditorDocument.Text == source)) throw new IOException("The original document buffer was not restored exactly.");
        stopped.Cancel(); if (heartbeat is not null) await heartbeat;
        var intervals = await Ui(() => { window.Dispatcher.Hooks.OperationStarted -= OperationStarted; window.Dispatcher.Hooks.OperationCompleted -= OperationCompleted; return dispatcherIntervals.ToArray(); });
        // Freeze product diagnostics before the deliberately blocked observer control.
        // Its imposed delay must never inflate product dispatcher/heartbeat evidence.
        await VerifyDelayedResponse();
        Program.Write(output, "completion.json", new { collection = "PASS", acceptance = "PENDING", sampleCount,
            exactSource = "PASS", completeContent = "PASS", historical250ms = "UNCHANGED / NOT EVALUATED BY THIS PROBE",
            heartbeatMaximumMs = heartbeatMaximum, dispatcherIntervals = intervals, intervalOverflow,
            scope = "Actual compiled MainWindow hosted by a validation process; not installed application startup or physical-machine certification" });
    }
    private bool CurrentPreviewReady()
    {
        var result = Field<PreviewResult>("rendered");
        if (result is null || preview.Document != result.Document || !preview.IsVisible ||
            Field<Guid?>("renderedDocumentId") != model.ActiveDocument?.Id ||
            Field<ICSharpCode.AvalonEdit.Document.ITextSourceVersion>("renderedVersion") is not { } version || editor.Document.Version.CompareAge(version) != 0 ||
            (window.FindName("PreviewContentHost") is FrameworkElement contentHost && contentHost.Visibility != Visibility.Visible) ||
            AutomationProperties.GetItemStatus(preview).StartsWith("Busy", StringComparison.Ordinal) ||
            !preview.IsMeasureValid || !preview.IsArrangeValid) return false;
        if (UIElementAutomationPeer.CreatePeerForElement(preview)?.GetPattern(PatternInterface.Text) is not ITextProvider provider) return false;
        return provider.GetVisibleRanges().Any(range => !string.IsNullOrWhiteSpace(range.GetText(128)) && range.GetBoundingRectangles().Length >= 4);
    }
    private bool Usable() => UsableMarker("ReadableStart");
    private bool UsableMarker(string marker)
    {
        if (!CurrentPreviewReady()) return false;
        var result = Field<PreviewResult>("rendered")!;
        var offset = editor.Text.IndexOf(marker, StringComparison.Ordinal);
        if (!result.TextMap.TryPreviewRange(offset, marker.Length, out var first, out var last) || first is null || last is null ||
            !first.HasValidLayout || new TextRange(first, last).Text != marker) return false;
        var rect = first.GetCharacterRect(LogicalDirection.Forward);
        return rect.Width >= 0 && rect.Height > 0 && rect.Top >= 0 && rect.Bottom <= preview.ActualHeight &&
            UIElementAutomationPeer.CreatePeerForElement(preview)?.GetPattern(PatternInterface.Text) is not null;
    }
    private async Task Complete(long started, string temperature)
    {
        currentAction = "complete-readiness";
        var facts = await Ui(() =>
        {
            var result = Field<PreviewResult>("rendered") ?? throw new IOException("No rendered document.");
            var text = result.ReadText();
            if (fixture.ExpectedTokens.Any(token => !text.Contains(token, StringComparison.Ordinal))) throw new IOException("Complete preview text omitted a fixture token.");
            foreach (var literal in result.TextMap.Literals)
                if (!literal.Start.HasValidLayout || !literal.End.HasValidLayout) throw new IOException("Complete source-map geometry is unavailable.");
            var counts = new Dictionary<string, int>();
            void Count(TextElement item)
            {
                var key = item.GetType().Name; counts[key] = counts.GetValueOrDefault(key) + 1;
                switch (item)
                {
                    case Section s: foreach (var b in s.Blocks) Count(b); break;
                    case System.Windows.Documents.List l: foreach (var b in l.ListItems) Count(b); break;
                    case ListItem l: foreach (var b in l.Blocks) Count(b); break;
                    case Table t: foreach (var g in t.RowGroups) foreach (var r in g.Rows) { counts["TableRow"] = counts.GetValueOrDefault("TableRow") + 1; foreach (var c in r.Cells) Count(c); } break;
                    case TableCell c: foreach (var b in c.Blocks) Count(b); break;
                    case Paragraph p: foreach (var b in p.Inlines) Count(b); break;
                    case Span s: foreach (var b in s.Inlines) Count(b); break;
                }
            }
            foreach (var block in result.Document.Blocks) Count(block);
            if (counts.GetValueOrDefault("ListItem") != fixture.ListItems || counts.GetValueOrDefault("TableRow") != fixture.TableRows)
                throw new IOException("Native table/list structure differs from the independent fixture manifest.");
            if (result.Warnings.Count > 0) throw new IOException("Preview warnings preclude complete content qualification: " + string.Join("; ", result.Warnings));
            if (result.Media.Count < fixture.Diagrams + fixture.Images || result.Media.Any(m => m.Image.Source is null || m.Image.ActualWidth <= 0 || m.Image.ActualHeight <= 0))
                throw new IOException("Diagram or local image geometry is incomplete.");
            if (!result.TextMap.TryPreviewRange(source.IndexOf("ReadableEnd", StringComparison.Ordinal), 11, out var from, out var to) || from is null || to is null ||
                new TextRange(from, to).Text != "ReadableEnd") throw new IOException("Distant source mapping is incomplete.");
            return new { logicalTextSha256 = Program.Hash(Encoding.UTF8.GetBytes(text)), counts, mappedLiterals = result.TextMap.Literals.Count, media = result.Media.Count };
        });
        // The distant operation is actual keyboard Find, not a programmatic selection proxy.
        await ClickControl("LayoutPreview"); await Until(() => model.Layout == "Preview");
        await PointAt(preview); Desktop.Click(); await Until(() => preview.IsKeyboardFocusWithin);
        Desktop.Key(0x46, 0x11); await Until(() => ((FrameworkElement)window.FindName("FindText")).IsVisible);
        Desktop.Text("ReadableEnd"); await Until(() => FindQueryReady("ReadableEnd", 1));
        Desktop.Key(0x0D);
        await Until(() => preview.Selection.Text == "ReadableEnd" && preview.Selection.Start.HasValidLayout &&
            preview.Selection.Start.GetCharacterRect(LogicalDirection.Forward).Top >= 0 &&
            preview.Selection.Start.GetCharacterRect(LogicalDirection.Forward).Bottom <= preview.ActualHeight);
        windowBounds = Desktop.ClientBounds(windowHandle);
        var frame = Desktop.Capture(windowBounds);
        Record(new { kind = "sample", action = "complete-readiness", temperature, iteration = 0, status = "OBSERVED",
            elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, startTick = started, endTick = Stopwatch.GetTimestamp(),
            facts, screenSha256 = frame.Sha256, foregroundVerified = true, observerIncluded = true,
            scope = "Conservative verified upper bound including full-tree audit and real distant Find/selection" });
        sampleCount++;
        Desktop.Save(frame, Path.Combine(output, "complete-distant-selection.png"));
        Desktop.Key(0x1B); await Until(() => !((FrameworkElement)window.FindName("FindBar")).IsVisible);
        await ClickControl("LayoutEditor"); await Until(() => model.Layout == "Editor");
    }
    private async Task Interactions(int i)
    {
        var temperature = i == 0 ? "first-interaction" : "warm";
        await FocusEditor(); Desktop.Key(0x24, 0x11); await Until(() => editor.CaretOffset == 0);
        await Measure("typing", temperature, i, () => Desktop.Text("x"), () => editor.Text == "x" + source && editor.CaretOffset == 1);
        await Measure("caret", temperature, i, () => Desktop.Key(0x27), () => editor.CaretOffset == 2 && editor.SelectionLength == 0);
        await Measure("selection", temperature, i, () => Desktop.Key(0x27, 0x10), () => editor.SelectionStart == 2 && editor.SelectionLength == 1);
        Desktop.Key(0x24, 0x11); Desktop.Key(0x2E); await Until(() => editor.Text == source); // Restore with real input; no buffer replacement.
        await Measure("find-open", temperature, i, () => Desktop.Key(0x46, 0x11), () => ((FrameworkElement)window.FindName("FindText")).IsVisible);
        Desktop.Key(0x41, 0x11); Desktop.Text("NeedleOne"); await Until(() => FindQueryReady("NeedleOne", 2));
        var caret = await Ui(() => editor.CaretOffset);
        // Use the shared visible Next control across all exact products. The
        // historical Enter observation remains a failed, separate keyboard case.
        await PointAt(await Ui(() => Control("FindNext")));
        await Measure("find-navigation", temperature, i, Desktop.Click, () => editor.SelectedText == "NeedleOne" && editor.CaretOffset != caret);
        Desktop.Key(0x1B); await FocusEditor(); Desktop.Key(0x24, 0x11);
        await Until(() => editor.TextArea.TextView.VisualLinesValid && editor.TextArea.TextView.ScrollOffset.Y < 1);
        if (await Ui(() => editor.ExtentHeight > editor.ViewportHeight))
        {
            // The HC template can leave TextEditor.VerticalOffset at zero while
            // the rendered TextView has moved. Observe the actual visible text view.
            await Measure("scroll", temperature, i, () => Desktop.Wheel(-120), () => editor.TextArea.TextView.VisualLinesValid && editor.TextArea.TextView.ScrollOffset.Y > 1);
            Record(await Ui(() => new { kind = "scroll-position", iteration = i, textViewOffset = editor.TextArea.TextView.ScrollOffset.Y,
                editorWrapperOffset = editor.VerticalOffset, firstVisibleLine = editor.TextArea.TextView.VisualLines.First().FirstDocumentLine.LineNumber }));
        }
        else Record(new { kind = "coverage", action = "scroll", status = "NOT APPLICABLE", reason = "Document fits the editor viewport" });
        var active = await Ui(() => model.ActiveDocument);
        await Measure("document-switch", temperature, i, () => Desktop.Key(0x09, 0x11), () => model.ActiveDocument != active && editor.Text.Contains("OtherSource", StringComparison.Ordinal));
        Desktop.Key(0x09, 0x11); await Until(() => model.ActiveDocument == active && editor.Text == source);
        await Measure("outline-open", temperature, i, () => Desktop.Key(0x4F, 0x11, 0x12), () => ((FrameworkElement)window.FindName("OutlinePanel")).IsVisible &&
            Tree((DependencyObject)window.FindName("Outline")).OfType<TextBlock>().Any(text => text.IsVisible && text.Text.Contains("ReadableStart", StringComparison.Ordinal)));
        await Measure("outline-close", temperature, i, () => Desktop.Key(0x4F, 0x11, 0x12), () => !((FrameworkElement)window.FindName("OutlinePanel")).IsVisible);
        foreach (var layout in new[] { "Split", "Preview", "Editor" })
        {
            await PointAt(await Ui(() => Control("Layout" + layout)));
            await Measure("layout-" + layout.ToLowerInvariant(), temperature, i, Desktop.Click,
                () => model.Layout == layout && (layout == "Editor" ? editor.IsVisible : CurrentPreviewReady()));
        }
    }
    private async Task FeatureInteractions()
    {
        var slash = await Ui(() => Field<object>("slashCommands"));
        if (slash is null) Record(new { kind = "coverage", action = "slash", status = "NOT RUN", reason = "Feature absent in this exact product" });
        else
        {
            await FocusEditor(); Desktop.Key(0x23, 0x11);
            Desktop.Key(0x0D); await Until(() => editor.CaretOffset == editor.Text.Length);
            var before = await Ui(() => editor.Text);
            var commands = await Ui(() => (ListBox)slash.GetType().GetProperty("CommandList")!.GetValue(slash)!);
            await Measure("slash-open", "first-interaction", 0, () => Desktop.Text("/"), () => (bool)slash.GetType().GetProperty("IsOpen")!.GetValue(slash)! &&
                commands.IsVisible && commands.IsArrangeValid && commands.ActualHeight > 20 && commands.SelectedIndex >= 0 && PresentationSource.FromVisual(commands) is not null);
            Desktop.Text("heading 2"); await Until(() => editor.Text.EndsWith("/heading 2", StringComparison.Ordinal));
            await Measure("slash-execute", "first-interaction", 0, () => Desktop.Key(0x0D), () => editor.Text == before + "## ");
            Desktop.Key(0x5A, 0x11); await Until(() => editor.Text == before + "/heading 2");
            Desktop.Key(0x59, 0x11); await Until(() => editor.Text == before + "## ");
            // Select the exact inserted suffix through real keyboard selection and restore it.
            var added = await Ui(() => editor.Text.Length - source.Length);
            Desktop.Key(0x23, 0x11); await Until(() => editor.CaretOffset == editor.Text.Length && editor.SelectionLength == 0);
            // One caret movement crosses CRLF atomically; UTF-16 length is not a key count.
            for (var n = 0; n < added && await Ui(() => editor.SelectionStart > source.Length); n++)
            {
                var previous = await Ui(() => editor.SelectionStart);
                Desktop.Key(0x25, 0x10); await Until(() => editor.SelectionStart < previous);
            }
            if (!await Ui(() => editor.SelectionStart == source.Length && editor.SelectionLength == added))
                throw new IOException("Keyboard restoration did not select exactly the added suffix.");
            Desktop.Key(0x08); await Until(() => editor.Text == source);
        }
        if (typeof(MainWindow).GetField("comparisonSlots", BindingFlags.NonPublic | BindingFlags.Instance) is null)
        { Record(new { kind = "coverage", action = "comparison-switch", status = "NOT RUN", reason = "Feature absent in this exact product" }); return; }
        currentAction = "comparison-open";
        // Use the displayed access keys. Popup coordinates can be stale until their
        // separate HWND has arranged; reading a MenuItem is not proof of click delivery.
        Desktop.Key(0x46, 0x12);
        await Until(() => ((Menu)window.FindName("ApplicationMenu")).Items.OfType<MenuItem>().First().IsSubmenuOpen);
        Desktop.Key(0x42);
        // B is also used by Import Markdown bundle: Windows selects the first
        // matching item and requires Enter to invoke it.
        await Until(() => System.Windows.Input.Keyboard.FocusedElement is MenuItem item && AutomationProperties.GetAutomationId(item) == "OpenBeside");
        Desktop.Key(0x0D);
        await Until(() => Field<object>("comparisonSlots") is not null && Tree(window).OfType<FrameworkElement>().Any(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "ActivateComparison1"));
        var original = await Ui(() => model.ActiveDocument);
        await PointAt(await Ui(() => Control("ActivateComparison1")));
        await Measure("comparison-switch", "first-interaction", 0, Desktop.Click, () => model.ActiveDocument != original && editor.Text.Contains("OtherSource", StringComparison.Ordinal) && UsableMarker("OtherSource"));
        await ClickControl("ActivateComparison0"); await Until(() => model.ActiveDocument == original);
        await ClickControl("CloseComparison"); await Until(() => Field<object>("comparisonSlots") is null);
        await ClickControl("LayoutEditor"); await Until(() => model.Layout == "Editor");
    }
    private async Task RenderingInput()
    {
        foreach (var stage in new[] { "started", "request/attachment-start" })
        {
            var action = stage == "started" ? "typing-during-construction" : "typing-during-attachment";
            if (stage != "started" && typeof(MainWindow).GetField("diagnosticRequest", BindingFlags.NonPublic | BindingFlags.Instance) is null)
            { Record(new { kind = "coverage", action, status = "NOT RUN", reason = "Exact historical product has no attachment marker" }); continue; }
            await ClickControl("LayoutSplit");
            await Until(() => preview.IsVisible && Field<ICSharpCode.AvalonEdit.Document.ITextSourceVersion>("renderedVersion") is { } version &&
                editor.Document.Version.CompareAge(version) == 0 && preview.IsMeasureValid && preview.IsArrangeValid);
            await FocusEditor(); Desktop.Key(0x24, 0x11); await Until(() => editor.CaretOffset == 0);
            // A real source edit schedules preview work while the editor keeps focus.
            // The next input is offered at the renderer marker, without asking the UI
            // thread whether it is idle. Any non-preemptible layout delay is retained.
            stages.Arm(stage); Desktop.Text("x"); var stageTick = await stages.Wait(TimeSpan.FromSeconds(ActionSeconds));
            var sent = Stopwatch.GetTimestamp(); Desktop.Text("y");
            await Measure(action, "render-load", 0, () => { }, () => editor.Text == "xy" + source && editor.CaretOffset == 2, false, sent);
            Record(new { kind = "render-load", action, stage, stageTick, inputTick = sent, status = "OBSERVED" });
            Desktop.Key(0x24, 0x11); Desktop.Key(0x2E); Desktop.Key(0x2E); await Until(() => editor.Text == source);
            await ClickControl("LayoutEditor"); await Until(() => model.Layout == "Editor");
        }
    }
    private async Task Cancellation()
    {
        // Invalidate via real input, then interrupt the next actual construction from a worker.
        await FocusEditor(); Desktop.Key(0x24, 0x11); Desktop.Text("x"); await Until(() => editor.Text == "x" + source);
        Desktop.Key(0x08); await Until(() => editor.Text == source);
        var active = await Ui(() => model.ActiveDocument);
        stages.Arm("started"); await ClickControl("LayoutSplit");
        var stage = await stages.Wait(TimeSpan.FromSeconds(ActionSeconds));
        // No Dispatcher request between the render marker and input: avoids coordinated omission.
        var sent = Stopwatch.GetTimestamp(); Desktop.Key(0x09, 0x11);
        await Measure("cancel-render-by-switch", "warm", 0, () => { }, () => model.ActiveDocument != active && editor.Text.Contains("OtherSource", StringComparison.Ordinal), false, sent);
        await Until(() => model.ActiveDocument != active && UsableMarker("OtherSource"));
        Record(new { kind = "cancellation", status = "PASS", triggeredAtStageTick = stage, inputTick = sent,
            originalBufferPreserved = await Ui(() => active!.EditorDocument.Text == source),
            stalePreviewRejected = true, replacementReadyMs = Stopwatch.GetElapsedTime(sent).TotalMilliseconds,
            scope = "Observed current, readable replacement after a switch offered at construction start; not proof of an unfinished-operation abort in every short-document trial" });
        Desktop.Key(0x09, 0x11); await Until(() => model.ActiveDocument == active);
        await ClickControl("LayoutEditor"); await Until(() => model.Layout == "Editor");
    }
    private async Task Heartbeat()
    {
        try
        {
            while (!stopped.IsCancellationRequested)
            {
                await Task.Delay(16, stopped.Token); var started = Stopwatch.GetTimestamp();
                await Ui(() => { }); heartbeatMaximum = Math.Max(heartbeatMaximum, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!stopped.IsCancellationRequested) Record(new { kind = "observer-error", error = error.Message }); }
    }
    private async Task VerifyDelayedResponse()
    {
        // Separate observer control, after all performance samples: no prewarming
        // or artificial delay is applied to a product measurement.
        await FocusEditor(); Desktop.Key(0x24, 0x11); await Until(() => editor.CaretOffset == 0);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = window.Dispatcher.BeginInvoke(() => { entered.SetResult(); if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Observer control release failed."); });
        await entered.Task;
        try
        {
            var delay = await Measure("observer-delay-control", "control", 0, () =>
            {
                Desktop.Text("x"); _ = Task.Run(async () => { await Task.Delay(200); release.Set(); });
            }, () => editor.Text == "x" + source && editor.CaretOffset == 1);
            if (delay < 180) throw new IOException("The observer omitted the deliberately blocked input interval.");
            Record(new { kind = "observer-control", status = "PASS", delayMs = delay, imposedDelayMs = 200 });
        }
        finally { release.Set(); }
        Desktop.Key(0x08); await Until(() => editor.Text == source);
    }
    internal async Task FailureSnapshot()
    {
        try
        {
            var state = await window.Dispatcher.InvokeAsync(() => new
            {
                action = currentAction, layout = model.Layout, previewStatus = model.PreviewStatus,
                activeFile = model.ActiveDocument?.FilePath, editor.CaretOffset, editor.SelectionStart, editor.SelectionLength,
                editor.VerticalOffset, editor.ExtentHeight, editor.ViewportHeight,
                textViewOffset = editor.TextArea.TextView.ScrollOffset.Y, visibleLinesValid = editor.TextArea.TextView.VisualLinesValid,
                firstVisibleLine = editor.TextArea.TextView.VisualLinesValid ? editor.TextArea.TextView.VisualLines.FirstOrDefault()?.FirstDocumentLine.LineNumber : null,
                bufferSha256 = Program.Hash(Encoding.UTF8.GetBytes(editor.Text)), tail = editor.Text[^Math.Min(80, editor.Text.Length)..],
                focusedType = System.Windows.Input.Keyboard.FocusedElement?.GetType().FullName,
                hoveredType = System.Windows.Input.Mouse.DirectlyOver?.GetType().FullName,
                capturedType = System.Windows.Input.Mouse.Captured?.GetType().FullName,
                editorBounds = Bounds(editor).ToString(), textViewBounds = Bounds(editor.TextArea.TextView).ToString(),
                pointer = System.Windows.Input.Mouse.GetPosition(window).ToString(),
                previewText = preview.Selection?.Text, previewVisible = preview.IsVisible,
                findCounter = (window.FindName("FindCounter") as TextBlock)?.Text,
                findText = (window.FindName("FindText") as TextBox)?.Text, searchPreview = Field<bool>("searchPreview"),
                status = model.Status, modifiers = System.Windows.Input.Keyboard.Modifiers.ToString(), window.Left, window.Top, window.ActualWidth, window.ActualHeight
            }, DispatcherPriority.Input).Task.WaitAsync(TimeSpan.FromSeconds(2));
            Program.Write(output, "failure-state.json", state);
            Desktop.Save(Desktop.Capture(Desktop.ClientBounds(windowHandle)), Path.Combine(output, "failure.png"));
        }
        catch (Exception error) { Program.Write(output, "failure-capture.json", new { error = error.Message }); }
    }
    private void OperationStarted(object? sender, DispatcherHookEventArgs e)
    { if (operations.Count < 4096) operations[e.Operation] = Stopwatch.GetTimestamp(); else intervalOverflow = true; }
    private void OperationCompleted(object? sender, DispatcherHookEventArgs e)
    {
        if (!operations.Remove(e.Operation, out var start)) return;
        var end = Stopwatch.GetTimestamp(); var duration = Stopwatch.GetElapsedTime(start, end).TotalMilliseconds;
        if (duration < 50) return;
        if (dispatcherIntervals.Count < 2048) dispatcherIntervals.Add(new { start, end, durationMs = duration, priority = e.Operation.Priority.ToString(), scope = "Inclusive dispatcher operation, possibly nested; not exclusive CPU" });
        else intervalOverflow = true;
    }
    public void Dispose() { stopped.Cancel(); stages.Dispose(); journal.Dispose(); }
}

internal sealed class StageListener : EventListener
{
    private string? expected;
    private TaskCompletionSource<long>? completion;
    protected override void OnEventSourceCreated(EventSource source)
    { if (source.Name == "LensDocsStudio-Native-PreviewConstruction") EnableEvents(source, EventLevel.Verbose); }
    internal void Arm(string stage) { expected = stage; completion = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    protected override void OnEventWritten(EventWrittenEventArgs e)
    { if (e.Payload?.FirstOrDefault()?.ToString() == expected) completion?.TrySetResult(Stopwatch.GetTimestamp()); }
    internal Task<long> Wait(TimeSpan timeout) => completion!.Task.WaitAsync(timeout);
}
