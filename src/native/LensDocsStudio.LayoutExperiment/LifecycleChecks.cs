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
            ActiveClosure = "PASS", Events = events, Cancellation = cancellation, InputGapsMilliseconds = gaps, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(),
            WholeScenarioMilliseconds = watch.Elapsed.TotalMilliseconds, WinningPreviewReadiness = readiness,
            TabIntegration = "MISSING: isolated diagnostic window", Appearance = "NOT TESTED", Reopen = "PASS" });
    }
    private static async Task Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Active preparation stage was not reached."); await Task.Delay(10); }
    }
}
