using System.Windows;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private sealed record OutlineEntry(DocumentHeading Heading, bool HasChildren, bool Expanded)
    {
        public string Title => Heading.Title;
        public int Level => Heading.Level;
        public int SourceStart => Heading.SourceStart;
        public string Anchor => Heading.Anchor;
        public string ToggleLabel => (Expanded ? "Collapse section " : "Expand section ") + Title;
        public string Glyph => Expanded ? "▾" : "▸";
        public Visibility ToggleVisibility => HasChildren ? Visibility.Visible : Visibility.Hidden;
    }

    private void ToggleOutlineBranch(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: OutlineEntry entry }) return;
        if (!CollapsedOutlineSections.Add(entry.Anchor)) CollapsedOutlineSections.Remove(entry.Anchor);
        e.Handled = true; RefreshOutline(); Outline.Focus();
    }

    private async void CopyDocumentTableOfContents(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } tab) return;
        if (tab.EditorDocument.TextLength > DocumentReviewService.MaximumSourceCharacters)
        { model.Status = "Table of contents supports up to 4 million source characters."; return; }
        var source = tab.EditorDocument.Text; var version = tab.EditorDocument.Version;
        var standalone = Path.GetExtension(tab.EffectivePath ?? tab.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        await model.RunAsync(async () =>
        {
            var current = await Task.Run(() => new MarkdownService().Parse(source, standalone), model.OperationToken);
            if (closing || model.ActiveDocument != tab || tab.EditorDocument.Version.CompareAge(version) != 0) return;
            new TableOfContentsWindow(current.Headings) { Owner = this }.ShowDialog();
        });
    }
}
