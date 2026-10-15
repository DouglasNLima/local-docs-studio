using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;
using Microsoft.Win32.SafeHandles;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Called only by the hosted-runner-gated installer wizard check, after its
    // disposable installation has finished. No user installation is discovered.
    private static void CheckInstallerInUse(string installer, string destination, string source, string root,
        UIA3Automation automation, string startShortcut, string desktopShortcut)
    {
        var output = Path.Combine(root, "running-application"); Directory.CreateDirectory(output);
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false)));
        var original = ReadObservedText(source); var sourceBytes = File.ReadAllBytes(source);
        const string addition = "Unsaved installer draft: Café and こんにちは.";
        var draft = original + addition;
        var installationBefore = SnapshotInstallation();
        File.WriteAllText(Path.Combine(output, "installation-before.json"), installationBefore);
        var checks = new List<string>();
        Process? started = null; Process? launcher = null; Process? setupProcess = null;
        Application? app = null; Window? window = null; Window? warning = null;
        try
        {
            var appStart = new ProcessStartInfo(Path.Combine(destination, "LensDocsStudio.Native.exe")) { UseShellExecute = false };
            foreach (var argument in new[] { "--state-directory", state, Path.GetDirectoryName(source)!, source }) appStart.ArgumentList.Add(argument);
            started = Process.Start(appStart) ?? throw new IOException("The installed fixture could not start.");
            app = Application.Attach(started.Id);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The installed fixture window did not appear.");
            var editor = Find(window, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == original, "The installed fixture did not open its exact source.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(addition);
            Wait(() => ReadEditorText(editor) == draft && RetainedDraft(), "The installed fixture did not retain the unsaved draft and saved baseline.");
            AssertPreserved("before-setup");
            window.CaptureToFile(Path.Combine(output, "unsaved-before-setup.png"));
            checks.Add("The installed application holds an exact unsaved Unicode draft with an unchanged saved baseline");

            var setupStart = new ProcessStartInfo(installer) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
            foreach (var argument in new[] { "/SP-", "/NORESTART", "/LANG=english", "/LOG=" + Path.Combine(output, "blocked-setup.log") })
                setupStart.ArgumentList.Add(argument);
            launcher = Process.Start(setupStart) ?? throw new IOException("The second Setup process could not start.");
            Wait(() => (warning = automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .Where(element => IsInstallerDescendant(element.Properties.ProcessId.Value, launcher))
                .Select(element => element.AsWindow()).FirstOrDefault(candidate => WizardText(candidate).Contains("currently running", StringComparison.Ordinal)
                    && WizardText(candidate).Contains(WizardProduct, StringComparison.Ordinal))) is not null,
                "Setup did not block installation while the installed application was running.", 40);
            setupProcess = Process.GetProcessById(warning!.Properties.ProcessId.Value);
            var warningText = WizardText(warning);
            if (!warningText.Contains("close all instances", StringComparison.Ordinal) || !warningText.Contains("Cancel to exit", StringComparison.Ordinal))
                throw new IOException("The running-application warning did not explain how to preserve work and cancel.");
            AssertPreserved("blocked");
            warning.CaptureToFile(Path.Combine(output, "setup-blocked.png"));
            File.WriteAllText(Path.Combine(output, "setup-warning.txt"), warningText);
            checks.Add("Setup identifies the running application and offers cancellation without closing it or changing installed files");

            var cancel = warning.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .Single(element => !element.IsOffscreen && Label(element.Name) == "Cancel");
            FocusWindow(warning); cancel.Focus();
            Wait(() => cancel.Properties.HasKeyboardFocus.Value, "The running-application warning cannot focus Cancel.");
            Keyboard.Type(VirtualKeyShort.SPACE);
            Wait(() => setupProcess.HasExited && launcher.HasExited, "Cancelling the running-application warning did not exit Setup.");
            // This observation precedes any action in the editor or application shutdown.
            AssertPreserved("after-cancel");
            window.CaptureToFile(Path.Combine(output, "unsaved-after-cancel.png"));
            checks.Add("Cancelling Setup leaves the same application alive with its exact draft, recovery baseline, source bytes, files, shortcuts and registration");

            // Close only the owned fixture, explicitly declining a source save. Normal
            // shutdown must still retain its draft in the independent recovery store.
            FocusWindow(window); window.Close();
            Window? close = null;
            Wait(() => (close = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(started.Id)
                .And(cf.ByName("Unsaved document")))?.AsWindow()) is not null, "The fixture did not request a decision before closing its unsaved document.");
            var no = close!.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).Single(element => Label(element.Name) == "No");
            no.Focus(); Wait(() => no.Properties.HasKeyboardFocus.Value, "The unsaved-document dialogue cannot focus No.");
            Keyboard.Type(VirtualKeyShort.SPACE);
            Wait(() => started.HasExited, "The installed fixture did not close after the explicit decision.");
            if (started.ExitCode != 0 || !RetainedDraft() || !File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes)
                || SnapshotInstallation() != installationBefore)
                throw new IOException("Fixture shutdown changed its source, installed payload or retained draft.");
            checks.Add("Explicit fixture shutdown preserves the unsaved draft independently without writing its source");
            Write(new { passed = true, checks, sameApplicationSurvived = true, sourcePreserved = true, installedPayloadPreserved = true,
                draftRetained = true, installationAttemptCancelled = true, applicationProcessId = started.Id,
                setupExited = setupProcess.HasExited, installerExitCode = launcher.ExitCode,
                limitation = "Running-application cancellation on disposable hosted Windows; not power-loss or clean-machine certification." });
        }
        catch (Exception error)
        {
            NativeUiFailureDiagnostics.Capture(started, output);
            try { window?.CaptureToFile(Path.Combine(output, "application-failure.png")); } catch { }
            try { warning?.CaptureToFile(Path.Combine(output, "setup-failure.png")); } catch { }
            Write(new { passed = false, checks, error = error.ToString() }); throw;
        }
        finally
        {
            // Handles refer only to processes launched here, or a Setup child whose
            // parent chain was checked before any interaction with its window.
            if (setupProcess is { HasExited: false }) { setupProcess.Kill(entireProcessTree: true); setupProcess.WaitForExit(10000); }
            if (launcher is { HasExited: false }) { launcher.Kill(entireProcessTree: true); launcher.WaitForExit(10000); }
            if (started is { HasExited: false }) { started.Kill(entireProcessTree: true); started.WaitForExit(10000); }
            setupProcess?.Dispose(); launcher?.Dispose(); app?.Dispose(); started?.Dispose();
        }

        void AssertPreserved(string phase)
        {
            var installed = SnapshotInstallation();
            File.WriteAllText(Path.Combine(output, "installation-" + phase + ".json"), installed);
            if (started!.HasExited || ReadEditorText(Find(window!, "MarkdownEditor")) != draft || !RetainedDraft()
                || !File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes) || installed != installationBefore)
                throw new IOException("Setup changed the live draft, source, recovery baseline or installed state.");
        }
        bool RetainedDraft()
        {
            try
            {
                using var file = new FileStream(Path.Combine(state, "session.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var document = JsonSerializer.Deserialize<SessionState>(file)?.Documents.SingleOrDefault();
                return document?.Path == source && document.Text == draft && document.SavedText == original;
            }
            catch (IOException) { return false; }
        }
        string SnapshotInstallation()
        {
            var files = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Concat([startShortcut, desktopShortcut])
                .Order(StringComparer.Ordinal).ToDictionary(path => path, path =>
                {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    return Convert.ToHexString(SHA256.HashData(file));
                });
            return JsonSerializer.Serialize(new { files, directories = Directory.EnumerateDirectories(destination, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray(),
                native = RegistrySnapshot(NativeUninstallKey), legacy = RegistrySnapshot(LegacyUninstallKey), markdown = DefaultMarkdownAssociation(),
                document = RegistrySnapshot(@"Software\Classes\LensDocsStudio.Native.Document"),
                command = RegistrySnapshot(@"Software\Classes\LensDocsStudio.Native.Document\shell\open\command") });
        }
        void Write(object value) => File.WriteAllText(Path.Combine(output, "installer-in-use.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool IsInstallerDescendant(int processId, Process launcher)
    {
        if (launcher.HasExited) return false;
        if (processId == launcher.Id) return true;
        using var snapshot = CreateToolhelp32Snapshot(0x00000002, 0); // TH32CS_SNAPPROCESS, read-only.
        if (snapshot.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var entry = new InstallerProcessEntry { Size = (uint)Marshal.SizeOf<InstallerProcessEntry>() };
        if (!Process32FirstW(snapshot, ref entry)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var parents = new Dictionary<uint, uint>();
        do { parents[entry.ProcessId] = entry.ParentProcessId; } while (Process32NextW(snapshot, ref entry));
        if (Marshal.GetLastWin32Error() != 18) throw new Win32Exception(Marshal.GetLastWin32Error()); // ERROR_NO_MORE_FILES.
        var visited = new HashSet<uint>(); var current = (uint)processId;
        while (visited.Add(current) && parents.TryGetValue(current, out var parent))
        {
            if (parent == launcher.Id)
            {
                using var child = Process.GetProcessById(processId);
                return !child.HasExited && child.StartTime >= launcher.StartTime;
            }
            current = parent;
        }
        return false;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InstallerProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId, ThreadCount, ParentProcessId;
        public int BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExecutableName;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(SafeFileHandle snapshot, ref InstallerProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(SafeFileHandle snapshot, ref InstallerProcessEntry entry);
}
