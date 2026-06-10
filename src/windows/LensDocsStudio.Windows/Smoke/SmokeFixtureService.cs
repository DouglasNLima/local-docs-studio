using LensDocsStudio.Windows.Services;

namespace LensDocsStudio.Windows.Smoke;

public sealed class SmokeFixtureService
{
    private readonly SmokeOptions options;
    private readonly NativeFileService nativeFileService;
    private readonly NativeWorkspaceService nativeWorkspaceService;

    public SmokeFixtureService(
        SmokeOptions options,
        NativeFileService nativeFileService,
        NativeWorkspaceService nativeWorkspaceService)
    {
        this.options = options;
        this.nativeFileService = nativeFileService;
        this.nativeWorkspaceService = nativeWorkspaceService;
    }

    public bool Enabled => options.Enabled;

    public Task<object> OpenFixtureFileAsync()
    {
        var path = ResolveSmokePath("single-file.md");
        return nativeFileService.OpenFilePathAsync(path);
    }

    public Task<object> SaveFixtureFileAsAsync(string? content)
    {
        var path = ResolveSmokePath("single-file-copy.md");
        return nativeFileService.SaveFileAsPathAsync(path, content);
    }

    public Task<object> OpenFixtureWorkspaceAsync()
    {
        var path = ResolveSmokePath("workspace");
        if (!Directory.Exists(path))
        {
            throw new NativeFileException("The smoke workspace fixture is unavailable.");
        }

        return nativeWorkspaceService.OpenFolderPathAsync(path);
    }

    public async Task<object> TouchWorkspaceFileAsync()
    {
        var relativePath = Path.Combine("workspace", "docs", "overview.md");
        var path = ResolveSmokePath(relativePath);
        if (!File.Exists(path))
        {
            throw new NativeFileException("The smoke workspace file is unavailable.");
        }

        await File.WriteAllTextAsync(path, "# Overview\r\n\r\nExternally changed by the automated native bridge smoke.\r\n");
        return new
        {
            changed = true,
            path = "docs/overview.md",
        };
    }

    private string ResolveSmokePath(string relativePath)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new NativeFileException("Smoke fixture operations are unavailable.");
        }

        var rootPath = Path.GetFullPath(options.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new NativeFileException("Smoke fixture paths must stay inside the smoke root.");
        }

        return fullPath;
    }
}
