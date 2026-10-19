using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Measured native chrome; no live control crosses its owning dispatcher.</summary>
internal sealed record ThemeGeometry(string Appearance, double ScrollbarWidth, ImmutableDictionary<string, Size> Buttons,
    DrawingImage Expanded, DrawingImage Collapsed, double Milliseconds, double DetailsMilliseconds, int Thread)
{
    private sealed class Capture(string appearance)
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private readonly double scrollbar = SystemParameters.VerticalScrollBarWidth;
        private readonly Dictionary<string, Size> buttons = [];
        private DrawingImage? expanded, collapsed;
        private double detailsMilliseconds;
        public void Step(int index)
        {
            if (index < 4)
            {
                var label = new[] { "Zoom", "SVG", "PNG", "Copy source" }[index];
                var button = new Button { Content = label, FontFamily = new FontFamily("Segoe UI"), FontSize = 15,
                    Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) };
                button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); buttons.Add(label, button.DesiredSize);
            }
            else
            {
                var at = watch.Elapsed.TotalMilliseconds; var image = DetailsDrawing.Create(index == 4, appearance);
                if (!image.IsFrozen) throw new IOException("Native details paint must be frozen before transfer.");
                if (index == 4) expanded = image; else collapsed = image;
                detailsMilliseconds += watch.Elapsed.TotalMilliseconds - at;
            }
        }
        public ThemeGeometry Complete() => new(appearance, scrollbar, buttons.ToImmutableDictionary(), expanded!, collapsed!,
            watch.Elapsed.TotalMilliseconds, detailsMilliseconds, Environment.CurrentManagedThreadId);
    }
    public static async Task<ThemeGeometry> Prepare(string appearance, CancellationToken cancellation, Func<bool>? current = null)
    {
        cancellation.ThrowIfCancellationRequested();
        Application.Current?.Dispatcher.VerifyAccess();
        var capture = new Capture(appearance);
        for (var index = 0; index < 6; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (current?.Invoke() == false) throw new OperationCanceledException("The requested native appearance was superseded.");
            capture.Step(index);
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        cancellation.ThrowIfCancellationRequested();
        if (current?.Invoke() == false) throw new OperationCanceledException("The requested native appearance was superseded.");
        return capture.Complete();
    }
    public static ThemeGeometry PrepareSynchronous(string appearance, CancellationToken cancellation, bool workerOwnershipControl = false)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!workerOwnershipControl) Application.Current?.Dispatcher.VerifyAccess();
        var capture = new Capture(appearance);
        for (var index = 0; index < 6; index++) { cancellation.ThrowIfCancellationRequested(); capture.Step(index); }
        return capture.Complete();
    }
}
