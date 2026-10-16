using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWordSyntaxChecks
{
    public static void Run(string output)
    {
        const string code = "/* First comment\r\n   Comment continues */\r\npublic string Describe()\r\n{\r\n\tvar label = \"Café <studio> 😀\";\r\n    return label + 42;\r\n}";
        const string fallback = "    PlainFallbackToken  retains\tspaces and tabs.";
        var source = "```csharp\r\n" + code + "\r\n```\r\n\r\n```unknown-language\r\n" + fallback + "\r\n```";
        var preparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        var content = preparation.Prepare(source, null, null, false);
        var bytes = new DocxExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-word-syntax.docx"), bytes);
        using var stream = new MemoryStream(bytes);
        using var word = WordprocessingDocument.Open(stream, false);
        if (new OpenXmlValidator().Validate(word).Any()) throw new IOException("Coloured Word code is not schema-valid.");
        var paragraphs = word.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        if (paragraphs.Length != 2 || ReadCode(paragraphs[0]) != code.Replace("\r\n", "\n") || ReadCode(paragraphs[1]) != fallback)
            throw new IOException("Word code lost exact Unicode, indentation, tabs or line breaks.");
        var runs = paragraphs[0].Elements<W.Run>().ToArray();
        void Colour(string fragment, string expected)
        {
            var run = runs.Single(r => r.InnerText.Contains(fragment, StringComparison.Ordinal));
            if (run.RunProperties?.Color?.Val?.Value != expected) throw new IOException("Word syntax colour was lost for " + fragment + ".");
        }
        Colour("public", "5B21B6"); Colour("Comment continues", "466147"); Colour("Café", "166534"); Colour("42", "92400E");
        if (paragraphs[1].Descendants<W.Color>().Any()) throw new IOException("Unknown-language Word fallback was unexpectedly coloured.");

        var largeCode = string.Join('\n', Enumerable.Range(0, 800).Select(i => $"// BudgetRow_{i:D4} with complete retained content and spacing."));
        var largeSource = "```csharp\n" + largeCode + "\n```";
        var large = preparation.Prepare(largeSource, null, null, false);
        var largeBytes = new DocxExporter().Export(large);
        File.WriteAllBytes(Path.Combine(output, "native-word-syntax-budget.docx"), largeBytes);
        using var largeStream = new MemoryStream(largeBytes);
        using var largeWord = WordprocessingDocument.Open(largeStream, false);
        var largeParagraph = largeWord.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        if (ReadCode(largeParagraph) != largeCode || largeParagraph.Descendants<W.Color>().Any() || new OpenXmlValidator().Validate(largeWord).Any())
            throw new IOException("The Word syntax-budget fallback changed complete editable content.");
        if (content.Document.Source != source || large.Document.Source != largeSource) throw new IOException("Word colouring changed Markdown source.");
        File.WriteAllText(Path.Combine(output, "native-word-syntax.json"), JsonSerializer.Serialize(new { passed = 5, fallbackRows = 800, checks = new[]
        {
            "Actual native preparation produces schema-valid Word keyword, comment, string and number colours",
            "Editable Unicode, indentation, tabs and exact line breaks remain intact",
            "Unknown code language retains complete plain text",
            "Over-budget code retains all 800 complete editable rows",
            "Markdown source remains unchanged"
        } }, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static string ReadCode(W.Paragraph paragraph) => string.Concat(paragraph.Elements<W.Run>().SelectMany(run => run.ChildElements)
        .Select(element => element switch { W.Text text => text.Text, W.TabChar => "\t", W.Break => "\n", _ => "" }));
}
