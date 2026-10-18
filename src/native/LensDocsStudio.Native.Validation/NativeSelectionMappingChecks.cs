using System.Text.Json;
using System.Windows.Documents;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSelectionMappingChecks
{
    public static void Run(string output)
    {
        const string source = "# action\n\naction **action** *action* [action](other.md) `action`\n\nCafé 😀 é action\n\nSoft\nbreak and hard  \nbreak.";
        var observations = new List<object>();
        foreach (var dark in new[] { false, true })
        {
            var preview = new NativeMarkdownRenderer(new NativeSvgService()).Render(new MarkdownService().Parse(source), dark,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            for (var start = source.IndexOf("action", StringComparison.Ordinal); start >= 0; start = source.IndexOf("action", start + 6, StringComparison.Ordinal))
            {
                if (!preview.TextMap.TryPreviewRange(start, 6, out var from, out var to) || new TextRange(from!, to!).Text != "action"
                    || !preview.TextMap.TrySourceRange(from!, to!, out var roundTrip, out var length) || roundTrip != start || length != 6)
                    throw new IOException($"Repeated-word selection mapping failed at source offset {start}.");
                observations.Add(new { Dark = dark, SourceOffset = start, Length = length, ExactRoundTrip = true });
            }
            foreach (var text in new[] { "Café", "😀", "é", "action **action** *action* [action", "hard  \nbreak.", "Café 😀 é action\n\nSoft" })
            {
                var start = source.IndexOf(text, StringComparison.Ordinal);
                if (!preview.TextMap.TryPreviewRange(start, text.Length, out var from, out var to)
                    || !preview.TextMap.TrySourceRange(from!, to!, out var restored, out var length) || restored != start || length != text.Length)
                    throw new IOException("Formatted or Unicode selection boundaries lost their source offsets.");
                var renderedText = new TextRange(from!, to!).Text;
                if (text == "hard  \nbreak." && renderedText != "hard\r\nbreak."
                    || text == "Café 😀 é action\n\nSoft" && renderedText != "Café 😀 é action\r\nSoft")
                    throw new IOException("Hard-break or cross-paragraph native text lost its exact mapped boundaries: " + renderedText);
                observations.Add(new { Dark = dark, Source = text, Rendered = renderedText, ExactRoundTrip = true });
            }
            var link = preview.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>()).Single();
            if (!preview.TextMap.TrySourceRange(link.ElementStart, link.ElementEnd, out var linkStart, out var linkLength)
                || linkStart != source.IndexOf("[action", StringComparison.Ordinal) + 1 || linkLength != 6)
                throw new IOException("Native structural hyperlink selection boundaries did not map to their literal label.");
            var literals = preview.TextMap.Literals.ToArray();
            var linkIndex = Array.FindIndex(literals, literal => literal.Source.Start == linkStart);
            var normalStart = literals[linkIndex - 1].Run.ContentEnd;
            var normalEnd = literals[linkIndex + 1].Run.ContentStart;
            if (new TextRange(normalStart, normalEnd).Text != "action"
                || !preview.TextMap.TrySourceRange(normalStart, normalEnd, out var normalSource, out var normalLength)
                || normalSource != linkStart || normalLength != 6)
                throw new IOException("Adjacent-run native selection boundaries included Markdown link syntax.");
            if (preview.TextMap.TryPreviewRange(source.IndexOf("**", StringComparison.Ordinal), 2, out _, out _))
                throw new IOException("Formatting delimiters claimed an exact literal selection.");
            var boldSource = source.IndexOf("**action**", StringComparison.Ordinal) + 2;
            var bold = preview.TextMap.Literals.Single(literal => literal.Source.Start == boldSource).Run;
            if (!preview.TextMap.TrySourceOffset(bold.ElementStart, out var readingSource) || readingSource != boldSource)
                throw new IOException("A reading position at a formatting boundary lost its forward source affinity.");
            observations.Add(new { Dark = dark, ReadingSource = readingSource, ForwardAffinity = true });
            var other = new FlowDocument(new Paragraph(new Run("action")));
            if (preview.TextMap.TrySourceRange(other.ContentStart, other.ContentEnd, out _, out _))
                throw new IOException("A stale document pointer mapped into the current preview.");
        }
        foreach (var newline in new[] { "\n", "\r\n" })
        {
            var coalescedSource = "Lead &amp; tail 😀" + newline + "next &#169; end  " + newline + "hard **bold &amp; label** [link &amp; label](#target).";
            var parsed = new MarkdownService().Parse(coalescedSource);
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            var paragraph = result.Document.Blocks.OfType<Paragraph>().Single();
            if (paragraph.Inlines.FirstInline is not Run { Text: "Lead & tail 😀 next © end" })
                throw new IOException("Adjacent plain/entity/soft-break runs were not coalesced with exact decoded text.");
            foreach (var text in new[] { "tail 😀", "next", "end", "label", "link" })
            {
                var start = coalescedSource.IndexOf(text, StringComparison.Ordinal);
                if (!result.TextMap.TryPreviewRange(start, text.Length, out var from, out var to) || new TextRange(from!, to!).Text != text
                    || !result.TextMap.TrySourceRange(from!, to!, out var restored, out var length) || restored != start || length != text.Length)
                    throw new IOException("Coalesced literal offsets lost exact Unicode/source selection.");
            }
            using var copied = new MemoryStream();
            var range = new TextRange(result.Document.ContentStart, result.Document.ContentEnd); range.Save(copied, System.Windows.DataFormats.Rtf);
            var pasted = new FlowDocument(); copied.Position = 0;
            new TextRange(pasted.ContentStart, pasted.ContentEnd).Load(copied, System.Windows.DataFormats.Rtf);
            if (new TextRange(pasted.ContentStart, pasted.ContentEnd).Text != range.Text || parsed.Source != coalescedSource
                || paragraph.Inlines.OfType<LineBreak>().Count() != 1 || paragraph.Inlines.OfType<Hyperlink>().Count() != 1)
                throw new IOException("Coalescing changed native RTF, a hard break, hyperlink or original source.");
            observations.Add(new { Coalesced = true, LineEnding = newline == "\n" ? "LF" : "CRLF", SourceSelection = true, NativeRtf = true });
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "native-selection-mapping.json"), JsonSerializer.Serialize(new { Passed = true, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
