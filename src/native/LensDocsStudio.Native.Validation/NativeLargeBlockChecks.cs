using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativePaginationChecks
{
    public static void RunLargeBlocks(string output)
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = application.Resources;
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var outcomes = new List<object>();
        try
        {
            application.Resources = new ResourceDictionary(); ThemeService.Apply(application.Resources, "Light");
            var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/LensDocsStudio.Native;component/Rendering/DocumentPreview.xaml") };
            // A single bounded 344 Ki-character block distinguishes native page layout from a many-item list.
            var text = string.Concat(Enumerable.Repeat("Native documentation preserves every word. ", 8192)) + "Final retained target.";
            var cases = new Dictionary<string, string>
            {
                ["paragraph"] = text,
                ["table-cell"] = "| Content |\n| --- |\n| " + text + " |",
                ["quote"] = "> " + text,
                ["list-item"] = "- " + text,
                ["code-block"] = "```text\n" + text + "\n```",
                ["details"] = "<details open><summary>Long details</summary>\n\n" + text + "\n\n</details>"
            };
            foreach (var (name, source) in cases)
            {
                var parsed = new MarkdownService().Parse(source);
                var renderer = new NativeMarkdownRenderer(new NativeSvgService());
                var renderWatch = Stopwatch.StartNew();
                var prepared = renderer.RenderAsync(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
                Complete(prepared);
                var result = prepared.Result; var constructionMs = renderWatch.Elapsed.TotalMilliseconds;
                var before = result.ReadText();
                var viewer = new DocumentPreview { Style = (Style)styles["DocumentPreviewStyle"] };
                viewer.SetPreview(result, source.Length, "Automatic");
                using var host = new HwndSource(new HwndSourceParameters("Hidden native single-block validation")
                    { Width = 800, Height = 480, WindowStyle = unchecked((int)0x80000000) });
                var watch = Stopwatch.StartNew(); double firstInputMs = -1;
                viewer.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => firstInputMs = watch.Elapsed.TotalMilliseconds);
                host.RootVisual = viewer; Layout(viewer);
                while (firstInputMs < 0) Pump();
                var target = Find(result.Document, "Final retained target.");
                var beats = 0; double worstInputMs = 0; var gap = Stopwatch.StartNew();
                var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) };
                heartbeat.Tick += (_, _) => { beats++; worstInputMs = Math.Max(worstInputMs, gap.Elapsed.TotalMilliseconds); gap.Restart(); };
                heartbeat.Start(); watch.Restart();
                var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, target, target.GetPositionAtOffset(22));
                try { Complete(navigation); }
                finally { heartbeat.Stop(); }
                var navigationMs = watch.Elapsed.TotalMilliseconds; Layout(viewer);
                var rectangle = target.GetCharacterRect(LogicalDirection.Forward);
                var exactSelection = viewer.Selection?.Text == "Final retained target.";
                var visible = !rectangle.IsEmpty && rectangle.Top >= 0 && rectangle.Bottom <= viewer.ActualHeight
                    && rectangle.Left >= 0 && rectangle.Right <= viewer.ActualWidth;
                var preserved = result.ReadText() == before && before.Contains(text, StringComparison.Ordinal) && parsed.Source == source;
                var passed = navigation.Result && exactSelection && visible && preserved && viewer.ViewingMode == FlowDocumentReaderViewingMode.Page;
                outcomes.Add(new { name, sourceCharacters = source.Length, constructionMs, firstInputMs, navigationMs,
                    worstInputMs, inputBeats = beats, viewer.PageCount, viewer.PageNumber, navigation = navigation.Result,
                    rectangle = rectangle.ToString(), exactSelection, visible, preserved, passed });
                File.WriteAllText(Path.Combine(output, "native-large-blocks.json"), JsonSerializer.Serialize(outcomes, new JsonSerializerOptions { WriteIndented = true }));
                Save(viewer, Path.Combine(output, "native-large-block-" + name + ".png"));
                host.RootVisual = null; viewer.Document = null; Pump();
                if (!passed) throw new IOException("A single large native block lost content, selection or visible navigation: " + name);
            }
        }
        finally
        {
            application.Resources = resources;
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }
}
