using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

public sealed record ImportedAsset(string Reference, byte[] Bytes, string SuggestedName);
public sealed record ImportedDocument(string Markdown, IReadOnlyList<ImportedAsset> Assets, IReadOnlyList<string> Warnings);

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
                ".docx" => ImportDocx(bytes),
                ".html" or ".htm" => ImportHtml(new UTF8Encoding(false, true).GetString(bytes)),
                ".pdf" => ImportPdf(bytes, Path.GetFileNameWithoutExtension(path), cancellationToken),
                _ => throw new InvalidDataException("Select a DOCX, HTML or PDF document.")
            };
        }, cancellationToken);
    }

    public ImportedDocument ImportHtml(string html)
    {
        if (html.Length > 32 * 1024 * 1024) throw new InvalidDataException("HTML import is limited to 32 MB.");
        var document = new HtmlParser().ParseDocument(html);
        var assets = new List<ImportedAsset>(); var warnings = new List<string>();
        var pending = new Stack<(INode Node, int Depth)>(); pending.Push((document, 0));
        var nodes = 0;
        while (pending.TryPop(out var item))
        {
            if (++nodes > 100_000 || item.Depth > 128) throw new InvalidDataException("HTML import exceeds the document complexity limit.");
            foreach (var child in item.Node.ChildNodes) pending.Push((child, item.Depth + 1));
        }
        string Children(INode node) => string.Concat(node.ChildNodes.Select(Convert));
        string Convert(INode node)
        {
            if (node is IText text) return text.Data;
            if (node is not IElement element) return Children(node);
            if (element.LocalName is "script" or "style" or "iframe" or "object" or "embed" or "head" or "svg") return "";
            // Tables and preformatted text own their traversal; converting twice duplicates embedded assets.
            var content = element.LocalName is "table" or "pre" ? "" : Children(element);
            switch (element.LocalName)
            {
                case "script": case "style": case "iframe": case "object": case "embed": case "head": case "svg": return "";
                case "h1": case "h2": case "h3": case "h4": case "h5": case "h6": return "\n\n" + new string('#', element.LocalName[1] - '0') + " " + content.Trim() + "\n\n";
                case "p": case "div": case "section": case "article": return "\n\n" + content.Trim() + "\n\n";
                case "br": return "\n";
                case "hr": return "\n\n---\n\n";
                case "strong": case "b": return "**" + content + "**";
                case "em": case "i": return "*" + content + "*";
                case "del": case "s": return "~~" + content + "~~";
                case "pre": return "\n\n```text\n" + element.TextContent.TrimEnd() + "\n```\n\n";
                case "code": return element.ParentElement?.LocalName == "pre" ? content : "`" + content.Replace("`", "\\`") + "`";
                case "blockquote": return "\n\n" + string.Join("\n", content.Trim().Split('\n').Select(l => "> " + l)) + "\n\n";
                case "li": return (element.ParentElement?.LocalName == "ol" ? "1. " : "- ") + content.Trim() + "\n";
                case "ul": case "ol": return "\n\n" + content + "\n";
                case "a":
                    var href = element.GetAttribute("href") ?? "";
                    if (Uri.TryCreate(href, UriKind.Absolute, out var link) && link.Scheme is not ("http" or "https" or "mailto")) return content;
                    return "[" + content + "](" + href.Replace(" ", "%20").Replace(")", "%29") + ")";
                case "img":
                    var source = element.GetAttribute("src") ?? "";
                    var alt = (element.GetAttribute("alt") ?? "Image").Replace("]", "\\]");
                    if (source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = DataImage().Match(source);
                        if (!match.Success) { warnings.Add("Unsupported embedded image was skipped."); return "[Image: " + alt + "]"; }
                        var bytes = System.Convert.FromBase64String(match.Groups[2].Value);
                        var reference = "assets/images/import-" + Guid.NewGuid().ToString("N") + ".png";
                        assets.Add(new(reference, bytes, "imported-image")); source = reference;
                    }
                    else { warnings.Add("Linked image was retained as a reference; it was not fetched."); }
                    return "![" + alt + "](" + source.Replace(" ", "%20").Replace(")", "%29") + ")";
                case "table":
                    var rows = element.QuerySelectorAll("tr").Select(row => row.Children.Where(c => c.LocalName is "td" or "th")
                        .Select(cell => Children(cell).Trim().Replace("|", "\\|").Replace("\n", "<br>")).ToArray()).Where(r => r.Length > 0).ToArray();
                    if (rows.Length == 0) return "";
                    var columns = rows.Max(r => r.Length);
                    string Row(string[] cells) => "| " + string.Join(" | ", Enumerable.Range(0, columns).Select(i => i < cells.Length ? cells[i] : "")) + " |";
                    return "\n\n" + Row(rows[0]) + "\n" + Row(Enumerable.Repeat("---", columns).ToArray()) + "\n" + string.Join("\n", rows.Skip(1).Select(Row)) + "\n\n";
                default: return content;
            }
        }
        var markdown = Convert(document.Body ?? (INode)document.DocumentElement!);
        return new(BlankLines().Replace(markdown, "\n\n").Trim() + "\n", assets, warnings.Distinct().ToArray());
    }

    public ImportedDocument ImportDocx(byte[] bytes)
    {
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
        string Paragraph(W.Paragraph paragraph)
        {
            var text = new StringBuilder();
            foreach (var run in paragraph.Descendants<W.Run>())
            {
                var value = string.Concat(run.ChildElements.Select(e => e switch { W.Text t => t.Text, W.Break => "\n", W.TabChar => "\t", _ => "" }));
                if (run.RunProperties?.Bold is { } bold && bold.Val?.Value != false) value = "**" + value + "**";
                if (run.RunProperties?.Italic is { } italic && italic.Val?.Value != false) value = "*" + value + "*";
                text.Append(value);
                foreach (var blip in run.Descendants<A.Blip>())
                {
                    var id = blip.Embed?.Value;
                    if (id is null) { warnings.Add("An external Word image was not fetched."); continue; }
                    if (!imageReferences.TryGetValue(id, out var reference))
                    {
                        if (main.GetPartById(id) is not ImagePart image || image.ContentType is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/bmp"))
                        { warnings.Add("An unsupported Word image was skipped."); continue; }
                        using var imageStream = image.GetStream();
                        using var data = new MemoryStream(); imageStream.CopyTo(data);
                        reference = "assets/images/import-" + Guid.NewGuid().ToString("N") + ".png";
                        assets.Add(new(reference, data.ToArray(), "word-image"));
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
    [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp|bmp);base64,([A-Za-z0-9+/=\s]+)$", RegexOptions.IgnoreCase)] private static partial Regex DataImage();
    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+")] private static partial Regex BlankLines();
    [GeneratedRegex(@"^heading\s*([1-6])$", RegexOptions.IgnoreCase)] private static partial Regex HeadingStyle();
}
