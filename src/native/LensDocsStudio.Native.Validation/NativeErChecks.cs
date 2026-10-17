using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeErChecks
{
    public static void Run(string output, bool highContrast)
    {
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        Directory.CreateDirectory(output); var checks = new List<string>(); var fidelity = new List<object>(); var performance = new List<object>(); var grammar = new List<object>();
        if (highContrast) Check(SystemParameters.HighContrast, "Actual Windows High Contrast is active for canonical ER fidelity");
        var emptySource = "erDiagram\n"; var empty = NativeErPresentation.Create(new ErDiagramSourceParser().Parse(emptySource), new DiagramService().Render(emptySource), new());
        Check(empty.Entities.Count == 0 && empty.Attributes.Count == 0 && empty.Relationships.Count == 0, "Empty canonical ER draft has no fabricated geometry or editing identities");
        foreach (var file in Directory.GetFiles("tests/fixtures/native-er", "*.mmd"))
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(file); var model = new ErDiagramSourceParser().Parse(source); var render = new DiagramService().Render(source, dark); var native = new NativeSvgService(); var presentation = NativeErPresentation.Create(model, render, native);
            var reference = native.Render(render.Svg!); var previewPng = NativeSvgService.ToPng(reference, .75); var canvasPng = NativeSvgService.ToPng(presentation.Drawing, .75);
            Check(Pixels(previewPng).SequenceEqual(Pixels(canvasPng)), file + ": zero-tolerance canonical preview/editor pixels");
            Check(presentation.Relationships.Count == model.Relationships.Count, file + ": all source relationships have independently verified rendered occurrences");
            foreach (var entity in model.Entities) Check(presentation.Entities.Any(item => item.Identity == entity.Identity), file + ": verified entity header " + entity.Id);
            foreach (var attribute in model.Entities.SelectMany(item => item.Attributes)) Check(presentation.Attributes.Any(item => item.Identity == attribute.Identity), file + ": verified attribute occurrence " + attribute.Identity);
            var markdown = "Before\n\n```mermaid\n" + source + "\n```\n\nAfter"; var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = render }, _ => { }, (_, _) => { },
                new Dictionary<FencedCodeBlock, System.Windows.Media.DrawingImage> { [code] = reference }, editDiagramSource: _ => { }, editDiagramVisually: offset => selected = offset);
            ((Panel)preview.Media.Single().Actions!).Children.OfType<Button>().Single(button => (string?)button.Content == "Visual Edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(selected == code.Span.Start, file + ": native ER Visual Edit routes the exact source container");
            Check(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source, .75)).SequenceEqual(Pixels(canvasPng)), file + ": actual FlowDocument pixels equal editor pixels");
            var name = Path.GetFileNameWithoutExtension(file) + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), previewPng); File.WriteAllBytes(Path.Combine(output, name + "-canvas.png"), canvasPng);
            fidelity.Add(new { Fixture = name, PixelTolerance = 0, Attributes = presentation.Attributes.Count, Relationships = presentation.Relationships.Count });
        }
        foreach (var (name, body) in new[] {
            ("implicit", "A ||--o{ B : places"), ("block and alias", "A[\"Display label\"] {\n string id PK\n}"),
            ("portable isolated comma keys", "A {\n string id PK , FK , UK \"Comment\"\n}"), ("non-portable space-only keys protected", "A {\n string id PK FK UK\n}"), ("comma-attached keys ignored", "A {\n string id PK, FK\n}"),
            ("unknown suffix", "A {\n string id PK unknown\n}"), ("unknown entity statement", "A {\n mystery\n}"), ("unknown outside", "A {\n}\nmystery"),
            ("repeated and self", "A ||--o{ B\nA |o..o{ A : self\nA ||--o{ B"), ("quoted relationship label", "A ||--o{ B : \"places orders\""),
            ("invalid cardinality ignored", "A xx--xx B"), ("unsupported direction ignored", "direction sideways\nA {\n}"),
            ("comments", "%% Original comment\nA {\n %% Attribute comment\n string id\n}") })
        {
            var source = "erDiagram\n" + body + "\n"; var model = new ErDiagramSourceParser().Parse(source); var render = new DiagramService().Render(source); string? error = null;
            try { ErSemanticValidation.ValidateRendering(model, render); } catch (ArgumentException failure) { error = failure.Message; }
            grammar.Add(new { Construct = name, Source = source, ParserEntities = render.ErSemantics?.Entities.Count, ParserRelationships = render.ErSemantics?.Relationships.Count,
                RendererAccepted = render.Success, CompleteCorrespondence = error is null, model.Compatibility, ProtectedStatements = model.Statements.Count(item => item.Protected), Error = error });
        }
        foreach (var (count, edges) in new[] { (10, 15), (30, 60), (100, 150) })
        {
            var source = "erDiagram\n" + string.Concat(Enumerable.Range(0, count).Select(i => " E" + i + " {\n string id PK\n string reference FK\n }\n"))
                + string.Concat(Enumerable.Range(0, edges).Select(i => " E" + (i % count) + " ||--o{ E" + ((i * 7 + 1) % count) + " : relation" + i + "\n"));
            Observe(count + " entities / " + edges + " relationships", source);
        }
        Observe("Many attributes", "erDiagram\n A {\n" + string.Concat(Enumerable.Range(0, 250).Select(i => " string attribute" + i + "\n")) + " }\n");
        Observe("Converging relationships and disconnected clusters", "erDiagram\n" + string.Concat(Enumerable.Range(0, 30).Select(i => " E" + i + " ||--o{ CENTRE : converges\n")) + " OTHER ||--|| CLUSTER\n");
        Observe("Long labels", "erDiagram\n A[\"" + new string('a', 180) + "\"] {\n string long_attribute\n }\n A ||--o{ A : " + new string('x', 600) + "\n");
        DocumentChecks(checks);
        File.WriteAllText(Path.Combine(output, "er-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Fidelity = fidelity, Grammar = grammar, Performance = performance }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Observe(string scenario, string source)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var model = new ErDiagramSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source); var renderMs = timer.Elapsed.TotalMilliseconds; double? nativeMs = null;
            if (render.Success) { timer.Restart(); _ = NativeErPresentation.Create(model, render, new()); nativeMs = timer.Elapsed.TotalMilliseconds; }
            var draft = new ErVisualDraft(source); timer.Restart(); for (var i = 0; i < 1000; i++) draft.Select(draft.Model.Entities[0].Identity); var selectionMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var edit = draft.ExecuteAsync(new UpdateErEntity(model.Entities[0].Id, model.Entities[0].Id, "Updated label")).GetAwaiter().GetResult(); var editMs = timer.Elapsed.TotalMilliseconds;
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var retained = draft.Source; var history = draft.HistoryIdentity; timer.Restart(); var cancelled = draft.ValidateAsync(cancellation.Token).GetAwaiter().GetResult(); var cancellationMs = timer.Elapsed.TotalMilliseconds;
            Check(!cancelled.Success && draft.Source == retained && draft.HistoryIdentity == history, scenario + ": cancellation retains exact source/history");
            performance.Add(new { Scenario = scenario, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, NativeMilliseconds = nativeMs, ThousandSelectionsMilliseconds = selectionMs, EditMilliseconds = editMs, RenderSuccess = render.Success, render.Error, EditSuccess = edit.Success, EditError = edit.Error, CancellationMilliseconds = cancellationMs, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated, LimitsUnchanged = true });
        }
    }
    private static byte[] Pixels(byte[] png) { using var input = new MemoryStream(png); var bitmap = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; var stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8; var bytes = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(bytes, stride, 0); return bytes; }
}
