using System.Windows;
using System.Windows.Input;
using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void CopyDocumentTableOfContents(object sender, RoutedEventArgs e)
    {
        if (closing || model.ActiveDocument is not { } tab) return;
        if (tab.EditorDocument.TextLength > DocumentReviewService.MaximumSourceCharacters)
        { model.Status = "Table of contents supports up to 4 million source characters."; return; }
        var document = tab.EditorDocument;
        var snapshot = document.CreateSnapshot();
        var identity = tab.EffectivePath ?? tab.DisplayName;
        var standalone = Path.GetExtension(identity)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        bool Current() => !closing && model.ActiveDocument == tab && tab.EditorDocument == document
            && document.Version.CompareAge(snapshot.Version) == 0 && (tab.EffectivePath ?? tab.DisplayName) == identity;
        var dialog = new TableOfContentsWindow(snapshot, standalone, Current, OutlineCurrent ? parsed!.Headings : null) { Owner = this };
        void Changed(object? sender, EventArgs args) { if (!Current()) dialog.InvalidateSource(); }
        var focus = Keyboard.FocusedElement;
        document.TextChanged += Changed;
        model.DocumentChanged += Changed;
        try { dialog.ShowDialog(); }
        finally
        {
            document.TextChanged -= Changed; model.DocumentChanged -= Changed;
            if (Current() && focus is UIElement { IsVisible: true, IsEnabled: true } element) element.Focus();
        }
    }
}
