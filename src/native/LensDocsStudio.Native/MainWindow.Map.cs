using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void OpenWorkspaceMap(object sender, RoutedEventArgs e)
    {
        if (model.WorkspaceRoot is not { } root) { model.Status = "Open a workspace folder to explore its document map."; return; }
        var captured = model.Documents.Where(document => document.FilePath is not null && WorkspacePath.Contains(root, document.FilePath))
            .Select(document => (Document: document, Path: document.FilePath!, Text: document.EditorDocument.Text)).ToArray();
        var edited = captured.ToDictionary(item => item.Path, item => item.Text, StringComparer.OrdinalIgnoreCase);
        WorkspaceMapResult? map = null;
        await model.RunAsync(async () =>
        {
            var progress = new Progress<string>(message => model.Status = message);
            var result = await Task.Run(() => new WorkspaceMapService(new DocumentStore(Path.Combine(model.StateDirectory, "map-recovery")))
                .BuildAsync(new(root), edited, model.StateDirectory, progress, model.OperationToken), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            map = result;
        });
        if (map is null) return;
        if (model.WorkspaceRoot != root || captured.Any(item => !model.Documents.Contains(item.Document) || item.Document.FilePath != item.Path || item.Document.EditorDocument.Text != item.Text))
        { model.Status = "The workspace changed. Open its map again for current drafts."; return; }
        var window = new WorkspaceMapWindow(new(map)) { Owner = this };
        if (window.ShowDialog() != true) { model.Status = "Workspace map closed."; return; }
        if (window.Navigation is { } navigation)
        {
            await model.RunAsync(async () =>
            {
                if (model.WorkspaceRoot != root) throw new IOException("Open the map again for the current workspace.");
                var node = map.Documents.Single(document => document.Path == navigation.Path);
                var path = new WorkspacePath(root).Resolve(node.Path);
                if (WorkspacePath.ContainsOrEquals(model.StateDirectory, path)) throw new IOException("Private application state is excluded from the map.");
                await model.OpenPathAsync(path); BindActiveDocument();
                if (navigation.Length > 0)
                {
                    if (model.ActiveDocument?.EditorDocument.Text != node.Source)
                    { model.Status = "The document changed since the map scan. Open the map again before following its source position."; return; }
                    CaptureCurrentNavigation();
                    if (model.Layout == "Preview") SetLayout("Split");
                    Editor.Select(navigation.Start, navigation.Length);
                    Editor.ScrollToLine(Editor.Document.GetLineByOffset(navigation.Start).LineNumber); Editor.Focus(); VisitCurrentNavigation();
                }
                model.Status = navigation.Length > 0 ? "Selected the original link source." : "Opened " + node.Path;
            });
        }
        else if (window.DiagramRequested)
        {
            DrawingImage? drawing = null; string? diagramSvg = null; WorkspaceMapDiagram? diagram = null;
            var dark = ThemeService.IsDark(model.Theme);
            await model.RunAsync(async () =>
            {
                model.Status = "Rendering workspace map…";
                diagram = WorkspaceMapService.Diagram(map, window.DiagramPath);
                var result = await Task.Run(() => new DiagramService().Render(diagram.Source, dark), model.OperationToken);
                if (!result.Success) throw new IOException(result.Error);
                diagramSvg = result.Svg!;
                var prepared = await StaWorker.RunAsync(() => new NativeSvgService().Render(diagramSvg), model.OperationToken);
                model.OperationToken.ThrowIfCancellationRequested();
                drawing = prepared;
            });
            if (drawing is null || diagramSvg is null || diagram is null) return;
            var viewer = new DiagramWindow(drawing, format => ExportDiagram(format, diagramSvg))
            { Owner = this, Title = $"Workspace map · Documents: {diagram.Documents} · Connections: {diagram.Connections}" + (diagram.Limited ? " · limited view" : "") };
            AutomationProperties.SetAutomationId(viewer, "WorkspaceMapDiagram"); viewer.Show();
            model.Status = diagram.Limited ? "Diagram limited to 60 documents and 240 connections. The map lists every scanned document." : "Workspace map diagram opened.";
        }
    }
}
