using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using SharpVectors.Runtime;

namespace LensDocsStudio.Native.Validation;

internal static class NativeMermaidFidelityChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var observations = new List<object>();
        foreach (var file in Directory.GetFiles("tests/fixtures/native-mermaid-fidelity", "*.mmd"))
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(file); var service = new DiagramService(); var svg = new NativeSvgService();
            var watch = Stopwatch.StartNew(); var result = service.Render(source, dark); var renderMs = watch.Elapsed.TotalMilliseconds;
            Check(result.Success, file + ": original source renders: " + result.Error);
            watch.Restart(); var presentation = NativeMermaidPresentation.Create(result, svg); var interactionMs = watch.Elapsed.TotalMilliseconds;
            var reference = svg.Render(result.Svg!); var model = new MermaidSourceParser().Parse(source);
            Check(!model.CanEdit || presentation.Maps(model), file + ": all editable identities map to the canonical drawing");
            var baseline = NativeSvgService.ToPng(reference, .75); var canvas = NativeSvgService.ToPng(presentation.Drawing, .75);
            Check(Pixels(baseline).SequenceEqual(Pixels(canvas)), file + ": normalised canvas pixels equal native preview pixels exactly");
            var name = Path.GetFileNameWithoutExtension(file) + (dark ? "-dark" : "-light");
            File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), baseline);
            File.WriteAllBytes(Path.Combine(output, name + "-canvas.png"), canvas);
            Check(presentation.Elements.Count == presentation.Graph.Nodes.Count + CountGroups(presentation.Graph.Groups), file + ": every canonical node and nested subgraph has geometry");
            Check(Groups(presentation.Drawing.Drawing).Count(group => SvgObject.GetId(group)?.StartsWith("lens-node-", StringComparison.Ordinal) == true) == presentation.Graph.Nodes.Count,
                file + ": native drawing groups retain safe session-only node identities");
            Check(Groups(presentation.Drawing.Drawing).Count(group => SvgObject.GetId(group)?.StartsWith("lens-group-label-", StringComparison.Ordinal) == true) == CountGroups(presentation.Graph.Groups),
                file + ": verified subgraph titles retain their own session translation identities");
            foreach (var node in presentation.Graph.Nodes)
            {
                var box = presentation.Elements["node:" + node.Id].Bounds; var origin = presentation.Drawing.Drawing.Bounds;
                Check(Math.Abs(box.X - node.X + origin.X) < .001 && Math.Abs(box.Y - node.Y + origin.Y) < .001
                    && Math.Abs(box.Width - node.Width) < .001 && Math.Abs(box.Height - node.Height) < .001, file + ": node bounds are canonical for " + node.Id);
                Check(presentation.Elements["node:" + node.Id].Geometry.FillContains(new Point(box.X + box.Width / 2, box.Y + box.Height / 2)),
                    file + ": the actual shape centre is a valid native hit target for " + node.Id);
            }
            var markdown = "Before\r\n\r\n```mermaid\r\n" + source + "\r\n```\r\n\r\nAfter";
            var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var preview = new NativeMarkdownRenderer(svg).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { },
                new Dictionary<FencedCodeBlock, DrawingImage> { [code] = reference }, editDiagramSource: _ => { }, editDiagramVisually: offset => selected = offset);
            var actions = (Panel)preview.Media.Single().Actions!;
            var buttons = actions.Children.OfType<Button>().ToArray();
            var visual = buttons.Single(button => (string?)button.Content == "Visual Edit");
            Check(buttons[0] == visual && buttons.Any(button => (string?)button.Content == "Edit source"), file + ": Visual Edit is primary and Edit source remains separate");
            visual.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(selected == code.Span.Start, file + ": preview targets its precise source container");
            Check(AutomationProperties.GetAutomationId(visual) == "DiagramEditVisually" + code.Span.Start, file + ": existing automation identity is preserved");
            Check(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source, .75)).SequenceEqual(Pixels(canvas)), file + ": actual FlowDocument diagram equals the canvas");
            var viewer = new DiagramWindow(reference, _ => { });
            try
            {
                var viewerImage = (Image)viewer.FindName("DiagramImage");
                Check(Pixels(NativeSvgService.ToPng(viewerImage.Source, .75)).SequenceEqual(Pixels(canvas)), file + ": actual Rendered preview image equals the canvas");
            }
            finally { viewer.Close(); }
            observations.Add(new { Fixture = name, Model = model.Compatibility.ToString(), Nodes = presentation.Graph.Nodes.Count, Connections = presentation.Connections.Count,
                RenderMilliseconds = renderMs, InteractionGeometryMilliseconds = interactionMs, PixelTolerance = 0, CoordinateTolerance = .001,
                Bounds = new { reference.Width, reference.Height }, SourceUnchanged = result.Source == source });
        }
        const string literalSource = "flowchart LR\nA[\"Read var(--missing) and 2rem\"] --> B[\"Keep color-mix(in srgb, red, blue) literal\"]";
        var literalRender = new DiagramService().Render(literalSource);
        var literalPresentation = NativeMermaidPresentation.Create(literalRender, new NativeSvgService());
        Check(literalPresentation.Maps(new MermaidSourceParser().Parse(literalSource)), "Literal CSS words in Flowchart source metadata retain exact canonical identities");
        Check(NativeSvgService.ToPng(literalPresentation.Drawing, .5).Length > 100, "Literal CSS labels retain actual Flowchart PNG output");
        var safeResult = new DiagramService().Render("flowchart LR\nA[Keep] --> B[Source]");
        Reject(safeResult with { Svg = safeResult.Svg!.Replace("data-id=\"A\"", "data-id=\"B\"", StringComparison.Ordinal) }, "Conflicting node IDs cannot supply interaction identities");
        Reject(safeResult with { Svg = "<!DOCTYPE svg [<!ENTITY unsafe SYSTEM 'file:///synthetic'>]>" + safeResult.Svg }, "DTD and external entities are rejected before metadata processing");
        Reject(safeResult with { Svg = safeResult.Svg!.Replace("</svg>", "<image href='https://example.invalid/external'/></svg>", StringComparison.Ordinal) }, "External SVG content cannot enter the native canvas");
        Reject(safeResult with { FlowchartLayout = safeResult.FlowchartLayout! with
        { Nodes = safeResult.FlowchartLayout.Nodes.Select((node, index) => index == 0 ? node with { X = double.NaN } : node).ToArray() } }, "Non-finite canonical coordinates cannot supply interaction geometry");
        var wide = new DrawingGroup();
        using (var context = wide.Open()) context.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, 200_000, 100));
        wide.Freeze(); var wideImage = new DrawingImage(wide); wideImage.Freeze();
        var wideViewer = new DiagramWindow(wideImage, _ => { }) { ShowInTaskbar = false };
        try
        {
            wideViewer.Show(); wideViewer.UpdateLayout();
            var fitButton = ((DockPanel)wideViewer.Content).Children.OfType<ToolBar>().Single().Items.OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "DiagramFit");
            fitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            wideViewer.UpdateLayout();
            var zoom = (Slider)wideViewer.FindName("Zoom"); var viewport = (ScrollViewer)wideViewer.FindName("DiagramScroll");
            Check(zoom.Value is > 0 and < 1 && viewport.ScrollableWidth < 1 && viewport.ScrollableHeight < 1,
                "Actual native Fit supports very wide drawings below one per cent without hiding content");
        }
        finally { wideViewer.Close(); }
        File.WriteAllText(Path.Combine(output, "fidelity.json"), JsonSerializer.Serialize(new { Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool success, string message) { if (!success) throw new IOException(message); checks.Add(message); }
        void Reject(DiagramResult candidate, string message)
        {
            try { NativeMermaidPresentation.Create(candidate, new NativeSvgService()); }
            catch (Exception error) when (error is InvalidDataException or System.Xml.XmlException or ArgumentException)
            { checks.Add(message); return; }
            throw new IOException(message);
        }
    }
    private static byte[] Pixels(byte[] png)
    {
        using var stream = new MemoryStream(png); var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var pixels = new byte[frame.PixelHeight * frame.PixelWidth * 4]; frame.CopyPixels(pixels, frame.PixelWidth * 4, 0); return pixels;
    }
    private static int CountGroups(IEnumerable<Mermaider.Models.PositionedGroup> groups) => groups.Sum(group => 1 + CountGroups(group.Children));
    private static IEnumerable<DrawingGroup> Groups(Drawing drawing)
    { if (drawing is DrawingGroup group) { yield return group; foreach (var child in group.Children) foreach (var nested in Groups(child)) yield return nested; } }
}
