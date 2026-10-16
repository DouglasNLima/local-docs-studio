using System.Drawing;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckDiagramViewport(Window drawing, string output)
    {
        var original = drawing.BoundingRectangle;
        var zoom = Find(drawing, "DiagramZoom");
        drawing.Patterns.Transform.Pattern.Resize(560, 400);
        Wait(() => drawing.BoundingRectangle.Width < 570, "The diagram viewport did not resize for pointer validation.");
        var viewport = Find(drawing, "DiagramViewport");
        var scroll = viewport.Patterns.Scroll.Pattern;
        try
        {
            zoom.Patterns.RangeValue.Pattern.SetValue(400);
            Wait(() => scroll.HorizontallyScrollable.Value, "The enlarged diagram did not exceed its actual native viewport.");
            var bounds = viewport.BoundingRectangle;
            var origin = new Point((int)(bounds.Left + bounds.Width * 0.7), (int)(bounds.Top + bounds.Height / 2));
            var before = scroll.HorizontalScrollPercent.Value;
            Mouse.DragHorizontally(origin, -110, MouseButton.Middle);
            Wait(() => scroll.HorizontalScrollPercent.Value > before, "Middle-button dragging did not pan the native drawing.");
            var middle = scroll.HorizontalScrollPercent.Value;
            viewport.Click();
            Keyboard.Press(VirtualKeyShort.SPACE);
            try { Mouse.DragHorizontally(origin, -90, MouseButton.Left); }
            finally { Keyboard.Release(VirtualKeyShort.SPACE); }
            Wait(() => scroll.HorizontalScrollPercent.Value > middle, "Space+drag did not pan the native drawing.");
            Find(drawing, "DiagramReset").AsButton().Invoke();
            Wait(() => Find(drawing, "DiagramZoomLabel").Name == "100%" && (!scroll.HorizontallyScrollable.Value || scroll.HorizontalScrollPercent.Value <= 0),
                "Reset did not restore actual size and the native viewport origin.");
            zoom.Patterns.RangeValue.Pattern.SetValue(200);
            Wait(() => Find(drawing, "DiagramZoomLabel").Name == "200%", "Pointer zoom did not reach its starting scale.");
            var image = drawing.FindFirstDescendant(cf => cf.ByName("Mermaid diagram drawing")) ?? throw new IOException("The actual diagram image is unavailable.");
            var imageBefore = image.BoundingRectangle;
            var pointer = new Point((int)(bounds.Left + bounds.Width / 3), (int)(bounds.Top + bounds.Height / 3));
            Mouse.MoveTo(pointer); Keyboard.Press(VirtualKeyShort.CONTROL);
            try { Mouse.Scroll(1); }
            finally { Keyboard.Release(VirtualKeyShort.CONTROL); }
            Wait(() => Find(drawing, "DiagramZoomLabel").Name == "220%", "Ctrl+wheel did not change native diagram scale.");
            var imageAfter = image.BoundingRectangle;
            var beforeCoordinate = (pointer.X - imageBefore.Left) / (double)imageBefore.Width;
            var afterCoordinate = (pointer.X - imageAfter.Left) / (double)imageAfter.Width;
            if (beforeCoordinate is <= 0.05 or >= 0.95) throw new IOException("Pointer zoom fixture did not use an interior drawing anchor.");
            if (Math.Abs(beforeCoordinate - afterCoordinate) > 0.02) throw new IOException("Pointer-centred zoom moved its drawing anchor.");
            Find(drawing, "DiagramFit").AsButton().Invoke();
            Wait(() => !scroll.HorizontallyScrollable.Value && !scroll.VerticallyScrollable.Value, "Fit did not bring the complete drawing into the native viewport.");
            drawing.CaptureToFile(Path.Combine(output, "native-diagram-viewport.png"));
            File.WriteAllText(Path.Combine(output, "native-diagram-viewport.json"), JsonSerializer.Serialize(new {
                Passed = true, MiddleButtonPan = true, SpaceDragPan = true, ResetOrigin = true, PointerAnchorBefore = beforeCoordinate,
                PointerAnchorAfter = afterCoordinate, FitContainsDrawing = true, ContentTransformOnly = true }));
        }
        finally { drawing.Patterns.Transform.Pattern.Resize(original.Width, original.Height); }
    }
}
