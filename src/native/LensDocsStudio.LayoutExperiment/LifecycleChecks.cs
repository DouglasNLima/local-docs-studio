using System.Diagnostics;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

internal static class LifecycleChecks
{
    public static async Task Run(ExperimentWindow window, string output)
    {
        var fixture = ContinuousPreviewFixtures.Create(output, "list")[0];
        await window.Request(fixture.Source);
        var peer = (LayoutAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(window.Surface);
        var oldRange = ((ITextProvider)peer).DocumentRange;
        var watch = Stopwatch.StartNew(); var gaps = new List<double>(); var last = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); last = at; }; timer.Start();
        var requests = new List<Task<bool>>();
        var failurePublished = false; window.CurrentFailure += _ => failurePublished = true;
        var faultEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var faultRelease = new ManualResetEventSlim();
        window.PreparationBoundaryForChecks = _ =>
        {
            faultEntered.TrySetResult(true);
            if (!faultRelease.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Bounded stale-failure check was not released.");
            throw new IOException("Deliberate obsolete preparation failure.");
        };
        var failing = window.Request(fixture.Source + "\n\nObsolete failure source.");
        var faultVersion = window.CurrentVersion;
        await faultEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        window.PreparationBoundaryForChecks = null;
        var recovered = window.Request(fixture.Source); faultRelease.Set();
        if (await failing || !await recovered || failurePublished || !window.Events.Any(item => item.Event == "stale-failure-rejected"))
            throw new IOException("An obsolete worker failure replaced current content/status.");
        var clipboardEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var clipboardRelease = new ManualResetEventSlim();
        using (var closingClipboard = new ClipboardDispatcher(() =>
        {
            clipboardEntered.TrySetResult(true);
            if (!clipboardRelease.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Bounded clipboard-close check was not released.");
        }))
        {
            var before = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
            var commit = closingClipboard.Plain("Obsolete closed clipboard payload " + Guid.NewGuid(), before, CancellationToken.None);
            await clipboardEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)); closingClipboard.Dispose(); clipboardRelease.Set();
            var rejected = false; try { await commit; } catch (OperationCanceledException) { rejected = true; }
            if (!rejected || LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != before) throw new IOException("Closed clipboard owner granted an obsolete native write.");
        }
        // Active replacement, then a burst that replaces queued requests without
        // starting a thread per edit. Final source is separately identifiable.
        requests.Add(window.Request(fixture.Source + "\n- **obsolete**"));
        var activeVersion = window.CurrentVersion;
        await Wait(() => window.Events.Any(item => item.Event == "worker-format-started" && item.Version == activeVersion));
        for (var i = 0; i < 12; i++) requests.Add(window.Request(fixture.Source.Replace("word4000", "word4000 revision" + i, StringComparison.Ordinal)));
        var winner = fixture.Source.Replace("word4000", "word4000 winning", StringComparison.Ordinal);
        requests.Add(window.Request(winner));
        await Task.WhenAll(requests);
        if (window.Surface.Snapshot?.Model.Source != winner || requests.Take(requests.Count - 1).Any(task => task.Result) || !requests[^1].Result)
            throw new IOException("Stale preparation installed or winning source was lost.");
        var unavailable = false;
        try { oldRange.GetText(-1); } catch (System.Windows.Automation.ElementNotAvailableException) { unavailable = true; }
        if (!unavailable) throw new IOException("An old UIA range retained access to replaced content.");
        var distant = winner.LastIndexOf("word3900", StringComparison.Ordinal); window.Surface.NavigateSourceCore(distant); window.UpdateLayout();
        var anchor = window.Surface.ViewportSourceAnchor();
        var leading = "Leading edit.\n\n" + winner;
        var sequenceBeforeSupersededCopy = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
        window.Surface.Select(0, window.Surface.Snapshot!.Model.Text.Length); window.Surface.Copy();
        // The same dispatcher turn supersedes preparation before any UI grant.
        if (!await window.Request(leading)) throw new IOException("Leading edit did not install.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != sequenceBeforeSupersededCopy) throw new IOException("Superseded clipboard preparation committed old content.");
        var preserved = window.Surface.ViewportSourceAnchor();
        if (preserved.Source != SourceEdit.Transform(winner, leading, anchor.Source) || Math.Abs(preserved.Delta - anchor.Delta) > .01)
            throw new IOException("Leading edit lost the source-based viewport anchor.");
        var distantEdit = leading.Replace("word4000 winning", "word4000 distant winning", StringComparison.Ordinal);
        if (!await window.Request(distantEdit)) throw new IOException("Distant edit did not install.");
        var afterDistant = window.Surface.ViewportSourceAnchor();
        if (afterDistant.Source != preserved.Source || Math.Abs(afterDistant.Delta - preserved.Delta) > .01) throw new IOException("Distant edit moved the earlier source anchor.");
        var closeWork = window.Request(fixture.Source + "\n- **close active**");
        activeVersion = window.CurrentVersion;
        await Wait(() => window.Events.Any(item => item.Event == "worker-format-started" && item.Version == activeVersion));
        window.Close(); await closeWork;
        if (window.CurrentWork is { } work) await work;
        if (window.Surface.Snapshot is not null) throw new IOException("Closed surface retained its layout.");
        var events = window.Events.ToArray();
        var cancellation = events.Where(item => item.Event == "cancellation-requested").GroupBy(item => item.Version).Select(group =>
        {
            var request = group.First(); var worker = events.FirstOrDefault(item => item.Version == request.Version && item.Event == "worker-cancellation-observed");
            var ui = events.FirstOrDefault(item => item.Version == request.Version && item.Event == "dispatcher-cancellation-observed");
            return new { request.Version, WorkerMilliseconds = worker is null ? (double?)null : (worker.Timestamp - request.Timestamp) * 1000d / Stopwatch.Frequency,
                DispatcherMilliseconds = ui is null ? (double?)null : (ui.Timestamp - request.Timestamp) * 1000d / Stopwatch.Frequency };
        }).ToArray();
        var reopen = new ExperimentWindow(); reopen.Show(); await reopen.Request(fixture.Source);
        if (reopen.Surface.Snapshot?.Model.Source != fixture.Source) throw new IOException("Reopened diagnostic window lost source.");
        await reopen.Request("", "Dark");
        if (reopen.Surface.Snapshot?.Model.Text.Length != 0 || reopen.Scroller.Background is not System.Windows.Media.SolidColorBrush background
            || background.Color != System.Windows.Media.Color.FromRgb(32, 33, 36)) throw new IOException("An empty diagnostic document lost its current appearance.");
        reopen.Close(); timer.Stop();
        var readiness = events.Where(item => item.Event == "winning-preview-ready").Select(ready =>
        {
            var requested = events.First(item => item.Version == ready.Version && item.Event == "requested");
            var installed = events.First(item => item.Version == ready.Version && item.Event == "installed");
            var input = events.First(item => item.Version == ready.Version && item.Event == "complete-preview-input-delivered");
            return new { ready.Version, RequestTimestamp = requested.Timestamp, InstallationTimestamp = installed.Timestamp,
                CompleteInputTimestamp = input.Timestamp, WinningPreviewReadyTimestamp = ready.Timestamp,
                WinningPreviewReadyMilliseconds = (ready.Timestamp - requested.Timestamp) * 1000d / Stopwatch.Frequency };
        }).ToArray();
        Program.Write(Path.Combine(output, "lifecycle.json"), new { Status = "PASS", LatestSource = "PASS", OldRangesUnavailable = unavailable,
            ActiveClosure = "PASS", LeadingAndDistantSourceAnchors = "PASS", SupersededCopyBeforeGrant = "PASS", Events = events, Cancellation = cancellation, InputGapsMilliseconds = gaps, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(),
            WholeScenarioMilliseconds = watch.Elapsed.TotalMilliseconds, WinningPreviewReadiness = readiness,
            InjectedFailureVersion = faultVersion, InjectedFailureCancellationObservation = "Deliberate non-cancellation failure; separately asserted stale rejection, not a natural cancellation timing sample",
            CancellationBudgetMilliseconds = 250, CancellationAcceptance = cancellation.Where(item => item.Version != faultVersion).All(item => item.WorkerMilliseconds is <= 250 && item.DispatcherMilliseconds is <= 250) ? "PASS" : "FAIL_OR_UNAVAILABLE",
            WinningPreviewReadinessBudgetMilliseconds = 1500, WinningPreviewReadinessAcceptance = readiness.All(item => item.WinningPreviewReadyMilliseconds <= 1500) ? "PASS" : "FAIL",
            StaleFailure = "PASS: bounded injected real worker failure after supersession, current status unchanged", ClipboardCloseBeforeNativeGrant = "PASS: actual queued STA commit rejected, OS clipboard sequence unchanged",
            TabIntegration = "Separate --studio-checks independent client", Appearance = "Separate --appearance actual OS probe", Reopen = "PASS", EmptyDarkDocument = "PASS", ReopenEvents = reopen.Events.ToArray() });
    }
    private static async Task Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Active preparation stage was not reached."); await Task.Delay(10); }
    }
}
