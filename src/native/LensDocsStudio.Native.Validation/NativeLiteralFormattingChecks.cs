using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeLiteralFormattingChecks
{
    public static void Run(string output)
    {
        var cases = new[]
        {
            (Source: "**Café 😀 é**", Text: "Café 😀 é", Kind: MarkdownEmphasis.Bold),
            (Source: "*Café 😀 é*", Text: "Café 😀 é", Kind: MarkdownEmphasis.Italic),
            (Source: "~~Withdrawn~~", Text: "Withdrawn", Kind: MarkdownEmphasis.Strike),
            (Source: "++Inserted++", Text: "Inserted", Kind: MarkdownEmphasis.Underline),
            (Source: "H~2~O", Text: "2", Kind: MarkdownEmphasis.Subscript),
            (Source: "x^3^", Text: "3", Kind: MarkdownEmphasis.Superscript),
            (Source: "==Marked==", Text: "Marked", Kind: MarkdownEmphasis.Highlight)
        };
        var checks = new List<object>();
        foreach (var dark in new[] { false, true })
        foreach (var test in cases)
        {
            var parsed = new MarkdownService().Parse(test.Source);
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            var paragraph = result.Document.Blocks.OfType<Paragraph>().Single();
            var run = paragraph.Inlines.OfType<Run>().SingleOrDefault(run => run.Text == test.Text)
                ?? throw new IOException("Expected one literal run for " + test.Kind);
            var selected = new TextRange(run.ContentStart, run.ContentEnd);
            if (selected.Text != test.Text || parsed.Source != test.Source || paragraph.Tag is not SourceReference source
                || source.Start != 0 || source.End != test.Source.Length - 1 || result.SourceElements[0] != paragraph
                || run.FontWeight != (test.Kind.HasFlag(MarkdownEmphasis.Bold) ? FontWeights.Bold : FontWeights.Normal)
                || run.FontStyle != (test.Kind.HasFlag(MarkdownEmphasis.Italic) ? FontStyles.Italic : FontStyles.Normal)
                || run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough) != test.Kind.HasFlag(MarkdownEmphasis.Strike)
                || run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline) != test.Kind.HasFlag(MarkdownEmphasis.Underline)
                || run.BaselineAlignment != (test.Kind.HasFlag(MarkdownEmphasis.Subscript) ? BaselineAlignment.Subscript
                    : test.Kind.HasFlag(MarkdownEmphasis.Superscript) ? BaselineAlignment.Superscript : BaselineAlignment.Baseline)
                || run.FontSize != (test.Kind.HasFlag(MarkdownEmphasis.Subscript) || test.Kind.HasFlag(MarkdownEmphasis.Superscript) ? 11 : 15)
                || (run.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue) != test.Kind.HasFlag(MarkdownEmphasis.Highlight)
                || paragraph.Inlines.OfType<Span>().Any())
                throw new IOException("Literal formatting lost exact selection, source mapping or native formatting.");
            using var rtf = new MemoryStream(); selected.Save(rtf, DataFormats.Rtf);
            var copied = new FlowDocument(new Paragraph());
            rtf.Position = 0; new TextRange(copied.ContentStart, copied.ContentEnd).Load(rtf, DataFormats.Rtf);
            if (new TextRange(copied.ContentStart, copied.ContentEnd).Text.TrimEnd('\r', '\n') != test.Text)
                throw new IOException("Native RTF copy changed the selected literal text.");
            checks.Add(new { Dark = dark, Kind = test.Kind.ToString(), ExactText = true, SourceMapping = true, NativeRtf = true });
        }
        foreach (var dark in new[] { false, true })
        {
            const string nestedSource = "***Café***";
            var nested = new NativeMarkdownRenderer(new NativeSvgService()).Render(new MarkdownService().Parse(nestedSource), dark,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            var span = nested.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Span>().Single();
            var literal = span.Inlines.OfType<Run>().Single();
            if (literal.Text != "Café" || literal.FontWeight != FontWeights.Bold || literal.FontStyle != FontStyles.Italic)
                throw new IOException("Nested bold and italic did not retain inherited formatting.");
            checks.Add(new { Dark = dark, Kind = "Nested bold and italic", ExactText = true, SourceMapping = true, NativeRtf = false });
        }
        // Entities, nested emphasis, code, links and breaks still need their container.
        const string complex = "**Café &amp; *review* [link](#target) `code`  \nnext**\n\n# Target";
        var mixed = new NativeMarkdownRenderer(new NativeSvgService()).Render(new MarkdownService().Parse(complex), false,
            new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
        var container = mixed.Document.Blocks.OfType<Paragraph>().First().Inlines.OfType<Span>().Single();
        if (container.FontWeight != FontWeights.Bold || container.Inlines.OfType<Hyperlink>().Count() != 1
            || container.Inlines.OfType<LineBreak>().Count() != 1
            || !mixed.ReadText().Contains("Café & review link code", StringComparison.Ordinal))
            throw new IOException("Complex emphasis lost its native container, link, break or decoded content.");
        File.WriteAllText(Path.Combine(output, "native-literal-formatting.json"), JsonSerializer.Serialize(new
            { Passed = true, Checks = checks, ComplexContainerPreserved = true }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
