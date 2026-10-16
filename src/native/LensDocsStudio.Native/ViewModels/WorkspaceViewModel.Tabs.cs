using CommunityToolkit.Mvvm.Input;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    [RelayCommand] private async Task TogglePinAsync(DocumentViewModel? tab)
    {
        if (tab is null || !Documents.Contains(tab)) return;
        tab.IsPinned = !tab.IsPinned;
        var selected = ActiveDocument;
        var index = Documents.Count(document => document.IsPinned) - (tab.IsPinned ? 1 : 0);
        Documents.Move(Documents.IndexOf(tab), index); ActiveDocument = selected;
        await PersistSessionAsync();
    }
    [RelayCommand] private Task CloseOthersAsync(DocumentViewModel? tab) => CloseTabSetAsync(tab, false);
    [RelayCommand] private Task CloseToRightAsync(DocumentViewModel? tab) => CloseTabSetAsync(tab, true);
    private async Task CloseTabSetAsync(DocumentViewModel? tab, bool toRight)
    {
        if (tab is null || !Documents.Contains(tab)) return;
        var targets = (toRight ? Documents.Skip(Documents.IndexOf(tab) + 1) : Documents.Where(document => document != tab))
            .Where(document => !document.IsPinned).ToArray();
        foreach (var target in targets)
        {
            if (!Documents.Contains(target)) continue;
            await CloseDocumentAsync(target);
            // Cancelling any save/close prompt cancels the remaining bulk operation.
            if (Documents.Contains(target)) break;
        }
    }
}
