using System.Text;
using System.Text.Json;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;
using UglyToad.PdfPig.Actions;

namespace LensDocsStudio.Native.Validation;

internal static class NativePdfUnicodeChecks
{
    public static void Run(string output)
    {
        var checks = new List<string>();
        const string source = "# Selectable 😀 PDF\n\nCafé Ελληνικά Документация 😀 ✅ 🚀\n\n"
            + "**Bold 😀** and *italic 🚀*, inline `code 😀`.\n\n[Linked 🚀](https://example.org/docs)\n\n"
            + "| Name | Value |\n| --- | --- |\n| Emoji | 😀 ✅ 🚀 |\n\n"
            + "```csharp\n    var text = \"Café 😀 ✅ 🚀\";\n```\n\n"
            + "```unknown\n    Plain 😀 🚀\n```";
        var content = Content(source);
        var bytes = new PdfExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-pdf-unicode.pdf"), bytes);
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes))
        {
            var observed = Text(pdf);
            foreach (var expected in new[] { "Selectable😀PDF", "CaféΕλληνικάДокументация😀✅🚀", "Bold😀", "italic🚀", "code😀",
                "Linked🚀", "Emoji😀✅🚀", "vartext=\"Café😀✅🚀\";", "Plain😀🚀" })
                if (!observed.Contains(expected, StringComparison.Ordinal))
                    throw new IOException("PDF selectable Unicode changed: " + expected + ". Observed: " + observed);
            if (observed.Contains('\0') || observed.Contains('\uFFFD')) throw new IOException("PDF retained missing or replacement glyphs.");
            var destinations = pdf.GetPages().SelectMany(page => page.GetAnnotations()).Select(annotation => annotation.Action)
                .OfType<UriAction>().Select(action => action.Uri).ToArray();
            if (!destinations.Contains("https://example.org/docs")) throw new IOException("Unicode PDF processing lost a hyperlink.");
            checks.Add("Actual selectable Latin, Greek, Cyrillic and supplementary emoji survive headings, emphasis, code, tables and links");
            checks.Add("Hyperlink annotations remain present after Unicode mapping correction");
        }

        // Language font installation varies across Windows editions. A fontless machine must
        // report an explicit unsupported character rather than create a successful broken PDF.
        var languageOutcomes = new List<object>();
        foreach (var (name, text) in new[] { ("cjk", "中文 日本語 한국어"), ("arabic", "العربية"), ("hebrew", "עברית") })
        {
            var languageSource = "# " + name + "\n\n" + text + "\n\n```text\n" + text + "\n```";
            var language = Content(languageSource);
            try
            {
                var languageBytes = new PdfExporter().Export(language);
                File.WriteAllBytes(Path.Combine(output, "native-pdf-unicode-" + name + ".pdf"), languageBytes);
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(languageBytes);
                var observed = Text(pdf);
                var expected = RemoveSpaces(text);
                if (observed.Split(expected, StringSplitOptions.None).Length != 3 || observed.Contains('\0') || observed.Contains('\uFFFD'))
                    throw new IOException("PDF prose/code glyphs changed for " + name + ".");
                languageOutcomes.Add(new { name, selectableCharactersRetained = true, explicitlyUnsupported = false });
            }
            catch (InvalidDataException ex) when (ex.Message.StartsWith("PDF export cannot render character U+", StringComparison.Ordinal))
            {
                languageOutcomes.Add(new { name, selectableCharactersRetained = false, explicitlyUnsupported = true });
            }
            if (language.Document.Source != languageSource) throw new IOException("A language export changed source.");
        }
        checks.Add("Optional language fonts retain prose/code characters or return an explicit unsupported-character error");

        const string unsupported = "# Unassigned character\n\n\U0010FFFF";
        var unsupportedContent = Content(unsupported);
        var rejected = false;
        try { new PdfExporter().Export(unsupportedContent); }
        catch (InvalidDataException ex) when (ex.Message.Contains("U+10FFFF", StringComparison.Ordinal)) { rejected = true; }
        if (!rejected) throw new IOException("An unassigned character was silently exported.");
        if (!new HtmlExporter().Export(unsupportedContent).Contains("\U0010FFFF", StringComparison.Ordinal))
            throw new IOException("The alternate HTML export lost the original Unicode source.");
        checks.Add("Unassigned characters fail clearly before returning bytes; HTML retains the portable source");

        var limited = content with { AllocationLimits = new ExportAllocationLimits(MaximumOutputBytes: 128) };
        rejected = false;
        try { new PdfExporter().Export(limited); }
        catch (IOException ex) when (ex.Message.Contains("size limit", StringComparison.Ordinal)) { rejected = true; }
        if (!rejected) throw new IOException("Unicode PDF bypassed the export byte limit.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        rejected = false;
        try { new PdfExporter().Export(content, cancellation.Token); }
        catch (OperationCanceledException) { rejected = true; }
        if (!rejected) throw new IOException("Cancelled Unicode PDF export completed.");
        checks.Add("Export allocation and cancellation guards remain enforced");
        if (content.Document.Source != source || unsupportedContent.Document.Source != unsupported)
            throw new IOException("PDF export changed Markdown source.");
        checks.Add("Successful and rejected exports leave source unchanged");
        File.WriteAllText(Path.Combine(output, "native-pdf-unicode.json"), JsonSerializer.Serialize(new
        {
            passed = checks.Count, checks, languageOutcomes,
            limitations = "Character retention does not certify complex-script shaping, bidirectional layout, joined emoji or colour emoji."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ExportContent Content(string source) =>
        new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
    private static string Text(UglyToad.PdfPig.PdfDocument pdf) =>
        RemoveSpaces(string.Concat(pdf.GetPages().SelectMany(page => page.Letters).Select(letter => letter.Value)));
    private static string RemoveSpaces(string text) => string.Concat(text.EnumerateRunes().Where(rune => !Rune.IsWhiteSpace(rune)));
}
