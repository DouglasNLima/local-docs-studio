namespace LensDocsStudio.Core.State;

public static class RecentPaths
{
    public const int Capacity = 20;
    public static string[] Normalise(IEnumerable<string>? paths) => (paths ?? []).Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(SafeFullPath).Where(p => p is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(Capacity).ToArray();
    public static string[] Touch(IEnumerable<string>? paths, string path) => Normalise(new[] { path }.Concat(paths ?? []));
    private static string? SafeFullPath(string path)
    {
        try { return Path.IsPathFullyQualified(path) ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) : null; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
}
