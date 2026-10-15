using System.Collections.ObjectModel;
using System.Security.Cryptography;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Exports;

public sealed record SavedExport(Guid Id, string Path, long Bytes, string Sha256, DateTimeOffset SavedAt)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Details => Name + " · " + SavedAt.ToLocalTime().ToString("t");

    public static SavedExport FromSavedBytes(string path, byte[] bytes)
    {
        ValidatePath(path);
        if (!File.Exists(path) || new FileInfo(path).Length != bytes.LongLength) throw new IOException("The exported file is no longer available.");
        return new(Guid.NewGuid(), System.IO.Path.GetFullPath(path), bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTimeOffset.UtcNow);
    }

    internal static void ValidatePath(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path) || path.Any(char.IsControl) || path.Contains('"')
            || path[(System.IO.Path.GetPathRoot(path)?.Length ?? 0)..].Contains(':')
            || System.IO.Path.GetExtension(path).ToLowerInvariant() is not (".docx" or ".pdf" or ".html" or ".zip" or ".csv" or ".svg" or ".png" or ".json"))
            throw new IOException("Choose a supported exported file on disk.");
        WorkspacePath.RejectReparsePoints(path);
    }
}

/// <summary>Only successful writes enter this bounded, memory-only history. No folder scanning or persisted paths.</summary>
public sealed class ExportSessionHistory
{
    public const int MaximumEntries = 10;
    private readonly ObservableCollection<SavedExport> entries = [];
    public ReadOnlyObservableCollection<SavedExport> Entries { get; }
    public ExportSessionHistory() => Entries = new(entries);
    public void Remember(SavedExport saved)
    {
        SavedExport.ValidatePath(saved.Path);
        foreach (var previous in entries.Where(entry => string.Equals(entry.Path, saved.Path, StringComparison.OrdinalIgnoreCase)).ToArray()) entries.Remove(previous);
        entries.Insert(0, saved);
        while (entries.Count > MaximumEntries) entries.RemoveAt(entries.Count - 1);
    }
    public async Task VerifyAsync(SavedExport saved, CancellationToken cancellationToken = default)
    {
        if (!entries.Contains(saved)) throw new IOException("This export is no longer in the current session history.");
        SavedExport.ValidatePath(saved.Path);
        await using var file = new FileStream(saved.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length != saved.Bytes || Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken)) != saved.Sha256)
            throw new IOException("The exported file has changed. Export again before using this action.");
    }
}
