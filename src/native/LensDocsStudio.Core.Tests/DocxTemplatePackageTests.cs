using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using System.IO.Compression;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace LensDocsStudio.Core.Tests;

public sealed class DocxTemplatePackageTests
{
    [Fact]
    public void Documents_without_fields_do_not_request_a_Word_field_update_on_open()
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content())), false);
        Assert.Empty(package.MainDocumentPart!.DocumentSettingsPart!.Settings!.Elements<W.UpdateFieldsOnOpen>());
    }

    [Fact]
    public void Supported_template_page_fields_retain_their_update_request()
    {
        var template = Template((_, main) => Header(main, new W.Paragraph(new W.SimpleField(new W.Run(new W.Text("1"))) { Instruction = "PAGE" })));
        using var package = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content(), template)), false);
        Assert.True(Assert.Single(package.MainDocumentPart!.DocumentSettingsPart!.Settings!.Elements<W.UpdateFieldsOnOpen>()).Val!.Value);
        Assert.Equal("PAGE", package.MainDocumentPart.HeaderParts.Single().Header!.Descendants<W.SimpleField>().Single().Instruction!.Value);
        Assert.Empty(new OpenXmlValidator().Validate(package));
    }

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
    private static ExportContent Content(string source = "# New document\n\nPublic content.") => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
    private static byte[] Template(Action<WordprocessingDocument, MainDocumentPart> configure)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("Original private body")))));
            configure(package, main);
        }
        return stream.ToArray();
    }
    private static HeaderPart Header(MainDocumentPart main, params OpenXmlElement[] content)
    {
        var header = main.AddNewPart<HeaderPart>(); header.Header = new W.Header(content);
        main.Document!.Body!.Append(new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) }));
        return header;
    }

    [Fact] public void Export_does_not_carry_unrelated_template_images_comments_custom_XML_or_personal_properties()
    {
        var template = Template((package, main) =>
        {
            package.PackageProperties.Creator = "Private original author";
            var image = main.AddImagePart(ImagePartType.Png); using var pixels = new MemoryStream([1, 2, 3, 4]); image.FeedData(pixels);
            var custom = main.AddCustomXmlPart(CustomXmlPartType.CustomXml); using var xml = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<private>Original private metadata</private>")); custom.FeedData(xml);
            main.AddNewPart<WordprocessingCommentsPart>().Comments = new W.Comments(new W.Comment(new W.Paragraph(new W.Run(new W.Text("Private original comment")))) { Id = "1", Author = "Original author" });
            Header(main, new W.Paragraph(new W.Run(new W.Text("Intended template header"))));
        });
        var original = template.ToArray();
        using var stream = new MemoryStream(new DocxExporter().Export(Content(), template)); using var output = WordprocessingDocument.Open(stream, false);
        var main = output.MainDocumentPart!;
        Assert.Empty(main.ImageParts); Assert.Empty(main.CustomXmlParts); Assert.Null(main.WordprocessingCommentsPart);
        Assert.NotEqual("Private original author", output.PackageProperties.Creator);
        Assert.Equal("Intended template header", main.HeaderParts.Single().Header!.InnerText);
        Assert.DoesNotContain("Original private body", main.Document!.InnerText); Assert.Equal(original, template);
        Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Theory] [InlineData("DDEAUTO cmd /c unsafe")] [InlineData("INCLUDEPICTURE https://example.com/tracker.png")]
    public void Active_simple_header_fields_are_rejected(string instruction)
    {
        var template = Template((_, main) => Header(main, new W.Paragraph(new W.SimpleField(new W.Run(new W.Text("Cached text"))) { Instruction = instruction })));
        Assert.Throws<InvalidDataException>(() => new DocxExporter().Export(Content(), template));
    }

    [Fact] public void Split_external_content_fields_are_rejected_before_they_can_update_in_Word()
    {
        var template = Template((_, main) => Header(main, new W.Paragraph(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin },
            new W.FieldCode(" INCLU"), new W.FieldCode("DETEXT https://example.com/private "), new W.FieldChar { FieldCharType = W.FieldCharValues.End }))));
        Assert.Throws<InvalidDataException>(() => new DocxExporter().Export(Content(), template));
    }

    [Fact] public void Referenced_header_pixels_links_and_page_fields_survive_without_unused_header_assets()
    {
        var imageContent = Content("![Image](image.png)") with { Images = new Dictionary<string, ExportImage> { ["image.png"] = new(Png, 1, 1, "Retained logo") } };
        using var imageStream = new MemoryStream(new DocxExporter().Export(imageContent)); using var imageDocument = WordprocessingDocument.Open(imageStream, false);
        var drawing = imageDocument.MainDocumentPart!.Document!.Descendants<W.Drawing>().Single().CloneNode(true);
        var template = Template((_, main) =>
        {
            var header = Header(main, new W.Paragraph(new W.Run(drawing), new W.SimpleField(new W.Run(new W.Text("1"))) { Instruction = " PAGE \\* MERGEFORMAT " }));
            var logo = header.AddImagePart(ImagePartType.Png, "logo"); using var data = new MemoryStream(Png); logo.FeedData(data);
            drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Single().Embed = "logo";
            var unused = header.AddImagePart(ImagePartType.Png, "unused"); using var unusedData = new MemoryStream([9, 8, 7]); unused.FeedData(unusedData);
            header.AddHyperlinkRelationship(new Uri("https://example.com/documentation"), true, "site");
            header.Header!.Append(new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Documentation"))) { Id = "site" }));
        });
        using var stream = new MemoryStream(new DocxExporter().Export(imageContent, template)); using var output = WordprocessingDocument.Open(stream, false);
        var headerPart = output.MainDocumentPart!.HeaderParts.Single();
        Assert.Single(headerPart.ImageParts); using var logoStream = headerPart.ImageParts.Single().GetStream(); using var copy = new MemoryStream(); logoStream.CopyTo(copy); Assert.Equal(Png, copy.ToArray());
        Assert.Equal("https://example.com/documentation", headerPart.HyperlinkRelationships.Single().Uri.AbsoluteUri);
        Assert.Contains("PAGE", headerPart.Header!.Descendants<W.SimpleField>().Single().Instruction!.Value);
        var allIds = output.MainDocumentPart.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Select(element => element.Id!.Value)
            .Concat(headerPart.Header.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Select(element => element.Id!.Value)).ToArray();
        Assert.Equal(allIds.Length, allIds.Distinct().Count());
        Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Fact] public void Last_section_inherits_default_header_and_retains_first_even_footer_layout_and_safe_settings()
    {
        var template = Template((_, main) =>
        {
            HeaderPart AddHeader(string text) { var part = main.AddNewPart<HeaderPart>(); part.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text(text)))); return part; }
            var normal = AddHeader("Default inherited"); var first = AddHeader("First header"); var even = AddHeader("Even header");
            var footer = main.AddNewPart<FooterPart>(); footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("Footer"))));
            main.Document!.Body!.Append(new W.Paragraph(new W.ParagraphProperties(new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(normal) }))));
            main.Document.Body.Append(new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.First, Id = main.GetIdOfPart(first) },
                new W.HeaderReference { Type = W.HeaderFooterValues.Even, Id = main.GetIdOfPart(even) }, new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                new W.PageSize { Width = 15840, Height = 12240, Orient = W.PageOrientationValues.Landscape }, new W.TitlePage()));
            main.AddNewPart<DocumentSettingsPart>().Settings = new W.Settings(new W.DefaultTabStop { Val = 720 }, new W.EvenAndOddHeaders(),
                new W.DocumentVariables(new W.DocumentVariable { Name = "PrivateOriginalValue", Val = "Do not distribute" }));
        });
        var prepared = new DocxTemplateService().Prepare(template); Assert.Contains(prepared.Warnings, message => message.Contains("final section"));
        using var stream = new MemoryStream(new DocxExporter().Export(Content(), prepared.Bytes)); using var output = WordprocessingDocument.Open(stream, false);
        var main = output.MainDocumentPart!; var section = main.Document!.Body!.Elements<W.SectionProperties>().Single();
        Assert.True(section.Elements<W.HeaderReference>().Count() == 3, section.OuterXml); Assert.Single(section.Elements<W.FooterReference>());
        Assert.Contains(main.HeaderParts, header => header.Header!.InnerText == "Default inherited");
        Assert.NotNull(section.GetFirstChild<W.TitlePage>()); Assert.Equal(W.PageOrientationValues.Landscape, section.GetFirstChild<W.PageSize>()!.Orient!.Value);
        Assert.NotNull(main.DocumentSettingsPart!.Settings!.GetFirstChild<W.EvenAndOddHeaders>()); Assert.Null(main.DocumentSettingsPart.Settings.GetFirstChild<W.DocumentVariables>());
        Assert.Equal(720, main.DocumentSettingsPart.Settings.GetFirstChild<W.DefaultTabStop>()!.Val!.Value);
        Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Fact] public void Template_numbering_is_retained_and_generated_lists_use_distinct_definition_ids()
    {
        var template = new DocxExporter().Export(Content("4. Original item"));
        using var originalStream = new MemoryStream(template); using var original = WordprocessingDocument.Open(originalStream, false);
        var definitions = original.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Elements<W.AbstractNum>().Select(element => element.OuterXml).ToArray();
        using var stream = new MemoryStream(new DocxExporter().Export(Content("7. New item"), template)); using var output = WordprocessingDocument.Open(stream, false);
        var numbering = output.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
        foreach (var definition in definitions) Assert.Contains(numbering.Elements<W.AbstractNum>(), element => element.OuterXml == definition);
        Assert.Equal(2, numbering.Elements<W.NumberingInstance>().Count());
        Assert.Equal(2, numbering.Elements<W.NumberingInstance>().Select(element => element.NumberID!.Value).Distinct().Count());
        Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Fact] public void Retained_colour_and_font_theme_survives_the_fresh_package()
    {
        static A.RgbColorModelHex Rgb(string colour) => new() { Val = colour };
        static A.SolidFill Fill() => new(Rgb("245577"));
        var theme = new A.Theme(new A.ThemeElements(new A.ColorScheme(new A.Dark1Color(Rgb("000000")), new A.Light1Color(Rgb("FFFFFF")),
                new A.Dark2Color(Rgb("245577")), new A.Light2Color(Rgb("F8F9FB")), new A.Accent1Color(Rgb("FF883E")), new A.Accent2Color(Rgb("245577")),
                new A.Accent3Color(Rgb("336655")), new A.Accent4Color(Rgb("665533")), new A.Accent5Color(Rgb("553366")), new A.Accent6Color(Rgb("336677")),
                new A.Hyperlink(Rgb("0000FF")), new A.FollowedHyperlinkColor(Rgb("800080"))) { Name = "Generic colours" },
            new A.FontScheme(new A.MajorFont(new A.LatinFont { Typeface = "Arial" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" })) { Name = "Generic fonts" },
            new A.FormatScheme(new A.FillStyleList(Fill(), Fill(), Fill()), new A.LineStyleList(new A.Outline(Fill()), new A.Outline(Fill()), new A.Outline(Fill())),
                new A.EffectStyleList(new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList())),
                new A.BackgroundFillStyleList(Fill(), Fill(), Fill())) { Name = "Generic formatting" })) { Name = "Generic test theme" };
        var template = Template((_, main) => main.AddNewPart<ThemePart>().Theme = theme);
        using var originalStream = new MemoryStream(template); using var original = WordprocessingDocument.Open(originalStream, false);
        using var stream = new MemoryStream(new DocxExporter().Export(Content(), template)); using var output = WordprocessingDocument.Open(stream, false);
        Assert.Equal(original.MainDocumentPart!.ThemePart!.Theme!.OuterXml, output.MainDocumentPart!.ThemePart!.Theme!.OuterXml);
        Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Fact] public void Large_existing_numbering_ids_do_not_overflow_generated_list_definitions()
    {
        var template = new DocxExporter().Export(Content("4. Template item"));
        using var editing = new MemoryStream(); editing.Write(template);
        using (var package = WordprocessingDocument.Open(editing, true))
        {
            var numbering = package.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
            numbering.Elements<W.AbstractNum>().Single().AbstractNumberId = int.MaxValue;
            var number = numbering.Elements<W.NumberingInstance>().Single(); number.NumberID = int.MaxValue; number.AbstractNumId!.Val = int.MaxValue;
        }
        using var stream = new MemoryStream(new DocxExporter().Export(Content("8. New item"), editing.ToArray())); using var output = WordprocessingDocument.Open(stream, false);
        var values = output.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Elements<W.NumberingInstance>().Select(element => element.NumberID!.Value).ToArray();
        Assert.Contains(int.MaxValue, values); Assert.Contains(1, values); Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Theory] [InlineData("missing-resource")] [InlineData("incomplete-field")] [InlineData("invalid-outline")]
    public void Incomplete_or_invalid_retained_template_structures_are_rejected(string variant)
    {
        var template = Template((_, main) =>
        {
            var paragraph = variant switch
            {
                "missing-resource" => new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Missing"))) { Id = "missing" }),
                "incomplete-field" => new W.Paragraph(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }, new W.FieldCode("PAGE"))),
                _ => new W.Paragraph(new W.ParagraphProperties(new W.OutlineLevel { Val = 12 }), new W.Run(new W.Text("Invalid")))
            };
            Header(main, paragraph);
        });
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(template));
    }

    [Fact] public void Macro_enabled_and_oversized_source_packages_are_rejected()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.MacroEnabledDocument, true))
            package.AddMainDocumentPart().Document = new W.Document(new W.Body(new W.Paragraph()));
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(stream.ToArray()));
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(new byte[32 * 1024 * 1024 + 1]));
    }

    [Fact] public void Retained_header_active_objects_and_resource_attributes_are_rejected()
    {
        var active = Template((_, main) => Header(main, new W.Paragraph(new W.Run(new W.EmbeddedObject()))));
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(active));
        var resource = Template((_, main) =>
        {
            var data = new OpenXmlUnknownElement("v", "imagedata", "urn:schemas-microsoft-com:vml");
            data.SetAttribute(new OpenXmlAttribute("", "src", "", "https://example.com/picture.png"));
            Header(main, new W.Paragraph(new W.Run(new W.Picture(data))));
        });
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(resource));
    }

    [Theory] [InlineData("../escape.xml")] [InlineData("/absolute.xml")] [InlineData("word\\unsafe.xml")]
    public void Unsafe_archive_paths_are_rejected_before_package_parsing(string path)
    {
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) zip.CreateEntry(path);
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(stream.ToArray()));
    }

    [Fact] public void Duplicate_paths_excessive_entries_and_large_expanded_parts_are_rejected()
    {
        foreach (var variant in new[] { "duplicates", "entries", "expanded" })
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                if (variant == "duplicates") { zip.CreateEntry("word/a.xml"); zip.CreateEntry("WORD/A.XML"); }
                else if (variant == "entries") for (var index = 0; index < 1025; index++) zip.CreateEntry("part" + index);
                else { using var data = zip.CreateEntry("word/large.xml").Open(); var buffer = new byte[1024 * 1024]; for (var index = 0; index < 33; index++) data.Write(buffer); }
            }
            Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(stream.ToArray()));
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Corrupt_checksums_and_declared_sizes_are_rejected(bool size)
    {
        var bytes = Template((_, _) => { }); var offset = -1;
        for (var index = 0; index < bytes.Length - 4; index++) if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index, 4)) == 0x02014b50) { offset = index; break; }
        Assert.True(offset >= 0);
        bytes[offset + (size ? 24 : 16)] ^= 0xff;
        Assert.Throws<InvalidDataException>(() => new DocxTemplateService().Prepare(bytes));
    }

    [Fact] public async Task Cancelled_template_reads_leave_original_bytes_unchanged()
    {
        var bytes = Template((_, _) => { }); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new DocxTemplateService().Prepare(bytes, cancelled.Token));
        var path = Path.Combine(Path.GetTempPath(), "lens-template-" + Guid.NewGuid().ToString("N") + ".docx");
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DocxTemplateService().ReadAsync(path, cancelled.Token));
            var prepared = await new DocxTemplateService().ReadAsync(path); Assert.NotEmpty(prepared.Bytes); Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally { File.Delete(path); }
    }
}
