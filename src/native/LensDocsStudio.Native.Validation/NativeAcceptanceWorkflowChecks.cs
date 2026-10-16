using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using MdTable = Markdig.Extensions.Tables.Table;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Native.Validation;

internal static class NativeAcceptanceWorkflowChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "acceptance-workflow-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var workspace = Path.Combine(root, "original"); Directory.CreateDirectory(workspace);
        var path = Path.Combine(workspace, "aceitacao.md");
        var pixels = new byte[16 * 12 * 4];
        for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = 62; pixels[index + 1] = 136; pixels[index + 2] = 255; pixels[index + 3] = 255; }
        var bitmap = BitmapSource.Create(16, 12, 96, 96, PixelFormats.Bgra32, null, pixels, 64);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var image = new MemoryStream(); encoder.Save(image);
        var reference = new ImageAssetService().ImportAsync(workspace, path, image.ToArray(), "Imagem portuguesa.png").GetAwaiter().GetResult();
        const string tsv = "Nome\tValor\tNotas\nCafé\t000123\t\"primeira linha\nsegunda linha\"\nAção\t=1+1\t\"disse \"\"Olá\"\"\"";
        var tableSource = new ClipboardPasteService().Convert(new(tsv, ""), PasteMode.Table).Document.Markdown;
        var source = "# Aceitação brasileira\n\nCafé, ação, coração e São Paulo. **Revisão concluída**.\n\n"
            + "```mermaid\nflowchart LR\n A[Escrever] --> B[Rever] --> C[Exportar]\n```\n\n"
            + "![Imagem](" + reference + ")\n\n![Repetida](" + reference + ")\n\n"
            + "[Guia](guias/nivel-2/guia.md#revisao) e [[guias/nivel-2/guia#Revisão|Revisão]].\n\n"
            + tableSource + "\n\n1. Abrir o documento.\n2. Guardar as alterações.\n\n- Imagens portáteis.\n- Conteúdo editável.";
        var store = new DocumentStore(Path.Combine(root, "state", "writes"));
        var original = store.SaveAsync(path, source, null).GetAwaiter().GetResult();
        var guide = Path.Combine(workspace, "guias", "nivel-2", "guia.md"); Directory.CreateDirectory(Path.GetDirectoryName(guide)!);
        store.SaveAsync(guide, "# Revisão\n\nGuia interno com ação e Café.", null).GetAwaiter().GetResult();
        var bundles = new MarkdownBundleService(); var bundle = bundles.ExportAsync(new(workspace)).GetAwaiter().GetResult();
        var bundlePath = Path.Combine(root, "workspace.zip"); File.WriteAllBytes(bundlePath, bundle);
        var copied = bundles.ImportAsync(bundlePath, Path.Combine(root, "copied")).GetAwaiter().GetResult();
        var copiedPath = Path.Combine(copied.Root, "aceitacao.md");
        Require(copied.Documents == 2 && copied.Assets == 1 && copied.Warnings.Count == 0, "Bundle document/asset counts differ.");
        Require(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(copiedPath)), "Bundle changed persisted source bytes.");
        var originalImage = new WorkspacePath(workspace).ResolveLink(path, reference);
        var copiedImage = new WorkspacePath(copied.Root).ResolveLink(copiedPath, reference);
        Require(File.ReadAllBytes(originalImage).SequenceEqual(File.ReadAllBytes(copiedImage)), "Copied workspace lost image bytes.");
        var markdown = new MarkdownService(); var diagrams = new DiagramService(); var svg = new NativeSvgService();
        var content = new ExportPreparationService(markdown, diagrams, svg).Prepare(File.ReadAllText(copiedPath), copiedPath, copied.Root, false);
        Require(content.Images.Count == 1 && content.Diagrams.Count == 1, "Copied exports lost repeated image reuse or Mermaid.");
        var selected = content.Document.Syntax.Descendants<MdTable>().Single(); var selection = new ExportTableSelection(selected.Span.Start, selected.Span.Length);
        var clipboard = new ClipboardExporter().Export(content, selection);
        Require(clipboard.PlainText == tsv, "Table clipboard changed quoted newlines, identifiers or Unicode.");
        File.WriteAllText(Path.Combine(root, "table-unicode.txt"), clipboard.PlainText); File.WriteAllText(Path.Combine(root, "table-cfhtml.txt"), clipboard.Html);
        var csv = new TableCsvExporter().Export(source, selection);
        File.WriteAllBytes(Path.Combine(root, "table.csv"), csv.Bytes);
        var reimportedCsv = new ClipboardPasteService().Convert(new(System.Text.Encoding.UTF8.GetString(csv.Bytes), ""), PasteMode.Table);
        var csvContent = new ExportContent(markdown.Parse(reimportedCsv.Document.Markdown), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        Require(new ClipboardExporter().Export(csvContent).PlainText == tsv, "CSV round-trip changed cell values.");
        var diagramResults = content.Document.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info == "mermaid")
            .ToDictionary(code => code, code => diagrams.Render(code.Lines.ToString(), false));
        var preview = new NativeMarkdownRenderer(svg).Render(content.Document, false, diagramResults, _ => { }, (_, _) => { });
        Require(preview.ReadText().Contains("primeira linha\r\nsegunda linha", StringComparison.Ordinal), "Native pasted table lost its line break.");
        Require(!preview.ReadText().Contains("<br>", StringComparison.Ordinal), "Native preview exposed generated HTML syntax.");
        var docx = new DocxExporter().Export(content); File.WriteAllBytes(Path.Combine(root, "portuguese.docx"), docx);
        using (var package = WordprocessingDocument.Open(new MemoryStream(docx), false))
        {
            Require(!new OpenXmlValidator().Validate(package).Any(), "Generated DOCX is not schema-valid.");
            var blips = package.MainDocumentPart!.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().ToArray();
            Require(blips.Length == 3 && blips.Select(blip => blip.Embed!.Value).Distinct().Count() == 2
                && package.MainDocumentPart.ImageParts.Count() == 2, "DOCX lost image occurrences or repeated-asset reuse.");
            Require(package.MainDocumentPart.Document!.Descendants<W.Break>().Any(), "DOCX lost the pasted table break.");
        }
        var imported = new DocumentImportService().ImportDocx(docx);
        Require(imported.Markdown.Contains("Café", StringComparison.Ordinal) && imported.Markdown.Contains("000123", StringComparison.Ordinal), "Word import lost literal Portuguese content.");
        var pdf = new PdfExporter().Export(content); File.WriteAllBytes(Path.Combine(root, "portuguese.pdf"), pdf);
        using (var package = UglyToad.PdfPig.PdfDocument.Open(pdf))
            Require(package.GetPages().SelectMany(page => page.Letters).Any(letter => letter.Value.Contains('é')), "PDF lost selectable Unicode text.");
        var html = new HtmlExporter().Export(content); File.WriteAllText(Path.Combine(root, "portuguese.html"), html);
        var htmlDocument = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        Require(htmlDocument.QuerySelectorAll("img").Length == 3 && htmlDocument.QuerySelectorAll("td br").Length == 1, "HTML lost images or table breaks.");
        var siteExporter = new DocsSiteExporter();
        var guideContent = new ExportPreparationService(markdown, diagrams, svg).Prepare(File.ReadAllText(guide), guide, workspace, false);
        var site = siteExporter.Export([siteExporter.PreparePage("aceitacao.md", content), siteExporter.PreparePage("guias/nivel-2/guia.md", guideContent)], new("Documentação brasileira"));
        File.WriteAllBytes(Path.Combine(root, "docs-site.zip"), site.Bytes);
        using (var archive = new ZipArchive(new MemoryStream(site.Bytes))) Require(archive.GetEntry("index.html") is not null, "Docs Site has no standalone entry point.");
        var diagram = diagramResults.Values.Single(); File.WriteAllText(Path.Combine(root, "diagram.svg"), diagram.Svg!);
        File.WriteAllBytes(Path.Combine(root, "diagram.png"), content.Diagrams.Values.Single().PngBytes);
        var template = Template(); File.WriteAllBytes(Path.Combine(root, "synthetic-template.docx"), template);
        var templated = new DocxExporter().Export(content, template); File.WriteAllBytes(Path.Combine(root, "portuguese-template.docx"), templated);
        using (var package = WordprocessingDocument.Open(new MemoryStream(templated), false))
        {
            Require(!new OpenXmlValidator().Validate(package).Any(), "Templated DOCX is not schema-valid.");
            Require(package.MainDocumentPart!.HeaderParts.Single().Header!.InnerText == "Cabeçalho sintético — Café", "Template header changed.");
            Require(package.MainDocumentPart.FooterParts.Single().Footer!.InnerText == "Rodapé sintético — São Paulo", "Template footer changed.");
        }
        Require((store.ReadAsync(path).GetAwaiter().GetResult()).Text == original.Text, "Exports changed the original workspace.");
        File.WriteAllText(Path.Combine(output, "native-acceptance-workflow.json"), JsonSerializer.Serialize(new
        {
            Status = "PASS", Root = root, copied.Documents, copied.Assets, SourcePreserved = true,
            SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            Checks = new[] { "Nested Portuguese workspace save/reopen", "Bundle byte-preserving round-trip to a different root", "Repeated portable image bytes and export reuse",
                "Native Mermaid preview and SVG/PNG export", "Native pasted table line break", "Quoted TSV and CSV round-trips", "Schema-valid DOCX with images/line breaks", "DOCX import",
                "PDF selectable Unicode", "HTML images and line breaks", "Standalone Docs Site", "Synthetic Word template header/footer", "Original workspace preserved" },
            Limitation = "Public-service/actual-artefact validation; it does not certify interactive destination pagination, live Azure DevOps or a clean Windows installation."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new IOException(message); }
    private static byte[] Template()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); main.Document = new W.Document(new W.Body());
            var header = main.AddNewPart<HeaderPart>(); header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Cabeçalho sintético — Café"))));
            var footer = main.AddNewPart<FooterPart>(); footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("Rodapé sintético — São Paulo"))));
            main.Document.Body!.Append(new W.SectionProperties(new W.HeaderReference { Id = main.GetIdOfPart(header), Type = W.HeaderFooterValues.Default },
                new W.FooterReference { Id = main.GetIdOfPart(footer), Type = W.HeaderFooterValues.Default }));
        }
        return stream.ToArray();
    }
}
