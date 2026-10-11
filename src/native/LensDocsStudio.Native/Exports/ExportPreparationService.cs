using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Native.Exports;

public sealed class ExportPreparationService(MarkdownService markdown, DiagramService diagrams, NativeSvgService svg)
{
    public ExportContent Prepare(string source, string? sourcePath, string? root, bool dark, CancellationToken cancellationToken = default,
        long maximumEmbeddedImageBytes = 256 * 1024 * 1024, bool markdownSourceOnly = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = markdown.Parse(source, Path.GetExtension(sourcePath)?.ToLowerInvariant() is ".mmd" or ".mermaid");
        var diagramImages = new Dictionary<int, ExportImage>();
        long imageBytes = 0;
        void CountImage(byte[] bytes)
        {
            imageBytes += bytes.Length;
            if (imageBytes > maximumEmbeddedImageBytes) throw new IOException("Prepared images exceed the output size limit. Export fewer or smaller images.");
        }
        foreach (var code in document.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info is "mermaid" or "mmd"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (markdownSourceOnly && code.ClosingFencedCharCount == 0) continue;
            var result = diagrams.Render(code.Lines.ToString(), dark);
            if (!result.Success) throw new InvalidDataException(result.Error);
            var drawing = svg.Render(result.Svg!);
            var bytes = NativeSvgService.ToPng(drawing);
            CountImage(bytes);
            diagramImages[code.Span.Start] = new(bytes, (int)Math.Ceiling(drawing.Width * 2), (int)Math.Ceiling(drawing.Height * 2), "Mermaid diagram");
        }
        var images = new Dictionary<string, ExportImage>();
        foreach (var link in document.Syntax.Descendants<LinkInline>().Where(l => l.IsImage))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (images.ContainsKey(link.Url ?? "")) continue;
            Stream OpenImage()
            {
                if ((link.Url ?? "").StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return new MemoryStream(LensDocsStudio.Core.Imports.EmbeddedImageData.Decode(link.Url!));
                if (sourcePath is null || root is null) throw new IOException("Save the document inside a workspace before exporting local images.");
                var path = new WorkspacePath(root).ResolveLink(sourcePath, link.Url ?? "");
                if (new FileInfo(path).Length > 32 * 1024 * 1024 || Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("An image cannot be safely exported: " + link.Url);
                return File.OpenRead(path);
            }
            if (markdownSourceOnly && (link.Url ?? "").StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(link.Url, UriKind.Absolute, out var remote) && remote.Scheme is "http" or "https") continue;
            using var input = OpenImage();
            var bitmap = LensDocsStudio.Native.Platform.NativeRasterService.Read(input);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var bytes = new MemoryStream(); encoder.Save(bytes);
            var png = bytes.ToArray(); CountImage(png);
            images[link.Url ?? ""] = new(png, bitmap.PixelWidth, bitmap.PixelHeight, MarkdownService.InlineText(link));
        }
        return new(document, diagramImages, images);
    }
}
