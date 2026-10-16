using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace LensDocsStudio.Core.Imports;

internal static class WordImageDescription
{
    public static string Read(A.Blip image)
    {
        // Metadata belongs to this occurrence, even when several drawings reuse one image part.
        var drawing = image.Ancestors().Where(element => element is DW.Inline or DW.Anchor)
            .Select(element => element.GetFirstChild<DW.DocProperties>()).FirstOrDefault(properties => properties is not null);
        var picture = image.Ancestors<PIC.Picture>().FirstOrDefault()?.NonVisualPictureProperties?.NonVisualDrawingProperties;
        // An explicitly empty description is meaningful; only an absent value falls back.
        var description = drawing?.Description?.Value ?? picture?.Description?.Value
            ?? drawing?.Title?.Value ?? picture?.Title?.Value ?? "Imported image";
        return description.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
    }
}
