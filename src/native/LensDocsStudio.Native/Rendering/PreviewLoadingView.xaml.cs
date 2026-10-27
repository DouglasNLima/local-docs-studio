using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Rendering;

public partial class PreviewLoadingView : UserControl
{
    public PreviewLoadingView() => InitializeComponent();

    public void Present(PreviewLoadingState state, TimeSpan now)
    {
        var failed = state.Phase == PreviewLoadingPhase.Failed;
        Visibility = state.IsVisible(now) || failed ? Visibility.Visible : Visibility.Collapsed;
        Ring.Visibility = state.Busy ? Visibility.Visible : Visibility.Collapsed;
        Ring.IsIndeterminate = state.Busy;
        Container.HorizontalAlignment = state.Refresh ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        Container.VerticalAlignment = state.Refresh ? VerticalAlignment.Top : VerticalAlignment.Center;
        ContentPanel.Orientation = state.Refresh ? Orientation.Horizontal : Orientation.Vertical;
        Ring.Width = Ring.Height = state.Refresh ? 18 : 32;
        Status.Margin = state.Refresh ? new Thickness(8, 0, 0, 0) : new Thickness(0, 10, 0, 0);
        Status.Text = failed ? "Preview could not be updated. Retry preview."
            : state.Refresh ? "Updating preview…"
            : state.Phase switch {
                PreviewLoadingPhase.Parsing => "Preparing preview…",
                PreviewLoadingPhase.Diagrams => "Rendering Mermaid diagram…",
                PreviewLoadingPhase.Media => "Preparing preview content…",
                PreviewLoadingPhase.Building => "Building preview…",
                _ => "Loading preview…"
            };
    }
}
