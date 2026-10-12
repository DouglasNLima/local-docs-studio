using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeDiagramThemeChecks
{
    public static void Run(string output)
    {
        const string source = "```mermaid\nflowchart LR\nA[Café] --> B[Review]\n```\n";
        var root = Path.Combine(output, "diagram-themes-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspace);
        var path = Path.Combine(workspace, "theme.md"); File.WriteAllText(path, source);
        var parsed = new MarkdownService().Parse(source);
        var code = parsed.Syntax.Descendants<FencedCodeBlock>().Single();
        var outcomes = new List<object>(); var fixedImages = new Dictionary<string, byte[]>();
        foreach (var choice in DiagramThemes.Choices)
        foreach (var dark in new[] { false, true })
        {
            var resolved = DiagramThemes.ResolveName(choice, dark);
            var diagram = new DiagramService().Render(code.Lines.ToString(), dark, choice);
            if (!diagram.Success || diagram.Theme != resolved) throw new IOException("The selected diagram theme was not rendered.");
            var svg = new NativeSvgService(); var drawing = svg.Render(diagram.Svg!);
            var png = NativeSvgService.ToPng(drawing); var bitmap = Decode(png);
            var corner = new byte[4]; bitmap.CopyPixels(new Int32Rect(1, 1, 1, 1), corner, 4, 0);
            var expected = resolved switch { "Dark" => "#202124", "Default" => "#FFFFFF", "Forest" => "#F7FCF7", _ => "#FAFAFA" };
            var colour = (Color)ColorConverter.ConvertFromString(expected);
            if (!corner.SequenceEqual(new byte[] { colour.B, colour.G, colour.R, 255 })) throw new IOException("The selected theme background was not painted.");
            if (choice != "Auto")
            {
                if (fixedImages.TryGetValue(choice, out var previous)) SamePixels(previous, png);
                else fixedImages.Add(choice, png);
            }
            var preview = new NativeMarkdownRenderer(svg).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = diagram }, _ => { }, (_, _) => { });
            var image = ((StackPanel)preview.Document.Blocks.OfType<BlockUIContainer>().Single().Child).Children.OfType<Image>().Single();
            if (AutomationProperties.GetHelpText(image) != "Diagram theme: " + resolved) throw new IOException("The native preview does not identify its actual rendered theme.");

            var label = choice.ToLowerInvariant() + (dark ? "-dark-app" : "-light-app");
            File.WriteAllText(Path.Combine(root, label + ".svg"), diagram.Svg);
            File.WriteAllBytes(Path.Combine(root, label + ".png"), png);
            var exporter = new DocumentExportService();
            var html = exporter.ExportAsync("HTML", source, path, workspace, dark, null, diagramTheme: choice).GetAwaiter().GetResult();
            SamePixels(png, HtmlImage(Encoding.UTF8.GetString(html)));
            var word = exporter.ExportAsync("DOCX", source, path, workspace, dark, null, diagramTheme: choice).GetAwaiter().GetResult();
            File.WriteAllBytes(Path.Combine(root, label + ".docx"), word);
            using (var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(word), false))
            {
                var media = package.MainDocumentPart!.ImageParts.Single();
                using var input = media.GetStream(); using var bytes = new MemoryStream(); input.CopyTo(bytes); SamePixels(png, bytes.ToArray());
            }
            var pdf = exporter.ExportAsync("PDF", source, path, workspace, dark, null, diagramTheme: choice).GetAwaiter().GetResult();
            File.WriteAllBytes(Path.Combine(root, label + ".pdf"), pdf);
            using (var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf))
            {
                var embedded = inspected.GetPages().SelectMany(page => page.GetImages()).Single();
                if (!embedded.TryGetPng(out var bytes)) throw new IOException("The PDF diagram image could not be inspected.");
                SamePixels(png, bytes);
            }
            var copied = new ClipboardExportService().PrepareAsync(source, path, workspace, dark, diagramTheme: choice).GetAwaiter().GetResult();
            SamePixels(png, HtmlImage(HtmlClipboardFormat.Decode(copied.Html)));
            var markdownCopy = new ClipboardExportService().PrepareAsync(source, path, workspace, dark, markdownSource: true, diagramTheme: choice).GetAwaiter().GetResult();
            SamePixels(png, HtmlImage(HtmlClipboardFormat.Decode(markdownCopy.Html)));
            // The site appearance deliberately differs; diagrams retain the reviewed resolved choice.
            var site = new DocsSiteExportService().ExportAsync(workspace, new Dictionary<string, string>(), Path.Combine(root, "state"),
                new("Theme fixture", "Native diagram colours", dark ? "light" : "dark"), diagramTheme: resolved).GetAwaiter().GetResult();
            File.WriteAllBytes(Path.Combine(root, label + "-site.zip"), site.Bytes);
            using (var package = new ZipArchive(new MemoryStream(site.Bytes), ZipArchiveMode.Read))
            using (var input = package.GetEntry("assets/search-index.json")!.Open())
            using (var index = JsonDocument.Parse(input))
                SamePixels(png, HtmlImage(index.RootElement.GetProperty("pages").EnumerateArray()
                    .Single(page => page.GetProperty("path").GetString() == "theme.md").GetProperty("html").GetString()!));
            if (File.ReadAllText(path) != source || parsed.Source != source) throw new IOException("Changing a diagram theme changed Markdown source.");
            outcomes.Add(new { Choice = choice, ApplicationDark = dark, Resolved = resolved, Background = expected,
                Passed = true, PreviewPixels = true, Html = true, Word = true, Pdf = true, RichCopy = true, MarkdownCopy = true, DocsSite = true, SourcePreserved = true });
        }
        if (fixedImages.Values.Select(bytes => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))).Distinct().Count() != 5)
            throw new IOException("The fixed diagram themes did not produce distinct native images.");
        File.WriteAllText(Path.Combine(output, "native-diagram-themes.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Cases = outcomes, Artefacts = root,
            Limitation = "Native flowchart and exported-image pixels; palette equivalents, not pixel identity with browser Mermaid. No system clipboard or foreground desktop used."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static byte[] HtmlImage(string html)
    {
        var image = new HtmlParser().ParseDocument(html).QuerySelector("img")?.GetAttribute("src") ?? throw new IOException("The exported diagram is missing.");
        const string prefix = "data:image/png;base64,";
        if (!image.StartsWith(prefix, StringComparison.Ordinal)) throw new IOException("The diagram is not an embedded PNG.");
        return Convert.FromBase64String(image[prefix.Length..]);
    }
    private static FormatConvertedBitmap Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new(BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
    }
    private static void SamePixels(byte[] expected, byte[] actual)
    {
        var left = Decode(expected); var right = Decode(actual);
        if (left.PixelWidth != right.PixelWidth || left.PixelHeight != right.PixelHeight) throw new IOException("Exported diagram dimensions differ from preview.");
        var stride = left.PixelWidth * 4; var a = new byte[stride * left.PixelHeight]; var b = new byte[a.Length];
        left.CopyPixels(a, stride, 0); right.CopyPixels(b, stride, 0);
        if (!a.SequenceEqual(b)) throw new IOException("Exported diagram pixels differ from the reviewed preview theme.");
    }
}
