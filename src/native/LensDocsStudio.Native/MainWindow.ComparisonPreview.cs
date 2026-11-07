using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
        var path = document.EffectivePath; var workspace = document.EffectiveWorkspace;
        var context = appearance + "|" + path + "|" + workspace;
        // A cancelled replacement can leave the loading document attached while
        // the last accepted owner/version still describe the previous preview.
        if (comparisonRenderedId == document.Id && comparisonRenderedVersion?.CompareAge(version) == 0 && comparisonRenderedContext == context
            && comparisonPreviewResult is { } accepted && comparisonPreview.Document == accepted.Document) return;
        comparisonCancellation?.Cancel(); comparisonCancellation?.Dispose(); comparisonCancellation = new();
        var cancellation = comparisonCancellation.Token; var generation = comparisonGeneration;
        var dark = ThemeService.IsDark(model.Theme); var theme = CurrentDiagramTheme;
        var standalone = Path.GetExtension(path ?? document.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        bool Current() => !closing && !cancellation.IsCancellationRequested && generation == comparisonGeneration && comparisonSlots?[1 - comparisonActive] == slot
            && slot.Mode == "Preview" && model.Documents.Contains(document) && document.EditorDocument.Version.CompareAge(version) == 0
            && appearance == model.Theme + "|" + CurrentDiagramTheme + "|" + SystemParameters.HighContrast
            && document.EffectivePath == path && document.EffectiveWorkspace == workspace && ThemeService.IsCurrentHighContrast(SystemParameters.HighContrast);
        bool Owner() => model.Documents.Contains(document) && document.EditorDocument.Version.CompareAge(version) == 0
            && document.EffectivePath == path && document.EffectiveWorkspace == workspace;
        void ActivateOwner() { if (comparisonSlots is not null && Owner()) { var index = Array.FindIndex(comparisonSlots, item => item.Document == document); if (index >= 0) ActivateComparison(index, false); } }
        var acquired = false;
        try
        {
            AutomationProperties.SetItemStatus(comparisonPreview, "Busy; comparison preview is being updated");
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
            comparisonRenderedId = document.Id; comparisonRenderedVersion = version; comparisonRenderedContext = context;
            var view = comparisonViews.GetValueOrDefault(document.Id);
            var restored = true;
            if (view?.PreviewAnchor is { } anchor && view.PreviewVersion is { } savedVersion
                && savedVersion.BelongsToSameDocumentAs(version) && savedVersion.CompareAge(version) == 0
                && result.TextMap.TryPreviewRange(anchor.Source, 0, out var position, out _) && position is not null)
            {
                if (comparisonPreview.ViewingMode == FlowDocumentReaderViewingMode.Scroll)
                    restored = await new PreviewViewportAnchor(result.Document, position, anchor.Fraction)
                        .RestoreAsync(comparisonPreview, Current, cancellation);
                else restored = await PreviewNavigationService.BringIntoViewAsync(comparisonPreview, position);
                // A reconstructed secondary reader must retain the same literal
                // target for its next capture, rather than fall back to a nearby heading.
                if (restored && Current()) PreviewNavigationService.RetainVisibleTarget(comparisonPreview, position);
            }
            else await Dispatcher.InvokeAsync(() => { if (Current()) ComparisonScroller(comparisonPreview)?.ScrollToVerticalOffset(view?.PreviewTop ?? 0); }, DispatcherPriority.Loaded, cancellation);
            if (Current())
            {
                if (view?.PreviewSelection is { } selection && view.PreviewVersion is { } selectionVersion
                    && selectionVersion.BelongsToSameDocumentAs(version) && selectionVersion.CompareAge(version) == 0
                    && result.TextMap.TryPreviewRange(selection.Start, selection.Length, out var from, out var to))
                    comparisonPreview.Selection?.Select(from!, to!);
                AutomationProperties.SetItemStatus(comparisonPreview, restored ? "Ready" : "Comparison preview position could not be restored");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (Current()) { comparisonRenderedId = null; comparisonRequestedId = null; comparisonPreview.Document = new FlowDocument(new Paragraph(new Run("Comparison preview could not be updated: " + error.Message))); AutomationProperties.SetItemStatus(comparisonPreview, "Preview unavailable"); }
        }
        finally { if (acquired) previewGate.Release(); }
    }
}
