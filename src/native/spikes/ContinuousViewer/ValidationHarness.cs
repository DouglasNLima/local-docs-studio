using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Documents;
using ContinuousViewer.Model;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace ContinuousViewer;

internal static class ValidationHarness
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); HarnessSupport.Pixel(output);
        var checks = new List<object>();
        void Check(string name, bool passed, object? detail = null)
        {
            checks.Add(new { name, passed, detail });
            File.WriteAllText(Path.Combine(output, "validation.json"), JsonSerializer.Serialize(new
            { scope = "Hidden native HWND, real WPF layout, logical/provider contracts; physical screen-reader and OS interaction certification outstanding.", checks }, new JsonSerializerOptions { WriteIndented = true }));
            if (!passed) throw new IOException("Feasibility regression failed: " + name);
        }
        foreach (var fixture in FixtureCatalogue.All)
        {
            Console.WriteLine(fixture.Name + ": logical preparation");
            var model = LogicalDocument.Build(new MarkdownService().Parse(fixture.Source));
            var media = DocumentLoader.PrepareMedia(model, Path.Combine(output, "synthetic.md"), output); HarnessSupport.Pump(media);
            Console.WriteLine(fixture.Name + ": hidden host");
            using var viewer = new VirtualDocumentView(); using var host = HarnessSupport.Host(viewer);
            Console.WriteLine(fixture.Name + ": set document"); viewer.SetDocument(model, media.Result);
            Console.WriteLine(fixture.Name + ": initial layout"); HarnessSupport.Layout(viewer);
            Console.WriteLine(fixture.Name + ": initial idle"); HarnessSupport.Idle();
            Check(fixture.Name + ": actual initial text controls are materialised", viewer.Realised.Count > 0 && viewer.Realised.Values.All(p => p.TextControl is not null));
            Check(fixture.Name + ": native text uses the real viewport width", viewer.Realised.Values.All(p => p.ActualWidth > 300 && p.TextControl!.ActualWidth > (p.Piece!.Kind == PieceKind.TableRow ? 100 : 300)));
            var initial = viewer.Realised.Keys.ToArray();
            if (model.Pieces.Length > 200) Check(fixture.Name + ": real virtualisation bounds initial containers", initial.Length < 100, new { logical = model.Pieces.Length, actual = initial.Length });
            var peer = (VirtualDocumentPeer)UIElementAutomationPeer.CreatePeerForElement(viewer);
            Check(fixture.Name + ": TextPattern exposes the complete offscreen document", peer.GetPattern(PatternInterface.Text) is ITextProvider && peer.DocumentRange.GetText(-1) == model.Text);
            var offscreen = peer.DocumentRange.FindText(fixture.Last, true, false)!;
            Check(fixture.Name + ": offscreen text is discoverable before materialisation", offscreen is not null && offscreen.GetText(-1) == fixture.Last);
            var target = fixture.Source.LastIndexOf(fixture.Last, StringComparison.Ordinal);
            var navigation = viewer.NavigateAsync(target, true); HarnessSupport.Pump(navigation); HarnessSupport.Layout(viewer);
            Check(fixture.Name + ": distant target is actually visible and selected", navigation.Result && viewer.Selection!.Copy().UnicodeText.Contains(fixture.Last, StringComparison.Ordinal), new { actual = viewer.Realised.Count, viewer.PeakRealised });
            Check(fixture.Name + ": completed distant navigation exposes actual selection geometry", peer.GetSelection().Single().GetBoundingRectangles().Length > 0);
            if (model.Pieces.Length > 200) Check(fixture.Name + ": old visuals are recycled and target mappings are current", viewer.Realised.Count < 100 && !viewer.Realised.Keys.Intersect(initial).Any()
                && viewer.Realised.Values.All(p => p.TextControl!.GetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty).ToString() == "LogicalPiece" + p.Piece!.Id));
            viewer.Selection!.Set(Math.Min(3, model.Text.Length), model.Text.Length - 1);
            var expected = model.Text[viewer.Selection.Start..viewer.Selection.End]; var data = viewer.ClipboardObject();
            Check(fixture.Name + ": cross-block copy includes previously virtualised content", (string)data.GetData(DataFormats.UnicodeText)! == expected);
            var native = new FlowDocument(); var range = new TextRange(native.ContentStart, native.ContentEnd);
            using (var rtf = new MemoryStream(Encoding.ASCII.GetBytes((string)data.GetData(DataFormats.Rtf)!))) range.Load(rtf, DataFormats.Rtf);
            Check(fixture.Name + ": native RTF round-trip preserves selected Unicode and line breaks", range.Text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') == expected.TrimEnd('\n'));
            Check(fixture.Name + ": CF_HTML uses validated byte offsets", HtmlClipboardFormat.Decode((string)data.GetData(DataFormats.Html)!).Contains("<div>", StringComparison.Ordinal));
            var before = viewer.Selection.Copy().UnicodeText;
            viewer.Scroller!.ScrollToTop(); HarnessSupport.Layout(viewer); HarnessSupport.Idle();
            Check(fixture.Name + ": scroll retains full logical selection", viewer.Selection.Copy().UnicodeText == before && peer.GetSelection().Single().GetText(-1) == before);
            var textRange = peer.Range(0, 0); textRange.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 4); textRange.Select();
            Check(fixture.Name + ": assistive selection updates the document model", viewer.Selection.End == model.Graphemes[Math.Min(4, model.Graphemes.Length - 1)]);
            var oldSelection = viewer.Selection.Copy().UnicodeText; viewer.SetAppearance(true, 18); HarnessSupport.Layout(viewer);
            Check(fixture.Name + ": theme and font changes preserve logical selection", viewer.Selection.Copy().UnicodeText == oldSelection && model.Parsed.Source == fixture.Source);
            viewer.SetAppearance(false, 15);
            if (model.Groups.Length > 0)
            {
                var anchor = viewer.Selection.Copy().UnicodeText; viewer.Toggle(0); HarnessSupport.Layout(viewer); viewer.Toggle(0); HarnessSupport.Layout(viewer);
                Check(fixture.Name + ": expansion state is independent of recycled controls", viewer.Selection.Copy().UnicodeText == anchor);
            }
            if (fixture.Name is "rich" or "mixed") Check(fixture.Name + ": existing native image and Mermaid services produce real media", media.Result.Count >= 2);
            // Export from the complete parsed source; never use realised controls as the export input.
            var exportTask = StaWorker.RunAsync(() => new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService())
                .Prepare(model.Parsed.Source, Path.Combine(output, "synthetic.md"), output, false));
            HarnessSupport.Pump(exportTask);
            var html = new HtmlExporter().Export(exportTask.Result);
            Check(fixture.Name + ": established HTML export includes the distant logical content", html.Contains(fixture.Last, StringComparison.Ordinal) && model.Parsed.Source == fixture.Source);
            HarnessSupport.Raster(viewer, Path.Combine(output, fixture.Name + ".png"));
            viewer.SetDocument(LogicalDocument.Build(new MarkdownService().Parse("# Replacement\n\nCurrent document.")));
            try { offscreen!.GetText(-1); Check(fixture.Name + ": stale UIA ranges are rejected", false); }
            catch (InvalidOperationException) { Check(fixture.Name + ": stale UIA ranges are rejected", true); }
            host.RootVisual = null; viewer.Dispose(); HarnessSupport.Idle();
            Check(fixture.Name + ": disposing clears controls, selection, source and media references", viewer.Document is null && viewer.Selection is null && viewer.Realised.Count == 0 && viewer.Media.Count == 0);
        }
        using (var viewer = new VirtualDocumentView())
        using (var loader = new DocumentLoader())
        using (var host = HarnessSupport.Host(viewer))
        {
            var source = FixtureCatalogue.All.Single(f => f.Name == "large-4mb").Source;
            var watch = Stopwatch.StartNew(); var old = loader.LoadAsync(viewer, source);
            var current = loader.LoadAsync(viewer, "# Current\n\nRapid replacement is retained.");
            HarnessSupport.Pump(Task.WhenAll(old, current));
            Check("rapid document replacement: obsolete projection cannot commit", !old.Result && current.Result && viewer.Document!.Parsed.Source == "# Current\n\nRapid replacement is retained.", new { elapsedMilliseconds = watch.Elapsed.TotalMilliseconds });
        }
        Console.WriteLine($"Hidden-HWND feasibility validation: {checks.Count} checks passed. Physical accessibility/OS clipboard remain separate gates.");
    }
}
