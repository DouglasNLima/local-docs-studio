using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Exports;

public sealed record ExportImage(byte[] PngBytes, int PixelWidth, int PixelHeight, string AlternativeText, double DisplayWidth = 0, double DisplayHeight = 0);
public sealed record ExportContent(ParsedDocument Document, IReadOnlyDictionary<int, ExportImage> Diagrams,
    IReadOnlyDictionary<string, ExportImage> Images)
{
    public IReadOnlyDictionary<int, ExportImage> Formulae { get; init; } = new Dictionary<int, ExportImage>();
    public PreparedCodeSyntax? CodeSyntax { get; init; }
    public ExportAllocationLimits AllocationLimits { get; init; } = ExportAllocationLimits.Default;
}
