using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.RequirementDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.GitGraphDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.KanbanDiagrams;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWorkflowEditorChecks
{
    public static void Run(string output, string repository)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        foreach (var family in new[] { "kanban", "requirement", "gitgraph" })
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid/expansion-iv", family + ".mmd"));
            var diagrams = new DiagramService(); SourceVisualEditorWindow window = family switch {
                "kanban" => new KanbanVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "requirement" => new RequirementVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                _ => new GitGraphVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark) };
            ThemeService.Apply(window.Resources, dark ? "Dark" : "Light");
            var draft = (ISourceDraft)typeof(SourceVisualEditorWindow).GetField("Draft", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(draft.ValidateAsync().GetAwaiter().GetResult().Success, family + ": actual editor draft validates exact fixture");
            var result = diagrams.Render(source, dark); var native = new NativeSvgService();
            var drawing = (System.Windows.Media.DrawingImage)typeof(NativeSvgService).GetMethod("RenderCanonical", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(native, [result, native.Prepare(result.Svg!), false, CancellationToken.None])!;
            var hits = (IReadOnlyList<NativeSourceHit>)window.GetType().GetMethod("Correspondence", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [source, result])!;
            Check(hits.Count > 0 && hits.Select(h => h.Identity).Distinct().Count() == hits.Count && hits.All(h => h.Shape is not null), family + ": every native hit uses a distinct source identity and real SVG shape");
            if (draft is KanbanVisualDraft board) Check(hits.Count == board.Model.Rows.Count, "Kanban: every duplicate card and column has distinct canonical correspondence");
            if (draft is RequirementVisualDraft req) Check(hits.Count == req.Model.Visible.Count + req.Model.Relations.Count, "Requirement: visible boxes and proved native relationship paths map separately");
            if (draft is GitGraphVisualDraft git) Check(hits.Count == git.Model.Marks.Count, "GitGraph: actual commit marks map by command occurrence; non-mark commands remain in the list");
            if (draft is GitGraphVisualDraft history)
            {
                var appended = history.AppendAsync("commit").GetAwaiter().GetResult(); Check(appended.Success, "GitGraph: anonymous commit can be appended through the actual editor draft"); history.Select(history.Model.Commands[^1].Identity);
                var fields = (Panel)window.FindName("InspectorFields"); fields.IsEnabled = false;
                window.GetType().GetMethod("ShowProperties", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, []);
                Check(new[] { "Label", "Identifier" }.All(name => ((FrameworkElement)typeof(ChartVisualEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Visibility == Visibility.Visible), "GitGraph: empty editable ID and tag fields remain visible while validation disables their ancestor");
                fields.IsEnabled = true; history.Undo();
            }
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
                var change = family switch { "kanban" => body.Replace("Write guide", "Edited guide", StringComparison.Ordinal), "requirement" => body.Replace("DOC-17", "DOC-18", StringComparison.Ordinal), _ => body.Replace("Start", "Edited start", StringComparison.Ordinal) };
                new MermaidDocumentEditSession(owner, target).Apply(change, true);
                Check(owner.EditorDocument.Text == original[..target.Body.Start] + change + original[target.Body.End..], family + ": exact second identical fence patch preserves neighbours and " + (nl == "\n" ? "LF" : "CRLF"));
                owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == original, family + ": owner Apply is one exact Undo step");
                owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text.Contains(change, StringComparison.Ordinal), family + ": owner Redo restores the complete patch");
                var conflict = new DocumentViewModel(original); var session = new MermaidDocumentEditSession(conflict, target); var prepared = session.PrepareAsync(change).GetAwaiter().GetResult(); conflict.EditorDocument.Insert(0, "Newer edit" + nl); var current = conflict.EditorDocument.Text;
                try { session.Commit(prepared, true); throw new IOException("Stale validation was committed."); } catch (ArgumentException) { }
                Check(conflict.EditorDocument.Text == current, family + ": asynchronous final owner-version conflict preserves both sources");
            }
        }
        File.WriteAllText(Path.Combine(output, "workflow-editor-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", SourceRevision = typeof(KanbanVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
    }
}
