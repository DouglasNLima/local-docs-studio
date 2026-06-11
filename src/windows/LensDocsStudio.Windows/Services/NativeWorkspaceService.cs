using System.Text;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LensDocsStudio.Windows.Services;

public sealed class NativeWorkspaceService
{
    public const int MaxFiles = 500;
    public const int MaxDepth = 12;
    private static readonly TimeSpan WatcherDebounceInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HostWriteSuppressionWindow = TimeSpan.FromSeconds(2);

    private readonly Window ownerWindow;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly Dictionary<string, string> nativeWorkspaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkspaceFileHandle> nativeFileHandles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> nativeFileHandlesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> recentHostWrites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingWorkspaceChange> pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object watcherLock = new();
    private readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private FileSystemWatcher? workspaceWatcher;
    private Timer? watcherDebounceTimer;
    private string activeWorkspaceId = string.Empty;
    private string activeWorkspaceRoot = string.Empty;

    public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    public NativeWorkspaceService(Window ownerWindow)
    {
        this.ownerWindow = ownerWindow;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task<object> OpenFolderAsync()
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            return await EnqueueOpenFolderAsync();
        }

        return await OpenFolderOnUiThreadAsync();
    }

    private async Task<object> EnqueueOpenFolderAsync()
    {
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                completion.SetResult(await OpenFolderOnUiThreadAsync());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }))
        {
            throw new NativeFileException("Windows folder picker could not open safely.");
        }

        return await completion.Task;
    }

    private async Task<object> OpenFolderOnUiThreadAsync()
    {
        var ownerWindowHandle = WindowNative.GetWindowHandle(ownerWindow);
        if (ownerWindowHandle == 0)
        {
            throw new NativeFileException("Windows folder picker could not attach to the app window.");
        }

        ownerWindow.Activate();
        NativeWindowInterop.RestoreAndForeground(ownerWindowHandle);

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");

        try
        {
            InitializeWithWindow.Initialize(picker, ownerWindowHandle);
        }
        catch
        {
            throw new NativeFileException("Windows folder picker could not attach to the app window.");
        }

        global::Windows.Storage.StorageFolder? folder;
        try
        {
            folder = await picker.PickSingleFolderAsync();
        }
        catch
        {
            throw new NativeFileException("Windows folder picker could not open safely.");
        }

        if (folder is null)
        {
            return new { cancelled = true };
        }

        return await OpenFolderPathAsync(folder.Path, folder.Name);
    }

    public async Task<object> OpenFolderPathAsync(string path, string? workspaceName = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new NativeFileException("The selected folder is unavailable.");
        }

        var rootPath = Path.GetFullPath(path);
        var workspaceId = Guid.NewGuid().ToString("N");
        StopWatching();
        nativeWorkspaces[workspaceId] = rootPath;

        var files = new List<object>();
        var skipped = new List<object>();
        await CollectFilesAsync(rootPath, rootPath, workspaceId, files, skipped, depth: 0);
        StartWatching(workspaceId, rootPath);

        return new
        {
            cancelled = false,
            workspaceName = string.IsNullOrWhiteSpace(workspaceName) ? Path.GetFileName(rootPath) : workspaceName,
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
        TrackHostWrite(handle.RelativePath);
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
        TrackHostWrite(relativePath);
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

    public async Task<object> RefreshWorkspaceFileAsync(string? nativeWorkspaceId, string? nativeHandleId, string? path)
    {
        if (string.IsNullOrWhiteSpace(nativeWorkspaceId) || !nativeWorkspaces.TryGetValue(nativeWorkspaceId, out var rootPath))
        {
            throw new NativeFileException("The Windows workspace handle is no longer available.");
        }

        WorkspaceFileHandle? handle = null;
        string relativePath;
        if (!string.IsNullOrWhiteSpace(nativeHandleId))
        {
            if (!nativeFileHandles.TryGetValue(nativeHandleId, out handle) || handle.WorkspaceId != nativeWorkspaceId)
            {
                throw new NativeFileException("The Windows workspace file handle is no longer available.");
            }

            relativePath = handle.RelativePath;
        }
        else
        {
            relativePath = ValidateRelativeWorkspacePath(path);
            if (nativeFileHandlesByPath.TryGetValue(GetHandlePathKey(nativeWorkspaceId, relativePath), out var knownHandleId))
            {
                nativeFileHandles.TryGetValue(knownHandleId, out handle);
            }
        }

        var fullPath = ResolveInsideRoot(rootPath, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new NativeFileException("The Windows workspace file is no longer available.");
        }

        var info = new FileInfo(fullPath);
        if (info.Length > NativeFileService.MaxFileBytes)
        {
            throw new NativeFileException("Choose a UTF-8 text file up to 5 MB.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(fullPath, strictUtf8);
        }
        catch (DecoderFallbackException)
        {
            throw new NativeFileException("Choose a UTF-8 encoded text file.");
        }

        var handleId = handle?.HandleId ?? CreateFileHandle(nativeWorkspaceId, fullPath, relativePath);
        return new
        {
            refreshed = true,
            name = Path.GetFileName(fullPath),
            path = relativePath,
            displayPath = relativePath,
            extension = Path.GetExtension(fullPath).ToLowerInvariant(),
            encoding = "utf-8",
            content,
            nativeHandleId = handleId,
        };
    }

    public void StopWatching()
    {
        lock (watcherLock)
        {
            watcherDebounceTimer?.Dispose();
            watcherDebounceTimer = null;
            pendingChanges.Clear();
            recentHostWrites.Clear();
            workspaceWatcher?.Dispose();
            workspaceWatcher = null;
            activeWorkspaceId = string.Empty;
            activeWorkspaceRoot = string.Empty;
        }
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
        nativeFileHandles[handleId] = new WorkspaceFileHandle(handleId, workspaceId, fullPath, relativePath);
        nativeFileHandlesByPath[GetHandlePathKey(workspaceId, relativePath)] = handleId;
        return handleId;
    }

    private void StartWatching(string workspaceId, string rootPath)
    {
        try
        {
            var watcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            watcher.Changed += HandleWatcherChanged;
            watcher.Created += HandleWatcherCreated;
            watcher.Deleted += HandleWatcherDeleted;
            watcher.Renamed += HandleWatcherRenamed;
            watcher.Error += HandleWatcherError;
            activeWorkspaceId = workspaceId;
            activeWorkspaceRoot = rootPath;
            workspaceWatcher = watcher;
        }
        catch
        {
            StopWatching();
        }
    }

    private void HandleWatcherChanged(object sender, FileSystemEventArgs args)
    {
        QueueWatcherChange("changed", args.FullPath);
    }

    private void HandleWatcherCreated(object sender, FileSystemEventArgs args)
    {
        QueueWatcherChange("created", args.FullPath);
    }

    private void HandleWatcherDeleted(object sender, FileSystemEventArgs args)
    {
        QueueWatcherChange("deleted", args.FullPath);
    }

    private void HandleWatcherRenamed(object sender, RenamedEventArgs args)
    {
        QueueWatcherRename(args.OldFullPath, args.FullPath);
    }

    private void HandleWatcherError(object sender, ErrorEventArgs args)
    {
        StopWatching();
    }

    private void QueueWatcherChange(string kind, string fullPath)
    {
        lock (watcherLock)
        {
            if (string.IsNullOrWhiteSpace(activeWorkspaceId)
                || !TryGetSupportedRelativePath(activeWorkspaceRoot, fullPath, out var relativePath)
                || ShouldSuppressHostWrite(kind, relativePath))
            {
                return;
            }

            var handleId = EnsureHandleForWatcherChange(kind, fullPath, relativePath);
            var key = $"path:{relativePath}";
            if (pendingChanges.TryGetValue(key, out var existing))
            {
                pendingChanges[key] = CoalesceChange(existing, new PendingWorkspaceChange(kind, relativePath, null, handleId));
            }
            else
            {
                pendingChanges[key] = new PendingWorkspaceChange(kind, relativePath, null, handleId);
            }

            ScheduleWatcherFlush();
        }
    }

    private void QueueWatcherRename(string oldFullPath, string fullPath)
    {
        lock (watcherLock)
        {
            if (string.IsNullOrWhiteSpace(activeWorkspaceId)
                || !TryGetSupportedRelativePath(activeWorkspaceRoot, oldFullPath, out var oldPath)
                || !TryGetSupportedRelativePath(activeWorkspaceRoot, fullPath, out var relativePath))
            {
                return;
            }

            var handleId = MoveKnownHandle(oldPath, fullPath, relativePath);
            pendingChanges.Remove($"path:{oldPath}");
            pendingChanges.Remove($"path:{relativePath}");
            pendingChanges[$"rename:{oldPath}>{relativePath}"] = new PendingWorkspaceChange("renamed", relativePath, oldPath, handleId);
            ScheduleWatcherFlush();
        }
    }

    private void ScheduleWatcherFlush()
    {
        watcherDebounceTimer?.Dispose();
        watcherDebounceTimer = new Timer(_ => FlushWatcherChanges(), null, WatcherDebounceInterval, Timeout.InfiniteTimeSpan);
    }

    private void FlushWatcherChanges()
    {
        PendingWorkspaceChange[] changes;
        string workspaceId;
        lock (watcherLock)
        {
            if (pendingChanges.Count == 0 || string.IsNullOrWhiteSpace(activeWorkspaceId))
            {
                return;
            }

            workspaceId = activeWorkspaceId;
            changes = pendingChanges.Values
                .OrderBy(static change => change.OldPath ?? change.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            pendingChanges.Clear();
        }

        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(workspaceId, changes));
    }

    private PendingWorkspaceChange CoalesceChange(PendingWorkspaceChange existing, PendingWorkspaceChange next)
    {
        if (existing.Kind == "deleted" || next.Kind == "deleted")
        {
            return next with { Kind = "deleted", NativeHandleId = existing.NativeHandleId ?? next.NativeHandleId };
        }

        if (existing.Kind == "created" || next.Kind == "created")
        {
            return next with { Kind = "created", NativeHandleId = next.NativeHandleId ?? existing.NativeHandleId };
        }

        return next with { NativeHandleId = next.NativeHandleId ?? existing.NativeHandleId };
    }

    private string? EnsureHandleForWatcherChange(string kind, string fullPath, string relativePath)
    {
        if (nativeFileHandlesByPath.TryGetValue(GetHandlePathKey(activeWorkspaceId, relativePath), out var handleId))
        {
            return handleId;
        }

        if (kind == "created" && File.Exists(fullPath))
        {
            return CreateFileHandle(activeWorkspaceId, Path.GetFullPath(fullPath), relativePath);
        }

        return null;
    }

    private string? MoveKnownHandle(string oldPath, string fullPath, string relativePath)
    {
        var oldKey = GetHandlePathKey(activeWorkspaceId, oldPath);
        if (!nativeFileHandlesByPath.TryGetValue(oldKey, out var handleId)
            || !nativeFileHandles.TryGetValue(handleId, out var handle))
        {
            return File.Exists(fullPath)
                ? CreateFileHandle(activeWorkspaceId, Path.GetFullPath(fullPath), relativePath)
                : null;
        }

        nativeFileHandlesByPath.Remove(oldKey);
        handle.FullPath = Path.GetFullPath(fullPath);
        handle.RelativePath = relativePath;
        nativeFileHandlesByPath[GetHandlePathKey(activeWorkspaceId, relativePath)] = handleId;
        return handleId;
    }

    private void TrackHostWrite(string relativePath)
    {
        lock (watcherLock)
        {
            recentHostWrites[relativePath] = DateTimeOffset.UtcNow;
        }
    }

    private bool ShouldSuppressHostWrite(string kind, string relativePath)
    {
        if (kind == "deleted" || !recentHostWrites.TryGetValue(relativePath, out var writtenAt))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - writtenAt <= HostWriteSuppressionWindow)
        {
            return true;
        }

        recentHostWrites.Remove(relativePath);
        return false;
    }

    private static bool TryGetSupportedRelativePath(string rootPath, string fullPath, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        string resolved;
        try
        {
            resolved = Path.GetFullPath(fullPath);
            ResolveInsideRoot(rootPath, Path.GetRelativePath(rootPath, resolved));
        }
        catch
        {
            return false;
        }

        relativePath = ToSafeRelativePath(rootPath, resolved);
        if (string.IsNullOrWhiteSpace(relativePath) || !NativeFileService.IsSupportedExtension(Path.GetExtension(relativePath)))
        {
            return false;
        }

        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && !parts.Any(static part => part is "." or "..");
    }

    private static string GetHandlePathKey(string workspaceId, string relativePath)
    {
        return $"{workspaceId}:{relativePath}";
    }

    private sealed class WorkspaceFileHandle
    {
        public WorkspaceFileHandle(string handleId, string workspaceId, string fullPath, string relativePath)
        {
            HandleId = handleId;
            WorkspaceId = workspaceId;
            FullPath = fullPath;
            RelativePath = relativePath;
        }

        public string HandleId { get; }

        public string WorkspaceId { get; }

        public string FullPath { get; set; }

        public string RelativePath { get; set; }
    }
}

public sealed record WorkspaceChangedEventArgs(string NativeWorkspaceId, IReadOnlyList<PendingWorkspaceChange> Changes);

public sealed record PendingWorkspaceChange(string Kind, string Path, string? OldPath, string? NativeHandleId);

internal static class NativeWindowInterop
{
    private const int SwRestore = 9;

    public static void RestoreAndForeground(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        _ = ShowWindow(windowHandle, SwRestore);
        _ = SetForegroundWindow(windowHandle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
