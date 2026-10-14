using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace LensDocsStudio.Native.Editor;

public static class EditorNavigationService
{
    private sealed class PendingNavigation { public long Revision; }
    private static readonly ConditionalWeakTable<TextEditor, PendingNavigation> Pending = new();
    public static void CancelPending(TextEditor editor)
    { editor.VerifyAccess(); if (Pending.TryGetValue(editor, out var pending)) pending.Revision++; }

    public static async Task<bool> BringIntoViewAsync(TextEditor editor, int offset)
    {
        var document = editor.Document;
        if (document is null) return false;
        var version = document.Version;
        var caret = editor.CaretOffset; var start = editor.SelectionStart; var length = editor.SelectionLength;
        var pending = Pending.GetOrCreateValue(editor); var revision = ++pending.Revision;
        offset = Math.Clamp(offset, 0, document.TextLength);
        // Wrapped virtual lines change their estimated height when they first enter the viewport.
        // Re-evaluate after layout instead of leaving a distant search/source target below the window.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (editor.Dispatcher.HasShutdownStarted) return false;
            bool? complete;
            try { complete = await editor.Dispatcher.InvokeAsync<bool?>(() =>
            {
                if (pending.Revision != revision || editor.Document != document || document.Version.CompareAge(version) != 0
                    || editor.CaretOffset != caret || editor.SelectionStart != start || editor.SelectionLength != length) return false;
                editor.UpdateLayout();
                var view = editor.TextArea.TextView;
                if (view.ActualHeight <= 0 || view.ActualWidth <= 0) return false;
                var location = document.GetLocation(offset);
                editor.ScrollTo(location.Line, location.Column, VisualYPosition.LineMiddle, view.ActualHeight / 3, 0);
                editor.UpdateLayout(); view.EnsureVisualLines();
                var point = view.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - view.ScrollOffset;
                return point.Y >= 0 && point.Y <= view.ActualHeight && point.X >= 0 && point.X <= view.ActualWidth ? true : null;
            }, DispatcherPriority.Loaded); }
            catch (TaskCanceledException) when (editor.Dispatcher.HasShutdownStarted) { return false; }
            if (complete.HasValue) return complete.Value;
        }
        return false;
    }
}
