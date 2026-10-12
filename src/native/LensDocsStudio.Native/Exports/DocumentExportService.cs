using System.Text;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Exports;

public sealed class DocumentExportService
{
    public Task<byte[]> ExportAsync(string format, string source, string? sourcePath, string? workspaceRoot, bool dark, byte[]? template, CancellationToken cancellationToken = default, string diagramTheme = "Auto") =>
        StaWorker.RunAsync(() =>
        {
            var content = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService()).Prepare(source, sourcePath, workspaceRoot, dark, cancellationToken, diagramTheme: diagramTheme);
            cancellationToken.ThrowIfCancellationRequested();
            return format switch
            {
                "DOCX" => new DocxExporter().Export(content, template, cancellationToken),
                "PDF" => new PdfExporter().Export(content, cancellationToken),
                "HTML" => Encoding.UTF8.GetBytes(new HtmlExporter().Export(content, cancellationToken)),
                _ => throw new ArgumentException("Unsupported export format.")
            };
        }, cancellationToken);
}
