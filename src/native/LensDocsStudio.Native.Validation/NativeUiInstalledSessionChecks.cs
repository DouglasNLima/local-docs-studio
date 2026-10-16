using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunInstalledSession(string executable, string state, string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
        { Console.Error.WriteLine("Installed session validation requires the disposable Windows GitHub-hosted runner."); return 1; }
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(CheckInstalledSession(executable, state, output)); }
            catch (Exception error) { Console.Error.WriteLine(error); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Installed session automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }

    private static int CheckInstalledSession(string executable, string state, string output)
    {
        executable = Path.GetFullPath(executable); state = Path.GetFullPath(state); output = Path.GetFullPath(output);
        var fixture = Directory.GetParent(state)!.FullName;
        using var priorRun = JsonDocument.Parse(ReadObservedText(Path.Combine(fixture, "native-ui.json")));
        if (!priorRun.RootElement.GetProperty("Passed").GetBoolean()
            || !string.Equals(priorRun.RootElement.GetProperty("FixtureRoot").GetString(), fixture, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Restore validation requires a completed, isolated native UI fixture.");
        var before = ReadSession();
        var preferences = ReadPreferences();
        if (before.SchemaVersion != 1 || before.Documents.Count == 0 || preferences.AutomaticUpdateChecks)
            throw new IOException("The preserved fixture must contain documents with automatic network checks disabled.");
        var activeIndex = before.Documents.ToList().FindIndex(document => document.Id == before.ActiveDocument);
        if (activeIndex < 0) throw new IOException("The preserved fixture has no active document.");
        var stateFiles = Snapshot(state);
        var sources = before.Documents.Where(document => document.Path is not null).Select(document => document.Path!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        Process? started = null; Application? app = null; Window? window = null;
        using var automation = new UIA3Automation();
        try
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            start.ArgumentList.Add("--state-directory"); start.ArgumentList.Add(state);
            started = Process.Start(start) ?? throw new IOException("The upgraded native application could not start.");
            app = Application.Attach(started.Id);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The preserved session window did not appear.");
            var editor = Find(window, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == before.Documents[activeIndex].Text, "The installed application did not restore the previous active document.");
            var tabs = Find(window, "DocumentTabs").FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
            if (tabs.Length != before.Documents.Count) throw new IOException("The installed application lost preserved document tabs.");
            for (var index = 0; index < tabs.Length; index++)
            {
                tabs[index].Patterns.SelectionItem.Pattern.Select();
                var expected = before.Documents[index];
                Wait(() => ReadEditorText(editor) == expected.Text, "The installed application changed recovered text for: " + expected.Id);
            }
            tabs[activeIndex].Patterns.SelectionItem.Pattern.Select();
            Wait(() => ReadEditorText(editor) == before.Documents[activeIndex].Text, "The original active tab was not restored after inspection.");
            CheckRestoredSettings(window, automation, app.ProcessId);
            window.CaptureToFile(Path.Combine(output, "restored-session.png"));
            window.Close(); Wait(() => started.HasExited, "The restored session did not close safely.");
            if (started.ExitCode != 0) throw new IOException("The restored session failed during shutdown: " + started.ExitCode);
            var after = ReadSession(); var afterPreferences = ReadPreferences();
            if (Canonical(before) != Canonical(after)
                || JsonSerializer.Serialize(preferences with { Window = null }) != JsonSerializer.Serialize(afterPreferences with { Window = null }))
                throw new IOException("Opening the preserved session changed document identity/text, workspace, settings or update consent.");
            // Normal shutdown can update caret/scroll/window positions and rotate these backups.
            // Every other retained state file and all workspace source bytes must remain exact.
            var mutable = new HashSet<string>(["session.json", "session.json.bak", "preferences.json", "preferences.json.bak"], StringComparer.OrdinalIgnoreCase);
            var remainingBefore = stateFiles.Where(entry => !mutable.Contains(entry.Key)).OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            var remainingAfter = Snapshot(state).Where(entry => !mutable.Contains(entry.Key)).OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            if (!remainingBefore.SequenceEqual(remainingAfter) || sources.Any(entry => Hash(entry.Key) != entry.Value))
                throw new IOException("Restoring the installed session changed a workspace source, library, snapshot or retained asset.");
            Write(new { passed = true, restoredDocuments = tabs.Length, retainedStateFiles = remainingBefore.Length,
                workspaceFiles = sources.Count, sameSession = true, priorFixture = fixture, historicalSchemaCertification = false,
                checks = new[] { "Prior active document and every preserved tab display exact text", "Saved appearance settings restore in native controls",
                    "Document identities, workspace, content, settings and update consent survive shutdown", "Library/snapshot/asset bytes and saved workspace sources remain unchanged" } });
            Console.WriteLine("Installed session restoration: " + tabs.Length + " preserved tabs passed."); return 0;
        }
        catch (Exception error)
        {
            NativeUiFailureDiagnostics.Capture(started, output);
            try { window?.CaptureToFile(Path.Combine(output, "native-ui-failure.png")); } catch { }
            Write(new { passed = false, error = error.ToString(), priorFixture = fixture }); throw;
        }
        finally
        {
            if (started is { HasExited: false }) started.Kill(entireProcessTree: true);
            app?.Dispose(); started?.Dispose();
        }

        SessionState ReadSession() => JsonSerializer.Deserialize<SessionState>(ReadObservedText(Path.Combine(state, "session.json")))
            ?? throw new IOException("The preserved session is empty.");
        UserPreferences ReadPreferences() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(Path.Combine(state, "preferences.json")))
            ?? throw new IOException("The preserved preferences are empty.");
        void Write(object value) => File.WriteAllText(Path.Combine(output, "restored-session.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        static string Canonical(SessionState session) => JsonSerializer.Serialize(session with
            { Documents = session.Documents.Select(document => document with { CaretOffset = 0, VerticalOffset = 0 }).ToArray() });
        static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
        static Dictionary<string, string> Snapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), Hash, StringComparer.OrdinalIgnoreCase);
    }
}
