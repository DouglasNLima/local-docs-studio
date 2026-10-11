using System.Text.Json;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativePreviewChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "preview-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "document.md"); var a = Path.Combine(root, "a.png"); var b = Path.Combine(root, "b.png");
        void Write(string path, byte colour)
        {
            var pixels = new byte[64 * 48 * 4];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = colour; pixels[i + 3] = 255; }
            var image = BitmapSource.Create(64, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(path); encoder.Save(file);
        }
        Write(a, 10); Write(b, 40);
        var parser = new MarkdownService(); var document = parser.Parse("# Prepared\n\n![A](a.png) ![Alias](./a.png) ![B](b.png)\n");
        var preparation = new PreviewImageService();
        var first = StaWorker.RunAsync(() => preparation.Prepare(document, source, root)).GetAwaiter().GetResult();
        if (first.Count != 3 || first.Values.Any(v => v.Pixels is null || !v.Pixels.IsFrozen)) throw new IOException("Background preview did not prepare frozen raster sources.");
        if (!ReferenceEquals(first["a.png"].Pixels, first["./a.png"].Pixels)) throw new IOException("Repeated raster content consumed duplicate preview pixels.");
        var second = StaWorker.RunAsync(() => preparation.Prepare(document, source, root)).GetAwaiter().GetResult();
        if (!ReferenceEquals(first["a.png"].Pixels, second["a.png"].Pixels)) throw new IOException("Unchanged preview content was decoded again.");
        var timestamp = File.GetLastWriteTimeUtc(a); Write(a, 20); File.SetLastWriteTimeUtc(a, timestamp);
        var changed = StaWorker.RunAsync(() => preparation.Prepare(document, source, root)).GetAwaiter().GetResult();
        if (ReferenceEquals(first["a.png"].Pixels, changed["a.png"].Pixels)) throw new IOException("An image change with a preserved timestamp reused stale preview pixels.");
        var bounded = new PreviewImageService(maximumDecodedBytes: 64 * 48 * 4, maximumCacheBytes: 0);
        var limited = StaWorker.RunAsync(() => bounded.Prepare(document, source, root)).GetAwaiter().GetResult();
        if (limited["a.png"].Pixels is null || limited["./a.png"].Pixels is null || limited["b.png"].Pixels is not null || limited["b.png"].Error is null)
            throw new IOException("Aggregate preview memory limits did not preserve the first image and its alias while rejecting extra pixels.");
        var cachedLimit = new PreviewImageService(maximumDecodedBytes: 64 * 48 * 4);
        StaWorker.RunAsync(() => cachedLimit.Prepare(parser.Parse("![B](b.png)"), source, root)).GetAwaiter().GetResult();
        var cacheBounded = StaWorker.RunAsync(() => cachedLimit.Prepare(document, source, root)).GetAwaiter().GetResult();
        if (cacheBounded["b.png"].Pixels is not null) throw new IOException("A cache hit bypassed the preview memory budget.");
        var evicting = new PreviewImageService(maximumCacheBytes: 64 * 48 * 4);
        var firstA = StaWorker.RunAsync(() => evicting.Prepare(parser.Parse("![A](a.png)"), source, root)).GetAwaiter().GetResult()["a.png"].Pixels;
        StaWorker.RunAsync(() => evicting.Prepare(parser.Parse("![B](b.png)"), source, root)).GetAwaiter().GetResult();
        var reloadedA = StaWorker.RunAsync(() => evicting.Prepare(parser.Parse("![A](a.png)"), source, root)).GetAwaiter().GetResult()["a.png"].Pixels;
        if (firstA is null || reloadedA is null || ReferenceEquals(firstA, reloadedA)) throw new IOException("The preview cache retained pixels beyond its configured budget.");
        var rejected = StaWorker.RunAsync(() => preparation.Prepare(parser.Parse("![Remote](https://example.invalid/image.png) ![Missing](missing.png)"), source, root)).GetAwaiter().GetResult();
        if (rejected.Values.Any(v => v.Pixels is not null || v.Error is null)) throw new IOException("Unresolved preview resources were not reported.");
        var cancelled = false;
        try { preparation.Prepare(document, source, root, new(true)); } catch (OperationCanceledException) { cancelled = true; }
        if (!cancelled) throw new IOException("Preview image preparation ignored cancellation.");
        File.Delete(a); File.Delete(b);
        var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(document, false,
            new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, preparedImages: first);
        if (rendered.Warnings.Count != 0 || !new TextRange(rendered.Document.ContentStart, rendered.Document.ContentEnd).Text.Contains("Prepared", StringComparison.Ordinal))
            throw new IOException("The native renderer attempted to reopen prepared image files.");
        File.WriteAllText(Path.Combine(output, "native-preview.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "STA preparation returns frozen pixels for native rendering", "Content caching reuses aliases and detects changes despite preserved timestamps",
                "Aggregate decoded memory limits apply to fresh images and cache hits", "Unavailable and remote images produce local placeholders without retrieval",
                "Cancelled preparation stops before decoding", "Native layout consumes prepared images after the source files are removed" },
            FixtureRoot = root
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
