using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LensDocsStudio.Native.Controls;

namespace LensDocsStudio.Native;

public partial class MermaidVisualEditorWindow
{
    // Presentation never interprets CSS. The transformation service still validates the authoritative text.
    private void UpdateColourSwatches()
    {
        foreach (var (field, swatch, label) in new[] { (FillColour, FillSwatch, "Fill"), (StrokeColour, StrokeSwatch, "Stroke"), (TextColour, TextSwatch, "Text") })
        {
            var value = field.Text;
            var valid = value.Length == 7 && value[0] == '#' && value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") == false;
            swatch.Background = valid && !highContrast ? new SolidColorBrush(Color.FromRgb(Convert.ToByte(value[1..3], 16), Convert.ToByte(value[3..5], 16), Convert.ToByte(value[5..7], 16))) : BrushResource("WindowBrush");
            swatch.ToolTip = valid ? label + " colour: " + value : "Enter a six-digit hexadecimal colour, for example #FF883E.";
            field.SetResourceReference(Control.BorderBrushProperty, valid ? "BorderBrush" : "AccentBrush");
            System.Windows.Automation.AutomationProperties.SetHelpText(field, valid ? "Six-digit hexadecimal colour" : "Invalid colour. Use # followed by six hexadecimal digits.");
        }
    }
    private void ZoomOut(object sender, RoutedEventArgs e) => Zoom.Value = Math.Max(Zoom.Minimum, Zoom.Value / 1.15);
    private void ZoomIn(object sender, RoutedEventArgs e) => Zoom.Value = Math.Min(Zoom.Maximum, Zoom.Value * 1.15);

    private Button ConnectionPort(string nodeId, FrameworkElement nodeButton)
    {
        var port = new Button { Content = new CommandIcon { Kind = "Connection", Width = 18, Height = 18 }, Width = 32, Height = 32,
            MinHeight = 32, Margin = new(0), Style = (Style)FindResource("Mermaid.Port"), ToolTip = "Connect from " + nodeId + "; choose a destination, Escape cancels", IsEnabled = draft.Model.CanEdit };
        nodeButton.MouseEnter += (_, _) => port.Tag = true;
        nodeButton.MouseLeave += (_, _) => port.Tag = draft.Selection.Contains("node:" + nodeId) || connectionSource == nodeId;
        return port;
    }
}
