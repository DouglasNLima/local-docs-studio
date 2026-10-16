using System.Buffers;
using System.Text;
using System.Windows.Media;
using MigraDoc.DocumentObjectModel;

namespace LensDocsStudio.Native.Exports;

/// <summary>Uses installed Windows fonts for selectable PDF text without replacing missing characters.</summary>
internal sealed class PdfFontFallback(CancellationToken cancellationToken)
{
    private static readonly string[] Families =
        ["Segoe UI", "Segoe UI Emoji", "Segoe UI Symbol", "Microsoft YaHei UI", "Yu Gothic UI", "Malgun Gothic", "Arial"];
    private readonly Dictionary<string, GlyphTypeface?> faces = new(StringComparer.Ordinal);
    private int runs;
    public bool HasSupplementaryCharacters { get; private set; }

    public void AddText(ParagraphElements elements, string text, string preferred = "Segoe UI")
    {
        var start = 0;
        string? selected = null;
        for (var position = 0; position < text.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(text.AsSpan(position), out var rune, out var consumed) != OperationStatus.Done)
                throw new InvalidDataException("PDF export found incomplete Unicode text. Correct the text or use Markdown, HTML or Word export.");
            HasSupplementaryCharacters |= rune.Value > 0xFFFF;
            var family = Resolve(rune.Value, preferred);
            if (selected is not null && family != selected)
            {
                Append(text[start..position], selected);
                start = position;
            }
            selected = family;
            position += consumed;
        }
        if (selected is not null) Append(text[start..], selected);

        void Append(string value, string family)
        {
            if (++runs > 65_536)
                throw new InvalidDataException("PDF export needs too many separate text runs. Export a smaller section or use Markdown, HTML or Word.");
            if (family == preferred) elements.AddText(value);
            else elements.AddFormattedText(value).Font.Name = family;
        }
    }

    private string Resolve(int codePoint, string preferred)
    {
        // MigraDoc handles these layout characters itself; they do not require visible glyphs.
        if (codePoint is '\r' or '\n' or '\t') return preferred;
        if (Supports(preferred, codePoint)) return preferred;
        foreach (var family in Families)
            if (family != preferred && Supports(family, codePoint)) return family;
        throw MissingGlyph(codePoint);
    }

    private bool Supports(string family, int codePoint)
    {
        if (!faces.TryGetValue(family, out var face))
        {
            new Typeface(family).TryGetGlyphTypeface(out face);
            // Some installed collection fonts are readable by WPF but unavailable to PDFsharp.
            // Only choose families that both renderers can resolve.
            if (face is not null)
            {
                try { _ = new PdfSharp.Drawing.XFont(family, 10); }
                catch (InvalidOperationException) { face = null; }
            }
            faces.Add(family, face);
        }
        return face is not null && face.CharacterToGlyphMap.TryGetValue(codePoint, out var index) && index != 0;
    }

    public static InvalidDataException MissingGlyph(int codePoint) =>
        new($"PDF export cannot render character U+{codePoint:X4} with the supported Windows fonts. Install an appropriate Windows language font, or use Markdown, HTML or Word export.");
}
