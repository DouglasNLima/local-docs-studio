using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private bool updatingOutline;
    private readonly Dictionary<Guid, List<CollapsedHeading>> collapsedOutlineSections = [];
    private readonly List<OutlineHeadingViewModel> outlineHeadings = [];
    private IReadOnlyList<DocumentHeading>? outlineSource;
    private Guid? outlineDocumentId;
    private bool outlineFiltering;
    private sealed record CollapsedHeading(TextAnchor Start, TextAnchor Text, string Title, int Level);
    private bool OutlineCurrent => parsed is not null && parsedVersion is not null
        && parsedDocumentId == model.ActiveDocument?.Id && Editor.Document == model.ActiveDocument?.EditorDocument
        && Editor.Document.Version.CompareAge(parsedVersion) == 0
        && parsed.StandaloneMermaid == (Path.GetExtension(model.ActiveDocument?.EffectivePath ?? model.ActiveDocument?.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid");

    private void InitialiseOutline()
    {
        model.DocumentChanged += OutlineDocumentChanged;
        model.Documents.CollectionChanged += OutlineDocumentsChanged;
        Outline.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OutlineExpansionChanged));
        Outline.AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(OutlineExpansionChanged));
        Outline.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OutlineKeyDown), handledEventsToo: true);
        Outline.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OutlineActivated), handledEventsToo: true);
        Closed += (_, _) =>
        {
            model.DocumentChanged -= OutlineDocumentChanged;
            model.Documents.CollectionChanged -= OutlineDocumentsChanged;
            collapsedOutlineSections.Clear();
        };
        RefreshOutline();
    }

    private void OutlineDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var open = model.Documents.Select(document => document.Id).ToHashSet();
        foreach (var id in collapsedOutlineSections.Keys.Where(id => !open.Contains(id)).ToArray())
            collapsedOutlineSections.Remove(id);
    }

    private void OutlineDocumentChanged(object? sender, EventArgs e) => RefreshOutline();
    private void OutlineFilterChanged(object sender, TextChangedEventArgs e) => RefreshOutline();

    private void OutlineUpdateFailed()
    {
        if (model.ActiveDocument is null || OutlineCurrent) return;
        Outline.IsEnabled = false;
        OutlineEmpty.Text = "Headings could not be updated. Edit the document or refresh its preview to try again.";
        OutlineEmpty.Visibility = Visibility.Visible;
    }

    private void RefreshOutline()
    {
        if (Outline is null || OutlineEmpty is null) return;
        updatingOutline = true;
        try
        {
            CopyTocButton.IsEnabled = model.ActiveDocument is not null;
            if (!OutlineCurrent)
            {
                // A pending parse must never leave outgoing headings actionable on a new buffer.
                Outline.IsEnabled = false; Outline.ItemsSource = null; outlineSource = null;
                outlineHeadings.Clear();
                OutlineEmpty.Text = model.ActiveDocument is null ? "Open a document to view its headings." : "Updating headings…";
                OutlineEmpty.Visibility = Visibility.Visible;
                return;
            }
            Outline.IsEnabled = true;
            if (!ReferenceEquals(outlineSource, parsed!.Headings) || outlineDocumentId != parsedDocumentId)
            {
                outlineSource = parsed.Headings; outlineDocumentId = parsedDocumentId;
                outlineHeadings.Clear();
                var roots = new List<OutlineHeadingViewModel>();
                var ancestors = new Stack<OutlineHeadingViewModel>();
                foreach (var heading in parsed.Headings)
                {
                    while (ancestors.TryPeek(out var parent) && parent.Heading.Level >= heading.Level) ancestors.Pop();
                    var node = new OutlineHeadingViewModel(heading, ancestors.TryPeek(out var ancestor) ? ancestor : null);
                    (node.Parent?.Children ?? roots).Add(node); outlineHeadings.Add(node); ancestors.Push(node);
                }
                var collapsed = CollapsedSections();
                var byStart = outlineHeadings.ToDictionary(node => node.Heading.SourceStart);
                collapsed.RemoveAll(saved => saved.Start.IsDeleted || saved.Text.IsDeleted
                    || !byStart.TryGetValue(saved.Start.Offset, out var node) || node.Title != saved.Title
                    || node.Heading.Level != saved.Level || node.Children.Count == 0);
                Outline.ItemsSource = roots;
            }
            var query = OutlineFilter.Text.Trim(); outlineFiltering = query.Length > 0;
            var closed = CollapsedSections().Where(saved => !saved.Start.IsDeleted).Select(saved => saved.Start.Offset).ToHashSet();
            foreach (var node in outlineHeadings)
            {
                node.IsVisible = !outlineFiltering || node.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase);
                node.IsExpanded = outlineFiltering || !closed.Contains(node.Heading.SourceStart);
            }
            // Matching descendants retain their ancestor path. Filtering never overwrites saved expansion.
            for (var index = outlineHeadings.Count - 1; index >= 0; index--)
                if (outlineHeadings[index] is { IsVisible: true, Parent: { } parent }) parent.IsVisible = true;
            var any = outlineHeadings.Any(node => node.IsVisible);
            OutlineEmpty.Text = outlineHeadings.Count == 0 ? "Add Markdown headings to navigate this document." : "No headings match this filter.";
            OutlineEmpty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            HighlightCurrentHeading();
        }
        finally { updatingOutline = false; }
    }

    private List<CollapsedHeading> CollapsedSections()
    {
        if (model.ActiveDocument is not { } document) return [];
        if (!collapsedOutlineSections.TryGetValue(document.Id, out var sections))
            collapsedOutlineSections[document.Id] = sections = [];
        return sections;
    }

    private void RememberExpansion(OutlineHeadingViewModel node, bool removeExisting = true)
    {
        if (!OutlineCurrent || outlineFiltering || node.Children.Count == 0) return;
        var sections = CollapsedSections();
        if (removeExisting) sections.RemoveAll(saved => saved.Start.IsDeleted || saved.Start.Offset == node.Heading.SourceStart);
        if (node.IsExpanded) return;
        var line = Editor.Document.GetLineByOffset(node.Heading.SourceStart);
        var start = Editor.Document.CreateAnchor(node.Heading.SourceStart);
        start.MovementType = AnchorMovementType.AfterInsertion;
        var text = Editor.Document.CreateAnchor(Math.Min(line.EndOffset, node.Heading.SourceStart + node.Heading.Level + 1));
        text.MovementType = AnchorMovementType.AfterInsertion;
        sections.Add(new(start, text, node.Title, node.Heading.Level));
    }

    private void OutlineExpansionChanged(object sender, RoutedEventArgs e)
    {
        if (updatingOutline || e.OriginalSource is not TreeViewItem { DataContext: OutlineHeadingViewModel node } row) return;
        node.IsExpanded = row.IsExpanded;
        RememberExpansion(node);
    }

    private void HighlightCurrentHeading()
    {
        if (!OutlineCurrent || !OutlinePanel.IsVisible || outlineDocumentId != parsedDocumentId) return;
        var current = outlineHeadings.LastOrDefault(node => node.Heading.SourceStart <= Editor.CaretOffset);
        foreach (var node in outlineHeadings) node.IsCurrent = ReferenceEquals(node, current);
        // Reading/caret updates must not steal the keyboard selection from an outline user.
        if (Outline.IsKeyboardFocusWithin) return;
        while (current is not null && (!current.IsVisible || Ancestors(current).Any(node => !node.IsExpanded))) current = current.Parent;
        var previous = updatingOutline; updatingOutline = true;
        try { foreach (var node in outlineHeadings) node.IsSelected = ReferenceEquals(node, current); }
        finally { updatingOutline = previous; }
    }

    private static IEnumerable<OutlineHeadingViewModel> Ancestors(OutlineHeadingViewModel node)
    { for (var parent = node.Parent; parent is not null; parent = parent.Parent) yield return parent; }

    private void ExpandOutlineGroups(object sender, RoutedEventArgs e) => SetOutlineExpansion(true);
    private void CollapseOutlineGroups(object sender, RoutedEventArgs e) => SetOutlineExpansion(false);
    private void SetOutlineExpansion(bool expanded)
    {
        if (!OutlineCurrent) return;
        updatingOutline = true;
        try
        {
            if (!outlineFiltering) CollapsedSections().Clear();
            // Batch rebuilding avoids rescanning an ever-growing collapse list for each section.
            foreach (var node in outlineHeadings) { node.IsExpanded = expanded; if (!expanded) RememberExpansion(node, removeExisting: false); }
        }
        finally { updatingOutline = false; }
    }

    private void OutlineKeyDown(object sender, KeyEventArgs e)
    {
        // WPF TreeView owns Up/Down, Home/End and parent/child Left/Right navigation,
        // including focus and ExpandCollapse/SelectionItem automation patterns.
        if (e.Key is not (Key.Enter or Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End)
            || !Outline.IsKeyboardFocusWithin || Outline.SelectedItem is not OutlineHeadingViewModel node) return;
        e.Handled = true; NavigateOutlineHeading(node);
    }

    private void OutlineActivated(object sender, MouseButtonEventArgs e)
    {
        // Selection bindings can be applied later when WPF realises/recycles a row.
        // SelectedItemChanged therefore cannot distinguish reading updates from input.
        // Only a header activation navigates; the expander retains its native action.
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject origin
            || Ancestor<System.Windows.Controls.Primitives.ToggleButton>(origin) is not null
            || Ancestor<TreeViewItem>(origin) is not { IsSelected: true, DataContext: OutlineHeadingViewModel node }) return;
        NavigateOutlineHeading(node);
    }

    private void NavigateOutlineHeading(OutlineHeadingViewModel node)
    {
        if (!OutlineCurrent || !outlineHeadings.Contains(node))
        { model.Status = "The outline is updating. Select the heading again shortly."; return; }
        JumpToSource(node.Heading.SourceStart, focusEditor: false);
        if (SyncCurrent && Preview.IsVisible && rendered!.SourceElements.TryGetValue(node.Heading.SourceStart, out var element))
        {
            var owner = model.ActiveDocument; var version = Editor.Document.Version; var result = rendered;
            var generation = viewportGeneration; var synchronisation = syncGeneration;
            result.RevealSource(node.Heading.SourceStart);
            _ = Rendering.PreviewNavigationService.BringIntoViewAsync(Preview, element.ContentStart, null, null,
                () => !closing && model.ActiveDocument == owner && viewportGeneration == generation
                    && syncGeneration == synchronisation && rendered == result && SyncCurrent
                    && Editor.Document == owner?.EditorDocument && Editor.Document.Version.CompareAge(version) == 0);
        }
    }
}
