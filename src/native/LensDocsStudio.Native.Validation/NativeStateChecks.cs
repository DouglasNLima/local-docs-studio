using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.StateDiagrams;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;
using Mermaider;

namespace LensDocsStudio.Native.Validation;

internal static class NativeStateChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var fidelity = new List<object>(); var performance = new List<object>(); var syntax = new List<object>();
        foreach (var file in Directory.GetFiles("tests/fixtures/native-state", "*.mmd"))
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(file); var timer = Stopwatch.StartNew(); var model = new StateDiagramSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source, dark); var renderMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var native = new NativeSvgService(); var presentation = NativeStatePresentation.Create(model, render, native); var nativeMs = timer.Elapsed.TotalMilliseconds;
            var reference = native.Render(render.Svg!); var previewPng = NativeSvgService.ToPng(reference, .75); var canvasPng = NativeSvgService.ToPng(presentation.Canonical.Drawing, .75);
            Check(Pixels(previewPng).SequenceEqual(Pixels(canvasPng)), file + ": zero-tolerance canonical preview/editor pixels");
            Check(presentation.Canonical.Connections.Count == model.Transitions.Count, file + ": every source transition has a verified ordinal and path");
            foreach (var node in model.Nodes) Check(presentation.Elements.ContainsKey("node:" + node.Id), file + ": state geometry " + node.Id);
            foreach (var pseudo in model.PseudoNodes) Check(presentation.Elements["node:" + pseudo.RendererId] == pseudo.Identity, file + ": scoped marker identity " + pseudo.Identity);
            var markdown = "Before\n\n```mermaid\n" + source + "\n```\n\nAfter"; var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = render }, _ => { }, (_, _) => { },
                new Dictionary<FencedCodeBlock, System.Windows.Media.DrawingImage> { [code] = reference }, editDiagramSource: _ => { }, editDiagramVisually: offset => selected = offset);
            ((Panel)preview.Media.Single().Actions!).Children.OfType<Button>().Single(button => (string?)button.Content == "Visual Edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(selected == code.Span.Start, file + ": native State Visual Edit routes the exact container");
            Check(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source, .75)).SequenceEqual(Pixels(canvasPng)), file + ": real FlowDocument pixels equal editor pixels");
            var name = Path.GetFileNameWithoutExtension(file) + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), previewPng); File.WriteAllBytes(Path.Combine(output, name + "-canvas.png"), canvasPng);
            fidelity.Add(new { Fixture = name, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, NativeMilliseconds = nativeMs, PixelTolerance = 0 });
        }
        foreach (var (name, body, intended) in new[]
        {
            ("v2 named/implicit", "A --> B", true), ("alias", "state \"Display\" as A\nA --> A", true),
            ("initial/final", "[*] --> A\nA --> [*]", true), ("labelled self-transition/cycle", "A --> A : Repeat\nA --> B\nB --> A", true),
            ("nested composites", "state Outer {\nstate Inner {\n[*] --> A\nA --> [*]\n}\n}", true),
            ("choice/fork/join", "state C <<choice>>\nstate F <<fork>>\nstate J <<join>>\nC --> F\nF --> J", true),
            ("inline note", "A --> B\nnote right of A : Note", true), ("block note", "A --> B\nnote left of B\nFirst\nSecond\nend note", true),
            ("direction", "direction LR\nA --> B", true), ("comments", "%% Keep\nA --> B", true),
            ("description", "A : Description\nA --> B", true), ("style/class", "A --> B\nclassDef quiet fill:#fafafa\nclass A quiet\nstyle B stroke:#333", true),
            ("bare state command", "state A", false), ("concurrent regions", "A --> B\n--\nC --> D", false),
            ("internal action", "A --> B\nA : entry / action", false), ("unknown directive", "A --> B\nunknown directive", false),
            ("configuration", "%%{init: {'theme': 'dark'}}%%\nA --> B", false)
        })
        {
            var source = "stateDiagram-v2\n" + body + "\n"; var model = new StateDiagramSourceParser().Parse(source); var render = new DiagramService().Render(source);
            var graph = MermaidRenderer.Parse(source); string? mappingError = null;
            try { StateSemanticValidation.ValidateRendering(model, render); } catch (ArgumentException error) { mappingError = error.Message; }
            if (intended) Check(render.Success && mappingError is null, name + ": actual pinned fixture renders and maps");
            syntax.Add(new { Construct = name, Source = source, IntendedConstructSupported = intended, Rendered = render.Success, Nodes = graph.Nodes.Count, Transitions = graph.Edges.Count,
                SourceModelValid = model.IsValid, UnknownDependencies = model.HasUnknownDependencies, MappingError = mappingError,
                Note = intended ? "Verified against pinned 0.15.1" : "Rendering success alone does not prove the intended semantics; retained/protected" });
        }
        foreach (var (states, transitions) in new[] { (10, 15), (30, 60), (100, 150) })
        {
            var source = "stateDiagram-v2\n" + string.Join('\n', Enumerable.Range(0, states).Select(i => "state \"State " + i + "\" as S" + i)) + "\n"
                + string.Join('\n', Enumerable.Range(0, transitions).Select(i => "S" + (i % states) + " --> S" + ((i * 7 + 1) % states) + " : Review " + i)) + "\n";
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var model = new StateDiagramSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source); var renderMs = timer.Elapsed.TotalMilliseconds; double? nativeMs = null;
            if (render.Success) { timer.Restart(); _ = NativeStatePresentation.Create(model, render, new()); nativeMs = timer.Elapsed.TotalMilliseconds; }
            var draft = new StateVisualDraft(source); timer.Restart(); for (var i = 0; i < 1000; i++) draft.Select(draft.Model.Transitions[i % transitions].Identity); var selectionMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var edited = draft.ExecuteAsync(new UpdateStateNode("S0", "S0", "Updated label")).GetAwaiter().GetResult(); var editMs = timer.Elapsed.TotalMilliseconds;
            performance.Add(new { States = states, Transitions = transitions, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, NativeMilliseconds = nativeMs, ThousandPrivateSelectionsMilliseconds = selectionMs,
                OperationMilliseconds = editMs, RenderSuccess = render.Success, render.Error, EditSuccess = edited.Success, EditError = edited.Error, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
                LimitsUnchanged = true, Scope = "Hosted thread measurements including cold work; UIA drag/open timing recorded separately. Over-budget fixtures are observations, never a larger rendering limit." });
        }
        DocumentChecks(checks);
        File.WriteAllText(Path.Combine(output, "state-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks, Fidelity = fidelity, Syntax = syntax, Performance = performance }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
    }
    private static byte[] Pixels(byte[] png)
    { using var input = new MemoryStream(png); var bitmap = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; var stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8; var bytes = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(bytes, stride, 0); return bytes; }
    private static void DocumentChecks(List<string> checks)
    {
        var body = "stateDiagram-v2\r\nstate \"Keep\" as A\r\nA --> A : Repeat\r\n";
        foreach (var (opening, closing) in new[] { ("```mermaid", "```"), ("~~~~mermaid", "~~~~"), ("::: mermaid", ":::") })
        {
            var block = opening + "\r\n" + body + closing + "\r\n"; var source = "Before\r\n\r\n" + block + "\r\nMiddle\r\n\r\n" + block;
            var owner = new DocumentViewModel(source) { DisplayName = "Unsaved state.md" }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1];
            var first = new MermaidDocumentEditSession(owner, target); var competing = new MermaidDocumentEditSession(owner, target); var changedBody = body.Replace("Keep", "Changed", StringComparison.Ordinal);
            owner.EditorDocument.Insert(0, "Outside\r\n\r\n"); first.Apply(changedBody, true); var expected = "Outside\r\n\r\n" + source[..target.Body.Start] + changedBody + source[target.Body.End..];
            Check(owner.EditorDocument.Text == expected, opening + ": exact repeated State container relocation"); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == "Outside\r\n\r\n" + source, opening + ": one document Undo"); owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == expected, opening + ": exact document Redo");
            Reject(() => competing.Apply(changedBody, true), "Competing State editor cannot overwrite a source revision");
            var conflict = new DocumentViewModel(source); var session = new MermaidDocumentEditSession(conflict, target); conflict.EditorDocument.Insert(target.Body.Start + 1, "changed"); var retained = conflict.EditorDocument.Text;
            Reject(() => session.Apply(changedBody, true), "Overlapping external source changes reject State Apply"); Check(conflict.EditorDocument.Text == retained, "Rejected State Apply is atomic");
            Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(changedBody, false), "Closed State owner rejects Apply");
            var raced = new DocumentViewModel(source); var racedSession = new MermaidDocumentEditSession(raced, target); var prepared = racedSession.PrepareAsync(changedBody).GetAwaiter().GetResult(); raced.EditorDocument.Insert(0, "Race"); Reject(() => racedSession.Commit(prepared, true), "Final State Apply checks AvalonEdit ownership/version");
            Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(body + closing + "\r\nEscaped", true), "State draft cannot close its Markdown wrapper");
        }
        foreach (var extension in new[] { ".mmd", ".mermaid" })
        {
            var owner = new DocumentViewModel(body) { DisplayName = "State" + extension }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(body, true), true).Single(); new MermaidDocumentEditSession(owner, target).Apply(body.Replace("Keep", "Changed", StringComparison.Ordinal), true);
            Check(!owner.EditorDocument.Text.Contains("```", StringComparison.Ordinal), extension + ": State Apply preserves raw portable source"); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == body, extension + ": exact State source Undo");
        }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
    }
}
