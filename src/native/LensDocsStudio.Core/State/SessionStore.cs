using System.Text.Json;

namespace LensDocsStudio.Core.State;

public sealed record RecoveredDocument(Guid Id, string? Path, string Text, string? SavedFingerprint, bool Utf8Bom, int CaretOffset, double VerticalOffset, string? SavedText = null, string? AssetWorkspace = null, string? RecoveryKey = null, string? DisplayName = null, string? WorkspaceRoot = null, bool IsPinned = false);
public sealed record SessionState(int SchemaVersion, string? Workspace, Guid? ActiveDocument, IReadOnlyList<RecoveredDocument> Documents);
public sealed record WindowBounds(int Left, int Top, int Right, int Bottom, bool Maximised);
public sealed record UserPreferences(string Theme = "System", string Layout = "Split", bool AutomaticUpdateChecks = true,
    string UpdateChannel = "Stable", DateTimeOffset? LastUpdateCheck = null, string? IgnoredVersion = null,
    string[]? RecentDocuments = null, string[]? RecentWorkspaces = null, WindowBounds? Window = null,
    EditorPreferences? Editor = null, PreviewPreferences? Preview = null, bool ShowOutline = true, DesktopPreferences? Desktop = null);

public sealed class SessionStore
{
    public const long DefaultMaximumStateBytes = 512L * 1024 * 1024;
    private readonly SemaphoreSlim gate = new(1, 1);
    public string DirectoryPath { get; }
    public long MaximumStateBytes { get; }
    public List<string> ReadWarnings { get; } = [];
    private readonly HashSet<string> blockedWrites = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SessionStore(string directory, long maximumStateBytes = DefaultMaximumStateBytes)
    {
        if (maximumStateBytes is < 1 or > DefaultMaximumStateBytes) throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        DirectoryPath = Path.GetFullPath(directory);
        MaximumStateBytes = maximumStateBytes;
    }

    public async Task WriteAsync<T>(string name, T value, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (blockedWrites.Contains(name)) throw new IOException("The saved state could not be read or retained safely. It has been preserved: " + name);
            // JSON conversion and the durable flush must not occupy the editor's dispatcher.
            await Task.Run(() => WriteFileAsync(name, value, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task WriteFileAsync<T>(string name, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DirectoryPath);
        var destination = Path.Combine(DirectoryPath, name);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                using var bounded = new BoundedStateWriteStream(file, MaximumStateBytes);
                await JsonSerializer.SerializeAsync(bounded, value, Options, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                file.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".bak", true);
            else File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<T?> ReadAsync<T>(string name, CancellationToken cancellationToken = default, Func<T, bool>? validate = null)
    {
        ValidateName(name);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => ReadFileAsync(name, cancellationToken, validate), cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task<T?> ReadFileAsync<T>(string name, CancellationToken cancellationToken, Func<T, bool>? validate)
    {
        foreach (var suffix in new[] { "", ".bak" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(DirectoryPath, name + suffix);
            if (!File.Exists(path)) continue;
            // Keep saves blocked after an I/O failure until a retry reads or retains the state.
            blockedWrites.Add(name);
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            try
            {
                // Inspect the opened, write-denying handle, not a racy FileInfo snapshot.
                if (file.Length > MaximumStateBytes) throw new JsonException("The saved state exceeds the recovery size limit.");
                var value = await JsonSerializer.DeserializeAsync<T>(file, Options, cancellationToken).ConfigureAwait(false);
                if (value is null || validate is not null && !validate(value)) throw new JsonException("The saved state is incomplete or unsupported.");
                blockedWrites.Remove(name);
                return value;
            }
            catch (JsonException ex)
            {
                // Retain malformed and oversized legacy state before any backup rotation.
                // The open handle also prevents replacement while this copy is made.
                var retained = path + ".unreadable-" + Guid.NewGuid().ToString("N") + ".json";
                File.Copy(path, retained, false);
                blockedWrites.Remove(name);
                ReadWarnings.Add("Unreadable saved state was retained as " + Path.GetFileName(retained) + ". " + ex.Message);
            }
        }
        return default;
    }

    private static void ValidateName(string name)
    {
        if (Path.GetFileName(name) != name || !name.EndsWith(".json", StringComparison.Ordinal)) throw new ArgumentException("Use a local JSON state filename.");
    }
}
