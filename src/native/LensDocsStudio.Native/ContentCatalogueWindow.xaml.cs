using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class ContentCatalogueWindow : Window
{
    private readonly Dictionary<string, Dictionary<string, string>> values = new(StringComparer.Ordinal);
    private readonly DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
    private bool ready, buildingFields;
    private LocalLibraryItem? generated;
    public LocalLibraryItem? SelectedItem { get; private set; }
    public LocalLibraryKind SelectedKind { get; private set; }

    public ContentCatalogueWindow(bool snippets = false)
    {
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        Category.ItemsSource = new[] { "All categories" }.Concat(BuiltInContentCatalogue.Entries.Select(entry => entry.Group).Distinct()).ToArray();
        Category.SelectedIndex = 0; Kind.SelectedIndex = snippets ? 1 : 0;
        ready = true; Populate();
    }
    private void FilterChanged(object sender, RoutedEventArgs e) { if (ready) Populate(); }
    private void Populate()
    {
        var previous = (Entries.SelectedItem as CatalogueEntry)?.Id;
        var category = Category.SelectedIndex == 0 ? null : Category.SelectedItem as string;
        var query = Filter.Text.Trim();
        var shown = BuiltInContentCatalogue.Entries.Where(entry => entry.IsSnippet == (Kind.SelectedIndex == 1)
            && (category is null || category == entry.Group)
            && (entry.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.Group.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToArray();
        Entries.ItemsSource = shown; Entries.SelectedItem = shown.FirstOrDefault(entry => entry.Id == previous) ?? shown.FirstOrDefault();
        BuildFields();
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ready) BuildFields(); }
    private void BuildFields()
    {
        buildingFields = true; Fields.Children.Clear();
        if (Entries.SelectedItem is CatalogueEntry entry)
        {
            if (!values.TryGetValue(entry.Id, out var metadata)) values.Add(entry.Id, metadata = new(BuiltInContentCatalogue.Defaults(entry, today), StringComparer.Ordinal));
            foreach (var field in entry.Fields)
            {
                var input = new TextBox { Text = metadata[field.Key], MaxLength = BuiltInContentCatalogue.MaximumFieldCharacters,
                    Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6), Tag = field.Key };
                AutomationProperties.SetName(input, field.Label); AutomationProperties.SetAutomationId(input, "CatalogueField-" + field.Key);
                var label = new Label { Content = field.Label, Target = input, Padding = new Thickness(0, 0, 0, 3) };
                input.TextChanged += (_, _) => { if (!buildingFields) { metadata[field.Key] = input.Text; UpdatePreview(); } };
                Fields.Children.Add(label); Fields.Children.Add(input);
            }
        }
        FieldsScroll.Visibility = Fields.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        buildingFields = false; UpdatePreview();
    }
    private void UpdatePreview()
    {
        generated = null; UseButton.IsEnabled = false; Preview.Clear();
        if (Entries.SelectedItem is not CatalogueEntry entry) { Status.Text = "No matching templates or snippets."; return; }
        UseButton.Content = entry.IsSnippet ? "Insert snippet" : "Create document";
        try
        {
            generated = BuiltInContentCatalogue.Generate(entry, values[entry.Id], today); Preview.Text = generated.Content;
            Status.Text = entry.IsSnippet ? entry.Format == "mermaid" ? "Insert into an existing diagram of the matching type. One Undo restores the original source."
                : "Insert at the current selection. One Undo restores the original source."
                : "New document: " + generated.Name + ". Your current tabs stay open. Blank fields use the template defaults.";
            UseButton.IsEnabled = true;
        }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
        catch (InvalidDataException ex) { Status.Text = ex.Message; }
    }
    private void UseSelected(object sender, RoutedEventArgs e)
    {
        if (generated is null || Entries.SelectedItem is not CatalogueEntry entry) return;
        SelectedItem = generated; SelectedKind = entry.IsSnippet ? LocalLibraryKind.Snippet : LocalLibraryKind.Template; DialogResult = true;
    }
}
