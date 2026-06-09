using System.Text;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LensDocsStudio.Windows.Services;

public sealed class NativeWorkspaceService
{
    public const int MaxFiles = 500;
    public const int MaxDepth = 12;

    private readonly nint ownerWindowHandle;
    private readonly Dictionary<string, string> nativeWorkspaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkspaceFileHandle> nativeFileHandles = new(StringComparer.Ordinal);
    private readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public NativeWorkspaceService(Window ownerWindow)
    {
        ownerWindowHandle = WindowNative.GetWindowHandle(ownerWindow);
    }

    public async Task<object> OpenFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, ownerWindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return new { cancelled = true };
        }

        var rootPath = Path.GetFullPath(folder.Path);
        var workspaceId = Guid.NewGuid().ToString("N");
        nativeWorkspaces[workspaceId] = rootPath;

        var files = new List<object>();
        var skipped = new List<object>();
        await CollectFilesAsync(rootPath, rootPath, workspaceId, files, skipped, depth: 0);

        return new
        {
            cancelled = false,
            workspaceName = folder.Name,
            nativeWorkspaceId = workspaceId,
            files,
            limits = new
            {
                maxFileSizeBytes = NativeFileService.MaxFileBytes,
                maxFiles = MaxFiles,
                maxDepth = MaxDepth,
            },
            skipped,
        };
    }

    public async Task<object> SaveWorkspaceFileAsync(string? nativeHandleId, string? content)
    {
        if (string.IsNullOrWhiteSpace(nativeHandleId) || !nativeFileHandles.TryGetValue(nativeHandleId, out var handle))
        {
            throw new NativeFileException("The Windows workspace file handle is no longer available.");
        }

        ValidateContent(content);
        var rootPath = nativeWorkspaces[handle.WorkspaceId];
        var fullPath = ResolveInsideRoot(rootPath, handle.RelativePath);
        if (!string.Equals(fullPath, handle.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new NativeFileException("The Windows workspace file handle is no longer valid.");
        }

        await File.WriteAllTextAsync(fullPath, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new
        {
            saved = true,
            name = Path.GetFileName(fullPath),
            path = handle.RelativePath,
            displayPath = handle.RelativePath,
            encoding = "utf-8",
        };
    }

    public async Task<object> CreateWorkspaceFileAsync(string? nativeWorkspaceId, string? path, string? content)
    {
        if (string.IsNullOrWhiteSpace(nativeWorkspaceId) || !nativeWorkspaces.TryGetValue(nativeWorkspaceId, out var rootPath))
        {
            throw new NativeFileException("The Windows workspace handle is no longer available.");
        }

        var relativePath = ValidateRelativeWorkspacePath(path);
        ValidateContent(content);
        var fullPath = ResolveInsideRoot(rootPath, relativePath);
        if (File.Exists(fullPath))
        {
            throw new NativeFileException("A workspace file already exists at that path.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var handleId = CreateFileHandle(nativeWorkspaceId, fullPath, relativePath);
        return new
        {
            cancelled = false,
            created = true,
            name = Path.GetFileName(fullPath),
            path = relativePath,
            displayPath = relativePath,
            extension = Path.GetExtension(fullPath).ToLowerInvariant(),
            encoding = "utf-8",
            content = content ?? string.Empty,
            nativeHandleId = handleId,
        };
    }

    private async Task CollectFilesAsync(
        string rootPath,
        string currentPath,
        string workspaceId,
        List<object> files,
        List<object> skipped,
        int depth)
    {
        if (depth > MaxDepth)
        {
            skipped.Add(new
            {
                path = ToSafeRelativePath(rootPath, currentPath),
                reason = $"Directory exceeds the {MaxDepth} level recursion limit.",
            });
            return;
        }

        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateFileSystemEntries(currentPath).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            skipped.Add(new
            {
                path = ToSafeRelativePath(rootPath, currentPath),
                reason = "Directory could not be read.",
            });
            return;
        }

        foreach (var child in children)
        {
            if (Directory.Exists(child))
            {
                await CollectFilesAsync(rootPath, child, workspaceId, files, skipped, depth + 1);
                continue;
            }

            if (!File.Exists(child) || !NativeFileService.IsSupportedExtension(Path.GetExtension(child)))
            {
                continue;
            }

            var relativePath = ToSafeRelativePath(rootPath, child);
            if (files.Count >= MaxFiles)
            {
                skipped.Add(new
                {
                    path = relativePath,
                    reason = $"Workspace file limit of {MaxFiles} supported files was reached.",
                });
                continue;
            }

            var info = new FileInfo(child);
            if (info.Length > NativeFileService.MaxFileBytes)
            {
                skipped.Add(new
                {
                    path = relativePath,
                    reason = "File exceeds the 5 MB limit.",
                });
                continue;
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(child, strictUtf8);
            }
            catch (DecoderFallbackException)
            {
                skipped.Add(new
                {
                    path = relativePath,
                    reason = "File is not valid UTF-8 text.",
                });
                continue;
            }
            catch
            {
                skipped.Add(new
                {
                    path = relativePath,
                    reason = "File could not be read.",
                });
                continue;
            }

            var handleId = CreateFileHandle(workspaceId, Path.GetFullPath(child), relativePath);
            files.Add(new
            {
                name = Path.GetFileName(child),
                path = relativePath,
                displayPath = relativePath,
                extension = Path.GetExtension(child).ToLowerInvariant(),
                encoding = "utf-8",
                content,
                nativeHandleId = handleId,
            });
        }
    }

    private static void ValidateContent(string? content)
    {
        var byteCount = Encoding.UTF8.GetByteCount(content ?? string.Empty);
        if (byteCount > NativeFileService.MaxFileBytes)
        {
            throw new NativeFileException("Save a UTF-8 text file up to 5 MB.");
        }
    }

    private static string ValidateRelativeWorkspacePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new NativeFileException("Use a relative workspace file path.");
        }

        var normalised = path.Replace('\\', '/').Trim();
        if (normalised.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(normalised)
            || normalised.Contains("//", StringComparison.Ordinal))
        {
            throw new NativeFileException("Use a relative workspace file path.");
        }

        var parts = normalised.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0
            || parts.Any(static part => part is "." or "..")
            || parts.Any(ContainsInvalidPathCharacters))
        {
            throw new NativeFileException("Use a relative workspace file path.");
        }

        var cleanPath = string.Join('/', parts);
        if (!NativeFileService.IsSupportedExtension(Path.GetExtension(cleanPath)))
        {
            throw new NativeFileException("Choose a .md, .markdown, .mmd, .mermaid, or .txt file.");
        }

        return cleanPath;
    }

    private static bool ContainsInvalidPathCharacters(string value)
    {
        return value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
    }

    private static string ResolveInsideRoot(string rootPath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new NativeFileException("Workspace paths must stay inside the selected folder.");
        }

        return fullPath;
    }

    private static string ToSafeRelativePath(string rootPath, string fullPath)
    {
        var relative = Path.GetRelativePath(rootPath, fullPath);
        return relative == "."
            ? string.Empty
            : relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private string CreateFileHandle(string workspaceId, string fullPath, string relativePath)
    {
        var handleId = Guid.NewGuid().ToString("N");
        nativeFileHandles[handleId] = new WorkspaceFileHandle(workspaceId, fullPath, relativePath);
        return handleId;
    }

    private sealed record WorkspaceFileHandle(string WorkspaceId, string FullPath, string RelativePath);
}
