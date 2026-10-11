using System.Diagnostics;
using System.Text.Json;
using System.Windows.Documents;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeLayoutChecks
{
    public static void Run(string output)
    {
        var source = string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Paragraph {i}: **Evidence** &amp; review remains selectable."));
        var parsed = new MarkdownService().Parse(source);
        var renderer = new NativeMarkdownRenderer(new NativeSvgService());
        var diagrams = new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var watch = Stopwatch.StartNew(); var inputMilliseconds = -1d;
        dispatcher.BeginInvoke(DispatcherPriority.Input, () => inputMilliseconds = watch.Elapsed.TotalMilliseconds);
        var pending = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { });
        if (pending.IsCompleted) throw new IOException("Large native layout did not yield to the dispatcher.");
        Pump(pending, dispatcher);
        var rendered = pending.GetAwaiter().GetResult(); var totalMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (inputMilliseconds < 0 || inputMilliseconds >= totalMilliseconds) throw new IOException("Input did not run during native layout preparation.");
        if (rendered.Document.Blocks.Count != 1200 || rendered.SourceElements.Count != 1200 || rendered.Warnings.Count != 0
            || !new TextRange(rendered.Document.ContentStart, rendered.Document.ContentEnd).Text.Contains("Paragraph 1200: Evidence & review remains selectable.", StringComparison.Ordinal))
            throw new IOException("Batched layout lost source mapping, paragraphs or decoded text.");
        using var cancellation = new CancellationTokenSource();
        dispatcher.BeginInvoke(DispatcherPriority.Input, () => cancellation.Cancel());
        var cancelled = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { }, cancellationToken: cancellation.Token);
        Pump(cancelled, dispatcher);
        try { cancelled.GetAwaiter().GetResult(); throw new IOException("New input did not cancel obsolete native layout."); }
        catch (OperationCanceledException) { }
        var beforeStart = renderer.RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { }, cancellationToken: new(true));
        if (!beforeStart.IsCanceled) throw new IOException("Already-cancelled native layout allocated a completed result.");
        File.WriteAllText(Path.Combine(output, "native-layout.json"), JsonSerializer.Serialize(new
        {
            Passed = true, SourceCharacters = source.Length, Blocks = rendered.Document.Blocks.Count,
            InputMilliseconds = inputMilliseconds, ConstructionMilliseconds = totalMilliseconds,
            Checks = new[] { "Input runs before a large native document finishes construction", "Batched rendering retains every paragraph, source span and decoded text",
                "New input cancels obsolete native construction", "Cancellation before construction returns no document" },
            Limitation = "Measures FlowDocument construction and dispatcher input scheduling, not final viewer pagination, scrolling or individual oversized block latency."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Pump(Task task, Dispatcher dispatcher)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("Native layout did not finish within the validation deadline.");
    }
}
