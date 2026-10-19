using System.Diagnostics;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

internal static class VisualChecks
{
    public static async Task Run(ExperimentWindow window, string output)
    {
        var observations = new List<object>(); NativeTestHighContrast? contrast = null; var restored = false;
        try
        {
            if (NativeTestHighContrast.IsEnabledNow) throw new IOException("Visual comparison requires an initially normal disposable test desktop.");
            foreach (var variant in new[] { "Light", "Dark" })
            {
                var folder = Path.Combine(output, variant); Directory.CreateDirectory(folder);
                var start = Stopwatch.GetTimestamp(); await TargetedChecks.Run(window, folder, true, appearance: variant);
                observations.Add(new { Appearance = variant, StartTimestamp = start, EndTimestamp = Stopwatch.GetTimestamp(), TimingAcceptanceData = false });
            }
            await window.Request("Visual appearance transition marker.", "Dark");
            contrast = await Task.Run(() => new NativeTestHighContrast());
            await Current(true);
            var appearance = ExperimentAppearance.Capture("Dark");
            var hcFolder = Path.Combine(output, "actual-high-contrast"); Directory.CreateDirectory(hcFolder);
            await TargetedChecks.Run(window, hcFolder, true, appearance: appearance);
            var source = window.LatestSource;
            await Task.Run(contrast.Dispose); restored = true;
            await Current(false);
            if (window.LatestSource != source) throw new IOException("Automatic visual comparison restoration changed source.");
            Program.Write(Path.Combine(output, "visual-comparison.json"), new { Status = "CAPTURED_PENDING_VISUAL_REVIEW", Observations = observations, ActualHighContrast = appearance,
                AutomaticEnableCurrentDisableCurrent = true, ManualThemeAfterDisable = false, TimingAcceptanceData = false, PhysicalScreenReaderCertification = false });
        }
        finally { if (!restored && contrast is not null) await Task.Run(contrast.Dispose); }
        async Task Current(bool hc)
        {
            var watch = Stopwatch.StartNew();
            while (NativeTestHighContrast.IsEnabledNow != hc || window.Surface.Snapshot?.Appearance != ExperimentAppearance.Capture("Dark") || window.Surface.Snapshot.Version != window.CurrentVersion)
            { if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Automatic visual appearance transition did not install the current result."); await Task.Delay(10); }
            if (window.CurrentWork is { } work) await work;
        }
    }
}
