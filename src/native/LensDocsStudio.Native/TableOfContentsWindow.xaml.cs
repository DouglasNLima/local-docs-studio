using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class TableOfContentsWindow : Window
{
    private readonly IReadOnlyList<DocumentHeading> headings;
    public TableOfContentsWindow(IReadOnlyList<DocumentHeading> headings)
    {
        this.headings = headings;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        StartLevel.ItemsSource = EndLevel.ItemsSource = Enumerable.Range(1, 6).ToArray();
        StartLevel.SelectedIndex = 0; EndLevel.SelectedIndex = 5;
        Refresh();
    }
    private void OptionsChanged(object sender, RoutedEventArgs e) => Refresh();
    private void Refresh()
    {
        if (PreviewText is null || CopyButton is null || StartLevel.SelectedItem is not int start || EndLevel.SelectedItem is not int end) return;
        try
        {
            PreviewText.Text = TableOfContentsService.Create(headings, new(start, end, (TableOfContentsStyle)ListStyle.SelectedIndex,
                NormaliseLevels.IsChecked == true, DetailsWrapper.IsChecked == true));
            CopyButton.IsEnabled = PreviewText.Text.Length != 0;
            ResultStatus.Text = CopyButton.IsEnabled ? "Copying does not change your document." : "No headings in the selected levels.";
        }
        catch (ArgumentException error) { PreviewText.Text = ""; CopyButton.IsEnabled = false; ResultStatus.Text = error.Message; }
    }
    private void Copy(object sender, RoutedEventArgs e)
    {
        if (!CopyButton.IsEnabled) return;
        try { Clipboard.SetText(PreviewText.Text); ResultStatus.Text = "Table of contents copied."; }
        catch (System.Runtime.InteropServices.COMException) { ResultStatus.Text = "Clipboard access blocked. Try copying again."; }
    }
}
