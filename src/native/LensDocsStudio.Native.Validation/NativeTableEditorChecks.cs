using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeTableEditorChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var oldResources = application.Resources; var shutdown = application.ShutdownMode; application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var cases = new List<object>();
        const string source = "Before\r\n\r\n| Name | Status |\r\n|:---|---:|\r\n| Café | Ready |\r\n| Second | Draft |\r\n\r\nAfter";
        try
        {
            foreach (var dark in new[] { false, true })
            foreach (var size in new[] { new Size(640, 460), new Size(940, 640) })
            {
                application.Resources = new(); ThemeService.Apply(application.Resources, dark ? "Dark" : "Light");
                var session = TableEditService.Open(new MarkdownService().Parse(source), source.IndexOf("Ready", StringComparison.Ordinal), 0);
                var window = new TableEditorWindow(session);
                try
                {
                    var content = (Grid)window.Content; window.Content = null;
                    var surface = new Border { Child = content, Background = (Brush)application.Resources["WindowBrush"] };
                    surface.Resources.MergedDictionaries.Add(window.Resources);
                    System.Windows.Documents.TextElement.SetForeground(surface, (Brush)application.Resources["TextBrush"]);
                    using var host = new HwndSource(new HwndSourceParameters("Native table grid validation")
                        { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
                    host.RootVisual = surface; Layout();
                    var grid = (DataGrid)window.FindName("Cells");
                    if (new DataGridAutomationPeer(grid).GetName() != "Markdown table cells" || grid.CanUserSortColumns || grid.CanUserReorderColumns || grid.CanUserDeleteRows)
                        throw new IOException("The native table grid lost its accessible name or guarded source ordering.");
                    if (((Button)window.FindName("RemoveRow")).IsEnabled || ((Button)window.FindName("RowDown")).IsEnabled)
                        throw new IOException("The table header can be deleted or moved into the body.");
                    var view = (DataView)grid.ItemsSource; view[1]["C1"] = "Reviewed Ω"; Layout();
                    if (grid.Columns[1].GetCellContent(view[1]) is not TextBlock text || text.Text != "Reviewed Ω")
                        throw new IOException("The native table cell binding did not display its edited Markdown value.");
                    grid.CurrentCell = new(view[1], grid.Columns[1]);
                    ((ComboBox)window.FindName("Alignment")).SelectedIndex = (int)TableColumnAlignment.Centre;
                    Click("AddRow"); Click("RowDown"); Click("RowUp"); Click("RemoveRow");
                    Click("AddColumn"); Click("ColumnLeft"); Click("ColumnRight"); Click("RemoveColumn");
                    if (!(bool)typeof(TableEditorWindow).GetMethod("Capture", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!)
                        throw new IOException("The native table draft cannot be committed.");
                    var draft = (TableEditGrid)typeof(TableEditorWindow).GetField("draft", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                    if (draft.RowCount != 3 || draft.ColumnCount != 2 || draft.Cell(1, 1) != "Reviewed Ω" || draft.Alignment(1) != TableColumnAlignment.Centre)
                        throw new IOException("Native row/column controls lost existing cells or alignment.");
                    var editor = new TextEditor { Text = source }; editor.Document.UndoStack.ClearAll();
                    var edit = session.Plan(draft); EditorFormattingService.Apply(editor, edit); var changed = editor.Text;
                    if (!changed.Contains("Reviewed Ω", StringComparison.Ordinal) || editor.SelectionStart != edit.SelectionStart || editor.SelectionLength != edit.SelectionLength
                        || !changed.StartsWith("Before\r\n\r\n", StringComparison.Ordinal) || !changed.EndsWith("\r\n\r\nAfter", StringComparison.Ordinal))
                        throw new IOException("Applying the table grid lost source or selection.");
                    editor.Document.UndoStack.Undo(); if (editor.Text != source) throw new IOException("A table edit did not undo as one complete change.");
                    editor.Document.UndoStack.Redo(); if (editor.Text != changed) throw new IOException("The complete table edit did not redo.");
                    editor.IsReadOnly = true; EditorFormattingService.Apply(editor, edit); if (editor.Text != changed) throw new IOException("A table edit changed read-only source.");
                    if (session.Grid.Cell(1, 1) != "Ready") throw new IOException("The private grid draft changed its original session before Apply.");
                    Layout();
                    var header = Visuals(grid).OfType<DataGridRowHeader>().First(item => item.Content?.ToString() == "Header");
                    var headerText = Visuals(header).OfType<TextBlock>().Single(item => item.Text == "Header");
                    for (var parent = VisualTreeHelper.GetParent(headerText); parent is FrameworkElement element; parent = VisualTreeHelper.GetParent(parent))
                    {
                        var textBounds = new Rect(headerText.TranslatePoint(new(), element), headerText.RenderSize);
                        if (textBounds.Left < 0 || textBounds.Right > element.ActualWidth + 0.5 || headerText.ActualWidth < headerText.DesiredSize.Width - 0.5)
                            throw new IOException("The native table header label is clipped.");
                        if (element == header) break;
                    }
                    foreach (var name in new[] { "AddRow", "AddColumn", "Alignment", "ApplyButton", "Cells" })
                    {
                        var control = (FrameworkElement)window.FindName(name); var bounds = new Rect(control.TranslatePoint(new(), surface), control.RenderSize);
                        if (bounds.Width < 20 || bounds.Height < 20 || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > size.Width || bounds.Bottom > size.Height)
                            throw new IOException("A native table control is clipped: " + name + " / " + bounds);
                    }
                    var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var imageName = "native-table-editor-" + (dark ? "dark" : "light") + "-" + size.Width + ".png";
                    using (var file = File.Create(Path.Combine(output, imageName))) encoder.Save(file);
                    cases.Add(new { Theme = dark ? "Dark" : "Light", Width = size.Width, Height = size.Height, Image = imageName });
                    host.RootVisual = null; surface.Child = null;
                    void Layout() { surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout(); }
                    void Click(string name)
                    {
                        var button = (Button)window.FindName(name);
                        if (!button.IsEnabled) throw new IOException("The native table action is unexpectedly disabled: " + name);
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
                    }
                }
                finally { window.Close(); }
            }
        }
        finally { application.Resources = oldResources; application.ShutdownMode = shutdown; }
        File.WriteAllText(Path.Combine(output, "native-table-editor.json"), JsonSerializer.Serialize(new
        { Passed = true, Cases = cases, SourcePreserved = true, PrivateDraft = true, RowAndColumnActions = true, AlignmentRetained = true,
            WholeUndoRedo = true, ReadOnlyRetained = true, AccessibleGrid = true, ControlsWithinBounds = true, HeaderLabelVisible = true,
            Limitation = "Hidden HWND layout, actual DataGrid binding and native control events. Foreground keyboard, Cancel/Apply and installed behaviour require hosted UI validation." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var descendant in Visuals(child)) yield return descendant;
        }
    }
}
