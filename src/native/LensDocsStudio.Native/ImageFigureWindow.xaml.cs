using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class ImageFigureWindow : Window
{
    private readonly ParsedDocument document;
    private readonly int start, length;
    public MarkdownEdit? Edit { get; private set; }
    public ImageFigureWindow(ParsedDocument document, int start, int length)
    {
        this.document = document; this.start = start; this.length = length;
        var existing = ImageFigureEditService.Find(document, start, length) ?? throw new ArgumentException("Choose an existing standalone image or figure.");
        InitializeComponent(); AlternativeText.Text = existing.Options.AlternativeText; Caption.Text = existing.Options.Caption; ImageReference.Text = existing.Reference;
        WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { Caption.Focus(); Caption.SelectAll(); };
    }
    private void Accept(object sender, RoutedEventArgs e)
    {
        try { Edit = ImageFigureEditService.Plan(document, start, length, new(AlternativeText.Text, Caption.Text)); ValidationMessage.Text = ""; DialogResult = true; }
        catch (ArgumentException ex) { ValidationMessage.Text = ex.Message; }
    }
}
