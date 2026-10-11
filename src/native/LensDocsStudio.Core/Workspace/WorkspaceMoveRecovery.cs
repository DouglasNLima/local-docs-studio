using System.Text.Json;

namespace LensDocsStudio.Core.Workspace;

public sealed record MoveRecoveryDraft(string Key, string Name, string Text);
public sealed record MoveRecoveryItem(string JournalPath, IReadOnlyList<MoveRecoveryDraft> Drafts);
public sealed record MoveRecoveryListing(IReadOnlyList<MoveRecoveryItem> Items, IReadOnlyList<string> Warnings);

/// <summary>Reads interrupted plans as text drafts only. Journal paths never authorise filesystem replay.</summary>
public sealed class WorkspaceMoveRecovery(string directory)
{
    private string DirectoryPath { get; } = Path.GetFullPath(directory);
    public async Task<MoveRecoveryListing> ReadAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<MoveRecoveryItem>(); var warnings = new List<string>();
        if (!Directory.Exists(DirectoryPath)) return new(items, warnings);
        long totalCharacters = 0;
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.move.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                WorkspacePath.RejectReparsePoints(path);
                if (new FileInfo(path).Length > 128 * 1024 * 1024) throw new IOException("The move record exceeds the recovery reading limit.");
                var journal = JsonSerializer.Deserialize<WorkspaceMoveJournal>(await File.ReadAllBytesAsync(path, cancellationToken));
                if (journal is not { SchemaVersion: 1, Plan.Source.Text: not null, Plan.UpdatedSource: not null, Plan.OtherDocuments: not null })
                    throw new IOException("The move record is incomplete or unsupported.");
                var drafts = new List<MoveRecoveryDraft>();
                void Add(string name, string text)
                {
                    totalCharacters += text.Length;
                    if (totalCharacters > 32 * 1024 * 1024) throw new IOException("Additional move recovery text was retained on disk because it exceeds the session safety limit.");
                    drafts.Add(new(Path.GetFileName(path) + ":" + drafts.Count, name, text));
                }
                Add("Recovered original — " + Path.GetFileName(journal.Plan.Source.Path), journal.Plan.Source.Text);
                if (journal.Plan.UpdatedSource != journal.Plan.Source.Text) Add("Planned move — " + Path.GetFileName(journal.Plan.Destination), journal.Plan.UpdatedSource);
                foreach (var change in journal.Plan.OtherDocuments)
                {
                    if (change is not { Original.Text: not null, UpdatedText: not null }) throw new IOException("A move recovery document is incomplete.");
                    Add("Recovered original — " + Path.GetFileName(change.Original.Path), change.Original.Text);
                    Add("Planned links — " + Path.GetFileName(change.Original.Path), change.UpdatedText);
                }
                items.Add(new(path, drafts));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { warnings.Add("Move recovery record retained: " + Path.GetFileName(path) + ". " + ex.Message); }
        }
        return new(items, warnings);
    }

    public void MarkReviewed(string path)
    {
        path = Path.GetFullPath(path);
        if (!WorkspacePath.Contains(DirectoryPath, path) || Path.GetDirectoryName(path) != DirectoryPath || !path.EndsWith(".move.json", StringComparison.Ordinal))
            throw new IOException("The move recovery record is outside native state.");
        WorkspacePath.RejectReparsePoints(path);
        // Call only after the draft copies have been saved in session recovery. Retain the journal.
        File.Move(path, path + ".reviewed.json", false);
    }
}
