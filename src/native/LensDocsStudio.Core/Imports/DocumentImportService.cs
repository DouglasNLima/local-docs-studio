using System.IO.Compression;
using System.Text;
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
        var paragraphs = (main.Document?.Body?.ChildElements ?? []).SelectMany(element => element switch
        {
            W.Paragraph paragraph => new[] { paragraph },
            W.Table table => table.Elements<W.TableRow>().SelectMany(r => r.Elements<W.TableCell>()).SelectMany(c => c.Elements<W.Paragraph>()),
            _ => Enumerable.Empty<W.Paragraph>()
        });
        var hyperlinks = new WordHyperlinkReader(main, paragraphs, warnings, cancellationToken);
        var paragraphStyles = new WordParagraphStyles(main.StyleDefinitionsPart?.Styles, warnings);
        string Run(W.Run run)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = new StringBuilder();
            var value = string.Concat(run.ChildElements.Select(e => e switch { W.Text t => HtmlMarkdownConverter.Escape(t.Text), W.Break => "\n", W.TabChar => "\t", _ => "" }));
            if (run.RunProperties?.Bold is { } bold && bold.Val?.Value != false) value = Emphasis(value, "**");
            if (run.RunProperties?.Italic is { } italic && italic.Val?.Value != false) value = Emphasis(value, "*");
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
                text.Append("![").Append(HtmlMarkdownConverter.Escape(WordImageDescription.Read(blip)))
                    .Append("](").Append(reference).Append(')');
            }
            return text.ToString();
        }
        string Paragraph(W.Paragraph paragraph, bool inTable = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var style = paragraphStyles.Read(paragraph);
            if (style.IsCode && !paragraph.Descendants<A.Blip>().Any())
            {
                var literal = hyperlinks.Read(paragraph, run => string.Concat(run.ChildElements.Select(e => e switch
                { W.Text text => text.Text, W.Break or W.CarriageReturn => "\n", W.TabChar => "\t", _ => "" })), literal: true);
                var anchors = hyperlinks.CodeAnchors(paragraph);
                if (inTable)
                {
                    warnings.Add("Word code blocks inside table cells were imported as literal text.");
                    return anchors + HtmlMarkdownConverter.Escape(literal).Replace("\n", "<br>", StringComparison.Ordinal);
                }
                return (anchors.Length == 0 ? "" : anchors + "\n\n") + HtmlMarkdownConverter.Fence(literal);
            }
            if (style.IsCode) warnings.Add("A Word code-styled paragraph containing an image was imported as ordinary content.");
            var text = hyperlinks.Read(paragraph, Run);
            var prefix = style.HeadingLevel > 0 ? new string('#', style.HeadingLevel) + " " : "";
            return prefix + text;
        }
        static string Emphasis(string value, string marker)
        {
            var start = 0; var end = value.Length;
            while (start < end && char.IsWhiteSpace(value[start])) start++;
            while (end > start && char.IsWhiteSpace(value[end - 1])) end--;
            return start == end ? value : value[..start] + marker + value[start..end] + marker + value[end..];
        }
        var output = new WordListMarkdownWriter(main.NumberingDefinitionsPart?.Numbering, main.StyleDefinitionsPart?.Styles, warnings);
        foreach (var element in main.Document?.Body?.ChildElements ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is W.Paragraph paragraph) output.Paragraph(paragraph, Paragraph(paragraph), paragraphStyles.Read(paragraph).IsQuote);
            else if (element is W.Table table)
            {
                var rows = table.Elements<W.TableRow>().Select(r => r.Elements<W.TableCell>().Select(c =>
                    output.Cell(c.Elements<W.Paragraph>(), p => Paragraph(p, inTable: true), p => paragraphStyles.Read(p).IsQuote)).ToArray()).ToArray();
                if (rows.Length == 0) continue;
                var text = new StringBuilder();
                text.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
                text.AppendLine("| " + string.Join(" | ", rows[0].Select(_ => "---")) + " |");
                foreach (var row in rows.Skip(1)) text.AppendLine("| " + string.Join(" | ", row) + " |");
                var indent = table.GetFirstChild<W.TableProperties>()?.TableIndentation;
                output.Block(text.ToString().TrimEnd('\r', '\n'), indent?.Type?.Value == W.TableWidthUnitValues.Dxa ? indent.Width?.Value : null);
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
}
