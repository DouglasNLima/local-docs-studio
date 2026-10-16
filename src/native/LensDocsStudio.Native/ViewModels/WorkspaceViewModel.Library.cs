using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Library;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    public async Task OpenLibraryTemplateAsync(LocalLibraryItem item, CancellationToken cancellationToken = default)
    {
        var tab = new DocumentViewModel("") { WorkspaceRoot = WorkspaceRoot, DisplayName = item.Name ?? "Template.md" };
        EnsureDraftWorkspace(tab);
        var text = await new LibraryContentService().MaterialiseAsync(item.Content, tab.EffectivePath!, tab.AssetWorkspace!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested(); tab.EditorDocument.Text = text;
        Attach(tab); ActiveDocument = tab; await PersistSessionAsync();
        Status = "Opened a new document from " + item.Label + ". Existing tabs are unchanged.";
    }
}
