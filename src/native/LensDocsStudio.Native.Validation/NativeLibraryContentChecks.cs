using System.Text.Json;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Library;
using LensDocsStudio.Native.Platform;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Native.Validation;

internal static class NativeLibraryContentChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "library-content-" + Guid.NewGuid().ToString("N"));
        var originalRoot = Path.Combine(root, "source"); var copiedRoot = Path.Combine(root, "draft");
        Directory.CreateDirectory(originalRoot); Directory.CreateDirectory(copiedRoot);
        var imagePath = Path.Combine(originalRoot, "logo.png"); var sourcePath = Path.Combine(originalRoot, "source.md");
        var originalPixels = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
        File.WriteAllBytes(imagePath, originalPixels);
        const string diagram = "```mermaid\r\nflowchart LR\r\nA[Editable] --> B[Diagram]\r\n```";
        var source = "# Café library\r\n\r\n" + diagram + "\r\n\r\n![Logo](<logo.png> \"Retained title\")\r\n\r\n"
            + "<figure data-image-figure><img src=\"logo.png\" alt=\"Same logo\"><figcaption>Café caption</figcaption></figure>\r\n\r\n"
            + "[Original link](guide.md)\r\n\r\n![Unfetched](https://example.invalid/not-requested.png)\r\n";
        File.WriteAllText(sourcePath, source);
        var service = new LibraryContentService(); var portable = service.CaptureAsync(source, sourcePath, originalRoot).GetAwaiter().GetResult();
        if (!portable.Contains(diagram, StringComparison.Ordinal) || !portable.Contains("data:image/png;base64,", StringComparison.Ordinal)
            || !portable.Contains("[Original link](guide.md)", StringComparison.Ordinal) || !portable.Contains("https://example.invalid/not-requested.png", StringComparison.Ordinal)
            || File.ReadAllText(sourcePath) != source || !File.ReadAllBytes(imagePath).SequenceEqual(originalPixels))
            throw new IOException("Library capture changed diagram/link source or original files, or failed to embed local pixels.");
        File.Delete(imagePath);
        var materialised = service.MaterialiseAsync(portable, Path.Combine(copiedRoot, "draft.md"), copiedRoot).GetAwaiter().GetResult();
        var parsed = new MarkdownService().Parse(materialised);
        var references = parsed.Syntax.Descendants<LinkInline>().Where(link => link.IsImage && !(link.Url ?? "").StartsWith("https:", StringComparison.Ordinal)).Select(link => link.Url!).ToArray();
        if (references.Length != 2 || references.Distinct().Count() != 1 || materialised.Contains("data:image/", StringComparison.Ordinal)
            || !materialised.Contains(diagram, StringComparison.Ordinal) || parsed.Syntax.OfType<DocumentImageFigureBlock>().Single().Caption != "Café caption")
            throw new IOException("Materialising library images changed diagram/caption content or failed to reuse the image asset.");
        using var original = new MemoryStream(originalPixels); using var copied = File.OpenRead(new WorkspacePath(copiedRoot).ResolveLink(Path.Combine(copiedRoot, "draft.md"), references[0]));
        var expected = NativeRasterService.Read(original); var actual = NativeRasterService.Read(copied);
        var expectedBytes = new byte[(expected.PixelWidth * expected.Format.BitsPerPixel + 7) / 8 * expected.PixelHeight];
        var actualBytes = new byte[(actual.PixelWidth * actual.Format.BitsPerPixel + 7) / 8 * actual.PixelHeight];
        expected.CopyPixels(expectedBytes, expectedBytes.Length / expected.PixelHeight, 0); actual.CopyPixels(actualBytes, actualBytes.Length / actual.PixelHeight, 0);
        if (expected.PixelWidth != actual.PixelWidth || expected.PixelHeight != actual.PixelHeight || !expectedBytes.SequenceEqual(actualBytes))
            throw new IOException("The library's materialised pixels differ from its source image.");
        var invalidDiagram = "```mermaid\nthis is deliberately incomplete {{{\n```\n";
        if (service.CaptureAsync(invalidDiagram, null, null).GetAwaiter().GetResult() != invalidDiagram)
            throw new IOException("Library capture requires diagram rendering or changes editable source.");
        try { service.CaptureAsync("![Missing](missing.png)", sourcePath, originalRoot).GetAwaiter().GetResult(); throw new InvalidOperationException("A missing source image was silently omitted."); }
        catch (IOException) { }
        try { service.CaptureAsync(new string('é', 524289), null, null).GetAwaiter().GetResult(); throw new InvalidOperationException("An oversized library item was accepted."); }
        catch (IOException) { }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { service.MaterialiseAsync(portable, Path.Combine(copiedRoot, "cancelled.md"), copiedRoot, cancellation.Token).GetAwaiter().GetResult(); throw new InvalidOperationException("Cancelled library preparation completed."); }
        catch (OperationCanceledException) { }
        File.WriteAllText(Path.Combine(output, "native-library-content.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Portable library Markdown embeds real local images and retains editable Mermaid, Unicode, CRLF, captions and links without changing originals",
                "Materialisation creates independent validated pixels after the original image is removed; repeated references share one asset",
                "Invalid diagrams remain editable source; remote images are not fetched", "Missing images, UTF-8 size limits and cancellation fail without source changes" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
