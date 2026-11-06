using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async Task RenderComparisonAsync()
    {
        if (comparisonSlots is null || closing || comparisonSlots[1 - comparisonActive].Mode != "Preview") return;
        if (!ThemeService.IsCurrentHighContrast(SystemParameters.HighContrast))
        { comparisonTimer.Start(); return; }
        var slot = comparisonSlots[1 - comparisonActive]; var document = slot.Document;
        var version = document.EditorDocument.Version; var source = document.EditorDocument.Text;
        var appearance = model.Theme + "|" + CurrentDiagramTheme + "|" + SystemParameters.HighContrast;
        if (comparisonRenderedId == document.Id && comparisonRenderedVersion?.CompareAge(version) == 0 && comparisonRenderedAppearance == appearance) return;
        comparisonCancellation?.Cancel(); comparisonCancellation?.Dispose(); comparisonCancellation = new();
        var cancellation = comparisonCancellation.Token; var generation = comparisonGeneration;
        var dark = ThemeService.IsDark(model.Theme); var theme = CurrentDiagramTheme; var path = document.EffectivePath; var workspace = document.EffectiveWorkspace;
        var standalone = Path.GetExtension(path ?? document.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        bool Current() => !closing && !cancellation.IsCancellationRequested && generation == comparisonGeneration && comparisonSlots?[1 - comparisonActive] == slot
            && slot.Mode == "Preview" && model.Documents.Contains(document) && document.EditorDocument.Version.CompareAge(version) == 0
            && appearance == model.Theme + "|" + CurrentDiagramTheme + "|" + SystemParameters.HighContrast
            && document.EffectivePath == path && document.EffectiveWorkspace == workspace && ThemeService.IsCurrentHighContrast(SystemParameters.HighContrast);
        bool Owner() => model.Documents.Contains(document) && document.EditorDocument.Version.CompareAge(version) == 0;
        void ActivateOwner() { if (comparisonSlots is not null && Owner()) { var index = Array.FindIndex(comparisonSlots, item => item.Document == document); if (index >= 0) ActivateComparison(index, false); } }
        var acquired = false;
        try
        {
            if (comparisonRenderedId != document.Id) comparisonPreview.Document = new FlowDocument(new Paragraph(new Run("Loading comparison preview…")));
            // Share the existing native work gate so two large documents cannot
            // start unbounded parallel parse/layout/media construction.
            await previewGate.WaitAsync(cancellation); acquired = true;
            var prepared = await new ComparisonPreviewBuilder(markdown, diagrams, svg, previewImages, previewMaths)
                .PrepareAsync(source, standalone, dark, theme, path, workspace, cancellation);
            if (!Current()) return;
            var result = await new NativeMarkdownRenderer(svg).RenderAsync(prepared.Parsed, dark, prepared.Diagrams,
                async reference => { if (!Owner()) return; ActivateOwner(); await UpdatePreviewAsync(true); if (Owner() && model.ActiveDocument == document) Navigate(reference); },
                (format, value) => { if (!Owner()) return; ActivateOwner(); ExportDiagram(format, value); }, prepared.Drawings,
                CopyDocumentImage, prepared.Images, cancellation, CopyCodeBlock, prepared.Syntax,
                selection => { if (!Owner()) return; ActivateOwner(); _ = CopyDocumentAsync(false, selection, source, document.Id); },
                selection => { if (!Owner()) return; ActivateOwner(); _ = ExportTableCsvAsync(selection, source, document.Id); }, prepared.Formulae,
                target => { if (!Owner()) return; ActivateOwner(); NavigateWikiLink(document.Id, source, target); },
                offset => { if (!Owner()) return; ActivateOwner(); EditDiagramSource(document, source, offset); },
                offset => { if (!Owner()) return; ActivateOwner(); OpenMermaidVisualEditor(document, prepared.Parsed, version, offset); });
            if (!Current()) return;
            comparisonPreview.SetPreview(result, source.Length, WorkspaceSettings.From(model.Preferences).Preview.ReadingMode);
            comparisonPreviewResult = result;
            comparisonRenderedId = document.Id; comparisonRenderedVersion = version; comparisonRenderedAppearance = appearance;
            var top = comparisonViews.GetValueOrDefault(document.Id)?.PreviewTop ?? 0;
            await Dispatcher.InvokeAsync(() => { if (Current()) ComparisonScroller(comparisonPreview)?.ScrollToVerticalOffset(top); }, DispatcherPriority.Loaded, cancellation);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (Current()) { comparisonRenderedId = null; comparisonRequestedId = null; comparisonPreview.Document = new FlowDocument(new Paragraph(new Run("Comparison preview could not be updated: " + error.Message))); }
        }
        finally { if (acquired) previewGate.Release(); }
    }
}
