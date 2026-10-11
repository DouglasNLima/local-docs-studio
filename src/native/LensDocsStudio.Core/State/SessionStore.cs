using System.Text.Json;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.State;

public sealed record RecoveredDocument(Guid Id, string? Path, string Text, string? SavedFingerprint, bool Utf8Bom, int CaretOffset, double VerticalOffset, string? SavedText = null);
public sealed record SessionState(int SchemaVersion, string? Workspace, Guid? ActiveDocument, IReadOnlyList<RecoveredDocument> Documents);
public sealed record WindowBounds(int Left, int Top, int Right, int Bottom, bool Maximised);
public sealed record UserPreferences(string Theme = "System", string Layout = "Split", bool AutomaticUpdateChecks = true,
    string UpdateChannel = "Stable", DateTimeOffset? LastUpdateCheck = null, string? IgnoredVersion = null,
    string[]? RecentDocuments = null, string[]? RecentWorkspaces = null, WindowBounds? Window = null);

public sealed class SessionStore(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task WriteAsync<T>(string name, T value, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var destination = Path.Combine(DirectoryPath, name);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await DocumentStore.WriteDurableAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(value, Options), cancellationToken);
                if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".bak", true);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { gate.Release(); }
    }

    public async Task<T?> ReadAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        foreach (var suffix in new[] { "", ".bak" })
        {
            var path = Path.Combine(DirectoryPath, name + suffix);
            if (!File.Exists(path)) continue;
            if (new FileInfo(path).Length > 128 * 1024 * 1024) throw new IOException("The saved session is too large.");
            try { return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), Options); }
            catch (JsonException) when (suffix.Length == 0) { }
        }
        return default;
    }

    private static void ValidateName(string name)
    {
        if (Path.GetFileName(name) != name || !name.EndsWith(".json", StringComparison.Ordinal)) throw new ArgumentException("Use a local JSON state filename.");
    }
}
