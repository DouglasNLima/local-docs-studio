using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeInlineLayoutChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try { Check(output, dispatcher); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static void Check(string output, Dispatcher dispatcher)
    {
        const int count = 1500;
        var rich = string.Join(" ", Enumerable.Range(1, count).Select(i => $"**Bold{i}** [*Café{i}*](#entry-{i})"));
        var richLabel = "[" + string.Join(" ", Enumerable.Range(1, count).Select(i => $"**Bold{i}** *Café{i}*")) + "](#one-target)";
        var plain = string.Join(" ", Enumerable.Range(1, count).Select(i => $"Bold{i} Café{i}"));
        var code = string.Join("\n", Enumerable.Range(1, 150).Select(i => $"var value{i} = \"Café\"; // retained"));
        var cases = new[]
        {
            (Name: "paragraph", Source: rich, Expected: plain, Formatted: true, Links: count),
            (Name: "heading", Source: "# " + rich, Expected: plain, Formatted: true, Links: count),
            (Name: "table-cell", Source: "| Value |\n|---|\n| " + rich + " |", Expected: plain, Formatted: true, Links: count),
            (Name: "coloured-code", Source: "```csharp\n" + code + "\n```", Expected: code, Formatted: false, Links: 0),
            (Name: "quote", Source: "> " + rich, Expected: plain, Formatted: true, Links: count),
            (Name: "list-item", Source: "- " + rich, Expected: "\u2022\t" + plain, Formatted: true, Links: count),
            (Name: "callout", Source: "> [!NOTE]\n> " + rich, Expected: plain, Formatted: true, Links: count),
            (Name: "open-details", Source: "<details open>\n<summary>Dense content</summary>\n\n" + rich + "\n\n</details>", Expected: plain, Formatted: true, Links: count),
            (Name: "long-link-label", Source: richLabel, Expected: plain, Formatted: true, Links: 1)
        };
        var outcomes = new List<object>(); var failures = new List<string>();
        var renderer = new NativeMarkdownRenderer(new NativeSvgService());
        var diagrams = new Dictionary<FencedCodeBlock, DiagramResult>();
        foreach (var item in cases)
        {
            var parsed = new MarkdownService().Parse(item.Source);
            var syntax = CodeSyntaxService.Prepare(parsed);
            if (!item.Formatted && syntax.Blocks.Count != 1) throw new IOException("The code fixture did not prepare syntax colouring.");
            var watch = Stopwatch.StartNew(); var inputAt = -1d; var lastInput = 0d; var maximumGap = 0d;
            void Input()
            {
                var elapsed = watch.Elapsed.TotalMilliseconds;
                maximumGap = Math.Max(maximumGap, elapsed - lastInput); lastInput = elapsed;
                if (inputAt < 0) inputAt = elapsed;
            }
            var queued = dispatcher.BeginInvoke(DispatcherPriority.Input, Input);
            var timer = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => Input(); timer.Start();
            PreviewResult result; bool yielded; double elapsed;
            try
            {
                var pending = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { }, codeSyntax: syntax);
                yielded = !pending.IsCompleted;
                Pump(pending, dispatcher); result = pending.GetAwaiter().GetResult(); elapsed = watch.Elapsed.TotalMilliseconds;
                maximumGap = Math.Max(maximumGap, elapsed - lastInput);
            }
            finally { timer.Stop(); queued.Abort(); }

            // Containers and their first paragraph may share a source offset. Inspect the
            // actual document tree rather than the one-element-per-offset navigation index.
            var target = Paragraphs(result.Document.Blocks).FirstOrDefault(paragraph =>
                Read(paragraph) == item.Expected);
            var complete = target is not null;
            var inlines = target is null ? [] : Flatten(target.Inlines).ToArray();
            var links = inlines.OfType<Hyperlink>().ToArray();
            var formatting = item.Formatted
                ? links.Length == item.Links && inlines.OfType<Run>().Count(run => run.FontWeight == FontWeights.Bold) >= count
                    && links.All(link => Flatten(link.Inlines).OfType<Run>().Any(run => run.FontStyle == FontStyles.Italic))
                : inlines.OfType<Run>().Count(run => run.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue) > 150;
            var sourcePreserved = parsed.Source == item.Source;
            var sourceMapping = target?.Tag is SourceReference reference && reference.Start >= 0 && reference.End < item.Source.Length
                && result.SourceElements.ContainsKey(reference.Start);

            using var stop = new CancellationTokenSource(); var cancelled = false; var cancelAt = -1d;
            var cancelWatch = Stopwatch.StartNew();
            var cancelInput = dispatcher.BeginInvoke(DispatcherPriority.Input, () => { cancelAt = cancelWatch.Elapsed.TotalMilliseconds; stop.Cancel(); });
            try
            {
                var pending = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { }, cancellationToken: stop.Token, codeSyntax: syntax);
                Pump(pending, dispatcher);
                try { pending.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { cancelled = true; }
            }
            finally { cancelInput.Abort(); }
            var passed = yielded && inputAt >= 0 && inputAt < elapsed && maximumGap < 250 && complete && formatting && sourceMapping && sourcePreserved
                && cancelled && cancelAt >= 0 && cancelAt < 250;
            outcomes.Add(new { item.Name, Passed = passed, SourceCharacters = item.Source.Length, Yielded = yielded,
                InputMilliseconds = inputAt, MaximumInputGapMilliseconds = maximumGap, ConstructionMilliseconds = elapsed,
                CompleteText = complete, Formatting = formatting, SourceMapping = sourceMapping, SourcePreserved = sourcePreserved,
                Cancelled = cancelled, CancelInputMilliseconds = cancelAt,
                TextMismatch = complete ? null : Paragraphs(result.Document.Blocks).Select(paragraph =>
                {
                    var text = Read(paragraph);
                    return new { Length = text.Length, Start = text[..Math.Min(60, text.Length)],
                        End = text[Math.Max(0, text.Length - 60)..], paragraph.Tag };
                }).ToArray() });
            if (!passed) failures.Add(item.Name);
        }
        File.WriteAllText(Path.Combine(output, "native-inline-layout.json"), JsonSerializer.Serialize(new
        {
            Passed = failures.Count == 0, Cases = outcomes,
            Limitation = "Dispatcher construction/cancellation measurements for these fixtures only; no foreground input, document pagination or physical-display certification."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count != 0) throw new IOException("Inline construction delayed input/cancellation or lost text/formatting/source: " + string.Join(", ", failures));
    }

    private static string Read(Paragraph paragraph) => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    private static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph paragraph) yield return paragraph;
            else if (block is Section section)
                foreach (var child in Paragraphs(section.Blocks)) yield return child;
            else if (block is System.Windows.Documents.List list)
                foreach (var item in list.ListItems)
                    foreach (var child in Paragraphs(item.Blocks)) yield return child;
            else if (block is Table table)
                foreach (var group in table.RowGroups)
                    foreach (var row in group.Rows)
                        foreach (var cell in row.Cells)
                            foreach (var child in Paragraphs(cell.Blocks)) yield return child;
        }
    }
    private static IEnumerable<Inline> Flatten(InlineCollection values)
    {
        foreach (var value in values)
        {
            yield return value;
            if (value is Span span) foreach (var child in Flatten(span.Inlines)) yield return child;
        }
    }
    private static void Pump(Task task, Dispatcher dispatcher)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("Inline construction exceeded the validation deadline.");
    }
}
