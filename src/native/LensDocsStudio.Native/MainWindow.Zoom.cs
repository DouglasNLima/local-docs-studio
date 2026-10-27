using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly DispatcherTimer zoomPreferenceTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private void InitialisePreviewZoom()
    {
        var property = DependencyPropertyDescriptor.FromProperty(FlowDocumentReader.ZoomProperty, typeof(FlowDocumentReader));
        void Changed(object? sender, EventArgs e)
        {
            if (!IsLoaded || closing) return;
            zoomPreferenceTimer.Stop(); zoomPreferenceTimer.Start();
        }
        property.AddValueChanged(Preview, Changed);
        zoomPreferenceTimer.Tick += async (_, _) =>
        {
            zoomPreferenceTimer.Stop();
            var zoom = (int)Math.Round(Preview.Zoom);
            await PersistDesktop(current => current with { Preview = (current.Preview ?? new()) with { Zoom = zoom } });
        };
        Closed += (_, _) => { property.RemoveValueChanged(Preview, Changed); zoomPreferenceTimer.Stop(); };
    }
    private void ResetPreviewZoom(object sender, RoutedEventArgs e) => Preview.Zoom = 100;
}
