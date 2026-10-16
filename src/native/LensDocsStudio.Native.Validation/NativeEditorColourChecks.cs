using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native.Validation;

internal static class NativeEditorColourChecks
{
    public static void Run(string output)
    {
        const string source = "# Native Markdown editor\r\n\r\n**Reviewed** text with `inline code` and [document](docs/review.md).\r\n\r\nUnicode stays editable: Café 😀";
        var editor = new TextEditor { Text = source, FontFamily = new FontFamily("Consolas"), FontSize = 15, Padding = new Thickness(16), ShowLineNumbers = true };
        using var host = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Native editor colour validation")
            { Width = 840, Height = 250, WindowStyle = unchecked((int)0x80000000) });
        host.RootVisual = editor;
        var highlighter = new MarkdownHighlighting(); editor.TextArea.TextView.LineTransformers.Insert(0, highlighter);
        editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromRgb(255, 136, 62));
        editor.TextArea.SelectionForeground = new SolidColorBrush(Color.FromRgb(32, 33, 36));
        editor.Select(3, 6); var originalSelection = editor.SelectedText;
        Color[]? lightColours = null;
        foreach (var dark in new[] { false, true })
        {
            highlighter.Dark = dark;
            var background = dark ? Color.FromRgb(32, 33, 36) : Color.FromRgb(250, 250, 250);
            var foreground = dark ? Color.FromRgb(243, 244, 246) : Color.FromRgb(32, 33, 36);
            editor.Background = new SolidColorBrush(background); editor.Foreground = new SolidColorBrush(foreground);
            Layout();
            var colours = Colours();
            if (colours.Distinct().Count() < 4 || colours.Any(colour => NativeSyntaxChecks.Contrast(colour, background) < 4.5))
                throw new IOException("Actual native editor runs lack distinct, readable Markdown colours in " + (dark ? "dark" : "light") + " mode: " + JsonSerializer.Serialize(colours.Select(colour => new { Colour = colour.ToString(), Contrast = NativeSyntaxChecks.Contrast(colour, background) })));
            if (dark && lightColours!.Intersect(colours).Any()) throw new IOException("Changing the editor theme retained a stale syntax colour.");
            if (editor.Text != source || editor.SelectedText != originalSelection) throw new IOException("Changing syntax colours changed editor text or selection.");
            if (!SelectedColours().Any() || SelectedColours().Any(colour => colour != Color.FromRgb(32, 33, 36)))
                throw new IOException("Markdown syntax colouring overrode the selected text colour.");
            if (!dark) lightColours = colours;
            var bitmap = new RenderTargetBitmap(840, 250, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, "native-editor-colours-" + (dark ? "dark" : "light") + ".png")); encoder.Save(file);
        }
        highlighter.HighContrast = true; editor.Foreground = SystemColors.WindowTextBrush; editor.Background = SystemColors.WindowBrush;
        editor.TextArea.SelectionBrush = SystemColors.HighlightBrush; editor.TextArea.SelectionForeground = SystemColors.HighlightTextBrush; Layout();
        if (Colours().Any(colour => colour != SystemColors.WindowTextColor)) throw new IOException("High-contrast editor tokens did not inherit the system text colour: " + JsonSerializer.Serialize(new { Expected = SystemColors.WindowTextColor.ToString(), Actual = Colours().Select(colour => colour.ToString()) }));
        if (SelectedColours().Any(colour => colour != SystemColors.HighlightTextColor)) throw new IOException("High-contrast selection did not retain system highlight text.");
        highlighter.HighContrast = false;
        editor.Text = "# " + new string('x', MarkdownHighlighting.MaximumLineCharacters + 1); Layout();
        if (Colours().Any(colour => colour != SystemColors.WindowTextColor) || editor.Text.Length != MarkdownHighlighting.MaximumLineCharacters + 3)
            throw new IOException("An oversized editor line was coloured, truncated or changed.");
        editor.Text = new string('[', MarkdownHighlighting.MaximumLineCharacters - 1) + "!";
        var watch = Stopwatch.StartNew(); Layout(); var malformedMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (editor.Text.Length != MarkdownHighlighting.MaximumLineCharacters) throw new IOException("Malformed Markdown colouring changed its source.");
        File.WriteAllText(Path.Combine(output, "native-editor-colours.json"), JsonSerializer.Serialize(new
        {
            Passed = true, MalformedLineLayoutMilliseconds = malformedMilliseconds,
            Checks = new[] { "Actual AvalonEdit runs retain distinct Markdown colours with 4.5:1 light/dark contrast", "Theme redraw replaces previous syntax colours without changing text or selection",
                "Syntax colouring respects selected text colours and system high-contrast highlights",
                "High contrast inherits system text colours", "Oversized and malformed lines preserve complete editable text under bounded highlighting" },
            Limitation = "Native control layout and colour checks; broader typing, scrolling, screen-reader and DPI certification remains."
        }, new JsonSerializerOptions { WriteIndented = true }));

        void Layout()
        {
            editor.TextArea.TextView.Redraw(); editor.Measure(new Size(840, 250)); editor.Arrange(new Rect(0, 0, 840, 250)); editor.UpdateLayout();
            editor.TextArea.TextView.EnsureVisualLines();
        }
        Color[] Colours() => ColoursWhere(false);
        Color[] SelectedColours() => ColoursWhere(true);
        Color[] ColoursWhere(bool selected) => editor.TextArea.TextView.VisualLines.SelectMany(line => line.Elements
            .Where(element => selected == (line.FirstDocumentLine.Offset + element.RelativeTextOffset >= editor.SelectionStart
                && line.FirstDocumentLine.Offset + element.RelativeTextOffset < editor.SelectionStart + editor.SelectionLength)))
            .Select(element => ((SolidColorBrush)element.TextRunProperties.ForegroundBrush).Color).ToArray();
    }
}
