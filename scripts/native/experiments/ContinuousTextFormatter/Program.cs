using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.ContinuousTextFormatter;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    [STAThread]
    private static int Main(string[] args)
    {
        string? evidenceOutput = null;
        try
        {
            var output = Path.GetFullPath(args[1]);
            if (Directory.Exists(output)) throw new IOException("Use a new evidence directory; unsuccessful samples are retained.");
            Directory.CreateDirectory(output);
            evidenceOutput = output;
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var fixtures = ContinuousPreviewFixtures.Create(output, "all");
            foreach (var fixture in fixtures) File.WriteAllText(Path.Combine(output, fixture.Name + ".md"), fixture.Source, new UTF8Encoding(false));
            Write(output, "fixtures.json", fixtures.Select(fixture => new {
                fixture.Name, Sha256 = ListSnapshot.Hash(fixture.Source), Utf8Bytes = Encoding.UTF8.GetByteCount(fixture.Source),
                PrototypeScope = fixture.Name == "list" ? "LIST COST SPIKE ONLY" : "NOT IMPLEMENTED"
            }));
            if (args[0] == "self-check") { Check(output); return 0; }
            if (args[0] is not ("baseline" or "prototype")) throw new ArgumentException("Choose baseline, prototype or self-check.");
            var count = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 1;
            if (count is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(args));
            var edited = args.Contains("--edited");
            var fixtureSource = fixtures.Single(fixture => fixture.Name == "list").Source;
            var failures = 0;
            for (var repetition = 0; repetition < count; repetition++)
            {
                var source = edited ? fixtureSource.Replace("word4000", "word4000 revised" + (repetition + 1), StringComparison.Ordinal)
                    // Preserve a list-only source grammar; this diagnostic revision is not
                    // the protected harness's six-fixture fresh-source acceptance group.
                    + "\n- **Changed distant content Café 😀 é " + (repetition + 1) + "**" : fixtureSource;
                if (!Sample(args[0], output, source, fixtureSource, repetition, edited)) failures++;
            }
            Write(output, "result.json", new {
                Classification = "CONTINUOUS_NATIVE_PROTOTYPE_PARTIAL", Samples = count, OverBudgetOrIncorrect = failures,
                FullAcceptance = false, Selection = "NOT IMPLEMENTED", Copy = "NOT IMPLEMENTED", Accessibility = "NOT IMPLEMENTED / NOT TESTED",
                Note = "The process preserves over-budget samples. A successful process exit means diagnostic completion, not performance acceptance."
            });
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (evidenceOutput is not null)
            {
                try { Write(evidenceOutput, "failure.json", new { Status = "FAIL", Error = error.ToString(), Acceptance = "NOT ESTABLISHED" }); }
                catch (Exception evidenceError) { Console.Error.WriteLine("Cannot write failure receipt: " + evidenceError.Message); }
            }
            return 1;
        }
    }

    private static bool Sample(string engine, string output, string source, string original, int repetition, bool edited)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        File.WriteAllText(Path.Combine(output, $"source-{repetition}.md"), source, new UTF8Encoding(false));
        var readiness = Stopwatch.StartNew();
        var totalAllocation = GC.GetTotalAllocatedBytes();
        var parsing = Stopwatch.StartNew();
        var parsed = new MarkdownService().Parse(source); parsing.Stop();
        var viewer = new ScrollViewer { CanContentScroll = false, VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Brushes.White };
        var native = engine == "baseline" ? new FlowDocumentScrollViewer { IsToolBarVisible = false, Background = Brushes.White } : null;
        FrameworkElement viewport = native is not null ? native : viewer;
        using var host = new HwndSource(new HwndSourceParameters("Native list layout feasibility") {
            Width = 640, Height = 300, WindowStyle = unchecked((int)0x80000000) });
        host.RootVisual = viewport;
        Layout(); Pump(dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
        using var surface = new ListSurface();
        viewer.ScrollChanged += (_, _) => surface.SetViewport(viewer.VerticalOffset, viewer.ViewportHeight);
        PreviewResult? result = null;
        ListSnapshot? snapshot = null;
        var allocation = GC.GetTotalAllocatedBytes();
        var collections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
        var clock = Stopwatch.StartNew();
        var lastInput = 0d; var maxInput = 0d; var constructionInput = -1d;
        var phase = "construction";
        var operations = new List<object>();
        var started = new Dictionary<DispatcherOperation, (double At, string Phase)>();
        void Input() { var now = clock.Elapsed.TotalMilliseconds; maxInput = Math.Max(maxInput, now - lastInput); lastInput = now; }
        void Started(object? sender, DispatcherHookEventArgs e) => started[e.Operation] = (clock.Elapsed.TotalMilliseconds, phase);
        void Completed(object? sender, DispatcherHookEventArgs e)
        {
            if (started.Remove(e.Operation, out var start) && operations.Count < 512)
                operations.Add(new { start.Phase, Priority = e.Operation.Priority.ToString(), Milliseconds = clock.Elapsed.TotalMilliseconds - start.At });
        }
        dispatcher.Hooks.OperationStarted += Started; dispatcher.Hooks.OperationCompleted += Completed;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => Input(); timer.Start();
        try
        {
            var constructionCallback = dispatcher.BeginInvoke(DispatcherPriority.Input, () => { constructionInput = clock.Elapsed.TotalMilliseconds; Input(); });
            if (engine == "prototype") snapshot = ListSnapshot.Create(parsed, repetition + 1);
            else
            {
                var task = new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, new Dictionary<Markdig.Syntax.FencedCodeBlock, LensDocsStudio.Core.Rendering.DiagramResult>(), _ => { }, (_, _) => { });
                Pump(task); result = task.GetAwaiter().GetResult();
            }
            var construction = clock.Elapsed.TotalMilliseconds;
            Pump(constructionCallback.Task); Input(); phase = "attachment/full-layout";
            var layoutInput = -1d;
            var layoutStart = clock.Elapsed.TotalMilliseconds;
            var callback = dispatcher.BeginInvoke(DispatcherPriority.Input, () => { layoutInput = clock.Elapsed.TotalMilliseconds - layoutStart; Input(); });
            if (engine == "prototype") { surface.Attach(snapshot!); viewer.Content = surface; }
            else native!.Document = result!.Document;
            var attach = clock.Elapsed.TotalMilliseconds - layoutStart;
            var layoutWatch = Stopwatch.StartNew();
            Layout(); layoutWatch.Stop();
            Pump(callback.Task);
            phase = "application-idle";
            Pump(dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
            Input(); timer.Stop();
            dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
            var completion = clock.Elapsed.TotalMilliseconds;
            var endToEnd = readiness.Elapsed.TotalMilliseconds;
            var allocated = GC.GetTotalAllocatedBytes() - allocation;
            var endToEndAllocated = GC.GetTotalAllocatedBytes() - totalAllocation;
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var managed = GC.GetTotalMemory(false); var privateBytes = process.PrivateMemorySize64; var workingSet = process.WorkingSet64;
            var gc = Enumerable.Range(0, GC.MaxGeneration + 1).Select(i => GC.CollectionCount(i) - collections[i]).ToArray();
            var timedFormatCalls = surface.FormatCalls;
            var firstPng = $"{engine}-{repetition}-initial.png";
            Raster(viewport, Path.Combine(output, firstPng));
            // Correctness follows timing. The original renderer is an independent text/style/source oracle.
            if (snapshot is null) snapshot = ListSnapshot.Create(parsed, repetition + 1);
            // Equal post-clock oracle construction on both sides. It may affect
            // subsequent repeated-process temperature and is disclosed as such.
            var oracle = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, LensDocsStudio.Core.Rendering.DiagramResult>(), _ => { }, (_, _) => { });
            var correctness = Verify(snapshot, oracle);
            object geometry;
            if (engine == "prototype")
            {
                VerifyRetainedLines(surface, snapshot);
                var last = surface.Lines.Last();
                var lastX = last.Native.GetDistanceFromCharacterHit(new(last.Start, 0));
                var sourceHit = surface.HitSource(last, lastX);
                var expected = last.Paragraph.Spans[0].SourceStart;
                var bounds = last.Native.GetTextBounds(last.Start, Math.Max(1, last.Length));
                var caret = last.Native.GetNextCaretCharacterHit(new(last.Start, 0));
                viewer.ScrollToVerticalOffset(last.Origin.Y - 75); Layout();
                var visiblePoint = surface.TransformToAncestor(viewer).Transform(last.Origin);
                geometry = new { Complete = surface.Complete, surface.FullHeight, Lines = surface.Lines.Count,
                    surface.MarkerCount, TimedFormatCalls = timedFormatCalls, FormatCallsAfterNavigation = surface.FormatCalls,
                    CompleteTextBounds = surface.Lines.Sum(line => line.TextBounds.Count),
                    CompleteMarkerGeometry = surface.Lines.Count(line => line.Paragraph.Marker && line.Start == 0 && line.Ink.MarkerHeight > 0),
                    DistantSourceHit = sourceHit, ExpectedDistantSourceHit = expected, DistantBounds = bounds.Count,
                    CaretMoves = caret.FirstCharacterIndex + caret.TrailingLength > last.Start,
                    DistantVisible = visiblePoint.Y >= 0 && visiblePoint.Y + last.Native.Height <= 300,
                    CompletionIdentity = new { snapshot.Sha256, snapshot.Generation, Width = surface.ActualWidth, Dpi = VisualTreeHelper.GetDpi(surface).PixelsPerInchX,
                        Zoom = 100, Appearance = "Light", Paragraphs = snapshot.Paragraphs.Length, Markers = snapshot.Items.Length } };
                correctness &= sourceHit == expected && bounds.Count > 0 && surface.Complete && timedFormatCalls == surface.FormatCalls
                    && surface.Lines.All(line => line.Length == 0 || line.TextBounds.Count > 0)
                    && surface.Lines.Count(line => line.Paragraph.Marker && line.Start == 0 && line.Ink.MarkerHeight > 0) == snapshot.Items.Length
                    && surface.MarkerCount == snapshot.Items.Length && visiblePoint.Y >= 0 && visiblePoint.Y + last.Native.Height <= 300;
            }
            else
            {
                var last = result!.SourceElements.MaxBy(pair => pair.Key).Value;
                native!.Selection.Select(last.ContentStart, last.ContentEnd);
                var navigation = PreviewNavigationService.BringIntoViewAsync(native, last.ContentStart); Pump(navigation); Layout();
                var rectangle = last.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                geometry = new { Complete = true, NativePointerValid = last.ContentStart.HasValidLayout, DistantTop = rectangle.Top,
                    DistantSelected = native.Selection.Text.Contains(edited ? "Changed distant content" : "word4000", StringComparison.Ordinal) };
                correctness &= last.ContentStart.HasValidLayout && !rectangle.IsEmpty
                    && native.Selection.Text.Contains(edited ? "Changed distant content" : "word4000", StringComparison.Ordinal);
            }
            Raster(viewport, Path.Combine(output, $"{engine}-{repetition}-distant.png"));
            var withinBudget = layoutInput >= 0 && layoutInput <= 250 && constructionInput >= 0 && constructionInput < 250 && maxInput < 250;
            var modules = process.Modules.Cast<ProcessModule>().Where(module => module.ModuleName is "coreclr.dll" or "wpfgfx_cor3.dll")
                .Select(module => new { module.ModuleName, Path = module.FileName, Sha256 = FileHash(module.FileName) }).ToArray();
            Write(output, $"sample-{repetition}.json", new {
                Engine = engine, Classification = "DIAGNOSTIC_ONLY", ProtectedGateEligible = false,
                FixtureSha256 = ListSnapshot.Hash(original), SourceSha256 = snapshot.Sha256, SourceUtf8Bytes = Encoding.UTF8.GetByteCount(source),
                Repetition = repetition, Edited = edited, Temperature = repetition == 0 ? "First execution in fresh process; no prewarm" : "Repeated full reconstruction in same process",
                SourceRevision = Environment.GetEnvironmentVariable("LENS_CONTINUOUS_SOURCE_REVISION"),
                HarnessSha256 = FileHash(Assembly.GetExecutingAssembly().Location), NativeAssemblySha256 = FileHash(typeof(NativeMarkdownRenderer).Assembly.Location),
                ParsingMilliseconds = parsing.Elapsed.TotalMilliseconds, ConstructionMilliseconds = construction, ConstructionInputMilliseconds = constructionInput,
                AttachmentMilliseconds = attach, SynchronousLayoutMilliseconds = layoutWatch.Elapsed.TotalMilliseconds,
                NativeFormatLineMilliseconds = surface.FormattingMilliseconds,
                FirstLayoutInputMilliseconds = layoutInput, MaximumInputGapMilliseconds = maxInput, InputBudgetMilliseconds = 250,
                LayoutBudgetObservation = withinBudget ? "PASS" : "FAIL", Correctness = correctness ? "PASS" : "FAIL",
                CompletionMilliseconds = completion, EndToEndReadinessMilliseconds = endToEnd,
                AllocatedBytes = allocated, EndToEndAllocatedBytes = endToEndAllocated, ManagedBytesWithoutCollection = managed,
                PrivateMemoryBytes = privateBytes, WorkingSetBytes = workingSet, GarbageCollections = gc, ForcedCollections = false,
                LogicalTextSha256 = ListSnapshot.Hash(snapshot.Text), SourceIntervals = snapshot.Paragraphs.Sum(p => p.Spans.Length),
                Lists = snapshot.Lists.Length, Items = snapshot.Items.Length, Paragraphs = snapshot.Paragraphs.Length,
                Geometry = geometry, Operations = operations, DeferredRequiredTextFormatting = false,
                Selection = engine == "prototype" ? "NOT IMPLEMENTED" : "Original WPF selection",
                Copy = "NOT TESTED", Accessibility = "NOT TESTED; prototype has no document text provider",
                SemanticLimit = "List literals, bold/italic and nested positive-numbered lists only. This is not full interaction/layout completion under Goal #56.",
                Environment = new { OS = Environment.OSVersion.ToString(), Runtime = RuntimeInformation.FrameworkDescription,
                    Environment.ProcessorCount, ImageOS = Environment.GetEnvironmentVariable("ImageOS"), ImageVersion = Environment.GetEnvironmentVariable("ImageVersion"),
                    Width = 640, Height = 300, Zoom = 100, ActualHighContrast = SystemParameters.HighContrast,
                    WpfAssembly = typeof(FlowDocument).Assembly.Location, WpfSha256 = FileHash(typeof(FlowDocument).Assembly.Location), Modules = modules,
                    Context = "Hidden HWND; framework-dependent; no physical-display, screen-reader or production integration certification" }
            });
            Console.WriteLine($"{engine}/{repetition}: layout {layoutInput:F2} ms; Input gap {maxInput:F2} ms; correctness {correctness}; diagnostic budget {withinBudget}.");
            if (!correctness) throw new InvalidDataException("Independent list text/style/source/geometry correctness failed; retain the sample.");
            return withinBudget;
        }
        finally
        {
            timer.Stop(); dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
            if (native is not null) native.Document = null;
            viewer.Content = null; host.RootVisual = null;
        }
        void Layout() { viewport.Measure(new Size(640, 300)); viewport.Arrange(new Rect(0, 0, 640, 300)); viewport.UpdateLayout(); }
    }

    private static bool Verify(ListSnapshot snapshot, PreviewResult original)
    {
        var nativeParagraphs = new List<(Paragraph Element, int ListId, int ItemId, int Depth, int Number, bool Marker)>();
        var nativeLists = new List<ListNode>();
        var nativeItems = new List<(int ListId, int Number)>();
        void Walk(System.Windows.Documents.List list, int depth, int? parentItem)
        {
            var listId = nativeLists.Count;
            if (list.MarkerStyle is not (TextMarkerStyle.Disc or TextMarkerStyle.Decimal))
                throw new InvalidDataException("The original renderer has an unsupported native marker style.");
            nativeLists.Add(new(listId, parentItem, depth, list.MarkerStyle == TextMarkerStyle.Decimal, list.StartIndex, list.ListItems.Count));
            var number = list.StartIndex;
            foreach (var item in list.ListItems)
            {
                var itemId = nativeItems.Count;
                nativeItems.Add((listId, number));
                var first = true;
                foreach (var child in item.Blocks)
                    if (child is Paragraph paragraph) { nativeParagraphs.Add((paragraph, listId, itemId, depth, number, first)); first = false; }
                    else if (child is System.Windows.Documents.List nested) Walk(nested, depth + 1, itemId);
                    else throw new InvalidDataException("Unexpected child in the original native list.");
                number++;
            }
        }
        foreach (var block in original.Document.Blocks)
            if (block is System.Windows.Documents.List list) Walk(list, 0, null);
            else throw new InvalidDataException("Unexpected root structure in the original renderer.");
        if (original.ReadText() != snapshot.Text)
            throw new InvalidDataException("Logical text differs: " + JsonSerializer.Serialize(new { Actual = original.ReadText(), Expected = snapshot.Text }));
        if (original.Warnings.Count != 0 || !nativeLists.SequenceEqual(snapshot.Lists)
            || nativeItems.Count != snapshot.Items.Length || nativeParagraphs.Count != snapshot.Paragraphs.Length)
            throw new InvalidDataException("List, item or paragraph structure differs from the original renderer.");
        for (var index = 0; index < nativeItems.Count; index++)
            if (snapshot.Items[index].Id != index || nativeItems[index] != (snapshot.Items[index].ListId, snapshot.Items[index].Number))
                throw new InvalidDataException("Item-to-list membership or numbering differs from the original renderer.");
        for (var i = 0; i < snapshot.Paragraphs.Length; i++)
        {
            var paragraph = snapshot.Paragraphs[i];
            var actual = nativeParagraphs[i];
            if (actual.ListId != paragraph.ListId || actual.ItemId != paragraph.ItemId || actual.Depth != paragraph.Depth
                || actual.Number != paragraph.Number || actual.Marker != paragraph.Marker
                || actual.Element.Tag is not SourceReference reference || reference.Start != paragraph.SourceStart || reference.End + 1 != paragraph.SourceEnd)
                throw new InvalidDataException("Paragraph hierarchy, marker placement or source identity differs from the original renderer.");
            foreach (var span in paragraph.Spans)
            {
                if (!original.TextMap.TryPreviewRange(span.SourceStart, span.Text.Length, out var from, out var to)
                    || !original.TextMap.TrySourceRange(from!, to!, out var sourceStart, out var sourceLength)
                    || sourceStart != span.SourceStart || sourceLength != span.Text.Length)
                    throw new InvalidDataException("Exact literal source mapping differs at " + span.SourceStart);
                var range = new TextRange(from!, to!);
                // TextRange.Text manufactures the native list marker at an item start.
                // The whole-document oracle above checks those generated characters;
                // literal identity is checked directly in its original mapped Run.
                if (from!.GetOffsetToPosition(to!) != span.Text.Length
                    || !from.GetTextInRun(LogicalDirection.Forward).StartsWith(span.Text, StringComparison.Ordinal)
                    || !Equals(range.GetPropertyValue(TextElement.FontWeightProperty), span.Style.HasFlag(MarkdownEmphasis.Bold) ? FontWeights.Bold : FontWeights.Normal)
                    || !Equals(range.GetPropertyValue(TextElement.FontStyleProperty), span.Style.HasFlag(MarkdownEmphasis.Italic) ? FontStyles.Italic : FontStyles.Normal))
                    throw new InvalidDataException("Literal text or style differs at " + span.SourceStart + ": " + JsonSerializer.Serialize(new {
                        Actual = range.Text, Expected = span.Text, ExpectedStyle = span.Style.ToString(),
                        ActualWeight = range.GetPropertyValue(TextElement.FontWeightProperty).ToString(), ActualStyle = range.GetPropertyValue(TextElement.FontStyleProperty).ToString() }));
            }
        }
        return true;
    }

    private static void Check(string output)
    {
        var cases = new[] {
            "- **Repeated** plain *italic*\n- **Repeated** Café 😀 é",
            "7. **First**\n   - Nested\n   - Wrapped " + string.Join(" ", Enumerable.Repeat("Café 😀 é", 80)) + "\n8. Last",
            "- Windows\r\n- **Line endings**\r\n",
            "- **bold***italic*plain\n\n  Continuation paragraph\n\n  5. Nested numbered\n  6. Next nested\n\n- Last parent",
            "- Original\n- Revised distant source"
        };
        var receipts = new List<object>();
        foreach (var source in cases)
        {
            var parsed = new MarkdownService().Parse(source);
            var snapshot = ListSnapshot.Create(parsed, receipts.Count + 1);
            var original = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, LensDocsStudio.Core.Rendering.DiagramResult>(), _ => { }, (_, _) => { });
            if (!Verify(snapshot, original)) throw new InvalidDataException("The independent original-renderer oracle failed.");
            using var surface = new ListSurface();
            surface.Attach(snapshot); surface.Measure(new Size(250, 300)); surface.Arrange(new Rect(0, 0, 250, surface.FullHeight)); surface.UpdateLayout();
            VerifyRetainedLines(surface, snapshot);
            var count = surface.FormatCalls;
            foreach (var line in surface.Lines)
            {
                var distance = line.Native.GetDistanceFromCharacterHit(new(line.Start, 0));
                var hit = surface.HitSource(line, distance);
                if (hit < line.Paragraph.SourceStart || hit >= line.Paragraph.SourceEnd) throw new InvalidDataException("Native hit/source range escaped its paragraph.");
            }
            foreach (var paragraph in snapshot.Paragraphs)
            {
                var displayOffset = 0;
                for (var index = 0; index + 1 < paragraph.Spans.Length; index++)
                {
                    displayOffset += paragraph.Spans[index].Text.Length;
                    var line = surface.Lines.Last(value => value.Paragraph == paragraph && value.Start <= displayOffset);
                    var distance = line.Native.GetDistanceFromCharacterHit(new(displayOffset, 0));
                    if (surface.HitSource(line, distance) != paragraph.Spans[index + 1].SourceStart
                        || surface.HitSource(line, distance, false) != paragraph.Spans[index].SourceStart + paragraph.Spans[index].Text.Length)
                        throw new InvalidDataException("Exact style-boundary source affinity was lost.");
                }
            }
            if (!surface.Complete || surface.FormatCalls != count || surface.MarkerCount != snapshot.Items.Length)
                throw new InvalidDataException("Full geometry was deferred or lost markers.");
            var priorWidth = surface.FullHeight; surface.Measure(new Size(150, 300));
            if (!surface.Complete || surface.FullHeight < priorWidth) throw new InvalidDataException("Wrapping did not reformat the complete document.");
            var replacement = ListSnapshot.Create(new MarkdownService().Parse("- Replacement"), snapshot.Generation + 1);
            surface.Attach(replacement); surface.Measure(new Size(250, 300));
            if (surface.Snapshot != replacement || surface.Lines.Any(line => !replacement.Paragraphs.Contains(line.Paragraph)))
                throw new InvalidDataException("A replaced source retained the previous layout.");
            try { surface.Attach(snapshot); throw new InvalidDataException("A stale generation replaced the current source."); }
            catch (InvalidOperationException) { }
            receipts.Add(new { snapshot.Sha256, snapshot.Generation, Lists = snapshot.Lists.Length, Items = snapshot.Items.Length,
                LinesBeforeReplacement = count, TextStyleAndExactSourceOracle = "PASS", CompleteNativeGeometry = "PASS", ReplacementAndStaleGeneration = "PASS" });
        }
        var rejected = 0;
        foreach (var source in new[] { "# Heading", "- [Link](target.md)", "- &amp;", "- ~~strike~~" })
        {
            try { ListSnapshot.Create(new MarkdownService().Parse(source), 1); }
            catch (NotSupportedException) { rejected++; }
        }
        if (rejected != 4) throw new InvalidDataException("Unsupported content was silently accepted.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { ListSnapshot.Create(new MarkdownService().Parse("- cancelled"), 1, cancelled.Token); throw new InvalidDataException("Cancellation was ignored."); }
        catch (OperationCanceledException) { }
        Write(output, "checks.json", new { Status = "PASS", Cases = receipts, ExplicitUnsupportedRejections = rejected,
            CancelledPreparation = "PASS", Selection = "NOT IMPLEMENTED", ActualUIA = "NOT TESTED", ProductIntegration = "ABSENT" });
    }

    private static void VerifyRetainedLines(ListSurface surface, ListSnapshot snapshot)
    {
        foreach (var paragraph in snapshot.Paragraphs)
        {
            var covered = 0;
            foreach (var line in surface.Lines.Where(line => ReferenceEquals(line.Paragraph, paragraph)))
            {
                if (line.Start != covered || line.DisplayText != paragraph.Text)
                    throw new InvalidDataException("Actual native line text or wrapping coverage differs from the semantic oracle.");
                var runOffset = line.Start;
                foreach (var run in line.Native.GetTextRunSpans())
                {
                    var length = Math.Min(run.Length, line.Start + line.Length - runOffset);
                    if (length <= 0) continue; // Native end-of-paragraph is not a literal.
                    var spanOffset = 0;
                    var expected = paragraph.Spans.First(span => { var contains = runOffset < spanOffset + span.Text.Length; if (!contains) spanOffset += span.Text.Length; return contains; });
                    var properties = run.Value.Properties;
                    if (run.Value is not TextCharacters
                        || runOffset + length > spanOffset + expected.Text.Length
                        || properties.Typeface.Weight != (expected.Style.HasFlag(MarkdownEmphasis.Bold) ? FontWeights.Bold : FontWeights.Normal)
                        || properties.Typeface.Style != (expected.Style.HasFlag(MarkdownEmphasis.Italic) ? FontStyles.Italic : FontStyles.Normal)
                        || properties.Typeface.FontFamily.Source != "Segoe UI" || properties.FontRenderingEmSize != 15)
                        throw new InvalidDataException("Actual retained TextLine style differs from the original-renderer-verified spans at " + runOffset);
                    runOffset += length;
                }
                if (runOffset != line.Start + line.Length) throw new InvalidDataException("Actual native line lost text-run coverage.");
                var glyphCoverage = new bool[line.Length];
                foreach (var glyph in line.Native.GetIndexedGlyphRuns())
                {
                    var start = glyph.TextSourceCharacterIndex;
                    var characters = glyph.GlyphRun.Characters;
                    if (start < line.Start || start + glyph.TextSourceLength > line.Start + line.Length) continue; // Generated marker/end-of-paragraph.
                    if (characters is null || characters.Count != glyph.TextSourceLength
                        || !new string(characters.ToArray()).AsSpan().SequenceEqual(paragraph.Text.AsSpan(start, glyph.TextSourceLength)))
                        throw new InvalidDataException("Actual native glyph characters differ from the independent literal text oracle.");
                    for (var index = start; index < start + glyph.TextSourceLength; index++) glyphCoverage[index - line.Start] = true;
                }
                if (glyphCoverage.Any(value => !value)) throw new InvalidDataException("Actual native glyph text does not cover the complete line.");
                covered += line.Length;
            }
            if (covered != paragraph.Text.Length) throw new InvalidDataException("Required offscreen text is missing from retained native lines.");
        }
    }

    private static void Pump(Task task)
    {
        while (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static void Raster(Visual visual, string path)
    {
        var bitmap = new RenderTargetBitmap(640, 300, 96, 96, PixelFormats.Pbgra32);
        var backing = new DrawingVisual();
        using (var context = backing.RenderOpen()) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 640, 300));
        bitmap.Render(backing); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Json));
}
