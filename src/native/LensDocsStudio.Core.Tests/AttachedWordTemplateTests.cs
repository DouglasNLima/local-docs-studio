using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class AttachedWordTemplateTests
{
    private const string Type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static ExportContent Content(string text) => new(new MarkdownService().Parse(text), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());

    private static byte[] Template(string target = "https://example.invalid/private-template.dotx", bool settingsContext = true)
    {
        using var stream = new MemoryStream();
        stream.Write(new DocxExporter().Export(Content("# Original heading\n\n3. Original list\n4. Second item")));
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart!;
            var settings = main.DocumentSettingsPart!;
            settings.Settings!.Append(new W.AttachedTemplate { Id = "attached" });
            (settingsContext ? (OpenXmlPart)settings : main).AddExternalRelationship(Type, new Uri(target, UriKind.RelativeOrAbsolute), "attached");
            var header = main.AddNewPart<HeaderPart>(); header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Retained header"))));
            var footer = main.AddNewPart<FooterPart>(); footer.Footer = new W.Footer(new W.Paragraph(new W.SimpleField(new W.Run(new W.Text("1"))) { Instruction = "PAGE" }));
            var section = main.Document!.Body!.Elements<W.SectionProperties>().Single();
            section.PrependChild(new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) });
            section.PrependChild(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) });
        }
        return stream.ToArray();
    }

    [Theory]
    [InlineData("https://example.invalid/private-template.dotx")]
    [InlineData("http://127.0.0.1:9/missing.dotx")]
    [InlineData("file:///C:/Unavailable/private-template.dotx")]
    [InlineData("file://unavailable.invalid/templates/private-template.dotx")]
    [InlineData("\\\\unavailable.invalid\\templates\\private-template.dotx")]
    public void Passive_attached_templates_are_removed_without_retaining_targets_or_changing_source(string target)
    {
        var bytes = Template(target); var original = bytes.ToArray();
        var prepared = new DocxTemplateService().Prepare(bytes);
        Assert.Equal(original, bytes);
        Assert.Contains(DocxTemplateService.AttachedTemplateWarning, prepared.Warnings);
        Assert.All(prepared.Warnings, warning => Assert.DoesNotContain(target, warning));
        using var source = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        using var result = WordprocessingDocument.Open(new MemoryStream(prepared.Bytes), false);
        var main = result.MainDocumentPart!;
        Assert.Empty(main.DocumentSettingsPart!.ExternalRelationships);
        Assert.Empty(main.DocumentSettingsPart.Settings!.Descendants<W.AttachedTemplate>());
        Assert.Equal(source.MainDocumentPart!.StyleDefinitionsPart!.Styles!.OuterXml, main.StyleDefinitionsPart!.Styles!.OuterXml);
        Assert.Equal(source.MainDocumentPart.NumberingDefinitionsPart!.Numbering!.OuterXml, main.NumberingDefinitionsPart!.Numbering!.OuterXml);
        Assert.Equal(source.MainDocumentPart.Document!.Body!.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageSize>()!.OuterXml,
            main.Document!.Body!.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageSize>()!.OuterXml);
        Assert.Equal("Retained header", main.HeaderParts.Single().Header!.InnerText);
        Assert.Equal("PAGE", main.FooterParts.Single().Footer!.Descendants<W.SimpleField>().Single().Instruction!.Value);
        Assert.DoesNotContain("Original heading", main.Document.InnerText);
        Assert.Empty(new OpenXmlValidator().Validate(result));
        using var exported = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content("# New heading\n\n1. New item"), prepared.Bytes)), false);
        Assert.Empty(exported.MainDocumentPart!.DocumentSettingsPart!.ExternalRelationships);
        Assert.Empty(exported.MainDocumentPart.DocumentSettingsPart.Settings!.Descendants<W.AttachedTemplate>());
        Assert.Empty(new OpenXmlValidator().Validate(exported));
    }

    [Fact]
    public void Import_does_not_connect_to_an_attached_template_server()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        new DocxTemplateService().Prepare(Template($"http://127.0.0.1:{port}/template.dotx"));
        Assert.False(listener.Pending());
    }

    [Fact]
    public async Task Import_ignores_a_locked_local_template_and_preserves_both_original_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "lens-attached-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var target = Path.Combine(root, "locked.dotx"); var source = Path.Combine(root, "source.docx");
        try
        {
            await File.WriteAllTextAsync(target, "Do not open or change");
            var bytes = Template(new Uri(target).AbsoluteUri); await File.WriteAllBytesAsync(source, bytes);
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.NotEmpty((await new DocxTemplateService().ReadAsync(source)).Bytes);
            Assert.Equal("Do not open or change", await File.ReadAllTextAsync(target));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        }
        finally { File.Delete(source); File.Delete(target); Directory.Delete(root); }
    }

    [Theory]
    [InlineData("missing-reference")]
    [InlineData("wrong-id")]
    [InlineData("duplicate-reference")]
    [InlineData("nested-reference")]
    [InlineData("missing-relationship")]
    public void Inconsistent_settings_references_are_rejected(string variant)
    {
        using var stream = new MemoryStream(); stream.Write(Template());
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var part = document.MainDocumentPart!.DocumentSettingsPart!; var reference = part.Settings!.Elements<W.AttachedTemplate>().Single();
            switch (variant)
            {
                case "missing-reference": reference.Remove(); break;
                case "wrong-id": reference.Id = "missing"; break;
                case "duplicate-reference": part.Settings.Append(reference.CloneNode(true)); break;
                case "nested-reference": reference.Remove(); part.Settings.Append(new W.Compatibility(reference)); break;
                case "missing-relationship": part.DeleteExternalRelationship("attached"); break;
            }
        }
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(stream.ToArray()));
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("missing-id")]
    [InlineData("missing-target")]
    [InlineData("internal")]
    [InlineData("unknown-mode")]
    [InlineData("unknown-attribute")]
    [InlineData("unknown-type")]
    public void Malformed_attached_relationship_metadata_is_rejected_before_SDK_loading(string variant)
    {
        using var stream = new MemoryStream(); stream.Write(Template());
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("word/_rels/settings.xml.rels")!; XDocument xml;
            using (var data = entry.Open()) xml = XDocument.Load(data);
            var relationship = xml.Root!.Elements(Relationships + "Relationship").Single();
            switch (variant)
            {
                case "duplicate-id": xml.Root.Add(new XElement(relationship)); break;
                case "missing-id": relationship.Attribute("Id")!.Remove(); break;
                case "missing-target": relationship.Attribute("Target")!.Remove(); break;
                case "internal": relationship.Attribute("TargetMode")!.Remove(); break;
                case "unknown-mode": relationship.SetAttributeValue("TargetMode", "external"); break;
                case "unknown-attribute": relationship.SetAttributeValue("Action", "open"); break;
                case "unknown-type": relationship.SetAttributeValue("Type", Type + "Unknown"); break;
            }
            using var output = entry.Open(); output.SetLength(0); xml.Save(output);
        }
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(stream.ToArray()));
    }

    [Fact]
    public void Attached_template_on_the_document_part_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(Template(settingsContext: false)));

    [Theory]
    [InlineData("image")]
    [InlineData("oleObject")]
    [InlineData("aFChunk")]
    [InlineData("unknown-active-resource")]
    public void Other_external_resources_remain_blocked_alongside_an_allowed_attached_template(string type)
    {
        using var stream = new MemoryStream(); stream.Write(Template());
        using (var document = WordprocessingDocument.Open(stream, true))
            document.MainDocumentPart!.AddExternalRelationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/" + type,
                new Uri("https://example.invalid/active"), "unsafe");
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(stream.ToArray()));
    }
}
