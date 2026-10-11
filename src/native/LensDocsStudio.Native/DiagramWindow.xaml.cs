using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class DiagramWindow : Window
{
    private readonly Action<string> export;
    public DiagramWindow(DrawingImage drawing, Action<string> export)
    {
        this.export = export;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        DiagramImage.Source = drawing;
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DiagramScale is null) return;
        DiagramScale.ScaleX = DiagramScale.ScaleY = e.NewValue / 100;
        ZoomLabel.Text = Math.Round(e.NewValue) + "%";
    }
    private void FitDiagram(object sender, RoutedEventArgs e)
    {
        if (DiagramImage.Source is null || DiagramScroll.ViewportWidth <= 0 || DiagramScroll.ViewportHeight <= 0) return;
        Zoom.Value = Math.Clamp(Math.Min((DiagramScroll.ViewportWidth - 48) / DiagramImage.Source.Width, (DiagramScroll.ViewportHeight - 48) / DiagramImage.Source.Height) * 100, 10, 400);
    }
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void DiagramMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        Zoom.Value = Math.Clamp(Zoom.Value * (e.Delta > 0 ? 1.1 : 1 / 1.1), 10, 400); e.Handled = true;
    }
    private void DiagramKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.Add: case Key.OemPlus: Zoom.Value = Math.Min(400, Zoom.Value + 10); break;
            case Key.Subtract: case Key.OemMinus: Zoom.Value = Math.Max(10, Zoom.Value - 10); break;
            case Key.D0: Zoom.Value = 100; break;
            default: return;
        }
        e.Handled = true;
    }
    private void CopyImage(object sender, RoutedEventArgs e)
    {
        try
        {
            using var stream = new MemoryStream(NativeSvgService.ToPng(DiagramImage.Source));
            var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); bitmap.Freeze();
            Clipboard.SetImage(bitmap); DiagramStatus.Text = "Diagram image copied to the Windows clipboard.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { DiagramStatus.Text = "The image could not be copied: " + ex.Message; }
    }
    private void Export(object sender, RoutedEventArgs e) => export((string)((FrameworkElement)sender).Tag);
}
