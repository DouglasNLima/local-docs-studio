using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LensDocsStudio.Native.Controls;

/// <summary>Original 24-unit outline artwork shared by native command surfaces.</summary>
public sealed class CommandIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(CommandIcon),
        new FrameworkPropertyMetadata("Document", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(CommandIcon),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public CommandIcon() { Width = Height = 18; Focusable = false; IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext drawing)
    {
        if (TryFindResource("Icon." + Kind) is not Geometry geometry) return;
        var pen = new Pen(Foreground, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawing.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        drawing.DrawGeometry(null, pen, geometry); drawing.Pop();
    }
}
