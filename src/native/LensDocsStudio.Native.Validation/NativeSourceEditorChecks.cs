using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.Mindmaps;
using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSourceEditorChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var captures = new List<object>(); var diagrams = new DiagramService();
        foreach (var family in new[] { "Gantt", "Mindmap", "Pie" })
        foreach (var dark in new[] { false, true })
        {
            var starter = family switch { "Gantt" => GanttVisualDraft.Starter, "Mindmap" => MindmapVisualDraft.Starter, _ => PieVisualDraft.Starter };
            ISourceDraft draft = family switch { "Gantt" => new GanttVisualDraft(starter), "Mindmap" => new MindmapVisualDraft(starter), _ => new PieVisualDraft(starter) };
            SourceAuthoringResult change = family switch {
                "Gantt" => ((GanttVisualDraft)draft).UpdateAsync(((GanttVisualDraft)draft).Model.Tasks[0].Identity, "Updated guide", "2026-10-21", "4").GetAwaiter().GetResult(),
                "Mindmap" => ((MindmapVisualDraft)draft).UpdateAsync(((MindmapVisualDraft)draft).Model.Nodes[1].Identity, "Updated guides").GetAwaiter().GetResult(),
                _ => ((PieVisualDraft)draft).UpdateAsync(((PieVisualDraft)draft).Model.Entries[0].Identity, "Updated guides", "7.25000").GetAwaiter().GetResult() };
            Check(change.Success, family + ": source transformation validates before native presentation");
            var source = draft.Source; var render = diagrams.Render(source, dark); Check(render.Success, family + ": authored source renders through ordinary native adapter");
            SourceVisualEditorWindow editor = family switch {
                "Gantt" => new GanttVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "Mindmap" => new MindmapVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                _ => new PieVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark) };
            ThemeService.Apply(editor.Resources, dark ? "Dark" : "Light");
            // Construct the same shared hit-target type/style without showing a desktop window.
            var target = new ToggleButton { Style = (Style)editor.FindResource("Mermaid.CanonicalElement") }; target.ApplyTemplate();
            var hits = (IReadOnlyList<NativeSourceHit>)editor.GetType().GetMethod("Correspondence", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, [source, render])!;
            var expected = family switch { "Gantt" => ((GanttVisualDraft)draft).Model.Tasks.Count, "Mindmap" => ((MindmapVisualDraft)draft).Model.Nodes.Count, _ => ((PieVisualDraft)draft).Model.Entries.Count(entry => entry.Value > 0) };
            Check(hits.Count == expected && hits.Select(hit => hit.Identity).Distinct().Count() == expected, family + ": every supported occurrence has distinct canonical geometry");
            var svg = new NativeSvgService(); var canonical = (DrawingImage)typeof(NativeSvgService).GetMethod("RenderCanonical", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(svg, [render, svg.Prepare(render.Svg!), false, CancellationToken.None])!;
            foreach (var hit in hits) Check(new Rect(0, 0, canonical.Width, canonical.Height).Contains(hit.Bounds), family + ": actual SVG hit bounds stay inside canonical canvas");
            if (family == "Pie")
            {
                var canvas = new Canvas { Width = canonical.Width, Height = canonical.Height };
                foreach (var hit in hits) canvas.Children.Add(new System.Windows.Shapes.Path { Data = hit.Shape, Fill = Brushes.Transparent, Stroke = Brushes.Transparent, StrokeThickness = 2, Tag = hit.Identity });
                canvas.Measure(new Size(canonical.Width, canonical.Height)); canvas.Arrange(new Rect(0, 0, canonical.Width, canonical.Height)); canvas.UpdateLayout();
                foreach (var hit in hits)
                {
                    var shape = canvas.Children.OfType<System.Windows.Shapes.Path>().Single(path => (string)path.Tag == hit.Identity);
                    Check(shape.RenderedGeometry.Bounds == hit.Shape!.Bounds, "Pie: WPF pointer path retains actual canonical geometry coordinates");
                    Point? inside = null;
                    for (var x = 1; x < 10 && inside is null; x++) for (var y = 1; y < 10 && inside is null; y++) { var point = new Point(hit.Bounds.X + hit.Bounds.Width * x / 10, hit.Bounds.Y + hit.Bounds.Height * y / 10); if (hit.Shape.FillContains(point)) inside = point; }
                    Check(inside is not null && ReferenceEquals(System.Windows.Media.VisualTreeHelper.HitTest(canvas, inside.Value)?.VisualHit, shape), "Pie: actual WPF hit testing resolves the exact canonical wedge occurrence");
                }
            }
            var markdown = "Before\n\n```mermaid\n" + source + "```\n\nAfter"; var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var ordinary = new NativeMarkdownRenderer(svg).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = render }, _ => { }, (_, _) => { }, editDiagramVisually: offset => selected = offset);
            ((Panel)ordinary.Media.Single().Actions!).Children.OfType<Button>().Single(button => (string?)button.Content == "Visual Edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(selected == code.Span.Start, family + ": ordinary preview dispatches exact authoring container");
            var editorPng = NativeSvgService.ToPng(canonical, .75); var ordinaryPng = NativeSvgService.ToPng(ordinary.Media.Single().Image.Source, .75);
            Check(Pixels(editorPng).SequenceEqual(Pixels(ordinaryPng)), family + ": complete ordinary preview and editor canonical pixels match with zero tolerance");
            var name = family.ToLowerInvariant() + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + "-editor.png"), editorPng); File.WriteAllBytes(Path.Combine(output, name + "-ordinary.png"), ordinaryPng);
            captures.Add(new { Family = family, Dark = dark, SourceSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))).ToLowerInvariant(), HitCount = hits.Count, PixelTolerance = 0 });
            DocumentChecks(source, family);
        }
        File.WriteAllText(Path.Combine(output, "source-editor-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks, Captures = captures }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
        void DocumentChecks(string body, string family)
        {
            foreach (var (opening, closing) in new[] { ("```mermaid", "```"), ("~~~~mermaid", "~~~~"), ("::: mermaid", ":::") })
            {
                var block = opening + "\r\n" + body.ReplaceLineEndings("\r\n") + closing + "\r\n"; var source = "Before\r\n\r\n" + block + "\r\nMiddle\r\n\r\n" + block;
                var owner = new DocumentViewModel(source) { DisplayName = family + ".md" }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1]; var session = new MermaidDocumentEditSession(owner, target); var competing = new MermaidDocumentEditSession(owner, target);
                owner.EditorDocument.Insert(0, "Outside\r\n\r\n"); session.Apply(body, true); var expected = "Outside\r\n\r\n" + source[..target.Body.Start] + body + source[target.Body.End..];
                Check(owner.EditorDocument.Text == expected, family + opening + ": exact second identical container relocation and outside bytes"); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == "Outside\r\n\r\n" + source, family + ": one-step owner Undo"); owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == expected, family + ": owner Redo");
                Reject(() => competing.Apply(body + "\n", true), family + ": competing stale author rejects Apply");
                var raced = new DocumentViewModel(source); var race = new MermaidDocumentEditSession(raced, target); var prepared = race.PrepareAsync(body).GetAwaiter().GetResult(); raced.EditorDocument.Insert(0, "Race"); Reject(() => race.Commit(prepared, true), family + ": final owner version check rejects racing edit");
                Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(body, false), family + ": closed owner rejects Apply");
            }
            foreach (var extension in new[] { ".mmd", ".mermaid" })
            {
                var owner = new DocumentViewModel(body) { DisplayName = family + extension }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(body, true), true).Single(); var session = new MermaidDocumentEditSession(owner, target);
                session.Apply(body + "\n", true); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == body, family + extension + ": standalone exact source and Undo");
            }
        }
    }
    private static byte[] Pixels(byte[] png)
    { using var input = new MemoryStream(png); var bitmap = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; var stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8; var bytes = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(bytes, stride, 0); return bytes; }
}
