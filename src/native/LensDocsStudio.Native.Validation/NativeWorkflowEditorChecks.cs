using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    public static void Run(string output, string repository, bool topology = false, string? expansionFamily = null)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        foreach (var family in expansionFamily is not null ? new[] { expansionFamily } : topology ? new[] { "architecture", "c4-context", "c4-container", "c4-component", "block" } : new[] { "kanban", "requirement", "gitgraph" })
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid", expansionFamily is not null ? "expansion-vi" : topology ? "expansion-v" : "expansion-iv", family + ".mmd"));
            var diagrams = new DiagramService(); SourceVisualEditorWindow window = family switch {
                "packet" => new PacketVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "treeview" or "treeview-box" => new TreeViewVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "kanban" => new KanbanVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "requirement" => new RequirementVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "architecture" => new ArchitectureVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "c4-context" or "c4-container" or "c4-component" => new C4VisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                "block" => new BlockVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark),
                _ => new GitGraphVisualEditorWindow(source, diagrams, (_, _) => Task.FromResult<string?>(null), dark) };
            ThemeService.Apply(window.Resources, dark ? "Dark" : "Light");
            var draft = (ISourceDraft)typeof(SourceVisualEditorWindow).GetField("Draft", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(draft.ValidateAsync().GetAwaiter().GetResult().Success, family + ": actual editor draft validates exact fixture");
            var result = diagrams.Render(source, dark); var native = new NativeSvgService();
            var drawing = (System.Windows.Media.DrawingImage)typeof(NativeSvgService).GetMethod("RenderCanonical", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(native, [result, native.Prepare(result.Svg!), false, CancellationToken.None])!;
            var hits = (IReadOnlyList<NativeSourceHit>)window.GetType().GetMethod("Correspondence", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [source, result])!;
            Check(hits.Count > 0 && hits.Select(h => h.Identity).Distinct().Count() == hits.Count && hits.All(h => h.Shape is not null), family + ": every native hit uses a distinct source identity and real SVG shape");
            if (draft is KanbanVisualDraft board) Check(hits.Count == board.Model.Rows.Count, "Kanban: every duplicate card and column has distinct canonical correspondence");
            if (draft is Core.PacketDiagrams.PacketVisualDraft packet) Check(hits.Count == packet.Model.Fields.Count && packet.Model.Fields.Count < Core.PacketDiagrams.PacketVisualDraft.Segments(packet.Model).Count(), "Packet: every split native segment belongs to its exact field occurrence, with combined actual geometry");
            if (draft is Core.TreeViewDiagrams.TreeViewVisualDraft tree) Check(hits.Count == tree.Model.Nodes.Count(n => n.Icon is null), "TreeView: actual default glyph/highlight targets map exactly; hidden icons remain keyboard-only");
            if (draft is RequirementVisualDraft req) Check(hits.Count == req.Model.Visible.Count + req.Model.Relations.Count, "Requirement: visible boxes and proved native relationship paths map separately");
            if (draft is GitGraphVisualDraft git) Check(hits.Count == git.Model.Marks.Count, "GitGraph: actual commit marks map by command occurrence; non-mark commands remain in the list");
            if (draft is Core.ArchitectureDiagrams.ArchitectureVisualDraft architecture) Check(hits.Count == architecture.Model.Nodes.Count + architecture.Model.Edges.Count, "Architecture: actual services, group boxes, junctions and connection paths have complete occurrence correspondence");
            if (draft is Core.C4Diagrams.C4VisualDraft c4) Check(hits.Count == c4.Model.Nodes.Count + c4.Model.Relations.Count, family + ": every actual element, boundary and relationship maps separately at this architecture level");
            if (draft is Core.BlockDiagrams.BlockVisualDraft blockDraft) Check(hits.Count == blockDraft.Model.Visible.Count + blockDraft.Model.Edges.Count && blockDraft.Model.Tokens.Any(t => t.Space), "Block: actual boxes/links map exactly while spacers retain keyboard-only source occurrences");
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
            RenderActual(window);
            var surface = (Canvas)window.FindName("Surface"); var actualImage = surface.Children.OfType<Image>().Single();
            Check(actualImage.Source is DrawingImage && ReferenceEquals(actualImage.Source, typeof(SourceVisualEditorWindow).GetField("drawing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)), family + ": actual editor render publishes its canonical drawing directly on the complete source canvas");
            Check(NativeSourceEditorChecks.Pixels(NativeSvgService.ToPng(actualImage.Source)).SequenceEqual(NativeSourceEditorChecks.Pixels(previewPng)), family + ": actual asynchronous editor render and ordinary preview retain every pixel with zero tolerance");
            var previewSurface = new Canvas { Width = surface.Width, Height = surface.Height, Background = Brushes.Transparent, UseLayoutRounding = surface.UseLayoutRounding, SnapsToDevicePixels = surface.SnapsToDevicePixels };
            previewSurface.Children.Add(new Image { Source = ordinary.Media.Single().Image.Source, Stretch = Stretch.None, IsHitTestVisible = false, SnapsToDevicePixels = actualImage.SnapsToDevicePixels });
            var actualCanvas = CanvasPixels(surface); var previewCanvas = CanvasPixels(previewSurface);
            var canvasName = family + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, canvasName + "-actual-editor-canvas.png"), actualCanvas); File.WriteAllBytes(Path.Combine(output, canvasName + "-ordinary-preview-canvas.png"), previewCanvas);
            File.WriteAllText(Path.Combine(output, canvasName + "-canvas-options.json"), JsonSerializer.Serialize(new { SourceRevision = typeof(KanbanVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, SourceWidth = surface.Width, SourceHeight = surface.Height, SourceActualWidth = surface.ActualWidth, SourceActualHeight = surface.ActualHeight, PreviewActualWidth = previewSurface.ActualWidth, PreviewActualHeight = previewSurface.ActualHeight, SourceBounds = VisualTreeHelper.GetDescendantBounds(surface).ToString(), PreviewBounds = VisualTreeHelper.GetDescendantBounds(previewSurface).ToString(), SourceOffset = VisualTreeHelper.GetOffset(surface).ToString(), PreviewOffset = VisualTreeHelper.GetOffset(previewSurface).ToString(), ImageOffset = VisualTreeHelper.GetOffset(actualImage).ToString(), ImageActualWidth = actualImage.ActualWidth, ImageActualHeight = actualImage.ActualHeight, LayoutTransform = surface.LayoutTransform.Value.ToString(), SourceDpi = VisualTreeHelper.GetDpi(surface).ToString(), PreviewDpi = VisualTreeHelper.GetDpi(previewSurface).ToString(), SourceImageBounds = VisualTreeHelper.GetContentBounds(actualImage).ToString(), PreviewImageBounds = VisualTreeHelper.GetContentBounds(previewSurface.Children.OfType<Image>().Single()).ToString(), SourceImageSize = actualImage.RenderSize.ToString(), PreviewImageSize = previewSurface.Children.OfType<Image>().Single().RenderSize.ToString(), SourceImageDpi = VisualTreeHelper.GetDpi(actualImage).DpiScaleY, PreviewImageDpi = VisualTreeHelper.GetDpi(previewSurface.Children.OfType<Image>().Single()).DpiScaleY, SourceDrawingSize = actualImage.Source.Height, PreviewDrawingSize = ordinary.Media.Single().Image.Source.Height, SourceSnap = actualImage.SnapsToDevicePixels, PreviewSnap = previewSurface.Children.OfType<Image>().Single().SnapsToDevicePixels, SourceClip = actualImage.Clip?.Bounds.ToString(), PreviewClip = previewSurface.Children.OfType<Image>().Single().Clip?.Bounds.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Check(NativeSourceEditorChecks.Pixels(actualCanvas).SequenceEqual(NativeSourceEditorChecks.Pixels(previewCanvas)), family + ": complete actual WPF editor canvas including every unselected hit overlay exactly matches ordinary preview at identical viewport/layout options");
            var name = family + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + "-editor.png"), editorPng); File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), previewPng);
            File.WriteAllBytes(Path.Combine(output, name + "-actual-editor-canvas.png"), actualCanvas); File.WriteAllBytes(Path.Combine(output, name + "-ordinary-preview-canvas.png"), previewCanvas);
            foreach (var nl in new[] { "\n", "\r\n" })
            {
                var body = source.ReplaceLineEndings(nl); var block = "```mermaid" + nl + body.TrimEnd('\r', '\n') + nl + "```" + nl;
                var original = "Before café Ω" + nl + block + "Between" + nl + block + "After" + nl;
                var owner = new DocumentViewModel(original) { DisplayName = family + ".md" }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(original))[1];
                var change = family switch { "kanban" => body.Replace("Write guide", "Edited guide", StringComparison.Ordinal), "requirement" => body.Replace("DOC-17", "DOC-18", StringComparison.Ordinal), "architecture" => body.Replace("Gateway", "Edited gateway", StringComparison.Ordinal), "c4-context" or "block" => body.Replace("Reader", "Edited reader", StringComparison.Ordinal), "c4-container" => body.Replace("Editor", "Edited editor", StringComparison.Ordinal), "c4-component" => body.Replace("Parser", "Edited parser", StringComparison.Ordinal), _ => body.Replace("Start", "Edited start", StringComparison.Ordinal) };
                if (family == "packet") change = body.Replace("Source", "Edited source", StringComparison.Ordinal);
                if (family.StartsWith("treeview", StringComparison.Ordinal)) change = body.Replace("main.cs", "edited.cs", StringComparison.Ordinal);
                new MermaidDocumentEditSession(owner, target).Apply(change, true);
                Check(owner.EditorDocument.Text == original[..target.Body.Start] + change + original[target.Body.End..], family + ": exact second identical fence patch preserves neighbours and " + (nl == "\n" ? "LF" : "CRLF"));
                owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == original, family + ": owner Apply is one exact Undo step");
                owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text.Contains(change, StringComparison.Ordinal), family + ": owner Redo restores the complete patch");
                var conflict = new DocumentViewModel(original); var session = new MermaidDocumentEditSession(conflict, target); var prepared = session.PrepareAsync(change).GetAwaiter().GetResult(); conflict.EditorDocument.Insert(0, "Newer edit" + nl); var current = conflict.EditorDocument.Text;
                try { session.Commit(prepared, true); throw new IOException("Stale validation was committed."); } catch (ArgumentException) { }
                Check(conflict.EditorDocument.Text == current, family + ": asynchronous final owner-version conflict preserves both sources");
            }
        }
        File.WriteAllText(Path.Combine(output, expansionFamily is not null ? expansionFamily + "-editor-fidelity.json" : topology ? "topology-editor-fidelity.json" : "workflow-editor-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", SourceRevision = typeof(KanbanVisualEditorWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
    }
    private static void RenderActual(SourceVisualEditorWindow window)
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var task = (Task)typeof(SourceVisualEditorWindow).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(bool)], null)!.Invoke(window, [false])!;
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame(); var expired = false; var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
                timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
                timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); if (expired) throw new TimeoutException("Actual source-editor render exceeded its observation deadline.");
            }
            task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static byte[] CanvasPixels(Canvas canvas)
    {
        var size = new Size(canvas.Width, canvas.Height); canvas.Measure(size); canvas.Arrange(new Rect(size)); canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var bytes = new MemoryStream(); encoder.Save(bytes); return bytes.ToArray();
    }
}
