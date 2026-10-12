using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class HelpWindow : Window
{
    private bool dark;
    private bool highContrast;
    private readonly MarkdownService markdown = new();
    private readonly NativeMarkdownRenderer renderer = new(new NativeSvgService());

    public HelpWindow(string theme)
    {
        dark = ThemeService.IsDark(theme); highContrast = SystemParameters.HighContrast; InitializeComponent(); WindowStateService.FitInitialWindow(this);
        RefreshTopics(); Loaded += (_, _) => Query.Focus();
    }
    public void ApplyTheme(string theme)
    {
        var changed = ThemeService.IsDark(theme); if (changed == dark && highContrast == SystemParameters.HighContrast) return;
        dark = changed; highContrast = SystemParameters.HighContrast; ShowTopic();
    }
    private void QueryChanged(object sender, TextChangedEventArgs e) { if (Topics is not null) RefreshTopics(); }
    private void RefreshTopics()
    {
        var previous = Topics.SelectedItem as HelpTopic; var matching = NativeHelpGuide.Search(Query.Text);
        Topics.ItemsSource = matching; Topics.SelectedItem = previous is not null && matching.Contains(previous) ? previous : matching.FirstOrDefault();
        if (matching.Count == 0) ShowTopic();
        Status.Text = matching.Count == 0 ? "No matching topics." : $"{matching.Count} {(matching.Count == 1 ? "topic" : "topics")} · Bundled offline guide · Ctrl+F to search";
    }
    private void TopicSelected(object sender, SelectionChangedEventArgs e) => ShowTopic();
    private void ShowTopic()
    {
        var source = (Topics.SelectedItem as HelpTopic)?.Source ?? "No matching topics. Try a shorter phrase.";
        var parsed = markdown.Parse(source);
        ContentViewer.Document = renderer.Render(parsed, dark, new Dictionary<Markdig.Syntax.FencedCodeBlock, LensDocsStudio.Core.Rendering.DiagramResult>(), Navigate, (_, _) => { }).Document;
    }
    private void Navigate(string reference)
    {
        if (!reference.StartsWith('#')) return;
        var target = NativeHelpGuide.Topics.FirstOrDefault(topic => topic.Anchor == Uri.UnescapeDataString(reference[1..]));
        if (target is null) return;
        Query.Clear(); Topics.SelectedItem = target; Topics.ScrollIntoView(target);
    }
    private void GuideKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Query.Focus(); Query.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }
    private void CloseGuide(object sender, RoutedEventArgs e) => Close();
}
