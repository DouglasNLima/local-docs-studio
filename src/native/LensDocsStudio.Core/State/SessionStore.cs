using System.Text.Json;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.State;

public sealed record RecoveredDocument(Guid Id, string? Path, string Text, string? SavedFingerprint, bool Utf8Bom, int CaretOffset, double VerticalOffset, string? SavedText = null, string? AssetWorkspace = null, string? RecoveryKey = null, string? DisplayName = null);
public sealed record SessionState(int SchemaVersion, string? Workspace, Guid? ActiveDocument, IReadOnlyList<RecoveredDocument> Documents);
public sealed record WindowBounds(int Left, int Top, int Right, int Bottom, bool Maximised);
public sealed record UserPreferences(string Theme = "System", string Layout = "Split", bool AutomaticUpdateChecks = true,
    string UpdateChannel = "Stable", DateTimeOffset? LastUpdateCheck = null, string? IgnoredVersion = null,
    string[]? RecentDocuments = null, string[]? RecentWorkspaces = null, WindowBounds? Window = null,
    EditorPreferences? Editor = null, PreviewPreferences? Preview = null, bool ShowOutline = true);

public sealed class SessionStore(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public List<string> ReadWarnings { get; } = [];
    private readonly HashSet<string> blockedWrites = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task WriteAsync<T>(string name, T value, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        if (blockedWrites.Contains(name)) throw new IOException("The unreadable saved state could not be backed up. It has been preserved: " + name);
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

    public async Task<T?> ReadAsync<T>(string name, CancellationToken cancellationToken = default, Func<T, bool>? validate = null)
    {
        ValidateName(name);
        foreach (var suffix in new[] { "", ".bak" })
        {
            var path = Path.Combine(DirectoryPath, name + suffix);
            if (!File.Exists(path)) continue;
            if (new FileInfo(path).Length > 128 * 1024 * 1024) throw new IOException("The saved session is too large.");
            try
            {
                var value = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), Options);
                if (value is null || validate is not null && !validate(value)) throw new JsonException("The saved state is incomplete or unsupported.");
                return value;
            }
            catch (JsonException)
            {
                // Preserve malformed state independently before a future save can rotate its backup.
                blockedWrites.Add(name);
                var retained = path + ".unreadable-" + Guid.NewGuid().ToString("N") + ".json";
                File.Copy(path, retained, false);
                blockedWrites.Remove(name);
                ReadWarnings.Add("Unreadable saved state was retained as " + Path.GetFileName(retained));
            }
        }
        return default;
    }

    private static void ValidateName(string name)
    {
        if (Path.GetFileName(name) != name || !name.EndsWith(".json", StringComparison.Ordinal)) throw new ArgumentException("Use a local JSON state filename.");
    }
}
