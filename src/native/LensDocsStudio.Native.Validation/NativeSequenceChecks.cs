using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSequenceChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var observations = new List<object>();
        foreach (var file in Directory.GetFiles("tests/fixtures/native-sequence", "*.mmd"))
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(file); var parser = new SequenceSourceParser(); var service = new DiagramService(); var svg = new NativeSvgService();
            var clock = Stopwatch.StartNew(); var model = parser.Parse(source); var parseMs = clock.Elapsed.TotalMilliseconds;
            Check(model.CanEdit, file + ": source recognition: " + string.Join(" ", model.Diagnostics));
            clock.Restart(); var render = service.Render(source, dark); var renderMs = clock.Elapsed.TotalMilliseconds;
            clock.Restart(); var presentation = NativeSequencePresentation.Create(model, render, svg); var presentationMs = clock.Elapsed.TotalMilliseconds;
            var reference = svg.Render(render.Svg!); var baseline = NativeSvgService.ToPng(reference, .75); var canvas = NativeSvgService.ToPng(presentation.Drawing, .75);
            Check(Pixels(baseline).SequenceEqual(Pixels(canvas)), file + ": canonical preview/editor pixels match exactly");
            foreach (var participant in model.Participants) Check(presentation.Targets.Any(item => item.Identity == "participant:" + participant.Id), file + ": participant geometry " + participant.Id);
            foreach (var message in model.Messages) Check(presentation.Targets.Any(item => item.Identity == message.Identity), file + ": message occurrence geometry " + message.Identity);
            foreach (var note in model.Notes) Check(presentation.Targets.Any(item => item.Identity == note.Identity), file + ": note geometry " + note.Identity);
            foreach (var activation in model.Activations.Where(item => item.Activate)) Check(presentation.Targets.Any(item => item.Identity == activation.Identity), file + ": simple activation geometry");
            var markdown = "Before\n\n```mermaid\n" + source + "\n```\n\nAfter"; var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single(); var selected = -1;
            var preview = new NativeMarkdownRenderer(svg).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = render }, _ => { }, (_, _) => { },
                new Dictionary<FencedCodeBlock, DrawingImage> { [code] = reference }, editDiagramSource: _ => { }, editDiagramVisually: offset => selected = offset);
            var visual = ((Panel)preview.Media.Single().Actions!).Children.OfType<Button>().Single(button => (string?)button.Content == "Visual Edit");
            visual.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(selected == code.Span.Start, file + ": Sequence preview routes the exact container offset");
            Check(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source, .75)).SequenceEqual(Pixels(canvas)), file + ": actual FlowDocument pixels equal editor pixels");
            var name = Path.GetFileNameWithoutExtension(file) + (dark ? "-dark" : "-light");
            File.WriteAllBytes(Path.Combine(output, name + "-preview.png"), baseline); File.WriteAllBytes(Path.Combine(output, name + "-canvas.png"), canvas);
            observations.Add(new { Fixture = name, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, NativePresentationMilliseconds = presentationMs, PixelTolerance = 0, Targets = presentation.Targets.Count });
        }
        var performance = new List<object>();
        foreach (var (participants, messages) in new[] { (2, 5), (10, 50), (20, 150) })
        {
            var source = "sequenceDiagram\n" + string.Join('\n', Enumerable.Range(0, participants).Select(index => "participant P" + index)) + "\n"
                + string.Join('\n', Enumerable.Range(0, messages).Select(index => "P" + (index % participants) + "->>P" + ((index + 1) % participants) + ": Message " + index)) + "\n";
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var model = new SequenceSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source); var renderMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); SequenceRenderCorrespondence.Validate(model, render); var validationMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var presentation = NativeSequencePresentation.Create(model, render, new()); var nativeMs = timer.Elapsed.TotalMilliseconds;
            var draft = new SequenceVisualDraft(source); timer.Restart(); for (var i = 0; i < 1000; i++) draft.Select(model.Messages[i % messages].Identity); var selectionMs = timer.Elapsed.TotalMilliseconds;
            Check(render.Success, "Performance fixture renders within existing resource limits: " + messages + " messages");
            performance.Add(new { Participants = participants, Messages = messages, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, SemanticValidationMilliseconds = validationMs,
                NativePresentationMilliseconds = nativeMs, ThousandPrivateSelectionsMilliseconds = selectionMs, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
                Scope = "Hosted thread measurements; includes cold work and allocations, excludes UIA transport. No universal responsiveness claim." });
        }
        File.WriteAllText(Path.Combine(output, "sequence-fidelity.json"), JsonSerializer.Serialize(new { Checks = checks, Observations = observations, Performance = performance }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
    }
    private static byte[] Pixels(byte[] png)
    {
        using var stream = new MemoryStream(png); var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bytes = new byte[frame.PixelHeight * ((frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8)]; frame.CopyPixels(bytes, (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8, 0); return bytes;
    }
}
