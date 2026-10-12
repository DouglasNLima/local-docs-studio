using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class DetailsWindow : Window
{
    private readonly ParsedDocument document;
    private readonly int start, length;
    private CancellationTokenSource? operation;
    private bool closed;
    public MarkdownEdit? Edit { get; private set; }

    public DetailsWindow(ParsedDocument document, int start, int length)
    {
        this.document = document; this.start = start; this.length = length;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        var options = DetailsEditService.ReadOptions(document, start, length);
        Heading.Text = DetailsEditService.Find(document, start, length) is null ? "Insert details section" : "Edit details section";
        Summary.Text = options.Summary; Body.Text = options.Body; Expanded.IsChecked = options.InitiallyExpanded;
        Loaded += (_, _) => { Summary.Focus(); Summary.SelectAll(); };
        Closed += (_, _) => { closed = true; operation?.Cancel(); };
    }
    private async void Accept(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        var options = new DetailsOptions(Summary.Text, Body.Text, Expanded.IsChecked == true);
        operation = new(); var token = operation.Token;
        ApplyButton.IsEnabled = Summary.IsEnabled = Body.IsEnabled = Expanded.IsEnabled = false;
        Status.Text = "Checking details source…";
        try
        {
            var edit = await Task.Run(() => DetailsEditService.Plan(document, start, length, options), token);
            token.ThrowIfCancellationRequested(); if (closed) return;
            Edit = edit; DialogResult = true;
        }
        catch (OperationCanceledException) { if (!closed) Status.Text = "Details edit cancelled."; }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException) { if (!closed) Status.Text = ex.Message; }
        finally
        {
            operation.Dispose(); operation = null;
            if (!closed) ApplyButton.IsEnabled = Summary.IsEnabled = Body.IsEnabled = Expanded.IsEnabled = true;
        }
    }
}
