using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Imports;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Exports;

public sealed record PreparedWordTemplate(byte[] Bytes, IReadOnlyList<string> Warnings);

/// <summary>Extracts supported formatting into a new package without retaining the source document.</summary>
public sealed class DocxTemplateService
{
    private const int MaximumBytes = 32 * 1024 * 1024;
    private const long MaximumExpandedBytes = 128L * 1024 * 1024;
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly HashSet<string> SafeSettings = new(StringComparer.Ordinal)
    {
        "zoom", "proofState", "defaultTabStop", "autoHyphenation", "consecutiveHyphenLimit", "hyphenationZone", "doNotHyphenateCaps",
        "evenAndOddHeaders", "mirrorMargins", "bordersDoNotSurroundHeader", "bordersDoNotSurroundFooter", "gutterAtTop", "compat",
        "themeFontLang", "clrSchemeMapping", "decimalSymbol", "listSeparator", "doNotUseMarginsForDrawingGridOrigin",
        "drawingGridHorizontalSpacing", "drawingGridVerticalSpacing", "displayHorizontalDrawingGridEvery", "displayVerticalDrawingGridEvery"
    };
    private static readonly HashSet<string> SafeSection = new(StringComparer.Ordinal)
    {
        "type", "pgSz", "pgMar", "paperSrc", "pgBorders", "lnNumType", "pgNumType", "cols", "vAlign", "noEndnote",
        "titlePg", "textDirection", "bidi", "rtlGutter", "docGrid"
    };
    private static readonly HashSet<string> SafeTable = new(StringComparer.Ordinal)
    {
        "tblStyle", "tblpPr", "tblOverlap", "bidiVisual", "tblStyleRowBandSize", "tblStyleColBandSize", "tblW", "jc",
        "tblCellSpacing", "tblInd", "tblBorders", "shd", "tblLayout", "tblCellMar", "tblLook"
    };
    private static readonly HashSet<string> SafeFields = new(StringComparer.OrdinalIgnoreCase)
        { "PAGE", "NUMPAGES", "SECTION", "SECTIONPAGES", "DATE", "TIME", "CREATEDATE", "PRINTDATE", "SAVEDATE" };

