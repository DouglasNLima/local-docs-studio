using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Library;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private LocalExportProfile? activeExportProfile;
    private LocalDocsSiteProfile? recentDocsSiteOptions;
    private LocalLibraryStore ContentLibrary => new(Path.Combine(model.StateDirectory, "content-library"));

    private async void ManageContentLibrary(object sender, RoutedEventArgs e)
    {
        var kind = sender is FrameworkElement { Tag: "profiles" } ? LocalLibraryKind.ExportProfile : LocalLibraryKind.Template;
        var tab = model.ActiveDocument; var source = tab?.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        await model.RunAsync(async () =>
        {
            var store = ContentLibrary; var snapshot = await Task.Run(() => store.ReadAsync(model.OperationToken), model.OperationToken);
            var window = new ContentLibraryWindow(store, snapshot, Path.Combine(model.StateDirectory, "export-recovery"), kind) { Owner = this };
            if (window.ShowDialog() != true || window.SelectedEntry is not { } selected) return;
            if (selected.Profile is { } profile) { ApplyProfile(profile); return; }
            if (selected.Item is not { } item) return;
            if (selected.Kind == LocalLibraryKind.Template) { await model.OpenLibraryTemplateAsync(item, model.OperationToken); return; }
            if (tab is null || model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly)
            { model.Status = "Open an editable document and choose the snippet again. Its insertion position was not available."; return; }
            model.EnsureDraftWorkspace(tab);
            var prepared = await new LibraryContentService().MaterialiseAsync(item.Content, tab.EffectivePath!, tab.EffectiveWorkspace!, model.OperationToken);
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly)
            { model.Status = "The document changed; the snippet was not applied. Choose it again."; return; }
            EditorFormattingService.Apply(Editor, MarkdownEditService.PlanBlock(source!, start, length, prepared, prepared.Trim('\r', '\n')));
            await model.PersistSessionAsync(); model.Status = "Inserted snippet: " + item.Label;
        });
    }
    private async void SaveLibraryContent(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } tab) return;
        var snippet = sender is FrameworkElement { Tag: "snippet" };
        var source = tab.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        if (string.IsNullOrWhiteSpace(source) || snippet && length == 0) { model.Status = snippet ? "Select Markdown before saving a snippet." : "Write a document before saving a template."; return; }
        var prompt = new TextPromptWindow(snippet ? "Save Markdown snippet" : "Save document template", snippet ? "Snippet name" : "Template name",
            snippet ? "Reusable snippet" : Path.GetFileNameWithoutExtension(tab.FilePath ?? tab.DisplayName ?? "Reusable template"), "Save",
            snippet ? "Snippet name" : "Template name", "Use 1–160 characters. Local images are embedded; editable Markdown and Mermaid are retained.") { Owner = this };
        if (prompt.ShowDialog() != true) return;
        var name = prompt.Value; var sourcePath = tab.EffectivePath; var workspace = tab.EffectiveWorkspace;
        await model.RunAsync(async () =>
        {
            var selected = snippet ? await Task.Run(() => LibrarySnippetSource.Select(source, start, length), model.OperationToken) : source;
            var portable = await new LibraryContentService().CaptureAsync(selected, sourcePath, workspace, model.OperationToken);
            var store = ContentLibrary; var snapshot = await Task.Run(() => store.ReadAsync(model.OperationToken), model.OperationToken);
            var filename = Path.GetFileName(tab.FilePath ?? tab.DisplayName ?? "template.md");
            if (filename.Length > 160 || Path.GetExtension(filename).ToLowerInvariant() is not (".md" or ".markdown" or ".mmd" or ".mermaid" or ".txt")) filename = "template.md";
            var item = new LocalLibraryItem((snippet ? "snippet-" : "template-") + Guid.NewGuid().ToString("N"), name, portable, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), snippet ? null : filename);
            var active = snippet ? snapshot.State.Active with { Snippets = [.. snapshot.State.Active.Snippets, item] }
                : snapshot.State.Active with { Templates = [.. snapshot.State.Active.Templates, item] };
            await Task.Run(() => store.SaveAsync(snapshot, snapshot.State with { Active = active }, model.OperationToken), model.OperationToken);
            model.Status = "Saved " + (snippet ? "snippet: " : "template: ") + name;
        });
    }
    private void ApplyProfile(LocalExportProfile? profile)
    {
        activeExportProfile = profile; recentDocsSiteOptions = profile?.DocsSite;
        CurrentExportProfile.Header = "Profile: " + (profile?.Label ?? "Generic documentation") + " (this session)";
        model.Status = "Applied export profile: " + (profile?.Label ?? "Generic documentation") + ". Current session only.";
    }
    private void ApplyBuiltInProfile(object sender, RoutedEventArgs e)
    {
        var kind = (string)((FrameworkElement)sender).Tag;
        if (kind == "generic") { ApplyProfile(null); return; }
        var site = kind == "docs-site" ? new LocalDocsSiteProfile(Path.GetFileName(model.WorkspaceRoot) ?? "Documentation", "Local documentation site.", "system") : null;
        ApplyProfile(new(kind, kind == "devops" ? "Azure DevOps Wiki Markdown" : "GitHub Pages Docs Site", kind == "devops", site, 0));
    }
    private async void SaveLibraryProfile(object sender, RoutedEventArgs e)
    {
        var prompt = new TextPromptWindow("Save export profile", "Profile name", activeExportProfile?.Label ?? "Local export profile", "Save", "Profile name", "Save the current Markdown bundle and Docs Site defaults to your local library.") { Owner = this };
        if (prompt.ShowDialog() != true) return;
        var name = prompt.Value; var site = recentDocsSiteOptions ?? activeExportProfile?.DocsSite;
        var devops = activeExportProfile?.DevopsMarkdownExport == true;
        await model.RunAsync(async () =>
        {
            var store = ContentLibrary; var snapshot = await Task.Run(() => store.ReadAsync(model.OperationToken), model.OperationToken);
            var profile = new LocalExportProfile("profile-" + Guid.NewGuid().ToString("N"), name, devops, site, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await Task.Run(() => store.SaveAsync(snapshot, snapshot.State with { Active = snapshot.State.Active with { Profiles = [.. snapshot.State.Active.Profiles, profile] } }, model.OperationToken), model.OperationToken);
            model.Status = "Saved export profile: " + name;
        });
    }
}
