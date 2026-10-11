using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Exports;

public sealed record WordTemplateRecord(int SchemaVersion, Guid Id, string Name, string SourceName, DateTimeOffset ImportedAt,
    long Bytes, string Sha256, string[] Warnings);
public sealed record WordTemplateEntry(WordTemplateRecord Record, string Fingerprint, bool Archived)
{
    public string Name => Record.Name;
    public string Details => Record.SourceName + " · " + Record.ImportedAt.ToLocalTime().ToString("g") + " · " + Record.Bytes.ToString("N0") + " bytes";
}
public sealed record WordTemplateListing(IReadOnlyList<WordTemplateEntry> Entries, IReadOnlyList<string> Warnings);

/// <summary>Private copies of validated template formatting; metadata never supplies filesystem paths.</summary>
public sealed class WordTemplateLibrary(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly SemaphoreSlim mutations = new(1, 1);
    private const int MaximumTemplates = 100;

    public async Task<WordTemplateEntry> ImportAsync(string sourcePath, string name, CancellationToken cancellationToken = default)
    {
        name = CleanName(name);
        var prepared = await new DocxTemplateService().ReadAsync(sourcePath, cancellationToken);
        var record = new WordTemplateRecord(1, Guid.NewGuid(), name, Path.GetFileName(sourcePath), DateTimeOffset.UtcNow,
            prepared.Bytes.LongLength, DocumentSnapshot.Hash(prepared.Bytes), prepared.Warnings.ToArray());
        Validate(record, record.Id);
        await mutations.WaitAsync(cancellationToken);
        try
        {
            using var libraryLock = Lock();
            if (Folders(false).Take(MaximumTemplates).Count() >= MaximumTemplates) throw new IOException("The Word template library holds up to 100 active templates. Archive an unused template first.");
            var stage = Path.Combine(DirectoryPath, ".import-" + record.Id.ToString("N")); Guard(stage); Directory.CreateDirectory(stage);
            try
            {
                await DocumentStore.WriteDurableAsync(Path.Combine(stage, "template.docx"), prepared.Bytes, cancellationToken);
                var metadata = JsonSerializer.SerializeToUtf8Bytes(record, Json);
                await DocumentStore.WriteDurableAsync(Path.Combine(stage, "record.json"), metadata, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(stage, Folder(record.Id, false));
                return new(record, DocumentSnapshot.Hash(metadata), false);
            }
            finally
            {
                // Only known files inside our unpublished staging folder are removed.
                if (Directory.Exists(stage))
                {
                    Guard(stage);
                    foreach (var nameToRemove in new[] { "template.docx", "record.json" })
                    { var file = Path.Combine(stage, nameToRemove); Guard(file); if (File.Exists(file)) File.Delete(file); }
                    if (!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
                }
            }
        }
        finally { mutations.Release(); }
    }

    public async Task<WordTemplateListing> ListAsync(bool archived = false, CancellationToken cancellationToken = default)
    {
        Guard(DirectoryPath); var entries = new List<WordTemplateEntry>(); var warnings = new List<string>();
        var limit = archived ? 1000 : MaximumTemplates;
        var folders = Folders(archived).Take(limit + 1).ToArray();
        if (folders.Length > limit) warnings.Add("The template listing reached its limit; additional entries have been retained.");
        foreach (var folder in folders.Take(limit))
        {
            cancellationToken.ThrowIfCancellationRequested(); var id = Guid.ParseExact(Path.GetFileName(folder), "N");
            try
            {
                var (record, snapshot) = await ReadRecord(id, archived, cancellationToken);
                var template = Path.Combine(folder, "template.docx"); Guard(template);
                if (!File.Exists(template)) throw new IOException("The template package is missing.");
                entries.Add(new(record, snapshot.Fingerprint, archived));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            { warnings.Add("An unavailable template was retained: " + id.ToString("N") + ". " + ex.Message); }
        }
        return new(entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Record.ImportedAt).ToArray(), warnings);
    }

    public async Task<PreparedWordTemplate> ReadAsync(WordTemplateEntry entry, CancellationToken cancellationToken = default)
    {
        var (record, _) = await ReadCurrent(entry, cancellationToken);
        var path = Path.Combine(Folder(record.Id, entry.Archived), "template.docx"); Guard(path);
        byte[] bytes;
        await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous))
        {
            if (file.Length != record.Bytes) throw new IOException("The stored Word template changed. Import the original file again.");
            bytes = await MarkdownBundleService.ReadBoundedAsync(file, 32 * 1024 * 1024, cancellationToken);
        }
        if (bytes.LongLength != record.Bytes || DocumentSnapshot.Hash(bytes) != record.Sha256) throw new IOException("The stored Word template failed its integrity check. Import the original file again.");
        return await Task.Run(() => new DocxTemplateService().Prepare(bytes, cancellationToken), cancellationToken);
    }

    public async Task<WordTemplateEntry> RenameAsync(WordTemplateEntry entry, string name, CancellationToken cancellationToken = default)
    {
        name = CleanName(name); await mutations.WaitAsync(cancellationToken);
        try
        {
            using var libraryLock = Lock(); var (record, snapshot) = await ReadCurrent(entry, cancellationToken);
            var renamed = record with { Name = name };
            var store = new DocumentStore(Path.Combine(DirectoryPath, "metadata-recovery")); Guard(store.RecoveryDirectory);
            var saved = await store.SaveAsync(snapshot.Path, JsonSerializer.Serialize(renamed, Json), snapshot, cancellationToken);
            return new(renamed, saved.Fingerprint, entry.Archived);
        }
        finally { mutations.Release(); }
    }

    public Task<WordTemplateEntry> ArchiveAsync(WordTemplateEntry entry, CancellationToken cancellationToken = default) => MoveAsync(entry, true, cancellationToken);
    public Task<WordTemplateEntry> RestoreAsync(WordTemplateEntry entry, CancellationToken cancellationToken = default) => MoveAsync(entry, false, cancellationToken);

    private async Task<WordTemplateEntry> MoveAsync(WordTemplateEntry entry, bool archive, CancellationToken cancellationToken)
    {
        if (entry.Archived == archive) throw new ArgumentException(archive ? "This template is already archived." : "This template is already active.");
        await mutations.WaitAsync(cancellationToken);
        try
        {
            using var libraryLock = Lock(); var (record, _) = await ReadCurrent(entry, cancellationToken);
            var limit = archive ? 1000 : MaximumTemplates;
            if (Folders(archive).Take(limit).Count() >= limit) throw new IOException("The destination template library is full.");
            var target = Folder(record.Id, archive); var parent = Path.GetDirectoryName(target)!; Guard(parent); Directory.CreateDirectory(parent);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(Folder(record.Id, entry.Archived), target);
            return entry with { Archived = archive };
        }
        finally { mutations.Release(); }
    }

    private async Task<(WordTemplateRecord Record, DocumentSnapshot Snapshot)> ReadCurrent(WordTemplateEntry entry, CancellationToken cancellationToken)
    {
        var result = await ReadRecord(entry.Record.Id, entry.Archived, cancellationToken);
        if (entry.Fingerprint != result.Snapshot.Fingerprint) throw new DocumentConflictException("The Word template changed. Refresh the library before continuing.");
        return result;
    }
    private async Task<(WordTemplateRecord Record, DocumentSnapshot Snapshot)> ReadRecord(Guid id, bool archived, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Folder(id, archived), "record.json"); Guard(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, FileOptions.Asynchronous);
        if (file.Length > 16384) throw new InvalidDataException("The template record exceeds its size limit.");
        var bytes = await MarkdownBundleService.ReadBoundedAsync(file, 16384, cancellationToken);
        var record = JsonSerializer.Deserialize<WordTemplateRecord>(bytes) ?? throw new InvalidDataException("The template record is empty."); Validate(record, id);
        return (record, DocumentSnapshot.FromBytes(path, bytes));
    }
    private IEnumerable<string> Folders(bool archived)
    {
        var root = archived ? Path.Combine(DirectoryPath, "archived") : DirectoryPath; Guard(root);
        return Directory.Exists(root) ? Directory.EnumerateDirectories(root).Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)) : [];
    }
    private string Folder(Guid id, bool archived)
    {
        if (id == Guid.Empty) throw new ArgumentException("Choose a valid Word template.");
        var path = Path.Combine(DirectoryPath, archived ? "archived" : "", id.ToString("N")); Guard(path); return path;
    }
    private FileStream Lock()
    {
        Guard(DirectoryPath); Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, "library.lock"); Guard(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private void Guard(string path)
    {
        if (!WorkspacePath.ContainsOrEquals(DirectoryPath, path)) throw new IOException("The template path leaves its private library.");
        WorkspacePath.RejectReparsePoints(path);
    }
    private static string CleanName(string? name)
    {
        var clean = name?.Trim() ?? "";
        if (clean.Length is < 1 or > 120 || clean.Any(char.IsControl)) throw new ArgumentException("Use a template name of 1–120 characters without line breaks or control characters.");
        return clean;
    }
    private static void Validate(WordTemplateRecord record, Guid expected)
    {
        if (record.SchemaVersion != 1 || record.Id == Guid.Empty || record.Id != expected || record.Name != CleanName(record.Name)
            || record.SourceName is null || record.SourceName.Length is < 1 or > 260 || record.SourceName.Any(char.IsControl)
            || record.Bytes is < 1 or > 32 * 1024 * 1024 || record.Sha256 is null || record.Sha256.Length != 64 || !record.Sha256.All(char.IsAsciiHexDigit)
            || record.Warnings is null || record.Warnings.Length > 8 || record.Warnings.Any(warning => warning is null || warning.Length > 1024))
            throw new InvalidDataException("The Word template record is incomplete or unsupported.");
    }
}
