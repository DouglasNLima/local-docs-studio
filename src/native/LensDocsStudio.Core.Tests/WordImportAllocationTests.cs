using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordImportAllocationTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");

    [Fact]
    public void Relationship_aliases_share_one_retained_image_and_preserve_every_reference()
    {
        var bytes = Package(64, aliases: true);
        using (var document = WordprocessingDocument.Open(new MemoryStream(bytes), false))
        {
            var parts = document.MainDocumentPart!.Parts.Where(p => p.OpenXmlPart is ImagePart).ToArray();
            Assert.Equal(64, parts.Length);
            Assert.Single(parts.Select(p => p.OpenXmlPart.Uri).Distinct());
        }
        var result = new DocumentImportService().ImportDocx(bytes, limits: new(Png.Length, Png.Length, 1));
        var asset = Assert.Single(result.Assets);
        Assert.Equal(Png, asset.Bytes);
        Assert.Equal(64, result.Markdown.Split(asset.Reference).Length - 1);
    }

    [Theory]
    [InlineData(67, 500, 10)]
    [InlineData(100, 100, 10)]
    [InlineData(100, 500, 1)]
    public void Distinct_image_parts_respect_individual_total_and_count_limits(long perImage, long total, int count)
    {
        var bytes = Package(3, aliases: false);
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes, limits: new(perImage, total, count)));
    }

    [Fact]
    public void Cancelled_import_does_not_parse_the_package()
    {
        Assert.Throws<OperationCanceledException>(() => new DocumentImportService().ImportDocx([], new CancellationToken(true)));
    }

    private static byte[] Package(int imageCount, bool aliases)
    {
        var source = string.Join("\n\n", Enumerable.Range(0, imageCount).Select(i => $"![Image](image-{i}.png)"));
        // Separate byte arrays create distinct valid image parts; aliases are introduced only in the package relationship map below.
        var images = Enumerable.Range(0, imageCount).ToDictionary(i => $"image-{i}.png", _ => new ExportImage(Png.ToArray(), 1, 1, "Image"));
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), images));
        if (!aliases) return bytes;
        using var stream = new MemoryStream(); stream.Write(bytes);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("word/_rels/document.xml.rels")!;
            XDocument xml;
            using (var input = entry.Open()) xml = XDocument.Load(input);
            var relationships = xml.Root!.Elements().Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/image", StringComparison.Ordinal) == true).ToArray();
            var target = (string)relationships[0].Attribute("Target")!;
            foreach (var relationship in relationships) relationship.SetAttributeValue("Target", target);
            entry.Delete();
            using var output = zip.CreateEntry("word/_rels/document.xml.rels").Open(); xml.Save(output);
        }
        return stream.ToArray();
    }
}
