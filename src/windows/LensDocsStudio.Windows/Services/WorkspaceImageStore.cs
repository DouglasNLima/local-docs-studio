using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace LensDocsStudio.Windows.Services;

// This store receives only the root authorised by NativeWorkspaceService. It
// never grants a path capability to document HTML or exposes general file I/O.
public static class WorkspaceImageStore
{
    public const int MaxBytes = 20 * 1024 * 1024;
    public const int MaxImages = 500;
    private const uint ReadAttributes = 0x80;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareRead = 1;
    private const uint OpenExisting = 3;
    private const uint CreateNew = 1;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint BackupSemantics = 0x02000000;
    private const uint ReparsePoint = 0x400;

    public static object List(string root)
    {
        var images = new List<object>();
        var skipped = new List<string>();
        using var pins = PinDirectories(root, root);
        void Walk(string directory, int depth)
        {
            if (depth > 12 || images.Count >= MaxImages) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (images.Count >= MaxImages) break;
                try
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { skipped.Add(Path.GetRelativePath(root, entry)); continue; }
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        using var childPins = PinDirectories(root, entry);
                        Walk(entry, depth + 1);
                    }
                    else if (IsImagePath(entry))
                    {
                        var info = new FileInfo(entry);
                        images.Add(new { path = Path.GetRelativePath(root, entry).Replace('\\', '/'), size = info.Length });
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NativeFileException) { skipped.Add(Path.GetRelativePath(root, entry)); }
            }
        }
        Walk(root, 0);
        return new { images, skipped, maxImages = MaxImages, maxBytes = MaxBytes };
    }

    public static string DocumentPath(string root, string fullPath)
    {
        if (!InsideRoot(root, fullPath) || !NativeFileService.IsSupportedExtension(Path.GetExtension(fullPath)))
            throw new NativeFileException("Select the folder containing the opened Markdown document.");
        using var pins = PinDirectories(root, Path.GetDirectoryName(fullPath)!);
        using var handle = Open(fullPath, ReadAttributes, OpenExisting, OpenReparsePoint);
        ValidateHandle(handle, fullPath, root);
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    public static async Task<object> ReadAsync(string root, string? input, bool absoluteInput = false)
    {
        var relative = ValidatePath(root, input, absoluteInput);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        using var pins = PinDirectories(root, Path.GetDirectoryName(full)!);
        using var handle = Open(full, GenericRead, OpenExisting, OpenReparsePoint);
        ValidateHandle(handle, full, root);
        using var stream = new FileStream(handle, FileAccess.Read);
        if (stream.Length == 0 || stream.Length > MaxBytes) throw new NativeFileException("Choose an image up to 20 MB.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes);
        var mime = await ValidateBytesAsync(bytes, relative, null);
        return new { path = relative.Replace('\\', '/'), base64 = Convert.ToBase64String(bytes), mimeType = mime, size = bytes.Length };
    }

    public static async Task<object> CreateAsync(string root, string? name, string? base64, string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || name.Contains('/') || name.Contains('\\')) throw new NativeFileException("Use a safe image filename.");
        _ = ValidatePath(root, name, false);
        if (string.IsNullOrWhiteSpace(base64) || base64.Length > ((MaxBytes + 2) / 3) * 4) throw new NativeFileException("Choose an image up to 20 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { throw new NativeFileException("Invalid image payload."); }
        var mime = await ValidateBytesAsync(bytes, name, mimeType);
        using var rootPins = PinDirectories(root, root);
        var assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(assets);
        using var assetPins = PinDirectories(root, assets);
        var directory = Path.Combine(assets, "images");
        Directory.CreateDirectory(directory);
        using var directoryPins = PinDirectories(root, directory);
        for (var number = 1; number <= 10000; number++)
        {
            var candidate = number == 1 ? name : $"{Path.GetFileNameWithoutExtension(name)}-{number}{Path.GetExtension(name)}";
            var full = Path.Combine(directory, candidate);
            using var handle = CreateFile(full, GenericRead | GenericWrite, ShareRead, 0, CreateNew, OpenReparsePoint, 0);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                if (error is 80 or 183) continue; // CREATE_NEW never truncates an existing file, including empty files.
                throw new NativeFileException("Image could not be saved in the workspace. Check folder access and retry.");
            }
            ValidateHandle(handle, full, root);
            using var stream = new FileStream(handle, FileAccess.ReadWrite);
            await stream.WriteAsync(bytes);
            stream.Flush(flushToDisk: true);
            stream.Position = 0;
            var verified = new byte[bytes.Length];
            await stream.ReadExactlyAsync(verified);
            if (!bytes.AsSpan().SequenceEqual(verified)) throw new NativeFileException("The saved image bytes could not be verified.");
            return new { created = true, path = $"assets/images/{candidate}", base64 = Convert.ToBase64String(verified), mimeType = mime, size = verified.Length };
        }
        throw new NativeFileException("Could not choose a free image filename.");
    }

    private static string ValidatePath(string root, string? input, bool absoluteInput)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 1024 || input.Any(char.IsControl)) throw new NativeFileException("Invalid workspace image path.");
        var path = input.Replace('/', '\\');
        if (Path.IsPathRooted(path))
        {
            if (!absoluteInput || path.StartsWith("\\") || path.StartsWith("//")) throw new NativeFileException("Absolute image paths require an explicit workspace selection.");
            var full = Path.GetFullPath(path);
            if (!InsideRoot(root, full)) throw new NativeFileException("The image is outside the authorised workspace. Import a copy explicitly.");
            path = Path.GetRelativePath(root, full);
        }
        var parts = path.Split('\\');
        if (parts.Length > 13 || parts.Any(part => string.IsNullOrWhiteSpace(part) || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
            || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || IsReservedName(part))) throw new NativeFileException("Image paths must stay inside the authorised workspace.");
        if (!IsImagePath(path)) throw new NativeFileException("Use PNG, JPEG, GIF, or WebP images. SVG images are not imported.");
        if (!InsideRoot(root, Path.GetFullPath(Path.Combine(root, path)))) throw new NativeFileException("Image paths must stay inside the authorised workspace.");
        return path;
    }

    private static bool IsReservedName(string part)
    {
        var stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || System.Text.RegularExpressions.Regex.IsMatch(stem, "^(COM|LPT)[1-9]$");
    }

    public static bool IsImagePath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";

    private static async Task<string> ValidateBytesAsync(byte[] bytes, string path, string? declaredMime)
    {
        if (bytes.Length < 12 || bytes.Length > MaxBytes) throw new NativeFileException("Choose an image up to 20 MB.");
        var mime = bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg"
            : System.Text.Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a" ? "image/gif"
            : System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? "image/webp" : "";
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var expected = extension is ".jpg" or ".jpeg" ? "image/jpeg" : $"image/{extension.TrimStart('.')}";
        if (mime.Length == 0 || mime != expected || (!string.IsNullOrEmpty(declaredMime) && declaredMime != mime)) throw new NativeFileException("Image extension, MIME type and bytes must match.");
        try
        {
            using var memory = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memory.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
            var decoder = await BitmapDecoder.CreateAsync(memory);
            if ((ulong)decoder.PixelWidth * decoder.PixelHeight > 40_000_000) throw new NativeFileException("Choose an image up to 40 million pixels.");
            _ = await decoder.GetPixelDataAsync();
        }
        catch (NativeFileException) { throw; }
        catch { throw new NativeFileException("The image bytes could not be decoded safely."); }
        return mime;
    }

    // Pin every directory with no write/delete sharing for the operation's
    // lifetime. Reject reparse points on the handles themselves, then confirm
    // their final physical paths. Textual prefix checks alone are insufficient.
    private static DirectoryPins PinDirectories(string root, string destination)
    {
        var pins = new DirectoryPins();
        try
        {
            var fullRoot = Path.GetFullPath(root);
            if (fullRoot.StartsWith("\\") || !InsideRoot(fullRoot, destination, allowRoot: true)) throw new NativeFileException("Unsupported workspace root.");
            var current = Path.GetPathRoot(destination)!;
            foreach (var part in destination[current.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                var handle = Open(current, ReadAttributes, OpenExisting, BackupSemantics | OpenReparsePoint);
                pins.Handles.Add(handle);
                ValidateHandle(handle, current, Path.GetPathRoot(current)!);
            }
            return pins;
        }
        catch { pins.Dispose(); throw; }
    }

    private static void ValidateHandle(SafeFileHandle handle, string expected, string root)
    {
        if (!GetFileInformationByHandle(handle, out var information) || (information.Attributes & ReparsePoint) != 0) throw new NativeFileException("Reparse points, junctions and symlinks are not allowed for workspace images.");
        var buffer = new System.Text.StringBuilder(32768);
        var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        var final = buffer.ToString();
        if (length == 0 || length >= buffer.Capacity || !final.StartsWith("\\\\?\\")) throw new NativeFileException("Image containment could not be verified.");
        final = final[4..];
        if (!string.Equals(final, Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase) || !InsideRoot(root, final, true)) throw new NativeFileException("Image containment could not be verified.");
    }

    private static bool InsideRoot(string root, string full, bool allowRoot = false) => full.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
        || allowRoot && string.Equals(full.TrimEnd('\\'), Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static SafeFileHandle Open(string path, uint access, uint disposition, uint flags)
    {
        var handle = CreateFile(path, access, ShareRead, 0, disposition, flags, 0);
        if (handle.IsInvalid) { handle.Dispose(); throw new NativeFileException("Workspace image access failed. Check that the file exists and folder access is allowed."); }
        return handle;
    }

    private sealed class DirectoryPins : IDisposable
    {
        public List<SafeFileHandle> Handles { get; } = [];
        public void Dispose() { foreach (var handle in Handles.AsEnumerable().Reverse()) handle.Dispose(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, System.Text.StringBuilder path, uint length, uint flags);
}
