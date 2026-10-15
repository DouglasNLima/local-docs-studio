using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeDiagramFailureChecks
{
    public static void Run(string output)
    {
        var cases = new[]
        {
            (Name: "missing-arrow-destination", Body: "flowchart LR\nA -->", NativeFailure: false, Success: false),
            (Name: "standalone-source", Body: "flowchart LR\nA -->", NativeFailure: false, Success: false),
            (Name: "devops-container", Body: "flowchart LR\nA -->", NativeFailure: false, Success: false),
            (Name: "unrecognised-diagram", Body: "unrecognisedDiagram\nRetain this source", NativeFailure: false, Success: false),
            (Name: "native-drawing-failure", Body: "flowchart LR\nA[Write] --> B[Review]", NativeFailure: true, Success: false),
            (Name: "oversized-source", Body: "flowchart LR\n%% " + new string('x', 100_001), NativeFailure: false, Success: false),
            (Name: "valid-control", Body: "flowchart LR\nA[Write] --> B[Review]", NativeFailure: false, Success: true)
        };
        var checks = new List<object>();
        foreach (var test in cases)
        foreach (var dark in new[] { false, true })
        {
            var source = "# Before the diagram\n\n```mermaid\n" + test.Body + "\n```\n\nAfter the diagram.\n";
            if (test.Name == "standalone-source") source = test.Body;
            if (test.Name == "devops-container") source = source.Replace("```", ":::", StringComparison.Ordinal);
            var parsed = new MarkdownService().Parse(source, test.Name == "standalone-source");
            var code = parsed.Syntax.Descendants<FencedCodeBlock>().Single();
            var diagram = new DiagramService().Render(code.Lines.ToString(), dark);
            if (test.NativeFailure)
                diagram = diagram with { Svg = "<svg xmlns='http://www.w3.org/2000/svg'><script/></svg>" };
            string? copied = null; var target = -1; var copyCalls = 0; var editCalls = 0;
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark,
                new Dictionary<FencedCodeBlock, DiagramResult> { [code] = diagram }, _ => { }, (format, value) =>
                {
                    if (format != "Copy source") throw new IOException("An error action tried to export a missing drawing.");
                    copied = value; copyCalls++;
                }, editDiagramSource: offset => { target = offset; editCalls++; });
            var host = result.Document.Blocks.OfType<BlockUIContainer>().Single();
            var panel = (StackPanel)host.Child;
            var actions = panel.Children.OfType<WrapPanel>().Single();
            var buttons = actions.Children.OfType<Button>().ToArray();
            var copy = buttons.Single(button => (string)button.Content == "Copy source");
            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (copied != diagram.Source || diagram.Source != test.Body || copyCalls != 1 || parsed.Source != source
                || host.Tag is not SourceReference reference || reference.Start != code.Span.Start
                || code.Span.Start != (test.Name == "standalone-source" ? 0 : 22))
                throw new IOException("Diagram recovery changed source or copied/navigated the wrong block.");
            if (test.Success)
            {
                if (!buttons.Select(button => (string)button.Content).SequenceEqual(new[] { "Zoom", "SVG", "PNG", "Copy source", "Edit source" })
                    || result.Warnings.Count != 0 || panel.Children.OfType<Image>().Count() != 1 || editCalls != 0)
                    throw new IOException("Valid diagrams lost their existing native image/export actions.");
                var sourceAction = buttons.Single(button => (string)button.Content == "Edit source");
                if (new ButtonAutomationPeer(sourceAction).GetPattern(PatternInterface.Invoke) is null || !sourceAction.Focusable || !sourceAction.IsTabStop)
                    throw new IOException("A valid diagram's added source action lost native keyboard/automation access.");
                sourceAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (target != code.Span.Start || editCalls != 1 || parsed.Source != source)
                    throw new IOException("A valid diagram's source action navigated to a different source block.");
            }
            else
            {
                var error = panel.Children.OfType<Label>().Single();
                var errorPeer = new LabelAutomationPeer(error);
                var edit = buttons.Single(button => (string)button.Content == "Edit source");
                var editPeer = new ButtonAutomationPeer(edit);
                if (buttons.Length != 2 || result.Warnings.Count != 1 || errorPeer.GetName() != result.Warnings[0]
                    || AutomationProperties.GetAutomationId(error) != "DiagramError" + code.Span.Start
                    || editPeer.GetPattern(PatternInterface.Invoke) is null || !edit.Focusable || !edit.IsTabStop
                    || panel.Children.OfType<Image>().Any())
                    throw new IOException("A failed diagram lost its accessible recovery actions or exposed an invalid export.");
                edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (target != code.Span.Start || editCalls != 1 || copyCalls != 1 || parsed.Source != source)
                    throw new IOException("Editing a diagram did not route exactly to its unchanged source block.");
            }
            if (test.Name == "missing-arrow-destination")
            {
                ThemeService.Apply(result.Document.Resources, dark ? "Dark" : "Light");
                result.Document.Background = (Brush)result.Document.Resources["WindowBrush"];
                var paginator = ((IDocumentPaginatorSource)result.Document).DocumentPaginator;
                paginator.PageSize = new Size(780, 320);
                var page = paginator.GetPage(0);
                if (buttons.Any(button => button.ActualWidth < 40 || button.ActualHeight < 20))
                    throw new IOException("The diagram recovery controls have no usable layout.");
                var pixels = new RenderTargetBitmap(780, 320, 96, 96, PixelFormats.Pbgra32); pixels.Render(page.Visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels));
                using var file = File.Create(Path.Combine(output, "native-diagram-error-" + (dark ? "dark" : "light") + ".png")); encoder.Save(file);
            }
            checks.Add(new { test.Name, Dark = dark, SourceCharacters = source.Length, SourceStart = code.Span.Start,
                SourcePreserved = parsed.Source == source, CopyExact = copied == diagram.Source, CopyCalls = copyCalls,
                EditCalls = editCalls, EditTarget = target, Actions = buttons.Select(button => (string)button.Content).ToArray() });
        }
        File.WriteAllText(Path.Combine(output, "native-diagram-failures.json"), JsonSerializer.Serialize(new
        { Passed = true, Checks = checks, Limitation = "Managed native controls and callbacks; no system clipboard or foreground input used." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
