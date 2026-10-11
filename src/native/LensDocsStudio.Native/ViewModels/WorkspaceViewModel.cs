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
    public ObservableCollection<WorkspaceEntry> Entries { get; } = [];
    [ObservableProperty] private DocumentViewModel? activeDocument;
    [ObservableProperty] private string status = "Local Markdown, Mermaid, and documentation studio";
    [ObservableProperty] private string? workspaceRoot;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string theme = "Light";
    [ObservableProperty] private string layout = "Split";
    private FileSystemWatcher? watcher;
    private CancellationTokenSource? operation;
    private readonly SemaphoreSlim recoveryGate = new(1, 1);
    private readonly SemaphoreSlim preferencesGate = new(1, 1);
    public UserPreferences Preferences { get; private set; } = new();
    public event EventHandler? DocumentChanged;
    partial void OnActiveDocumentChanged(DocumentViewModel? value) => DocumentChanged?.Invoke(this, EventArgs.Empty);
    public string StateDirectory => session.DirectoryPath;
    public CancellationToken OperationToken => operation?.Token ?? CancellationToken.None;

    public async Task InitialiseAsync(string[] paths)
    {
        await RunAsync(async () =>
        {
            var pending = await documents.ReadPendingWritesAsync();
            if (pending.Count != 0) Status = $"{pending.Count} interrupted saves are available in recovery.";
            Preferences = await session.ReadAsync<UserPreferences>("preferences.json") ?? new();
            Theme = Preferences.Theme;
            Layout = Preferences.Layout;
            var saved = await session.ReadAsync<SessionState>("session.json");
            if (saved is { SchemaVersion: 1 })
            {
                if (saved.Workspace is not null && Directory.Exists(saved.Workspace)) await SetWorkspaceAsync(saved.Workspace);
                foreach (var recovered in saved.Documents)
                {
                    DocumentSnapshot? baseline = null;
                    if (recovered.Path is not null && recovered.SavedFingerprint is not null)
                        baseline = new(recovered.Path, recovered.SavedText ?? recovered.Text, recovered.SavedFingerprint, recovered.Utf8Bom);
                    var tab = new DocumentViewModel(recovered.Text, baseline) { Id = recovered.Id, CaretOffset = recovered.CaretOffset, VerticalOffset = recovered.VerticalOffset, WorkspaceRoot = WorkspaceRoot };
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
            foreach (var path in paths)
            {
                if (Directory.Exists(path)) await SetWorkspaceAsync(path);
                else if (File.Exists(path) && WorkspaceService.IsDocument(path)) await OpenPathAsync(path);
            }
            if (Documents.Count == 0) NewDocument();
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
        if (existing is not null) { ActiveDocument = existing; return; }
        var snapshot = await documents.ReadAsync(path);
        var root = WorkspaceRoot is not null && path.StartsWith(WorkspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? WorkspaceRoot : Path.GetDirectoryName(path);
        var tab = new DocumentViewModel(snapshot.Text, snapshot) { WorkspaceRoot = root };
        Attach(tab); ActiveDocument = tab; Status = "Opened " + Path.GetFileName(path);
    }

    [RelayCommand] private async Task OpenFolderAsync()
    {
        if (dialogs.OpenFolder() is not { } path) return;
        await RunAsync(() => SetWorkspaceAsync(path));
    }
    public async Task SetWorkspaceAsync(string path)
    {
        var workspace = new WorkspacePath(path);
        var listing = await workspaces.EnumerateAsync(workspace, operation?.Token ?? default);
        watcher?.Dispose();
        WorkspaceRoot = workspace.Root;
        Entries.Clear();
        foreach (var entry in listing.Entries) Entries.Add(entry);
        Status = listing.Warnings.Count == 0 ? "Workspace opened" : string.Join(" ", listing.Warnings.Distinct());
        watcher = new FileSystemWatcher(path) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
        watcher.Changed += OnFileChanged; watcher.Deleted += OnFileChanged; watcher.Renamed += OnFileChanged;
        watcher.Error += (_, _) => Application.Current.Dispatcher.BeginInvoke(() => Status = "Folder monitoring was interrupted. Reopen the workspace to resume.");
        watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs args) => Application.Current.Dispatcher.BeginInvoke(async () =>
    {
        foreach (var document in Documents.Where(d => string.Equals(d.FilePath, args.FullPath, StringComparison.OrdinalIgnoreCase) ||
            args is RenamedEventArgs rename && string.Equals(d.FilePath, rename.OldFullPath, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            try { document.ExternalChange = (await documents.ReadAsync(document.FilePath!)).Fingerprint != document.SavedSnapshot?.Fingerprint; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { document.ExternalChange = true; }
        }
    });

    [RelayCommand] private Task SaveAsync() => RunAsync(async () => { if (ActiveDocument is { } tab) await SaveTabAsync(tab, false); });
    [RelayCommand] private Task SaveAsAsync() => RunAsync(async () => { if (ActiveDocument is { } tab) await SaveTabAsync(tab, true); });
    public async Task<bool> SaveTabAsync(DocumentViewModel tab, bool saveAs)
    {
        var path = tab.FilePath;
        DocumentSnapshot? expected = tab.SavedSnapshot;
        if (saveAs || path is null)
        {
            path = dialogs.SaveDocument(Path.GetFileName(path) ?? "Untitled.md");
            if (path is null) return false;
            // The native save dialogue has already confirmed replacement; retain a fresh fingerprint.
            expected = File.Exists(path) ? await documents.ReadAsync(path) : null;
        }
        var text = tab.EditorDocument.Text;
        var saved = await documents.SaveAsync(path, text, expected);
        tab.MarkSaved(saved);
        Status = "Saved " + Path.GetFileName(path);
        await PersistSessionAsync();
        return true;
    }

    [RelayCommand] private Task ReloadAsync() => RunAsync(async () =>
    {
        if (ActiveDocument is not { FilePath: not null } tab) return;
        if (tab.IsDirty && !dialogs.Confirm("Reloading replaces your editor text with the file on disk. A recovery snapshot will be retained.", "Reload document")) return;
        await session.WriteAsync($"snapshot-{Guid.NewGuid():N}.json", tab.Recovery());
        var snapshot = await documents.ReadAsync(tab.FilePath);
        tab.EditorDocument.Text = snapshot.Text;
        tab.MarkSaved(snapshot);
        Status = "Reloaded document";
    });

    [RelayCommand] private async Task CloseDocumentAsync(DocumentViewModel? tab)
    {
        if (tab is null) return;
        await RunAsync(async () =>
        {
            if (!await CanCloseAsync(tab)) return;
            tab.Edited -= OnEdited;
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
            await session.WriteAsync("preferences.json", changed);
            Preferences = changed;
        }
        finally { preferencesGate.Release(); }
    }

    [RelayCommand] private void CancelOperation() => operation?.Cancel();
    public async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        operation = new CancellationTokenSource();
        try { await action(); }
        catch (OperationCanceledException) { Status = "Operation cancelled; documents preserved."; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Status = ex.Message; dialogs.Error(ex.Message); }
        finally { operation.Dispose(); operation = null; IsBusy = false; }
    }
    public void Dispose() { watcher?.Dispose(); operation?.Cancel(); }
}