    public async Task<PreparedWordTemplate> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (input.Length > MaximumBytes) throw new InvalidDataException("The Word template exceeds 32 MB.");
        var bytes = await MarkdownBundleService.ReadBoundedAsync(input, MaximumBytes, cancellationToken);
        return await Task.Run(() => Prepare(bytes, cancellationToken), cancellationToken);
    }

    public PreparedWordTemplate Prepare(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ValidateArchive(bytes, cancellationToken);
        using var input = new MemoryStream(bytes, false);
        using var source = WordprocessingDocument.Open(input, false, new OpenSettings { AutoSave = false, MaxCharactersInPart = 16 * 1024 * 1024 });
        var original = source.MainDocumentPart ?? throw new InvalidDataException("The Word template has no document part.");
        if (source.DocumentType != WordprocessingDocumentType.Document) throw new InvalidDataException("Select an ordinary .docx template document without macros.");
        ValidateRelationships(original, cancellationToken);
        var originalBody = original.Document?.Body ?? throw new InvalidDataException("The Word template has no document body.");
        var warnings = new List<string>();
        using var output = new MemoryStream();
        using (var target = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document, true))
        {
            var main = target.AddMainDocumentPart(); var body = new W.Body(); main.Document = new W.Document(body);
            Copy(original.StyleDefinitionsPart, () => main.AddNewPart<StyleDefinitionsPart>());
            Copy(original.NumberingDefinitionsPart, () => main.AddNewPart<NumberingDefinitionsPart>());
            Copy(original.ThemePart, () => main.AddNewPart<ThemePart>());
            if (original.DocumentSettingsPart?.Settings is { } settings)
            {
                var safe = new W.Settings(settings.ChildElements.Where(element => element.NamespaceUri == WordNamespace && SafeSettings.Contains(element.LocalName)).Select(element => element.CloneNode(true)));
                if (safe.ChildElements.Count != settings.ChildElements.Count) warnings.Add("Unsupported document settings were omitted from the template.");
                ValidateMarkup(safe); RejectUnresolvedRelationships(safe);
                main.AddNewPart<DocumentSettingsPart>().Settings = safe;
            }
            var prototype = originalBody.Descendants<W.Table>().FirstOrDefault()?.TableProperties;
            if (prototype is not null)
            {
                var properties = new W.TableProperties(prototype.ChildElements.Where(element => element.NamespaceUri == WordNamespace && SafeTable.Contains(element.LocalName)).Select(element => element.CloneNode(true)));
                ValidateMarkup(properties); RejectUnresolvedRelationships(properties);
                body.Append(new W.Table(properties, new W.TableGrid(new W.GridColumn()), new W.TableRow(new W.TableCell(new W.Paragraph()))));
            }
            var sections = originalBody.Descendants<W.SectionProperties>().Where(section => !section.Ancestors<W.SectionPropertiesChange>().Any()).ToArray();
            var last = sections.LastOrDefault();
            var final = new W.SectionProperties(last?.ChildElements.Where(element => element.NamespaceUri == WordNamespace && SafeSection.Contains(element.LocalName)).Select(element => element.CloneNode(true)) ?? []);
            ValidateMarkup(final); RejectUnresolvedRelationships(final);
            // Missing references inherit the matching type from earlier sections in Word.
            var headers = new Dictionary<W.HeaderFooterValues, W.HeaderReference>();
            var footers = new Dictionary<W.HeaderFooterValues, W.FooterReference>();
            foreach (var section in sections)
            {
                foreach (var reference in section.Elements<W.HeaderReference>()) headers[reference.Type?.Value ?? W.HeaderFooterValues.Default] = reference;
                foreach (var reference in section.Elements<W.FooterReference>()) footers[reference.Type?.Value ?? W.HeaderFooterValues.Default] = reference;
            }
            var copied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in headers.Values)
            {
                var id = reference.Id?.Value ?? throw new InvalidDataException("A template header reference is missing its relationship.");
                if (original.GetPartById(id) is not HeaderPart header) throw new InvalidDataException("A template header reference has an unsupported target.");
                if (copied.Add(id)) Copy(header, () => main.AddNewPart<HeaderPart>(id));
                final.Append(reference.CloneNode(true));
            }
            foreach (var reference in footers.Values)
            {
                var id = reference.Id?.Value ?? throw new InvalidDataException("A template footer reference is missing its relationship.");
                if (original.GetPartById(id) is not FooterPart footer) throw new InvalidDataException("A template footer reference has an unsupported target.");
                if (copied.Add(id)) Copy(footer, () => main.AddNewPart<FooterPart>(id));
                final.Append(reference.CloneNode(true));
            }
            // AddChild replaces alternatives in a schema choice; append all reference types explicitly.
            var ordered = new W.SectionProperties(final.Elements<W.HeaderReference>().Select(element => element.CloneNode(true)));
            ordered.Append(final.Elements<W.FooterReference>().Select(element => element.CloneNode(true)));
            ordered.Append(final.ChildElements.Where(element => element is not (W.HeaderReference or W.FooterReference)).Select(element => element.CloneNode(true)));
            if (last is not null || headers.Count > 0 || footers.Count > 0) body.Append(ordered);
            if (original.Parts.Any(pair => pair.OpenXmlPart is not (StyleDefinitionsPart or NumberingDefinitionsPart or ThemePart or DocumentSettingsPart or HeaderPart or FooterPart)))
                warnings.Add("Original document content, metadata and unrelated package parts were excluded.");
            if (sections.Length > 1) warnings.Add("The final section's page layout and inherited headers/footers are used for the generated document.");
            main.Document.Save();
            var validation = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(FileFormatVersions.Microsoft365) { MaxNumberOfErrors = 1 };
            if (validation.Validate(target, cancellationToken).FirstOrDefault() is { } invalid)
                throw new InvalidDataException("The retained Word template structure is invalid: " + invalid.Description);

            void Copy(OpenXmlPart? part, Func<OpenXmlPart> create)
            {
                if (part is null) return;
                cancellationToken.ThrowIfCancellationRequested();
                var root = part.RootElement ?? throw new InvalidDataException("A supported Word template part has no XML content.");
                ValidateMarkup(root);
                var destination = create();
                using (var stream = destination.GetStream(FileMode.Create, FileAccess.Write)) root.Save(stream);
                var ids = RelationshipIds(root).Distinct(StringComparer.Ordinal);
                foreach (var id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var link = part.HyperlinkRelationships.FirstOrDefault(link => link.Id == id);
                    if (link is not null) { destination.AddHyperlinkRelationship(link.Uri, link.IsExternal, id); continue; }
                    var child = part.Parts.FirstOrDefault(pair => pair.RelationshipId == id).OpenXmlPart;
                    if (child is not ImagePart image || image.Parts.Any() || image.ExternalRelationships.Any() || image.HyperlinkRelationships.Any())
                        throw new InvalidDataException("A retained template part uses an unsupported or missing resource. Use embedded static images in headers and footers.");
                    if (image.ContentType is not ("image/png" or "image/jpeg" or "image/gif" or "image/bmp" or "image/tiff" or "image/x-emf" or "image/x-wmf"))
                        throw new InvalidDataException("A template image has an unsupported format. Use PNG, JPEG, GIF, BMP, TIFF, EMF or WMF.");
                    destination.AddPart(image, id);
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(output.ToArray(), warnings);
    }

    private static void ValidateRelationships(OpenXmlPart first, CancellationToken cancellationToken)
    {
        var pending = new Queue<OpenXmlPart>(); var seen = new HashSet<OpenXmlPart>(); pending.Enqueue(first);
        while (pending.TryDequeue(out var part))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (!seen.Add(part)) continue;
            if (part is VbaProjectPart) throw new InvalidDataException("Macro-enabled templates are not supported.");
            if (part.ExternalRelationships.Any()) throw new InvalidDataException("Templates with external resources or attached remote templates are not supported.");
            foreach (var link in part.HyperlinkRelationships)
                if (!link.Uri.IsAbsoluteUri || link.Uri.Scheme is not ("https" or "http" or "mailto")) throw new InvalidDataException("The Word template contains an unsafe hyperlink.");
            foreach (var child in part.Parts) pending.Enqueue(child.OpenXmlPart);
        }
    }

    private static IEnumerable<string> RelationshipIds(OpenXmlElement root) => root.Descendants().Prepend(root)
        .SelectMany(element => element.GetAttributes()).Where(attribute => attribute.NamespaceUri == RelationshipNamespace)
        .Select(attribute => attribute.Value ?? throw new InvalidDataException("A template resource reference is empty."));
    private static void RejectUnresolvedRelationships(OpenXmlElement root)
    {
        if (RelationshipIds(root).Any()) throw new InvalidDataException("Template page or table settings contain an unsupported resource reference.");
    }
    private static void ValidateMarkup(OpenXmlElement root)
    {
        StringBuilder? instruction = null; var separated = false;
        foreach (var element in root.Descendants().Prepend(root))
        {
            if (element.GetAttributes().Any(attribute => attribute.LocalName is "src" or "href" or "relid" or "action"))
                throw new InvalidDataException("A retained template part contains a direct resource or action attribute. Use embedded static images and ordinary Word hyperlinks.");
            if (element.LocalName is "object" or "OLEObject" or "control" or "altChunk" or "attachedTemplate" or "dataBinding" or "subDoc" or "contentPart" or "webVideoPr"
                || element.NamespaceUri == WordNamespace && (element.LocalName.EndsWith("Change", StringComparison.Ordinal) || element.LocalName is "ins" or "del" or "moveFrom" or "moveTo" or "commentRangeStart" or "commentRangeEnd" or "commentReference"))
                throw new InvalidDataException("A retained template part contains active content, comments or tracked changes. Remove them in Word before importing the template.");
            if (element is W.SimpleField simple) CheckField(simple.Instruction?.Value ?? "");
            if (element is W.FieldChar field)
            {
                if (field.FieldCharType?.Value == W.FieldCharValues.Begin)
                {
                    if (instruction is not null) throw new InvalidDataException("Nested template fields are not supported.");
                    instruction = new StringBuilder(); separated = false;
                }
                else if (field.FieldCharType?.Value == W.FieldCharValues.Separate)
                { if (instruction is null || separated) throw new InvalidDataException("A template field is incomplete."); CheckField(instruction.ToString()); separated = true; }
                else if (field.FieldCharType?.Value == W.FieldCharValues.End)
                { if (instruction is null) throw new InvalidDataException("A template field is incomplete."); CheckField(instruction.ToString()); instruction = null; }
                else throw new InvalidDataException("A template field has an invalid boundary.");
            }
            if (element is W.FieldCode code)
            {
                if (instruction is null || separated) throw new InvalidDataException("A template field instruction has no valid boundary.");
                instruction.Append(code.Text); if (instruction.Length > 4096) throw new InvalidDataException("A template field exceeds the supported length.");
            }
        }
        if (instruction is not null) throw new InvalidDataException("A template field is incomplete.");
        static void CheckField(string value)
        {
            var trimmed = value.TrimStart(); var end = 0;
            while (end < trimmed.Length && char.IsAsciiLetter(trimmed[end])) end++;
            if (value.Length > 4096 || !SafeFields.Contains(trimmed[..end]) || end < trimmed.Length && !char.IsWhiteSpace(trimmed[end]) && trimmed[end] != '\\')
                throw new InvalidDataException("Only page, section and date/time fields are supported in Word templates. Remove external-content or other active fields before importing.");
        }
    }

    private static void ValidateArchive(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("The Word template exceeds 32 MB.");
        using var stream = new MemoryStream(bytes, false); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count > 1024) throw new InvalidDataException("The Word template exceeds 1,024 package entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var buffer = new byte[65536]; long total = 0;
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested(); var name = entry.FullName;
            if (name.StartsWith('/') || name.Contains('\\') || name.Contains(':') || name.Split('/').Any(piece => piece is "." or "..") || !names.Add(name))
                throw new InvalidDataException("The Word template contains an unsafe or duplicate package path.");
            if (entry.Length > MaximumBytes || entry.Length > MaximumExpandedBytes - total) throw new InvalidDataException("The Word template exceeds the expanded package size limit.");
            using var data = entry.Open(); var checksum = new Crc32(); long actual = 0; int read;
            while ((read = data.Read(buffer)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested(); actual += read; total += read;
                if (actual > MaximumBytes || total > MaximumExpandedBytes) throw new InvalidDataException("The Word template exceeds the expanded package size limit.");
                checksum.Append(buffer.AsSpan(0, read));
            }
            if (actual != entry.Length || checksum.GetCurrentHashAsUInt32() != entry.Crc32) throw new InvalidDataException("A Word template entry failed its size or checksum check.");
        }
    }
}
