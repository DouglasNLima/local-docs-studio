using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Exports;

public sealed class ClipboardExportService
{
    public Task<ExportedClipboard> PrepareAsync(string source, string? sourcePath, string? workspaceRoot, bool dark, CancellationToken cancellationToken = default) =>
        StaWorker.RunAsync(() =>
        {
            if (source.Length > 4 * 1024 * 1024) throw new IOException("Copy rendered content is limited to 4 million source characters. Export larger documents to a file.");
            var content = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService()).Prepare(source, sourcePath, workspaceRoot, dark, cancellationToken, 16 * 1024 * 1024);
            cancellationToken.ThrowIfCancellationRequested();
            return new ClipboardExporter().Export(content);
        }, cancellationToken);
}
