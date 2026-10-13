using System.Globalization;
using System.Text;
using LensDocsStudio.Core.Imports;

namespace LensDocsStudio.Core.Tests;

public sealed class PdfImportLimitTests
{
    [Fact]
    public void Shared_page_content_is_bounded_across_the_complete_import()
    {
        var bytes = SharedContentPdf(20, "Repeated text"); var original = bytes.ToArray();
        var service = new DocumentImportService();
        var control = service.ImportPdf(bytes, "Control");
        Assert.Equal(20, control.Markdown.Split("Repeated text", StringSplitOptions.None).Length - 1);
        Assert.Throws<InvalidDataException>(() => service.ImportPdf(bytes, "Control", limits: new(MaximumTextCharacters: 100)));
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void Exact_character_and_UTF8_limits_include_titles_page_headers_and_separators()
    {
        var bytes = SharedContentPdf(2, "Text"); var service = new DocumentImportService();
        var expected = "# Café\n\n## Page 1\n\nText" + Environment.NewLine + Environment.NewLine
            + "## Page 2\n\nText" + Environment.NewLine + Environment.NewLine;
        var length = expected.Length; var byteLength = Encoding.UTF8.GetByteCount(expected);
        var result = service.ImportPdf(bytes, "Café", limits: new(MaximumTextCharacters: length, MaximumOutputBytes: byteLength));
        Assert.Equal(expected, result.Markdown); Assert.Empty(result.Assets); Assert.Single(result.Warnings);
        Assert.Throws<InvalidDataException>(() => service.ImportPdf(bytes, "Café", limits: new(MaximumTextCharacters: length - 1)));
        Assert.Throws<InvalidDataException>(() => service.ImportPdf(bytes, "Café", limits: new(MaximumOutputBytes: byteLength - 1)));
    }

    [Fact]
    public void Page_text_is_rejected_before_aggregating_an_oversized_page()
    {
        var bytes = SharedContentPdf(1, "Long page text"); var service = new DocumentImportService();
        Assert.Contains("Long page text", service.ImportPdf(bytes, "Control").Markdown);
        Assert.Throws<InvalidDataException>(() => service.ImportPdf(bytes, "Control", limits: new(MaximumPageTextCharacters: 4)));
    }

    [Fact]
    public void Page_count_and_empty_page_overheads_are_bounded()
    {
        var bytes = SharedContentPdf(3, ""); var service = new DocumentImportService();
        Assert.Contains("## Page 3", service.ImportPdf(bytes, "Control").Markdown);
        Assert.Throws<InvalidDataException>(() => service.ImportPdf(bytes, "Control", limits: new(MaximumPages: 2)));
        Assert.Throws<InvalidDataException>(() => service.ImportPdf(bytes, "Control", limits: new(MaximumTextCharacters: 25)));
    }

    [Fact]
    public void Cancelled_imports_stop_before_parsing_or_reading_the_input_path()
    {
        Assert.Throws<OperationCanceledException>(() => new DocumentImportService().ImportPdf([], "Cancelled", new(true)));
    }

    [Fact]
    public async Task Cancelled_file_imports_do_not_read_a_missing_input()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DocumentImportService().ImportAsync("missing.pdf", new(true)));
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void Non_positive_limits_are_rejected(int pages, int characters, int bytes, int pageCharacters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentImportService().ImportPdf([], "Control",
            limits: new(pages, characters, bytes, pageCharacters)));
    }

    // All pages reference one uncompressed content stream, reproducing amplification without large allocations.
    private static byte[] SharedContentPdf(int pages, string text)
    {
        var content = "BT /F1 12 Tf 50 700 Td (" + text + ") Tj ET\n";
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count " + pages + " /Kids [" + string.Join(' ', Enumerable.Range(5, pages).Select(i => i + " 0 R")) + "] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Length " + Encoding.ASCII.GetByteCount(content) + " >>\nstream\n" + content + "endstream"
        };
        objects.AddRange(Enumerable.Range(0, pages).Select(_ => "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents 4 0 R >>"));
        var output = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Length); output.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = output.Length;
        output.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets) output.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        output.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }
}
