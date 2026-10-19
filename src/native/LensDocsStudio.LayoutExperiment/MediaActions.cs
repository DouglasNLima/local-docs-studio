using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal sealed class MediaActions
{
    private readonly ExperimentWindow owner;
    private Window? zoom;
    public MediaActions(ExperimentWindow owner)
    {
        this.owner = owner; owner.Surface.MediaAction += Act;
        owner.Installed += _ => zoom?.Close(); owner.Closed += (_, _) => zoom?.Close();
    }
    private async void Act(ControlBox box)
    {
        var snapshot = owner.Surface.Snapshot;
        if (snapshot?.Model.Root.Descendants().FirstOrDefault(node => node.Id == box.Node && node.Kind == "Media") is not { Media: { } image, Target: { } source }) return;
        try
        {
            if (box.Label == "Zoom")
            {
                zoom?.Close();
                var visual = new Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(16) };
                System.Windows.Automation.AutomationProperties.SetName(visual, "Mermaid diagram");
                zoom = new Window { Owner = owner, Title = "EXPERIMENT ONLY — Mermaid zoom", Width = 900, Height = 650,
                    Content = new ScrollViewer { Content = visual } };
                zoom.Show(); return;
            }
            if (box.Label == "Copy source")
            {
                var sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
                await owner.Surface.ClipboardWorker.Plain(source, sequence, CancellationToken.None); return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "diagram." + box.Label.ToLowerInvariant(),
                Filter = box.Label == "SVG" ? "SVG drawing|*.svg" : "PNG image|*.png", AddExtension = true };
            if (dialog.ShowDialog(owner) != true) return;
            var output = dialog.FileName;
            var bytes = await ExperimentWindow.Worker(() =>
            {
                if (box.Label == "PNG") return NativeSvgService.ToPng(image);
                var diagram = new DiagramService().Render(source, ExperimentAppearance.IsDark(snapshot.Appearance));
                return System.Text.Encoding.UTF8.GetBytes(new NativeSvgService().Prepare(diagram.Svg ?? throw new IOException(diagram.Error)));
            });
            if (!ReferenceEquals(owner.Surface.Snapshot, snapshot)) throw new IOException("The diagram changed before export completed.");
            await File.WriteAllBytesAsync(output, bytes);
        }
        catch (Exception error) when (error is IOException or System.Runtime.InteropServices.ExternalException)
        { owner.Surface.InteractionStages.Add(new { Stage = "media-action-failure", box.Label, Error = error.Message }); }
    }
}
