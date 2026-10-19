using System.Collections.Immutable;
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
    public string? SourcePath { get; set; }
    public string? WorkspaceRoot { get; set; }
    public string LatestSource { get; private set; } = "";
    public string ThemePreference { get; private set; } = "Light";
    public event Action<LayoutSnapshot>? Installed;
    public event Action<string>? CurrentFailure;
    private readonly Dictionary<int, bool> details = [];
    public Task? CurrentWork { get; private set; }
    public ExperimentWindow()
    {
        Title = "EXPERIMENT ONLY — Continuous TextFormatter layout";
        SizeToContent = SizeToContent.WidthAndHeight; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Scroller = new ScrollViewer { Content = Surface, Width = 640, Height = 300, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible, CanContentScroll = false, Background = System.Windows.Media.Brushes.White };
        Surface.Scroller = Scroller; Scroller.ScrollChanged += (_, _) => Surface.InvalidateVisual();
        Content = Scroller;
        Surface.Activated += node =>
        {
            if (node.Kind == "Details") { details[node.SourceStart] = !node.Expanded; _ = Request(LatestSource, ThemePreference); }
            else if (node.Kind == "Link" && node.Target?.StartsWith('#') == true)
            {
                var heading = Surface.Snapshot?.Model.Headings.FirstOrDefault(item => item.Anchor == node.Target[1..]);
                if (heading is not null) Surface.NavigateSource(heading.SourceStart);
            }
        };
        SystemParameters.StaticPropertyChanged += AppearanceChanged;
        SourceInitialized += (_, _) => ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this)).AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        { if (message is 0x001A or 0x0015 or 0x031A) _ = Dispatcher.InvokeAsync(RefreshAppearance, DispatcherPriority.Background); return IntPtr.Zero; });
        Closed += (_, _) => { closed = true; SystemParameters.StaticPropertyChanged -= AppearanceChanged; version++; if (active is not null) { Log("cancellation-requested", activeVersion); active.Cancel(); } pending?.Completion.TrySetCanceled(); pending = null; Surface.Install(null); Log("closed"); };
    }
    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { if (!closed) _ = Dispatcher.InvokeAsync(RefreshAppearance, DispatcherPriority.Background); }
    private void RefreshAppearance()
    {
        if (!closed && LatestSource.Length > 0 && Surface.Snapshot?.Appearance != ExperimentAppearance.Capture(ThemePreference)) _ = Request(LatestSource, ThemePreference);
    }
    private void Log(string name, long? owner = null) => Events.Enqueue(new(name, Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, owner ?? Volatile.Read(ref version)));
    public Task<bool> Request(string source, string appearance = "Light")
    {
        Dispatcher.VerifyAccess(); if (closed) return Task.FromCanceled<bool>(new CancellationToken(true));
        LatestSource = source;
        ThemePreference = appearance.StartsWith("HC:", StringComparison.Ordinal) ? ThemePreference : appearance;
        appearance = ExperimentAppearance.Capture(ThemePreference);
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
                var sourcePath = SourcePath; var workspaceRoot = WorkspaceRoot; var detailState = details.ToDictionary();
                var old = Surface.Snapshot;
                var selectionStart = Surface.SelectionSourceStart; var selectionEnd = Surface.SelectionSourceEnd;
                var anchor = Surface.ViewportSourceAnchor();
                try
                {
                    Log("worker-queued");
                    var snapshot = await Worker(() =>
                    {
                        Log("worker-started", request.Version);
                        try
                        {
                            var model = CompleteLayout.Parse(request.Source, token, sourcePath, workspaceRoot, request.Appearance);
                            if (detailState.Count > 0) model = model with { Root = ApplyDetails(model.Root, detailState), OriginalListShape = false };
                            Log("worker-format-started", request.Version);
                            var result = CompleteLayout.Format(model, 640, request.Version, request.Appearance, token);
                            Log("worker-completed", request.Version); return result;
                        }
                        catch (OperationCanceledException) { Log("worker-cancellation-observed", request.Version); throw; }
                    });
                    if (closed || request.Version != version || token.IsCancellationRequested || request.Appearance != ExperimentAppearance.Capture(ThemePreference))
                    { Log("stale-rejected", request.Version); request.Completion.TrySetResult(false); continue; }
                    Surface.Install(snapshot);
                    if (old is not null) { Surface.SelectSource(SourceEdit.Transform(old.Model.Source, request.Source, selectionStart), SourceEdit.Transform(old.Model.Source, request.Source, selectionEnd)); Surface.RestoreViewportSourceAnchor((SourceEdit.Transform(old.Model.Source, request.Source, anchor.Source), anchor.Delta)); }
                    Log("installed", request.Version); Installed?.Invoke(snapshot); UpdateLayout();
                    await Dispatcher.InvokeAsync(() => Log("complete-preview-input-delivered", request.Version), DispatcherPriority.Input);
                    if (closed || request.Version != version || request.Appearance != ExperimentAppearance.Capture(ThemePreference))
                    { Log("stale-readiness-rejected", request.Version); request.Completion.TrySetResult(false); }
                    else { Log("winning-preview-ready", request.Version); request.Completion.TrySetResult(true); }
                }
                catch (OperationCanceledException) { Log("dispatcher-cancellation-observed", request.Version); request.Completion.TrySetResult(false); }
                catch (Exception error)
                {
                    if (closed || request.Version != version || token.IsCancellationRequested) { Log("stale-failure-rejected", request.Version); request.Completion.TrySetResult(false); }
                    else { Log("current-failure", request.Version); CurrentFailure?.Invoke(error.Message); request.Completion.TrySetException(error); }
                }
                finally { active.Dispose(); active = null; }
            }
        }
        finally { running = false; }
    }
    private static SemanticNode ApplyDetails(SemanticNode node, IReadOnlyDictionary<int, bool> states) => node with
    { Expanded = node.Kind == "Details" && states.TryGetValue(node.SourceStart, out var expanded) ? expanded : node.Expanded,
        Children = node.Children.Select(child => ApplyDetails(child, states)).ToImmutableArray() };
    public static Task<T> Worker<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { completion.TrySetResult(work()); } catch (Exception error) { completion.TrySetException(error); } })
            { IsBackground = true, Name = "Continuous layout experiment STA" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
