using System.Reflection;
using System.Text.Json;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

// UI Automation normally focuses a ValuePattern target before calling its provider.
// Suppress that client behaviour only for the already authorised explicit owner
// mutation. This does not activate a window, bypass a guard or restore OS focus.
internal static class NativeHostAutomaticFocus
{
    public static object Run(UIA3Automation automation, Action action)
    {
        var native = automation.NativeAutomation2;
        var previous = native.AutoSetFocus;
        var started = DateTimeOffset.UtcNow;
        int during;
        int restored;
        try
        {
            native.AutoSetFocus = 0;
            during = native.AutoSetFocus;
            if (during != 0) throw new IOException("Automatic UIA focus could not be disabled; no owner mutation sent.");
            action();
        }
        finally
        {
            native.AutoSetFocus = previous;
            restored = native.AutoSetFocus;
            if (restored != previous) throw new IOException("The UIA client's automatic focus setting was not restored.");
        }
        return new { startedAtUtc = started, completedAtUtc = DateTimeOffset.UtcNow, previous, during, restored,
            scope = "Temporary UIA client option only; exact foreground/owner guards remain mandatory" };
    }

    public static void RunControls(string output)
    {
        using var automation = new UIA3Automation();
        var native = automation.NativeAutomation2;
        var initial = native.AutoSetFocus;
        var rows = new List<object>();
        try
        {
            foreach (var prior in new[] { 0, 1 })
            {
                native.AutoSetFocus = prior;
                var calls = 0;
                var successful = Run(automation, () =>
                {
                    calls++;
                    if (native.AutoSetFocus != 0) throw new IOException("The actual UIA client still enables automatic focus.");
                });
                if (calls != 1 || native.AutoSetFocus != prior) throw new IOException("Successful scope did not preserve the client setting.");
                var sentinel = new InvalidOperationException("Synthetic callback rejection");
                var rejected = false;
                try { Run(automation, () => { calls++; throw sentinel; }); }
                catch (InvalidOperationException error) when (ReferenceEquals(error, sentinel)) { rejected = true; }
                if (!rejected || calls != 2 || native.AutoSetFocus != prior) throw new IOException("Rejected scope retried or failed to restore the client setting.");
                rows.Add(new { prior, successful, exceptionPreserved = rejected, callbackCount = calls, restored = native.AutoSetFocus });
            }
        }
        finally { native.AutoSetFocus = initial; }
        using var stream = new FileStream(Path.Combine(output, "automatic-focus-controls.json"), FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, new { atUtc = DateTimeOffset.UtcNow, status = "PASS", initial,
            source = typeof(NativeHostAutomaticFocus).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            scope = "Actual UIA client option and exception restoration; no window discovery, focus action or document mutation", guiAcceptance = "NOT RUN", rows },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
