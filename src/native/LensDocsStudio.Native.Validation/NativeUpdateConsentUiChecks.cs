using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunUpdateConsent(string executable, string output, bool expected, bool installed, bool? nextChoice = null)
    {
        if (installed && (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"))
        { Console.Error.WriteLine("Default-state installed consent validation requires disposable GitHub-hosted Windows."); return 1; }
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Check(); completion.SetResult(0); }
            catch (Exception ex) { Console.Error.WriteLine(ex); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Native update consent UI Automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();

        void Check()
        {
            executable = Path.GetFullPath(executable); output = Path.GetFullPath(output);
            if (Directory.Exists(output)) throw new IOException("Consent UI evidence must use a new directory.");
            Directory.CreateDirectory(output);
            var state = installed ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensDocsStudio.Native") : Path.Combine(output, "state");
            var preferencesPath = Path.Combine(state, "preferences.json");
            if (installed)
            {
                using var registration = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(NativeUninstallKey);
                if (registration?.GetValue("InstallLocation")?.ToString()?.TrimEnd('\\') != Path.GetDirectoryName(executable))
                    throw new IOException("Installed consent validation requires this runner's exact registered native installation.");
            }
            // Opt-in UI observations must not contact the live update feed. Synthetic
            // transports separately prove due checks; here retain a future attempt time.
            var previous = File.Exists(preferencesPath) ? JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(preferencesPath)) : null;
            if (expected)
            {
                if (!installed || previous is null)
                    throw new IOException("An enabled UI fixture requires a previous durable opt-in in this disposable installation.");
                if (!previous.AutomaticUpdateChecks)
                {
                    var instruction = JsonSerializer.Deserialize<InstallerUpdateChoice>(ReadObservedText(Path.Combine(state, SessionStore.InstallerChoiceFileName)));
                    var fingerprint = new SessionStore(state).PreferencesFingerprintAsync().GetAwaiter().GetResult();
                    File.WriteAllText(Path.Combine(output, "installer-choice-validation.json"), JsonSerializer.Serialize(new
                    {
                        instruction?.SchemaVersion, instruction?.Action, InstallerFingerprint = instruction?.PreferencesSha256,
                        CurrentFingerprint = fingerprint, FingerprintMatches = string.Equals(instruction?.PreferencesSha256, fingerprint, StringComparison.OrdinalIgnoreCase),
                        previous.LastUpdateCheck, BeforeApplicationLaunch = true
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    if (instruction is not { SchemaVersion: 1, Action: "enable" }
                        || !string.Equals(instruction.PreferencesSha256, fingerprint, StringComparison.OrdinalIgnoreCase)
                        || previous.LastUpdateCheck is not { } attempt || attempt < DateTimeOffset.UtcNow.AddDays(1))
                        throw new IOException("The installer opt-in fixture requires unchanged preferences and an already seeded future attempt time.");
                }
                else if (previous.LastUpdateCheck is not { } attempt || attempt < DateTimeOffset.UtcNow.AddDays(1))
                {
                    previous = previous with { LastUpdateCheck = DateTimeOffset.UtcNow.AddDays(7) };
                    new SessionStore(state).WriteAsync("preferences.json", previous).GetAwaiter().GetResult();
                }
            }
            var previousAttempt = previous?.LastUpdateCheck;
            var source = Path.Combine(output, "consent.md"); const string text = "# Update consent\n\nLocal synthetic document.\n";
            File.WriteAllText(source, text);
            var sourceHash = SHA256.HashData(File.ReadAllBytes(source));
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            if (!installed) { start.ArgumentList.Add("--state-directory"); start.ArgumentList.Add(state); }
            // Reproduce an older updater helper's default-state restart route.
            if (installed && expected) { start.ArgumentList.Add("--state-directory"); start.ArgumentList.Add(state); }
            start.ArgumentList.Add(source);
            using var process = Process.Start(start) ?? throw new IOException("The consent fixture did not start.");
            using var app = Application.Attach(process.Id);
            using var automation = new UIA3Automation();
            Window? window = null;
            try
            {
                window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The consent fixture has no main window.");
                Wait(() => ReadEditorText(Find(window, "MarkdownEditor")) == text, "The consent fixture did not finish workspace initialisation.");
                Find(window, "HelpMenu").Patterns.ExpandCollapse.Pattern.Expand();
                Find(window, "UpdatesMenu").Patterns.Invoke.Pattern.Invoke();
                Window? updates = null;
                Wait(() => (updates = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process.Id).And(cf.ByName("Lens Docs Studio updates")))?.AsWindow()) is not null,
                    "The update preference window did not open.");
                var choice = Find(updates!, "AutomaticUpdateChecks");
                if (Checked(choice) != expected) throw new IOException("The actual executable did not apply the expected update preference before presenting its controls.");
                updates!.CaptureToFile(Path.Combine(output, "updates-before-timer.png"));
                var observation = Stopwatch.StartNew();
                Wait(() => observation.Elapsed >= TimeSpan.FromSeconds(11), "The first automatic timer observation did not complete.", 15);
                if (Checked(choice) != expected) throw new IOException("The update preference changed during the first timer interval.");
                updates.CaptureToFile(Path.Combine(output, "updates-after-timer.png"));
                if (nextChoice is { } changed && changed != expected)
                {
                    choice.Patterns.Toggle.Pattern.Toggle();
                    Wait(() => File.Exists(preferencesPath) && JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(preferencesPath))?.AutomaticUpdateChecks == changed,
                        "The user's changed update preference did not persist.");
                    updates.CaptureToFile(Path.Combine(output, "updates-user-choice.png"));
                }
                updates.Close(); window.Close();
                if (!process.WaitForExit(20000) || process.ExitCode != 0) throw new IOException("The consent fixture did not close normally.");
                var saved = JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(preferencesPath)) ?? throw new IOException("The consent preference was not persisted.");
                if (saved.AutomaticUpdateChecks != (nextChoice ?? expected) || saved.LastUpdateCheck != previousAttempt
                    || !SHA256.HashData(File.ReadAllBytes(source)).AsSpan().SequenceEqual(sourceHash))
                    throw new IOException("First-launch consent, attempt time or workspace source was not preserved.");
                File.WriteAllText(Path.Combine(output, "update-consent-ui.json"), JsonSerializer.Serialize(new
                {
                    Passed = true, Installed = installed, IsolatedStateOverride = !installed, ExpectedAutomaticChecks = expected,
                    DefaultStateOverride = installed && expected,
                    UserChangedChoice = nextChoice, SyntheticFutureAttempt = expected,
                    saved.AutomaticUpdateChecks, saved.LastUpdateCheck, TimerObservedSeconds = observation.Elapsed.TotalSeconds,
                    SourcePreserved = true, ProcessId = process.Id,
                    Limitation = "Actual executable/UI and persisted attempt observation; synthetic transports prove requests separately. Not a packet capture or clean-machine certification."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                try { window?.CaptureToFile(Path.Combine(output, "failure.png")); } catch { }
                File.WriteAllText(Path.Combine(output, "failure.json"), JsonSerializer.Serialize(new { Passed = false, Error = ex.ToString() }));
                throw;
            }
            finally
            {
                // Only this owned synthetic process can be closed; no desktop-wide input.
                if (!process.HasExited) { process.CloseMainWindow(); if (!process.WaitForExit(5000)) process.Kill(); }
            }
        }
    }
}
