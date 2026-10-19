using System.Diagnostics;
using System.Windows.Threading;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

internal static class AppearanceChecks
{
    public static async Task Run(ExperimentWindow window, string output)
    {
        var fixture = ContinuousPreviewFixtures.Create(output, "mixed")[0];
        window.SourcePath = Path.Combine(output, "fixture.md"); window.WorkspaceRoot = output;
        var installations = new List<object>(); var invalid = false;
        window.Installed += snapshot =>
        {
            var current = snapshot.Version == window.CurrentVersion && snapshot.Model.Source == window.LatestSource
                && snapshot.Appearance == ExperimentAppearance.Capture(window.ThemePreference);
            invalid |= !current;
            installations.Add(new { snapshot.Version, snapshot.Appearance, Current = current, ActualOsHighContrast = ExperimentAppearance.ActualHighContrast,
                SourceHash = Program.Hash(System.Text.Encoding.UTF8.GetBytes(snapshot.Model.Source)), Thread = Environment.CurrentManagedThreadId, Timestamp = Stopwatch.GetTimestamp() });
        };
        var watch = Stopwatch.StartNew(); var last = 0d; var gaps = new List<double>(); var timeline = new List<object>(); var captures = new List<object>(); var phase = "Light";
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        heartbeat.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); timeline.Add(new { Phase = phase, Timestamp = Stopwatch.GetTimestamp(), GapMilliseconds = at - last }); last = at; }; heartbeat.Start();
        async Task Capture(string name)
        {
            // Capture is independent correctness instrumentation, outside the declared
            // product clocks. Preserve its blocked dispatcher interval separately.
            await window.Dispatcher.InvokeAsync(() => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); last = at; }, DispatcherPriority.Input);
            heartbeat.Stop(); var start = Stopwatch.GetTimestamp();
            WindowCapture.Save(window, Path.Combine(output, name));
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            captures.Add(new { Name = name, StartTimestamp = start, EndTimestamp = Stopwatch.GetTimestamp(), DiagnosticCaptureInputDeliveryMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency });
            last = watch.Elapsed.TotalMilliseconds; heartbeat.Start();
        }
        NativeTestHighContrast? contrast = null; var disabledByTest = false;
        try
        {
            if (NativeTestHighContrast.IsEnabledNow) throw new IOException("The disposable appearance test requires an initial normal desktop.");
            await window.Request(fixture.Source, "Light"); await Capture("light.png"); phase = "Dark";
            await window.Request(fixture.Source, "Dark"); await Capture("dark.png"); phase = "actual-enable";
            // OS broadcasts can take time; execute the setting request away from the application dispatcher.
            contrast = await Task.Run(() => new NativeTestHighContrast());
            await Wait(() => NativeTestHighContrast.IsEnabledNow && window.Surface.Snapshot?.Appearance == ExperimentAppearance.Capture("Dark")
                && window.Surface.Snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal));
            await Capture("actual-high-contrast.png"); phase = "automatic-disable";
            await Task.Run(contrast.Dispose); disabledByTest = true;
            // No manual Request/ApplyTheme after disable: current preview must update automatically.
            await Wait(() => !NativeTestHighContrast.IsEnabledNow && window.Surface.Snapshot?.Appearance == "Dark" && window.Surface.Snapshot.Model.Source == fixture.Source);
            await Capture("automatic-disable-current.png");
            if (invalid) throw new IOException("A source/version/appearance-stale installation was observed.");
            Program.Write(Path.Combine(output, "appearance.json"), new { Status = gaps.DefaultIfEmpty().Max() <= 100 ? "PASS" : "FAIL", Installations = installations,
                AutomaticEnableCurrentDisableCurrent = "PASS", ManualThemeAfterDisable = false, FailureOnlyRestoration = false,
                InputGapsMilliseconds = gaps, InputTimeline = timeline, DiagnosticCaptures = captures, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), Events = window.Events.ToArray() });
        }
        catch (Exception error)
        {
            Program.Write(Path.Combine(output, "appearance.json"), new { Status = "FAIL", Error = error.ToString(), Installations = installations, InputGapsMilliseconds = gaps,
                FailureOnlyRestoration = !disabledByTest && contrast is not null }); throw;
        }
        finally { heartbeat.Stop(); if (!disabledByTest && contrast is not null) await Task.Run(contrast.Dispose); }
    }
    private static async Task Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Automatic actual OS appearance transition did not produce the current preview."); await Task.Delay(10); }
    }
}
