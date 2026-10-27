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
    private bool panning;
    private MouseButton? panButton;
    private readonly DiagramInspection? inspection;
    private string? selected, hovered;
    private sealed record InspectionChoice(string? Identity, string Description);
    public DiagramWindow(DrawingImage drawing, Action<string> export, DiagramInspection? inspection = null)
    {
        this.export = export;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        DiagramImage.Source = drawing;
        this.inspection = ReferenceEquals(inspection?.Drawing, drawing) ? inspection : null;
        InspectionOverlay.Inspection = this.inspection;
        if (this.inspection is { } view)
        {
            InspectionChoices.ItemsSource = new[] { new InspectionChoice(null, "Clear inspection selection") }
                .Concat(view.Entities.Select(item => new InspectionChoice(item.Identity, "Entity · " + item.Description)))
                .Concat(view.Connections.Select(item => new InspectionChoice(item.Identity, "Relationship · " + item.Description))).ToArray();
            InspectionChoices.Visibility = Visibility.Visible;
        }
        Deactivated += (_, _) => EndPan();
        Closed += (_, _) => EndPan();
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DiagramScale is null) return;
        DiagramScale.ScaleX = DiagramScale.ScaleY = e.NewValue / 100;
        UpdateInspection();
        ZoomLabel.Text = (e.NewValue < 1 ? e.NewValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : Math.Round(e.NewValue).ToString()) + "%";
    }
    private void FitDiagram(object sender, RoutedEventArgs e)
    {
        if (DiagramImage.Source is null || DiagramScroll.ViewportWidth <= 0 || DiagramScroll.ViewportHeight <= 0) return;
        var fit = Math.Min((DiagramScroll.ViewportWidth - 48) / DiagramImage.Source.Width, (DiagramScroll.ViewportHeight - 48) / DiagramImage.Source.Height) * 100;
        if (fit <= 0 || !double.IsFinite(fit)) return;
        Zoom.Minimum = Math.Min(1, fit); Zoom.Value = Math.Min(fit, Zoom.Maximum);
        DiagramScroll.ScrollToHorizontalOffset(0); DiagramScroll.ScrollToVerticalOffset(0);
    }
    private void InitialView(object sender, RoutedEventArgs e)
    {
        FitDiagram(sender, e);
        if (Zoom.Value < 60)
        {
            Zoom.Value = 100;
            DiagramStatus.Text = "Opened at actual size for readable labels. Fit diagram shows the whole drawing. Left-drag pans; Ctrl+wheel zooms.";
        }
        else if (Zoom.Value > 100) Zoom.Value = 100;
    }
    private void FitWidth(object sender, RoutedEventArgs e)
    {
        if (DiagramImage.Source is null || DiagramScroll.ViewportWidth <= 48) return;
        var fit = (DiagramScroll.ViewportWidth - 48) / DiagramImage.Source.Width * 100;
        Zoom.Minimum = Math.Min(1, fit); Zoom.Value = Math.Min(fit, Zoom.Maximum);
        DiagramScroll.ScrollToHorizontalOffset(0); DiagramScroll.ScrollToVerticalOffset(0);
    }
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void ResetView(object sender, RoutedEventArgs e)
    { EndPan(); Zoom.Value = 100; DiagramScroll.ScrollToHorizontalOffset(0); DiagramScroll.ScrollToVerticalOffset(0); }
    private void DiagramMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var point = e.GetPosition(DiagramScroll); var anchor = e.GetPosition(DiagramImage);
        Zoom.Value = Math.Clamp(Zoom.Value * (e.Delta > 0 ? 1.1 : 1 / 1.1), Zoom.Minimum, Zoom.Maximum);
        DiagramScroll.UpdateLayout(); var after = DiagramImage.TranslatePoint(anchor, DiagramScroll);
        DiagramScroll.ScrollToHorizontalOffset(DiagramScroll.HorizontalOffset + after.X - point.X);
        DiagramScroll.ScrollToVerticalOffset(DiagramScroll.VerticalOffset + after.Y - point.Y); e.Handled = true;
    }
    private void PanStarted(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Middle)) return;
        for (var target = e.OriginalSource as DependencyObject; target is not null && target != DiagramScroll; target = VisualTreeHelper.GetParent(target))
            if (target is System.Windows.Controls.Primitives.ScrollBar) return;
        DiagramScroll.Focus(); panButton = e.ChangedButton;
        panning = e.ChangedButton == MouseButton.Middle || Keyboard.IsKeyDown(Key.Space);
        panOrigin = e.GetPosition(DiagramScroll); panHorizontal = DiagramScroll.HorizontalOffset; panVertical = DiagramScroll.VerticalOffset;
        if (!DiagramScroll.CaptureMouse()) { EndPan(); return; }
        DiagramScroll.Cursor = panning ? Cursors.SizeAll : Cursors.Arrow; e.Handled = true;
    }
    private void PanMoved(object sender, MouseEventArgs e)
    {
        if (panOrigin is not { } origin)
        {
            var hit = inspection?.Hit(e.GetPosition(DiagramImage), Zoom.Value / 100);
            if (hovered != hit) { hovered = hit; UpdateInspection(); }
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed) { EndPan(); return; }
        var point = e.GetPosition(DiagramScroll);
        if (!panning && !DiagramViewportMath.BeginsPan(point.X - origin.X, point.Y - origin.Y)) return;
        panning = true; hovered = null; DiagramScroll.Cursor = Cursors.SizeAll; UpdateInspection();
        DiagramScroll.ScrollToHorizontalOffset(DiagramViewportMath.PanOffset(panHorizontal, origin.X, point.X, DiagramScroll.ExtentWidth, DiagramScroll.ViewportWidth));
        DiagramScroll.ScrollToVerticalOffset(DiagramViewportMath.PanOffset(panVertical, origin.Y, point.Y, DiagramScroll.ExtentHeight, DiagramScroll.ViewportHeight));
        e.Handled = true;
    }
    private void PanFinished(object sender, MouseButtonEventArgs e)
    {
        if (panOrigin is null || e.ChangedButton != panButton) return;
        var point = e.GetPosition(DiagramScroll);
        var click = !panning && panButton == MouseButton.Left && !DiagramViewportMath.BeginsPan(point.X - panOrigin.Value.X, point.Y - panOrigin.Value.Y);
        var inside = new Rect(0, 0, DiagramScroll.ViewportWidth, DiagramScroll.ViewportHeight).Contains(point);
        var hit = click && inside ? inspection?.Hit(e.GetPosition(DiagramImage), Zoom.Value / 100) : null;
        EndPan();
        if (click && inside) SelectInspection(hit);
        e.Handled = true;
    }
    private void PanCaptureLost(object sender, MouseEventArgs e) => EndPan();
    private void EndPan()
    {
        panOrigin = null; panButton = null; panning = false; if (DiagramScroll.IsMouseCaptured) DiagramScroll.ReleaseMouseCapture();
        DiagramScroll.Cursor = Keyboard.IsKeyDown(Key.Space) ? Cursors.Hand : Cursors.Arrow;
    }
    private void DiagramKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Space && panOrigin is null) DiagramScroll.Cursor = Cursors.Arrow; }
    private void DiagramKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && InspectionChoices.IsDropDownOpen) return;
        if (e.Key == Key.Escape) { if (panOrigin is not null || selected is not null || hovered is not null) { EndPan(); hovered = null; SelectInspection(null); } else Close(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            // Preserve native list navigation and type-ahead in the accessible inspector.
            if (InspectionChoices.IsKeyboardFocusWithin) return;
            if (e.Key == Key.Space && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.ButtonBase)
            { DiagramScroll.Cursor = Cursors.Hand; e.Handled = true; return; }
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
            case Key.Subtract: case Key.OemMinus: Zoom.Value = Math.Max(Zoom.Minimum, Zoom.Value - 10); break;
            case Key.D0: Zoom.Value = 100; break;
            case Key.C: CopyImage(sender, e); break;
            default: return;
        }
        e.Handled = true;
    }
    private void DiagramMouseLeave(object sender, MouseEventArgs e) { hovered = null; UpdateInspection(); }
    private void InspectionChoiceChanged(object sender, SelectionChangedEventArgs e)
    { if (InspectionChoices.SelectedItem is InspectionChoice choice) SelectInspection(choice.Identity); }
    private void SelectInspection(string? identity)
    {
        selected = identity;
        if (!Equals(InspectionChoices.SelectedValue, identity)) InspectionChoices.SelectedValue = identity;
        UpdateInspection();
    }
    private void UpdateInspection()
    {
        if (InspectionOverlay is null) return;
        InspectionOverlay.Update(selected, hovered, Zoom.Value / 100);
        if (inspection is not null)
        {
            var selectedDescription = inspection.Description(selected); var hoverDescription = inspection.Description(hovered);
            DiagramStatus.Text = selectedDescription is not null
                ? "Selected: " + selectedDescription + (hovered != selected && hoverDescription is not null ? " · Hover: " + hoverDescription : "") + " · Escape clears selection."
                : hoverDescription is not null ? hoverDescription + " · Click to select."
                : "Click an entity or relationship to inspect. Left-drag pans; Ctrl+wheel zooms. Escape clears selection.";
        }
    }

    public static async Task ShowCanonicalAsync(Window owner, DiagramResult result, DrawingImage drawing, Action<string> export)
    {
        using var cancellation = new CancellationTokenSource();
        void OwnerClosed(object? sender, EventArgs e) => cancellation.Cancel();
        owner.Closed += OwnerClosed;
        try
        {
            DiagramInspection? metadata = null;
            try { metadata = await StaWorker.RunAsync(() => DiagramInspection.Create(result, new NativeSvgService(), cancellation.Token), cancellation.Token); }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException or FormatException)
            { /* Unverified metadata grants no interaction. The canonical drawing remains available. */ }
            if (cancellation.IsCancellationRequested) return;
            var window = new DiagramWindow(metadata?.Drawing ?? drawing, export, metadata) { Owner = owner };
            if (metadata is null) window.DiagramStatus.Text = "Connection inspection is unavailable for this drawing. Left-drag pans; Ctrl+wheel zooms.";
            window.Show();
        }
        catch (OperationCanceledException) { }
        finally { owner.Closed -= OwnerClosed; }
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
