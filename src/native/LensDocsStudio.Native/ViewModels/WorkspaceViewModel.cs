using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel(DocumentStore documents, SessionStore session, WorkspaceService workspaces, DesktopDialogs dialogs) : ObservableObject, IDisposable
{
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];
    public ObservableCollection<WorkspaceEntryViewModel> Entries { get; } = [];
    [ObservableProperty] private DocumentViewModel? activeDocument;
    [ObservableProperty] private string status = "Local Markdown, Mermaid, and documentation studio";
    [ObservableProperty] private string previewStatus = "Preview pending";
    [ObservableProperty] private string? workspaceRoot;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string theme = "Light";
    [ObservableProperty] private string layout = "Split";
    private FileSystemWatcher? watcher;
    private CancellationTokenSource? operation;
    private readonly SemaphoreSlim recoveryGate = new(1, 1);
    private readonly SemaphoreSlim preferencesGate = new(1, 1);
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private int queuedOperations;
    public UserPreferences Preferences { get; private set; } = new();
    public event EventHandler? DocumentChanged;
    public event Action<Guid, string>? DocumentRelocated;
    partial void OnActiveDocumentChanged(DocumentViewModel? value) => DocumentChanged?.Invoke(this, EventArgs.Empty);
    public string StateDirectory => session.DirectoryPath;
    public CancellationToken OperationToken => operation?.Token ?? CancellationToken.None;

    public async Task InitialiseAsync(string[] paths)
    {
        await RunAsync(async () =>
        {
            var pending = await documents.ReadPendingWritesAsync();
            if (pending.Count != 0) Status = $"{pending.Count} interrupted saves are available in recovery.";
            Preferences = await session.ReadAsync<UserPreferences>("preferences.json", validate: WorkspaceSettings.ArePreferencesValid) ?? new();
            RefreshRecentPaths();
            Theme = Preferences.Theme;
            Layout = Preferences.Layout;
            var saved = await session.ReadAsync<SessionState>("session.json", validate: saved => saved is { SchemaVersion: 1, Documents: not null } &&
                saved.Documents.All(d => d is { Text: not null } && (d.Path is null || Path.IsPathFullyQualified(d.Path))));
            if (saved is { SchemaVersion: 1 })
            {
                if (saved.Workspace is not null && Directory.Exists(saved.Workspace)) await SetWorkspaceAsync(saved.Workspace);
                foreach (var recovered in saved.Documents)
                {
                    DocumentSnapshot? baseline = null;
                    if (recovered.Path is not null && recovered.SavedFingerprint is not null)
                        baseline = new(recovered.Path, recovered.SavedText ?? recovered.Text, recovered.SavedFingerprint, recovered.Utf8Bom);
                    var tab = new DocumentViewModel(recovered.Text, baseline) { Id = recovered.Id, CaretOffset = recovered.CaretOffset, VerticalOffset = recovered.VerticalOffset, WorkspaceRoot = WorkspaceRoot, DisplayName = recovered.DisplayName, RecoveryKey = recovered.RecoveryKey };
                    if (recovered.AssetWorkspace is not null && string.Equals(Path.GetFullPath(recovered.AssetWorkspace), DraftWorkspace(recovered.Id), StringComparison.OrdinalIgnoreCase))
                        tab.AssetWorkspace = recovered.AssetWorkspace;
                    if (tab.FilePath is not null) tab.WorkspaceRoot = RootForDocument(tab.FilePath);
                    if (baseline is not null)
                    {
                        try
                        {
                            var current = await documents.ReadAsync(baseline.Path);
                            tab.IsDirty = DocumentSnapshot.Hash(DocumentSnapshot.Encode(recovered.Text, recovered.Utf8Bom)) != recovered.SavedFingerprint;
                            tab.ExternalChange = current.Fingerprint != baseline.Fingerprint;
                            // SavedSnapshot.Text must remain the saved version, not a recovered draft.
                            if (!tab.ExternalChange) { tab.MarkSaved(current); }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { tab.ExternalChange = true; tab.IsDirty = true; }
                    }
                    Attach(tab);
                }
                ActiveDocument = Documents.FirstOrDefault(d => d.Id == saved.ActiveDocument) ?? Documents.FirstOrDefault();
            }
            foreach (var pendingWrite in pending)
            {
                var recovered = DocumentSnapshot.FromBytes(pendingWrite.Path, pendingWrite.Intended);
                var tab = new DocumentViewModel(recovered.Text) { ExternalChange = true };
                Attach(tab);
                ActiveDocument = tab;
            }
            var moveRecovery = new WorkspaceMoveRecovery(Path.Combine(StateDirectory, "move-recovery"));
            var interruptedMoves = await moveRecovery.ReadAsync(OperationToken);
            foreach (var item in interruptedMoves.Items)
                foreach (var draft in item.Drafts.Where(d => !Documents.Any(tab => tab.RecoveryKey == d.Key)))
                {
                    var tab = new DocumentViewModel(draft.Text) { DisplayName = draft.Name, RecoveryKey = draft.Key };
                    Attach(tab); ActiveDocument = tab;
                }
            if (interruptedMoves.Items.Count != 0)
            {
                await PersistSessionAsync();
                foreach (var item in interruptedMoves.Items) moveRecovery.MarkReviewed(item.JournalPath);
            }
            foreach (var path in paths)
            {
                if (Directory.Exists(path)) await SetWorkspaceAsync(path);
                else if (File.Exists(path) && WorkspaceService.IsDocument(path)) await OpenPathAsync(path);
            }
            if (Documents.Count == 0) NewDocument();
            var warnings = documents.RecoveryWarnings.Concat(session.ReadWarnings).Concat(interruptedMoves.Warnings).ToList();
            if (interruptedMoves.Items.Count != 0) warnings.Add("Interrupted file moves were recovered into named draft tabs. Review the original and planned text before saving; no workspace files were automatically changed.");
            if (warnings.Count != 0) dialogs.Error("Some saved state needs review. Original files have been retained.\n\n" + string.Join("\n", warnings));
        });
    }

    private void Attach(DocumentViewModel tab)
    {
        tab.Edited += OnEdited;
        Documents.Add(tab);
    }
    private void OnEdited(object? sender, EventArgs args) => DocumentChanged?.Invoke(this, EventArgs.Empty);

    [RelayCommand] private void NewDocument()
    {
        var tab = new DocumentViewModel("") { WorkspaceRoot = WorkspaceRoot };
        Attach(tab); ActiveDocument = tab;
    }

    [RelayCommand] private async Task OpenDocumentAsync()
    {
        if (dialogs.OpenDocument() is { } path) await RunAsync(() => OpenPathAsync(path));
    }
    public async Task OpenPathAsync(string path)
    {
        path = Path.GetFullPath(path);
        var existing = Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { ActiveDocument = existing; await RememberDocumentAsync(path); return; }
        var snapshot = await documents.ReadAsync(path);
        var root = RootForDocument(path);
        var tab = new DocumentViewModel(snapshot.Text, snapshot) { WorkspaceRoot = root };
        Attach(tab); ActiveDocument = tab; Status = "Opened " + Path.GetFileName(path);
        await RememberDocumentAsync(path);
    }

    [RelayCommand] private async Task OpenFolderAsync()
    {
        if (dialogs.OpenFolder() is not { } path) return;
        await RunAsync(() => SetWorkspaceAsync(path));
    }
    public async Task SetWorkspaceAsync(string path)
    {
        var workspace = new WorkspacePath(path);
        var listing = await workspaces.EnumerateAsync(workspace, operation?.Token ?? default, excludedDirectory: StateDirectory);
        treeRefresh?.Cancel();
        watcher?.Dispose();
        if (!string.Equals(WorkspaceRoot, workspace.Root, StringComparison.OrdinalIgnoreCase)) Entries.Clear();
        WorkspaceRoot = workspace.Root;
        WorkspaceEntryViewModel.Reconcile(Entries, listing.Entries);
        Status = listing.Warnings.Count == 0 ? "Workspace opened" : string.Join(" ", listing.Warnings.Distinct());
        StartMonitoring(workspace.Root);
        foreach (var tab in Documents.Where(d => d.FilePath is not null))
            tab.WorkspaceRoot = RootForDocument(tab.FilePath!);
        await RememberWorkspaceAsync(workspace.Root);
    }

    [RelayCommand] private Task SaveAsync()
    {
        var tab = ActiveDocument;
        return RunAsync(async () => { if (tab is not null && Documents.Contains(tab)) await SaveTabAsync(tab, false); });
    }
    [RelayCommand] private Task SaveAsAsync()
    {
        var tab = ActiveDocument;
        return RunAsync(async () => { if (tab is not null && Documents.Contains(tab)) await SaveTabAsync(tab, true); });
    }
    public async Task<bool> SaveTabAsync(DocumentViewModel tab, bool saveAs)
    {
        if (tab.IsSaving) return false;
        var path = tab.FilePath;
        DocumentSnapshot? expected = tab.SavedSnapshot;
        if (saveAs || path is null)
        {
            path = dialogs.SaveDocument(Path.GetFileName(path) ?? tab.DisplayName ?? "Untitled.md", path is null ? tab.WorkspaceRoot ?? WorkspaceRoot : Path.GetDirectoryName(path));
            if (path is null) return false;
            // The native save dialogue has already confirmed replacement; retain a fresh fingerprint.
            expected = File.Exists(path) ? await documents.ReadAsync(path) : null;
        }
        if (Documents.Any(other => other != tab && string.Equals(other.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("This destination is already open in another tab. Close that tab or choose another filename.");
        tab.IsSaving = true;
        try
        {
            var text = tab.EditorDocument.Text;
            // Retain the draft before asset transfer or a destination failure that precedes the write journal.
            await PersistSessionAsync();
            await using var transfer = tab.EffectivePath is { } source && tab.EffectiveWorkspace is { } sourceRoot &&
                !string.Equals(source, path, StringComparison.OrdinalIgnoreCase)
                ? await new AssetTransferService().PrepareAsync(text, source, new(sourceRoot), path, new(RootForDocument(path)), OperationToken,
                    forceCopyImages: tab.FilePath is null, preserveDocumentLinks: tab.FilePath is null, excludedDirectory: StateDirectory)
                : new PreparedAssetTransfer(text);
            if (transfer.UncopiedDocumentLinks.Count != 0 && !dialogs.Confirm("Local images will be copied. These document links point into the previous workspace and may need updating in this copy:\n\n" +
                string.Join("\n", transfer.UncopiedDocumentLinks.Distinct().Take(12)) + "\n\nContinue saving the copy?", "Save as in another workspace")) return false;
            var saved = await documents.SaveAsync(path, transfer.Text, expected, OperationToken);
            transfer.Commit();
            if (transfer.Text != text) tab.EditorDocument.Replace(0, tab.EditorDocument.TextLength, transfer.Text);
            tab.MarkSaved(saved);
            DocumentRelocated?.Invoke(tab.Id, saved.Path);
            tab.WorkspaceRoot = RootForDocument(saved.Path);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            await RememberDocumentAsync(saved.Path);
            Status = "Saved " + Path.GetFileName(path);
            await PersistSessionAsync();
            return true;
        }
        finally { tab.IsSaving = false; }
    }

    private string DraftWorkspace(Guid id) => Path.Combine(StateDirectory, "drafts", id.ToString("N"));
    public void EnsureDraftWorkspace(DocumentViewModel tab)
    {
        if (tab.FilePath is not null) return;
        var root = DraftWorkspace(tab.Id);
        WorkspacePath.RejectReparsePoints(root);
        Directory.CreateDirectory(root);
        tab.AssetWorkspace = root;
    }

    [RelayCommand] private Task ReloadAsync()
    {
        var tab = ActiveDocument;
        return RunAsync(async () =>
        {
            if (tab is not { FilePath: not null } || !Documents.Contains(tab)) return;
            if (tab.IsDirty && !dialogs.Confirm("Reloading replaces your editor text with the file on disk. A recovery snapshot will be retained.", "Reload document")) return;
            tab.IsSaving = true;
            try
            {
                await CaptureSnapshotAsync(tab, "Before reload");
                var snapshot = await documents.ReadAsync(tab.FilePath, OperationToken);
                tab.EditorDocument.Text = snapshot.Text;
                tab.MarkSaved(snapshot);
                Status = "Reloaded document. Previous editor text is available in Document snapshots.";
            }
            finally { tab.IsSaving = false; }
        });
    }

    [RelayCommand] private async Task CloseDocumentAsync(DocumentViewModel? tab)
    {
        if (tab is null) return;
        await RunAsync(async () =>
        {
            if (!await CanCloseAsync(tab)) return;
            tab.Edited -= OnEdited;
            if (tab.FilePath is null) DocumentRemoved?.Invoke(tab.Id);
            Documents.Remove(tab);
            if (ActiveDocument == tab) ActiveDocument = Documents.LastOrDefault();
            await PersistSessionAsync();
        });
    }

    private async Task<bool> CanCloseAsync(DocumentViewModel tab)
    {
        if (!tab.IsDirty) return true;
        return dialogs.SaveChanges(tab.Title) switch
        {
            MessageBoxResult.Yes => await SaveTabAsync(tab, false),
            MessageBoxResult.No => true,
            _ => false
        };
    }
    public async Task<bool> PrepareCloseAsync()
    {
        foreach (var tab in Documents) if (!await CanCloseAsync(tab)) return false;
        await PersistSessionAsync();
        return true;
    }
    public async Task PersistSessionAsync()
    {
        // Capture UI-owned text before awaiting IO.
        var state = new SessionState(1, WorkspaceRoot, ActiveDocument?.Id, Documents.Select(d => d.Recovery()).ToArray());
        await recoveryGate.WaitAsync();
        try { await session.WriteAsync("session.json", state); }
        finally { recoveryGate.Release(); }
    }
    public async Task UpdatePreferencesAsync(Func<UserPreferences, UserPreferences> update)
    {
        await preferencesGate.WaitAsync();
        try
        {
            var changed = update(Preferences);
            if (!WorkspaceSettings.ArePreferencesValid(changed)) throw new ArgumentException("The selected preferences are unsupported.");
            await session.WriteAsync("preferences.json", changed);
            Preferences = changed;
        }
        finally { preferencesGate.Release(); }
    }

    [RelayCommand] private void CancelOperation() => operation?.Cancel();
    public async Task RunAsync(Func<Task> action)
    {
        queuedOperations++;
        IsBusy = true;
        await operationGate.WaitAsync();
        operation = new CancellationTokenSource();
        try { await action(); }
        catch (OperationCanceledException) { Status = "Operation cancelled; documents preserved."; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Status = ex.Message; dialogs.Error(ex.Message); }
        finally
        {
            operation.Dispose(); operation = null;
            queuedOperations--; IsBusy = queuedOperations != 0;
            operationGate.Release();
        }
    }
    public void Dispose() { disposed = true; treeRefresh?.Cancel(); watcher?.Dispose(); operation?.Cancel(); }
}
