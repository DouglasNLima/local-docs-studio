using System.Text.Json;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly bool viewportDiagnostics = Environment.GetEnvironmentVariable("LENS_NATIVE_VIEWPORT_DIAGNOSTICS") == "1";

    // Opt-in synthetic certification evidence: identities and offsets, never document text.
    private void TraceViewport(string operation)
    {
        if (!viewportDiagnostics) return;
        try
        {
            var path = Path.Combine(model.StateDirectory, "viewport-navigation.jsonl");
            Directory.CreateDirectory(model.StateDirectory);
            if (File.Exists(path) && new FileInfo(path).Length >= 1_000_000) return;
            File.AppendAllText(path, JsonSerializer.Serialize(new
            {
                Operation = operation, Active = model.ActiveDocument?.Id, Displayed = displayedTab?.Id,
                Rendered = renderedDocumentId, Pending = pendingViewportDocument, Generation = viewportGeneration,
                Restoring = restoringNavigation, EditorCaret = Editor.CaretOffset, EditorVertical = Editor.VerticalOffset,
                Busy = model.IsBusy, PreviewPage = Preview.PageNumber, PreviewPages = Preview.PageCount, PreviewHeight = Preview.ActualHeight,
                // Accessing DocumentPaginator suspends Continuous formatting.
                PageSize = Preview.ViewingMode != System.Windows.Controls.FlowDocumentReaderViewingMode.Scroll && Preview.Document is { } document
                    ? ((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator.PageSize.ToString() : null,
                Back = navigation.Back, Forward = navigation.Forward,
                Documents = model.Documents.Select(tab => new { tab.Id, tab.CaretOffset, tab.VerticalOffset, tab.PreviewSourceOffset })
            }) + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Viewport diagnostics could not be retained: " + error.Message); }
    }
}
