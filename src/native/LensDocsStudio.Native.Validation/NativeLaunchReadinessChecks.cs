using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.Exceptions;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static class NativeLaunchReadinessChecks
{
    public static void Run(string output)
    {
        using var automation = new UIA3Automation();
        var timeout = new PatternNotSupportedException(automation.PatternLibrary.TransformPattern,
            new COMException("Injected read-only GetCurrentPattern timeout", unchecked((int)0x80131505)));
        var unsupported = new PatternNotSupportedException(automation.PatternLibrary.TransformPattern);
        var denied = new PatternNotSupportedException(automation.PatternLibrary.TransformPattern,
            new COMException("Injected access denied", unchecked((int)0x80070005)));
        var checks = new List<object>();
        var target = new Rectangle(20, 30, 1280, 800);
        var ready = new NativeLaunchWindow(7, 123, false, null, true, 7, 0, true, true, true, 123, 7, true, true, target, target);

        Case("Ready orchestration sends one counted resize", null, null, null, null, 1);
        Case("Known wrapped provider timeout is an observation before one resize", null,
            attempt => attempt == 1 ? timeout : null, null, null, 1, 2);
        Case("Permanent unsupported Transform fails immediately", null, _ => unsupported, null, unsupported, 0, 1);
        Case("Unrelated wrapped provider error propagates immediately", null, _ => denied, null, denied, 0, 1);
        Case("Wrong native process fails before pattern acquisition", _ => ready with { NativePid = 8 }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Wrong UIA process fails before pattern acquisition", _ => ready with { UiaPid = 8 }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Wrong UIA HWND fails before pattern acquisition", _ => ready with { UiaHandle = 124 }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Owned dialogue is not accepted as main window", _ => ready with { Owner = 555 }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Changed process main HWND fails before resize", n => n == 1 ? ready : ready with { ProcessHandle = 124, UiaHandle = 124 }, null, null, typeof(InvalidOperationException), 0, 1);
        Case("Exited process fails before pattern acquisition", _ => ready with { Exited = true, ExitCode = 5 }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Persistent unresponsive window fails within shared budget", _ => ready with { Responding = false }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Persistent invisible window fails within shared budget", _ => ready with { Visible = false }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Unloaded or different owner source cannot resize", _ => ready with { EditorReady = false }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Persistent missing window fails within shared budget", _ => ready with { ProcessHandle = 0, UiaHandle = 0 }, null, null, typeof(InvalidOperationException), 0, 0);
        Case("Persistent Transform timeout fails closed without resize", null, _ => timeout, null, typeof(InvalidOperationException), 0);
        Case("Timed-out resize action is never repeated", null, null, timeout, timeout, 1, 1);
        Case("Timeout observing completed resize never repeats the action", n => n == 3 ? throw timeout : ready, null, null, null, 1, 1);
        Case("Stale bounds after resize require fresh exact bounds", n => n == 3 ? ready with { UiaBounds = Rectangle.Empty } : ready, null, null, null, 1, 1);
        Case("Persistent wrong bounds fail without resize repetition", n => n < 3 ? ready : ready with { Bounds = Rectangle.Empty }, null, null, typeof(InvalidOperationException), 1, 1);
        Case("Owner mismatch after resize fails without repetition", n => n < 3 ? ready : ready with { NativePid = 8 }, null, null, typeof(InvalidOperationException), 1, 1);
        var expiredActions = 0;
        try
        {
            NativeLaunchReadiness.ResizeOnce(Stopwatch.StartNew(), TimeSpan.Zero, () => ready, () => new object(),
                _ => expiredActions++, (_, _, _) => { });
            throw new IOException("Expired launch budget was accepted.");
        }
        catch (InvalidOperationException) { if (expiredActions != 0) throw new IOException("Expired budget sent Resize."); }
        checks.Add(new { Name = "Expired shared launch budget never starts a mutation", ResizeActions = expiredActions });
        File.WriteAllText(Path.Combine(output, "native-launch-readiness-controls.json"), JsonSerializer.Serialize(new
        { Passed = true, Checks = checks, SourceRevision = typeof(NativeLaunchReadinessChecks).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
            Limitation = "Injected provider/window observations and counted action delegates. Actual UIA Transform, installed application and Windows High Contrast require hosted acceptance." },
            new JsonSerializerOptions { WriteIndented = true }));

        void Case(string name, Func<int, NativeLaunchWindow>? observe, Func<int, Exception?>? acquisitionError,
            Exception? actionError, object? expectedFailure, int expectedActions, int? expectedAcquisitions = null)
        {
            var observations = 0; var acquisitions = 0; var actions = 0; var errors = new List<string>();
            Exception? caught = null; var timer = Stopwatch.StartNew();
            try
            {
                NativeLaunchReadiness.ResizeOnce(timer, TimeSpan.FromSeconds(1), () => { observations++; return observe?.Invoke(observations) ?? ready; },
                    () => { acquisitions++; if (acquisitionError?.Invoke(acquisitions) is { } error) throw error; return new object(); },
                    _ => { actions++; if (actionError is not null) throw actionError; },
                    (_, _, error) => { if (error is not null) errors.Add(error.ToString()); });
            }
            catch (Exception error) { caught = error; }
            if (expectedFailure is null ? caught is not null : expectedFailure is Type type ? caught?.GetType() != type : !ReferenceEquals(caught, expectedFailure))
                throw new IOException(name + ": wrong failure outcome.", caught);
            if (actions != expectedActions || expectedAcquisitions.HasValue && acquisitions != expectedAcquisitions)
                throw new IOException(name + ": action/acquisition count differs.");
            if (name.StartsWith("Persistent Transform", StringComparison.Ordinal)
                && (!ReferenceEquals(caught?.InnerException, timeout) || acquisitions < 2 || errors.Count < 2))
                throw new IOException("Persistent provider timeout lost its recorded original error.");
            if (timer.Elapsed >= TimeSpan.FromSeconds(2)) throw new IOException(name + ": shared one-second control budget was enlarged.");
            checks.Add(new { Name = name, Observations = observations, Acquisitions = acquisitions, ResizeActions = actions,
                ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds, ErrorCount = errors.Count, Failure = caught?.ToString() });
        }
    }
}
