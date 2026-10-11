using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class AnchorWindow : Window
{
    private readonly ParsedDocument document;
    private readonly int start, length;
    public MarkdownEdit? Edit { get; private set; }
    public AnchorWindow(ParsedDocument document, int start, int length)
    {
        this.document = document; this.start = start; this.length = length;
        InitializeComponent();
        var existing = AnchorEditService.Find(document, start, length);
        AnchorId.Text = existing?.Id ?? "section-anchor";
        AnchorLabel.Text = existing?.Text ?? (length is > 0 and <= 512 ? document.Source.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ') : "");
        if (existing is not null)
        {
            Heading.Text = "Edit document anchor"; AnchorId.IsReadOnly = true;
            IdentifierHint.Text = "The identifier is retained so existing links keep working.";
        }
        WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { if (existing is null) { AnchorId.Focus(); AnchorId.SelectAll(); } else { AnchorLabel.Focus(); AnchorLabel.SelectAll(); } };
    }
    private void Accept(object sender, RoutedEventArgs e)
    {
        try { Edit = AnchorEditService.Plan(document, start, length, new(AnchorId.Text, AnchorLabel.Text)); DialogResult = true; }
        catch (ArgumentException ex) { ValidationMessage.Text = ex.Message.Split(" (Parameter", StringSplitOptions.None)[0]; }
    }
    private void ClearValidation(object sender, System.Windows.Controls.TextChangedEventArgs e)
    { if (ValidationMessage is not null) ValidationMessage.Text = ""; }
}
