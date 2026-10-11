using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Exports;

public sealed record ExportImage(byte[] PngBytes, int PixelWidth, int PixelHeight, string AlternativeText);
public sealed record ExportContent(ParsedDocument Document, IReadOnlyDictionary<int, ExportImage> Diagrams,
    IReadOnlyDictionary<string, ExportImage> Images);
