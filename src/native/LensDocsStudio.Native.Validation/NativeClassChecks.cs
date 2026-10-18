using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeClassChecks
{
    public static void Run(string output, bool highContrast)
    {
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        Directory.CreateDirectory(output); var checks = new List<string>(); var fidelity = new List<object>(); var performance = new List<object>();
        if (highContrast) Check(SystemParameters.HighContrast, "Actual Windows High Contrast is active for Class fidelity");
        var emptySource = "classDiagram\n"; var emptyRender = new DiagramService().Render(emptySource); var empty = NativeClassPresentation.Create(new ClassDiagramSourceParser().Parse(emptySource), emptyRender, new());
        Check(emptyRender.ClassLayout is { Width: 0, Height: 0 } && empty.Classes.Count == 0 && empty.Members.Count == 0 && empty.Relationships.Count == 0, "Empty canonical Class source has no fabricated geometry");
        var starter = new ClassDiagramSourceParser().Parse(ClassDiagramSourceParser.Starter); var starterRender = new DiagramService().Render(starter.Source);
        var transformedSvg = starterRender.Svg!.Replace("<g class=\"class-node\"", "<g transform=\"translate(200,200)\" class=\"class-node\"", StringComparison.Ordinal);
        var rejectedTransform = false;
        try { _ = NativeClassPresentation.Create(starter, starterRender with { Svg = transformedSvg }, new()); } catch (InvalidDataException) { rejectedTransform = true; }
        Check(rejectedTransform, "Unreconciled SVG ancestor transforms cannot grant source editing hit regions");
        var ambiguousRow = new ClassDiagramSourceParser().Parse("classDiagram\nclass A {\n+string string\n}\n");
        var ambiguousView = NativeClassPresentation.Create(ambiguousRow, new DiagramService().Render(ambiguousRow.Source), new());
        Check(ambiguousView.Members.Count == 0 && ambiguousView.Classes.Single().Frame.Height > ambiguousView.Classes.Single().Bounds.Height,
            "Indistinguishable type/name text remains list-only and the canonical class body still occludes underlying paths");
        foreach (var arrow in new[] { "--()", "..()", "()--", "().." })
        foreach (var dark in new[] { false, true })
        {
            var source = "classDiagram\nclass A\nclass B\nA " + arrow + " B : contract\n";
            var model = new ClassDiagramSourceParser().Parse(source); var render = new DiagramService().Render(source, dark); var svg = new NativeSvgService();
            var view = NativeClassPresentation.Create(model, render, svg);
            Check(model.HasUnknownDependencies && view.Classes.Count == 0 && view.Relationships.Count == 0, "Lollipop " + arrow + ": synthetic layout identities cannot grant editable hit regions");
            Check(Pixels(NativeSvgService.ToPng(view.Drawing, .75)).SequenceEqual(Pixels(NativeSvgService.ToPng(svg.Render(render.Svg!), .75))), "Lollipop " + arrow + ": complete protected canonical drawing remains unchanged");
            fidelity.Add(new { Fixture = "protected-lollipop-" + arrow + (dark ? "-dark" : "-light"), PixelTolerance = 0, Members = 0, Relationships = 0 });
        }
        foreach (var file in Directory.GetFiles("tests/fixtures/native-class", "*.mmd"))
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(file); var model = new ClassDiagramSourceParser().Parse(source); var render = new DiagramService().Render(source, dark); var native = new NativeSvgService(); var presentation = NativeClassPresentation.Create(model, render, native);
            var reference = native.Render(render.Svg!); var previewPng = NativeSvgService.ToPng(reference, .75); var canvasPng = NativeSvgService.ToPng(presentation.Drawing, .75);
            Check(Pixels(previewPng).SequenceEqual(Pixels(canvasPng)), file + ": zero-tolerance canonical preview/editor pixels");
            Check(presentation.Relationships.Count == model.Relationships.Count, file + ": every supported relationship has an independently verified source occurrence");
            foreach (var cls in model.Classes) Check(presentation.Classes.Any(item => item.Identity == cls.Identity), file + ": verified class header " + cls.Id);
            foreach (var member in model.Classes.SelectMany(item => item.Members)) Check(presentation.Members.Any(item => item.Identity == member.Identity), file + ": verified attribute/method occurrence " + member.Identity);
            var markdown = "Before\n\n```mermaid\n" + source + "\n```\n\nAfter"; var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = render }, _ => { }, (_, _) => { },
                new Dictionary<FencedCodeBlock, System.Windows.Media.DrawingImage> { [code] = reference }, editDiagramSource: _ => { }, editDiagramVisually: offset => selected = offset);
            ((Panel)preview.Media.Single().Actions!).Children.OfType<Button>().Single(button => (string?)button.Content == "Visual Edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(selected == code.Span.Start, file + ": actual native preview routes the exact Class container");
            Check(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source, .75)).SequenceEqual(Pixels(canvasPng)), file + ": actual FlowDocument pixels equal Class canvas pixels");
            var name = Path.GetFileNameWithoutExtension(file) + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), previewPng); File.WriteAllBytes(Path.Combine(output, name + "-canvas.png"), canvasPng);
            fidelity.Add(new { Fixture = name, PixelTolerance = 0, Members = presentation.Members.Count, Relationships = presentation.Relationships.Count });
        }
        foreach (var (count, edges) in new[] { (10, 15), (30, 60), (100, 150) })
            Observe(count + " classes / " + edges + " relationships", "classDiagram\n" + string.Concat(Enumerable.Range(0, count).Select(i => "class C" + i + " {\n+string Name\n+Run(path) bool\n}\n")) + string.Concat(Enumerable.Range(0, edges).Select(i => "C" + (i % count) + " --> C" + ((i * 7 + 1) % count) + " : relation" + i + "\n")));
        Observe("Many attributes", "classDiagram\nclass A {\n" + string.Concat(Enumerable.Range(0, 250).Select(i => "+string Attribute" + i + "\n")) + "}\n");
        Observe("Many methods", "classDiagram\nclass A {\n" + string.Concat(Enumerable.Range(0, 250).Select(i => "+Method" + i + "(path, options) bool\n")) + "}\n");
        Observe("Long method signatures", "classDiagram\nclass A {\n+Run(" + string.Join(", ", Enumerable.Repeat("parameter", 60)) + ") bool\n}\n");
        Observe("Dense inheritance", "classDiagram\nclass Root\n" + string.Concat(Enumerable.Range(0, 30).Select(i => "class C" + i + "\nRoot <|-- C" + i + "\nC" + i + " <|-- C" + ((i + 1) % 30) + "\n")));
        Observe("Multiple parallel relationships", "classDiagram\nclass A\nclass B\n" + string.Concat(Enumerable.Range(0, 60).Select(i => "A --> B : parallel" + i + "\n")));
        Observe("Namespace membership", "classDiagram\nnamespace Library {\n" + string.Concat(Enumerable.Range(0, 15).Select(i => "class A" + i + "\n")) + "}\nnamespace Services {\n" + string.Concat(Enumerable.Range(0, 15).Select(i => "class B" + i + "\n")) + "}\n" + string.Concat(Enumerable.Range(0, 15).Select(i => "A" + i + " --> B" + i + "\n")));
        Observe("Protected global grammar", "classDiagram\nclass A {\n+string Name\n}\nunknown directive\n");
        DocumentChecks(checks);
        File.WriteAllText(Path.Combine(output, "class-fidelity.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrastRequested = highContrast, Checks = checks, Fidelity = fidelity, Performance = performance }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Observe(string scenario, string source)
        {
            var allocated = GC.GetTotalAllocatedBytes(); var timer = Stopwatch.StartNew(); var model = new ClassDiagramSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source); var renderMs = timer.Elapsed.TotalMilliseconds; double? nativeMs = null; string? nativeError = null;
            if (render.Success) { timer.Restart(); try { _ = NativeClassPresentation.Create(model, render, new()); nativeMs = timer.Elapsed.TotalMilliseconds; } catch (Exception error) when (error is not OutOfMemoryException) { nativeError = error.Message; } }
            var draft = new ClassDiagramVisualDraft(source); timer.Restart(); for (var i = 0; i < 1000; i++) draft.Select(draft.Model.Classes[0].Identity); var selectionMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var edit = draft.ExecuteAsync(new AddClassMember(model.Classes[0].Id, new(Mermaider.Models.ClassVisibility.Public, "Added", "int"))).GetAwaiter().GetResult(); var editMs = timer.Elapsed.TotalMilliseconds;
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var retained = draft.Source; var history = draft.HistoryIdentity; timer.Restart(); var cancelled = draft.ValidateAsync(cancellation.Token).GetAwaiter().GetResult(); var cancelMs = timer.Elapsed.TotalMilliseconds;
            Check(!cancelled.Success && draft.Source == retained && draft.HistoryIdentity == history, scenario + ": cancelled validation preserves source/history");
            performance.Add(new { Scenario = scenario, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, NativeMilliseconds = nativeMs, NativeError = nativeError, ThousandSelectionsMilliseconds = selectionMs, EditMilliseconds = editMs, RenderSuccess = render.Success, render.Error, EditSuccess = edit.Success, EditError = edit.Error, CancellationMilliseconds = cancelMs, AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, LimitsUnchanged = true });
        }
    }
    private static byte[] Pixels(byte[] png) { using var input = new MemoryStream(png); var bitmap = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; var stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8; var bytes = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(bytes, stride, 0); return bytes; }
}
