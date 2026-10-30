using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using LensDocsStudio.Core.State;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Microsoft.Win32;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private const string NativeUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}_is1";
    private const string LegacyUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{E5D86651-4B27-4C59-B7D4-2EA98F2F8D26}_is1";
    private const string WizardProduct = "Lens Docs Studio Native Preview";

    public static int RunInstallerWizard(string installer, string output, bool noFileAssociations = false)
    {
        // This command changes foreground focus and installs a disposable fixture.
        // Never use it on the developer's active desktop or an existing installation.
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
        { Console.Error.WriteLine("Installer wizard validation requires the disposable Windows GitHub-hosted runner."); return 1; }
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(CheckInstallerWizard(installer, output, noFileAssociations)); }
            catch (Exception ex) { Console.Error.WriteLine(ex); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Installer wizard automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }

    private static int CheckInstallerWizard(string installer, string output, bool noFileAssociations)
    {
        installer = Path.GetFullPath(installer); output = Path.GetFullPath(output);
        if (!File.Exists(installer)) throw new FileNotFoundException("The native installer is missing.", installer);
        using var identity = JsonDocument.Parse(ReadObservedText(Path.Combine(Path.GetDirectoryName(installer)!, "version.json")));
        if (identity.RootElement.GetProperty("product").GetString() != "LensDocsStudio.Native")
            throw new IOException("Wizard validation requires the isolated native product.");
        var version = identity.RootElement.GetProperty("version").GetString()!;
        var startShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), WizardProduct, WizardProduct + ".lnk");
        var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), WizardProduct + ".lnk");
        var originalDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Lens Docs Studio Native");
        if (RegistrySnapshot(NativeUninstallKey) is not null || RegistrySnapshot(@"Software\Classes\LensDocsStudio.Native.Document") is not null
            || File.Exists(startShortcut) || File.Exists(desktopShortcut) || Directory.Exists(originalDestination) || File.Exists(originalDestination))
            throw new IOException("An existing native installation or shortcut must be preserved; use a fresh runner.");
        if (noFileAssociations) AssertNoWizardAssociations();
        var legacyBefore = RegistrySnapshot(LegacyUninstallKey);
        var stateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensDocsStudio.Native");
        var stateBefore = PrivateStateSnapshot();
        var keepExisting = File.Exists(Path.Combine(stateRoot, "preferences.json")) || File.Exists(Path.Combine(stateRoot, "preferences.json.bak"));
        var markdownBefore = DefaultMarkdownAssociation();
        var root = Path.Combine(output, "wizard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var destination = Path.Combine(root, "reviewed installation");
        var workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspace);
        var source = Path.Combine(workspace, "preserved.md"); const string sourceText = "# Wizard preservation\n\nCafé remains portable.\n";
        File.WriteAllText(source, sourceText);
        var sourceBytes = File.ReadAllBytes(source);
        using var automation = new UIA3Automation();
        var checks = new List<string>(); Window? wizard = null; Process? launcher = null; Process? wizardProcess = null;
        var discoveredWindows = new List<object>();
        var installationStarted = false;
        Exception? wizardFailure = null;
        try
        {
            foreach (var phase in new[] { "cancel", "install", "opt-in-reinstall" })
            {
                var cancel = phase == "cancel";
                var optIn = phase == "opt-in-reinstall";
                if (optIn)
                {
                    var retained = PrivateStateSnapshot();
                    RemoveTestInstallation("before-opt-in");
                    if (PrivateStateSnapshot() != retained) throw new IOException("Uninstall changed private consent/session state.");
                    var store = new SessionStore(stateRoot);
                    var saved = store.InitialisePreferencesAsync().GetAwaiter().GetResult();
                    store.WriteAsync("preferences.json", saved with { LastUpdateCheck = DateTimeOffset.UtcNow.AddDays(7) }).GetAwaiter().GetResult();
                    keepExisting = true;
                }
                if (FindWizard() is not null) throw new IOException("A native installation wizard already exists; leave it untouched.");
                var start = new ProcessStartInfo(installer) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
                foreach (var argument in new[] { "/SP-", "/NORESTART", "/LANG=english", "/LOG=" + Path.Combine(root, phase + ".log") })
                    start.ArgumentList.Add(argument);
                launcher = Process.Start(start) ?? throw new IOException("The installer could not be started.");
                Wait(() => (wizard = FindWizard()) is not null, "The native installation wizard did not appear.", 40);
                wizardProcess = Process.GetProcessById(wizard!.Properties.ProcessId.Value);
                discoveredWindows.Add(new { Phase = phase, Method = "Win32 class and caption, then UIA from the matching handle",
                    ProcessId = wizardProcess.Id, Class = wizard.Properties.ClassName.Value, Title = wizard.Name });
                File.WriteAllText(Path.Combine(root, "wizard-discovery.json"), JsonSerializer.Serialize(discoveredWindows,
                    new JsonSerializerOptions { WriteIndented = true }));
                FocusWindow(wizard);
                Wait(() => WizardText(wizard).Contains(version, StringComparison.Ordinal)
                    && WizardText(wizard).Contains("Workspace documents", StringComparison.Ordinal), "Welcome did not expose the version and preservation guidance.");
                Capture(phase + "-welcome");
                Next("Next");
                Wait(() => WizardText(wizard).Contains("Privacy policy", StringComparison.Ordinal)
                    && WizardText(wizard).Contains("GitHub", StringComparison.Ordinal), "The embedded local privacy policy was not accessible.");
                Capture(phase + "-local-policy"); Next("Next");
                Wait(() => WizardText(wizard).Contains("Privacy and updates", StringComparison.Ordinal)
                    && WizardText(wizard).Contains("Uninstall retains", StringComparison.Ordinal), "The privacy/network disclosure was not visible.");
                var keep = Choice("Keep my saved update preference (off if none is valid)");
                var automatic = Choice("Automatically check for updates");
                if (Checked(keep) != keepExisting || Checked(automatic) || automatic.IsEnabled == keepExisting)
                    throw new IOException("The initial update choices do not preserve existing preferences or default fresh checks off.");
                if (Checked(keep)) { keep.Focus(); Keyboard.Type(VirtualKeyShort.SPACE); }
                Wait(() => Choice("Automatically check for updates").IsEnabled, "The explicit update choice could not be enabled.");
                automatic.Focus(); Wait(() => automatic.Properties.HasKeyboardFocus.Value, "The update option cannot receive keyboard focus.");
                Keyboard.Type(VirtualKeyShort.SPACE);
                Wait(() => Checked(Choice("Automatically check for updates")), "Keyboard opt-in was not retained.");
                if (!cancel && !optIn) { automatic.Focus(); Keyboard.Type(VirtualKeyShort.SPACE); Wait(() => !Checked(Choice("Automatically check for updates")), "Keyboard opt-out was not retained."); }
                Capture(phase + "-privacy-choice"); Next("Next");
                var destinationEdit = DestinationEdit();
                if (!string.Equals(Path.GetFullPath(destinationEdit.Text), originalDestination, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The wizard did not default to the isolated native per-user location.");
                destinationEdit.Text = destination;
                Wait(() => DestinationEdit().Text == destination, "The destination editor did not retain the reviewed path.");
                Capture(phase + "-destination"); Next("Next");
                foreach (var task in new[] { "Create a Desktop shortcut", "Add Native Preview to Open with for Markdown and Mermaid files" })
                {
                    var choice = Choice(task);
                    if (Checked(choice)) throw new IOException("An optional wizard task was selected without consent: " + task);
                    if (noFileAssociations && task.StartsWith("Add Native Preview", StringComparison.Ordinal)) continue;
                    choice.Focus(); Keyboard.Type(VirtualKeyShort.SPACE);
                    Wait(() => Checked(Choice(task)), "Keyboard selection did not enable the optional task: " + task);
                }
                Capture(phase + "-tasks"); Next("Next");
                Wait(() => Button("Install") is not null && WizardText(wizard).Contains(destination, StringComparison.OrdinalIgnoreCase)
                    && WizardText(wizard).Contains("Create a Desktop shortcut", StringComparison.Ordinal), "Ready to Install did not reflect the reviewed path and chosen tasks.");
                Capture(phase + "-ready");
                if (cancel)
                {
                    Next("Back");
                    if (!Checked(Choice("Create a Desktop shortcut")) || Checked(Choice("Add Native Preview to Open with for Markdown and Mermaid files")) == noFileAssociations)
                        throw new IOException("Back navigation discarded the selected tasks.");
                    Next("Back");
                    if (DestinationEdit().Text != destination) throw new IOException("Back navigation discarded the chosen destination.");
                    Next("Cancel");
                    Window? confirm = null;
                    Wait(() => (confirm = wizard.ModalWindows.FirstOrDefault()) is not null, "Cancel did not offer an exit confirmation.");
                    Capture("cancel-confirmation", confirm!);
                    var yes = confirm!.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).Single(element => Label(element.Name) == "Yes");
                    yes.Focus(); Wait(() => yes.Properties.HasKeyboardFocus.Value, "The exit confirmation cannot focus Yes.");
                    Capture("cancel-confirmed-choice", confirm);
                    // Enter can still activate the message box's default No button after
                    // programmatic focus. Space activates the explicitly focused Yes button.
                    Keyboard.Type(VirtualKeyShort.SPACE);
                    Wait(() => wizardProcess.HasExited, "The cancelled wizard did not exit.");
                    if (Directory.Exists(destination) || Directory.Exists(originalDestination) || RegistrySnapshot(NativeUninstallKey) is not null
                        || File.Exists(startShortcut) || File.Exists(desktopShortcut) || DefaultMarkdownAssociation() != markdownBefore || PrivateStateSnapshot() != stateBefore)
                        throw new IOException("Cancelling the wizard created installation files, shortcuts or registration.");
                    checks.Add("Keyboard navigation, reviewed path/tasks, Back and confirmed Cancel preserve the uninstalled state");
                    checks.Add("Cancelling an explicit update opt-in leaves all private state and pending installer choices byte-for-byte unchanged");
                }
                else
                {
                    installationStarted = true; Next("Install");
                    Wait(() => Button("Finish") is { IsEnabled: true }, "The interactive installation did not reach Finish.", 90);
                    var launch = Choice("Launch " + WizardProduct);
                    if (Checked(launch)) { launch.Focus(); Keyboard.Type(VirtualKeyShort.SPACE); }
                    Wait(() => !Checked(Choice("Launch " + WizardProduct)), "The launch choice could not be disabled.");
                    Capture("install-finished"); Next("Finish");
                    Wait(() => wizardProcess.HasExited, "Finish did not close the installation wizard.");
                    using (var intent = JsonDocument.Parse(ReadObservedText(Path.Combine(stateRoot, SessionStore.InstallerChoiceFileName))))
                        if (intent.RootElement.GetProperty("SchemaVersion").GetInt32() != 1 || intent.RootElement.GetProperty("Action").GetString() != (optIn ? "enable" : "disable"))
                            throw new IOException("The explicit wizard update choice was not transferred after successful installation.");
                    checks.Add("Local privacy policy and network disclosure are visible; keyboard opt-in and opt-out produce a bounded completed-install instruction");
                    var installedExe = Path.Combine(destination, "LensDocsStudio.Native.exe");
                    var installedDll = Path.Combine(destination, "LensDocsStudio.Native.dll");
                    foreach (var process in Process.GetProcessesByName("LensDocsStudio.Native"))
                    {
                        using (process)
                            if (!process.HasExited && string.Equals(process.MainModule?.FileName, installedExe, StringComparison.OrdinalIgnoreCase))
                            {
                                // This exact executable belongs to the disposable installation under test.
                                process.Kill(entireProcessTree: true); process.WaitForExit(10000);
                                throw new IOException("Finish launched the installed application despite the unchecked launch choice.");
                            }
                    }
                    using var installed = JsonDocument.Parse(ReadObservedText(Path.Combine(destination, "version.json")));
                    using var registration = Registry.CurrentUser.OpenSubKey(NativeUninstallKey);
                    using var association = Registry.CurrentUser.OpenSubKey(@"Software\Classes\LensDocsStudio.Native.Document\shell\open\command");
                    if (!File.Exists(installedExe) || installed.RootElement.GetProperty("version").GetString() != version
                        || installed.RootElement.GetProperty("sourceRevision").GetString() != identity.RootElement.GetProperty("sourceRevision").GetString()
                        || FileVersionInfo.GetVersionInfo(installedDll).ProductVersion?.Split('+')[0] != version
                        || registration?.GetValue("DisplayVersion")?.ToString() != version
                        || (noFileAssociations ? association is not null : association?.GetValue("")?.ToString()?.Contains(destination, StringComparison.OrdinalIgnoreCase) != true)
                        || !File.Exists(startShortcut) || !File.Exists(desktopShortcut))
                        throw new IOException("Interactive installation lost its version, files, chosen shortcuts or Open with registration.");
                    if (noFileAssociations) AssertNoWizardAssociations();
                    checks.Add(noFileAssociations
                        ? "Visible Install and Finish produce matching native files/version and selected shortcuts; optional associations absent"
                        : "Visible Install and Finish produce matching native files/version, selected shortcuts and Open with registration");
                    if (RunUpdateConsent(installedExe, Path.Combine(root, phase + "-first-launch-consent"), optIn, true, optIn ? false : null) != 0)
                        throw new IOException("The installed executable did not honour the wizard update choice through its first timer interval.");
                    checks.Add(optIn ? "Successful wizard opt-in survives reinstall; actual first launch sees it and a later in-app opt-out persists"
                        : "Actual fresh interactive first launch consumes the wizard opt-out before its timer and preserves it through shutdown");
                }
                wizard = null; wizardProcess.Dispose(); wizardProcess = null; launcher.Dispose(); launcher = null;
            }
            CheckInstallerInUse(installer, destination, source, root, automation, startShortcut, desktopShortcut);
            checks.Add("An installed application with an unsaved draft blocks Setup; cancelling preserves the live draft, source and installed payload");
            if (!File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes) || RegistrySnapshot(LegacyUninstallKey) != legacyBefore)
                throw new IOException("The wizard changed workspace source or the existing shell registration.");
            checks.Add("Workspace bytes and the legacy product registration remain unchanged");
        }
        catch (Exception error)
        {
            wizardFailure = error;
            if (wizard is not null) { try { Capture("failure"); } catch { } }
            File.WriteAllText(Path.Combine(root, "installer-wizard.json"), JsonSerializer.Serialize(new
            { passed = false, version, checks, error = error.ToString(), installationStarted }, new JsonSerializerOptions { WriteIndented = true }));
            throw;
        }
        finally
        {
            try
            {
                if (wizardProcess is { HasExited: false }) { wizardProcess.Kill(entireProcessTree: true); wizardProcess.WaitForExit(10000); }
                if (launcher is { HasExited: false }) { launcher.Kill(entireProcessTree: true); launcher.WaitForExit(10000); }
                wizardProcess?.Dispose(); launcher?.Dispose();
                // Only remove this test's exact installation; never infer an uninstaller from registry data.
                var uninstaller = Path.Combine(destination, "unins000.exe");
                if (installationStarted && File.Exists(uninstaller))
                {
                    var cleanup = new ProcessStartInfo(uninstaller) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                    foreach (var argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=" + Path.Combine(root, "cleanup.log") }) cleanup.ArgumentList.Add(argument);
                    using var process = Process.Start(cleanup) ?? throw new IOException("The test installation could not be removed.");
                    if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Test installer cleanup exceeded its deadline."); }
                    if (process.ExitCode != 0) throw new IOException("Test installer cleanup failed: " + process.ExitCode);
                }
            }
            catch (Exception cleanupFailure)
            {
                File.WriteAllText(Path.Combine(root, "installer-cleanup-failure.json"), JsonSerializer.Serialize(new
                { passed = false, primaryError = wizardFailure?.ToString(), cleanupError = cleanupFailure.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
                if (wizardFailure is not null)
                    throw new AggregateException("The installer check and its cleanup both failed.", wizardFailure, cleanupFailure);
                throw;
            }
        }
        if (RegistrySnapshot(NativeUninstallKey) is not null || RegistrySnapshot(@"Software\Classes\LensDocsStudio.Native.Document") is not null
            || File.Exists(startShortcut) || File.Exists(desktopShortcut) || DefaultMarkdownAssociation() != markdownBefore
            || File.Exists(Path.Combine(destination, "LensDocsStudio.Native.exe"))
            || Directory.Exists(originalDestination) || RegistrySnapshot(LegacyUninstallKey) != legacyBefore
            || !File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes))
            throw new IOException("Wizard cleanup did not preserve prior registration and source.");
        if (noFileAssociations) AssertNoWizardAssociations();
        checks.Add("Cleanup removes only the test installation and retains prior default associations and source");
        File.WriteAllText(Path.Combine(root, "installer-wizard.json"), JsonSerializer.Serialize(new
        { passed = true, version, checks, sourcePreserved = true, associationsExcluded = noFileAssociations, cleanMachineCertification = false,
            limitation = "GitHub-hosted Windows GUI and keyboard validation; physical screen-reader/DPI and SDK-free certification remain separate." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Native installer wizard: " + checks.Count + " checks passed."); return 0;

        Window? FindWizard()
        {
            // Desktop-wide UIA enumeration timed out before it could return the installer.
            // Resolve its exact class/caption through Win32, then query only that provider.
            var handle = NativeWindowDiscovery.FindTopLevelWindows("TWizardForm", WizardProduct).SingleOrDefault();
            return handle == 0 ? null : automation.FromHandle(handle).AsWindow();
        }
        AutomationElement? Button(string name) => wizard!.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            // Inno 6.7's modern buttons omit the arrows used by older wizard styles.
            .FirstOrDefault(element => !element.IsOffscreen && Label(element.Name).Trim('<', '>', ' ') == name);
        string PrivateStateSnapshot() => !Directory.Exists(stateRoot) ? "{}" : JsonSerializer.Serialize(Directory.GetFiles(stateRoot, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(stateRoot, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));
        void RemoveTestInstallation(string phase)
        {
            var cleanup = new ProcessStartInfo(Path.Combine(destination, "unins000.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=" + Path.Combine(root, phase + ".log") }) cleanup.ArgumentList.Add(argument);
            using var process = Process.Start(cleanup) ?? throw new IOException("The owned test installation could not be removed.");
            if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Owned test uninstall exceeded its deadline."); }
            if (process.ExitCode != 0 || RegistrySnapshot(NativeUninstallKey) is not null) throw new IOException("Owned test uninstall did not remove its registration.");
        }
        void Next(string name)
        {
            AutomationElement? button = null;
            Wait(() => (button = Button(name)) is { IsEnabled: true }, "The wizard action is missing or disabled: " + name);
            button!.Focus(); Wait(() => button.Properties.HasKeyboardFocus.Value, "The wizard action cannot receive keyboard focus: " + name);
            Keyboard.Type(VirtualKeyShort.RETURN);
        }
        TextBox DestinationEdit()
        {
            AutomationElement? edit = null;
            Wait(() => (edit = wizard!.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                .FirstOrDefault(element => !element.IsOffscreen && element.IsEnabled && element.Patterns.Value.IsSupported
                    && !element.Patterns.Value.Pattern.IsReadOnly.Value)) is not null, "The destination path is not accessible as an editable field.");
            return edit!.AsTextBox();
        }
        AutomationElement Choice(string name)
        {
            AutomationElement? choice = null;
            Wait(() => (choice = wizard!.FindAllDescendants().FirstOrDefault(element => !element.IsOffscreen && Label(element.Name) == name
                && (element.Patterns.Toggle.IsSupported || element.Patterns.LegacyIAccessible.IsSupported))) is not null,
                "The wizard choice is not accessible: " + name);
            return choice!;
        }
        void Capture(string name, Window? target = null)
        {
            target ??= wizard!; target.CaptureToFile(Path.Combine(root, name + ".png"));
            var elements = target.FindAllDescendants().Take(512).Select(element => new
            {
                element.Name, Type = element.ControlType.ToString(), Class = element.Properties.ClassName.ValueOrDefault,
                element.IsEnabled, element.IsOffscreen, Bounds = element.BoundingRectangle,
                Value = element.Patterns.Value.IsSupported ? element.Patterns.Value.Pattern.Value.Value : null,
                Role = element.Patterns.LegacyIAccessible.IsSupported ? element.Patterns.LegacyIAccessible.Pattern.Role.Value.ToString() : null,
                State = element.Patterns.LegacyIAccessible.IsSupported ? element.Patterns.LegacyIAccessible.Pattern.State.Value.ToString() : null
            });
            File.WriteAllText(Path.Combine(root, name + "-controls.json"), JsonSerializer.Serialize(elements, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void AssertNoWizardAssociations()
    {
        if (RegistrySnapshot(@"Software\Classes\LensDocsStudio.Native.Document") is not null)
            throw new IOException("The excluded native association registration exists.");
        foreach (var extension in new[] { ".md", ".markdown", ".mmd", ".mermaid" })
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + extension + @"\OpenWithProgids");
            if (key?.GetValueNames().Contains("LensDocsStudio.Native.Document", StringComparer.OrdinalIgnoreCase) == true)
                throw new IOException("An excluded Open with registration exists for " + extension);
        }
    }

    private static bool Checked(AutomationElement element) => element.Patterns.Toggle.IsSupported
        ? element.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On
        : element.Patterns.LegacyIAccessible.Pattern.State.Value.HasFlag(AccessibilityState.STATE_SYSTEM_CHECKED);
    private static string Label(string? value) => (value ?? "").Replace("&", "", StringComparison.Ordinal).Trim();
    private static string WizardText(Window wizard) => string.Join('\n', wizard.FindAllDescendants().Where(element => !element.IsOffscreen)
        .Select(element => element.Name + "\n" + (element.Patterns.Value.IsSupported ? element.Patterns.Value.Pattern.Value.Value : "")));
    private static string? RegistrySnapshot(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key is null ? null : JsonSerializer.Serialize(key.GetValueNames().Order(StringComparer.Ordinal)
            .ToDictionary(name => name, name => key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)));
    }
    private static string? DefaultMarkdownAssociation()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.md");
        return key?.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
    }
}
