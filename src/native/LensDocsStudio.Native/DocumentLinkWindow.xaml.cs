using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class DocumentLinkWindow : Window
{
    private sealed record Candidate(string Path, string RelativePath);
    private sealed record SectionChoice(string Title, string? Anchor);
    private readonly Candidate[] candidates;
    private readonly WorkspacePath workspace;
    private readonly string sourcePath;
    private readonly string recoveryDirectory;
    private readonly IReadOnlyDictionary<string, string>? editedDocuments;
    private CancellationTokenSource? loading;
    public string? MarkdownLink { get; private set; }

    public DocumentLinkWindow(WorkspacePath workspace, string sourcePath, IReadOnlyList<WorkspaceEntry> entries, string recoveryDirectory, IReadOnlyDictionary<string, string>? editedDocuments = null)
    {
        this.workspace = workspace; this.sourcePath = sourcePath; this.recoveryDirectory = recoveryDirectory;
        this.editedDocuments = editedDocuments;
        candidates = Flatten(entries).Select(e => new Candidate(e.Path, Path.GetRelativePath(workspace.Root, e.Path))).ToArray();
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        DocumentsList.ItemsSource = candidates;
        StatusText.Text = candidates.Length + " documents";
        Loaded += (_, _) => FilterText.Focus();
        Closed += (_, _) => { loading?.Cancel(); loading?.Dispose(); };
    }
    private static IEnumerable<WorkspaceEntry> Flatten(IEnumerable<WorkspaceEntry> entries) =>
        entries.SelectMany(e => e.IsDirectory ? Flatten(e.Children) : [e]);
    private void FilterChanged(object sender, TextChangedEventArgs e)
    {
        if (DocumentsList is null) return;
        var words = FilterText.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = candidates.Where(c => words.All(w => c.RelativePath.Contains(w, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        DocumentsList.ItemsSource = matches;
        StatusText.Text = matches.Length == 0 ? "No matching documents" : matches.Length + " documents";
    }
    private async void DocumentSelected(object sender, SelectionChangedEventArgs e)
    {
        loading?.Cancel(); loading?.Dispose(); loading = new();
        var token = loading.Token;
        InsertButton.IsEnabled = DocumentsList.SelectedItem is Candidate;
        SectionList.ItemsSource = new[] { new SectionChoice("Whole document", null) }; SectionList.SelectedIndex = 0;
        if (DocumentsList.SelectedItem is not Candidate selected) return;
        try
        {
            var source = editedDocuments?.TryGetValue(selected.Path, out var current) == true ? current : (await new DocumentStore(recoveryDirectory).ReadAsync(selected.Path, token)).Text;
            var standalone = Path.GetExtension(selected.Path).ToLowerInvariant() is ".mmd" or ".mermaid";
            var targets = await Task.Run(() => new MarkdownService().Parse(source, standalone).Targets, token);
            if (token.IsCancellationRequested || !ReferenceEquals(DocumentsList.SelectedItem, selected)) return;
            SectionList.ItemsSource = new[] { new SectionChoice("Whole document", null) }.Concat(targets.Select(t => new SectionChoice(
                t.HeadingLevel == 0 ? "Anchor: " + t.Title : t.Title, t.Id))).ToArray();
            SectionList.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (!token.IsCancellationRequested) StatusText.Text = ex.Message; }
    }
    private void InsertSelected(object sender, RoutedEventArgs e)
    {
        if (DocumentsList.SelectedItem is not Candidate selected) return;
        try
        {
            MarkdownLink = DocumentLinks.Create(workspace, sourcePath, selected.Path, LinkLabel.Text, (SectionList.SelectedItem as SectionChoice)?.Anchor);
            DialogResult = true;
        }
        catch (IOException ex) { StatusText.Text = ex.Message; }
    }
    private void FilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || DocumentsList.Items.Count == 0) return;
        DocumentsList.SelectedIndex = 0; DocumentsList.Focus(); e.Handled = true;
    }
    private void ListKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { InsertSelected(sender, e); e.Handled = true; } }
}
