using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native;

public partial class DiagramWindow : Window
{
    private readonly Action<string> export;
    private Point? panOrigin;
    private double panHorizontal, panVertical;
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
        DiagramScroll.ScrollToHorizontalOffset(0); DiagramScroll.ScrollToVerticalOffset(0);
    }
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void ResetView(object sender, RoutedEventArgs e)
    { EndPan(); Zoom.Value = 100; DiagramScroll.ScrollToHorizontalOffset(0); DiagramScroll.ScrollToVerticalOffset(0); }
    private void DiagramMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var point = e.GetPosition(DiagramScroll); var before = Zoom.Value / 100;
        var next = Math.Clamp(Zoom.Value * (e.Delta > 0 ? 1.1 : 1 / 1.1), 10, 400);
        var horizontal = DiagramViewportMath.ZoomOffset(DiagramScroll.HorizontalOffset, point.X, DiagramScroll.Padding.Left, before, next / 100);
        var vertical = DiagramViewportMath.ZoomOffset(DiagramScroll.VerticalOffset, point.Y, DiagramScroll.Padding.Top, before, next / 100);
        Zoom.Value = next; DiagramScroll.UpdateLayout();
        DiagramScroll.ScrollToHorizontalOffset(horizontal); DiagramScroll.ScrollToVerticalOffset(vertical); e.Handled = true;
    }
    private void PanStarted(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle && !(e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space))) return;
        panOrigin = e.GetPosition(DiagramScroll); panHorizontal = DiagramScroll.HorizontalOffset; panVertical = DiagramScroll.VerticalOffset;
        DiagramScroll.CaptureMouse(); DiagramScroll.Cursor = Cursors.SizeAll; e.Handled = true;
    }
    private void PanMoved(object sender, MouseEventArgs e)
    {
        if (panOrigin is not { } origin) return;
        if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed) { EndPan(); return; }
        var point = e.GetPosition(DiagramScroll);
        DiagramScroll.ScrollToHorizontalOffset(DiagramViewportMath.PanOffset(panHorizontal, origin.X, point.X, DiagramScroll.ExtentWidth, DiagramScroll.ViewportWidth));
        DiagramScroll.ScrollToVerticalOffset(DiagramViewportMath.PanOffset(panVertical, origin.Y, point.Y, DiagramScroll.ExtentHeight, DiagramScroll.ViewportHeight));
        e.Handled = true;
    }
    private void PanFinished(object sender, MouseButtonEventArgs e) { if (panOrigin is null) return; EndPan(); e.Handled = true; }
    private void PanCaptureLost(object sender, MouseEventArgs e) => EndPan();
    private void EndPan()
    {
        panOrigin = null; if (DiagramScroll.IsMouseCaptured) DiagramScroll.ReleaseMouseCapture();
        DiagramScroll.Cursor = Keyboard.IsKeyDown(Key.Space) ? Cursors.Hand : Cursors.Arrow;
    }
    private void DiagramKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Space && panOrigin is null) DiagramScroll.Cursor = Cursors.Arrow; }
    private void DiagramKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (panOrigin is not null) EndPan(); else Close(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Space) { DiagramScroll.Cursor = Cursors.Hand; e.Handled = true; return; }
            if (e.Key == Key.F) { FitDiagram(sender, e); e.Handled = true; return; }
            if (e.Key == Key.Home) { ResetView(sender, e); e.Handled = true; return; }
            if (Keyboard.FocusedElement is not Slider)
            {
                if (e.Key is Key.Left or Key.Right) { DiagramScroll.ScrollToHorizontalOffset(DiagramScroll.HorizontalOffset + (e.Key == Key.Left ? -32 : 32)); e.Handled = true; return; }
                if (e.Key is Key.Up or Key.Down) { DiagramScroll.ScrollToVerticalOffset(DiagramScroll.VerticalOffset + (e.Key == Key.Up ? -32 : 32)); e.Handled = true; return; }
            }
        }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.Add: case Key.OemPlus: Zoom.Value = Math.Min(400, Zoom.Value + 10); break;
            case Key.Subtract: case Key.OemMinus: Zoom.Value = Math.Max(10, Zoom.Value - 10); break;
            case Key.D0: Zoom.Value = 100; break;
            case Key.C: CopyImage(sender, e); break;
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
