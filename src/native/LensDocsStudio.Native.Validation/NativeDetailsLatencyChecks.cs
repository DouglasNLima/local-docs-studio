using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeDetailsLatencyChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var checks = new List<object>(); var failures = new List<string>();
        try
        {
            var rich = string.Concat(Enumerable.Range(1, 2400).Select(index => $"**Item{index}** *review* [source](#target) "));
            var cases = new[]
            {
                (Name: "paragraphs", Body: string.Join("\n\n", Enumerable.Range(1, 1200).Select(index => $"Paragraph{index} **complete**."))),
                (Name: "rich-paragraph", Body: rich),
                (Name: "rich-table-cell", Body: "| Evidence |\n|---|\n| " + rich + " |")
            };
            foreach (var test in cases)
            foreach (var dark in new[] { false, true })
            {
                var source = "# Before\n\n<details>\n<summary>Large hidden content</summary>\n\n" + test.Body
                    + "\n\nHidden final marker.\n\n</details>\n\nAfter details.\n";
                var parsed = new MarkdownService().Parse(source);
                var pending = new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, dark,
                    new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
                Pump(pending, dispatcher);
                var result = pending.GetAwaiter().GetResult();
                var reader = new DocumentPreview(); reader.SetPreview(result, source.Length, "Automatic");
                using var host = new HwndSource(new HwndSourceParameters("Native details latency validation")
                    { Width = 680, Height = 360, WindowStyle = unchecked((int)0x80000000) });
                host.RootVisual = reader;
                reader.Measure(new Size(680, 360)); reader.Arrange(new Rect(0, 0, 680, 360)); reader.UpdateLayout();
                dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                var toggle = result.Details.Single().Toggle;
                var expected = Enumerable.Range(1, test.Name == "paragraphs" ? 1200 : 2400)
                    .Select(index => (test.Name == "paragraphs" ? "Paragraph" : "Item") + index).ToArray();
                var markerPattern = test.Name == "paragraphs" ? @"\bParagraph[0-9]+\b" : @"\bItem[0-9]+\b";
                var paragraph = result.SourceElements.Values.OfType<Paragraph>().First(element =>
                    new TextRange(element.ContentStart, element.ContentEnd).Text.Contains(expected[0] + " ", StringComparison.Ordinal));
                var formatted = test.Name == "paragraphs" ? paragraph.Inlines.OfType<Run>().First()
                    : paragraph.Inlines.OfType<Run>().First(run => run.FontWeight == FontWeights.Bold);
                string Text() => new TextRange(result.Document.ContentStart, result.Document.ContentEnd).Text;
                if (Text().Contains("Hidden final marker.", StringComparison.Ordinal))
                    throw new IOException("Collapsed details exposed hidden text before expansion.");
                foreach (var expanded in new[] { true, false, true })
                {
                    var inputAt = -1d; var watch = Stopwatch.StartNew();
                    var input = dispatcher.BeginInvoke(DispatcherPriority.Input, () => inputAt = watch.Elapsed.TotalMilliseconds);
                    toggle.IsExpanded = expanded;
                    var mutation = watch.Elapsed.TotalMilliseconds;
                    Pump(input.Task, dispatcher);
                    var text = Text(); var actual = Regex.Matches(text, markerPattern).Select(match => match.Value).ToArray();
                    var complete = text.Contains("Hidden final marker.", StringComparison.Ordinal) == expanded
                        && text.Contains("After details.", StringComparison.Ordinal) && actual.SequenceEqual(expanded ? expected : []);
                    var formatting = formatted.FontFamily.Source == result.Document.FontFamily.Source
                        && formatted.FontSize == result.Document.FontSize
                        && ((SolidColorBrush)formatted.Foreground).Color == ((SolidColorBrush)result.Document.Foreground).Color
                        && (test.Name == "paragraphs" || formatted.FontWeight == FontWeights.Bold
                            && paragraph.Inlines.OfType<Run>().Count(run => run.FontWeight == FontWeights.Bold) == 2400
                            && paragraph.Inlines.OfType<Hyperlink>().Count() == 2400
                            && paragraph.Inlines.OfType<Run>().Count(run => run.FontStyle == FontStyles.Italic) == 2400);
                    checks.Add(new { test.Name, Dark = dark, Expanded = expanded, SourceCharacters = source.Length,
                        MutationMilliseconds = mutation, InputMilliseconds = inputAt, CompleteText = complete,
                        FormattingPreserved = formatting, VisibleMarkers = actual.Length,
                        SourcePreserved = parsed.Source == source, SourceElements = result.SourceElements.Count });
                    if (inputAt < 0 || inputAt >= 250 || !complete || !formatting || parsed.Source != source)
                        failures.Add(test.Name + (dark ? " dark" : " light") + (expanded ? " expansion" : " collapse") + ": input "
                            + inputAt.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms; complete=" + complete + "; formatting=" + formatting);
                }
                host.RootVisual = null; reader.Document = null;
            }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        File.WriteAllText(Path.Combine(output, "native-details-latency.json"), JsonSerializer.Serialize(new
        { Passed = failures.Count == 0, Checks = checks, Failures = failures,
            Limitation = "Hidden native reader at 100% zoom; expansion mutation and first input, not physical desktop or all pagination latency." },
            new JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count != 0) throw new IOException(string.Join("; ", failures));
    }

    private static void Pump(Task task, Dispatcher dispatcher)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("Native details did not complete within the observation deadline.");
    }
}
