namespace LensDocsStudio.Windows.Services;

public sealed class StaticAppLocator
{
    private static readonly string[] RequiredPaths =
    [
        "index.html",
        "assets",
        "docs",
        "manifest.webmanifest",
        "icon.svg",
        "md-mmd-renderer-v5.html",
        "service-worker.js"
    ];

    public string GetStaticAppRoot(string baseDirectory)
    {
        var staticAppRoot = Path.Combine(baseDirectory, "StaticApp");
        var missingPaths = RequiredPaths
            .Where(path => !File.Exists(Path.Combine(staticAppRoot, path)) && !Directory.Exists(Path.Combine(staticAppRoot, path)))
            .ToArray();

        if (missingPaths.Length > 0)
        {
            throw new DirectoryNotFoundException(
                $"The packaged static app could not be found at {staticAppRoot}. Missing: {string.Join(", ", missingPaths)}.");
        }

        return staticAppRoot;
    }
}
