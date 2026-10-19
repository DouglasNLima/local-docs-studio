using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Transient diagnostic shell. No ordinary application state, saving, updater or associations.</summary>
internal sealed class DiagnosticStudio
{
    private sealed record Session(string Name, string Source, string? Path);
    private readonly ExperimentWindow window;
    private readonly TextEditor editor = new() { ShowLineNumbers = true, FontFamily = new("Consolas"), FontSize = 14, Width = 400, Height = 600 };
    private readonly ComboBox documents = new() { Width = 200, DisplayMemberPath = "Name" };
    private readonly ListBox outline = new() { Width = 180, Height = 600, DisplayMemberPath = "Title" };
    private readonly TextBox search = new() { Width = 160 };
    private readonly TextBlock status = new() { Margin = new(8), Text = "EXPERIMENT ONLY — transient source; stock application unchanged" };
    private bool switching;
    private int current = -1;
    private readonly List<Session> sessions = [];
    private readonly Stack<Session> closedSessions = [];
    public DiagnosticStudio(ExperimentWindow window, string source, string? path)
    {
        this.window = window;
        AutomationProperties.SetAutomationId(editor, "ExperimentalSourceEditor");
        AutomationProperties.SetAutomationId(documents, "ExperimentalDocuments");
        AutomationProperties.SetAutomationId(outline, "ExperimentalOutline");
        AutomationProperties.SetAutomationId(search, "ExperimentalSearch");
        var host = new DockPanel(); var tools = new StackPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(documents);
        Add("New document", () => Open("Untitled " + (sessions.Count + 1), "# New document\n", null));
        Add("Close document", CloseDocument);
        Add("Reopen closed", () => { if (closedSessions.TryPop(out var item)) Open(item.Name, item.Source, item.Path); });
        tools.Children.Add(search); Add("Previous match", () => Find(false)); Add("Next match", () => Find(true));
        Add("Copy rendered", async () => await CopyRendered());
        var theme = new ComboBox { ItemsSource = new[] { "Light", "Dark" }, SelectedIndex = 0, Width = 80 }; tools.Children.Add(theme);
        theme.SelectionChanged += (_, _) => _ = Prepare();
        DockPanel.SetDock(tools, Dock.Top); host.Children.Add(tools); DockPanel.SetDock(status, Dock.Bottom); host.Children.Add(status);
        var body = new StackPanel { Orientation = Orientation.Horizontal }; body.Children.Add(editor); body.Children.Add(window.Scroller); body.Children.Add(outline); host.Children.Add(body);
        window.Content = host; window.Scroller.Height = 600;
        documents.SelectionChanged += (_, _) => Switch(documents.SelectedIndex);
        editor.TextChanged += (_, _) => { if (!switching && current >= 0) { sessions[current] = sessions[current] with { Source = editor.Text }; _ = Prepare(); } };
        search.TextChanged += (_, _) => { window.Surface.Search(search.Text); };
        outline.SelectionChanged += (_, _) => { if (outline.SelectedItem is DocumentHeading heading) { window.Surface.NavigateSource(heading.SourceStart); editor.Select(heading.SourceStart, 0); editor.ScrollToLine(heading.SourceLine + 1); } };
        window.Surface.SelectionUpdated += () => { if (window.Surface.Snapshot is null) return; editor.Select(window.Surface.SelectionSourceStart, Math.Max(0, window.Surface.SelectionSourceEnd - window.Surface.SelectionSourceStart)); };
        window.Surface.Activated += node =>
        {
            if (node.Kind == "Link" && node.Target is { } target && !target.StartsWith('#'))
            {
                try
                {
                    if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                    else if (window.SourcePath is { } sourcePath && window.WorkspaceRoot is { } workspace)
                    {
                        var linked = new LensDocsStudio.Core.Workspace.WorkspacePath(workspace).ResolveLink(sourcePath, target);
                        if (new FileInfo(linked).Length > 4 * 1024 * 1024 || Path.GetExtension(linked).ToLowerInvariant() is not (".md" or ".markdown" or ".txt")) throw new IOException("Open a supported Markdown document within the selected workspace.");
                        Open(Path.GetFileName(linked), File.ReadAllText(linked), linked);
                    }
                    else status.Text = "Choose a workspace source path before opening a relative link.";
                }
                catch (Exception error) { status.Text = error.Message; }
            }
        };
        window.Installed += snapshot => { outline.ItemsSource = snapshot.Model.Headings; window.Surface.Search(search.Text); status.Text = $"Complete version {snapshot.Version}; {snapshot.Appearance}; {snapshot.Model.Items.Length} text blocks; source unchanged"; };
        window.CurrentFailure += message => status.Text = message;
        Open(path is null ? "Diagnostic document" : System.IO.Path.GetFileName(path), source, path);

        void Add(string label, Action action) { var button = new Button { Content = label, Margin = new(3) }; AutomationProperties.SetAutomationId(button, "Experimental" + label.Replace(" ", "", StringComparison.Ordinal)); button.Click += (_, _) => action(); tools.Children.Add(button); }
        async Task Prepare()
        {
            try { if (current >= 0) await window.Request(editor.Text, theme.SelectedItem as string ?? "Light"); }
            catch (Exception error) { status.Text = error.Message; }
        }
    }
    private void Open(string name, string source, string? path)
    {
        sessions.Add(new(name, source, path)); documents.ItemsSource = null; documents.ItemsSource = sessions; documents.SelectedIndex = sessions.Count - 1;
    }
    private void Switch(int index)
    {
        if (index < 0 || index >= sessions.Count) return; current = index; switching = true;
        try { editor.Text = sessions[index].Source; window.SourcePath = sessions[index].Path; window.WorkspaceRoot = sessions[index].Path is { } path ? System.IO.Path.GetDirectoryName(path) : null; }
        finally { switching = false; }
        _ = RequestCurrent();
    }
    private async Task RequestCurrent() { try { await window.Request(editor.Text, window.ThemePreference); } catch (Exception error) { status.Text = error.Message; } }
    private void CloseDocument()
    {
        if (current < 0) return;
        if (closedSessions.Count >= 8) { status.Text = "Eight closed diagnostic sources are retained; reopen one before closing another."; return; }
        closedSessions.Push(sessions[current]); sessions.RemoveAt(current); current = -1;
        if (sessions.Count == 0) { Open("Untitled", "", null); return; }
        documents.ItemsSource = null; documents.ItemsSource = sessions; documents.SelectedIndex = 0;
    }
    private void Find(bool next) => window.Surface.FindNext(next);
    private async Task CopyRendered()
    {
        if (window.Surface.Snapshot is not { } snapshot) return;
        var sequence = NativeClipboardService.Sequence; var path = window.SourcePath; var root = window.WorkspaceRoot;
        try
        {
            var payload = await ExperimentWindow.Worker(() => new ClipboardExporter().Export(new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService())
                .Prepare(snapshot.Model.Source, path, root, snapshot.Appearance == "Dark")));
            if (!ReferenceEquals(window.Surface.Snapshot, snapshot)) throw new IOException("The document changed before copy completed.");
            NativeClipboardService.WriteRendered(payload, sequence); status.Text = "Rendered document copied with the existing HTML clipboard/export service";
        }
        catch (Exception error) { status.Text = error.Message; }
    }
}
