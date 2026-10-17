using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSequenceChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var observations = new List<object>();
        CheckDocumentOwnership(checks);
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
            foreach (var activation in model.Activations.Where(item => item.Activate && model.Activations.Count(other => other.Activate && other.Participant == item.Participant) == 1))
                Check(presentation.Targets.Any(item => item.Identity == activation.Identity), file + ": unique simple activation geometry");
            if (Path.GetFileName(file) == "repeated-blocks-activations.mmd")
            {
                Check(model.Statements.Where(item => item.Kind == SequenceStatementKind.Block).All(item => presentation.Targets.All(target => target.Identity != item.Identity)), file + ": repeated block headings never map to the wrong source occurrence");
                Check(presentation.Diagnostics.Any(item => item.Contains("block", StringComparison.Ordinal)) && presentation.Diagnostics.Any(item => item.Contains("activation", StringComparison.Ordinal)), file + ": ambiguous canvas regions explain the exact-list inspection alternative");
            }
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
        foreach (var colour in new[] { "rgb(240,240,240)", "rgba(240,240,240,0.5)", "rgb(100%,50%,0%)" })
        {
            var source = "sequenceDiagram\nbox " + colour + " Services\nparticipant A\nparticipant B\nend\nA->>B: Protected grouping\n";
            var render = new DiagramService().Render(source); var native = new NativeSvgService();
            Check(render.Success, "Pinned renderer accepts protected grouping colour " + colour);
            var prepared = native.Prepare(render.Svg!);
            Check(!prepared.Contains("color-mix(", StringComparison.Ordinal), "Functional RGB interpolation resolves before native conversion: " + colour);
            Check(NativeSvgService.ToPng(native.Render(render.Svg!), .5).Length > 100, "Protected grouping retains actual native PNG output: " + colour);
        }
        foreach (var (participants, messages) in new[] { (2, 5), (10, 50), (20, 150) })
        {
            var source = "sequenceDiagram\n" + string.Join('\n', Enumerable.Range(0, participants).Select(index => "participant P" + index)) + "\n"
                + string.Join('\n', Enumerable.Range(0, messages).Select(index => "P" + (index % participants) + "->>P" + ((index + 1) % participants) + ": Message " + index)) + "\n";
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var model = new SequenceSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source); var renderMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); SequenceRenderCorrespondence.Validate(model, render); var validationMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var presentation = NativeSequencePresentation.Create(model, render, new()); var nativeMs = timer.Elapsed.TotalMilliseconds;
            var draft = new SequenceVisualDraft(source); timer.Restart(); for (var i = 0; i < 1000; i++) draft.Select(draft.Model.Messages[i % messages].Identity); var selectionMs = timer.Elapsed.TotalMilliseconds;
            Check(render.Success, "Performance fixture renders within existing resource limits: " + messages + " messages");
            performance.Add(new { Participants = participants, Messages = messages, ParseMilliseconds = parseMs, RenderMilliseconds = renderMs, SemanticValidationMilliseconds = validationMs,
                NativePresentationMilliseconds = nativeMs, ThousandPrivateSelectionsMilliseconds = selectionMs, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
                Scope = "Hosted thread measurements; includes cold work and allocations, excludes UIA transport. No universal responsiveness claim." });
        }
        foreach (var (name, source) in new[]
        {
            ("Long labels", "sequenceDiagram\nparticipant A as " + new string('A', 180) + "\nparticipant B as Studio\nA->>B: " + new string('x', 600) + "\nB-->>A: Response\n"),
            ("Notes and standalone activations", File.ReadAllText("tests/fixtures/native-sequence/local-document.mmd")),
            ("Nested structural blocks", File.ReadAllText("tests/fixtures/native-sequence/blocks.mmd")),
            ("Protected grouping", "sequenceDiagram\nbox rgb(240,240,240) Services\nparticipant A\nparticipant B\nend\nA->>B: Protected grouping\n")
        })
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var model = new SequenceSourceParser().Parse(source); var parseMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var render = new DiagramService().Render(source); var renderMs = timer.Elapsed.TotalMilliseconds;
            Check(render.Success, name + ": performance fixture uses the canonical renderer");
            timer.Restart(); if (model.CanEdit) SequenceRenderCorrespondence.Validate(model, render); var semanticMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); if (model.CanEdit) _ = NativeSequencePresentation.Create(model, render, new()); else _ = new NativeSvgService().Render(render.Svg!); var nativeMs = timer.Elapsed.TotalMilliseconds;
            performance.Add(new { Scenario = name, ParseMilliseconds = parseMs, SemanticValidationMilliseconds = model.CanEdit ? (double?)semanticMs : null,
                RenderMilliseconds = renderMs, NativePresentationMilliseconds = nativeMs, ReadOnly = !model.CanEdit, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated });
        }
        var edited = new SequenceVisualDraft("sequenceDiagram\nparticipant A\nparticipant B\nA->>B: Original\n"); var editTimer = Stopwatch.StartNew();
        var trigger = edited.Model.Messages[0].Identity;
        for (var index = 0; index < 10; index++)
        {
            var result = edited.ExecuteAsync(new UpdateSequenceMessage(trigger, "A", "B", "Edit " + index, "->>")).GetAwaiter().GetResult();
            Check(result.Success, "Repeated validated message edit " + index + ": " + result.Error);
        }
        var editMs = editTimer.Elapsed.TotalMilliseconds; var retained = edited.Source; var retainedIdentity = edited.HistoryIdentity;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); editTimer.Restart();
        var rejection = edited.ExecuteAsync(new AddSequenceMessage("A", "B", "Cancelled"), cancelled.Token).GetAwaiter().GetResult();
        Check(!rejection.Success && edited.Source == retained && edited.HistoryIdentity == retainedIdentity, "Pre-cancelled work leaves source and private history unchanged");
        performance.Add(new { Scenario = "Repeated edits and cancellation", TenValidatedEditsMilliseconds = editMs, PreCancelledEditMilliseconds = editTimer.Elapsed.TotalMilliseconds,
            CancellationScope = "Pre-cancelled source operation. SharpVectors conversion itself is non-interruptible; production serialises it and rejects stale results." });
        File.WriteAllText(Path.Combine(output, "sequence-fidelity.json"), JsonSerializer.Serialize(new { Checks = checks, Observations = observations, Performance = performance }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
    }
    private static byte[] Pixels(byte[] png)
    {
        using var stream = new MemoryStream(png); var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bytes = new byte[frame.PixelHeight * ((frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8)]; frame.CopyPixels(bytes, (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8, 0); return bytes;
    }
    private static void CheckDocumentOwnership(List<string> checks)
    {
        const string body = "sequenceDiagram\r\n    participant A\r\n    participant B\r\n    A->>B: Keep\r\n";
        foreach (var (opening, closing) in new[] { ("```mermaid", "```"), ("~~~~mermaid", "~~~~"), ("::: mermaid", ":::") })
        {
            var block = opening + "\r\n" + body + closing + "\r\n"; var source = "Before\r\n\r\n" + block + "\r\nMiddle\r\n\r\n" + block;
            var owner = new DocumentViewModel(source) { DisplayName = "Unsaved sequence.md" }; var other = new DocumentViewModel("Other tab");
            var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1]; var first = new MermaidDocumentEditSession(owner, target); var competing = new MermaidDocumentEditSession(owner, target);
            var draft = body.Replace("Keep", "Changed", StringComparison.Ordinal); owner.EditorDocument.Insert(0, "Outside\r\n\r\n"); first.Apply(draft, true);
            var expected = "Outside\r\n\r\n" + source[..target.Body.Start] + draft + source[target.Body.End..];
            Check(owner.EditorDocument.Text == expected && other.EditorDocument.Text == "Other tab", opening + ": identical Sequence container relocates safely without redirecting to another tab");
            owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == "Outside\r\n\r\n" + source, opening + ": one document Undo retains outside changes");
            owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == expected, opening + ": document Redo restores exact Sequence source");
            Reject(() => competing.Apply(draft, true), opening + ": a competing editor cannot overwrite a committed Sequence session");
            var conflict = new DocumentViewModel(source); var session = new MermaidDocumentEditSession(conflict, target); conflict.EditorDocument.Insert(target.Body.Start + 1, "changed"); var changed = conflict.EditorDocument.Text;
            Reject(() => session.Apply(draft, true), opening + ": overlapping source revisions reject Apply"); Check(conflict.EditorDocument.Text == changed, opening + ": rejected Apply retains independent source");
            var closed = new DocumentViewModel(source); Reject(() => new MermaidDocumentEditSession(closed, target).Apply(draft, false), opening + ": closed owner rejects Apply");
            var raced = new DocumentViewModel(source); var racedSession = new MermaidDocumentEditSession(raced, target); var prepared = racedSession.PrepareAsync(draft).GetAwaiter().GetResult(); raced.EditorDocument.Insert(0, "Race\r\n");
            Reject(() => racedSession.Commit(prepared, true), opening + ": changed version rejects prepared Apply");
        }
        foreach (var extension in new[] { ".mmd", ".mermaid" })
        {
            var owner = new DocumentViewModel(body) { DisplayName = "Sequence" + extension }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(body, true), true).Single();
            new MermaidDocumentEditSession(owner, target).Apply(body.Replace("Keep", "Changed", StringComparison.Ordinal), true);
            Check(!owner.EditorDocument.Text.Contains("```", StringComparison.Ordinal) && owner.EditorDocument.Text.Contains("Changed", StringComparison.Ordinal), extension + ": standalone Apply retains ordinary raw Mermaid");
            owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == body, extension + ": standalone Undo restores exact source");
        }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
    }
}
