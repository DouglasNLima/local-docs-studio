using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Platform;

public sealed class ImageAssetService
{
    public async Task<string> ImportAsync(string workspaceRoot, string documentPath, byte[] bytes, string suggestedName, CancellationToken cancellationToken = default)
    {
        if (bytes.Length > 32 * 1024 * 1024) throw new IOException("The image exceeds the 32 MB import limit.");
        var encoded = await StaWorker.RunAsync(() =>
        {
            using var input = new MemoryStream(bytes, false);
            var frame = NativeRasterService.Read(input);
            // Re-encode imported raster data as portable PNG without active content.
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
            using var output = new MemoryStream(); encoder.Save(output);
            if (output.Length > 32 * 1024 * 1024) throw new IOException("The converted image exceeds the 32 MB import limit.");
            return output.ToArray();
        }, cancellationToken);
        var workspace = new WorkspacePath(workspaceRoot);
        var baseName = string.Concat(Path.GetFileNameWithoutExtension(suggestedName).Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')).Trim('-');
        if (baseName.Length == 0) baseName = "image";
        baseName = baseName[..Math.Min(baseName.Length, 60)];
        var path = workspace.Resolve($"assets/images/{baseName}-{Guid.NewGuid():N}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WorkspacePath.RejectReparsePoints(path);
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
        {
            await file.WriteAsync(encoded, cancellationToken);
            file.Flush(true);
        }
        return string.Join("/", Path.GetRelativePath(Path.GetDirectoryName(documentPath)!, path).Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    }
}
