using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.ViewModels;

public sealed record RecentPath(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public partial class WorkspaceViewModel
{
    public ObservableCollection<RecentPath> RecentDocuments { get; } = [];
    public ObservableCollection<RecentPath> RecentWorkspaces { get; } = [];

    private string RootForDocument(string path, string? retainedRoot = null) => DocumentWorkspace.Resolve(path, WorkspaceRoot, retainedRoot);

    private void RefreshRecentPaths()
    {
        RecentDocuments.Clear(); RecentWorkspaces.Clear();
        foreach (var path in RecentPaths.Normalise(Preferences.RecentDocuments)) RecentDocuments.Add(new(path));
        foreach (var path in RecentPaths.Normalise(Preferences.RecentWorkspaces)) RecentWorkspaces.Add(new(path));
    }
    private Task RememberDocumentAsync(string path) => RememberAsync(p => p with { RecentDocuments = RecentPaths.Touch(p.RecentDocuments, path) });
    private Task RememberWorkspaceAsync(string path) => RememberAsync(p => p with { RecentWorkspaces = RecentPaths.Touch(p.RecentWorkspaces, path) });
    private async Task RememberAsync(Func<UserPreferences, UserPreferences> update)
    {
        try { await UpdatePreferencesAsync(update); RefreshRecentPaths(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = "The recent list could not be saved: " + ex.Message; }
    }
    [RelayCommand] private Task OpenRecentDocumentAsync(string? path) => RunAsync(async () =>
    {
        if (path is null) return;
        if (!File.Exists(path)) throw new IOException("This recent document is unavailable. Reconnect its drive or open its new location.\n\n" + path);
        await OpenPathAsync(path);
    });
    [RelayCommand] private Task OpenRecentWorkspaceAsync(string? path) => RunAsync(async () =>
    {
        if (path is null) return;
        if (!Directory.Exists(path)) throw new IOException("This recent workspace is unavailable. Reconnect its drive or open its new location.\n\n" + path);
        await SetWorkspaceAsync(path);
    });
    [RelayCommand] private Task ClearRecentPathsAsync() => RememberAsync(p => p with { RecentDocuments = [], RecentWorkspaces = [] });
}
