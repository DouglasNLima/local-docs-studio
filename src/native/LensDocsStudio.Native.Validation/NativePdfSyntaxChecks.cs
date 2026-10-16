using System.Text.Json;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;
using UglyToad.PdfPig.Content;

namespace LensDocsStudio.Native.Validation;

internal static class NativePdfSyntaxChecks
{
    public static void Run(string output)
    {
        const string source = "# PDF syntax colours\r\n\r\n```csharp\r\n/* First comment\r\n   Comment continues */\r\npublic string Describe()\r\n{\r\n    var label = \"Café <studio>\";\r\n    return label + 42;\r\n}\r\n```\r\n\r\n"
            + "```unknown-language\r\n    PlainFallbackToken keeps complete text.\r\n```";
        var parsed = new MarkdownService().Parse(source);
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var bytes = new PdfExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-pdf-syntax.pdf"), bytes);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var words = pdf.GetPages().SelectMany(p => p.GetWords()).ToArray();
        Word Find(string text) => words.Single(w => w.Text == text);
        CheckColour(Find("public").Letters, "5B21B6", "keyword");
        CheckColour(Find("continues").Letters, "466147", "multiline comment");
        CheckColour(words.Single(w => w.Text.Contains("Café", StringComparison.Ordinal)).Letters, "166534", "Unicode string");
        CheckColour(words.Single(w => w.Text == "42;").Letters.Where(l => l.Value is "4" or "2"), "92400E", "number");
        var checks = new List<string> { "Actual PDF glyphs retain keyword, multiline comment, Unicode string and number colours" };
        var selectable = string.Join(" ", words.Select(w => w.Text));
        foreach (var fragment in new[] { "Comment continues", "public string Describe()", "var label", "Café", "<studio>", "return label + 42;", "PlainFallbackToken keeps complete text." })
            if (!selectable.Contains(fragment, StringComparison.Ordinal)) throw new IOException("Coloured PDF text lost content: " + fragment);
        if (Find("var").BoundingBox.Left - Find("public").BoundingBox.Left < 15 || Find("return").BoundingBox.Top >= Find("public").BoundingBox.Top
            || Find("PlainFallbackToken").BoundingBox.Left - Find("public").BoundingBox.Left < 15)
            throw new IOException("PDF code indentation or line order changed.");
        checks.Add("Selectable Unicode, punctuation, indentation and line order remain intact");
        CheckColour(Find("PlainFallbackToken").Letters, "000000", "unknown-language fallback");
        checks.Add("Unknown code language retains complete plain text");

        var largeSource = "```csharp\n" + string.Join('\n', Enumerable.Range(0, 800).Select(i => $"// BudgetRow_{i:D4} with complete retained content and spacing.")) + "\n```";
        var largeDocument = new MarkdownService().Parse(largeSource);
        var largeContent = new ExportContent(largeDocument, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var largeBytes = new PdfExporter().Export(largeContent);
        File.WriteAllBytes(Path.Combine(output, "native-pdf-syntax-budget.pdf"), largeBytes);
        using var largePdf = UglyToad.PdfPig.PdfDocument.Open(largeBytes);
        var rows = largePdf.GetPages().SelectMany(p => p.GetWords()).Where(w => w.Text.StartsWith("BudgetRow_", StringComparison.Ordinal)).ToArray();
        if (rows.Length != 800 || rows[0].Text != "BudgetRow_0000" || rows[^1].Text != "BudgetRow_0799" || largePdf.NumberOfPages < 2)
            throw new IOException("The syntax budget fallback lost code rows or pagination.");
        CheckColour(rows.SelectMany(w => w.Letters), "000000", "syntax-budget fallback");
        checks.Add("Over-budget code remains complete selectable plain text across PDF pages");
        if (parsed.Source != source || largeDocument.Source != largeSource) throw new IOException("PDF code export changed Markdown source.");
        checks.Add("Source text remains unchanged");
        File.WriteAllText(Path.Combine(output, "native-pdf-syntax.json"), JsonSerializer.Serialize(new { passed = checks.Count, budgetFallbackRows = rows.Length,
            budgetFallbackPages = largePdf.NumberOfPages, checks }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CheckColour(IEnumerable<Letter> letters, string expected, string label)
    {
        var found = false;
        foreach (var letter in letters)
        {
            found = true;
            var (red, green, blue) = letter.Color.ToRGBValues();
            var actual = $"{(int)Math.Round(red * 255):X2}{(int)Math.Round(green * 255):X2}{(int)Math.Round(blue * 255):X2}";
            if (actual != expected) throw new IOException($"The PDF {label} colour was {actual}, expected {expected}.");
        }
        if (!found) throw new IOException("No PDF glyphs were found for " + label + ".");
    }
}
