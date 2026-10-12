using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunInterruptedSave(string executable, string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
        { Console.Error.WriteLine("Interrupted-save validation requires the disposable Windows GitHub-hosted runner."); return 1; }
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(CheckInterruptedSave(executable, output)); }
            catch (Exception error) { Console.Error.WriteLine(error); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Interrupted-save automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }

    private static int CheckInterruptedSave(string executable, string output)
    {
        executable = Path.GetFullPath(executable); output = Path.GetFullPath(output);
        var root = Path.Combine(output, "interrupted-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspace);
        var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false)));
        var source = Path.Combine(workspace, "Café.md");
        const string original = "# Interrupted save\r\n\r\nSaved Café baseline.\r\n";
        const string addition = "Latest unsaved Café and こんにちは.";
        const string draft = original + addition;
        var sourceBytes = DocumentSnapshot.Encode(original, true); File.WriteAllBytes(source, sourceBytes);
        var checks = new List<string>(); var terminatedProcessId = 0;
        Process? started = null; Application? app = null; Window? window = null;
        using var automation = new UIA3Automation();
        try
        {
            Launch(true);
            var editor = Find(window!, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == original, "The interrupted-save fixture did not open exactly.");
            FocusWindow(window!); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(addition);
            Wait(() => ReadEditorText(editor) == draft, "The real editor did not retain the exact unsaved draft.");

            Guid documentId;
            using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Find(window!, "SaveDocument").Patterns.Invoke.Pattern.Invoke();
                Window? warning = null;
                Wait(() => (warning = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(started!.Id)
                    .And(cf.ByClassName("#32770")).And(cf.ByName("Lens Docs Studio")))?.AsWindow()) is not null,
                    "The blocked save did not show its native failure message.");
                SessionState? retained = null;
                Wait(() => (retained = ReadSession())?.Documents.SingleOrDefault()?.Text == draft,
                    "The failed save did not retain the current draft on disk.");
                var saved = retained!.Documents.Single(); documentId = saved.Id;
                if (saved.SavedText != original || !saved.Utf8Bom || saved.Path != source
                    || !File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes)
                    || Directory.EnumerateFiles(Path.Combine(state, "write-recovery"), "*.pending.json").Any())
                    throw new IOException("The failed save changed the baseline, source bytes or pre-journal recovery state.");
                warning!.CaptureToFile(Path.Combine(root, "blocked-save.png"));
                checks.Add("The native save error retains exact draft, original baseline and unchanged source before journal creation");

                // Terminate only this fixture's process, while its failure dialogue remains open.
                // No Closing handler or normal shutdown persistence can contribute to recovery.
                terminatedProcessId = started!.Id; started.Kill(entireProcessTree: true);
                if (!started.WaitForExit(10000)) throw new IOException("The isolated application did not terminate.");
                checks.Add("The owned application process was terminated without a normal shutdown");
            }
            app!.Dispose(); app = null; started!.Dispose(); started = null; window = null;
            Launch(false);
            editor = Find(window!, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == draft, "A new application process did not recover the exact unsaved draft.");
            var recovered = ReadSession()?.Documents.Single();
            if (recovered?.Id != documentId || recovered.SavedText != original || !recovered.Utf8Bom
                || !File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes))
                throw new IOException("Process restart lost the document identity or original source baseline.");
            window!.CaptureToFile(Path.Combine(root, "recovered-draft.png"));
            checks.Add("A new application process restores the exact draft, document identity and saved baseline");

            Find(window, "SaveDocument").Patterns.Invoke.Pattern.Invoke();
            var expected = DocumentSnapshot.Encode(draft, true);
            Wait(() => ReadSession()?.Documents.Single().SavedText == draft, "Retry did not update the saved recovery baseline.");
            if (!File.ReadAllBytes(source).AsSpan().SequenceEqual(expected))
                throw new IOException("Retry did not save recovered text with the original BOM and CRLF.");
            window.Close(); Wait(() => started!.HasExited, "The recovered application did not close safely.");
            if (started!.ExitCode != 0 || ReadSession()?.Documents.Single().Text != draft
                || !File.ReadAllBytes(source).AsSpan().SequenceEqual(expected))
                throw new IOException("Successful recovery retry or shutdown changed source text.");
            checks.Add("Explicit retry and normal shutdown retain exact UTF-8 BOM and line endings");
            Write(new { passed = true, checks, terminatedProcessId, actualProcessTermination = true,
                fixtureRoot = root, limitation = "Process termination after a rejected save; not power-loss or removable-drive certification." });
            Console.WriteLine("Interrupted native save: " + checks.Count + " recovery outcomes passed."); return 0;
        }
        catch (Exception error)
        {
            NativeUiFailureDiagnostics.Capture(started, root);
            try { window?.CaptureToFile(Path.Combine(root, "native-ui-failure.png")); } catch { }
            Write(new { passed = false, checks, terminatedProcessId, error = error.ToString(), fixtureRoot = root }); throw;
        }
        finally
        {
            if (started is { HasExited: false }) started.Kill(entireProcessTree: true);
            app?.Dispose(); started?.Dispose();
        }

        void Launch(bool openSource)
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            start.ArgumentList.Add("--state-directory"); start.ArgumentList.Add(state);
            if (openSource) { start.ArgumentList.Add(workspace); start.ArgumentList.Add(source); }
            started = Process.Start(start) ?? throw new IOException("The isolated application could not start.");
            app = Application.Attach(started.Id);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The isolated native window did not appear.");
        }
        SessionState? ReadSession()
        {
            try
            {
                // Observing recovery must not deny the application's atomic replacement.
                using var file = new FileStream(Path.Combine(state, "session.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<SessionState>(file);
            }
            catch (IOException) { return null; }
        }
        void Write(object value) => File.WriteAllText(Path.Combine(root, "interrupted-save.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
