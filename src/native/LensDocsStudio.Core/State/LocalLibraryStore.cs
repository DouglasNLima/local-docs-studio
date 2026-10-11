using System.Text;
using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.State;

public enum LocalLibraryKind { Template, Snippet, ExportProfile }
public sealed record LocalLibraryState(int SchemaVersion, LocalLibraryData Active, LocalLibraryData Archived);
public sealed record LocalLibrarySnapshot(LocalLibraryState State, DocumentSnapshot? File);
public sealed record LocalLibraryImport(LocalLibrarySnapshot Snapshot, int Added, int AlreadyPresent, int ReassignedIds);

/// <summary>Fingerprint-checked native library state; imports merge, archives retain content, source exports stay untouched.</summary>
public sealed class LocalLibraryStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private string LibraryPath => Path.Combine(DirectoryPath, "library.json");
    private string RecoveryPath => Path.Combine(DirectoryPath, "recovery");

    public async Task<LocalLibrarySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        Guard(LibraryPath);
        byte[] bytes;
        try
        {
            await using var input = new FileStream(LibraryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (input.Length > DocumentStore.MaximumDocumentBytes) throw new InvalidDataException("The saved content library exceeds 32 MB.");
            bytes = await MarkdownBundleService.ReadBoundedAsync(input, DocumentStore.MaximumDocumentBytes, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        { cancellationToken.ThrowIfCancellationRequested(); return new(new(1, LocalLibraryData.Empty, LocalLibraryData.Empty), null); }
        var snapshot = DocumentSnapshot.FromBytes(LibraryPath, bytes);
        try
        {
            using var document = JsonDocument.Parse(snapshot.Text, new JsonDocumentOptions { MaxDepth = 16 });
            LocalLibraryFormat.ValidateUniqueProperties(document.RootElement);
            var state = document.RootElement.Deserialize<LocalLibraryState>(LocalLibraryFormat.Json)
                ?? throw new InvalidDataException("The saved content library is empty.");
            Validate(state); return new(state, snapshot);
        }
        catch (JsonException exception) { throw new InvalidDataException("The saved content library is unreadable. Its original bytes have been retained.", exception); }
    }

    public async Task<LocalLibrarySnapshot> SaveAsync(LocalLibrarySnapshot expected, LocalLibraryState next, CancellationToken cancellationToken = default)
    {
        Validate(next); Guard(LibraryPath); Guard(RecoveryPath);
        if (expected.File is not null && !StringComparer.OrdinalIgnoreCase.Equals(expected.File.Path, LibraryPath))
            throw new ArgumentException("The content library snapshot belongs to another location.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next, LocalLibraryFormat.Json);
        if (bytes.Length > DocumentStore.MaximumDocumentBytes) throw new InvalidDataException("The saved library and archive exceed 32 MB. Export archived entries before adding more content.");
        cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(DirectoryPath);
        var saved = await new DocumentStore(RecoveryPath).SaveAsync(LibraryPath, Encoding.UTF8.GetString(bytes), expected.File, cancellationToken);
        return new(next, saved);
    }

    public async Task<LocalLibraryImport> ImportAsync(LocalLibrarySnapshot expected, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var imported = LocalLibraryFormat.Read(bytes);
        var merge = LocalLibraryFormat.Merge(expected.State.Active, imported, expected.State.Archived);
        var saved = await SaveAsync(expected, expected.State with { Active = merge.Library }, cancellationToken);
        return new(saved, merge.Added, merge.AlreadyPresent, merge.ReassignedIds);
    }

    public Task<LocalLibrarySnapshot> RenameAsync(LocalLibrarySnapshot expected, LocalLibraryKind kind, string id, string name, bool archived = false,
        CancellationToken cancellationToken = default)
    {
        name = name.Trim(); LocalLibraryFormat.Label(name, 160, "Library name");
        var source = archived ? expected.State.Archived : expected.State.Active;
        var renamed = kind switch
        {
            LocalLibraryKind.Template => source with { Templates = Rename(source.Templates) },
            LocalLibraryKind.Snippet => source with { Snippets = Rename(source.Snippets) },
            LocalLibraryKind.ExportProfile => source with { Profiles = RenameProfile(source.Profiles) },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SaveAsync(expected, archived ? expected.State with { Archived = renamed } : expected.State with { Active = renamed }, cancellationToken);

        LocalLibraryItem[] Rename(LocalLibraryItem[] entries)
        {
            if (!entries.Any(entry => entry.Id == id)) throw new ArgumentException("The selected library entry is unavailable.");
            return entries.Select(entry => entry.Id == id ? entry with { Label = name } : entry).ToArray();
        }
        LocalExportProfile[] RenameProfile(LocalExportProfile[] entries)
        {
            if (!entries.Any(entry => entry.Id == id)) throw new ArgumentException("The selected export profile is unavailable.");
            return entries.Select(entry => entry.Id == id ? entry with { Label = name } : entry).ToArray();
        }
    }

    public Task<LocalLibrarySnapshot> MoveAsync(LocalLibrarySnapshot expected, LocalLibraryKind kind, string id, bool archive, CancellationToken cancellationToken = default)
    {
        var source = archive ? expected.State.Active : expected.State.Archived;
        var destination = archive ? expected.State.Archived : expected.State.Active;
        switch (kind)
        {
            case LocalLibraryKind.Template:
                var template = source.Templates.SingleOrDefault(entry => entry.Id == id) ?? throw new ArgumentException("The selected template is unavailable.");
                source = source with { Templates = source.Templates.Where(entry => entry.Id != id).ToArray() };
                destination = destination with { Templates = [.. destination.Templates, template] }; break;
            case LocalLibraryKind.Snippet:
                var snippet = source.Snippets.SingleOrDefault(entry => entry.Id == id) ?? throw new ArgumentException("The selected snippet is unavailable.");
                source = source with { Snippets = source.Snippets.Where(entry => entry.Id != id).ToArray() };
                destination = destination with { Snippets = [.. destination.Snippets, snippet] }; break;
            case LocalLibraryKind.ExportProfile:
                var profile = source.Profiles.SingleOrDefault(entry => entry.Id == id) ?? throw new ArgumentException("The selected export profile is unavailable.");
                source = source with { Profiles = source.Profiles.Where(entry => entry.Id != id).ToArray() };
                destination = destination with { Profiles = [.. destination.Profiles, profile] }; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return SaveAsync(expected, archive ? expected.State with { Active = source, Archived = destination }
            : expected.State with { Active = destination, Archived = source }, cancellationToken);
    }

    private static void Validate(LocalLibraryState state)
    {
        if (state is null || state.SchemaVersion != 1 || state.Active is null || state.Archived is null)
            throw new InvalidDataException("The saved content library is incomplete or uses an unsupported version.");
        LocalLibraryFormat.Validate(state.Active); LocalLibraryFormat.Validate(state.Archived);
        Unique(state.Active.Templates.Select(entry => entry.Id), state.Archived.Templates.Select(entry => entry.Id));
        Unique(state.Active.Snippets.Select(entry => entry.Id), state.Archived.Snippets.Select(entry => entry.Id));
        Unique(state.Active.Profiles.Select(entry => entry.Id), state.Archived.Profiles.Select(entry => entry.Id));
        static void Unique(IEnumerable<string> active, IEnumerable<string> archived)
        {
            if (active.Intersect(archived, StringComparer.Ordinal).Any()) throw new InvalidDataException("An active and archived entry share an identifier. Existing content has been retained.");
        }
    }
    private void Guard(string path)
    {
        if (!WorkspacePath.ContainsOrEquals(DirectoryPath, path)) throw new IOException("The content library path leaves its private directory.");
        WorkspacePath.RejectReparsePoints(path);
    }
}
