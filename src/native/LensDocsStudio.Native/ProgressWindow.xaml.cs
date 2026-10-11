using System.Globalization;
using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class ProgressWindow : Window
{
    public ProgressOptions Options { get; private set; } = new();
    public ProgressWindow(ProgressOptions options, bool editing)
    {
        InitializeComponent(); Heading.Text = editing ? "Edit progress indicator" : "Insert progress indicator";
        ProgressLabel.Text = options.Label; PercentageSlider.Value = options.Percentage; ProgressColour.SelectedValue = options.Colour;
        WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { ProgressLabel.Focus(); ProgressLabel.SelectAll(); };
    }
    private void UpdateSlider(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(Percentage.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) && value is >= 0 and <= 100) PercentageSlider.Value = value;
    }
    private void Accept(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProgressLabel.Text)) { ValidationMessage.Text = "Enter a progress label."; ProgressLabel.Focus(); return; }
        if (!int.TryParse(Percentage.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) || value is < 0 or > 100)
        { ValidationMessage.Text = "Enter a whole percentage from 0 to 100."; Percentage.Focus(); return; }
        Options = new(ProgressLabel.Text, value, ProgressColour.SelectedValue as string ?? "accent"); DialogResult = true;
    }
}
