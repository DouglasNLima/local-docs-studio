using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Minimal public WPF resource/broadcast control, never a renderer substitute.</summary>
internal static class AppearanceControlChecks
{
    public static async Task Run(ExperimentWindow window, string output)
    {
        if (NativeTestHighContrast.IsEnabledNow) throw new IOException("The disposable appearance control requires an initially normal desktop.");
        window.BeginDocument(null); window.Title = "CONTROL ONLY — native appearance notifications";
        var text = new TextBlock { Text = "Native appearance control", FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Margin = new Thickness(28) };
        text.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.WindowTextBrushKey);
        var border = new Border { Child = text, Width = 640, Height = 300 };
        border.SetResourceReference(Border.BackgroundProperty, SystemColors.WindowBrushKey);
        window.Content = border; window.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        using var trace = new OperationTrace(window);
        var phase = "initial"; var gaps = new List<object>(); var last = Stopwatch.GetTimestamp(); var maximum = 0d;
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        void Input()
        {
            var at = Stopwatch.GetTimestamp(); var milliseconds = (at - last) * 1000d / Stopwatch.Frequency;
            maximum = Math.Max(maximum, milliseconds); gaps.Add(new { Phase = phase, StartTimestamp = last, EndTimestamp = at, Milliseconds = milliseconds }); last = at;
        }
        heartbeat.Tick += (_, _) => Input(); heartbeat.Start();
        var mutations = new List<object>(); NativeTestHighContrast? contrast = null; var restored = false;
        try
        {
            phase = "actual-enable"; var requested = Stopwatch.GetTimestamp();
            contrast = await Task.Run(() => new NativeTestHighContrast()); var returned = Stopwatch.GetTimestamp();
            await Current(true); await window.Dispatcher.InvokeAsync(Input, DispatcherPriority.Input);
            mutations.Add(new { Enabled = true, RequestTimestamp = requested, OsRequestReturnTimestamp = returned, CurrentInputTimestamp = Stopwatch.GetTimestamp() });
            phase = "automatic-disable"; requested = Stopwatch.GetTimestamp();
            await Task.Run(contrast.Dispose); restored = true; returned = Stopwatch.GetTimestamp();
            await Current(false); await window.Dispatcher.InvokeAsync(Input, DispatcherPriority.Input);
            mutations.Add(new { Enabled = false, RequestTimestamp = requested, OsRequestReturnTimestamp = returned, CurrentInputTimestamp = Stopwatch.GetTimestamp() });
            if (window.Events.Any(row => row.Event == "worker-queued")) throw new IOException("Candidate preparation contaminated the minimal appearance control.");
            Program.Write(Path.Combine(output, "appearance-control.json"), new { Status = "OBSERVED", DiagnosticOnly = true,
                Route = "Shown WPF window, TextBlock and automatic public system colour resources; no document/model/formatting worker",
                AutomaticEnableCurrentDisableCurrent = true, ManualThemeAfterDisable = false,
                MaximumInputGapMilliseconds = maximum, ObservedInputBudgetMilliseconds = 100, ObservedInputBudgetPass = maximum <= 100,
                Meaning = "Same-host comparative control only; its observations neither exempt candidate delays nor change acceptance semantics.",
                Mutations = mutations, InputTimeline = gaps });
        }
        catch (Exception error)
        {
            Program.Write(Path.Combine(output, "appearance-control.json"), new { Status = "FAIL", DiagnosticOnly = true, Error = error.ToString(),
                MaximumInputGapMilliseconds = maximum, Mutations = mutations, InputTimeline = gaps, FailureOnlyRestoration = !restored && contrast is not null });
            throw;
        }
        finally
        {
            heartbeat.Stop();
            try { if (!restored && contrast is not null) await Task.Run(contrast.Dispose); }
            finally { trace.Dispose(); Program.Write(Path.Combine(output, "appearance-control-trace.json"), trace.Events); }
        }
        async Task Current(bool enabled)
        {
            var watch = Stopwatch.StartNew();
            while (NativeTestHighContrast.IsEnabledNow != enabled || SystemParameters.HighContrast != enabled
                || border.Background is not SolidColorBrush background || background.Color != SystemColors.WindowColor
                || text.Foreground is not SolidColorBrush foreground || foreground.Color != SystemColors.WindowTextColor)
            {
                if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Automatic minimal WPF system resources did not become current.");
                await Task.Delay(10);
            }
        }
    }
}
