using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MermaidDiagramPickerWindow : Window
{
    private readonly DiagramService diagrams;
    private readonly NativeSvgService svg = new();
    private readonly SemaphoreSlim previewGate = new(1, 1);
    private bool dark;
    private readonly string diagramTheme;
    private CancellationTokenSource? preview;
    private bool ready, closed, confirmed;
    private MermaidDiagramFamily? preferredFamily;
    private readonly Dictionary<MermaidDiagramFamily, string> preferredVariants = [];
    public MermaidDiagramDefinition? SelectedDefinition { get; private set; }
    public MermaidStarterVariant? SelectedVariant { get; private set; }
    public bool InsertAsCode { get; private set; }

    public MermaidDiagramPickerWindow(DiagramService diagrams, string theme, string diagramTheme, string targetDescription)
    {
        this.diagrams = diagrams; this.diagramTheme = diagramTheme; dark = ThemeService.IsDark(theme);
        InitializeComponent(); ThemeService.Apply(Resources, theme); WindowStateService.FitInitialWindow(this);
        TargetStatus.Text = targetDescription;
        Category.ItemsSource = new[] { "All categories" }.Concat(MermaidDiagramCatalogue.All.Select(item => item.Category).Distinct());
        Category.SelectedIndex = 0; ready = true; Filter();
        Loaded += (_, _) => Search.Focus();
        Closed += (_, _) => { closed = true; preview?.Cancel(); preview?.Dispose(); Sample.Source = null; };
    }
    public void ApplyTheme(string theme)
    {
        if (closed) return;
        ThemeService.Apply(Resources, theme); dark = ThemeService.IsDark(theme);
        AutomationProperties.SetItemStatus(this, theme);
        if (ready) UpdateSample();
    }
    private void FilterChanged(object sender, RoutedEventArgs e) { if (ready) Filter(); }
    private void ClearSearch(object sender, RoutedEventArgs e) { Search.Clear(); Category.SelectedIndex = 0; Search.Focus(); }
    private void Filter()
    {
        var shown = MermaidDiagramCatalogue.Search(Search.Text, Category.SelectedIndex > 0 ? Category.SelectedItem as string : null);
        ready = false;
        Families.ItemsSource = shown;
        Families.SelectedItem = shown.FirstOrDefault(item => item.Family == preferredFamily) ?? shown.FirstOrDefault();
        ready = true; SelectFamily(false);
    }
    private void FamilyChanged(object sender, SelectionChangedEventArgs e) { if (ready) SelectFamily(true); }
    private void SelectFamily(bool remember)
    {
        ready = false;
        if (Families.SelectedItem is MermaidDiagramDefinition definition)
        {
            if (remember) preferredFamily = definition.Family;
            FamilyName.Text = definition.Name; Description.Text = definition.Description;
            Variants.ItemsSource = definition.Variants;
            Variants.SelectedItem = definition.Variants.FirstOrDefault(item => preferredVariants.GetValueOrDefault(definition.Family) == item.Id) ?? definition.SuggestedVariant;
        }
        else { FamilyName.Text = "No matching families"; Description.Text = "Clear the search or choose another category."; Variants.ItemsSource = null; }
        ready = true; UpdateSample();
    }
    private void VariantChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        if (Families.SelectedItem is MermaidDiagramDefinition definition && Variants.SelectedItem is MermaidStarterVariant variant)
            preferredVariants[definition.Family] = variant.Id;
        UpdateSample();
    }
    private async void UpdateSample()
    {
        preview?.Cancel(); preview?.Dispose(); preview = new(); var token = preview.Token;
        CreateButton.IsEnabled = false; Sample.Source = null; SampleSource.Clear(); VariantNote.Text = "";
        if (Families.SelectedItem is not MermaidDiagramDefinition definition || Variants.SelectedItem is not MermaidStarterVariant variant)
        { ResultStatus.Text = "No matching diagram families. Clear search to restore all 24."; return; }
        SampleSource.Text = variant.Source; VariantNote.Text = variant.Note;
        ResultStatus.Text = $"{Families.Items.Count} of 24 families · Checking {definition.Name} example…";
        var acquired = false;
        try
        {
            await Task.Delay(100, token);
            await previewGate.WaitAsync(token); acquired = true;
            var result = await Task.Run(async () =>
            {
                var error = await definition.ValidateAsync(variant.Source, diagrams, token);
                token.ThrowIfCancellationRequested();
                if (error is not null) throw new ArgumentException(error);
                var render = diagrams.Render(variant.Source, dark, diagramTheme, token);
                if (!render.Success) throw new ArgumentException(render.Error);
                // The established empty Flowchart starter intentionally has no native geometry.
                if (definition.Family == MermaidDiagramFamily.Flowchart && variant.Id == "default") return null;
                return svg.RenderCanonical(render, svg.Prepare(render.Svg!), cancellationToken: token);
            }, token);
            if (closed || token.IsCancellationRequested) return;
            Sample.Source = result; SampleSource.Visibility = Visibility.Visible; CreateButton.IsEnabled = true;
            if (result is null) { SourcePanel.IsExpanded = true; VariantNote.Text = "Empty canvas: add your first node in the visual editor. The source below is the exact starting point."; }
            ResultStatus.Text = $"{Families.Items.Count} of 24 families · Example ready. Create visually opens a private draft.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!closed && !token.IsCancellationRequested)
                ResultStatus.Text = "Example unavailable; use the source alternative. " + error.Message;
        }
        finally { if (acquired) previewGate.Release(); }
    }
    private void Create(object sender, RoutedEventArgs e)
    {
        if (confirmed || !CreateButton.IsEnabled || Families.SelectedItem is not MermaidDiagramDefinition definition || Variants.SelectedItem is not MermaidStarterVariant variant) return;
        confirmed = true; SelectedDefinition = definition; SelectedVariant = variant; DialogResult = true;
    }
    private void InsertCode(object sender, RoutedEventArgs e) { if (confirmed) return; confirmed = true; InsertAsCode = true; DialogResult = true; }
    private void FamilyDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(Families, e.OriginalSource as DependencyObject) is ListBoxItem) Create(sender, e);
    }
    private void PickerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && Search.IsKeyboardFocusWithin) { Families.Focus(); if (Families.SelectedItem is { } item) (Families.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem)?.Focus(); e.Handled = true; }
        if (e.Key == Key.Enter && Families.IsKeyboardFocusWithin) { Create(sender, e); e.Handled = true; }
    }
}
