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
        var watch = Stopwatch.StartNew(); var last = 0d; var gaps = new List<double>();
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        heartbeat.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); last = at; }; heartbeat.Start();
        NativeTestHighContrast? contrast = null; var disabledByTest = false;
        try
        {
            if (NativeTestHighContrast.IsEnabledNow) throw new IOException("The disposable appearance test requires an initial normal desktop.");
            await window.Request(fixture.Source, "Light"); WindowCapture.Save(window, Path.Combine(output, "light.png"));
            await window.Request(fixture.Source, "Dark"); WindowCapture.Save(window, Path.Combine(output, "dark.png"));
            // OS broadcasts can take time; execute the setting request away from the application dispatcher.
            contrast = await Task.Run(() => new NativeTestHighContrast());
            await Wait(() => NativeTestHighContrast.IsEnabledNow && window.Surface.Snapshot?.Appearance == ExperimentAppearance.Capture("Dark")
                && window.Surface.Snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal));
            WindowCapture.Save(window, Path.Combine(output, "actual-high-contrast.png"));
            await Task.Run(contrast.Dispose); disabledByTest = true;
            // No manual Request/ApplyTheme after disable: current preview must update automatically.
            await Wait(() => !NativeTestHighContrast.IsEnabledNow && window.Surface.Snapshot?.Appearance == "Dark" && window.Surface.Snapshot.Model.Source == fixture.Source);
            WindowCapture.Save(window, Path.Combine(output, "automatic-disable-current.png"));
            if (invalid) throw new IOException("A source/version/appearance-stale installation was observed.");
            Program.Write(Path.Combine(output, "appearance.json"), new { Status = gaps.DefaultIfEmpty().Max() <= 100 ? "PASS" : "FAIL", Installations = installations,
                AutomaticEnableCurrentDisableCurrent = "PASS", ManualThemeAfterDisable = false, FailureOnlyRestoration = false,
                InputGapsMilliseconds = gaps, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), Events = window.Events.ToArray() });
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
