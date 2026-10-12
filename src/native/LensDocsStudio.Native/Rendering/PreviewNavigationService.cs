using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace LensDocsStudio.Native.Rendering;

public static class PreviewNavigationService
{
    public static async Task BringIntoViewAsync(FlowDocumentScrollViewer viewer, TextPointer position)
    {
        var document = viewer.Document;
        if (!position.IsInSameDocument(document.ContentStart)) return;
        await viewer.Dispatcher.InvokeAsync(() =>
        {
            if (viewer.Document != document || !position.IsInSameDocument(document.ContentStart)) return;
            viewer.UpdateLayout();
            ((FrameworkContentElement?)position.Paragraph ?? position.Parent as FrameworkContentElement)?.BringIntoView();
            viewer.UpdateLayout();
            var rectangle = position.GetCharacterRect(LogicalDirection.Forward);
            if (rectangle.IsEmpty || viewer.Template.FindName("PART_ContentHost", viewer) is not ScrollViewer scrolling) return;
            scrolling.ScrollToVerticalOffset(Math.Max(0, scrolling.VerticalOffset + rectangle.Top - scrolling.ViewportHeight / 3));
        }, DispatcherPriority.Loaded);
    }
}
