using System.Text;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Library;

/// <summary>Portable library text with embedded raster images and unchanged editable diagram source.</summary>
public sealed class LibraryContentService
{
    public Task<string> CaptureAsync(string source, string? sourcePath, string? workspace, CancellationToken cancellationToken = default) =>
        StaWorker.RunAsync(() =>
        {
            CheckSize(source);
            var prepared = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService())
                .Prepare(source, sourcePath, workspace, false, cancellationToken, 768 * 1024, markdownSourceOnly: true, prepareDiagrams: false);
            var portable = new MarkdownClipboardExporter().Export(prepared, cancellationToken).PlainText;
            CheckSize(portable); return portable;
        }, cancellationToken);

    public async Task<string> MaterialiseAsync(string source, string targetDocument, string targetWorkspace, CancellationToken cancellationToken = default)
    {
        CheckSize(source);
        var imported = await Task.Run(() => new MarkdownImageImporter().Import(source, cancellationToken), cancellationToken);
        var text = imported.Markdown; var images = new ImageAssetService();
        foreach (var asset in imported.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = await images.ImportAsync(targetWorkspace, targetDocument, asset.Bytes, "library-image", cancellationToken);
            text = text.Replace(asset.Reference, reference, StringComparison.Ordinal);
        }
        cancellationToken.ThrowIfCancellationRequested(); return text;
    }
    private static void CheckSize(string source)
    {
        if (Encoding.UTF8.GetByteCount(source) > LocalLibraryFormat.MaximumContentBytes)
            throw new IOException("A reusable template or snippet supports up to 1 MB of UTF-8 text, including embedded images.");
    }
}
