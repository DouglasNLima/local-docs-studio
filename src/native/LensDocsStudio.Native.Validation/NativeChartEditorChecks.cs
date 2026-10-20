using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.XyCharts;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeChartEditorChecks
{
    public static void Run(string output, string repository)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        foreach (var family in new[] { "xychart", "radar", "sankey" })
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid", family + ".mmd"));
            var diagrams = new DiagramService(); SourceVisualEditorWindow window = family switch {
                "xychart" => new XyVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "radar" => new RadarVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                _ => new SankeyVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark) };
            ThemeService.Apply(window.Resources, dark ? "Dark" : "Light");
            var draft = (ISourceDraft)typeof(SourceVisualEditorWindow).GetField("Draft", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(draft.ValidateAsync().GetAwaiter().GetResult().Success, family + ": actual editor draft validates exact fixture");
            var result = diagrams.Render(source, dark); var native = new NativeSvgService();
            var drawing = (System.Windows.Media.DrawingImage)typeof(NativeSvgService).GetMethod("RenderCanonical", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(native, [result, native.Prepare(result.Svg!), false, CancellationToken.None])!;
            var hits = (IReadOnlyList<NativeSourceHit>)window.GetType().GetMethod("Correspondence", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [source, result])!;
            Check(hits.Count > 0 && hits.Select(h => h.Identity).Distinct().Count() == hits.Count && hits.All(h => h.Shape is not null), family + ": every native hit uses a distinct source identity and real SVG shape");
            if (draft is XyVisualDraft xy) Check(hits.Count == xy.Model.Series.Sum(s => s.Values.Items.Count), "XY: duplicate and zero marks have complete canonical correspondence");
            if (draft is RadarVisualDraft radar) Check(hits.Count == radar.Model.Curves.Count * (1 + radar.Model.Axes.Count), "Radar: overlapping polygons and all positional marks map separately");
            if (draft is SankeyVisualDraft sankey) Check(hits.Count == sankey.Model.Links.Count + sankey.Model.Nodes.Count, "Sankey: parallel ribbons and native nodes map to proved source occurrences");
            var markdown = "Before Ω\n\n```mermaid\n" + source + "\n```\n\nAfter\n";
            var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var ordinary = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { }, editDiagramVisually: offset => selected = offset);
            ((Panel)ordinary.Media.Single().Actions!).Children.OfType<Button>().Single(b => (string?)b.Content == "Visual Edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(selected == code.Span.Start, family + ": ordinary document exposes exact Visual Edit context action");
            var editorPng = NativeSvgService.ToPng(drawing); var previewPng = NativeSvgService.ToPng(ordinary.Media.Single().Image.Source);
            Check(NativeSourceEditorChecks.Pixels(editorPng).SequenceEqual(NativeSourceEditorChecks.Pixels(previewPng)), family + ": complete editor and ordinary preview pixels match at identical source, appearance and scale with zero tolerance");
            var name = family + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + "-editor.png"), editorPng); File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), previewPng);
            foreach (var nl in new[] { "\n", "\r\n" })
            {
                var body = source.ReplaceLineEndings(nl); var block = "```mermaid" + nl + body.TrimEnd('\r', '\n') + nl + "```" + nl;
                var original = "Before café Ω" + nl + block + "Between" + nl + block + "After" + nl;
                var owner = new DocumentViewModel(original) { DisplayName = family + ".md" }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1];
                var change = family switch { "xychart" => body.Replace("+5e0", "+4e0", StringComparison.Ordinal), "radar" => body.Replace("+4e0", "+3e0", StringComparison.Ordinal), _ => body.Replace("3.000", "4.000", StringComparison.Ordinal) };
                new MermaidDocumentEditSession(owner, target).Apply(change, true);
                Check(owner.EditorDocument.Text == original[..target.Body.Start] + change + original[target.Body.End..], family + ": exact second identical fence patch preserves neighbours and " + (nl == "\n" ? "LF" : "CRLF"));
                owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == original, family + ": owner Apply is one exact Undo step");
                owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text.Contains(change, StringComparison.Ordinal), family + ": owner Redo restores the complete patch");
                var conflict = new DocumentViewModel(original); var session = new MermaidDocumentEditSession(conflict, target); var prepared = session.PrepareAsync(change).GetAwaiter().GetResult(); conflict.EditorDocument.Insert(0, "Newer edit" + nl); var current = conflict.EditorDocument.Text;
                try { session.Commit(prepared, true); throw new IOException("Stale validation was committed."); } catch (ArgumentException) { }
                Check(conflict.EditorDocument.Text == current, family + ": asynchronous final owner-version conflict preserves both sources");
            }
        }
        File.WriteAllText(Path.Combine(output, "chart-editor-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", SourceRevision = typeof(XyVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
    }
}
