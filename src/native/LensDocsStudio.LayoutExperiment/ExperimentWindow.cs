using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record DiagnosticView(int SelectionStart, int SelectionEnd, int AnchorSource, double AnchorDelta, Dictionary<int, bool> Details);

internal sealed record PreparationEvent(string Event, long Timestamp, int Thread, long Version);

/// <summary>One active preparation and one replaceable pending request; no product state.</summary>
internal sealed class ExperimentWindow : Window
{
    public LayoutSurface Surface { get; } = new();
    public ScrollViewer Scroller { get; }
    public System.Collections.Concurrent.ConcurrentQueue<PreparationEvent> Events { get; } = new();
    private int droppedPreparationObservations;
    public int DroppedPreparationObservations => Volatile.Read(ref droppedPreparationObservations);
    private static int activeWorkerDispatchers, createdWorkerDispatchers, releasedWorkerDispatchers;
    internal static int ActiveWorkerDispatchers => Volatile.Read(ref activeWorkerDispatchers);
    internal static object WorkerOwnership => new { Active = ActiveWorkerDispatchers, Created = Volatile.Read(ref createdWorkerDispatchers), Released = Volatile.Read(ref releasedWorkerDispatchers) };
    private CancellationTokenSource? active;
    private sealed record RequestData(string Source, string Appearance, long Version, string? Path, string? Root, Dictionary<int, bool> Details, DiagnosticView? Restore, TaskCompletionSource<bool> Completion);
    private RequestData? pending;
    private RequestData? activeRequest;
    private DiagnosticView? restoreView;
    private bool running, closed;
    private bool hasDocument;
    private long version;
    private long activeVersion;
    public long CurrentVersion => version;
    public string? SourcePath { get; set; }
    public string? WorkspaceRoot { get; set; }
    public string ExportRecoveryDirectory { get; } = Path.Combine(Path.GetTempPath(), "LensDocsStudio.LayoutExperiment", Guid.NewGuid().ToString("N"), "export-recovery");
    public string LatestSource { get; private set; } = "";
    public string ThemePreference { get; private set; } = "Light";
    public event Action<LayoutSnapshot>? Installed;
    public event Action<string>? CurrentFailure;
    public event Action? PreparationRequested;
    private readonly Dictionary<int, bool> details = [];
    public Task? CurrentWork { get; private set; }
    public OperationTrace? Trace { get; set; }
    internal bool LegacyMediaLocationForChecks { get; set; }
    internal Action<long>? PreparationBoundaryForChecks { get; set; }
    private CancellationTokenSource? renderedCopy;
    private DispatcherOperation? appearanceRefresh;
    private int appearanceQueued;
    public ExperimentWindow()
    {
        Title = "EXPERIMENT ONLY — Continuous TextFormatter layout";
        SizeToContent = SizeToContent.WidthAndHeight; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Scroller = new ScrollViewer { Content = Surface, Width = 640, Height = 300, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible, CanContentScroll = false, Background = System.Windows.Media.Brushes.White };
        Surface.Scroller = Scroller; Scroller.ScrollChanged += (_, _) => Surface.ViewportChanged();
        Content = Scroller;
        Surface.IsCurrent = snapshot => !closed && snapshot.Version == version && snapshot.Model.Source == LatestSource
            && snapshot.Appearance == ExperimentAppearance.Capture(ThemePreference);
        _ = new MediaActions(this);
        Surface.SourceNavigationRequested += async source =>
        {
            var snapshot = Surface.Snapshot; if (snapshot is null) return;
            var collapsed = (snapshot.CompleteModel ?? snapshot.Model).Root.Descendants().Where(node => node.Kind == "Details" && !node.Expanded && source > node.Children[0].SourceEnd && source <= node.SourceEnd).ToArray();
            foreach (var node in collapsed) details[node.SourceStart] = true;
            if (collapsed.Length > 0 && !await Request(LatestSource, ThemePreference)) return;
            Surface.NavigateSourceCore(source);
        };
        Surface.SearchRequested += async query =>
        {
            if (query.Length == 0 || Surface.Snapshot?.CompleteModel is not { } complete) return;
            foreach (var node in complete.Root.Descendants().Where(node => node.Kind == "Details" && !node.Expanded)) details[node.SourceStart] = true;
            if (await Request(LatestSource, ThemePreference)) Surface.Search(query);
        };
        Surface.Activated += node =>
        {
            if (node.Kind == "Details") { details[node.SourceStart] = !node.Expanded; _ = Request(LatestSource, ThemePreference); }
            else if (node.Kind == "Link" && node.Target?.StartsWith('#') == true)
            {
                var heading = Surface.Snapshot?.Model.Headings.FirstOrDefault(item => item.Anchor == node.Target[1..]);
                if (heading is not null) Surface.NavigateSource(heading.SourceStart);
            }
        };
        var copyMenu = new MenuItem { Header = "Copy rendered document" };
        System.Windows.Automation.AutomationProperties.SetAutomationId(copyMenu, "ExperimentalCopyRendered");
        copyMenu.Click += async (_, _) => await CopyRendered();
        Surface.ContextMenu = new ContextMenu { Items = { copyMenu } };
        SystemParameters.StaticPropertyChanged += AppearanceChanged;
        SourceInitialized += (_, _) => ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this)).AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        { if (message is 0x001A or 0x0015 or 0x031A) ScheduleAppearance(); return IntPtr.Zero; });
        Closed += (_, _) =>
        {
            closed = true; appearanceRefresh?.Abort(); appearanceRefresh = null; renderedCopy?.Cancel(); Surface.CloseClipboard();
            SystemParameters.StaticPropertyChanged -= AppearanceChanged; version++;
            if (active is not null) { Log("cancellation-requested", activeVersion); active.Cancel(); }
            pending?.Completion.TrySetCanceled(); pending = null; Surface.Install(null);
            Scroller.Content = null; Surface.ReleaseOwner(); Content = null; LatestSource = ""; SourcePath = WorkspaceRoot = null; hasDocument = false;
            Installed = null; CurrentFailure = null; PreparationRequested = null; PreparationBoundaryForChecks = null; Log("closed");
        };
    }
    public DiagnosticView CaptureView()
    {
        var anchor = Surface.ViewportSourceAnchor();
        return new(Surface.SelectionSourceStart, Surface.SelectionSourceEnd, anchor.Source, anchor.Delta, details.ToDictionary());
    }
    public void BeginDocument(DiagnosticView? view)
    {
        Dispatcher.VerifyAccess(); renderedCopy?.Cancel();
        if (active is not null) { Log("cancellation-requested", activeVersion); active.Cancel(); }
        if (pending is { } queued) { Log("queued-rejected", queued.Version); queued.Completion.TrySetResult(false); pending = null; }
        version++; PreparationRequested?.Invoke(); Surface.Install(null); Scroller.ScrollToTop(); LatestSource = ""; hasDocument = false;
        details.Clear(); if (view is not null) foreach (var value in view.Details) details.Add(value.Key, value.Value);
        restoreView = view; Log("document-switched");
    }
    public async Task CopyRendered()
    {
        if (Surface.Snapshot is not { } snapshot || Surface.IsCurrent?.Invoke(snapshot) != true) return;
        renderedCopy?.Cancel(); var owner = new CancellationTokenSource(); renderedCopy = owner;
        var sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence; var path = SourcePath; var root = WorkspaceRoot;
        try
        {
            var payload = await Worker(() => new LensDocsStudio.Core.Exports.ClipboardExporter().Export(
                new LensDocsStudio.Native.Exports.ExportPreparationService(new LensDocsStudio.Core.Parsing.MarkdownService(),
                    new LensDocsStudio.Core.Rendering.DiagramService(), new LensDocsStudio.Native.Rendering.NativeSvgService())
                .Prepare(snapshot.Model.Source, path, root, snapshot.Appearance == "Dark", owner.Token), cancellationToken: owner.Token));
            if (owner.IsCancellationRequested || Surface.IsCurrent?.Invoke(snapshot) != true) { Log("rendered-copy-stale-rejected"); return; }
            var commit = await Surface.ClipboardWorker.Rendered(payload, sequence, owner.Token);
            Surface.RecordInteraction(new { Stage = "rendered-copy", NativeCommit = commit,
                NativeCommitMilliseconds = (commit.EndTimestamp - commit.StartTimestamp) * 1000d / Stopwatch.Frequency });
            if (!owner.IsCancellationRequested && Surface.IsCurrent?.Invoke(snapshot) == true) Log("rendered-copy-committed");
            else Log("rendered-copy-stale-completion-rejected");
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or System.Runtime.InteropServices.ExternalException)
        { if (Surface.IsCurrent?.Invoke(snapshot) == true) CurrentFailure?.Invoke(error.Message); else Log("rendered-copy-stale-failure-rejected"); }
        finally { if (ReferenceEquals(renderedCopy, owner)) renderedCopy = null; owner.Dispose(); }
    }
    public void InstallMeasured(LayoutSnapshot snapshot)
    {
        Dispatcher.VerifyAccess(); LatestSource = snapshot.Model.Source; version = snapshot.Version; hasDocument = true;
        ThemePreference = snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal) ? ThemePreference : snapshot.Appearance;
        Surface.Install(snapshot);
    }
    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ScheduleAppearance();
    private void ScheduleAppearance()
    {
        if (closed || Dispatcher.HasShutdownStarted) return;
        if (Interlocked.CompareExchange(ref appearanceQueued, 1, 0) != 0) { Trace?.Stage("appearance-notification-coalesced"); return; }
        Trace?.Stage("appearance-notification-queued");
        appearanceRefresh = Dispatcher.InvokeAsync(() => { Volatile.Write(ref appearanceQueued, 0); appearanceRefresh = null; RefreshAppearance(); }, DispatcherPriority.Background);
    }
    private void RefreshAppearance()
    {
        Trace?.Stage("appearance-refresh-enter");
        if (closed || !hasDocument) return;
        var appearance = ExperimentAppearance.Capture(ThemePreference);
        Trace?.Stage("appearance-palette-captured", new { Appearance = appearance });
        if (Surface.Snapshot?.Appearance == appearance) return;
        // Windows broadcasts several related property changes for one palette.
        // Do not repeatedly cancel the identical current rebuild before it installs.
        if (pending is { } queued && queued.Source == LatestSource && queued.Appearance == appearance) return;
        if (activeRequest is { } preparing && preparing.Version == version && preparing.Source == LatestSource && preparing.Appearance == appearance) return;
        _ = Request(LatestSource, ThemePreference);
    }
    private void Log(string name, long? owner = null)
    {
        Events.Enqueue(new(name, Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, owner ?? Volatile.Read(ref version)));
        while (Events.Count > 8192 && Events.TryDequeue(out _)) Interlocked.Increment(ref droppedPreparationObservations);
    }
    public Task<bool> Request(string source, string appearance = "Light")
    {
        Dispatcher.VerifyAccess(); if (closed) return Task.FromCanceled<bool>(new CancellationToken(true));
        LatestSource = source; hasDocument = true;
        ThemePreference = appearance.StartsWith("HC:", StringComparison.Ordinal) ? ThemePreference : appearance;
        appearance = ExperimentAppearance.Capture(ThemePreference);
        version++;
        Surface.CancelCopyPreparation(); renderedCopy?.Cancel(); PreparationRequested?.Invoke();
        if (active is not null) { Log("cancellation-requested", activeVersion); active.Cancel(); }
        if (pending is { } old) { Log("queued-rejected", old.Version); old.Completion.TrySetResult(false); }
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = new(source, appearance, version, SourcePath, WorkspaceRoot, details.ToDictionary(), restoreView, completion); restoreView = null; Log("requested");
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
                pending = null; activeRequest = request; active = new(); activeVersion = request.Version; var token = active.Token;
                var sourcePath = request.Path; var workspaceRoot = request.Root; var detailState = request.Details;
                var preparationBoundary = PreparationBoundaryForChecks;
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
                            preparationBoundary?.Invoke(request.Version);
                            if (detailState.Count > 0) model = model with { Root = ApplyDetails(model.Root, detailState, request.Appearance), OriginalListShape = false };
                            Log("worker-format-started", request.Version);
                            var result = CompleteLayout.Format(model, 640, request.Version, request.Appearance, token);
                            Log("worker-completed", request.Version); return result;
                        }
                        catch (OperationCanceledException) { Log("worker-cancellation-observed", request.Version); throw; }
                    });
                    if (closed || request.Version != version || token.IsCancellationRequested || request.Appearance != ExperimentAppearance.Capture(ThemePreference))
                    { Log("stale-rejected", request.Version); request.Completion.TrySetResult(false); continue; }
                    Trace?.Stage("snapshot-install-enter", new { request.Version, request.Appearance });
                    var hadDocumentFocus = Surface.IsKeyboardFocusWithin;
                    Surface.Install(snapshot);
                    Trace?.Stage("snapshot-install-exit", new { request.Version });
                    if (old is not null) { Surface.SelectSource(SourceEdit.Transform(old.Model.Source, request.Source, selectionStart), SourceEdit.Transform(old.Model.Source, request.Source, selectionEnd)); Surface.RestoreViewportSourceAnchor((SourceEdit.Transform(old.Model.Source, request.Source, anchor.Source), anchor.Delta)); }
                    else if (request.Restore is { } restored) { Surface.SelectSource(restored.SelectionStart, restored.SelectionEnd); Surface.RestoreViewportSourceAnchor((restored.AnchorSource, restored.AnchorDelta)); }
                    Log("installed", request.Version); Installed?.Invoke(snapshot);
                    Trace?.Stage("window-layout-enter", new { request.Version }); UpdateLayout();
                    if (hadDocumentFocus) Surface.Focus();
                    Trace?.Stage("window-layout-exit", new { request.Version });
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
                finally { active.Dispose(); active = null; activeRequest = null; }
            }
        }
        finally { running = false; }
    }
    private static SemanticNode ApplyDetails(SemanticNode node, IReadOnlyDictionary<int, bool> states, string appearance) => node with
    { Expanded = node.Kind == "Details" && states.TryGetValue(node.SourceStart, out var expanded) ? expanded : node.Expanded,
        Children = node.Children.Select(child => ApplyDetails(child, states, appearance)).ToImmutableArray(),
        Media = node.Kind == "Toggle" && states.TryGetValue(node.SourceStart, out var toggleExpanded) ? DetailsDrawing.Create(toggleExpanded, appearance) : node.Media };
    public static Task<T> Worker<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            // Public WPF controls/resources can create an owning dispatcher on
            // this STA. Shut it down here, before publishing frozen results or
            // failures, so its native notification windows cannot outlive work.
            var dispatcher = Dispatcher.CurrentDispatcher;
            Interlocked.Increment(ref createdWorkerDispatchers); Interlocked.Increment(ref activeWorkerDispatchers);
            T result = default!; Exception? failure = null;
            try
            {
                result = work();
            }
            catch (Exception error) { failure = error; }
            try
            {
                dispatcher.InvokeShutdown();
                Interlocked.Increment(ref releasedWorkerDispatchers);
            }
            catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
            finally { Interlocked.Decrement(ref activeWorkerDispatchers); }
            if (failure is not null) completion.TrySetException(failure); else completion.TrySetResult(result);
        })
            { IsBackground = true, Name = "Continuous layout experiment STA" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
