using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Content;

namespace LensDocsStudio.Native.Validation;

/// <summary>Checks actual PDF text, glyph positions and images without opening a viewer.</summary>
internal static class NativePdfListChecks
{
    public static void Run(string output, string repository)
    {
        var source = File.ReadAllText(Path.Combine(repository, "tests", "fixtures", "native", "pdf-lists.md"));
        var checks = new List<string>();
        using var pdf = Export(source, "native-pdf-lists");
        var words = pdf.GetPages().SelectMany(p => p.GetWords()).ToArray();
        Word Find(string text) => words.Single(w => w.Text == text);
        void Aligned(string first, params string[] others)
        {
            foreach (var other in others)
                if (Math.Abs(Find(first).BoundingBox.Left - Find(other).BoundingBox.Left) > .2)
                    throw new IOException($"PDF list text {other} is not aligned with {first}.");
        }
        void Marker(string value, int count)
        {
            if (words.Count(w => w.Text == value) != count) throw new IOException("PDF list marker count changed: " + value);
        }
        Marker("7.", 1); Marker("8.", 1); Marker("3.", 1); Marker("•", 5);
        Aligned("FirstTop", "ContinuationTop", "ContinuationAfterNested", "SecondTop");
        Aligned("NestedFirst", "NestedContinuation", "NestedSecond");
        Aligned("DeepFirst", "DeepContinuation");
        if (Find("NestedFirst").BoundingBox.Left - Find("FirstTop").BoundingBox.Left < 12
            || Find("DeepFirst").BoundingBox.Left - Find("NestedFirst").BoundingBox.Left < 12)
            throw new IOException("Nested PDF lists lost their distinct indentation.");
        Aligned("AfterList", "AfterBlocks", "7.", "8.");
        checks.Add("One marker per item, three indentation levels, continuation alignment and restored body margin");
        Marker("0.", 1); Marker("1.", 1); Marker("999999998.", 1); Marker("999999999.", 1);
        Aligned("ZeroStart", "ZeroNext"); Aligned("WideFirst", "WideNext");
        if (Find("WideFirst").BoundingBox.Left - Find("999999998.").BoundingBox.Right < 4
            || Find("WideNext").BoundingBox.Left - Find("999999999.").BoundingBox.Right < 4)
            throw new IOException("A wide PDF list marker overlaps its item text.");
        checks.Add("Zero-based and nine-digit ordered markers retain their values without text overlap");
        Aligned("QuoteFirst", "QuoteContinuation", "HeadingFirst", "HeadingContinuation", "CodeFirst", "CodeSecond", "CodeContinuation");
        foreach (var label in new[] { "QuoteFirst", "HeadingFirst", "CodeFirst" })
            if (!words.Any(w => w.Text == "•" && Math.Abs(w.BoundingBox.Bottom - Find(label).BoundingBox.Bottom) < 8
                && w.BoundingBox.Right < Find(label).BoundingBox.Left))
                throw new IOException("A non-paragraph first block lost its list marker: " + label);
        if (!Find("StrongText").Letters.All(l => l.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true)
            || !Find("HeadingFirst").Letters.All(l => l.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true)
            || !words.Any(w => w.Text == "Café")
            || !pdf.GetPages().SelectMany(p => p.GetAnnotations()).Any(a => a.Action is UriAction { Uri: "https://example.org/list" }))
            throw new IOException("List layout changed selectable Unicode, inline formatting, headings or hyperlinks.");
        checks.Add("Quotes, headings and code retain their first marker, continuation text, styles and safe links");

        var paginatedSource = "41. " + string.Join("\n\n    ", Enumerable.Range(0, 90).Select(i => $"Row{i:D3} "
            + string.Join(' ', Enumerable.Repeat("wrapped selectable content remains inside the same list item", 3))))
            + "\n\n42. FinalItem closes the list.\n\nAfterPagination returns to the body margin.";
        using var paginated = Export(paginatedSource, "native-pdf-lists-pagination");
        var pageWords = paginated.GetPages().SelectMany(p => p.GetWords().Select(w => (Page: p.Number, Word: w))).ToArray();
        var rows = pageWords.Where(w => w.Word.Text.StartsWith("Row", StringComparison.Ordinal)).ToArray();
        var itemLeft = rows.First().Word.BoundingBox.Left;
        var bodyLeft = pageWords.Single(w => w.Word.Text == "AfterPagination").Word.BoundingBox.Left;
        if (paginated.NumberOfPages < 3 || rows.Length != 90
            || !rows.Select(w => w.Word.Text).SequenceEqual(Enumerable.Range(0, 90).Select(i => $"Row{i:D3}"))
            || rows.Any(w => Math.Abs(w.Word.BoundingBox.Left - itemLeft) > .2)
            || pageWords.Count(w => w.Word.Text == "41.") != 1 || pageWords.Count(w => w.Word.Text == "42.") != 1
            || itemLeft - bodyLeft < 12)
            throw new IOException("A multi-page PDF item lost rows, indentation or its single marker.");
        var wrapped = pageWords.TakeWhile(w => w.Word.Text != "42.")
            .Where(w => w.Word.Text is "wrapped" or "selectable" or "content" or "remains" or "inside" or "the" or "same" or "list" or "item").ToArray();
        if (wrapped.Length != 2430 || wrapped.Any(w => w.Word.BoundingBox.Left < itemLeft - .2 || w.Word.BoundingBox.Right > 539))
            throw new IOException("Wrapped PDF list text escaped the item or page margins.");
        var expectedWords = paginatedSource.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var actualWords = pageWords.Where(w => !w.Word.Text.All(char.IsDigit)).Select(w => w.Word.Text);
        if (!actualWords.SequenceEqual(expectedWords)) throw new IOException("Pagination lost or reordered selectable list content.");
        checks.Add("Ninety multi-line continuation paragraphs retain selectable text and indentation across pages without repeated markers");

        // A wide generated image makes clipping visible without external files or network access.
        const int imageWidth = 1200, imageHeight = 80;
        var pixels = Enumerable.Repeat((byte)120, imageWidth * imageHeight * 3).ToArray();
        var bitmap = BitmapSource.Create(imageWidth, imageHeight, 96, 96, PixelFormats.Rgb24, null, pixels, imageWidth * 3);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var imageBytes = new MemoryStream(); encoder.Save(imageBytes);
        var assets = new Dictionary<string, ExportImage> { ["wide.png"] = new(imageBytes.ToArray(), imageWidth, imageHeight, "Wide list image", DisplayWidth: 1200) };
        const string tableSource = "# List images and tables\n\n- ImageLead\n\n  ![Wide list image](wide.png)\n\n  ImageContinuation\n\n"
            + "- | TableFirst | ImageCell |\n  | --- | --- |\n  | TableValue | ![Cell image](wide.png) |\n\n  TableContinuation\n\n"
            + "- - NestedOnly\n\n    NestedOnlyContinuation\n\nAfterTables returns to the body margin.";
        using var tables = Export(tableSource, "native-pdf-lists-tables", assets);
        var tableWords = tables.GetPages().SelectMany(p => p.GetWords()).ToArray();
        var images = tables.GetPages().SelectMany(p => p.GetImages()).ToArray();
        var listLeft = tableWords.Single(w => w.Text == "ImageLead").BoundingBox.Left;
        if (tableWords.Count(w => w.Text == "•") != 4 || images.Length != 2
            || images.Any(i => i.BoundingBox.Left < listLeft - .5 || i.BoundingBox.Right > 539)
            || images.Count(i => i.BoundingBox.Width > 400) != 1 || images.Count(i => i.BoundingBox.Width < 235) != 1
            || tableWords.Single(w => w.Text == "TableFirst").BoundingBox.Left < listLeft
            || Math.Abs(tableWords.Single(w => w.Text == "TableContinuation").BoundingBox.Left - listLeft) > .2
            || tableWords.Single(w => w.Text == "NestedOnly").BoundingBox.Left - listLeft < 12
            || tableWords.Single(w => w.Text == "AfterTables").BoundingBox.Left >= listLeft - 12)
            throw new IOException("List tables, images or nested-only items lost markers, width bounds or continuation indentation.");
        checks.Add("Table-first and nested-only items retain markers; images fit the list and table cell widths");
        File.WriteAllText(Path.Combine(output, "native-pdf-lists.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = checks, PaginationPages = paginated.NumberOfPages, ContinuationRows = rows.Length,
            Geometry = pdf.GetPages().SelectMany(p => p.GetWords().Select(w => new { Page = p.Number, w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom })),
            ImageBounds = images.Select(i => new { i.BoundingBox.Left, i.BoundingBox.Right, i.BoundingBox.Width }),
            Limitation = "Checks cover selectable content and visual hierarchy; they do not certify tagged PDF list accessibility."
        }, new JsonSerializerOptions { WriteIndented = true }));

        UglyToad.PdfPig.PdfDocument Export(string markdown, string name, IReadOnlyDictionary<string, ExportImage>? images = null)
        {
            var parsed = new MarkdownService().Parse(markdown);
            var bytes = new PdfExporter().Export(new ExportContent(parsed, new Dictionary<int, ExportImage>(), images ?? new Dictionary<string, ExportImage>()));
            File.WriteAllText(Path.Combine(output, name + ".md"), markdown);
            File.WriteAllBytes(Path.Combine(output, name + ".pdf"), bytes);
            if (parsed.Source != markdown) throw new IOException("PDF list export changed Markdown source.");
            return UglyToad.PdfPig.PdfDocument.Open(bytes);
        }
    }
}
