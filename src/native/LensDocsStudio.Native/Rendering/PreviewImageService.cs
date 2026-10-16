using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Native.Rendering;

public sealed record PreviewImage(BitmapSource? Pixels, string? Error);

/// <summary>Prepares frozen raster sources on the preview STA worker, with bounded retained pixels.</summary>
public sealed class PreviewImageService(long maximumDecodedBytes = 256L * 1024 * 1024, long maximumCacheBytes = 128L * 1024 * 1024)
{
    private sealed record CacheEntry(string Hash, BitmapSource Pixels, long Bytes);
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> cache = new(StringComparer.Ordinal);
    private readonly LinkedList<CacheEntry> recency = new();
    private long cacheBytes;

    // The owning window serialises preparations. Each source is immutable before crossing to the UI thread.
    public IReadOnlyDictionary<string, PreviewImage> Prepare(ParsedDocument document, string? sourcePath, string? workspaceRoot, CancellationToken cancellationToken = default)
    {
        var images = new Dictionary<string, PreviewImage>(StringComparer.Ordinal);
        var prepared = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        long decodedBytes = 0; long sourceBytes = 0;
        foreach (var link in document.Syntax.Descendants<LinkInline>().Where(link => link.IsImage))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = link.Url ?? "";
            if (images.ContainsKey(reference)) continue;
            try
            {
                if (images.Count >= 512) throw new IOException("The preview image limit was reached. Source and exports remain available.");
                if (sourcePath is null || workspaceRoot is null) throw new IOException("Save the document inside a workspace to resolve images.");
                var path = new WorkspacePath(workspaceRoot).ResolveLink(sourcePath, reference);
                if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase)) throw new IOException("User-supplied SVG images are not displayed.");
                using var input = File.OpenRead(path);
                if (input.Length > 32 * 1024 * 1024 || (sourceBytes += input.Length) > 256L * 1024 * 1024) throw new IOException("Preview image files exceed the size budget.");
                var hash = Convert.ToHexString(SHA256.HashData(input));
                cancellationToken.ThrowIfCancellationRequested();
                BitmapSource pixels;
                if (prepared.TryGetValue(hash, out var alreadyPrepared)) pixels = alreadyPrepared;
                else if (cache.TryGetValue(hash, out var cached))
                {
                    pixels = cached.Value.Pixels;
                    recency.Remove(cached); recency.AddLast(cached);
                }
                else
                {
                    input.Position = 0;
                    pixels = NativeRasterService.Read(input, maximumDecodedBytes - decodedBytes);
                    cancellationToken.ThrowIfCancellationRequested();
                    Retain(hash, pixels);
                }
                if (!prepared.ContainsKey(hash))
                {
                    var size = NativeRasterService.DecodedBytes(pixels);
                    if (decodedBytes + size > maximumDecodedBytes) throw new IOException("Preview images exceed the decoded pixel memory budget. Source and exports remain available.");
                    decodedBytes += size; prepared.Add(hash, pixels);
                }
                images[reference] = new(pixels, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
            { images[reference] = new(null, ex.Message); }
        }
        return images;
    }
    private void Retain(string hash, BitmapSource pixels)
    {
        var bytes = NativeRasterService.DecodedBytes(pixels);
        if (bytes > maximumCacheBytes) return;
        while ((cacheBytes + bytes > maximumCacheBytes || cache.Count >= 256) && recency.First is { } oldest)
        {
            cache.Remove(oldest.Value.Hash); recency.RemoveFirst(); cacheBytes -= oldest.Value.Bytes;
        }
        var entry = new LinkedListNode<CacheEntry>(new(hash, pixels, bytes));
        cache.Add(hash, entry); recency.AddLast(entry); cacheBytes += bytes;
    }
}
