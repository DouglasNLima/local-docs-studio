using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using W = DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWordListChecks
{
    public static void Run(string output, string repository)
    {
        var sourcePath = Path.Combine(repository, "tests", "fixtures", "native", "pdf-lists.md");
        var source = File.ReadAllText(sourcePath);
        var preparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        var content = preparation.Prepare(source, sourcePath, Path.GetDirectoryName(sourcePath)!, false);
        var bytes = new DocxExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-word-lists.docx"), bytes);
        using var package = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Validate(package);
        var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        var markers = paragraphs.Where(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value > 0).ToArray();
        if (markers.Length != 12 || paragraphs.Where(p => p.InnerText.Contains("Continuation", StringComparison.Ordinal))
            .Any(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value != 0))
            throw new IOException("Native Word preparation lost list item boundaries or continuation numbering overrides.");

        const string visualSource = "2. ```mermaid\n   flowchart LR\n   A[Write] --> B[Review]\n   ```\n\n   DiagramContinuation\n\n3. $$\n   x^2 + y^2\n   $$\n\n   FormulaContinuation\n\nAfterVisualLists";
        var visualPath = Path.Combine(output, "native-word-list-visuals.md"); File.WriteAllText(visualPath, visualSource);
        var visuals = preparation.Prepare(visualSource, visualPath, output, false);
        var visualBytes = new DocxExporter().Export(visuals);
        File.WriteAllBytes(Path.Combine(output, "native-word-list-visuals.docx"), visualBytes);
        using var visualPackage = WordprocessingDocument.Open(new MemoryStream(visualBytes), false);
        Validate(visualPackage);
        var visualParagraphs = visualPackage.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        var images = visualParagraphs.SelectMany(p => p.Descendants<DW.Inline>()).ToArray();
        if (visuals.Diagrams.Count != 1 || visuals.Formulae.Count != 1 || images.Length != 2
            || visualParagraphs.Count(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value > 0) != 2
            || visualParagraphs.Where(p => p.InnerText.EndsWith("Continuation", StringComparison.Ordinal))
                .Any(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value != 0)
            || images.Any(i => i.Extent?.Cx?.Value is null or <= 0 || i.Extent.Cx.Value > (11906 - 2268 - 360) * 635L))
            throw new IOException("Native diagrams or formulae lost list markers, continuations or available-width bounds in Word.");
        if (content.Document.Source != source || visuals.Document.Source != visualSource) throw new IOException("Word list preparation changed source.");
        File.WriteAllText(Path.Combine(output, "native-word-lists.json"), JsonSerializer.Serialize(new
        {
            Passed = true, MarkedItems = markers.Length, NativeDiagramImages = visuals.Diagrams.Count, NativeFormulaImages = visuals.Formulae.Count,
            Checks = new[] { "Native preparation retains list item boundaries and explicit unnumbered continuations",
                "First-block Mermaid and maths retain editable list numbering and bounded native images",
                "Both actual DOCX packages pass Open XML schema validation and source remains unchanged" },
            Paragraphs = paragraphs.Select(p => new { Text = p.InnerText, NumberingId = p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value,
                Level = p.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value, LeftIndent = p.ParagraphProperties?.Indentation?.Left?.Value }),
            Limitation = "Package semantics and native preparation are checked; actual Microsoft Word pagination, wide-marker layout and screen-reader behaviour remain unverified."
        }, new JsonSerializerOptions { WriteIndented = true }));

        var imported = new DocumentImportService().ImportDocx(bytes);
        var importedDocument = new MarkdownService().Parse(imported.Markdown);
        var importedLists = importedDocument.Syntax.Descendants<ListBlock>().ToArray();
        var importedVisuals = new DocumentImportService().ImportDocx(visualBytes);
        var importedVisualSyntax = new MarkdownService().Parse(importedVisuals.Markdown).Syntax;
        File.WriteAllText(Path.Combine(output, "native-word-lists-reimported.md"), imported.Markdown);
        File.WriteAllText(Path.Combine(output, "native-word-list-visuals-reimported.md"), importedVisuals.Markdown);
        var expectedImages = visuals.Diagrams.Values.Concat(visuals.Formulae.Values).Select(i => i.PngBytes).ToArray();
        if (importedLists.Length != 6 || importedLists.Sum(l => l.Count) != 12
            || !content.Document.Headings.Select(h => h.Level).SequenceEqual(importedDocument.Headings.Select(h => h.Level))
            || !importedLists.Where(l => l.IsOrdered).Select(l => l.OrderedStart).SequenceEqual(new[] { "7", "3", "0", "999999998" })
            || imported.Warnings.Count != 0 || importedVisuals.Warnings.Count != 0
            || importedVisualSyntax.Descendants<ListItemBlock>().Count() != 2 || importedVisuals.Assets.Count != 2
            || expectedImages.Any(expected => !importedVisuals.Assets.Any(asset => expected.SequenceEqual(asset.Bytes))))
            throw new IOException("Word reimport changed list hierarchy, starting numbers, item boundaries or native image bytes.");
        foreach (var token in new[] { "ContinuationTop", "NestedContinuation", "DeepContinuation", "ContinuationAfterNested", "HeadingContinuation", "CodeContinuation" })
        {
            var continuation = importedLists.SelectMany(l => l.OfType<ListItemBlock>()).SelectMany(i => i.OfType<ParagraphBlock>())
                .SingleOrDefault(p => MarkdownService.InlineText(p.Inline).StartsWith(token, StringComparison.Ordinal));
            if (continuation?.Parent is not ListItemBlock parent || parent[0] == continuation)
                throw new IOException("The reimported Word continuation '" + token + "' did not retain its parent item.");
        }
        File.WriteAllText(Path.Combine(output, "native-word-list-import.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Lists = importedLists.Length, Items = importedLists.Sum(l => l.Count), Continuations = 6,
            OrderedStarts = importedLists.Where(l => l.IsOrdered).Select(l => l.OrderedStart), ExactNativeImages = importedVisuals.Assets.Count,
            Limitation = "Direct Word numbering and matching continuation indents are checked. Style-inherited numbering, field/composite markers and full Word layout or formatting round trips remain unverified."
        }, new JsonSerializerOptions { WriteIndented = true }));

        var originalCode = content.Document.Syntax.Descendants<CodeBlock>().ToArray();
        var importedCode = importedDocument.Syntax.Descendants<CodeBlock>().ToArray();
        var originalQuotes = content.Document.Syntax.Descendants<QuoteBlock>().ToArray();
        var importedQuotes = importedDocument.Syntax.Descendants<QuoteBlock>().ToArray();
        // DOCX represents line boundaries as break elements; imported Markdown
        // consistently uses LF regardless of the source checkout's line endings.
        var expectedCode = originalCode.FirstOrDefault()?.Lines.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        var actualCode = importedCode.FirstOrDefault()?.Lines.ToString();
        var listPlacement = importedCode.Length == 1 && importedQuotes.Length == 1
            && importedCode[0].Parent is ListItemBlock && importedQuotes[0].Parent is ListItemBlock;
        var blocksPassed = originalCode.Length == 1 && originalQuotes.Length == 1 && listPlacement
            && actualCode == expectedCode && importedQuotes[0].Count == 2 && importedQuotes[0].Count == originalQuotes[0].Count;
        File.WriteAllText(Path.Combine(output, "native-word-block-import.json"), JsonSerializer.Serialize(new
        {
            Passed = blocksPassed, CodeBlocks = importedCode.Length, QuoteBlocks = importedQuotes.Length, QuoteParagraphs = importedQuotes.FirstOrDefault()?.Count,
            LiteralCode = actualCode, ExpectedCode = expectedCode, ListPlacementRetained = listPlacement,
            Limitation = "Recognised paragraph styles are checked. Code language, nested quote depth, quote/list orientation and ambiguous template spacing styles are not reconstructed."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!blocksPassed) throw new IOException("Native Word reimport changed literal code or quote paragraph boundaries inside a list.");
        CheckStyleNumbering(bytes, imported.Markdown, output);
    }

    private static void CheckStyleNumbering(byte[] bytes, string expectedMarkdown, string output)
    {
        using var stream = new MemoryStream(); stream.Write(bytes);
        var converted = 0;
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart!; var styles = main.StyleDefinitionsPart!.Styles!;
            var numbering = main.NumberingDefinitionsPart!.Numbering!;
            var definitions = new Dictionary<(int Id, int Level), string>();
            foreach (var paragraph in main.Document!.Body!.Elements<W.Paragraph>())
            {
                var properties = paragraph.ParagraphProperties;
                if (properties?.NumberingProperties?.NumberingId?.Val?.Value is not > 0) continue;
                var numberId = properties.NumberingProperties.NumberingId.Val.Value;
                var level = properties.NumberingProperties.NumberingLevelReference?.Val?.Value ?? 0;
                if (!definitions.TryGetValue((numberId, level), out var baseId))
                {
                    baseId = "ProbeList" + numberId + "Level" + level;
                    var baseStyle = new W.Style(new W.StyleName { Val = baseId }, new W.StyleParagraphProperties(
                        new W.NumberingProperties(new W.NumberingId { Val = numberId }))) { StyleId = baseId, Type = W.StyleValues.Paragraph };
                    if (properties.Indentation is { } indent) baseStyle.StyleParagraphProperties!.Indentation = (W.Indentation)indent.CloneNode(true);
                    styles.Append(baseStyle); definitions[(numberId, level)] = baseId;
                    var abstractId = numbering.Elements<W.NumberingInstance>().Single(n => n.NumberID?.Value == numberId).AbstractNumId!.Val!.Value;
                    numbering.Elements<W.AbstractNum>().Single(n => n.AbstractNumberId?.Value == abstractId).Elements<W.Level>()
                        .Single(l => l.LevelIndex?.Value == level).ParagraphStyleIdInLevel = new() { Val = baseId };
                }
                var originalId = properties.ParagraphStyleId?.Val?.Value;
                var name = styles.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == originalId)?.StyleName?.Val?.Value ?? originalId ?? "Normal";
                var derivedId = "ProbeParagraph" + ++converted;
                styles.Append(new W.Style(new W.StyleName { Val = name }, new W.BasedOn { Val = baseId }) { StyleId = derivedId, Type = W.StyleValues.Paragraph });
                properties.ParagraphStyleId = new() { Val = derivedId };
                properties.NumberingProperties.Remove(); properties.Indentation?.Remove();
            }
            Validate(package);
        }
        var styledBytes = stream.ToArray();
        File.WriteAllBytes(Path.Combine(output, "native-word-style-lists.docx"), styledBytes);
        var imported = new DocumentImportService().ImportDocx(styledBytes);
        File.WriteAllText(Path.Combine(output, "native-word-style-lists-reimported.md"), imported.Markdown);
        var parsed = new MarkdownService().Parse(imported.Markdown);
        var passed = converted == 12 && imported.Markdown == expectedMarkdown && imported.Warnings.Count == 0;
        File.WriteAllText(Path.Combine(output, "native-word-style-list-import.json"), JsonSerializer.Serialize(new
        {
            Passed = passed, StyledItems = converted, Lists = parsed.Syntax.Descendants<ListBlock>().Count(),
            ExactMarkdown = imported.Markdown == expectedMarkdown, Warnings = imported.Warnings,
            Limitation = "Schema-valid inherited paragraph numbering is checked. Field/composite markers and full Word layout or formatting are not certified."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!passed) throw new IOException("Word style-inherited numbering changed list structure, code, quotes or continuation text.");
    }

    private static void Validate(WordprocessingDocument package)
    {
        var errors = new OpenXmlValidator().Validate(package).Select(e => e.Description).ToArray();
        if (errors.Length != 0) throw new IOException(string.Join(" ", errors));
    }
}
