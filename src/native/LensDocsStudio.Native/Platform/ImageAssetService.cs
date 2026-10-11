using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Platform;

public sealed class ImageAssetService
{
    public async Task<string> ImportAsync(string workspaceRoot, string documentPath, byte[] bytes, string suggestedName, CancellationToken cancellationToken = default)
    {
        if (bytes.Length > 32 * 1024 * 1024) throw new IOException("The image exceeds the 32 MB import limit.");
        using var input = new MemoryStream(bytes, false);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 64_000_000) throw new IOException("The image exceeds the 64 megapixel limit.");
        // Re-encode imported raster data: persisted bytes are validated, portable PNG with no active content.
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(frame);
        using var output = new MemoryStream(); encoder.Save(output);
        var workspace = new WorkspacePath(workspaceRoot);
        var baseName = string.Concat(Path.GetFileNameWithoutExtension(suggestedName).Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')).Trim('-');
        if (baseName.Length == 0) baseName = "image";
        baseName = baseName[..Math.Min(baseName.Length, 60)];
        var path = workspace.Resolve($"assets/images/{baseName}-{Guid.NewGuid():N}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WorkspacePath.RejectReparsePoints(path);
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
        {
            await file.WriteAsync(output.ToArray(), cancellationToken);
            file.Flush(true);
        }
        return string.Join("/", Path.GetRelativePath(Path.GetDirectoryName(documentPath)!, path).Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    }
}
