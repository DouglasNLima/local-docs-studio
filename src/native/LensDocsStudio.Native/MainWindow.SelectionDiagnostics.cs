using System.Text.Json;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly bool selectionDiagnostics = Environment.GetEnvironmentVariable("LENS_NATIVE_SELECTION_DIAGNOSTICS") == "1";
    private int selectionDiagnosticCount;

    // Opt-in, bounded synthetic evidence. Never record document/selection text.
    private void TraceSelection(string operation)
    {
        if (!selectionDiagnostics || selectionDiagnosticCount++ >= 2000) return;
        try
        {
            Directory.CreateDirectory(model.StateDirectory);
            File.AppendAllText(Path.Combine(model.StateDirectory, "selection-events.jsonl"), JsonSerializer.Serialize(new {
                Operation = operation, At = System.Diagnostics.Stopwatch.GetTimestamp(),
                Document = model.ActiveDocument?.Id, RenderedDocument = renderedDocumentId, Layout = model.Layout,
                syncGeneration, pendingSync, applyingSyncOrigin, SyncCurrent, BothPanesVisible,
                restoringNavigation, bindingViewport, pendingViewportDocument,
                Start = Editor.SelectionStart, Length = Editor.SelectionLength, Caret = Editor.CaretOffset,
                EditorFocus = Editor.IsKeyboardFocusWithin, PreviewFocus = Preview.IsKeyboardFocusWithin,
                Mouse = System.Windows.Input.Mouse.LeftButton.ToString(),
                ReadingMode = Preview.ViewingMode.ToString(), Zoom = Preview.Zoom,
                PreviewStart = Preview.Selection is { } selection && Preview.Document is { } document
                    && selection.Start.IsInSameDocument(document.ContentStart)
                    ? document.ContentStart.GetOffsetToPosition(selection.Start) : (int?)null
            }) + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Selection diagnostics could not be retained: " + error.Message); }
    }
}
