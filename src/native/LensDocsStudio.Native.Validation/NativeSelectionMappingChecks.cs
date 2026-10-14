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
            foreach (var text in new[] { "Café", "😀", "é", "action **action** *action* [action" })
            {
                var start = source.IndexOf(text, StringComparison.Ordinal);
                if (!preview.TextMap.TryPreviewRange(start, text.Length, out var from, out var to)
                    || !preview.TextMap.TrySourceRange(from!, to!, out var restored, out var length) || restored != start || length != text.Length)
                    throw new IOException("Formatted or Unicode selection boundaries lost their source offsets.");
            }
            if (preview.TextMap.TryPreviewRange(source.IndexOf("**", StringComparison.Ordinal), 2, out _, out _))
                throw new IOException("Formatting delimiters claimed an exact literal selection.");
            var other = new FlowDocument(new Paragraph(new Run("action")));
            if (preview.TextMap.TrySourceRange(other.ContentStart, other.ContentEnd, out _, out _))
                throw new IOException("A stale document pointer mapped into the current preview.");
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "native-selection-mapping.json"), JsonSerializer.Serialize(new { Passed = true, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
