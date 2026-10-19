using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Unchanged native product appearance route, isolated state and owning dispatcher.</summary>
internal static class StockAppearanceChecks
{
    public static async Task Run(ExperimentWindow experiment, string output)
    {
        if (NativeTestHighContrast.IsEnabledNow) throw new IOException("The disposable stock control requires initial normal appearance.");
        var fixture = ContinuousPreviewFixtures.Create(output, "mixed")[0];
        var path = Path.Combine(output, "fixture.md"); File.WriteAllText(path, fixture.Source);
        experiment.Hide();
        var stock = await StockWindowForChecks.Open(path, output, Path.Combine(output, "isolated-state"));
        var type = typeof(LensDocsStudio.Native.MainWindow);
        var paletteCurrent = typeof(ThemeService).GetMethod("IsCurrentHighContrast", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<Func<bool, bool>>();
        var preview = (DocumentPreview)stock.Window.FindName("Preview");
        stock.Model.Theme = "Dark";
        type.GetMethod("ApplyTheme", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(stock.Window, null);
        await (Task)type.GetMethod("UpdatePreviewAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(stock.Window, [false])!;
        stock.Window.Activate(); preview.Focus();
        preview.Selection.Select(preview.Document.Blocks.LastBlock.ContentStart, preview.Document.Blocks.LastBlock.ContentEnd);
        var selected = preview.Selection.Text;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        using var trace = new OperationTrace(stock.Window);
        var phase = "before-enable"; var timeline = new List<object>(); var mutations = new List<object>(); var last = Stopwatch.GetTimestamp(); var maximum = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        void Input() { var at = Stopwatch.GetTimestamp(); var gap = (at - last) * 1000d / Stopwatch.Frequency; maximum = Math.Max(maximum, gap); timeline.Add(new { Phase = phase, StartTimestamp = last, EndTimestamp = at, Milliseconds = gap }); last = at; }
        timer.Tick += (_, _) => Input(); timer.Start();
        NativeTestHighContrast? contrast = null; var restored = false; string? error = null;
        try
        {
            foreach (var enabled in new[] { true, false })
            {
                phase = enabled ? "actual-enable" : "automatic-disable";
                var old = preview.Document; var requested = Stopwatch.GetTimestamp(); trace.Stage("stock-os-request", new { Enabled = enabled });
                if (enabled) contrast = await Task.Run(() => new NativeTestHighContrast());
                else { await Task.Run(contrast!.Dispose); restored = true; }
                var returned = Stopwatch.GetTimestamp();
                var waiting = Stopwatch.StartNew();
                while (ReferenceEquals(preview.Document, old) || NativeTestHighContrast.IsEnabledNow != enabled || SystemParameters.HighContrast != enabled
                    || !paletteCurrent(enabled) || (string?)type.GetField("diagnosticPhase", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(stock.Window) != "finished")
                {
                    if (waiting.Elapsed.TotalSeconds > 20) throw new TimeoutException("Automatic stock current preview did not complete.");
                    await Task.Delay(10);
                }
                await stock.Window.Dispatcher.InvokeAsync(Input, DispatcherPriority.Input);
                var sameSelection = preview.Selection.Text == selected; var focus = preview.IsKeyboardFocusWithin;
                mutations.Add(new { Enabled = enabled, RequestTimestamp = requested, OsRequestReturnTimestamp = returned, CompleteInputTimestamp = Stopwatch.GetTimestamp(),
                    SelectionRetained = sameSelection, FocusRetained = focus, SourceUnchanged = File.ReadAllText(path) == fixture.Source,
                    AcceptedGeneration = type.GetField("diagnosticAcceptedGeneration", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(stock.Window),
                    SourceGeneration = type.GetField("diagnosticSourceGeneration", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(stock.Window) });
                trace.Stage("stock-current-input", new { Enabled = enabled, SelectionRetained = sameSelection, FocusRetained = focus });
                if (!sameSelection || !focus || File.ReadAllText(path) != fixture.Source) throw new IOException("Automatic stock appearance lost selection/focus or changed source.");
            }
        }
        catch (Exception exception) { error = exception.ToString(); throw; }
        finally
        {
            timer.Stop();
            try { if (!restored && contrast is not null) await Task.Run(contrast.Dispose); }
            finally
            {
                trace.Dispose(); Program.Write(Path.Combine(output, "appearance-stock-trace.json"), trace.Events);
                Program.Write(Path.Combine(output, "appearance-stock.json"), new { Status = error is null ? "OBSERVED" : "FAIL", DiagnosticOnly = true, Error = error,
                    Route = "Unmodified MainWindow automatic WindowsPreferenceChanged, ApplyTheme, QueuePreview and UpdatePreviewAsync; isolated loader/state",
                    ManualThemeAfterDisable = false, FailureOnlyRestoration = !restored && contrast is not null,
                    MaximumInputGapMilliseconds = maximum, Observed100MillisecondBudgetPass = maximum <= 100, Mutations = mutations, InputTimeline = timeline });
                await stock.Close();
            }
        }
    }
}
