using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record PreparationEvent(string Event, long Timestamp, int Thread, long Version);

/// <summary>One active preparation and one replaceable pending request; no product state.</summary>
internal sealed class ExperimentWindow : Window
{
    public LayoutSurface Surface { get; } = new();
    public ScrollViewer Scroller { get; }
    public System.Collections.Concurrent.ConcurrentQueue<PreparationEvent> Events { get; } = new();
    private CancellationTokenSource? active;
    private (string Source, string Appearance, long Version, TaskCompletionSource<bool> Completion)? pending;
    private bool running, closed;
    private long version;
    private long activeVersion;
    public long CurrentVersion => version;
    public Task? CurrentWork { get; private set; }
    public ExperimentWindow()
    {
        Title = "EXPERIMENT ONLY — Continuous TextFormatter layout";
        SizeToContent = SizeToContent.WidthAndHeight; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Scroller = new ScrollViewer { Content = Surface, Width = 640, Height = 300, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible, CanContentScroll = false, Background = System.Windows.Media.Brushes.White };
        Surface.Scroller = Scroller; Scroller.ScrollChanged += (_, _) => Surface.InvalidateVisual();
        Content = Scroller;
        Closed += (_, _) => { closed = true; version++; active?.Cancel(); pending?.Completion.TrySetCanceled(); pending = null; Surface.Install(null); Log("closed"); };
    }
    private void Log(string name, long? owner = null) => Events.Enqueue(new(name, Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, owner ?? Volatile.Read(ref version)));
    public Task<bool> Request(string source, string appearance = "Light")
    {
        Dispatcher.VerifyAccess(); if (closed) return Task.FromCanceled<bool>(new CancellationToken(true));
        version++;
        if (active is not null) { Log("cancellation-requested", activeVersion); active.Cancel(); }
        if (pending is { } old) { Log("queued-rejected", old.Version); old.Completion.TrySetResult(false); }
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = (source, appearance, version, completion); Log("requested");
        if (!running) CurrentWork = Process();
        return completion.Task;
    }
    private async Task Process()
    {
        running = true;
        try
        {
            while (pending is { } request && !closed)
            {
                pending = null; active = new(); activeVersion = request.Version; var token = active.Token;
                try
                {
                    Log("worker-queued");
                    var snapshot = await Worker(() =>
                    {
                        Log("worker-started", request.Version);
                        try
                        {
                            var model = CompleteLayout.Parse(request.Source, token);
                            Log("worker-format-started", request.Version);
                            var result = CompleteLayout.Format(model, 640, request.Version, request.Appearance, token);
                            Log("worker-completed", request.Version); return result;
                        }
                        catch (OperationCanceledException) { Log("worker-cancellation-observed", request.Version); throw; }
                    });
                    if (closed || request.Version != version || token.IsCancellationRequested)
                    { Log("stale-rejected", request.Version); request.Completion.TrySetResult(false); continue; }
                    Surface.Install(snapshot); Log("installed", request.Version); request.Completion.TrySetResult(true);
                }
                catch (OperationCanceledException) { Log("dispatcher-cancellation-observed", request.Version); request.Completion.TrySetResult(false); }
                catch (Exception error) { request.Completion.TrySetException(error); }
                finally { active.Dispose(); active = null; }
            }
        }
        finally { running = false; }
    }
    public static Task<T> Worker<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { completion.TrySetResult(work()); } catch (Exception error) { completion.TrySetException(error); } })
            { IsBackground = true, Name = "Continuous layout experiment STA" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
