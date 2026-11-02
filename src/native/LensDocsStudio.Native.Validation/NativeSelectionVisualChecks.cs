using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSelectionVisualChecks
{
    // A synthetic isolation probe, not a substitute for natural mouse reproduction.
    // It can load unchanged installed/baseline DLLs through the retained four-argument API.
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LensDocsStudio.Native.Platform.ThemeService.Apply(app.Resources, "Light");
        var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/LensDocsStudio.Native;component/Rendering/DocumentPreview.xaml") };
        const string source = "First planning and **planning**.\n\nSecond planning.";
        var start = source.IndexOf("**planning", StringComparison.Ordinal) + 2;
        var observations = new List<object>();
        foreach (var mode in new[] { "Continuous", "Pages" })
        {
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(new MarkdownService().Parse(source), false,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            var editor = new TextEditor { Text = source, FontSize = 16 };
            var viewer = new DocumentPreview { Style = (Style)styles["DocumentPreviewStyle"] };
            viewer.SetPreview(result, source.Length, mode);
            var panel = new Grid(); panel.ColumnDefinitions.Add(new()); panel.ColumnDefinitions.Add(new());
            panel.Children.Add(editor); panel.Children.Add(viewer); Grid.SetColumn(viewer, 1);
            var window = new Window { Title = "Synthetic selection visual isolation", Width = 1000, Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, Content = panel };
            try
            {
                window.Show(); window.Activate(); editor.Focus(); editor.Select(start, 8); Pump();
                if (!result.TextMap.TryPreviewRange(start, 8, out var from, out var to)) throw new IOException("Visual probe mapping failed.");
                var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, from!, to!);
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (!navigation.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5)) Pump();
                if (!navigation.IsCompletedSuccessfully || !navigation.Result) throw new IOException("Visual probe navigation failed.");
                Pump();
                var inner = FindViewer(viewer) ?? throw new IOException("Visual probe inner viewer missing.");
                using var automation = new FlaUI.UIA3.UIA3Automation();
                var nativeWindow = automation.FromHandle(new WindowInteropHelper(window).Handle);
                var textProvider = UIElementAutomationPeer.CreatePeerForElement(viewer)?.GetPattern(PatternInterface.Text) as ITextProvider;
                var bounds = textProvider?.GetSelection().Single().GetBoundingRectangles();
                if (bounds is not { Length: >= 4 }) throw new IOException("Visual probe has no selected screen rectangle.");
                var inactiveBefore = Inactive();
                var caretField = typeof(TextSelection).GetField("_caretElement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var coldCaretPresent = caretField is null ? (bool?)null : caretField.GetValue(viewer.Selection) is not null;
                var before = Capture("product");
                var coldLogicalStart = result.Document.ContentStart.GetOffsetToPosition(viewer.Selection!.Start);
                var coldLogicalEnd = result.Document.ContentStart.GetOffsetToPosition(viewer.Selection.End);
                var coldUiaMatches = textProvider!.GetSelection() is { Length: 1 } coldRanges && coldRanges[0].GetText(9) == "planning";
                var previewAcceptedFocus = inner.Focus(); editor.Focus(); Pump();
                var afterFocus = Capture("after-focus");
                var warmCaretPresent = caretField is null ? (bool?)null : caretField.GetValue(viewer.Selection) is not null;
                var logical = viewer.Selection ?? throw new IOException("Visual probe selection missing.");
                var focusPreservedRange = coldLogicalStart == result.Document.ContentStart.GetOffsetToPosition(logical.Start)
                    && coldLogicalEnd == result.Document.ContentStart.GetOffsetToPosition(logical.End);
                // Establish a painted reference only after observing the untouched
                // product. Never count these isolation steps as natural mouse input.
                logical.Select(from!, from!); Pump(); var collapsed = Capture("collapsed");
                logical.Select(from!, to!); Pump(); var restored = Capture("restored");
                var exact = result.TextMap.TrySourceRange(logical.Start, logical.End, out var mappedStart, out var mappedLength)
                    && mappedStart == start && mappedLength == 8 && editor.SelectionStart == start && editor.SelectionLength == 8;
                var provider = UIElementAutomationPeer.CreatePeerForElement(viewer)?.GetPattern(PatternInterface.Text) as ITextProvider;
                var ranges = provider?.GetSelection();
                var uiaExact = ranges is { Length: 1 } && ranges[0].GetText(9) == logical.Text && logical.Text == "planning";
                var coldChanged = Changed(before, collapsed); var warmChanged = Changed(afterFocus, collapsed);
                var restoredChanged = Changed(restored, collapsed);
                var rectangle = from!.GetCharacterRect(LogicalDirection.Forward);
                var screen = inner.PointToScreen(rectangle.TopLeft);
                observations.Add(new { Mode = mode, SourceStart = editor.SelectionStart, SourceLength = editor.SelectionLength,
                    MappedStart = mappedStart, MappedLength = mappedLength, Exact = exact, UiaMatchesLogical = uiaExact,
                    LogicalStart = result.Document.ContentStart.GetOffsetToPosition(logical.Start),
                    LogicalEnd = result.Document.ContentStart.GetOffsetToPosition(logical.End),
                    EditorFocus = editor.IsKeyboardFocusWithin, ReaderInactiveHighlight = viewer.IsInactiveSelectionHighlightEnabled,
                    ViewerInactiveHighlight = inactiveBefore, ColdUiaMatchesLogical = coldUiaMatches, FocusPreservedRange = focusPreservedRange,
                    ColdCaretVisualPresent = coldCaretPresent, WarmCaretVisualPresent = warmCaretPresent,
                    ColdChangedPixels = coldChanged, WarmChangedPixels = warmChanged, RestoredChangedPixels = restoredChanged,
                    ColdHighlightVisible = coldChanged > before.Length / 15, WarmHighlightVisible = warmChanged > before.Length / 15,
                    PreviewAcceptedFocus = previewAcceptedFocus, FocusInitialisationChangedPixels = Changed(before, afterFocus),
                    ScreenStart = new[] { screen.X, screen.Y }, UiaRectangles = ranges?.Select(range => range.GetBoundingRectangles()).ToArray() });
                File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
                if (!exact || !uiaExact || !coldUiaMatches || !focusPreservedRange || !editor.IsKeyboardFocusWithin || warmChanged <= before.Length / 15)
                    throw new IOException("Logical, UIA and painted selection isolation did not correlate.");

                bool Inactive() => inner is FlowDocumentScrollViewer scrolling ? scrolling.IsInactiveSelectionHighlightEnabled
                    : ((FlowDocumentPageViewer)inner).IsInactiveSelectionHighlightEnabled;
                static int Changed(byte[] left, byte[] right)
                {
                    var changed = 0;
                    for (var index = 0; index < left.Length; index += 3)
                        if (Math.Abs(left[index] - right[index]) + Math.Abs(left[index + 1] - right[index + 1])
                            + Math.Abs(left[index + 2] - right[index + 2]) > 6) changed++;
                    return changed;
                }
                byte[] Capture(string label)
                {
                    if (!window.IsActive) throw new IOException("Synthetic visual probe lost its foreground window.");
                    using var full = nativeWindow.Capture();
                    var windowBounds = nativeWindow.BoundingRectangle;
                    var crop = new System.Drawing.Rectangle((int)bounds[0] - windowBounds.Left, (int)bounds[1] - windowBounds.Top,
                        (int)Math.Ceiling(bounds[2]), (int)Math.Ceiling(bounds[3]));
                    using var bitmap = full.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var bytes = new byte[bitmap.Width * bitmap.Height * 3]; var index = 0;
                    for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var colour = bitmap.GetPixel(x, y);
                        bytes[index++] = colour.R; bytes[index++] = colour.G; bytes[index++] = colour.B;
                    }
                    bitmap.Save(Path.Combine(output, mode + "-" + label + ".png"));
                    return bytes;
                }
            }
            finally { window.Close(); }
        }
        var assembly = typeof(DocumentPreview).Assembly;
        File.WriteAllText(Path.Combine(output, "visual-correlation.json"), JsonSerializer.Serialize(new {
            ProbeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NativeSelectionVisualChecks).Assembly.Location))),
            ProductSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
            ProductVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion,
            Observations = observations
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static FrameworkElement? FindViewer(DependencyObject parent)
    {
        if (parent is FlowDocumentScrollViewer or FlowDocumentPageViewer) return (FrameworkElement)parent;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindViewer(VisualTreeHelper.GetChild(parent, index)) is { } child) return child;
        return null;
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(80) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
