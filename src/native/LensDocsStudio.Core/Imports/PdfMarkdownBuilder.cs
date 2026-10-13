using System.Text;

namespace LensDocsStudio.Core.Imports;

/// <summary>Bounds application-retained PDF Markdown before buffer growth, including UTF-8 output bytes.</summary>
internal sealed class PdfMarkdownBuilder(PdfImportLimits limits, CancellationToken cancellationToken)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly StringBuilder output = new();
    private long bytes;
    public int RemainingCharacters => limits.MaximumTextCharacters - output.Length;

    public void Append(string value)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.Length > RemainingCharacters) throw new InvalidDataException("The PDF document exceeds the imported text limit.");
        var addedBytes = Utf8.GetByteCount(value);
        if (bytes + addedBytes > limits.MaximumOutputBytes) throw new InvalidDataException("The PDF document exceeds the imported UTF-8 byte limit.");
        cancellationToken.ThrowIfCancellationRequested();
        output.Append(value); bytes += addedBytes;
    }

    public override string ToString()
    {
        cancellationToken.ThrowIfCancellationRequested();
        return output.ToString();
    }
}
