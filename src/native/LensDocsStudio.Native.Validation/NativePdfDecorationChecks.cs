using System.Text;
using System.Text.Json;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics;

namespace LensDocsStudio.Native.Validation;

internal static class NativePdfDecorationChecks
{
    public static void Run(string output)
    {
        const string source = "# Decorated native PDF\n\n"
            + "BeforePlain ~~Withdrawn Café~~ AfterPlain. ==Highlighted== Unmarked.\n\n"
            + "~~**StrongDeleted** *ItalicDeleted* `CodeDeleted`~~ ==**StrongMarked**==.\n\n"
            + "~~==Combined😀==~~ and ==[MarkedLink](https://example.org/decorated)==.\n\n"
            + "[~~DeletedLink~~](https://example.org/deleted) and [==HeadingLink==](#destination).\n\n"
            + "| ==HeaderMarked== | NeighbourHeader |\n| --- | --- |\n| ~~TableDeleted~~ | ==TableMarked== |\n\n"
            + "## Destination\n\nRetained bookmark destination.";
        var content = Content(source);
        var bytes = new PdfExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-pdf-decorations.pdf"), bytes);
        var checks = new List<string>();
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes))
        {
            var page = pdf.GetPage(1); var words = page.GetWords().ToArray();
            Word Find(string text)
            {
                var word = words.Single(word => word.Text.TrimEnd('.') == text);
                return word.Text.EndsWith('.') ? new Word(word.Letters.Take(word.Letters.Count - 1).ToArray()) : word;
            }
            foreach (var text in new[] { "Withdrawn", "Café", "StrongDeleted", "ItalicDeleted", "CodeDeleted", "Combined😀", "DeletedLink", "TableDeleted" })
                if (!Struck(page, Find(text))) throw new IOException("PDF text lost its visible strike-through line: " + text);
            checks.Add("Actual PDF strokes cross deleted text, including nested bold, italic, code, Unicode, links and table cells");
            foreach (var text in new[] { "Highlighted", "StrongMarked", "Combined😀", "MarkedLink", "HeadingLink", "HeaderMarked", "TableMarked" })
                if (!Highlighted(page, Find(text))) throw new IOException("PDF text lost its highlight rectangle: " + text);
            checks.Add("Actual PDF highlight geometry covers only marked text, including shaded table headers and combined decorations");
            foreach (var text in new[] { "BeforePlain", "AfterPlain", "Unmarked", "NeighbourHeader" })
                if (Struck(page, Find(text)) || Highlighted(page, Find(text))) throw new IOException("PDF decoration leaked into neighbouring text: " + text);
            foreach (var word in words.Where(word => word.Text is "StrongMarked." or "MarkedLink." or "HeadingLink."))
                if (Highlighted(page, new Word([word.Letters[^1]]))) throw new IOException("Highlighting leaked into punctuation outside the Markdown span.");
            if (!Find("StrongDeleted").Letters.All(letter => letter.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true)
                || !Find("ItalicDeleted").Letters.All(letter => letter.FontName?.Contains("Italic", StringComparison.OrdinalIgnoreCase) == true))
                throw new IOException("Decorated text lost its independent bold or italic face.");
            checks.Add("Neighbouring plain text stays undecorated and independent font emphasis remains intact");
            var textContent = string.Concat(page.Letters.Select(letter => letter.Value));
            if (!textContent.Contains("Combined😀", StringComparison.Ordinal) || textContent.Contains("~~", StringComparison.Ordinal)
                || textContent.Contains("==", StringComparison.Ordinal) || textContent.Contains('\uFFFD') || textContent.Contains('\0'))
                throw new IOException("Decorations changed selectable Unicode or retained Markdown delimiters.");
            var actions = pdf.GetPages().SelectMany(p => p.GetAnnotations()).Select(annotation => annotation.Action).ToArray();
            var links = actions.OfType<UriAction>().Select(action => action.Uri).ToArray();
            if (!links.Contains("https://example.org/decorated") || !links.Contains("https://example.org/deleted")
                || !actions.OfType<GoToAction>().Any() || links.Any(link => link.Contains("lens-docs.invalid", StringComparison.Ordinal)))
                throw new IOException("Decoration processing lost a real hyperlink/bookmark or exposed a temporary layout link.");
            if (Encoding.Latin1.GetString(bytes).Contains("lens-docs.invalid", StringComparison.Ordinal))
                throw new IOException("The final PDF retains temporary decoration metadata.");
            checks.Add("Selectable supplementary Unicode, real external links and bookmark navigation survive; temporary layout links are removed");
        }

        var longSource = "# Paginated decorations\n\n" + string.Join("\n\n", Enumerable.Range(0, 250).Select(index =>
            $"~~Deleted_{index:D4} remains selectable across pages and wrapped lines with all characters retained. "
            + $"This additional sentence forces native wrapping before Continued_{index:D4} reaches its final position.~~ "
            + $"==Marked_{index:D4} keeps its highlight and original text.=="));
        var longContent = Content(longSource);
        var longBytes = new PdfExporter().Export(longContent);
        File.WriteAllBytes(Path.Combine(output, "native-pdf-decorations-pages.pdf"), longBytes);
        var deletedCount = 0; var markedCount = 0; var continuedCount = 0; int pages;
        var starts = new Dictionary<string, (int Page, double Top)>();
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(longBytes))
        {
            pages = pdf.NumberOfPages;
            foreach (var page in pdf.GetPages())
            foreach (var word in page.GetWords())
            {
                if (word.Text.StartsWith("Deleted_", StringComparison.Ordinal))
                {
                    if (!Struck(page, word)) throw new IOException("A paginated deletion lost its stroke: " + word.Text);
                    starts.Add(word.Text["Deleted_".Length..], (page.Number, word.BoundingBox.Top)); deletedCount++;
                }
                if (word.Text.StartsWith("Continued_", StringComparison.Ordinal))
                {
                    if (!Struck(page, word) || !starts.TryGetValue(word.Text["Continued_".Length..], out var first)
                        || first.Page == page.Number && Math.Abs(first.Top - word.BoundingBox.Top) < 2)
                        throw new IOException("A deletion lost its stroke after wrapping or the fixture failed to wrap: " + word.Text);
                    continuedCount++;
                }
                if (word.Text.StartsWith("Marked_", StringComparison.Ordinal))
                { if (!Highlighted(page, word)) throw new IOException("A paginated highlight lost its fill: " + word.Text); markedCount++; }
            }
            if (pages < 2 || deletedCount != 250 || continuedCount != 250 || markedCount != 250) throw new IOException("Paginated PDF decoration lost text or pages.");
        }
        checks.Add("All 250 deletions and 250 highlights retain selectable text and matching geometry across PDF pages");
        var rejected = false;
        try { new PdfExporter().Export(content with { AllocationLimits = new ExportAllocationLimits(MaximumOutputBytes: 128) }); }
        catch (IOException ex) when (ex.Message.Contains("size limit", StringComparison.Ordinal)) { rejected = true; }
        if (!rejected) throw new IOException("Decorated PDF bypassed its output byte limit.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); rejected = false;
        try { new PdfExporter().Export(content, cancellation.Token); }
        catch (OperationCanceledException) { rejected = true; }
        if (!rejected || content.Document.Source != source || longContent.Document.Source != longSource)
            throw new IOException("PDF decorations lost cancellation or changed Markdown source.");
        checks.Add("Allocation limits, cancellation and unchanged source remain enforced");
        File.WriteAllText(Path.Combine(output, "native-pdf-decorations.json"), JsonSerializer.Serialize(new
        { passed = checks.Count, checks, pages, deletedCount, continuedCount, markedCount }, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static bool Struck(Page page, Word word) => page.Paths.Any(path => path.IsStroked && path.GetBoundingRectangle() is { } bounds
        && bounds.Height < .1 && bounds.Left <= word.BoundingBox.Left + .5 && bounds.Right >= word.BoundingBox.Right - .5
        && bounds.Bottom > word.BoundingBox.Bottom + word.BoundingBox.Height * .15 && bounds.Top < word.BoundingBox.Top - word.BoundingBox.Height * .1);

    internal static bool Highlighted(Page page, Word word) => page.Paths.Any(path => path.IsFilled && IsHighlight(path)
        && path.GetBoundingRectangle() is { } bounds && bounds.Left <= word.BoundingBox.Left + .5 && bounds.Right >= word.BoundingBox.Right - .5
        && bounds.Bottom <= word.BoundingBox.Bottom + .5 && bounds.Top >= word.BoundingBox.Top - .5);

    private static bool IsHighlight(PdfPath path)
    {
        if (path.FillColor is not { } fill) return false;
        var (red, green, blue) = fill.ToRGBValues();
        return Math.Abs(red * 255 - 237) < .5 && Math.Abs(green * 255 - 237) < .5 && Math.Abs(blue * 255 - 237) < .5;
    }
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
}
