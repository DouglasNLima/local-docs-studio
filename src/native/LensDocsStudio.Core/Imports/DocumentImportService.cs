using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

public sealed record ImportedAsset(string Reference, byte[] Bytes, string SuggestedName);
public sealed record ImportedDocument(string Markdown, IReadOnlyList<ImportedAsset> Assets, IReadOnlyList<string> Warnings);
public sealed record WordImportLimits(long MaximumImageBytes = 32 * 1024 * 1024,
    long MaximumTotalImageBytes = 64 * 1024 * 1024, int MaximumImages = 1024);

public sealed partial class DocumentImportService
{
    public async Task<ImportedDocument> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new IOException("Document import is limited to 32 MB.");
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".docx" => ImportDocx(bytes, cancellationToken),
                ".html" or ".htm" => ImportHtml(new UTF8Encoding(false, true).GetString(bytes), cancellationToken),
                ".pdf" => ImportPdf(bytes, Path.GetFileNameWithoutExtension(path), cancellationToken),
                _ => throw new InvalidDataException("Select a DOCX, HTML or PDF document.")
            };
        }, cancellationToken);
    }

    public ImportedDocument ImportHtml(string html, CancellationToken cancellationToken = default) => new HtmlMarkdownConverter().Convert(html, cancellationToken);

    public ImportedDocument ImportDocx(byte[] bytes, CancellationToken cancellationToken = default, WordImportLimits? limits = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        limits ??= new();
        if (limits.MaximumImageBytes < 1 || limits.MaximumTotalImageBytes < 1 || limits.MaximumImages < 1)
            throw new ArgumentOutOfRangeException(nameof(limits));
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        {
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 256 * 1024 * 1024 || zip.Entries.Any(e => e.Length > 32 * 1024 * 1024))
                throw new InvalidDataException("The Word package exceeds the import limits.");
        }
        using var stream = new MemoryStream(bytes, false);
        using var package = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 32 * 1024 * 1024 });
        var main = package.MainDocumentPart ?? throw new InvalidDataException("The Word document has no body.");
        var assets = new List<ImportedAsset>(); var warnings = new List<string>();
        var imageReferences = new Dictionary<string, string>();
        var partReferences = new Dictionary<Uri, string>();
        long retainedImageBytes = 0;
        string Paragraph(W.Paragraph paragraph)
        {
            var text = new StringBuilder();
            foreach (var run in paragraph.Descendants<W.Run>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = string.Concat(run.ChildElements.Select(e => e switch { W.Text t => t.Text, W.Break => "\n", W.TabChar => "\t", _ => "" }));
                if (run.RunProperties?.Bold is { } bold && bold.Val?.Value != false) value = "**" + value + "**";
                if (run.RunProperties?.Italic is { } italic && italic.Val?.Value != false) value = "*" + value + "*";
                text.Append(value);
                foreach (var blip in run.Descendants<A.Blip>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = blip.Embed?.Value;
                    if (id is null) { warnings.Add("An external Word image was not fetched."); continue; }
                    if (!imageReferences.TryGetValue(id, out var reference))
                    {
                        if (main.GetPartById(id) is not ImagePart image || image.ContentType is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/bmp"))
                        { warnings.Add("An unsupported Word image was skipped."); continue; }
                        // Relationship aliases can resolve to the same package part. Retain that payload only once.
                        if (!partReferences.TryGetValue(image.Uri, out reference))
                        {
                            if (assets.Count >= limits.MaximumImages) throw new InvalidDataException("The Word document contains too many images.");
                            var remaining = Math.Min(limits.MaximumImageBytes, limits.MaximumTotalImageBytes - retainedImageBytes);
                            using var imageStream = image.GetStream();
                            using var data = new MemoryStream();
                            var buffer = new byte[64 * 1024];
                            while (true)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var count = imageStream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining - data.Length + 1));
                                if (count == 0) break;
                                if (count > remaining - data.Length) throw new InvalidDataException("Word images exceed the import size limit.");
                                data.Write(buffer, 0, count);
                            }
                            reference = "assets/images/import-" + Guid.NewGuid().ToString("N") + ".png";
                            assets.Add(new(reference, data.ToArray(), "word-image"));
                            retainedImageBytes += data.Length;
                            partReferences.Add(image.Uri, reference);
                        }
                        imageReferences[id] = reference;
                    }
                    text.Append("![Imported image](").Append(reference).Append(')');
                }
            }
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var styleName = main.StyleDefinitionsPart?.Styles?.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == styleId)?.StyleName?.Val?.Value ?? styleId ?? "";
            var heading = HeadingStyle().Match(styleName);
            var prefix = styleName.Equals("Title", StringComparison.OrdinalIgnoreCase) ? "# " : heading.Success ? new string('#', Math.Min(6, int.Parse(heading.Groups[1].Value) + 1)) + " " : "";
            var number = paragraph.ParagraphProperties?.NumberingProperties;
            if (number is not null)
            {
                var definitionId = main.NumberingDefinitionsPart?.Numbering?.Elements<W.NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == number.NumberingId?.Val?.Value)?.AbstractNumId?.Val?.Value;
                var format = main.NumberingDefinitionsPart?.Numbering?.Elements<W.AbstractNum>().FirstOrDefault(n => n.AbstractNumberId?.Value == definitionId)?.Elements<W.Level>().FirstOrDefault()?.NumberingFormat?.Val;
                prefix = format?.Value == W.NumberFormatValues.Bullet ? "- " : "1. ";
            }
            return prefix + text;
        }
        var output = new StringBuilder();
        foreach (var element in main.Document?.Body?.ChildElements ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is W.Paragraph paragraph) output.AppendLine(Paragraph(paragraph)).AppendLine();
            else if (element is W.Table table)
            {
                var rows = table.Elements<W.TableRow>().Select(r => r.Elements<W.TableCell>().Select(c =>
                    string.Join("<br>", c.Elements<W.Paragraph>().Select(Paragraph)).Replace("|", "\\|")).ToArray()).ToArray();
                if (rows.Length == 0) continue;
                output.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
                output.AppendLine("| " + string.Join(" | ", rows[0].Select(_ => "---")) + " |");
                foreach (var row in rows.Skip(1)) output.AppendLine("| " + string.Join(" | ", row) + " |");
                output.AppendLine();
            }
        }
        return new(output.ToString(), assets, warnings);
    }

    public ImportedDocument ImportPdf(byte[] bytes, string name, CancellationToken cancellationToken = default)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(bytes);
        if (document.NumberOfPages > 1000) throw new InvalidDataException("PDF text import is limited to 1,000 pages.");
        var output = new StringBuilder("# " + name + "\n\n");
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Append("## Page ").Append(page.Number).Append("\n\n");
            output.AppendLine(UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page)).AppendLine();
        }
        return new(output.ToString(), [], ["PDF import extracts text only; it does not perform OCR or reconstruct images and page layout."]);
    }
    [GeneratedRegex(@"^heading\s*([1-6])$", RegexOptions.IgnoreCase)] private static partial Regex HeadingStyle();
}
