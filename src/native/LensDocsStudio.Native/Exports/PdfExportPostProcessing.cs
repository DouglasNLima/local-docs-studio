using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LensDocsStudio.Core.Exports;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace LensDocsStudio.Native.Exports;

internal static partial class PdfExportPostProcessing
{
    // PDFsharp 6.2.4 writes supplementary code points as five/six hex digits in
    // ToUnicode ranges, instead of the required UTF-16BE surrogate pair.
    // Only process our bounded, freshly generated PDF; this is not a PDF importer.
    public static byte[] Complete(byte[] generated, long maximumBytes, CancellationToken cancellationToken,
        bool correctSupplementary, PdfTextDecorations? decorations)
    {
        using var input = new MemoryStream(generated, writable: false);
        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var corrected = false;
        foreach (var font in correctSupplementary ? pdf.Internals.GetAllObjects().OfType<PdfDictionary>() : [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (font.Elements.GetName("/Type") != "/Font" || font.Elements.GetName("/Subtype") != "/Type0"
                || font.Elements.GetDictionary("/ToUnicode") is not { Stream: { } stream } map) continue;
            var source = Encoding.ASCII.GetString(stream.UnfilteredValue);
            var changed = false;
            var text = SupplementaryRange().Replace(source, match =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (!Rune.IsValid(value) || value <= 0xFFFF)
                    throw new InvalidDataException("PDF export produced an invalid Unicode character mapping.");
                var utf16 = char.ConvertFromUtf32(value);
                changed = true;
                return $"<{match.Groups[1].Value}><{match.Groups[1].Value}><{(int)utf16[0]:X4}{(int)utf16[1]:X4}>";
            });
            if (!changed) continue;
            map.Elements.Remove("/Filter");
            map.Elements.Remove("/DecodeParms");
            stream.Value = Encoding.ASCII.GetBytes(text);
            map.Elements.SetInteger("/Length", stream.Value.Length);
            corrected = true;
        }
        decorations?.Apply(pdf);
        if (!corrected && decorations is null) return generated;
        cancellationToken.ThrowIfCancellationRequested();
        using var output = new BoundedExportStream(maximumBytes, cancellationToken);
        pdf.Save(output, false);
        return output.ToArray();
    }

    [GeneratedRegex(@"^<([0-9A-F]{4})><\1><([0-9A-F]{5,6})>(?=\r?$)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SupplementaryRange();
}
