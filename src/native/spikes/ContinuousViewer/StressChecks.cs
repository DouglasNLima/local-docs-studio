using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using ContinuousViewer.Model;
using LensDocsStudio.Core.Parsing;

namespace ContinuousViewer;

internal static class StressChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var observations = new List<object>();
        var longText = string.Concat(Enumerable.Repeat("Native documentation preserves every word. ", 8192)) + "Final retained target.";
        var sources = new Dictionary<string, string>
        {
            ["paragraph"] = longText, ["table-cell"] = "| Content |\n| --- |\n| " + longText + " |",
            ["quote"] = "> " + longText, ["list-item"] = "- " + longText,
            ["code-block"] = "```text\n" + longText + "\n```",
            ["details"] = "<details open>\n<summary>Long details</summary>\n\n" + longText + "\n\n</details>"
        };
        foreach (var (name, source) in sources)
        {
            var model = LogicalDocument.Build(new MarkdownService().Parse(source));
            using var viewer = new VirtualDocumentView(); using var host = HarnessSupport.Host(viewer);
            HarnessSupport.Layout(viewer); HarnessSupport.Idle(); using var input = new InputProbe();
            var first = input.Measure(() => { viewer.SetDocument(model); HarnessSupport.Layout(viewer); });
            var watch = Stopwatch.StartNew(); var navigate = viewer.NavigateAsync(source.LastIndexOf("Final retained target.", StringComparison.Ordinal), true);
            HarnessSupport.Pump(navigate); HarnessSupport.Layout(viewer); input.Pulse(); var navigation = watch.Elapsed.TotalMilliseconds;
            var copied = viewer.Selection!.Copy().UnicodeText;
            var sizeBounded = model.Pieces.All(p => p.Length <= LogicalDocument.PieceCharacters);
            var peer = (VirtualDocumentPeer)UIElementAutomationPeer.CreatePeerForElement(viewer);
            var bounds = peer.GetSelection().Single().GetBoundingRectangles().Length > 0;
            input.Dispose();
            var semanticPass = sizeBounded && copied.Contains("Final retained target.", StringComparison.Ordinal) && navigate.Result && bounds && model.Parsed.Source == source;
            observations.Add(new { name, logicalPieces = model.Pieces.Length, actualContainers = VirtualDocumentView.Descendants<ListBoxItem>(viewer).Count(),
                sizeBounded, sourcePreserved = model.Parsed.Source == source, targetVisible = navigate.Result, bounds, semanticPass,
                selectedPiece = model.AtSource(source.LastIndexOf("Final retained target.", StringComparison.Ordinal)).Id, selectedText = copied,
                finalPieceRuns = model.Pieces[^1].Runs, firstPieceRuns = model.Pieces[Math.Min(1, model.Pieces.Length - 1)].Runs,
                firstLayoutInputMilliseconds = first, worstInputMilliseconds = input.Worst, navigationMilliseconds = navigation,
                inputAcceptance = first < 250 && input.Worst < 250 ? "PASS" : "FAIL", viewer.MaximumPieceLayoutMilliseconds });
            Save(); HarnessSupport.Raster(viewer, Path.Combine(output, name + ".png"));
            if (!semanticPass) throw new IOException("An oversized native semantic block lost content, bounds or target visibility: " + name);
        }
        using (var viewer = new VirtualDocumentView())
        using (var host = HarnessSupport.Host(viewer))
        {
            var source = FixtureCatalogue.All.Single(f => f.Name == "rich").Source;
            var model = LogicalDocument.Build(new MarkdownService().Parse(source)); viewer.SetDocument(model); HarnessSupport.Layout(viewer); HarnessSupport.Idle();
            var target = model.Parsed.Targets.Single(t => t.Id == "distant");
            var internalLink = model.Pieces.SelectMany(p => p.Runs).First(r => r.Link == "#distant");
            viewer.ActivateLinkAt(internalLink.Start); HarnessSupport.Idle();
            var internalTarget = model.AtSource(target.SourceStart);
            var peer = (VirtualDocumentPeer)UIElementAutomationPeer.CreatePeerForElement(viewer);
            var internalVisible = peer.Range(internalTarget.Start, internalTarget.End).GetBoundingRectangles().Length > 0;
            string? wikiActivation = null; viewer.LinkActivated += value => wikiActivation = value;
            var wiki = model.Pieces.SelectMany(p => p.Runs).First(r => r.Link?.StartsWith("wiki:", StringComparison.Ordinal) == true);
            viewer.ActivateLinkAt(wiki.Start);
            var literalOffset = viewer.SourceAt(internalTarget.Start + 3);
            observations.Add(new { name = "internal fragment / wiki callback / preview source mapping", internalVisible,
                wikiActivation, mappedSourceOffset = literalOffset, exactLiteralMapping = source[literalOffset] == model.Text[internalTarget.Start + 3],
                scope = "Actual native navigation and the same source-follow entry point; wiki activation is delegated to a host callback, with no external file opened." }); Save();
            var targetAnchor = viewer.CaptureAnchor(); viewer.History(true); HarnessSupport.Layout(viewer); var previousAnchor = viewer.CaptureAnchor();
            viewer.History(false); HarnessSupport.Layout(viewer); var restored = viewer.CaptureAnchor();
            observations.Add(new { name = "navigation history and scroll restoration", previousPiece = previousAnchor.Piece,
                restoredPiece = restored.Piece, expectedPiece = targetAnchor.Piece, restored = restored.Piece == targetAnchor.Piece }); Save();
            var sourceFollow = viewer.NavigateAsync(source.IndexOf("First **item**", StringComparison.Ordinal)); HarnessSupport.Pump(sourceFollow);
            observations.Add(new { name = "source-follow navigation entry point", visible = sourceFollow.Result, sourcePreserved = model.Parsed.Source == source }); Save();
            var navigate = viewer.NavigateAnchorAsync("#distant"); HarnessSupport.Pump(navigate);
            var expanded = model.Groups.All(g => viewer.IsExpanded(g.Id));
            var anchor = viewer.CaptureAnchor();
            viewer.SetAppearance(true, 20); HarnessSupport.Layout(viewer); var after = viewer.CaptureAnchor();
            var jump = Math.Abs(after.RelativeTop - anchor.RelativeTop);
            using var cancellation = new CancellationTokenSource();
            var cancelled = viewer.NavigateAsync(0, cancellation: cancellation.Token); cancellation.Cancel();
            try { HarnessSupport.Pump(cancelled); throw new IOException("Cancelled navigation returned a usable result."); } catch (OperationCanceledException) { }
            observations.Add(new { name = "nested details / appearance / navigation cancellation", visible = navigate.Result, expanded,
                logicalAnchorRetained = anchor.Piece == after.Piece, correctionPixels = jump, selectionPreserved = anchor.Anchor == after.Anchor && anchor.Focus == after.Focus,
                cancellation = "PASS", sourcePreserved = viewer.Document!.Parsed.Source == source }); Save();
            // Exercise actual pixel scrollbar positions, mouse-wheel-equivalent operations, Home/End and Page Up/Down.
            foreach (var operation in new Action[] { viewer.Scroller!.ScrollToEnd, viewer.Scroller.ScrollToHome, viewer.Scroller.PageDown,
                viewer.Scroller.PageUp, viewer.Scroller.LineDown, viewer.Scroller.LineUp }) { operation(); HarnessSupport.Layout(viewer); }
            observations.Add(new { name = "scroll operations", nonEmptyViewport = viewer.Realised.Count > 0,
                currentSourceMappings = viewer.Realised.Values.All(p => model.Pieces[p.Piece!.Id] == p.Piece), sourcePreserved = model.Parsed.Source == source }); Save();
        }
        var weak = ReleaseViewer(); HarnessSupport.Idle(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        observations.Add(new { name = "resource release", viewerCollected = !weak.IsAlive, managedBytesAfterCollection = GC.GetTotalMemory(false),
            scope = "Explicit disposal and collection in the harness; no forced collection in interactive presentation." }); Save();
        using (var viewer = new VirtualDocumentView())
        using (var host = HarnessSupport.Host(viewer))
        {
            // The unchanged comparison fixture contains one initial heading. Append a separately labelled distant-heading variant.
            var original = FixtureCatalogue.All.Single(f => f.Name == "large-4mb").Source;
            var source = original + "\n## Distant heading\n\nRetained distant heading content.\n";
            var model = LogicalDocument.Build(new MarkdownService().Parse(source)); viewer.SetDocument(model); HarnessSupport.Layout(viewer); HarnessSupport.Idle();
            var target = model.Parsed.Targets.Single(t => t.Id == "distant-heading");
            var watch = Stopwatch.StartNew(); var navigation = viewer.NavigateAnchorAsync("#distant-heading"); HarnessSupport.Pump(navigation); HarnessSupport.Layout(viewer);
            var peer = (VirtualDocumentPeer)UIElementAutomationPeer.CreatePeerForElement(viewer);
            var piece = model.AtSource(target.SourceStart); var bounds = peer.Range(piece.Start, piece.End).GetBoundingRectangles();
            observations.Add(new { name = "4.14 MB derived distant-heading variant", sourceUtf8Bytes = System.Text.Encoding.UTF8.GetByteCount(source),
                originalComparisonSourceUnchanged = original == FixtureCatalogue.All.Single(f => f.Name == "large-4mb").Source,
                sourceStart = target.SourceStart, headings = model.Parsed.Headings.Count, visible = navigation.Result, actualBounds = bounds.Length > 0,
                nativeContainers = VirtualDocumentView.Descendants<ListBoxItem>(viewer).Count(), navigationMilliseconds = watch.Elapsed.TotalMilliseconds }); Save();
            HarnessSupport.Raster(viewer, Path.Combine(output, "large-distant-heading.png"));
            if (!navigation.Result || bounds.Length == 0) throw new IOException("The distant heading in the separately labelled large document was not reached.");
        }
        Console.WriteLine("Stress evidence retained; individual input failures remain failures.");
        void Save() => File.WriteAllText(Path.Combine(output, "stress.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ReleaseViewer()
    {
        var viewer = new VirtualDocumentView(); using var host = HarnessSupport.Host(viewer);
        viewer.SetDocument(LogicalDocument.Build(new MarkdownService().Parse("# Disposed\n\nReleased resources.")));
        HarnessSupport.Layout(viewer); HarnessSupport.Idle(); var weak = new WeakReference(viewer);
        host.RootVisual = null; viewer.Dispose(); return weak;
    }
}
